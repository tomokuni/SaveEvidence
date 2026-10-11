using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.Parts.CapturePreview.AvaloniaUI.Crop;
using EsUtil.SaveEvidence.Parts.CapturePreview.AvaloniaUI.Sample.ViewModels;
using EsUtil.SaveEvidence.Parts.CapturePreview.AvaloniaUI.Session;
using EsUtil.SaveEvidence.Parts.CapturePreview.AvaloniaUI.ZoomView;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Session;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Imaging;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Interop;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.AvaloniaUI.Sample.Views;

/// <summary>メインウィンドウ。 CaptureSessionControl の呼び出し・結果反映・Crop プレビューを担う。</summary>
/// <remarks>
/// CaptureSessionControl は UI スレッドから呼び出す必要があるため、ボタンイベントから直接呼び出す。<br/>
/// Crop プレビューはダミー画像（グリッド付き）を生成して表示し、その上で切り出しを確認できる。<br/>
/// ズーム操作（⊖⊕ / Fit / スライダ / Ctrl+ホイール）は <see cref="CropViewControl"/> が自己完結し、<br/>
/// 本クラスは <c>ZoomChanged</c> を購読して UI とスライダを同期する。
/// </remarks>
public sealed partial class MainWindow : Window
{
    /// <summary>Crop プレビュー用のダミー画像サイズ（幅）。</summary>
    private const int PreviewWidth = 640;

    /// <summary>Crop プレビュー用のダミー画像サイズ（高さ）。</summary>
    private const int PreviewHeight = 400;

    /// <summary>スライダ同期時の値比較許容差。</summary>
    private const double ZoomSliderEpsilon = 0.01;

    /// <summary>キャプチャ統括者（Facade）。</summary>
    private readonly CaptureSessionControl _capture;

    /// <summary>ビューモデル。</summary>
    private readonly MainWindowViewModel _vm = new();

    /// <summary>Crop プレビュー用の画像の元（読み取りの面。 本ウィンドウが所有する。 差し替え・クローズで破棄する）。</summary>
    private EsReadOnlyBitmap? _previewEs;

    /// <summary>Crop プレビュー用の表示画像（<see cref="_previewEs"/> を借りて包む非所有アダプタ）。</summary>
    private EsBitmapAvaloniaAdapter? _previewImage;

    /// <summary>スライダ同期中の再入防止フラグ（プログラム側の同期による ValueChanged を無視する）。</summary>
    private bool _isSyncingZoomSlider;

    /// <summary>MainWindow を初期化する。</summary>
    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        _capture = new CaptureSessionControl(initialSettings: null);
        _capture.AppearanceSettings = _capture.AppearanceSettings with { MinSelectionSize = 10 };

        // キャプチャ完了イベント（同期型）を購読する。
        // 画像変更（結果の反映）をここで行い、完了後にキャプチャセッションがオーナーを再表示する
        // （「画像変更 → オーナー再表示」の順序保証の確認）。
        _capture.CaptureCompleted += OnCaptureCompleted;

        // Crop プレビューのダミー画像を生成して表示する
        SetPreviewBitmap(CreateDummyBitmap(PreviewWidth, PreviewHeight));

        // プレビューのズーム状態（ズーム率・可否）を自 UI のボタン・スライダへ同期する
        // （ズーム率の単一所有は CropViewControl。 ホストは通知を受け取って表示するだけ）。
        PreviewCropView.ZoomChanged += OnZoomChanged;
        ZoomSlider.ValueChanged += OnZoomSliderValueChanged;
        PreviewCropView.PointerMoved += OnPreviewPointerMoved;

        // ログ行の追加を監視して自動スクロールする
        _vm.LogLines.CollectionChanged += OnLogLinesChanged;

