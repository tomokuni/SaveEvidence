using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.App.Core.Models;
using EsUtil.SaveEvidence.App.Core.Services;
using EsUtil.SaveEvidence.App.Core.ViewModels;
using EsUtil.SaveEvidence.App.WinForms.Logging;
using EsUtil.SaveEvidence.App.WinForms.Services;

namespace EsUtil.SaveEvidence.App.WinForms;

/// <summary>SaveEvidence の WinForms 版ホスト。</summary>
/// <remarks>
/// <b>構成</b>: UI 非依存の <c>App.Core</c>（設定・ViewModel）と <c>Platform.WinForms</c>（Interop）を使い、<br/>
/// UI 層だけを WinForms で実装する。 他の UI 実装と同じコアロジックを共有し、<br/>
/// 設定ファイル（<c>%LOCALAPPDATA%\SaveEvidence\settings.json</c>）とログ<br/>
/// （<c>%LOCALAPPDATA%\SaveEvidence\logs\SaveEvidence.log</c>）も共有する。<br/>
/// <br/>
/// <b>必須の初期化</b>:<br/>
/// 1. <c>Application.SetHighDpiMode(PerMonitorV2)</c>（DPI 対応。 他の API より前に呼ぶ）<br/>
/// 2. <c>Application.EnableVisualStyles()</c> / <c>SetCompatibleTextRenderingDefault(false)</c>（見た目の既定）<br/>
/// 3. 未処理の例外の記録（クラッシュ原因をログから特定できるようにする）<br/>
/// <br/>
/// <b>UI スレッド</b>: クリップボード（OLE）とコントロールの生成は STA を要求するため <c>[STAThread]</c> を付ける。
/// </remarks>
internal static class Program
{
    /// <summary>ロガー（他の UI 実装と同じログファイルへ出力する）。</summary>
    private static readonly Microsoft.Extensions.Logging.ILogger s_logger =
        Logging.AppLog.CreateLogger(nameof(Program));

    /// <summary>アプリケーションを起動する。</summary>
    [STAThread]
    private static void Main()
    {
        // DPI 対応を有効化する（他の API より前に呼ぶ必要がある）
        _ = Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        RegisterExceptionLogging();

        // 設定は UI 非依存の共通実装を使う（%LOCALAPPDATA%\SaveEvidence\settings.json）
        // JSON ソース生成コンテキストを渡すことで、トリミング / Native AOT でも設定を読み書きできる
        var settings = new SettingsService(SettingsJsonContext.Default);
        var viewModel = new MainViewModel(settings, new EsBitmapImageWriter());

        // 履歴はキャプチャページと Undo 一覧で共有する（画像編集の直前に現在の画像を積む）
        var undoHistory = new UndoHistory();
        viewModel.BeforeEditAction = () => undoHistory.Push(viewModel.PreviewImage);

        var services = new AppServices(
            viewModel, settings, undoHistory, new FolderViewViewModel(), new UndoViewModel());

        // 撮影前のプレースホルダ画像（起動直後からプレビュー・保存・履歴を操作できるようにする）
        viewModel.SetPreviewImage(CreatePlaceholderImage());

        s_logger.LogAppStart(typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown");

        using var form = new Views.MainForm(services);
        Application.Run(form);

        s_logger.LogAppExit(0);
    }

    /// <summary>未処理の例外を記録するハンドラを登録する。</summary>
    /// <remarks>
    /// 1. <c>AppDomain.UnhandledException</c>: アプリ全体の未処理例外（プロセス終了を伴う）。<br/>
    /// 2. <c>TaskScheduler.UnobservedTaskException</c>: 待機されなかったタスクの例外（観測済みにして継続する）。<br/>
    /// 3. <c>Application.ThreadException</c>: UI スレッドの例外。 記録だけ行い、ウィンドウの表示を維持する<br/>
    ///    （UI スレッドを黙って終了させない）。<br/>
    /// <b>書式</b>: 他の UI 実装と同じ <c>未処理の例外: {発生源}</c> で記録し、同じログファイルで追跡できるようにする。
    /// </remarks>
    private static void RegisterExceptionLogging()
    {
        // ① アプリ全体の未処理例外（スタックトレースを含む Critical で記録）
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

        // ② 待機されなかったタスクの例外（fire-and-forget のタスクが対象）
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            s_logger.LogUnhandledException(e.Exception, "TaskScheduler.UnobservedTaskException");
            e.SetObserved();
        };

        // ③ UI スレッドの例外（入力・描画・撮影の例外を捕捉して記録し、アプリは継続する）
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
            s_logger.LogUnhandledException(e.Exception, "Application.ThreadException");
    }

    /// <summary>撮影前のプレースホルダ画像を生成する。</summary>
    /// <returns>生成した画像。 所有権は呼び出し側（メイン ViewModel）へ移る。</returns>
    /// <remarks>
    /// 起動直後は画像が無く、保存・コピー・切り出しがすべて無効になる。 プレースホルダを置くことで、<br/>
    /// 撮影の成否とは無関係に UI の各機能を確認できる（座標表示・ズーム・保存の動作確認に使う）。
    /// </remarks>
    private static EsBitmap CreatePlaceholderImage()
    {
        const int width = 640;
        const int height = 400;
        using var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            using var topLeft = new SolidBrush(Color.FromArgb(255, 32, 64, 128));
            using var bottomRight = new SolidBrush(Color.FromArgb(255, 16, 24, 48));
            graphics.FillRectangle(topLeft, 0, 0, width, height / 2);
            graphics.FillRectangle(bottomRight, 0, height / 2, width, height / 2);

            using var border = new Pen(Color.FromArgb(255, 200, 200, 200), 2);
            graphics.DrawRectangle(border, 8, 8, width - 17, height - 17);

            TextRenderer.DrawText(
                graphics,
                "SaveEvidence (WinForms)",
                SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont,
                new Rectangle(0, 0, width, height),
                Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        return GdiBitmapInterop.ToEsBitmap(bitmap);
    }
}
