using Avalonia.Controls;
using FluentAvalonia.UI.Controls;

namespace EsUtil.SaveEvidence.App.AvaloniaUI.Views;

/// <summary>簡易確認ダイアログ（はい / いいえ）を表示する静的ヘルパー。</summary>
/// <remarks>
/// Avalonia の Fluent ダイアログ（<see cref="FAContentDialog"/>）をオーナー ウィンドウの中央へモーダル表示する。<br/>
/// 見た目・ボタンの並び・既定のボタンはフレームワークが決める（独自のウィンドウを組み立てない）。<br/>
/// WinUI 3 版の <c>ContentDialog</c> と同じ系統のダイアログになる。<br/>
/// <b>閉じられた場合</b>はキャンセル（「いいえ」）と同じ扱いにする。
/// </remarks>
public static class ConfirmDialog
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

        var dialog = new FAContentDialog
        {
            Title = title,
            Content = message,
            PrimaryButtonText = "はい",
            CloseButtonText = "いいえ",
            // 既定のボタンは「いいえ」にする（Enter の連打で破壊的な操作を実行しない）
            DefaultButton = FAContentDialogButton.Close,
        };

        var result = await dialog.ShowAsync(owner);
        return result == FAContentDialogResult.Primary;
    }
}
