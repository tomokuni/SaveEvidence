using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Point = Avalonia.Point;
using Rect = Avalonia.Rect;
using DragRectControl.Sample.Logging;
using DragRectControl.Sample.ViewModels;
using Estable.AvaloniaUI.DragRect;
using Estable.AvaloniaUI.Platform;
using Estable.AvaloniaUI.Platform.Geometry;
using Estable.AvaloniaUI.Platform.Imaging;
using System.Collections.Specialized;

namespace DragRectControl.Sample.Views;

/// <summary>メインウィンドウ。DragRectControl のイベント購読・設定反映・座標ポート設定を担う。</summary>
/// <remarks>
/// 座標ポートは「ドメイン = コントロールローカル DIP」の等価変換（DPI 100% 相当）で動作確認する。<br/>
/// 選択状態は <see cref="MainWindowViewModel"/> へ同期し、コントロール設定は PropertyChanged を購読して反映する。<br/>
/// </remarks>
public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _vm = new();

    /// <summary>背景に敷くダミー画像（ドメイン面のサイズ感を示す）。Skia 直描画（PbBitmapAvaloniaImage）で表示する。</summary>
    private static readonly PbBitmapAvaloniaImage Backdrop = CreateBackdrop();

    /// <summary>画像の論理幅（ドメインピクセル）。</summary>
    private const double ImageWidth = 1200;

    /// <summary>画像の論理高さ（ドメインピクセル）。</summary>
    private const double ImageHeight = 800;

    /// <summary>ズーム倍率（1.0 = 等倍）。</summary>
    private double _zoom = 1.0;

    /// <summary>ズーム倍率の下限（10%）。</summary>
    private const double MinZoom = 0.1;

    /// <summary>ズーム倍率の上限（500%）。</summary>
    private const double MaxZoom = 5.0;

    /// <summary><see cref="MainWindow"/> の新しいインスタンスを初期化する。</summary>
    /// <remarks>
    /// DragRectControl の公開イベントを購読し、選択状態を ViewModel へ同期する。<br/>
    /// ViewModel のコントロール設定（外観・動作）を DragRectControl へ反映する。<br/>
    /// </remarks>
    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;

        // 背景のダミー画像（ドメイン面のサイズ感）
        BackdropImage.Source = Backdrop;

        // コントロール内部の操作ログを画面のログ表示領域へ出力する
        DragRect.Log = new InMemoryLogger(line => Dispatcher.UIThread.Post(() => _vm.AddLog(line)));

        // 連携 ScrollViewer を明示注入する（スクロール・ズーム検知 + ホイール操作。DragRect は Canvas 内にあるため祖先探索は不可）
        DragRect.AttachScrollViewer(ImageScroll);

        // ズーム倍率に応じたサイズ・座標ポートを適用する
        ApplyZoom(null, 1.0);

        // DragRectControl のイベント購読（上位APはマウス・キーを直接処理しない）
        DragRect.SelectionChanged += OnSelectionChanged;
        DragRect.SelectionStarted += OnSelectionStarted;
        DragRect.SelectionCompleted += OnSelectionCompleted;
        DragRect.SelectionCancelled += OnSelectionCancelled;
        DragRect.MouseDown += OnMouseDown;
        DragRect.MouseUp += OnMouseUp;
        DragRect.MouseClicked += OnMouseClicked;
        DragRect.MouseDoubleClicked += OnMouseDoubleClicked;
        DragRect.MouseMoved += OnMouseMoved;
        DragRect.LoupePositionMoved += OnLoupePositionMoved;
        // Escape / Enter の自己処理（確定・キャンセル要求）とズーム・スクロール検知も購読する
        DragRect.ConfirmRequested += OnConfirmRequested;
        DragRect.CancelRequested += OnCancelRequested;
        DragRect.ViewportChanged += OnViewportChanged;

        // ViewModel のコントロール設定を DragRectControl へ反映する
        _vm.PropertyChanged += OnViewModelPropertyChanged;
        ApplyControlSettings();

        // ログ追加時に最下部（最新行）へ自動スクロールする
        _vm.LogLines.CollectionChanged += OnLogLinesChanged;

        _vm.StatusText = "Ctrl+ホイールでズーム / ホイールでスクロール / ドラッグで選択";
        _vm.AddLog("[App] サンプル起動。ドラッグで領域選択してください。");
    }

    /// <summary>ズーム倍率を Canvas・画像・DragRectControl のサイズと座標ポートへ適用する。</summary>
    /// <param name="anchor">ズームの中心（Canvasローカル座標）。null の場合は初期化のみ。</param>
    /// <param name="oldZoom">適用前のズーム倍率（スクロール位置の調整用）。</param>
    /// <remarks>
    /// スクロールオフセットの調整は<b>同期的</b>に行う（<c>Dispatcher.Post</c> を使わない）。<br/>
    /// 非同期にすると、ズーム → サイズ変更 → レイアウト（オフセットがクランプされた状態で描画）→<br/>
    /// オフセット変更 → 再レイアウト、と 2 段階の描画が発生し、選択矩形・背景がちらつく。<br/>
    /// Canvas サイズ変更とオフセット変更を同じフレームで行うことで、描画を 1 回にまとめる。
    /// </remarks>
    private void ApplyZoom(Point? anchor, double oldZoom)
    {
        var ratio = _zoom / oldZoom;

        // ズーム中心（カーソル位置）が同じ表示位置に残るようスクロールオフセットを先に計算する
        // （ScrollViewer のオフセットはスクロール範囲内にクランプされるため、Canvas サイズ変更前に
        // 目標値を計算し、サイズ変更と同じフレームで設定する）。
        var offset = ImageScroll.Offset;
        var newOffset = anchor is { } a
            ? new Vector(a.X * ratio - a.X + offset.X, a.Y * ratio - a.Y + offset.Y)
            : offset;

        // Canvas・背景画像・DragRectControl の表示サイズをズーム倍率に合わせる
        CanvasHost.Width = ImageWidth * _zoom;
        CanvasHost.Height = ImageHeight * _zoom;
        BackdropImage.Width = ImageWidth * _zoom;
        BackdropImage.Height = ImageHeight * _zoom;
        DragRect.Width = ImageWidth * _zoom;
        DragRect.Height = ImageHeight * _zoom;

        // 座標ポート: ドメイン = 画像ピクセル（800x500）、DIP = 表示座標（ズーム倍率で変換）
        // SetCoordinateTransform は内部で InvalidateVisual を呼び、ズーム時のちらつきを防ぐ
        // Forward = DIP→ドメイン（_zoom で除算 = 画像ピクセルへ）、Inverse = ドメイン→DIP（_zoom で乗算）
        DragRect.SetCoordinateTransform(new CoordTransform(
            p => new Point(p.X / _zoom, p.Y / _zoom),
            p => new Point(p.X * _zoom, p.Y * _zoom),
            () => new Size(ImageWidth, ImageHeight)));

        // スクロールオフセットを同期的に設定する（Canvas サイズ変更と同じフレームで反映される）
        ImageScroll.Offset = newOffset;

        _vm.ZoomPercent = (int)Math.Round(_zoom * 100);

        _vm.AddLog($"[App] ズーム: {_zoom:F2}x");
    }

    /// <summary>選択面のマウスホイール処理。Ctrl+ホイールでズーム、通常ホイールはスクロール。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">ホイールイベント引数。</param>
    private void OnCanvasWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        // Ctrl なしの通常ホイールは ScrollViewer にスクロールさせる
        if ((e.KeyModifiers & KeyModifiers.Control) == 0)
        {
            return;
        }

        var anchor = e.GetPosition(CanvasHost);
        var oldZoom = _zoom;
        _zoom = Math.Clamp(_zoom + (e.Delta.Y > 0 ? 0.1 : -0.1), MinZoom, MaxZoom);
        ApplyZoom(anchor, oldZoom);
        e.Handled = true;
    }

    /// <summary>ログ行が追加されたときに最下部（最新行）へ自動スクロールする。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">コレクション変更イベント引数。</param>
    private void OnLogLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // 追加（またはリセット）後に最新行が表示されるよう、レイアウト完了を待って最下部へスクロールする
        Dispatcher.UIThread.Post(() => LogScroll.ScrollToEnd());
    }

    /// <summary>ViewModel の設定プロパティを DragRectControl へ反映する。</summary>
    private void ApplyControlSettings()
    {
        DragRect.BorderThickness = _vm.BorderThickness;
        DragRect.HandleSize = _vm.HandleSize;
        DragRect.HandleHitRadius = _vm.HandleHitRadius;
        DragRect.MinSelectionSize = _vm.MinSelectionSize;
        DragRect.BorderColor = _vm.BorderColorOption.Color;
        DragRect.HandleColor = _vm.HandleColorOption.Color;
        DragRect.MarkerColor = _vm.MarkerColorOption.Color;
        DragRect.MarkerLength = _vm.MarkerLength;
        DragRect.ClampToDomainBounds = _vm.ClampToDomainBounds;
        // マウスが描画域にない場合でも、設定変更を即時に再描画へ反映する
        DragRect.InvalidateVisual();
    }

    /// <summary>ViewModel の設定プロパティ変更を DragRectControl へ反映する。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">プロパティ変更イベント引数。</param>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainWindowViewModel.BorderThickness):
                DragRect.BorderThickness = _vm.BorderThickness;
                break;
            case nameof(MainWindowViewModel.HandleSize):
                DragRect.HandleSize = _vm.HandleSize;
                break;
            case nameof(MainWindowViewModel.HandleHitRadius):
                DragRect.HandleHitRadius = _vm.HandleHitRadius;
                break;
            case nameof(MainWindowViewModel.MinSelectionSize):
                DragRect.MinSelectionSize = _vm.MinSelectionSize;
                break;
            case nameof(MainWindowViewModel.BorderColorOption):
                DragRect.BorderColor = _vm.BorderColorOption.Color;
                break;
            case nameof(MainWindowViewModel.HandleColorOption):
                DragRect.HandleColor = _vm.HandleColorOption.Color;
                break;
            case nameof(MainWindowViewModel.MarkerColorOption):
                DragRect.MarkerColor = _vm.MarkerColorOption.Color;
                break;
            case nameof(MainWindowViewModel.MarkerLength):
                DragRect.MarkerLength = _vm.MarkerLength;
                break;
            case nameof(MainWindowViewModel.ClampToDomainBounds):
                DragRect.ClampToDomainBounds = _vm.ClampToDomainBounds;
                _vm.AddLog($"[App] ClampToDomainBounds: {(_vm.ClampToDomainBounds ? "ON（画像内にクリップ）" : "OFF（画像外も選択可能）")}");
                break;
            default:
                return;
        }

        // マウスが描画域にない場合でも、設定変更を即時に再描画へ反映する
        DragRect.InvalidateVisual();
    }

    /// <summary>選択矩形が変更されたときの処理。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">選択矩形イベント引数。</param>
    private void OnSelectionChanged(object? sender, SelectionRectEventArgs e)
    {
        _vm.UpdateSelection(true, e.Rect.ToAvaloniaRect());
        _vm.AddLog($"[Event] SelectionChanged: X={e.Rect.X:F0}, Y={e.Rect.Y:F0}, W={e.Rect.Width:F0}, H={e.Rect.Height:F0}");
    }

    /// <summary>ドラッグの始点が決定されたときの処理。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">座標イベント引数。</param>
    private void OnSelectionStarted(object? sender, DragRectPointEventArgs e)
        => _vm.AddLog($"[Event] SelectionStarted: ({e.Point.X:F0}, {e.Point.Y:F0})");

    /// <summary>選択が確定したときの処理。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">選択矩形イベント引数。</param>
    private void OnSelectionCompleted(object? sender, SelectionRectEventArgs e)
    {
        _vm.UpdateSelection(true, e.Rect.ToAvaloniaRect());
        _vm.AddLog($"[Event] SelectionCompleted: X={e.Rect.X:F0}, Y={e.Rect.Y:F0}, W={e.Rect.Width:F0}, H={e.Rect.Height:F0}");
    }

    /// <summary>領域がキャンセルされたときの処理。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void OnSelectionCancelled(object? sender, EventArgs e)
    {
        _vm.UpdateSelection(false, default);
        _vm.AddLog("[Event] SelectionCancelled");
    }

    /// <summary>左ボタン押下時の処理。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">座標イベント引数。</param>
    private void OnMouseDown(object? sender, DragRectPointEventArgs e)
        => _vm.AddLog($"[Event] MouseDown: ({e.Point.X:F0}, {e.Point.Y:F0})");

    /// <summary>左ボタン解放時の処理。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">座標イベント引数。</param>
    private void OnMouseUp(object? sender, DragRectPointEventArgs e)
        => _vm.AddLog($"[Event] MouseUp: ({e.Point.X:F0}, {e.Point.Y:F0})");

    /// <summary>クリック（ドラッグ移動なしの押下→解放）時の処理。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">座標イベント引数。</param>
    private void OnMouseClicked(object? sender, DragRectPointEventArgs e)
        => _vm.AddLog($"[Event] MouseClicked: ({e.Point.X:F0}, {e.Point.Y:F0})");

    /// <summary>ダブルクリック検出時の処理（汎用公開。確定/キャンセルは DragRectControl 自身が判定し ConfirmRequested / CancelRequested で通知する）。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">ダブルクリックイベント引数。</param>
    private void OnMouseDoubleClicked(object? sender, DragRectPointEventArgs e)
    {
        _vm.AddLog($"[Event] MouseDoubleClicked: ({e.Point.X:F0}, {e.Point.Y:F0})");
    }

    /// <summary>マウス移動ごとのポインタ位置の処理。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">座標イベント引数。</param>
    /// <remarks>マウス移動ごとに発火するため、ログは抑制してデバッグ用途のみにする。</remarks>
    private void OnMouseMoved(object? sender, DragRectPointEventArgs e)
        => _vm.AddLog($"[Event] MouseMoved: ({e.Point.X:F0}, {e.Point.Y:F0})");

    /// <summary>nudge 後のルーペ中心位置要求の処理。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">座標イベント引数。</param>
    private void OnLoupePositionMoved(object? sender, DragRectPointEventArgs e)
        => _vm.AddLog($"[Event] LoupePositionMoved: ({e.Point.X:F0}, {e.Point.Y:F0})");

    /// <summary>Enter キーによる確定要求の処理（選択破棄はコントロールが実施済み）。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">選択矩形イベント引数。</param>
    private void OnConfirmRequested(object? sender, SelectionRectEventArgs e)
    {
        _vm.StatusText = "Enter: 確定要求を受信しました。";
        _vm.AddLog($"[Event] ConfirmRequested: X={e.Rect.X:F0}, Y={e.Rect.Y:F0}, W={e.Rect.Width:F0}, H={e.Rect.Height:F0}");
    }

    /// <summary>Escape キーによるキャンセル要求の処理（選択破棄はコントロールが実施済み）。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void OnCancelRequested(object? sender, EventArgs e)
    {
        _vm.UpdateSelection(false, default);
        _vm.StatusText = "Escape: キャンセル要求を受信しました。";
        _vm.AddLog("[Event] CancelRequested");
    }

    /// <summary>ズーム・スクロール（ScrollViewer の ScrollChanged）検知時の処理。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">選択矩形イベント引数（現在の選択矩形 + Snapshot）。</param>
    private void OnViewportChanged(object? sender, SelectionRectEventArgs e)
        => _vm.AddLog($"[Event] ViewportChanged: X={e.Rect.X:F0}, Y={e.Rect.Y:F0}, W={e.Rect.Width:F0}, H={e.Rect.Height:F0}");

    /// <summary>選択面の押下時に DragRectControl へフォーカスを渡す。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">ポインターイベント引数。</param>
    /// <remarks>矢印キー nudge は DragRectControl のフォーカスが必要なため、面の押下でフォーカスを移動する。</remarks>
    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        DragRect.Focus();
    }

    /// <summary>選択をクリアする。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void OnClearClicked(object? sender, RoutedEventArgs e)
    {
        DragRect.ResetSelection();
        _vm.UpdateSelection(false, default);
        _vm.AddLog("[App] ResetSelection() を呼び出しました。");
    }

    /// <summary>固定の選択矩形を設定する。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    /// <remarks>外部からの復元（<see cref="DragRectControl.SetSelection"/>）の動作確認。</remarks>
    private void OnSetFixedClicked(object? sender, RoutedEventArgs e)
    {
        DragRect.SetSelection(new Rect(250, 180, 500, 360));
        _vm.UpdateSelection(true, new Rect(250, 180, 500, 360));
        _vm.AddLog("[App] SetSelection(250,180,500,360) を呼び出しました。");
    }

    /// <summary>確定要求（Enter キーと同じ条件）を発行する。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    /// <remarks>プログラムからの確定要求（<see cref="DragRectControl.RequestConfirm"/>）の動作確認。</remarks>
    private void OnConfirmClicked(object? sender, RoutedEventArgs e)
    {
        DragRect.RequestConfirm();
        _vm.AddLog("[App] RequestConfirm() を呼び出しました。");
    }

    /// <summary>キャンセル要求（Escape キーと同じ条件）を発行する。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    /// <remarks>プログラムからのキャンセル要求（<see cref="DragRectControl.RequestCancel"/>）の動作確認。</remarks>
    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        DragRect.RequestCancel();
        _vm.AddLog("[App] RequestCancel() を呼び出しました。");
    }

    /// <summary>操作ログをクリアする。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void OnClearLogClicked(object? sender, RoutedEventArgs e)
        => _vm.ClearLog();

    /// <summary>背景用のダミー画像を生成する（グリッド付き、サイズ感の確認用）。</summary>
    /// <returns>生成した WriteableBitmap。</returns>
    /// <remarks>
    /// ビットマップサイズは <see cref="ImageWidth"/> / <see cref="ImageHeight"/> に合わせる
    /// （const のためリテラルで指定。グリッド間隔は 100px で視認性を確保する）。
    /// </remarks>
    private static PbBitmapAvaloniaImage CreateBackdrop()
    {
        const int width = 1200;
        const int height = 800;
        var bitmap = PbBitmap.Create(width, height);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                // 淡いグリッド（100px 間隔。画像サイズ拡大に合わせて間隔も広げる）
                var isGrid = x % 100 == 0 || y % 100 == 0;
                byte v = isGrid ? (byte)200 : (byte)235;
                bitmap.SetPixel(x, y, v, v, v, 255);
            }
        }

        return new PbBitmapAvaloniaImage(bitmap);
    }
}
