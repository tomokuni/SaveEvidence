using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using OpenCvSharp;
using System.Runtime.InteropServices;

namespace Estable.AvaloniaUI.LineDetection.Tests;

/// <summary>テスト用の共通ヘルパー。</summary>
/// <remarks>
/// 検出器本体（<see cref="LogicLineDetector"/>）は <see cref="Bitmap"/> を入力に取る。<br/>
/// テストでは PNG を OpenCV（<see cref="Cv2"/>）で読み込み、BGRA の <see cref="WriteableBitmap"/> を構築する。<br/>
/// （Avalonia の <c>new Bitmap(path)</c> はヘッドレスで 1x1 になるため、OpenCV でデコードする）<br/>
/// OpenCV はテスト専用のユーティリティで、検出器の public API には露出しない。
/// </remarks>
internal static class TestHelper
{
    /// <summary>PNG 画像を読み込んで BGRA の Bitmap を構築する。</summary>
    /// <param name="path">PNG ファイルのパス。</param>
    /// <returns>構築した Bitmap。</returns>
    /// <remarks>
    /// 1. <see cref="Cv2.ImRead(string)"/> で BGR 画像を読み込む<br/>
    /// 2. <see cref="ColorConversionCodes.BGR2BGRA"/> で BGRA へ変換する<br/>
    /// 3. <c>Mat.Data</c> から <see cref="WriteableBitmap"/> のピクセルバッファへコピーする
    /// </remarks>
    public static Bitmap LoadBitmap(string path)
    {
        using var mat = Cv2.ImRead(path);
        using var bgra = new Mat();
        Cv2.CvtColor(mat, bgra, ColorConversionCodes.BGR2BGRA);

        var height = bgra.Height;
        var width = bgra.Width;
        var srcStride = (int)bgra.Step();
        var pixels = new byte[height * srcStride];
        Marshal.Copy(bgra.Data, pixels, 0, pixels.Length);

        var bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        using var locked = bitmap.Lock();
        var dstStride = locked.RowBytes;
        unsafe
        {
            // 行ごとにピクセルバッファへ直接コピーする（WriteableBitmap の RowBytes と OpenCV の Step() が異なる場合に対応）
            var dst = (byte*)locked.Address;
            for (var y = 0; y < height; y++)
            {
                var srcRow = y * srcStride;
                var dstRow = y * dstStride;
                for (var x = 0; x < width * 4; x++)
                {
                    dst[dstRow + x] = pixels[srcRow + x];
                }
            }
        }
        return bitmap;
    }
}
