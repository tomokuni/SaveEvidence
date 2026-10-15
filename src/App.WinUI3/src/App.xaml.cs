using EsUtil.SaveEvidence.App.WinUI3.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;

namespace EsUtil.SaveEvidence.App.WinUI3;

/// <summary>SaveEvidence の WinUI 3 版アプリケーション（XAML アプリケーション定義）。</summary>
/// <remarks>
/// <b>役割</b>: XAML コントロールの既定スタイル（<c>XamlControlsResources</c>）を読み込み、<br/>
/// 起動時にメイン ウィンドウを生成して表示する（合成ルートは <see cref="AppHost"/> が所有する）。<br/>
/// <b>エントリ ポイント</b>: 本クラスの <c>InitializeComponent</c> だけを使い、<c>Main</c> は <see cref="Program"/> が持つ<br/>
/// （<c>DISABLE_XAML_GENERATED_MAIN</c> で XAML コンパイラの生成する <c>Main</c> を無効化する）。<br/>
/// <b>UI スレッドの例外</b>: 記録だけを行い、アプリを終了させない（他の UI 版と同じ扱い）。
/// </remarks>
public partial class App : Application
{
    /// <summary>ロガー（UI スレッドの例外を記録する）。</summary>
    private static readonly ILogger s_logger = AppLog.CreateLogger(nameof(App));

    /// <summary>アプリケーションを初期化する。</summary>
    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
    }

    /// <summary>メイン ウィンドウを生成して表示する。</summary>
    /// <param name="args">起動引数（本アプリは使用しない）。</param>
    /// <remarks>
    /// ウィンドウは <see cref="AppHost"/> が保持する（本クラスで参照を持たない）。<br/>
    /// 参照が無いとガベージ コレクションの対象になり、ウィンドウが閉じてしまうためである。
    /// </remarks>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // XAML のディスパッチャが用意された後にウィンドウを生成する（UI 要素の生成は UI スレッドを要する）
        AppHost.CreateMainWindow();

        // 背景のサムネイル生成の結果を UI スレッドで反映できるようにする
        AppHost.OnWindowCreated();

        AppHost.MainWindow.Activate();
    }

    /// <summary>UI スレッドの未処理例外を記録して、アプリの継続を許可する。</summary>
    /// <param name="sender">イベントの送信元（本アプリケーション）。</param>
    /// <param name="e">例外の情報。</param>
    /// <remarks>
    /// 記録したうえで <c>Handled</c> を立て、ウィンドウの表示を維持する（入力・描画の 1 回分の失敗で<br/>
    /// アプリを終了させない）。 原因は Critical のログとして残る。
    /// </remarks>
    private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        s_logger.LogUnhandledException(e.Exception, "Application.UnhandledException");
        e.Handled = true;
    }
}
