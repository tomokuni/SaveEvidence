using EsUtil.Platform.Core.Geometry;
using EsUtil.SaveEvidence.Common.Imaging;

using EsUtil.Feature.LoupeCore.Checkerboard;

namespace EsUtil.Feature.LoupeCore;

/// <summary>ルーペの市松模様の暗色タイルの矩形を、リストを確保せずに列挙する構造体。</summary>
/// <remarks>
/// <b>位置付け</b>: コアが解決した描画内容（<see cref="LoupeDrawContent.CheckerTiles"/>）の一部であり、<br/>
/// 描画実装は返された矩形を順に塗るだけである（タイルの位置と個数の計算を描画側に持たせない）。<br/>
/// <b>なぜ列挙子か</b>: タイル数は描画先の大きさに比例して増えるため（既定でも数百、タイルサイズを小さくすると数万）、<br/>
/// 描画のたびにリストを確保するとヒープ確保がフレームごとに積み上がる。<br/>
/// 構造体のため <c>foreach</c> でもボックス化せず、<see cref="IEsCanvas.FillRects{TWalker}"/> へは<br/>
/// 制約付きジェネリックとして渡せる（リスト確保もボックス化も発生しない）。<br/>
/// <b>列挙は 1 回だけ</b>（内部に走査位置を持つため、同じ値で 2 回列挙すると 2 回目は空になる）。<br/>
/// <b>既定値は空</b>（タイルなし = 描かない）であり、作成はコア（<see cref="Loupe.ResolveContent"/>）だけが行う。
/// </remarks>
public struct LoupeCheckerTiles : IEsRectWalker<LoupeCheckerTiles>
{
    /// <summary>一辺のタイル数（切り上げ。0 の場合は列挙する矩形がない）。</summary>
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
    /// <param name="pixelSize">ルーペの一辺のサイズ（物理ピクセル）。0 以下の場合は列挙する矩形がない。</param>
    /// <param name="tileSize">1 タイルの一辺（物理ピクセル）。1 以下の場合は列挙する矩形がない（0 除算を避ける）。</param>
    internal LoupeCheckerTiles(int pixelSize, int tileSize)
    {
        _tileSize = tileSize;
        _tiles = tileSize > 0 ? LoupeDivMath.CeilDiv(pixelSize, tileSize) : 0;
        _col = 0;
        _row = 0;
        _current = default;
    }

    /// <summary>列挙する暗色タイルの総数を取得する。</summary>
    /// <value>一辺 <c>n</c> タイルの市松模様で暗色になるタイル数（<c>n² / 2</c> の整数除算）。タイルなしの場合は 0。</value>
    /// <remarks>走査位置に依存しない（列挙の前後を問わず同じ値）。</remarks>
    public readonly int Count => _tiles * _tiles / 2;

    /// <summary>現在の矩形を取得する。</summary>
    /// <value>直近の <see cref="MoveNext"/> が true を返したときの矩形。</value>
    public readonly EsRect Current => _current;

    /// <summary>列挙子自身を取得する（<c>foreach</c> をボックス化させない）。</summary>
    /// <returns>現在位置を持つ列挙子のコピー。列挙前の値に対して呼ぶと先頭から列挙する。</returns>
    public readonly LoupeCheckerTiles GetEnumerator() => this;

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

            if (LoupeCheckerboard.IsDarkTile(col, row))
            {
                _current = new EsRect(col * _tileSize, row * _tileSize, _tileSize, _tileSize);
                return true;
            }
        }

        return false;
    }
}
