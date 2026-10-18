using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using EsUtil.SaveEvidence.App.Core.Services;
using EsUtil.SaveEvidence.Parts.CapturePreview.AvaloniaUI.Crop;

namespace EsUtil.SaveEvidence.App.AvaloniaUI.Controls;

/// <summary>キャプチャページの画像の 4 辺・4 隅へ影を描く重ね合わせ（Avalonia 実装）。</summary>
/// <remarks>
/// <b>適用パターン</b>: 描画層（画像の外側へ影を描くだけの重ね合わせ）。<br/>
/// 影の幅・濃さの変化は <see cref="ShadowPlan"/>（App.Core）が単一所有するため、本クラスは<br/>
/// 「計画の 4 辺を線形グラデーションで、4 隅を放射グラデーションで塗る」手順だけを持つ<br/>
/// （4 UI 版で同じ結果になる）。<br/>
/// <b><c>BoxShadow</c> を使わない理由</b>: 一覧のサムネイル（<see cref="ThumbnailList"/>）は <c>BoxShadow</c> を<br/>
/// 使うが、キャプチャページの画像は<b>他の UI 版と同じ合成</b>にする必要がある（<c>BoxShadow</c> の<br/>
/// ぼかしは画像の端で濃い輪を作り、画像との間に線として見える）。<br/>
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

        // 影は画像の外側へ 10 DIP 広がるため、プレビュー領域の外（ツールバー・ステータスバー）へ
        // 描かないよう自分の領域で切り取る（画像が Fit より大きいと影の位置が領域の外へ出る）
        ClipToBounds = true;
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
        ArgumentNullException.ThrowIfNull(context);

        if (_crop is null)
        {
            return;
        }

        var image = _crop.ImageRectViewport();
        if (image.Width <= 0 || image.Height <= 0)
        {
            return;
        }

        // 物理ピクセルの格子へ合わせる（半端な位置に描くと端の画素が半透明になり、
        // 画像との間に「白い線」「黒い線」として見える）
        var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;

        foreach (var fill in ShadowPlan.BuildEdgeRects(image.X, image.Y, image.Width, image.Height))
        {
            context.DrawRectangle(
                CreateEdgeBrush(fill),
                null,
                Snap(fill.X, fill.Y, fill.Width, fill.Height, scale));
        }

        foreach (var fill in ShadowPlan.BuildCornerFills(image.X, image.Y, image.Width, image.Height))
        {
            context.DrawRectangle(
                CreateCornerBrush(fill),
                null,
                Snap(fill.X, fill.Y, fill.Size, fill.Size, scale));
        }
    }

    /// <summary>矩形を物理ピクセルの格子へ丸める。</summary>
    /// <param name="x">左端（DIP）。</param>
    /// <param name="y">上端（DIP）。</param>
    /// <param name="width">幅（DIP）。</param>
    /// <param name="height">高さ（DIP）。</param>
    /// <param name="scale">DIP → 物理ピクセルの倍率。</param>
    /// <returns>格子へ丸めた矩形。</returns>
    private static Rect Snap(double x, double y, double width, double height, double scale)
    {
        var left = Math.Round(x * scale) / scale;
        var top = Math.Round(y * scale) / scale;
        var right = Math.Round((x + width) * scale) / scale;
        var bottom = Math.Round((y + height) * scale) / scale;
        return new Rect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }
    /// <summary>辺のグラデーションを生成する（位置 0 が外側）。</summary>
    /// <param name="fill">塗る矩形と辺の計画。</param>
    /// <returns>グラデーションのブラシ。</returns>
    private static IBrush CreateEdgeBrush(ShadowEdgeFill fill)
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

        AddStops(brush, fill.Edge.Stops);
        return brush;
    }

    /// <summary>隅の放射グラデーションを生成する（中心は画像の隅の頂点）。</summary>
    /// <param name="fill">塗る矩形と隅の計画。</param>
    /// <returns>放射グラデーションのブラシ。</returns>
    private static IBrush CreateCornerBrush(ShadowCornerFill fill)
    {
        var center = new RelativePoint(
            fill.X + (fill.CenterX * fill.Size),
            fill.Y + (fill.CenterY * fill.Size),
            RelativeUnit.Absolute);
        var radius = new RelativeScalar(fill.Size, RelativeUnit.Absolute);

        var brush = new RadialGradientBrush
        {
            Center = center,
            GradientOrigin = center,
            RadiusX = radius,
            RadiusY = radius,
        };

        AddStops(brush, fill.Stops);
        return brush;
    }

    /// <summary>停止点をグラデーションへ追加する。</summary>
    /// <param name="brush">追加先のグラデーション。</param>
    /// <param name="stops">停止点（位置 0〜1 と色）。</param>
    private static void AddStops(GradientBrush brush, IReadOnlyList<ShadowStop> stops)
    {
        foreach (var stop in stops)
        {
            brush.GradientStops.Add(new GradientStop(
                Color.FromUInt32(stop.Argb),
                Math.Clamp(stop.Offset, 0, 1)));
        }
    }
}
