using EsUtil.Platform.Core;
using SkiaSharp;

namespace EsUtil.SaveEvidence.App.WinUI3.Icons;

/// <summary>ウィンドウ（タスク バー）アイコンを ICO として書き出す。</summary>
/// <remarks>
/// <b>書き出す理由</b>: WinUI 3 の <c>AppWindow.SetIcon</c> は<b>アイコン ファイルのパス</b>しか受け付けず、<br/>
/// 実行時にラスタライズした画像を直接渡せない。 他の UI 版と同じ SVG（共有の Fluent アイコン）から<br/>
/// <b>複数サイズの ICO</b> を組み立てて一時ファイルへ書き、そのパスを渡す。<br/>
/// <br/>
/// <b>ICO の形式</b>: 各エントリの画像データを PNG で持つ形式（Windows Vista 以降が対応）。<br/>
/// 32bpp の DIB を自前で組み立てるより単純で、透過もそのまま保持できる。<br/>
/// <br/>
/// <b>失敗時</b>: 書き出せない場合は空文字を返す（アイコンが既定のままでもアプリは動作する）。
/// </remarks>
internal static class WindowIconFile
{
    /// <summary>ウィンドウ アイコンに使う SVG ファイル名。</summary>
    /// <remarks>他の UI 版と同じアイコンを使う（AvaloniaUI 版の <c>MainWindow</c> と同じ選択）。</remarks>
    private const string IconFileName = "ic_fluent_scan_table_24_regular.svg";

    /// <summary>ICO に含めるサイズ（ピクセル）。</summary>
    /// <remarks>タスク バー・Alt+Tab・大きいアイコン表示のそれぞれで必要になるサイズを揃える。</remarks>
    private static readonly int[] s_sizes = [16, 20, 24, 32, 40, 48, 64, 256];

    /// <summary>一度だけ書き出した結果（パス。 失敗した場合は空文字）。</summary>
    private static string? s_path;

    /// <summary>ウィンドウ アイコンのファイルを用意し、そのパスを取得する。</summary>
    /// <returns>ICO ファイルの絶対パス。 書き出せない場合は空文字。</returns>
    /// <remarks>2 回目以降は同じパスを返す（起動時に 1 回だけ書き出す）。</remarks>
    public static string Ensure()
    {
        if (s_path is not null)
        {
            return s_path;
        }

        try
        {
            var directory = Path.Combine(Path.GetTempPath(), "SaveEvidence");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "app-icon.ico");
            File.WriteAllBytes(path, Build());
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
        var images = new List<byte[]>(s_sizes.Length);
        foreach (var size in s_sizes)
        {
            images.Add(RenderPng(size));
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

    /// <summary>指定サイズのアイコンを PNG へ符号化する。</summary>
    /// <param name="size">一辺（ピクセル）。</param>
    /// <returns>PNG のバイト列。</returns>
    private static byte[] RenderPng(int size)
    {
        var info = new SKImageInfo(size, size, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(info);
        using (var canvas = new SKCanvas(bitmap))
        {
            IconLoader.Draw(canvas, IconFileName, size, 0xFF000000);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
