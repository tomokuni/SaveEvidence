using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;

namespace EsUtil.SaveEvidence.CaptureSession;

/// <summary>仮想スクリーン画像の取得（バックグラウンド実行）を待つ手順を単一所有する。</summary>
/// <remarks>
/// 画面キャプチャは DWM（デスクトップ合成）の応答不能時に長時間ブロックし得るため、取得を待ち続けると<br/>
/// オーバーレイが背景なしのまま操作不能になる。この<b>上限（タイムアウト）で打ち切る</b>判断と、<br/>
/// <b>遅れて完了した未使用の結果を破棄する</b>規則は UI フレームワークに依存しないため、本クラスが単一所有する<br/>
/// （UI が <c>Task.WhenAny</c> / <c>Task.Delay</c> / 継続での破棄を書くと、UI ごとに打ち切り方と破棄が食い違う）。<br/>
/// <br/>
/// 取得そのもの（<c>ICaptureService.CaptureVirtualScreen</c> の呼び出し）と、結果の表示（UI スレッドへの反映）は<br/>
/// 呼び出し側が担う（本クラスは「待ち方」だけを持つ）。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public static class CaptureVirtualScreen
{
    /// <summary>撮影対象となる範囲（全モニタの包含矩形）を求める。</summary>
    /// <param name="screenBounds">モニタの矩形（画面物理ピクセル）。null 可。</param>
    /// <returns>全モニタを含む最小の矩形。モニタが 1 つも無い場合は null（撮影する領域が無い）。</returns>
    /// <remarks>
    /// <b>撮影は全モニタ（仮想スクリーン）を対象にする</b>という判断と、<b>モニタが無ければ撮影しない</b>という
    /// 前提条件を単一所有する（UI がループを書くと、空のときに 0×0 のウィンドウを作る等の差が出る）。<br/>
    /// 座標は画面物理ピクセル（プライマリモニタが (0, 0) にない場合は負値を含む）。
    /// </remarks>
    public static EsRectInt? ResolveBounds(IEnumerable<EsRectInt>? screenBounds)
    {
        if (screenBounds is null)
        {
            return null;
        }

        EsRectInt? union = null;
        foreach (var bounds in screenBounds)
        {
            union = union is { } current ? current.Union(bounds) : bounds;
        }

        return union;
    }

    /// <summary>仮想スクリーンの取得をタイムアウト付きで待つ。</summary>
    /// <param name="captureTask">仮想スクリーン取得のタスク（バックグラウンドで実行されたもの）。</param>
    /// <param name="timeout">待ち時間の上限。既定は <see cref="CaptureTiming.ScreenCaptureTimeout"/>。</param>
    /// <returns>取得できた画像。タイムアウトした場合・取得に失敗した場合は null。</returns>
    /// <remarks>
    /// 1. 取得の完了とタイムアウトの早い方まで待つ。<br/>
    /// 2. 取得が先に完了した場合はその結果（画像または null）を返す。<br/>
    /// 3. タイムアウトした場合は null を返し、<b>遅れて完了した結果は破棄する</b><br/>
    ///    （呼び出し側は表示しないため、保持しても使われずメモリを占有するだけになる）。<br/>
    /// <br/>
    /// 呼び出し側は null を「取得できなかった」として扱い、代替の取得経路（領域指定のキャプチャ）へ委ねる。
    /// </remarks>
    public static async Task<EsBitmap?> AwaitAsync(
        Task<EsBitmap?> captureTask, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(captureTask);
        var limit = timeout ?? CaptureTiming.ScreenCaptureTimeout;

        var done = await Task.WhenAny(captureTask, Task.Delay(limit)).ConfigureAwait(false);
        if (done == captureTask)
        {
            return await captureTask.ConfigureAwait(false);
        }

        // タイムアウト: 遅れて完了した取得結果は未使用のため、完了した時点で破棄する
        _ = captureTask.ContinueWith(
            static t =>
            {
                if (t.IsCompletedSuccessfully)
                {
                    t.Result?.Dispose();
                }
            },
            TaskScheduler.Default);
        return null;
    }
}