        // ウィンドウクローズ時に CaptureSessionControl を破棄する
        Closed += (_, _) =>
        {
            _previewImage?.Dispose();
            _previewEs?.Dispose();
            _capture.Dispose();
        };
    }

    /// <summary>プレビュー画像を差し替える（旧画像は破棄し、ViewModel の表示も更新する）。</summary>
    /// <param name="bitmap">新しいプレビュー画像（読み取りの面）。 所有権は本ウィンドウへ移る。</param>
    private void SetPreviewBitmap(EsReadOnlyBitmap bitmap)
    {
        // 新しいアダプタを先に Source へ設定し、旧アダプタと旧画像は後から破棄する
        // （IImage は非所有のため、保持するホストが破棄する。 破棄の順序の理由は EsBitmapAvaloniaAdapter の doc を参照）。
        var previousImage = _previewImage;
        var previousEs = _previewEs;
        _previewEs = bitmap;
        _previewImage = new EsBitmapAvaloniaAdapter(bitmap);
        PreviewCropView.Source = _previewImage;
        previousImage?.Dispose();
        previousEs?.Dispose();
        _vm.PreviewImageSizeText = $"プレビュー: {bitmap.Width} x {bitmap.Height}";
    }

    /// <summary>プレビューのズーム状態が変化したときに、ボタンの可否・スライダ・表示テキストを同期する。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">ズーム状態（実効ズーム率・Fit 状態・可否）。</param>
    private void OnZoomChanged(object? sender, ZoomChangedEventArgs e)
    {
        ZoomInButton.IsEnabled = e.CanZoomIn;
        ZoomOutButton.IsEnabled = e.CanZoomOut;
        SyncZoomSlider(e.ZoomPercent);
        _vm.ZoomPercent = e.ZoomPercent;
        // 表示文字列はホストの書式（整数 % 表記）で生成する（e.ZoomPercent は実効ズーム率のため Fit 中も 0 にならない）
        _vm.ZoomDisplayText = $"{e.ZoomPercent:0}%";
    }

    /// <summary>ズーム率スライダの値を現在のズーム率へ同期する（同期中の再入は無視する）。</summary>
    /// <param name="zoom">現在の実効ズーム率（%）。</param>
    private void SyncZoomSlider(double zoom)
    {
        if (_isSyncingZoomSlider)
        {
            return;
        }

        _isSyncingZoomSlider = true;
        try
        {
            var sliderValue = Math.Clamp(zoom, ZoomSlider.Minimum, ZoomSlider.Maximum);
            if (Math.Abs(ZoomSlider.Value - sliderValue) > ZoomSliderEpsilon)
            {
                ZoomSlider.Value = sliderValue;
            }
        }
        finally
        {
            _isSyncingZoomSlider = false;
        }
    }

    /// <summary>スライダ操作によるズーム率の変更をプレビューへ反映する。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">値変更イベント。</param>
    private void OnZoomSliderValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isSyncingZoomSlider)
        {
            return;
        }

        PreviewCropView.ZoomPercent = ZoomSlider.Value;
    }

    /// <summary>ズームアウトする（⊖ ボタン。 段階遷移）。</summary>
    private void OnZoomOutClick(object? sender, RoutedEventArgs e) => PreviewCropView.ZoomOut();

    /// <summary>ズームインする（⊕ ボタン。 段階遷移）。</summary>
    private void OnZoomInClick(object? sender, RoutedEventArgs e) => PreviewCropView.ZoomIn();

    /// <summary>Fit 表示（ビューポートに収まる自動倍率）へ戻す。</summary>
    private void OnZoomFitClick(object? sender, RoutedEventArgs e) => PreviewCropView.ZoomFit();

    /// <summary>プレビュー上のポインタ位置を元画像ピクセルへ変換して表示する。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">ポインタイベント。</param>
    private void OnPreviewPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_previewEs is not { } img)
        {
            PointerPositionText.Text = "ポインタ: -";
            return;
        }

        var viewportPos = e.GetPosition(PreviewCropView);
        var contentPos = PreviewCropView.ClientToSource(viewportPos);
        if (contentPos.X < 0 || contentPos.Y < 0 ||
            contentPos.X >= img.Size.Width || contentPos.Y >= img.Size.Height)
        {
            PointerPositionText.Text = "ポインタ: -（画像外）";
            return;
        }

        PointerPositionText.Text = $"ポインタ: {(int)contentPos.X}, {(int)contentPos.Y}（画像ピクセル）";
    }

    /// <summary>画像ファイルを開いてプレビューへ表示する。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">ルーティングイベント。</param>
    private async void OnOpenImageClick(object? sender, RoutedEventArgs e)
    {
        if (GetTopLevel(this) is not { } topLevel)
        {
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "画像を選択",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("画像ファイル")
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.webp"]
                }
            ],
        });

        if (files.Count == 0)
        {
            return;
        }

        try
        {
            // ファイルの読み込みはコアの画像型が単一所有する（形式は内容から判別される）。
            // プレビューは読むだけ（表示と切り出しのみ）であるため、読み取りの面のまま渡す
            // （書き換え可能へ複製すると、全域コピーが 1 回増える。 実測: 4K で 5.7 ms / 31 MiB）。
            // 所有権は SetPreviewBitmap へ移るため、ここでは破棄しない。
            SetPreviewBitmap(EsReadOnlyBitmap.Load(files[0].Path.LocalPath));
            _vm.AddLog($"[App] 画像を読み込みました: {files[0].Name}");
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 画像を読み込めない場合は何もしない（現在の画像を維持する）
            _vm.AddLog("[App] 画像を読み込めませんでした（現在の画像を維持します）。");
        }
    }

    /// <summary>キャプチャ完了イベント（同期実行）。 画像変更をここで行い、完了後にオーナーが再表示される。</summary>
    /// <param name="sender">イベントの発信元（使用しない）。</param>
    /// <param name="e">キャプチャ結果（null = キャンセル・エラー）。</param>
    /// <remarks>
    /// 同期型イベントのため、このハンドラ（画像変更）の完了後に、キャプチャセッションの後続処理（オーナー再表示）が実行される。<br/>
    /// ハンドラを非同期（<c>async void</c>）にすると順序保証が失われるため、同期ハンドラで画像変更を行う。<br/>
    /// </remarks>
    private void OnCaptureCompleted(object? sender, CaptureCompletedEventArgs e)
    {
        if (e.Result?.Image is { } image)
        {
            // 画像変更（同期実行。 このハンドラの完了後に APP が再表示される）。 キャプチャ結果を表示用 Bitmap へ変換する
            // （変換は読むだけ: 凍結して 0 コピーで読み取り専用の面へ渡す）
            image.SetReadOnly();
            using var read = EsReadOnlyBitmap.FromBitmap(image);
            _vm.CapturedImage = read.ToAvaloniaBitmap();
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
        // ルーペの拡大倍率を反映する（表示可否はモードとソースの有無から導出されるため設定しない）
        _capture.AppearanceSettings = _capture.AppearanceSettings with
        {
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
        if (_previewEs is null)
        {
            _vm.AddLog("[App] プレビュー画像がありません。");
            return;
        }

        _vm.AddLog("[App] Crop を開始します（プレビュー上でドラッグ選択 → Enter / ダブルクリックで確定）。");

        // 切り出しの対象画像は CropView が表示している画像（PreviewCropView.Source）から解決される。
        // 結果はコア型（EsRectInt）で返るため、表示用の UI 型へはサンプル側（境界）で変換する
        var rect = await _capture.StartCropAsync(PreviewCropView, this);
        _vm.UpdateCropResult(rect?.ToAvaloniaPixelRect());
        _vm.AddLog(rect is { } bounds && !bounds.IsEmpty
            ? $"[App] Crop が完了しました: {bounds.X},{bounds.Y},{bounds.Width},{bounds.Height}（元画像ピクセル）"
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
    /// <returns>生成した画像（読み取りの面。 呼び出し側（<see cref="SetPreviewBitmap"/>）が所有する）。</returns>
    /// <remarks>
    /// 白地に 100px 間隔の薄いグリッドと、中央に色付きの矩形を描画する（サイズ感・座標の確認用）。<br/>
    /// <b>EsBitmap 採用理由</b>: <c>WriteableBitmap</c>（<c>Lock()</c> 書き込み）を避け、ピクセル操作が安定する。<br/>
    /// 描画の完了後は読むだけ（表示と切り出しのみ）であるため、凍結して読み取り専用の面へ 0 コピーで渡す。
    /// </remarks>
    private static EsReadOnlyBitmap CreateDummyBitmap(int width, int height)
    {
        var bitmap = EsBitmap.Create(width, height);

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

                bitmap.SetPixel(x, y, b, g, r, 255);
            }
        }

        // 以降は読むだけ（表示と切り出しのみ）であるため、凍結して 0 コピーで読み取り専用の面へ渡す
        bitmap.SetReadOnly();
        var read = EsReadOnlyBitmap.FromBitmap(bitmap);
        bitmap.Dispose();
        return read;
    }
}
