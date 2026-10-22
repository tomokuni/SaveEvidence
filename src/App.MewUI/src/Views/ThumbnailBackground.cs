using System.Collections.Concurrent;
using Aprillz.MewUI;
using EsUtil.SaveEvidence.App.Core.Models;
using EsUtil.SaveEvidence.App.MewUI.Logging;
using Microsoft.Extensions.Logging;

namespace EsUtil.SaveEvidence.App.MewUI.Views;

/// <summary>一覧のサムネイルを背景で生成するための共通処理（スケジューラ・代替画像・UI スレッドへの反映）。</summary>
/// <remarks>
/// <b>生成は背景で行う</b>: 縮小デコード（ファイル読み込み）と縮小は <see cref="ThumbnailScheduler"/> が持つ
/// 背景の実行で行い、要素への設定だけを UI スレッドで行う（一覧の表示を生成時間で待たせない）。<br/>
/// <b>負荷は表示状態で変える</b>: ページが表示されている間は並列（全力）、隠れている間は 1 件ずつ。
/// ページ側が <see cref="ThumbnailScheduler.IsActive"/> を切り替える。<br/>
/// <b>代替画像は寸法ごとに 1 枚を共有する</b>: 呼び出し側は借用して表示するだけで、破棄しない。
/// </remarks>
internal static class ThumbnailBackground
{
    /// <summary>寸法ごとの代替画像（共有するためプロセス内で保持する）。</summary>
    private static readonly ConcurrentDictionary<int, EsReadOnlyBitmap> s_placeholders = new();

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

    /// <summary>生成が終わるまで一覧に表示する代替画像を取得する。</summary>
    /// <param name="size">サムネイルの一辺のサイズ（ピクセル）。</param>
    /// <returns>代替画像（読み取りの面）。<b>借用</b>（破棄しない）。</returns>
    /// <remarks>
    /// <b>正方形で場所を確保する</b>: 生成が終わるまで実体の縦横比は分からないため、要求寸法の正方形を表示して
    /// 行の位置を先に決める。<br/>
    /// <b>透明にする理由</b>: 枠の背景（テーマ色）が見えるため、ライト / ダークのどちらでも色が衝突しない。<br/>
    /// <b>共有するため破棄しない</b>（破棄すると他の行の表示が壊れる）。<br/>
    /// <b>読み取りの面で保持する理由</b>: 表示は読むだけ（書き換えない）であるため、生成後に凍結して 0 コピーで渡す。
    /// </remarks>
    public static EsReadOnlyBitmap Placeholder(int size)
        => s_placeholders.GetOrAdd(Math.Max(1, size), static s =>
        {
            using var bitmap = EsBitmap.Create(s, s);
            bitmap.SetReadOnly();
            return EsReadOnlyBitmap.FromBitmap(bitmap);
        });

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
