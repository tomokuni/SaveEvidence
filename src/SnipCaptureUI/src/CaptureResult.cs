using Avalonia;
using Estable.AvaloniaUI.Platform.Imaging;
using Estable.Platform.Imaging;

namespace Estable.AvaloniaUI.SnipCaptureUI;

/// <summary>キャプチャ操作の結果を表すレコード型。</summary>
/// <param name="Image">キャプチャした画像。エラー時は null。</param>
/// <param name="SourceBounds">キャプチャした領域（仮想画面座標）。エラー時は null。</param>
/// <remarks>
/// `Image` と `SourceBounds` は常に同時に null または同時に非 null となる。<br/>
/// - エラー時: 両方 null<br/>
/// - キャンセル時: 両方 0x0（Image: 0x0 の空 EsBitmap、SourceBounds: 0x0 の空 PixelRect）<br/>
/// `SourceBounds` は仮想画面座標（複数モニターを包含する座標空間）で表されるため、<br/>
/// プライマリモニターが (0,0) にない場合、X / Y は負値になり得る。<br/>
/// `Image` は表示境界で <c>ToAvaloniaWriteableBitmap()</c> により変換する（破棄は呼び出し側）。
/// </remarks>
public sealed record CaptureResult(EsBitmap? Image, PixelRect? SourceBounds);

