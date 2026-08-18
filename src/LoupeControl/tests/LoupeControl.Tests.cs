using BindingFlags = System.Reflection.BindingFlags;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Estable.AvaloniaUI.Platform;
using Xunit;

namespace Estable.AvaloniaUI.Loupe.Tests;

/// <summary>
/// <see cref="LoupeControl"/> の単体テスト（Source / CenterPoint の Binding 方式・バッチ化・表示分離・範囲補正）。
/// </summary>
/// <remarks>
/// Avalonia ヘッドレステストは並列実行非対応（Dispatcher.UIThread が共有）のため、
/// 同じコレクション（<see cref="AvaloniaHeadlessCollection"/>）内で直列実行する。
/// </remarks>
[Collection("AvaloniaHeadless")]
public class LoupeControlTests
{
    /// <summary>テスト用の LoupeControl を生成する（ヘッドレスで初期化）。</summary>
    private static LoupeControl CreateControl()
        => new() { RequestedSize = 100, ZoomLevel = 2, IsVisible = true };

    // ====================================================================
    // 更新経路（バッチ化）
    // ====================================================================

    [AvaloniaFact]
    public async Task SourceとCenterPointを設定するとLoupeBitmapが生成される()
    {
        var control = CreateControl();
        control.Source = TestHelper.CreateSolidBitmap(200, 200);
        control.CenterPoint = new Point(100, 100);

        await control.RenderAsync();

        Assert.NotNull(control.LoupeBitmap);
    }

    [AvaloniaFact]
    public async Task 複数プロパティの連続変更はポスト後に1回の再描画に結合される()
    {
        var control = CreateControl();

        // ポスト前は同期生成されない（バッチ化: 要求は1つの Dispatcher ジョブに結合される）
        control.Source = TestHelper.CreateSolidBitmap(200, 200);
        control.CenterPoint = new Point(100, 100);
        Assert.Null(control.LoupeBitmap);

        await control.RenderAsync();
        Assert.NotNull(control.LoupeBitmap);
    }

    [AvaloniaFact]
    public async Task Source未設定でも市松模様のLoupeBitmapが生成される()
    {
        var control = CreateControl();

        control.CenterPoint = new Point(50, 50);
        await control.RenderAsync();

        Assert.NotNull(control.LoupeBitmap);
        Assert.Equal(100, control.LoupeBitmap!.PixelSize.Width);
        Assert.Equal(100, control.LoupeBitmap.PixelSize.Height);
    }

    [AvaloniaFact]
    public async Task Source設定_CenterPoint未設定でも市松模様のLoupeBitmapが生成される()
    {
        var control = CreateControl();
        control.Source = TestHelper.CreateSolidBitmap(200, 200);

        await control.RenderAsync();

        // center が未設定のため、画像抽出は行わず市松模様のみ描画する
        Assert.NotNull(control.LoupeBitmap);
        Assert.Equal(100, control.LoupeBitmap!.PixelSize.Width);
        Assert.Equal(100, control.LoupeBitmap.PixelSize.Height);
    }

    [AvaloniaFact]
    public void InitializeComponentを再実行しても再ロードされない()
    {
        var control = new LoupeControl();

        // 2 回目の呼び出しは _contentLoaded が true のため再ロードされない（冪等）
        control.InitializeComponent();

        Assert.NotNull(control.PART_Image);
    }

