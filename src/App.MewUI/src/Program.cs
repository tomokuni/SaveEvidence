using Aprillz.MewUI;
using Aprillz.MewUI.Skia.Interop;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Platform.Core.Native;
using EsUtil.Platform.Core.Native.Win;
using EsUtil.SaveEvidence.App.Core.Models;
using EsUtil.SaveEvidence.App.Core.Services;
using EsUtil.SaveEvidence.App.Core.ViewModels;
using EsUtil.SaveEvidence.App.MewUI.Logging;
using EsUtil.SaveEvidence.App.MewUI.Services;
using EsUtil.SaveEvidence.App.MewUI.Views;
using EsUtil.SaveEvidence.Platform.Skia;
using Microsoft.Extensions.Logging;
using SkiaSharp;


namespace EsUtil.SaveEvidence.App.MewUI;

/// <summary>SaveEvidence の MewUI 版ホスト。</summary>
/// <remarks>
/// <b>構成</b>: UI 非依存の <c>App.Core</c>（設定・ViewModel）と <c>Platform.MewUI</c>（Interop）を使い、<br/>
/// UI 層だけを MewUI で実装する。 Avalonia 版 <c>app</c> と同じコアロジックを共有する。<br/>
/// <br/>
/// <b>必須の初期化</b>:<br/>
/// 1. <c>DpiHelper.EnablePerMonitorDpiAwareness()</c>（DPI 対応。 呼ばないとウィンドウ配置の復元がずれる）<br/>
/// 2. <c>Direct2DBackend.Register()</c> / <c>Win32Platform.Register()</c>（バックエンドとプラットフォームホストの明示登録）<br/>
/// 3. <c>SkiaDirect2DInterop.Register()</c>（Direct2D ⇔ Skia の GPU ブリッジ。 未登録でも動作するが遅い）<br/>
/// <br/>
/// <b>ウィンドウ配置の復元</b>: 位置・サイズの DIP はモニターの DPI が必要なため、<c>Application.Run</c> の<br/>
/// <c>startup</c>（ディスパッチャ導入後・ウィンドウ表示前）で復元する。<br/>
/// <br/>
/// <b>現状</b>: キャプチャページ（プレビュー・ルーペ・ツールバー）と設定の配線までを実装する。<br/>
/// 撮影フローと残りのページは移植の段階で追加する。
/// </remarks>
internal static class Program
{
    /// <summary>ロガー（Avalonia 版と同じログファイルへ出力する）。</summary>
    private static readonly ILogger s_logger = AppLog.CreateLogger(nameof(Program));

    /// <summary>アプリケーションを起動する。</summary>
    [STAThread]
    private static void Main()
    {
        // Per-Monitor V2 の DPI 対応を有効化する（アプリ起動時に呼ぶ必要がある）。
        // これを呼ばないと OS の DPI クエリが 96 に仮想化され、ウィンドウ配置の復元がずれる。
        DpiHelper.EnablePerMonitorDpiAwareness();

        // 描画バックエンドとプラットフォームホストを明示登録する（メタパッケージでは自動登録されない）
        Direct2DBackend.Register();
        Win32Platform.Register();

        // Direct2D ⇔ Skia のブリッジを有効化する（未登録だと Skia 描画が毎フレーム CPU 転送になる）
        SkiaDirect2DInterop.Register();

        // 未処理の例外を記録する（クラッシュ原因をログから特定できるようにする。 Avalonia 版と同じ対象）
        RegisterExceptionLogging();

        // 設定は UI 非依存の共通実装を使う（%LOCALAPPDATA%\SaveEvidence\settings.json）
        // JSON ソース生成コンテキストを渡すことで、トリミング / Native AOT でも設定を読み書きできる
        var settings = new SettingsService(SettingsJsonContext.Default);
        var viewModel = new MainViewModel(settings, new EsBitmapImageWriter());

        // Undo / Redo 履歴はキャプチャページと Undo 一覧で共有する（画像編集の直前に現在の画像を積む）
        var undoStack = new UndoHistory();
        viewModel.BeforeEditAction = () => undoStack.Push(viewModel.PreviewImage);

        // フォルダビューの表示状態（表示モード・ソート順）もコアが保持し、設定へ保存する
        var services = new AppServices(viewModel, settings, undoStack, new FolderViewViewModel());

        // キャプチャの開始は ViewModel のコールバック（ツールバーのボタン）とグローバルホットキーの両方から呼ぶ。
        // ホットキーの開始経路（StartSelectionMode）は、撮影・切り出しのセッション寿命を所有するページが
        // Facade を生成した時点で設定する（本ホストは UI 層のオブジェクトを生成しないため、ここでは設定しない）。

        // 骨格確認用の仮画像（キャプチャ実装までの暫定）
        viewModel.SetPreviewImage(CreatePlaceholderImage());

        // ウィンドウ配置（位置・サイズ・状態）とナビ開閉は設定から復元し、終了時に保存する
        var window = MainWindow.Create(services);

        // グローバルホットキーはウィンドウハンドルとメッセージフックを必要とするため、初回描画後に登録する
        // （登録・解除の失敗は OS 操作の失敗として同じログ基盤へ記録する）
        var hotKeys = new WinGlobalHotKeyService<CaptureType>();
        window.Closed += hotKeys.Dispose;
        window.FirstFrameRendered += () => RegisterHotKeys(hotKeys, window, settings, services);

        // 位置・サイズの復元は表示前（ディスパッチャ導入後）に行う:
        // この時点で初めてモニターの DPI を取得でき、かつ表示前の位置指定が有効になる。
        // テーマも同じタイミングで適用する（Application.Current が初期化される前は変更できない）。
        s_logger.LogAppStart(typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown");
        Application.Run(window, () =>
        {
            AppStyles.ApplyTheme(settings.Current.ThemeMode);
            MainWindow.RestorePlacement(window, settings);
        });
        s_logger.LogAppExit(0);
    }

    /// <summary>未処理の例外を記録するハンドラを登録する。</summary>
    /// <remarks>
    /// 1. <c>AppDomain.UnhandledException</c>: アプリ全体の未処理例外（プロセス終了を伴う）。<br/>
    /// 2. <c>TaskScheduler.UnobservedTaskException</c>: 待機されなかったタスクの例外（観測済みにして継続する）。<br/>
    /// 3. <c>Application.DispatcherUnhandledException</c>: UI スレッドの例外。 記録後に <c>Handled</c> を立てて<br/>
    ///    ウィンドウの表示を維持する（Avalonia 版と同じ扱い。 UI スレッドを黙って終了させない）。<br/>
    /// <b>書式</b>: Avalonia 版と同じ <c>未処理の例外: {発生源}</c> で記録し、同じログファイルで追跡できるようにする。
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
        Application.DispatcherUnhandledException += e =>
        {
            s_logger.LogUnhandledException(e.Exception, "Application.DispatcherUnhandledException");
            e.Handled = true;
        };
    }

