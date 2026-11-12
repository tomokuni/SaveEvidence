using Aprillz.MewUI;
using Aprillz.MewUI.Controls;

namespace EsUtil.SaveEvidence.App.MewUI.Views;

/// <summary>簡易確認ダイアログ（はい / いいえ）を表示する静的ヘルパー（MewUI 版）。</summary>
/// <remarks>
/// MewUI 標準の <see cref="MessageBox"/>（<see cref="MessageBoxOptions"/>）を使い、オーナーの中央へモーダル表示する。<br/>
/// 見た目・ボタンの並び・既定のボタンはフレームワークが決める（独自のウィンドウを組み立てない）。<br/>
/// 用途: 保存時の上書き確認・フォルダビューの削除確認など。<br/>
/// <b>閉じられた場合</b>はキャンセル（「いいえ」）と同じ扱いにする。
/// </remarks>
internal static class ConfirmDialog
{
    /// <summary>確認ダイアログを表示し、ユーザーの選択を返す。</summary>
    /// <param name="owner">オーナーウィンドウ。</param>
    /// <param name="title">ダイアログ タイトル。</param>
    /// <param name="message">表示メッセージ。</param>
    /// <returns>「はい」が選択された場合は true、それ以外（「いいえ」・Esc・閉じる操作）は false。</returns>
    /// <remarks>
    /// 1. タイトル・本文・はい / いいえ のボタンを持つダイアログを組み立てる。<br/>
    /// 2. 表示して、選択された内容から結果を返す。<br/>
    /// 3. 結果が得られない場合（閉じる操作）は false（キャンセル）として扱う。
    /// </remarks>
    public static async Task<bool> ShowAsync(Window owner, string title, string message)
    {
        ArgumentNullException.ThrowIfNull(owner);

        var options = new MessageBoxOptions
        {
            Title = title,
            Message = message,
            Icon = PromptIconKind.Question,
            Buttons =
            [
                new MessageButton("はい", MessageButtonRole.Accept),
                new MessageButton("いいえ", MessageButtonRole.Reject),
            ],
            Owner = owner,
        };

        var result = await MessageBox.PromptAsync(options).ConfigureAwait(true);
        return result == true;
    }
}
