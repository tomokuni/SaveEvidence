using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using EsUtil.SaveEvidence.App.Core.Services;
using EsUtil.SaveEvidence.Parts.CapturePreview.AvaloniaUI.Crop;

namespace EsUtil.SaveEvidence.App.AvaloniaUI.Controls;

/// <summary>キャプチャページの画像の 4 辺へ影を描く重ね合わせ（Avalonia 実装）。</summary>
/// <remarks>
/// <b>適用パターン</b>: 描画層（画像の外側へ影を描くだけの重ね合わせ）。<br/>
/// 影の幅・濃さの変化は <see cref="ShadowPlan"/>（App.Core）が単一所有するため、本クラスは<br/>
/// 「計画の 4 辺の矩形を、計画の停止点のグラデーションで塗る」手順だけを持つ（4 UI 版で同じ結果になる）。<br/>
/// <b>画像の上へ重ねてよい理由</b>: 計画の辺の矩形は画像の外側にだけ置かれる（画像へ掛からない）。<br/>
/// <b>追従</b>: ズーム・パン・ウィンドウの大きさの変更で画像の表示矩形が変わるため、<br/>
/// <see cref="CropViewControl.ViewportChanged"/> を購読して描き直す。
/// </remarks>
internal sealed class ImageShadowOverlay : Control
{
    /// <summary>影を落とす画像の表示矩形を提供するコントロール。</summary>
    private CropViewControl? _crop;

    /// <summary>重ね合わせを初期化する（マウス操作は下のコントロールへ通す）。</summary>
    public ImageShadowOverlay()
    {
        IsHitTestVisible = false;
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
    /// <param name="context">描画先。</param>
    public override void Render(DrawingContext context)
    {
        if (_crop is null)
        {
            return;
        }

        var image = _crop.ImageRectViewport();
        foreach (var fill in ShadowPlan.BuildEdgeRects(image.X, image.Y, image.Width, image.Height))
        {
            context.DrawRectangle(
                CreateBrush(fill),
                null,
                new Rect(fill.X, fill.Y, fill.Width, fill.Height));
        }
    }

    /// <summary>影の 1 辺分のグラデーションを生成する（位置 0 が外側）。</summary>
    /// <param name="fill">塗る矩形と辺の計画。</param>
    /// <returns>グラデーションのブラシ。</returns>
    private static IBrush CreateBrush(ShadowEdgeFill fill)
    {
        var left = fill.X;
        var top = fill.Y;
        var right = fill.X + fill.Width;
        var bottom = fill.Y + fill.Height;

        double startX;
        double startY;
        double endX;
        double endY;
        if (fill.Edge.IsVertical)
        {
            startX = left;
            endX = left;
            startY = fill.Edge.IsOuterAtStart ? top : bottom;
            endY = fill.Edge.IsOuterAtStart ? bottom : top;
        }
        else
        {
            startY = top;
            endY = top;
            startX = fill.Edge.IsOuterAtStart ? left : right;
            endX = fill.Edge.IsOuterAtStart ? right : left;
        }

        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(startX, startY, RelativeUnit.Absolute),
            EndPoint = new RelativePoint(endX, endY, RelativeUnit.Absolute),
        };

        foreach (var stop in fill.Edge.Stops)
        {
            brush.GradientStops.Add(new GradientStop(Color.FromUInt32(stop.Argb), stop.Offset));
        }

        return brush;
    }
}
