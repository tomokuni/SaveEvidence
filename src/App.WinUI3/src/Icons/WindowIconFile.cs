using System.Runtime.InteropServices.WindowsRuntime;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Svg;
using EsUtil.SaveEvidence.App.Core.Icons;
using EsUtil.SaveEvidence.App.Core.Services;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace EsUtil.SaveEvidence.App.WinUI3.Icons;

/// <summary>ウィンドウ（タスク バー）アイコンを ICO として書き出す。</summary>
/// <remarks>
/// <b>書き出す理由</b>: WinUI 3 の <c>AppWindow.SetIcon</c> は<b>アイコン ファイルのパス</b>しか受け付けず、<br/>
/// 実行時に作った画素を直接渡せない。 他の UI 版と同じ SVG（共有の Fluent アイコン）から<br/>
/// <b>複数サイズの ICO</b> を組み立てて一時ファイルへ書き、そのパスを渡す。<br/>
/// <br/>
/// <b>画素の作り方</b>: 表示ツリーに依存しない <see cref="SvgPathRasterizer"/> が形状から作る<br/>
/// （表示要素を画面外で描画して読み戻す方法は、要素が表示ツリーに載っていることを要求し、起動直後の<br/>
/// タイミングに依存する）。<br/>
/// <br/>
/// <b>ICO の形式</b>: 各エントリの画像データを PNG で持つ形式（Windows Vista 以降が対応）。<br/>
/// 32bpp の DIB を自前で組み立てるより単純で、透過もそのまま保持できる。 PNG の符号化は WIC<br/>
/// （<see cref="BitmapEncoder"/>）が担う。<br/>
/// <br/>
/// <b>失敗時</b>: 書き出せない場合は空文字を返す（アイコンが既定のままでもアプリは動作する）。
/// </remarks>
internal static class WindowIconFile
{
    /// <summary>ICO に含めるサイズ（ピクセル）。</summary>
    /// <remarks>タスク バー・Alt+Tab・大きいアイコン表示のそれぞれで必要になるサイズを揃える。</remarks>
    private static readonly int[] s_sizes = [16, 20, 24, 32, 40, 48, 64, 256];

    /// <summary>アイコンの色（<c>0xAARRGGBB</c>）。</summary>
    private const uint IconArgb = 0xFF000000;

    /// <summary>一度だけ書き出した結果（パス。 失敗した場合は空文字）。</summary>
    private static string? s_path;

    /// <summary>ウィンドウ アイコンのファイルを用意し、そのパスを取得する。</summary>
    /// <returns>ICO ファイルの絶対パス。 書き出せない場合は空文字。</returns>
    /// <remarks>
    /// 2 回目以降は同じパスを返す（起動時に 1 回だけ書き出す）。<br/>
    /// WIC の API は非同期だが、呼び出し元は起動時の UI スレッドであるため、本メソッドは<br/>
    /// 専用のスレッドへ隔離して待つ（UI の同期コンテキストで待つとデッドロックする）。
    /// </remarks>
    public static string Ensure()
    {
        if (s_path is not null)
        {
            return s_path;
        }

        try
        {
            var directory = AppIdentity.TempFolder;
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "app-icon.ico");
            File.WriteAllBytes(path, Task.Run(Build).GetAwaiter().GetResult());
            s_path = path;
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 書き出せなくてもアプリは動作する（アイコンが既定のままになるだけ）
            s_path = string.Empty;
        }

        return s_path;
    }

    /// <summary>複数サイズの ICO を組み立てる。</summary>
    /// <returns>ICO のバイト列。</returns>
    private static byte[] Build()
    {
        var figures = SvgPathParser.Parse(AppIcons.Source.Load(AppIdentity.IconFileName).PathData[0]);
        var images = new List<byte[]>(s_sizes.Length);
        foreach (var size in s_sizes)
        {
            images.Add(EncodePng(SvgPathRasterizer.Rasterize(figures, size, IconArgb), size));
        }

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        // ICONDIR（予約 0 / 種別 1 = アイコン / エントリ数）
        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)images.Count);

        // ICONDIRENTRY は 16 バイト固定。 画像データの後ろにまとめて置くため、先にオフセットを計算する
        var offset = 6 + (16 * images.Count);
        for (var i = 0; i < images.Count; i++)
        {
            var size = s_sizes[i];

            // 256 は 1 バイトで表せないため 0 と書く（ICO の仕様）
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)0);   // パレット数
            writer.Write((byte)0);   // 予約
            writer.Write((ushort)1); // プレーン数
            writer.Write((ushort)32);
            writer.Write(images[i].Length);
            writer.Write(offset);
            offset += images[i].Length;
        }

        foreach (var image in images)
        {
            writer.Write(image);
        }

        writer.Flush();
        return stream.ToArray();
    }

    /// <summary>画素を PNG へ符号化する（WIC）。</summary>
    /// <param name="pixels">BGRA8888・非乗算の画素。</param>
    /// <param name="size">一辺（ピクセル）。</param>
    /// <returns>PNG のバイト列。</returns>
    private static byte[] EncodePng(byte[] pixels, int size)
        => EncodePngAsync(pixels, size).GetAwaiter().GetResult();

    /// <summary>画素を PNG へ符号化する（WIC の非同期 API）。</summary>
    /// <param name="pixels">BGRA8888・非乗算の画素。</param>
    /// <param name="size">一辺（ピクセル）。</param>
    /// <returns>PNG のバイト列を返すタスク。</returns>
    private static async Task<byte[]> EncodePngAsync(byte[] pixels, int size)
    {
        var bitmap = SoftwareBitmap.CreateCopyFromBuffer(
            pixels.AsBuffer(), BitmapPixelFormat.Bgra8, size, size, BitmapAlphaMode.Straight);

        using var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetSoftwareBitmap(bitmap);
        await encoder.FlushAsync();

        stream.Seek(0);
        var result = new byte[stream.Size];
        using (var reader = new DataReader(stream.GetInputStreamAt(0)))
        {
            await reader.LoadAsync((uint)stream.Size);
            reader.ReadBytes(result);
        }

        return result;
    }
}
