namespace Estable.Marquee.Decoration;

/// <summary>暗転マスクのラベル 1 行分の外観（フォントサイズ・文字色・背景色）。</summary>
/// <remarks>
/// サブライン（操作ラベル・情報ラベル）の外観を 1 つの値としてまとめ、行の組み立て（<see cref="OuterDecoLabels.BuildSubLines"/>）へ渡す。<br/>
/// 文言と表示可否は <see cref="OuterDecoLabels"/> が判定し、本型は外観のみを持つ（関心の分離）。<br/>
/// 外観の公開面は <see cref="OuterDecoAppearance"/> が所有し、本型はフレームの受け渡し専用とする。<br/>
/// 色は UI 非依存の <see cref="System.Drawing.Color"/> を使用する。
/// </remarks>
/// <param name="FontSize">フォントサイズ（DIP）。</param>
/// <param name="Color">文字色。</param>
/// <param name="BackgroundColor">背景色（不透明度は色のアルファに統合。アルファ 0 の場合は黒 50% へフォールバック）。</param>
public readonly record struct OuterDecoLabelStyle(
    double FontSize,
    System.Drawing.Color Color,
    System.Drawing.Color BackgroundColor);
