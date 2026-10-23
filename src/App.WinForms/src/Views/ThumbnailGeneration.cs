using EsUtil.Platform.Core.Imaging;
using EsUtil.Platform.Core;
using EsUtil.SaveEvidence.App.WinForms.Services;
using EsUtil.Ui.WinForms.Thumbnail;

namespace EsUtil.SaveEvidence.App.WinForms.Views;

/// <summary>サムネイルの生成（縮小と一覧への反映）を担う。</summary>
/// <remarks>
/// <b>背景で実行する</b>: 縮小は画素数に比例して重いため、一覧の表示を待たせないよう<br/>
/// スケジューラの実行スレッド（背景）で行い、完成した 1 件から一覧へ反映する。<br/>
/// <b>UI スレッドへの反映</b>: コントロールの操作は UI スレッドに限るため、<c>BeginInvoke</c> で戻す。<br/>
/// <b>中断</b>: 一覧を組み直すと渡されたトークンがキャンセルされる。 キャンセル後に完成した画像は破棄し、<br/>
/// 破棄済みの一覧へは触れない（古い結果を新しい一覧へ登録しない）。
/// </remarks>
internal static class ThumbnailGeneration
{
    /// <summary>サムネイルを生成して一覧へ反映する。</summary>
    /// <param name="list">反映先の一覧。</param>
    /// <param name="index">項目の添字。</param>
    /// <param name="load">元画像を取得する処理（背景スレッドで実行される）。</param>
    /// <param name="token">中断のトークン。</param>
    /// <returns>反映の要求が終わるまで完了しないタスク。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="list"/> または <paramref name="load"/> が null の場合。</exception>
    public static async ValueTask GenerateAsync(
        ThumbnailControl list, int index, Func<EsReadOnlyBitmap?> load, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(load);

        Bitmap? bitmap;
        try
        {
            bitmap = await Task.Run(
                () =>
                {
                    token.ThrowIfCancellationRequested();
                    var source = load();
                    return source is null ? null : GdiBitmapInterop.ToDrawingBitmap(source);
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

        if (list.IsDisposed)
        {
            bitmap.Dispose();
            return;
        }

        // 反映は UI スレッドで行う。 ページを表示する前に生成した項目は自分のハンドルを持たないため、
        // 自分の BeginInvoke ではなく所属するフォームの BeginInvoke を使う
        // （自分のハンドルが無いと BeginInvoke できず、画像を捨ててしまい一覧が空のままになる）。
        void Apply()
        {
            if (list.IsDisposed || token.IsCancellationRequested)
            {
                bitmap.Dispose();
                return;
            }

            list.SetImage(index, bitmap);
        }

        try
        {
            if (list.FindForm() is { IsHandleCreated: true } form && form.InvokeRequired)
            {
                form.BeginInvoke(Apply);
                return;
            }

            Apply();
        }
        catch (Exception ex) when (EsErrors.IsRecoverable(ex))
        {
            // 一覧が既に破棄されている（組み直しと競合した）場合は表示しない
            bitmap.Dispose();
        }
    }
}
