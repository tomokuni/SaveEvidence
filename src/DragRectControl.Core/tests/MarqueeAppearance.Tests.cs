using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace Estable.Marquee.Tests;

/// <summary><see cref="MarqueeAppearance"/> の単体テスト（共有外観値・座標ラベル配置・値補正）。</summary>
/// <remarks>
/// UI フレームワークに依存しないため <c>[Fact]</c> で並列検証できる。<br/>
/// 配置計算は両 UI が共通で使うため、本テストが単独でその仕様を担保する（各 UI のテストでは検証しない）。
/// </remarks>
public class MarqueeAppearanceTests
{
    // ====================================================================
    // ComputeCoordLabelPosition（カーソル座標ラベル配置・クランプ）
    // ====================================================================

    [Fact]
    public void 配置_カーソル右側のオフセット位置に配置する()
    {
        var pos = MarqueeAppearance.ComputeCoordLabelPosition(
            new EsPoint(100, 50), new EsSize(800, 600), 120, 24);

        // カーソル (100,50) + オフセット (8,8)
        Assert.Equal(new EsPoint(108, 58), pos);
    }

    [Fact]
    public void 配置_右端をはみ出す場合は領域内へクランプする()
    {
        var pos = MarqueeAppearance.ComputeCoordLabelPosition(
            new EsPoint(790, 50), new EsSize(800, 600), 120, 24);

        // x = 798 → クランプ: 800 - 120 = 680
        Assert.Equal(680, pos.X);
        Assert.Equal(58, pos.Y);
    }

    [Fact]
    public void 配置_下端をはみ出す場合は領域内へクランプする()
    {
        var pos = MarqueeAppearance.ComputeCoordLabelPosition(
            new EsPoint(100, 590), new EsSize(800, 600), 120, 24);

        // y = 598 → クランプ: 600 - 24 = 576
        Assert.Equal(108, pos.X);
        Assert.Equal(576, pos.Y);
    }

    [Fact]
    public void 配置_負座標でも0未満にならない()
    {
        var pos = MarqueeAppearance.ComputeCoordLabelPosition(
            new EsPoint(0, 0), new EsSize(800, 600), 120, 24);

        // x = 8, y = 8（負にならない）
        Assert.Equal(8, pos.X);
        Assert.Equal(8, pos.Y);
    }

    [Fact]
    public void 配置_クライアントサイズ0ではクランプしない()
    {
        var pos = MarqueeAppearance.ComputeCoordLabelPosition(
            new EsPoint(100, 50), new EsSize(0, 0), 120, 24);

        // サイズ 0 の軸はクランプ条件をスキップし、オフセットのみ
        Assert.Equal(new EsPoint(108, 58), pos);
    }

    [Fact]
    public void 配置_ラベルが領域より大きい場合は0にクランプする()
    {
        var pos = MarqueeAppearance.ComputeCoordLabelPosition(
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
        => Assert.Equal(expected, MarqueeAppearance.CoerceCoordLabelFontSize(input));

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void 補正_非数は既定値にする(double input)
        => Assert.Equal(
            MarqueeAppearance.CoordLabelFontSizeDefault,
            MarqueeAppearance.CoerceCoordLabelFontSize(input));

    // ====================================================================
    // DirectionalHandles（描画対象のハンドル一覧）
    // ====================================================================

    [Fact]
    public void ハンドル一覧_8方向のみを宣言順で持つ()
    {
        // None / Move を除く 8 方向
        Assert.Equal(
            [
                HitTarget.TopLeft, HitTarget.TopCenter, HitTarget.TopRight,
                HitTarget.MiddleLeft, HitTarget.MiddleRight,
                HitTarget.BottomLeft, HitTarget.BottomCenter, HitTarget.BottomRight,
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
        Assert.Equal(MarqueeCursorKind.DiagonalNWSE, Selection.SelectionGeometry.ToCursorKind(HitTarget.TopLeft));
        Assert.Equal(MarqueeCursorKind.DiagonalNWSE, Selection.SelectionGeometry.ToCursorKind(HitTarget.BottomRight));
        Assert.Equal(MarqueeCursorKind.DiagonalNESW, Selection.SelectionGeometry.ToCursorKind(HitTarget.TopRight));
        Assert.Equal(MarqueeCursorKind.DiagonalNESW, Selection.SelectionGeometry.ToCursorKind(HitTarget.BottomLeft));

        // 辺のハンドルは向きごとのカーソル
        Assert.Equal(MarqueeCursorKind.Vertical, Selection.SelectionGeometry.ToCursorKind(HitTarget.TopCenter));
        Assert.Equal(MarqueeCursorKind.Vertical, Selection.SelectionGeometry.ToCursorKind(HitTarget.BottomCenter));
        Assert.Equal(MarqueeCursorKind.Horizontal, Selection.SelectionGeometry.ToCursorKind(HitTarget.MiddleLeft));
        Assert.Equal(MarqueeCursorKind.Horizontal, Selection.SelectionGeometry.ToCursorKind(HitTarget.MiddleRight));

        // 矩形内は移動
        Assert.Equal(MarqueeCursorKind.Move, Selection.SelectionGeometry.ToCursorKind(HitTarget.Move));
    }

    [Fact]
    public void カーソル種別_ハンドル外は既定の種別を返す()
        => Assert.Equal(MarqueeCursorKind.Default, Selection.SelectionGeometry.ToCursorKind(HitTarget.None));

    [Fact]
    public void カーソル種別_全ハンドルが既定以外の種別へ解決する()
    {
        // None 以外の全ハンドルが Default 以外へ解決する（マッピング漏れの検出）
        foreach (var handle in Enum.GetValues<HitTarget>())
        {
            if (handle == HitTarget.None)
            {
                continue;
            }

            Assert.NotEqual(MarqueeCursorKind.Default, Selection.SelectionGeometry.ToCursorKind(handle));
        }
    }
}
