using Aprillz.MewUI;
using Aprillz.MewUI.Controls;

namespace EsUtil.SaveEvidence.MewUI.Views;

/// <summary>クリックできる要素へツールヒントとクリック処理をまとめて登録する。</summary>
/// <remarks>
/// <b>MewUI 0.21.1 の不具合の回避</b>: ツールヒントが表示されている要素を押すと、ツールヒントを閉じる際に<br/>
/// ポップアップのネイティブウィンドウが破棄され、その破棄処理がスレッド全体のマウスキャプチャを解放する。<br/>
/// 直前に押下で取得したキャプチャが奪われ、ボタンの押下が取り消されて <c>Click</c> が発生しない<br/>
/// （aprillz/MewUI issue #253。修正版は未リリース）。<br/>
/// <br/>
/// 本クラスは <c>Click</c> が発生しなかった押下を <c>MouseUp</c> で補完する。<br/>
/// 補完の条件は「左ボタン」「未処理」「押下状態が入っていない」「ポインターが要素上」「有効」であり、<br/>
/// 通常の押下（押下状態が入っている）では条件が成立しないため二重実行は起こらない。<br/>
/// MewUI 側が修正されても押下状態が入るため、同様に条件が成立せず無害である。
/// </remarks>
internal static class ClickableToolTip
{
    /// <summary>ツールヒント付きのボタンへクリック処理を登録する。</summary>
    /// <typeparam name="T">ボタンの型。</typeparam>
    /// <param name="button">対象のボタン（null 不可）。</param>
    /// <param name="text">ツールヒント文字列。</param>
    /// <param name="onClick">クリック時の処理（null 不可）。</param>
    /// <returns>対象のボタン（メソッドチェーン用）。</returns>
    public static T WithToolTip<T>(this T button, string text, Action onClick)
        where T : Button
    {
        button.ToolTip(text);
        button.OnClick(onClick);

        button.MouseUp += e =>
        {
            if (e.Button == MouseButton.Left
                && !e.Handled
                && !button.IsPressed
                && button.IsMouseOver
                && button.IsEffectivelyEnabled)
            {
                onClick();
            }
        };

        return button;
    }
}
