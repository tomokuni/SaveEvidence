using Avalonia.Media.Imaging;

namespace app.Models;

/// <summary>
/// 画像の Undo / Redo スタックを管理する。
/// </summary>
/// <remarks>
/// MainWindow の <c>PushUndo</c>/<c>RestoreUndo</c> で使用していたスタックを独立させたもの。<br/>
/// 最大 <see cref="MaxCount"/> 段階まで画像を保管し、超過時は古いものから破棄する。<br/>
/// 画像のクローンは PNG エンコード＋デコードで行い、元の Bitmap に影響を与えない。<br/>
/// スレッドセーフではないので、UI スレッドからのみ操作すること。<br/>
/// Undo 実行時は現在の画像を Redo スタックへ退避し、新しい操作（<see cref="Push"/>）で Redo 履歴をクリアする。
/// </remarks>
public sealed class UndoStack
{
    /// <summary>Undo の最大保存段数。</summary>
    public const int MaxCount = 5;

    private readonly List<Bitmap> _stack = new(MaxCount);
    private readonly List<Bitmap> _redoStack = new(MaxCount);

    /// <summary>現在の Undo スタック深度。</summary>
    public int Count => _stack.Count;

    /// <summary>現在の Redo スタック深度。</summary>
    public int RedoCount => _redoStack.Count;

    /// <summary>Undo スタック内の画像を古い順に取得する（読み取り専用）。</summary>
    /// <returns>Undo スタック内の Bitmap の一覧（_stack[0] が最古）。</returns>
    /// <remarks>参照を返すため、呼び出し元で変更・破棄してはならない。</remarks>
    public IReadOnlyList<Bitmap> GetUndoHistory() => _stack;

    /// <summary>Redo スタック内の画像を古い順（退避された順）に取得する（読み取り専用）。</summary>
    /// <returns>Redo スタック内の Bitmap の一覧（_redoStack[0] が現在に最も近い未来）。</returns>
    /// <remarks>参照を返すため、呼び出し元で変更・破棄してはならない。</remarks>
    public IReadOnlyList<Bitmap> GetRedoHistory() => _redoStack;

    /// <summary>Undo 復元中かどうか。</summary>
    public bool IsRestoring { get; private set; }

    /// <summary>
    /// 現在の画像を Undo スタックにプッシュする。
    /// </summary>
    /// <param name="currentImage">現在表示中の画像。null の場合は何もしない。</param>
    /// <remarks>
    /// 画像は PNG 形式でシリアライズ→デシリアライズして複製されるため、<br/>
    /// 元の <c>Bitmap</c> オブジェクトに変更があっても影響を受けない。<br/>
    /// 新しい操作のため Redo 履歴はクリアされる。
    /// </remarks>
    public void Push(Bitmap? currentImage)
    {
        if (IsRestoring) return;
        if (currentImage is null) return;

        // 新しい操作を加えた時点で Redo 履歴は無効になる
        ClearRedo();

        var copy = Clone(currentImage);
        _stack.Add(copy);
        if (_stack.Count > MaxCount)
        {
            _stack[0].Dispose();
            _stack.RemoveAt(0);
        }
    }

    /// <summary>
    /// Undo を実行する。現在の画像を Redo スタックへ退避し、Undo スタックから最新の画像を返す。
    /// </summary>
    /// <param name="currentImage">現在表示中の画像（Redo スタックへ退避される）。null の場合は退避しない。</param>
    /// <returns>復元された画像。スタックが空の場合は null。</returns>
    /// <remarks>
    /// 呼び出し元は返された <c>Bitmap</c> の所有権を持つため、<br/>
    /// 適切に破棄する責任がある。
    /// </remarks>
    public Bitmap? Undo(Bitmap? currentImage)
    {
        if (_stack.Count == 0) return null;

        // 現在の画像を Redo スタックへ退避（後で Redo で戻せるようにする）
        if (currentImage is not null)
        {
            _redoStack.Add(Clone(currentImage));
            if (_redoStack.Count > MaxCount)
            {
                _redoStack[0].Dispose();
                _redoStack.RemoveAt(0);
            }
        }

        IsRestoring = true;
        try
        {
            var last = _stack[^1];
            _stack.RemoveAt(_stack.Count - 1);
            return last;
        }
        finally
        {
            IsRestoring = false;
        }
    }

    /// <summary>
    /// Redo を実行する。Redo スタックから最新の画像を返す。
    /// </summary>
    /// <returns>復元された画像。スタックが空の場合は null。</returns>
    /// <remarks>
    /// 呼び出し元は返された <c>Bitmap</c> の所有権を持つため、<br/>
    /// 適切に破棄する責任がある。
    /// </remarks>
    public Bitmap? Redo()
    {
        if (_redoStack.Count == 0) return null;

        var last = _redoStack[^1];
        _redoStack.RemoveAt(_redoStack.Count - 1);
        return last;
    }

    /// <summary>画像を PNG 経由で複製する。</summary>
    private static Bitmap Clone(Bitmap source)
    {
        using var ms = new MemoryStream();
        source.Save(ms, PngBitmapEncoderOptions.Default);
        ms.Seek(0, SeekOrigin.Begin);
        return new Bitmap(ms);
    }

    /// <summary>Redo スタック内の画像を全て破棄して空にする。</summary>
    private void ClearRedo()
    {
        foreach (var bmp in _redoStack)
        {
            bmp.Dispose();
        }
        _redoStack.Clear();
    }

    /// <summary>スタック内の全ての画像を破棄し、スタックを空にする。</summary>
    public void DisposeAll()
    {
        Clear();
    }

    /// <summary>スタック内の全ての画像を破棄し、スタックを空にする。</summary>
    public void Clear()
    {
        foreach (var bmp in _stack)
        {
            bmp.Dispose();
        }
        _stack.Clear();
        ClearRedo();
    }
}
