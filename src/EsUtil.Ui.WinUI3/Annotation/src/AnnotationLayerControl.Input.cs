using EsUtil.Feature.Annotation;
using EsUtil.Feature.Marquee;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.System;

namespace EsUtil.Ui.WinUI3.Annotation;

/// <summary>注釈レイヤーの描画と入力（WinUI 3 版）。</summary>
/// <remarks>
/// <b>描画の順序</b>: 画像 → 注釈 → 選択枠 → しっぽのハンドル（画像と注釈は元画像ピクセル、選択枠は DIP）。<br/>
/// <b>入力は 1 本の経路</b>: 押下 → しっぽの先端 → ハンドル → 注釈の本体（Marquee へ転送）→ 背景の順に判定する
/// （判断はコアと Marquee の提示層が単一所有する）。
/// </remarks>
public sealed partial class AnnotationLayerControl
{
    /// <summary>ポインター キャプチャ中のポインター（解放に使う）。</summary>
    private Microsoft.UI.Xaml.Input.Pointer? _capturedPointer;

    /// <summary>注釈の無い場所でドラッグしている間かどうか（プレビューのパンへ渡す）。</summary>
    private bool _isPreviewDragging;

    /// <summary>最後に押下した時刻（ダブルクリックの判定）。</summary>
    private DateTimeOffset _lastPressedAt = DateTimeOffset.MinValue;

    /// <summary>最後に押下した位置（ダブルクリックの判定）。</summary>
    private EsPoint _lastPressedPoint;

    /// <summary>再描画する（画像・注釈・選択枠を今の状態へ合わせる）。</summary>
    internal void Redraw()
    {
        var (scale, offset) = ResolveImageTransform();

        ApplyImageSource();
        if (!ShowImage)
        {
            _image.Visibility = Visibility.Collapsed;
        }
        else if (_image.Source is not null && ActualWidth > 0 && ActualHeight > 0)
        {
            _image.Width = SourcePixelWidth * scale;
            _image.Height = SourcePixelHeight * scale;
            _image.Margin = new Thickness(offset.X, offset.Y, 0, 0);
            _image.Visibility = Visibility.Visible;
        }
        else
        {
            _image.Visibility = Visibility.Collapsed;
        }

        var plan = _presenter.ResolveDrawPlan(_document, MeasureText, SelectedId);
        AnnotationPainter.Draw(
            _shapes,
            plan,
            scale,
            offset.X,
            offset.Y,
            IsEditingText ? SelectedId : null,
            MeasureText);

        DrawEditing();

        DrawSelection();
        Clip = new RectangleGeometry
        {
            Rect = new Windows.Foundation.Rect(0, 0, Math.Max(0, ActualWidth), Math.Max(0, ActualHeight)),
        };

    }

    /// <summary>再描画を要求する。</summary>
    internal void Invalidate() => Redraw();

    /// <summary>表示用の画像を作り直す（同じ画像なら作り直さない）。</summary>
    private void ApplyImageSource()
    {
        if (_imageBitmap is not null || Source is not { IsDisposed: false } source)
        {
            _image.Source = _imageBitmap;
            return;
        }

        var bitmap = new WriteableBitmap(source.Width, source.Height);
        var pixels = source.ReadOnlyPixels.ToArray();
        using (var stream = bitmap.PixelBuffer.AsStream())
        {
            stream.Write(pixels, 0, pixels.Length);
        }

        _imageBitmap = bitmap;
        _image.Source = bitmap;
    }

    /// <summary>Marquee の要求に従ってポインター キャプチャを取得・解放する。</summary>
    /// <param name="capture">取得する場合は <see langword="true"/>。</param>
    internal void ApplyPointerCapture(bool capture)
    {
        if (capture)
        {
            if (_capturedPointer is { } pointer)
            {
                CapturePointer(pointer);
            }

            return;
        }

        if (_capturedPointer is { } captured)
        {
            ReleasePointerCapture(captured);
        }
    }

