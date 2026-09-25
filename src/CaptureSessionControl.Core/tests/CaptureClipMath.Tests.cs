using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Xunit;

namespace EsUtil.SaveEvidence.CaptureSession.Tests;

/// <summary><see cref="CaptureClipMath"/> の仕様テスト（UI 非依存・並列実行）。</summary>
/// <remarks>画面物理座標 → 画像ローカル座標の変換と、切り出しのフォールバック規則を固定する。</remarks>
public class CaptureClipMathTests
{
    /// <summary>検証用の単色画像を生成する。</summary>
    /// <param name="width">幅。</param>
    /// <param name="height">高さ。</param>
    /// <returns>指定サイズの画像。</returns>
    private static EsBitmap CreateImage(int width, int height) => EsBitmap.Create(width, height);

    // ====================================================================
    // ResolveCaptureRect（選択矩形 → 切り出し矩形）
    // ====================================================================

    [Fact]
    public void ResolveCaptureRect_範囲選択は閉区間化して終点のピクセルを含める()
    {
        // ドラッグ (10,10)→(50,50) の矩形は幅 40（半開区間）→ 閉区間では 41 ピクセル
        var result = CaptureClipMath.ResolveCaptureRect(
            CaptureType.AreaSelect, new EsRect(10, 10, 40, 40), new EsPixelRect(0, 0, 100, 100));

        Assert.Equal(new EsPixelRect(10, 10, 41, 41), result);
    }

    [Fact]
    public void ResolveCaptureRect_ハイライト型は閉区間化しない()
    {
        // 検出矩形は最初から全ピクセルを含むため加算しない（+1 すると 1 ピクセルはみ出す）
        var result = CaptureClipMath.ResolveCaptureRect(
            CaptureType.ScreenSelect, new EsRect(0, 0, 3840, 2160), new EsPixelRect(0, 0, 3840, 2160));

        Assert.Equal(new EsPixelRect(0, 0, 3840, 2160), result);
    }

    [Fact]
    public void ResolveCaptureRect_画像の右端と下端でクランプする()
    {
        // クランプしないと画像外を切り出すことになり、結果の端に透明な余白が付く
        var result = CaptureClipMath.ResolveCaptureRect(
            CaptureType.AreaSelect, new EsRect(90, 90, 10, 10), new EsPixelRect(0, 0, 100, 100));

        Assert.Equal(new EsPixelRect(90, 90, 10, 10), result);
    }

    [Fact]
    public void ResolveCaptureRect_原点が負値でも画面物理座標で返す()
    {
        // セカンダリが左・上にある構成（原点が負値）。ローカル (0,0) から切り出す = 画面物理 (-1920,-200) から
        var result = CaptureClipMath.ResolveCaptureRect(
            CaptureType.AreaSelect, new EsRect(-1920, -200, 40, 40), new EsPixelRect(-1920, -200, 100, 100));

        Assert.Equal(new EsPixelRect(-1920, -200, 41, 41), result);
    }

    [Theory]
    [InlineData(10.4, 20.6, 39.8, 29.2, 10, 21, 41, 30)]
    [InlineData(10.5, 20.5, 10.0, 10.0, 11, 21, 11, 11)]
    public void ResolveCaptureRect_端数は四捨五入する(
        double x, double y, double width, double height,
        int expectedX, int expectedY, int expectedWidth, int expectedHeight)
    {
        // 丸め規則はコアが単一所有する（切り捨て・銀行丸めで書くと UI ごとに 1 ピクセルずれる）。
        // 0.5 は左右の端で切り上げる（10.5→11 / 20.5→21）
        var result = CaptureClipMath.ResolveCaptureRect(
            CaptureType.AreaSelect, new EsRect(x, y, width, height), new EsPixelRect(0, 0, 100, 100));

        Assert.Equal(new EsPixelRect(expectedX, expectedY, expectedWidth, expectedHeight), result);
    }

    [Fact]
    public void ResolveCaptureRect_画像サイズが0の場合は空を返す()
    {
        var result = CaptureClipMath.ResolveCaptureRect(
            CaptureType.AreaSelect, new EsRect(0, 0, 10, 10), default);

        Assert.True(result.IsEmpty);
    }

    // ====================================================================
    // ResolveCaptureRect（整数の選択矩形 = クリック確定・ホバー）
    // ====================================================================

    [Fact]
    public void ResolveCaptureRect_整数版も浮動小数版と同じ規則を適用する()
    {
        // 検出矩形（整数）でも範囲選択は閉区間化して終点のピクセルを含める
        var result = CaptureClipMath.ResolveCaptureRect(
            CaptureType.AreaSelect, new EsPixelRect(10, 10, 40, 40), new EsPixelRect(0, 0, 100, 100));

        Assert.Equal(new EsPixelRect(10, 10, 41, 41), result);
    }

