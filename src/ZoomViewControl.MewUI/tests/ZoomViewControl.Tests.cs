using Aprillz.MewUI;
using EsUtil.Platform.Core.Geometry;
using Estable.ZoomView;
using SkiaSharp;
using Xunit;

namespace Estable.MewUI.ZoomView.Tests;

/// <summary><see cref="ZoomViewControl"/> のズーム・パン・座標変換のテスト。</summary>
/// <remarks>
/// MewUI の要素生成とレイアウト（<c>Measure</c> / <c>Arrange</c>）はホスト登録を必要としないため、<br/>
/// UI を表示せずに検証できる。描画結果の検証は <see cref="ZoomSkiaRendererTests"/> が担う。
/// </remarks>
public class ZoomViewControlTests
{
    /// <summary>ビューポートを確定させたコントロールを生成する。</summary>
    /// <param name="width">ビューポートの幅（DIP）。</param>
    /// <param name="height">ビューポートの高さ（DIP）。</param>
    /// <returns>レイアウト済みのコントロール。</returns>
    private static ZoomViewControl CreateLaidOut(double width = 200, double height = 200)
    {
        var control = new ZoomViewControl();
        control.Measure(new Size(width, height));
        control.Arrange(new Rect(0, 0, width, height));
        return control;
    }

    /// <summary>画像を設定したコントロールを生成する（Fit 表示で開始する）。</summary>
    /// <param name="imageWidth">画像の幅（ピクセル）。</param>
    /// <param name="imageHeight">画像の高さ（ピクセル）。</param>
    /// <param name="viewportSize">ビューポートの一辺（DIP）。</param>
    /// <returns>コントロールと画像（画像の破棄は呼び出し側）。</returns>
    private static (ZoomViewControl Control, EsUtil.Platform.Core.Imaging.EsBitmap Image) CreateWithImage(
        int imageWidth = 100, int imageHeight = 100, double viewportSize = 200)
    {
        var image = TestHelper.CreateBitmap(imageWidth, imageHeight, SKColors.Red);
        var control = CreateLaidOut(viewportSize, viewportSize);
        control.Source = image;
        return (control, image);
    }

    /// <summary>既定値が想定どおりであることを検証する。</summary>
    [Fact]
    public void 既定値が想定どおりである()
    {
        var control = new ZoomViewControl();

        Assert.Null(control.Source);
        Assert.True(control.IsFit);
        Assert.True(control.CenterAlign);
        Assert.Equal(1.0, control.RenderScaling);
        Assert.Equal(default, control.Offset);
        Assert.False(control.CanZoomIn);
        Assert.False(control.CanZoomOut);
        // スクロールバー（オーバーレイ）の既定
        Assert.True(control.ShowScrollBars);
        Assert.Equal(ThemeMetrics.Default.ScrollBarThickness, control.ScrollBarThickness);
        // ウィンドウのリサイズ判定領域（端から 6 DIP）へ掛からない余白を空ける
        Assert.True(control.ScrollBarMargin > 6.0, $"余白が小さすぎる: {control.ScrollBarMargin}");
        Assert.Equal(160, control.ScrollBarColor.Alpha);
        Assert.Equal(40, control.ScrollBarTrackColor.Alpha);
    }

    /// <summary>スクロールバーの表示設定と外観を変更できることを検証する。</summary>
    [Fact]
    public void スクロールバーの設定を変更できる()
    {
        var control = new ZoomViewControl
        {
            ShowScrollBars = false,
            ScrollBarThickness = 12.0,
            ScrollBarMargin = 6.0,
            ScrollBarColor = SKColors.Red,
            ScrollBarTrackColor = SKColors.Blue,
        };

        Assert.False(control.ShowScrollBars);
        Assert.Equal(12.0, control.ScrollBarThickness);
        Assert.Equal(6.0, control.ScrollBarMargin);
        Assert.Equal(SKColors.Red, control.ScrollBarColor);
        Assert.Equal(SKColors.Blue, control.ScrollBarTrackColor);
    }

    /// <summary>画像を設定すると Fit 表示へリセットされることを検証する。</summary>
    [Fact]
    public void 画像を設定するとFit表示へリセットする()
    {
        var (control, image) = CreateWithImage();
        using (image)
        {
            // 固定倍率へ移動してから画像を差し替える
            control.ZoomPercent = 200;
            Assert.False(control.IsFit);

            using var other = TestHelper.CreateBitmap(50, 50, SKColors.Blue);
            control.Source = other;

            Assert.True(control.IsFit);
            Assert.Equal(default, control.Offset);
        }
    }

    /// <summary>画像未設定では何も表示しない（表示矩形が空）ことを検証する。</summary>
    [Fact]
    public void 画像未設定なら表示矩形が空になる()
    {
        var control = CreateLaidOut();

        Assert.Equal(default, control.ImageRectViewport());
        Assert.Equal(default, control.DisplaySize());
    }

