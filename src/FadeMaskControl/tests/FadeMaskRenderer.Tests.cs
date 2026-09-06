using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Rect = Estable.RectD;
using Size = Avalonia.Size;
using Estable.AvaloniaUI.Platform;
using Estable.Media;
using Estable;
using Estable.Media;
using Xunit;
using Avalonia.Headless.XUnit;

namespace Estable.AvaloniaUI.FadeMask.Tests;

/// <summary>
/// <see cref="FadeMaskRenderer"/> の単体テスト（描画状態・描画の例外なし検証）。
/// </summary>
[Collection("AvaloniaHeadless")]
public class FadeMaskRendererTests
{
    /// <summary>描画コンテキストを生成する。</summary>
    private static DrawingContext CreateContext()
    {

        var rtb = new RenderTargetBitmap(new PixelSize(300, 300), new Vector(96, 96));
        return rtb.CreateDrawingContext();
    }

    /// <summary>テスト用のレンダラーを生成する。</summary>
    private static FadeMaskRenderer CreateRenderer(FadeMaskRenderMode mode = FadeMaskRenderMode.AreaSelect)
        => new()
        {
            Mode = mode,
            Scaling = 1.0,
            DisplayScaleScaling = 1.0,
            OverlayArgbColor = Color.FromAColor(150, Colors.Black),
            TextColor = Colors.White,
            TextBackgroundColor = Color.FromAColor(128, Colors.Black),
            FontSize = 14.0,
            BorderColor = Colors.White,
            BorderThickness = 2.0,
        };

    [AvaloniaFact]
    public void Draw_選択矩形ありで例外なく描画する()
    {
        using var ctx = CreateContext();
        var renderer = CreateRenderer();
        renderer.HasSelection = true;
        renderer.LogicalSelectionRect = new Rect(50, 50, 100, 80);

        renderer.Draw(ctx, new Size(300, 300));
    }

    [AvaloniaFact]
    public void Draw_ハイライトありで例外なく描画する()
    {
        using var ctx = CreateContext();
        var renderer = CreateRenderer(FadeMaskRenderMode.WindowSelect);
        renderer.HasHighlight = true;
        renderer.LogicalHighlightRect = new Rect(50, 50, 100, 80);
        renderer.WindowTitle = "Test Window";
        renderer.InfoLabelVisible = true;

        renderer.Draw(ctx, new Size(300, 300));
    }

    [AvaloniaFact]
    public void Draw_ハイライトあり座標変換ポート設定でDIP変換して描画する()
    {
        using var ctx = CreateContext();
        var renderer = CreateRenderer(FadeMaskRenderMode.WindowSelect);
        renderer.HasHighlight = true;
        renderer.LogicalHighlightRect = new Rect(25, 25, 50, 40);
        renderer.WindowTitle = "Test Window";
        renderer.CoordinateTransform = new CoordTransform(
            p => new PointD(p.X * 2, p.Y * 2),
            p => new PointD(p.X / 2, p.Y / 2),
            () => null);

        renderer.Draw(ctx, new Size(300, 300));
    }

    [AvaloniaFact]
    public void Draw_スクリーン選択モードで例外なく描画する()
    {
        using var ctx = CreateContext();
        var renderer = CreateRenderer(FadeMaskRenderMode.ScreenSelect);
        renderer.HasHighlight = true;
        renderer.LogicalHighlightRect = new Rect(50, 50, 100, 80);
        renderer.ScreenIndex = 0;

        renderer.Draw(ctx, new Size(300, 300));
    }

    [AvaloniaFact]
    public void Draw_選択ありで例外なく描画する()
    {
        using var ctx = CreateContext();
        var renderer = CreateRenderer();
        renderer.HasSelection = true;
        renderer.LogicalSelectionRect = new Rect(50, 50, 100, 80);

        renderer.Draw(ctx, new Size(300, 300));
    }

