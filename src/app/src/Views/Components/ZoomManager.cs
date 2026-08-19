using app.Logging;
using app.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Microsoft.Extensions.Logging;

namespace app.Views.Components;

/// <summary>
/// MainWindow 用のズーム管理クラス。
/// </summary>
/// <remarks>
/// 画像プレビューのズームイン・ズームアウト・Fit表示、スライダとの同期、<br/>
/// ビューポートリサイズ時の Fit 再適用、画像配置（中央寄せ/左寄せ）などを管理する。<br/>
/// プロパティ注入で Window の ViewModel・PreviewImageControl・ZoomableView・<br/>
/// ZoomSlider を受け取り、内部でズーム計算・UI更新を行う。<br/>
/// ズーム率の選択はステータスバーのスライダ（10〜500%）とフライアウト（自動, 10〜500%）で行う。<br/>
/// <br/>
/// <b>ズーム表示方式</b>: <see cref="ZoomableView"/>（ScrollViewer 不使用）が RenderTransform<br/>
/// （ScaleTransform + TranslateTransform）でズーム・パンを実現する。<br/>
/// ScrollViewer のレイアウト処理によるオフセット上書きが発生しないため、<br/>
/// ズーム中心維持の防御コード（保留オフセット再設定等）が不要。<br/>
/// ズーム中心の計算（<see cref="ZoomMath"/>）と遷移（<see cref="ZoomTransition"/>）は純粋関数に分離し、<br/>
/// 単体テストで仕様を検証できる。
/// </remarks>
public sealed class ZoomManager
{
    /// <summary>アプリケーションロガー。</summary>
    private static readonly ILogger _logger = AppLog.CreateLogger("ZoomManager");

    private bool _isSyncingZoom;

    /// <summary>ApplyZoom 再入防止フラグ（ZoomPercent の PropertyChanged と直接呼び出しの二重実行を防ぐ）。</summary>
    private bool _isApplyingZoom;

    /// <summary>メインViewModel。Window から設定する。</summary>
    public MainViewModel ViewModel { get; set; } = null!;

    /// <summary>画像表示コントロール。Window から設定する。</summary>
    public Image PreviewImageControl { get; set; } = null!;

    /// <summary>ズーム・パン対応ビューポート（ScrollViewer 不使用）。Window から設定する。</summary>
    /// <remarks>
    /// RenderTransform ベースのため、レイアウト処理によるオフセット上書きが発生しない。<br/>
    /// ズーム中心維持（<see cref="ZoomMath.ComputeOffsetAfterScaleChange"/>）も内部的に処理する。
    /// </remarks>
    public app.Controls.ZoomableView ZoomableView { get; set; } = null!;

    /// <summary>ズーム率調整用スライダ（ステータスバー）。Window から設定する。</summary>
    public Slider ZoomSlider { get; set; } = null!;

    /// <summary>ズーム中心として使用するマウスポインタ位置（ビューポートローカル DIP）。null の場合はビューポート中心。</summary>
    /// <remarks>
    /// Ctrl+ホイールズーム時に設定され、マウスポインタ位置を中心に拡大縮小する。<br/>
    /// null（ボタン・スライダ・フライアウト操作）の場合は、表示されている中心位置を中心に拡大縮小する。
    /// </remarks>
    public Point? PointerPosition { get; set; }

    /// <summary>ズームインする（⊖⊕ボタン）。</summary>
    /// <remarks>
    /// <see cref="ZoomTransition.ButtonZoomLevels"/> の次のレベルへ移動する（<see cref="ZoomTransition.NextLevel"/>）。<br/>
    /// Fit は遷移に含めない。リスト末尾（500%）で停止する。<br/>
    /// 移動先がなければ何もしない（現在のズーム率を維持）。
    /// </remarks>
    public void ZoomIn()
    {
        var current = ViewModel.ZoomPercent;
        var target = ZoomTransition.NextLevel(current, ComputeRawFitZoomPercent());
        if (target is not { } t) return;

        _logger.LogZoomIn(current, t);
        // ZoomPercent の設定で PropertyChanged が発火し、CapturePage のハンドラが ApplyZoom() を呼ぶ（1 回だけ実行）
        ViewModel.ZoomPercent = t;
    }

    /// <summary>ズームアウトする（⊖⊕ボタン）。</summary>
    /// <remarks>
    /// <see cref="ZoomTransition.ButtonZoomLevels"/> の前のレベルへ移動する（<see cref="ZoomTransition.PrevLevel"/>）。<br/>
    /// Fit は遷移に含めない。リスト先頭（10%）で停止する。<br/>
    /// 移動先がなければ何もしない（現在のズーム率を維持）。
    /// </remarks>
    public void ZoomOut()
    {
        var current = ViewModel.ZoomPercent;
        var target = ZoomTransition.PrevLevel(current, ComputeRawFitZoomPercent());
        if (target is not { } t) return;

        _logger.LogZoomOut(current, t);
        // ZoomPercent の設定で PropertyChanged が発火し、CapturePage のハンドラが ApplyZoom() を呼ぶ（1 回だけ実行）
        ViewModel.ZoomPercent = t;
    }

