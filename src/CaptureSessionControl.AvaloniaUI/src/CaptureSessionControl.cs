using Avalonia.Controls;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Native;
using EsUtil.Platform.Core.Native.Win;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.CaptureSession.AvaloniaUI.Sessions;
using EsUtil.SaveEvidence.CropView.AvaloniaUI;
using Microsoft.Extensions.Logging;

namespace EsUtil.SaveEvidence.CaptureSession.AvaloniaUI;

/// <summary>全コンポーネントを統括する Facade パターンの窓口として、外部アプリケーションに単一の API を提供する。</summary>
/// <remarks>
/// IDisposable を実装する。 Dispose 後は全ての公開操作が ObjectDisposedException を投げる。<br/>
/// StartCaptureAsync / StartCropAsync 実行中に同一インスタンスへ再呼び出しされた場合は無視する（既存のセッションを継続）。<br/>
/// <br/>
    /// <b>薄い統括者</b>: 状態は MarqueeControl / LoupeControl が保持し、<br/>
/// 本クラスは「組み立て・配線・ライフサイクル」のみを担う。<br/>
/// - プレビュー状態（CropPreviewState）は持たない（スクロールは Marquee イベント / TranslatePoint から取得）。<br/>
/// - 座標変換は EsUtil.Platform.Core.Coord の <see cref="ICoordTransform"/>（<see cref="CoordTransform"/> ファクトリ:
///   <c>ImageLayout</c> / <c>ScrollOffset</c> / <c>Compose</c> 等）に委譲する。<br/>
/// <br/>
/// ログ: コンストラクタで注入された <see cref="ILogger"/> へ内部ログを転送する。<br/>
/// ライブラリ自身はファイル出力等のログ基盤を所有しない（出力先・レベル制御はホストの責務）。<br/>
/// </remarks>
public sealed class CaptureSessionControl : IDisposable
{
    /// <summary>キャプチャが完了したときに発生するイベント（同期実行）。</summary>
    /// <remarks>
    /// <b>同期型イベント</b>のため、ハンドラの完了後にキャプチャセッションの後続処理（オーナー再表示）が実行される。<br/>
    /// ホストは本イベントの受信時に画像変更（キャプチャ結果の反映）を行うことで、<br/>
    /// 「APP 非表示 → キャプチャ → 画像変更（イベント受信） → APP 表示（再表示）」の順序を保証できる。<br/>
    /// 引数はキャプチャ結果（キャンセル時は 0x0 の空結果）。 UI スレッドで発行される。
    /// </remarks>
    public event Action<CaptureResult?>? CaptureCompleted;

    /// <summary>現在有効な設定（全メンバー non-null）。</summary>
    private CaptureSettings _currentSettings;

    /// <summary>破棄済みかどうか。</summary>
    private bool _isDisposed;

    /// <summary>進行中のセッション（撮影と切り出しで同時に 1 つまで）。</summary>
    /// <remarks>排他規則（片方が実行中なら他方も開始できない）はコアの <see cref="CaptureSessionSlot{TCapture, TCrop}"/> が単一所有する。</remarks>
    private readonly CaptureSessionSlot<SelectionCaptureSession, CropSession> _slot = new();

    /// <summary>内部ロガー（インスタンス単位。 複数インスタンスで混線しない）。</summary>
    private readonly ILogger _log;

    /// <summary>ネイティブウィンドウ操作サービス（キャプチャ除外・退避等）。</summary>
    private readonly IWindowService _windowService;

    /// <summary>インスタンスを生成する。</summary>
    /// <param name="initialSettings">初期設定。 null メンバーはデフォルト値が使用される。</param>
    /// <param name="logger">内部ログの転送先。 null の場合はログを出力しない。</param>
    /// <exception cref="ArgumentOutOfRangeException">設定値が許容範囲外の場合。</exception>
    public CaptureSessionControl(CaptureSettings? initialSettings = null, ILogger? logger = null)
    {
        initialSettings?.Validate();
        // 入力なし（null）は既定値で埋める（規則はコアが単一所有する）
        _currentSettings = CaptureSettingsMerge.Merge(initialSettings);
        _log = logger.OrNullLogger();
        _windowService = new WinWindowService(_log);
    }