    /// <summary>選択枠としっぽのハンドルを描く。</summary>
    private void DrawSelection()
    {
        if (SelectedId is null)
        {
            AnnotationPainter.DrawSelection(
                _selection, [], AnnotationDefaults.FrameAppearance.Border, null, AnnotationDefaults.FrameAppearance.Border,
                AnnotationDefaults.FrameAppearance.Border);
            return;
        }

        var plan = _marquee.ResolveDrawPlan(
            new EsSize(Math.Max(0, ActualWidth), Math.Max(0, ActualHeight)),
            static (text, size) => new EsSize(text.Length * size * 0.5, size),
            false);

        var handles = plan.Handles is { } value
            ? new List<EsRect>(8)
            : null;

        if (plan.Handles is { } source)
        {
            for (var i = 0; i < 8; i++)
            {
                handles!.Add(source.HandleDipRects[i]);
            }
        }

        AnnotationPainter.DrawSelection(
            _selection,
            plan.BorderDipRects,
            plan.Appearance.BorderColor,
            handles,
            plan.Appearance.HandleColor,
            plan.Appearance.HandleBorderColor);

        if (GetTailHandleCenterDip() is not { } center)
        {
            return;
        }

        var size = HandleSize;
        var handle = new Microsoft.UI.Xaml.Shapes.Rectangle
        {
            Width = size,
            Height = size,
            Fill = AnnotationTextMetrics.ToBrush(plan.Appearance.HandleColor),
            Stroke = AnnotationTextMetrics.ToBrush(plan.Appearance.HandleBorderColor),
            StrokeThickness = 1,
        };

        Canvas.SetLeft(handle, center.X - (size / 2));
        Canvas.SetTop(handle, center.Y - (size / 2));
        _selection.Children.Add(handle);
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

    /// <summary>ポインターが押されたときの処理。</summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">ポインターの引数。</param>
    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _ = sender;

        var point = e.GetCurrentPoint(this);
        var position = new EsPoint(point.Position.X, point.Position.Y);

        if (point.Properties.IsRightButtonPressed)
        {
            HandleRightButton(position);
            e.Handled = true;
            return;
        }

        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        _ = Focus(FocusState.Pointer);

        // 押下から始まる操作（移動・サイズ変更・しっぽの移動）を 1 手として記録する
        PrepareUndo();

        var source = ToSource(position);
        var now = DateTimeOffset.UtcNow;
        var isDoubleClick = now - _lastPressedAt < TimeSpan.FromMilliseconds(500)
            && Math.Abs(position.X - _lastPressedPoint.X) < 4
            && Math.Abs(position.Y - _lastPressedPoint.Y) < 4;
        _lastPressedAt = now;
        _lastPressedPoint = position;

        if (IsEditingText)
        {
            // 編集中は本文の内側を押したらキャレットを移す（外側を押したら編集を終える）
            if (Selected is BubbleAnnotation editingBubble && editingBubble.Bounds.Contains(source))
            {
                CapturePointerHere(e);
                _undoCandidate = null;
                SetCaretFromPoint(source, IsDown(VirtualKey.Shift));
                e.Handled = true;
                return;
            }

            EndTextEditing();
        }

        if (Selected is { } selected)
        {
            if (selected is BubbleAnnotation bubble && IsNearTailTip(bubble, source))
            {
                CapturePointerHere(e);
                _isDraggingTail = true;
                e.Handled = true;
                return;
            }

            if (HitsSelectionHandle(position))
            {
                CapturePointerHere(e);
                _marquee.Press(source, 1);
                e.Handled = true;
                return;
            }
        }

        if (AnnotationHitTest.HitTest(_document, source) is not { } hit)
        {
            Select(null);

            // 注釈の無い場所はプレビューの操作（パン・ダブルクリック）へ渡す
            if (isDoubleClick)
            {
                RaisePreviewGesture(AnnotationPreviewGesture.DoubleClick, point.Position, 2, 0);
                return;
            }

            _isPreviewDragging = true;
            RaisePreviewGesture(AnnotationPreviewGesture.DragStart, point.Position, 1, 0);
            e.Handled = true;
            return;
        }

        Select(hit.Id);
        if (isDoubleClick && hit is BubbleAnnotation)
        {
            CapturePointerHere(e);
            BeginTextEditing();
            e.Handled = true;
            return;
        }

        CapturePointerHere(e);
        _marquee.Press(source, 1);
        e.Handled = true;
    }

    /// <summary>ポインターのキャプチャを取得する（注釈の操作を始めるときだけ呼ぶ）。</summary>
    /// <param name="e">押下の引数。</param>
    /// <remarks>
    /// <b>注釈の無い場所ではキャプチャしない</b>: プレビューのパンはキャプチャ不要であり、
    /// キャプチャが残るとカーソルがウィンドウへ掴まれたままになる（実測 2026-11-20）。
    /// </remarks>
    private void CapturePointerHere(PointerRoutedEventArgs e)
    {
        _capturedPointer = e.Pointer;
        CapturePointer(e.Pointer);
    }

    /// <summary>ポインターが動いたときの処理。</summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">ポインターの引数。</param>
    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        _ = sender;

        var point = e.GetCurrentPoint(this);
        var source = ToSource(new EsPoint(point.Position.X, point.Position.Y));
        _lastPointerSource = source;

        if (_isDraggingTail)
        {
            if (Selected is BubbleAnnotation bubble)
            {
                bubble.Tail = bubble.Tail with { Tip = source };
                RaiseDocumentChanged();
                Redraw();
            }

            return;
        }

        // 注釈の無い場所を押したまま動かしたら、プレビューのパンへ渡す
        if (_isPreviewDragging)
        {
            RaisePreviewGesture(AnnotationPreviewGesture.DragMove, point.Position, 1, 0);
            return;
        }

        _marquee.Move(source);

        // 注釈の上に無い間は、プレビューのホバー表示（座標・自動切り出しの強調）へ位置を渡す
        if (!HitTestAt(point.Position))
        {
            RaisePreviewGesture(AnnotationPreviewGesture.Hover, point.Position, 1, 0);
        }