    /// <summary>Ctrl+ホイールでズームインする（拡大 = Round(現在の拡大率 × 1.1)）。</summary>
    /// <remarks>
    /// <see cref="ZoomTransition.WheelZoomIn"/> で倍率を計算する。Fit は遷移に含めない。<br/>
    /// Fit 表示中は実効倍率の 1.1 倍へ移動する（実効倍率 ≥ 500 なら拡大不可）。<br/>
    /// 移動先がなければ何もしない（現在のズーム率を維持）。
    /// </remarks>
    public void ZoomInByWheel()
    {
        var current = ViewModel.ZoomPercent;
        var target = ZoomTransition.WheelZoomIn(current, ComputeRawFitZoomPercent());
        if (target is not { } t) return;

        _logger.LogZoomInWheel(current, t);
        // ZoomPercent の設定で PropertyChanged が発火し、CapturePage のハンドラが ApplyZoom() を呼ぶ
        ViewModel.ZoomPercent = t;
    }

    /// <summary>Ctrl+ホイールでズームアウトする（縮小 = Round(現在の拡大率 ÷ 1.1)）。</summary>
    /// <remarks>
    /// <see cref="ZoomTransition.WheelZoomOut"/> で倍率を計算する。Fit は遷移に含めない。<br/>
    /// Fit 表示中は実効倍率の 1/1.1 倍へ移動する（実効倍率 ≤ 10 なら縮小不可）。<br/>
    /// 移動先がなければ何もしない（現在のズーム率を維持）。
    /// </remarks>
    public void ZoomOutByWheel()
    {
        var current = ViewModel.ZoomPercent;
        var target = ZoomTransition.WheelZoomOut(current, ComputeRawFitZoomPercent());
        if (target is not { } t) return;

        _logger.LogZoomOutWheel(current, t);
        // ZoomPercent の設定で PropertyChanged が発火し、CapturePage のハンドラが ApplyZoom() を呼ぶ
        ViewModel.ZoomPercent = t;
    }

    /// <summary>ComboBox のテキスト入力からズーム率をパースして適用する。</summary>
    /// <param name="text">入力テキスト（例: "25"、"25%"、"150 %"）。</param>
    /// <returns>適用できた場合は true、無効な入力の場合は false。</returns>
    /// <remarks>
    /// 入力値は「数値」または「数値 + %」形式を受理し、10〜500 の範囲に制限する<br/>
    /// （ZoomLevels の最小固定倍率 10% と最大倍率 500% と整合）。<br/>
    /// 数値以外・範囲外・空の入力は無視し、既存のズーム率を維持する（適用不可を false で通知）。
    /// </remarks>
    public bool ApplyZoomFromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var cleaned = text.Trim().TrimEnd('%');
        if (!int.TryParse(cleaned, out var value)) return false;
        if (value is < 10 or > 500) return false;

