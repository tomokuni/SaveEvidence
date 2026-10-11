using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.Capture;

/// <summary>キャプチャ操作の結果を表すレコード型。</summary>
/// <param name="Image">キャプチャした画像（<b>書き換え可能</b>）。 キャンセル時は null。</param>
/// <param name="SourceBounds">キャプチャした領域（仮想画面座標）。 キャンセル時は 0x0。</param>
/// <remarks>
/// <b>キャンセルは「画像 null・領域 0x0」で表します</b>（<see cref="From"/> が単一所有する。 UI が null 判定を
/// 書くと、UI ごとにキャンセルの表現が食い違います）。<br/>
/// <b>画像を書き換え可能（<see cref="EsBitmap"/>）で返す理由</b>: 受け取った側（<c>CaptureImageHolder</c>）が
/// <b>書き終えた時点で凍結して</b>読み取り専用の面を作るためです（撮影直後のオーバーレイ描画は書き込みを要します）。
/// 読み取りだけの層へ渡す場合は <see cref="EsReadOnlyBitmap.FromBitmap"/> で読み取り専用の面にします。<br/>
/// <b>所有権</b>: 画像は呼び出し側へ移ります（破棄するのは受け取った側）。<br/>
/// <b>領域の座標</b>: 仮想画面座標（複数モニターを包含する座標空間）で表すため、プライマリモニターが (0, 0) に
/// ない場合、X / Y は負値になり得ます。
/// </remarks>
public sealed record CaptureResult(EsBitmap? Image, EsRectInt? SourceBounds)
{
    /// <summary>切り出し結果と領域からキャプチャ結果を生成する（未確定はキャンセルの結果へ正規化する）。</summary>
    /// <param name="image">切り出した画像。 キャンセル（未確定）の場合は null。</param>
    /// <param name="bounds">切り出した領域（画面物理座標）。 キャンセル（未確定）の場合は null。</param>
    /// <returns>キャンセルの場合は画像 null・領域 0x0 の空結果、それ以外は指定された値の結果。</returns>
    /// <remarks>
    /// 「キャンセルは 0x0 の空結果として返す」規則を単一所有する<br/>
    /// （UI が null 判定を書くと、UI ごとにキャンセルの表現が食い違う）。<br/>
    /// 片方だけ無い場合もキャンセルとして扱う（両者は同時に null または同時に非 null の契約）。
    /// </remarks>
    public static CaptureResult From(EsBitmap? image, EsRectInt? bounds)
        => image is null || bounds is null
            ? new CaptureResult(null, default(EsRectInt))
            : new CaptureResult(image, bounds.Value);
}