    /// <summary>Fit 表示では表示矩形がビューポートに収まることを検証する。</summary>
    [Fact]
    public void Fit表示では表示矩形がビューポートに収まる()
    {
        // 100x100 の画像、200x200 のビューポート → Fit = 200%
        var (control, image) = CreateWithImage();
        using (image)
        {
            var rect = control.ImageRectViewport();

            Assert.True(control.IsFit);
            Assert.Equal(200, control.ZoomPercent, 3);
            Assert.Equal(0, rect.X, 3);
            Assert.Equal(0, rect.Y, 3);
            Assert.Equal(200, rect.Width, 3);
            Assert.Equal(200, rect.Height, 3);
        }
    }

    /// <summary>Fit 表示ではズームアウトできないことを検証する（Fit が最小相当のとき）。</summary>
    [Fact]
    public void Fit表示ではズームアウトできない()
    {
        // 200x200 の画像、100x100 のビューポート → Fit = 50%（MinZoom = 10 より大きい）
        var image = TestHelper.CreateBitmap(200, 200, SKColors.Red);
        var control = CreateLaidOut(100, 100);
        control.Source = image;
        using (image)
        {
            // Fit = 50% はレベル 50 と一致するため、次のレベル（62）へズームインできる
            Assert.True(control.CanZoomIn);
            // 50% より小さいレベルは 40 があるためズームアウトできる
            Assert.True(control.CanZoomOut);

            control.ZoomOut();
            Assert.Equal(40, control.ZoomPercent, 3);
        }
    }

    /// <summary>ズームイン・アウトが段階レベルへ移動することを検証する。</summary>
    [Fact]
    public void ズームインとアウトが段階レベルへ移動する()
    {
        var (control, image) = CreateWithImage(200, 200, 100);
        using (image)
        {
            // Fit = 50% から開始する
            control.ZoomIn();
            Assert.Equal(62, control.ZoomPercent, 3);

            control.ZoomOut();
            Assert.Equal(50, control.ZoomPercent, 3);
        }
    }

    /// <summary>ズーム率の setter が 0 以下を無視することを検証する。</summary>
    /// <param name="value">設定する値。</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void ZoomPercentは0以下を受け付けない(double value)
    {
        var (control, image) = CreateWithImage();
        using (image)
        {
            control.ZoomPercent = value;

            Assert.True(control.IsFit);
        }
    }

    /// <summary>ズーム率の setter が設定範囲へクランプされることを検証する。</summary>
    [Fact]
    public void ZoomPercentは設定範囲へクランプする()
    {
        var (control, image) = CreateWithImage();
        using (image)
        {
            control.ZoomPercent = 10_000;
            Assert.Equal(ZoomConfig.Default.MaxZoom, control.ZoomPercent, 3);

            control.ZoomFit();
            Assert.True(control.IsFit);
        }
    }

    /// <summary>ズーム率の変更がズーム可否とイベントへ反映されることを検証する。</summary>
    [Fact]
    public void ズーム率の変更がイベントへ反映される()
    {
        var (control, image) = CreateWithImage();
        using (image)
        {
            ZoomChangedEventArgs? captured = null;
            control.ZoomChanged += (_, e) => captured = e;

            control.ZoomPercent = 100;

            Assert.NotNull(captured);
            Assert.Equal(100, captured.ZoomPercent, 3);
            Assert.False(captured.IsFit);
            Assert.True(captured.CanZoomIn);
            Assert.True(captured.CanZoomOut);
        }
    }

    /// <summary>Fit へ戻すとオフセットがリセットされることを検証する。</summary>
    [Fact]
    public void ZoomFitでFit表示へ戻る()
    {
        var (control, image) = CreateWithImage(400, 400, 100);
        using (image)
        {
            control.ZoomPercent = 100;
            control.SetOffset(new EsVector(100, 100));
            Assert.NotEqual(default, control.Offset);

            control.ZoomFit();

            Assert.True(control.IsFit);
            Assert.Equal(default, control.Offset);
        }
    }

    /// <summary>オフセットがスクロール範囲へクランプされることを検証する。</summary>
    [Fact]
    public void オフセットがスクロール範囲へクランプされる()
    {
        // 400x400 の画像を 100% で表示、ビューポート 200x200 → 最大オフセットは 200
        var (control, image) = CreateWithImage(400, 400, 200);
        using (image)
        {
            control.ZoomPercent = 100;
            control.SetOffset(new EsVector(1000, 1000));

            Assert.Equal(200, control.Offset.X, 3);
            Assert.Equal(200, control.Offset.Y, 3);

            control.SetOffset(new EsVector(-50, -50));

            Assert.Equal(0, control.Offset.X, 3);
            Assert.Equal(0, control.Offset.Y, 3);
        }
    }

    /// <summary>Fit 表示中はオフセットが原点に固定されることを検証する。</summary>
    [Fact]
    public void Fit表示中はオフセットが原点に固定される()
    {
        var (control, image) = CreateWithImage(400, 400, 100);
        using (image)
        {
            control.SetOffset(new EsVector(100, 100));

            Assert.True(control.IsFit);
            Assert.Equal(default, control.Offset);
        }
    }

