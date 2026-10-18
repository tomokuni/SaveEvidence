using EsUtil.SaveEvidence.App.Core.Services;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using CropControl = EsUtil.SaveEvidence.Parts.CapturePreview.WinUI3.Crop.CropViewControl;

namespace EsUtil.SaveEvidence.App.WinUI3.Views;

/// <summary>キャプチャページの画像の 4 辺・4 隅へ影を描く重ね合わせ（WinUI 3 実装）。</summary>
/// <remarks>
/// <b>適用パターン</b>: 描画層（画像の外側へ影を描くだけの重ね合わせ）。<br/>
/// 影の幅・濃さの変化は <see cref="ShadowPlan"/>（App.Core）が単一所有するため、本クラスは<br/>
/// 「計画の 4 辺を線形グラデーションで、4 隅を放射グラデーションで塗る」手順だけを持つ<br/>
/// （4 UI 版で同じ結果になる）。<br/>
/// <b>画像の上へ重ねてよい理由</b>: 計画の辺と隅の矩形は画像の外側にだけ置かれる（画像へ掛からない）。<br/>
/// <b>追従</b>: ズーム・パン・ウィンドウの大きさの変更で画像の表示矩形が変わるため、<br/>
/// <see cref="CropControl.ViewportChanged"/> を購読して位置を更新する。
/// </remarks>
internal sealed class ImageShadowOverlay : Canvas
{
    /// <summary>4 辺の帯（<see cref="ShadowPlan.Edges"/> と同じ順）。</summary>
    private readonly List<Border> _edges = [];

    /// <summary>4 隅の帯（左上・右上・右下・左下）。</summary>
    private readonly List<Border> _corners = [];

    /// <summary>影を落とす画像の表示矩形を提供するコントロール。</summary>
    private CropControl? _crop;

    /// <summary>重ね合わせを初期化する（マウス操作は下のコントロールへ通す）。</summary>
    public ImageShadowOverlay()
    {
        IsHitTestVisible = false;

        // 影は画像の外側へ 10 DIP 広がるため、プレビュー領域の外（ツールバー・ステータスバー）へ
        // 描かないよう自分の領域で切り取る（画像が Fit より大きいと影の位置が領域の外へ出る）
        SizeChanged += (_, _) => Clip = new RectangleGeometry
        {
            Rect = new Rect(0, 0, ActualWidth, ActualHeight),
        };

        foreach (var edge in ShadowPlan.Edges)
        {
            var part = new Border { Background = CreateEdgeBrush(edge), IsHitTestVisible = false };
            _edges.Add(part);
            Children.Add(part);
        }

        // 中心は画像の隅の頂点（左上 = 右下の角、右上 = 左下の角、右下 = 左上の角、左下 = 右上の角）
        AddCorner(1, 1);
        AddCorner(0, 1);
        AddCorner(0, 0);
        AddCorner(1, 0);
    }

    /// <summary>影を落とす対象のコントロールを設定する。</summary>
    /// <param name="crop">画像を表示するコントロール。</param>
    public void Attach(CropControl crop)
    {
        ArgumentNullException.ThrowIfNull(crop);

        _crop = crop;
        crop.ViewportChanged += (_, _) => Update();
        Loaded += (_, _) => Update();
        Update();
    }

    /// <summary>隅の帯を 1 つ追加する。</summary>
    /// <param name="centerX">放射グラデーションの中心の X（0〜1）。</param>
    /// <param name="centerY">放射グラデーションの中心の Y（0〜1）。</param>
    private void AddCorner(double centerX, double centerY)
    {
        var part = new Border
        {
            Width = ShadowPlan.BlurRadius,
            Height = ShadowPlan.BlurRadius,
            Background = CreateCornerBrush(centerX, centerY),
            IsHitTestVisible = false,
        };
        _corners.Add(part);
        Children.Add(part);
    }

