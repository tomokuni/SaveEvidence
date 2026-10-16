using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Platform.Core.Native;
using EsUtil.Platform.Core.Native.Win;
using EsUtil.SaveEvidence.Parts.CapturePreview.AvaloniaUI.Crop;
using EsUtil.SaveEvidence.Parts.CapturePreview.AvaloniaUI.SelectionOverlay;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Capture;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Crop;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Logging;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Presentation;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.SelectionOverlay;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Session;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Setting;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Interop;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.AvaloniaUI.Session;

/// <summary>切出し（Crop）セッション（CropViewControl のライフサイクル管理と確定・キャンセル処理）。</summary>
/// <remarks>
/// 内部実装であり、公開 API は <see cref="CaptureSessionControl.StartCropAsync"/> を使用する。<br/>
/// <br/>
/// 状態は持たない（選択矩形・ルーペ表示状態・暗転マスクは <see cref="CropViewControl"/> が保持し、本クラスはライフサイクルのみ）。<br/>
/// - 配線（SelectionChanged → 暗転マスク同期 / LoupePositionMoved → ルーペ更新 / 確定・キャンセル）は<br/>
///   複合コントロール内で完結する（ホストの配線コードは不要）。<br/>
/// - 対象 = 元画像ピクセル（論理座標）。 座標変換は <see cref="CropViewControl"/> が内包する<br/>
///   ズーム・パン・Fit のビューポートへ委譲する（ズーム座標の単一所有）。<br/>
/// <br/>
/// プレビュー状態（ScrollOffset / ViewportOrigin / ZoomPercent）は保持しない。<br/>
/// オーバーレイ（Marquee / Loupe）とズームビューポートは <see cref="CropViewControl"/> 内で<br/>
/// 同一座標空間に配置されるため、座標変換は「ビューポートDIP ↔ 元画像ピクセル」の 1 段で完結する。
/// </remarks>
internal sealed class CropSession(
    CropViewControl cropView,
    Window hostWindow,
    CaptureSettings settings,
    ILogger? logger = null)
{
    /// <summary>内部ロガー（null の場合は出力なしに正規化）。</summary>
    private readonly ILogger _log = logger ?? NullLogger.Instance;

    /// <summary>カーソル上書き抑止（Crop 中の十字カーソル維持）。</summary>
    /// <remarks>WndProc の差し替えに失敗した場合（OS 操作の失敗）は共通のロガーへ記録される（null の場合は出力なし）。</remarks>
    private readonly ICursorOverrideGuard _cursorGuard = new WinCursorOverrideGuard();

    /// <summary>座標変換ポート（ビューポートDIP ↔ 元画像ピクセル）。</summary>
    /// <remarks>
    /// 境界（元画像の範囲）は現在の画像から都度評価するため、生成は 1 度で足りる<br/>
    /// （ズーム・パン・画像の差し替えのいずれにも追従する）。
    /// </remarks>
    private readonly ICoordTransform _coordPort = CreateCoordPort(cropView);

    /// <summary>設定（全メンバー non-null）。 実行中に <see cref="ApplySettings"/> で更新できる。</summary>
    private CaptureSettings _settings = settings.ToNonNull();

    /// <summary>セッションの状態（再入禁止・完了は 1 回だけ）。</summary>
    private readonly CaptureSessionState _state = new();

    /// <summary>セッション結果の完了通知（確定・キャンセルの最初の 1 件を採用する）。</summary>
    /// <remarks>
    /// 結果はコア型（<see cref="EsRectInt"/>）で返す（UI 型は境界より外へ出さない）。<br/>
    /// ホストは切り出しの適用先（アプリのコマンド）へそのまま渡せる。
    /// </remarks>
    private readonly CaptureCompletion<EsRectInt?> _completion = new();

    /// <summary>選択矩形が有効かどうか（複合コントロールの選択状態）。 ホスト（CaptureSessionControl）の操作分岐用。</summary>
    /// <remarks>選択状態の真のソースは選択矩形コントロール（<see cref="CropViewControl.HasSelection"/>）に集約する。</remarks>
    public bool HasSelection => cropView.HasSelection;

    /// <summary>ビューポートDIP ↔ 元画像ピクセルの座標変換ポートを生成する。</summary>
    /// <param name="cropView">変換の基準となる複合コントロール。</param>
    /// <returns>ToSource = ビューポートDIP → 元画像ピクセル、ToDip = 元画像ピクセル → ビューポートDIP、SourceArea = 現在の画像のサイズのポート。</returns>
    /// <remarks>
    /// <see cref="CropViewControl"/> の座標変換（<see cref="CropViewControl.ClientToSource"/> /
    /// <see cref="CropViewControl.SourceToClient"/>）へ委譲する。 これにより、ズーム・パン・Fit・中央寄せの<br/>
    /// 原点補正を全て内包ビューポートが単一所有し、ホストが変換を重複実装する必要がない（DRY 原則）。<br/>
    /// 境界は表示中の画像から都度評価する（切り出しの対象は <see cref="CropViewImage"/> が単一所有する）。
    /// </remarks>
    private static ICoordTransform CreateCoordPort(CropViewControl cropView)
        => new CoordTransform(
            toSource: dip => cropView.ClientToSource(dip.ToAvaloniaPoint()).ToEsPoint(),
            toDip: logical => cropView.SourceToClient(logical.ToAvaloniaPoint()).ToEsPoint(),
            sourceArea: () => cropView.CurrentImage() is { } image ? CropClipMath.ImageSize(image) : null);

    /// <summary>切出しセッションを実行する（確定待機 → 結果返却）。 UI スレッドから呼び出すこと。</summary>
    /// <param name="sourceImage">開始時点の元画像（開始ログのサイズに使う。 呼び出し側が表示中の画像から解決して渡す）。</param>
    /// <returns>確定時は元画像上の切り出し範囲（コア型）、キャンセル時は 0x0 の空矩形。</returns>
    /// <remarks>切り出しの境界（元画像の範囲）は開始時のサイズへ固定せず、現在の画像から都度取り出す（表示中の画像が単一所有する）。</remarks>
    public Task<EsRectInt?> RunAsync(EsReadOnlyBitmap sourceImage)
    {
        if (!_state.TryBegin())
        {
            return Task.FromResult<EsRectInt?>(null); // 再入禁止
        }

        // 外観・座標変換・ルーペソースを設定する（CropViewControl は既に配置済み。 追加・除去はしない）
        ConfigureControls();
        // 領域選択オーバーレイを可視化する（プレビュー表示時は非表示。 Crop セッション中のみ表示して暗転を出す）。
        cropView.SetSelectionOverlayVisible(true);
        // 開始ログのサイズは元画像からコアが取り出す（UI は整数へ丸めない）
        _log.LogCropStart(sourceImage);

        void OnConfirm(Rect logicalRect)
        {
            // 切り出しの対象は表示中の画像（差し替え後は差し替え後の画像の範囲で切り出す）。
            // 対象が無い場合は画像が差し替えられた（または破棄された）ため、キャンセルとして扱う
            if (cropView.CurrentImage() is not { } currentImage)
            {
                OnCancel();
                return;
            }

            if (!_state.TryComplete()) return; // 二重完了を防ぐ
            // UI の矩形型（Rect）は区間の流儀を持てないため、切り出しの手前で付ける
            // （切り出しの対象は常にドラッグ選択 = 範囲選択であり、規則はキャプチャ種別が単一所有する）
            var selection = logicalRect.ToEsRect().WithInterval(CaptureTypeTraits.ResolveInterval(CaptureType.AreaSelect));
            // 切り出し範囲の計算（画像内へのクランプ・端数の丸め・終点の加算）はコアが単一所有する
            var bounds = CropBoundsMath.ComputeCropBounds(
                selection, CropClipMath.ImageSize(currentImage), _settings.MinSelectionSize!.Value);
            // 退化した選択（切り出せない矩形）はキャンセルとして記録する（規則はコアが単一所有する）
            _log.LogCropFinished(bounds);
            Cleanup();
            // 退化した選択は空の矩形（0x0）のまま返す（キャンセルと同じ表現。 境界で UI 型へ変換する）
            _completion.TrySetResult(bounds);
        }

        void OnCancel()
        {
            if (!_state.TryComplete()) return; // 二重完了を防ぐ
            _log.LogCropCancelled();
            Cleanup();
            // キャンセルは 0x0 の空矩形で表す（公開契約。 null は「開始できなかった」の意味に限定する）
            _completion.TrySetResult(default(EsRectInt));
        }

        void OnConfirmEvent(object? sender, SelectionConfirmedEventArgs e) => OnConfirm(e.SelectionSourceRect);

        void OnCancelEvent(object? sender, EventArgs e) => OnCancel();

        void Cleanup()
        {
            hostWindow.Deactivated -= OnDeactivated;
            cropView.ConfirmRequested -= OnConfirmEvent;
            cropView.CancelRequested -= OnCancelEvent;
            _cursorGuard.Dispose();
            cropView.ClearLoupe();
            // 確定・キャンセル後は領域選択オーバーレイを非表示に戻す（プレビュー表示時は非表示のため）。
            // 選択状態のクリアとオーバーレイの非表示化を一括で行う（暗転マスクの残留を防ぐ）。
            cropView.SetSelectionOverlayVisible(false);
            hostWindow.Cursor = Cursor.Default;
            _state.End();
        }

        void OnDeactivated(object? s, EventArgs e) => OnCancel();

        // イベント配線（配線は複合コントロール内で完結。 ホストは確定・キャンセルのみ購読）
        cropView.ConfirmRequested += OnConfirmEvent;
        cropView.CancelRequested += OnCancelEvent;
        hostWindow.Deactivated += OnDeactivated;

        // カーソル上書き抑制（WM_SETCURSOR による十字カーソルの上書き防止）
        _cursorGuard.Attach(hostWindow);
        cropView.FocusMarquee();

        return _completion.Task;
    }

    /// <summary>進行中の切出しセッションをプログラム的にキャンセルする。</summary>
    /// <remarks>Marquee にキャンセル要求を委譲する（選択破棄 + CancelRequested 発火 → OnCancelEvent 経由で完了）。</remarks>
    public void Cancel()
    {
        if (!_state.IsRunning) return;
        cropView.RequestCancel();
    }

    /// <summary>進行中の切出しセッションを現在の選択矩形で確定する（Enter キー相当）。</summary>
    /// <remarks>Marquee に確定要求を委譲する（選択確定時のみ ConfirmRequested 発火 → OnConfirmEvent 経由で完了）。</remarks>
    public void Confirm()
    {
        if (!_state.IsRunning) return;
        cropView.RequestConfirm();
    }

    /// <summary>ビューポート（パン・ズーム・表示サイズ）の変化をオーバーレイへ通知する（ホストからの呼び出し）。</summary>
    /// <remarks>
    /// ビューポート実装（ScrollViewer / RenderTransform ベース等）に依存せず、ホスト（上位 AP）が<br/>
    /// ビューポート変化を検知した際に本メソッドで中継する（ScrollViewer 連携の Marquee の ViewportChanged と同等）。<br/>
    /// 暗転マスク・選択枠・ルーペの再描画・再計算を要求する。 座標変換ポートは動的評価のため再描画だけで追従する。
    /// </remarks>
    public void NotifyViewportChanged()
    {
        if (!_state.IsRunning) return;
        cropView.NotifyViewportChanged();
    }

    /// <summary>コントロール（外観・座標変換・ルーペソース）を設定する。</summary>
    /// <remarks>セッション開始時（<see cref="RunAsync"/>）と、実行中の設定変更（<see cref="ApplySettings"/>）で呼ばれる。</remarks>
    private void ConfigureControls()
    {
        var s = _settings;
        // 設定を複合コントロールの設定値（1 値）へ変換して一括適用する（変換はコアのマッパーが単一所有する）
        cropView.SelectionOverlayOptions = s.ToSelectionOverlayOptions();
        // 色は描画にだけ必要な設定のため、コアの設定ではなく描画の設定として与える
        // （プレビューの切り出しは背後の要素を透かすため、ルーペの下地は透明にする）
        cropView.SelectionOverlayMarqueeAppearance = CapturePaintMapper.ToMarqueeAppearance(s);
        cropView.SelectionOverlayLoupeAppearance = CapturePaintMapper.ToLoupeAppearance(s);
        // DPI スケーリング・ルーペ描画スケールは設定（Options）の対象外（環境値。 コードから動的設定）。
        // プレビュー内 Crop は対象 = 論理座標（元画像ピクセル）のため DIP と 1:1（基準値）にする。
        cropView.ClientToSourceScaling = DpiScaler.StandardScaling;
        cropView.LoupeClientToPixelScaling = s.LoupeClientToPixelScaling!.Value;
        // サイズラベルも描画時に最新のズーム率から生成する（ズーム率表示の追従）。
        // 書式と「未選択なら描かない」規則はコアが単一所有する。
        cropView.SizeLabelOverrideProvider = () => CropLabelTexts.ResolveSizeLabel(
            cropView.HasSelection,
            // 切り出しの対象は常にドラッグ選択（範囲選択）のため、区間の流儀をここで付ける
            cropView.SelectionSourceRect.ToEsRect().WithInterval(CaptureTypeTraits.ResolveInterval(CaptureType.AreaSelect)),
            cropView.ZoomPercent);
        // 操作ラベルの文言はホスト（切り出し文脈）が与える（汎用コアは文言を持たない）。
        // 情報ラベルは切り出しでは表示しない（CaptureLabelTexts が切り出しモードで null を返す）。
        cropView.OperationTextProvider = () => CaptureLabelTexts.GetOperationText(cropView.SelectionMode);
        cropView.InfoTextProvider = () => CaptureLabelTexts.GetInfoText(cropView.SelectionMode, null);
        // 座標変換ポート: 対象 = 論理座標（元画像ピクセル）。 ズーム・パン・Fit・中央寄せの原点補正は
        // 内包する ZoomViewControl が単一所有する（座標変換ポートはそれを委譲）。
        cropView.SetCoordTransform(_coordPort);
        // ルーペのソースは表示中の画像（切り出しの対象と同じ画像。 画像の差し替えに追従する）。
        // 終了時にルーペを解除するため、開始と設定変更のたびに表示中の画像へ結び直す。
        // 拡大元はコアの画像（表示中のアダプタが内包する画像）、表示用の射影は表示中の画像そのもの
        // （どちらも表示中の画像が単一所有するため、本クラスは複製も保持もしない）。
        cropView.SetLoupeSource(cropView.CurrentImage(), cropView.Source);
    }

    /// <summary>セッション実行中に最新の設定を再適用する（Crop 中の設定変更の即時反映）。</summary>
    /// <param name="newSettings">最新の設定（null メンバーは現在値を維持）。</param>
    /// <remarks>
    /// <see cref="CaptureSessionControl.AppearanceSettings"/> の変更時に CaptureSessionControl から呼ばれる。<br/>
    /// 外観・クリップ設定（<see cref="ConfigureControls"/>）を再適用し、オーバーレイを再描画する。<br/>
    /// セッションの選択状態・ルーペの表示位置は維持される（座標変換ポートは再設定されるが、動的評価のため追従）。
    /// </remarks>
    public void ApplySettings(CaptureSettings newSettings)
    {
        if (!_state.IsRunning) return;
        _settings = newSettings.ToNonNull();
        ConfigureControls();
    }
}



