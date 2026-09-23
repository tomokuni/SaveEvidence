using Aprillz.MewUI.Controls;
using Estable.CaptureSession;
using Estable.FadeMaskDragRect;
using Estable.MewUI.CropView;
using Estable.MewUI.FadeMaskDragRect;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Microsoft.Extensions.Logging;
using DrawingColor = System.Drawing.Color;

namespace Estable.MewUI.CaptureSession;

/// <summary>プレビュー画像の切り出し（Crop）セッション（MewUI 版）。</summary>
/// <remarks>
/// Avalonia 版の <c>CropSession</c>（<c>CaptureSessionControl.AvaloniaUI</c>）に相当する。プレビューの上へ領域選択の<br/>
/// オーバーレイを重ね、ドラッグで指定した領域で現在の画像を切り出す。<br/>
/// <b>1 セッション = 1 インスタンス</b>であり、生成・実行・破棄は Facade（<see cref="CaptureSessionControl"/>）が担う<br/>
/// （本クラスは公開しない。排他の受付は Facade がコアの <c>CaptureSessionSlot</c> で行う）。<br/>
/// <br/>
/// <b>ライフサイクル</b>: 生成時にオーバーレイを表示ツリーへ加えて表示し、確定（Enter / イベント）または<br/>
/// キャンセル（Escape）で表示ツリーから外れて終了する（終了は <c>onEnded</c> で Facade へ通知する）。<br/>
/// 確定時は <see cref="ICaptureTarget.ApplyCrop"/> を通すため、Undo 履歴への退避などのドメイン操作は<br/>
/// アプリ側（<see cref="ICaptureTarget"/> の実装）に委ねる。<br/>
/// <br/>
/// <b>座標変換</b>: プレビューはズーム・パンできるため、ローカル DIP ↔ 画像ピクセルの変換は<br/>
/// 内包する <see cref="CropViewControl"/>（ズームビューポート）へ委譲する（画像矩形・実効倍率・Fit / 中央寄せの<br/>
/// 原点補正を単一所有する）。ズーム・パンで表示が変わった時は<br/>
/// <see cref="CropViewControl.ViewportChanged"/> でポートを作り直す。<br/>
/// <br/>
/// <b>テーマ</b>: 市松模様の下地色はテーマに依存するため、Facade が <see cref="UpdateThemeColor"/> で更新する<br/>
/// （テーマ変更の購読はホストが行う。本クラスはテーマの通知元を知らない）。<br/>
/// <br/>
/// <b>外観</b>: 枠線・ハンドル・十字マーカー・座標ラベルは <see cref="CaptureSettings"/> に従う（Avalonia 版の<br/>
/// <c>CropSession</c> と同じマッピング）。白枠などの固定値は持たない（設定を変えると反映されなくなるため）。<br/>
/// <br/>
/// <b>ズーム・パン</b>: 切り出し中もホイール（Ctrl = ズーム / 通常 = パン）でプレビューを操作できる<br/>
/// （オーバーレイ上のホイールを <see cref="CropViewControl.PanByWheel"/> / <see cref="CropViewControl.ZoomByWheel"/> へ転送する）。
/// </remarks>
internal sealed class CropSession
{
    /// <summary>ロガー（ホストが注入する。null の場合は出力しない）。</summary>
    private readonly ILogger _log;

    /// <summary>対象のプレビューコントロール。</summary>
    private readonly CropViewControl _view;

    /// <summary>切り出しの適用先（アプリ側が実装する）。</summary>
    private readonly ICaptureTarget _target;

    /// <summary>設定（全メンバー non-null）。<see cref="ApplySettings"/> で実行中に更新する。</summary>
    private CaptureSettings _settings;

    /// <summary>選択オーバーレイ（暗転マスク + 選択矩形 + ルーペ）。</summary>
    private readonly FadeMaskDragRectControl _selector;

    /// <summary>オーバーレイを重ねる領域（終了時に取り外すため保持する）。</summary>
    private readonly Grid _host;

    /// <summary>セッションの状態（切り出し中かどうか・完了は 1 回だけ）。</summary>
    private readonly CaptureSessionState _state = new();

    /// <summary>市松模様の下地色（テーマのページ背景色）。<see cref="UpdateThemeColor"/> で更新する。</summary>
    private DrawingColor _themeBackground;

