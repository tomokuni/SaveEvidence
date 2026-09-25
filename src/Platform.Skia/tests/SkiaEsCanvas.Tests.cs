using EsUtil.Platform.Core.Geometry;
using SkiaSharp;
using Xunit;
using DrawingColor = System.Drawing.Color;
using EsUtil.Platform.Core.Imaging;

namespace EsUtil.Platform.Skia.Tests;

/// <summary><see cref="SkiaEsCanvas"/> のテスト（描画ポートの契約をビットマップ上で検証する）。</summary>
/// <remarks>
/// 描画の実行は UI フレームワーク非依存の Skia で行うため、ウィンドウと UI 初期化なしで画素を検証できる。<br/>
/// 各メソッドがポートの契約どおりに描くこと（とくに「背景 → 文字」の順序）を固定する。
/// </remarks>
public class SkiaEsCanvasTests
{
    /// <summary>検証に使う白（不透明）。</summary>
    private static readonly DrawingColor s_white = DrawingColor.FromArgb(255, 255, 255, 255);

    /// <summary>検証に使う赤（不透明）。</summary>
    private static readonly DrawingColor s_red = DrawingColor.FromArgb(255, 255, 0, 0);

    /// <summary>テスト用の描画先（透明で初期化したビットマップとキャンバス）。</summary>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <returns>生成した描画先。破棄は呼び出し側。</returns>
    private static (SKBitmap Bitmap, SkiaEsCanvas Canvas) CreateTarget(int width, int height)
    {
        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        bitmap.Erase(SKColors.Transparent);
        return (bitmap, new SkiaEsCanvas(new SKCanvas(bitmap)));
    }

    [Fact]
    public void FillRect_矩形を塗る()
    {
        var (bitmap, canvas) = CreateTarget(20, 20);
        using var _ = bitmap;

        canvas.FillRect(new EsRect(5, 5, 10, 10), s_white);

        Assert.Equal(SKColors.White, bitmap.GetPixel(5, 5));
        Assert.Equal(SKColors.White, bitmap.GetPixel(14, 14));
        // 矩形の外側は塗られない
        Assert.Equal(0, bitmap.GetPixel(4, 5).Alpha);
        Assert.Equal(0, bitmap.GetPixel(15, 15).Alpha);
    }

    [Fact]
    public void FillRects_複数の矩形をまとめて塗る()
    {
        var (bitmap, canvas) = CreateTarget(20, 20);
        using var _ = bitmap;

        canvas.FillRects([new EsRect(0, 0, 5, 5), new EsRect(10, 10, 5, 5)], s_red);

        Assert.Equal(new SKColor(255, 0, 0), bitmap.GetPixel(2, 2));
        Assert.Equal(new SKColor(255, 0, 0), bitmap.GetPixel(12, 12));
        Assert.Equal(0, bitmap.GetPixel(8, 8).Alpha);
    }

    [Fact]
    public void FillRects_列挙子でも同じ結果になる()
    {
        var (bitmap, canvas) = CreateTarget(20, 20);
        using var _ = bitmap;

        canvas.FillRects(new TestRects([new EsRect(0, 0, 5, 5), new EsRect(10, 10, 5, 5)]), s_red);

        Assert.Equal(new SKColor(255, 0, 0), bitmap.GetPixel(2, 2));
        Assert.Equal(new SKColor(255, 0, 0), bitmap.GetPixel(12, 12));
        Assert.Equal(0, bitmap.GetPixel(8, 8).Alpha);
    }

    [Fact]
    public void FillRoundRect_角丸0は四角形として塗る()
    {
        var (bitmap, canvas) = CreateTarget(20, 20);
        using var _ = bitmap;

        canvas.FillRoundRect(new EsRect(2, 2, 10, 10), cornerRadius: 0, s_white);

        // 角丸なしは「描かない」ではなく四角形（角も塗られる）
        Assert.Equal(SKColors.White, bitmap.GetPixel(2, 2));
        Assert.Equal(SKColors.White, bitmap.GetPixel(11, 11));
    }

    [Fact]
    public void FillRoundRect_角丸は角を塗らない()
    {
        var (bitmap, canvas) = CreateTarget(20, 20);
        using var _ = bitmap;

        canvas.FillRoundRect(new EsRect(2, 2, 10, 10), cornerRadius: 5, s_white);

        // 角（左上の外側）は丸みの外になる
        Assert.Equal(0, bitmap.GetPixel(2, 2).Alpha);
        // 辺の中央は塗られる
        Assert.Equal(SKColors.White, bitmap.GetPixel(7, 2));
    }

