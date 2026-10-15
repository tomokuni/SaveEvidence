using EsUtil.SaveEvidence.Parts.CapturePreview.Core.ZoomView;
using EsUtil.SaveEvidence.Platform.WinForms.Imaging;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.WinForms.ZoomView;

/// <summary>ズーム画像の描画層（WinForms / GDI+ 実装）。</summary>
/// <remarks>
/// <b>描画のみ</b>を担う層である。 表示矩形（配置・倍率）はコアの <see cref="ZoomViewSession"/> が単一所有するため、<br/>
/// 本クラスは矩形を GDI+ の描画 API へ写すだけで、判断・計算を持たない。<br/>
/// 描画先はコントロールの描画面と共有されるため、描画の前後で設定（補間・画素のオフセット）を保存・復帰する。
/// </remarks>
internal static class ZoomGraphicsRenderer
{
    /// <summary>ズーム画像を描画する。</summary>
    /// <param name="graphics">描画先（DIP 座標で描画できる状態であること）。 null 不可。</param>
    /// <param name="source">ズーム対象の画像（GDI+ へ複製済みのビットマップ）。 null の場合は何もしない。</param>
    /// <param name="session">表示矩形（配置・倍率）を単一所有するセッション。</param>
    /// <remarks>
    /// 画像の全面を表示矩形へ 1 回で描画する（コアが配置を確定済みのため、GDI+ 側の計算は不要）。<br/>
    /// 拡大・縮小の見え方は <see cref="GdiImageRenderer.DrawScaled"/> が単一所有する。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="graphics"/> または <paramref name="session"/> が null の場合。</exception>
    public static void Render(Graphics graphics, Bitmap? source, ZoomViewSession session)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(session);

        if (source is null)
        {
            return;
        }

        GdiImageRenderer.DrawScaled(graphics, source, session.ImageRect);
    }

    /// <summary>スクロールバー 1 本を描画する。</summary>
    /// <param name="graphics">描画先（DIP 座標で描画できる状態であること）。 null 不可。</param>
    /// <param name="bar">描画するスクロールバー。 null の場合は何もしない。</param>
    /// <param name="trackColor">トラックの色。</param>
    /// <param name="thumbColor">サムの色。</param>
    /// <param name="drawTrack">トラックを描くかどうか。</param>
    /// <remarks>
    /// 矩形はコア（<see cref="ZoomViewSession.CurrentScrollBars"/>）が確定済みのため、本メソッドは塗るだけである。<br/>
    /// トラックを先に、サムを後に塗る（サムがトラックの上に載る）。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="graphics"/> が null の場合。</exception>
    public static void RenderScrollBar(
        Graphics graphics, ZoomScrollBar? bar, Color trackColor, Color thumbColor, bool drawTrack)
    {
        ArgumentNullException.ThrowIfNull(graphics);

        if (bar is not { } value)
        {
            return;
        }

        if (drawTrack)
        {
            GdiFill.Rect(graphics, value.Track, trackColor);
        }

        GdiFill.Rect(graphics, value.Thumb, thumbColor);
    }
}