    /// <summary>長命のオブジェクト（ビュー・適用先）への購読を解除する処理（終了時に 1 回だけ実行する）。</summary>
    /// <remarks>
    /// ビューポートと適用先はアプリと同じ寿命を持つため、終了したセッションのハンドラを残すと<br>
    /// セッションの数だけ呼び出しが増え、終了したセッションを参照し続ける（リークになる）。
    /// </remarks>
    private readonly List<Action> _unsubscribe = [];

    /// <summary>選択矩形が有効かどうかを取得する。</summary>
    /// <value>選択矩形がある場合は true。</value>
    /// <remarks>選択状態の真のソースは選択コントロール（<c>HasSelection</c>）に集約する（Facade の操作分岐用）。</remarks>
    internal bool HasSelection => _selector.HasSelection;

    /// <summary>切り出しセッションを生成する（表示は <see cref="Start"/> が行う）。</summary>
    /// <param name="view">対象のプレビューコントロール。</param>
    /// <param name="target">切り出しの適用先（アプリ側の実装）。</param>
    /// <param name="settings">キャプチャ設定（null メンバーは既定値で埋める）。</param>
    /// <param name="host">オーバーレイを重ねる領域（プレビューと同じセル）。</param>
    /// <param name="themeBackground">市松模様の下地色（テーマのページ背景色）。</param>
    /// <param name="logger">ロガー（null の場合は出力しない）。</param>
    /// <remarks>
    /// 1. 選択オーバーレイを生成してイベントを配線する（確定・キャンセル・ビューポート変化・画像差し替え・ホイール）。<br/>
    /// 2. 表示とセッションの開始は <see cref="Start"/> が行う（Facade が排他の受付を試みてから呼ぶため）。
    /// </remarks>
    internal CropSession(
        CropViewControl view, ICaptureTarget target, CaptureSettings settings, Grid host,
        DrawingColor themeBackground, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(host);

        _view = view;
        _target = target;
        _settings = settings.ToNonNull();
        _host = host;
        _themeBackground = themeBackground;
        _log = logger.OrNullLogger();

        _selector = new FadeMaskDragRectControl
        {
            Mode = FadeMaskDragRectMode.Crop,
            IsVisible = false,
            Log = _log,
        };

        _selector.ConfirmRequested += (_, _) => Confirm();
        _selector.CancelRequested += (_, _) => Cancel();

        // ビューポートと適用先はアプリと同じ寿命を持つため、終了時に解除できるようハンドラを保持する
        EventHandler onViewportChanged = (_, _) => UpdatePort();
        _view.ViewportChanged += onViewportChanged;
        _unsubscribe.Add(() => _view.ViewportChanged -= onViewportChanged);

        EventHandler onImageChanged = (_, _) =>
        {
            _selector.SetLoupeSource(_target.CurrentImage);
            UpdatePort();

            // 画像の差し替えで座標系（元画像ピクセル）が変わるため、選択を破棄して残留を防ぐ。
            // ズーム・パン（ビューポートの変化）では座標系が変わらないため、選択は維持する。
            if (_state.IsRunning)
            {
                _selector.ClearSelection();
            }
        };
        _target.ImageChanged += onImageChanged;
        _unsubscribe.Add(() => _target.ImageChanged -= onImageChanged);

        // 選択オーバーレイ上のホイールをビューポート（ズーム・パン）へ転送する。
        // オーバーレイは切り出し中にプレビューを覆って入力を占有するため、ホイールはオーバーレイだけが受け取る。
        // 回転量から移動量への換算とズーム率の決定は、ズームビューポートが単一所有する。
        _selector.ScrollRequested += (_, request) =>
            _view.PanByWheel(request.Delta, request.Shift);
        _selector.ZoomRequested += (_, request) =>
            _view.ZoomByWheel(request.ZoomIn, ToViewportPosition(request.Position));
    }

    /// <summary>切り出しセッションの終了を通知する（Facade が排他の受付を解放する）。</summary>
    /// <remarks>確定・キャンセルの両方で 1 回だけ通知する（セッションの破棄前に届く）。</remarks>
    internal event Action? Ended;

