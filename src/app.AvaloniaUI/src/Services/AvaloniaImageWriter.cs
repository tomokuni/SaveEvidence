using Avalonia.Media.Imaging;
using EsUtil.Platform.AvaloniaUI.Imaging;
using EsUtil.Platform.Core.Imaging;

namespace Estable.SaveEvidence.Services;

/// <summary><see cref="IImageWriter"/> の本番実装（Avalonia Skia で画像をエンコードして保存する）。</summary>
/// <remarks>
/// <see cref="EsBitmap"/> を <see cref="WriteableBitmap"/> へ変換し、ファイル拡張子から自動判別した形式で保存する。<br/>
/// Avalonia Skia（<c>RenderTargetBitmap</c> / <c>WriteableBitmap</c>）を必要とするため、ヘッドレス初期化が必要になる。<br/>
/// テストでは <see cref="IImageWriter"/> をモックに差し替え、ヘッドレス不要で検証する。
/// </remarks>
public sealed class AvaloniaImageWriter : IImageWriter
{
    /// <summary>既定の PNG エンコードオプション。</summary>
    private static readonly PngBitmapEncoderOptions s_pngOptions = PngBitmapEncoderOptions.Default;

    /// <inheritdoc/>
    public void Save(EsBitmap image, string filePath)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(filePath);

        // EsBitmap を表示境界で Avalonia Bitmap へ変換して保存（ファイル拡張子から自動判別）
        using var av = image.ToAvaloniaBitmap();
        av.Save(filePath, s_pngOptions);
    }
}
