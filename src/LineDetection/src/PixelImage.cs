namespace Estable.AvaloniaUI.LineDetection;

/// <summary>画像のピクセルバッファを表す不変の値型。</summary>
/// <param name="Width">画像の幅（ピクセル）。</param>
/// <param name="Height">画像の高さ（ピクセル）。</param>
/// <param name="Stride">1 行のバイト数（幅 × 4 以上）。</param>
/// <param name="Pixels">ピクセルデータ（BGRA 順・1 ピクセル 4 バイト）。</param>
/// <remarks>
/// OpenCV・Avalonia に依存しない純粋な値型であり、検出器の入力として使用する。<br/>
/// これにより <see cref="ILineDetector"/> は特定の画像フレームワークに依存せず、<br/>
/// ヘッドレス環境でもピクセルデータを直接テストに渡せる。<br/>
/// ピクセルは BGRA 順（B が先頭）で、各行は <see cref="Stride"/> バイトでパックされる。
/// </remarks>
public readonly record struct PixelImage(int Width, int Height, int Stride, ReadOnlyMemory<byte> Pixels);