    [Fact]
    public void ResolveCaptureRect_整数版は画像の右端でクランプする()
    {
        var result = CaptureClipMath.ResolveCaptureRect(
            CaptureType.AreaSelect, new EsPixelRect(90, 90, 10, 10), new EsPixelRect(0, 0, 100, 100));

        Assert.Equal(new EsPixelRect(90, 90, 10, 10), result);
    }

    [Theory]
    [InlineData(CaptureType.AreaSelect)]
    [InlineData(CaptureType.WindowSelect)]
    public void ResolveCaptureRect_整数版と浮動小数版は同じ結果になる(CaptureType captureType)
    {
        // 同じ選択を 2 つの入口（整数 / 浮動小数）で解決しても一致する（入口による差を作らない）
        var screenRect = new EsPixelRect(30, 20, 50, 40);
        var imageBounds = new EsPixelRect(-1920, -200, 3840, 2160);

        var asInt = CaptureClipMath.ResolveCaptureRect(captureType, screenRect, imageBounds);
        var asRect = CaptureClipMath.ResolveCaptureRect(
            captureType,
            new EsRect(screenRect.X, screenRect.Y, screenRect.Width, screenRect.Height),
            imageBounds);

        Assert.Equal(asRect, asInt);
    }

    // ====================================================================
    // 切り出し矩形の座標空間（画面物理座標に統一する契約）
    // ====================================================================

    [Fact]
    public void ResolveCaptureRect_負の原点でも結果は画面物理座標のまま()
    {
        // マルチモニタで仮想スクリーンの原点が負値（セカンダリが左・上）でも、
        // 入口（選択矩形）と占有範囲が同じ座標空間（画面物理座標）なら結果も画面物理座標になる
        var imageBounds = new EsPixelRect(-1920, -200, 3840, 2160);
        // 画像の右端側（画面物理座標では X=1080 が画像ローカル座標では 3000 に相当する）
        var screenRect = new EsRect(1080, -100, 30, 30);

        var result = CaptureClipMath.ResolveCaptureRect(CaptureType.AreaSelect, screenRect, imageBounds);

        Assert.Equal(new EsPixelRect(1080, -100, 31, 31), result);
        Assert.False(result.IsEmpty);
    }

    [Fact]
    public void ResolveCaptureRect_画像ローカル座標を渡すと切り出せなくなる()
    {
        // 座標空間を混ぜる（画面物理の占有範囲に画像ローカル座標の選択矩形を渡す）と、
        // 原点の分だけずれて画像外になり、画像の右端側では幅が 0 になる。
        // この機序をテストで固定する（入口は画面物理座標に統一する契約）
        var imageBounds = new EsPixelRect(-1920, -200, 3840, 2160);
        var localRect = new EsRect(3000, 100, 30, 30);

        var mixed = CaptureClipMath.ResolveCaptureRect(CaptureType.AreaSelect, localRect, imageBounds);

        Assert.True(mixed.IsEmpty);
    }

    // ====================================================================
    // ResolveCaptureRect（整数の選択矩形 = クリック確定・ホバー）
    // ====================================================================

    // ====================================================================
    // ImageSize / ImageBounds / ValidateImage（画像の共有値）
    // ====================================================================

    [Fact]
    public void ImageSize_画像のピクセルサイズを返す()
    {
        using var image = CreateImage(320, 180);

        Assert.Equal(new EsSize(320, 180), CaptureClipMath.ImageSize(image));
    }

    [Fact]
    public void ImageBounds_原点とサイズから占有範囲を返す()
    {
        using var image = CreateImage(320, 180);

        var bounds = CaptureClipMath.ImageBounds(image, new EsPixelPoint(-1920, -200));

        Assert.Equal(new EsPixelRect(-1920, -200, 320, 180), bounds);
    }

    [Fact]
    public void ValidateImage_サイズが正なら例外を投げない()
    {
        using var image = CreateImage(1, 1);

        CaptureClipMath.ValidateImage(image, nameof(image));
    }

    [Fact]
    public void ValidateImage_サイズが0なら引数名付きで例外を投げる()
    {
        // 「サイズが 0 の画像は切り出せない」規則をコアが単一所有する
        using var image = CreateImage(0, 0);

        var exception = Assert.Throws<ArgumentException>(() => CaptureClipMath.ValidateImage(image, "sourceImage"));
        Assert.Equal("sourceImage", exception.ParamName);
    }

    [Fact]
    public void ResolveCaptureRect_画像外の選択は空を返す()
    {
        var result = CaptureClipMath.ResolveCaptureRect(
            CaptureType.WindowSelect, new EsRect(200, 200, 10, 10), new EsPixelRect(0, 0, 100, 100));

        Assert.True(result.IsEmpty);
    }

    [Fact]
    public void ResolveCaptureRect_最小サイズを適用しない()
    {
        // 最小サイズは選択操作（Marquee）が所有する。検出矩形は要素の大きさであり小さい要素も対象にする
        var result = CaptureClipMath.ResolveCaptureRect(
            CaptureType.WindowSelect, new EsRect(10, 10, 1, 1), new EsPixelRect(0, 0, 100, 100));

        Assert.Equal(new EsPixelRect(10, 10, 1, 1), result);
    }

