using EsUtil.Platform.Core.Imaging;
using SkiaSharp;

namespace EsUtil.SaveEvidence.FadeMaskDragRect.UIBase.Tests;

/// <summary>テストで共用するユーティリティ（UI 非依存の描画ポートで描画結果を検証する）。</summary>
/// <remarks>
/// 描画の実行は Skia（<see cref="SkiaEsCanvas"/>）で行うため、ウィンドウと UI 初期化なしで<br/>
/// 描画結果の画素を検証できる（各 UI のアダプタを必要としない）。
/// </remarks>
internal static class TestHelper
{
    /// <summary>描画先（描画ポートとその画素を読む手段）を生成する。</summary>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <returns>生成した描画先。破棄は呼び出し側。</returns>
    public static RenderTarget CreateTarget(int width, int height) => new(width, height);
}

/// <summary>テスト用の描画先（描画ポートと画素読み取り）。</summary>
/// <remarks>内部は乗算済みアルファで保持されるが、<c>SKBitmap.GetPixel</c> は非乗算の色を返す。</remarks>
internal sealed class RenderTarget : IDisposable
{
    /// <summary>実際の画素を保持するビットマップ。</summary>
    private readonly SKBitmap _bitmap;

    /// <summary>指定サイズの描画先を初期化する（透明で初期化する）。</summary>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    public RenderTarget(int width, int height)
    {
        _bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        _bitmap.Erase(SKColors.Transparent);
        Canvas = new SkiaEsCanvas(new SKCanvas(_bitmap));
    }

    /// <summary>描画に使用する描画ポートを取得する。</summary>
    /// <value>描画に使用する描画ポート。</value>
    public SkiaEsCanvas Canvas { get; }

    /// <summary>指定座標の画素色を取得する。</summary>
    /// <param name="x">X 座標。</param>
    /// <param name="y">Y 座標。</param>
    /// <returns>画素色（非乗算）。</returns>
    public SKColor GetPixel(int x, int y) => _bitmap.GetPixel(x, y);

    /// <summary>描画先を破棄する。</summary>
    public void Dispose() => _bitmap.Dispose();
}
