using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Estable.AvaloniaUI.FadeMask.Tests;

/// <summary>
/// <see cref="FadeMaskDrawer"/> の単体テスト（DrawingContext 描画の例外なし検証）。
/// </summary>
[Collection("AvaloniaHeadless")]
public class FadeMaskDrawerTests
{
    /// <summary>描画コンテキストを生成する。</summary>
    private static DrawingContext CreateContext()
    {

        var rtb = new RenderTargetBitmap(new PixelSize(300, 300), new Vector(96, 96));
        return rtb.CreateDrawingContext();
    }

    [AvaloniaFact]
    public void DrawOverlay_選択矩形ありで穴あけマスクを描画する()
    {
        using var ctx = CreateContext();

        FadeMaskDrawer.DrawOverlay(
            ctx,
            new Rect(0, 0, 300, 300),
            new Rect(50, 50, 100, 80),
            Color.FromArgb(150, 0, 0, 0),
            Colors.White, 14.0,
            showSizeLabel: false);
    }

    [AvaloniaFact]
    public void DrawOverlay_サイズラベル自動生成で描画する()
    {
        using var ctx = CreateContext();

        FadeMaskDrawer.DrawOverlay(
            ctx,
            new Rect(0, 0, 300, 300),
            new Rect(50, 50, 100, 80),
            Color.FromArgb(150, 0, 0, 0),
            Colors.White, 14.0,
            showSizeLabel: true,
            scaling: 1.25);
    }

    [AvaloniaFact]
    public void DrawOverlay_サイズラベルオーバーライドで描画する()
    {
        using var ctx = CreateContext();

        FadeMaskDrawer.DrawOverlay(
            ctx,
            new Rect(0, 0, 300, 300),
            new Rect(50, 50, 100, 80),
            Color.FromArgb(150, 0, 0, 0),
            Colors.White, 14.0,
            showSizeLabel: true,
            sizeLabelOverride: "Custom Label");
    }

    [AvaloniaFact]
    public void DrawOverlay_選択矩形なしで全面暗転する()
    {
        using var ctx = CreateContext();

        FadeMaskDrawer.DrawOverlay(
            ctx,
            new Rect(0, 0, 300, 300),
            null,
            Color.FromArgb(150, 0, 0, 0),
            Colors.White, 14.0,
            showSizeLabel: false);
    }

    [AvaloniaFact]
    public void DrawOverlay_画面端に接する選択矩形で描画する()
    {
        using var ctx = CreateContext();

        // 選択矩形が clientRect 端に接するケース（4辺のうち一部が 0）
        FadeMaskDrawer.DrawOverlay(
            ctx,
            new Rect(0, 0, 300, 300),
            new Rect(0, 0, 300, 300),
            Color.FromArgb(150, 0, 0, 0),
            Colors.White, 14.0,
            showSizeLabel: true);
    }

    [AvaloniaFact]
    public void DrawOverlay_スケーリング0でも例外なく描画する()
    {
        using var ctx = CreateContext();

        FadeMaskDrawer.DrawOverlay(
            ctx,
            new Rect(0, 0, 300, 300),
            new Rect(50, 50, 100, 80),
            Color.FromArgb(150, 0, 0, 0),
            Colors.White, 14.0,
            showSizeLabel: true,
            scaling: 0);
    }

    [AvaloniaFact]
    public void DrawOverlay_サブラインありで2行目以降に描画する()
    {
        using var ctx = CreateContext();

        FadeMaskDrawer.DrawOverlay(
            ctx,
            new Rect(0, 0, 300, 300),
            new Rect(50, 50, 100, 80),
            Color.FromArgb(150, 0, 0, 0),
            Colors.White, 14.0,
            showSizeLabel: true,
            subLines:
            [
                new FadeMaskDrawer.SubLine("Enter / クリック: 確定  Esc: キャンセル", 12.0, Colors.White),
                new FadeMaskDrawer.SubLine("Test Window", 11.0, Colors.LightGray),
            ]);
    }

    [AvaloniaFact]
    public void DrawOverlay_サイズラベル非表示時にサブラインを繰り上げて描画する()
    {
        using var ctx = CreateContext();

        // サイズラベル非表示（showSizeLabel: false）でも、サブラインは 1 行目から例外なく描画される
        FadeMaskDrawer.DrawOverlay(
            ctx,
            new Rect(0, 0, 300, 300),
            new Rect(50, 50, 100, 80),
            Color.FromArgb(150, 0, 0, 0),
            Colors.White, 14.0,
            showSizeLabel: false,
            subLines:
            [
                new FadeMaskDrawer.SubLine("ダブルクリック: 確定  Esc / 矩形外クリック: キャンセル", 12.0, Colors.White),
            ]);
    }

    [AvaloniaFact]
    public void DrawOverlay_サブライン空リストでサイズラベルだけ描画する()
    {
        using var ctx = CreateContext();

        FadeMaskDrawer.DrawOverlay(
            ctx,
            new Rect(0, 0, 300, 300),
            new Rect(50, 50, 100, 80),
            Color.FromArgb(150, 0, 0, 0),
            Colors.White, 14.0,
            showSizeLabel: true,
            subLines: []);
    }

    [AvaloniaFact]
    public void DrawOverlay_背景色指定で1行目とサブラインの背景を描画する()
    {
        using var ctx = CreateContext();

        // 1 行目（サイズラベル）の背景色と、サブライン各行の背景色（不透明度はアルファに統合）を個別指定
        FadeMaskDrawer.DrawOverlay(
            ctx,
            new Rect(0, 0, 300, 300),
            new Rect(50, 50, 100, 80),
            Color.FromArgb(150, 0, 0, 0),
            Colors.White, 14.0,
            showSizeLabel: true,
            subLines:
            [
                new FadeMaskDrawer.SubLine("Enter / クリック: 確定  Esc: キャンセル", 12.0, Colors.White, Color.FromArgb(179, 0, 0, 139)),
                new FadeMaskDrawer.SubLine("Test Window", 11.0, Colors.LightGray, Color.FromArgb(77, 139, 0, 0)),
            ],
            textBgColor: Color.FromArgb(128, 0, 0, 0));
    }

    [AvaloniaFact]
    public void DrawOverlay_サブライン背景色省略時は黒背景になる()
    {
        using var ctx = CreateContext();

        // BackgroundColor 省略（デフォルト = 黒 50%）でも例外なく描画される
        FadeMaskDrawer.DrawOverlay(
            ctx,
            new Rect(0, 0, 300, 300),
            new Rect(50, 50, 100, 80),
            Color.FromArgb(150, 0, 0, 0),
            Colors.White, 14.0,
            showSizeLabel: true,
            subLines:
            [
                new FadeMaskDrawer.SubLine("操作ラベル", 12.0, Colors.White),
            ]);
    }
}

