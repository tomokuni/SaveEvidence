using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Estable.AvaloniaUI.ZoomView.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Estable.AvaloniaUI.ZoomView;

/// <summary>
/// 画像プレビューのズーム・パン・Fit 表示を提供する自己完結型複合コントロール。
/// </summary>
/// <remarks>
/// <see cref="ZoomableView"/>（ScrollViewer 不使用のパン・ズームビューポート）を内包し、<br/>
/// ズーム率の遷移（⊖⊕ 23 段階 / Ctrl+ホイール ×1.1）・適用・Fit 表示を統括する。<br/>
/// <br/>
/// <b>ズーム率の単一所有</b>: ズーム率は内包する <see cref="ZoomableView"/> が単一所有し、<br/>
/// 本コントロールは <see cref="ZoomPercent"/>（読み書き可能）でホストへ操作窓口を提供する。<br/>
/// ズーム可否（<see cref="CanZoomIn"/> / <see cref="CanZoomOut"/>）・表示テキスト（<see cref="ZoomDisplayText"/>）も本コントロールが所有する。<br/>
/// <br/>
/// <b>自己完結</b>: Ctrl+ホイールのズーム要求（<see cref="ZoomableView.ZoomRequested"/>）とビューポートリサイズ時の<br/>
/// Fit 再適用（<see cref="ZoomableView.SizeChanged"/>）を内部で購読し、ホスト側の配線を不要にする。<br/>
/// イベント（<see cref="ZoomChanged"/>）でズーム率・可否の変化をホストへ通知し、任意の UI（スライダ・ボタン等）へ反映できる。<br/>
/// <br/>
/// <b>汎用性</b>: 特定コントロール（Slider 等）へは依存しない。ズーム表示の反映は <see cref="ZoomChanged"/> で疎結合にする。<br/>
/// 計算（<see cref="ZoomMath"/>）と遷移（<see cref="ZoomTransition"/>）は純粋関数に分離し、単体テストで仕様を検証できる。
/// </remarks>
public sealed class ZoomViewControl : Panel
{
    /// <summary>ズーム状態反映（ApplyZoom）の再入防止フラグ。</summary>
    private bool _isApplyingZoom;

    /// <summary>ズーム操作の可否（null = 未計算。参照時に計算してキャッシュ）。</summary>
    /// <remarks>
    /// 2 つの可否（<see cref="CanZoomIn"/> / <see cref="CanZoomOut"/>）を単一の値として保持し、<br/>
    /// 「片方だけ計算済み」という矛盾状態を構造的に排除する。<br/>
    /// null の場合はプロパティ参照時に <see cref="ZoomTransition"/> から遅延計算する。
    /// </remarks>
    private ZoomAvailability? _availability;

    /// <summary>内包するパン・ズームビューポート。</summary>
    private readonly ZoomableView _zoomableView = new();

    /// <summary>内部ロガー（横断的関心事）。null を設定した場合は <see cref="NullLogger"/> へフォールバックする。</summary>
    /// <value>null 可。設定しない場合はログを出力しない。</value>
    public ILogger Log
    {
        get => field ?? NullLogger.Instance;
        set => field = value ?? NullLogger.Instance;
    }

    /// <summary>ZoomViewControl を初期化し、内包する ZoomableView の配線を自己完結させる。</summary>
    /// <remarks>
    /// <see cref="ZoomableView"/> を子として追加し、Ctrl+ホイールのズーム要求（<see cref="ZoomableView.ZoomRequested"/>）と<br/>
    /// ビューポートリサイズ時の Fit 再適用（<see cref="ZoomableView.SizeChanged"/>）を内部で購読する。<br/>
    /// これによりホストは <see cref="ZoomableView.ZoomRequested"/> / <see cref="ZoomableView.SizeChanged"/> の配線を
    /// コードビハインドで行う必要がなくなる（配線の重複を排除・責任の集約）。
    /// </remarks>
    public ZoomViewControl()
    {
        Children.Add(_zoomableView);

        // Ctrl+ホイールのズーム要求（ズーム率の決定は本コントロール。マウスポインタ位置をズーム中心に使用）。
        // ズーム後に PointerPosition をリセットする（次回のボタン・スライダ操作はビューポート中心でズームする）。
        _zoomableView.ZoomRequested += (_, e) =>
        {
            PointerPosition = e.PointerPosition;
            if (e.ZoomIn) ZoomInByWheel();
            else ZoomOutByWheel();
            PointerPosition = null;
        };

        // ビューポートリサイズ時に Fit 表示（表示テキスト）を再計算する
        _zoomableView.SizeChanged += (_, e) => OnZoomableViewSizeChanged(e);

        // 初期状態のズーム可否を未計算に戻す（初回参照時に遅延計算してキャッシュする）
        _availability = null;
    }

