using EsUtil.Platform.Core.Imaging;
using EsUtil.Platform.MewUI.Imaging;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Platform.MewUI.Tests;

/// <summary><see cref="SkiaImageView"/> のプロパティと継承のテスト。</summary>
/// <remarks>
/// MewUI の要素生成はプラットフォームホストの登録を必要としないため、UI を表示せずに検証できる。<br/>
/// 描画結果の検証は <see cref="SkiaImageRendererTests"/> が担う。
/// </remarks>
public class SkiaImageViewTests
{
    [Fact]
    public void Source_既定はnull()
    {
        var view = new SkiaImageView();

        Assert.Null(view.Source);
    }

    [Fact]
    public void Source_設定した値をそのまま返す()
    {
        var view = new SkiaImageView();
        using var bitmap = TestHelper.CreateBitmap(2, 2, DrawingColor.Red);

        view.Source = bitmap;

        Assert.Same(bitmap, view.Source);
    }

    [Fact]
    public void Source_派生クラスでも基底へ登録したプロパティを共有する()
    {
        // MewProperty を基底で登録するため、派生クラスのインスタンスでも同じプロパティとして扱えることを固定する
        var view = new UniformView();
        using var bitmap = TestHelper.CreateBitmap(2, 2, DrawingColor.Red);

        view.Source = bitmap;

        Assert.Same(bitmap, view.Source);
        Assert.True(view.UniformDrawExposed);
    }

    [Fact]
    public void Source_nullへ戻せる()
    {
        var view = new SkiaImageView();
        using var bitmap = TestHelper.CreateBitmap(2, 2, DrawingColor.Red);
        view.Source = bitmap;

        view.Source = null;

        Assert.Null(view.Source);
    }

    /// <summary>縦横比を保つ配置を選ぶ派生クラス（検証用に <see cref="UniformDraw"/> を公開する）。</summary>
    private sealed class UniformView : SkiaImageView
    {
        /// <summary>縦横比を保って収める。</summary>
        protected override bool UniformDraw => true;

        /// <summary>縦横比を保つ指定を公開する（検証用）。</summary>
        public bool UniformDrawExposed => UniformDraw;
    }
}
