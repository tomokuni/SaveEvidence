using EsUtil.Platform.Core.Capture;
using EsUtil.SaveEvidence.Parts.CapturePreview.WinUI3.Session;
using EsUtil.SaveEvidence.App.Core.Services;
using Microsoft.UI.Xaml;

namespace EsUtil.SaveEvidence.App.WinUI3.Services;

/// <summary>キャプチャ（スクリーン選択 / ウィンドウ選択 / 範囲選択）の開始を担う窓口。</summary>
/// <remarks>
/// <b>多重起動の防止と状態の戻しはコアが単一所有する</b>（コアの <c>CaptureLauncher</c>。 3 つの UI 版で同じ規則）。<br/>
/// 本クラスは<b>オーナー ウィンドウの受け渡しだけ</b>を持つ（<c>Window</c> は WinUI 3 の型のため、コアには置けない）。<br/>
/// 確定した画像の反映（Undo 退避・プレビュー差し替え・ステータス通知）はホスト（キャプチャ ページ）が
/// <see cref="CaptureSessionControl.CaptureCompleted"/> の購読で行う（画像変更の唯一の経路）。
/// </remarks>
internal static class CaptureFlow
{
    /// <summary>撮影中かどうかを取得する。</summary>
    /// <value>撮影セッションが進行中の場合は true。</value>
    public static bool IsCapturing => CaptureLauncher.IsCapturing;

    /// <summary>指定された種別のキャプチャを開始する。</summary>
    /// <param name="type">キャプチャ種別。</param>
    /// <param name="owner">オーナー ウィンドウ（撮影中は非表示になる場合がある）。</param>
    /// <param name="captureSession">撮影・切り出しの撮影セッション（排他とセッション寿命の窓口）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> または <paramref name="captureSession"/> が null の場合。</exception>
    public static void Start(CaptureType type, Window owner, CaptureSessionControl captureSession)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(captureSession);

        CaptureLauncher.Start(() => captureSession.StartCaptureAsync(type, owner));
    }
}