    /// <summary>進行中の切り出しを現在の選択矩形で確定する（Enter キー相当）。</summary>
    /// <remarks>
    /// 切り出しの適用先（<see cref="ICaptureTarget.ApplyCrop"/>）へ委譲する（Undo 退避と画像差し替えはアプリ側の責務）。<br/>
    /// 実行中でない場合は何もしない（確定後に届いた要求を無視する）。
    /// </remarks>
    internal void Confirm()
    {
        if (_target.CurrentImage is not { } image)
        {
            // 切り出す対象が無い（画像が差し替えられた等）→ キャンセル扱い
            Cancel();
            return;
        }

        // 完了は 1 回だけ受け付ける（実行中でない場合も false になる。規則はコアが単一所有する）
        if (!_state.TryComplete())
        {
            return;
        }

        // 切り出し範囲の計算（閉区間化・画像内へのクランプ・端数の丸め）はコアが単一所有する
        var pixels = CropBoundsMath.ComputeCropBounds(
            _selector.SelectionRect, CaptureClipMath.ImageSize(image), _settings.MinSelectionSize!.Value);
        // 退化した選択（切り出せない矩形）はキャンセルとして記録する（規則はコアが単一所有する）
        _log.LogCropFinished(pixels);
        EndSession();

        // 切り出せない場合は画像を差し替えない（0 サイズの画像を作らない）
        if (!pixels.IsEmpty)
        {
            _target.ApplyCrop(pixels);
        }
    }

    /// <summary>進行中の切り出しをキャンセルする（Escape キー相当）。</summary>
    /// <remarks>選択を破棄してオーバーレイを取り外す（画像は差し替えない）。</remarks>
    internal void Cancel()
    {
        // 完了は 1 回だけ受け付ける（確定とキャンセルの競合で二重終了にしない。規則はコアが単一所有する）
        if (!_state.TryComplete())
        {
            return;
        }

        _log.LogCropCancelled();
        EndSession();
    }

    /// <summary>セッション実行中に最新の設定を再適用する（切り出し中の設定変更の即時反映）。</summary>
    /// <param name="newSettings">最新の設定（null メンバーは現在値を維持）。</param>
    /// <remarks>
    /// Facade（<c>AppearanceSettings</c>）の変更時に呼ばれる。外観・クリップ設定を再適用する。<br/>
    /// セッションの選択状態・ルーペの表示位置は維持される（座標変換ポートは再設定されるが、動的評価のため追従）。
    /// </remarks>
    internal void ApplySettings(CaptureSettings newSettings)
    {
        if (!_state.IsRunning)
        {
            return;
        }

        _settings = newSettings.ToNonNull();
        ApplyAppearance();
    }

    /// <summary>テーマの変更を外観へ反映する（市松模様の下地色）。</summary>
    /// <param name="themeBackground">テーマのページ背景色。</param>
    /// <remarks>
    /// テーマ変更の購読はホストが行う（本クラスはテーマの通知元を知らない）。<br/>
    /// 呼び出し後は選択コントロールが描き直され、ルーペの市松模様の下地が新しいテーマ色になる。
    /// </remarks>
    internal void UpdateThemeColor(DrawingColor themeBackground)
    {
        _themeBackground = themeBackground;
        ApplyAppearance();
        _selector.InvalidateOverlay();
    }

    /// <summary>切り出しを開始する（オーバーレイを表示し、入力を選択コントロールへ移す）。</summary>
    /// <param name="image">切り出す対象の元画像。</param>
    /// <remarks>
    /// 外観の反映（<see cref="ApplyAppearance"/>）と表示の準備を行い、選択コントロールへフォーカスを移す。<br/>
    /// Facade が排他の受付（コアの <c>CaptureSessionSlot</c>）を済ませてから呼ぶため、呼び出し時点で実行中にはならない。<br/>
    /// 開始状態は <see cref="CaptureSessionState"/> が単一所有する（確定・キャンセルの二重完了を防ぐ規則と同じ所有者に置く）。
    /// </remarks>
    internal void Start(EsBitmap image)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (!_state.TryBegin())
        {
            return;
        }

        // 開始ログのサイズは元画像からコアが取り出す（UI は整数へ丸めない）
        _log.LogCropStart(image);
        UpdatePort();
        _selector.SetLoupeSource(image);
        _selector.ClearSelection();
        ApplyAppearance();
        _host.Add(_selector);
        _selector.IsVisible = true;

        // 非表示の間に描画済みのキャンバスは再描画されないため、表示後に明示的に無効化する
        _selector.InvalidateOverlay();
        _selector.NotifyViewportChanged();

