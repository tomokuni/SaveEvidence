using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;

namespace EsUtil.SaveEvidence.Parts.CaptureSession;

/// <summary>キャプチャ操作の結果を表すレコード型。</summary>
/// <param name="Image">キャプチャした画像。 エラー時は null。</param>
/// <param name="SourceBounds">キャプチャした領域（仮想画面座標）。 エラー時は null。</param>
/// <remarks>
/// `Image` と `SourceBounds` は常に同時に null または同時に非 null となる。<br/>
/// - エラー時: 両方 null<br/>
/// - キャンセル時: 両方 0x0（Image: 0x0 の空 EsBitmap、SourceBounds: 0x0 の空 PixelRect）<br/>
/// `SourceBounds` は仮想画面座標（複数モニターを包含する座標空間）で表されるため、<br/>
/// プライマリモニターが (0,0) にない場合、X / Y は負値になり得る。<br/>
/// `Image` は表示境界で各 UI のビットマップ形式へ変換する（破棄は呼び出し側）。
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

