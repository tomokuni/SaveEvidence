using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Native;
using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.SaveEvidence.CaptureSession.Tests;

/// <summary><see cref="CaptureTypeTraits"/> の仕様テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 「どの種別がハイライト型か」「閉区間か」「検出対象はどれか」の判定が両 UI で同じになることを固定する。
/// </remarks>
public class CaptureTypeTraitsTests
{
    [Theory]
    [InlineData(CaptureType.ScreenSelect)]
    [InlineData(CaptureType.WindowSelect)]
    public void IsHighlighted_ハイライト型はtrue(CaptureType type)
    {
        Assert.True(CaptureTypeTraits.IsHighlighted(type));
    }

    [Fact]
    public void IsHighlighted_範囲選択はfalse()
    {
        Assert.False(CaptureTypeTraits.IsHighlighted(CaptureType.AreaSelect));
    }

    [Fact]
    public void UsesClosedInterval_範囲選択のみtrue()
    {
        Assert.True(CaptureTypeTraits.UsesClosedInterval(CaptureType.AreaSelect));
        Assert.False(CaptureTypeTraits.UsesClosedInterval(CaptureType.ScreenSelect));
        Assert.False(CaptureTypeTraits.UsesClosedInterval(CaptureType.WindowSelect));
    }

    [Fact]
    public void GetElementTarget_スクリーン選択は画面を検出する()
    {
        Assert.Equal(CaptureElementTarget.Screen, CaptureTypeTraits.GetElementTarget(CaptureType.ScreenSelect));
    }

    [Theory]
    [InlineData(CaptureType.WindowSelect)]
    [InlineData(CaptureType.AreaSelect)]
    public void GetElementTarget_それ以外はウィンドウを検出する(CaptureType type)
    {
        Assert.Equal(CaptureElementTarget.Window, CaptureTypeTraits.GetElementTarget(type));
    }

    [Fact]
    public void IsHighlighted_とUsesClosedInterval_は排他である()
    {
        // ハイライト型は検出矩形（全ピクセルを含む閉区間）を使うため +1 しない
        foreach (var type in Enum.GetValues<CaptureType>())
        {
            Assert.NotEqual(CaptureTypeTraits.IsHighlighted(type), CaptureTypeTraits.UsesClosedInterval(type));
        }
    }

    // ====================================================================
    // FindUnderCursor（検出関数の呼び分け）
    // ====================================================================

    [Fact]
    public void FindUnderCursor_スクリーン選択は画面検出を呼ぶ()
    {
        var finder = new RecordingElementFinder();

        CaptureTypeTraits.FindUnderCursor(finder, CaptureType.ScreenSelect, new EsPointInt(10, 20));

        Assert.Equal(1, finder.ScreenCalls);
        Assert.Equal(0, finder.WindowCalls);
    }

    [Theory]
    [InlineData(CaptureType.WindowSelect)]
    [InlineData(CaptureType.AreaSelect)]
    public void FindUnderCursor_それ以外はウィンドウ検出を呼ぶ(CaptureType type)
    {
        var finder = new RecordingElementFinder();

        CaptureTypeTraits.FindUnderCursor(finder, type, new EsPointInt(10, 20));

        Assert.Equal(0, finder.ScreenCalls);
        Assert.Equal(1, finder.WindowCalls);
    }

    [Fact]
    public void FindUnderCursor_検出サービスが無い場合は未検出を返す()
    {
        var result = CaptureTypeTraits.FindUnderCursor(null, CaptureType.ScreenSelect, new EsPointInt(10, 20));

        Assert.False(result.IsDetected);
        Assert.Equal(default, result.Bounds);
    }

    [Fact]
    public void FindUnderCursor_検出結果をそのまま返す()
    {
        var finder = new RecordingElementFinder
        {
            Result = new ElementFinderResults(new EsRectInt(1, 2, 3, 4), IsDetected: true, Title: "T"),
        };

        var result = CaptureTypeTraits.FindUnderCursor(finder, CaptureType.WindowSelect, new EsPointInt(10, 20));

        Assert.True(result.IsDetected);
        Assert.Equal("T", result.Title);
        // カーソル位置がそのまま検出サービスへ渡る（座標変換は呼び出し側の責務）
        Assert.Equal(new EsPointInt(10, 20), finder.LastPoint);
    }

    // ====================================================================
    // ResolveRect（確定対象の選択）
    // ====================================================================

    [Fact]
    public void ResolveRect_ハイライト型はハイライト矩形を選ぶ()
    {
        var highlight = new EsRect(1, 2, 3, 4);
        var selection = new EsRect(9, 9, 9, 9);

        Assert.Equal(highlight, CaptureTypeTraits.ResolveRect(CaptureType.ScreenSelect, highlight, selection));
        Assert.Equal(highlight, CaptureTypeTraits.ResolveRect(CaptureType.WindowSelect, highlight, selection));
    }

    [Fact]
    public void ResolveRect_範囲選択は選択矩形を選ぶ()
    {
        var highlight = new EsRect(1, 2, 3, 4);
        var selection = new EsRect(9, 9, 9, 9);

        Assert.Equal(selection, CaptureTypeTraits.ResolveRect(CaptureType.AreaSelect, highlight, selection));
    }

    [Fact]
    public void ResolveRect_対象が無い場合はnullを返す()
    {
        Assert.Null(CaptureTypeTraits.ResolveRect(CaptureType.ScreenSelect, null, new EsRect(1, 2, 3, 4)));
        Assert.Null(CaptureTypeTraits.ResolveRect(CaptureType.AreaSelect, new EsRect(1, 2, 3, 4), null));
    }

    /// <summary>呼び出された検出関数を記録する要素検出サービス（テスト用）。</summary>
    private sealed class RecordingElementFinder : IElementFinder
    {
        /// <summary>返す検出結果。</summary>
        public ElementFinderResults Result { get; set; }

        /// <summary>画面検出の呼び出し回数。</summary>
        public int ScreenCalls { get; private set; }

        /// <summary>ウィンドウ検出の呼び出し回数。</summary>
        public int WindowCalls { get; private set; }

        /// <summary>最後に渡されたカーソル位置。</summary>
        public EsPointInt LastPoint { get; private set; }

        /// <inheritdoc/>
        public ElementFinderResults FindWindowUnderCursor(EsPointInt screenPoint)
        {
            WindowCalls++;
            LastPoint = screenPoint;
            return Result;
        }

        /// <inheritdoc/>
        public ElementFinderResults FindScreenUnderCursor(EsPointInt screenPoint)
        {
            ScreenCalls++;
            LastPoint = screenPoint;
            return Result;
        }

        /// <inheritdoc/>
        public IReadOnlyList<ElementFinderResults> ListScreens() => [];

        /// <inheritdoc/>
        public IReadOnlyList<ElementFinderResults> ListWindows() => [];
    }
}
