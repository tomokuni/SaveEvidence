namespace Estable.Loupe;

/// <summary>ルーペの市松模様のタイル配置ロジック（純粋関数）。</summary>
/// <remarks>
/// 市松模様の明暗判定を単一所有し、各 UI（Avalonia / MewUI）が同一のパターンを描画できるようにする。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
internal static class LoupeCheckerboard
{
    /// <summary>指定したタイルが暗色（色B）かどうかを判定する。</summary>
    /// <param name="col">タイルの列インデックス（左から 0 始まり）。</param>
    /// <param name="row">タイルの行インデックス（上から 0 始まり）。</param>
    /// <returns>暗色（色B）なら true、明色（色A）なら false。</returns>
    /// <remarks>
    /// 交互パターン（<c>(col + row)</c> が奇数なら暗色）を判定する。<br/>
    /// 背景色（明色）を全面へ描画した後に暗色タイルのみを描くことで、描画回数を半減できる。
    /// </remarks>
    public static bool IsDarkTile(int col, int row) => (col + row) % 2 == 1;
}
