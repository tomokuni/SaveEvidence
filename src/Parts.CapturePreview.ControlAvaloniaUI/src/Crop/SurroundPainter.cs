using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.AvaloniaUI.Crop;

/// <summary>画像の周囲（影など）を画像より奥へ描く子コントロール。</summary>
/// <remarks>
/// <b>適用パターン</b>: 描画層（描画手順をホストから受け取り、画像の下へ描くだけ）。<br/>
/// <b>画像より奥に置く理由</b>: 画像の外側へ別のコントロールを重ねると、画像の端の画素<br/>
/// （画像と背景の混色）の下地が背景になり、画像と影の間に白い線として見える（実測）。<br/>
/// 奥に置けば混色の下地が影になる。 入力は受けない（ズーム・ドラッグは上のレイヤが受ける）。
/// </remarks>
internal sealed class SurroundPainter : Control
{
    /// <summary>周辺を描く処理を取得または設定する。</summary>
    /// <value>描画先・画像の表示矩形（DIP）・倍率を受け取る処理。 null の場合は何も描かない。</value>
    public Action<DrawingContext, Rect, double>? Renderer { get; set; }

    /// <summary>画像の表示矩形（DIP）を返す処理を取得または設定する。</summary>
    /// <value>画像の表示矩形を返す処理。 null の場合は何も描かない。</value>
    public Func<Rect>? ImageRect { get; set; }

    /// <summary>周辺描画の子コントロールを初期化する。</summary>
    public SurroundPainter()
    {
        IsHitTestVisible = false;

        // 帯の矩形は物理ピクセルに一致しているため、端をアンチエイリアスしない（半端な画素を作らない）
        RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);

        // 影は画像の外側へ広がるため、プレビュー領域の外（ツールバー・ステータスバー）へ描かない
        ClipToBounds = true;
    }

    /// <summary>周辺を描画する。</summary>
    /// <param name="context">描画先。</param>
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (Renderer is not { } renderer || ImageRect is not { } resolve)
        {
            return;
        }

        var image = resolve();
        if (image.Width <= 0 || image.Height <= 0)
        {
            return;
        }

        renderer(context, image, TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0);
    }
}