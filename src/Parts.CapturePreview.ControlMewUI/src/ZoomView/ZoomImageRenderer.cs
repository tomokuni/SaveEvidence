using Aprillz.MewUI.Rendering;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.ZoomView;
using EsUtil.Ui.MewUI.Interop.Imaging;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.MewUI.ZoomView;

/// <summary>ズーム画像の描画層（MewUI 標準描画版）。</summary>
/// <remarks>
/// <b>描画のみ</b>を担う層である。 表示矩形（配置・倍率）はコアの <see cref="ZoomViewSession"/> が単一所有するため、
/// 本クラスは矩形を <see cref="IGraphicsContext"/> の描画 API へ写すだけで、判断・計算を持たない。<br/>
/// 変換した画像の保持は <see cref="ImageRenderer"/> が担う（同じ画像を渡し続ける限り変換しない）。
/// </remarks>
internal sealed class ZoomImageRenderer : IDisposable
{
    /// <summary>画像の描画と変換の保持を担う層。</summary>
    private readonly ImageRenderer _renderer = new();

    /// <summary>ズーム画像を描画する。</summary>
    /// <param name="context">描画先のコンテキスト（DIP 座標で描画できる状態であること）。</param>
    /// <param name="source">ズーム対象の画像。 null の場合は何もしない。</param>
    /// <param name="session">表示矩形（配置・倍率）を単一所有するセッション。</param>
    /// <remarks>画像の全面を表示矩形へ 1 回で描画する（コアが配置を確定済みのため、計算は不要）。</remarks>
    public void Render(IGraphicsContext context, EsReadOnlyBitmap? source, ZoomViewSession session)
        => _renderer.DrawTo(context, source, session.ImageRect);

    /// <summary>保持している画像を解放する。</summary>
    public void Dispose() => _renderer.Dispose();
}
