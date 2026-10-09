using EsUtil.Platform.Core.Imaging;

namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>画像（コアの画像）をファイルへ書き込むサービスインターフェース。</summary>
/// <remarks>
/// <c>MainViewModel.SaveImageAsync</c> から画像のエンコード・ファイル書き込みを分離する。<br/>
/// 本インターフェースは UI フレームワーク非依存（<see cref="IEsBitmap"/> のみに依存）であり、<br/>
/// 本番の実装は <see cref="EsBitmapImageWriter"/>（同じプロジェクト。 コアの保存へ委譲する）だけである
/// （UI ごとの実装を持たないため、保存結果が UI で食い違わない）。<br/>
/// テストではモックに差し替えてヘッドレス不要で検証できる。
/// </remarks>
public interface IImageWriter
{
    /// <summary>画像を指定されたパスへ書き込む。</summary>
    /// <param name="image">書き込む画像（<b>読み取るだけのため <see cref="IEsBitmap"/> を受ける</b>）。</param>
    /// <param name="filePath">保存先のファイルパス。</param>
    /// <exception cref="ArgumentNullException">画像またはファイルパスが null の場合。</exception>
    void Save(IEsBitmap image, string filePath);
}
