using EsUtil.Platform.Core.Imaging;

namespace EsUtil.SaveEvidence.Services;

/// <summary>画像（EsBitmap）をファイルへ書き込むサービスインターフェース。</summary>
/// <remarks>
/// UI 実装の <c>MainViewModel.SaveImageAsync</c> から画像のエンコード・ファイル書き込みを分離する。<br/>
/// 本インターフェースは UI フレームワーク非依存（<see cref="EsBitmap"/> のみに依存）であり、<br/>
/// 具象実装（Avalonia 版は <c>AvaloniaImageWriter</c>）が各 UI プロジェクト側でエンコードを行う。<br/>
/// テストではモックに差し替えてヘッドレス不要で検証できる。
/// </remarks>
public interface IImageWriter
{
    /// <summary>画像を指定されたパスへ書き込む。</summary>
    /// <param name="image">書き込む画像（EsBitmap）。</param>
    /// <param name="filePath">保存先のファイルパス。</param>
    /// <exception cref="ArgumentNullException">画像またはファイルパスが null の場合。</exception>
    void Save(EsBitmap image, string filePath);
}
