using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace Estable.Loupe.Tests;

/// <summary><see cref="Loupe"/>（公開ファサード）の単体テスト（表示レイアウト・描画入力・通知内容・設定）。</summary>
/// <remarks>
/// UI 層とホストが使う唯一の窓口であるファサードの振る舞いを、UI フレームワークなしで検証する。<br/>
/// クランプ・位置計算そのもの（純粋計算）は <see cref="LoupePositionCalculator"/> のテストが検証するため、<br/>
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
        Assert.Equal(LoupeValueCoercer.RenderScalingDefault, loupe.RenderScaling);
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
        // 補正の規則は LoupeValueCoercer が単一所有する（UI の依存関係プロパティを経由しない代入でも範囲内になる）
        var loupe = new Loupe { RenderScaling = input };

        Assert.Equal(expected, loupe.RenderScaling);
    }

    // ====================================================================
    // 表示サイズ（親領域へのクランプ）
    // ====================================================================

    [Fact]
    public void DisplaySize_親領域未通知は要求サイズを使う()
    {
        var loupe = new Loupe(new LoupeOptions { RequestedSize = 200 });

        Assert.Equal(200, loupe.DisplaySize);
    }

    [Fact]
    public void DisplaySize_親領域の最小辺へクランプする()
    {
        var loupe = new Loupe(new LoupeOptions { RequestedSize = 200 });

        loupe.ResolveLayout(new EsSize(120, 300));

        Assert.Equal(120, loupe.DisplaySize);
    }

    [Fact]
    public void DisplaySize_親領域が未確定ならクランプしない()
    {
        var loupe = new Loupe(new LoupeOptions { RequestedSize = 200 });

        loupe.ResolveLayout(default);

        Assert.Equal(200, loupe.DisplaySize);
    }

    [Fact]
    public void DisplaySize_設定変更で再導出される()
    {
        var loupe = new Loupe(new LoupeOptions { RequestedSize = 200 });
        loupe.ResolveLayout(ParentSize);

        // 要求サイズと親領域の小さい方（親領域 400x300 なので要求サイズ 400 がクランプされる）
        loupe.Options = loupe.Options with { RequestedSize = 400 };

        Assert.Equal(300, loupe.DisplaySize);
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
        var loupe = new Loupe { CenterPoint = new EsPoint(50, 60), PositionOffset = 20 };

        var layout = loupe.ResolveLayout(ParentSize);

        Assert.Equal(new EsPoint(70, 80), layout.Position);
    }

    [Fact]
    public void ResolveLayout_親領域の外へは配置しない()
    {
        var loupe = new Loupe { CenterPoint = new EsPoint(390, 290) };

        var layout = loupe.ResolveLayout(ParentSize);

        // 親領域 400x300・表示サイズ 160 では左上の上限が (240, 140) になる
        Assert.Equal(new EsPoint(240, 140), layout.Position);
    }

    [Fact]
    public void ResolveLayout_親領域が未確定ならクランプしない()
    {
        var loupe = new Loupe { CenterPoint = new EsPoint(500, 600) };

        var layout = loupe.ResolveLayout(default);

        Assert.Equal(new EsPoint(500, 600), layout.Position);
    }

    [Fact]
    public void ResolveLayout_座標変換ポートを適用する()
    {
        var loupe = new Loupe { CenterPoint = new EsPoint(10, 20) };
        loupe.SetCoordinateTransform(CreateDoublingPort());

        var layout = loupe.ResolveLayout(ParentSize);

        Assert.Equal(new EsPoint(20, 40), layout.Position);
    }

    [Fact]
    public void ResolveLayout_ポート未設定は等価変換になる()
    {
        var loupe = new Loupe { CenterPoint = new EsPoint(10, 20) };

        loupe.SetCoordinateTransform(null);

        var layout = loupe.ResolveLayout(ParentSize);

        Assert.Equal(new EsPoint(10, 20), layout.Position);
    }

    // ====================================================================
    // 描画入力
    // ====================================================================

    [Fact]
    public void CreateRequest_現在の状態から描画入力を組み立てる()
    {
        var appearance = new LoupeAppearance { CheckerSize = 8 };
        var loupe = new Loupe(new LoupeOptions { RequestedSize = 160, ZoomLevel = 4, Appearance = appearance })
        {
            CenterPoint = new EsPoint(10, 20),
            RenderScaling = 1.5,
        };

        var request = loupe.CreateRequest();

        Assert.Equal(new EsPoint(10, 20), request.Center);
        Assert.Equal(240, request.PixelSize);
        Assert.Equal(4, request.ZoomLevel);
        Assert.Equal(1.5, request.Scaling);
        Assert.Same(appearance, request.Appearance);
    }

    [Fact]
    public void CreateRequest_描画先の領域へクランプする()
    {
        var loupe = new Loupe(new LoupeOptions { RequestedSize = 300 });

        var request = loupe.CreateRequest(new EsSize(200, 150));

        Assert.Equal(150, request.PixelSize);
    }

    [Fact]
    public void CreateRequest_親領域のクランプを反映する()
    {
        var loupe = new Loupe(new LoupeOptions { RequestedSize = 300 });
        loupe.ResolveLayout(new EsSize(120, 400));

        var request = loupe.CreateRequest();

        // 表示サイズ（DIP）が親領域の最小辺へクランプされ、その値が物理ピクセルへ換算される
        Assert.Equal(120, request.PixelSize);
    }

    [Fact]
    public void CreateRequest_中心未設定でも組み立てられる()
    {
        var loupe = new Loupe();

        var request = loupe.CreateRequest();

        Assert.Null(request.Center);
    }

    // ====================================================================
    // 描画完了通知の内容
    // ====================================================================

    [Fact]
    public void ResolveNotification_切り出し領域とスケールと座標ラベルを返す()
    {
        var loupe = new Loupe(new LoupeOptions { RequestedSize = 160, ZoomLevel = 4 })
        {
            CenterPoint = new EsPoint(50, 60),
        };
        var request = loupe.CreateRequest();

        var notification = loupe.ResolveNotification(request);

        // 切り出し一辺 = 160 / 4 = 40 のため、左上は中心から 20 引いた位置になる
        Assert.Equal(new EsPoint(30, 40), notification.CaptureTopLeft);
        Assert.Equal(4, notification.Scale);
        Assert.NotNull(notification.CoordLabel);
        Assert.Equal("(50, 60)", notification.CoordLabel!.Value.Text);
        // 表示サイズは DIP（物理ピクセル 160 をスケーリング 1.0 で換算した値）
        Assert.Equal(160, notification.CoordLabel.Value.DisplaySize);
    }

    [Fact]
    public void ResolveNotification_中心未設定なら切り出し領域とラベルを返さない()
    {
        var loupe = new Loupe();
        var request = loupe.CreateRequest();

        var notification = loupe.ResolveNotification(request);

        Assert.Null(notification.CaptureTopLeft);
        Assert.Null(notification.CoordLabel);
        Assert.Equal(4, notification.Scale);
    }

    [Fact]
    public void ResolveNotification_ラベル非表示の設定ではラベルを返さない()
    {
        var options = new LoupeOptions { Appearance = new LoupeAppearance { ShowCoordLabel = false } };
        var loupe = new Loupe(options) { CenterPoint = new EsPoint(50, 60) };
        var request = loupe.CreateRequest();

        var notification = loupe.ResolveNotification(request);

        Assert.Null(notification.CoordLabel);
        Assert.NotNull(notification.CaptureTopLeft);
    }

    [Fact]
    public void ResolveNotification_ラベルの寸法はDIPのまま返す()
    {
        var loupe = new Loupe(new LoupeOptions { RequestedSize = 160 })
        {
            CenterPoint = new EsPoint(50, 60),
            RenderScaling = 2.0,
        };
        var request = loupe.CreateRequest();

        var notification = loupe.ResolveNotification(request);

        // テキスト要素として配置するため、フォントサイズは設定（DIP）のまま渡す
        Assert.Equal(12.0, notification.CoordLabel!.Value.FontSize);
        // 配置の基準は物理ピクセルを DIP へ換算した表示サイズ（160 DIP の要求が 2 倍の物理ピクセルになり、DIP へ戻る）
        Assert.Equal(160.0, notification.CoordLabel.Value.DisplaySize);
    }

    [Fact]
    public void ResolveNotification_描く層は下地から中心座標ラベルの順で返す()
    {
        var loupe = new Loupe { CenterPoint = new EsPoint(50, 60) };
        var request = loupe.CreateRequest();

        var notification = loupe.ResolveNotification(request);

        // 描画順はコアが単一所有する（UI 層は先頭から順に処理するだけ）
        Assert.Equal(
            [
                LoupeDrawLayer.Background,
                LoupeDrawLayer.Checkerboard,
                LoupeDrawLayer.Image,
                LoupeDrawLayer.Crosshair,
                LoupeDrawLayer.Frame,
                LoupeDrawLayer.CoordLabel,
            ],
            notification.Layers);
    }

    [Fact]
    public void ResolveNotification_ラベルを描かない場合はラベルの層を含めない()
    {
        var options = new LoupeOptions { Appearance = new LoupeAppearance { ShowCoordLabel = false } };
        var loupe = new Loupe(options) { CenterPoint = new EsPoint(50, 60) };
        var request = loupe.CreateRequest();

        var notification = loupe.ResolveNotification(request);

        Assert.Equal(5, notification.Layers.Count);
        Assert.DoesNotContain(LoupeDrawLayer.CoordLabel, notification.Layers);
    }
}
