using System.Drawing.Drawing2D;
using EsUtil.Feature.Annotation;
using EsUtil.Feature.Marquee;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Input;

namespace EsUtil.Ui.WinForms.Annotation;

/// <summary>注釈レイヤーの描画と入力（WinForms 版）。</summary>
/// <remarks>
/// <b>描画の順序</b>: 画像 → 注釈 → 選択枠 → しっぽのハンドル（画像と注釈は元画像ピクセル、選択枠は DIP）。<br/>
/// <b>入力は 1 本の経路</b>: 押下 → しっぽの先端 → ハンドル → 注釈の本体（Marquee へ転送）→ 背景の順に判定する。
/// </remarks>
public sealed partial class AnnotationLayerControl
{
    /// <summary>最後に押下した時刻（ダブルクリックの判定）。</summary>
    private DateTime _lastPressedAt = DateTime.MinValue;

    /// <summary>最後に押下した位置（ダブルクリックの判定）。</summary>
    private Point _lastPressedPoint;

    /// <summary>再描画と選択枠の更新を行う（画像・注釈・選択枠を今の状態へ合わせる）。</summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        var (scale, offset) = ResolveImageTransform();
        if (ShowImage && _image is { } image)
        {
            graphics.DrawImage(
                image,
                new RectangleF(
                    (float)offset.X,
                    (float)offset.Y,
                    (float)(SourcePixelWidth * scale),
                    (float)(SourcePixelHeight * scale)));
        }

        var plan = _presenter.ResolveDrawPlan(_document, AnnotationTextMetrics.Measure, SelectedId);
        AnnotationPainter.Draw(graphics, plan, scale, offset.X, offset.Y, IsEditingText ? SelectedId : null);

