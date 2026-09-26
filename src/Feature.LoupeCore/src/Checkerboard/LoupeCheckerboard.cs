using EsUtil.Feature.LoupeCore.Drawing;

namespace EsUtil.Feature.LoupeCore.Checkerboard;

/// <summary>ルーペの市松模様の明暗パターン（純粋関数）。</summary>
/// <remarks>
/// タイルの明暗の判定規則を単一所有し、各 UI 実装が同一のパターンを描画できるようにする。<br/>
/// タイルの矩形は描画内容（<see cref="Loupe.ResolveContent"/> の結果）へ<b>リストを確保しない列挙子</b><br/>
/// （<see cref="LoupeCheckerTiles"/>）として含まれるため、描画実装は返された矩形を順に塗るだけである。<br/>
/// <b>可視性</b>: タイルの位置と個数はコアが解決して描画内容へ含めるため、UI が直接呼ぶ入口を持たない（internal）。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
internal static class LoupeCheckerboard
{
    /// <summary>指定したタイルが暗色（色 B）かどうかを判定する。</summary>
    /// <param name="col">タイルの列インデックス（左から 0 始まり）。</param>
    /// <param name="row">タイルの行インデックス（上から 0 始まり）。</param>
    /// <returns>暗色（色 B）なら true、明色（色 A）なら false。</returns>
    /// <remarks>
    /// 交互パターン（<c>(col + row)</c> が奇数なら暗色）を判定する。<br/>
    /// 明色で全面を塗った後に暗色のタイルのみを描くことで、描画回数を半減できる。
    /// </remarks>
    internal static bool IsDarkTile(int col, int row) => (col + row) % 2 == 1;
}
