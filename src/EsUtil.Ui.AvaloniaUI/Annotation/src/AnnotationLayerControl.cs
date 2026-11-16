using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using EsUtil.Feature.Annotation;
using EsUtil.Feature.Marquee;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Ui.AvaloniaUI.Annotation;

/// <summary>画像の上へ注釈（枠線・吹き出し）を重ねるレイヤー コントロール（Avalonia UI 版）。</summary>
/// <remarks>
/// <b>役割</b>: 画像の表示・注釈の描画・入力の受付を担う。 判断（当たり判定・折り返し・描画計画）は
/// <c>EsUtil.Feature.Annotation</c> が単一所有し、本クラスは自 UI の描画 API へ写すだけである。<br/>
/// <b>選択枠とハンドルは Marquee の提示層を再利用する</b>（枠・ハンドル・nudge・カーソルを 4 つの UI 版で揃えるため）。<br/>
/// <b>本文の編集はホストが行う</b>: ダブルクリックで <see cref="EditTextRequested"/> を発生させ、ホストが入力欄を
/// 重ねる（コントロールは子要素を持たないため、入力欄の生成は UI 版のホストの仕事にする）。 確定した文字列は
/// <see cref="SetSelectedText"/> で戻す。<br/>
/// <b>座標</b>: 注釈は元画像ピクセルで保持し、描画時は表示倍率で写す（画像へ焼き込まない）。
/// </remarks>
public sealed class AnnotationLayerControl : Control
{
    /// <summary>画像を取得または設定する。</summary>
    /// <value>背景に表示する画像。 null の場合は注釈だけを描く。</value>
    public static readonly StyledProperty<IImage?> SourceProperty =
        AvaloniaProperty.Register<AnnotationLayerControl, IImage?>(nameof(Source));

    /// <summary>元画像の幅（ピクセル）を取得または設定する。</summary>
    public static readonly StyledProperty<int> SourcePixelWidthProperty =
        AvaloniaProperty.Register<AnnotationLayerControl, int>(nameof(SourcePixelWidth), 1);

    /// <summary>元画像の高さ（ピクセル）を取得または設定する。</summary>
    public static readonly StyledProperty<int> SourcePixelHeightProperty =
        AvaloniaProperty.Register<AnnotationLayerControl, int>(nameof(SourcePixelHeight), 1);

    /// <summary>選択中の注釈の識別子を取得または設定する。</summary>
    public static readonly StyledProperty<string?> SelectedIdProperty =
        AvaloniaProperty.Register<AnnotationLayerControl, string?>(nameof(SelectedId));

    /// <summary>注釈の描画計画を解決する提示層。</summary>
    private readonly AnnotationPresenter _presenter = new();

    /// <summary>選択枠・ハンドルを解決する Marquee の提示層。</summary>
    private readonly MarqueePresenter _marquee;

    /// <summary>選択枠の外観（暗転・ラベル・マーカーは使わない）。</summary>
    private static readonly MarqueeAppearance s_marqueeAppearance = MarqueeAppearance.Default with
    {
        ShowOuterMask = false,
        ShowInnerMask = false,
        ShowMarker = false,
        ShowSizeLabel = false,
        ShowCoordLabel = false,
    };

    /// <summary>注釈の文書。</summary>
    private AnnotationDocument _document = new(1, 1);

    /// <summary>しっぽの先端をドラッグ中かどうか。</summary>
    private bool _isDraggingTail;

    /// <summary>Marquee からの通知で文書を更新している最中かどうか（再入防止）。</summary>
    private bool _isApplyingMarquee;

    /// <summary>最後に観測したポインター位置（元画像ピクセル。 nudge の起点に使う）。</summary>
    private EsPoint _lastPointerSource;

    /// <summary>注釈レイヤーを初期化する。</summary>
    public AnnotationLayerControl()
    {
        Focusable = true;
        ClipToBounds = true;
        _marquee = new MarqueePresenter(new AnnotationMarqueeEffects(this))
        {
            Appearance = s_marqueeAppearance,
        };
        _marquee.SetCoordTransform(CoordinateTransform);
        _marquee.Subscribe(OnMarqueeNotice);
    }

    /// <summary>画像を取得または設定する。</summary>
    public IImage? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>元画像の幅（ピクセル）を取得または設定する。</summary>
    public int SourcePixelWidth
    {
        get => GetValue(SourcePixelWidthProperty);
        set => SetValue(SourcePixelWidthProperty, value);
    }

    /// <summary>元画像の高さ（ピクセル）を取得または設定する。</summary>
    public int SourcePixelHeight
    {
        get => GetValue(SourcePixelHeightProperty);
        set => SetValue(SourcePixelHeightProperty, value);
    }