        // 矢印キーの微調整と Escape を受け取れるよう、選択コントロールへフォーカスを移す
        _selector.FocusMarquee();
    }

    /// <summary>セッションの後片付け（選択の破棄・ルーペの解除・オーバーレイの取り外し）を行う。</summary>
    /// <remarks>
    /// 終了の記録は行わない（確定時はコアの <c>LogCropFinished</c> が既に記録済みのため、キャンセルの記録を重ねない）。<br/>
    /// 状態を終了してから Facade へ通知する（排他の受付の解放を状態と矛盾させない）。
    /// </remarks>
    private void EndSession()
    {
        foreach (var unsubscribe in _unsubscribe)
        {
            unsubscribe();
        }

        _unsubscribe.Clear();
        _selector.ClearSelection();
        _selector.ClearLoupe();
        _selector.IsVisible = false;
        _host.Remove(_selector);
        _state.End();
        Ended?.Invoke();
    }

    /// <summary>表示中のズーム・パンに合わせて座標変換ポートを更新する。</summary>
    /// <remarks>
    /// ローカル DIP ⇔ 画像ピクセルの変換は、表示中の画像矩形・実効倍率・Fit / 中央寄せの原点補正を<br/>
    /// 内包する <see cref="CropViewControl"/>（ズームビューポート）へ委譲する（Avalonia 版の <c>CropSession</c> と同じ）。<br/>
    /// ズーム・パンで表示が変わるたびに呼び直す（ポートは都度生成する）。<br/>
    /// ドメイン座標（元画像ピクセル）はズーム・パンで不変のため、選択矩形は維持される（オーバーレイの描画位置のみが追従する）。
    /// </remarks>
    private void UpdatePort()
    {
        var image = _target.CurrentImage;

        _selector.SetCoordinateTransform(new CoordTransform(
            forward: _view.ViewportToContent,
            inverse: _view.ContentToViewport,
            bounds: () => image is null ? null : CaptureClipMath.ImageSize(image)));
    }

    /// <summary>設定を選択コントロールへ反映する。</summary>
    /// <remarks>
    /// 設定項目と外観の対応はコアのマッパー（<c>FadeMaskDragRectOptionsMapper.ToFadeMaskDragRectOptions</c>）が単一所有する<br/>
    /// （Avalonia 版の <c>CropSession</c> も同じ変換を使うため、UI ごとに見た目がずれない）。<br/>
    /// ルーペの市松模様の下地にはテーマの背景色を渡す（プレビュー画像を透かして市松色をそのまま重ねない）。
    /// </remarks>
    private void ApplyAppearance()
    {
        var s = _settings;

        // 設定を 1 値に変換して一括適用する（ラベル文言は描画時に評価するため別途設定する）
        _selector.Options = s.ToFadeMaskDragRectOptions(_themeBackground);

        // サイズラベルは画像ピクセル（ドメイン座標）で表示する（表示時点の選択を持ち出す）。
        // 書式と「未選択なら描かない」規則はコアが単一所有する
        // （Avalonia 版と同じ文言・同じ表示条件になる）。
        _selector.SizeLabelOverrideProvider = () => CropLabelTexts.ResolveSizeLabel(
            _selector.HasSelection, _selector.SelectionRect, _view.ZoomPercent);

        // 操作ラベル・情報ラベルの文言はキャプチャコア（CaptureLabelTexts）が単一所有する。
        // 情報ラベルは切り出しでは表示しない（GetInfoText が切り出しモードで null を返す）。
        _selector.OperationTextProvider = () => CaptureLabelTexts.GetOperationText(_selector.Mode);
        _selector.InfoTextProvider = () => CaptureLabelTexts.GetInfoText(_selector.Mode, null);
    }

    /// <summary>選択オーバーレイのローカル座標をビューポート（ズームビュー）のローカル座標へ変換する。</summary>
    /// <param name="overlayPosition">オーバーレイのローカル座標（DIP）。</param>
    /// <returns>ビューポートのローカル座標（DIP）。</returns>
    /// <remarks>
    /// 両者は通常同一領域を占める（重ね合わせ）が、レイアウトの差に依存しないようビジュアルツリーの変換に委譲する。
    /// </remarks>
    private EsPoint ToViewportPosition(EsPoint overlayPosition)
        => _view.TranslatePositionFrom(_selector, overlayPosition);
}
