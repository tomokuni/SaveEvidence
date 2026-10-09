using EsUtil.Platform.Core.Imaging;
using EsUtil.Platform.Core.Native.Imaging;
using EsUtil.SaveEvidence.App.Core.Services;

namespace EsUtil.SaveEvidence.App.AvaloniaUI.Services;

/// <summary><see cref="IImageWriter"/> の本番実装（コアの保存（Windows 専用のエンコード）へ委譲する）。</summary>
/// <remarks>
/// <b>保存形式（拡張子からの判別）とエンコード（BMP / PNG / JPEG / GIF / TIFF）は
/// <see cref="EsBitmapFileExtensions.Save"/> が単一所有する</b>（両 UI で同じ結果にする）。<br/>
/// 本クラスは「ホスト（Avalonia アプリ）が契約を満たす」ことを示す薄い実装である。
/// </remarks>
public sealed class AvaloniaImageWriter : IImageWriter
{
    /// <inheritdoc/>
    public void Save(IEsBitmap image, string filePath)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(filePath);

        image.Save(filePath);
    }
}