    // ====================================================================
    // ToLocalRect / ToScreenRect（画面物理座標 ⇔ 画像ローカル座標）
    // ====================================================================
    [Fact]
    public void ToLocalRect_原点を減算する()
    {
        // 画像ローカル = 画面物理 − 原点
        var result = CaptureClipMath.ToLocalRect(new EsPixelRect(200, 150, 40, 30), new EsPixelPoint(100, 50));

        Assert.Equal(new EsPixelRect(100, 100, 40, 30), result);
    }

    [Fact]
    public void ToLocalRect_原点が負値でも減算で一致する()
    {
        // セカンダリが左・上にある構成（原点が負値）
        var result = CaptureClipMath.ToLocalRect(new EsPixelRect(-1800, -100, 40, 30), new EsPixelPoint(-1920, -200));

        Assert.Equal(new EsPixelRect(120, 100, 40, 30), result);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1920, 0)]
    [InlineData(-1920, -1080)]
    public void ToLocalRect_幅と高さは変更しない(int originX, int originY)
    {
        var capture = new EsPixelRect(originX + 7, originY + 9, 41, 31);

        var result = CaptureClipMath.ToLocalRect(capture, new EsPixelPoint(originX, originY));

        Assert.Equal(new EsPixelRect(7, 9, 41, 31), result);
    }

    [Fact]
    public void Extract_切り出し矩形のサイズの画像を返す()
    {
        using var screen = CreateImage(200, 100);
        using var result = CaptureClipMath.Extract(screen, new EsPixelPoint(0, 0), new EsPixelRect(10, 20, 41, 31));

        Assert.NotNull(result);
        Assert.Equal(41, result!.Width);
        Assert.Equal(31, result.Height);
    }

    [Fact]
    public void Extract_原点を考慮して切り出す()
    {
        // 原点 (100, 50) の画像で画面物理 (100, 50) から切り出す = 画像の (0, 0) から
        using var screen = CreateImage(200, 100);
        using var result = CaptureClipMath.Extract(screen, new EsPixelPoint(100, 50), new EsPixelRect(100, 50, 10, 10));

        Assert.NotNull(result);
        Assert.Equal(10, result!.Width);
        Assert.Equal(10, result.Height);
    }

    [Fact]
    public void Extract_仮想スクリーン未取得の場合はnullを返す()
    {
        // 切り取り元が無い場合は呼び出し側が領域指定のキャプチャへフォールバックする
        var result = CaptureClipMath.Extract(null, default, new EsPixelRect(0, 0, 10, 10));

        Assert.Null(result);
    }

    [Fact]
    public void Extract_破棄済みの画像の場合はnullを返す()
    {
        // ウィンドウクローズと背景の切り出しが競合するため、破棄済みは切り出せない（例外にしない）
        var screen = CreateImage(100, 100);
        screen.Dispose();

        var result = CaptureClipMath.Extract(screen, default, new EsPixelRect(0, 0, 10, 10));

        Assert.Null(result);
    }

    [Fact]
    public void ToScreenRect_原点を加算する()
    {
        // ToLocalRect の逆変換
        var result = CaptureClipMath.ToScreenRect(new EsRect(100, 100, 40, 30), new EsPixelPoint(1920, 0));

        Assert.Equal(new EsRect(2020, 100, 40, 30), result);
    }

    [Fact]
    public void ToScreenRect_原点が原点なら変更しない()
    {
        var result = CaptureClipMath.ToScreenRect(new EsRect(10, 20, 30, 40), default);

        Assert.Equal(new EsRect(10, 20, 30, 40), result);
    }

    [Theory]
    [InlineData(0, 0, 10.5, 20.25, 40, 30)]
    [InlineData(1920, 0, 100, 200, 41, 31)]
    [InlineData(-1920, -1080, -1800, -900, 40, 30)]
    public void ToScreenRect_ToLocalRectの逆変換として往復一致する(
        int originX, int originY, double x, double y, double width, double height)
    {
        // 双方向の変換が対称であることを固定する（片方向だけのマッピングを残さない）
        var origin = new EsPixelPoint(originX, originY);
        var local = new EsRect(x, y, width, height);

        var screen = CaptureClipMath.ToScreenRect(local, origin);
        var back = CaptureClipMath.ToLocalRect(screen, origin);

        Assert.Equal(local, back);
    }

    [Fact]
    public void ToLocalRect_浮動小数の矩形も同じ式で変換する()
    {
        var result = CaptureClipMath.ToLocalRect(new EsRect(2020.5, 200.25, 40, 30), new EsPixelPoint(1920, 0));

        Assert.Equal(new EsRect(100.5, 200.25, 40, 30), result);
    }
}
