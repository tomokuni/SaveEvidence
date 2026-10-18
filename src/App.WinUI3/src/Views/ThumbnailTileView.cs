using EsUtil.SaveEvidence.App.Core.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace EsUtil.SaveEvidence.App.WinUI3.Views;

/// <summary>一覧（フォルダビュー / Undo 一覧）のサムネイル 1 件。</summary>
/// <remarks>
/// <b>画像の周りは影だけ</b>: 枠線・下地・角丸は描かない（<see cref="ThumbnailTileMetrics"/> が単一所有する寸法に従う）。<br/>
/// <b>構成</b>: 外側の余白（ホバー時の帯。<see cref="ThumbnailTileMetrics.ItemPadding"/>）→ 画像の外側の余白
/// （<see cref="ThumbnailTileMetrics.ItemMargin"/>）→ 影 + 画像 → 名前の行。<br/>
/// <b>ホバー</b>: マウスが重なると薄いグレーの帯を出し、影を消す。 一覧（<see cref="ThumbnailList"/>）が<br/>
/// ポインターの位置から対象の項目を決めて <see cref="SetHover"/> を呼ぶ（子要素のヒット テストに依存しない）。<br/>
/// <b>影</b>: 画像の上へ半透明の塗りを置くと画像が隠れる（縮小キャンバスの内容は XAML 要素より背面に合成される）ため、<br/>
/// 画像の外側へ<b>ぼかしの濃さを写したグラデーション</b>を貼る（輪を重ねると縞に見える。 実測）。
/// </remarks>
internal sealed class ThumbnailTileView : Grid
{
    /// <summary>影の要素（ホバー中は消す）。</summary>
    private readonly List<FrameworkElement> _shadowParts = [];

    /// <summary>サムネイル 1 件分の要素を生成する。</summary>
    /// <param name="image">サムネイルの表示要素（幅を <paramref name="width"/> に固定して使う）。</param>
    /// <param name="caption">サムネイルの下へ表示する名前（ファイル名・「現在」等）。</param>
    /// <param name="width">サムネイルの幅（DIP）。</param>
    /// <param name="isCurrent">現在表示中の画像かどうか（true のとき名前を太字にする）。</param>
    public ThumbnailTileView(FrameworkElement image, string caption, double width, bool isCurrent)
    {
        Background = new SolidColorBrush(Colors.Transparent);

        var host = new Grid();
        AddShadowParts(host);
        host.Children.Add(image);
        AddCornerParts(host);

        var label = new TextBlock
        {
            Text = caption,
            FontSize = ThumbnailTileMetrics.LabelFontSize,
            Height = ThumbnailTileMetrics.LabelHeight,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 2,
            Padding = new Thickness(
                ThumbnailTileMetrics.LabelHorizontalPadding, 1, ThumbnailTileMetrics.LabelHorizontalPadding, 1),
            FontWeight = isCurrent ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
        };

        Children.Add(new StackPanel
        {
            Orientation = Orientation.Vertical,
            Width = width,
            HorizontalAlignment = HorizontalAlignment.Center,

            // 行内で下端を揃える（行の高さは行内の最大アイテム高さ）
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(ThumbnailTileMetrics.ItemPadding + ThumbnailTileMetrics.ItemMargin),
            Children = { host, label },
        });
    }

    /// <summary>マウスが重なっているかどうかに合わせて見た目を切り替える。</summary>
    /// <param name="isHovered">マウスが重なっているかどうか。</param>
    public void SetHover(bool isHovered)
    {
        Background = new SolidColorBrush(
            isHovered
                ? AppStyles.ToColor(ThumbnailTileMetrics.HoverBackground(AppStyles.IsDarkTheme))
                : Colors.Transparent);

        foreach (var part in _shadowParts)
        {
            part.Opacity = isHovered ? 0 : 1;
        }
    }

    /// <summary>画像の外側へ影のグラデーションを配置する。</summary>
    /// <param name="host">画像を置く要素。</param>
    /// <remarks>
    /// 辺の幅・濃さの変化は <see cref="ShadowPlan"/>（App.Core。 4 UI 版で共通）が単一所有する。<br/>
    /// 上辺と下辺は左右の角まで広げ（角の隙間を埋め）、左右の辺は画像の高さいっぱいに置く。
    /// </remarks>
    private void AddShadowParts(Grid host)
    {
        var blur = ShadowPlan.BlurRadius;

        foreach (var edge in ShadowPlan.Edges)
        {
            var margin = edge.Side switch
            {
                ShadowSide.Top => new Thickness(-blur, -edge.Extent, -blur, 0),
                ShadowSide.Bottom => new Thickness(-blur, 0, -blur, -edge.Extent),
                ShadowSide.Left => new Thickness(-edge.Extent, 0, 0, 0),
                _ => new Thickness(0, 0, -edge.Extent, 0),
            };

            AddPart(
                host,
                edge,
                margin,
                edge.Side switch
                {
                    ShadowSide.Top => VerticalAlignment.Top,
                    ShadowSide.Bottom => VerticalAlignment.Bottom,
                    _ => VerticalAlignment.Stretch,
                },
                edge.Side switch
                {
                    ShadowSide.Left => HorizontalAlignment.Left,
                    ShadowSide.Right => HorizontalAlignment.Right,
                    _ => HorizontalAlignment.Stretch,
                });
        }
    }

