using Aprillz.MewUI.Skia.Controls;
using EsUtil.SaveEvidence.App.Core.Services;
using EsUtil.SaveEvidence.Parts.CapturePreview.MewUI.Crop;
using EsUtil.SaveEvidence.Platform.MewUI.Imaging;
using SkiaSharp;

namespace EsUtil.SaveEvidence.App.MewUI.Views;

/// <summary>キャプチャページの画像の 4 辺へ影を描く重ね合わせ（MewUI 実装）。</summary>
/// <remarks>
/// <b>適用パターン</b>: 描画層（画像の外側へ影を描くだけの重ね合わせ）。<br/>
/// 影の幅・濃さの変化は <see cref="ShadowPlan"/>（App.Core）が単一所有するため、本クラスは<br/>
/// 「計画の 4 辺の矩形を、計画の停止点のグラデーションで塗る」手順だけを持つ（4 UI 版で同じ結果になる）。<br/>
/// <b>画像の上へ重ねてよい理由</b>: 計画の辺の矩形は画像の外側にだけ置かれる（画像へ掛からない）。<br/>
/// <b>追従</b>: ズーム・パン・ウィンドウの大きさの変更で画像の表示矩形が変わるため、<br/>
/// <see cref="CropViewControl.ViewportChanged"/> を購読して描き直す。
/// </remarks>
internal sealed class ImageShadowOverlay : SkiaCanvasView
{
    /// <summary>影を落とす画像の表示矩形を提供するコントロール。</summary>
    private CropViewControl? _crop;

    /// <summary>重ね合わせを初期化する（マウス操作は下のコントロールへ通す）。</summary>
    public ImageShadowOverlay()
    {
        ContinuousAnimation = false;
        IsHitTestVisible = false;
        PaintSurface += OnPaintSurface;
    }

    /// <summary>影を落とす対象のコントロールを設定する。</summary>
    /// <param name="crop">画像を表示するコントロール。</param>
    public void Attach(CropViewControl crop)
    {
        ArgumentNullException.ThrowIfNull(crop);

        _crop = crop;
        crop.ViewportChanged += (_, _) => InvalidateVisual();
        InvalidateVisual();
    }

    /// <summary>影を描画する。</summary>
    /// <param name="canvas">描画先のキャンバス（物理ピクセル）。</param>
    /// <param name="info">描画先の情報（幅・高さ）。</param>
    /// <remarks>
    /// 影の帯は物理ピクセルの矩形で描く（倍率を掛けて描くため、端の画素が半端にならない）。<br/>
    /// 影の外側は透明にして下の画像を見せる。
    /// </remarks>
    private void OnPaintSurface(SKCanvas canvas, SKImageInfo info)
    {
        canvas.Clear(SKColors.Transparent);

        if (_crop is null || Bounds.Size.Width <= 0)
        {
            return;
        }

        // スケーリングは描画面（物理ピクセル）と割り当て領域（DIP）の比から求める
        var scaling = ClientPixelScale.Resolve(info.Width, Bounds.Size.Width, fallback: 1.0);
        var image = _crop.ImageRectViewport();

        // 影は物理ピクセルで描く（帯の矩形が画素に一致する）
        ShadowRenderer.Render(canvas, image.X, image.Y, image.Width, image.Height, scaling);
    }
}
