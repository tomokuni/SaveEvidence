using EsUtil.Platform.Core.Geometry;
using SkiaSharp;

namespace Estable.MewUI.Loupe;

/// <summary>ルーペの描画完了時に上位へ通知する描画先と表示情報。</summary>
/// <remarks>
/// <see cref="LoupeControl.Rendered"/> イベントの引数。<br/>
/// MewUI 版のルーペは中間ビットマップを作らず画面のキャンバスへ直接描画するため、<br/>
/// ホスト側はイベントハンドラ内で <see cref="Canvas"/> へ追加描画（オーバーレイ等）を行う。<br/>
/// 座標系は次のとおり:<br/>
/// - <see cref="Canvas"/>: ルーペの描画先（1 ピクセル = ルーペ画像の 1 ピクセル）<br/>
/// - <see cref="CenterPoint"/> / <see cref="CaptureTopLeft"/>: ドメイン座標（元画像ピクセル座標）<br/>
/// - <see cref="Scale"/>: ドメイン座標 → ルーペ画像ピクセル座標の変換係数<br/>
/// オーバーレイの描画位置は <c>(ドメイン座標 − CaptureTopLeft) × Scale</c> で求められる。
/// </remarks>
public sealed class LoupeRenderedEventArgs : EventArgs
{
    /// <summary>表示情報を初期化する。</summary>
    /// <param name="canvas">ルーペを描画したキャンバス（描画済み。追加描画はイベントハンドラ内で行う）。</param>
    /// <param name="pixelSize">ルーペの一辺のサイズ（物理ピクセル）。</param>
    /// <param name="zoomLevel">拡大率。</param>
    /// <param name="centerPoint">拡大中心（ドメイン座標 = 元画像ピクセル座標）。未設定時は null。</param>
    /// <param name="captureTopLeft">切り出し領域の左上（ドメイン座標）。中心未設定時は null。</param>
    /// <param name="scale">ドメイン座標 → ルーペ画像ピクセル座標の変換係数。</param>
    public LoupeRenderedEventArgs(
        SKCanvas canvas, int pixelSize, int zoomLevel, EsPoint? centerPoint, EsPoint? captureTopLeft, double scale)
    {
        Canvas = canvas;
        PixelSize = pixelSize;
        ZoomLevel = zoomLevel;
        CenterPoint = centerPoint;
        CaptureTopLeft = captureTopLeft;
        Scale = scale;
    }

    /// <summary>ルーペを描画したキャンバスを取得する。</summary>
    /// <value>描画済みのキャンバス（座標系はルーペ画像のピクセル）。ホストはここへオーバーレイを追加描画できる。</value>
    /// <remarks>イベントハンドラ内でのみ使用できる（描画が終わると内容は表示へ反映される）。</remarks>
    public SKCanvas Canvas { get; }

    /// <summary>ルーペの一辺のサイズ（物理ピクセル）を取得する。</summary>
    /// <value>一辺の物理ピクセル数。</value>
    public int PixelSize { get; }

    /// <summary>拡大率を取得する。</summary>
    /// <value>拡大率（1〜64）。</value>
    public int ZoomLevel { get; }

    /// <summary>拡大中心（ドメイン座標 = 元画像ピクセル座標）を取得する。</summary>
    /// <value>拡大中心。未設定時は null。</value>
    public EsPoint? CenterPoint { get; }

    /// <summary>切り出し領域の左上（ドメイン座標）を取得する。</summary>
    /// <value>切り出し領域の左上。中心が未設定の場合は null（オーバーレイを描画できない）。</value>
    public EsPoint? CaptureTopLeft { get; }

    /// <summary>ドメイン座標 → ルーペ画像ピクセル座標の変換係数を取得する。</summary>
    /// <value>変換係数（表示 DIP あたりの拡大率）。</value>
    public double Scale { get; }
}
