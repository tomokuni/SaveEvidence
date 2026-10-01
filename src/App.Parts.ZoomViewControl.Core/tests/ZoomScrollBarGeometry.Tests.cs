using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.SaveEvidence.Parts.ZoomView.Tests;

/// <summary><see cref="ZoomScrollBarGeometry"/> の単体テスト（スクロールバーの配置計算）。</summary>
/// <remarks>純粋計算のみのため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。</remarks>
public class ZoomScrollBarGeometryTests
{
    /// <summary>検証に使うスクロールバーの太さ（DIP）。</summary>
    private const double Thickness = 8.0;

    /// <summary>検証に使う余白（DIP）。</summary>
    private const double Margin = 4.0;

    // ====================================================================
    // 表示の要否
    // ====================================================================

    [Fact]
    public void Compute_画像がビューポートに収まる軸には生成しない()
    {
        // 200x100 の画像を 400x400 のビューポートへ等倍表示（どちらの軸も収まる）
        var (horizontal, vertical) = ZoomScrollBarGeometry.Compute(
            new EsSize(200, 100), new EsSize(400, 400), 1.0, default, Thickness, Margin);

        Assert.Null(horizontal);
        Assert.Null(vertical);
    }

    [Fact]
    public void Compute_画像が大きい軸のみ生成する()
    {
        // 800x100 の画像（横のみはみ出す）
        var (horizontal, vertical) = ZoomScrollBarGeometry.Compute(
            new EsSize(800, 100), new EsSize(400, 400), 1.0, default, Thickness, Margin);

        Assert.NotNull(horizontal);
        Assert.Null(vertical);
    }

    [Fact]
    public void Compute_スケールを考慮して表示の要否を決める()
    {
        // 200x200 の画像を倍率 4 で表示 = 800x800（両軸ともはみ出す）
        var (horizontal, vertical) = ZoomScrollBarGeometry.Compute(
            new EsSize(200, 200), new EsSize(400, 400), 4.0, default, Thickness, Margin);

        Assert.NotNull(horizontal);
        Assert.NotNull(vertical);
    }

    /// <summary>画像サイズ・スケール・太さが不正な場合は生成しないことを検証する。</summary>
    /// <param name="width">画像の幅。</param>
    /// <param name="height">画像の高さ。</param>
    /// <param name="scale">実効スケール。</param>
    /// <param name="thickness">スクロールバーの太さ。</param>
    [Theory]
    [InlineData(0, 100, 1.0, 8.0)]
    [InlineData(100, 0, 1.0, 8.0)]
    [InlineData(100, 100, 0.0, 8.0)]
    [InlineData(100, 100, 1.0, 0.0)]
    public void Compute_不正な入力では生成しない(double width, double height, double scale, double thickness)
    {
        var (horizontal, vertical) = ZoomScrollBarGeometry.Compute(
            new EsSize(width, height), new EsSize(10, 10), scale, default, thickness, Margin);

        Assert.Null(horizontal);
        Assert.Null(vertical);
    }

    // ====================================================================
    // トラックの配置
    // ====================================================================

    [Fact]
    public void Compute_垂直トラックは右端の余白分内側に配置する()
    {
        var (_, vertical) = ZoomScrollBarGeometry.Compute(
            new EsSize(800, 800), new EsSize(400, 400), 1.0, default, Thickness, Margin);

        var track = vertical!.Value.Track;
        // x = 400 - 4 - 8 = 388 / y = 4 / 幅 = 8 / 高さ = 400 - 8 = 392
        Assert.Equal(388, track.X);
        Assert.Equal(4, track.Y);
        Assert.Equal(8, track.Width);
        Assert.Equal(392, track.Height);
    }

    [Fact]
    public void Compute_水平トラックは下端の余白分内側に配置する()
    {
        var (horizontal, _) = ZoomScrollBarGeometry.Compute(
            new EsSize(800, 800), new EsSize(400, 400), 1.0, default, Thickness, Margin);

        var track = horizontal!.Value.Track;
        Assert.Equal(4, track.X);
        Assert.Equal(388, track.Y);
        Assert.Equal(392, track.Width);
        Assert.Equal(8, track.Height);
    }

    // ====================================================================
    // サムの長さと位置
    // ====================================================================

    [Fact]
    public void Compute_サム長はビューポートと表示サイズの比になる()
    {
        // 表示 800 / ビューポート 400 = 1/2 → トラック 392 の半分 = 196
        var (_, vertical) = ZoomScrollBarGeometry.Compute(
            new EsSize(800, 800), new EsSize(400, 400), 1.0, default, Thickness, Margin);

        Assert.Equal(196, vertical!.Value.Thumb.Height);
    }

    [Fact]
    public void Compute_サム長は最小長で下限を設ける()
    {
        // 表示 8000 / ビューポート 400 → 比 0.05（トラック 392 の 5% = 19.6）は最小長 24 へ引き上げられる
        var (_, vertical) = ZoomScrollBarGeometry.Compute(
            new EsSize(8000, 8000), new EsSize(400, 400), 1.0, default, Thickness, Margin, minThumbLength: 24);

        Assert.Equal(24, vertical!.Value.Thumb.Height);
    }

