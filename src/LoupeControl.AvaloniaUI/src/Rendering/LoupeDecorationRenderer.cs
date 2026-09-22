using Avalonia.Media;
using EsUtil.Platform.AvaloniaUI;
using Estable.Loupe;
using EsUtil.Platform.Core.Geometry;

namespace Estable.AvaloniaUI.Loupe.Rendering;

/// <summary>ルーペの十字線・外枠を描画します。</summary>
/// <remarks>
/// 矩形計算は UI 非依存の <see cref="LoupeDecorationGeometry"/> が単一所有し、<br/>
/// 本クラスは <see cref="EsRect"/> を Avalonia の矩形へ変換して描画するだけにする（MewUI と共用）。<br/>
/// 描画の可否（透明な色は描画しない）はコアの <see cref="LoupeAppearance"/> に問い合わせるため、<br/>
/// 本クラスは判断を持たず、描画入力（<see cref="LoupeRenderRequest"/>）から描くだけにする<br/>
/// （MewUI 版の描画層と同じ形）。
/// </remarks>
internal static class LoupeDecorationRenderer
{
    /// <summary>十字線を描画する（拡大中心の十字）。</summary>
    /// <param name="context">描画コンテキスト。</param>
    /// <param name="request">描画入力（物理ピクセルサイズ・拡大率・外観）。</param>
    /// <remarks>描画の可否は <see cref="LoupeAppearance.HasCrosshair"/>（コア）が単一所有する。</remarks>
    public static void DrawCrosshair(DrawingContext context, LoupeRenderRequest request)
    {
        var appearance = request.Appearance;
        if (!appearance.HasCrosshair)
        {
            return;
        }

        var brush = LoupeBrushes.Get(appearance.CrossColor);
        foreach (var rect in LoupeDecorationGeometry.ComputeCrosshairRects(request.PixelSize, request.ZoomLevel))
        {
            context.DrawRectangle(brush, null, rect.ToAvaloniaRect());
        }
    }

    /// <summary>外枠を描画する（ルーペの四辺）。</summary>
    /// <param name="context">描画コンテキスト。</param>
    /// <param name="request">描画入力（物理ピクセルサイズ・外観・スケーリング）。</param>
    /// <remarks>
    /// 描画の可否は <see cref="LoupeAppearance.HasFrame"/>（コア）が単一所有し、<br/>
    /// 太さの補正と DIP → 物理ピクセルの換算は <see cref="LoupeAppearanceScaler"/>（コア）が行う。
    /// </remarks>
    public static void DrawFrame(DrawingContext context, LoupeRenderRequest request)
    {
        var appearance = request.Appearance;
        if (!appearance.HasFrame)
        {
            return;
        }

        var width = LoupeAppearanceScaler.ScaleFrameWidth(appearance, request.Scaling);
        var brush = LoupeBrushes.Get(appearance.FrameColor);
        foreach (var rect in LoupeDecorationGeometry.ComputeFrameRects(request.PixelSize, width))
        {
            context.DrawRectangle(brush, null, rect.ToAvaloniaRect());
        }
    }
}
