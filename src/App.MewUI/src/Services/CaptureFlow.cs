using Aprillz.MewUI;
using EsUtil.SaveEvidence.Parts.CapturePreview.MewUI;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Native.Win;
using EsUtil.SaveEvidence.App.MewUI.Logging;

namespace EsUtil.SaveEvidence.App.MewUI.Services;

/// <summary>キャプチャ（スクリーン / ウィンドウ / 範囲選択）の開始と結果の反映を担う窓口。</summary>
/// <remarks>
/// <b>役割</b>: 撮影の実行を Facade（<see cref="CaptureSessionControl"/>。 CaptureSessionControl.MewUI）へ委ね、<br/>
/// 確定した画像をプレビューへ反映する。<br/>
/// 呼び出し経路（ツールバーのボタン / グローバルホットキー）は複数あるが、処理は本クラスへ集約する（DRY）。<br/>
/// <br/>
/// <b>結果の反映順序</b>:<br/>
/// 1. 撮影前に表示していた画像を Undo 履歴へ積む（撮影前の状態へ戻せるようにする）。<br/>
/// 2. 保存状態を未保存へ戻す（新しい画像はまだ保存されていない）。<br/>
/// 3. 撮影結果をプレビューへ設定する（所有権は ViewModel へ移る）。<br/>
/// 4. ステータスへ結果（サイズ）を通知する。<br/>
/// <br/>
/// <b>キャンセル時</b>: 何もしない（プレビュー・Undo・ステータスとも変更しない）。<br/>
/// <b>多重起動</b>: 撮影中は <see cref="IsCapturing"/> が true になり、以降の要求を無視する<br/>
/// （ホットキーの連打でオーバーレイが重なるのを防ぐ）。
/// </remarks>
internal static class CaptureFlow
{
    /// <summary>撮影中かどうかを取得する。</summary>
    /// <value>撮影セッションが進行中の場合は true。</value>
    public static bool IsCapturing { get; private set; }

    /// <summary>指定された種別のキャプチャを開始し、結果をプレビューへ反映する。</summary>
    /// <param name="services">アプリ全体で共有するコアオブジェクト。</param>
    /// <param name="type">キャプチャ種別。</param>
    /// <param name="captureSession">撮影・切り出しの Facade（排他とセッション寿命の窓口）。</param>
    /// <remarks>
    /// 1. 撮影中・オーナーウィンドウ未取得の場合は何もしない。<br/>
    /// 2. オーバーレイを表示して選択を待つ（確定した領域が返る。 キャンセル時は null）。<br/>
    /// 3. 画像が返った場合のみ、Undo への退避 → 未保存化 → プレビュー設定 → ステータス通知を行う。<br/>
    /// 4. 例外が発生しても撮影中フラグを必ず戻す（以降のキャプチャが起動できなくなるのを防ぐ）。<br/>
    /// <b>切り出し中</b>は Facade が受付を拒否する（コアの排他規則。 画像は変更されない）。
    /// </remarks>
    public static void Start(AppServices services, CaptureType type, CaptureSessionControl captureSession)
    {
        ArgumentNullException.ThrowIfNull(captureSession);

        if (IsCapturing || Application.Current?.MainWindow is not { } owner)
        {
            return;
        }

        IsCapturing = true;
        try
        {
            // 撮影セッション（UI 非依存のライブラリ）へ、UI 非依存のサービスを渡す。
            // 設定は Facade（CaptureSessionControl）が単一所有するため、ここでは組み立てない。
            // ログはアプリのログ基盤（AppLog）のロガーを渡し、撮影の開始・確定・キャンセルを同じファイルへ記録する。
            // OS 操作の失敗（キャプチャ・列挙・ウィンドウ操作）も同じロガーへ記録される。
            var logger = AppLog.CreateLogger(nameof(CaptureSessionControl));
            var overlayServices = new CaptureOverlayServices(
                new WinCaptureService(logger),
                new WinElementFinder(logger),
                new WinWindowService(logger),
                logger);

            var image = captureSession.Capture(owner, type, overlayServices);
            if (image is null)
            {
                return;
            }

            var viewModel = services.Main;

            // 撮影前の画像へ戻せるように履歴へ積む（現在の画像が null の場合は何も積まれない）
            services.UndoHistory.Push(viewModel.PreviewImage);

            // 差し替え前の画像の破棄と未保存化は MainViewModel（PreviewImage の所有者）が行う
            viewModel.SetPreviewImage(image);
            viewModel.StatusMessage = $"キャプチャしました（{image.Width} x {image.Height}）";
        }
        finally
        {
            IsCapturing = false;
        }
    }
}
