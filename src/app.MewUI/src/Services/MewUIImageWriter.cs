using EsUtil.Platform.Core.Imaging;
using SkiaSharp;
using EsUtil.Platform.Skia;


namespace EsUtil.SaveEvidence.MewUI.Services;

/// <summary>MewUI 版の画像書き込みサービス。</summary>
/// <remarks>
/// <see cref="EsUtil.SaveEvidence.Services.IImageWriter"/> は UI 実装側がエンコードを担う契約。<br/>
/// MewUI は <c>SkiaCanvasView</c> で SkiaSharp を直接扱えるため、エンコードも SkiaSharp で行う。<br/>
/// 保存形式は拡張子から判別する（判別できない場合は PNG）。<br/>
/// 画質は形式が対応する場合に適用する（PNG では無視される）。
/// </remarks>
internal sealed class MewUIImageWriter : EsUtil.SaveEvidence.Services.IImageWriter
{
    /// <summary>エンコード品質（0〜100）。 非可逆形式に適用する。</summary>
    private const int EncodeQuality = 100;

    /// <summary>拡張子ごとの保存形式（判別できない拡張子は PNG とする）。</summary>
    /// <remarks>BMP は Skia のエンコーダが対応しないため対象外とする（PNG として保存される）。</remarks>
    private static readonly IReadOnlyDictionary<string, SKEncodedImageFormat> s_formats =
        new Dictionary<string, SKEncodedImageFormat>(StringComparer.OrdinalIgnoreCase)
        {
            [".png"] = SKEncodedImageFormat.Png,
            [".jpg"] = SKEncodedImageFormat.Jpeg,
            [".jpeg"] = SKEncodedImageFormat.Jpeg,
            [".webp"] = SKEncodedImageFormat.Webp,
        };

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="image"/> または <paramref name="filePath"/> が null。</exception>
    /// <exception cref="IOException">ファイルを作成できない。</exception>
    /// <exception cref="InvalidOperationException">エンコード結果が得られない。</exception>
    public void Save(EsBitmap image, string filePath)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(filePath);

        var format = ResolveFormat(filePath);

        // ToSKBitmap() は内部インスタンスの共有参照を返すため、破棄してはならない
        using var skImage = SKImage.FromBitmap(image.ToSKBitmap());
        using var data = skImage.Encode(format, EncodeQuality);

        if (data is null)
        {
            throw new InvalidOperationException($"画像を {format} 形式へエンコードできませんでした。");
        }

        using var stream = File.Create(filePath);
        data.SaveTo(stream);
    }

    /// <summary>ファイルパスの拡張子から保存形式を決める。</summary>
    /// <param name="filePath">保存先のファイルパス。</param>
    /// <returns>保存形式。 未知の拡張子の場合は PNG。</returns>
    private static SKEncodedImageFormat ResolveFormat(string filePath)
    {
        var extension = Path.GetExtension(filePath);

        return s_formats.TryGetValue(extension, out var format) ? format : SKEncodedImageFormat.Png;
    }
}

