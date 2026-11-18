using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using EsUtil.Feature.Annotation;
using EsUtil.Feature.Marquee;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Platform.Core.Palette;
using System.ComponentModel;
using EsUtil.Ui.WinForms.Interop.Imaging;

namespace EsUtil.Ui.WinForms.Annotation;

/// <summary>画像の上へ注釈（枠線・吹き出し）を重ねるレイヤー コントロール（WinForms 版）。</summary>
/// <remarks>
/// <b>役割</b>: 画像の表示・注釈の描画・入力の受付を担う。 判断（当たり判定・折り返し・描画計画）は
/// <c>EsUtil.Feature.Annotation</c> が単一所有し、本クラスは GDI+ へ写すだけである。<br/>
/// <b>選択枠と 8 方向のハンドルは Marquee の提示層を再利用する</b>（枠・ハンドル・nudge を UI 版で揃えるため）。<br/>
/// <b>本文の編集はリッチ テキストの入力欄を本文の矩形へ重ねて行う</b>: WinForms の <see cref="RichTextBox"/> が
/// IME・選択・書式の適用を担う（読み込み時と確定時に書式をコアのモデルへ写す）。<br/>
/// <b>座標</b>: 注釈は元画像ピクセルで保持し、描画時は表示倍率で写す（画像へ焼き込まない）。
/// </remarks>
public sealed partial class AnnotationLayerControl : Control
{
    /// <summary>しっぽのハンドルの半径（DIP。 描画と当たり判定で同じ値を使う）。</summary>
    private const double TailHandleRadiusDip = AnnotationHitTest.TailHandleRadiusDefault;

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

    /// <summary>変更履歴（元に戻す・やり直す）。</summary>
    private readonly AnnotationHistory _history = new();

    /// <summary>これから記録する変更の直前の文書（変更が起きるまで保持する）。</summary>
    private AnnotationDocument? _undoCandidate;

    /// <summary>しっぽの先端をドラッグ中かどうか。</summary>
    private bool _isDraggingTail;

    /// <summary>Marquee からの通知で文書を更新している最中かどうか（再入防止）。</summary>
    private bool _isApplyingMarquee;

    /// <summary>最後に観測したポインター位置（元画像ピクセル。 nudge の起点に使う）。</summary>
    private EsPoint _lastPointerSource;

    /// <summary>表示用に変換した画像（差し替え時に解放する）。</summary>
    private Bitmap? _image;

    /// <summary>本文の入力欄（編集中だけ保持する）。</summary>
    private RichTextBox? _editor;

    /// <summary>入力欄の変更をモデルへ写している最中かどうか（再入防止）。</summary>
    private bool _isSyncingEditor;

    /// <summary>注釈レイヤーを初期化する。</summary>
    public AnnotationLayerControl()
    {
        SetStyle(
            ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.Selectable | ControlStyles.ResizeRedraw,
            true);

        TabStop = true;
        _coordinateTransform = CreateCoordinateTransform(this);
        _marquee = new MarqueePresenter(new AnnotationMarqueeEffects(this))
        {
            Appearance = s_marqueeAppearance,
        };
        _marquee.SetCoordTransform(_coordinateTransform);
        _marquee.Subscribe(OnMarqueeNotice);
    }

    /// <summary>外観・書式の編集パネルを要求する（ホストが表示する）。</summary>
    public event EventHandler? StyleEditorRequested;

    /// <summary>文書が変わったときに発生する。</summary>
    public event EventHandler? DocumentChanged;

    /// <summary>選択が変わったときに発生する。</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>本文の編集の開始・終了で発生する。</summary>
    public event EventHandler? EditingChanged;

    /// <summary>本文の選択範囲が変わったときに発生する。</summary>
    public event EventHandler? TextSelectionChanged;

    /// <summary>表示する画像を取得または設定する。</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public EsReadOnlyBitmap? Source
    {
        get => _source;
        set
        {
            if (ReferenceEquals(_source, value))
            {
                return;
            }

            _source = value;
            _image?.Dispose();
            _image = value is { IsDisposed: false } bitmap ? EsBitmapDrawingInterop.ToDrawingBitmap(bitmap) : null;
            Invalidate();
        }
    }

    /// <summary>表示する画像（変換前）。</summary>
    private EsReadOnlyBitmap? _source;

    /// <summary>元画像の幅（ピクセル）を取得または設定する。</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SourcePixelWidth { get; set; } = 1;

