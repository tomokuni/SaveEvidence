using EsUtil.Platform.Core.Geometry;
using Estable.Loupe;
using SkiaSharp;

namespace Estable.MewUI.Loupe;

/// <summary>ルーペの描画完了時に上位へ通知する描画先と表示情報。</summary>
/// <remarks>
/// <see cref="LoupeControl.Rendered"/> イベントの引数。<br/>
/// MewUI 版のルーペは中間ビットマップを作らず画面のキャンバスへ直接描画するため、<br/>
/// ホスト側はイベントハンドラ内で <see cref="Canvas"/> へ追加描画（オーバーレイ・文字等）を行う。<br/>
/// <b>座標ラベルの文字はホストが描く</b>（<see cref="CoordLabel"/> の契約に従い、テキストの実寸測定と描画を行う。実測は <c>Platform.Core</c> の <c>SkiaText</c>、描画順は <c>SkiaLabel</c> が単一所有する）。<br/>
/// 座標系は次のとおり:<br/>
/// - <see cref="Canvas"/>: ルーペの描画先（1 ピクセル = ルーペ画像の 1 ピクセル）<br/>
/// - <see cref="CaptureTopLeft"/>: ドメイン座標（元画像ピクセル座標）<br/>
/// - <see cref="Scale"/>: ドメイン座標 → ルーペ画像ピクセル座標の変換係数<br/>
/// オーバーレイの描画位置は <c>(ドメイン座標 − CaptureTopLeft) × Scale</c> で求められる。<br/>
/// 拡大率・拡大中心は、描画に必要な値をホストが既に保持しているため通知しない<br/>
/// （必要な場合は <see cref="LoupeControl"/> の公開プロパティから取得する）。
/// </remarks>
public sealed class LoupeRenderedEventArgs : EventArgs
{
    /// <summary>表示情報を初期化する。</summary>
    /// <param name="canvas">ルーペを描画したキャンバス（描画済み。追加描画はイベントハンドラ内で行う）。</param>
    /// <param name="pixelSize">ルーペの一辺のサイズ（物理ピクセル）。</param>
    /// <param name="captureTopLeft">切り出し領域の左上（ドメイン座標）。中心未設定時は null。</param>
    /// <param name="scale">ドメイン座標 → ルーペ画像ピクセル座標の変換係数。</param>
    /// <param name="coordLabel">中心座標ラベルの描画契約。非表示の設定または中心未設定時は null。</param>
    public LoupeRenderedEventArgs(SKCanvas canvas, int pixelSize, EsPoint? captureTopLeft, double scale, LoupeCoordLabel? coordLabel = null)
    {
        Canvas = canvas;
        PixelSize = pixelSize;
        CaptureTopLeft = captureTopLeft;
        Scale = scale;
        CoordLabel = coordLabel;
    }

    /// <summary>ルーペを描画したキャンバスを取得する。</summary>
    /// <value>描画済みのキャンバス（座標系はルーペ画像のピクセル）。ホストはここへオーバーレイを追加描画できる。</value>
    /// <remarks>イベントハンドラ内でのみ使用できる（描画が終わると内容は表示へ反映される）。</remarks>
    public SKCanvas Canvas { get; }

    /// <summary>ルーペの一辺のサイズ（物理ピクセル）を取得する。</summary>
    /// <value>一辺の物理ピクセル数。</value>
    public int PixelSize { get; }

    /// <summary>切り出し領域の左上（ドメイン座標）を取得する。</summary>
    /// <value>切り出し領域の左上。中心が未設定の場合は null（オーバーレイを描画できない）。</value>
    public EsPoint? CaptureTopLeft { get; }

    /// <summary>ドメイン座標 → ルーペ画像ピクセル座標の変換係数を取得する。</summary>
    /// <value>変換係数（拡大率と同値。DPI スケーリングの影響を受けない）。</value>
    public double Scale { get; }

    /// <summary>中心座標ラベルの描画契約（文言・色・寸法・配置の計算元）を取得する。</summary>
    /// <value>ラベルを描く場合の契約。非表示の設定または中心未設定時は null。</value>
    /// <remarks>
    /// 文字はホストが描く（テキストの実寸測定は描画 API 依存のため）。<br/>
    /// 寸法（フォントサイズ・角丸）は物理ピクセル、配置は <see cref="LoupeCoordLabel.ComputeFrame"/> が求める。
    /// </remarks>
    public LoupeCoordLabel? CoordLabel { get; }
}
