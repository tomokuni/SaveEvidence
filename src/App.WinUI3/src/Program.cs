using EsUtil.Platform.Core.Native.Win;
using EsUtil.SaveEvidence.App.Core.Services;
using EsUtil.SaveEvidence.App.WinUI3.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using WinRT;

namespace EsUtil.SaveEvidence.App.WinUI3;

/// <summary>SaveEvidence の WinUI 3 版ホスト（エントリ ポイント）。</summary>
/// <remarks>
/// <b>構成</b>: UI 非依存の <c>App.Core</c>（設定・ViewModel）と WinUI 3 版の各層を使い、UI だけを<br/>
/// WinUI 3 で実装する。 他の UI 版と同じコア ロジックと設定ファイル（<c>%LOCALAPPDATA%\SaveEvidence\settings.json</c>）を共有する。<br/>
/// <br/>
/// <b>起動の手順</b>:<br/>
/// 1. 未処理の例外の記録を登録する（クラッシュ原因をログから特定できるようにする）<br/>
/// 2. 合成ルート（<see cref="AppHost"/>）でサービスとメイン ウィンドウを組み立てる<br/>
/// 3. WinRT の COM ラッパーを初期化し、XAML アプリケーションを開始する（メッセージ ループ）<br/>
/// 4. メッセージ ループを抜けたら終了を記録し、DI コンテナを破棄する<br/>
/// <br/>
/// <b>エントリ ポイントを自前で持つ理由</b>: 撮影の実行に必要な初期化（ログと設定の読み込み）を<br/>
/// ウィンドウ生成より前に済ませるためである（XAML コンパイラが生成する <c>Main</c> は<br/>
/// <c>Application.Start</c> だけを行う）。 <c>DISABLE_XAML_GENERATED_MAIN</c> で生成側を無効化する。
/// </remarks>
internal static class Program
{
    /// <summary>ロガー（他の UI 版と同じログファイルへ出力する）。</summary>
    private static readonly ILogger s_logger = AppLog.CreateLogger(nameof(Program));

    /// <summary>アプリケーションを起動する。</summary>
    [STAThread]
    private static void Main()
    {
        // タスク バーのグループ分けを 4 つの UI 版で揃える（ウィンドウを作る前に設定する）
        TaskbarIdentity.Apply();
        // 未処理の例外を記録する（クラッシュ原因をログから特定できるようにする）
        RegisterExceptionLogging();

        // アンパッケージの WinUI 3 アプリは .NET ホスト（dotnet <アプリ>.dll）から起動できない。
        // マニフェストによるアクティベーションと resources.pri の解決をアプリ ホストの exe が担うため、
        // XAML の初期化で stowed exception（0xC000027B）になり、理由の分からない即時終了に見える。
        // 起動方法の誤りを利用者へ示す（ログだけでは気づけないため、メッセージ ボックスも出す）
        if (IsStartedByDotnetHost())
        {
            s_logger.LogStartedFromDotnetHost();
            WinMessageBox.ShowError(
                "SaveEvidence.WinUI3.exe（実行ファイル）から起動してください。\n"
                + ".NET ホスト（dotnet <アプリ>.dll）からは起動できません。",
                "SaveEvidence");
            return;
        }

        // 設定の読み込みとサービス・ウィンドウの生成（UI スレッドの開始前に行う）
        AppHost.Compose();

        s_logger.LogAppStart(ReadVersion());

        // WinRT の COM ラッパーを初期化し、XAML アプリケーション（メッセージ ループ）を開始する
        ComWrappersSupport.InitializeComWrappers();
        Application.Start(initializationParams =>
        {
            _ = initializationParams;
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);

            // 生成した App は Application.Current が保持する（ローカル変数に保持する必要はない）
            _ = new App();
        });

        s_logger.LogAppExit(0);
        AppHost.Shutdown();
    }

    /// <summary>実行ファイルのバージョンを取得する。</summary>
    /// <returns>バージョン文字列。 取得できない場合は "unknown"。</returns>
    private static string ReadVersion()
        => typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown";

    /// <summary>.NET ホスト（<c>dotnet</c>）から起動されたかどうかを判定する。</summary>
    /// <returns><c>dotnet</c> から起動された場合は true。</returns>
    /// <remarks>
    /// アプリ ホストの exe から起動した場合、<see cref="Environment.ProcessPath"/> はアプリの exe になる。<br/>
    /// <c>dotnet exec</c> / <c>dotnet &lt;dll&gt;</c> の場合は <c>dotnet.exe</c> になる。
    /// </remarks>
    private static bool IsStartedByDotnetHost()
        => Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "")
            .Equals("dotnet", StringComparison.OrdinalIgnoreCase);

    /// <summary>未処理の例外を記録するハンドラを登録する。</summary>
    /// <remarks>
    /// 1. <c>AppDomain.UnhandledException</c>: アプリ全体の未処理例外（プロセス終了を伴う）。<br/>
    /// 2. <c>TaskScheduler.UnobservedTaskException</c>: 待機されなかったタスクの例外（観測済みにして継続する）。<br/>
    /// <b>UI スレッドの例外</b>は XAML アプリケーションの <c>UnhandledException</c> で記録する（<c>App</c> が購読する）。<br/>
    /// <b>書式</b>: 他の UI 版と同じ <c>未処理の例外: {発生源}</c> で記録し、同じログファイルで追跡できるようにする。
    /// </remarks>
    private static void RegisterExceptionLogging()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                s_logger.LogUnhandledException(ex, "AppDomain.UnhandledException");
            }
            else
            {
                s_logger.LogUnhandledException(
                    new InvalidOperationException("例外オブジェクトを取得できません"), "AppDomain.UnhandledException");
            }
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            s_logger.LogUnhandledException(e.Exception, "TaskScheduler.UnobservedTaskException");
            e.SetObserved();
        };
    }
}
