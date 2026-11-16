using Microsoft.Extensions.Logging;

namespace EsUtil.Feature.Annotation.Core;

/// <summary>注釈のログ定義（LoggerMessage ソース生成・拡張メソッド）。</summary>
/// <remarks>
/// <b>値を所有する層が記録する</b>: 文書の読み込みと保存は本層（Core）、選択と編集の判断は UI が記録する
/// （規則の所有者は <c>.github/ログ規約.md</c> §2・§5）。<br/>
/// <b>出力先と行の書式はホストが所有する</b>（本層は定義だけを持つ）。 接頭辞の既定値は <c>[Annotation]</c>。
/// </remarks>
internal static partial class AnnotationLogMessage
{
    /// <summary>接頭辞の既定値（ホストが設定しない場合に使う）。</summary>
    internal const string DefaultPrefix = "[Annotation]";

    /// <summary>適用する接頭辞（ホストが <c>LogPrefix</c> で設定する）。</summary>
    internal static string Prefix { get; set; } = DefaultPrefix;

    /// <summary>注釈の読み込みを記録する。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Annotation] 読み込み: {FileName} 注釈={Count} 件</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="fileName">読み込んだファイルの名前。</param>
    /// <param name="count">読み込んだ注釈の件数。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix} 読み込み: {FileName} 注釈={Count} 件")]
    private static partial void LogDocumentLoadedCore(this ILogger logger, string prefix, string fileName, int count);

    /// <summary>注釈の読み込みを記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="fileName">読み込んだファイルの名前。</param>
    /// <param name="count">読み込んだ注釈の件数。</param>
    internal static void LogDocumentLoaded(this ILogger logger, string fileName, int count)
        => logger.LogDocumentLoadedCore(Prefix, fileName, count);
}
