using Aprillz.MewUI;
using Aprillz.MewUI.Input;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Ui.MewUI.Annotation;

/// <summary>注釈レイヤーの IME 連携（変換候補の位置と文字の受け取り）。</summary>
/// <remarks>
/// <b>MewUI はフォーカス中の要素から IME の位置を求める</b>: ウィンドウのバックエンドは
/// <see cref="ITextCompositionClient.GetCharRectInWindow"/> で変換中の文字の位置を得て、
/// <c>ImmSetCompositionWindow</c> / <c>ImmSetCandidateWindow</c> を呼びます。 本レイヤーが
/// このインターフェイスを実装しないと<b>候補の位置が求まらず、画面の隅（右下）に出ます</b>
/// （実測 2026-11-23: MewUI 版だけで発生）。<br/>
/// <b>文字の受け取りはウィンドウのプレビューで行う</b>: プレビューで <c>Handled</c> を立てるため、
/// <see cref="ITextInputClient.HandleTextInput"/> と <see cref="ITextCompositionClient"/> の
/// <c>Handle…</c> は呼ばれません（同じ文字を 2 回入れない）。 ここで実装するのは
/// <b>位置を答えるため</b>のメンバーです。
/// </remarks>
public sealed partial class AnnotationLayerControl : ITextCompositionClient, ITextInputClient
{
    /// <summary>変換中の文字列の先頭の位置（文字数）を取得する。</summary>
    /// <value>変換中でなければキャレットの位置。</value>
    public int CompositionStartIndex => _compositionStart;

    /// <summary>IME が変換中かどうかを取得する。</summary>
    /// <value>変換中の文字列を持っている場合は <see langword="true"/>。</value>
    public bool IsComposing => _isEditingText && _preeditText.Length > 0;

    /// <summary>指定した位置の文字の矩形（ウィンドウ座標）を取得する。</summary>
    /// <param name="charIndex">文字位置（文字数）。</param>
    /// <returns>文字の矩形（DIP。 ウィンドウ座標）。 求められない場合は空の矩形。</returns>
    /// <remarks>
    /// <b>ウィンドウ座標で答える</b>: MewUI の <c>Element.Bounds</c> はウィンドウ基準であるため、
    /// ローカル DIP の左上へ <c>Bounds</c> の左上を足します。 変換候補はこの矩形の
    /// 下へ出ます。
    /// </remarks>
    public Rect GetCharRectInWindow(int charIndex)
    {
        if (!_isEditingText || !TryResolveCaretSource(charIndex, DefaultLayouts, out var caret))
        {
            return default;
        }

        var dip = ToClientDip(new EsPoint(caret.X, caret.Y));
        var (scale, _) = ResolveImageTransform();
        return new Rect(
            Bounds.X + dip.X,
            Bounds.Y + dip.Y,
            Math.Max(1.0, CaretThicknessDip),
            Math.Max(1.0, caret.Height * scale));
    }

    /// <inheritdoc/>
    public void HandleTextCompositionStart(TextCompositionEventArgs e) => ApplyCompositionStart(e);

    /// <inheritdoc/>
    public void HandleTextCompositionUpdate(TextCompositionEventArgs e) => ApplyCompositionUpdate(e);

    /// <inheritdoc/>
    public void HandleTextCompositionEnd(TextCompositionEventArgs e) => ApplyCompositionEnd(e);

    /// <inheritdoc/>
    public void HandleTextInput(TextInputEventArgs e) => ApplyTextInput(e);
}
