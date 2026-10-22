using Aprillz.MewUI;
using EsUtil.Platform.Core;
using EsUtil.SaveEvidence.Parts.CapturePreview.MewUI.Session;

namespace EsUtil.SaveEvidence.App.MewUI.Services;

/// <summary>キャプチャ（スクリーン選択 / ウィンドウ選択 / 範囲選択）の開始を担う窓口。</summary>
/// <remarks>
/// <b>役割</b>: 撮影の実行を Facade（<see cref="CaptureSessionControl"/>。 Parts.CapturePreview.ControlMewUI）へ委ね、<br/>
/// 実行中の多重起動を防ぐ。 確定した画像の反映（Undo 退避・プレビュー差し替え・ステータス通知）は<br/>
/// ホスト（<c>CapturePage</c>）が <see cref="CaptureSessionControl.CaptureCompleted"/> の購読で行う<br/>
/// （Facade の公開イベントが画像変更の唯一の経路。 Avalonia 版と同じ分担）。<br/>
/// <br/>
/// <b>呼び出し経路</b>: ツールバーのボタン / メニュー / グローバルホットキーの 3 経路があるが、処理は本クラスへ集約する（DRY）。<br/>
/// <br/>
/// <b>多重起動</b>: 撮影中は <see cref="IsCapturing"/> が true になり、以降の要求を無視する<br/>
/// （ホットキーの連打でオーバーレイが重なるのを防ぐ）。 切り出し中は Facade が受付を拒否する（コアの排他規則）。
/// </remarks>
internal static class CaptureFlow
{
    /// <summary>撮影中かどうかを取得する。</summary>
    /// <value>撮影セッションが進行中の場合は true。</value>
    /// <remarks>撮影はオーバーレイが閉じるまで完了しないため、待機タスクの完了で false へ戻す。</remarks>
    public static bool IsCapturing { get; private set; }

    /// <summary>指定された種別のキャプチャを開始する。</summary>
    /// <param name="type">キャプチャ種別。</param>
    /// <param name="captureSession">撮影・切り出しの Facade（排他とセッション寿命の窓口）。</param>
    /// <remarks>
    /// <b>完了を待たない</b>: 呼び出し経路（ホットキー・メニュー・ボタン）は同期のため、撮影の完了は待たない。<br/>
    /// 撮影中の判定（<see cref="IsCapturing"/>）は待機タスクの完了で戻す（撮影中に再要求されても何もしない）。<br/>
    /// 撮影の失敗（画面取得不可等）はアプリを継続する（原因は Facade が既にログへ記録している）。<br/>
    /// <b>切り出し中</b>は Facade が受付を拒否する（コアの排他規則。 画像は変更されない）。
    /// </remarks>
    public static void Start(CaptureType type, CaptureSessionControl captureSession)
    {
        ArgumentNullException.ThrowIfNull(captureSession);

        if (IsCapturing || Application.Current?.MainWindow is not { } owner)
        {
            return;
        }

        IsCapturing = true;
        _ = RunAsync(type, owner, captureSession);
    }

    /// <summary>撮影セッションを実行し、完了時に撮影中フラグを戻す。</summary>
    /// <param name="type">キャプチャ種別。</param>
    /// <param name="owner">オーナーウィンドウ。</param>
    /// <param name="captureSession">撮影・切り出しの Facade。</param>
    /// <returns>待機タスク。</returns>
    private static async Task RunAsync(CaptureType type, Window owner, CaptureSessionControl captureSession)
    {
        try
        {
            await captureSession.StartCaptureAsync(type, owner).ConfigureAwait(true);
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 撮影の開始・実行の失敗（画面取得不可・要素検出の失敗等）ではアプリを継続する。
            // 原因は Facade（公開 API）が公開操作の境界で既に記録しているため、ここでは再記録しない
            // （同じ例外を 2 回記録すると、原因の特定に不要な重複行が増える）
        }
        finally
        {
            IsCapturing = false;
        }
    }
}