    /// <summary>現在の外観・動作設定を取得する（getter は現在有効な値、すなわち全メンバー non-null の CaptureSettings を返す）。</summary>
    /// <remarks>
    /// 設定時は null メンバーのみ現在値を維持し、それ以外を反映する（部分更新）。<br/>
    /// 設定適用時に検証を行い、範囲外の値は ArgumentOutOfRangeException を投げる。<br/>
    /// マージ結果が現在の設定と等しい場合（同じ値の再設定）は更新をスキップし、無駄な再計算・再配布を防ぐ。<br/>
    /// 設定はセッション開始時に各ライブラリ（Marquee / Loupe）へ反映される。<br/>
    /// さらに、実行中の Crop セッションには <see cref="CropSession.ApplySettings"/> で即時反映される（Crop 中の設定変更対応）。
    /// </remarks>
    public CaptureSettings AppearanceSettings
    {
        get
        {
            ThrowIfDisposed();
            return _currentSettings;
        }
        set
        {
            ThrowIfDisposed();
            // 部分更新のマージ・検証・同値スキップはコアが単一所有する（UI は 45 項目の ?? 連鎖を持たない）
            if (!CaptureSettingsMerge.Apply(value, ref _currentSettings, _log)) return;

            // 実行中の Crop セッションへ最新設定を再適用する（Crop 中の設定変更の即時反映）。
            // キャプチャセッション（OverlayWindow）はモーダル全画面のため、実行中の設定変更は対象外。
            _slot.Crop?.ApplySettings(_currentSettings);
        }
    }

    /// <summary>選択キャプチャ（ScreenSelect / WindowSelect / AreaSelect）を開始する。 UI スレッドから呼び出すこと。</summary>
    /// <param name="type">キャプチャ種別。</param>
    /// <param name="owner">オーナーウィンドウ（スクリーン情報の取得元・キャプチャ除外設定）。</param>
    /// <returns>キャプチャ結果。 キャンセル時は 0x0 の空の結果。</returns>
    /// <remarks>
    /// 実行中に同一インスタンスへ再呼び出しされた場合は null を返す（既存セッションを継続）。<br/>
    /// DPI スケーリング係数はオーナーウィンドウのスクリーン情報から算出する。
    /// </remarks>
    public async Task<CaptureResult?> StartCaptureAsync(CaptureType type, Window owner)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(owner);

        // 画面の取得は UI 固有、代表スケーリングの解決（未取得は基準値・0 以下の正規化）はコアが単一所有する
        var scaling = MonitorScalingMath.ResolveScaling(owner.Screens.All.Select(screen => screen.Scaling));

        var session = new SelectionCaptureSession(
            _windowService,
            new WinCaptureService(_log),
            new WinElementFinder(_log),
            type, _currentSettings, scaling,
            // キャプチャ完了イベントを公開イベントへ中継する（同期実行 → ホストの画像変更後に再表示）
            onCaptured: result => CaptureCompleted?.Invoke(result),
            _log);

        if (!_slot.TryBeginCapture(session))
        {
            return null; // 再入禁止（他のセッションが実行中）
        }

