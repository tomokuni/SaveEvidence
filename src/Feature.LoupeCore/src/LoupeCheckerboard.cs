using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;

namespace EsUtil.Feature.LoupeCore;

/// <summary>ルーペの市松模様のタイル配置ロジック（純粋関数）。</summary>
/// <remarks>
/// 市松模様の明暗判定とタイルの矩形を単一所有し、各 UI 実装が同一のパターンを描画できるようにする。<br/>
/// <b>タイルの矩形はリストで返さない</b>: タイル数は描画先の大きさに比例して増えるため（既定でも数百、<br/>
/// タイルサイズを小さくすると数万）、描画のたびにリストを確保するとヒープ確保がフレームごとに積み上がる。<br/>
/// 座標を進めながら矩形を返す構造体（<see cref="DarkTiles"/>）を返し、確保をゼロにする。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public static class LoupeCheckerboard
{
    /// <summary>指定したタイルが暗色（色B）かどうかを判定する。</summary>
    /// <param name="col">タイルの列インデックス（左から 0 始まり）。</param>
    /// <param name="row">タイルの行インデックス（上から 0 始まり）。</param>
    /// <returns>暗色（色B）なら true、明色（色A）なら false。</returns>
    /// <remarks>
    /// 交互パターン（<c>(col + row)</c> が奇数なら暗色）を判定する。<br/>
    /// 背景色（明色）を全面へ描画した後に暗色タイルのみを描くことで、描画回数を半減できる。
    /// </remarks>
    internal static bool IsDarkTile(int col, int row) => (col + row) % 2 == 1;

    /// <summary>暗色（色B）で描画するタイルの矩形を列挙する。</summary>
    /// <param name="pixelSize">ルーペの一辺のサイズ（物理ピクセル）。</param>
    /// <param name="tileSize">1 タイルの一辺（物理ピクセル。1 以上の値を渡す）。</param>
    /// <returns>暗色タイルの矩形を列挙する構造体（左上を原点とする物理ピクセル座標）。</returns>
    /// <remarks>
    /// タイル数の算出（切り上げ）と走査順を単一所有し、各 UI は返された矩形を塗るだけにする<br/>
    /// （十字線・外枠の <see cref="LoupeDecorationGeometry"/> と同じ形にする）。<br/>
    /// タイル座標で走査するため除算（<c>x / tileSize</c>）は発生しない。<br/>
    /// タイルサイズの安全域への補正は <see cref="LoupeMetrics"/>（生成時）が単一所有する。
    /// </remarks>
    public static DarkTiles ComputeDarkTiles(int pixelSize, int tileSize) => new(pixelSize, tileSize);

    /// <summary>暗色タイルの矩形を、リストを確保せずに列挙する。</summary>
    /// <remarks>
    /// <b>位置付け</b>: 描画層が返された矩形を順に塗るための列挙子である。<br/>
    /// 構造体のため <c>foreach</c> でもボックス化せず、<see cref="IEsCanvas.FillRects{TWalker}"/> へは<br/>
    /// 制約付きジェネリックとして渡せる（リスト確保もボックス化も発生しない）。<br/>
    /// <b>列挙は 1 回だけ</b>（内部に走査位置を持つため、同じ値で 2 回列挙すると 2 回目は空になる）。
    /// </remarks>
    public struct DarkTiles : IEsRectWalker<DarkTiles>
    {
        /// <summary>一辺のタイル数（切り上げ）。</summary>
        private readonly int _tiles;

        /// <summary>1 タイルの一辺（物理ピクセル）。</summary>
        private readonly int _tileSize;

        /// <summary>次に判定する列（左から 0 始まり）。</summary>
        private int _col;

        /// <summary>次に判定する行（上から 0 始まり）。</summary>
        private int _row;

        /// <summary>現在の矩形。</summary>
        private EsRect _current;

        /// <summary>列挙子を初期化する。</summary>
        /// <param name="pixelSize">ルーペの一辺のサイズ（物理ピクセル）。</param>
        /// <param name="tileSize">1 タイルの一辺（物理ピクセル。1 以上の値を渡す）。</param>
        internal DarkTiles(int pixelSize, int tileSize)
        {
            _tileSize = tileSize;
            // 一辺のタイル数は切り上げ（余りを 1 タイルとして数える。計算は IntMath が単一所有する）
            _tiles = IntMath.CeilDiv(pixelSize, tileSize);
            _col = 0;
            _row = 0;
            _current = default;
        }

        /// <summary>列挙する暗色タイルの総数を取得する。</summary>
        /// <value>一辺 <c>n</c> タイルの市松模様で暗色になるタイル数（<c>n² / 2</c> の整数除算）。</value>
        /// <remarks>走査位置に依存しない（列挙の前後を問わず同じ値）。</remarks>
        public readonly int Count => _tiles * _tiles / 2;

        /// <summary>現在の矩形を取得する。</summary>
        /// <value>直近の <see cref="MoveNext"/> が true を返したときの矩形。</value>
        public readonly EsRect Current => _current;

        /// <summary>列挙子自身を取得する（<c>foreach</c> をボックス化させない）。</summary>
        /// <returns>現在位置を持つ列挙子のコピー。列挙前の値に対して呼ぶと先頭から列挙する。</returns>
        public readonly DarkTiles GetEnumerator() => this;

        /// <summary>次の暗色タイルへ進む。</summary>
        /// <returns>次の暗色タイルがある場合は true。列の末尾を超えた場合は false。</returns>
        /// <remarks>明色のタイルは内部で読み飛ばす（呼び出し側は暗色タイルだけを受け取る）。</remarks>
        public bool MoveNext()
        {
            while (_row < _tiles)
            {
                var col = _col;
                var row = _row;

                // 次の位置へ進める（明色は読み飛ばす）
                if (++_col >= _tiles)
                {
                    _col = 0;
                    _row++;
                }

                if (IsDarkTile(col, row))
                {
                    _current = new EsRect(col * _tileSize, row * _tileSize, _tileSize, _tileSize);
                    return true;
                }
            }

            return false;
        }
    }
}
