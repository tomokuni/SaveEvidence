using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using EsUtil.Platform.AvaloniaUI.Imaging;
using EsUtil.Platform.Native.Imaging;
using EsUtil.SaveEvidence.ZoomView.Sample.Imaging;
using EsUtil.SaveEvidence.ZoomView.Sample.ViewModels;

namespace EsUtil.SaveEvidence.ZoomView.Sample.Views;

/// <summary>メインウィンドウ。ZoomViewControl の ZoomViewControl（自己完結型複合コントロール）の動作確認を担う。</summary>
/// <remarks>
/// ズーム操作（ボタン・スライダ・Ctrl+ホイール）は <c>ZoomViewControl</c> が自己完結し、<br/>
/// ズーム率・可否・表示テキストの変化を <c>ZoomChanged</c> イベントで購読して UI へ反映する。<br/>
/// </remarks>
public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _vm = new();

    /// <summary>スライダ同期中の再入防止フラグ（プログラム側の同期による ValueChanged を無視する）。</summary>
    private bool _isSyncingZoomSlider;

    /// <summary>MainWindow の新しいインスタンスを初期化する。</summary>
    /// <remarks>
    /// ZoomViewControl の状態変化・ViewModel のプロパティ変更・コントロールのイベントを配線する。<br/>
    /// 初期表示としてテスト画像を設定する。
    /// </remarks>
    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;

        // ズーム率の変化をスライダと ⊕⊖ ボタン・ViewModel へ同期する（ZoomViewControl は特定コントロールに依存しないため、ホスト側で同期する）
        ZoomableView.ZoomChanged += (_, e) =>
        {
            SyncZoomSlider(e.ZoomPercent);
            UpdateZoomButtonState(e.CanZoomIn, e.CanZoomOut);
            _vm.ZoomPercent = e.ZoomPercent;
            // 表示文字列はホスト側の書式（整数%表記）で生成する（e.ZoomPercent は実効ズーム率のため Fit 中も 0 にならない）
            _vm.ZoomDisplayText = $"{e.ZoomPercent:0}%";
        };

        // スライダの変更を ZoomViewControl へ反映する
        // （XAML ロード中の Slider 初期化（Value の coerce）でも ValueChanged が発火するため、
        //  依存注入後のコンストラクタで購読する。XAML 属性では未注入状態で呼ばれてしまう）
        ZoomSlider.ValueChanged += OnZoomSliderValueChanged;

        // ViewModel のプロパティ変更時にズーム表示を適用する
        _vm.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(MainWindowViewModel.PreviewImage):
                    ZoomableView.Source = _vm.PreviewImage;
                    break;
                case nameof(MainWindowViewModel.CenterAlign):
                    // 中央寄せ/左上寄せの変更を画像配置へ即時反映する
                    ZoomableView.CenterAlign = _vm.CenterAlign;
                    break;
            }
        };

        // マウスポインタ移動でポインタ位置のドメイン座標（画像ピクセル）を表示する
        ZoomableView.PointerMoved += OnPointerMoved;

        // 初期表示: テスト画像を設定（ズーム・パンの動作確認用）
        _vm.SetPreviewImage((EsBitmapAvaloniaImage)TestImageFactory.Create());

        // 初期のズーム率をスライダに反映し、⊕⊖ ボタン・表示テキストの状態を ZoomViewControl で初期化する
        SyncZoomSlider(_vm.ZoomPercent);
        UpdateZoomButtonState(ZoomableView.CanZoomIn, ZoomableView.CanZoomOut);
    }

    /// <summary>ズームアウトボタンクリック。</summary>
    private void OnZoomOutClick(object? sender, RoutedEventArgs e) => ZoomableView.ZoomOut();

    /// <summary>ズームインボタンクリック。</summary>
    private void OnZoomInClick(object? sender, RoutedEventArgs e) => ZoomableView.ZoomIn();

    /// <summary>Fit ボタンクリック（ズーム率 0 = 自動）。</summary>
    private void OnFitClick(object? sender, RoutedEventArgs e) => ZoomableView.ZoomFit();

    /// <summary>ズームスライダの変更ハンドラ。スライダ操作によるズーム率の適用を行う。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">値変更イベント。</param>
    /// <remarks>プログラム側の同期（<see cref="SyncZoomSlider"/>）による変更は無視する。</remarks>
    private void OnZoomSliderValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isSyncingZoomSlider) return;
        // ズーム率は double で設定する（ApplyZoomFromSlider は廃止・ZoomPercent に統合）
        ZoomableView.ZoomPercent = ZoomSlider.Value;
    }

    /// <summary>スライダ同期時の値比較許容差。</summary>
    private const double ZoomSliderEpsilon = 0.01;

    /// <summary>ズームスライダの値を現在のズーム率へ同期する。</summary>
    /// <param name="zoom">現在の実効ズーム率（%）。Fit 中は実効倍率。</param>
    /// <remarks>
    /// 実効ズーム率の変化（<see cref="ZoomViewControl.ZoomChanged"/>）を受けて、スライダ値を現在のズーム率へ同期する。<br/>
    /// Fit 中も実際に表示されている実効倍率をスライダに反映する（スライダ範囲 <see cref="ZoomSlider.Minimum"/> 〜 <see cref="ZoomSlider.Maximum"/> にクランプ）。<br/>
    /// 同期中の ValueChanged による再入を防ぐ。
    /// </remarks>
    private void SyncZoomSlider(double zoom)
    {
        if (_isSyncingZoomSlider) return;
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

    /// <summary>ズームイン・アウトボタンの Enable/Disable をズーム可否に応じて更新する。</summary>
    /// <param name="canZoomIn">ズームイン可能かどうか。</param>
    /// <param name="canZoomOut">ズームアウト可能かどうか。</param>
    /// <remarks>
    /// <see cref="ZoomViewControl.ZoomChanged"/> で通知される可否、または <see cref="ZoomViewControl.CanZoomIn"/> /
    /// <see cref="ZoomViewControl.CanZoomOut"/>（初期表示時の参照）に基づいて、⊖⊕ ボタンの <c>IsEnabled</c> を更新する。
    /// </remarks>
    private void UpdateZoomButtonState(bool canZoomIn, bool canZoomOut)
    {
        ZoomInButton.IsEnabled = canZoomIn;
        ZoomOutButton.IsEnabled = canZoomOut;
    }

    /// <summary>マウスポインタ移動時に、ポインタ位置のドメイン座標（画像ピクセル）をステータスへ表示する。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">ポインタイベント。</param>
    /// <remarks>
    /// ビューポート座標 → コンテンツ座標（元画像ピクセル）の変換は ZoomableView が行う。<br/>
    /// 画像外（ビューポートの空き領域）では "-" を表示する。
    /// </remarks>
    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_vm.PreviewImage is not { } img)
        {
            PointerPositionText.Text = "ポインタ: -（画像なし）";
            return;
        }

        var viewportPos = e.GetPosition(ZoomableView);
        var imageRect = ZoomableView.ImageRectViewport();
        var displayPoint = viewportPos - imageRect.Position;
        if (displayPoint.X < 0 || displayPoint.Y < 0 ||
            displayPoint.X >= imageRect.Width || displayPoint.Y >= imageRect.Height)
        {
            PointerPositionText.Text = "ポインタ: -（画像外）";
            return;
        }

        // 画像表示矩形の原点を基準に変換する（Fit 中央寄せの原点補正を含む）
        var contentPos = ZoomableView.ViewportToContentImage(viewportPos);
        var x = (int)Math.Clamp(contentPos.X, 0, img.Size.Width - 1);
        var y = (int)Math.Clamp(contentPos.Y, 0, img.Size.Height - 1);
        PointerPositionText.Text = $"ポインタ: {x}, {y}（画像ピクセル）";
    }

    /// <summary>画像ファイルを開いてプレビューに設定する。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">ルーティングイベント。</param>
    private async void OnOpenImageClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

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

        if (files.Count == 0) return;

        try
        {
            using var stream = File.OpenRead(files[0].Path.LocalPath);
            using var bitmap = new Bitmap(stream);
            _vm.SetPreviewImage(new EsBitmapAvaloniaImage(bitmap.ToEsBitmap()));
        }
        catch (Exception)
        {
            // 画像を読み込めない場合は何もしない（現在の画像を維持）
        }
    }
}
