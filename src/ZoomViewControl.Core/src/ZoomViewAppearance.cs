using DrawingColor = System.Drawing.Color;
using EsUtil.Platform.Core.Geometry;

namespace Estable.ZoomView;

/// <summary>ズームビューの外観（下地・市松模様・スクロールバー）をまとめた不変の設定値。</summary>
/// <remarks>
/// UI 実装が描画に使う外観値を 1 つの値として所有する（UI 層に色・サイズのリテラルを置かない）。<br/>
/// 描画の可否（市松を描くか・トラックを描くか）も本型が判定し、UI 層は <c>Has*</c> を問い合わせるだけでよい<br/>
/// （両 UI で判定がずれない）。<br/>
/// 色は UI 非依存の <see cref="DrawingColor"/> を使用し、各 UI の色型への変換は境界（UI 層）で 1 行行う。<br/>
/// スクロールバーの太さ・余白は UI ごとに見た目が異なってよい値であり、ホストが本値で与える<br/>
/// （Avalonia 版は標準 <c>ScrollBar</c> を使うため本値の幾何は参照しない）。
/// </remarks>
public sealed record ZoomViewAppearance
{
    /// <summary>スクロールバーの太さの既定値（DIP）。</summary>
    /// <remarks>MewUI 標準のスクロールバーと同じ太さに揃える。</remarks>
    public const double DefaultScrollBarThickness = 4.0;

    /// <summary>スクロールバーとビューポート端との余白の既定値（DIP）。</summary>
    /// <remarks>
    /// MewUI のウィンドウは端の数 DIP をサイズ変更の判定領域にするため、余白 0 ではサム上でポインタが<br/>
    /// ウィンドウサイズ変更になり、マウスでスクロールできない。<br/>
    /// 判定領域の外側へ出る最小限の値とする（Avalonia 版は標準 <c>ScrollBar</c> のため本値を使用しない）。
    /// </remarks>
    public const double DefaultScrollBarMargin = 8.0;

    /// <summary>すべての項目を既定値にした外観。</summary>
    /// <value>暗色の下地・市松模様なし・スクロールバー表示（白のサム）。</value>
    public static ZoomViewAppearance Default { get; } = new();

    /// <summary>画像が描画されていない領域の下地色を取得する。</summary>
    /// <value>アルファを含む色。既定は暗色（#FF202020）。</value>
    /// <remarks>市松模様を描く場合も、まず本色で全面を塗ってからタイルを重ねる（タイルは半透明のため下地と合成される）。</remarks>
    public DrawingColor BackgroundColor { get; init; } = DrawingColor.FromArgb(255, 32, 32, 32);

    /// <summary>市松模様（画像の透明部分の背景）の 1 タイルの一辺（DIP）を取得する。</summary>
    /// <value>1 タイルの一辺（DIP）。既定は 0（市松模様を描かない）。</value>
    /// <remarks>
    /// 0 以下は「描かない」を意味する（下地色の単色背景になる）。ホストが用途に応じて指定する。<br/>
    /// 1 は 1 ピクセルの隙間が見えないため、実効値は 2 になる（<see cref="ResolveCheckerTileSize"/> が単一所有する）。
    /// </remarks>
    public int CheckerSize { get; init; }

    /// <summary>市松模様のタイルサイズの下限（実効値。これ未満の値は本色へ切り上げる）。</summary>
    public const int MinimumCheckerTileSize = 2;

    /// <summary>要求されたタイルサイズを実効値へ解決する（0 以下は描かない・1 は下限へ切り上げ）。</summary>
    /// <param name="checkerSize">要求されたタイルサイズ（DIP。0 以下も可）。</param>
    /// <returns>実効タイルサイズ。<c>0</c> の場合は市松模様を描かない。</returns>
    /// <remarks>
    /// タイル 1 の市松模様は明暗の隙間が見えない（1 ピクセル幅の市松が縞に見える）ため、下限へ切り上げる。<br/>
    /// 本規則を UI 層（ホストのブラシ構築含む）に置くと、UI ごとに「0 の意味」がずれる（実際に Avalonia は 0 を 2 へ、<br/>
    /// MewUI は 0 を「描かない」と解釈していた）。両 UI が同じ意味を共有するよう、本メソッドが単一所有する。
    /// </remarks>
    public static int ResolveCheckerTileSize(int checkerSize)
        => checkerSize <= 0 ? 0 : Math.Max(MinimumCheckerTileSize, checkerSize);

