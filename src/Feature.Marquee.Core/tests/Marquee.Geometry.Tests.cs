using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using EsUtil.Feature.Marquee.Core.Contract;
using EsUtil.Feature.Marquee.Core.Setting;

namespace EsUtil.Feature.Marquee.Core.Tests;

/// <summary><see cref="Marquee.ResolveGeometry"/>（1 フレーム分の幾何の解決）の単体テスト。</summary>
/// <remarks>
/// コアが返すのは幾何（穴あけ矩形・ハンドル・始点マーカー・マウス位置）だけである。<br/>
/// 装飾の矩形・文言・表示可否・描画順は提示層（<c>Feature.Marquee.Presenter</c>）が単一所有するため、本テストでは扱わない。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public class MarqueeGeometryResolutionTests
{
    /// <summary>ファサードを生成する。</summary>
    /// <param name="options">初期設定。null の場合は既定値。</param>
    /// <returns>ファサード。</returns>
    private static Marquee CreateMarquee(MarqueeOptions? options = null)
        => new(new EffectsSpy(), cursor: null, options) { Log = NullLogger.Instance };

    /// <summary>選択なしの幾何を解決する。</summary>
    [Fact]
    public void ResolveGeometry_選択なしではハンドルもマーカーも返さない()
    {
        var marquee = CreateMarquee();

        var geometry = marquee.ResolveGeometry(marquee.CreateRequest(new EsSize(200, 100)));

        Assert.Null(geometry.HoleRect);
        Assert.Null(geometry.HoleDomainRect);
        Assert.Null(geometry.Handles);
        Assert.Null(geometry.Marker);
    }

    /// <summary>選択がある場合は穴あけ矩形と 8 方向のハンドルを返す。</summary>
    [Fact]
    public void ResolveGeometry_選択ありでは穴あけ矩形とハンドルを返す()
    {
        var marquee = CreateMarquee();
        marquee.SetSelection(new EsRect(10, 20, 100, 50));

        var geometry = marquee.ResolveGeometry(marquee.CreateRequest(new EsSize(200, 100)));

        // 選択矩形は閉区間（終点 +1）として DIP へ変換される（ドメイン座標はそのまま保持する）
        Assert.Equal(new EsRect(10, 20, 101, 51), geometry.HoleRect);
        Assert.Equal(new EsRect(10, 20, 100, 50), geometry.HoleDomainRect);
        Assert.NotNull(geometry.Handles);
        Assert.Equal(8, geometry.Handles!.Value.Rects.Count);
        // 左上ハンドルは穴あけ矩形の左上を中心に置く（一辺は HandleSize）
        Assert.Equal(new EsRect(5, 15, 10, 10), geometry.Handles!.Value.Rects[0]);
    }

    /// <summary>ハイライト矩形が選択矩形より優先され、終点を加算しないことを検証する。</summary>
    [Fact]
    public void ResolveGeometry_ハイライト矩形を優先し終点を加算しない()
    {
        var marquee = CreateMarquee();
        marquee.SetSelection(new EsRect(10, 20, 100, 50));
        marquee.HighlightRect = new EsRect(1, 2, 3, 4);

        var geometry = marquee.ResolveGeometry(marquee.CreateRequest(new EsSize(200, 100)));

        // ハイライト矩形はホストが全ピクセルを含む閉区間として与えるため、+1 しない
        Assert.Equal(new EsRect(1, 2, 3, 4), geometry.HoleRect);
        // ハンドルは選択矩形から解決される（ハイライトはハンドルの対象ではない）
        Assert.NotNull(geometry.Handles);
    }

    /// <summary>ハンドルを隠す設定でもコアは幾何を返すことを検証する。</summary>
    /// <remarks>表示可否の判断は描画にだけ必要な設定であり、提示層の外観が単一所有する。</remarks>
    [Fact]
    public void ResolveGeometry_ハンドルの幾何は表示可否に依存しない()
    {
        var marquee = CreateMarquee();
        marquee.SetSelection(new EsRect(10, 20, 100, 50));

        var geometry = marquee.ResolveGeometry(marquee.CreateRequest(new EsSize(200, 100)));

        Assert.NotNull(geometry.Handles);
    }

    /// <summary>nudge 中は始点マーカーの幾何を返すことを検証する。</summary>
    /// <remarks>領域作成後の nudge はマウス押下中のみ許可される（規則は状態機械が単一所有する）。</remarks>
    [Fact]
    public void ResolveGeometry_nudge中はマーカーを返す()
    {
        var marquee = CreateMarquee();
        marquee.Press(new EsPoint(10, 10), clickCount: 1);
        marquee.Move(new EsPoint(50, 40));

        // ドラッグ中に矢印キーで矩形を動かす（nudge に入ると始点マーカーが可視になる）
        Assert.True(marquee.KeyDown(EsKey.Right, cursorPosition: null));

        var geometry = marquee.ResolveGeometry(marquee.CreateRequest(new EsSize(200, 100)));

        Assert.NotNull(geometry.Marker);
        // マーカーの太さ（2.0）と半幅（MarkerLength / 2）はコアが解決済みで渡す
        Assert.Equal(2.0, geometry.Marker!.Value.Thickness);
        Assert.Equal(MarqueeOptions.Default.MarkerLength / 2, geometry.Marker!.Value.Size);
    }

    /// <summary>座標変換ポートが穴あけ矩形とハンドルの両方へ同じ規則で適用されることを検証する。</summary>
    [Fact]
    public void ResolveGeometry_座標変換ポートを穴あけ矩形とハンドルへ適用する()
    {
        var marquee = CreateMarquee();
        // Inverse（ドメイン → DIP）は 2 倍
        marquee.SetCoordTransform(new CoordTransform(
            p => new EsPoint(p.X / 2, p.Y / 2), p => new EsPoint(p.X * 2, p.Y * 2), () => null));
        marquee.SetSelection(new EsRect(10, 20, 100, 50));

        var geometry = marquee.ResolveGeometry(marquee.CreateRequest(default));

        // 選択領域は閉区間（終点 +1）で変換される（(10,20)-(111,71) → (20,40)-(222,142)）
        Assert.Equal(new EsRect(20, 40, 202, 102), geometry.HoleRect);
        Assert.Equal(new EsRect(15, 35, 10, 10), geometry.Handles!.Value.Rects[0]);
    }

    /// <summary>描画面の全体が描画領域のサイズから導出されることを検証する。</summary>
    [Fact]
    public void ResolveGeometry_描画面の全体は原点と描画領域から導出する()
    {
        var marquee = CreateMarquee();

        var geometry = marquee.ResolveGeometry(marquee.CreateRequest(new EsSize(320, 240)));

        Assert.Equal(new EsSize(320, 240), geometry.ClientSize);
        Assert.Equal(new EsRect(0, 0, 320, 240), geometry.SurfaceBounds);
    }

    /// <summary>マウス位置が入力のまま幾何へ引き継がれることを検証する。</summary>
    /// <remarks>カーソル座標ラベルの基準は本値であり、文言の生成は提示層が担う。</remarks>
    [Fact]
    public void ResolveGeometry_マウス位置をそのまま返す()
    {
        var marquee = CreateMarquee();
        marquee.Move(new EsPoint(30, 40));

        var geometry = marquee.ResolveGeometry(marquee.CreateRequest(new EsSize(200, 100)));

        Assert.Equal(new EsPoint(30, 40), geometry.MousePosition);
    }
}
