using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Aprillz.MewUI.Rendering;
using EsUtil.Feature.Annotation;
using EsUtil.Feature.Marquee;
using EsUtil.Feature.Marquee.Core.Contract;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Input;

namespace EsUtil.Ui.MewUI.Annotation;

/// <summary>注釈レイヤーの描画と入力（MewUI 版）。</summary>
/// <remarks>
/// <b>描画の順序</b>: 画像 → 注釈（元画像ピクセルの座標系）→ 選択枠 → しっぽのハンドルの順に描く
/// （選択枠は DIP、注釈は元画像ピクセルで、表示倍率の変換を 1 回だけ掛ける）。<br/>
/// <b>入力は 1 本の経路</b>: 押下 → しっぽの先端 → ハンドル → 注釈の本体（Marquee へ転送）→ 背景の順に判定する
/// （判断はコアと Marquee の提示層が単一所有する）。
/// </remarks>
public sealed partial class AnnotationLayerControl
{
    /// <summary>注釈・画像・選択枠を描く。</summary>
    /// <param name="context">描画先。</param>
    protected override void OnRender(IGraphicsContext context)
    {
        base.OnRender(context);

        var (scale, offset) = ResolveImageTransform();
        DrawAll(context, includeSelection: !_isEditingText, scale, offset);
    }

    /// <summary>画像・注釈・選択枠を描く。</summary>
    /// <param name="context">描画先。</param>
    /// <param name="includeSelection">選択枠としっぽのハンドルを描く場合は <see langword="true"/>。</param>
    /// <param name="scale">表示倍率。</param>
    /// <param name="offset">画像の左上（DIP）。</param>
    private void DrawAll(IGraphicsContext context, bool includeSelection, double scale, Point offset)
    {
        if (ShowImage && ResolveImage() is { } image)
        {
            context.DrawImage(
                image,
                new Rect(offset.X, offset.Y, SourcePixelWidth * scale, SourcePixelHeight * scale));
        }

        if (_document.Items.Length > 0)
        {
            var plan = _presenter.ResolveDrawPlan(_document, AnnotationTextMetrics.Measure, SelectedId);
            _selectedBodyRectSource = Selected is BubbleAnnotation bubble
                ? bubble.ResolveBodyRect()
                : null;

            context.Save();
            try
            {
                context.Translate(offset.X, offset.Y);
                context.Scale(scale, scale);

                foreach (var shape in plan.Shapes)
                {
                    AnnotationPainter.DrawShape(context, shape);
                }

                DrawTextEditing(context, scale);
            }
            finally
            {
                context.Restore();
            }
        }

        if (!includeSelection)
        {
            return;
        }

        DrawSelection(context);
        DrawTailHandle(context);
    }

    /// <summary>選択枠（枠・8 方向のハンドル）を描く。</summary>
    /// <param name="context">描画先。</param>
    private void DrawSelection(IGraphicsContext context)
    {
        if (SelectedId is null)
        {
            return;
        }

        var plan = _marquee.ResolveDrawPlan(
            new EsSize(RenderSize.Width, RenderSize.Height),
            static (text, size) => new EsSize(text.Length * size * 0.5, size),
            false);

        if (plan.Appearance.ShowBorder)
        {
            var color = AnnotationPainter.ToColor(plan.Appearance.BorderColor);
            foreach (var rect in plan.BorderDipRects)
            {
                context.FillRectangle(new Rect(rect.X, rect.Y, rect.Width, rect.Height), color);
            }
        }

        if (plan.Handles is not { } handles)
        {
            return;
        }

        var fill = AnnotationPainter.ToColor(plan.Appearance.HandleColor);
        var border = AnnotationPainter.ToColor(plan.Appearance.HandleBorderColor);
        for (var i = 0; i < MarqueeHandleRects.Count; i++)
        {
            var rect = handles.HandleDipRects[i];
            var mewRect = new Rect(rect.X, rect.Y, rect.Width, rect.Height);
            context.FillRectangle(mewRect, fill);
            context.DrawRectangle(mewRect, border, handles.BorderThickness);
        }
    }