    /// <summary>市松模様の明色（色 A）を取得する。</summary>
    /// <value>アルファを含む色。既定は白。</value>
    public DrawingColor CheckerColorA { get; init; } = DrawingColor.FromArgb(255, 255, 255, 255);

    /// <summary>市松模様の暗色（色 B）を取得する。</summary>
    /// <value>アルファを含む色。既定は薄いグレー。</value>
    public DrawingColor CheckerColorB { get; init; } = DrawingColor.FromArgb(255, 204, 204, 204);

    /// <summary>オーバーレイスクロールバーを表示するかどうかを取得する。</summary>
    /// <value>true（既定）の場合は、画像がビューポートより大きい軸にスクロールバーを表示する。</value>
    /// <remarks>パン・ホイールでも操作できるため、表示はホストの選択とする。</remarks>
    public bool ShowScrollBars { get; init; } = true;

    /// <summary>スクロールバーの太さ（DIP）を取得する。</summary>
    /// <value>太さ（DIP）。既定は <see cref="DefaultScrollBarThickness"/>。</value>
    public double ScrollBarThickness { get; init; } = DefaultScrollBarThickness;

    /// <summary>スクロールバーとビューポート端との余白（DIP）を取得する。</summary>
    /// <value>余白（DIP）。既定は <see cref="DefaultScrollBarMargin"/>。</value>
    public double ScrollBarMargin { get; init; } = DefaultScrollBarMargin;

    /// <summary>スクロールバーのサムの色を取得する。</summary>
    /// <value>アルファを含む色。既定は白 63%。</value>
    public DrawingColor ScrollBarColor { get; init; } = DrawingColor.FromArgb(160, 255, 255, 255);

    /// <summary>スクロールバーのトラックの色を取得する。</summary>
    /// <value>アルファを含む色。既定は白 16%。</value>
    /// <remarks>アルファ 0 の場合はトラックを描かない（サムのみの表示を要求するホスト向け）。</remarks>
    public DrawingColor ScrollBarTrackColor { get; init; } = DrawingColor.FromArgb(40, 255, 255, 255);

    /// <summary>市松模様を描画するかどうかを取得する。</summary>
    /// <value>タイルサイズの実効値が 0 より大きい場合は true。</value>
    /// <remarks>「0 は描かない」の意味は <see cref="ResolveCheckerTileSize"/> が単一所有する。</remarks>
    public bool HasCheckerboard => ResolveCheckerTileSize(CheckerSize) > 0;

    /// <summary>スクロールバーのトラックを描画するかどうかを取得する。</summary>
    /// <value>トラック色のアルファが 0 より大きい場合は true。</value>
    public bool HasScrollBarTrack => ScrollBarTrackColor.A > 0;

    /// <summary>市松模様の明色タイル（色 A）の矩形を列挙する。</summary>
    /// <param name="viewport">ビューポートのサイズ（DIP）。</param>
    /// <returns>明色タイルの矩形の一覧。市松模様を描かない場合は空。</returns>
    /// <remarks>
    /// 描画手順は「暗色（<see cref="CheckerColorB"/>）で全面を塗る → 明色（<see cref="CheckerColorA"/>）のタイルを重ねる」。<br/>
    /// 本メソッドが返すのは手順の後半（明色タイル）であり、タイルの配置規則（<c>(列 + 行)</c> が偶数）を単一所有する。<br/>
    /// UI 層は返された矩形を自 UI の描画 API へ渡すだけで、明暗の判定を持たない。<br/>
    /// タイルサイズは <see cref="ResolveCheckerTileSize"/> が解決した実効値を使う。
    /// </remarks>
    public IReadOnlyList<EsRect> ComputeLightTileRects(EsSize viewport)
    {
        var tile = ResolveCheckerTileSize(CheckerSize);
        if (tile <= 0 || viewport.Width <= 0 || viewport.Height <= 0)
        {
            return [];
        }

        var columns = (int)Math.Ceiling(viewport.Width / tile);
        var rows = (int)Math.Ceiling(viewport.Height / tile);
        var rects = new List<EsRect>((columns * rows + 1) / 2);

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                if ((row + column) % 2 == 0)
                {
                    rects.Add(new EsRect(column * tile, row * tile, tile, tile));
                }
            }
        }

        return rects;
    }
}
