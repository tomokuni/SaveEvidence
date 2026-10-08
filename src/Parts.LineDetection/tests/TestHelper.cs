using EsUtil.Platform.Core.Imaging;
using OpenCvSharp;

namespace EsUtil.SaveEvidence.Parts.LineDetection.Tests;

/// <summary>テスト用の共通ヘルパー。</summary>
/// <remarks>
/// 検出器本体（<see cref="LogicLineDetector"/>）は <see cref="IEsBitmap"/>（読み取りの面）を入力に取る。<br/>
/// テストでは PNG を OpenCV（<see cref="Cv2"/>）で読み込み、BGRA の <see cref="EsWritableBitmap"/> を構築する。<br/>
/// （Avalonia の <c>new Bitmap(path)</c> はヘッドレスで 1x1 になるため、OpenCV でデコードする）<br/>
/// OpenCV はテスト専用のユーティリティで、検出器の public API には露出しない。
/// </remarks>
internal static class TestHelper
{
    /// <summary>PNG 画像を読み込んで BGRA の画像を構築する。</summary>
    /// <param name="path">PNG ファイルのパス。</param>
    /// <returns>構築した画像（BGRA8888。 書き換え可能）。</returns>
    /// <remarks>
    /// 1. <c>Cv2.ImRead</c> で BGR 画像を読み込む<br/>
    /// 2. <see cref="ColorConversionCodes.BGR2BGRA"/> で BGRA へ変換する<br/>
    /// 3. <c>Mat.Data</c> からコアのピクセル バッファへ直接コピーする（中間バッファなし）
    /// </remarks>
    public static EsWritableBitmap LoadBitmap(string path)
    {
        using var mat = Cv2.ImRead(path);
        using var bgra = new Mat();
        Cv2.CvtColor(mat, bgra, ColorConversionCodes.BGR2BGRA);

        var height = bgra.Height;
        var width = bgra.Width;
        var srcStride = (int)bgra.Step();
        var result = EsWritableBitmap.Create(width, height);
        var dstStride = result.Stride;
        var rowBytes = Math.Min(srcStride, dstStride);
        var dst = result.WritablePixels;
        unsafe
        {
            // Mat のピクセル バッファからコアのピクセル バッファへ行単位で直接コピーする
            // （中間バッファ・Lock を介さず、安定かつ低アロケーション）。
            var src = (byte*)bgra.Data;
            for (var y = 0; y < height; y++)
            {
                new ReadOnlySpan<byte>(src + y * srcStride, rowBytes).CopyTo(dst.Slice(y * dstStride, rowBytes));
            }
        }
        return result;
    }
}
