namespace EsUtil.Feature.LoupeCore.Drawing;

/// <summary>ルーペの UI 層が描く層（描画順の契約）。</summary>
/// <remarks>
/// <b>描画順はコアが単一所有する</b>: UI 層は <see cref="LoupeDrawContent.Layers"/> を先頭から順に処理するだけで、<br/>
/// 順序を判断しない（描画の起動は <see cref="Loupe.Draw"/>、描画内容の解決は <see cref="Loupe.ResolveContent"/>）。<br/>
/// <b>テキスト（<see cref="Label"/>）は UI 要素でも描ける</b>: 要素（<c>TextBlock</c> 等）として置く UI は<br/>
/// 要素を子として置く（親より後に描かれるため常に最上位になる）。
/// その場合、描画実装は本層を無視してよい（基底のフックの既定は何もしない）。<br/>
/// canvas へ直接描く実装は本層に従ってテキストを描く。<br/>
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

    /// <summary>テキスト（中心座標ラベル。テキストがある場合のみ。常に最上位）。</summary>
    /// <remarks>UI 要素として置く UI は本層を無視してよい（基底のフックの既定は何もしない）。</remarks>
    Label,
}
