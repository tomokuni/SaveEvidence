namespace EsUtil.Feature.LoupeCore.Drawing;

/// <summary>ルーペの UI 層が描く図形の層（描画順の契約）。</summary>
/// <remarks>
/// <b>描画順はコアが単一所有する</b>: UI 層は <see cref="LoupeDrawContent.Layers"/> を先頭から順に処理するだけで、<br/>
/// 順序を判断しない（描画の起動は <see cref="Loupe.Draw"/>、描画内容の解決は <see cref="Loupe.ResolveContent"/>）。<br/>
/// <b>文字（ラベル）の層は含まない</b>: テキストは <see cref="LoupeDrawContent.CoordLabelText"/> として渡され、<br/>
/// UI 層が要素として置くか画像として描くかを決める（UI の要素は親より後に描かれるため常に最上位になる）。<br/>
/// <br/>
/// <b>UI 層の実装</b>: 本層の順序に従って自 UI の描画 API へ写す。
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
}