    /// <summary>元画像の高さ（ピクセル）を取得または設定する。</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SourcePixelHeight { get; set; } = 1;

    /// <summary>選択中の注釈の識別子を取得または設定する。</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? SelectedId { get; set; }

    /// <summary>新規作成時の既定値を取得または設定する。</summary>
    /// <remarks><b>既存の注釈には影響しない</b>（各注釈が自分の外観・書式を持つ）。</remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public AnnotationDraftDefaults DraftDefaults { get; set; } = AnnotationDraftDefaults.Default;

    /// <summary>色の入力欄へ出す表記を取得または設定する。</summary>
    /// <value>色名 + 不透明度（既定）またはカラーコード（<c>#AARRGGBB</c>）。</value>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ColorTextFormat ColorTextFormat { get; set; } = ColorTextFormat.NameWithAlpha;

    /// <summary>行間の倍率を取得または設定する。</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double LineHeightFactor
    {
        get => _presenter.LineHeightFactor;
        set
        {
            _presenter.LineHeightFactor = value;
            Invalidate();
        }
    }

    /// <summary>選択枠のハンドルの一辺の大きさ（DIP）を取得または設定する。</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double HandleSize
    {
        get => _marquee.Options.HandleSize;
        set
        {
            _marquee.Options = _marquee.Options with { HandleSize = value };
            Invalidate();
        }
    }

