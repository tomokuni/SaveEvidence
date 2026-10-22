using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Session;
using Xunit;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.Tests.Session;

/// <summary><see cref="CaptureImageHolder"/> の仕様テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 「差し替え時の旧値の破棄」「破棄後に届いた画像のその場での破棄」「未取得時の占有範囲のフォールバック」<br/>
/// という 3 つの規則を固定する。
/// </remarks>
public class CaptureImageHolderTests
{
    /// <summary>検証で使うフォールバック範囲。</summary>
    private static readonly EsRectInt s_fallback = new(-1920, -200, 3840, 2160);

    /// <summary>検証用の画像を生成する。</summary>
    /// <param name="width">幅。</param>
    /// <param name="height">高さ。</param>
    /// <returns>指定サイズの画像。</returns>
    private static EsBitmap CreateImage(int width, int height) => EsBitmap.Create(width, height);

    [Fact]
    internal void Set_画像と原点を保持する()
    {
        using var holder = new CaptureImageHolder(s_fallback);
        using var image = CreateImage(10, 20);

        Assert.True(holder.Set(image, new EsPointInt(100, 50)));

        // 保持する型は読み取りの面（渡した書き換え可能の実体は読み取り専用の面へ移して解放する）
        Assert.True(image.IsDisposed);
        Assert.NotNull(holder.Image);
        Assert.Equal(new EsSizeInt(10, 20), holder.Image.Size);
        Assert.Equal(new EsPointInt(100, 50), holder.Origin);
        Assert.Equal(new EsRectInt(100, 50, 10, 20), holder.Bounds);
    }

    [Fact]
    internal void Bounds_未取得の場合はフォールバック範囲を返す()
    {
        // 背景撮影が完了する前に確定されても切り出し範囲をクランプできる
        using var holder = new CaptureImageHolder(s_fallback);

        Assert.Equal(s_fallback, holder.Bounds);
        Assert.Null(holder.Image);
    }

    [Fact]
    internal void Set_nullは未取得として扱う()
    {
        using var holder = new CaptureImageHolder(s_fallback);
        using var image = CreateImage(10, 20);
        holder.Set(image, new EsPointInt(1, 2));

        Assert.True(holder.Set(null, new EsPointInt(3, 4)));

        Assert.Null(holder.Image);
        Assert.Equal(s_fallback, holder.Bounds);
        Assert.True(image.IsDisposed); // 旧い画像は破棄する
    }

    [Fact]
    internal void Set_差し替え時は旧い画像を破棄する()
    {
        using var holder = new CaptureImageHolder(s_fallback);
        var first = CreateImage(10, 20);
        using var second = CreateImage(30, 40);

        holder.Set(first, new EsPointInt(0, 0));
        holder.Set(second, new EsPointInt(5, 6));

        // 旧い画像は破棄され、新しい画像は読み取りの面として保持される（書き換え可能の実体は Set が解放する）
        Assert.True(first.IsDisposed);
        Assert.True(second.IsDisposed);
        Assert.NotNull(holder.Image);
        Assert.Equal(new EsSizeInt(30, 40), holder.Image.Size);
        Assert.Equal(new EsRectInt(5, 6, 30, 40), holder.Bounds);
    }

    [Fact]
    internal void Dispose_保持している画像を破棄する()
    {
        var holder = new CaptureImageHolder(s_fallback);
        var image = CreateImage(10, 20);
        holder.Set(image, new EsPointInt(0, 0));
        var held = holder.Image;

        holder.Dispose();

        Assert.True(held!.IsDisposed);
        Assert.Null(holder.Image);
        Assert.Equal(s_fallback, holder.Bounds);
    }

    [Fact]
    internal void Set_破棄後は受け付けず渡された画像をその場で破棄する()
    {
        // 背景撮影の完了がウィンドウの後片付けより遅れた場合の破棄漏れを防ぐ
        using var holder = new CaptureImageHolder(s_fallback);
        holder.Dispose();
        var late = CreateImage(10, 20);

        Assert.False(holder.Set(late, new EsPointInt(0, 0)));

        Assert.True(late.IsDisposed);
        Assert.Null(holder.Image);
    }

    [Fact]
    internal void Dispose_複数回呼んでも安全である()
    {
        var holder = new CaptureImageHolder(s_fallback);

        holder.Dispose();
        holder.Dispose();
    }

    [Fact]
    internal void Dispose_未取得でも安全である()
    {
        using var holder = new CaptureImageHolder(s_fallback);

        holder.Dispose();
        holder.Dispose();

        Assert.Null(holder.Image);
    }
}