        Redraw();
    }

    /// <summary>ポインターが離されたときの処理。</summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">ポインターの引数。</param>
    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _ = sender;

        var point = e.GetCurrentPoint(this);
        var source = ToSource(new EsPoint(point.Position.X, point.Position.Y));

        if (_isPreviewDragging)
        {
            _isPreviewDragging = false;
            RaisePreviewGesture(AnnotationPreviewGesture.DragEnd, point.Position, 1, 0);
        }
        else if (_isDraggingTail)
        {
            _isDraggingTail = false;
            RaiseDocumentChanged();
            Redraw();
        }
        else
        {
            _marquee.Release(source);
        }

        if (_capturedPointer is { } captured)
        {
            ReleasePointerCapture(captured);
            _capturedPointer = null;
        }

        Redraw();
    }

    /// <summary>キーが押されたときの処理（本文の編集・削除・nudge）。</summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">キーの引数。</param>
    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        _ = sender;

        if (IsEditingText)
        {
            if (HandleTextEditingKey(e))
            {
                e.Handled = true;
            }

            return;
        }

        var control = IsDown(VirtualKey.Control);
        switch (e.Key)
        {
            case VirtualKey.Z when control:
                e.Handled = Undo();
                return;
            case VirtualKey.Y when control:
                e.Handled = Redo();
                return;
            case VirtualKey.Delete:
                e.Handled = DeleteSelected();
                return;
            case VirtualKey.Enter or VirtualKey.GamepadA when Selected is BubbleAnnotation:
                BeginTextEditing();
                e.Handled = true;
                return;
            case VirtualKey.Left:
            case VirtualKey.Right:
            case VirtualKey.Up:
            case VirtualKey.Down:
                PrepareUndo();
                e.Handled = _marquee.KeyDown(ToEsKey(e.Key), _lastPointerSource);
                Redraw();
                return;
        }
    }

    /// <summary>修飾キーが押されているかどうかを判定する。</summary>
    /// <param name="key">修飾キー。</param>
    /// <returns>押されている場合は <see langword="true"/>。</returns>
    private static bool IsDown(VirtualKey key)
    {
        var state = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(key);
        return (state & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;
    }

    /// <summary>キーをコアのキーへ写す（nudge に使う 4 方向だけ）。</summary>
    /// <param name="key">WinUI 3 のキー。</param>
    /// <returns>コアのキー。 対象外は <see cref="EsKey.None"/>。</returns>
    private static EsKey ToEsKey(VirtualKey key) => key switch
    {
        VirtualKey.Left => EsKey.Left,
        VirtualKey.Right => EsKey.Right,
        VirtualKey.Up => EsKey.Up,
        VirtualKey.Down => EsKey.Down,
        _ => EsKey.None,
    };

    /// <summary>右クリックを処理する（注釈の上なら編集パネルを出す。 注釈の外なら追加メニューを要求する）。</summary>
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
            // 注釈の外は「枠線・吹き出しを追加」のメニューを出す（見た目はホストが決める）
            AddMenuRequested?.Invoke(this, new AnnotationAddMenuRequestedEventArgs(position.X, position.Y));
            return;
        }

        ShowStyleEditor();
    }

    /// <summary>プレビューへ操作を渡す。</summary>
    /// <param name="gesture">操作の種類。</param>
    /// <param name="position">位置（ローカル DIP）。</param>
    /// <param name="clickCount">クリック回数。</param>
    /// <param name="wheelDelta">ホイールの移動量。</param>
    private void RaisePreviewGesture(
        AnnotationPreviewGesture gesture,
        Windows.Foundation.Point position,
        int clickCount,
        int wheelDelta)
        => PreviewGestureRequested?.Invoke(
            this,
            new AnnotationPreviewGestureEventArgs(
                gesture,
                position.X,
                position.Y,
                clickCount,
                wheelDelta,
                IsDown(VirtualKey.Shift),
                IsDown(VirtualKey.Control)));

    /// <summary>ホイールが回されたときの処理（ズーム・スクロールはプレビューへ渡す）。</summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">ポインターの引数。</param>
    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        _ = sender;

        RaisePreviewGesture(
            AnnotationPreviewGesture.Wheel,
            e.GetCurrentPoint(this).Position,
            1,
            e.GetCurrentPoint(this).Properties.MouseWheelDelta);
        e.Handled = true;
    }

    /// <summary>ポインターが領域から外れたときの処理（プレビューのホバー表示を消す）。</summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">ポインターの引数。</param>
    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        _ = sender;

        if (!_isPreviewDragging && !IsEditingText)
        {
            RaisePreviewGesture(AnnotationPreviewGesture.Leave, e.GetCurrentPoint(this).Position, 1, 0);
        }
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
            new EsSize(Math.Max(0, ActualWidth), Math.Max(0, ActualHeight)),
            static (text, size) => new EsSize(text.Length * size * 0.5, size),
            false);
        if (plan.Handles is not { } handles)
        {
            return false;
        }

        var radius = HandleHitRadius;
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

        Redraw();
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
