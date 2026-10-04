namespace EsUtil.SaveEvidence.Parts.LineDetection.Sample.ViewModels;

/// <summary>データセット保存完了イベントの引数（保存したファイルのパス）。</summary>
/// <remarks>
/// イベントの型は <see cref="EventHandler{T}"/>（引数は <see cref="EventArgs"/> 派生）に統一する（CA1003）。
/// </remarks>
/// <param name="path">保存したファイルのパス。</param>
public sealed class DatasetSavedEventArgs(string path) : EventArgs
{
    /// <summary>保存したファイルのパスを取得する。</summary>
    /// <value>保存先の絶対パス。</value>
    public string Path { get; } = path;
}
