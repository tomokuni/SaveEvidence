using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Estable.AvaloniaUI.ZoomView;
using ZoomViewControl.Sample.Imaging;
using ZoomViewControl.Sample.ViewModels;

namespace ZoomViewControl.Sample.Views;

/// <summary>メインウィンドウ。ZoomViewControl の ZoomableView + ZoomManager の連携を担う。</summary>
/// <remarks>
/// ズーム操作（ボタン・スライダ・Ctrl+ホイール）と ViewModel（<see cref="IZoomHost"/>）の橋渡しを<br/>
/// コードビハインドで行う。ZoomManager がズーム計算・表示更新を統括する。<br/>
/// </remarks>
public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _vm = new();
    private readonly ZoomManager _zoomManager = new();

    /// <summary>スライダ同期中の再入防止フラグ（プログラム側の同期による ValueChanged を無視する）。</summary>
    private bool _isSyncingZoomSlider;

    /// <summary>MainWindow の新しいインスタンスを初期化する。</summary>
    /// <remarks>
    /// ZoomManager に依存関係を注入し、ViewModel のプロパティ変更・コントロールのイベントを配線する。<br/>
    /// 初期表示としてテスト画像を設定する。
    /// </remarks>
    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;

        // ZoomManager に依存関係を注入
        _zoomManager.ViewModel = _vm;
        _zoomManager.ZoomableView = ZoomableView;

        // ズーム率の変化をスライダへ同期する（ZoomManager はスライダに依存しないため、ホスト側で同期する）
        _zoomManager.ZoomChanged += (_, _) => SyncZoomSlider();

        // スライダの変更を ZoomManager へ反映する
        // （XAML ロード中の Slider 初期化（Value の coerce）でも ValueChanged が発火するため、
        //  依存注入後のコンストラクタで購読する。XAML 属性では未注入状態で呼ばれてしまう）
        ZoomSlider.ValueChanged += OnZoomSliderValueChanged;

        // ViewModel のプロパティ変更時にズーム表示を適用する
        _vm.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(MainWindowViewModel.PreviewImage):
                    ZoomableView.SetSource(_vm.PreviewImage);
                    _zoomManager.ApplyZoom();
                    break;
                case nameof(MainWindowViewModel.ZoomPercent):
                    _zoomManager.ApplyZoom();
                    break;
                case nameof(MainWindowViewModel.CenterAlign):
                    // 中央寄せ/左上寄せの変更を画像配置へ即時反映する
                    _zoomManager.UpdateImageAlignment();
                    break;
            }
        };

        // ビューポートリサイズ時に Fit 表示を再適用
        ZoomableView.SizeChanged += OnZoomableViewSizeChanged;

        // Ctrl+マウスホイールによるズーム要求（ZoomableView が発火。ズーム率の決定は ZoomManager）。
        // マウスポインタ位置をズーム中心に設定し、ズーム後にリセットする。
        ZoomableView.ZoomRequested += (_, e) =>
        {
            _zoomManager.PointerPosition = e.PointerPosition;
            if (e.ZoomIn) _zoomManager.ZoomInByWheel();
            else _zoomManager.ZoomOutByWheel();
            _zoomManager.PointerPosition = null;
        };

        // マウスポインタ移動でポインタ位置のドメイン座標（画像ピクセル）を表示する
        ZoomableView.PointerMoved += OnPointerMoved;

        // 初期表示: テスト画像を設定（ズーム・パンの動作確認用）
        _vm.SetPreviewImage(TestImageFactory.Create());

        // 初期のズーム率をスライダに反映
        SyncZoomSlider();
    }

    /// <summary>ズームアウトボタンクリック。</summary>
    private void OnZoomOutClick(object? sender, RoutedEventArgs e) => _zoomManager.ZoomOut();

    /// <summary>ズームインボタンクリック。</summary>
    private void OnZoomInClick(object? sender, RoutedEventArgs e) => _zoomManager.ZoomIn();

    /// <summary>Fit ボタンクリック（ズーム率 0 = 自動）。</summary>
    private void OnFitClick(object? sender, RoutedEventArgs e) => _vm.ZoomPercent = 0;

    /// <summary>ズームスライダの変更ハンドラ。スライダ操作によるズーム率の適用を行う。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">値変更イベント。</param>
    /// <remarks>プログラム側の同期（<see cref="SyncZoomSlider"/>）による変更は無視する。</remarks>
    private void OnZoomSliderValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isSyncingZoomSlider) return;
        _zoomManager.ApplyZoomFromSlider(ZoomSlider.Value);
    }

    /// <summary>ビューポートのサイズ変更時に Fit 表示を再適用する。</summary>
    private void OnZoomableViewSizeChanged(object? sender, SizeChangedEventArgs e)
        => _zoomManager.OnZoomableViewSizeChanged(e);

    /// <summary>スライダ同期時の値比較許容差。</summary>
    private const double ZoomSliderEpsilon = 0.01;

    /// <summary>ズームスライダの値を現在のズーム率へ同期する。</summary>
    /// <remarks>
    /// ズーム率の変化（<see cref="ZoomManager.ZoomChanged"/>）を受けて、スライダ値を現在のズーム率へ同期する。<br/>
    /// 自動（Fit）表示中はスライダで表現できないため、最小値に合わせる。<br/>
    /// 同期中の ValueChanged による再入を防ぐ。
    /// </remarks>
    private void SyncZoomSlider()
    {
        if (_isSyncingZoomSlider) return;
        _isSyncingZoomSlider = true;
        try
        {
            var zoom = _vm.ZoomPercent;
            var sliderValue = zoom > 0 ? zoom : ZoomSlider.Minimum;
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

        var contentPos = ZoomableView.ViewportToContent(viewportPos);
        var x = (int)Math.Clamp(contentPos.X, 0, img.PixelSize.Width - 1);
        var y = (int)Math.Clamp(contentPos.Y, 0, img.PixelSize.Height - 1);
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
            _vm.SetPreviewImage(new Bitmap(stream));
        }
        catch (Exception)
        {
            // 画像を読み込めない場合は何もしない（現在の画像を維持）
        }
    }
}