        // ZoomPercent の設定で PropertyChanged が発火し、CapturePage のハンドラが ApplyZoom() を呼ぶ
        ViewModel.ZoomPercent = value;
        return true;
    }

    /// <summary>現在の ZoomPercent に基づいてプレビュー画像のズーム表示を適用する。</summary>
    /// <remarks>
    /// <see cref="ZoomableView"/>（ScrollViewer 不使用）にズーム率を設定する。<br/>
    /// ズーム中心（マウスポインタ位置 or ビューポート中心）とズーム中心維持は<br/>
    /// <see cref="ZoomMath.ComputeOffsetAfterScaleChange"/> により ZoomableView 内で処理される。<br/>
    /// ZoomPercent の PropertyChanged（CapturePage のハンドラ）と本メソッドの直接呼び出しが重なるため、<br/>
    /// 再入防止フラグで 1 回だけ実行する。
    /// </remarks>
    public void ApplyZoom()
    {
        if (_isApplyingZoom) return; // 再入防止（PropertyChanged と直接呼び出しの二重実行を防ぐ）
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
        var zoom = ViewModel.ZoomPercent;
        if (ViewModel.PreviewImage is null) return;

        // ズーム中心（ビューポートローカル）。マウスポインタが無い場合はビューポート中心
        var center = PointerPosition ?? new Point(ZoomableView.Bounds.Width / 2, ZoomableView.Bounds.Height / 2);

        // ZoomableView にズーム率を設定（ズーム中心維持は ZoomableView 内で処理。防御コード不要）
        ZoomableView.SetZoom(zoom, center);

        // Fit の実効倍率は 1 回だけ計算し、遷移可否判定と表示テキストで共有する
        var fitZoom = ComputeRawFitZoomPercent();

        // ズーム遷移ロジック（⊖⊕ 23 段階・Fit 基準）で有効/無効を判定する
        ViewModel.CanZoomIn = ZoomTransition.CanZoomIn(zoom, fitZoom);
        ViewModel.CanZoomOut = ZoomTransition.CanZoomOut(zoom, fitZoom);

        // ステータスバーには現在の実効拡大率を % で表示する（「自動」表記は行わない）
        var actual = zoom > 0 ? zoom : Math.Clamp((int)Math.Round(fitZoom), 1, 500);
        ViewModel.ZoomDisplayText = $"{actual}%";
    }

    /// <summary>ビューポートリサイズ時に Fit モードの拡大率表示を更新する。</summary>
    /// <remarks>Fit 実効倍率はビューポートと元画像サイズから算出する（<see cref="ZoomMath.FitZoomPercent"/> に委譲）。</remarks>
    public void OnZoomableViewSizeChanged(SizeChangedEventArgs e)
    {
        if (ViewModel.ZoomPercent <= 0 && ViewModel.PreviewImage is { } img)
        {
            // Fit の実効倍率が変わるため、ステータスバーの拡大率表示を新しいビューポートサイズで更新する
            var scale = ZoomMath.FitZoomPercent(
                new Size(img.PixelSize.Width, img.PixelSize.Height), e.NewSize) / 100.0;
            ViewModel.ZoomDisplayText = $"{Math.Clamp((int)Math.Round(scale * 100), 1, 500)}%";
        }
    }

    /// <summary>スライダの値（ZoomPercent）変更時にズームを適用する。</summary>
    /// <param name="value">スライダ値（10〜500。0=自動はフライアウト選択時のみ）。</param>
    /// <remarks>スライダは 10〜500% の範囲で調整可能。自動（0）はフライアウトメニューでのみ選択する。</remarks>
    public void ApplyZoomFromSlider(double value)
    {
        var zoom = (int)Math.Round(value);
        if (zoom is < 10 or > 500) return;
        // ZoomPercent の設定で PropertyChanged が発火し、CapturePage のハンドラが ApplyZoom() を呼ぶ
        ViewModel.ZoomPercent = zoom;
    }

    /// <summary>ZoomPercent の変更をスライダに反映する（自動=0 の場合はスライダ最小値 10 に合わせる）。</summary>
    /// <remarks>
    /// 自動（Fit）表示中はスライダで表現できないため、最小値（10）に合わせる。<br/>
    /// フライアウトで自動を選択した場合も同様。スライダを動かすと固定倍率に切り替わる。
    /// </remarks>
    public void SyncZoomSlider()
    {
        if (_isSyncingZoom) return;
        _isSyncingZoom = true;
        try
        {
            var zoom = ViewModel.ZoomPercent;
            var sliderValue = zoom > 0 ? zoom : ZoomSlider.Minimum;
            if (Math.Abs(ZoomSlider.Value - sliderValue) > 0.01)
            {
                ZoomSlider.Value = sliderValue;
            }
        }
        finally
        {
            _isSyncingZoom = false;
        }
    }

    /// <summary>中央寄せ設定を ZoomableView に反映する（Fit 時の中央寄せ/左上寄せ）。</summary>
    /// <remarks>
    /// ZoomableView は画像がビューポートより大きい場合（拡大時・スクロールが必要な場合）は<br/>
    /// パン位置で制御し、小さい場合（縮小時）のみ CenterAlign 設定で中央寄せ/左上寄せを切り替える。
    /// </remarks>
    public void UpdateImageAlignment()
    {
        ZoomableView.CenterAlign = ViewModel.Settings.CenterAlign;
    }

    /// <summary>Fit 表示時の実表示倍率（%）をクランプなしで計算する。</summary>
    /// <returns>実表示倍率（%）。ビューポートと元画像サイズから算出、画像未設定時は 100。</returns>
    /// <remarks>
    /// 現在の表示状態に依存しないため、固定倍率表示中でも常に一定の Fit 実効倍率を返す。<br/>
    /// 必ずビューポートと元画像サイズから計算する（<see cref="ZoomMath.FitZoomPercent"/> に委譲）。
    /// </remarks>
    private double ComputeRawFitZoomPercent()
    {
        if (ViewModel.PreviewImage is { } img)
        {
            return ZoomMath.FitZoomPercent(
                new Size(img.PixelSize.Width, img.PixelSize.Height),
                ZoomableView.Bounds.Size);
        }
        return 100;
    }

    /// <summary>
    /// 現在の実表示倍率（%）を計算する。
    /// </summary>
    /// <returns>実表示倍率（%）。1〜500 の範囲にクランプされる。</returns>
    /// <remarks>
    /// ZoomPercent &gt; 0 の場合はそのまま返す。<br/>
    /// ZoomPercent &lt;= 0（Fit 表示）の場合は、プレビュー画像の表示サイズと元画像サイズから<br/>
    /// 実表示倍率（Uniform 表示）を算出する。<br/>
    /// 実表示倍率を 1〜500 の範囲にクランプする。
    /// </remarks>
    public int ComputeActualZoomPercent()
    {
        var zoom = ViewModel.ZoomPercent;
        if (zoom > 0) return zoom;
        return Math.Clamp((int)Math.Round(ComputeRawFitZoomPercent()), 1, 500);
    }
}
