namespace Estable.CaptureSession;

/// <summary>撮影・切り出しセッションの状態（実行中かどうか・完了済みかどうか）を管理する。</summary>
/// <remarks>
/// 再入の禁止と「完了は 1 回だけ」の規則は 2 つの UI で共通であり、UI にフラグを書くと片方だけ抜ける。<br/>
/// そのため本クラスが状態と規則を単一所有する（UI は戻り値を見て分岐するだけ）。<br/>
/// <br/>
/// <b>規則</b>:<br/>
/// 1. 実行中に <see cref="TryBegin"/> を呼ぶと false を返す（既存セッションを継続する）。<br/>
/// 2. 完了（確定・キャンセル）は 1 回だけ受け付ける。2 回目以降の <see cref="TryComplete"/> は false を返す。<br/>
/// 3. <see cref="End"/> で実行中フラグを落とす（後片付け）。完了フラグも同時にリセットされるため、<br/>
///    同じインスタンスを次のセッションへ再利用できる。<br/>
/// <br/>
/// <b>スレッド</b>: 撮影はバックグラウンドの完了通知（仮想スクリーンの取得等）からも触れるため、内部で排他する。
/// </remarks>
public sealed class CaptureSessionState
{
    /// <summary>状態を保護するゲート。</summary>
    private readonly object _gate = new();

    /// <summary>実行中かどうかを取得する。</summary>
    /// <value>実行中の場合は true。</value>
    public bool IsRunning { get; private set; }

    /// <summary>完了済みかどうかを取得する。</summary>
    /// <value>確定またはキャンセルで完了した場合は true。</value>
    public bool IsCompleted { get; private set; }

    /// <summary>セッションを開始する。</summary>
    /// <returns>開始できた場合は true。既に実行中の場合は false（再入禁止）。</returns>
    /// <remarks>開始すると完了フラグはリセットされる（前回のセッションの状態を引き継がない）。</remarks>
    public bool TryBegin()
    {
        lock (_gate)
        {
            if (IsRunning)
            {
                return false;
            }

            IsRunning = true;
            IsCompleted = false;
            return true;
        }
    }

    /// <summary>セッションを完了する（確定またはキャンセル）。</summary>
    /// <returns>完了を受け付けた場合は true。既に完了している・開始していない場合は false。</returns>
    /// <remarks>二重完了（確定とキャンセルの競合、非アクティブ化とキー入力の重なり等）を防ぐ。</remarks>
    public bool TryComplete()
    {
        lock (_gate)
        {
            if (!IsRunning || IsCompleted)
            {
                return false;
            }

            IsCompleted = true;
            return true;
        }
    }

    /// <summary>実行中フラグを落とす（後片付け）。</summary>
    /// <remarks>完了していない場合でも呼べる（例外時の後片付けに使う）。次回の <see cref="TryBegin"/> で再開できる。</remarks>
    public void End()
    {
        lock (_gate)
        {
            IsRunning = false;
            IsCompleted = false;
        }
    }
}
