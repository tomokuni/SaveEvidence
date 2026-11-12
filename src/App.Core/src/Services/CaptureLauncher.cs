using EsUtil.Platform.Core;

namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>撮影（スクリーン選択 / ウィンドウ選択 / 範囲選択）の開始を受け付け、実行中の多重起動を防ぐ。</summary>
/// <remarks>
/// <b>なぜ本層が持つか</b>: <b>実行中は新しい撮影を始めない</b>という規則（ホットキーの連打でオーバーレイが
/// 重なるのを防ぐ）と、<b>完了時に状態を戻す</b>手順は UI に依存しない。 UI 版ごとに書き分けると
/// <b>片方だけ直したときに多重起動の抑止が崩れる</b>（実測 2026-11-13: 3 か所に同じ実装があった）。<br/>
/// <b>UI 層に残すもの</b>: オーナー ウィンドウの解決（<c>Window</c> / <c>Form</c> は UI の型である）と、
/// セッションの開始メソッドの呼び出しだけ（<see cref="Start"/> へデリゲートとして渡す）。<br/>
/// <b>完了を待たない</b>: 呼び出し経路（ホットキー・メニュー・ボタン）は同期のため、撮影の完了は待たない。
/// <b>失敗しても継続する</b>: 画面取得不可などの失敗は握って継続する（原因は撮影スタックが公開操作の境界で
/// 既に記録している。 ここで再記録すると同じ例外が 2 行になる）。
/// </remarks>
internal static class CaptureLauncher
{
    /// <summary>撮影中かどうかを取得する。</summary>
    /// <value>撮影セッションが進行中の場合は true。</value>
    /// <remarks>撮影はオーバーレイが閉じるまで完了しないため、開始したタスクの完了で false へ戻す。</remarks>
    public static bool IsCapturing { get; private set; }

    /// <summary>撮影を開始する（実行中は何もしない）。</summary>
    /// <param name="capture">撮影セッションを開始する処理。</param>
    /// <exception cref="ArgumentNullException"><paramref name="capture"/> が null の場合。</exception>
    /// <remarks>
    /// 開始したタスクは保持しない（呼び出し元は同期であり、完了を待つ相手がいない）。<br/>
    /// 失敗は握って継続し、状態だけを必ず戻す（例外を投げるとホットキーのメッセージ処理を壊す）。
    /// </remarks>
    public static void Start(Func<Task> capture)
    {
        ArgumentNullException.ThrowIfNull(capture);

        if (IsCapturing)
        {
            return;
        }

        IsCapturing = true;
        _ = RunAsync(capture);
    }

    /// <summary>撮影セッションを実行し、完了時に撮影中フラグを戻す。</summary>
    /// <param name="capture">撮影セッションを開始する処理。</param>
    /// <returns>待機タスク。</returns>
    private static async Task RunAsync(Func<Task> capture)
    {
        try
        {
            await capture().ConfigureAwait(true);
        }
        catch (Exception ex) when (EsErrors.IsRecoverable(ex))
        {
            // 撮影スタックが公開操作の境界で記録済みのため、ここでは握って継続する
        }
        finally
        {
            IsCapturing = false;
        }
    }
}

