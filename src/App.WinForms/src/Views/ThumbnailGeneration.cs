using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.App.WinForms.Services;

namespace EsUtil.SaveEvidence.App.WinForms.Views;

/// <summary>サムネイルの生成（縮小と項目への反映）を担う。</summary>
/// <remarks>
/// <b>背景で実行する</b>: 縮小は画素数に比例して重いため、一覧の表示を待たせないよう<br/>
/// スケジューラの実行スレッド（背景）で行い、完成した 1 件から項目へ反映する。<br/>
/// <b>UI スレッドへの反映</b>: コントロールの操作は UI スレッドに限るため、<c>BeginInvoke</c> で戻す。<br/>
/// <b>中断</b>: 一覧を組み直すと渡されたトークンがキャンセルされる。 キャンセル後に完成した画像は破棄し、<br/>
/// 破棄済みの項目へは触れない（古い結果を新しい一覧へ登録しない）。
/// </remarks>
internal static class ThumbnailGeneration
{
    /// <summary>サムネイルを生成して項目へ反映する。</summary>
    /// <param name="item">反映先の項目。</param>
    /// <param name="load">元画像を取得する処理（背景スレッドで実行される）。</param>
    /// <param name="iconSize">サムネイルの長辺の上限（ピクセル）。</param>
    /// <param name="token">中断のトークン。</param>
    /// <returns>反映の要求が終わるまで完了しないタスク。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> または <paramref name="load"/> が null の場合。</exception>
    public static async ValueTask GenerateAsync(
        ThumbnailItem item, Func<EsReadOnlyBitmap?> load, int iconSize, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(load);

        Bitmap? bitmap;
        try
        {
            bitmap = await Task.Run(
                () =>
                {
                    token.ThrowIfCancellationRequested();
                    var source = load();
                    return source is null ? null : ImageThumbnailLoader.ToThumbnailBitmap(source, iconSize);
                },
                token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 一覧を組み直した（古い結果は反映しない）
            return;
        }

        if (bitmap is null)
        {
            return;
        }

        if (item.IsDisposed || !item.IsHandleCreated)
        {
            bitmap.Dispose();
            return;
        }

        try
        {
            item.BeginInvoke(() =>
            {
                if (item.IsDisposed || token.IsCancellationRequested)
                {
                    bitmap.Dispose();
                    return;
                }

                item.SetImage(bitmap);
            });
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 項目が既に破棄されている（一覧の組み直しと競合した）場合は表示しない
            bitmap.Dispose();
        }
    }
}