    /// <summary>選択中の注釈の識別子を取得または設定する。</summary>
    public string? SelectedId
    {
        get => GetValue(SelectedIdProperty);
        set => SetValue(SelectedIdProperty, value);
    }

    /// <summary>注釈の文書を取得または設定する。</summary>
    /// <value>表示・編集する文書。 null は空の文書として扱う。</value>
    /// <remarks>差し替えると選択を解除する（別の画像の注釈を編集しないため）。</remarks>
    public AnnotationDocument Document
    {
        get => _document;
        set
        {
            _document = value ?? new AnnotationDocument(SourcePixelWidth, SourcePixelHeight);
            SelectedId = null;
            _marquee.ResetSelection();
            RaiseDocumentChanged();
            InvalidateVisual();
        }
    }

    /// <summary>文書が変わったときに発生する。</summary>
    /// <remarks>保存（注釈ファイルの書き出し）はホストが行う（本クラスはファイルへ書かない）。</remarks>
    public event EventHandler? DocumentChanged;

    /// <summary>選択が変わったときに発生する。</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>本文の編集が要求されたときに発生する（ダブルクリック）。</summary>
    /// <remarks>ホストは <see cref="GetSelectedBodyRect"/> の位置へ入力欄を置き、確定した文字列を <see cref="SetSelectedText"/> へ渡す。</remarks>
    public event EventHandler? EditTextRequested;

    /// <summary>枠線の注釈を追加する。</summary>
    /// <param name="bounds">矩形（元画像ピクセル）。</param>
    /// <returns>追加した注釈の識別子。</returns>
    public string AddFrame(EsRect bounds)
    {
        var frame = new FrameAnnotation { Bounds = bounds, Appearance = AnnotationDefaults.FrameAppearance };
        _document = _document.Add(frame);
        RaiseDocumentChanged();
        Select(frame.Id);
        return frame.Id;
    }

    /// <summary>吹き出しの注釈を追加する。</summary>
    /// <param name="bounds">本体の矩形（元画像ピクセル）。</param>
    /// <param name="text">本文。</param>
    /// <returns>追加した注釈の識別子。</returns>
    /// <remarks>しっぽの先端は本体の下辺の中央から 40 px 下に置く（追加後すぐ掴めるようにする）。</remarks>
    public string AddBubble(EsRect bounds, string text)
    {
        var bubble = new BubbleAnnotation
        {
            Bounds = bounds,
            Appearance = AnnotationDefaults.BubbleAppearance,
            Text = RichText.Create(text, AnnotationDefaults.TextStyle),
            Tail = new BubbleTail(new EsPoint(bounds.Center.X, bounds.Bottom + 40)),
        };

        _document = _document.Add(bubble);
        RaiseDocumentChanged();
        Select(bubble.Id);
        return bubble.Id;
    }

    /// <summary>選択中の注釈を削除する。</summary>
    /// <returns>削除した場合は <see langword="true"/>。</returns>
    public bool DeleteSelected()
    {
        if (SelectedId is not { } id)
        {
            return false;
        }

        _document = _document.Remove(id);
        Select(null);
        RaiseDocumentChanged();
        return true;
    }

    /// <summary>選択中の吹き出しの本文を置き換える。</summary>
    /// <param name="text">新しい本文（明示改行は <c>\n</c>）。</param>
    /// <returns>置き換えた場合は <see langword="true"/>。</returns>
    /// <remarks>書式は既存の先頭 run のものを引き継ぐ（文字単位の書式の編集は後段で対応する）。</remarks>
    public bool SetSelectedText(string text)
    {
        if (SelectedId is null || _document.Find(SelectedId) is not BubbleAnnotation bubble)
        {
            return false;
        }

        var style = bubble.Text.Runs.FirstOrDefault()?.Style ?? AnnotationDefaults.TextStyle;
        _document = _document.WithText(bubble, RichText.Create(text, style));
        RaiseDocumentChanged();
        InvalidateVisual();
        return true;
    }

    /// <summary>選択中の吹き出しの本文の矩形（DIP）を取得する。</summary>
    /// <returns>本文の矩形（DIP）。 吹き出しを選んでいない場合は空の矩形。</returns>
    /// <remarks>ホストが入力欄を重ねる位置に使う（コントロールは子要素を持たない）。</remarks>
    public Rect GetSelectedBodyRect()
    {
        if (SelectedId is null || _document.Find(SelectedId) is not BubbleAnnotation bubble)
        {
            return default;
        }

        var body = bubble.ResolveBodyRect();
        var topLeft = ToClientDip(body.TopLeft);
        var bottomRight = ToClientDip(body.BottomRight);
        return new Rect(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);
    }

    /// <summary>Marquee のカーソル形状を適用する。</summary>
    /// <param name="kind">カーソル種別。</param>
    internal void ApplyCursor(MarqueeCursorKind kind)
        => Cursor = new Cursor(AnnotationMarqueeEffects.ToStandardCursor(kind));

