using System.Globalization;
using EsUtil.SaveEvidence.App.Core.Services;
using Microsoft.Extensions.Logging;
using Serilog;

namespace EsUtil.SaveEvidence.App.Core.Logging;

/// <summary>ログの出力先（Serilog のファイル シンク）と MEL ブリッジの構成を単一所有する。</summary>
/// <remarks>
/// <b>4 つの UI 版で 1 つ</b>: 行の書式・出力先・ローテーションはアプリの共通の契約であり、UI 版ごとに
/// 書き分ける理由が無い（規則の所有者は <c>.github/ログ規約.md</c> §1.1・§4.1）。<br/>
/// <b>ファイルは UI 版ごとに分かれる</b>: シンクへ渡す名前は<b>ホストのアセンブリ名</b>である
/// （各 UI の <c>AppLog</c> が自分のアセンブリ名を渡す。 <see cref="AppIdentity.GetLogFilePath"/> が
/// <c>&lt;アセンブリ名&gt;.&lt;yyyyMMdd&gt;.log</c> を組み立てる）。<br/>
/// <b>接頭辞は持たない</b>: 接頭辞（<c>[App.WinForms]</c> 等）はメッセージ本文の先頭へ付く値であり、
/// ホストが自分の接頭辞を渡す（<see cref="AppLogMessage"/>）。
/// </remarks>
internal static class AppLogging
{
    /// <summary>Serilog のファイル シンクと MEL ブリッジを初期化する。</summary>
    /// <param name="assemblyName">ログ ファイル名の先頭に使うアセンブリ名（ホストの実行ファイル）。</param>
    /// <returns>Serilog へ転送する <see cref="ILoggerFactory"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="assemblyName"/> が null の場合。</exception>
    /// <exception cref="ArgumentException"><paramref name="assemblyName"/> が空白の場合。</exception>
    /// <remarks>
    /// <b>静的 <see cref="Log.Logger"/> も設定する</b>: グローバル例外ハンドラ（<c>Log.Fatal</c>）と
    /// <c>ILoggerFactory</c>（MEL ブリッジ）の両方が同じパイプラインを共有する必要がある。<br/>
    /// <b>本メソッドはプロセスで 1 回だけ呼ぶ</b>（複数回呼ぶとログが二重に出力される）。
    /// 各 UI の <c>AppLog</c> が静的フィールドの初期化で 1 回だけ呼ぶ。
    /// </remarks>
    public static ILoggerFactory CreateFactory(string assemblyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyName);

        Log.Logger = CreateConfiguration(assemblyName).CreateLogger();
        return LoggerFactory.Create(builder => builder.AddSerilog(dispose: false));
    }

    /// <summary>Serilog のファイル出力構成を作成する。</summary>
    /// <param name="assemblyName">ログ ファイル名の先頭に使うアセンブリ名。</param>
    /// <returns>ログを <c>%LOCALAPPDATA%\SaveEvidence\logs\&lt;アセンブリ名&gt;.&lt;yyyyMMdd&gt;.log</c>（日次ローテーション・7 日保持）へ書き込む構成。</returns>
    private static LoggerConfiguration CreateConfiguration(string assemblyName)
    {
        return new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            // レベルは 3 文字（[TRC] 等）で表す。 {Level:u3} は Verbose を VRB と表すため使わない
            // （規則の所有者は .github/ログ規約.md §4.1）
            .Enrich.With(new LogLevelTextEnricher())
            .WriteTo.File(
                AppIdentity.GetLogFilePath(assemblyName),
                outputTemplate: "{Timestamp:HH:mm:ss.fff} [{LogLevelText}] [{SourceContext}] {Message:lj}{NewLine}{Exception}",
                formatProvider: CultureInfo.InvariantCulture,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                encoding: System.Text.Encoding.UTF8);
    }

    /// <summary>ログ イベントへ 3 文字のレベル表記（<c>TRC</c> 等）を追加するエンリッチャー。</summary>
    /// <remarks>
    /// 写像の規則は <see cref="LogLevelText"/> が単一所有する。 本クラスは Serilog のレベル名を渡すだけである
    /// （<see cref="LogLevelText"/> は Serilog に依存しない）。
    /// </remarks>
    private sealed class LogLevelTextEnricher : Serilog.Core.ILogEventEnricher
    {
        /// <summary>ログ イベントへ <c>LogLevelText</c> プロパティを追加する。</summary>
        /// <param name="logEvent">対象のログ イベント。</param>
        /// <param name="propertyFactory">プロパティの生成元。</param>
        public void Enrich(Serilog.Events.LogEvent logEvent, Serilog.Core.ILogEventPropertyFactory propertyFactory)
            => logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
                LogLevelText.PropertyName, LogLevelText.Abbreviate(logEvent.Level.ToString())));
    }
}
