using OpenCvSharp;

namespace EsUtil.SaveEvidence.Parts.LineDetection.Tests;

/// <summary>テスト用の共通ヘルパー。</summary>
/// <remarks>
/// 検出器本体（<see cref="LogicLineDetector"/>）は <see cref="EsReadOnlyBitmap"/>（読み取りの面）を入力に取る。<br/>
/// テストでは PNG を OpenCV（<see cref="Cv2"/>）で読み込み、BGRA の読み取り専用の画像（<see cref="EsReadOnlyBitmap"/>）を構築する。<br/>
/// （Avalonia の <c>new Bitmap(path)</c> はヘッドレスで 1x1 になるため、OpenCV でデコードする）<br/>
/// OpenCV はテスト専用のユーティリティで、検出器の public API には露出しない。
/// </remarks>
internal static class TestHelper
{
    /// <summary>PNG 画像を読み込んで BGRA の画像を構築する。</summary>
    /// <param name="path">PNG ファイルのパス。</param>
    /// <returns>構築した画像（BGRA8888。 読み取りの面）。 破棄は呼び出し側。</returns>
    /// <remarks>
    /// 1. <c>Cv2.ImRead</c> で BGR 画像を読み込む<br/>
    /// 2. <see cref="ColorConversionCodes.BGR2BGRA"/> で BGRA へ変換する<br/>
    /// 3. <c>Mat.Data</c> からコアのピクセル バッファへ直接コピーする（中間バッファなし）<br/>
    /// 4. コピーの完了後は読むだけ（検出器へ渡す）であるため、凍結して読み取り専用の面へ 0 コピーで渡す
    /// </remarks>
    public static EsReadOnlyBitmap LoadBitmap(string path)
    {
        using var mat = Cv2.ImRead(path);
        using var bgra = new Mat();
        Cv2.CvtColor(mat, bgra, ColorConversionCodes.BGR2BGRA);

        var height = bgra.Height;
        var width = bgra.Width;
        var srcStride = (int)bgra.Step();
        var result = EsBitmap.Create(width, height);
        var dstStride = result.Stride;
        var rowBytes = Math.Min(srcStride, dstStride);
        var dst = result.Pixels;
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
        // 以降は読むだけ（検出器へ渡す）: 凍結して 0 コピーで読み取り専用の面へ渡す
        result.SetReadOnly();
        var read = EsReadOnlyBitmap.FromBitmap(result);
        result.Dispose();
        return read;
    }
}