    /// <summary>しっぽのハンドル（円）を描く。</summary>
    /// <param name="context">描画先。</param>
    /// <remarks>
    /// <b>当たり判定と同じ半径で描く</b>（<see cref="TailHandleRadiusDip"/>）。 表示倍率の影響を受けないよう、
    /// 注釈の座標変換の外（DIP）で描く。
    /// </remarks>
    private void DrawTailHandle(IGraphicsContext context)
    {
        if (GetTailHandleCenterDip() is not { } center)
        {
            return;
        }

        var size = HandleSize;
        var fill = AnnotationPainter.ToColor(MarqueeAppearance.Default.HandleColor);
        var border = AnnotationPainter.ToColor(MarqueeAppearance.Default.HandleBorderColor);
        var rect = new Rect(center.X - (size / 2), center.Y - (size / 2), size, size);
        context.FillRectangle(rect, fill);
        context.DrawRectangle(rect, border, 1);
    }

    /// <summary>しっぽの先端のハンドルの中心（DIP）を求める。</summary>
    /// <returns>中心の座標。 吹き出しを選んでいない場合は <see langword="null"/>。</returns>
    internal Point? GetTailHandleCenterDip()
    {
        if (Selected is not BubbleAnnotation bubble)
        {
            return null;
        }

        var center = ToClientDip(bubble.Tail.Tip);
        return new Point(center.X, center.Y);
    }

    /// <summary>ポインターが押されたときの処理（左: 選択・移動・サイズ変更。 右: 編集パネル）。</summary>
    /// <param name="e">ポインターの引数。</param>
    /// <remarks>
    /// <b>注釈の無い場所も本レイヤーが受け取る</b>: 選択の解除と、プレビューへの操作の引き渡しを行う。
    /// 入力を要素の切り替えで振り分けると、切り替えた側が次の押下を受け取れない（実測 2026-11-20）。
    /// </remarks>
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        var position = e.GetPosition(this);

        // 押下をホストへ知らせる（表示中のメニューを外側の押下で閉じるために使う）
        RaisePreviewGesture(AnnotationPreviewGesture.Press, position, 1, 0, e.ShiftKey, e.ControlKey);

        if (e.RightButton)
        {
            HandleRightButton(position);
            e.Handled = true;
            return;
        }

        if (!e.LeftButton)
        {
            return;
        }

        Focus();

        // 押下の間はポインターを捕まえる（MewUI は捕まえないと 2 回目の押下をダブルクリックとして通知しない。 解放は Up で行う）
        CapturePointer();

        // 押下から始まる操作（移動・サイズ変更・しっぽの移動）を 1 手として記録する。
        // 実際に文書が変わらなければ履歴へは積まれない（選択だけのクリックで手数が増えない）
        PrepareUndo();

        var source = ToSource(position);

        if (_isEditingText)
        {
            if (Selected is BubbleAnnotation editingBubble && editingBubble.Bounds.Contains(source))
            {
                CapturePointer();
                _undoCandidate = null;
                _isSelectingText = true;
                SetCaretFromPoint(source, e.ShiftKey);
                InvalidateVisual();
                return;
            }

            EndTextEditing();
        }

        if (Selected is not null)
        {
            if (Selected is BubbleAnnotation selectedBubble && IsNearTailTip(selectedBubble, source))
            {
                CapturePointer();
                _isDraggingTail = true;
                InvalidateVisual();
                return;
            }

            // ハンドルの当たり判定を注釈の当たり判定より先に見る（外周を押したときに選択が解除されない）
            if (HitsSelectionHandle(position))
            {
                CapturePointer();
                _marquee.Press(source, e.ClickCount);
                InvalidateVisual();
                return;
            }
        }

        if (AnnotationHitTest.HitTest(_document, source) is { } hit)
        {
            if (!string.Equals(SelectedId, hit.Id, StringComparison.Ordinal))
            {
                Select(hit.Id);
            }

            if (e.ClickCount >= 2 && hit is BubbleAnnotation)
            {
                CapturePointer();
                BeginTextEditing();
                e.Handled = true;
                return;
            }

            CapturePointer();
            _marquee.Press(source, e.ClickCount);
        }
        else
        {
            Select(null);

            // 注釈の無い場所はプレビューの操作（パン・ダブルクリック）へ渡す
            if (e.ClickCount >= 2)
            {
                RaisePreviewGesture(AnnotationPreviewGesture.DoubleClick, position, e.ClickCount, 0, e.ShiftKey, e.ControlKey);
                return;
            }

            _isPreviewDragging = true;
            RaisePreviewGesture(AnnotationPreviewGesture.DragStart, position, 1, 0, e.ShiftKey, e.ControlKey);
        }

