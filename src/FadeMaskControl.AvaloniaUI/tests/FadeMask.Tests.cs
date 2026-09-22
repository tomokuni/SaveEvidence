using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using EsUtil.Platform.AvaloniaUI;
using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using Estable.FadeMask;
using Xunit;
using Rect = Avalonia.Rect;
using Size = Avalonia.Size;

namespace Estable.AvaloniaUI.FadeMask.Tests;

/// <summary><see cref="FadeMaskControl"/> の描画テスト（Render の例外なし検証）。</summary>
/// <remarks>
/// 対象: <c>FadeMaskControl.cs</c>（Render オーバーライドの描画経路）。<br/>
/// ヘッドレス不要の公開 API 検証（Log / Options / SetCoordinateTransform / 既定値）は
/// <c>FadeMaskControlStateTests</c> が並列実行する。
/// </remarks>
[Collection("AvaloniaHeadless")]
public class FadeMaskTests
{
    /// <summary>描画コンテキストを生成する。</summary>
    /// <returns>描画先のコンテキスト。破棄は呼び出し側。</returns>
    private static DrawingContext CreateContext()
    {
        var rtb = new RenderTargetBitmap(new PixelSize(300, 300), new Vector(96, 96));
        return rtb.CreateDrawingContext();
    }

    /// <summary>テスト用のコントロールを生成する（レイアウト済み）。</summary>
    /// <param name="mode">オーバーレイ描画モード。</param>
    /// <param name="appearance">外観。null の場合は枠線・ラベルが判別しやすいテスト用の外観。</param>
    /// <returns>レイアウト済みのコントロール。</returns>
    private static FadeMaskControl CreateControl(
        FadeMaskRenderMode mode = FadeMaskRenderMode.AreaSelect, FadeMaskAppearance? appearance = null)
    {
        var control = new FadeMaskControl
        {
            Options = new FadeMaskOptions { Mode = mode, Appearance = appearance ?? CreateTestAppearance() },
            Scaling = 1.0,
            DisplayScaleScaling = 1.0,
        };
        control.Measure(new Size(300, 300));
        control.Arrange(new Rect(0, 0, 300, 300));
        return control;
    }

    /// <summary>テスト用の外観を生成する（枠線・ラベルが判別しやすい値）。</summary>
    /// <returns>テスト用の外観。</returns>
    private static FadeMaskAppearance CreateTestAppearance() => new()
    {
        OverlayColor = Color.FromAColor(150, Colors.Black).ToDrawingColor(),
        SizeLabelColor = Colors.White.ToDrawingColor(),
        SizeLabelBackgroundColor = Color.FromAColor(128, Colors.Black).ToDrawingColor(),
        SizeLabelFontSize = 14.0,
        BorderColor = Colors.White.ToDrawingColor(),
        BorderThickness = 2.0,
    };

    /// <summary>選択矩形ありの状態にする。</summary>
    /// <param name="control">対象のコントロール。</param>
    /// <param name="rect">選択矩形（Avalonia の DIP 座標）。</param>
    private static void Select(FadeMaskControl control, Rect rect)
        => control.Options = control.Options with
        {
            HasSelection = true,
            LogicalSelectionRect = rect.ToEsRect(),
        };

    /// <summary>ハイライト矩形ありの状態にする。</summary>
    /// <param name="control">対象のコントロール。</param>
    /// <param name="rect">ハイライト矩形（Avalonia の DIP 座標）。</param>
    /// <param name="windowTitle">情報ラベルの文言。</param>
    private static void Highlight(FadeMaskControl control, Rect rect, string windowTitle = "")
        => control.Options = control.Options with
        {
            HasHighlight = true,
            LogicalHighlightRect = rect.ToEsRect(),
            WindowTitle = windowTitle,
        };

    /// <summary>全面暗転ありの状態にする。</summary>
    /// <param name="control">対象のコントロール。</param>
    private static void ShowFullOverlay(FadeMaskControl control)
        => control.Options = control.Options with { ShowFullOverlay = true };

    /// <summary>選択矩形ありで例外なく描画することを検証する。</summary>
    [AvaloniaFact]
    public void Render_選択矩形ありで例外なく描画する()
    {
        using var ctx = CreateContext();
        var control = CreateControl();
        Select(control, new Rect(50, 50, 100, 80));

        control.Render(ctx);
    }

    /// <summary>ハイライトあり（WindowSelect）で例外なく描画することを検証する。</summary>
    [AvaloniaFact]
    public void Render_ハイライトありで例外なく描画する()
    {
        using var ctx = CreateContext();
        var control = CreateControl(FadeMaskRenderMode.WindowSelect);
        Highlight(control, new Rect(50, 50, 100, 80), "Test Window");

        control.Render(ctx);
    }

