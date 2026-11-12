using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace EsUtil.SaveEvidence.App.AvaloniaUI.Views;

/// <summary>簡易確認ダイアログ（はい / いいえ）を表示する静的ヘルパー。</summary>
/// <remarks>
/// MainWindow に直接実装されていた確認ダイアログを分離し、再利用可能な形にした。<br/>
/// ウィンドウ装飾なし（<see cref="WindowDecorations.None"/>）のモーダルダイアログとして表示する。
/// </remarks>
public static class ConfirmDialog
{
    /// <summary>確認ダイアログを表示し、ユーザーの選択を返す。</summary>
    /// <param name="owner">オーナーウィンドウ。</param>
    /// <param name="title">ダイアログタイトル。</param>
    /// <param name="message">表示メッセージ。</param>
    /// <returns>「はい」が選択された場合は true、それ以外（「いいえ」等）は false。</returns>
    /// <remarks>
    /// ダイアログはオーナーの中央に表示され、「はい」または「いいえ」の選択を待つ。<br/>
    /// ダイアログが閉じられるまで <see cref="TaskCompletionSource{TResult}"/> で選択結果を保持する。
    /// </remarks>
    public static async Task<bool> ShowAsync(Window owner, string title, string message)
    {
        var tcs = new TaskCompletionSource<bool>();

        // **標準のウィンドウ装飾を使う**: 装飾なし・固定サイズだとボタンの位置と余白だけが浮いて見え、
        // ダイアログに見えない（実測 2026-11-12）。 高さは中身に合わせ、幅だけ固定する。
        var dialog = new Window
        {
            Title = title,
            Width = 420,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
        };

        var yesButton = new Button { Content = "はい", Width = 88, MinHeight = 32 };
        var noButton = new Button { Content = "いいえ", Width = 88, MinHeight = 32 };

        yesButton.Click += (_, _) => { tcs.TrySetResult(true); dialog.Close(); };
        noButton.Click += (_, _) => { tcs.TrySetResult(false); dialog.Close(); };

        // ウィンドウが閉じられた場合（Alt+F4 等）も待機が完了するようにする（キャンセル扱い）
        dialog.Closed += (_, _) => tcs.TrySetResult(false);

        // Esc キーでキャンセルできるようにする
        dialog.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                tcs.TrySetResult(false);
                dialog.Close();
            }
        };

        // 本文とボタンを左右に分け、ボタンは右下へ寄せる（WinUI 3 の ContentDialog と同じ見え方）
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Children = { yesButton, noButton },
        };

        dialog.Content = new Grid
        {
            Margin = new Thickness(20),
            RowDefinitions = new RowDefinitions("*,Auto"),
            Children =
            {
                new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 20),
                    VerticalAlignment = VerticalAlignment.Center,
                },
                buttons,
            },
        };
        Grid.SetRow(buttons, 1);

        await dialog.ShowDialog(owner);
        return await tcs.Task;
    }
}
