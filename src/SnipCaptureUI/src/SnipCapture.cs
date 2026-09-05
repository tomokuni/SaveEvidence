using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Estable.AvaloniaUI.Platform;
using Estable.AvaloniaUI.Platform.Imaging;
using Estable.AvaloniaUI.ZoomView;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Estable.AvaloniaUI.SnipCaptureUI;

/// <summary>全コンポーネントを統括する Facade パターンの窓口として、外部アプリケーションに単一の API を提供する。</summary>
/// <remarks>
/// IDisposable を実装する。Dispose 後は全ての公開操作が ObjectDisposedException を投げる。<br/>
/// StartCaptureAsync / StartCropAsync 実行中に同一インスタンスへ再呼び出しされた場合は無視する（既存のセッションを継続）。<br/>
/// <br/>
/// <b>薄い統括者</b>: 状態は DragRectControl / FadeMaskControl / LoupeControl / PlatformFactory が保持し、<br/>
/// 本クラスは「組み立て・配線・ライフサイクル」のみを担う。<br/>
/// - プレビュー状態（CropPreviewState）は持たない（スクロールは DragRect イベント / TranslatePoint から取得）。<br/>
/// - 座標変換は PlatformFactory の <see cref="ICoordinatePort"/>（<see cref="CoordinatePort"/> ファクトリ:
///   <c>ImageLayout</c> / <c>ScrollOffset</c> / <c>Compose</c> 等）に委譲する。<br/>
/// <br/>
/// ログ: コンストラクタで注入された <see cref="ILogger"/> へ内部ログを転送する。<br/>
/// ライブラリ自身はファイル出力等のログ基盤を所有しない（出力先・レベル制御はホストの責務）。<br/>
/// </remarks>
public sealed class SnipCapture : IDisposable
{
    /// <summary>キャプチャが完了したときに発生するイベント（同期実行）。</summary>
    /// <remarks>
    /// <b>同期型イベント</b>のため、ハンドラの完了後にキャプチャセッションの後続処理（オーナー再表示）が実行される。<br/>
    /// ホストは本イベントの受信時に画像変更（キャプチャ結果の反映）を行うことで、<br/>
    /// 「APP 非表示 → キャプチャ → 画像変更（イベント受信） → APP 表示（再表示）」の順序を保証できる。<br/>
    /// 引数はキャプチャ結果（キャンセル時は 0x0 の空結果）。UI スレッドで発行される。
    /// </remarks>
    public event Action<CaptureResult?>? CaptureCompleted;

    /// <summary>現在有効な設定（全メンバー non-null）。</summary>
    private CaptureSettings _currentSettings;

    /// <summary>破棄済みかどうか。</summary>
    private bool _isDisposed;

    /// <summary>進行中のキャプチャセッション（Cancel 用）。</summary>
    private CaptureSession? _captureSession;

    /// <summary>進行中の Crop セッション（Cancel 用）。</summary>
    private CropSession? _cropSession;

    /// <summary>内部ロガー（インスタンス単位。複数インスタンスで混線しない）。</summary>
    private readonly ILogger _log;

    /// <summary>ネイティブウィンドウ操作サービス（キャプチャ除外・退避等）。</summary>
    private readonly IWindowService _windowService;

    /// <summary>インスタンスを生成する。</summary>
    /// <param name="initialSettings">初期設定。null メンバーはデフォルト値が使用される。</param>
    /// <param name="logger">内部ログの転送先。null の場合はログを出力しない。</param>
    /// <exception cref="ArgumentOutOfRangeException">設定値が許容範囲外の場合。</exception>
    public SnipCapture(CaptureSettings? initialSettings = null, ILogger? logger = null)
    {
        initialSettings?.Validate();
        _currentSettings = MergeWithDefaults(initialSettings);
        _log = logger ?? NullLogger.Instance;
        _windowService = PlatformFactory.CreateWindowService(_log);
    }

