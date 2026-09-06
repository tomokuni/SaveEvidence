using Estable.Primitives;
using Estable.AvaloniaUI.Platform.Imaging;

namespace Estable.AvaloniaUI.Loupe;

/// <summary>ルーペの描画完了時に上位へ通知する描画結果と表示情報。</summary>
/// <remarks>
/// <see cref="LoupeControl.Rendered"/> イベントの引数。<br/>
/// 描画済みのルーペ画像（<see cref="PbBitmap"/>）と表示状態（位置・サイズ・切り出し領域等）を表し、<br/>
/// ホスト側はイベントハンドラ内で <see cref="PbBitmap"/> のピクセルへオーバーレイ（破線・図形等）を<br/>
/// 直接描画できる（描画後の画像は <see cref="LoupeControl"/> が表示へ反映する）。<br/>
/// 座標系は次のとおり:<br/>
/// - <see cref="PbBitmap"/>: ルーペ画像のピクセル座標（1 ピクセル = <see cref="Scale"/> の逆数 DIP）<br/>
/// - <see cref="Position"/>: 親パネル内の表示左上（DIP）<br/>
/// - <see cref="CenterPoint"/> / <see cref="CaptureTopLeft"/>: ドメイン座標（元画像ピクセル座標）<br/>
/// - <see cref="Scale"/>: ドメイン座標 → ルーペ画像ピクセル座標の変換係数<br/>
/// オーバーレイの描画位置は <c>(ドメイン座標 − CaptureTopLeft) × Scale</c> で求められる。
/// </remarks>
public sealed class LoupeRenderedEventArgs : EventArgs
{
    /// <summary>表示情報を初期化する。</summary>
    /// <param name="bitmap">描画済みのルーペ画像（PbBitmap）。</param>
    /// <param name="position">ルーペの表示左上（親パネル内 DIP）。</param>
    /// <param name="size">ルーペの表示サイズ（一辺・DIP）。</param>
    /// <param name="zoomLevel">拡大率。</param>
    /// <param name="centerPoint">拡大中心（ドメイン座標 = 元画像ピクセル座標）。未設定時は null。</param>
    /// <param name="captureTopLeft">切り出し領域の左上（ドメイン座標）。中心未設定時は null。</param>
    /// <param name="scale">ドメイン座標 → ルーペ画像ピクセル座標の変換係数。</param>
    public LoupeRenderedEventArgs(
        PbBitmap bitmap, PointD position, double size, int zoomLevel, PointD? centerPoint, PointD? captureTopLeft, double scale)
    {
        Bitmap = bitmap;
        Position = position;
        Size = size;
        ZoomLevel = zoomLevel;
        CenterPoint = centerPoint;
        CaptureTopLeft = captureTopLeft;
        Scale = scale;
    }

    /// <summary>描画済みのルーペ画像（PbBitmap）。イベントハンドラ内でピクセルへオーバーレイを描画できる。</summary>
    /// <remarks>
    /// イベントハンドラ内で <see cref="Bitmap"/> のピクセルを更新した内容は、イベント終了後に
    /// <see cref="LoupeControl"/> が表示へ反映する（イベントは表示反映前に同期的に発火する）。<br/>
    /// <see cref="Bitmap"/> の所有権は <see cref="LoupeControl"/> が保持する（ホスト側で Dispose しないこと）。
    /// </remarks>
    public PbBitmap Bitmap { get; }

    /// <summary>ルーペの表示左上（親パネル内 DIP）。</summary>
    public PointD Position { get; }

    /// <summary>ルーペの表示サイズ（一辺・DIP）。</summary>
    public double Size { get; }

    /// <summary>拡大率。</summary>
    public int ZoomLevel { get; }

    /// <summary>拡大中心（ドメイン座標 = 元画像ピクセル座標）。未設定時は null。</summary>
    public PointD? CenterPoint { get; }

    /// <summary>切り出し領域の左上（ドメイン座標）。中心未設定時は null。</summary>
    public PointD? CaptureTopLeft { get; }

    /// <summary>ドメイン座標 → ルーペ画像ピクセル座標の変換係数。</summary>
    public double Scale { get; }
}
