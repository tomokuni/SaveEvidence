using EsUtil.Platform.Core.Geometry;
using Xunit;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Session;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.Tests.Session;

/// <summary><see cref="CaptureVirtualScreen.ResolveBounds"/> の仕様テスト（撮影対象の範囲）。</summary>
/// <remarks>
/// 「撮影は全モニタ（仮想スクリーン）を対象にする」「モニタが無ければ撮影しない」という規則を固定する。<br/>
/// 座標は画面物理ピクセルで、プライマリモニタが (0, 0) にない場合は負値を含む。
/// </remarks>
public class CaptureVirtualScreenBoundsTests
{
    [Fact]
    internal void ResolveBounds_単一モニタは自身の矩形を返す()
    {
        var result = CaptureVirtualScreen.ResolveBounds([new EsRectInt(0, 0, 1920, 1080)]);

        Assert.Equal(new EsRectInt(0, 0, 1920, 1080), result);
    }

    [Fact]
    internal void ResolveBounds_複数モニタを包含する最小の矩形を返す()
    {
        // 左（負座標）と右下にモニタがある構成
        var result = CaptureVirtualScreen.ResolveBounds(
        [
            new EsRectInt(-1920, 0, 1920, 1080),
            new EsRectInt(0, 0, 1920, 1080),
            new EsRectInt(1920, 200, 1280, 1024),
        ]);

        Assert.Equal(new EsRectInt(-1920, 0, 5120, 1224), result);
    }

    [Fact]
    internal void ResolveBounds_負の原点でも包含矩形を返す()
    {
        // セカンダリが上（負の Y）にある構成
        var result = CaptureVirtualScreen.ResolveBounds(
        [
            new EsRectInt(0, 0, 3840, 2160),
            new EsRectInt(0, -1200, 1920, 1200),
        ]);

        Assert.Equal(new EsRectInt(0, -1200, 3840, 3360), result);
    }

    [Fact]
    internal void ResolveBounds_モニタが無い場合はnullを返す()
    {
        // 撮影する領域が無い（オーバーレイを表示する意味もない）
        Assert.Null(CaptureVirtualScreen.ResolveBounds([]));
        Assert.Null(CaptureVirtualScreen.ResolveBounds(null));
    }

    [Fact]
    internal void ResolveBounds_入力を変更しない()
    {
        // 引数の列挙を 1 度だけ辿る（複数回の列挙で結果が変わらない実装にする）
        var screens = new List<EsRectInt>
        {
            new(-1920, 0, 1920, 1080),
            new(0, 0, 1920, 1080),
        };

        var first = CaptureVirtualScreen.ResolveBounds(screens);
        var second = CaptureVirtualScreen.ResolveBounds(screens);

        Assert.Equal(first, second);
        Assert.Equal(2, screens.Count);
    }
}

