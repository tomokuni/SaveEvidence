using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using LoupeControl.Sample.Logging;
using LoupeControl.Sample.ViewModels;

namespace LoupeControl.Sample.Views;

/// <summary>メインウィンドウ。マウス追従とルーペ配置を担う。</summary>
/// <remarks>
/// マウス位置（View 固有の操作）の取得とルーペの配置のみをコードビハインドで行い、<br/>
/// 外観設定は <see cref="MainWindowViewModel"/> へバインドする。<br/>
/// </remarks>
public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _vm = new();

    /// <summary><see cref="MainWindow"/> の新しいインスタンスを初期化する。</summary>
    /// <remarks>
    /// ルーペの初期化と初期中心座標を設定する。<br/>
    /// </remarks>
    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;

        // ルーペの表示状態を明示的に反映（AXAML の初期値は非表示のため）
        Loupe.IsVisible = _vm.IsLoupeVisible;

        // 描画エラーの通知先として ILogger を設定する（デバッグ出力へ転送）
        Loupe.Log = new DebugLogger();

        // 初期状態でも拡大表示が見えるように中心を設定
        _vm.CenterPoint = new Point(120, 120);

        Loaded += (_, _) => UpdateLoupePosition(new Point(120, 120));
    }

    /// <summary>テスト画像上のマウス移動で中心座標とルーペ位置を更新する。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">ポインタイベント引数。</param>
    /// <remarks>
    /// 中心座標は画像の外もそのまま許可する。LoupeControl は画像外の領域を透明として切り出し、<br/>
    /// 市松模様はカーソルと画像端の距離に応じて部分的に表示される（視野全体が画像外になった場合のみ全面市松）。<br/>
    /// Stretch=None のため画像ローカル DIP ≒ 元画像ピクセル。<br/>
    /// </remarks>
    private void OnCanvasPointerMoved(object? sender, PointerEventArgs e)
    {
        var pos = e.GetPosition(TestImage);

        // 画像範囲へクランプしない（画像外も中心として許容し、距離に応じた部分市松を表示する）
        _vm.CenterPoint = new Point(pos.X, pos.Y);
        UpdateLoupePosition(pos);
    }

    /// <summary>ルーペをカーソルの右下（+24 DIP）へ配置し、描画領域内へクランプする。</summary>
    /// <param name="pos">カーソル位置（画像ローカル座標）。</param>
    private void UpdateLoupePosition(Point pos)
    {
        var size = _vm.RequestedSize;
        var bounds = new Size(CanvasHost.Bounds.Width, CanvasHost.Bounds.Height);

        var x = Math.Clamp(pos.X + 24, 0, Math.Max(0, bounds.Width - size));
        var y = Math.Clamp(pos.Y + 24, 0, Math.Max(0, bounds.Height - size));

        Canvas.SetLeft(Loupe, x);
        Canvas.SetTop(Loupe, y);
    }

    /// <summary>Clear() の動作確認（非表示にして Bitmap を解放）。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void OnClearClicked(object? sender, RoutedEventArgs e) => Loupe.Clear();

    /// <summary>ForceUpdate() の動作確認（即時再生成）。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void OnForceUpdateClicked(object? sender, RoutedEventArgs e) => Loupe.ForceUpdate();

    /// <summary>RenderAsync() の動作確認（非同期生成の完了待機）。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    /// <remarks>
    /// 生成完了を待機できることを確認する（Fire-and-forget の自動再描画との違い）。<br/>
    /// 完了を await するため、連打しても世代管理により最新の結果のみ表示される。
    /// </remarks>
    private async void OnRenderAsyncClicked(object? sender, RoutedEventArgs e)
    {
        await Loupe.RenderAsync();
    }
}
