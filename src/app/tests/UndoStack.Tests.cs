using app.Models;
using Avalonia.Headless.XUnit;
using Estable.AvaloniaUI.Platform.Imaging;
using Xunit;

namespace app.Tests;

/// <summary><see cref="UndoStack"/> の単体テスト（Undo / Redo の動作確認）。</summary>
/// <remarks>
/// Undo 実行時に現在の画像が Redo スタックへ退避され、Redo で戻せることを検証する。<br/>
/// 新しい操作（<see cref="UndoStack.Push"/>）で Redo 履歴がクリアされることも検証する。
/// </remarks>
[Collection("AvaloniaHeadless")]
public class UndoStackTests
{
    /// <summary>テスト用の UnifiedBitmap（size x size）を生成する。</summary>
    /// <param name="size">一辺のピクセル数（既定 10）。</param>
    /// <returns>生成した UnifiedBitmap。</returns>
    private static UnifiedBitmap CreateBitmap(int size = 10)
        => UnifiedBitmap.Create(size, size);

    [AvaloniaFact]
    public void Undo_現在の画像をRedoスタックへ退避して復元する()
    {
        var stack = new UndoStack();
        var img1 = CreateBitmap();
        var img2 = CreateBitmap();

        stack.Push(img1);
        img1.Dispose();

        // Undo: 現在の画像（img2）を Redo へ退避し、img1 を復元する
        var restored = stack.Undo(img2);

        Assert.NotNull(restored);
        Assert.Equal(0, stack.Count);
        Assert.Equal(1, stack.RedoCount);

        // Redo: 退避した img2 が返る
        var redone = stack.Redo();
        Assert.NotNull(redone);
        Assert.Equal(0, stack.RedoCount);

        restored!.Dispose();
        redone!.Dispose();
        img2.Dispose();
    }

    [AvaloniaFact]
    public void Redo_スタックが空ならnullを返す()
    {
        var stack = new UndoStack();

        Assert.Null(stack.Redo());
    }

    [AvaloniaFact]
    public void Undo_スタックが空ならnullを返しRedoへ退避しない()
    {
        var stack = new UndoStack();
        var img = CreateBitmap();

        Assert.Null(stack.Undo(img));
        Assert.Equal(0, stack.RedoCount);

        img.Dispose();
    }

    [AvaloniaFact]
    public void Push_新しい操作でRedo履歴がクリアされる()
    {
        var stack = new UndoStack();
        var img1 = CreateBitmap();
        var img2 = CreateBitmap();
        var img3 = CreateBitmap();

        stack.Push(img1);
        img1.Dispose();

        // Undo して Redo が 1 件ある状態
        var restored = stack.Undo(img2);
        Assert.NotNull(restored);
        Assert.Equal(1, stack.RedoCount);
        restored!.Dispose();

        // 新しい操作（Push）で Redo がクリアされる
        stack.Push(img3);

        Assert.Equal(0, stack.RedoCount);
        Assert.Equal(1, stack.Count);

        img2.Dispose();
        img3.Dispose();
    }

    [AvaloniaFact]
    public void Clear_UndoとRedoの両方が破棄される()
    {
        var stack = new UndoStack();
        var img1 = CreateBitmap();
        var img2 = CreateBitmap();

        stack.Push(img1);
        img1.Dispose();
        var restored = stack.Undo(img2);
        Assert.NotNull(restored);
        restored!.Dispose();

        // Undo スタックと Redo スタックの両方が残っている
        Assert.Equal(0, stack.Count);
        Assert.Equal(1, stack.RedoCount);

        stack.Clear();

        Assert.Equal(0, stack.Count);
        Assert.Equal(0, stack.RedoCount);

        img2.Dispose();
    }

    [AvaloniaFact]
    public void GetUndoHistory_Pushした順で古い順に取得できる()
    {
        var stack = new UndoStack();
        var img1 = CreateBitmap(10);
        var img2 = CreateBitmap(11);

        // Push はクローンを積むため、元 Bitmap は破棄しても履歴には影響しない
        stack.Push(img1);
        stack.Push(img2);
        img1.Dispose();
        img2.Dispose();

        var history = stack.GetUndoHistory();

        // 古い順（_stack[0] が最古）で取得できる（クローンのためサイズで判別）
        Assert.Equal(2, history.Count);
        Assert.Equal(10, history[0].Size.Width);
        Assert.Equal(10, history[0].Size.Height);
        Assert.Equal(11, history[1].Size.Width);
        Assert.Equal(11, history[1].Size.Height);
    }

    [AvaloniaFact]
    public void GetRedoHistory_Undoで退避された順に取得できる()
    {
        var stack = new UndoStack();
        var img1 = CreateBitmap(10);
        var img2 = CreateBitmap(11);
        var img3 = CreateBitmap(12);

        // _stack = [img1', img2']
        stack.Push(img1);
        img1.Dispose();
        stack.Push(img2);
        img2.Dispose();

        // Undo 1回目: 現在の img3 を退避 → _redoStack = [img3']、img2' を復元
        var restored1 = stack.Undo(img3);
        Assert.NotNull(restored1);

        // Undo 2回目: 現在の restored1（img2 のクローン）を退避 → _redoStack = [img3', img2'']、img1' を復元
        var restored2 = stack.Undo(restored1);
        Assert.NotNull(restored2);

        var history = stack.GetRedoHistory();

        // 退避された順（_redoStack[0] が現在に最も近い未来）で取得できる（クローンのためサイズで判別）
        Assert.Equal(2, history.Count);
        Assert.Equal(12, history[0].Size.Width);
        Assert.Equal(12, history[0].Size.Height);
        Assert.Equal(11, history[1].Size.Width);
        Assert.Equal(11, history[1].Size.Height);

        restored1!.Dispose();
        restored2!.Dispose();
        img3.Dispose();
    }

    [AvaloniaFact]
    public void GetUndoHistory_スタックが空なら空リストを返す()
    {
        var stack = new UndoStack();

        Assert.Empty(stack.GetUndoHistory());
        Assert.Empty(stack.GetRedoHistory());
    }
}
