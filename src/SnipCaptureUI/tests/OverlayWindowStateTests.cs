using Avalonia;
using System.Reflection;
using Xunit;

namespace Estable.AvaloniaUI.SnipCaptureUI.Tests;

/// <summary><see cref="OverlayWindow"/> の純粋計算テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 座標変換（画面物理 ↔ ドメイン ↔ ウィンドウローカル）とスクリーン検出の純粋静的関数のみを検証するため、<br/>
/// Avalonia ヘッドレス（<c>[AvaloniaFact]</c>）に依存せず <c>[Fact]</c> で並列実行する（テスト全体の高速化）。<br/>
/// ヘッドレス必須のテスト（ウィンドウ生成・クリック確定フロー等）は <see cref="OverlayWindowTests"/> が直列コレクションで検証する。
/// </remarks>
public class OverlayWindowStateTests
{
    // ====================================================================
    // 回帰: 選択矩形と取得画像がずれる（座標変換）
    // ====================================================================

    [Fact]
    public void DomainToScreenRect_ドメイン座標にウィンドウ位置を加算する()
    {
        // ドメイン = 仮想スクリーンローカル物理ピクセル。画面物理座標へは Position（仮想スクリーン原点）を加算する。
        var result = OverlayWindow.DomainToScreenRect(new Rect(100, 200, 400, 300), new PixelPoint(1920, 0));

        Assert.Equal(new Rect(2020, 200, 400, 300), result);
    }

    [Fact]
    public void DomainToScreenRect_ウィンドウ位置が原点の場合は加算されない()
    {
        var result = OverlayWindow.DomainToScreenRect(new Rect(100, 200, 400, 300), default);

        Assert.Equal(new Rect(100, 200, 400, 300), result);
    }

    // ====================================================================
    // 回帰: 閉区間キャプチャ（始点・終点のピクセルを含む）
    // ====================================================================

    [Fact]
    public void ToClosedIntervalPixelRect_半開区間の幅に1を加算して閉区間にする()
    {
        // ドラッグ (10,10)→(50,50) の矩形は幅40（半開区間 [10,50)）。閉区間では [10,50] = 41 ピクセル
        var result = OverlayWindow.ToClosedIntervalPixelRect(new Rect(10, 10, 40, 40));

        Assert.Equal(new PixelRect(10, 10, 41, 41), result);
    }

    [Fact]
    public void ToClosedIntervalPixelRect_小数座標は切り捨てて幅を1加算する()
    {
        var result = OverlayWindow.ToClosedIntervalPixelRect(new Rect(10.4, 20.6, 39.8, 29.2));

        // X=10, Y=20（切り捨て）。幅 = 39+1 = 40、高さ = 29+1 = 30
        Assert.Equal(new PixelRect(10, 20, 40, 30), result);
    }

    // ====================================================================
    // ハイライト（ScreenSelect / WindowSelect の検出ハイライト）
    // ====================================================================

    [Fact]
    public void FindScalingForPoint_点を含むスクリーンのスケーリングを返す()
    {
        var screens = new[]
        {
            (new PixelRect(0, 0, 1920, 1080), 1.0),
            (new PixelRect(1920, 0, 1280, 1024), 1.25),
        };

        // 1 番目のスクリーン内 → 1.0
        Assert.Equal(1.0, InvokeFindScalingForPoint(new PixelPoint(500, 500), 2.0, screens));
        // 2 番目のスクリーン内 → 1.25
        Assert.Equal(1.25, InvokeFindScalingForPoint(new PixelPoint(2000, 500), 2.0, screens));
        // どのスクリーンにも含まれない → フォールバック
        Assert.Equal(2.0, InvokeFindScalingForPoint(new PixelPoint(-100, -100), 2.0, screens));
    }

    [Fact]
    public void ParseScreenIndex_Screen形式のタイトルからインデックスを取得する()
    {
        // 「Screen {インデックス}」形式 → インデックス
        Assert.Equal(0, InvokeParseScreenIndex("Screen 0"));
        Assert.Equal(2, InvokeParseScreenIndex("Screen 2"));

        // パース不能 → -1
        Assert.Equal(-1, InvokeParseScreenIndex(null));
        Assert.Equal(-1, InvokeParseScreenIndex("NotAScreen"));
        Assert.Equal(-1, InvokeParseScreenIndex(""));
        // プレフィックスより短い（"Screen" ちょうど・それ未満）→ -1（範囲外例外にならない）
        Assert.Equal(-1, InvokeParseScreenIndex("Screen"));
        Assert.Equal(-1, InvokeParseScreenIndex("Scr"));
    }

    // ====================================================================
    // サイズラベル（ドメイン矩形からの物理サイズ + DisplayScale 生成）
    // ====================================================================

    [Fact]
    public void FormatDomainSizeLabel_ドメイン矩形とスケーリングからラベルを生成する()
    {
        // 100% モニタ: 物理サイズはドメインの値そのまま（3840x2160）、DisplayScale は 100%
        var label100 = InvokeFormatDomainSizeLabel(new Rect(0, 0, 3840, 2160), 1.0);
        Assert.Equal("(0, 0) - (3840, 2160),  (3840×2160), DisplayScale: (100%)", label100);

        // 120% モニタ: 物理サイズは変わらず 3840x2160（スケーリングに依存しない）、DisplayScale は 120%
        var label120 = InvokeFormatDomainSizeLabel(new Rect(0, 0, 3840, 2160), 1.2);
        Assert.Equal("(0, 0) - (3840, 2160),  (3840×2160), DisplayScale: (120%)", label120);
    }

    [Fact]
    public void FormatDomainSizeLabel_オフセット付き矩形の座標とサイズが正しい()
    {
        // セカンダリモニタ（負の座標・1920x1200）: 物理サイズはドメインの値そのまま
        var label = InvokeFormatDomainSizeLabel(new Rect(1914, -1200, 1920, 1200), 1.0);
        Assert.Equal("(1914, -1200) - (3834, 0),  (1920×1200), DisplayScale: (100%)", label);
    }

    // ====================================================================
    // リフレクションヘルパー（private 静的メソッドの直接検証）
    // ====================================================================

    private static double InvokeFindScalingForPoint(
        PixelPoint point, double fallback, IEnumerable<(PixelRect Bounds, double Scaling)> screens)
    {
        var method = typeof(OverlayWindow).GetMethod(
            "FindScalingForPoint", BindingFlags.Static | BindingFlags.NonPublic)!;
        return (double)method.Invoke(null, [point, fallback, screens])!;
    }

    private static int InvokeParseScreenIndex(string? title)
    {
        var method = typeof(OverlayWindow).GetMethod(
            "ParseScreenIndex", BindingFlags.Static | BindingFlags.NonPublic)!;
        return (int)method.Invoke(null, [title])!;
    }

    private static string InvokeFormatDomainSizeLabel(Rect domainRect, double scaling)
    {
        var method = typeof(OverlayWindow).GetMethod(
            "FormatDomainSizeLabel", BindingFlags.Static | BindingFlags.NonPublic)!;
        return (string)method.Invoke(null, [domainRect, scaling])!;
    }
}
