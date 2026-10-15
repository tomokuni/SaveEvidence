using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace EsUtil.SaveEvidence.App.WinUI3.Views;

/// <summary>簡易確認ダイアログ（はい / いいえ）を表示する静的ヘルパー（WinUI 3 版）。</summary>
/// <remarks>
/// WinUI 3 の <see cref="ContentDialog"/> を使い、オーナー ウィンドウの中央へモーダル表示する。<br/>
/// 用途: 保存時の上書き確認など（メイン ViewModel の上書き確認コールバックへ配線する）。<br/>
/// <b>閉じられた場合</b>はキャンセル（「いいえ」）と同じ扱いにする（保存を続行しない）。
/// </remarks>
internal static class ConfirmDialog
{
    /// <summary>確認ダイアログを表示し、ユーザーの選択を返す。</summary>
    /// <param name="xamlRoot">ダイアログを表示するウィンドウの XAML ルート。</param>
    /// <param name="title">ダイアログ タイトル。</param>
    /// <param name="message">表示メッセージ。</param>
    /// <returns>「はい」が選択された場合は true、それ以外（「いいえ」・Esc・閉じる操作）は false。</returns>
    /// <remarks>
    /// 1. メッセージと はい / いいえ のボタンを持つダイアログを組み立てる。<br/>
    /// 2. 表示して、選択された内容から結果を返す。<br/>
    /// 3. 結果が得られない場合（閉じる操作）は false（キャンセル）として扱う。
    /// </remarks>
    public static async Task<bool> ShowAsync(XamlRoot xamlRoot, string title, string message)
    {
        ArgumentNullException.ThrowIfNull(xamlRoot);

        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = title,
            Content = message,
            PrimaryButtonText = "はい",
            CloseButtonText = "いいえ",
            DefaultButton = ContentDialogButton.Close,
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }
}
