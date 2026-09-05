using SkiaSharp;
using Point = System.Drawing.Point;
using Rectangle = System.Drawing.Rectangle;
using Size = System.Drawing.Size;
using Color = System.Drawing.Color;

namespace Estable.AvaloniaUI.Platform.Imaging;

/// <summary>複数のビットマップ種別を横断して扱う軽量なユニバーサルビットマップラッパー（コア）。</summary>
/// <remarks>
/// <b>設計</b>: 本型は内部表現（<see cref="SKBitmap"/> BGRA8888・Premul）と、ビットマップ種別に依存しない
/// ピクセル操作のみを提供する。<br/>
/// Avalonia / System.Drawing / WPF 依存の生成・変換（<c>Create</c> / <c>From～</c> / <c>To～</c>）は
/// <see cref="PbBitmapExtensions"/>（<c>extension(PbBitmap)</c>）が担う。<br/>
/// <br/>
/// <b>ピクセル形式</b>: BGRA8888（アルファ乗算済み、System.Drawing の Format32bppPArgb 相当）。<br/>
/// <b>所有権</b>: 内部 SKBitmap の所有権は本型が保持する。<br/>
/// <b>スレッド安全性</b>: 静的共有状態を持たないため各インスタンスは独立して並列利用できるが、
/// 単一インスタンスへの同時ピクセル操作は直列化すること（<see cref="SKBitmap"/> はスレッドセーフではない）。
/// </remarks>
public sealed class PbBitmap : IDisposable
{
    /// <summary>1 ピクセルあたりのバイト数（BGRA8888）。</summary>
    private const int BytesPerPixel = 4;

    private readonly SKBitmap _bitmap;

    private bool _disposed;

    /// <summary>内部 SKBitmap からラッパーを構築する（所有権を譲り受ける）。</summary>
    /// <param name="bitmap">内部表現となる SKBitmap（BGRA8888・Premul）。所有権が本型へ移る。</param>
    /// <remarks>
    /// ピクセルポインタ・ストライド・寸法はビットマップ生成後に不変のため、<br/>
    /// 構築時に一度だけ取得して読み取り専用プロパティへ設定する（<see cref="GetPixel"/> / <see cref="SetPixel"/> の
    /// ホットパスで毎回のネイティブ呼び出し・プロパティ評価を排除）。<br/>
    /// <c>init</c> アクセサにより、初期化後は再設定できない（読み取り専用）。
    /// </remarks>
    internal PbBitmap(SKBitmap bitmap)
    {
        _bitmap = bitmap;
        Address = bitmap.GetPixels();
        Stride = bitmap.RowBytes;
        Width = bitmap.Width;
        Height = bitmap.Height;
        Size = new Size(Width, Height);
    }

    // ====================================================================
    // 解放
    // ====================================================================