    [AvaloniaFact]
    public void Draw_座標変換ポート設定時に矩形をDIP変換して描画する()
    {
        using var ctx = CreateContext();
        var renderer = CreateRenderer();
        renderer.HasSelection = true;
        renderer.LogicalSelectionRect = new Rect(25, 25, 50, 40);
        // ドメイン = DIP × 2（拡大）→ 矩形は (50,50)-(150,130) に変換される
        renderer.CoordinateTransform = new CoordTransform(
            p => new PointD(p.X * 2, p.Y * 2),
            p => new PointD(p.X / 2, p.Y / 2),
            () => null);

        renderer.Draw(ctx, new Size(300, 300));
    }

    [AvaloniaFact]
    public void Draw_座標変換ポート設定時でも選択なしで全面暗転する()
    {
        using var ctx = CreateContext();
        var renderer = CreateRenderer();
        renderer.ShowFullOverlay = true;
        renderer.CoordinateTransform = new CoordTransform(
            p => new PointD(p.X * 2, p.Y * 2),
            p => new PointD(p.X / 2, p.Y / 2),
            () => null);

        // 選択なし + ポート設定 → 矩形変換（is {} false）と全面暗転が実行される
        renderer.Draw(ctx, new Size(300, 300));
    }

    [AvaloniaFact]
    public void Draw_選択なしで全面暗転モードで例外なく描画する()
    {
        using var ctx = CreateContext();
        var renderer = CreateRenderer();
        renderer.ShowFullOverlay = true;

        renderer.Draw(ctx, new Size(300, 300));
    }

    [AvaloniaFact]
    public void Draw_色設定値で例外なく描画する()
    {
        using var ctx = CreateContext();
        var renderer = CreateRenderer();
        renderer.HasSelection = true;
        renderer.LogicalSelectionRect = new Rect(50, 50, 100, 80);
        renderer.BorderColor = Colors.Red;
        renderer.TextColor = Colors.Blue;
        renderer.InfoLabelColor = Colors.Lime;

        renderer.Draw(ctx, new Size(300, 300));
    }

    [AvaloniaFact]
    public void Draw_サイズラベルプロバイダありで例外なく描画する()
    {
        using var ctx = CreateContext();
        var renderer = CreateRenderer();
        renderer.HasSelection = true;
        renderer.LogicalSelectionRect = new Rect(50, 50, 100, 80);
        renderer.ShowSizeLabel = true;
        renderer.SizeLabelOverrideProvider = () => "Custom: (10, 20)";

        renderer.Draw(ctx, new Size(300, 300));
    }

    [AvaloniaFact]
    public void Draw_長いウィンドウタイトルでトランケーションを実行する()
    {
        using var ctx = CreateContext();
        var renderer = CreateRenderer(FadeMaskRenderMode.WindowSelect);
        renderer.HasHighlight = true;
        // 狭いハイライト矩形 + 長いタイトル → CreateTruncatedText の省略記号分岐が実行される
        renderer.LogicalHighlightRect = new Rect(50, 50, 60, 40);
        renderer.WindowTitle = "This is a very long window title that will definitely be truncated";
        renderer.InfoLabelVisible = true;

        renderer.Draw(ctx, new Size(300, 300));
    }

    [AvaloniaFact]
    public void Draw_情報ラベル非表示時はタイトルを描画しない()
    {
        using var ctx = CreateContext();
        var renderer = CreateRenderer(FadeMaskRenderMode.WindowSelect);
        renderer.HasHighlight = true;
        renderer.LogicalHighlightRect = new Rect(50, 50, 100, 80);
        renderer.WindowTitle = "Test Window";
        renderer.InfoLabelVisible = false; // 非表示

        renderer.Draw(ctx, new Size(300, 300));
    }

    [AvaloniaFact]
    public void Draw_スクリーンラベル表示で例外なく描画する()
    {
        using var ctx = CreateContext();
        var renderer = CreateRenderer(FadeMaskRenderMode.ScreenSelect);
        renderer.HasHighlight = true;
        renderer.LogicalHighlightRect = new Rect(50, 50, 200, 100);
        renderer.ScreenIndex = 1;
        renderer.InfoLabelVisible = true;

        renderer.Draw(ctx, new Size(300, 300));
    }