    /// <summary>ズーム中心として使用するマウスポインタ位置（ビューポートローカル DIP）。null の場合はビューポート中心。</summary>
    /// <remarks>
    /// Ctrl+ホイールズーム時に設定され、マウスポインタ位置を中心に拡大縮小する。<br/>
    /// null（ボタン・スライダ・フライアウト操作）の場合は、表示されている中心位置を中心に拡大縮小する。
    /// </remarks>
    public Point? PointerPosition { get; set; }

    /// <summary>表示する画像（ズーム対象）。設定すると Fit 表示にリセットする。</summary>
    /// <remarks>画像変更時は内部 <see cref="ZoomableView"/> へ伝搬する。null で画像なし。</remarks>
    public IImage? Source
    {
        get => _zoomableView.ImageControl.Source;
        set
        {
            _zoomableView.SetSource(value);
            ApplyZoom();
        }
    }

    /// <summary>Fit 時の画像の中央寄せ / 左上寄せ。</summary>
    public bool CenterAlign
    {
        get => _zoomableView.CenterAlign;
        set
        {
            _zoomableView.CenterAlign = value;
            ApplyZoom();
        }
    }

    /// <summary>ステータスバー等に表示する拡大率テキスト（例: "100%"）。本コントロールが更新する。</summary>
    public string ZoomDisplayText { get; private set; } = "100%";

    /// <summary>現在のズーム率（%）。0 = Fit。ズーム率の単一所有（<see cref="ZoomableView"/>）から読み書きする。</summary>
    /// <remarks>
    /// ズーム率は <see cref="ZoomableView"/> が単一所有する。<br/>
    /// getter は <see cref="ZoomableView.ZoomPercent"/>（<c>double</c>）を <c>int</c> 化して返し、<br/>
    /// setter は <see cref="ZoomableView.SetZoom"/> で設定した後に <see cref="ApplyZoom"/> で表示テキスト・可否・イベントを反映する。<br/>
    /// ズーム中心維持には <see cref="PointerPosition"/> を使用する（null = ビューポート中心）。
    /// </remarks>
    public int ZoomPercent
    {
        get => (int)Math.Round(_zoomableView.ZoomPercent);
        set
        {
            _zoomableView.SetZoom(value, PointerPosition);
            ApplyZoom();
        }
    }

    /// <summary>現在のズーム状態でズームインが可能かどうか（段階遷移仕様に基づく遅延計算キャッシュ）。</summary>
    /// <value>ズームイン可能なら true。未計算の場合は参照時に計算してキャッシュする。</value>
    /// <remarks>
    /// <see cref="ZoomTransition.CanZoomIn"/>（段階遷移仕様）で判定し、初回参照・状態変化時にキャッシュする。<br/>
    /// イベント（<see cref="ZoomChanged"/>）を待たずに初期表示時のボタン状態を設定したい場合に参照する。<br/>
    /// 画像未設定時（<see cref="Source"/> が null）は false。
    /// </remarks>
    public bool CanZoomIn => GetAvailability().CanZoomIn;

    /// <summary>現在のズーム状態でズームアウトが可能かどうか（段階遷移仕様に基づく遅延計算キャッシュ）。</summary>
    /// <value>ズームアウト可能なら true。未計算の場合は参照時に計算してキャッシュする。</value>
    /// <remarks>
    /// <see cref="ZoomTransition.CanZoomOut"/>（段階遷移仕様）で判定し、初回参照・状態変化時にキャッシュする。<br/>
    /// イベント（<see cref="ZoomChanged"/>）を待たずに初期表示時のボタン状態を設定したい場合に参照する。<br/>
    /// 画像未設定時（<see cref="Source"/> が null）は false。
    /// </remarks>
    public bool CanZoomOut => GetAvailability().CanZoomOut;

