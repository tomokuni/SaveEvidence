namespace EsUtil.Feature.LoupeCore.Drawing;

/// <summary>ルーペの UI 層が処理する層（描画順の契約）。</summary>
/// <remarks>
/// <b>描画順はコアが単一所有する</b>: UI 層は <see cref="LoupeDrawContent.Layers"/> を先頭から順に処理するだけで、<br/>
/// 順序を判断しない（描画の起動は <see cref="Loupe.Draw"/>、描画内容の解決は <see cref="Loupe.ResolveContent"/>）。<br/>
/// 中心座標ラベル（<see cref="CoordLabel"/>）は<b>必ず最後</b>に置く（常に最上位に表示する）。<br/>
/// <br/>
/// <b>UI 層の実装</b>: 図形の層は自 UI の描画 API へ写し、文字の層は UI 要素（テキスト要素）を配置する。<br/>
/// どちらの方式で描くかは UI の裁量であるが、<b>層の順序と有無は本契約に従う</b>。
/// </remarks>
public enum LoupeDrawLayer
{
    /// <summary>下地（ベース色）。透明の場合は描かない。</summary>
    Background,

    /// <summary>市松模様（透明色は描かない）。</summary>
    Checkerboard,

    /// <summary>拡大画像（切り出せない場合は描かない）。</summary>
    Image,

    /// <summary>十字線（透明色は描かない）。</summary>
    Crosshair,

    /// <summary>外枠（透明色は描かない）。</summary>
    Frame,

    /// <summary>中心座標ラベル（常に最上位）。</summary>
    CoordLabel,
}