    [Fact]
    public void Compute_オフセット0ではサムがトラック先頭に位置する()
    {
        var (_, vertical) = ZoomScrollBarGeometry.Compute(
            new EsSize(800, 800), new EsSize(400, 400), 1.0, default, Thickness, Margin);

        // トラック y = 4、サムも同じ位置から始まる
        Assert.Equal(vertical!.Value.Track.Y, vertical.Value.Thumb.Y);
    }

    [Fact]
    public void Compute_最大オフセットではサムがトラック末尾に位置する()
    {
        // 表示 800 / ビューポート 400 / 等倍 → 最大オフセット = 800 - 400 = 400
        var (_, vertical) = ZoomScrollBarGeometry.Compute(
            new EsSize(800, 800), new EsSize(400, 400), 1.0, new EsVector(0, 400), Thickness, Margin);

        var bar = vertical!.Value;
        // サムの下端がトラックの下端と一致する
        Assert.Equal(bar.Track.Bottom, bar.Thumb.Bottom, 6);
    }

    [Fact]
    public void Compute_中間オフセットではサムが可動範囲の中間に位置する()
    {
        // 最大オフセット 400 の半数（200）→ 可動範囲（392 - 196 = 196）の半分 = 98
        var (_, vertical) = ZoomScrollBarGeometry.Compute(
            new EsSize(800, 800), new EsSize(400, 400), 1.0, new EsVector(0, 200), Thickness, Margin);

        Assert.Equal(4 + 98, vertical!.Value.Thumb.Y, 6);
    }

    [Fact]
    public void Compute_範囲外のオフセットは両端へクランプする()
    {
        var (_, over) = ZoomScrollBarGeometry.Compute(
            new EsSize(800, 800), new EsSize(400, 400), 1.0, new EsVector(0, 9999), Thickness, Margin);
        var (_, under) = ZoomScrollBarGeometry.Compute(
            new EsSize(800, 800), new EsSize(400, 400), 1.0, new EsVector(0, -9999), Thickness, Margin);

        Assert.Equal(over!.Value.Track.Bottom, over.Value.Thumb.Bottom, 6);
        Assert.Equal(under!.Value.Track.Y, under.Value.Thumb.Y, 6);
    }

    // ====================================================================
    // MaxContentOffset / ContentOffsetFromThumb
    // ====================================================================

    [Fact]
    public void MaxContentOffset_表示がビューポートを超えた分を返す()
    {
        // 等倍・ビューポート 400 → 800 - 400 = 400
        Assert.Equal(400, ZoomScrollBarGeometry.MaxContentOffset(800, 400, 1.0));
    }

    [Fact]
    public void MaxContentOffset_スケールを考慮する()
    {
        // 「コンテンツ長 − ビューポート長 / 倍率」を返す（表示の要否は呼び出し側が判定する）
        Assert.Equal(0, ZoomScrollBarGeometry.MaxContentOffset(50, 400, 4.0));
        Assert.Equal(100, ZoomScrollBarGeometry.MaxContentOffset(200, 400, 4.0));
    }

    [Fact]
    public void MaxContentOffset_収まる場合は0になる()
    {
        Assert.Equal(0, ZoomScrollBarGeometry.MaxContentOffset(200, 400, 1.0));
    }

    [Fact]
    public void MaxContentOffset_スケールが0以下なら0になる()
    {
        Assert.Equal(0, ZoomScrollBarGeometry.MaxContentOffset(800, 400, 0));
    }

    [Fact]
    public void ContentOffsetFromThumb_進捗率を最大オフセットへ写す()
    {
        // 可動範囲 196 の半分（98）→ 最大 400 の半分 = 200
        Assert.Equal(200, ZoomScrollBarGeometry.ContentOffsetFromThumb(98, 392, 196, 400), 6);
    }

    [Fact]
    public void ContentOffsetFromThumb_範囲外はクランプする()
    {
        Assert.Equal(400, ZoomScrollBarGeometry.ContentOffsetFromThumb(9999, 392, 196, 400), 6);
        Assert.Equal(0, ZoomScrollBarGeometry.ContentOffsetFromThumb(-9999, 392, 196, 400), 6);
    }

    [Fact]
    public void ContentOffsetFromThumb_可動範囲が0なら0を返す()
    {
        Assert.Equal(0, ZoomScrollBarGeometry.ContentOffsetFromThumb(50, 196, 196, 400));
    }

    [Fact]
    public void ContentOffsetFromThumb_往復変換で値が保たれる()
    {
        var (_, vertical) = ZoomScrollBarGeometry.Compute(
            new EsSize(800, 800), new EsSize(400, 400), 1.0, new EsVector(0, 200), Thickness, Margin);

        var bar = vertical!.Value;
        var thumbOffset = bar.Thumb.Y - bar.Track.Y;
        var restored = ZoomScrollBarGeometry.ContentOffsetFromThumb(thumbOffset, bar.Track.Height, bar.Thumb.Height, 400);

        Assert.Equal(200, restored, 6);
    }
}
