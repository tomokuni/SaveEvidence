using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using Estable.Marquee.Decoration;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace Estable.Marquee.Tests;

/// <summary><see cref="MarqueeFrame.ResolveInner"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// <see cref="MarqueeFrame.Resolve"/>（外側の装飾）と同じく、セッションの状態から内側の装飾の描画内容を組み立てる<br/>
/// 経路を検証する。選択矩形はセッションが単一所有するため、UI 実装は本メソッドを呼ぶだけでよい。
/// </remarks>
public class MarqueeFrameInnerTests
{
    /// <summary>検証に使う選択矩形（ドメイン座標）。</summary>
    private static readonly EsRect s_selection = new(50, 50, 100, 80);

    /// <summary>塗りと枠線を描く内側の装飾の外観。</summary>
    private static readonly InnerDecoAppearance s_visibleDecoration = new()
    {
        FillColor = DrawingColor.FromArgb(40, 255, 59, 48),
        BorderColor = DrawingColor.FromArgb(255, 255, 59, 48),
        BorderThickness = 2.0,
    };

    /// <summary>テスト用のセッションを生成する。</summary>
    /// <param name="options">設定。</param>
    /// <param name="selection">設定する選択矩形。null の場合は設定しない。</param>
    /// <returns>生成したセッション。</returns>
    private static MarqueeSession CreateSession(MarqueeOptions options, EsRect? selection = null)
    {
        var session = new MarqueeSession(null, options);
        if (selection is { } rect)
        {
            session.SetSelection(rect);
        }

        return session;
    }

    [Fact]
    public void ResolveInner_選択矩形の内側を塗り内枠を描く()
    {
        var session = CreateSession(
            MarqueeOptions.Default with { InnerAppearance = s_visibleDecoration }, s_selection);

        var frame = MarqueeFrame.ResolveInner(session, port: null, scaling: 1.0);

        // 選択矩形は閉区間（終点 +1）で DIP 変換される（外側の枠線と同一の規則）
        Assert.Equal(new EsRect(50, 50, 101, 81), frame.FillRect);
        Assert.Equal(4, frame.BorderRects.Count);
    }

    [Fact]
    public void ResolveInner_選択が無ければ空のフレームを返す()
    {
        var session = CreateSession(MarqueeOptions.Default with { InnerAppearance = s_visibleDecoration });

        var frame = MarqueeFrame.ResolveInner(session, port: null, scaling: 1.0);

        Assert.Null(frame.FillRect);
        Assert.Empty(frame.BorderRects);
    }

    [Fact]
    public void ResolveInner_ハイライトが選択より優先される()
    {
        var highlight = new EsRect(10, 20, 30, 40);
        var session = CreateSession(
            MarqueeOptions.Default with
            {
                InnerAppearance = s_visibleDecoration,
                OuterHasHighlight = true,
                OuterHighlightRect = highlight,
            },
            s_selection);

        var frame = MarqueeFrame.ResolveInner(session, port: null, scaling: 1.0);

        Assert.Equal(highlight, frame.FillRect);
    }

    [Fact]
    public void ResolveInner_セッションがnullなら例外()
    {
        Assert.Throws<ArgumentNullException>(() => MarqueeFrame.ResolveInner(null!, port: null, scaling: 1.0));
    }

    [Fact]
    public void ResolveInner_倍率を適用する()
    {
        var session = CreateSession(
            MarqueeOptions.Default with { InnerAppearance = s_visibleDecoration }, s_selection);

        var frame = MarqueeFrame.ResolveInner(session, port: null, scaling: 2.0);

        Assert.Equal(new EsRect(100, 100, 202, 162), frame.FillRect);
    }

    [Fact]
    public void ResolveInner_ポートと倍率はポートが優先される()
    {
        var session = CreateSession(
            MarqueeOptions.Default with { InnerAppearance = s_visibleDecoration }, s_selection);

        var frame = MarqueeFrame.ResolveInner(session, port: CoordTransform.Identity, scaling: 2.0);

        Assert.Equal(new EsRect(50, 50, 101, 81), frame.FillRect);
    }
}
