using EsUtil.Platform.Core.Geometry;

namespace Estable.Loupe;

/// <summary>ルーペの市松模様のタイル配置ロジック（純粋関数）。</summary>
/// <remarks>
/// 市松模様の明暗判定とタイルの矩形を単一所有し、各 UI（Avalonia / MewUI）が同一のパターンを描画できるようにする。<br/>
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

    /// <summary>暗色（色B）で描画するタイルの矩形を列挙する。</summary>
    /// <param name="pixelSize">ルーペの一辺のサイズ（物理ピクセル）。</param>
    /// <param name="tileSize">1 タイルの一辺（物理ピクセル。正の値）。</param>
    /// <returns>暗色タイルの矩形（左上を原点とする物理ピクセル座標）。</returns>
    /// <remarks>
    /// タイル数の算出（切り上げ）と走査順を単一所有し、各 UI は返された矩形を塗るだけにする<br/>
    /// （十字線・外枠の <see cref="LoupeDecorationGeometry"/> と同じ形にする）。<br/>
    /// タイル座標で走査するため除算（<c>x / tileSize</c>）は発生しない。
    /// </remarks>
    public static IReadOnlyList<EsRect> ComputeDarkTileRects(int pixelSize, int tileSize)
    {
        var tiles = (int)Math.Ceiling((double)pixelSize / tileSize);
        var rects = new List<EsRect>(tiles * tiles / 2 + 1);

        for (var row = 0; row < tiles; row++)
        {
            var y = row * tileSize;
            for (var col = 0; col < tiles; col++)
            {
                if (IsDarkTile(col, row))
                {
                    rects.Add(new EsRect(col * tileSize, y, tileSize, tileSize));
                }
            }
        }

        return rects;
    }
}
