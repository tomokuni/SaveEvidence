using EsUtil.Platform.Core.Capture;
using EsUtil.Platform.Core;
using EsUtil.SaveEvidence.Parts.CapturePreview.WinUI3.Session;
using Microsoft.UI.Xaml;

namespace EsUtil.SaveEvidence.App.WinUI3.Services;

/// <summary>キャプチャ（スクリーン選択 / ウィンドウ選択 / 範囲選択）の開始を担う窓口。</summary>
/// <remarks>
/// <b>役割</b>: 撮影の実行を撮影セッション（<c>Parts.CapturePreview.ControlWinUI3</c> の<br/>
/// <see cref="CaptureSessionControl"/>）へ委ね、実行中の多重起動を防ぐ。 確定した画像の反映<br/>
/// （Undo 退避・プレビュー差し替え・ステータス通知）はホスト（キャプチャ ページ）が<br/>
/// <c>CaptureCompleted</c> の購読で行う（撮影セッションの公開イベントが画像変更の唯一の経路）。<br/>
/// <br/>
/// <b>呼び出し経路</b>: ツールバーのボタン / メニュー / グローバル ホットキーの 3 経路があるが、<br/>
/// 処理は本クラスへ集約する。<br/>
/// <br/>
/// <b>多重起動</b>: 撮影中は <see cref="IsCapturing"/> が true になり、以降の要求を無視する<br/>
/// （ホットキーの連打でオーバーレイが重なるのを防ぐ）。 切り出し中は撮影セッションが受付を拒否する。
/// </remarks>
internal static class CaptureFlow
{
    /// <summary>撮影中かどうかを取得する。</summary>
    /// <value>撮影セッションが進行中の場合は true。</value>
    public static bool IsCapturing { get; private set; }

    /// <summary>指定された種別のキャプチャを開始する。</summary>
    /// <param name="type">キャプチャ種別。</param>
    /// <param name="owner">オーナー ウィンドウ（撮影中は非表示になる場合がある）。</param>
    /// <param name="captureSession">撮影・切り出しの撮影セッション（排他とセッション寿命の窓口）。</param>
    /// <remarks>
    /// <b>完了を待たない</b>: 呼び出し経路（ホットキー・メニュー・ボタン）は同期のため、撮影の完了は待たない。<br/>
    /// 撮影中の判定（<see cref="IsCapturing"/>）は待機タスクの完了で戻す。<br/>
    /// 撮影の失敗（画面取得不可等）はアプリを継続する（原因は撮影セッションが既にログへ記録している）。<br/>
    /// <b>切り出し中</b>は撮影セッションが受付を拒否する（画像は変更されない）。
    /// </remarks>
    public static void Start(CaptureType type, Window owner, CaptureSessionControl captureSession)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(captureSession);

        if (IsCapturing)
        {
            return;
        }

        IsCapturing = true;
        _ = RunAsync(type, owner, captureSession);
    }

    /// <summary>撮影セッションを実行し、完了時に撮影中フラグを戻す。</summary>
    /// <param name="type">キャプチャ種別。</param>
    /// <param name="owner">オーナー ウィンドウ。</param>
    /// <param name="captureSession">撮影セッション。</param>
    /// <returns>待機タスク。</returns>
    private static async Task RunAsync(CaptureType type, Window owner, CaptureSessionControl captureSession)
    {
        try
        {
            // 待機中は UI スレッドへ戻す（イベント購読と画面の反映は UI スレッドで行う）
            await captureSession.StartCaptureAsync(type, owner);
        }
        catch (Exception ex) when (EsErrors.IsRecoverable(ex))
        {
            // 撮影の開始・実行の失敗（画面取得不可・要素検出の失敗等）ではアプリを継続する。
            // 原因は撮影セッション（公開操作の境界）が既に記録しているため、ここでは再記録しない
            // （同じ例外を 2 回記録すると、原因の特定に不要な重複行が増える）
        }
        finally
        {
            IsCapturing = false;
        }
    }
}
