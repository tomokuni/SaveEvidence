using EsUtil.Feature.MarqueeCore.Selection;
using EsUtil.Platform.Core.Geometry;
using Xunit;
using EsUtil.Feature.MarqueeCore.Interaction;

namespace EsUtil.Feature.MarqueeCore.Tests.Selection;

/// <summary><see cref="MarqueeSelectionGeometry"/> の単体テスト（純粋な幾何計算）。</summary>
/// <remarks>
/// 状態を持たない純粋関数（正規化・ハンドル位置・近傍判定・対角計算・境界クリップ・ドラッグ変形）のみを検証する。<br/>
/// UI フレームワークに依存せず、ヘッドレス実行を必要としないため <c>[Fact]</c> で並列検証できる。
/// </remarks>
public class MarqueeSelectionGeometryTests
{
    // ====================================================================
    // CreateFromPoints
    // ====================================================================

    [Fact]
    public void CreateFromPoints_始点と終点から正規化された矩形を生成する()
    {
        // 右下方向（通常）
        Assert.Equal(new EsRect(10, 20, 30, 40), MarqueeSelectionGeometry.CreateFromPoints(new EsPoint(10, 20), new EsPoint(40, 60)));
        // 左上方向（反転）
        Assert.Equal(new EsRect(10, 20, 30, 40), MarqueeSelectionGeometry.CreateFromPoints(new EsPoint(40, 60), new EsPoint(10, 20)));
        // X のみ反転
        Assert.Equal(new EsRect(10, 20, 30, 40), MarqueeSelectionGeometry.CreateFromPoints(new EsPoint(40, 20), new EsPoint(10, 60)));
        // Y のみ反転
        Assert.Equal(new EsRect(10, 20, 30, 40), MarqueeSelectionGeometry.CreateFromPoints(new EsPoint(10, 60), new EsPoint(40, 20)));
    }

    // ====================================================================
    // GetHandlePosition
    // ====================================================================

    [Fact]
    public void GetHandlePosition_8方向ハンドルとMoveの位置を返す()
    {
        var rect = new EsRect(10, 20, 100, 80); // Right=110 Bottom=100 Center=(60,60)

        Assert.Equal(new EsPoint(10, 20), MarqueeSelectionGeometry.GetHandlePosition(rect, HitTarget.TopLeft));
        Assert.Equal(new EsPoint(60, 20), MarqueeSelectionGeometry.GetHandlePosition(rect, HitTarget.TopCenter));
        Assert.Equal(new EsPoint(110, 20), MarqueeSelectionGeometry.GetHandlePosition(rect, HitTarget.TopRight));
        Assert.Equal(new EsPoint(10, 60), MarqueeSelectionGeometry.GetHandlePosition(rect, HitTarget.MiddleLeft));
        Assert.Equal(new EsPoint(110, 60), MarqueeSelectionGeometry.GetHandlePosition(rect, HitTarget.MiddleRight));
        Assert.Equal(new EsPoint(10, 100), MarqueeSelectionGeometry.GetHandlePosition(rect, HitTarget.BottomLeft));
        Assert.Equal(new EsPoint(60, 100), MarqueeSelectionGeometry.GetHandlePosition(rect, HitTarget.BottomCenter));
        Assert.Equal(new EsPoint(110, 100), MarqueeSelectionGeometry.GetHandlePosition(rect, HitTarget.BottomRight));
        Assert.Equal(new EsPoint(60, 60), MarqueeSelectionGeometry.GetHandlePosition(rect, HitTarget.Move));
    }

    [Fact]
    public void GetHandlePosition_未知のハンドルは右下隅を返す()
    {
        var rect = new EsRect(10, 20, 100, 80);

        Assert.Equal(rect.BottomRight, MarqueeSelectionGeometry.GetHandlePosition(rect, (HitTarget)999));
    }

    // ====================================================================
    // IsNear
    // ====================================================================

    [Fact]
    public void IsNear_半径以内はtrueを返す()
    {
        Assert.True(MarqueeSelectionGeometry.IsNear(new EsPoint(10, 10), new EsPoint(15, 12), 5.0));
    }

    [Fact]
    public void IsNear_半径を超えるとfalseを返す()
    {
        Assert.False(MarqueeSelectionGeometry.IsNear(new EsPoint(10, 10), new EsPoint(16, 12), 5.0));
        // X は範囲内・Y が範囲外
        Assert.False(MarqueeSelectionGeometry.IsNear(new EsPoint(10, 10), new EsPoint(12, 16), 5.0));
    }

    // ====================================================================
    // GetOppositeCorner
    // ====================================================================

