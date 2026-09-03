using Estable.AvaloniaUI.LineDetection;
using OpenCvSharp;
using System.Runtime.InteropServices;

namespace Estable.AvaloniaUI.LineDetection.Tests;

/// <summary>テスト用の共通ヘルパー。</summary>
/// <remarks>
/// 検出器本体（<see cref="LogicLineDetector"/>）は OpenCV 非依存の <see cref="PixelImage"/> を入力に取る。<br/>
/// テストでは PNG を OpenCV（<see cref="Cv2"/>）で読み込み、BGRA の <see cref="PixelImage"/> を構築する。<br/>
/// OpenCV はテスト専用のユーティリティで、検出器の public API には露出しない。
/// </remarks>
internal static class TestHelper
{
    /// <summary>PNG 画像を読み込んで BGRA の PixelImage を構築する。</summary>
    /// <param name="path">PNG ファイルのパス。</param>
    /// <returns>構築した PixelImage。</returns>
    /// <remarks>
    /// 1. <see cref="Cv2.ImRead(string)"/> で BGR 画像を読み込む<br/>
    /// 2. <see cref="ColorConversionCodes.BGR2BGRA"/> で BGRA へ変換する<br/>
    /// 3. <c>Mat.Data</c> から <c>byte[]</c> へコピーし、幅・高さ・ストライドから <see cref="PixelImage"/> を構築する
    /// </remarks>
    public static PixelImage LoadPixelImage(string path)
    {
        using var mat = Cv2.ImRead(path);
        using var bgra = new Mat();
        Cv2.CvtColor(mat, bgra, ColorConversionCodes.BGR2BGRA);

        var height = bgra.Height;
        var width = bgra.Width;
        var stride = (int)bgra.Step();
        var pixels = new byte[height * stride];
        Marshal.Copy(bgra.Data, pixels, 0, pixels.Length);

        return new PixelImage(width, height, stride, pixels);
    }
}
