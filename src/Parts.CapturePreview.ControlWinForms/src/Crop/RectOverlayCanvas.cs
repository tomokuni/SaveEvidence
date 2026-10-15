using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Overlay;
using EsUtil.SaveEvidence.Parts.CapturePreview.WinForms.Overlay;
using EsUtil.SaveEvidence.Parts.CapturePreview.WinForms.ZoomView;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.WinForms.Crop;

/// <summary>外部提供矩形のオーバーレイを描画するコントロール（ズームビューの上へ重ねる）。</summary>
/// <remarks>
/// ズームビューが画像を描画し、本コントロールがその上へ矩形枠のみを描画する（透明なオーバーレイ）。<br/>
/// 矩形のビューポート座標変換はズームビューの座標変換（画像表示矩形の原点基準）を使うため、<br/>
/// Fit の中央寄せ補正を含めて画像と正確に一致する。<br/>
/// <b>入力は受け取らない</b>（下のズームビューが入力を受け取れるようにする。 基底クラスの既定）。
/// </remarks>
internal sealed class RectOverlayCanvas : Control
{
    /// <summary>座標変換の基準とするズームビュー。</summary>
    private readonly ZoomViewControl _zoomView;

    /// <summary>描画する矩形（対象座標）。</summary>
    private IReadOnlyList<OverlayRect> _rects = [];

    /// <summary>矩形オーバーレイ コントロールを初期化する。</summary>
    /// <param name="zoomView">座標変換の基準とするズームビュー。 null 不可。</param>
    /// <remarks>
    /// <b>重ね表示のスタイル設定</b>: 自前で描く（<see cref="ControlStyles.UserPaint"/> /<br/>
    /// <see cref="ControlStyles.AllPaintingInWmPaint"/>）、ちらつきを抑える（<see cref="ControlStyles.OptimizedDoubleBuffer"/> /<br/>
    /// <see cref="ControlStyles.ResizeRedraw"/>）、親の描画を透かす（<see cref="ControlStyles.SupportsTransparentBackColor"/>）。<br/>
    /// <b>入力は受け取らない</b>（無効状態。 下のズームビューが入力を受け取る）。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="zoomView"/> が null の場合。</exception>
    public RectOverlayCanvas(ZoomViewControl zoomView)
    {
        ArgumentNullException.ThrowIfNull(zoomView);

        SetStyle(
            ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor,
            true);
        BackColor = Color.Transparent;
        Enabled = false;

        _zoomView = zoomView;
    }

    /// <summary>描画する矩形（対象座標）を設定する。</summary>
    /// <param name="rects">描画する矩形。 空の場合は何も描画しない。</param>
    public void SetRects(IReadOnlyList<OverlayRect> rects)
    {
        _rects = rects;
        Invalidate();
    }

    /// <summary>矩形オーバーレイを描画する。</summary>
    /// <param name="e">描画のイベント引数（描画面を持つ）。</param>
    /// <remarks>
    /// ズームビューと同じく描画面を DPI スケーリングしてから DIP 座標で描画する。<br/>
    /// 各矩形の左上・右下を対象座標からビューポート座標へ変換し、コア（<see cref="OverlayRectLayout"/>）が<br/>
    /// 確定した描画入力を描画層へ渡す（クランプ・太さ補正・破線の判定は本メソッドが持たない）。
    /// </remarks>
    protected override void OnPaint(PaintEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPaint(e);

        if (_rects.Count == 0)
        {
            return;
        }

        // スケーリング係数の正規化（0 以下は基準値）は DpiScaler が単一所有する
        var scaling = DpiScaler.NormalizeScaling(_zoomView.ResolveClientScaling());

        var state = e.Graphics.Save();
        e.Graphics.ScaleTransform((float)scaling, (float)scaling);
        try
        {
            foreach (var rect in _rects)
            {
                var topLeft = _zoomView.DisplayedSourceToClient(new EsPoint(rect.Bounds.X, rect.Bounds.Y));
                var bottomRight = _zoomView.DisplayedSourceToClient(new EsPoint(rect.Bounds.Right, rect.Bounds.Bottom));

                RectOverlayGraphicsRenderer.Render(e.Graphics, OverlayRectLayout.Resolve(rect, topLeft, bottomRight));
            }
        }
        finally
        {
            e.Graphics.Restore(state);
        }
    }

    /// <summary>ウィンドウ メッセージを処理する（入力を受け取らず下のズームビューへ通す）。</summary>
    /// <param name="m">処理するメッセージ。</param>
    /// <remarks>入力の透過の規則（対象メッセージと戻り値）は <see cref="OverlayHitTest"/> が単一所有する。</remarks>
    protected override void WndProc(ref Message m)
    {
        if (OverlayHitTest.ShouldPassThrough(m.Msg, Enabled))
        {
            m.Result = OverlayHitTest.Transparent;
            return;
        }

        base.WndProc(ref m);
    }
}