    [Fact]
    public void GetOppositeCorner_始点と反対側の隅を返す()
    {
        var rect = new EsRect(10, 20, 100, 80);

        // 始点が左上（rect.X, rect.Y に一致）→ 右下（Right, Bottom）
        Assert.Equal(new EsPoint(110, 100), MarqueeSelectionGeometry.GetOppositeCorner(rect, new EsPoint(10, 20)));
        // 始点が右上（rect.Right, rect.Y に一致）→ 左下（X, Bottom）
        Assert.Equal(new EsPoint(10, 100), MarqueeSelectionGeometry.GetOppositeCorner(rect, new EsPoint(110, 20)));
        // 始点が左下（rect.X, rect.Bottom に一致）→ 右上（Right, Y）
        Assert.Equal(new EsPoint(110, 20), MarqueeSelectionGeometry.GetOppositeCorner(rect, new EsPoint(10, 100)));
        // 始点が右下（rect.Right, rect.Bottom に一致）→ 左上（X, Y）
        Assert.Equal(new EsPoint(10, 20), MarqueeSelectionGeometry.GetOppositeCorner(rect, new EsPoint(110, 100)));
    }

    // ====================================================================
    // ClampPointToBounds
    // ====================================================================

    [Fact]
    public void ClampPointToBounds_境界内へクリップする()
    {
        var bounds = new EsRect(0, 0, 200, 100);

        Assert.Equal(new EsPoint(10, 10), MarqueeSelectionGeometry.ClampPointToBounds(new EsPoint(10, 10), bounds));
        // 左端・上端を超える → 0 にクランプ
        Assert.Equal(new EsPoint(0, 0), MarqueeSelectionGeometry.ClampPointToBounds(new EsPoint(-5, -5), bounds));
        // 右端・下端を超える → 境界へクランプ
        Assert.Equal(new EsPoint(200, 100), MarqueeSelectionGeometry.ClampPointToBounds(new EsPoint(250, 120), bounds));
    }

    // ====================================================================
    // ClampRectToBounds（ハンドル固定）
    // ====================================================================

    [Fact]
    public void ClampRectToBounds_Moveは矩形全体を境界内へシフトする()
    {
        var bounds = new EsRect(0, 0, 200, 100);
        // 左下方向へはみ出した矩形（Move 中）→ 境界内へシフト
        var rect = new EsRect(180, 80, 50, 40); // Right=230 Bottom=120

        var result = MarqueeSelectionGeometry.ClampRectToBounds(rect, bounds, HitTarget.Move);

        Assert.Equal(new EsRect(150, 60, 50, 40), result); // サイズは維持・シフトのみ
    }

    [Fact]
    public void ClampRectToBounds_ハンドル変形は固定角を維持して辺のみクランプする()
    {
        var bounds = new EsRect(0, 0, 200, 100);
        // TopLeft ハンドルが左端・上端を超える → 固定角（右下）を維持し、左・上のみクランプ
        var rect = new EsRect(-20, -10, 100, 80); // Right=80 Bottom=70

        var result = MarqueeSelectionGeometry.ClampRectToBounds(rect, bounds, HitTarget.TopLeft);

        Assert.Equal(new EsRect(0, 0, 80, 70), result);
    }

    // ====================================================================
    // ComputeDragRect
    // ====================================================================

    [Fact]
    public void ComputeDragRect_TopLeftハンドルで変形する()
    {
        // 元矩形 (10,20,100,80)、TopLeft を (dx=30, dy=10) 移動 → l=40, t=30, r=110, b=100 → (40,30,70,70)
        var result = MarqueeSelectionGeometry.ComputeDragRect(new EsRect(10, 20, 100, 80), 30, 10, HitTarget.TopLeft, 10);
        Assert.Equal(new EsRect(40, 30, 70, 70), result);
    }

    [Fact]
    public void ComputeDragRect_各辺ハンドルは1軸のみ変形する()
    {
        var rect = new EsRect(10, 20, 100, 80);

        // TopCenter は上辺のみ、MiddleLeft は左辺のみを動かす
        Assert.Equal(new EsRect(10, 30, 100, 70), MarqueeSelectionGeometry.ComputeDragRect(rect, 0, 10, HitTarget.TopCenter, 10));
        Assert.Equal(new EsRect(20, 20, 90, 80), MarqueeSelectionGeometry.ComputeDragRect(rect, 10, 0, HitTarget.MiddleLeft, 10));
        Assert.Equal(new EsRect(10, 20, 110, 80), MarqueeSelectionGeometry.ComputeDragRect(rect, 10, 0, HitTarget.MiddleRight, 10));
        Assert.Equal(new EsRect(10, 20, 100, 90), MarqueeSelectionGeometry.ComputeDragRect(rect, 0, 10, HitTarget.BottomCenter, 10));
    }

    [Fact]
    public void ComputeDragRect_Moveで全体を平行移動する()
    {
        var result = MarqueeSelectionGeometry.ComputeDragRect(new EsRect(10, 20, 100, 80), 5, -10, HitTarget.Move, 10);
        Assert.Equal(new EsRect(15, 10, 100, 80), result);
    }

    [Fact]
    public void ComputeDragRect_反対側までドラッグすると矩形が反転される()
    {
        // TopLeft を右へ移動し l > r になる → 反転（正規化）
        var result = MarqueeSelectionGeometry.ComputeDragRect(new EsRect(10, 20, 100, 80), 200, 0, HitTarget.TopLeft, 10);
        // l=210, r=110 → x1=110, x2=210
        Assert.Equal(new EsRect(110, 20, 100, 80), result);
    }