    /// <summary>画像の 4 隅へ影の放射グラデーションを配置する。</summary>
    /// <param name="host">画像を置く要素。</param>
    /// <remarks>
    /// 隅の影は「画像の隅の頂点を中心にした放射グラデーション」である（矩形の外側の点の距離は、<br/>
    /// 隅の付近では隅の頂点からの距離と一致する）。 幅・濃さは <see cref="ShadowPlan"/>（4 UI 版で共通）が<br/>
    /// 単一所有する。 隅は画像の外側へはみ出させる（負の余白）ため、画像には掛からない。
    /// </remarks>
    private void AddCornerParts(Grid host)
    {
        var size = ShadowPlan.BlurRadius;

        // 左上・右上・右下・左下（中心はそれぞれ画像の隅の頂点 = 隅の矩形の内側の角）
        AddCornerPart(host, new Thickness(-size, -size, 0, 0), HorizontalAlignment.Left, VerticalAlignment.Top, 1, 1);
        AddCornerPart(host, new Thickness(0, -size, -size, 0), HorizontalAlignment.Right, VerticalAlignment.Top, 0, 1);
        AddCornerPart(host, new Thickness(0, 0, -size, -size), HorizontalAlignment.Right, VerticalAlignment.Bottom, 0, 0);
        AddCornerPart(host, new Thickness(-size, 0, 0, -size), HorizontalAlignment.Left, VerticalAlignment.Bottom, 1, 0);
    }

    /// <summary>隅の影を 1 つ配置する。</summary>
    /// <param name="host">配置先。</param>
    /// <param name="margin">外側へはみ出させる余白（負の値）。</param>
    /// <param name="horizontalAlignment">横の配置。</param>
    /// <param name="verticalAlignment">縦の配置。</param>
    /// <param name="centerX">放射グラデーションの中心の X（0〜1）。</param>
    /// <param name="centerY">放射グラデーションの中心の Y（0〜1）。</param>
    private void AddCornerPart(
        Grid host,
        Thickness margin,
        HorizontalAlignment horizontalAlignment,
        VerticalAlignment verticalAlignment,
        double centerX,
        double centerY)
    {
        var part = new Border
        {
            Background = CreateCornerBrush(centerX, centerY),
            Width = ShadowPlan.BlurRadius,
            Height = ShadowPlan.BlurRadius,
            Margin = margin,
            HorizontalAlignment = horizontalAlignment,
            VerticalAlignment = verticalAlignment,
            IsHitTestVisible = false,
        };

        _shadowParts.Add(part);
        host.Children.Add(part);
    }

    /// <summary>隅の放射グラデーションを生成する。</summary>
    /// <param name="centerX">中心の X（0〜1）。</param>
    /// <param name="centerY">中心の Y（0〜1）。</param>
    /// <returns>放射グラデーションのブラシ。</returns>
    /// <remarks>中心は画像の隅の頂点であり、半径は隅の矩形いっぱい（相対 1）にする。</remarks>
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
    /// <summary>影の 1 辺分を配置する。</summary>
    /// <param name="host">配置先。</param>
    /// <param name="edge">辺の計画。</param>
    /// <param name="margin">外側へ広げる余白。</param>
    /// <param name="verticalAlignment">縦の配置。</param>
    /// <param name="horizontalAlignment">横の配置。</param>
    private void AddPart(
        Grid host,
        ShadowEdgePlan edge,
        Thickness margin,
        VerticalAlignment verticalAlignment,
        HorizontalAlignment horizontalAlignment)
    {
        var part = new Border
        {
            Background = CreateStrip(edge),
            Margin = margin,
            Width = edge.IsVertical ? double.NaN : edge.Extent,
            Height = edge.IsVertical ? edge.Extent : double.NaN,
            VerticalAlignment = verticalAlignment,
            HorizontalAlignment = horizontalAlignment,
            IsHitTestVisible = false,
        };

        _shadowParts.Add(part);
        host.Children.Add(part);
    }

    /// <summary>1 辺分のグラデーションを生成する。</summary>
    /// <param name="edge">辺の計画。</param>
    /// <returns>グラデーション。</returns>
    /// <remarks>濃さの停止点は <see cref="ShadowPlan"/>（4 UI 版で共通）が単一所有する。</remarks>
    private static LinearGradientBrush CreateStrip(ShadowEdgePlan edge)
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
}