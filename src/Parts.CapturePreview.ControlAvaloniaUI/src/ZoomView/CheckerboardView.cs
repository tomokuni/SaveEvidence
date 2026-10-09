using Avalonia.Controls;
using Avalonia.Media;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.ZoomView;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Imaging;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Interop;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.AvaloniaUI.ZoomView;

/// <summary>画像の透明部分の背景（市松模様）を描く内部ビュー。</summary>
/// <remarks>
/// <b>なぜ子要素か</b>: Avalonia の <see cref="Panel"/> は <c>Render</c> をシールしており、<br/>
/// 派生コントロールが背景の描画を差し替えられない（画像より先に描く層が必要である）。<br/>
/// そのため本ビューを最初の子として置き、画像を描く内部ビューポートより先に描かれるようにする。<br/>
/// <br/>
/// <b>描く範囲</b>: 市松模様は画像の透明部分の背景であるため、画像の表示矩形の内側に限る<br/>
/// （範囲とタイルの位置はコアの <see cref="ZoomViewAppearance"/> が単一所有する）。<br/>
/// 矩形の外（ペーストボード）は本ビューを透明のままにする（親の背景がそのまま見える）。<br/>
/// <br/>
/// 入力は受けない（<c>IsHitTestVisible</c> を false にする）。
/// </remarks>
internal sealed class CheckerboardView : Control
{
    /// <summary>描画に必要な値（外観・画像の表示矩形・ビューポート）の取得元。</summary>
    private readonly ZoomViewSession _session;

    /// <summary>描画の取得元を指定して初期化する。</summary>
    /// <param name="session">ズームの状態・計算を単一所有するセッション。</param>
    public CheckerboardView(ZoomViewSession session)
    {
        _session = session;
        IsHitTestVisible = false;
    }

    /// <summary>下地と市松模様を描画する（親の背景より後、画像より先に描かれる）。</summary>
    /// <param name="context">描画コンテキスト。</param>
    /// <remarks>
    /// 描画の重ね順は「暗色（B）で範囲を塗る → 明色（A）のタイルを市松位置へ重ねる」。<br/>
    /// 範囲とタイルの位置はコア（<see cref="ZoomViewAppearance.ComputeCheckerArea"/> / <see cref="ZoomViewAppearance.ComputeLightTileRects"/>）が単一所有する。
    /// </remarks>
    public override void Render(DrawingContext context)
    {
        var appearance = _session.Appearance;
        var area = appearance.ComputeCheckerArea(_session.ImageRect, _session.ViewportSize);
        if (area.IsEmpty)
        {
            return;
        }

        context.FillRectangle(
            SolidColorBrushCache.GetOrCreate(appearance.CheckerColorB),
            area.ToAvaloniaRect());

        var lightBrush = SolidColorBrushCache.GetOrCreate(appearance.CheckerColorA);
        foreach (var tile in appearance.ComputeLightTileRects(area))
        {
            context.FillRectangle(lightBrush, tile.ToAvaloniaRect());
        }
    }
}
