using Avalonia;
using Avalonia.Controls;
using Estable.AvaloniaUI.ZoomView.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Estable.AvaloniaUI.ZoomView;

/// <summary>
/// 画像プレビューのズーム操作を統括するマネージャー。
/// </summary>
/// <remarks>
/// 画像プレビューのズームイン・ズームアウト・Fit表示、ビューポートリサイズ時の Fit 再適用、<br/>
/// 画像配置（中央寄せ/左寄せ）などを管理する。<br/>
/// プロパティ注入でホスト（<see cref="IZoomHost"/>）・<see cref="ZoomableView"/> を受け取り、<br/>
/// 内部でズーム計算・UI更新を行う。<br/>
/// ズーム率の変化は <see cref="ZoomChanged"/> イベントでホストへ通知する（ホスト側のスライダ等の同期用）。<br/>
/// <br/>
/// <b>ズーム表示方式</b>: <see cref="ZoomableView"/>（ScrollViewer 不使用）が RenderTransform<br/>
/// （ScaleTransform + TranslateTransform）でズーム・パンを実現する。<br/>
/// ScrollViewer のレイアウト処理によるオフセット上書きが発生しないため、<br/>
/// ズーム中心維持の防御コード（保留オフセット再設定等）が不要。<br/>
/// ズーム中心の計算（<see cref="ZoomMath"/>）と遷移（<see cref="ZoomTransition"/>）は純粋関数に分離し、<br/>
/// 単体テストで仕様を検証できる。<br/>
/// <b>汎用性</b>: 特定コントロール（Slider 等）へは依存しない。ズーム表示の反映は <see cref="ZoomChanged"/> で疎結合にする。
/// </remarks>
public sealed class ZoomManager
{
    /// <summary>ApplyZoom 再入防止フラグ（ZoomPercent の PropertyChanged と直接呼び出しの二重実行を防ぐ）。</summary>
    private bool _isApplyingZoom;

    /// <summary>内部ロガー（横断的関心事）。null を設定した場合は <see cref="NullLogger"/> へフォールバックする。</summary>
    /// <value>null 可。設定しない場合はログを出力しない。</value>
    public ILogger Log
    {
        get => field ?? NullLogger.Instance;
        set => field = value ?? NullLogger.Instance;
    }

    /// <summary>ズーム対象のホスト（ViewModel 等）。Window から設定する。</summary>
    public IZoomHost ViewModel { get; set; } = null!;

    /// <summary>ズーム・パン対応ビューポート（ScrollViewer 不使用）。Window から設定する。</summary>
    /// <remarks>
    /// RenderTransform ベースのため、レイアウト処理によるオフセット上書きが発生しない。<br/>
    /// ズーム中心維持（<see cref="ZoomMath.ComputeOffsetAfterScaleChange"/>）も内部的に処理する。
    /// </remarks>
    public ZoomableView ZoomableView { get; set; } = null!;

    /// <summary>ズーム率が変更されたときに発火するイベント（ホスト側のスライダ等の同期用）。</summary>
    /// <remarks>
    /// <see cref="ApplyZoom"/> 実行後に現在のズーム率（<see cref="IZoomHost.ZoomPercent"/>）を通知する。<br/>
    /// ホスト側は本イベントを購読し、スライダ・表示テキスト等の任意の UI へ反映できる<br/>
    /// （<see cref="ZoomManager"/> は特定コントロールに依存しない）。
    /// </remarks>
    public event EventHandler<int>? ZoomChanged;

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
        var zoom = ViewModel.ZoomPercent;
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
        var zoom = ViewModel.ZoomPercent;
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
        var zoom = ViewModel.ZoomPercent;
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
        var zoom = ViewModel.ZoomPercent;
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

