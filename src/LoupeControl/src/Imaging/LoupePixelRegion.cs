namespace Estable.AvaloniaUI.Loupe.Imaging;

/// <summary>ルーペ画像の抽出済みピクセル領域。</summary>
internal sealed class LoupePixelRegion(byte[] pixels, int size, int stride)
{
    /// <summary>BGRAピクセルデータ。</summary>
    public byte[] Pixels { get; } = pixels;

    /// <summary>領域の一辺のサイズ。</summary>
    public int Size { get; } = size;

    /// <summary>1行のバイト数。</summary>
    public int Stride { get; } = stride;
}
