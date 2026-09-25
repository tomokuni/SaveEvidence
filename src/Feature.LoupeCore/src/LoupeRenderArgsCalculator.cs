using EsUtil.Feature.LoupeCore.Imaging;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.LoupeCore;

/// <summary>ルーペの描画完了時の表示情報計算ロジック（純粋関数）。</summary>
/// <remarks>
/// 描画完了の通知に必要な表示情報（切り出し領域の左上とスケール）の計算を<br/>
/// UI 非依存の純粋関数として提供する。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。<br/>
/// 描画完了通知の解決は公開ファサード <see cref="Loupe.ResolveNotification"/> が本計算を吸収して提供する。<br/>
/// <b>外部公開しない</b>: 利用者（UI 実装・ホスト）はファサードの戻り値（<see cref="LoupeRenderNotification"/>）を使うため、
/// 計算の内部手順は本アセンブリと単体テストに限定する。
/// </remarks>
internal static class LoupeRenderArgsCalculator
{
    /// <summary>ルーペの表示情報（描画完了イベント引数用）を計算する。</summary>
    /// <param name="request">描画入力（実際に描画した値を持つ。本メソッドが DIP から再換算しないための契約）。</param>
    /// <returns>
    /// 切り出し領域の左上（ドメイン座標・中心未設定時は null）と、ドメイン座標 → ルーペ画像ピクセル座標の変換係数。
    /// </returns>
    /// <remarks>
    /// 描画に使った値（実際に描画した物理ピクセルサイズを含む）を受け取る（本メソッドが DIP から再換算すると、<br/>
    /// 描画先の領域によるクランプで実際の描画と値がずれ、ホストへ通知する切り出し領域・スケールが食い違うため）。<br/>
    /// 切り出し領域の左上計算は <see cref="LoupeImageExtractor.ComputeCaptureTopLeft"/> に委譲する（単一所有・DRY）。<br/>
    /// 物理ピクセルサイズ・切り出し領域の一辺は、ホストが消費しないため結果に含めない（必要な場合はコアの<br/>
    /// <see cref="LoupePositionCalculator.ResolvePixelSize"/> / <see cref="LoupeImageExtractor.ComputeCaptureSize"/> から直接求める）。
    /// </remarks>
    public static RenderArgs Compute(LoupeRenderRequest request)
    {
        var captureSize = LoupeImageExtractor.ComputeCaptureSize(request.PixelSize, request.ZoomLevel);

        return new RenderArgs(
            request.Center is { } c
                ? LoupeImageExtractor.ComputeCaptureTopLeft((int)c.X, (int)c.Y, captureSize)
                : null,
            request.ZoomLevel);
    }

    /// <summary>ルーペの表示情報（<see cref="Loupe.ResolveNotification"/> が公開契約へ詰め替える内部結果）。</summary>
    /// <param name="CaptureTopLeft">切り出し領域の左上（ドメイン座標）。中心未設定時は null。</param>
    /// <param name="Scale">ドメイン座標 → ルーペ画像ピクセル座標の変換係数（拡大率と同値）。</param>
    internal readonly record struct RenderArgs(EsPoint? CaptureTopLeft, double Scale);
}