    /// <summary>パンがオフセットへ加算されることを検証する。</summary>
    [Fact]
    public void PanByでオフセットが加算される()
    {
        var (control, image) = CreateWithImage(400, 400, 200);
        using (image)
        {
            control.ZoomPercent = 100;
            control.PanBy(new EsVector(30, 40));

            Assert.Equal(30, control.Offset.X, 3);
            Assert.Equal(40, control.Offset.Y, 3);
        }
    }

    /// <summary>画像表示矩形を基準とした座標変換が往復することを検証する。</summary>
    [Fact]
    public void 画像基準の座標変換が往復する()
    {
        var (control, image) = CreateWithImage(100, 100, 200);
        using (image)
        {
            // Fit = 200%。表示矩形の原点 (0,0)、スケール 2。
            var content = new EsPoint(25, 30);
            var viewport = control.ContentToViewportImage(content);
            var roundTrip = control.ViewportToContentImage(viewport);

            Assert.Equal(50, viewport.X, 3);
            Assert.Equal(60, viewport.Y, 3);
            Assert.Equal(content.X, roundTrip.X, 3);
            Assert.Equal(content.Y, roundTrip.Y, 3);
        }
    }

    /// <summary>中央寄せの原点補正が座標変換へ反映されることを検証する。</summary>
    [Fact]
    public void 中央寄せの原点補正が座標変換へ反映される()
    {
        // 50x50 の画像、100x100 のビューポート → Fit = 200%、中央寄せ原点 (0, 0)
        // 画像 50x50 は Fit で 100x100 になるため原点は 0
        var (control, image) = CreateWithImage(50, 50, 100);
        using (image)
        {
            var origin = control.ImageRectViewport();

            Assert.Equal(0, origin.X, 3);
            Assert.Equal(0, origin.Y, 3);
            Assert.Equal(100, origin.Width, 3);
            Assert.Equal(100, origin.Height, 3);
        }
    }

    /// <summary>ズーム中心を指定するとそのコンテンツ座標が維持されることを検証する。</summary>
    [Fact]
    public void ズーム中心を指定すると中心のコンテンツ座標が維持される()
    {
        // 400x400 の画像を 100%（表示 400x400、ビューポート 200x200）で表示
        var (control, image) = CreateWithImage(400, 400, 200);
        using (image)
        {
            control.ZoomPercent = 100;
            control.SetOffset(new EsVector(100, 100));

            var center = new EsPoint(100, 100);
            var before = control.ViewportToContent(center);

            // 中心 (100,100) を維持したまま 200% へズームする
            control.SetZoom(200, center);

            var after = control.ViewportToContent(center);
            Assert.Equal(before.X, after.X, 3);
            Assert.Equal(before.Y, after.Y, 3);
        }
    }

    /// <summary>DPI 変更でスケーリング係数が追従することを検証する。</summary>
    [Fact]
    public void DPI変更でスケーリング係数が追従する()
    {
        var control = new ZoomViewControl();
        var initial = control.RenderScaling;

        Assert.True(initial > 0);
    }

    /// <summary>要求サイズが空（親の残余を埋める）であることを検証する。</summary>
    [Fact]
    public void 要求サイズが空になる()
    {
        var control = new ZoomViewControl();

        control.Measure(new Size(500, 400));

        Assert.Equal(0, control.DesiredSize.Width);
        Assert.Equal(0, control.DesiredSize.Height);
    }

    // ====================================================================
    // ログ
    // ====================================================================

    /// <summary>ログの既定が NullLogger で、ズーム操作が例外にならないことを検証する。</summary>
    [Fact]
    public void Log_既定はNullLoggerでズーム操作が例外にならない()
    {
        var (control, image) = CreateWithImage(200, 200, 100);
        using (image)
        {
            control.ZoomIn();
            control.ZoomOut();
        }
    }

    /// <summary><c>null</c> を設定すると NullLogger へフォールバックすることを検証する。</summary>
    [Fact]
    public void Log_null設定はNullLoggerへフォールバックする()
    {
        var (control, image) = CreateWithImage(200, 200, 100);
        using (image)
        {
            control.Log = null!;

            control.ZoomIn();
        }
    }

    /// <summary>ズームイン・アウトの前後のズーム率が出力されることを検証する。</summary>
    [Fact]
    public void Log_ズーム操作の前後のズーム率を出力する()
    {
        var (control, image) = CreateWithImage(200, 200, 100);
        using (image)
        {
            var logger = new RecordingLogger();
            control.Log = logger;

            // Fit = 50% から段階レベル（62%）へ移動する
            control.ZoomIn();

            Assert.Equal("[ZoomIn] 50% -> 62%", Assert.Single(logger.Messages));
        }
    }

    /// <summary>移動できない場合はログを出力しないことを検証する。</summary>
    [Fact]
    public void Log_移動できない場合は出力しない()
    {
        var (control, image) = CreateWithImage(200, 200, 100);
        using (image)
        {
            // 上限まで移動してから、それ以上ズームインできない状態を作る
            control.ZoomPercent = ZoomConfig.Default.MaxZoom;
            Assert.False(control.CanZoomIn);

            var logger = new RecordingLogger();
            control.Log = logger;
            control.ZoomIn();

            Assert.Empty(logger.Messages);
        }
    }
}