    /// <summary>設定のホットキーを登録し、押下時にキャプチャを開始するよう配線する。</summary>
    /// <param name="hotKeys">ホットキーサービス。</param>
    /// <param name="window">通知を受け取るウィンドウ。</param>
    /// <param name="settingsService">設定サービス。</param>
    /// <param name="services">アプリ全体で共有するコアオブジェクト。</param>
    /// <remarks>
    /// 1. ウィンドウハンドルをサービスへ渡す（<c>WM_HOTKEY</c> の受信先。 ウィンドウプロシージャのサブクラス化を含む）。<br/>
    /// 2. 3 種のキャプチャに割り当てられた設定を登録する（未設定のものはスキップされる）。<br/>
    /// 3. 押下時は <c>MainViewModel.StartSelectionMode</c> を経由してキャプチャを開始する<br/>
    ///    （ツールバーのボタンと同じ経路に一本化し、開始処理を二重所有しない）。<br/>
    /// <b>呼び出し時期</b>: ウィンドウのハンドルとウィンドウプロシージャは表示時に確定するため、<br/>
    /// 起動時コールバックではなく初回描画後（<c>FirstFrameRendered</c>）に呼ぶ。<br/>
    /// それより早いとサブクラス化が MewUI 側の設定で上書きされ、<c>WM_HOTKEY</c> を受け取れない。<br/>
    /// <b>登録できなかった組み合わせを記録する</b>: 他のアプリが同じ組み合わせを使用している場合にも起こるため、
    /// 1 件の競合でアプリを止めず、失敗した組み合わせだけを Warning で残す。
    /// </remarks>
    private static void RegisterHotKeys(
        IGlobalHotKeyService<CaptureType> hotKeys, Window window,
        ISettingsService settingsService, AppServices services)
    {
        var settings = settingsService.Current;
        hotKeys.Subscribe(type => services.Main.StartSelectionMode?.Invoke(type));
        hotKeys.Initialize(window.Handle);
        var failed = hotKeys.RegisterAll(
        [
            (CaptureType.ScreenSelect, settings.SelectScreenHotKey),
            (CaptureType.WindowSelect, settings.WindowSelectHotKey),
            (CaptureType.AreaSelect, settings.AreaSelectHotKey),
        ]);

        foreach (var (action, setting) in failed)
        {
            s_logger.LogHotKeyRegisterFailed(action, setting.Key);
        }
    }

    /// <summary>骨格確認用のプレビュー画像を生成する。</summary>
    /// <returns>生成した画像。 所有権は呼び出し側（<c>MainViewModel</c> へ渡す）。</returns>
    /// <remarks>キャプチャ実装までの暫定。 グラデーションにすることで描画の成否を目視で判定できる。</remarks>
    private static EsBitmap CreatePlaceholderImage()
    {
        // 所有権は FromSKBitmap へ移るため using を付けない（付けると戻り値が破棄済みになる）
        var bitmap = new SKBitmap(320, 180);
        using var canvas = new SKCanvas(bitmap);

        using var paint = new SKPaint { IsAntialias = false };
        paint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(0, 0), new SKPoint(320, 180),
            [new SKColor(30, 144, 255), new SKColor(255, 140, 0)],
            null, SKShaderTileMode.Clamp);
        canvas.DrawRect(new SKRect(0, 0, 320, 180), paint);

        return bitmap.ToEsBitmap();
    }
}