        DrawSelection(graphics);
        DrawEditing(graphics);
    }

    /// <summary>選択枠としっぽのハンドルを描く。</summary>
    /// <param name="graphics">描画先（DIP）。</param>
    private void DrawSelection(Graphics graphics)
    {
        if (SelectedId is null)
        {
            return;
        }

        var plan = _marquee.ResolveDrawPlan(
            new EsSize(ClientSize.Width, ClientSize.Height),
            static (text, size) => new EsSize(text.Length * size * 0.5, size),
            false);

        List<EsRect>? handles = null;
        if (plan.Handles is { } source)
        {
            handles = new List<EsRect>(8);
            for (var i = 0; i < 8; i++)
            {
                handles.Add(source.HandleDipRects[i]);
            }
        }

        AnnotationPainter.DrawSelection(
            graphics,
            plan.BorderDipRects,
            plan.Appearance.BorderColor,
            handles,
            plan.Appearance.HandleColor,
            plan.Appearance.HandleBorderColor);

        if (GetTailHandleCenterDip() is not { } center)
        {
            return;
        }

        AnnotationPainter.DrawTailHandle(
            graphics,
            new PointF((float)center.X, (float)center.Y),
            HandleSize,
            plan.Appearance.HandleColor,
            plan.Appearance.HandleBorderColor);
    }

    /// <summary>しっぽの先端のハンドルの中心（DIP）を求める。</summary>
    /// <returns>中心の座標。 吹き出しを選んでいない場合は <see langword="null"/>。</returns>
    private EsPoint? GetTailHandleCenterDip()
    {
        if (Selected is not BubbleAnnotation bubble)
        {
            return null;
        }

        return ToClientDip(bubble.Tail.Tip);
    }

    /// <summary>マウスが押されたときの処理。</summary>
    /// <param name="e">マウスの引数。</param>
    protected override void OnMouseDown(MouseEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnMouseDown(e);

        var position = new EsPoint(e.X, e.Y);
        if (e.Button == MouseButtons.Right)
        {
            HandleRightButton(position);
            return;
        }

        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        Focus();
        Capture = true;
        PrepareUndo();

        var source = ToSource(position);
        var now = DateTime.UtcNow;
        var isDoubleClick = now - _lastPressedAt < TimeSpan.FromMilliseconds(500)
            && Math.Abs(position.X - _lastPressedPoint.X) < 4
            && Math.Abs(position.Y - _lastPressedPoint.Y) < 4;
        _lastPressedAt = now;
        _lastPressedPoint = new Point((int)position.X, (int)position.Y);
        _lastPointerSource = source;

        if (IsEditingText)
        {
            // 編集中は本文の内側を押したらキャレットを移す（外側を押したら編集を終える）
            if (Selected is BubbleAnnotation editingBubble && editingBubble.Bounds.Contains(source))
            {
                _undoCandidate = null;
                SetCaretFromPoint(source, (ModifierKeys & Keys.Shift) == Keys.Shift);
                return;
            }

            EndTextEditing();
        }

        if (Selected is { } selected)
        {
            if (selected is BubbleAnnotation bubble && IsNearTailTip(bubble, source))
            {
                _isDraggingTail = true;
                return;
            }

            if (HitsSelectionHandle(position))
            {
                _marquee.Press(source, 1);
                return;
            }
        }

        if (AnnotationHitTest.HitTest(_document, source) is not { } hit)
        {
            Select(null);
            return;
        }

        Select(hit.Id);
        if (isDoubleClick && hit is BubbleAnnotation)
        {
            BeginTextEditing();
            return;
        }

        _marquee.Press(source, 1);
    }

    /// <summary>マウスが動いたときの処理。</summary>
    /// <param name="e">マウスの引数。</param>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnMouseMove(e);

        var source = ToSource(new EsPoint(e.X, e.Y));
        _lastPointerSource = source;

        if (_isDraggingTail)
        {
            if (Selected is BubbleAnnotation bubble)
            {
                bubble.Tail = bubble.Tail with { Tip = source };
                RaiseDocumentChanged();
                Invalidate();
            }

            return;
        }

        _marquee.Move(source);
        Invalidate();
    }

    /// <summary>マウスが離されたときの処理。</summary>
    /// <param name="e">マウスの引数。</param>
    protected override void OnMouseUp(MouseEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnMouseUp(e);

        var source = ToSource(new EsPoint(e.X, e.Y));
        if (_isDraggingTail)
        {
            _isDraggingTail = false;
            RaiseDocumentChanged();
            Invalidate();
        }
        else
        {
            _marquee.Release(source);
        }

        Capture = false;
        Invalidate();
    }

    /// <summary>キーが押されたときの処理（本文の編集・削除・nudge）。</summary>
    /// <param name="e">キーの引数。</param>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnKeyDown(e);

        if (IsEditingText)
        {
            if (HandleTextEditingKey(e))
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
            }

            return;
        }

        if (e.Control && e.KeyCode == Keys.Z)
        {
            e.Handled = Undo();
            return;
        }

        if (e.Control && e.KeyCode == Keys.Y)
        {
            e.Handled = Redo();
            return;
        }

        if (e.KeyCode == Keys.Delete)
        {
            e.Handled = DeleteSelected();
            return;
        }

        if (e.KeyCode == Keys.Enter && Selected is BubbleAnnotation)
        {
            BeginTextEditing();
            e.Handled = true;
            return;
        }

        if (e.KeyCode is Keys.Left or Keys.Right or Keys.Up or Keys.Down)
        {
            PrepareUndo();
            e.Handled = _marquee.KeyDown(ToEsKey(e.KeyCode), _lastPointerSource);
            Invalidate();
        }
    }

    /// <summary>文字入力を受け取る（IME の変換中は受け取らない）。</summary>
    /// <param name="e">キーの引数。</param>
    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnKeyPress(e);

        if (!IsEditingText || _isComposing || e.Handled)
        {
            return;
        }

        if (char.IsControl(e.KeyChar))
        {
            return;
        }

        ReplaceSelection(e.KeyChar.ToString());
        e.Handled = true;
    }

    /// <summary>キーをコアのキーへ写す（nudge に使う 4 方向だけ）。</summary>
    /// <param name="key">WinForms のキー。</param>
    /// <returns>コアのキー。 対象外は <see cref="EsKey.None"/>。</returns>
    private static EsKey ToEsKey(Keys key) => key switch
    {
        Keys.Left => EsKey.Left,
        Keys.Right => EsKey.Right,
        Keys.Up => EsKey.Up,
        Keys.Down => EsKey.Down,
        _ => EsKey.None,
    };

    /// <summary>右クリックを処理する（選択してから編集パネルを出す）。</summary>
    /// <param name="position">位置（DIP）。</param>
    private void HandleRightButton(EsPoint position)
    {
        var source = ToSource(position);
        if (AnnotationHitTest.HitTest(_document, source) is { } hit)
        {
            Select(hit.Id);
        }

        if (Selected is null)
        {
            return;
        }

        ShowStyleEditor();
    }

    /// <summary>選択中の注釈のハンドルを押したかどうかを判定する。</summary>
    /// <param name="position">位置（DIP）。</param>
    /// <returns>ハンドルの上なら <see langword="true"/>。</returns>
    private bool HitsSelectionHandle(EsPoint position)
    {
        if (SelectedId is null)
        {
            return false;
        }

        var plan = _marquee.ResolveDrawPlan(
            new EsSize(ClientSize.Width, ClientSize.Height),
            static (text, size) => new EsSize(text.Length * size * 0.5, size),
            false);
        if (plan.Handles is not { } handles)
        {
            return false;
        }

        var radius = _marquee.Options.HandleHitRadius;
        var squared = radius * radius;
        for (var i = 0; i < 8; i++)
        {
            var rect = handles.HandleDipRects[i];
            var dx = (rect.X + (rect.Width / 2)) - position.X;
            var dy = (rect.Y + (rect.Height / 2)) - position.Y;
            if ((dx * dx) + (dy * dy) <= squared)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Marquee からの通知を文書へ反映する（移動・サイズ変更）。</summary>
    /// <param name="notice">通知。</param>
    private void OnMarqueeNotice(MarqueeNotice notice)
    {
        if (_isApplyingMarquee || SelectedId is null)
        {
            return;
        }

        if (notice.Kind is not (MarqueeNoticeKind.SelectionChanged or MarqueeNoticeKind.SelectionCompleted))
        {
            return;
        }

        var rect = notice.Snapshot.SelectionSourceRect;
        if (rect.IsEmpty || _document.Find(SelectedId) is not { } item)
        {
            return;
        }

        if (item.Bounds.X == rect.X && item.Bounds.Y == rect.Y
            && item.Bounds.Width == rect.Width && item.Bounds.Height == rect.Height)
        {
            return;
        }

        _isApplyingMarquee = true;
        try
        {
            item.Bounds = rect;
            RaiseDocumentChanged();
        }
        finally
        {
            _isApplyingMarquee = false;
        }

        Invalidate();
    }

    /// <summary>しっぽの先端の近くかどうかを判定する（描画したハンドルと同じ半径）。</summary>
    /// <param name="bubble">対象の吹き出し。</param>
    /// <param name="source">位置（元画像ピクセル）。</param>
    /// <returns>近くの場合は <see langword="true"/>。</returns>
    private bool IsNearTailTip(BubbleAnnotation bubble, EsPoint source)
    {
        var (scale, _) = ResolveImageTransform();
        var radius = TailHandleRadiusDip / Math.Max(scale, 0.0001);
        var dx = bubble.Tail.Tip.X - source.X;
        var dy = bubble.Tail.Tip.Y - source.Y;
        return (dx * dx) + (dy * dy) <= radius * radius;
    }
}