        try
        {
            return await session.RunAsync(owner);
        }
        finally
        {
            _slot.EndCapture(session);
        }
    }

    /// <summary>切出し（Crop）を開始する。 UI スレッドから呼び出すこと。</summary>
    /// <param name="cropView">切り出しを行う高レベル複合コントロール（ズーム + 領域選択 + ルーペを内包）。 表示中の画像が切り出しの対象になる。</param>
    /// <param name="hostWindow">ホストウィンドウ。</param>
    /// <returns>確定時は元画像上の切り出し範囲（コア型）、キャンセル時は 0x0 の空矩形。 開始できなかった場合は null。</returns>
    /// <exception cref="ArgumentException"><paramref name="cropView"/> が表示している画像のサイズが 0 の場合。</exception>
    /// <remarks>
    /// 切り出しの対象画像は <paramref name="cropView"/> が表示している画像（<see cref="CropViewControl.Source"/>）から解決する<br/>
    /// （画像を別引数で受け取らない。 表示している画像と切り出す画像を二重所有しない）。<br/>
    /// 画像が設定されていない場合と表示アダプタ（<c>EsBitmapAvaloniaImage</c>）以外が設定されている場合は、<br/>
    /// 開始できなかったことを null の結果で表す（実行中の再呼び出しと同じ表現）。<br/>
    /// 切り出しの境界（元画像の範囲）は開始時に固定せず、確定時に<b>現在の画像</b>から都度解決する<br/>
    /// （実行中に画像が差し替わっても、差し替え後の画像の範囲で切り出せる）。<br/>
    /// プレビュー状態（ScrollOffset / ZoomPercent）は引数で受け取らない。<br/>
    /// ズーム・パン・Fit・座標変換は <paramref name="cropView"/> が内包する ZoomViewControl が単一所有する。<br/>
    /// 切出し中のホイール操作（Ctrl+ホイールのズーム・通常ホイールのパン）は <paramref name="cropView"/> が内部で完結する（上位の配線は不要）。<br/>
    /// 実行中に同一インスタンスへ再呼び出しされた場合は完了済みのタスク（結果 null）を返す。<br/>
    /// 結果の型はコア型（<see cref="EsRectInt"/>）とし、UI 型は境界より外へ出さない。
    /// </remarks>
    public Task<EsRectInt?> StartCropAsync(CropViewControl cropView, Window hostWindow)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(cropView);
        ArgumentNullException.ThrowIfNull(hostWindow);

        // 切り出しの対象は表示中の画像（CropViewControl.Source が単一所有する）
        if (cropView.CurrentImage() is not { } sourceImage)
        {
            return Task.FromResult<EsRectInt?>(null); // 対象が無い: 開始できなかったことを null で表す
        }

        // 「サイズが 0 の画像は切り出せない」という前提条件はコアが単一所有する
        CaptureClipMath.ValidateImage(sourceImage, nameof(cropView));

        var session = new CropSession(cropView, hostWindow, _currentSettings, _log);
        if (!_slot.TryBeginCrop(session))
        {
            // 再入禁止（他のセッションが実行中）: 何も起きなかったことを null の結果で表す
            return Task.FromResult<EsRectInt?>(null);
        }

        return AwaitCropAsync(session, sourceImage);
    }

    /// <summary>Crop セッションを待機し、完了後に保持を解除する。</summary>
    /// <param name="session">実行中の Crop セッション。</param>
    /// <param name="sourceImage">開始時点の元画像（開始ログのサイズに使う）。</param>
    /// <returns>切り出し結果（キャンセル時は 0x0）。</returns>
    private async Task<EsRectInt?> AwaitCropAsync(CropSession session, EsBitmap sourceImage)
    {
        try
        {
            return await session.RunAsync(sourceImage);
        }
        finally
        {
            _slot.EndCrop(session);
        }
    }

    /// <summary>切り出し中かどうかを取得する。</summary>
    /// <value>切り出しセッションが進行中の場合は true。</value>
    /// <remarks>
    /// 「実行中か」の真のソースは排他スロット（コアが単一所有する）である。<br/>
    /// ホストが自前のフラグ（開始したタスクの有無等）で判定すると、スロットの状態と二重所有になる。
    /// </remarks>
    public bool IsCropping => _slot.HasCrop;

    /// <summary>切り出し操作（開始・確定・キャンセル）のうち、いま行うべき操作を取得する。</summary>
    /// <returns>実行すべき切り出し操作。</returns>
    /// <remarks>
    /// どの操作を行うかの判断はコア（<see cref="CropSessionActions.Resolve"/>）が単一所有する<br/>
    /// （未開始なら開始 / 選択済みなら確定 / 未選択ならキャンセル）。<br/>
    /// ホストは戻り値を効果へ写すだけにする（UI ごとに条件を書くと片方だけ変わる）。
    /// </remarks>
    public CropSessionAction ResolveCropAction()
    {
        ThrowIfDisposed();
        return CropSessionActions.Resolve(_slot.HasCrop, _slot.HasCropSelection(static crop => crop.HasSelection));
    }

    /// <summary>進行中の操作（StartCaptureAsync / StartCropAsync）をキャンセルする。</summary>
    /// <remarks>
    /// - Crop 実行中: セッションをキャンセルし、0x0 の空結果で完了する。<br/>
    /// - Capture 実行中: オーバーレイウィンドウを閉じてキャンセルする（Deactivated 経由でキャンセル処理が走る）。<br/>
    /// - 操作なし: 何もしない。
    /// </remarks>
    public void Cancel()
    {
        ThrowIfDisposed();
        _slot.CancelAll(static s => s.Cancel(), static s => s.Cancel());
    }

    /// <summary>進行中の切出し（Crop）を現在の選択矩形で確定する（Enter キー相当）。</summary>
    /// <remarks>
    /// Crop 実行中のみ有効。 選択確定時のみ Marquee の <see cref="MarqueeControl.RequestConfirm"/> に委譲する。<br/>
    /// Capture 実行中 / 操作なしでは何もしない（Capture の確定はユーザー操作で完結する）。
    /// </remarks>
    public void Confirm()
    {
        ThrowIfDisposed();
        _slot.Crop?.Confirm();
    }

    /// <summary>実行中の Crop セッションへビューポート（パン・ズーム・表示サイズ）の変化を通知する。</summary>
    /// <remarks>
    /// ビューポート実装（ScrollViewer / RenderTransform ベースのパン・ズーム等）に依存せず、<br/>
    /// ホスト（上位 AP）がプレビューのビューポート変化を検知した際に呼び出す。<br/>
    /// Crop 実行中のみ有効（未実行時は何もしない）。 暗転マスク・選択枠・ルーペがビューポート変化に追従する。
    /// </remarks>
    public void NotifyCropViewportChanged()
    {
        ThrowIfDisposed();
        _slot.Crop?.NotifyViewportChanged();
    }

    /// <summary>リソースを解放する。</summary>
    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _slot.CancelAll(static s => s.Cancel(), static s => s.Cancel());
    }

    /// <summary>破棄済みなら例外を投げる。</summary>
    private void ThrowIfDisposed()
    {
        if (_isDisposed) throw new ObjectDisposedException(nameof(CaptureSessionControl));
    }
}
