using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using Xunit;
using EsUtil.Feature.LoupeCore.Setting;
using EsUtil.Feature.LoupeCore.Drawing;

namespace EsUtil.Feature.LoupeCore.Tests;

/// <summary><see cref="Loupe"/>（公開ファサード）の単体テスト（表示レイアウト・描画入力・通知内容・設定）。</summary>
/// <remarks>
/// UI 層とホストが使う唯一の窓口であるファサードの振る舞いを、UI フレームワークなしで検証する。<br/>
/// 位置計算そのもの（純粋計算）は <see cref="LoupePositionTests"/> のテストが検証するため、<br/>
/// 本テストは「設定からの解決」「ポートとオフセットの適用」「描画入力・通知内容の組み立て」に焦点を当てる。
/// </remarks>
public class LoupeTests
{
    /// <summary>親領域のサイズ（表示サイズのクランプ基準）。</summary>
    private static readonly EsSize ParentSize = new(400, 300);

    /// <summary>ドメイン → 親領域 DIP を 2 倍で返す座標変換ポートを生成する。</summary>
    /// <returns>Inverse が 2 倍の座標変換ポート。</returns>
    private static ICoordTransform CreateDoublingPort()
        => new CoordTransform(p => new EsPoint(p.X / 2, p.Y / 2), p => new EsPoint(p.X * 2, p.Y * 2), () => null);

    // ====================================================================
    // 生成・設定
    // ====================================================================

    [Fact]
    public void 生成_既定の設定を保持する()
    {
        var loupe = new Loupe();

        Assert.Same(LoupeOptions.Default, loupe.Options);
        Assert.Equal(Loupe.Limits.RenderScalingDefault, loupe.RenderScaling);
        Assert.Null(loupe.CenterPoint);
        Assert.Equal(0.0, loupe.PositionOffset);
    }

    [Fact]
    public void 生成_指定した設定を保持する()
    {
        var options = new LoupeOptions { RequestedSize = 200, ZoomLevel = 8 };

        var loupe = new Loupe(options);

        Assert.Equal(200, loupe.Options.RequestedSize);
        Assert.Equal(8, loupe.Options.ZoomLevel);
    }

    [Theory]
    [InlineData(0.0, 0.1)]
    [InlineData(-1.0, 0.1)]
    [InlineData(100.0, 8.0)]
    [InlineData(double.NaN, 1.0)]
    [InlineData(double.PositiveInfinity, 1.0)]
    public void RenderScaling_安全域外の指定は補正して保持する(double input, double expected)
    {
        // 補正の規則は Loupe.Limits が単一所有する（UI の依存関係プロパティを経由しない代入でも範囲内になる）
        var loupe = new Loupe() { RenderScaling = input };

        Assert.Equal(expected, loupe.RenderScaling);
    }

    // ====================================================================
    // 表示サイズ（親領域へのクランプ）
    // ====================================================================

    [Fact]
    public void ResolveLayout_要求サイズを親領域の最小辺へクランプする()
    {
        var loupe = new Loupe(new LoupeOptions { RequestedSize = 200 });

        var layout = loupe.ResolveLayout(new EsSize(120, 300));

        Assert.Equal(120, layout.DisplaySize);
    }

    [Fact]
    public void ResolveLayout_親領域が未確定なら要求サイズを表示サイズにする()
    {
        var loupe = new Loupe(new LoupeOptions { RequestedSize = 200 });

        var layout = loupe.ResolveLayout(default);

        Assert.Equal(200, layout.DisplaySize);
    }

    [Fact]
    public void ResolveLayout_設定変更で表示サイズを再導出する()
    {
        var loupe = new Loupe(new LoupeOptions { RequestedSize = 200 });

        // 要求サイズと親領域の小さい方（親領域 400x300 なので要求サイズ 400 がクランプされる）
        loupe.Options = loupe.Options with { RequestedSize = 400 };

        Assert.Equal(300, loupe.ResolveLayout(ParentSize).DisplaySize);
    }

    // ====================================================================
    // 表示レイアウト（位置）
    // ====================================================================

    [Fact]
    public void ResolveLayout_中心未設定なら配置しない()
    {
        var loupe = new Loupe();

        var layout = loupe.ResolveLayout(ParentSize);

        Assert.Equal(160, layout.DisplaySize);
        Assert.Null(layout.Position);
    }

    [Fact]
    public void ResolveLayout_中心とオフセットから位置を求める()
    {
        var loupe = new Loupe() { CenterPoint = new EsPoint(50, 60), PositionOffset = 20 };

        var layout = loupe.ResolveLayout(ParentSize);

        Assert.Equal(new EsPoint(70, 80), layout.Position);
    }