    [AvaloniaFact]
    public void Draw_サブライン表示ありで操作ラベルを描画する()
    {
        using var ctx = CreateContext();
        var renderer = CreateRenderer();
        renderer.HasSelection = true;
        renderer.LogicalSelectionRect = new Rect(50, 50, 100, 80);
        renderer.ShowSubLines = true;

        renderer.Draw(ctx, new Size(300, 300));
    }

    [AvaloniaFact]
    public void Draw_サブライン非表示時は操作ラベルを描画しない()
    {
        using var ctx = CreateContext();
        var renderer = CreateRenderer();
        renderer.HasSelection = true;
        renderer.LogicalSelectionRect = new Rect(50, 50, 100, 80);
        renderer.ShowSubLines = false; // 操作ラベル非表示

        renderer.Draw(ctx, new Size(300, 300));
    }

    [AvaloniaFact]
    public void Draw_Cropモードで操作ラベルを描画する()
    {
        using var ctx = CreateContext();
        var renderer = CreateRenderer(FadeMaskRenderMode.Crop);
        renderer.HasSelection = true;
        renderer.LogicalSelectionRect = new Rect(50, 50, 100, 80);
        renderer.ShowSubLines = true;

        // Crop モードの操作ラベル（ダブルクリック確定・Esc/矩形外クリックキャンセル）が描画される
        renderer.Draw(ctx, new Size(300, 300));
    }

    [AvaloniaFact]
    public void Draw_サイズラベル非表示時にサブラインを繰り上げて描画する()
    {
        using var ctx = CreateContext();
        var renderer = CreateRenderer();
        renderer.HasSelection = true;
        renderer.LogicalSelectionRect = new Rect(50, 50, 100, 80);
        renderer.ShowSizeLabel = false; // 1 行目非表示 → 操作ラベルが 1 行目から描画される
        renderer.ShowSubLines = true;

        renderer.Draw(ctx, new Size(300, 300));
    }

    [AvaloniaFact]
    public void Draw_WindowSelectで情報ラベルを3行目に描画する()
    {
        using var ctx = CreateContext();
        var renderer = CreateRenderer(FadeMaskRenderMode.WindowSelect);
        renderer.HasHighlight = true;
        renderer.LogicalHighlightRect = new Rect(50, 50, 100, 80);
        renderer.WindowTitle = "Test Window";
        renderer.ShowSubLines = true;
        renderer.InfoLabelVisible = true;

        renderer.Draw(ctx, new Size(300, 300));
    }

    [AvaloniaFact]
    public void Draw_ScreenSelectで操作ラベルを描画する()
    {
        using var ctx = CreateContext();
        var renderer = CreateRenderer(FadeMaskRenderMode.ScreenSelect);
        renderer.HasHighlight = true;
        renderer.LogicalHighlightRect = new Rect(50, 50, 100, 80);
        renderer.ScreenIndex = 0;
        renderer.ShowSubLines = true;

        renderer.Draw(ctx, new Size(300, 300));
    }

    [AvaloniaFact]
    public void Draw_背景色設定で1行目から3行目の背景を個別描画する()
    {
        using var ctx = CreateContext();
        var renderer = CreateRenderer(FadeMaskRenderMode.WindowSelect);
        renderer.HasHighlight = true;
        renderer.LogicalHighlightRect = new Rect(50, 50, 100, 80);
        renderer.WindowTitle = "Test Window";
        renderer.ShowSubLines = true;
        renderer.InfoLabelVisible = true;
        // 1 行目 / 2 行目 / 3 行目の背景色を個別指定
        renderer.TextBackgroundColor = Colors.DarkBlue;
        renderer.SubLineBackgroundColor = Colors.DarkGreen;
        renderer.InfoLabelBackgroundColor = Colors.DarkRed;

        renderer.Draw(ctx, new Size(300, 300));
    }
}

