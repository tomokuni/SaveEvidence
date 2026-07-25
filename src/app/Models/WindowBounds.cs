namespace app.Models;

/// <summary>
/// ウィンドウの位置・サイズ・状態を表す値オブジェクト（JSONシリアル化対応）。
/// </summary>
/// <remarks>
/// <see cref="System.Drawing.Rectangle"/> に依存せず、JSONシリアル化が容易なプリミティブ型で構成する。<br/>
/// </remarks>
/// <param name="X">ウィンドウのX座標</param>
/// <param name="Y">ウィンドウのY座標</param>
/// <param name="Width">ウィンドウの幅</param>
/// <param name="Height">ウィンドウの高さ</param>
public sealed record WindowBounds(int X, int Y, int Width, int Height)
{
    /// <summary>AvaloniaのPixelRectに変換する。</summary>
    /// <returns>AvaloniaのPixelRect</returns>
    public Avalonia.PixelRect ToAvalonia() => new(X, Y, Width, Height);

    /// <summary>AvaloniaのPixelRectから生成する。</summary>
    /// <param name="rect">変換元のPixelRect</param>
    /// <returns>新しいWindowBoundsインスタンス</returns>
    public static WindowBounds FromAvalonia(Avalonia.PixelRect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);
}