    [Fact]
    public void ResolveLayout_親領域の外へは配置しない()
    {
        var loupe = new Loupe() { CenterPoint = new EsPoint(390, 290) };

        var layout = loupe.ResolveLayout(ParentSize);

        // 親領域 400x300・表示サイズ 160 では左上の上限が (240, 140) になる
        Assert.Equal(new EsPoint(240, 140), layout.Position);
    }

    [Fact]
    public void ResolveLayout_親領域が未確定ならクランプしない()
    {
        var loupe = new Loupe() { CenterPoint = new EsPoint(500, 600) };

        var layout = loupe.ResolveLayout(default);

        Assert.Equal(new EsPoint(500, 600), layout.Position);
    }

    [Fact]
    public void ResolveLayout_座標変換ポートを適用する()
    {
        var loupe = new Loupe() { CenterPoint = new EsPoint(10, 20) };
        loupe.SetCoordinateTransform(CreateDoublingPort());

        var layout = loupe.ResolveLayout(ParentSize);

        Assert.Equal(new EsPoint(20, 40), layout.Position);
    }

    [Fact]
    public void ResolveLayout_ポート未設定は等価変換になる()
    {
        var loupe = new Loupe() { CenterPoint = new EsPoint(10, 20) };

        loupe.SetCoordinateTransform(null);

        var layout = loupe.ResolveLayout(ParentSize);

        Assert.Equal(new EsPoint(10, 20), layout.Position);
    }

    // ====================================================================
    // 描画入力
    // ====================================================================

    [Fact]
    public void CreateRequest_表示サイズと設定から描画入力を組み立てる()
    {
        var appearance = new LoupeMetrics { CheckerSize = 8 };
        var loupe = new Loupe(new LoupeOptions { RequestedSize = 160, ZoomLevel = 4, Metrics = appearance })
        {
            CenterPoint = new EsPoint(10, 20),
            RenderScaling = 1.5,
        };

        var request = loupe.CreateRequest(160);

        Assert.Equal(new EsPoint(10, 20), request.Center);
        Assert.Equal(240, request.PixelSize);
        Assert.Equal(4, request.ZoomLevel);
        Assert.Equal(1.5, request.Scaling);
        Assert.Same(appearance, request.Metrics);
    }

    [Fact]
    public void CreateRequest_描画先の領域へクランプする()
    {
        var loupe = new Loupe(new LoupeOptions { RequestedSize = 300 });

        var request = loupe.CreateRequest(300, new EsSize(200, 150));

        Assert.Equal(150, request.PixelSize);
    }

    [Fact]
    public void CreateRequest_レイアウトのクランプ結果を物理ピクセルへ換算する()
    {
        var loupe = new Loupe(new LoupeOptions { RequestedSize = 300 });

        var layout = loupe.ResolveLayout(new EsSize(120, 400));
        var request = loupe.CreateRequest(layout.DisplaySize);

        // 表示サイズ（DIP）が親領域の最小辺へクランプされ、その値が物理ピクセルへ換算される
        Assert.Equal(120, request.PixelSize);
    }

    [Fact]
    public void CreateRequest_中心未設定でも組み立てられる()
    {
        var loupe = new Loupe();

        var request = loupe.CreateRequest(160);

        Assert.Null(request.Center);
    }

    // ====================================================================
    // 描画完了通知の内容
    // ====================================================================

    [Fact]
    public void ResolveNotification_切り出し領域とスケールを返す()
    {
        var loupe = new Loupe(new LoupeOptions { RequestedSize = 160, ZoomLevel = 4 })
        {
            CenterPoint = new EsPoint(50, 60),
        };
        var request = loupe.CreateRequest(160);

        var notification = loupe.ResolveNotification(request);

        // 切り出し一辺 = 160 / 4 = 40 のため、左上は中心から 20 引いた位置になる
        Assert.Equal(new EsPoint(30, 40), notification.CaptureTopLeft);
        Assert.Equal(4, notification.Scale);
    }

    [Fact]
    public void ResolveNotification_中心未設定なら切り出し領域を返さない()
    {
        var loupe = new Loupe();
        var request = loupe.CreateRequest(160);

        var notification = loupe.ResolveNotification(request);

        Assert.Null(notification.CaptureTopLeft);
        Assert.Equal(4, notification.Scale);
    }

