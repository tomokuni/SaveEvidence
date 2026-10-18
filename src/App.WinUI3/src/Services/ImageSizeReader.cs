using EsUtil.Platform.Core;
using SkiaSharp;

namespace EsUtil.SaveEvidence.App.WinUI3.Services;

/// <summary>画像ファイルの画素サイズを、画素を展開せずに読む。</summary>
/// <remarks>
/// <b>用途</b>: 一覧のサムネイルは<b>幅を固定し、高さを元画像の縦横比で決める</b><br/>
/// （AvaloniaUI 版が <c>Image</c> の幅だけ与えて縮小するのと同じ）。 表示要素を作る時点で高さが決まっていれば、<br/>
/// 画像の生成後に大きさを変えずに済む（縮小キャンバスの大きさを変えると描画が消えるため）。<br/>
/// <b>負荷</b>: <see cref="SKCodec"/> はヘッダーだけを読むため、画素の展開は行わない。
/// </remarks>
internal static class ImageSizeReader
{
    /// <summary>画像ファイルの画素サイズを読む。</summary>
    /// <param name="filePath">対象のファイル パス。</param>
    /// <param name="fallback">読めない場合に使う一辺（正方形として扱う）。</param>
    /// <returns>画素サイズ（読めない場合は <paramref name="fallback"/> の正方形）。</returns>
    public static (int Width, int Height) Read(string filePath, int fallback)
    {
        try
        {
            using var codec = SKCodec.Create(filePath);
            var info = codec.Info;
            return info.Width > 0 && info.Height > 0 ? (info.Width, info.Height) : (fallback, fallback);
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 非対応形式・アクセス拒否などは「正方形」として扱う（一覧の表示は継続する）
            return (fallback, fallback);
        }
    }
}