    [Fact]
    public void ComputeDragRect_最小サイズ未満は拡張される()
    {
        // BottomRight を (dx=-95, dy=-95): r=15, b=5, l=10, t=20 → x1=10,x2=15(幅5)→最小10へ、y1=5,y2=20(高さ15)維持
        var result = MarqueeSelectionGeometry.ComputeDragRect(new EsRect(10, 20, 100, 80), -95, -95, HitTarget.BottomRight, 10);
        Assert.Equal(new EsRect(10, 5, 10, 15), result);
    }

    [Fact]
    public void ComputeDragRect_最小サイズの指定が1未満でも1に補正される()
    {
        // 最小サイズ 0 の指定は下限 1.0 に補正され、幅・高さは 1 未満にならない
        var result = MarqueeSelectionGeometry.ComputeDragRect(new EsRect(10, 20, 0.2, 0.2), 0, 0, HitTarget.Move, 0);

        Assert.Equal(1.0, result.Width);
        Assert.Equal(1.0, result.Height);
    }

    // ====================================================================
    // ClampRectToDomainBounds（四隅の包絡矩形）
    // ====================================================================

    [Fact]
    public void ClampRectToDomainBounds_矩形を境界内へクリップする()
    {
        // 境界 200x100 に対し、はみ出した矩形を包絡矩形でクリップ
        var result = MarqueeSelectionGeometry.ClampRectToDomainBounds(new EsRect(-10, -20, 150, 80), new EsSize(200, 100));
        // l=0, t=0, r=140, b=60 → (0,0,140,60)
        Assert.Equal(new EsRect(0, 0, 140, 60), result);
    }

    [Fact]
    public void ClampRectToDomainBounds_完全に外側の矩形は空矩形になる()
    {
        // 右下が境界外（左上も境界外）→ 包絡矩形は幅・高さ 0 になる
        var result = MarqueeSelectionGeometry.ClampRectToDomainBounds(new EsRect(300, 300, 50, 50), new EsSize(200, 100));

        Assert.Equal(new EsRect(200, 100, 0, 0), result);
    }

    // ====================================================================
    // DirectionalHandles（描画対象のハンドル一覧）
    // ====================================================================

    [Fact]
    public void DirectionalHandles_8方向のみを宣言順で持つ()
    {
        // None / Move を除く 8 方向
        Assert.Equal(
            [
                HitTarget.TopLeft, HitTarget.TopCenter, HitTarget.TopRight,
                HitTarget.MiddleLeft, HitTarget.MiddleRight,
                HitTarget.BottomLeft, HitTarget.BottomCenter, HitTarget.BottomRight,
            ],
            MarqueeSelectionGeometry.DirectionalHandles);
    }

    // ====================================================================
    // ToCursorKind（ハンドル → カーソル形状の種別）
    // ====================================================================

    [Fact]
    public void ToCursorKind_対角と辺のハンドルが同じ種別へ解決する()
    {
        // 対角のハンドルは同じ向きのカーソル（左上 = 右下 / 右上 = 左下）
        Assert.Equal(MarqueeCursorKind.DiagonalNWSE, MarqueeSelectionGeometry.ToCursorKind(HitTarget.TopLeft));
        Assert.Equal(MarqueeCursorKind.DiagonalNWSE, MarqueeSelectionGeometry.ToCursorKind(HitTarget.BottomRight));
        Assert.Equal(MarqueeCursorKind.DiagonalNESW, MarqueeSelectionGeometry.ToCursorKind(HitTarget.TopRight));
        Assert.Equal(MarqueeCursorKind.DiagonalNESW, MarqueeSelectionGeometry.ToCursorKind(HitTarget.BottomLeft));

        // 辺のハンドルは向きごとのカーソル
        Assert.Equal(MarqueeCursorKind.Vertical, MarqueeSelectionGeometry.ToCursorKind(HitTarget.TopCenter));
        Assert.Equal(MarqueeCursorKind.Vertical, MarqueeSelectionGeometry.ToCursorKind(HitTarget.BottomCenter));
        Assert.Equal(MarqueeCursorKind.Horizontal, MarqueeSelectionGeometry.ToCursorKind(HitTarget.MiddleLeft));
        Assert.Equal(MarqueeCursorKind.Horizontal, MarqueeSelectionGeometry.ToCursorKind(HitTarget.MiddleRight));

        // 矩形内は移動
        Assert.Equal(MarqueeCursorKind.Move, MarqueeSelectionGeometry.ToCursorKind(HitTarget.Move));
    }

    [Fact]
    public void ToCursorKind_ハンドル外は既定の種別を返す()
        => Assert.Equal(MarqueeCursorKind.Default, MarqueeSelectionGeometry.ToCursorKind(HitTarget.None));

    [Fact]
    public void ToCursorKind_全ハンドルが既定以外の種別へ解決する()
    {
        // None 以外の全ハンドルが Default 以外へ解決する（マッピング漏れの検出）
        foreach (var handle in Enum.GetValues<HitTarget>())
        {
            if (handle == HitTarget.None)
            {
                continue;
            }

            Assert.NotEqual(MarqueeCursorKind.Default, MarqueeSelectionGeometry.ToCursorKind(handle));
        }
    }
}
