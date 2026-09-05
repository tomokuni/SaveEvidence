using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Estable.AvaloniaUI.Platform.Imaging;
using Estable.AvaloniaUI.SnipCaptureUI;
using SnipCaptureUI.Sample.ViewModels;
using System.Collections.Specialized;

namespace SnipCaptureUI.Sample.Views;

/// <summary>メインウィンドウ。SnipCapture の呼び出し・結果反映・Crop プレビューを担う。</summary>
/// <remarks>
/// SnipCapture は UI スレッドから呼び出す必要があるため、ボタンイベントから直接呼び出す。<br/>
/// Crop プレビューはダミー画像（グリッド付き）を生成して表示し、その上で切り出しを確認できる。<br/>
/// 操作ログは ViewModel の LogLines へ追加し、画面下のログ表示領域に反映する。
/// </remarks>
public sealed partial class MainWindow : Window
{
    /// <summary>Crop プレビュー用のダミー画像サイズ（幅）。</summary>
    private const int PreviewWidth = 640;

    /// <summary>Crop プレビュー用のダミー画像サイズ（高さ）。</summary>
    private const int PreviewHeight = 400;

    /// <summary>キャプチャ統括者（Facade）。</summary>
    private readonly SnipCapture _capture;

    /// <summary>ビューモデル。</summary>
    private readonly MainWindowViewModel _vm = new();

    /// <summary>Crop プレビュー用のダミー画像。</summary>
    private WriteableBitmap? _previewBitmap;

    /// <summary>MainWindow を初期化する。</summary>
    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        _capture = new SnipCapture(initialSettings: null);
        _capture.AppearanceSettings = _capture.AppearanceSettings with { MinSelectionSize = 10 };

        // キャプチャ完了イベント（同期型）を購読する。
        // 画像変更（結果の反映）をここで行い、完了後にキャプチャセッションがオーナーを再表示する
        // （「画像変更 → オーナー再表示」の順序保証の確認）。
        _capture.CaptureCompleted += OnCaptureCompleted;

        // Crop プレビューのダミー画像を生成して表示する
        _previewBitmap = CreateDummyBitmap(PreviewWidth, PreviewHeight);
        PreviewCropView.Source = _previewBitmap;

        // ログ行の追加を監視して自動スクロールする
        _vm.LogLines.CollectionChanged += OnLogLinesChanged;

