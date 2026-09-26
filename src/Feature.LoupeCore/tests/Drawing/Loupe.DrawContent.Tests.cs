using EsUtil.Platform.Core.Geometry;
using Xunit;
using EsUtil.Feature.LoupeCore.Drawing;

namespace EsUtil.Feature.LoupeCore.Tests.Drawing;

/// <summary><see cref="Loupe.ResolveContent"/>（1 フレーム分の描画内容の解決）のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 描く層の一覧と順序、テキストの有無の判定を検証する。<br/>
/// 描画実装は <see cref="LoupeDrawContent"/> の値を持つ項目を描画ポートへ写すだけであり、<br/>
/// 「どの層をどの順に描くか」の判断がコアにあることを本テストが担保する。
/// </remarks>
public class LoupeDrawContentTests
{
    /// <summary>描く層が下地からテキストの順で返ることを検証する。</summary>
    [Fact]
    public void ResolveContent_描く層は下地からテキストの順で返す()
    {
        var loupe = new Loupe() { CenterPoint = new EsPoint(50, 60) };
        var content = loupe.ResolveContent(loupe.CreateRequest(160));

        // 描画順はコアが単一所有する（UI 層は先頭から順に処理するだけ）
        Assert.Equal(
            [
                LoupeDrawLayer.Background,
                LoupeDrawLayer.Checkerboard,
                LoupeDrawLayer.Image,
                LoupeDrawLayer.Crosshair,
                LoupeDrawLayer.Frame,
                LoupeDrawLayer.Label,
            ],
            content.Layers);

        // テキストの有無は HasText で判定できる（描く必要があるかどうかの判断はコアが持つ）
        Assert.True(content.HasText);
    }

    /// <summary>テキストが無ければ層に含まれないことを検証する。</summary>
    [Fact]
    public void ResolveContent_テキストが無ければ層に含まれない()
    {
        // 拡大中心が無ければ中心座標ラベルは表示されない
        var loupe = new Loupe();
        var content = loupe.ResolveContent(loupe.CreateRequest(160));

        Assert.Null(content.CoordLabelText);
        Assert.False(content.HasText);
        Assert.DoesNotContain(LoupeDrawLayer.Label, content.Layers);
    }

    /// <summary>層の一覧が共有される（描画のたびに組み立てない）ことを検証する。</summary>
    [Fact]
    public void ResolveContent_層の一覧を共有する()
    {
        var loupe = new Loupe() { CenterPoint = new EsPoint(50, 60) };

        var first = loupe.ResolveContent(loupe.CreateRequest(160));
        var second = loupe.ResolveContent(loupe.CreateRequest(160));

        // 層の順序は固定であり、描画のたびに一覧を組み立てない
        Assert.Same(first.Layers, second.Layers);
    }
}
