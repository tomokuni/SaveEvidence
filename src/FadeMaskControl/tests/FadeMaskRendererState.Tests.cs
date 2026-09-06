using Avalonia.Media;
using Estable.AvaloniaUI.Platform;
using Estable.Primitives;
using Xunit;

namespace Estable.AvaloniaUI.FadeMask.Tests;

/// <summary><see cref="FadeMaskRenderer"/> のヘッドレス不要テスト（描画状態の純粋なプロパティ検証）。</summary>
/// <remarks>
/// 描画（<c>Draw</c>）を実行せず、プロパティの既定値・フォールバックのみを検証するため、
/// Avalonia ヘッドレス実行（Dispatcher / RenderTargetBitmap）を必要としない。<br/>
/// 並列実行を妨げないよう <c>[Fact]</c> で検証する（描画テストは <c>FadeMaskRendererTests</c> が直列コレクションで検証する）。
/// </remarks>
public class FadeMaskRendererStateTests
{
    [Fact]
    public void DisplayScaleScaling_未設定時はScalingと同じ値を返す()
    {
        var renderer = new FadeMaskRenderer { Scaling = 1.25 };

        // DisplayScaleScaling 未設定（0）→ Scaling と同じ値
        Assert.Equal(1.25, renderer.DisplayScaleScaling);
    }

    [Fact]
    public void DisplayScaleScaling_設定値が優先される()
    {
        var renderer = new FadeMaskRenderer { Scaling = 1.25, DisplayScaleScaling = 2.0 };

        Assert.Equal(2.0, renderer.DisplayScaleScaling);
    }

    [Fact]
    public void Draw_背景色の既定値は黒60パーセントである()
    {
        var renderer = new FadeMaskRenderer();

        // 1〜3 行目の背景色は不透明度をアルファに統合した黒 60%（#99000000）
        Assert.Equal(Color.FromAColor(153, Colors.Black), renderer.TextBackgroundColor);
        Assert.Equal(Color.FromAColor(153, Colors.Black), renderer.SubLineBackgroundColor);
        Assert.Equal(Color.FromAColor(153, Colors.Black), renderer.InfoLabelBackgroundColor);
    }

    [Fact]
    public void SubLineColor_既定値は薄いグレーである()
    {
        var renderer = new FadeMaskRenderer();

        // 操作ラベルの文字色の既定は #DCDCDC（Gainsboro）
        Assert.Equal(Colors.Gainsboro, renderer.SubLineColor);
    }

    // ====================================================================
    // ToDipRect（論理矩形 → DIP 変換・閉区間 +1）
    // ====================================================================

    [Fact]
    public void ToDipRect_等価変換で終点に1を加算した閉区間になる()
    {
        var logical = new RectD(10, 20, 30, 40);
        var result = FadeMaskRenderer.ToDipRect(logical, CoordinatePort.Identity);

        // 終点（Right / Bottom）に +1 した閉区間 [10,41) × [20,61)
        Assert.Equal(new Avalonia.Rect(10, 20, 31, 41), result);
    }

    [Fact]
    public void ToDipRect_スケール2倍のポートで座標が拡大される()
    {
        var logical = new RectD(10, 20, 30, 40);
        // Scale(2.0) の Inverse = ドメイン座標 → DIP 座標（2 倍）
        var result = FadeMaskRenderer.ToDipRect(logical, CoordinatePort.Scale(2.0));

        // 終点 +1 後に 2 倍: (10*2, 20*2, 31*2, 41*2)
        Assert.Equal(new Avalonia.Rect(20, 40, 62, 82), result);
    }

    [Fact]
    public void ToDipRect_closedIntervalがfalseなら終点を加算しない()
    {
        var logical = new RectD(10, 20, 30, 40);
        var result = FadeMaskRenderer.ToDipRect(logical, CoordinatePort.Identity, closedInterval: false);

        // ハイライト矩形（スクリーン/ウィンドウ境界）は既に全ピクセルを含むため +1 しない
        // （+1 すると 3840x2160 が 3841x2161 になる不具合の回帰防止）
        Assert.Equal(new Avalonia.Rect(10, 20, 30, 40), result);
    }

    [Fact]
    public void ToDipRect_closedIntervalがfalseでもスケールポートで正しく変換される()
    {
        var logical = new RectD(10, 20, 30, 40);
        // Scale(2.0) の Inverse = ドメイン座標 → DIP 座標（2 倍）
        var result = FadeMaskRenderer.ToDipRect(logical, CoordinatePort.Scale(2.0), closedInterval: false);

        // 終点を加算せず 2 倍: (10*2, 20*2, 30*2, 40*2)
        Assert.Equal(new Avalonia.Rect(20, 40, 60, 80), result);
    }

