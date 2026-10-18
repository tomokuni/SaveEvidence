using EsUtil.SaveEvidence.App.Core.Services;
using CropControl = EsUtil.SaveEvidence.Parts.CapturePreview.WinUI3.Crop.CropViewControl;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace EsUtil.SaveEvidence.App.WinUI3.Views;

/// <summary>キャプチャページの画像の 4 辺へ影を描く重ね合わせ（WinUI 3 実装）。</summary>
/// <remarks>
/// <b>適用パターン</b>: 描画層（画像の外側へ影を描くだけの重ね合わせ）。<br/>
/// 影の幅・濃さの変化は <see cref="ShadowPlan"/>（App.Core）が単一所有するため、本クラスは<br/>
/// 「計画の 4 辺の矩形を、計画の停止点のグラデーションで塗る」手順だけを持つ（4 UI 版で同じ結果になる）。<br/>
/// <b>画像の上へ重ねてよい理由</b>: 計画の辺の矩形は画像の外側にだけ置かれる（画像へ掛からない）。<br/>
/// <b>追従</b>: ズーム・パン・ウィンドウの大きさの変更で画像の表示矩形が変わるため、<br/>
/// <see cref="CropControl.ViewportChanged"/> を購読して位置を更新する。
/// </remarks>
internal sealed class ImageShadowOverlay : Canvas
{
    /// <summary>辺ごとの帯（4 辺）。</summary>
    private readonly List<Border> _parts = [];

    /// <summary>影を落とす画像の表示矩形を提供するコントロール。</summary>
    private CropControl? _crop;

    /// <summary>重ね合わせを初期化する（マウス操作は下のコントロールへ通す）。</summary>
    public ImageShadowOverlay()
    {
        IsHitTestVisible = false;

        foreach (var edge in ShadowPlan.Edges)
        {
            var part = new Border
            {
                Background = CreateBrush(edge),
                IsHitTestVisible = false,
            };
            _parts.Add(part);
            Children.Add(part);
        }
    }

    /// <summary>影を落とす対象のコントロールを設定する。</summary>
    /// <param name="crop">画像を表示するコントロール。</param>
    public void Attach(CropControl crop)
    {
        ArgumentNullException.ThrowIfNull(crop);

        _crop = crop;
        crop.ViewportChanged += (_, _) => Update();
        Update();
    }

    /// <summary>画像の表示矩形へ影の位置を合わせる。</summary>
    private void Update()
    {
        if (_crop is null)
        {
            return;
        }

        var image = _crop.ImageRectViewport();
        var fills = ShadowPlan.BuildEdgeRects(image.X, image.Y, image.Width, image.Height);
        for (var i = 0; i < _parts.Count && i < fills.Count; i++)
        {
            var fill = fills[i];
            var part = _parts[i];
            SetLeft(part, fill.X);
            SetTop(part, fill.Y);
            part.Width = fill.Width;
            part.Height = fill.Height;
        }
    }

    /// <summary>影の 1 辺分のグラデーションを生成する（位置 0 が外側）。</summary>
    /// <param name="edge">辺の計画。</param>
    /// <returns>グラデーションのブラシ。</returns>
    private static LinearGradientBrush CreateBrush(ShadowEdgePlan edge)
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
