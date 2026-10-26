using EsUtil.Platform.Core.Imaging;
using OpenCvSharp;

namespace EsUtil.SaveEvidence.Parts.LineDetection;

/// <summary>コアの画像 ↔ Mat 変換のヘルパー。</summary>
/// <remarks>OpenCV 依存を内部に閉じ込める（本クラスだけが <c>OpenCvSharp</c> を参照する）。</remarks>
internal static class MatHelper
{
    /// <summary>画像（BGRA8888）を Mat（CV_8UC4）へ変換する。</summary>
    /// <param name="bitmap">変換元の画像。 借用であり破棄しない（読み取るだけのため <see cref="EsReadOnlyBitmap"/> を受ける）。</param>
    /// <returns>変換された Mat（呼び出し元で破棄する）。</returns>
    /// <remarks>
    /// ピクセル バッファ（<see cref="EsReadOnlyBitmap.ReadOnlyPixels"/>）から <see cref="Mat"/> の領域へ**直接**コピーする。<br/>
    /// <b>中間のマネージド配列を作らない</b>: 以前は画像全体の <c>byte[]</c> を確保して 2 回コピーしていた
    /// （配列へコピー → <c>Marshal.Copy</c> で Mat へ）。 検出のたびに幅×高さ×4 バイトの配列を確保しないため、
    /// フレーム全体のコピーが 2 回から 1 回になり、大きな画像での LOH の断片化も避けられる。<br/>
    /// ストライドが一致しない場合は行単位でコピーする（コピー元とコピー先の両方を見る）。
    /// </remarks>
    public static Mat ToMat(EsReadOnlyBitmap bitmap)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;
        var rowBytes = width * EsReadOnlyBitmap.BytesPerPixel;
        var srcStride = bitmap.Stride;
        var mat = new Mat(height, width, MatType.CV_8UC4);

        try
        {
            var dstStride = (int)mat.Step();
            var total = (long)rowBytes * height;

            unsafe
            {
                fixed (byte* src = bitmap.ReadOnlyPixels)
                {
                    var dst = (byte*)mat.Data;
                    if (srcStride == rowBytes && dstStride == rowBytes)
                    {
                        // どちらも詰まっている（最も多い経路）。 1 回のコピーで済む
                        Buffer.MemoryCopy(src, dst, total, total);
                    }
                    else
                    {
                        for (var y = 0; y < height; y++)
                        {
                            Buffer.MemoryCopy(src + (y * srcStride), dst + (y * dstStride), rowBytes, rowBytes);
                        }
                    }
                }
            }

            return mat;
        }
        catch
        {
            // コピーの途中で失敗したら Mat を残さない（ネイティブ領域の解放漏れを防ぐ）
            mat.Dispose();
            throw;
        }
    }
}