    /// <summary>座標変換ポート設定時にハイライト矩形を DIP 変換して描画することを検証する。</summary>
    [AvaloniaFact]
    public void Render_ハイライトあり座標変換ポート設定でDIP変換して描画する()
    {
        using var ctx = CreateContext();
        var control = CreateControl(FadeMaskRenderMode.WindowSelect);
        Highlight(control, new Rect(25, 25, 50, 40), "Test Window");
        control.SetCoordinateTransform(CreateDoubleScalePort());

        control.Render(ctx);
    }

    /// <summary>スクリーン選択モードで例外なく描画することを検証する。</summary>
    [AvaloniaFact]
    public void Render_スクリーン選択モードで例外なく描画する()
    {
        using var ctx = CreateContext();
        var control = CreateControl(FadeMaskRenderMode.ScreenSelect);
        Highlight(control, new Rect(50, 50, 100, 80));

        control.Render(ctx);
    }

    /// <summary>座標変換ポート設定時に選択矩形を DIP 変換して描画することを検証する。</summary>
    [AvaloniaFact]
    public void Render_座標変換ポート設定時に矩形をDIP変換して描画する()
    {
        using var ctx = CreateContext();
        var control = CreateControl();
        Select(control, new Rect(25, 25, 50, 40));
        // ドメイン = DIP × 2（拡大）→ 矩形は (50,50)-(150,130) に変換される
        control.SetCoordinateTransform(CreateDoubleScalePort());

        control.Render(ctx);
    }

    /// <summary>座標変換ポート設定時でも選択なしなら全面暗転することを検証する。</summary>
    [AvaloniaFact]
    public void Render_座標変換ポート設定時でも選択なしで全面暗転する()
    {
        using var ctx = CreateContext();
        var control = CreateControl();
        ShowFullOverlay(control);
        control.SetCoordinateTransform(CreateDoubleScalePort());

        control.Render(ctx);
    }

    /// <summary>選択なし + 全面暗転モードで例外なく描画することを検証する。</summary>
    [AvaloniaFact]
    public void Render_選択なしで全面暗転モードで例外なく描画する()
    {
        using var ctx = CreateContext();
        var control = CreateControl();
        ShowFullOverlay(control);

        control.Render(ctx);
    }

    /// <summary>選択なし + 全面暗転なしでは何も描画しない（例外も出ない）ことを検証する。</summary>
    [AvaloniaFact]
    public void Render_選択なしで全面暗転なしなら描画しない()
    {
        using var ctx = CreateContext();
        var control = CreateControl();

        control.Render(ctx);
    }

    /// <summary>外観の色設定値で例外なく描画することを検証する。</summary>
    [AvaloniaFact]
    public void Render_外観の色設定値で例外なく描画する()
    {
        using var ctx = CreateContext();
        var control = CreateControl(appearance: new FadeMaskAppearance
        {
            OverlayColor = Color.FromAColor(150, Colors.Black).ToDrawingColor(),
            BorderColor = Colors.Red.ToDrawingColor(),
            SizeLabelColor = Colors.Blue.ToDrawingColor(),
            InfoLabelColor = Colors.Lime.ToDrawingColor(),
        });
        Select(control, new Rect(50, 50, 100, 80));

        control.Render(ctx);
    }

    /// <summary>サイズラベルのプロバイダありで例外なく描画することを検証する。</summary>
    [AvaloniaFact]
    public void Render_サイズラベルプロバイダありで例外なく描画する()
    {
        using var ctx = CreateContext();
        var control = CreateControl();
        Select(control, new Rect(50, 50, 100, 80));
        control.SizeLabelOverrideProvider = () => "Custom: (10, 20)";

        control.Render(ctx);
    }

    /// <summary>サイズラベルのプロバイダが null を返す場合は自動生成へフォールバックすることを検証する。</summary>
    [AvaloniaFact]
    public void Render_サイズラベルプロバイダがnullなら自動生成する()
    {
        using var ctx = CreateContext();
        var control = CreateControl();
        Select(control, new Rect(50, 50, 100, 80));
        control.SizeLabelOverrideProvider = () => null;

        control.Render(ctx);
    }

    /// <summary>サイズラベル非表示の場合はプロバイダを評価しないことを検証する。</summary>
    [AvaloniaFact]
    public void Render_サイズラベル非表示ならプロバイダを評価しない()
    {
        using var ctx = CreateContext();
        var control = CreateControl(appearance: new FadeMaskAppearance { ShowSizeLabel = false, ShowSubLines = false });
        Select(control, new Rect(50, 50, 100, 80));
        var invoked = false;
        control.SizeLabelOverrideProvider = () => { invoked = true; return "x"; };

        control.Render(ctx);

        Assert.False(invoked);
    }

