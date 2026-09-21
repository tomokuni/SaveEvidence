using Estable.CaptureSession;
using Estable.Platform;
using Microsoft.Extensions.Logging;

namespace Estable.MewUI.CaptureSession;

/// <summary>撮影セッションが必要とするサービスと設定の束。</summary>
/// <param name="Capture">画面キャプチャ（仮想スクリーンの取得）。</param>
/// <param name="Finder">要素検出（カーソル下のモニター・ウィンドウ）。</param>
/// <param name="Window">ネイティブウィンドウ操作（キャプチャ除外・画面外への退避・物理ピクセルでの配置）。</param>
/// <param name="Settings">キャプチャ設定（オーバーレイの外観・動作）。</param>
/// <param name="Logger">ロガー（null の場合は出力しない）。</param>
/// <remarks>
/// MewUI 版は DI コンテナを持たないため、ホストが生成したサービスを本レコードで束ねて渡す。<br/>
/// UI 非依存のサービス（<see cref="ICaptureService"/> / <see cref="IElementFinder"/> / <see cref="IWindowService"/>）は<br/>
/// <c>PlatformFactory.Core</c> が提供するため、本ライブラリはアプリの型を参照しない。
/// </remarks>
public sealed record CaptureOverlayServices(
    ICaptureService Capture,
    IElementFinder Finder,
    IWindowService Window,
    CaptureSettings Settings,
    ILogger? Logger = null)
{
    /// <summary>オーバーレイウィンドウのタイトルを取得または設定する。</summary>
    /// <value>ウィンドウのタイトル。既定値は <c>SaveEvidence キャプチャ</c>。</value>
    /// <remarks>検証スクリプト等がウィンドウを識別するために使う（タイトルに「キャプチャ」を含めること）。</remarks>
    public string Title { get; init; } = "SaveEvidence キャプチャ";
}