    /// <summary>ズーム率が変更されたときに発火するイベント（ホスト側のスライダ・⊖⊕ ボタン等の同期用）。</summary>
    /// <remarks>
    /// <see cref="ApplyZoom"/> 実行後に変更後のズーム率と可否（<see cref="ZoomChangedEventArgs"/>）を通知する。<br/>
    /// ホスト側は本イベントを購読し、スライダ・⊖⊕ ボタンの Enable/Disable・表示テキスト等の任意の UI へ反映できる<br/>
    /// （本コントロールは特定コントロールに依存しない）。
    /// </remarks>
    public event EventHandler<ZoomChangedEventArgs>? ZoomChanged;

    /// <summary>内包するパン・ズームビューポート（座標変換・イベント等の低レベルアクセス用）。</summary>
    /// <remarks>
    /// 通常は <see cref="ZoomIn"/> 等の高レベル API で操作するが、<br/>
    /// 座標変換（<see cref="ZoomableView.ViewportToContentImage"/> 等）やオーバーレイ追従（<see cref="ZoomableView.ViewportChanged"/>）等、
    /// 低レベルの細かな操作が必要なホストへ公開する。
    /// </remarks>
    public ZoomableView ZoomableView => _zoomableView;

    /// <summary>ズームインする（⊖⊕ボタン）。</summary>
    /// <remarks>
    /// <see cref="ZoomTransition.ButtonZoomLevels"/> の次のレベルへ移動する（<see cref="ZoomTransition.NextLevel"/>）。<br/>
    /// Fit は遷移に含めない。リスト末尾（500%）で停止する。<br/>
    /// 移動先がなければ何もしない（現在のズーム率を維持）。
    /// </remarks>
    public void ZoomIn()
    {
        var zoom = ZoomPercent;
        // 遷移関数は Fit 中（current <= 0）のみ fitZoom を使うため、固定倍率中は計算しない
        var fitZoom = zoom <= 0 ? ComputeRawFitZoomPercent() : 0;
        ApplyZoomTransition(ZoomTransition.NextLevel(zoom, fitZoom), Log.LogZoomIn);
    }

    /// <summary>ズームアウトする（⊖⊕ボタン）。</summary>
    /// <remarks>
    /// <see cref="ZoomTransition.ButtonZoomLevels"/> の前のレベルへ移動する（<see cref="ZoomTransition.PrevLevel"/>）。<br/>
    /// Fit は遷移に含めない。リスト先頭（10%）で停止する。<br/>
    /// 移動先がなければ何もしない（現在のズーム率を維持）。
    /// </remarks>
    public void ZoomOut()
    {
        var zoom = ZoomPercent;
        var fitZoom = zoom <= 0 ? ComputeRawFitZoomPercent() : 0;
        ApplyZoomTransition(ZoomTransition.PrevLevel(zoom, fitZoom), Log.LogZoomOut);
    }

    /// <summary>Ctrl+ホイールでズームインする（拡大 = Round(現在の拡大率 × 1.1)）。</summary>
    /// <remarks>
    /// <see cref="ZoomTransition.WheelZoomIn"/> で倍率を計算する。Fit は遷移に含めない。<br/>
    /// Fit 表示中は実効倍率の 1.1 倍へ移動する（実効倍率 ≥ 500 なら拡大不可）。<br/>
    /// 移動先がなければ何もしない（現在のズーム率を維持）。
    /// </remarks>
    public void ZoomInByWheel()
    {
        var zoom = ZoomPercent;
        var fitZoom = zoom <= 0 ? ComputeRawFitZoomPercent() : 0;
        ApplyZoomTransition(ZoomTransition.WheelZoomIn(zoom, fitZoom), Log.LogZoomInWheel);
    }

    /// <summary>Ctrl+ホイールでズームアウトする（縮小 = Round(現在の拡大率 ÷ 1.1)）。</summary>
    /// <remarks>
    /// <see cref="ZoomTransition.WheelZoomOut"/> で倍率を計算する。Fit は遷移に含めない。<br/>
    /// Fit 表示中は実効倍率の 1/1.1 倍へ移動する（実効倍率 ≤ 10 なら縮小不可）。<br/>
    /// 移動先がなければ何もしない（現在のズーム率を維持）。
    /// </remarks>
    public void ZoomOutByWheel()
    {
        var zoom = ZoomPercent;
        var fitZoom = zoom <= 0 ? ComputeRawFitZoomPercent() : 0;
        ApplyZoomTransition(ZoomTransition.WheelZoomOut(zoom, fitZoom), Log.LogZoomOutWheel);
    }

