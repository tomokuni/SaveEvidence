using Avalonia;
using EsUtil.Platform.Core.Imaging;
using Estable.Loupe;

namespace Estable.AvaloniaUI.Loupe;

/// <summary>ルーペの描画完了時に上位へ通知する描画結果と表示情報。</summary>
/// <remarks>
/// <see cref="LoupeControl.Rendered"/> イベントの引数。<br/>
/// 描画済みのルーペ画像（<see cref="EsBitmap"/>）と、ホストがオーバーレイ・文字を描くために必要な最小限の情報を表す。<br/>
/// ホスト側はイベントハンドラ内で <see cref="EsBitmap"/> のピクセルへオーバーレイ（破線・図形等）を<br/>
/// 直接描画できる（描画後の画像は <see cref="LoupeControl"/> が表示へ反映する）。<br/>
/// <b>座標ラベルの文字はホストが描く</b>（<see cref="CoordLabel"/> の契約に従い、テキストの実寸測定と描画を行う。実測は <c>Platform.Core</c> の <c>SkiaText</c>、描画順は <c>SkiaLabel</c> が単一所有する）。<br/>
/// 座標系は次のとおり:<br/>
/// - <see cref="Bitmap"/>: ルーペ画像のピクセル座標<br/>
/// - <see cref="CaptureTopLeft"/>: ドメイン座標（元画像ピクセル座標）<br/>
/// - <see cref="Scale"/>: ドメイン座標 → ルーペ画像ピクセル座標の変換係数<br/>
/// オーバーレイの描画位置は <c>(ドメイン座標 − CaptureTopLeft) × Scale</c> で求められる。<br/>
/// 表示位置・表示サイズ・拡大率・拡大中心は、描画に必要な値をホストが既に保持しているため通知しない<br/>
/// （必要な場合は <see cref="LoupeControl"/> の公開プロパティから取得する）。
/// </remarks>
public sealed class LoupeRenderedEventArgs : EventArgs
{
    /// <summary>表示情報を初期化する。</summary>
    /// <param name="bitmap">描画済みのルーペ画像（EsBitmap）。</param>
    /// <param name="captureTopLeft">切り出し領域の左上（ドメイン座標）。中心未設定時は null。</param>
    /// <param name="scale">ドメイン座標 → ルーペ画像ピクセル座標の変換係数。</param>
    /// <param name="coordLabel">中心座標ラベルの描画契約。非表示の設定または中心未設定時は null。</param>
    public LoupeRenderedEventArgs(EsBitmap bitmap, Point? captureTopLeft, double scale, LoupeCoordLabel? coordLabel = null)
    {
        Bitmap = bitmap;
        CaptureTopLeft = captureTopLeft;
        Scale = scale;
        CoordLabel = coordLabel;
    }

    /// <summary>描画済みのルーペ画像（EsBitmap）。イベントハンドラ内でピクセルへオーバーレイを描画できる。</summary>
    /// <remarks>
    /// イベントハンドラ内で <see cref="Bitmap"/> のピクセルを更新した内容は、イベント終了後に
    /// <see cref="LoupeControl"/> が表示へ反映する（イベントは表示反映前に同期的に発火する）。<br/>
    /// <see cref="Bitmap"/> の所有権は <see cref="LoupeControl"/> が保持する（ホスト側で Dispose しないこと）。
    /// </remarks>
    public EsBitmap Bitmap { get; }

    /// <summary>切り出し領域の左上（ドメイン座標）。中心未設定時は null。</summary>
    /// <value>切り出し領域の左上（元画像ピクセル座標）。null の場合はオーバーレイを描画できない。</value>
    public Point? CaptureTopLeft { get; }

    /// <summary>ドメイン座標 → ルーペ画像ピクセル座標の変換係数。</summary>
    /// <value>変換係数（拡大率と同値。DPI スケーリングの影響を受けない）。</value>
    public double Scale { get; }

    /// <summary>中心座標ラベルの描画契約（文言・色・寸法・配置の計算元）。</summary>
    /// <value>ラベルを描く場合の契約。非表示の設定または中心未設定時は null。</value>
    /// <remarks>
    /// 文字はホストが描く（テキストの実寸測定は描画 API 依存のため）。<br/>
    /// 寸法（フォントサイズ・角丸）は物理ピクセル、配置は <see cref="LoupeCoordLabel.ComputeFrame"/> が求める。
    /// </remarks>
    public LoupeCoordLabel? CoordLabel { get; }
}
