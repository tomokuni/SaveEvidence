using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace Estable.DragRect.Tests;

/// <summary><see cref="DragRectAppearance"/> の単体テスト（共有外観値・座標ラベル配置・値補正）。</summary>
/// <remarks>
/// UI フレームワークに依存しないため <c>[Fact]</c> で並列検証できる。<br/>
/// 配置計算は両 UI が共通で使うため、本テストが単独でその仕様を担保する（各 UI のテストでは検証しない）。
/// </remarks>
public class DragRectAppearanceTests
{
    // ====================================================================
    // ComputeCoordLabelPosition（カーソル座標ラベル配置・クランプ）
    // ====================================================================

    [Fact]
    public void 配置_カーソル右側のオフセット位置に配置する()
    {
        var pos = DragRectAppearance.ComputeCoordLabelPosition(
            new EsPoint(100, 50), new EsSize(800, 600), 120, 24);

        // カーソル (100,50) + オフセット (8,8)
        Assert.Equal(new EsPoint(108, 58), pos);
    }

    [Fact]
    public void 配置_右端をはみ出す場合は領域内へクランプする()
    {
        var pos = DragRectAppearance.ComputeCoordLabelPosition(
            new EsPoint(790, 50), new EsSize(800, 600), 120, 24);

        // x = 798 → クランプ: 800 - 120 = 680
        Assert.Equal(680, pos.X);
        Assert.Equal(58, pos.Y);
    }

    [Fact]
    public void 配置_下端をはみ出す場合は領域内へクランプする()
    {
        var pos = DragRectAppearance.ComputeCoordLabelPosition(
            new EsPoint(100, 590), new EsSize(800, 600), 120, 24);

        // y = 598 → クランプ: 600 - 24 = 576
        Assert.Equal(108, pos.X);
        Assert.Equal(576, pos.Y);
    }

    [Fact]
    public void 配置_負座標でも0未満にならない()
    {
        var pos = DragRectAppearance.ComputeCoordLabelPosition(
            new EsPoint(0, 0), new EsSize(800, 600), 120, 24);

        // x = 8, y = 8（負にならない）
        Assert.Equal(8, pos.X);
        Assert.Equal(8, pos.Y);
    }

    [Fact]
    public void 配置_クライアントサイズ0ではクランプしない()
    {
        var pos = DragRectAppearance.ComputeCoordLabelPosition(
            new EsPoint(100, 50), new EsSize(0, 0), 120, 24);

        // サイズ 0 の軸はクランプ条件をスキップし、オフセットのみ
        Assert.Equal(new EsPoint(108, 58), pos);
    }

    [Fact]
    public void 配置_ラベルが領域より大きい場合は0にクランプする()
    {
        var pos = DragRectAppearance.ComputeCoordLabelPosition(
            new EsPoint(100, 50), new EsSize(50, 50), 120, 24);

        // ラベル幅 120 > 領域幅 50 → x = 0
        Assert.Equal(0, pos.X);
    }

    // ====================================================================
    // CoerceCoordLabelFontSize（フォントサイズの安全域補正）
    // ====================================================================

    [Theory]
    [InlineData(12.0, 12.0)]
    [InlineData(6.0, 6.0)]
    [InlineData(48.0, 48.0)]
    [InlineData(1.0, 6.0)]
    [InlineData(100.0, 48.0)]
    [InlineData(-5.0, 6.0)]
    public void 補正_範囲内は保持し範囲外は安全域へ丸める(double input, double expected)
        => Assert.Equal(expected, DragRectAppearance.CoerceCoordLabelFontSize(input));

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void 補正_非数は既定値にする(double input)
        => Assert.Equal(
            DragRectAppearance.CoordLabelFontSizeDefault,
            DragRectAppearance.CoerceCoordLabelFontSize(input));

    // ====================================================================
    // DirectionalHandles（描画対象のハンドル一覧）
    // ====================================================================

    [Fact]
    public void ハンドル一覧_8方向のみを宣言順で持つ()
    {
        // None / Move を除く 8 方向
        Assert.Equal(
            [
                DragHandle.TopLeft, DragHandle.TopCenter, DragHandle.TopRight,
                DragHandle.MiddleLeft, DragHandle.MiddleRight,
                DragHandle.BottomLeft, DragHandle.BottomCenter, DragHandle.BottomRight,
            ],
            Selection.SelectionGeometry.DirectionalHandles);
    }

    // ====================================================================
    // ToCursorKind（ハンドル → カーソル形状の種別）
    // ====================================================================

    [Fact]
    public void カーソル種別_対角と辺のハンドルが同じ種別へ解決する()
    {
        // 対角のハンドルは同じ向きのカーソル（左上 = 右下 / 右上 = 左下）
        Assert.Equal(DragCursorKind.DiagonalNWSE, Selection.SelectionGeometry.ToCursorKind(DragHandle.TopLeft));
        Assert.Equal(DragCursorKind.DiagonalNWSE, Selection.SelectionGeometry.ToCursorKind(DragHandle.BottomRight));
        Assert.Equal(DragCursorKind.DiagonalNESW, Selection.SelectionGeometry.ToCursorKind(DragHandle.TopRight));
        Assert.Equal(DragCursorKind.DiagonalNESW, Selection.SelectionGeometry.ToCursorKind(DragHandle.BottomLeft));

        // 辺のハンドルは向きごとのカーソル
        Assert.Equal(DragCursorKind.Vertical, Selection.SelectionGeometry.ToCursorKind(DragHandle.TopCenter));
        Assert.Equal(DragCursorKind.Vertical, Selection.SelectionGeometry.ToCursorKind(DragHandle.BottomCenter));
        Assert.Equal(DragCursorKind.Horizontal, Selection.SelectionGeometry.ToCursorKind(DragHandle.MiddleLeft));
        Assert.Equal(DragCursorKind.Horizontal, Selection.SelectionGeometry.ToCursorKind(DragHandle.MiddleRight));

        // 矩形内は移動
        Assert.Equal(DragCursorKind.Move, Selection.SelectionGeometry.ToCursorKind(DragHandle.Move));
    }

    [Fact]
    public void カーソル種別_ハンドル外は既定の種別を返す()
        => Assert.Equal(DragCursorKind.Default, Selection.SelectionGeometry.ToCursorKind(DragHandle.None));

    [Fact]
    public void カーソル種別_全ハンドルが既定以外の種別へ解決する()
    {
        // None 以外の全ハンドルが Default 以外へ解決する（マッピング漏れの検出）
        foreach (var handle in Enum.GetValues<DragHandle>())
        {
            if (handle == DragHandle.None)
            {
                continue;
            }

            Assert.NotEqual(DragCursorKind.Default, Selection.SelectionGeometry.ToCursorKind(handle));
        }
    }
}