    /// <summary>画像の表示矩形へ影の位置を合わせる。</summary>
    private void Update()
    {
        if (_crop is null)
        {
            return;
        }

        var image = _crop.ImageRectViewport();

        // 物理ピクセルの格子へ合わせる（半端な位置に置くと端の画素が半透明になり、
        // 画像との間に「白い線」「黒い線」として見える）
        var scale = XamlRoot?.RasterizationScale ?? 1.0;
        var edges = ShadowPlan.BuildEdgeRects(image.X, image.Y, image.Width, image.Height);
        for (var i = 0; i < _edges.Count && i < edges.Count; i++)
        {
            Place(_edges[i], edges[i].X, edges[i].Y, edges[i].Width, edges[i].Height, scale);
        }

        var corners = ShadowPlan.BuildCornerFills(image.X, image.Y, image.Width, image.Height);
        for (var i = 0; i < _corners.Count && i < corners.Count; i++)
        {
            Place(_corners[i], corners[i].X, corners[i].Y, corners[i].Size, corners[i].Size, scale);
        }
    }

    /// <summary>要素を指定の位置と大きさへ置く（物理ピクセルの格子へ丸める）。</summary>
    /// <param name="part">対象の要素。</param>
    /// <param name="x">左端（DIP）。</param>
    /// <param name="y">上端（DIP）。</param>
    /// <param name="width">幅（DIP）。</param>
    /// <param name="height">高さ（DIP）。</param>
    /// <param name="scale">DIP → 物理ピクセルの倍率。</param>
    private static void Place(Border part, double x, double y, double width, double height, double scale)
    {
        var left = Math.Round(x * scale) / scale;
        var top = Math.Round(y * scale) / scale;
        var right = Math.Round((x + width) * scale) / scale;
        var bottom = Math.Round((y + height) * scale) / scale;

        SetLeft(part, left);
        SetTop(part, top);
        part.Width = Math.Max(0, right - left);
        part.Height = Math.Max(0, bottom - top);
    }

    /// <summary>辺のグラデーションを生成する（位置 0 が外側）。</summary>
    /// <param name="edge">辺の計画。</param>
    /// <returns>グラデーションのブラシ。</returns>
    private static LinearGradientBrush CreateEdgeBrush(ShadowEdgePlan edge)
    {
        var outer = edge.IsOuterAtStart
            ? (edge.IsVertical ? new Point(0.5, 0) : new Point(0, 0.5))
            : (edge.IsVertical ? new Point(0.5, 1) : new Point(1, 0.5));
        var inner = edge.IsOuterAtStart
            ? (edge.IsVertical ? new Point(0.5, 1) : new Point(1, 0.5))
            : (edge.IsVertical ? new Point(0.5, 0) : new Point(0, 0.5));

        var brush = new LinearGradientBrush { StartPoint = outer, EndPoint = inner };
        foreach (var stop in edge.Stops)
        {
            brush.GradientStops.Add(new GradientStop
            {
                Color = AppStyles.ToColor(stop.Argb),
                Offset = Math.Clamp(stop.Offset, 0, 1),
            });
        }

        return brush;
    }

    /// <summary>隅の放射グラデーションを生成する。</summary>
    /// <param name="centerX">中心の X（0〜1）。</param>
    /// <param name="centerY">中心の Y（0〜1）。</param>
    /// <returns>放射グラデーションのブラシ。</returns>
    private static RadialGradientBrush CreateCornerBrush(double centerX, double centerY)
    {
        var brush = new RadialGradientBrush
        {
            Center = new Point(centerX, centerY),

            // 焦点も中心へ置く（既定は矩形の中央であり、そのままだと濃さが斜めへ伸びる）
            GradientOrigin = new Point(centerX, centerY),
            RadiusX = 1,
            RadiusY = 1,
        };

        foreach (var stop in ShadowPlan.CornerStops)
        {
            brush.GradientStops.Add(new GradientStop
            {
                Color = AppStyles.ToColor(stop.Argb),
                Offset = Math.Clamp(stop.Offset, 0, 1),
            });
        }

        return brush;
    }
}