    /// <summary>長いウィンドウタイトルでも例外なく描画することを検証する。</summary>
    [AvaloniaFact]
    public void Render_長いウィンドウタイトルでも例外なく描画する()
    {
        using var ctx = CreateContext();
        var control = CreateControl(FadeMaskRenderMode.WindowSelect);
        // 狭いハイライト矩形 + 長いタイトル（矩形の幅を超える文言の描画経路）
        Highlight(control, new Rect(50, 50, 60, 40), "This is a very long window title that will definitely be truncated");

        control.Render(ctx);
    }

    /// <summary>情報ラベル非表示時でも例外なく描画することを検証する。</summary>
    [AvaloniaFact]
    public void Render_情報ラベル非表示時も例外なく描画する()
    {
        using var ctx = CreateContext();
        var control = CreateControl(
            FadeMaskRenderMode.WindowSelect,
            new FadeMaskAppearance { InfoLabelVisible = false });
        Highlight(control, new Rect(50, 50, 100, 80), "Test Window");

        control.Render(ctx);
    }

    /// <summary>サブライン表示ありで操作ラベルを描画する（例外なし）ことを検証する。</summary>
    [AvaloniaFact]
    public void Render_サブライン表示ありで操作ラベルを描画する()
    {
        using var ctx = CreateContext();
        var control = CreateControl();
        Select(control, new Rect(50, 50, 100, 80));

        control.Render(ctx);
    }

    /// <summary>サブライン非表示時でも例外なく描画することを検証する。</summary>
    [AvaloniaFact]
    public void Render_サブライン非表示時も例外なく描画する()
    {
        using var ctx = CreateContext();
        var control = CreateControl(appearance: new FadeMaskAppearance { ShowSubLines = false });
        Select(control, new Rect(50, 50, 100, 80));

        control.Render(ctx);
    }

    /// <summary>切出しモードの操作ラベルを描画する（例外なし）ことを検証する。</summary>
    [AvaloniaFact]
    public void Render_Cropモードで操作ラベルを描画する()
    {
        using var ctx = CreateContext();
        var control = CreateControl(FadeMaskRenderMode.Crop);
        Select(control, new Rect(50, 50, 100, 80));

        control.Render(ctx);
    }

    /// <summary>サイズラベル非表示時にサブラインを繰り上げて描画することを検証する。</summary>
    [AvaloniaFact]
    public void Render_サイズラベル非表示時にサブラインを繰り上げて描画する()
    {
        using var ctx = CreateContext();
        var control = CreateControl(appearance: new FadeMaskAppearance { ShowSizeLabel = false });
        Select(control, new Rect(50, 50, 100, 80));

        control.Render(ctx);
    }

    /// <summary>WindowSelect で情報ラベルを 3 行目に描画することを検証する。</summary>
    [AvaloniaFact]
    public void Render_WindowSelectで情報ラベルを3行目に描画する()
    {
        using var ctx = CreateContext();
        var control = CreateControl(FadeMaskRenderMode.WindowSelect);
        Highlight(control, new Rect(50, 50, 100, 80), "Test Window");

        control.Render(ctx);
    }

    /// <summary>1 行目から 3 行目までの背景色を個別に描画することを検証する。</summary>
    [AvaloniaFact]
    public void Render_背景色設定で1行目から3行目の背景を個別描画する()
    {
        using var ctx = CreateContext();
        var control = CreateControl(
            FadeMaskRenderMode.WindowSelect,
            new FadeMaskAppearance
            {
                SizeLabelBackgroundColor = Colors.DarkBlue.ToDrawingColor(),
                SubLineBackgroundColor = Colors.DarkGreen.ToDrawingColor(),
                InfoLabelBackgroundColor = Colors.DarkRed.ToDrawingColor(),
            });
        Highlight(control, new Rect(50, 50, 100, 80), "Test Window");

        control.Render(ctx);
    }

    /// <summary>ドメイン → DIP を 2 倍にする座標変換ポートを生成する。</summary>
    /// <returns>Forward = DIP × 2（ドメイン）/ Inverse = ドメイン ÷ 2（DIP）のポート。</returns>
    private static ICoordTransform CreateDoubleScalePort()
        => new CoordTransform(
            p => new EsPoint(p.X * 2, p.Y * 2),
            p => new EsPoint(p.X / 2, p.Y / 2),
            () => null);
}
