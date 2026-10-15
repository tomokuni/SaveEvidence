using EsUtil.SaveEvidence.App.Core.Models;
using EsUtil.SaveEvidence.App.Core.Services;
using EsUtil.SaveEvidence.App.Core.ViewModels;
using EsUtil.SaveEvidence.App.WinUI3.Services;
using EsUtil.SaveEvidence.App.WinUI3.Views;
using Microsoft.Extensions.DependencyInjection;

namespace EsUtil.SaveEvidence.App.WinUI3;

/// <summary>アプリの合成ルート（サービスとメイン ウィンドウの生成・共有）。</summary>
/// <remarks>
/// <b>役割</b>: 起動時に 1 度だけ呼ばれ、DI コンテナへ共有オブジェクトを登録して組み立てる。<br/>
/// 生成した <see cref="AppServices"/> と <see cref="MainWindow"/> はアプリの生存期間で共有する。<br/>
/// <br/>
/// <b>DI コンテナを使う理由</b>: ページとコア オブジェクトの対応（共有 / 都度生成）を宣言で表し、<br/>
/// 生成順の誤り（履歴より先にページを作る等）を起動時の解決で検出する。<br/>
/// <b>共有の範囲</b>: メイン ViewModel・設定サービス・編集履歴は共有（複数ページが同じ状態を見る）。<br/>
/// フォルダビューの表示状態も、ページを再利用するため 1 つだけ生成して共有する。
/// </remarks>
internal static class AppHost
{
    /// <summary>組み立て済みのサービス束。</summary>
    private static AppServices? s_services;

    /// <summary>DI コンテナ（アプリの生存期間で保持する。 破棄すると共有サービスが破棄されるため保持し続ける）。</summary>
    private static ServiceProvider? s_provider;

    /// <summary>アプリのメイン ウィンドウ。</summary>
    private static MainWindow? s_mainWindow;

    /// <summary>アプリ全体で共有するコアオブジェクトを取得する。</summary>
    /// <value>組み立て済みのサービス束。</value>
    /// <exception cref="InvalidOperationException"><see cref="Compose"/> より前に参照した場合。</exception>
    public static AppServices Services
        => s_services ?? throw new InvalidOperationException("AppHost.Compose を先に呼ぶ必要があります。");

    /// <summary>アプリのメイン ウィンドウを取得する。</summary>
    /// <value>生成済みのメイン ウィンドウ。</value>
    /// <exception cref="InvalidOperationException"><see cref="Compose"/> より前に参照した場合。</exception>
    public static MainWindow MainWindow
        => s_mainWindow ?? throw new InvalidOperationException("AppHost.Compose を先に呼ぶ必要があります。");

    /// <summary>サービスを組み立てる（メイン ウィンドウは生成しない）。</summary>
    /// <remarks>
    /// <b>ウィンドウをここで生成しない理由</b>: UI 要素の生成は XAML のディスパッチャ（UI スレッド）を<br/>
    /// 必要とする。 本メソッドは <c>Application.Start</c> より前に呼ぶため、ここでウィンドウを作ると<br/>
    /// 別スレッドからの呼び出しになり失敗する（<see cref="CreateMainWindow"/> が担う）。<br/>
    /// <b>手順</b>:<br/>
    /// 1. DI コンテナへ共有オブジェクトを登録する。<br/>
    /// 2. 設定サービスを解決する（JSON のソース生成コンテキストを渡す）。<br/>
    /// 3. メイン ViewModel を生成し、編集の直前に現在の画像を履歴へ積む配線を行う。<br/>
    /// 4. ページへ渡すサービス束を組み立てる。
    /// </remarks>
    public static void Compose()
    {
        var collection = new ServiceCollection();
        collection.AddSingleton<ISettingsService>(_ => new SettingsService(AppJsonContext.Default));
        collection.AddSingleton<IImageWriter, EsBitmapImageWriter>();
        collection.AddSingleton<MainViewModel>();
        collection.AddSingleton<UndoHistory>();
        collection.AddSingleton<FolderViewViewModel>();

        // コンテナはアプリの生存期間で保持する（破棄すると共有サービスも破棄される）
        var provider = collection.BuildServiceProvider();
        s_provider = provider;

        var settings = provider.GetRequiredService<ISettingsService>();
        var viewModel = provider.GetRequiredService<MainViewModel>();
        var undoHistory = provider.GetRequiredService<UndoHistory>();
        var folderView = provider.GetRequiredService<FolderViewViewModel>();

        // Undo 履歴は画像編集の直前に現在の画像を積む（撮影・切り出しの前の状態へ戻せるようにする）
        viewModel.BeforeEditAction = () => undoHistory.Push(viewModel.PreviewImage);

        s_services = new AppServices(viewModel, settings, undoHistory, folderView);
    }

    /// <summary>メイン ウィンドウを生成する（UI スレッドから呼ぶ）。</summary>
    /// <remarks>XAML のディスパッチャが用意された後に呼ぶ（<c>Application.Start</c> の初期化コールバック）。</remarks>
    public static void CreateMainWindow()
    {
        if (s_mainWindow is not null)
        {
            return;
        }

        s_mainWindow = new MainWindow(Services);
    }

    /// <summary>アプリの初期化完了後に一度だけ呼ぶ後処理を実行する。</summary>
    /// <remarks>ウィンドウのディスパッチャ キューを背景処理へ渡す（サムネイルの生成結果を UI スレッドで反映するため）。</remarks>
    public static void OnWindowCreated()
    {
        ThumbnailBackground.SetDispatcherQueue(MainWindow.DispatcherQueue);
    }

    /// <summary>アプリ終了時に DI コンテナを破棄する。</summary>
    /// <remarks>メッセージ ループを抜けた後に呼ぶ（共有サービスを確実に解放する）。</remarks>
    public static void Shutdown()
    {
        s_provider?.Dispose();
        s_provider = null;
    }
}
