using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using EsUtil.Platform.Core;
using EsUtil.SaveEvidence.App.AvaloniaUI.Logging;
using FluentAvalonia.UI.Controls;
using Microsoft.Extensions.Logging;

namespace EsUtil.SaveEvidence.App.AvaloniaUI.Views;

/// <summary>簡易確認ダイアログ（はい / いいえ）を表示する静的ヘルパー。</summary>
/// <remarks>
/// Avalonia の Fluent ダイアログ（<see cref="FAContentDialog"/>）をオーナー ウィンドウの中央へモーダル表示する。<br/>
/// 見た目・ボタンの並び・既定のボタンはフレームワークが決める（独自のウィンドウを組み立てない）。<br/>
/// WinUI 3 版の <c>ContentDialog</c> と同じ系統のダイアログになる。<br/>
/// <b>Fluent のダイアログを表示できない場合は標準のウィンドウ ダイアログへ落とす</b>（テーマの組み合わせ等で<br/>
/// 例外になったときに、確認そのものが失敗して保存・削除ができない状態を作らない）。<br/>
/// <b>閉じられた場合</b>はキャンセル（「いいえ」）と同じ扱いにする。
/// </remarks>
public static class ConfirmDialog
{
    /// <summary>アプリケーションロガー。</summary>
    private static readonly ILogger s_logger = AppLog.CreateLogger("ConfirmDialog");

    /// <summary>確認ダイアログを表示し、ユーザーの選択を返す。</summary>
    /// <param name="owner">オーナーウィンドウ。</param>
    /// <param name="title">ダイアログ タイトル。</param>
    /// <param name="message">表示メッセージ。</param>
    /// <returns>「はい」が選択された場合は true、それ以外（「いいえ」・Esc・閉じる操作）は false。</returns>
    /// <remarks>
    /// 1. タイトル・本文・はい / いいえ のボタンを持つ Fluent のダイアログを組み立てて表示する。<br/>
    /// 2. 表示できなかった場合は標準のウィンドウ ダイアログで表示する。<br/>
    /// 3. 結果が得られない場合（閉じる操作）は false（キャンセル）として扱う。
    /// </remarks>
    public static async Task<bool> ShowAsync(Window owner, string title, string message)
    {
        ArgumentNullException.ThrowIfNull(owner);

        try
        {
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
        catch (Exception ex) when (EsErrors.IsRecoverable(ex))
        {
            // Fluent のダイアログを表示できない（テーマの読み込み等）。 確認を諦めず標準のダイアログで続ける
            s_logger.LogConfirmDialogFallback(ex);
            return await ShowWindowDialogAsync(owner, title, message);
        }
    }

    /// <summary>標準のウィンドウで確認ダイアログを表示する（Fluent のダイアログを使えない場合の代替）。</summary>
    /// <param name="owner">オーナーウィンドウ。</param>
    /// <param name="title">ダイアログ タイトル。</param>
    /// <param name="message">表示メッセージ。</param>
    /// <returns>「はい」が選択された場合は true。</returns>
    private static async Task<bool> ShowWindowDialogAsync(Window owner, string title, string message)
    {
        var completion = new TaskCompletionSource<bool>();

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

        yesButton.Click += (_, _) => { completion.TrySetResult(true); dialog.Close(); };
        noButton.Click += (_, _) => { completion.TrySetResult(false); dialog.Close(); };

        // ウィンドウが閉じられた場合（Alt+F4 等）も待機が完了するようにする（キャンセル扱い）
        dialog.Closed += (_, _) => completion.TrySetResult(false);

        // Esc キーでキャンセルできるようにする
        dialog.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape)
            {
                return;
            }

            e.Handled = true;
            completion.TrySetResult(false);
            dialog.Close();
        };

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
        return await completion.Task;
    }
}