    // ====================================================================
    // GetBorderRects（枠線 4 辺矩形計算）
    // ====================================================================

    [Fact]
    public void GetBorderRects_四辺の矩形を返す()
    {
        var rect = new Avalonia.Rect(10, 20, 100, 80);
        var result = FadeMaskRenderer.GetBorderRects(rect, 2.0);

        Assert.Equal(4, result.Count);
        // 上: (10,20) 幅100 高さ2
        Assert.Equal(new Avalonia.Rect(10, 20, 100, 2), result[0]);
        // 下: (10,98) 幅100 高さ2
        Assert.Equal(new Avalonia.Rect(10, 98, 100, 2), result[1]);
        // 左: (10,22) 幅2 高さ76
        Assert.Equal(new Avalonia.Rect(10, 22, 2, 76), result[2]);
        // 右: (108,22) 幅2 高さ76
        Assert.Equal(new Avalonia.Rect(108, 22, 2, 76), result[3]);
    }

    [Fact]
    public void GetBorderRects_太さ05未満は05に正規化される()
    {
        var rect = new Avalonia.Rect(10, 20, 100, 80);
        var result = FadeMaskRenderer.GetBorderRects(rect, 0.1);

        // 上辺の高さは 0.5 に正規化される
        Assert.Equal(0.5, result[0].Height);
    }

    [Fact]
    public void GetBorderRects_高さより太い枠線でも負にならない()
    {
        // 高さ 1・太さ 2 → 左右の高さは Math.Max(0, 1 - 4) = 0
        var rect = new Avalonia.Rect(10, 20, 100, 1);
        var result = FadeMaskRenderer.GetBorderRects(rect, 2.0);

        Assert.Equal(0, result[2].Height);
        Assert.Equal(0, result[3].Height);
    }

    // ====================================================================
    // BuildSubLines（サブライン生成）
    // ====================================================================

    [Fact]
    public void BuildSubLines_AreaSelectモードで操作ラベルを返す()
    {
        var renderer = new FadeMaskRenderer { Mode = FadeMaskRenderMode.AreaSelect, ShowSubLines = true };

        var lines = renderer.BuildSubLines();

        Assert.Single(lines);
        Assert.Equal("ダブルクリック: 確定  Esc / 矩形外クリック: キャンセル", lines[0].Text);
    }

    [Fact]
    public void BuildSubLines_ScreenSelectモードでEnterクリック操作を返す()
    {
        var renderer = new FadeMaskRenderer { Mode = FadeMaskRenderMode.ScreenSelect, ShowSubLines = true };

        var lines = renderer.BuildSubLines();

        Assert.Single(lines);
        Assert.Equal("Enter / クリック: 確定  Esc: キャンセル", lines[0].Text);
    }

    [Fact]
    public void BuildSubLines_ShowSubLines無効時は操作ラベルを返さない()
    {
        var renderer = new FadeMaskRenderer { Mode = FadeMaskRenderMode.AreaSelect, ShowSubLines = false };

        var lines = renderer.BuildSubLines();

        Assert.Empty(lines);
    }

    [Fact]
    public void BuildSubLines_操作ラベル無効でも情報ラベルは表示する()
    {
        var renderer = new FadeMaskRenderer
        {
            Mode = FadeMaskRenderMode.WindowSelect,
            ShowSubLines = false,
            InfoLabelVisible = true,
            WindowTitle = "Test Window",
        };

        var lines = renderer.BuildSubLines();

        Assert.Single(lines);
        Assert.Equal("Test Window", lines[0].Text);
    }

    [Fact]
    public void BuildSubLines_WindowSelectで情報ラベルを3行目に追加する()
    {
        var renderer = new FadeMaskRenderer
        {
            Mode = FadeMaskRenderMode.WindowSelect,
            ShowSubLines = true,
            InfoLabelVisible = true,
            WindowTitle = "Test Window",
        };

        var lines = renderer.BuildSubLines();

        Assert.Equal(2, lines.Count);
        Assert.Equal("Enter / クリック: 確定  Esc: キャンセル", lines[0].Text); // 2 行目 = 操作
        Assert.Equal("Test Window", lines[1].Text); // 3 行目 = 情報
    }

    [Fact]
    public void BuildSubLines_情報ラベル非表示時はタイトルを追加しない()
    {
        var renderer = new FadeMaskRenderer
        {
            Mode = FadeMaskRenderMode.WindowSelect,
            ShowSubLines = true,
            InfoLabelVisible = false,
            WindowTitle = "Test Window",
        };

        var lines = renderer.BuildSubLines();

        Assert.Single(lines);
    }
}
