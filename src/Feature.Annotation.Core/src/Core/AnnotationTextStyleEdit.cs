namespace EsUtil.Feature.Annotation.Core;

/// <summary>本文の書式を適用する範囲の規則（単一所有）。</summary>
/// <remarks>
/// <b>選択が無ければ適用しない</b>: キャレットだけの状態で書式を変えると、キャレット以降の文字が
/// 意図せず変わる（実測 2026-11-21: 4 つの UI 版で「全体」または「キャレット以降」に適用されていた）。
/// 本文の書式は「選択した文字」へだけ適用する。 選択の有無は UI が持ち、範囲の決定は本クラスが持つ。
/// </remarks>
public static class AnnotationTextStyleEdit
{
    /// <summary>書式を適用する範囲を決める。</summary>
    /// <param name="selectionStart">選択の開始位置（文字数）。</param>
    /// <param name="selectionLength">選択の長さ（0 は選択なし）。</param>
    /// <returns>適用する範囲。 選択が無い場合は <see langword="null"/>。</returns>
    public static (int Start, int Length)? ResolveRange(int selectionStart, int selectionLength)
        => selectionLength <= 0 ? null : (Math.Max(0, selectionStart), selectionLength);
}
