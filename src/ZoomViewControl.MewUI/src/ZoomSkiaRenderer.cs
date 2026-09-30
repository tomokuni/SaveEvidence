using EsUtil.Platform.Core.Imaging;
using SkiaSharp;
using EsUtil.Platform.Skia;


namespace EsUtil.SaveEvidence.ZoomView.MewUI;

/// <summary>ズーム画像の描画層（MewUI 実装）。</summary>
/// <remarks>
/// <b>描画のみ</b>を担う層である。 表示矩形（配置・倍率）はコアの <see cref="ZoomViewSession"/> が単一所有するため、<br/>
/// 本クラスは矩形を <see cref="SKCanvas"/> の描画 API へ写すだけで、判断・計算を持たない。<br/>
/// ドキュメントコメント・コードコメントにも最適化の内容は記載しない（実装の一部として静かに行う）。
/// </remarks>
internal static class ZoomSkiaRenderer
{
    /// <summary>ズーム画像を描画する。</summary>
    /// <param name="canvas">描画先のキャンバス（DIP 座標で描画できる状態であること）。</param>
    /// <param name="source">ズーム対象の画像。 null の場合は何もしない。</param>
    /// <param name="session">表示矩形（配置・倍率）を単一所有するセッション。</param>
    /// <remarks>画像の全面を表示矩形へ 1 回で描画する（コアが配置を確定済みのため、Skia 側の計算は不要）。</remarks>
    public static void Render(SKCanvas canvas, EsBitmap? source, ZoomViewSession session)
    {
        var rect = session.ImageRect;
        if (source is null || rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        var src = new SKRect(0, 0, source.Width, source.Height);
        var dst = new SKRect((float)rect.X, (float)rect.Y, (float)rect.Right, (float)rect.Bottom);
        canvas.DrawBitmap(source.ToSKBitmap(), src, dst);
    }
}

