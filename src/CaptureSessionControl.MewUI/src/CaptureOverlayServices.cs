using EsUtil.Platform.Core;
using Microsoft.Extensions.Logging;

namespace EsUtil.SaveEvidence.CaptureSession.MewUI;

/// <summary>撮影セッションが必要とするサービスの束。</summary>
/// <param name="Capture">画面キャプチャ（仮想スクリーンの取得）。</param>
/// <param name="Finder">要素検出（カーソル下のモニター・ウィンドウ）。</param>
/// <param name="Window">ネイティブウィンドウ操作（キャプチャ除外・画面外への退避・物理ピクセルでの配置）。</param>
/// <param name="Logger">ロガー（null の場合は出力しない）。</param>
/// <remarks>
/// MewUI 版は DI コンテナを持たないため、ホストが生成したサービスを本レコードで束ねて渡す。<br/>
/// UI 非依存のサービス（<see cref="ICaptureService"/> / <see cref="IElementFinder"/> / <see cref="IWindowService"/>）は<br/>
/// <c>Platform.Core</c> が提供するため、本ライブラリはアプリの型を参照しない。<br/>
/// <br/>
/// <b>設定は含めない</b>: 設定は Facade（<see cref="CaptureSessionControl.AppearanceSettings"/>）が単一所有し、<br/>
/// 撮影と切り出しが同じ値を使う（ホストが変換を 2 回呼ぶ・2 つの設定値が食い違う余地をなくす）。
/// </remarks>
public sealed record CaptureOverlayServices(
    ICaptureService Capture,
    IElementFinder Finder,
    IWindowService Window,
    ILogger? Logger = null);