    /// <summary>ComboBox のテキスト入力からズーム率をパースして適用する。</summary>
    /// <param name="text">入力テキスト（例: "25"、"25%"、"150 %"）。</param>
    /// <returns>適用できた場合は true、無効な入力の場合は false。</returns>
    /// <remarks>
    /// 入力値は「数値」または「数値 + %」形式を受理し、<see cref="ZoomTransition.MinZoom"/> 〜
    /// <see cref="ZoomTransition.MaxZoom"/> の範囲に制限する。<br/>
    /// 数値以外・範囲外・空の入力は無視し、既存のズーム率を維持する（適用不可を false で通知）。
    /// </remarks>
    public bool ApplyZoomFromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var cleaned = text.Trim().TrimEnd('%');
        if (!int.TryParse(cleaned, out var value)) return false;
        if (value is < ZoomTransition.MinZoom or > ZoomTransition.MaxZoom) return false;

        ZoomPercent = value;
        return true;
    }

    /// <summary>スライダの値（ZoomPercent）変更時にズームを適用する。</summary>
    /// <param name="value">スライダ値（10〜500。0=自動はフライアウト選択時のみ）。</param>
    /// <remarks>スライダは 10〜500% の範囲で調整可能。自動（0）はフライアウトメニューでのみ選択する。</remarks>
    public void ApplyZoomFromSlider(double value)
    {
        var zoom = (int)Math.Round(value);
        if (zoom is < ZoomTransition.MinZoom or > ZoomTransition.MaxZoom) return;
        ZoomPercent = zoom;
    }

    /// <summary>現在のズーム状態を表示テキストとイベント購読者へ反映する。</summary>
    /// <remarks>
    /// 内包する <see cref="ZoomableView"/> が所有する現在のズーム率から、<br/>
    /// ステータスバーの表示テキスト（<see cref="ZoomDisplayText"/>）とズーム可否を更新する。<br/>
    /// <see cref="ZoomPercent"/> と <see cref="ZoomableView"/> のズーム率を同期させるため、<br/>
    /// ズーム率変更（<see cref="ZoomPercent"/> の setter・<see cref="ZoomIn"/>・<see cref="ApplyZoomFromSlider"/> 等）の後に呼ぶ。
    /// </remarks>
    public void ApplyZoom()
    {
        if (_isApplyingZoom) return; // 再入防止
        _isApplyingZoom = true;
        try
        {
            ApplyZoomCore();
        }
        finally
        {
            _isApplyingZoom = false;
        }
    }

    /// <summary>ApplyZoom の本体（再入防止フラグは呼び出し側で管理）。</summary>
    private void ApplyZoomCore()
    {
        if (Source is null) return;

        // Fit の実効倍率は 1 回だけ計算し、表示テキストとズーム率の実効値で共有する
        var fitZoom = ComputeRawFitZoomPercent();

        // ステータスバーには現在の実効拡大率を % で表示する（「自動」表記は行わない）
        var zoom = ZoomPercent;
        var actual = zoom > 0 ? zoom : ClampZoomPercent(fitZoom);
        ZoomDisplayText = $"{actual}%";

        // ⊖⊕ ボタンの Enable/Disable 制御用に、段階遷移仕様（ZoomTransition）で可否を即時計算してキャッシュし、通知する
        RefreshZoomAvailability(zoom, fitZoom);
        ZoomChanged?.Invoke(this, new ZoomChangedEventArgs(zoom, _availability!.Value));
    }

    /// <summary>現在のズーム状態からズーム可否を取得する（未計算なら遅延計算してキャッシュ）。</summary>
    /// <returns>ズーム可否。</returns>
    /// <remarks>
    /// <see cref="CanZoomIn"/> / <see cref="CanZoomOut"/> プロパティの参照時に呼ばれる。<br/>
    /// Fit 実効倍率は現在のビューポートと画像から計算する（<see cref="ComputeRawFitZoomPercent"/>）。
    /// </remarks>
    private ZoomAvailability GetAvailability()
        => _availability ??= BuildAvailability(ZoomPercent, ComputeRawFitZoomPercent());

    /// <summary>段階遷移仕様（<see cref="ZoomTransition"/>）に基づき、ズーム可否を計算してキャッシュする。</summary>
    /// <param name="zoom">現在のズーム率（%）。0 = Fit。</param>
    /// <param name="fitZoom">Fit の実効倍率（%）。</param>
    /// <remarks>
    /// <see cref="CanZoomIn"/> / <see cref="CanZoomOut"/> プロパティのキャッシュを更新する。<br/>
    /// <see cref="ApplyZoomCore"/>（fitZoom 計算済みを再利用）と <see cref="GetAvailability"/>（遅延計算）から呼ばれる。<br/>
    /// 画像未設定時（<see cref="Source"/> が null）は false にする。
    /// </remarks>
    private void RefreshZoomAvailability(int zoom, double fitZoom)
        => _availability = BuildAvailability(zoom, fitZoom);

    /// <summary>段階遷移仕様（<see cref="ZoomTransition"/>）に基づき、ズーム可否を計算する。</summary>
    /// <param name="zoom">現在のズーム率（%）。0 = Fit。</param>
    /// <param name="fitZoom">Fit の実効倍率（%）。</param>
    /// <returns>ズーム可否。画像未設定時（<see cref="Source"/> が null）は false。</returns>
    private ZoomAvailability BuildAvailability(int zoom, double fitZoom)
        => zoom > 0 || Source is not null
            ? new ZoomAvailability(ZoomTransition.CanZoomIn(zoom, fitZoom), ZoomTransition.CanZoomOut(zoom, fitZoom))
            : new ZoomAvailability(false, false);

    /// <summary>ズーム遷移の適用（ログ記録 + ZoomableView へのズーム率設定）を共通化する。</summary>
    /// <param name="target">遷移先のズーム率。null の場合は何もしない（移動不可）。</param>
    /// <param name="log">変更前後のズーム率を記録するログメソッド。</param>
    /// <remarks>ズーム率は <see cref="ZoomableView"/> が単一所有するため、<see cref="ZoomableView.SetZoom"/> で変更する。</remarks>
    private void ApplyZoomTransition(int? target, Action<int, int> log)
    {
        if (target is not { } t) return;
        log(ZoomPercent, t);
        _zoomableView.SetZoom(t, PointerPosition);
        ApplyZoom();
    }

    /// <summary>ビューポートリサイズ時に Fit モードの拡大率表示とズーム可否を更新する。</summary>
    /// <remarks>
    /// Fit 実効倍率はビューポートと元画像サイズから算出する（<see cref="ZoomMath.FitZoomPercent"/> に委譲）。<br/>
    /// Fit 中のズーム可否（<see cref="CanZoomIn"/> / <see cref="CanZoomOut"/>）も実効倍率に依存するため、
    /// 未計算（null）に戻して次の参照時に遅延計算する。
    /// </remarks>
    private void OnZoomableViewSizeChanged(SizeChangedEventArgs e)
    {
        if (ZoomPercent <= 0)
        {
            // Fit の実効倍率が変わるため、ステータスバーの拡大率表示を新しいビューポートサイズで更新する
            var fitZoom = ComputeRawFitZoomPercent();
            if (Source is not null)
            {
                ZoomDisplayText = $"{ClampZoomPercent(fitZoom)}%";
            }
            // ズーム可否は実効倍率に依存するため、未計算（null）に戻して次の参照時に再計算（遅延）する
            _availability = null;
        }
    }

    /// <summary>Fit 表示時の実表示倍率（%）をクランプなしで計算する。</summary>
    /// <returns>実表示倍率（%）。ビューポートと元画像サイズから算出、画像未設定時は 100。</returns>
    /// <remarks>
    /// 現在の表示状態に依存しないため、固定倍率表示中でも常に一定の Fit 実効倍率を返す。<br/>
    /// 必ずビューポートと元画像サイズから計算する（<see cref="ZoomMath.FitZoomPercent"/> に委譲）。
    /// </remarks>
    private double ComputeRawFitZoomPercent()
    {
        if (Source is { } img && img is Bitmap bitmap)
        {
            return ZoomMath.FitZoomPercent(
                new Size(bitmap.PixelSize.Width, bitmap.PixelSize.Height),
                _zoomableView.Bounds.Size);
        }
        return 100;
    }

    /// <summary>実効倍率（%）を表示用の整数値（1〜500）に丸めてクランプする。</summary>
    /// <param name="fitZoom">Fit の実効倍率（%）。</param>
    /// <returns>1〜500 の範囲にクランプされた倍率（%）。</returns>
    private static int ClampZoomPercent(double fitZoom)
        => Math.Clamp((int)Math.Round(fitZoom), 1, 500);
}