    [Fact]
    public void StrokeRect_辺の上に線を描く()
    {
        var (bitmap, canvas) = CreateTarget(20, 20);
        using var _ = bitmap;

        canvas.StrokeRect(new EsRect(4, 4, 10, 10), s_white, thickness: 2);

        // 上辺（線は辺の上に中心を置くため y=4 を含む）
        Assert.Equal(SKColors.White, bitmap.GetPixel(9, 4));
        // 内側は塗られない
        Assert.Equal(0, bitmap.GetPixel(9, 9).Alpha);
    }

    [Fact]
    public void DrawLine_始点から終点へ線を描く()
    {
        var (bitmap, canvas) = CreateTarget(20, 20);
        using var _ = bitmap;

        canvas.DrawLine(new EsPoint(2, 10), new EsPoint(18, 10), s_white, thickness: 1);

        Assert.Equal(SKColors.White, bitmap.GetPixel(10, 10));
        Assert.Equal(0, bitmap.GetPixel(10, 14).Alpha);
    }

    [Fact]
    public void DrawLabel_背景の上に文字を描く()
    {
        var (bitmap, canvas) = CreateTarget(64, 32);
        using var _ = bitmap;

        var textOrigin = new EsPoint(4, 4);
        canvas.DrawLabel(new EsRect(0, 0, 60, 24), s_white, "W", textOrigin, fontSize: 14, s_red, cornerRadius: 0);

        // 背景は白で塗られる
        Assert.Equal(SKColors.White, bitmap.GetPixel(1, 1));

        // 文字（赤）が背景の後に描かれる（順序が逆なら赤は残らない）
        var red = 0;
        for (var y = 0; y < 32; y++)
        {
            for (var x = 0; x < 64; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.Red > 200 && pixel.Green < 60 && pixel.Blue < 60)
                {
                    red++;
                }
            }
        }

        Assert.True(red > 0, "文字が背景の後に描かれていない");
    }

    [Fact]
    public void DrawImage_切り出し元を描画先へ写す()
    {
        using var source = EsBitmap.Create(4, 4, DrawingColor.FromArgb(255, 0, 0, 255));
        var (bitmap, canvas) = CreateTarget(16, 16);
        using var _ = bitmap;

        canvas.DrawImage(source, new EsRect(0, 0, 4, 4), new EsRect(0, 0, 16, 16));

        // 拡大して全面へ描かれる（最近傍拡大のため単色が保たれる）
        Assert.Equal(new SKColor(0, 0, 255), bitmap.GetPixel(8, 8));
        Assert.Equal(new SKColor(0, 0, 255), bitmap.GetPixel(15, 15));
    }

    [Fact]
    public void Clear_描画面の全体を置き換える()
    {
        var (bitmap, canvas) = CreateTarget(16, 16);
        using var _ = bitmap;

        canvas.FillRect(new EsRect(0, 0, 16, 16), s_red);
        canvas.Clear(new EsRect(0, 0, 16, 16), s_white);

        // 描画先の矩形に限定せず、表面全体が置き換わる
        Assert.Equal(SKColors.White, bitmap.GetPixel(0, 0));
        Assert.Equal(SKColors.White, bitmap.GetPixel(15, 15));
    }

    [Fact]
    public void MeasureText_実寸を返す()
    {
        var (bitmap, canvas) = CreateTarget(4, 4);
        using var _ = bitmap;

        var size = canvas.MeasureText("W", 20);

        Assert.True(size.Width > 0, "幅が測れていない");
        Assert.True(size.Height > 0, "行高が測れていない");
    }

    [Fact]
    public void コンストラクタ_キャンバスがnullなら例外()
        => Assert.Throws<ArgumentNullException>(() => new SkiaEsCanvas(null!));

    /// <summary>テスト用の矩形列挙子（<see cref="IEsRectWalker{TSelf}"/> の実装検証を兼ねる）。</summary>
    private struct TestRects : IEsRectWalker<TestRects>
    {
        /// <summary>列挙する矩形。</summary>
        private readonly EsRect[] _rects;

        /// <summary>次に返す位置（-1 は開始前）。</summary>
        private int _index;

        /// <summary>列挙子を初期化する。</summary>
        /// <param name="rects">列挙する矩形。</param>
        public TestRects(EsRect[] rects)
        {
            _rects = rects;
            _index = -1;
        }

        /// <summary>列挙子自身を返す（<c>foreach</c> をボックス化させない）。</summary>
        /// <returns>列挙子のコピー。</returns>
        public readonly TestRects GetEnumerator() => this;

        /// <summary>現在の矩形を取得する。</summary>
        /// <value>直近の <see cref="MoveNext"/> が true を返したときの矩形。</value>
        public readonly EsRect Current => _rects[_index];

        /// <summary>次の矩形へ進む。</summary>
        /// <returns>次の矩形がある場合は true。</returns>
        public bool MoveNext() => ++_index < _rects.Length;
    }
}
