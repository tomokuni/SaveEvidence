using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.App.WinUI3.Logging;
using EsUtil.SaveEvidence.Platform.Skia;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;

namespace EsUtil.SaveEvidence.App.WinUI3.Services;

/// <summary>プレビュー画像と OS のクリップボードの相互変換。</summary>
/// <remarks>
/// <b>境界の役割</b>: アプリのコアは UI 型を知らないため、クリップボード（WinRT の投影型）との<br/>
/// 変換は UI 層である本クラスが所有する（メイン ViewModel のコールバックへ配線する）。<br/>
/// <b>受け渡す形式</b>: クリップボードの画像は PNG へ符号化した 1 本のストリームとして渡す<br/>
/// （<c>DataPackage.SetBitmap</c> が要求する <c>IRandomAccessStreamReference</c> を満たす最も確実な形式）。<br/>
/// <b>失敗時</b>: クリップボードは他のプロセスと共有する資源であり使用中に失敗し得るため、<br/>
/// 例外にせず false / null を返し、原因を Warning で記録する。
/// </remarks>
internal static class CaptureClipboard
{
    /// <summary>ロガー（クリップボード操作の失敗を記録する）。</summary>
    private static readonly ILogger s_logger = AppLog.CreateLogger(nameof(CaptureClipboard));

    /// <summary>プレビュー画像をクリップボードへコピーする。</summary>
    /// <param name="image">コピーする画像（読み取りの面。<b>借用</b>であり破棄しない）。</param>
    /// <returns>コピーできた場合は true。</returns>
    public static bool Copy(EsReadOnlyBitmap image)
    {
        ArgumentNullException.ThrowIfNull(image);

        try
        {
            var stream = new InMemoryRandomAccessStream();
            WritePng(image, stream);

            var data = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            data.SetBitmap(RandomAccessStreamReference.CreateFromStream(stream));

            Clipboard.SetContent(data);
            // アプリ終了後も貼り付けられるように内容を確定する
            Clipboard.Flush();
            return true;
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            s_logger.LogClipboardError(ex, "コピー");
            return false;
        }
    }

    /// <summary>クリップボードの画像を読み込む。</summary>
    /// <returns>読み込んだ画像。 画像が無い・読み込めない場合は null。</returns>
    /// <remarks>読み込んだ画像の所有権は呼び出し側（メイン ViewModel）へ移る。</remarks>
    public static async Task<EsBitmap?> PasteAsync()
    {
        try
        {
            var content = Clipboard.GetContent();
            if (!content.Contains(StandardDataFormats.Bitmap))
            {
                return null;
            }

            var reference = await content.GetBitmapAsync();
            using var source = await reference.OpenReadAsync();

            return DecodePng(source);
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            s_logger.LogClipboardError(ex, "貼り付け");
            return null;
        }
    }

    /// <summary>画像を PNG としてストリームへ書き出す。</summary>
    /// <param name="image">書き出す画像（読み取りの面）。</param>
    /// <param name="stream">書き出し先のストリーム。</param>
    /// <remarks>コアのピクセルはコピーせず、非所有ビュー（<c>ToSKBitmap</c>）を 1 回の符号化で使う。</remarks>
    private static void WritePng(EsReadOnlyBitmap image, IRandomAccessStream stream)
    {
        using var bitmap = image.ToSKBitmap();
        using var skiaImage = SKImage.FromBitmap(bitmap);
        using var encoded = skiaImage.Encode(SKEncodedImageFormat.Png, 100);
        using var target = stream.AsStreamForWrite();

        encoded.SaveTo(target);
        target.Flush();
        stream.Seek(0);
    }

    /// <summary>PNG ストリームを画像として読み込む。</summary>
    /// <param name="stream">読み込み元のストリーム。</param>
    /// <returns>読み込んだ画像。 読み込めない場合は null。</returns>
    private static EsBitmap? DecodePng(IRandomAccessStreamWithContentType stream)
    {
        using var source = stream.AsStreamForRead();
        var codec = CreateCodec(source);
        if (codec is null)
        {
            return null;
        }

        using (codec)
        {
            var info = codec.Info;
            using var bitmap = new SKBitmap(new SKImageInfo(info.Width, info.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
            var result = codec.GetPixels(bitmap.Info, bitmap.GetPixels());
            return result is SKCodecResult.Success or SKCodecResult.IncompleteInput ? bitmap.ToEsBitmap() : null;
        }
    }

    /// <summary>ストリームから画像コーデックを生成する。</summary>
    /// <param name="stream">対象のストリーム。</param>
    /// <returns>生成したコーデック。 判別できない場合は null。</returns>
    /// <remarks>
    /// SkiaSharp の <c>SKCodec.Create</c> は失敗時に null を返すが、注釈は非 null である。<br/>
    /// 生成と判定を別のメソッドへ分けることで、注釈を信用した到達不能な判定（CA1508）にしない。
    /// </remarks>
    private static SKCodec? CreateCodec(Stream stream)
    {
        var codec = SKCodec.Create(stream);
        return codec;
    }
}