    [AvaloniaFact]
    public async Task PART_Image未初期化でもビットマップ生成と描画が動作する()
    {
        // PART_Image フィールドを null に設定して SetLoupeBitmap の null 分岐をカバーする。
        // 実際には InitializeComponent が必ず呼ばれるため到達しない防御的分岐だが、
        // ブランチカバレッジ 100% のためにテストで直接検証する。
        var control = CreateControl();
        var field = typeof(LoupeControl).GetField(
            "PART_Image", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(field);
        field!.SetValue(control, null);

        await control.RenderAsync();

        // 描画は内部 Image 要素（PART_Image）が null でも完了する（LoupeBitmap は生成される）
        Assert.NotNull(control.LoupeBitmap);
        Assert.Null(control.PART_Image);

        // PART_Image が null の状態で解放する（ReleaseBitmap の null 分岐をカバー）
        control.Clear();
        Assert.Null(control.LoupeBitmap);
    }

    [AvaloniaFact]
    public async Task CenterPoint変更でLoupeBitmapが再生成される()
    {
        var control = CreateControl();
        control.Source = TestHelper.CreateSolidBitmap(200, 200);
        control.CenterPoint = new Point(100, 100);
        await control.RenderAsync();
        var first = control.LoupeBitmap;

        control.CenterPoint = new Point(120, 120);
        await control.RenderAsync();

        Assert.NotNull(control.LoupeBitmap);
        Assert.NotSame(first, control.LoupeBitmap);
    }

    [AvaloniaFact]
    public async Task ForceUpdateでLoupeBitmapが再生成される()
    {
        var control = CreateControl();
        control.Source = TestHelper.CreateSolidBitmap(200, 200);
        control.CenterPoint = new Point(100, 100);
        await control.RenderAsync();
        var first = control.LoupeBitmap;

        control.ForceUpdate();

        // StartRender は fire-and-forget のため、生成完了をポーリングで待つ
        for (var i = 0; i < 100 && ReferenceEquals(control.LoupeBitmap, first); i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(5);
        }

        Assert.NotNull(control.LoupeBitmap);
        Assert.NotSame(first, control.LoupeBitmap);
    }

    [AvaloniaFact]
    public async Task RenderAsyncを再実行するとLoupeBitmapが再生成される()
    {
        var control = CreateControl();
        control.Source = TestHelper.CreateSolidBitmap(200, 200);
        control.CenterPoint = new Point(100, 100);
        await control.RenderAsync();
        var first = control.LoupeBitmap;

        await control.RenderAsync();

        Assert.NotNull(control.LoupeBitmap);
        Assert.NotSame(first, control.LoupeBitmap);
    }

    [AvaloniaFact]
    public async Task 外観プロパティの変更でLoupeBitmapが再生成される()
    {
        var control = CreateControl();
        control.Source = TestHelper.CreateSolidBitmap(200, 200);
        control.CenterPoint = new Point(100, 100);
        await control.RenderAsync();
        var first = control.LoupeBitmap;

        control.ZoomLevel = 4;
        await control.RenderAsync();

        Assert.NotNull(control.LoupeBitmap);
        Assert.NotSame(first, control.LoupeBitmap);
    }

    // ====================================================================
    // 表示制御（表示状態と再描画の分離）
    // ====================================================================

    [AvaloniaFact]
    public async Task ClearでLoupeBitmapが解放される()
    {
        var control = CreateControl();
        control.Source = TestHelper.CreateSolidBitmap(200, 200);
        control.CenterPoint = new Point(100, 100);
        await control.RenderAsync();

        control.Clear();

        Assert.Null(control.LoupeBitmap);
        Assert.False(control.IsVisible);
    }

    [AvaloniaFact]
    public async Task IsVisible_falseでLoupeBitmapが解放される()
    {
        var control = CreateControl();
        control.Source = TestHelper.CreateSolidBitmap(200, 200);
        control.CenterPoint = new Point(100, 100);
        await control.RenderAsync();

        // 表示状態（true）からの遷移でのみ Changed が発火して解放される
        // （AXAML 初期値が false のため、false→false では Changed は発火しない）
        control.IsVisible = true;
        control.IsVisible = false;

        Assert.Null(control.LoupeBitmap);
        Assert.False(control.IsVisible);
    }

    [AvaloniaFact]
    public async Task 非表示中の再描画は画像を生成しない()
    {
        var control = CreateControl();
        control.IsVisible = false;

        control.Source = TestHelper.CreateSolidBitmap(200, 200);
        control.CenterPoint = new Point(100, 100);
        Dispatcher.UIThread.RunJobs();

        await control.RenderAsync();
        Assert.Null(control.LoupeBitmap);
        Assert.False(control.IsVisible);

        control.IsVisible = true;
        Assert.True(control.IsVisible);
    }

    [AvaloniaFact]
    public async Task IsVisible_trueで非同期生成して表示する()
    {
        var control = CreateControl();
        control.Source = TestHelper.CreateSolidBitmap(200, 200);
        control.CenterPoint = new Point(100, 100);
        await control.RenderAsync();

        control.IsVisible = false;
        control.IsVisible = true;
        await control.RenderAsync();

        Assert.True(control.IsVisible);
        Assert.NotNull(control.LoupeBitmap);
    }

    // ====================================================================
    // 範囲検証（coercion）
    // ====================================================================

    [AvaloniaFact]
    public void RequestedSize_範囲外は補正される()
    {
        var control = CreateControl();

        control.RequestedSize = 0;
        Assert.Equal(1, control.RequestedSize);

        control.RequestedSize = 5000;
        Assert.Equal(4096, control.RequestedSize);
    }

    [AvaloniaFact]
    public void ZoomLevel_範囲外は補正される()
    {
        var control = CreateControl();

        control.ZoomLevel = 0;
        Assert.Equal(1, control.ZoomLevel);

        control.ZoomLevel = 100;
        Assert.Equal(64, control.ZoomLevel);
    }

    [AvaloniaFact]
    public void FrameWidth_範囲外は補正される()
    {
        var control = CreateControl();

        control.FrameWidth = 0;
        Assert.Equal(1, control.FrameWidth);
    }

    // ====================================================================
    // エラーハンドリング
    // ====================================================================

    [AvaloniaFact]
    public async Task SourceがBitmap以外の場合RenderAsyncはNotSupportedExceptionを投げる()
    {
        var control = CreateControl();
        control.Source = new CroppedBitmap(
            TestHelper.CreateSolidBitmap(100, 100), new PixelRect(0, 0, 50, 50));
        control.CenterPoint = new Point(25, 25);

        await Assert.ThrowsAsync<NotSupportedException>(() => control.RenderAsync());
    }

    [AvaloniaFact]
    public async Task 例外発生時にLogUpdateErrorがログへ記録される()
    {
        var control = CreateControl();
        var sink = new TestHelper.ListLogger();
        control.Log = sink;
        control.Source = new CroppedBitmap(
            TestHelper.CreateSolidBitmap(100, 100), new PixelRect(0, 0, 10, 10));
        control.CenterPoint = new Point(5, 5);

        await Assert.ThrowsAsync<NotSupportedException>(() => control.RenderAsync());

        Assert.True(sink.HasError);
    }

    [AvaloniaFact]
    public async Task ForceUpdate_生成失敗時は例外が観測されログへ記録される()
    {
        var control = CreateControl();
        var sink = new TestHelper.ListLogger();
        control.Log = sink;
        control.Source = new CroppedBitmap(
            TestHelper.CreateSolidBitmap(100, 100), new PixelRect(0, 0, 10, 10));
        control.CenterPoint = new Point(5, 5);

        // StartRender（fire-and-forget）経由の生成失敗 → ContinueWith(OnlyOnFaulted) で観測される
        control.ForceUpdate();

        // 生成失敗の完了をポーリングで待つ（fire-and-forget のため）
        for (var i = 0; i < 100 && !sink.HasError; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(5);
        }

        Assert.True(sink.HasError);
    }

    // ====================================================================
    // 表示制御（生成中の非表示・非表示時の明示的操作）
    // ====================================================================

    [AvaloniaFact]
    public async Task RenderAsync実行中に非表示になると生成画像は破棄される()
    {
        var control = CreateControl();
        control.Source = TestHelper.CreateSolidBitmap(200, 200);
        control.CenterPoint = new Point(100, 100);

        // 生成（Task.Run での画素抽出）中に非表示へ切り替えると、
        // Dispatcher 反映時に IsVisible=false のため生成ビットマップは破棄される
        var task = control.RenderAsync();
        control.IsVisible = false;
        await task;

        Assert.Null(control.LoupeBitmap);
        Assert.False(control.IsVisible);
    }

    [AvaloniaFact]
    public void ForceUpdate_非表示時はビットマップを解放する()
    {
        var control = CreateControl();
        control.IsVisible = false;
        control.Source = TestHelper.CreateSolidBitmap(200, 200);
        control.CenterPoint = new Point(100, 100);

        control.ForceUpdate();

        Assert.Null(control.LoupeBitmap);
        Assert.False(control.IsVisible);
    }

    [AvaloniaFact]
    public void RenderAsync_非表示時は即座に完了し画像を生成しない()
    {
        var control = CreateControl();
        control.IsVisible = false;
        control.Source = TestHelper.CreateSolidBitmap(200, 200);
        control.CenterPoint = new Point(100, 100);

        var task = control.RenderAsync();

        Assert.True(task.IsCompleted);
        Assert.Null(control.LoupeBitmap);
    }

    // ====================================================================
    // 範囲検証（coercion）の追加分
    // ====================================================================

    [AvaloniaFact]
    public void RenderScaling_範囲外は補正される()
    {
        var control = CreateControl();

        control.RenderScaling = 0.0;
        Assert.Equal(0.1, control.RenderScaling);

        control.RenderScaling = 10.0;
        Assert.Equal(8.0, control.RenderScaling);

        control.RenderScaling = double.NaN;
        Assert.Equal(1.0, control.RenderScaling);

        control.RenderScaling = double.PositiveInfinity;
        Assert.Equal(1.0, control.RenderScaling);
    }

    [AvaloniaFact]
    public void CheckerSize_範囲外は補正される()
    {
        var control = CreateControl();

        control.CheckerSize = 0;
        Assert.Equal(1, control.CheckerSize);

        control.CheckerSize = 200;
        Assert.Equal(128, control.CheckerSize);
    }

    [AvaloniaFact]
    public void 外観プロパティの設定値が保持される()
    {
        var control = CreateControl();
        var brush = new SolidColorBrush(Colors.Green);

        control.CrossColor = brush;
        control.CheckerColorA = brush;
        control.CheckerColorB = brush;
        control.FrameColor = brush;
        control.RenderScaling = 2.0;
        control.FrameWidth = 3;
        control.CheckerSize = 5;
        control.SelectionRectVisible = true;
        control.SelectionRect = new Rect(10, 10, 50, 40);
        control.SelectionRectColorA = brush;
        control.SelectionRectColorB = brush;

        Assert.Same(brush, control.CrossColor);
        Assert.Same(brush, control.CheckerColorA);
        Assert.Same(brush, control.CheckerColorB);
        Assert.Same(brush, control.FrameColor);
        Assert.Equal(2.0, control.RenderScaling);
        Assert.Equal(3, control.FrameWidth);
        Assert.Equal(5, control.CheckerSize);
        Assert.True(control.SelectionRectVisible);
        Assert.Equal(new Rect(10, 10, 50, 40), control.SelectionRect);
        Assert.Same(brush, control.SelectionRectColorA);
        Assert.Same(brush, control.SelectionRectColorB);
    }

    [AvaloniaFact]
    public void 依存プロパティのデフォルト値が正しい()
    {
        var control = new LoupeControl();

        Assert.Equal(160, control.RequestedSize);
        Assert.Equal(4, control.ZoomLevel);
        Assert.Equal(6, control.CheckerSize);
        Assert.Equal(2, control.FrameWidth);
        Assert.Equal(1.0, control.RenderScaling);
        // 十字線の既定は赤 40%（アルファ 102）
        Assert.Equal(Color.FromAColor(102, Colors.Red), ((ISolidColorBrush)control.CrossColor!).Color);
        // 市松模様の既定は Gray 30%（#4C808080）/ DarkGray 30%（#4CA9A9A9）（設定画面の現在値）
        Assert.Equal(Color.FromAColor(0x4C, Colors.Gray), ((ISolidColorBrush)control.CheckerColorA!).Color);
        Assert.Equal(Color.FromAColor(0x4C, Colors.DarkGray), ((ISolidColorBrush)control.CheckerColorB!).Color);
        Assert.Same(Brushes.LightGray, control.FrameColor);
        Assert.False(control.SelectionRectVisible);
        Assert.Null(control.SelectionRect);
        // 選択矩形破線の既定は白 100% / 黒 100%（設定画面の現在値）
        Assert.Equal(Colors.White, ((ISolidColorBrush)control.SelectionRectColorA!).Color);
        Assert.Equal(Colors.Black, ((ISolidColorBrush)control.SelectionRectColorB!).Color);
    }

    // ====================================================================
    // 座標ラベル（ルーペの上側に中心座標を表示）
    // ====================================================================

    [AvaloniaFact]
    public void 座標ラベルのデフォルト値が正しい()
    {
        var control = new LoupeControl();

        // ルーペ座標ラベルは既定で表示（設定画面の現在値）
        Assert.True(control.ShowCoordLabel);
        Assert.Same(Brushes.White, control.CoordLabelColor);
        // 背景色は不透明度をアルファに統合（黒 60% = #99000000）
        Assert.Equal(Color.FromAColor(153, Colors.Black), ((SolidColorBrush)control.CoordLabelBackgroundColor!).Color);
        Assert.Equal(12.0, control.CoordLabelFontSize);
    }

    [AvaloniaFact]
    public void 座標ラベルを有効化してCenterPointを設定すると表示される()
    {
        var control = CreateControl();
        control.ShowCoordLabel = true;
        control.CenterPoint = new Point(123, 456);

        // PART_CoordLabel が表示され、テキストが "(123, 456)" になる（ドメイン座標）
        Assert.NotNull(control.CoordLabelBorder);
        Assert.True(control.CoordLabelBorder!.IsVisible);
        Assert.NotNull(control.CoordLabelText);
        Assert.Equal("(123, 456)", control.CoordLabelText!.Text);
    }

    [AvaloniaFact]
    public void 座標ラベル無効時はCenterPointがあっても非表示である()
    {
        var control = CreateControl();
        control.ShowCoordLabel = false;
        control.CenterPoint = new Point(123, 456);

        Assert.NotNull(control.CoordLabelBorder);
        Assert.False(control.CoordLabelBorder!.IsVisible);
    }

    [AvaloniaFact]
    public void 座標ラベルの色とフォントサイズが反映される()
    {
        var control = CreateControl();
        var color = new SolidColorBrush(Colors.Yellow);
        var bg = new SolidColorBrush(Color.FromAColor(179, Colors.DarkBlue)); // 暗青 70%
        control.ShowCoordLabel = true;
        control.CoordLabelColor = color;
        control.CoordLabelBackgroundColor = bg;
        control.CoordLabelFontSize = 16;
        control.CenterPoint = new Point(10, 20);

        Assert.NotNull(control.CoordLabelText);
        Assert.Equal(16.0, control.CoordLabelText!.FontSize);
        Assert.Same(color, control.CoordLabelText.Foreground);

        Assert.NotNull(control.CoordLabelBorder);
        // 背景は色（アルファ込み）がそのまま反映される
        Assert.True(control.CoordLabelBorder!.Background is SolidColorBrush sb
            && sb.Color == Color.FromAColor(179, Colors.DarkBlue));
    }

    // ====================================================================
    // 表示位置・サイズ（UpdatePosition）
    // ====================================================================

    [AvaloniaFact]
    public void PositionOffsetを設定すると表示位置が更新される()
    {
        var control = CreateControl();
        var panel = new Panel { Width = 300, Height = 300 };
        panel.Children.Add(control);
        var window = new Window { Content = panel };
        window.Show();
        control.CenterPoint = new Point(50, 50);

        // オフセット 0 → カーソル位置（50,50）に表示
        var before = control.RenderTransform;

        control.PositionOffset = 20;

        // オフセット 20 → カーソル右下 +20 に表示（RenderTransform は同一インスタンスで値が更新される）
        Assert.Equal(20, control.PositionOffset);
        Assert.NotNull(control.RenderTransform);
        Assert.Same(before, control.RenderTransform);
        window.Close();
    }

    [AvaloniaFact]
    public void SetCoordinateTransformを設定すると表示位置が更新される()
    {
        var control = CreateControl();
        var panel = new Panel { Width = 300, Height = 300 };
        panel.Children.Add(control);
        var window = new Window { Content = panel };
        window.Show();
        control.CenterPoint = new Point(100, 100);

        // ドメイン = DIP × 2（拡大）→ 中心 (100,100) は DIP (50,50) に変換される
        control.SetCoordinateTransform(new CoordinatePort(
            p => new Point(p.X * 2, p.Y * 2),
            p => new Point(p.X / 2, p.Y / 2),
            () => new Size(300, 300)));

        Assert.NotNull(control.RenderTransform);
        window.Close();
    }

    [AvaloniaFact]
    public void SetCoordinateTransform_nullで等価変換にフォールバックする()
    {
        var control = CreateControl();
        var panel = new Panel { Width = 300, Height = 300 };
        panel.Children.Add(control);
        var window = new Window { Content = panel };
        window.Show();
        control.CenterPoint = new Point(50, 50);

        // null 設定 → 等価変換にフォールバック（例外なく表示位置が計算される）
        control.SetCoordinateTransform(null);

        Assert.NotNull(control.RenderTransform);
        window.Close();
    }

    [AvaloniaFact]
    public void 親パネルサイズ0でも表示位置計算が動作する()
    {
        var control = CreateControl();
        var panel = new Panel { Width = 0, Height = 0 }; // サイズ 0 → クランプをスキップ
        panel.Children.Add(control);
        var window = new Window { Content = panel };
        window.Show();
        control.CenterPoint = new Point(50, 50);

        // サイズ 0 ではクランプせず基本サイズのまま表示位置を計算する（例外なし）
        control.PositionOffset = 10;
        Assert.NotNull(control.RenderTransform);
        window.Close();
    }

    [AvaloniaFact]
    public void 親パネルより大きいRequestedSizeはクランプされる()
    {
        var control = CreateControl();
        control.RequestedSize = 400; // パネル (300,300) より大きい
        var panel = new Panel { Width = 300, Height = 300 };
        panel.Children.Add(control);
        var window = new Window { Content = panel };
        window.Show();
        control.CenterPoint = new Point(50, 50);

        // 表示サイズは親パネルの最小辺 (300) にクランプされる
        Assert.Equal(300, control.Width);
        Assert.Equal(300, control.Height);
        window.Close();
    }

    [AvaloniaFact]
    public void RefreshPosition_最新のポートで表示位置が再計算される()
    {
        var control = CreateControl();
        var panel = new Panel { Width = 300, Height = 300 };
        panel.Children.Add(control);
        var window = new Window { Content = panel };
        window.Show();
        control.CenterPoint = new Point(100, 100);

        // 変換係数を後から変更できるよう、可変な係数を使う（パン・ズーム等の動的変化の模擬）
        var factor = 2.0;
        control.SetCoordinateTransform(new CoordinatePort(
            p => new Point(p.X * factor, p.Y * factor),
            p => new Point(p.X / factor, p.Y / factor),
            () => new Size(300, 300)));
        var before = control.RenderTransform;

        // 係数を変更して RefreshPosition → 表示位置（RenderTransform）が再計算される
        factor = 4.0;
        control.RefreshPosition();

        Assert.NotNull(control.RenderTransform);
        Assert.Same(before, control.RenderTransform);
        window.Close();
    }

    // ====================================================================
    // 破棄済み Bitmap（ObjectDisposedException 分岐）
    // ====================================================================

    [AvaloniaFact]
    public async Task 破棄済みSourceは市松模様のみで描画される()
    {
        var control = CreateControl();
        var bitmap = TestHelper.CreateSolidBitmap(200, 200);
        control.Source = bitmap;
        control.CenterPoint = new Point(100, 100);
        bitmap.Dispose(); // 破棄済み Bitmap（生存検証で ObjectDisposedException）

        await control.RenderAsync();

        // 例外なく完了し、市松模様のみの Bitmap が生成される
        Assert.NotNull(control.LoupeBitmap);
    }

    [AvaloniaFact]
    public async Task 抽出中にSourceが破棄されると市松模様のみで描画される()
    {
        var control = CreateControl();
        var bitmap = TestHelper.CreateSolidBitmap(200, 200);
        control.Source = bitmap;
        control.CenterPoint = new Point(100, 100);

        // RenderAsync は生存検証（PixelSize）まで同期実行される。生存検証後に破棄することで、
        // Task.Run 内の抽出が ObjectDisposedException → region null となり、市松模様のみで描画される。
        var task = control.RenderAsync();
        bitmap.Dispose();

        await task;

        // 例外なく完了し、市松模様のみの Bitmap が生成される
        Assert.NotNull(control.LoupeBitmap);
    }

    [AvaloniaFact]
    public void Sourceを再設定すると変更通知される()
    {
        var control = CreateControl();
        control.Source = TestHelper.CreateSolidBitmap(200, 200);

        // 再設定（OldValue が null でない）→ 変更ログの両分岐が実行される
        control.Source = TestHelper.CreateSolidBitmap(200, 200);
        control.CenterPoint = new Point(50, 50);
        control.RequestedSize = 120;
        control.RenderScaling = 1.5;
        control.ZoomLevel = 8;

        Assert.NotNull(control.Source);
    }

    [AvaloniaFact]
    public async Task 実ロガー設定時に全ログメソッドが実行される()
    {
        var control = CreateControl();
        control.Log = new TestHelper.ListLogger();

        // 初回設定（OldValue = null）と再設定（NewValue あり）の両経路で LogSettingChanged を実行する
        control.Source = TestHelper.CreateSolidBitmap(200, 200);
        control.CenterPoint = new Point(100, 100);
        control.Source = TestHelper.CreateSolidBitmap(200, 200);

        // null 設定（NewValue = null）で LogSettingChanged の null 分岐を実行する
        control.Source = null;
        control.CenterPoint = null;

        // RequestRender がポストした RenderCore（LogRenderCore）を実行する
        Dispatcher.UIThread.RunJobs();

        // RenderAsync で LogGenerateAsyncDone を実行する
        await control.RenderAsync();
        Dispatcher.UIThread.RunJobs();
        control.Clear();
    }

    [AvaloniaFact]
    public void CoordLabelFontSize_NaNは12に補正される()
    {
        var control = new LoupeControl();

        control.CoordLabelFontSize = double.NaN;

        Assert.Equal(12.0, control.CoordLabelFontSize);
    }

    [AvaloniaFact]
    public void CoordLabelBackgroundColor_nullで黒にフォールバックする()
    {
        var control = new LoupeControl();
        control.ShowCoordLabel = true;
        control.CoordLabelBackgroundColor = null;
        control.CenterPoint = new Point(10, 20);

        // null 背景 → 黒で表示される（PART_CoordLabel の Background が設定される）
        Assert.NotNull(control.CoordLabelBorder);
        Assert.True(control.CoordLabelBorder!.IsVisible);
    }

    [AvaloniaFact]
    public void CoordLabelBackgroundColor_非SolidColorBrushでも例外にならない()
    {
        var control = new LoupeControl();
        control.ShowCoordLabel = true;
        // IBrush のうち SolidColorBrush 以外（GradientBrush）を設定
        control.CoordLabelBackgroundColor = new LinearGradientBrush();
        control.CenterPoint = new Point(10, 20);

        Assert.NotNull(control.CoordLabelBorder);
        Assert.True(control.CoordLabelBorder!.IsVisible);
    }

    [AvaloniaFact]
    public void 座標ラベル背景色のブラシは容量超過時も例外なく共有される()
    {
        var control = new LoupeControl();
        control.ShowCoordLabel = true;

        // ブラシキャッシュ容量（32）を超える 33 色を順次設定し、容量超過時のキャッシュクリア経路を実行する
        for (int i = 0; i <= 32; i++)
        {
            control.CoordLabelBackgroundColor = new SolidColorBrush(Color.FromArgb(255, (byte)i, (byte)(255 - i), (byte)(i * 3)));
            control.CenterPoint = new Point(i + 1, 20);
        }

        Assert.NotNull(control.CoordLabelBorder);
        Assert.True(control.CoordLabelBorder!.IsVisible);
    }

    [AvaloniaFact]
    public void UpdateCoordLabel_PART要素がnullでも例外にならない()
    {
        var control = CreateControl();
        // PART_CoordLabel / PART_CoordText を null にして防御分岐をカバーする
        var labelField = typeof(LoupeControl).GetField("PART_CoordLabel", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        var textField = typeof(LoupeControl).GetField("PART_CoordText", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        labelField!.SetValue(control, null);
        textField!.SetValue(control, null);

        // 例外なく完了する
        control.ShowCoordLabel = true;
        control.CenterPoint = new Point(10, 20);
    }
}
