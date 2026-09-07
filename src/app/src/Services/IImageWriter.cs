using Estable.AvaloniaUI.Platform.Imaging;

namespace Estable.SaveEvidence.Services;

/// <summary>画像（PbBitmap）をファイルへ書き込むサービスインターフェース。</summary>
/// <remarks>
/// <see cref="MainViewModel.SaveImageAsync"/> から画像のエンコード・ファイル書き込みを分離する。<br/>
/// 本番実装（<see cref="AvaloniaImageWriter"/>）は Avalonia Skia（<c>WriteableBitmap</c>）に依存するため、
/// テストではモックに差し替えてヘッドレス不要で検証できるようにする。
/// </remarks>
public interface IImageWriter
{
    /// <summary>画像を指定されたパスへ書き込む。</summary>
    /// <param name="image">書き込む画像（PbBitmap）。</param>
    /// <param name="filePath">保存先のファイルパス。</param>
    /// <exception cref="ArgumentNullException">画像またはファイルパスが null の場合。</exception>
    void Save(PbBitmap image, string filePath);
}
