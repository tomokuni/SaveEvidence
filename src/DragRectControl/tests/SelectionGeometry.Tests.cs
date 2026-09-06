using Estable.AvaloniaUI.Platform.Geometry;
using Xunit;

namespace Estable.AvaloniaUI.DragRect.Tests;

/// <summary><see cref="Selection.SelectionGeometry"/> の単体テスト（純粋な幾何計算）。</summary>
/// <remarks>
/// 状態を持たない純粋関数（正規化・ハンドル位置・ヒットテスト・対角計算）のみを検証するため、
/// Avalonia ヘッドレス実行を必要としない。並列実行を妨げないよう <c>[Fact]</c> で検証する。<br/>
/// 座標は UI フレームワーク非依存の <see cref="PointD"/> / <see cref="RectD"/> を使用する（テスト容易性・移植性向上）。
/// </remarks>
public class SelectionGeometryTests
{
    // ====================================================================
    // CreateFromPoints
    // ====================================================================

    [Fact]
    public void CreateFromPoints_始点と終点から正規化された矩形を生成する()
    {
        // 右下方向（通常）
        Assert.Equal(new RectD(10, 20, 30, 40), Selection.SelectionGeometry.CreateFromPoints(new PointD(10, 20), new PointD(40, 60)));
        // 左上方向（反転）
        Assert.Equal(new RectD(10, 20, 30, 40), Selection.SelectionGeometry.CreateFromPoints(new PointD(40, 60), new PointD(10, 20)));
        // X のみ反転
        Assert.Equal(new RectD(10, 20, 30, 40), Selection.SelectionGeometry.CreateFromPoints(new PointD(40, 20), new PointD(10, 60)));
        // Y のみ反転
        Assert.Equal(new RectD(10, 20, 30, 40), Selection.SelectionGeometry.CreateFromPoints(new PointD(10, 60), new PointD(40, 20)));
    }

    // ====================================================================
    // GetHandlePosition
    // ====================================================================

    [Fact]
    public void GetHandlePosition_8方向ハンドルとMoveの位置を返す()
    {
        var rect = new RectD(10, 20, 100, 80); // Left=10 Top=20 Right=110 Bottom=100 Center=(60,60)

        Assert.Equal(new PointD(10, 20), Selection.SelectionGeometry.GetHandlePosition(rect, DragHandle.TopLeft));
        Assert.Equal(new PointD(60, 20), Selection.SelectionGeometry.GetHandlePosition(rect, DragHandle.TopCenter));
        Assert.Equal(new PointD(110, 20), Selection.SelectionGeometry.GetHandlePosition(rect, DragHandle.TopRight));
        Assert.Equal(new PointD(10, 60), Selection.SelectionGeometry.GetHandlePosition(rect, DragHandle.MiddleLeft));
        Assert.Equal(new PointD(110, 60), Selection.SelectionGeometry.GetHandlePosition(rect, DragHandle.MiddleRight));
        Assert.Equal(new PointD(10, 100), Selection.SelectionGeometry.GetHandlePosition(rect, DragHandle.BottomLeft));
        Assert.Equal(new PointD(60, 100), Selection.SelectionGeometry.GetHandlePosition(rect, DragHandle.BottomCenter));
        Assert.Equal(new PointD(110, 100), Selection.SelectionGeometry.GetHandlePosition(rect, DragHandle.BottomRight));
        Assert.Equal(new PointD(60, 60), Selection.SelectionGeometry.GetHandlePosition(rect, DragHandle.Move));
    }

    [Fact]
    public void GetHandlePosition_未知のハンドルは右下隅を返す()
    {
        var rect = new RectD(10, 20, 100, 80);

        Assert.Equal(rect.BottomRight, Selection.SelectionGeometry.GetHandlePosition(rect, (DragHandle)999));
    }

    // ====================================================================
    // IsNear
    // ====================================================================

    [Fact]
    public void IsNear_半径以内はtrueを返す()
    {
        Assert.True(Selection.SelectionGeometry.IsNear(new PointD(10, 10), new PointD(15, 12), 5.0));
    }

    [Fact]
    public void IsNear_半径を超えるとfalseを返す()
    {
        Assert.False(Selection.SelectionGeometry.IsNear(new PointD(10, 10), new PointD(16, 12), 5.0));
        // X は範囲内・Y が範囲外
        Assert.False(Selection.SelectionGeometry.IsNear(new PointD(10, 10), new PointD(12, 16), 5.0));
    }

    // ====================================================================
    // GetOppositeCorner
    // ====================================================================

    [Fact]
    public void GetOppositeCorner_始点と反対側の隅を返す()
    {
        var rect = new RectD(10, 20, 100, 80);

        // 始点が左上（rect.X, rect.Y に一致）→ 右下（Right, Bottom）
        Assert.Equal(new PointD(110, 100), Selection.SelectionGeometry.GetOppositeCorner(rect, new PointD(10, 20)));
        // 始点が右上（rect.Right, rect.Y に一致）→ 左下（X, Bottom）
        Assert.Equal(new PointD(10, 100), Selection.SelectionGeometry.GetOppositeCorner(rect, new PointD(110, 20)));
        // 始点が左下（rect.X, rect.Bottom に一致）→ 右上（Right, Y）
        Assert.Equal(new PointD(110, 20), Selection.SelectionGeometry.GetOppositeCorner(rect, new PointD(10, 100)));
        // 始点が右下（rect.Right, rect.Bottom に一致）→ 左上（X, Y）
        Assert.Equal(new PointD(10, 20), Selection.SelectionGeometry.GetOppositeCorner(rect, new PointD(110, 100)));
    }
}
