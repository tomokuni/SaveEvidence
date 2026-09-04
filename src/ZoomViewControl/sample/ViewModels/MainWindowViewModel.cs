using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ZoomViewControl.Sample.ViewModels;

/// <summary>
/// メイン画面の ViewModel。ZoomViewControl の状態（ズーム率・表示テキスト・中央寄せ）を表示用に保持する。
/// </summary>
/// <remarks>
/// ズーム率・可否・表示テキストの状態は <c>ZoomViewControl</c>（自己完結型複合コントロール）が所有し、<br/>
/// 本 ViewModel は <c>ZoomChanged</c> イベントで通知された値を表示用に保持する。<br/>
/// 画像のライフサイクル管理（Dispose）は <see cref="SetPreviewImage"/> で行う。
/// </remarks>
public sealed partial class MainWindowViewModel : ObservableObject
{
    /// <summary>プレビュー画像変更前の旧画像（破棄用）。</summary>
    private Bitmap? _previousPreviewImage;

    /// <summary>プレビュー画像（Avalonia Bitmap）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ImageSizeText))]
    private Bitmap? _previewImage;

    /// <summary>現在のズーム率（%）。0 = Fit（自動）。表示用ストア（ズーム率の単一所有は ZoomViewControl。ZoomChanged で更新）。</summary>
    [ObservableProperty]
    private double _zoomPercent;

    /// <summary>ステータスバー等に表示する拡大率テキスト。（表示用ストア。ZoomChanged で更新）。</summary>
    [ObservableProperty]
    private string _zoomDisplayText = "100%";

    /// <summary>Fit 時の画像の中央寄せ / 左上寄せ（設定パネルのチェックボックスで変更）。</summary>
    [ObservableProperty]
    private bool _centerAlign = true;

    /// <summary>画像サイズ（元画像ピクセル）の表示テキスト。画像未設定時は空欄。</summary>
    public string ImageSizeText => PreviewImage is { } img ? $"{img.PixelSize.Width} x {img.PixelSize.Height}" : "";

    /// <summary>指定された画像（Avalonia Bitmap）をプレビューとして設定する。</summary>
    /// <param name="image">設定する画像。null の場合は画像なし。</param>
    /// <remarks>既存の画像は破棄してから差し替える。</remarks>
    public void SetPreviewImage(Bitmap? image)
    {
        _previousPreviewImage?.Dispose();
        _previousPreviewImage = null;

        PreviewImage = image;
    }
}