    /// <summary>内部 SKBitmap を解放する。</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _bitmap.Dispose();
        _disposed = true;
    }

    /// <summary>破棄済みなら例外を投げる（拡張クラスからも利用）。</summary>
    internal void ThrowIfDisposed()
        => ObjectDisposedException.ThrowIf(_disposed, this);

    // ====================================================================
    // 生成・変換
    // ====================================================================

    /// <summary>指定サイズの空のビットマップ（BGRA8888・Premul）を生成する。</summary>
    /// <param name="width">幅（ピクセル）。正の値。</param>
    /// <param name="height">高さ（ピクセル）。正の値。</param>
    /// <returns>生成されたビットマップ。</returns>
    public static PbBitmap Create(int width, int height)
        => new(new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul)));

    /// <summary>SKBitmap からラッパーを生成する（引数の所有権を譲り受ける）。</summary>
    /// <param name="bitmap">内部表現となる SKBitmap（BGRA8888・Premul）。</param>
    /// <returns>生成されたビットマップ。</returns>
    /// <remarks>
    /// 引数はアルファ乗算済み BGRA8888 であること。<br/>
    /// 戻り値の破棄時に引数の SKBitmap も Dispose される（所有権が移る）。
    /// </remarks>
    public static PbBitmap FromSKBitmap(SKBitmap bitmap) => new(bitmap);

    /// <summary>内部 SKBitmap を返す（呼び出し側で Dispose しないこと）。</summary>
    /// <returns>内部 SKBitmap（共有参照）。</returns>
    /// <remarks>
    /// 本型はコア（ピクセル操作）に該当するため、拡張クラスではなく本型に内包する。<br/>
    /// 内部インスタンスをそのまま返すため、呼び出し側で Dispose しないこと（所有権は本型が保持）。
    /// </remarks>
    public SKBitmap ToSKBitmap()
    {
        ThrowIfDisposed();
        return _bitmap;
    }

    // ====================================================================
    // 基本プロパティ
    // ====================================================================

    /// <summary>幅（ピクセル）。</summary>
    public int Width { get; init; }

    /// <summary>高さ（ピクセル）。</summary>
    public int Height { get; init; }

    /// <summary>幅・高さ（ピクセル）。</summary>
    public Size Size { get; init; }

    /// <summary>ピクセルデータ先頭アドレス（BGRA8888）。</summary>
    public nint Address { get; init; }

    /// <summary>1 ラインあたりのバイト数（パディングを含む）。</summary>
    public int Stride { get; init; }

    /// <summary>破棄済みかどうか。</summary>
    public bool IsDisposed => _disposed;

    // ====================================================================
    // ピクセル操作
    // ====================================================================

    /// <summary>指定座標のピクセルを BGRA（アルファ乗算済み）で取得する。</summary>
    /// <param name="x">X 座標（ピクセル）。</param>
    /// <param name="y">Y 座標（ピクセル）。</param>
    /// <returns>(B, G, R, A) のタプル。範囲外は IndexOutOfRangeException。</returns>
    public unsafe (byte B, byte G, byte R, byte A) GetPixel(int x, int y)
    {
        ThrowIfDisposed();
        var p = (byte*)Address + y * Stride + x * BytesPerPixel;
        return (p[0], p[1], p[2], p[3]);
    }

    /// <summary>指定座標のピクセルを BGRA（アルファ乗算済み）で取得する。</summary>
    /// <param name="point">座標（ピクセル）。</param>
    /// <returns>取得した色（<see cref="Color"/>）。範囲外は IndexOutOfRangeException。</returns>
    public Color GetPixel(Point point)
    {
        var (b, g, r, a) = GetPixel(point.X, point.Y);
        return Color.FromArgb(a, r, g, b);
    }

    /// <summary>指定座標のピクセルを BGRA（アルファ乗算済み）で設定する。</summary>
    /// <param name="x">X 座標（ピクセル）。</param>
    /// <param name="y">Y 座標（ピクセル）。</param>
    /// <param name="b">青成分（0〜255）。</param>
    /// <param name="g">緑成分（0〜255）。</param>
    /// <param name="r">赤成分（0〜255）。</param>
    /// <param name="a">アルファ成分（0〜255）。</param>
    public unsafe void SetPixel(int x, int y, byte b, byte g, byte r, byte a)
    {
        ThrowIfDisposed();
        var p = (byte*)Address + y * Stride + x * BytesPerPixel;
        p[0] = b;
        p[1] = g;
        p[2] = r;
        p[3] = a;
    }

    /// <summary>指定座標のピクセルを BGRA（アルファ乗算済み）で設定する。</summary>
    /// <param name="point">座標（ピクセル）。</param>
    /// <param name="color">設定する色。</param>
    public void SetPixel(Point point, Color color)
        => SetPixel(point.X, point.Y, color.B, color.G, color.R, color.A);

    /// <summary>指定座標のピクセルを色で設定する（範囲外は無視）。</summary>
    /// <param name="x">X 座標（ピクセル）。</param>
    /// <param name="y">Y 座標（ピクセル）。</param>
    /// <param name="color">設定する色。</param>
    /// <returns>書き込めたかどうか。範囲外の場合は false。</returns>
    /// <remarks>
    /// 描画（破線・矩形オーバーレイ等）で範囲外を無視したい場合に使用する。<br/>
    /// <see cref="SetPixel(int, int, byte, byte, byte, byte)"/> は範囲外で例外のため、<br/>
    /// 範囲外チェック（<c>x</c> / <c>y</c> が画像サイズ内かどうか）を行ってから委譲する。
    /// </remarks>
    public bool TrySetPixel(int x, int y, Color color)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
        {
            return false;
        }

        SetPixel(x, y, color.B, color.G, color.R, color.A);
        return true;
    }

    /// <summary>全ピクセルを指定色（BGRA）で塗りつぶす。</summary>
    /// <param name="b">青成分（0〜255）。</param>
    /// <param name="g">緑成分（0〜255）。</param>
    /// <param name="r">赤成分（0〜255）。</param>
    /// <param name="a">アルファ成分（0〜255）。</param>
    public void Clear(byte b, byte g, byte r, byte a)
    {
        ThrowIfDisposed();
        _bitmap.Erase(new SKColor(r, g, b, a));
    }

    /// <summary>全ピクセルを指定色で塗りつぶす。</summary>
    /// <param name="color">塗りつぶし色。</param>
    public void Clear(Color color)
        => Clear(color.B, color.G, color.R, color.A);

    /// <summary>本ビットマップの複製を生成する。</summary>
    /// <returns>独立した複製（所有権は呼び出し側）。</returns>
    public PbBitmap Clone()
    {
        ThrowIfDisposed();
        return new PbBitmap(_bitmap.Copy());
    }

    /// <summary>画像から指定された矩形領域を切り出す。</summary>
    /// <param name="rect">切り出し領域（画像ローカルピクセル）。</param>
    /// <returns>切り出された新しいビットマップ（要求サイズ。ソース範囲外は透明パディング）。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="rect"/> の幅・高さが0以下。</exception>
    /// <remarks>
    /// デスティネーションは事前にゼロ初期化（透明）されるため、ソース範囲外は透明パディングとなる。<br/>
    /// 矩形がソースと交差しない場合は全ピクセル透明のビットマップを返す。<br/>
    /// 呼び出し側で戻り値を破棄すること。
    /// </remarks>
    public PbBitmap Crop(Rectangle rect)
        => Crop(rect.X, rect.Y, rect.Width, rect.Height);

    /// <summary>画像から指定された矩形領域を切り出す。</summary>
    /// <param name="x">切り出し領域の左上X座標（画像ローカルピクセル）。負値は0へクランプ。</param>
    /// <param name="y">切り出し領域の左上Y座標（画像ローカルピクセル）。負値は0へクランプ。</param>
    /// <param name="width">切り出し領域の幅（ピクセル）。正の値。</param>
    /// <param name="height">切り出し領域の高さ（ピクセル）。正の値。</param>
    /// <returns>切り出された新しいビットマップ（要求サイズ。ソース範囲外は透明パディング）。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/> / <paramref name="height"/> が0以下。</exception>
    /// <remarks>
    /// デスティネーションは事前にゼロ初期化（透明）されるため、ソース範囲外は透明パディングとなる。<br/>
    /// 矩形がソースと交差しない場合は全ピクセル透明のビットマップを返す。<br/>
    /// 内部ではピクセルバッファ（<see cref="Address"/>）へ直接アクセスし、行単位で <c>Buffer.MemoryCopy</c> によりコピーする。<br/>
    /// 呼び出し側で戻り値を破棄すること。
    /// </remarks>
    public unsafe PbBitmap Crop(int x, int y, int width, int height)
    {
        ThrowIfDisposed();
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "切り出し領域の幅は正の値である必要があります。");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "切り出し領域の高さは正の値である必要があります。");
        }

        var srcW = Width;
        var srcH = Height;

        // ソース範囲内にクランプ（交差しない場合は透明パディングのまま。コピー行数が 0 となる）
        var cropX = Math.Max(0, x);
        var cropY = Math.Max(0, y);
        var cropW = Math.Min(width, srcW - cropX);
        var cropH = Math.Min(height, srcH - cropY);

        var result = Create(width, height);
        var dstStride = result.Stride;
        var srcStride = Stride;

        // コピーする 1 行のバイト数（BGRA8888 は 4 バイト/ピクセル。左右のストライド差を考慮して短い方）
        var rowBytes = Math.Min(cropW * BytesPerPixel, dstStride);

        var src = (byte*)Address;
        var dst = (byte*)result.Address;

        for (var row = 0; row < cropH; row++)
        {
            // ソースの切り出し開始行へ移動し、行単位で memcpy（デスティネーションは行先頭から）
            var pSrc = src + (cropY + row) * srcStride + cropX * BytesPerPixel;
            var pDst = dst + row * dstStride;
            Buffer.MemoryCopy(pSrc, pDst, dstStride, rowBytes);
        }

        return result;
    }
}
