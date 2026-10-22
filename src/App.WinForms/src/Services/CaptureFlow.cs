using EsUtil.SaveEvidence.Parts.CapturePreview.WinForms.Session;

namespace EsUtil.SaveEvidence.App.WinForms.Services;

/// <summary>キャプチャ（スクリーン選択 / ウィンドウ選択 / 範囲選択）の開始を担う窓口。</summary>
/// <remarks>
/// <b>役割</b>: 撮影の実行をファサード（<see cref="CaptureSessionControl"/>）へ委ね、実行中の多重起動を防ぐ。<br/>
/// 確定した画像の反映（Undo 退避・プレビュー差し替え・ステータス通知）はホスト（キャプチャページ）が<br/>
/// <see cref="CaptureSessionControl.CaptureCompleted"/> の購読で行う（画像変更の唯一の経路）。<br/>
/// <br/>
/// <b>呼び出し経路</b>: ツールバーのボタン / メニュー / グローバル ホットキーの 3 経路があるが、<br/>
/// 処理は本クラスへ集約する。<br/>
/// <b>多重起動</b>: 撮影中は <see cref="IsCapturing"/> が true になり、以降の要求を無視する<br/>
/// （ホットキーの連打でオーバーレイが重なるのを防ぐ）。 切り出し中はファサードが受付を拒否する。
/// </remarks>
internal static class CaptureFlow
{
    /// <summary>撮影中かどうかを取得する。</summary>
    /// <value>撮影セッションが進行中の場合は true。</value>
    /// <remarks>撮影はオーバーレイが閉じるまで完了しないため、待機タスクの完了で false へ戻す。</remarks>
    public static bool IsCapturing { get; private set; }

    /// <summary>指定された種別のキャプチャを開始する。</summary>
    /// <param name="type">キャプチャ種別。</param>
    /// <param name="captureSession">撮影・切り出しのファサード（排他とセッション寿命の窓口）。</param>
    /// <param name="owner">オーナーウィンドウ（撮影中の退避とオーバーレイの親）。</param>
    /// <remarks>
    /// <b>完了を待たない</b>: 呼び出し経路（ホットキー・メニュー・ボタン）は同期のため、撮影の完了は待たない。<br/>
    /// 撮影中の判定（<see cref="IsCapturing"/>）は待機タスクの完了で戻す。<br/>
    /// 撮影の失敗（画面取得不可等）はアプリを継続する（原因はファサードが既にログへ記録している）。<br/>
    /// <b>切り出し中</b>はファサードが受付を拒否する（画像は変更されない）。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="captureSession"/> または <paramref name="owner"/> が null の場合。</exception>
    public static void Start(CaptureType type, CaptureSessionControl captureSession, Form owner)
    {
        ArgumentNullException.ThrowIfNull(captureSession);
        ArgumentNullException.ThrowIfNull(owner);

        if (IsCapturing)
        {
            return;
        }

        IsCapturing = true;
        _ = RunAsync(type, owner, captureSession);
    }

    /// <summary>撮影セッションを実行し、完了時に撮影中フラグを戻す。</summary>
    /// <param name="type">キャプチャ種別。</param>
    /// <param name="owner">オーナーウィンドウ。</param>
    /// <param name="captureSession">撮影・切り出しのファサード。</param>
    /// <returns>待機タスク。</returns>
    /// <remarks>
    /// 撮影の開始・実行の失敗（画面取得不可・要素検出の失敗等）ではアプリを継続する。<br/>
    /// 原因はファサードが公開操作の境界で既に記録しているため、ここでは再記録しない。
    /// </remarks>
    private static async Task RunAsync(CaptureType type, Form owner, CaptureSessionControl captureSession)
    {
        try
        {
            await captureSession.StartCaptureAsync(type, owner).ConfigureAwait(true);
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // ファサードが記録済みのため、ここでは握って継続する
            _ = ex;
        }
        finally
        {
            IsCapturing = false;
        }
    }
}
