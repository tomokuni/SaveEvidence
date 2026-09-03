using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Estable.AvaloniaUI.LineDetection;

namespace LineDetectionSample.ViewModels;

/// <summary>「線分検出」ページの ViewModel。クリップボードの画像からウィンドウ枠となる辺（線分）を検出する。</summary>
/// <remarks>
/// 検出対象の画像・検出結果・ルーペ・ポインタ状態は <see cref="DetectionContext"/> が保持する。<br/>
/// 本ページ専用のコンテキストを生成し、他のページとは状態を共有しない。<br/>
/// 本クラスは線分検出ページ固有の状態（検出方式の選択肢・選択中）と、クリップボード貼り付け処理を担う。
/// </remarks>
public sealed partial class DetectPageViewModel : ObservableObject
{
    /// <summary>検出方式の選択肢（ロジック）。</summary>
    public IReadOnlyList<LineDetectorOption> Detectors { get; } =
    [
        new LineDetectorOption(new LogicLineDetector()),
    ];

    /// <summary>本ページが専有する検出コンテキスト。</summary>
    public DetectionContext Context { get; }

    /// <summary>選択中の検出方式。</summary>
    [ObservableProperty]
    private LineDetectorOption _selectedDetector;

    /// <summary>DetectPageViewModel を初期化する。</summary>
    public DetectPageViewModel()
    {
        Context = new DetectionContext();

        _selectedDetector = Detectors[0];
        Context.SelectedDetector = _selectedDetector;
    }

    /// <summary>検出方式の変更時に、既存の画像（再貼り付けなし）で再検出する。</summary>
    /// <remarks>同じ画像に対して方式を切り替えて比較できるようにする。</remarks>
    partial void OnSelectedDetectorChanged(LineDetectorOption value)
    {
        Context.SelectedDetector = value;

        if (Context.Image is not null)
        {
            Context.Detect();
        }
    }

    /// <summary>クリップボードから取得した画像を表示画像として設定し、選択中の方式で再検出する。</summary>
    /// <param name="bitmap">クリップボードから取得した画像。null の場合は何もしない。</param>
    /// <remarks>画像の所有権は <see cref="DetectionContext"/> へ移る（次回の画像更新時に <see cref="DetectionContext.Image"/> を破棄する）。</remarks>
    public void SetPastedImage(Bitmap? bitmap)
        => Context.SetPastedImage(bitmap);
}
