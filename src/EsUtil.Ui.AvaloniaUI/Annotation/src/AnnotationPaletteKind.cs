namespace EsUtil.Ui.AvaloniaUI.Annotation;

/// <summary>注釈の色選択で使う代表色の組。</summary>
/// <remarks>
/// <b>2 組を用意する</b>: 用途（枠線は落ち着いた色・強調は鮮やかな色など）で選び分けられるよう、
/// 別の 16 色を 2 組持つ。 書式バーは選んだ組だけを出し、設定画面は両方を並べる。
/// </remarks>
public enum AnnotationPaletteKind
{
    /// <summary>代表色 1（無彩色 → 暖色 → 寒色の 16 色）。</summary>
    Representative,

    /// <summary>代表色 2（和名の 16 色。 代表色 1 と重ならない色）。</summary>
    Accent,
}
