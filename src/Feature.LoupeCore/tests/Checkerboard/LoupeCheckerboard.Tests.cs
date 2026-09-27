using EsUtil.Feature.LoupeCore.Checkerboard;
using Xunit;

namespace EsUtil.Feature.LoupeCore.Tests.Checkerboard;

/// <summary><see cref="LoupeCheckerboard"/> の純粋計算テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 市松模様の明暗の判定規則を直接検証する（列挙子（<see cref="Drawing.LoupeCheckerTiles"/>）経由の検証は<br/>
/// <see cref="Drawing.LoupeCheckerTilesTests"/> が担うため、本テストは判定そのものの境界を固定する）。<br/>
/// 判定は描画のたびにタイルごとに呼ばれるため、規則が変わると両 UI の見た目が同時に変わる。
/// </remarks>
public class LoupeCheckerboardTests
{
    /// <summary>列と行の和が奇数なら暗色になると判定されることを検証する。</summary>
    /// <param name="col">タイルの列インデックス。</param>
    /// <param name="row">タイルの行インデックス。</param>
    /// <param name="expected">暗色（色 B）と判定されるかどうか。</param>
    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(1, 0, true)]
    [InlineData(0, 1, true)]
    [InlineData(1, 1, false)]
    public void IsDarkTile_列と行の和が奇数なら暗色を返す(int col, int row, bool expected)
        => Assert.Equal(expected, LoupeCheckerboard.IsDarkTile(col, row));

    /// <summary>列と行を入れ替えても同じ判定になることを検証する。</summary>
    /// <param name="col">タイルの列インデックス。</param>
    /// <param name="row">タイルの行インデックス。</param>
    /// <remarks>和で判定するため対角線に対して対称になる（タイルの走査方向を変えても同じ模様になる）。</remarks>
    [Theory]
    [InlineData(0, 3)]
    [InlineData(3, 0)]
    [InlineData(4, 11)]
    public void IsDarkTile_列と行を入れ替えても同じ判定になる(int col, int row)
        => Assert.Equal(LoupeCheckerboard.IsDarkTile(col, row), LoupeCheckerboard.IsDarkTile(row, col));

    /// <summary>隣接するタイルが必ず交互の明暗になることを検証する。</summary>
    /// <param name="col">基準にするタイルの列インデックス。</param>
    /// <param name="row">基準にするタイルの行インデックス。</param>
    /// <remarks>縦横の隣はともにインデックスの和の奇偶が反転するため、明暗も必ず反転する（市松模様の前提）。</remarks>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(5, 8)]
    public void IsDarkTile_隣接するタイルは交互になる(int col, int row)
    {
        var current = LoupeCheckerboard.IsDarkTile(col, row);

        Assert.NotEqual(current, LoupeCheckerboard.IsDarkTile(col + 1, row));
        Assert.NotEqual(current, LoupeCheckerboard.IsDarkTile(col, row + 1));
    }
}
