using System.Runtime.InteropServices.WindowsRuntime;
using EsUtil.Platform.Core.Common;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.App.WinUI3.Logging;
using Microsoft.Extensions.Logging;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace EsUtil.SaveEvidence.App.WinUI3.Services;

/// <summary>プレビュー画像と OS のクリップボードの相互変換。</summary>
/// <remarks>
/// <b>境界の役割</b>: アプリのコアは UI 型を知らないため、クリップボード（WinRT の投影型）との<br/>
/// 変換は UI 層である本クラスが所有する（メイン ViewModel のコールバックへ配線する）。<br/>
/// <b>受け渡す形式</b>: クリップボードの画像は PNG へ符号化した 1 本のストリームとして渡す<br/>
/// （<c>DataPackage.SetBitmap</c> が要求する <c>IRandomAccessStreamReference</c> を満たす最も確実な形式）。<br/>
/// <b>符号化と復号</b>: WIC（<see cref="BitmapEncoder"/> / <see cref="BitmapDecoder"/>）が担う<br/>
/// （OS 標準の仕組みであり、追加のライブラリを必要としない）。<br/>
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

            return await DecodePngAsync(source);
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            s_logger.LogClipboardError(ex, "貼り付け");
            return null;
        }
    }

    /// <summary>画像を PNG としてストリームへ書き出す（WIC）。</summary>
    /// <param name="image">書き出す画像（読み取りの面）。</param>
    /// <param name="stream">書き出し先のストリーム。</param>
    /// <remarks>
    /// WIC の API は非同期であるため、専用のスレッドへ隔離して待つ（UI スレッドの同期コンテキストで<br/>
    /// 待つとデッドロックする）。<br/>
    /// 画素はコアの面から 1 回だけ複製する（WIC へ渡す <c>SoftwareBitmap</c> は自前の領域を要求する）。
    /// </remarks>
    private static void WritePng(EsReadOnlyBitmap image, IRandomAccessStream stream)
    {
        var task = Task.Run(async () =>
        {
            var pixels = image.ReadOnlyPixels.ToArray();
            var bitmap = SoftwareBitmap.CreateCopyFromBuffer(
                pixels.AsBuffer(),
                BitmapPixelFormat.Bgra8,
                image.Width,
                image.Height,
                BitmapAlphaMode.Premultiplied);

            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            encoder.SetSoftwareBitmap(bitmap);
            await encoder.FlushAsync();
            stream.Seek(0);
        });

        task.GetAwaiter().GetResult();
    }

    /// <summary>PNG ストリームを画像として読み込む（WIC）。</summary>
    /// <param name="stream">読み込み元のストリーム。</param>
    /// <returns>読み込んだ画像。 読み込めない場合は null。</returns>
    /// <remarks>画素はコアの内部表現（BGRA8888・乗算済み）へ揃えて返す。</remarks>
    private static async Task<EsBitmap?> DecodePngAsync(IRandomAccessStreamWithContentType stream)
    {
        var decoder = await BitmapDecoder.CreateAsync(stream);
        var width = (int)decoder.PixelWidth;
        var height = (int)decoder.PixelHeight;
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        using var bitmap = await decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            new BitmapTransform(),
            ExifOrientationMode.RespectExifOrientation,
            ColorManagementMode.DoNotColorManage);

        var stride = width * EsBitmap.BytesPerPixel;
        var pixels = new byte[stride * height];
        bitmap.CopyToBuffer(pixels.AsBuffer());

        return EsBitmap.CreateFilled(width, height, span => pixels.AsSpan().CopyTo(span));
    }
}