        // ZoomPercent の設定で PropertyChanged が発火し、ホスト側のハンドラが ApplyZoom() を呼ぶ
        ViewModel.ZoomPercent = value;
        return true;
    }

    /// <summary>現在の ZoomPercent に基づいてプレビュー画像のズーム表示を適用する。</summary>
    /// <remarks>
    /// <see cref="ZoomableView"/>（ScrollViewer 不使用）にズーム率を設定する。<br/>
    /// ズーム中心（マウスポインタ位置 or ビューポート中心）とズーム中心維持は<br/>
    /// <see cref="ZoomMath.ComputeOffsetAfterScaleChange"/> により ZoomableView 内で処理される。<br/>
    /// ZoomPercent の PropertyChanged（ホスト側のハンドラ）と本メソッドの直接呼び出しが重なるため、<br/>
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

        // ZoomableView にズーム率を設定。ズーム中心（ポインタ位置 or ビューポート中心）と
        // ズーム中心維持は ZoomableView 内で処理される（null 中心 = ビューポート中心）
        ZoomableView.SetZoom(zoom, PointerPosition);

        // Fit の実効倍率は 1 回だけ計算し、遷移可否判定と表示テキストで共有する
        var fitZoom = ComputeRawFitZoomPercent();

        // ズーム遷移ロジック（⊖⊕ 23 段階・Fit 基準）で有効/無効を判定する
        ViewModel.CanZoomIn = ZoomTransition.CanZoomIn(zoom, fitZoom);
        ViewModel.CanZoomOut = ZoomTransition.CanZoomOut(zoom, fitZoom);

        // ステータスバーには現在の実効拡大率を % で表示する（「自動」表記は行わない）
        var actual = zoom > 0 ? zoom : ClampZoomPercent(fitZoom);
        ViewModel.ZoomDisplayText = $"{actual}%";

        // ズーム率の変化をホストへ通知する（スライダ等の同期用）
        ZoomChanged?.Invoke(this, zoom);
    }

    /// <summary>ズーム遷移の適用（ログ記録 + ZoomPercent 設定）を共通化する。</summary>
    /// <param name="target">遷移先のズーム率。null の場合は何もしない（移動不可）。</param>
    /// <param name="log">変更前後のズーム率を記録するログメソッド。</param>
    /// <remarks>ZoomPercent の設定で PropertyChanged が発火し、ホスト側のハンドラが ApplyZoom() を呼ぶ（1 回だけ実行）。</remarks>
    private void ApplyZoomTransition(int? target, Action<int, int> log)
    {
        if (target is not { } t) return;
        log(ViewModel.ZoomPercent, t);
        ViewModel.ZoomPercent = t;
    }

    /// <summary>ビューポートリサイズ時に Fit モードの拡大率表示を更新する。</summary>
    /// <remarks>Fit 実効倍率はビューポートと元画像サイズから算出する（<see cref="ZoomMath.FitZoomPercent"/> に委譲）。</remarks>
    public void OnZoomableViewSizeChanged(SizeChangedEventArgs e)
    {
        if (ViewModel.ZoomPercent <= 0 && ViewModel.PreviewImage is { } img)
        {
            // Fit の実効倍率が変わるため、ステータスバーの拡大率表示を新しいビューポートサイズで更新する
            var fitZoom = ZoomMath.FitZoomPercent(
                new Size(img.PixelSize.Width, img.PixelSize.Height), e.NewSize);
            ViewModel.ZoomDisplayText = $"{ClampZoomPercent(fitZoom)}%";
        }
    }

    /// <summary>スライダの値（ZoomPercent）変更時にズームを適用する。</summary>
    /// <param name="value">スライダ値（10〜500。0=自動はフライアウト選択時のみ）。</param>
    /// <remarks>スライダは 10〜500% の範囲で調整可能。自動（0）はフライアウトメニューでのみ選択する。</remarks>
    public void ApplyZoomFromSlider(double value)
    {
        var zoom = (int)Math.Round(value);
        if (zoom is < ZoomTransition.MinZoom or > ZoomTransition.MaxZoom) return;
        // ZoomPercent の設定で PropertyChanged が発火し、ホスト側のハンドラが ApplyZoom() を呼ぶ
        ViewModel.ZoomPercent = zoom;
    }

    /// <summary>中央寄せ設定を ZoomableView に反映する（Fit 時の中央寄せ/左上寄せ）。</summary>
    /// <remarks>
    /// ZoomableView は画像がビューポートより大きい場合（拡大時・スクロールが必要な場合）は<br/>
    /// パン位置で制御し、小さい場合（縮小時）のみ CenterAlign 設定で中央寄せ/左上寄せを切り替える。
    /// </remarks>
    public void UpdateImageAlignment()
    {
        ZoomableView.CenterAlign = ViewModel.CenterAlign;
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

    /// <summary>実効倍率（%）を表示用の整数値（1〜500）に丸めてクランプする。</summary>
    /// <param name="fitZoom">Fit の実効倍率（%）。</param>
    /// <returns>1〜500 の範囲にクランプされた倍率（%）。</returns>
    private static int ClampZoomPercent(double fitZoom)
        => Math.Clamp((int)Math.Round(fitZoom), 1, 500);
}
