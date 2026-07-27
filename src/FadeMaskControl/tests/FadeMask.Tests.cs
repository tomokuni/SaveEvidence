using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Xunit;

namespace Estable.AvaloniaUI.FadeMask.Tests;

/// <summary><see cref="FadeMaskControl"/> の単体テスト（Render の外部レンダラー委譲）。</summary>
/// <remarks>
/// 対象: <c>FadeMaskControl.cs</c>（Render オーバーライドの ExternalRenderer 委譲・描画）。<br/>
/// ヘッドレス不要の公開 API 検証（Log / SetCoordinateTransform / LogicalSelectionRect / 既定値 / ExternalRenderer）は
/// <c>FadeMaskControlStateTests</c> が並列実行する。
/// </remarks>
[Collection("AvaloniaHeadless")]
public class FadeMaskTests
{
    [AvaloniaFact]
    public void Render_ExternalRenderer設定時に委譲して例外なく描画する()
    {
        var control = new FadeMaskControl();
        var renderer = new FadeMaskRenderer { HasSelection = true, LogicalSelectionRect = new Rect(10, 10, 50, 50) };
        control.ExternalRenderer = renderer;
        control.Measure(new Size(200, 200));
        control.Arrange(new Rect(0, 0, 200, 200));

        using var ctx = CreateContext();
        control.Render(ctx);
    }

    [AvaloniaFact]
    public void Render_外部レンダラー未設定でも例外なく描画する()
    {
        var control = new FadeMaskControl
        {
            LogicalSelectionRect = new Rect(10, 10, 50, 50),
            OverlayColor = Color.FromArgb(150, 0, 0, 0),
        };
        control.Measure(new Size(200, 200));
        control.Arrange(new Rect(0, 0, 200, 200));

        using var ctx = CreateContext();
        control.Render(ctx);
    }

    [AvaloniaFact]
    public void Render_選択なしでも例外なく描画する()
    {
        var control = new FadeMaskControl();
        control.Measure(new Size(200, 200));
        control.Arrange(new Rect(0, 0, 200, 200));

        using var ctx = CreateContext();
        control.Render(ctx);
    }

    [AvaloniaFact]
    public void Render_幅0の矩形でも例外なく描画する()
    {
        var control = new FadeMaskControl
        {
            LogicalSelectionRect = new Rect(10, 10, 0, 50), // 幅 0 → 選択なし扱い
        };
        control.Measure(new Size(200, 200));
        control.Arrange(new Rect(0, 0, 200, 200));

        using var ctx = CreateContext();
        control.Render(ctx);
    }

    /// <summary>テスト用の DrawingContext を生成する。</summary>
    private static DrawingContext CreateContext()
    {
        var rtb = new RenderTargetBitmap(new PixelSize(300, 300), new Vector(96, 96));
        return rtb.CreateDrawingContext();
    }
}
