namespace Estable.Marquee.Decoration;

/// <summary>暗転マスクのサイズラベルの下に表示する 1 行分の内容（テキスト・フォントサイズ・色）。</summary>
/// <param name="Text">行のテキスト。</param>
/// <param name="FontSize">フォントサイズ（DIP）。</param>
/// <param name="Color">文字色。</param>
/// <param name="BackgroundColor">背景色（不透明度は色のアルファに統合。アルファ 0 の場合は黒 50% へフォールバック）。</param>
/// <remarks>
/// 2 行目以降（操作ラベル・情報ラベル）を行単位で指定するための不変データ。<br/>
/// 文言の選定（操作ラベル・情報ラベルの表示判定）は <see cref="OuterDecoLabels"/> が単一所有し、<br/>
/// 行の一覧の組み立ては <see cref="OuterDecoLabels.BuildSubLines"/> が担う（両 UI で同一の結果になる）。<br/>
/// <b>可視性</b>: 本値は <see cref="OuterDecoAppearance.BuildSubLines"/> の戻り値として UI の描画層だけが読むため、本値を公開しない。
/// </remarks>
internal sealed record OuterDecoSubLine(
    string Text,
    double FontSize,
    System.Drawing.Color Color,
    System.Drawing.Color BackgroundColor = default);