    /// <summary>注釈の文書を取得または設定する。</summary>
    /// <remarks>差し替えると選択を解除し、変更履歴を空にする（別の画像の注釈を引き継がない）。</remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public AnnotationDocument Document
    {
        get => _document;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            EndTextEditing();
            _document = value;
            _history.Clear();
            _undoCandidate = null;
            SelectedId = null;
            _marquee.ResetSelection();
            RaiseDocumentChanged();
            Invalidate();
        }
    }

    /// <summary>選択中の注釈を取得する。</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public AnnotationItem? Selected => SelectedId is null ? null : _document.Find(SelectedId);

    /// <summary>選択中の本文の書式を取得する（選択範囲の先頭の書式）。</summary>
    public AnnotationTextStyle SelectedTextStyle
        => Selected is BubbleAnnotation bubble
            ? bubble.Text.StyleAt(Math.Max(0, TextSelectionStart))
            : AnnotationDefaults.TextStyle;

    /// <summary>元に戻すことができるかどうかを取得する。</summary>
    public bool CanUndo => _history.CanUndo;

    /// <summary>やり直すことができるかどうかを取得する。</summary>
    public bool CanRedo => _history.CanRedo;

    /// <summary>本文を編集中かどうかを取得する。</summary>
    public bool IsEditingText => _editor is not null;

    /// <summary>本文の選択範囲の開始位置（文字数）を取得する。</summary>
    public int TextSelectionStart => _editor is null ? 0 : Math.Max(0, _editor.SelectionStart);

    /// <summary>本文の選択範囲の長さ（文字数）を取得する。</summary>
    public int TextSelectionLength => _editor is null ? 0 : Math.Max(0, _editor.SelectionLength);

    /// <summary>本文の選択範囲（開始位置と長さ）を取得する。</summary>
    public (int Start, int Length) TextSelectionRange => (TextSelectionStart, TextSelectionLength);

    /// <summary>枠線を追加する。</summary>
    /// <param name="bounds">位置と大きさ（元画像ピクセル）。</param>
    /// <returns>追加した注釈の識別子。</returns>
    public string AddFrame(EsRect bounds)
    {
        PrepareUndo();
        var item = new FrameAnnotation { Bounds = bounds, Appearance = DraftDefaults.FrameAppearance };
        _document = _document.Add(item);
        Select(item.Id);
        RaiseDocumentChanged();
        Invalidate();
        return item.Id;
    }

    /// <summary>吹き出しを追加する。</summary>
    /// <param name="bounds">位置と大きさ（元画像ピクセル）。</param>
    /// <param name="text">本文。</param>
    /// <returns>追加した注釈の識別子。</returns>
    public string AddBubble(EsRect bounds, string text)
    {
        PrepareUndo();
        var item = new BubbleAnnotation
        {
            Bounds = bounds,
            Appearance = DraftDefaults.BubbleAppearance,
            BodyPadding = DraftDefaults.BodyPadding,
            Tail = new BubbleTail(default, DraftDefaults.TailEdge, 0.5, DraftDefaults.TailWidth),
            Text = RichText.Create(text, DraftDefaults.TextStyle),
        };

        _document = _document.Add(item);
        Select(item.Id);
        RaiseDocumentChanged();
        Invalidate();
        return item.Id;
    }

    /// <summary>選択中の注釈を削除する。</summary>
    /// <returns>削除した場合は <see langword="true"/>。</returns>
    public bool DeleteSelected()
    {
        if (SelectedId is not { } id)
        {
            return false;
        }

        EndTextEditing();
        PrepareUndo();
        _document = _document.Remove(id);
        SelectedId = null;
        _marquee.ResetSelection();
        RaiseSelectionChanged();
        RaiseDocumentChanged();
        Invalidate();
        return true;
    }

    /// <summary>選択中の吹き出しの本文を置き換える。</summary>
    /// <param name="text">新しい本文。</param>
    /// <returns>置き換えた場合は <see langword="true"/>。</returns>
    public bool SetSelectedText(string text)
    {
        if (Selected is not BubbleAnnotation bubble)
        {
            return false;
        }

        PrepareUndo();
        _document = _document.WithText(bubble, bubble.Text.WithPlainText(text));
        RaiseDocumentChanged();
        Invalidate();
        return true;
    }

    /// <summary>選択中の本文へ書式を適用する（<see cref="TextSelectionRange"/> の範囲）。</summary>
    /// <param name="style">書式。</param>
    /// <returns>適用した場合は <see langword="true"/>。</returns>
    public bool ApplyTextStyle(AnnotationTextStyle style)
        => ApplyTextStyle(TextSelectionStart, TextSelectionLength, style);

    /// <summary>選択中の本文へ書式を適用する。</summary>
    /// <param name="start">開始位置（文字数）。</param>
    /// <param name="length">長さ（0 は全体）。</param>
    /// <param name="style">書式。</param>
    /// <returns>適用した場合は <see langword="true"/>。</returns>
    public bool ApplyTextStyle(int start, int length, AnnotationTextStyle style)
        => SetTextStyle(text => text.ApplyStyle(start, length, style));

    /// <summary>選択中の本文の書式を変換する（1 つの属性だけ変える）。</summary>
    /// <param name="start">開始位置（文字数）。</param>
    /// <param name="length">長さ（0 は全体）。</param>
    /// <param name="transform">書式を変える処理。</param>
    /// <returns>適用した場合は <see langword="true"/>。</returns>
    public bool TransformTextStyle(int start, int length, Func<AnnotationTextStyle, AnnotationTextStyle> transform)
        => SetTextStyle(text => text.TransformStyle(start, length, transform));

    /// <summary>選択中の本文の書式を置き換える。</summary>
    /// <param name="change">本文を変える処理。</param>
    /// <returns>変えた場合は <see langword="true"/>。</returns>
    private bool SetTextStyle(Func<RichText, RichText> change)
    {
        if (Selected is not BubbleAnnotation bubble)
        {
            return false;
        }

        PrepareUndo();
        _document = _document.WithText(bubble, change(bubble.Text));
        RaiseDocumentChanged();
        SyncEditorFromModel();
        Invalidate();
        return true;
    }

    /// <summary>選択中の注釈の外観を差し替える。</summary>
    /// <param name="transform">外観を変える処理。</param>
    /// <returns>変えた場合は <see langword="true"/>。</returns>
    public bool UpdateSelectedAppearance(Func<AnnotationAppearance, AnnotationAppearance> transform)
        => UpdateSelected(item => item.Appearance = transform(item.Appearance));

    /// <summary>選択中の吹き出しの本文の余白を設定する。</summary>
    /// <param name="padding">余白（元画像ピクセル）。</param>
    /// <returns>変えた場合は <see langword="true"/>。</returns>
    public bool SetSelectedBodyPadding(double padding)
        => UpdateSelected(item =>
        {
            if (item is BubbleAnnotation bubble)
            {
                bubble.BodyPadding = padding;
            }
        });

    /// <summary>選択中の吹き出しのしっぽを差し替える。</summary>
    /// <param name="transform">しっぽを変える処理。</param>
    /// <returns>変えた場合は <see langword="true"/>。</returns>
    public bool UpdateSelectedTail(Func<BubbleTail, BubbleTail> transform)
        => UpdateSelected(item =>
        {
            if (item is BubbleAnnotation bubble)
            {
                bubble.Tail = transform(bubble.Tail);
            }
        });

    /// <summary>選択中の注釈を最前面へ移す。</summary>
    /// <returns>移した場合は <see langword="true"/>。</returns>
    public bool MoveSelectedToFront()
    {
        if (SelectedId is not { } id)
        {
            return false;
        }

        PrepareUndo();
        _document = _document.BringToFront(id);
        RaiseDocumentChanged();
        Invalidate();
        return true;
    }

    /// <summary>選択中の注釈を最背面へ移す。</summary>
    /// <returns>移した場合は <see langword="true"/>。</returns>
    public bool MoveSelectedToBack()
    {
        if (SelectedId is not { } id)
        {
            return false;
        }

        PrepareUndo();
        _document = _document.SendToBack(id);
        RaiseDocumentChanged();
        Invalidate();
        return true;
    }

    /// <summary>変更を元に戻す。</summary>
    /// <returns>戻した場合は <see langword="true"/>。</returns>
    public bool Undo() => ApplyHistory(_history.Undo(_document));

    /// <summary>変更をやり直す。</summary>
    /// <returns>やり直した場合は <see langword="true"/>。</returns>
    public bool Redo() => ApplyHistory(_history.Redo(_document));

    /// <summary>外観・書式の編集パネルを表示する（右クリックと同じ）。</summary>
    public void ShowStyleEditor()
    {
        if (Selected is null)
        {
            return;
        }

        Focus();
        StyleEditorRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>選択中の本文の矩形（DIP）を取得する。</summary>
    /// <returns>本文の矩形。 吹き出しを選んでいない場合は空の矩形。</returns>
    public EsRect GetSelectedBodyRect()
    {
        if (Selected is not BubbleAnnotation bubble)
        {
            return default;
        }

        var body = bubble.ResolveBodyRect();
        var (scale, offset) = ResolveImageTransform();
        return new EsRect(
            (body.X * scale) + offset.X,
            (body.Y * scale) + offset.Y,
            body.Width * scale,
            body.Height * scale);
    }

    /// <summary>画像と注釈を元画像ピクセルの 1:1 で合成した画像を作る（保存用）。</summary>
    /// <returns>合成した画像。 破棄は呼び出し側。 作れない場合は <see langword="null"/>。</returns>
    /// <remarks>選択枠・ハンドル・キャレットは描かない（編集中の一時的な表示であり、保存する絵ではない）。</remarks>
    public EsBitmap? RenderComposedImage()
    {
        var width = Math.Max(1, SourcePixelWidth);
        var height = Math.Max(1, SourcePixelHeight);

        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (_image is { } image)
            {
                graphics.DrawImage(image, new Rectangle(0, 0, width, height));
            }

            var plan = _presenter.ResolveDrawPlan(_document, AnnotationTextMetrics.Measure, null);
            AnnotationPainter.Draw(graphics, plan, 1, 0, 0);
        }

        var data = bitmap.LockBits(
            new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var pixels = new byte[data.Stride * height];
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
            return EsBitmap.FromPixelCopy(width, height, pixels);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    /// <summary>選択中の注釈を 1 か所だけ変更する。</summary>
    /// <param name="change">変更する処理。</param>
    /// <returns>変えた場合は <see langword="true"/>。</returns>
    private bool UpdateSelected(Action<AnnotationItem> change)
    {
        if (Selected is not { } item)
        {
            return false;
        }

        PrepareUndo();
        change(item);
        RaiseDocumentChanged();
        Invalidate();
        return true;
    }

    /// <summary>選択中の注釈を選択する。</summary>
    /// <param name="id">選択する注釈の識別子（解除は <see langword="null"/>）。</param>
    private void Select(string? id)
    {
        if (string.Equals(SelectedId, id, StringComparison.Ordinal))
        {
            return;
        }

        if (IsEditingText)
        {
            EndTextEditing();
        }

        SelectedId = id;
        if (Selected is { } item)
        {
            _marquee.SetSelection(item.Bounds);
        }
        else
        {
            _marquee.ResetSelection();
        }

        RaiseSelectionChanged();
        Invalidate();
    }

    /// <summary>選択の変更を通知する。</summary>
    private void RaiseSelectionChanged() => SelectionChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>これから変更することを履歴へ準備する（変更の直前に呼ぶ）。</summary>
    private void PrepareUndo() => _undoCandidate ??= _document.Clone();

    /// <summary>文書が変わったことを通知する（控えた文書があれば履歴へ積む）。</summary>
    private void RaiseDocumentChanged()
    {
        if (_undoCandidate is { } candidate)
        {
            _history.Record(candidate);
            _undoCandidate = null;
        }

        DocumentChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>履歴から取り出した文書を適用する。</summary>
    /// <param name="document">適用する文書（無い場合は何もしない）。</param>
    /// <returns>適用した場合は <see langword="true"/>。</returns>
    private bool ApplyHistory(AnnotationDocument? document)
    {
        if (document is null)
        {
            return false;
        }

        EndTextEditing();
        _document = document;
        if (SelectedId is { } id && _document.Find(id) is null)
        {
            SelectedId = null;
        }

        _marquee.ResetSelection();
        if (Selected is { } item)
        {
            _marquee.SetSelection(item.Bounds);
        }

        RaiseSelectionChanged();
        RaiseDocumentChanged();
        Invalidate();
        return true;
    }

    /// <summary>画像の表示倍率と位置を求める（縦横比を保って中央に置く）。</summary>
    /// <returns>倍率と左上の位置（DIP）。</returns>
    internal (double Scale, EsPoint Offset) ResolveImageTransform()
    {
        var width = Math.Max(1, SourcePixelWidth);
        var height = Math.Max(1, SourcePixelHeight);
        if (ClientSize.Width <= 0 || ClientSize.Height <= 0)
        {
            return (1, default);
        }

        var scale = Math.Min((double)ClientSize.Width / width, (double)ClientSize.Height / height);
        return (scale, new EsPoint(
            (ClientSize.Width - (width * scale)) / 2,
            (ClientSize.Height - (height * scale)) / 2));
    }

    /// <summary>ローカル DIP を元画像ピクセルへ変換する。</summary>
    /// <param name="dip">ローカル DIP の座標。</param>
    /// <returns>元画像ピクセルの座標。</returns>
    internal EsPoint ToSource(EsPoint dip)
    {
        var (scale, offset) = ResolveImageTransform();
        return new EsPoint((dip.X - offset.X) / scale, (dip.Y - offset.Y) / scale);
    }

    /// <summary>元画像ピクセルをローカル DIP へ変換する。</summary>
    /// <param name="source">元画像ピクセルの座標。</param>
    /// <returns>ローカル DIP の座標。</returns>
    internal EsPoint ToClientDip(EsPoint source)
    {
        var (scale, offset) = ResolveImageTransform();
        return new EsPoint(offset.X + (source.X * scale), offset.Y + (source.Y * scale));
    }

    /// <summary>Marquee へ渡す座標変換ポート（変換の規則が変わらないため 1 度だけ作る）。</summary>
    private readonly ICoordTransform _coordinateTransform;

    /// <summary>Marquee へ渡す座標変換ポートを生成する。</summary>
    /// <param name="owner">座標変換の持ち主。</param>
    /// <returns>座標変換ポート。</returns>
    private static ICoordTransform CreateCoordinateTransform(AnnotationLayerControl owner) => new CoordTransform(
        owner.ToSource,
        owner.ToClientDip,
        () => new EsSize(owner.SourcePixelWidth, owner.SourcePixelHeight));

    /// <summary>カーソル形状を Marquee の種別へ合わせる。</summary>
    /// <param name="kind">Marquee のカーソル種別。</param>
    internal void ApplyCursor(MarqueeCursorKind kind) => Cursor = kind switch
    {
        MarqueeCursorKind.Move => Cursors.SizeAll,
        MarqueeCursorKind.Horizontal => Cursors.SizeWE,
        MarqueeCursorKind.Vertical => Cursors.SizeNS,
        MarqueeCursorKind.DiagonalNWSE => Cursors.SizeNWSE,
        MarqueeCursorKind.DiagonalNESW => Cursors.SizeNESW,
        _ => Cursors.Default,
    };

    /// <summary>子要素（入力欄）も含めて破棄する。</summary>
    /// <param name="disposing">マネージド リソースを解放する場合は <see langword="true"/>。</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _editor?.Dispose();
            _editor = null;
            _image?.Dispose();
            _image = null;
        }

        base.Dispose(disposing);
    }
}
