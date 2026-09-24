using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Estable.AvaloniaUI.Loupe;
using Estable.Loupe;
using EsUtil.Platform.Core.Imaging;
using LoupeControl.Sample.Logging;
using LoupeControl.Sample.ViewModels;
using SkiaSharp;

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

        // 文字（中心座標ラベル）はコントロールが描かないため、描画完了イベントでホストが描く
        Loupe.Rendered += OnLoupeRendered;

        // ルーペをカーソルの右下（+24 DIP）へ配置する。
        // 配置は LoupeControl.UpdatePosition（RenderTransform）が担い、ホスト側の Canvas.SetLeft/Top とは
        // 競合しない（Canvas.SetLeft/Top は LoupeControl の RenderTransform と加算され、ルーペが右下に
        // 大きくずれるため、配置はコントロール自身に任せる）。
        Loupe.PositionOffset = 24;

        // 初期状態でも拡大表示が見えるように中心を設定
        _vm.CenterPoint = new Point(120, 120);
    }

    /// <summary>テスト画像上のマウス移動で中心座標を更新する。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">ポインタイベント引数。</param>
    /// <remarks>
    /// 中心座標は画像の外もそのまま許可する。LoupeControl は画像外の領域を透明として切り出し、<br/>
    /// 市松模様はカーソルと画像端の距離に応じて部分的に表示される（視野全体が画像外になった場合のみ全面市松）。<br/>
    /// Stretch=None のため画像ローカル DIP ≒ 元画像ピクセル。<br/>
    /// ルーペの表示位置は LoupeControl.UpdatePosition（RenderTransform + PositionOffset）が計算するため、
    /// ホスト側では中心座標（CenterPoint）の設定のみを行う。
    /// </remarks>
    private void OnCanvasPointerMoved(object? sender, PointerEventArgs e)
    {
        var pos = e.GetPosition(TestImage);

        // 画像範囲へクランプしない（画像外も中心として許容し、距離に応じた部分市松を表示する）
        _vm.CenterPoint = new Point(pos.X, pos.Y);
    }

    /// <summary>描画完了時に中心座標ラベルの文字を描画する。</summary>
    /// <param name="sender">イベント送信元（LoupeControl）。</param>
    /// <param name="e">描画済み Bitmap と表示情報（中心座標ラベルの契約を含む）。</param>
    /// <remarks>
    /// 文字の描画はホストの責務である（テキストの実寸測定は描画 API 依存のため）。<br/>
    /// 配置はコアの <see cref="LoupeCoordLabel.ComputeFrame"/> が、描画順はプラットフォーム層の
    /// <see cref="SkiaLabel.Draw"/> が単一所有するため、本ハンドラは実測と描画先の用意だけを行う。
    /// </remarks>
    private void OnLoupeRendered(object? sender, LoupeRenderedEventArgs e)
    {
        if (e.CoordLabel is not { } coordLabel)
        {
            return;
        }

        var frame = coordLabel.ComputeFrame(SkiaText.Measure(coordLabel.Text, coordLabel.FontSize));
        using var canvas = new SKCanvas(e.Bitmap.ToSKBitmap());
        SkiaLabel.Draw(
            canvas, frame.Background, coordLabel.BackgroundColor, coordLabel.Text, frame.TextOrigin,
            coordLabel.FontSize, coordLabel.TextColor, coordLabel.CornerRadius);
    }

    /// <summary>Clear() の動作確認（非表示にして Bitmap を解放）。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void OnClearClicked(object? sender, RoutedEventArgs e) => Loupe.Clear();

    /// <summary>RequestRender() の動作確認（プロパティ変更を伴わない再生成要求）。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void OnRequestRenderClicked(object? sender, RoutedEventArgs e) => Loupe.RequestRender();
}