    [Fact]
    public void ResolveContent_描く層は下地からテキストの順で返す()
    {
        var loupe = new Loupe() { CenterPoint = new EsPoint(50, 60) };
        var content = loupe.ResolveContent(loupe.CreateRequest(160));

        // 描画順はコアが単一所有する（UI 層は先頭から順に処理するだけ）
        Assert.Equal(
            [
                LoupeDrawLayer.Background,
                LoupeDrawLayer.Checkerboard,
                LoupeDrawLayer.Image,
                LoupeDrawLayer.Crosshair,
                LoupeDrawLayer.Frame,
                LoupeDrawLayer.Label,
            ],
            content.Layers);
    }

    [Fact]
    public void ResolveContent_テキストが無ければ層に含まれない()
    {
        // 拡大中心が無ければ中心座標ラベルは表示されない
        var loupe = new Loupe();
        var content = loupe.ResolveContent(loupe.CreateRequest(160));

        Assert.Null(content.CoordLabelText);
        Assert.DoesNotContain(LoupeDrawLayer.Label, content.Layers);
    }

    [Fact]
    public void ResolveContent_層の一覧を共有する()
    {
        var loupe = new Loupe() { CenterPoint = new EsPoint(50, 60) };

        var first = loupe.ResolveContent(loupe.CreateRequest(160));
        var second = loupe.ResolveContent(loupe.CreateRequest(160));

        // 層の順序は固定であり、描画のたびに一覧を組み立てない
        Assert.Same(first.Layers, second.Layers);
    }

    /// <summary>スケールが切り出しサイズの切り上げ（描画先によるクランプを含む）に影響されないことを検証する。</summary>
    /// <param name="pixelSize">ルーペの一辺のサイズ（物理ピクセル）。</param>
    /// <remarks>
    /// 実描画は切り出し領域（切り出しサイズのドメインピクセル）を <c>zoomLevel</c> 倍で描くため、<br/>
    /// 変換係数は切り出しサイズで割った値（<c>物理ピクセル / 切り出しサイズ</c>）ではなく <c>zoomLevel</c> になる。
    /// </remarks>
    [Theory]
    [InlineData(160)]
    [InlineData(150)]
    [InlineData(200)]
    public void ResolveNotification_スケールは切り出しサイズの切り上げに影響されない(int pixelSize)
    {
        var loupe = new Loupe(new LoupeOptions { ZoomLevel = 4 }) { CenterPoint = new EsPoint(50, 60) };

        var notification = loupe.ResolveNotification(loupe.CreateRequest(pixelSize));

        Assert.Equal(4.0, notification.Scale, 6);
    }

    /// <summary>スケールが DPI スケーリング係数に依存しないことを検証する。</summary>
    /// <param name="renderScaling">表示 DIP → 物理ピクセルのスケーリング係数。</param>
    /// <remarks>
    /// 物理ピクセル数は DIP とスケーリングに依存するが、描画の倍率（ドメイン → 描画ピクセル）は変わらない。<br/>
    /// 破線オーバーレイの位置が DPI でずれないことを保証する。
    /// </remarks>
    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(2.0)]
    public void ResolveNotification_スケールはDPIスケーリングに依存しない(double renderScaling)
    {
        var loupe = new Loupe(new LoupeOptions { ZoomLevel = 4 }) { CenterPoint = new EsPoint(50, 60) };
        var pixelSize = DpiScaler.ScaleToPixel(160, renderScaling);

        var notification = loupe.ResolveNotification(loupe.CreateRequest(pixelSize));

        Assert.Equal(4.0, notification.Scale, 6);
    }

    /// <summary>表示レイアウト・描画入力・通知の解決がヒープを確保しないことを検証する。</summary>
    /// <remarks>
    /// 描画のたび（マウス移動・スクロール）に呼ばれるため、層の一覧を共有し値型だけで組み立てる。<br/>
    /// 計測は <see cref="GC.GetAllocatedBytesForCurrentThread"/> で行う。
    /// </remarks>
    [Fact]
    public void レイアウトと描画入力の解決はヒープを確保しない()
    {
        var loupe = new Loupe() { CenterPoint = new EsPoint(50, 60) };
        loupe.SetCoordinateTransform(CreateDoublingPort());

        for (var i = 0; i < 100; i++)
        {
            loupe.ResolveLayout(ParentSize);
            loupe.ResolveNotification(loupe.CreateRequest(160));
        }

        GC.Collect();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            loupe.ResolveLayout(ParentSize);
            loupe.ResolveNotification(loupe.CreateRequest(160));
        }

        var perCall = (GC.GetAllocatedBytesForCurrentThread() - before) / 1000.0;
        Assert.True(perCall <= 16, $"レイアウトと描画入力 1 回の確保が {perCall} バイト（16 バイト以下を期待）");
    }
}
