using Estable.AvaloniaUI.Platform.Imaging;
using OpenCvSharp;
using PbBitmap = Estable.Media.EsBitmap;

namespace Estable.AvaloniaUI.LineDetection.Tests;

/// <summary>テスト用の共通ヘルパー。</summary>
/// <remarks>
/// 検出器本体（<see cref="LogicLineDetector"/>）は <see cref="PbBitmap"/> を入力に取る。<br/>
/// テストでは PNG を OpenCV（<see cref="Cv2"/>）で読み込み、BGRA の <see cref="PbBitmap"/> を構築する。<br/>
/// （Avalonia の <c>new Bitmap(path)</c> はヘッドレスで 1x1 になるため、OpenCV でデコードする）<br/>
/// OpenCV はテスト専用のユーティリティで、検出器の public API には露出しない。<br/>
/// <b>PbBitmap 採用理由</b>: ヘッドレスでは <c>WriteableBitmap</c> のピクセル保持が不完全なため、
/// 検出器の入力を <see cref="PbBitmap"/>（SkiaHub 内部・安定）にして厳密検証をヘッドレスで成立させる。
/// </remarks>
internal static class TestHelper
{
    /// <summary>PNG 画像を読み込んで BGRA の PbBitmap を構築する。</summary>
    /// <param name="path">PNG ファイルのパス。</param>
    /// <returns>構築した PbBitmap（BGRA8888）。</returns>
    /// <remarks>
    /// 1. <see cref="Cv2.ImRead(string)"/> で BGR 画像を読み込む<br/>
    /// 2. <see cref="ColorConversionCodes.BGR2BGRA"/> で BGRA へ変換する<br/>
    /// 3. <c>Mat.Data</c> から <see cref="PbBitmap"/> のピクセルバッファへ直接コピーする（中間バッファなし）
    /// </remarks>
    public static PbBitmap LoadBitmap(string path)
    {
        using var mat = Cv2.ImRead(path);
        using var bgra = new Mat();
        Cv2.CvtColor(mat, bgra, ColorConversionCodes.BGR2BGRA);

        var height = bgra.Height;
        var width = bgra.Width;
        var srcStride = (int)bgra.Step();
        var result = PbBitmap.Create(width, height);
        var dstStride = result.Stride;
        unsafe
        {
            // Mat のピクセルバッファから PbBitmap のピクセルバッファへ行単位で直接コピーする
            // （WriteableBitmap の中間バッファ・Lock を介さず、安定かつ低アロケーション）。
            var src = (byte*)bgra.Data;
            var dst = (byte*)result.Address;
            for (var y = 0; y < height; y++)
            {
                Buffer.MemoryCopy(
                    src + y * srcStride,
                    dst + y * dstStride,
                    dstStride,
                    Math.Min(srcStride, dstStride));
            }
        }
        return result;
    }
}