        // ウィンドウクローズ時に SnipCapture を破棄する
        Closed += (_, _) => _capture.Dispose();
    }

    /// <summary>キャプチャ完了イベント（同期実行）。画像変更をここで行い、完了後にオーナーが再表示される。</summary>
    /// <param name="result">キャプチャ結果（null = キャンセル・エラー）。</param>
    /// <remarks>
    /// 同期型イベントのため、このハンドラ（画像変更）の完了後に、キャプチャセッションの後続処理（オーナー再表示）が実行される。<br/>
    /// ハンドラを非同期（<c>async void</c>）にすると順序保証が失われるため、同期ハンドラで画像変更を行う。<br/>
    /// </remarks>
    private void OnCaptureCompleted(CaptureResult? result)
    {
        if (result?.Image is { } image)
        {
            // 画像変更（同期実行。このハンドラの完了後に APP が再表示される）。キャプチャ結果（UnifiedBitmap）を表示用 Bitmap へ変換。
            _vm.CapturedImage = image.ToAvaloniaWriteableBitmap();
            _vm.AddLog("[Event] CaptureCompleted（同期）：キャプチャ画像を更新しました。");
        }
        else
        {
            _vm.AddLog("[Event] CaptureCompleted（同期）：キャンセル・エラーのため画像は未更新です。");
        }
    }

    /// <summary>全画面キャプチャを開始する。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private async void OnScreenSelectClicked(object? sender, RoutedEventArgs e)
    {
        _vm.AddLog("[App] ScreenSelect を開始します。");
        var result = await _capture.StartCaptureAsync(CaptureType.ScreenSelect, this);
        // 画像は CaptureCompleted イベント（同期）で更新済みのため、テキストのみ更新する
        _vm.UpdateResultText(result);
        _vm.AddLog(result?.Image is null ? "[App] ScreenSelect はキャンセルされました。" : "[App] ScreenSelect が完了しました。");
    }

    /// <summary>ウィンドウ選択キャプチャを開始する。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private async void OnWindowSelectClicked(object? sender, RoutedEventArgs e)
    {
        _vm.AddLog("[App] WindowSelect を開始します。");
        var result = await _capture.StartCaptureAsync(CaptureType.WindowSelect, this);
        // 画像は CaptureCompleted イベント（同期）で更新済みのため、テキストのみ更新する
        _vm.UpdateResultText(result);
        _vm.AddLog(result?.Image is null ? "[App] WindowSelect はキャンセルされました。" : "[App] WindowSelect が完了しました。");
    }

    /// <summary>範囲選択キャプチャを開始する。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private async void OnAreaSelectClicked(object? sender, RoutedEventArgs e)
    {
        // ルーペ設定を反映する（LoupeVisible / LoupeZoomLevel）
        _capture.AppearanceSettings = _capture.AppearanceSettings with
        {
            LoupeVisible = _vm.LoupeVisible,
            LoupeZoomLevel = _vm.LoupeZoomLevel,
        };

        _vm.AddLog("[App] AreaSelect を開始します（ルーペ付き）。");
        var result = await _capture.StartCaptureAsync(CaptureType.AreaSelect, this);
        // 画像は CaptureCompleted イベント（同期）で更新済みのため、テキストのみ更新する
        _vm.UpdateResultText(result);
        _vm.AddLog(result?.Image is null ? "[App] AreaSelect はキャンセルされました。" : "[App] AreaSelect が完了しました。");
    }

    /// <summary>Crop プレビュー上で切出しを開始する。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private async void OnCropClicked(object? sender, RoutedEventArgs e)
    {
        if (_previewBitmap is null)
        {
            _vm.AddLog("[App] プレビュー画像がありません。");
            return;
        }

        _vm.AddLog("[App] Crop を開始します（プレビュー上でドラッグ選択 → Enter / ダブルクリックで確定）。");
        using var unified = _previewBitmap.ToUnifiedBitmap();
        var rect = await _capture.StartCropAsync(PreviewCropView.ZoomView, this, unified);
        _vm.UpdateCropResult(rect);
        _vm.AddLog(rect is { Width: > 0, Height: > 0 }
            ? $"[App] Crop が完了しました: {rect.Value}（元画像ピクセル）"
            : "[App] Crop はキャンセルされました。");
    }

    /// <summary>進行中の操作をキャンセルする。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        _capture.Cancel();
        _vm.AddLog("[App] Cancel() を呼び出しました。");
    }

    /// <summary>進行中の Crop を確定する（Enter キー相当）。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void OnConfirmClicked(object? sender, RoutedEventArgs e)
    {
        _capture.Confirm();
        _vm.AddLog("[App] Confirm() を呼び出しました（Crop 実行中のみ有効）。");
    }

    /// <summary>ログ行が追加されたときに最下部（最新行）へ自動スクロールする。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">コレクション変更イベント引数。</param>
    private void OnLogLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // 追加（またはリセット）後に最新行が表示されるよう、レイアウト完了を待って最下部へスクロールする
        Dispatcher.UIThread.Post(() =>
        {
            if (_vm.LogLines.Count > 0)
            {
                LogList.ScrollIntoView(_vm.LogLines[^1]);
            }
        });
    }

    /// <summary>グリッド付きのダミー画像を生成する（Crop プレビュー用）。</summary>
    /// <param name="width">画像の幅。</param>
    /// <param name="height">画像の高さ。</param>
    /// <returns>生成した WriteableBitmap。</returns>
    /// <remarks>
    /// 白地に 100px 間隔の薄いグリッドと、中央に色付きの矩形を描画する（サイズ感・座標の確認用）。<br/>
    /// ピクセル操作は unsafe で行い、BGRA32 形式で書き込む。
    /// </remarks>
    private static unsafe WriteableBitmap CreateDummyBitmap(int width, int height)
    {
        var bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        using (var fb = bitmap.Lock())
        {
            var buffer = (byte*)fb.Address;
            int stride = fb.RowBytes;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    byte r = 255;
                    byte g = 255;
                    byte b = 255;

                    // 100px 間隔のグリッド線（薄いグレー）
                    if (x % 100 == 0 || y % 100 == 0)
                    {
                        r = 220;
                        g = 220;
                        b = 220;
                    }

                    // 中央の色付き矩形（サイズ感の確認用）
                    if (x is >= 200 and < 440 && y is >= 100 and < 300)
                    {
                        r = 120;
                        g = 180;
                        b = 240;
                    }

                    int offset = y * stride + x * 4;
                    buffer[offset + 0] = b;
                    buffer[offset + 1] = g;
                    buffer[offset + 2] = r;
                    buffer[offset + 3] = 255;
                }
            }
        }

        return bitmap;
    }
}
