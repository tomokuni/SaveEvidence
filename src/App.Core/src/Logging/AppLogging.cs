using System.Globalization;
using EsUtil.SaveEvidence.App.Core.Enums;
using EsUtil.SaveEvidence.App.Core.Models;
using EsUtil.SaveEvidence.App.Core.Services;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

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
    /// <summary>出力する下限レベル（設定 <c>Settings.LogOutputLevel</c> に追従する）。</summary>
    /// <remarks>
    /// <b>スイッチを経由する理由</b>: Serilog の <see cref="Serilog.Core.LoggingLevelSwitch"/> は
    /// ロガーを作り直さずに下限を切り替えられる（設定の変更が<b>再起動なしで</b>効く）。 既定は
    /// <see cref="LogOutputLevel.Trace"/>（すべて出力する）。<br/>
    /// <b>本クラスが単一所有する</b>: 下限の決定（設定 → レベル）はログの構成の一部であり、UI 版ごとに
    /// 書くと片方だけ写像がずれる（規則の所有者は <c>.github/ログ規約.md</c> §2）。
    /// </remarks>
    private static readonly Serilog.Core.LoggingLevelSwitch s_levelSwitch = new(LogEventLevel.Verbose);

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

    /// <summary>設定の出力レベルをログの構成へ反映し、以後の変更にも追従させる。</summary>
    /// <param name="settings">アプリの設定（<c>LogOutputLevel</c> の所有者）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> が null の場合。</exception>
    /// <remarks>
    /// <b>起動時に各 UI 版の合成ルートが 1 回だけ呼ぶ</b>（設定を読み込んだ直後）。 以後は
    /// <see cref="Settings.LogOutputLevel"/> の変更通知で下限を切り替えるため、設定ページは
    /// 値を書き換えるだけでよい（UI 版ごとに切り替えの手順を書かない）。<br/>
    /// <b>本メソッドはプロセスで 1 回だけ呼ぶ</b>（複数回呼ぶと変更通知の購読が重複する）。
    /// </remarks>
    public static void ApplySettings(Settings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        SetMinimumLevel(settings.LogOutputLevel);
        settings.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(Settings.LogOutputLevel), StringComparison.Ordinal))
            {
                SetMinimumLevel(settings.LogOutputLevel);
            }
        };
    }

    /// <summary>出力する下限レベルを切り替える。</summary>
    /// <param name="level">設定の出力レベル。</param>
    /// <remarks>同じ値を再設定しても何も起きない（<see cref="Serilog.Core.LoggingLevelSwitch"/> が吸収する）。</remarks>
    internal static void SetMinimumLevel(LogOutputLevel level)
        => s_levelSwitch.MinimumLevel = ToLogEventLevel(level);

    /// <summary>出力する下限レベル（現在値）を取得する。</summary>
    /// <remarks>設定の反映を確かめるための参照（テストと診断用。 ログの出力そのものには使わない）。</remarks>
    internal static LogEventLevel MinimumLevel => s_levelSwitch.MinimumLevel;

    /// <summary>設定の出力レベルを Serilog のレベルへ写す。</summary>
    /// <param name="level">設定の出力レベル。</param>
    /// <returns>Serilog のレベル。</returns>
    /// <remarks>
    /// <b>名前が違うのは 2 つだけ</b>である（<c>Trace</c> は Serilog では <c>Verbose</c>、
    /// <c>Critical</c> は <c>Fatal</c>）。 取り違えると下限が 1 段ずれるため、写像はここだけに置く
    /// （規則の所有者は <c>.github/ログ規約.md</c> §2）。
    /// </remarks>
    internal static LogEventLevel ToLogEventLevel(LogOutputLevel level) => level switch
    {
        LogOutputLevel.Trace => LogEventLevel.Verbose,
        LogOutputLevel.Debug => LogEventLevel.Debug,
        LogOutputLevel.Information => LogEventLevel.Information,
        LogOutputLevel.Warning => LogEventLevel.Warning,
        LogOutputLevel.Error => LogEventLevel.Error,
        LogOutputLevel.Critical => LogEventLevel.Fatal,
        _ => LogEventLevel.Verbose,
    };

    /// <summary>Serilog のファイル出力構成を作成する。</summary>
    /// <param name="assemblyName">ログ ファイル名の先頭に使うアセンブリ名。</param>
    /// <returns>ログを <c>%LOCALAPPDATA%\SaveEvidence\logs\&lt;アセンブリ名&gt;.&lt;yyyyMMdd&gt;.log</c>（日次ローテーション・7 日保持）へ書き込む構成。</returns>
    private static LoggerConfiguration CreateConfiguration(string assemblyName)
    {
        return new LoggerConfiguration()
            .MinimumLevel.ControlledBy(s_levelSwitch)
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