    /// <summary>現在の外観・動作設定を取得する（getter は現在有効な値、すなわち全メンバー non-null の CaptureSettings を返す）。</summary>
    /// <remarks>
    /// 設定時は null メンバーのみ現在値を維持し、それ以外を反映する（部分更新）。<br/>
    /// 設定適用時に検証を行い、範囲外の値は ArgumentOutOfRangeException を投げる。<br/>
    /// マージ結果が現在の設定と等しい場合（同じ値の再設定）は更新をスキップし、無駄な再計算・再配布を防ぐ。<br/>
    /// 設定はセッション開始時に各ライブラリ（DragRect / FadeMask / Loupe）へ反映される。<br/>
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
            value.Validate();
            var merged = MergeWithDefaults(value, _currentSettings);
            // 同じ値が設定された場合は更新しない（record の等値比較。部分更新の入力が全て現在値と一致する場合）
            if (merged == _currentSettings) return;
            _currentSettings = merged;

            // 実行中の Crop セッションへ最新設定を再適用する（Crop 中の設定変更の即時反映）。
            // キャプチャセッション（OverlayWindow）はモーダル全画面のため、実行中の設定変更は対象外。
            _cropSession?.ApplySettings(merged);
        }
    }

    /// <summary>選択キャプチャ（ScreenSelect / WindowSelect / AreaSelect）を開始する。UI スレッドから呼び出すこと。</summary>
    /// <param name="type">キャプチャ種別。</param>
    /// <param name="owner">オーナーウィンドウ（スクリーン情報の取得元・キャプチャ除外設定）。</param>
    /// <returns>キャプチャ結果。キャンセル時は 0x0 の空の結果。</returns>
    /// <remarks>
    /// 実行中に同一インスタンスへ再呼び出しされた場合は null を返す（既存セッションを継続）。<br/>
    /// DPI スケーリング係数はオーナーウィンドウのスクリーン情報から算出する。
    /// </remarks>
    public async Task<CaptureResult?> StartCaptureAsync(CaptureType type, Window owner)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(owner);
        if (_captureSession is not null) return null; // 再入禁止

        var screens = owner.Screens.All;
        double scaling = screens.Count > 0 && screens[0].Scaling > 0 ? screens[0].Scaling : 1.0;

        var session = new CaptureSession(
            _windowService,
            PlatformFactory.CreateCaptureService(_log),
            PlatformFactory.CreateElementFinder(),
            type, _currentSettings, scaling,
            // キャプチャ完了イベントを公開イベントへ中継する（同期実行 → ホストの画像変更後に再表示）
            onCaptured: result => CaptureCompleted?.Invoke(result),
            _log);
        _captureSession = session;
        try
        {
            return await session.RunAsync(owner);
        }
        finally
        {
            _captureSession = null;
        }
    }

    /// <summary>切出し（Crop）を開始する。UI スレッドから呼び出すこと。</summary>
    /// <param name="previewControl">プレビュー画像を表示するコントロール。</param>
    /// <param name="hostWindow">ホストウィンドウ。</param>
    /// <param name="sourceImage">元画像（100%キャプチャ画像）。切り出しの論理座標系の基準とルーペ用のソース。</param>
    /// <param name="zoomRequestHandler">Ctrl+ホイールによるズーム要求ハンドラ（true = ズームイン / false = ズームアウト）。null の場合はズーム操作なし。</param>
    /// <returns>確定時は元画像上の切り出し範囲、キャンセル時は 0x0 の空 PixelRect。</returns>
    /// <remarks>
    /// プレビュー状態（ScrollOffset / ZoomPercent）は引数で受け取らない。<br/>
    /// スクロールは DragRect のイベント / TranslatePoint から取得し、ズームはプレビューの表示状態から導出する。<br/>
    /// Ctrl+ホイールによるズーム要求は <paramref name="zoomRequestHandler"/> で上位 AP に通知する（ズームの実処理は上位が実施）。<br/>
    /// 実行中に同一インスタンスへ再呼び出しされた場合は null を返す。
    /// </remarks>
    public Task<PixelRect?> StartCropAsync(
        ZoomViewControl previewControl, Window hostWindow, UnifiedBitmap sourceImage,
        Action<bool>? zoomRequestHandler = null)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(previewControl);
        ArgumentNullException.ThrowIfNull(hostWindow);
        ArgumentNullException.ThrowIfNull(sourceImage);
        if (_captureSession is not null || _cropSession is not null) return Task.FromResult<PixelRect?>(null); // 再入禁止

        var sourceImageSize = new Size(sourceImage.Size.Width, sourceImage.Size.Height);
        if (sourceImageSize.Width <= 0 || sourceImageSize.Height <= 0)
            throw new ArgumentException("sourceImage のサイズが 0 です。", nameof(sourceImage));

        var session = new CropSession(previewControl, hostWindow, sourceImage, _currentSettings, _log, zoomRequestHandler);
        _cropSession = session;
        return AwaitCropAsync(session);
    }

    /// <summary>Crop セッションを待機し、完了後に保持を解除する。</summary>
    /// <param name="session">実行中の Crop セッション。</param>
    /// <returns>切り出し結果（キャンセル時は 0x0）。</returns>
    private async Task<PixelRect?> AwaitCropAsync(CropSession session)
    {
        try
        {
            return await session.RunAsync();
        }
        finally
        {
            if (ReferenceEquals(_cropSession, session)) _cropSession = null;
        }
    }

    /// <summary>実行中の Crop セッションに選択矩形があるかどうか。Crop 未実行中は false。</summary>
    /// <remarks>ホスト（上位 AP）が「確定 / キャンセル」の操作分岐に使用する（矩形あり = 確定 / なし = キャンセル）。</remarks>
    public bool HasSelection => _cropSession?.HasSelection ?? false;

    /// <summary>進行中の操作（StartCaptureAsync / StartCropAsync）をキャンセルする。</summary>
    /// <remarks>
    /// - Crop 実行中: セッションをキャンセルし、0x0 の空結果で完了する。<br/>
    /// - Capture 実行中: オーバーレイウィンドウを閉じてキャンセルする（Deactivated 経由でキャンセル処理が走る）。<br/>
    /// - 操作なし: 何もしない。
    /// </remarks>
    public void Cancel()
    {
        ThrowIfDisposed();
        _captureSession?.Cancel();
        _cropSession?.Cancel();
    }

    /// <summary>進行中の切出し（Crop）を現在の選択矩形で確定する（Enter キー相当）。</summary>
    /// <remarks>
    /// Crop 実行中のみ有効。選択確定時のみ DragRect の <see cref="DragRectControl.RequestConfirm"/> に委譲する。<br/>
    /// Capture 実行中 / 操作なしでは何もしない（Capture の確定はユーザー操作で完結する）。
    /// </remarks>
    public void Confirm()
    {
        ThrowIfDisposed();
        _cropSession?.Confirm();
    }

    /// <summary>実行中の Crop セッションへビューポート（パン・ズーム・表示サイズ）の変化を通知する。</summary>
    /// <remarks>
    /// ビューポート実装（ScrollViewer / RenderTransform ベースのパン・ズーム等）に依存せず、<br/>
    /// ホスト（上位 AP）がプレビューのビューポート変化を検知した際に呼び出す。<br/>
    /// Crop 実行中のみ有効（未実行時は何もしない）。暗転マスク・選択枠・ルーペがビューポート変化に追従する。
    /// </remarks>
    public void NotifyCropViewportChanged()
    {
        ThrowIfDisposed();
        _cropSession?.NotifyViewportChanged();
    }

    /// <summary>リソースを解放する。</summary>
    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _captureSession?.Cancel();
        _cropSession?.Cancel();
    }

    /// <summary>破棄済みなら例外を投げる。</summary>
    private void ThrowIfDisposed()
    {
        if (_isDisposed) throw new ObjectDisposedException(nameof(SnipCapture));
    }

    /// <summary>null のメンバーをデフォルト値で埋める（部分更新マージ）。</summary>
    /// <param name="input">入力設定。null メンバーは <paramref name="current"/> の値またはデフォルト値が使用される。</param>
    /// <param name="current">現在の設定（部分更新の維持元）。null の場合はデフォルト値のみ。</param>
    /// <returns>全メンバー non-null の設定。</returns>
    private static CaptureSettings MergeWithDefaults(CaptureSettings? input, CaptureSettings? current = null)
    {
        var def = new CaptureSettings().ToNonNull();
        var cur = current ?? def;
        var i = input ?? new CaptureSettings();
        return new CaptureSettings(
            HideOwnerWindow: i.HideOwnerWindow ?? cur.HideOwnerWindow,
            LoupeVisible: i.LoupeVisible ?? cur.LoupeVisible,
            HideBeforeCaptureDelay: i.HideBeforeCaptureDelay ?? cur.HideBeforeCaptureDelay,
            MinSelectionSize: i.MinSelectionSize ?? cur.MinSelectionSize,
            BorderColor: i.BorderColor ?? cur.BorderColor,
            BorderThickness: i.BorderThickness ?? cur.BorderThickness,
            OverlayColor: i.OverlayColor ?? cur.OverlayColor,
            HandleColor: i.HandleColor ?? cur.HandleColor,
            HandleSize: i.HandleSize ?? cur.HandleSize,
            HandleHitRadius: i.HandleHitRadius ?? cur.HandleHitRadius,
            ClampToDomainBounds: i.ClampToDomainBounds ?? cur.ClampToDomainBounds,
            MarkerColor: i.MarkerColor ?? cur.MarkerColor,
            MarkerLength: i.MarkerLength ?? cur.MarkerLength,
            ShowCoordLabel: i.ShowCoordLabel ?? cur.ShowCoordLabel,
            CoordLabelColor: i.CoordLabelColor ?? cur.CoordLabelColor,
            CoordLabelBackgroundColor: i.CoordLabelBackgroundColor ?? cur.CoordLabelBackgroundColor,
            CoordLabelFontSize: i.CoordLabelFontSize ?? cur.CoordLabelFontSize,
            LoupeSize: i.LoupeSize ?? cur.LoupeSize,
            LoupeZoomLevel: i.LoupeZoomLevel ?? cur.LoupeZoomLevel,
            LoupeOffset: i.LoupeOffset ?? cur.LoupeOffset,
            LoupeRenderScaling: i.LoupeRenderScaling ?? cur.LoupeRenderScaling,
            LoupeCrossColor: i.LoupeCrossColor ?? cur.LoupeCrossColor,
            LoupeFrameColor: i.LoupeFrameColor ?? cur.LoupeFrameColor,
            LoupeFrameThickness: i.LoupeFrameThickness ?? cur.LoupeFrameThickness,
            LoupeCheckerSize: i.LoupeCheckerSize ?? cur.LoupeCheckerSize,
            LoupeCheckerColorA: i.LoupeCheckerColorA ?? cur.LoupeCheckerColorA,
            LoupeCheckerColorB: i.LoupeCheckerColorB ?? cur.LoupeCheckerColorB,
            LoupeSelectionRectColorA: i.LoupeSelectionRectColorA ?? cur.LoupeSelectionRectColorA,
            LoupeSelectionRectColorB: i.LoupeSelectionRectColorB ?? cur.LoupeSelectionRectColorB,
            LoupeShowCoordLabel: i.LoupeShowCoordLabel ?? cur.LoupeShowCoordLabel,
            LoupeCoordLabelColor: i.LoupeCoordLabelColor ?? cur.LoupeCoordLabelColor,
            LoupeCoordLabelBackgroundColor: i.LoupeCoordLabelBackgroundColor ?? cur.LoupeCoordLabelBackgroundColor,
            LoupeCoordLabelFontSize: i.LoupeCoordLabelFontSize ?? cur.LoupeCoordLabelFontSize,
            SizeLabelVisible: i.SizeLabelVisible ?? cur.SizeLabelVisible,
            SizeLabelColor: i.SizeLabelColor ?? cur.SizeLabelColor,
            SizeLabelFontSize: i.SizeLabelFontSize ?? cur.SizeLabelFontSize,
            SizeLabelOverride: i.SizeLabelOverride ?? cur.SizeLabelOverride,
            TextBackgroundColor: i.TextBackgroundColor ?? cur.TextBackgroundColor,
            InfoLabelVisible: i.InfoLabelVisible ?? cur.InfoLabelVisible,
            InfoLabelColor: i.InfoLabelColor ?? cur.InfoLabelColor,
            InfoLabelBackgroundColor: i.InfoLabelBackgroundColor ?? cur.InfoLabelBackgroundColor,
            InfoLabelFontSize: i.InfoLabelFontSize ?? cur.InfoLabelFontSize,
            ShowSubLines: i.ShowSubLines ?? cur.ShowSubLines,
            SubLineFontSize: i.SubLineFontSize ?? cur.SubLineFontSize,
            SubLineColor: i.SubLineColor ?? cur.SubLineColor,
            SubLineBackgroundColor: i.SubLineBackgroundColor ?? cur.SubLineBackgroundColor,
            DisplayScaleScaling: i.DisplayScaleScaling ?? cur.DisplayScaleScaling);
    }
}