        InvalidateVisual();
    }

    /// <summary>ダブルクリックで本文の編集を始める。</summary>
    /// <param name="e">マウスの引数。</param>
    /// <remarks>
    /// <b>ダブルクリックは専用のイベントで受ける</b>: MewUI は押下の <c>ClickCount</c> を 1 のまま返し、
    /// 2 回目の押下を本イベントとして通知する（実測 2026-11-20）。 <c>ClickCount</c> だけを見ると
    /// 本文の編集を始められない。
    /// </remarks>
    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);

        var position = e.GetPosition(this);
        var source = ToSource(position);
        if (!IsEditingText && AnnotationHitTest.HitTest(_document, source) is BubbleAnnotation)
        {
            CapturePointer();
            BeginTextEditing();
            e.Handled = true;
        }
    }
    /// <summary>ポインターが動いたときの処理（ドラッグとカーソル形状）。</summary>
    /// <param name="e">ポインターの引数。</param>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var position = e.GetPosition(this);
        var source = ToSource(position);
        _lastPointerSource = source;

        // 注釈の無い場所を押したまま動かしたら、プレビューのパンへ渡す
        if (_isPreviewDragging)
        {
            RaisePreviewGesture(AnnotationPreviewGesture.DragMove, position, 1, 0, e.ShiftKey, e.ControlKey);
            return;
        }

        if (_isSelectingText)
        {
            SetCaretFromPoint(source, extend: true);
            return;
        }

        if (_isDraggingTail)
        {
            if (Selected is BubbleAnnotation bubble && _document.SetTailTip(bubble.Id, source))
            {
                RaiseDocumentChanged();
                InvalidateVisual();
            }

            return;
        }

        _marquee.Move(source);

        // 注釈の上に無い間は、プレビューのホバー表示（座標・自動切り出しの強調）へ位置を渡す
        if (!HitTestAt(position))
        {
            RaisePreviewGesture(AnnotationPreviewGesture.Hover, position, 1, 0, e.ShiftKey, e.ControlKey);
        }

        if (Selected is BubbleAnnotation selected && IsNearTailTip(selected, source))
        {
            Cursor = CursorType.Hand;
        }

        InvalidateVisual();
    }

    /// <summary>ポインターが離されたときの処理。</summary>
    /// <param name="e">ポインターの引数。</param>
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (_isSelectingText)
        {
            _isSelectingText = false;
            ReleasePointer();
            InvalidateVisual();
            return;
        }

        if (_isDraggingTail)
        {
            _isDraggingTail = false;
            RaiseDocumentChanged();
            InvalidateVisual();
            return;
        }

        if (_isPreviewDragging)
        {
            _isPreviewDragging = false;
            RaisePreviewGesture(AnnotationPreviewGesture.DragEnd, e.GetPosition(this), 1, 0, e.ShiftKey, e.ControlKey);
            ReleasePointer();
            return;
        }

        _marquee.Release(ToSource(e.GetPosition(this)));
        ReleasePointer();
        InvalidateVisual();
    }

    /// <summary>ホイールが回されたときの処理（ズーム・スクロールはプレビューへ渡す）。</summary>
    /// <param name="e">ホイールの引数。</param>
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);

        RaisePreviewGesture(
            AnnotationPreviewGesture.Wheel, e.GetPosition(this), 1, (int)e.Delta.Y, e.ShiftKey, e.ControlKey);
        e.Handled = true;
    }

    /// <summary>ポインターが領域から外れたときの処理（プレビューのホバー表示を消す）。</summary>
    protected override void OnMouseLeave()
    {
        base.OnMouseLeave();

        if (!_isPreviewDragging && !_isEditingText)
        {
            RaisePreviewGesture(AnnotationPreviewGesture.Leave, default, 1, 0, false, false);
        }
    }

    /// <summary>この位置が入力の対象かどうかを判定する。</summary>
    /// <param name="point">位置（このコントロールの座標）。</param>
    /// <returns>このレイヤー（注釈レイヤーがプレビュー領域の入力を単一所有する）。</returns>
    /// <remarks>
    /// <b>下地を描かない面でも入力を受ける</b>: 画像の上へ重ねるため背景を塗らないが、MewUI の
    /// パネルは背景の無い面をヒットテストの対象にしない。 常に対象とする（実測 2026-11-20）。
    /// </remarks>
    protected override UIElement OnHitTest(Point point)
        => IsVisible && IsHitTestVisible
            && point.X >= 0 && point.Y >= 0 && point.X < RenderSize.Width && point.Y < RenderSize.Height
                ? this
                : null!;

    /// <summary>キーが押されたときの処理（本文の編集・削除・nudge）。</summary>
    /// <param name="e">キーの引数。</param>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (_isEditingText && HandleTextEditingKey(e))
        {
            e.Handled = true;
            return;
        }

        if (e.ControlKey && e.Key is Key.Z or Key.Y)
        {
            e.Handled = e.Key == Key.Z ? Undo() : Redo();
            return;
        }

        if (e.Key == Key.Delete)
        {
            e.Handled = DeleteSelected();
            return;
        }

        if (e.Key == Key.Enter && Selected is BubbleAnnotation)
        {
            BeginTextEditing();
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            PrepareUndo();
            e.Handled = _marquee.KeyDown(ToEsKey(e.Key), _lastPointerSource);
            InvalidateVisual();
        }
    }

    /// <summary>フォーカスを失ったときの処理。</summary>
    /// <remarks>
    /// <b>編集は終えない</b>: 書式バーはポップアップへ出すため、開くとフォーカスが一時的に移る。
    /// ここで編集を終えると書式バーが出た瞬間に編集が終わってしまう（実測 2026-11-20）。
    /// 編集を終えるのは、外側のクリック・Esc・Ctrl+Enter のいずれかである。
    /// </remarks>
    protected override void OnLostFocus() => base.OnLostFocus();

    /// <summary>右クリックを処理する（注釈の上なら編集パネルを出す。 注釈の外なら追加メニューを要求する）。</summary>
    /// <param name="position">位置（DIP）。</param>
    private void HandleRightButton(Point position)
    {
        var source = ToSource(position);

        // 外観フライアウトは注釈（枠線・吹き出し）の上を右クリックしたときだけ出す。
        // 注釈の外は「枠線・吹き出しを追加」のメニューにする（選択も解除する）。
        // 追加できるのは画像の内側だけである（規則はコアが単一所有する）
        if (AnnotationHitTest.HitTest(_document, source) is not { } hit)
        {
            Select(null);
            if (AnnotationGeometry.Contains(source, _document.ImageSize))
            {
                AddMenuRequested?.Invoke(
                    this, new AnnotationAddMenuRequestedEventArgs(position.X, position.Y));
            }

            return;
        }

        if (!string.Equals(SelectedId, hit.Id, StringComparison.Ordinal))
        {
            Select(hit.Id);
        }

        _styleEditorX = position.X;
        _styleEditorY = position.Y;

        Focus();
        ShowStyleEditor();
    }

    /// <summary>プレビューへ操作を渡す。</summary>
    /// <param name="gesture">操作の種類。</param>
    /// <param name="position">位置（ローカル DIP）。</param>
    /// <param name="clickCount">クリック回数。</param>
    /// <param name="wheelDelta">ホイールの移動量。</param>
    /// <param name="shift">Shift キーが押されているかどうか。</param>
    /// <param name="control">Ctrl キーが押されているかどうか。</param>
    private void RaisePreviewGesture(
        AnnotationPreviewGesture gesture,
        Point position,
        int clickCount,
        int wheelDelta,
        bool shift,
        bool control)
        => PreviewGestureRequested?.Invoke(
            this,
            new AnnotationPreviewGestureEventArgs(
                gesture, position.X, position.Y, clickCount, wheelDelta, shift, control));

    /// <summary>選択中の注釈のハンドルを押したかどうかを判定する。</summary>
    /// <param name="dip">位置（DIP）。</param>
    /// <returns>ハンドルの上なら <see langword="true"/>。</returns>
    private bool HitsSelectionHandle(Point dip)
    {
        if (SelectedId is null)
        {
            return false;
        }

        var plan = _marquee.ResolveDrawPlan(
            new EsSize(RenderSize.Width, RenderSize.Height),
            static (text, size) => new EsSize(text.Length * size * 0.5, size),
            false);
        if (plan.Handles is not { } handles)
        {
            return false;
        }

        var radius = HandleHitRadius;
        var squared = radius * radius;
        for (var i = 0; i < MarqueeHandleRects.Count; i++)
        {
            var rect = handles.HandleDipRects[i];
            var centerX = rect.X + (rect.Width / 2);
            var centerY = rect.Y + (rect.Height / 2);
            var dx = centerX - dip.X;
            var dy = centerY - dip.Y;
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

        _isApplyingMarquee = true;
        try
        {
            // 画像の外へは出さない（収める規則はコアが単一所有する）
            if (_document.SetBounds(item.Id, rect))
            {
                RaiseDocumentChanged();
            }
        }
        finally
        {
            _isApplyingMarquee = false;
        }

        InvalidateVisual();
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

    /// <summary>ポインターのキャプチャを取得する（コントロールの外へ出ても移動を受け取る）。</summary>
    private void CapturePointer()
    {
        if (FindVisualRoot() is Window window)
        {
            window.CaptureMouse(this);
        }
    }

    /// <summary>ポインターのキャプチャを解放する。</summary>
    private void ReleasePointer()
    {
        if (FindVisualRoot() is Window window)
        {
            window.ReleaseMouseCapture();
        }
    }

    /// <summary>MewUI のキーをコアのキーへ写す（nudge に使う 4 方向だけ）。</summary>
    /// <param name="key">MewUI のキー。</param>
    /// <returns>コアのキー。 対象外は <see cref="EsKey.None"/>。</returns>
    private static EsKey ToEsKey(Key key) => key switch
    {
        Key.Left => EsKey.Left,
        Key.Right => EsKey.Right,
        Key.Up => EsKey.Up,
        Key.Down => EsKey.Down,
        _ => EsKey.None,
    };

    /// <summary>Marquee のコアが要求する効果を MewUI のコントロールへ写すアダプタ。</summary>
    /// <remarks>
    /// <b>効果だけを実装する</b>: 選択の判断・ハンドルの当たり判定・nudge は Marquee のコアが単一所有する
    /// （自前で書き直すと 4 つの UI 版で挙動がずれる）。
    /// </remarks>
    private sealed class MarqueeEffects(AnnotationLayerControl owner) : MarqueeEffectsBase
    {
        /// <inheritdoc/>
        public override void RequestFocus() => owner.Focus();

        /// <inheritdoc/>
        public override void RequestRedraw() => owner.InvalidateVisual();

        /// <inheritdoc/>
        public override void SetCursorShape(MarqueeCursorKind kind)
        {
            owner.Cursor = kind switch
            {
                MarqueeCursorKind.DiagonalNWSE => CursorType.SizeNWSE,
                MarqueeCursorKind.DiagonalNESW => CursorType.SizeNESW,
                MarqueeCursorKind.Vertical => CursorType.SizeNS,
                MarqueeCursorKind.Horizontal => CursorType.SizeWE,
                MarqueeCursorKind.Move => CursorType.SizeAll,
                _ => null,
            };
        }

        /// <inheritdoc/>
        public override void SetPointerCapture(bool capture)
        {
            if (capture)
            {
                owner.CapturePointer();
            }
            else
            {
                owner.ReleasePointer();
            }
        }

        /// <inheritdoc/>
        public override bool? TryMoveCursor(EsPointInt screenPoint)
            => owner._cursor.TrySetCursorScreenPoint(screenPoint);

        /// <inheritdoc/>
        /// <remarks>
        /// <b>ポインタは隠さない</b>: 隠すと入力位置を見失い、他のウィンドウへフォーカスするまで戻らない
        /// （Win32 の <c>ShowCursor</c> はカウンタ式のため、対応が崩れると復元できない）。 nudge 中も
        /// 表示したままにする（実測 2026-11-21）。
        /// </remarks>
        public override void ShowCursor(bool show) => _ = show;

        /// <inheritdoc/>
        public override EsPointInt ToScreenPoint(EsPoint sourcePoint)
        {
            if (owner.FindVisualRoot() is not Window window)
            {
                return default;
            }

            var localDip = owner.ToClientDip(sourcePoint);
            var screen = window.ClientToScreen(new Point(localDip.X, localDip.Y));
            return new EsPointInt((int)screen.X, (int)screen.Y);
        }
    }
}