using Aprillz.MewUI;
using Aprillz.MewUI.Rendering;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.MewUI.Tests;

/// <summary>テストで共用するユーティリティ。</summary>
/// <remarks>
/// カテゴリごとのテスト（<c>Tests.Crop</c> / <c>Tests.SelectionOverlay</c> / <c>Tests.ZoomView</c>）は<br/>
/// 親名前空間（<c>Tests</c>）の本型をそのまま参照できる（同名クラスの複製を作らない）。
/// </remarks>
internal static class TestHelper
{
    /// <summary>バックエンドを登録済みかどうか。</summary>
    private static bool s_registered;

    /// <summary>テストの実行前にバックエンドを登録する（テキストの実寸測定もバックエンドを必要とするため）。</summary>
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void Initialize() => _ = Factory;

    /// <summary>グラフィックス ファクトリを取得する（初回に GDI バックエンドを登録する）。</summary>
    /// <value>登録済みのファクトリ。</value>
    /// <remarks>
    /// <b>なぜ GDI か</b>: 画素を読み戻せるのは GDI の CPU 表面だけである（Direct2D と MewVG の
    /// オフスクリーンは読み戻せない、または OpenGL の現在のコンテキストを要求する）。
    /// </remarks>
    public static IGraphicsFactory Factory
    {
        get
        {
            if (!s_registered)
            {
                GdiBackend.Register();
                s_registered = true;
            }

            return Application.DefaultGraphicsFactory;
        }
    }

    /// <summary>指定サイズの単色画像（読み取りの面）を生成する。</summary>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <param name="color">塗りつぶす色。</param>
    /// <returns>生成した画像（読み取りの面）。 破棄は呼び出し側。</returns>
    /// <remarks>
    /// テスト画像は生成後に書き換えない（表示・描画の入力）ため、凍結して 0 コピーで読み取り専用の面へ渡す。
    /// </remarks>
    public static EsReadOnlyBitmap CreateBitmap(int width, int height, DrawingColor color)
    {
        using var created = EsBitmap.Create(width, height, color);
        created.SetReadOnly();
        return EsReadOnlyBitmap.FromBitmap(created);
    }

    /// <summary>描画先（描画コンテキストとその画素を読む手段）を生成する。</summary>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <returns>生成した描画先。 破棄は呼び出し側。</returns>
    public static RenderTarget CreateTarget(int width, int height) => new(width, height);
}

/// <summary>テスト用の描画先（描画コンテキストと画素読み取り）。</summary>
/// <remarks>
/// ウィンドウを必要としないため UI 初期化なしで検証できる。<br/>
/// 画素の読み取りは<b>最初の要求時に 1 回だけ</b>行う（フレームを閉じてから表面を読み戻す）。
/// </remarks>
internal sealed class RenderTarget : IDisposable
{
    /// <summary>描画先の表面。</summary>
    private readonly IRenderSurface _renderTarget;

    /// <summary>描画コンテキスト。</summary>
    private readonly IGraphicsContext _context;

    /// <summary>読み戻した画素（未読み取りの場合は null）。</summary>
    private byte[]? _pixels;

    /// <summary>行幅（バイト）。</summary>
    private readonly int _stride;

    /// <summary>指定サイズの描画先を初期化する（透明で初期化する）。</summary>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    public RenderTarget(int width, int height)
    {
        var factory = TestHelper.Factory;
        _renderTarget = factory.CreateSurface(RenderSurfaceDescriptor.CpuPixels(width, height, 1.0, true, "test"));
        _context = factory.CreateContext(_renderTarget);
        _context.BeginFrame(_renderTarget);
        _context.Clear(Color.Transparent);
        _stride = width * 4;
        Width = width;
        Height = height;
    }

    /// <summary>幅（ピクセル）。</summary>
    public int Width { get; }

    /// <summary>高さ（ピクセル）。</summary>
    public int Height { get; }

    /// <summary>描画に使用するコンテキスト。</summary>
    public IGraphicsContext Canvas => _context;

    /// <summary>指定座標の画素色を取得する（アルファは無視する）。</summary>
    /// <param name="x">X 座標。</param>
    /// <param name="y">Y 座標。</param>
    /// <returns>画素色。</returns>
    public DrawingColor GetPixel(int x, int y)
    {
        var pixels = Read();
        var i = (y * _stride) + (x * 4);
        return DrawingColor.FromArgb(255, pixels[i + 2], pixels[i + 1], pixels[i]);
    }


    /// <summary>指定座標の画素のアルファを取得する。</summary>
    /// <param name="x">X 座標。</param>
    /// <param name="y">Y 座標。</param>
    /// <returns>アルファ（0〜255）。</returns>
    public int GetAlpha(int x, int y) => Read()[(y * _stride) + (x * 4) + 3];

    /// <summary>指定領域に指定色の画素が何個あるかを数える。</summary>
    /// <param name="x0">左上 X 座標。</param>
    /// <param name="y0">左上 Y 座標。</param>
    /// <param name="x1">右下 X 座標（含まない）。</param>
    /// <param name="y1">右下 Y 座標（含まない）。</param>
    /// <param name="color">数える色。</param>
    /// <returns>一致する画素数。</returns>
    public int CountColor(int x0, int y0, int x1, int y1, DrawingColor color)
    {
        var pixels = Read();
        var count = 0;
        for (var y = y0; y < y1; y++)
        {
            for (var x = x0; x < x1; x++)
            {
                var i = (y * _stride) + (x * 4);
                if (DrawingColor.FromArgb(255, pixels[i + 2], pixels[i + 1], pixels[i]) == color)
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>フレームを閉じて画素を読み戻す（初回のみ）。</summary>
    /// <returns>読み戻した画素。</returns>
    private byte[] Read()
    {
        if (_pixels is { } cached)
        {
            return cached;
        }

        _context.EndFrame();
        var pixels = new byte[_stride * Height];
        _ = TestHelper.Factory.TryReadPixels(_renderTarget, pixels, _stride);
        _pixels = pixels;
        return pixels;
    }

    /// <summary>コンテキストと表面を破棄する。</summary>
    public void Dispose()
    {
        _context.Dispose();
        _renderTarget.Dispose();
    }
}
