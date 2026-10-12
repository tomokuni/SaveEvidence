using EsUtil.Platform.Core.Imaging;

namespace EsUtil.SaveEvidence.App.Core.Models;

/// <summary>画像の Undo / Redo スタックを管理する。</summary>
/// <remarks>
/// 最大 <see cref="MaxCount"/> 段階まで画像を保管し、超過時は古いものから破棄する。<br/>
/// 履歴の画像は <see cref="EsReadOnlyBitmap.Copy"/>（独立した画素の複製）で保管し、元の画像に影響を与えない。<br/>
/// <b>保持する型は読み取りの面（<see cref="EsReadOnlyBitmap"/>）である</b>: 履歴の画像は取り出すまで書き換えないため、<br/>
/// 書き換えの入口を持たない型で保持する（表示・保存・縮小はすべて読み取りの面で行える）。<br/>
/// スレッドセーフではないので、UI スレッドからのみ操作すること。<br/>
/// Undo 実行時は現在の画像を Redo スタックへ退避し、新しい操作（<see cref="Push"/>）で Redo 履歴をクリアする。
/// </remarks>
public sealed class UndoHistory
{
    /// <summary>Undo の最大保存段数。</summary>
    public const int MaxCount = 5;

    private readonly List<EsReadOnlyBitmap> _stack = new(MaxCount);
    private readonly List<EsReadOnlyBitmap> _redoStack = new(MaxCount);

    /// <summary>現在の Undo スタック深度。</summary>
    public int Count => _stack.Count;

    /// <summary>現在の Redo スタック深度。</summary>
    public int RedoCount => _redoStack.Count;

    /// <summary>Undo スタック内の画像を古い順に取得する（読み取り専用）。</summary>
    /// <returns>Undo スタック内の画像の一覧（_stack[0] が最古）。</returns>
    /// <remarks>参照を返すため、呼び出し元で変更・破棄してはならない。</remarks>
    public IReadOnlyList<EsReadOnlyBitmap> GetUndoHistory() => _stack;

    /// <summary>Redo スタック内の画像を古い順（退避された順）に取得する（読み取り専用）。</summary>
    /// <returns>Redo スタック内の画像の一覧（_redoStack[0] が現在に最も近い未来）。</returns>
    /// <remarks>参照を返すため、呼び出し元で変更・破棄してはならない。</remarks>
    public IReadOnlyList<EsReadOnlyBitmap> GetRedoHistory() => _redoStack;

    /// <summary>Undo 復元中かどうか。</summary>
    public bool IsRestoring { get; private set; }

    /// <summary>現在の画像を Undo スタックにプッシュする。</summary>
    /// <param name="currentImage">現在表示中の画像。 null の場合は何もしない。</param>
    /// <remarks>
    /// 画像は独立した画素へ複製（<see cref="EsBitmap.Copy"/>）して保管するため、元の画像に変更があっても影響を受けない。<br/>
    /// 新しい操作のため Redo 履歴はクリアされる。
    /// </remarks>
    public void Push(EsReadOnlyBitmap? currentImage)
    {
        if (IsRestoring) return;
        if (currentImage is null) return;

        // 新しい操作を加えた時点で Redo 履歴は無効になる
        ClearRedo();

        AddWithLimit(_stack, Copy(currentImage));
    }

    /// <summary>Undo を実行する。 現在の画像を Redo スタックへ退避し、Undo スタックから最新の画像を返す。</summary>
    /// <param name="currentImage">現在表示中の画像（Redo スタックへ退避される）。 null の場合は退避しない。</param>
    /// <returns>復元された画像。 スタックが空の場合は null。</returns>
    /// <remarks>
    /// 呼び出し元は返された画像の所有権を持つため、適切に破棄する責任がある。
    /// </remarks>
    public EsReadOnlyBitmap? Undo(EsReadOnlyBitmap? currentImage)
    {
        if (_stack.Count == 0) return null;

        // 現在の画像を Redo スタックへ退避（後で Redo で戻せるようにする）
        if (currentImage is not null)
        {
            AddWithLimit(_redoStack, Copy(currentImage));
        }

        IsRestoring = true;
        try
        {
            return PopLast(_stack);
        }
        finally
        {
            IsRestoring = false;
        }
    }

    /// <summary>Redo を実行する。 Redo スタックから最新の画像を返す。</summary>
    /// <returns>復元された画像。 スタックが空の場合は null。</returns>
    /// <remarks>
    /// 呼び出し元は返された画像の所有権を持つため、適切に破棄する責任がある。
    /// </remarks>
    public EsReadOnlyBitmap? Redo()
    {
        if (_redoStack.Count == 0) return null;
        return PopLast(_redoStack);
    }

    /// <summary>画像を独立した画素の複製にする（履歴は元画像と画素を共有しない）。</summary>
    /// <param name="source">複製元の画像。 借用であり破棄しない。</param>
    /// <returns>元の画像と独立した画素を持つ複製。 所有権は呼び出し側へ移る。</returns>
    /// <remarks>複製（<see cref="EsReadOnlyBitmap.Copy"/>）にする理由は、履歴の画像が元の画像の破棄で変わらないようにするためである
    /// （同じ実体を保持すると、元を破棄した時点で履歴の画像も読めなくなる）。</remarks>
    private static EsReadOnlyBitmap Copy(EsReadOnlyBitmap source) => source.Copy();

    /// <summary>スタックに画像を追加し、容量超過時は最古の画像を破棄する。</summary>
    /// <param name="stack">追加対象のスタック。</param>
    /// <param name="copy">追加する画像（独立した画素の複製）。</param>
    private static void AddWithLimit(List<EsReadOnlyBitmap> stack, EsReadOnlyBitmap copy)
    {
        stack.Add(copy);
        if (stack.Count > MaxCount)
        {
            stack[0].Dispose();
            stack.RemoveAt(0);
        }
    }

    /// <summary>スタック末尾の画像を取り出して削除する。</summary>
    /// <param name="stack">対象のスタック（空でないこと）。</param>
    /// <returns>末尾の画像。 呼び出し元が所有権を持つ。</returns>
    private static EsReadOnlyBitmap PopLast(List<EsReadOnlyBitmap> stack)
    {
        var last = stack[^1];
        stack.RemoveAt(stack.Count - 1);
        return last;
    }

    /// <summary>Redo スタック内の画像を全て破棄して空にする。</summary>
    private void ClearRedo() => DisposeAll(_redoStack);

    /// <summary>スタック内の全ての画像を破棄し、スタックを空にする。</summary>
    public void Clear()
    {
        DisposeAll(_stack);
        ClearRedo();
    }

    /// <summary>スタック内の画像を全て破棄して空にする。</summary>
    /// <param name="stack">対象のスタック。</param>
    private static void DisposeAll(List<EsReadOnlyBitmap> stack)
    {
        foreach (var bmp in stack)
        {
            bmp.Dispose();
        }
        stack.Clear();
    }
}
