using System.Collections.Concurrent;
using EsUtil.SaveEvidence.App.Core.Models;
using EsUtil.SaveEvidence.App.WinUI3.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;

namespace EsUtil.SaveEvidence.App.WinUI3.Services;

/// <summary>一覧のサムネイルを背景で生成するための共通処理（スケジューラ・代替画像・UI スレッドへの反映）。</summary>
/// <remarks>
/// <b>生成は背景で行う</b>: 縮小デコード（ファイル読み込み）と縮小は <see cref="ThumbnailScheduler"/> が持つ<br/>
/// 背景の実行で行い、要素への設定だけを UI スレッドで行う（一覧の表示を生成時間で待たせない）。<br/>
/// <b>負荷は表示状態で変える</b>: ページが表示されている間は並列（全力）、隠れている間は 1 件ずつ。<br/>
/// ページ側が <see cref="ThumbnailScheduler.IsActive"/> を切り替える。<br/>
/// <b>代替画像は寸法ごとに 1 枚を共有する</b>: 呼び出し側は借用して表示するだけで、破棄しない。
/// </remarks>
internal static class ThumbnailBackground
{
    /// <summary>寸法ごとの代替画像（共有するためプロセス内で保持する）。</summary>
    private static readonly ConcurrentDictionary<int, EsReadOnlyBitmap> s_placeholders = new();

    /// <summary>ロガー（背景の生成で想定外の例外が発生した場合に記録する）。</summary>
    private static readonly ILogger s_logger = AppLog.CreateLogger(nameof(ThumbnailBackground));

    /// <summary>UI スレッドのディスパッチャ キュー（起動時に設定する）。</summary>
    /// <remarks>ウィンドウ生成前は null であり、その間の反映は <see cref="s_pending"/> へ保留する。</remarks>
    private static DispatcherQueue? s_dispatcherQueue;

    /// <summary>ディスパッチャ キューが未設定の間に届いた反映を保留する待ち行列。</summary>
    private static readonly ConcurrentQueue<Action> s_pending = new();

    /// <summary>UI スレッドのディスパッチャ キューを設定し、保留していた反映を実行する。</summary>
    /// <param name="dispatcherQueue">UI スレッドのディスパッチャ キュー（UI スレッドから渡す）。</param>
    /// <remarks>ウィンドウの生成（ページの先読みの開始）より前に呼ぶ。</remarks>
    public static void SetDispatcherQueue(DispatcherQueue dispatcherQueue)
    {
        ArgumentNullException.ThrowIfNull(dispatcherQueue);

        s_dispatcherQueue = dispatcherQueue;

        while (s_pending.TryDequeue(out var action))
        {
            action();
        }
    }

    /// <summary>背景の生成のスケジューラを生成する。</summary>
    /// <returns>ページが保持するスケジューラ。</returns>
    /// <remarks>ページはアプリの生存期間で 1 つ生成されるため、明示的に破棄しない（終了時にプロセスごと解放される）。</remarks>
    public static ThumbnailScheduler CreateScheduler()
        => new(static ex => s_logger.LogThumbnailGenerateError(ex));

    /// <summary>生成が終わるまで一覧に表示する代替画像を取得する。</summary>
    /// <param name="size">サムネイルの一辺のサイズ（ピクセル）。</param>
    /// <returns>代替画像（読み取りの面）。 <b>借用</b>（破棄しない）。</returns>
    /// <remarks>
    /// <b>正方形で場所を確保する</b>: 生成が終わるまで実体の縦横比は分からないため、要求寸法の正方形を<br/>
    /// 表示して行の位置を先に決める。<br/>
    /// <b>透明にする理由</b>: 枠の背景（テーマ色）が見えるため、ライト / ダークのどちらでも色が衝突しない。<br/>
    /// <b>共有するため破棄しない</b>（破棄すると他の行の表示が壊れる）。
    /// </remarks>
    public static EsReadOnlyBitmap Placeholder(int size)
        => s_placeholders.GetOrAdd(Math.Max(1, size), static s =>
        {
            using var bitmap = EsBitmap.Create(s, s);
            bitmap.SetReadOnly();
            return EsReadOnlyBitmap.FromBitmap(bitmap);
        });

    /// <summary>背景の生成の結果を UI スレッドで反映する。</summary>
    /// <param name="action">反映する処理。</param>
    /// <remarks>
    /// ディスパッチャ キューが未設定の場合（起動前の先読み）は保留し、設定時に UI スレッドで流す<br/>
    /// （背景スレッドのまま UI を触ると例外になる）。
    /// </remarks>
    public static void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (s_dispatcherQueue is not { } queue)
        {
            s_pending.Enqueue(action);
            return;
        }

        if (queue.HasThreadAccess)
        {
            action();
            return;
        }

        queue.TryEnqueue(() => action());
    }
}
