using EsUtil.SaveEvidence.Parts.CapturePreview.Core.ZoomView;
using Xunit;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.Tests.ZoomView;

/// <summary><see cref="ZoomViewAppearance"/> の仕様テスト（外観の単一所有と下地・スクロールバーの既定）。</summary>
/// <remarks>
/// 外観値（下地色・スクロールバーの太さと余白・色）と描画の可否（トラックを描くか）はコアが単一所有するため、<br/>
/// 既定と判定の意味を本テストで固定する（UI ごとに見た目がずれる事故の防止）。<br/>
/// 画像が無い部分は下地色の単色であり、市松模様は描かない（廃止済み）。<br/>
/// UI 初期化を必要としないため、Avalonia ヘッドレスに依存せず並列実行できる。
/// </remarks>
public class ZoomViewAppearanceTests
{
    [Fact]
    internal void Default_は下地とスクロールバーの既定を持つ()
    {
        var appearance = ZoomViewAppearance.Default;

        Assert.Equal(System.Drawing.Color.FromArgb(255, 32, 32, 32), appearance.BackgroundColor);
        Assert.Equal(ZoomViewAppearance.DefaultScrollBarThickness, appearance.ScrollBarThickness);
        Assert.Equal(ZoomViewAppearance.DefaultScrollBarMargin, appearance.ScrollBarMargin);
        Assert.True(appearance.ShowScrollBars);
    }

    [Fact]
    internal void HasScrollBarTrack_アルファ0なら描かない()
    {
        var appearance = ZoomViewAppearance.Default with
        {
            ScrollBarTrackColor = System.Drawing.Color.FromArgb(0, 255, 255, 255),
        };

        Assert.False(appearance.HasScrollBarTrack);
    }

    [Fact]
    internal void HasScrollBarTrack_アルファが0より大きければ描く()
    {
        var appearance = ZoomViewAppearance.Default with
        {
            ScrollBarTrackColor = System.Drawing.Color.FromArgb(40, 255, 255, 255),
        };

        Assert.True(appearance.HasScrollBarTrack);
    }

    [Fact]
    internal void HasScrollBarBorder_アルファ0なら描かない()
    {
        var appearance = ZoomViewAppearance.Default with
        {
            ScrollBarBorderColor = System.Drawing.Color.FromArgb(0, 160, 160, 160),
        };

        Assert.False(appearance.HasScrollBarBorder);
    }

    [Fact]
    internal void HasScrollBarBorder_太さ0なら描かない()
    {
        var appearance = ZoomViewAppearance.Default with { ScrollBarBorderThickness = 0 };

        Assert.False(appearance.HasScrollBarBorder);
    }

    [Fact]
    internal void HasScrollBarBorder_色と太さがあれば描く()
    {
        var appearance = ZoomViewAppearance.Default;

        Assert.True(appearance.HasScrollBarBorder);
        Assert.Equal(ZoomViewAppearance.DefaultScrollBarBorderThickness, appearance.ScrollBarBorderThickness);
    }

    /// <summary>枠の色がグレー（#808080）で、透過度が元の色（16% = アルファ 40）のままであることを検証する。</summary>
    /// <remarks>枠の目的は「単色のバーでは見えない画像の上でも輪郭で視認できる」ことである。</remarks>
    [Fact]
    internal void 枠の色はグレーで透過度は元のまま()
    {
        var appearance = ZoomViewAppearance.Default;

        Assert.Equal(System.Drawing.Color.FromArgb(40, 128, 128, 128), appearance.ScrollBarBorderColor);
    }

    /// <summary>市松模様の設定が公開されていないことを検証する（廃止の固定）。</summary>
    /// <remarks>
    /// 画像が無い部分は下地色の単色で塗る（ルーペのペーストボードと同じ扱い）。<br/>
    /// タイルの描画を復活させるときは、本テストを消してから設計を見直す。
    /// </remarks>
    [Fact]
    internal void 市松模様の設定を持たない()
    {
        var names = typeof(ZoomViewAppearance).GetProperties().Select(static p => p.Name);

        Assert.DoesNotContain(names, static name => name.Contains("Checker", StringComparison.Ordinal));
    }
}