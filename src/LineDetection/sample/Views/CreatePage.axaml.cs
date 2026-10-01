using Avalonia.Controls;
using Avalonia.Interactivity;
using EsUtil.Platform.Core;
using EsUtil.SaveEvidence.Parts.CaptureSession.AvaloniaUI;
using EsUtil.SaveEvidence.LineDetection.Sample.ViewModels;

namespace EsUtil.SaveEvidence.LineDetection.Sample.Views;

/// <summary>「データ作成」ページのコードビハインド。 Marquee キャプチャの呼び出しと結果の反映を担う。</summary>
/// <remarks>
/// CaptureSessionControl は UI スレッドから呼び出す必要があるため、ボタンイベントから直接呼び出す。<br/>
/// キャプチャ結果（画像 + 領域）は <see cref="CreatePageViewModel.ApplyCaptureResult"/> で ViewModel へ渡し、<br/>
/// 領域内ウィンドウの列挙・正解選択・保存は ViewModel が行う。
/// </remarks>
public partial class CreatePage : UserControl
{
    /// <summary>キャプチャ統括者（Facade）。</summary>
    private readonly CaptureSessionControl _capture = new(initialSettings: null);

    /// <summary>ビューモデル。</summary>
    private CreatePageViewModel? _vm;

    /// <summary>CreatePage を初期化する。</summary>
    public CreatePage()
    {
        InitializeComponent();

        // コントロール破棄時に CaptureSessionControl を破棄する
        Unloaded += (_, _) => _capture.Dispose();
    }

    /// <summary>DataContext が設定されたとき、ビューモデルを保持する。</summary>
    /// <param name="e">依存プロパティ変更イベント。</param>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        _vm = DataContext as CreatePageViewModel;
    }

    /// <summary>スクリーンから Marquee で領域選択キャプチャを開始し、データ作成ページへ反映する。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    /// <remarks>
    /// キャプチャ完了後、<see cref="CreatePageViewModel.ApplyCaptureResult"/> で画像・領域を反映し、<br/>
    /// 領域内ウィンドウの一覧（正解選択用）を構築する。
    /// </remarks>
    private async void OnCaptureClicked(object? sender, RoutedEventArgs e)
    {
        if (_vm is null)
        {
            return;
        }

        // キャプチャのオーナーウィンドウを取得する（null の場合はキャプチャを開始しない）
        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        _vm.StatusText = "スクリーン上で Marquee しています（Enter / ダブルクリックで確定、Esc でキャンセル）...";
        var result = await _capture.StartCaptureAsync(CaptureType.AreaSelect, owner);
        _vm.ApplyCaptureResult(result);
    }
}
