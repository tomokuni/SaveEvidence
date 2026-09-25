using Aprillz.MewUI;
using Aprillz.MewUI.Controls;

namespace EsUtil.SaveEvidence.MewUI.Views;

/// <summary>簡易確認ダイアログ（はい / いいえ）を表示する静的ヘルパー（MewUI 版）。</summary>
/// <remarks>
/// Avalonia 版 <c>ConfirmDialog</c> に相当する。MewUI は XAML を持たないため C# マークアップで組み立てる。<br/>
/// ウィンドウ装飾なし（<c>Borderless</c>）・固定サイズのモーダルダイアログとしてオーナーの中央に表示する。<br/>
/// 用途: 保存時の上書き確認など（<c>MainViewModel.ConfirmOverwriteAsync</c> へ配線する）。
/// </remarks>
internal static class ConfirmDialog
{
    /// <summary>ダイアログの幅（DIP）。</summary>
    private const double DialogWidth = 420;

    /// <summary>ダイアログの高さ（DIP）。</summary>
    private const double DialogHeight = 180;

    /// <summary>ボタンの幅（DIP）。</summary>
    private const double ButtonWidth = 80;

    /// <summary>確認ダイアログを表示し、ユーザーの選択を返す。</summary>
    /// <param name="owner">オーナーウィンドウ。</param>
    /// <param name="title">ダイアログタイトル。</param>
    /// <param name="message">表示メッセージ。</param>
    /// <returns>「はい」が選択された場合は true、それ以外（「いいえ」・Esc・閉じる操作）は false。</returns>
    /// <remarks>
    /// 1. メッセージと はい / いいえ のボタンを縦に積んだウィンドウを組み立てる。<br/>
    /// 2. 選択結果を <see cref="TaskCompletionSource{TResult}"/> へ設定し、ダイアログを閉じる。<br/>
    /// 3. ダイアログが閉じるまで待ってから結果を返す。<br/>
    /// 4. Esc と閉じる操作（Alt+F4 等）でも待機が完了するようにする（キャンセル扱い。永久待ちを防ぐ）。
    /// </remarks>
    public static async Task<bool> ShowAsync(Window owner, string title, string message)
    {
        // 応答は「はい」のみを true とし、閉じる操作・Esc は false（キャンセル）として扱う
        var result = new TaskCompletionSource<bool>();

        var dialog = new Window()
        {
            Title = title,
            StartupLocation = WindowStartupLocation.CenterOwner,
        };

        void Finish(bool accepted)
        {
            result.TrySetResult(accepted);
            dialog.Close();
        }

        var yes = new Button().Content("はい").Width(ButtonWidth).OnClick(() => Finish(true));
        var no = new Button().Content("いいえ").Width(ButtonWidth).OnClick(() => Finish(false));

        // 閉じる操作（Alt+F4 等）で結果が設定されないまま待ち続けるのを防ぐ
        dialog.Closed += () => result.TrySetResult(false);
        dialog.PreviewKeyDown += e =>
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Finish(false);
            }
        };

        dialog.WindowSize = WindowSize.Fixed(DialogWidth, DialogHeight);
        dialog.Borderless().Content(
            new StackPanel()
                .Vertical()
                .Spacing(20)
                .Padding(20)
                .Children(
                    new TextBlock().Text(message),
                    new StackPanel().Horizontal().Spacing(10).Children(yes, no)));

        await dialog.ShowDialogAsync(owner);
        return await result.Task;
    }
}
