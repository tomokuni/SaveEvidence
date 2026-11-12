using System.Collections.Concurrent;
using Aprillz.MewUI;
using EsUtil.SaveEvidence.App.Core.Models;
using EsUtil.SaveEvidence.App.MewUI.Logging;
using Microsoft.Extensions.Logging;

namespace EsUtil.SaveEvidence.App.MewUI.Views;

/// <summary>一覧のサムネイルを背景で生成するための共通処理（スケジューラと UI スレッドへの反映）。</summary>
/// <remarks>
/// <b>生成は背景で行う</b>: 縮小デコード（ファイル読み込み）と縮小は <see cref="ThumbnailScheduler"/> が持つ
/// 背景の実行で行い、要素への設定だけを UI スレッドで行う（一覧の表示を生成時間で待たせない）。<br/>
/// <b>負荷は表示状態で変える</b>: ページが表示されている間は並列（全力）、隠れている間は 1 件ずつ。
/// ページ側が <see cref="ThumbnailScheduler.IsActive"/> を切り替える。<br/>
/// </remarks>
internal static class ThumbnailBackground
{
    /// <summary>ロガー（背景の生成で想定外の例外が発生した場合に記録する）。</summary>
    private static readonly ILogger s_logger = AppLog.CreateLogger(nameof(ThumbnailBackground));

    /// <summary>UI スレッドのディスパッチャ（<see cref="Initialize"/> が設定する）。</summary>
    /// <remarks>
    /// <b>背景スレッドから <c>Application.Current</c> を参照しない</b>: アプリの初期化前は例外になり、<br/>
    /// 参照できても UI スレッド以外では当てにできない。 UI スレッドで 1 度だけ取得して保持する。
    /// </remarks>
    private static IDispatcher? s_dispatcher;

    /// <summary>ディスパッチャが未設定の間に届いた反映を保留する待ち行列（設定時に UI スレッドで流す）。</summary>
    private static readonly ConcurrentQueue<Action> s_pending = new();

    /// <summary>背景の生成のスケジューラを生成する。</summary>
    /// <returns>ページが保持するスケジューラ。</returns>
    /// <remarks>ページはアプリの生存期間で 1 つ生成されるため、明示的に破棄しない（終了時にプロセスごと解放される）。</remarks>
    public static ThumbnailScheduler CreateScheduler()
        => new(static ex => s_logger.LogThumbnailGenerateError(ex));

    /// <summary>UI スレッドのディスパッチャを取得し、保留していた反映を実行する（UI スレッドから呼ぶ）。</summary>
    /// <remarks>
    /// <c>Application.Run</c> の起動コールバック（ディスパッチャ導入後）で 1 度だけ呼ぶ。<br/>
    /// 起動前の先読みで生成が終わったサムネイルは、ここで初めて一覧へ反映される。
    /// </remarks>
    public static void Initialize()
    {
        s_dispatcher = Application.Current?.Dispatcher;

        while (s_pending.TryDequeue(out var action))
        {
            action();
        }
    }

    /// <summary>背景の生成の結果を UI スレッドで反映する。</summary>
    /// <param name="action">反映する処理。</param>
    /// <remarks>
    /// ディスパッチャが未設定の場合（起動前の先読み）は保留し、<see cref="Initialize"/> で流す<br/>
    /// （背景スレッドのまま UI を触ると例外になる）。
    /// </remarks>
    public static void Post(Action action)
    {
        if (s_dispatcher is not { } dispatcher)
        {
            s_pending.Enqueue(action);
            return;
        }

        if (dispatcher.IsOnUIThread)
        {
            action();
            return;
        }

        dispatcher.BeginInvoke(action);
    }
}
