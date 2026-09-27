using EsUtil.Platform.Core.Geometry;
using Xunit;
using EsUtil.Feature.LoupeCore.Drawing;
using EsUtil.Feature.LoupeCore.Setting;

namespace EsUtil.Feature.LoupeCore.Tests;

/// <summary><see cref="Loupe.ResolveContent"/>（1 フレーム分の描画内容の解決）のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 描く層の一覧と順序、テキストの有無の判定を検証する。<br/>
/// 描画実装は <see cref="LoupeDrawContent"/> の値を持つ項目を描画ポートへ写すだけであり、<br/>
/// 「どの層をどの順に描くか」の判断がコアにあることを本テストが担保する。
/// </remarks>
public class LoupeDrawContentTests
{
    /// <summary>描く層がペーストボードからテキストの順で返ることを検証する。</summary>
    [Fact]
    public void ResolveContent_描く層はペーストボードからテキストの順で返す()
    {
        var loupe = new Loupe() { CenterPoint = new EsPoint(50, 60) };
        var content = loupe.ResolveContent(loupe.CreateRequest(160));

        // 描画順はコアが単一所有する（UI 層は先頭から順に処理するだけ）
        Assert.Equal(
            [
                LoupeDrawLayer.Pasteboard,
                LoupeDrawLayer.Image,
                LoupeDrawLayer.Crosshair,
                LoupeDrawLayer.Border,
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

    /// <summary>十字線の非表示設定で十字線の描画内容が空になることを検証する。</summary>
    [Fact]
    public void ResolveContent_十字線の非表示設定で描画内容を空にする()
    {
        var loupe = new Loupe(LoupeOptions.Default with { ShowCrosshair = false })
        {
            CenterPoint = new EsPoint(50, 60),
        };

        var content = loupe.ResolveContent(loupe.CreateRequest(160));

        Assert.Empty(content.CrosshairRects);
    }

    /// <summary>枠線の非表示設定で枠線の描画内容が空になることを検証する。</summary>
    [Fact]
    public void ResolveContent_枠線の非表示設定で描画内容を空にする()
    {
        var loupe = new Loupe(LoupeOptions.Default with { ShowBorder = false })
        {
            CenterPoint = new EsPoint(50, 60),
        };

        var content = loupe.ResolveContent(loupe.CreateRequest(160));

        Assert.Empty(content.BorderRects);
    }

    /// <summary>既定では十字線・枠線の描画内容が返ることを検証する。</summary>
    [Fact]
    public void ResolveContent_既定では十字線と枠線の描画内容を返す()
    {
        var loupe = new Loupe() { CenterPoint = new EsPoint(50, 60) };

        var content = loupe.ResolveContent(loupe.CreateRequest(160));

        Assert.NotEmpty(content.CrosshairRects);
        Assert.NotEmpty(content.BorderRects);
    }

    /// <summary>非表示の項目が層の一覧から外れることを検証する。</summary>
    /// <param name="crosshair">十字線の表示可否。</param>
    /// <param name="border">枠線の表示可否。</param>
    [Theory]
    [InlineData(false, true, LoupeDrawLayer.Crosshair)]
    [InlineData(true, false, LoupeDrawLayer.Border)]
    public void ResolveContent_非表示の項目は層に含まれない(
        bool crosshair, bool border, LoupeDrawLayer hidden)
    {
        var options = LoupeOptions.Default with
        {
            ShowCrosshair = crosshair,
            ShowBorder = border,
        };
        var loupe = new Loupe(options) { CenterPoint = new EsPoint(50, 60) };

        var content = loupe.ResolveContent(loupe.CreateRequest(160));

        Assert.DoesNotContain(hidden, content.Layers);
    }

    /// <summary>ペーストボードは常に層に含まれる（描画面を置き換えて残像を残さない）ことを検証する。</summary>
    [Fact]
    public void ResolveContent_ペーストボードは常に層に含まれる()
    {
        var options = LoupeOptions.Default with
        {
            ShowCrosshair = false,
            ShowBorder = false,
            ShowCoordLabel = false,
        };
        var loupe = new Loupe(options);

        var content = loupe.ResolveContent(loupe.CreateRequest(160));

        // 透明色を指定した場合も描画面の消去が必要であるため、ペーストボードは常に含める
        Assert.Equal([LoupeDrawLayer.Pasteboard], content.Layers);
    }

    /// <summary>拡大画像が解決できない場合は層に含まれないことを検証する。</summary>
    [Fact]
    public void ResolveContent_拡大画像が無ければ層に含まれない()
    {
        // 拡大中心が未設定の場合は切り出し領域が解決されない
        var loupe = new Loupe();

        var content = loupe.ResolveContent(loupe.CreateRequest(160));

        Assert.DoesNotContain(LoupeDrawLayer.Image, content.Layers);
    }
}