    /// <summary>Marquee の要求でポインター キャプチャを取得する（押下時にコントロールが取得済みのため何もしない）。</summary>
    internal void CapturePointerForMarquee() => _ = this;

    /// <summary>コントロールの寸法が変わったら選択枠を追従させる。</summary>
    /// <param name="e">サイズ変更の引数。</param>
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        InvalidateVisual();
    }

    /// <summary>描画する（画像 → 注釈 → 選択枠の順）。</summary>
    /// <param name="context">描画先。</param>
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var (scale, offset) = ResolveImageTransform();
        if (Source is { } image)
        {
            context.DrawImage(image, new Rect(offset.X, offset.Y, SourcePixelWidth * scale, SourcePixelHeight * scale));
        }

        if (_document.Items.Length > 0)
        {
            var plan = _presenter.ResolveDrawPlan(_document, MeasureText, SelectedId);

            // 注釈は元画像座標で描く（枠線の太さ・文字の大きさを保存画像と同じ比率にする）
            using (context.PushTransform(
                Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offset.X, offset.Y)))
            {
                foreach (var shape in plan.Shapes)
                {
                    AnnotationPainter.DrawShape(context, shape);
                }
            }
        }

        DrawSelection(context);
    }

    /// <summary>ポインターが押されたときの処理。</summary>
    /// <param name="e">ポインターの引数。</param>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        Focus();
        e.Pointer.Capture(this);

        var source = ToSource(point.Position);
        if (SelectedId is not null
            && _document.Find(SelectedId) is BubbleAnnotation selected
            && IsNearTailTip(selected, source))
        {
            _isDraggingTail = true;
            InvalidateVisual();
            return;
        }

        if (AnnotationHitTest.HitTest(_document, source) is { } hit)
        {
            if (!string.Equals(SelectedId, hit.Id, StringComparison.Ordinal))
            {
                Select(hit.Id);
            }

            if (e.ClickCount >= 2 && hit is BubbleAnnotation)
            {
                EditTextRequested?.Invoke(this, EventArgs.Empty);
            }

            _marquee.Press(source, e.ClickCount);
        }
        else
        {
            Select(null);
        }

        InvalidateVisual();
    }

    /// <summary>ポインターが動いたときの処理。</summary>
    /// <param name="e">ポインターの引数。</param>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        var source = ToSource(e.GetPosition(this));
        _lastPointerSource = source;
        if (_isDraggingTail)
        {
            if (SelectedId is not null && _document.Find(SelectedId) is BubbleAnnotation bubble)
            {
                bubble.Tail = bubble.Tail with { Tip = source };
                RaiseDocumentChanged();
                InvalidateVisual();
            }

            return;
        }

        _marquee.Move(source);
        InvalidateVisual();
    }

    /// <summary>ポインターが離されたときの処理。</summary>
    /// <param name="e">ポインターの引数。</param>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_isDraggingTail)
        {
            _isDraggingTail = false;
            return;
        }

        _marquee.Release(ToSource(e.GetPosition(this)));
        e.Pointer.Capture(null);
        InvalidateVisual();
    }

    /// <summary>キーが押されたときの処理（Delete・Enter・方向キーの nudge）。</summary>
    /// <param name="e">キーの引数。</param>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Delete)
        {
            e.Handled = DeleteSelected();
            return;
        }

        if (e.Key == Key.Enter && SelectedId is not null)
        {
            EditTextRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            e.Handled = _marquee.KeyDown(ToEsKey(e.Key), _lastPointerSource);
            InvalidateVisual();
        }
    }

    /// <summary>本文の実寸を測る（フォントの実寸に依存する部分だけを UI が担う）。</summary>
    /// <param name="text">文字列。</param>
    /// <param name="style">書式。</param>
    /// <returns>実寸（元画像ピクセル）。</returns>
    private static EsSize MeasureText(string text, AnnotationTextStyle style)
    {
        var formatted = AnnotationPainter.CreateText(text, style);
        return new EsSize(formatted.Width, formatted.Height);
    }

    /// <summary>画像の表示倍率と位置を求める（縦横比を保って中央に置く）。</summary>
    /// <returns>倍率と左上の位置（DIP）。</returns>
    private (double Scale, Point Offset) ResolveImageTransform()
    {
        var width = Math.Max(1, SourcePixelWidth);
        var height = Math.Max(1, SourcePixelHeight);
        var client = Bounds.Size;
        if (client.Width <= 0 || client.Height <= 0)
        {
            return (1, default);
        }

        var scale = Math.Min(client.Width / width, client.Height / height);
        return (scale, new Point((client.Width - (width * scale)) / 2, (client.Height - (height * scale)) / 2));
    }

    /// <summary>ローカル DIP を元画像ピクセルへ変換する。</summary>
    /// <param name="dip">ローカル DIP の座標。</param>
    /// <returns>元画像ピクセルの座標。</returns>
    private EsPoint ToSource(Point dip)
    {
        var (scale, offset) = ResolveImageTransform();
        return new EsPoint((dip.X - offset.X) / scale, (dip.Y - offset.Y) / scale);
    }

    /// <summary>元画像ピクセルをローカル DIP へ変換する。</summary>
    /// <param name="source">元画像ピクセルの座標。</param>
    /// <returns>ローカル DIP の座標。</returns>
    private EsPoint ToClientDip(EsPoint source)
    {
        var (scale, offset) = ResolveImageTransform();
        return new EsPoint(offset.X + (source.X * scale), offset.Y + (source.Y * scale));
    }

    /// <summary>Marquee へ渡す座標変換ポートを生成する。</summary>
    /// <returns>双方向の変換ポート。</returns>
    private ICoordTransform CoordinateTransform => new CoordTransform(
        dip => ToSource(new Point(dip.X, dip.Y)),
        ToClientDip,
        () => new EsSize(SourcePixelWidth, SourcePixelHeight));

    /// <summary>注釈を選択する。</summary>
    /// <param name="id">選択する注釈の識別子（解除は null）。</param>
    private void Select(string? id)
    {
        SelectedId = id;
        if (id is null || _document.Find(id) is not { } item)
        {
            _marquee.ResetSelection();
        }
        else
        {
            _marquee.SetSelection(item.Bounds);
        }

        SelectionChanged?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
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

        InvalidateVisual();
    }

    /// <summary>選択枠（枠・8 方向のハンドル）を描く。</summary>
    /// <param name="context">描画先。</param>
    private void DrawSelection(DrawingContext context)
    {
        if (SelectedId is null)
        {
            return;
        }

        var plan = _marquee.ResolveDrawPlan(new EsSize(Bounds.Width, Bounds.Height), (text, size) => new EsSize(text.Length * size * 0.5, size));
        if (plan.Appearance.ShowBorder)
        {
            var brush = AnnotationPainter.ToBrush(plan.Appearance.BorderColor);
            foreach (var rect in plan.BorderDipRects)
            {
                context.FillRectangle(brush, new Rect(rect.X, rect.Y, rect.Width, rect.Height));
            }
        }

        if (plan.Handles is not { } handles)
        {
            return;
        }

        var fill = AnnotationPainter.ToBrush(plan.Appearance.HandleColor);
        var border = new Pen(AnnotationPainter.ToBrush(plan.Appearance.HandleBorderColor), handles.BorderThickness);
        for (var i = 0; i < MarqueeHandleRects.Count; i++)
        {
            var rect = handles.HandleDipRects[i];
            context.DrawRectangle(fill, border, new Rect(rect.X, rect.Y, rect.Width, rect.Height));
        }
    }

    /// <summary>Avalonia のキーをコアのキーへ写す（nudge に使う 4 方向だけ）。</summary>
    /// <param name="key">Avalonia のキー。</param>
    /// <returns>コアのキー。 対象外は <see cref="EsUtil.Platform.Core.Input.EsKey.None"/>。</returns>
    private static EsUtil.Platform.Core.Input.EsKey ToEsKey(Key key) => key switch
    {
        Key.Left => EsUtil.Platform.Core.Input.EsKey.Left,
        Key.Right => EsUtil.Platform.Core.Input.EsKey.Right,
        Key.Up => EsUtil.Platform.Core.Input.EsKey.Up,
        Key.Down => EsUtil.Platform.Core.Input.EsKey.Down,
        _ => EsUtil.Platform.Core.Input.EsKey.None,
    };

    /// <summary>しっぽの先端の近くかどうかを判定する。</summary>
    /// <param name="bubble">対象の吹き出し。</param>
    /// <param name="source">位置（元画像ピクセル）。</param>
    /// <returns>近くの場合は <see langword="true"/>。</returns>
    /// <remarks>掴む半径は元画像ピクセルで一定にする（表示倍率が変わっても画面上の掴みやすさを保つ）。</remarks>
    private bool IsNearTailTip(BubbleAnnotation bubble, EsPoint source)
    {
        var (scale, _) = ResolveImageTransform();
        var radius = AnnotationHitTest.TailHandleRadiusDefault / Math.Max(scale, 0.0001);
        var dx = bubble.Tail.Tip.X - source.X;
        var dy = bubble.Tail.Tip.Y - source.Y;
        return (dx * dx) + (dy * dy) <= radius * radius;
    }

    /// <summary>文書が変わったことを通知する。</summary>
    private void RaiseDocumentChanged() => DocumentChanged?.Invoke(this, EventArgs.Empty);
}
