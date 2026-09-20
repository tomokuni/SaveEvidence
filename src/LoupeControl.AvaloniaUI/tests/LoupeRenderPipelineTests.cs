using Avalonia;
using Estable.AvaloniaUI.Platform.Imaging;
using Estable.Platform.Imaging;
using Xunit;

namespace Estable.AvaloniaUI.Loupe.Tests;

/// <summary><see cref="LoupeRenderPipeline.DecideRenderMode"/> の純粋計算テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 描画モード決定（市松模様のみ / ソース画像合成）の分岐ロジックのみを検証するため、<br/>
/// Avalonia ヘッドレス（<c>[AvaloniaFact]</c>）に依存せず <c>[Fact]</c> で並列実行する。<br/>
/// <see cref="Bitmap"/> 由来の判定（破棄済み・未対応型）はヘッドレスが必要なため <see cref="LoupeControlTests"/>（ヘッドレス直列）が担う。
/// </remarks>
public class LoupeRenderPipelineTests
{
    // ====================================================================
    // DecideRenderMode（描画モード決定）
    // ====================================================================

    [Fact]
    public void DecideRenderMode_中心未設定は市松模様のみである()
    {
        // center 未設定 → 市松模様のみ（ソースがあっても抽出しない）
        var mode = LoupeRenderPipeline.DecideRenderMode(source: null, center: null);

        Assert.Equal(LoupeRenderPipeline.RenderMode.CheckerboardOnly, mode);
    }

    [Fact]
    public void DecideRenderMode_ソースnullでも中心があれば合成できる()
    {
        // source=null（画像なし）でも resolver は「対応可能（null = 画像なし）」のため Composite。
        // 実際の描画では Resolve が null を返し、市松模様のみへフォールバックする（パイプライン内で処理）。
        var mode = LoupeRenderPipeline.DecideRenderMode(source: null, center: new Point(50, 50));

        Assert.Equal(LoupeRenderPipeline.RenderMode.Composite, mode);
    }

    [Fact]
    public void DecideRenderMode_EsBitmapAvaloniaImageと中心があれば合成できる()
    {
        using var bmp = EsBitmap.Create(100, 100);
        using var image = new EsBitmapAvaloniaImage(bmp);

        var mode = LoupeRenderPipeline.DecideRenderMode(image, new Point(50, 50));

        Assert.Equal(LoupeRenderPipeline.RenderMode.Composite, mode);
    }

    [Fact]
    public void DecideRenderMode_中心未設定はソースに関係なく市松模様のみである()
    {
        using var bmp = EsBitmap.Create(100, 100);
        using var image = new EsBitmapAvaloniaImage(bmp);

        var mode = LoupeRenderPipeline.DecideRenderMode(image, center: null);

        Assert.Equal(LoupeRenderPipeline.RenderMode.CheckerboardOnly, mode);
    }
}
