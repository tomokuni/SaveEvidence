using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Imaging;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.AvaloniaUI;

/// <summary>切り出しの対象画像（<see cref="CropViewControl"/> が表示している画像）を取り出す窓口。</summary>
/// <remarks>
/// 切り出しの対象（元画像ピクセル）は<b>表示中の画像が単一所有する</b>ため、切り出しセッションは画像を固定値で受け取らず、<br/>
/// 本クラス経由で都度読み出す（画像が差し替わると切り出しの境界も現在の画像へ追従する）。<br/>
/// <br/>
/// <see cref="CropViewControl.Source"/> は表示境界の型（<c>IImage</c>）のため、表示アダプタ（<see cref="EsBitmapAvaloniaImage"/>）から<br/>
/// 内包する <see cref="EsBitmap"/> を取り出す（表示している画像と切り出す画像が同じであることをここで保証する）。<br/>
/// 表示アダプタ以外が設定されている場合は「切り出せる画像が無い」として null を返す。
/// </remarks>
internal static class CropViewImage
{
    /// <summary>表示中の画像（切り出しの元画像）を取得する。</summary>
    /// <param name="cropView">切り出しの対象となる複合コントロール。</param>
    /// <returns>表示中の画像。 未設定・破棄済み・表示アダプタ以外の場合は null。</returns>
    internal static EsBitmap? CurrentImage(this CropViewControl cropView)
        => (cropView.Source as EsBitmapAvaloniaImage)?.Bitmap;
}
