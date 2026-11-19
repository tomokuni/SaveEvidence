using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using EsUtil.Feature.Annotation;
using EsUtil.Feature.Marquee;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Palette;

namespace EsUtil.Ui.AvaloniaUI.Annotation;

/// <summary>画像の上へ注釈（枠線・吹き出し）を重ねるレイヤー コントロール（Avalonia UI 版）。</summary>
/// <remarks>
/// <b>役割</b>: 画像の表示・注釈の描画・入力の受付・外観と書式の編集を担う。 判断（当たり判定・折り返し・
/// 描画計画）は <c>EsUtil.Feature.Annotation</c> が単一所有し、本クラスは自 UI の描画 API へ写すだけである。<br/>
/// <b>選択枠と 8 方向のハンドルは Marquee の提示層を再利用する</b>（枠・ハンドル・nudge・カーソルを 4 つの UI 版で揃えるため）。
/// ハンドルの当たり判定も <b>同じ解決結果（<c>MarqueeDrawPlan</c>）と <c>HandleHitRadius</c> を使う</b>ため、
/// 「カーソルが変わる範囲」と「押して操作できる範囲」が一致する。<br/>
/// <b>本文の編集はその場で行う</b>: ダブルクリックまたは Enter で編集を始め、表示中のレイアウトのまま
/// キャレット・選択範囲を描いて入力する（<see cref="BeginTextEditing"/>）。 書式は <see cref="TextSelectionRange"/> と
/// <c>TransformTextStyle</c> で文字単位に適用し、入力中も反映される。<br/>
/// <b>外観と書式の編集は右クリックのフライアウト</b>（<see cref="ShowStyleEditor"/>）で行う。 サンプルは同じ編集 UI を
/// 設定ペインから使える（既定値の編集）。<br/>
/// <b>座標</b>: 注釈は元画像ピクセルで保持し、描画時は表示倍率で写す（画像へ焼き込まない）。
/// </remarks>
public sealed partial class AnnotationLayerControl : Control
{
    /// <summary>画像を取得または設定する。</summary>
    /// <value>背景に表示する画像。 null の場合は注釈だけを描く。</value>
    public static readonly StyledProperty<IImage?> SourceProperty =
        AvaloniaProperty.Register<AnnotationLayerControl, IImage?>(nameof(Source));

    /// <summary>画像を自分で描くかどうかを取得または設定する。</summary>
    /// <value>プレビューへ重ねるときは <see langword="false"/>（画像と影はプレビューが描く）。</value>
    public bool ShowImage { get; set; } = true;

    /// <summary>プレビュー内の画像の表示矩形（DIP）を取得または設定する。</summary>
    /// <value>プレビューが画像を表示している矩形。 未設定の場合は自分の領域いっぱいへ中央寄せで表示する。</value>
    /// <remarks><b>プレビューのズーム・パンへ追従する</b>: ホストがズームの変化のたびに設定する。</remarks>
    public EsRect? PreviewImageRect { get; set; }
    /// <summary>元画像の幅（ピクセル）を取得または設定する。</summary>
    public static readonly StyledProperty<int> SourcePixelWidthProperty =
        AvaloniaProperty.Register<AnnotationLayerControl, int>(nameof(SourcePixelWidth), 1);

    /// <summary>元画像の高さ（ピクセル）を取得または設定する。</summary>
    public static readonly StyledProperty<int> SourcePixelHeightProperty =
        AvaloniaProperty.Register<AnnotationLayerControl, int>(nameof(SourcePixelHeight), 1);

    /// <summary>選択中の注釈の識別子を取得または設定する。</summary>
    public static readonly StyledProperty<string?> SelectedIdProperty =
        AvaloniaProperty.Register<AnnotationLayerControl, string?>(nameof(SelectedId));

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

    /// <summary>外観・書式の編集フライアウト（開いている間だけ保持する）。</summary>
    private Flyout? _flyout;

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

        // 本文をその場で編集するため、IME（日本語入力）の窓口を渡す。
        // フォーカスを得た時点で問い合わせが来るため、編集中かどうかで分けずに常に同じクライアントを返す
        // （編集の開始時に問い合わせ直せないため。 編集中でなければ変換は受け取らない）。
        TextInputMethodClientRequested += (_, e) => e.Client = TextInputClient;
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
            if (_isEditingText)
            {
                EndTextEditing();
            }

            _document = value ?? new AnnotationDocument(SourcePixelWidth, SourcePixelHeight);
            _history.Clear();
            _undoCandidate = null;
            SelectedId = null;
            _marquee.ResetSelection();
            RaiseDocumentChanged();
            InvalidateVisual();
        }
    }

    /// <summary>新規作成時の既定値を取得または設定する。</summary>
    /// <value>枠線・吹き出し・本文の既定。 既定は <see cref="AnnotationDraftDefaults.Default"/>。</value>
    /// <remarks>
    /// <b>既存の注釈には影響しない</b>（各注釈が自分の外観・書式を持つ）。 設定ページやサンプルの設定ペインから変える。
    /// </remarks>
    public AnnotationDraftDefaults DraftDefaults { get; set; } = AnnotationDraftDefaults.Default;

    /// <summary>色の入力欄へ出す表記を取得または設定する。</summary>
    /// <value>色名 + 不透明度（既定）またはカラーコード（<c>#AARRGGBB</c>）。</value>
    /// <remarks>
    /// <b>保存値は表記に依らず <c>#AARRGGBB</c></b>である。 右クリックのフライアウトの色は本表記で表示する
    /// （書式バーの色は <c>AnnotationFormatBar.TextFormat</c> が同じ値を受ける）。
    /// </remarks>
    public ColorTextFormat ColorTextFormat { get; set; } = ColorTextFormat.NameWithAlpha;

    /// <summary>行間の倍率を取得または設定する。</summary>
    /// <value>文字の実寸の高さに対する行の高さの比。 既定は 1.25。</value>
    public double LineHeightFactor
    {
        get => _presenter.LineHeightFactor;
        set
        {
            _presenter.LineHeightFactor = value;
            InvalidateVisual();
        }
    }

    /// <summary>本文の書式を適用する範囲（文字数）を取得または設定する。</summary>
    /// <value>開始位置と長さ（0 起点）。 長さ 0 は「全体」を表す。</value>
    /// <remarks>
    /// 本文の入力欄はホストが持つため、入力欄の選択範囲をホストがここへ写す。
    /// 右クリックのフライアウトと <see cref="ApplyTextStyle(AnnotationTextStyle)"/> は本値の範囲へ適用する。
    /// </remarks>
    public (int Start, int Length) TextSelectionRange { get; set; }

    /// <summary>選択枠のハンドルを掴む半径（DIP）を取得または設定する。</summary>
    /// <value>ハンドルの中心からの距離。 既定は 12。</value>
    /// <remarks>
    /// <b>描画（カーソルの変化）と押下の当たり判定が同じ値を使う</b>（Marquee の <c>HandleHitRadius</c>）。
    /// 見えているハンドルの大きさは <see cref="HandleSize"/> で別に決める。
    /// </remarks>
    public double HandleHitRadius
    {
        get => _marquee.Options.HandleHitRadius;
        set
        {
            _marquee.Options = _marquee.Options with { HandleHitRadius = value };
            InvalidateVisual();
        }
    }

    /// <summary>選択枠のハンドルの一辺の大きさ（DIP）を取得または設定する。</summary>
    /// <value>既定は 10。</value>
    public double HandleSize
    {
        get => _marquee.Options.HandleSize;
        set
        {
            _marquee.Options = _marquee.Options with { HandleSize = value };
            InvalidateVisual();
        }
    }

    /// <summary>文書が変わったときに発生する。</summary>
    /// <remarks>保存（注釈ファイルの書き出し）はホストが行う（本クラスはファイルへ書かない）。</remarks>
    public event EventHandler? DocumentChanged;

    /// <summary>選択が変わったときに発生する。</summary>
    public event EventHandler? SelectionChanged;


    /// <summary>選択中の注釈を取得する。</summary>
    /// <value>選択が無い場合は <see langword="null"/>。</value>
    public AnnotationItem? Selected => SelectedId is null ? null : _document.Find(SelectedId);

    /// <summary>選択中の吹き出しの本文の書式（選択範囲の先頭の文字）を取得する。</summary>
    /// <value>選択が吹き出しでない場合は <see cref="AnnotationDefaults.TextStyle"/>。</value>
    /// <remarks>フライアウトの初期表示に使う。</remarks>
    public AnnotationTextStyle SelectedTextStyle
        => Selected is BubbleAnnotation bubble
            ? bubble.Text.StyleAt(TextSelectionRange.Length <= 0 ? 0 : TextSelectionRange.Start)
            : AnnotationDefaults.TextStyle;

    /// <summary>指定した位置を左上として枠線を追加する（右クリックのメニューから追加する）。</summary>
    /// <param name="sourcePoint">左上の位置（元画像ピクセル）。</param>
    /// <returns>追加した注釈の識別子。</returns>
    public string AddFrameAt(EsPoint sourcePoint) => AddFrame(ResolveDefaultBounds(sourcePoint));

    /// <summary>指定した位置（DIP）を左上として枠線を追加する。</summary>
    /// <param name="dip">左上の位置（ローカル DIP）。</param>
    /// <returns>追加した注釈の識別子。</returns>
    public string AddFrameAt(Point dip) => AddFrameAt(ToSource(dip));

    /// <summary>指定した位置を左上として吹き出しを追加する（右クリックのメニューから追加する）。</summary>
    /// <param name="sourcePoint">左上の位置（元画像ピクセル）。</param>
    /// <returns>追加した注釈の識別子。</returns>
    public string AddBubbleAt(EsPoint sourcePoint) => AddBubble(ResolveDefaultBounds(sourcePoint), string.Empty);

    /// <summary>指定した位置（DIP）を左上として吹き出しを追加する。</summary>
    /// <param name="dip">左上の位置（ローカル DIP）。</param>
    /// <returns>追加した注釈の識別子。</returns>
    public string AddBubbleAt(Point dip) => AddBubbleAt(ToSource(dip));

    /// <summary>追加する注釈の既定の矩形を求める（指定した位置を左上にし、画像の内側へ収める）。</summary>
    /// <param name="sourcePoint">左上の位置（元画像ピクセル）。</param>
    /// <returns>画像の内側へ収めた矩形。</returns>
    private EsRect ResolveDefaultBounds(EsPoint sourcePoint)
    {
        var imageWidth = Math.Max(1, SourcePixelWidth);
        var imageHeight = Math.Max(1, SourcePixelHeight);
        var width = Math.Min(DefaultNewWidth, imageWidth);
        var height = Math.Min(DefaultNewHeight, imageHeight);
        var x = Math.Clamp(sourcePoint.X, 0, Math.Max(0, imageWidth - width));
        var y = Math.Clamp(sourcePoint.Y, 0, Math.Max(0, imageHeight - height));
        return new EsRect(x, y, width, height);
    }

    /// <summary>右クリックのメニューから追加する注釈の既定の幅（元画像ピクセル）。</summary>
    private const double DefaultNewWidth = 240;

    /// <summary>右クリックのメニューから追加する注釈の既定の高さ（元画像ピクセル）。</summary>
    private const double DefaultNewHeight = 160;

    /// <summary>指定した位置（DIP）に注釈があるかどうかを判定する（プレビューの入力を受け取るかの判断に使う）。</summary>
    /// <param name="dip">位置（ローカル DIP）。</param>
    /// <returns>注釈・ハンドル・しっぽの近くにある場合は <see langword="true"/>。</returns>
    public bool HitTestAt(Point dip)
    {
        var source = ToSource(dip);
        if (AnnotationHitTest.HitTest(_document, source) is not null)
        {
            return true;
        }

        if (Selected is BubbleAnnotation bubble && IsNearTailTip(bubble, source))
        {
            return true;
        }

        return HitsSelectionHandle(dip);
    }

    /// <summary>選択と本文の編集を解除する（注釈の外をクリックしたとき）。</summary>
    public void ClearSelection()
    {
        EndTextEditing();
        Select(null);
    }
    /// <summary>枠線の注釈を追加する。</summary>
    /// <param name="bounds">矩形（元画像ピクセル）。</param>
    /// <returns>追加した注釈の識別子。</returns>
    public string AddFrame(EsRect bounds)
    {
        PrepareUndo();
        var frame = new FrameAnnotation { Bounds = bounds, Appearance = DraftDefaults.FrameAppearance };
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
        PrepareUndo();
        var defaults = DraftDefaults;
        var bubble = new BubbleAnnotation
        {
            Bounds = bounds,
            Appearance = defaults.BubbleAppearance,
            BodyPadding = defaults.BodyPadding,
            Text = RichText.Create(text, defaults.TextStyle),
            Tail = new BubbleTail(
                new EsPoint(bounds.Center.X, bounds.Bottom + 40),
                defaults.TailEdge,
                AnnotationDefaults.TailBaseOffsetDefault,
                defaults.TailWidth),
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

        PrepareUndo();
        _document = _document.Remove(id);
        Select(null);
        RaiseDocumentChanged();
        return true;
    }

    /// <summary>選択中の吹き出しの本文を置き換える。</summary>
    /// <param name="text">新しい本文（明示改行は <c>\n</c>）。</param>
    /// <returns>置き換えた場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// <b>書式は文字位置で引き継ぐ</b>: 先頭から同じ長さの範囲は元の run の書式を保ち、増えた部分は先頭 run の書式にする
    /// （編集のたびにすべての書式が消えないようにする）。
    /// </remarks>
    public bool SetSelectedText(string text)
    {
        if (Selected is not BubbleAnnotation bubble)
        {
            return false;
        }

        // 同じ文字列なら何もしない（履歴に同じ内容の手が増えないようにする）
        if (string.Equals(bubble.Text.PlainText, text, StringComparison.Ordinal))
        {
            return false;
        }

        PrepareUndo();
        _document = _document.WithText(bubble, bubble.Text.WithPlainText(text));
        RaiseDocumentChanged();
        InvalidateVisual();
        return true;
    }

    /// <summary>選択中の吹き出しの本文へ書式を適用する（<see cref="TextSelectionRange"/> の範囲）。</summary>
    /// <param name="style">適用する書式。</param>
    /// <returns>適用した場合は <see langword="true"/>。</returns>
    /// <remarks>長さ 0 は「全体」へ適用する。 文字単位の書式は run の分割で表す（コアが単一所有する）。</remarks>
    public bool ApplyTextStyle(AnnotationTextStyle style)
    {
        var range = EffectiveTextRange();
        return ApplyTextStyle(range.Start, range.Length, style);
    }

    /// <summary>選択中の吹き出しの本文へ、指定した範囲の書式を適用する。</summary>
    /// <param name="start">開始位置（文字数。 0 起点）。</param>
    /// <param name="length">長さ（文字数）。 0 以下は本文全体。</param>
    /// <param name="style">適用する書式。</param>
    /// <returns>適用した場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// <b>範囲は本文の文字数で数える</b>（入力欄の選択位置をそのまま渡せる）。 範囲の解決と run の分割は
    /// コアの <c>RichText.ApplyStyle</c> が単一所有する。
    /// </remarks>
    public bool ApplyTextStyle(int start, int length, AnnotationTextStyle style)
    {
        if (Selected is not BubbleAnnotation bubble)
        {
            return false;
        }

        PrepareUndo();
        _document = _document.WithText(bubble, bubble.Text.ApplyStyle(start, length, style));
        RaiseDocumentChanged();
        InvalidateVisual();
        return true;
    }

    /// <summary>選択中の吹き出しの本文の書式を 1 文字ずつ変換する（変えない属性を保つ）。</summary>
    /// <param name="transform">現在の書式を受け取って新しい書式を返す関数（run ごとに呼ぶ）。</param>
    /// <returns>適用した場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// <b>「太字にする」「色だけ変える」は本メソッドを使う</b>: <see cref="ApplyTextStyle(AnnotationTextStyle)"/> は
    /// 範囲の書式を丸ごと差し替えるため、指定しなかった属性（色・大きさなど）が既定へ戻る。
    /// 切り替えの判断（太字にする／戻す）は呼び出し側で 1 回だけ行い、その値を transform で書く。
    /// </remarks>
    public bool TransformTextStyle(Func<AnnotationTextStyle, AnnotationTextStyle> transform)
    {
        var range = EffectiveTextRange();
        return TransformTextStyle(range.Start, range.Length, transform);
    }

    /// <summary>選択中の吹き出しの本文の、指定した範囲の書式を 1 文字ずつ変換する。</summary>
    /// <param name="start">開始位置（文字数。 0 起点）。</param>
    /// <param name="length">長さ（文字数）。 0 以下は本文全体。</param>
    /// <param name="transform">現在の書式を受け取って新しい書式を返す関数（run ごとに呼ぶ）。</param>
    /// <returns>適用した場合は <see langword="true"/>。</returns>
    public bool TransformTextStyle(int start, int length, Func<AnnotationTextStyle, AnnotationTextStyle> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);

        if (Selected is not BubbleAnnotation bubble)
        {
            return false;
        }

        PrepareUndo();
        _document = _document.WithText(bubble, bubble.Text.TransformStyle(start, length, transform));
        RaiseDocumentChanged();
        InvalidateVisual();
        return true;
    }

    /// <summary>選択中の注釈の外観を差し替える。</summary>
    /// <param name="transform">現在の外観を受け取って新しい外観を返す関数。</param>
    /// <returns>適用した場合は <see langword="true"/>。</returns>
    public bool UpdateSelectedAppearance(Func<AnnotationAppearance, AnnotationAppearance> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);

        if (Selected is not { } item)
        {
            return false;
        }

        PrepareUndo();
        item.Appearance = transform(item.Appearance).Normalized();
        RaiseDocumentChanged();
        InvalidateVisual();
        return true;
    }

    /// <summary>選択中の吹き出しの本文の余白を差し替える。</summary>
    /// <param name="padding">本文の余白（元画像ピクセル）。</param>
    /// <returns>適用した場合は <see langword="true"/>。</returns>
    public bool SetSelectedBodyPadding(double padding)
    {
        if (Selected is not BubbleAnnotation bubble)
        {
            return false;
        }

        PrepareUndo();
        bubble.BodyPadding = AnnotationLimits.CoerceBodyPadding(padding);
        RaiseDocumentChanged();
        InvalidateVisual();
        return true;
    }

    /// <summary>選択中の吹き出しのしっぽの太さ・辺を差し替える。</summary>
    /// <param name="transform">現在のしっぽを受け取って新しいしっぽを返す関数。</param>
    /// <returns>適用した場合は <see langword="true"/>。</returns>
    public bool UpdateSelectedTail(Func<BubbleTail, BubbleTail> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);

        if (Selected is not BubbleAnnotation bubble)
        {
            return false;
        }

        PrepareUndo();
        bubble.Tail = transform(bubble.Tail).Normalized();
        RaiseDocumentChanged();
        InvalidateVisual();
        return true;
    }

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
        InvalidateVisual();
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
        InvalidateVisual();
        return true;
    }

    /// <summary>元に戻せるかどうかを取得する。</summary>
    /// <value>変更履歴に戻せる状態がある場合は <see langword="true"/>。</value>
    public bool CanUndo => _history.CanUndo;

    /// <summary>やり直せるかどうかを取得する。</summary>
    /// <value>元に戻した変更が残っている場合は <see langword="true"/>。</value>
    public bool CanRedo => _history.CanRedo;

    /// <summary>直前の変更を元に戻す。</summary>
    /// <returns>戻した場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// <b>選択は識別子で引き継ぐ</b>（戻した後も同じ注釈を選んだまま、続けて操作できるようにする）。
    /// 変更の単位は「追加・削除・移動・サイズ変更・しっぽの移動・外観・書式・重なり順」である
    /// （ドラッグは押下から離すまでで 1 手）。
    /// </remarks>
    public bool Undo() => ApplyHistory(_history.Undo(_document));

    /// <summary>元に戻した変更をやり直す。</summary>
    /// <returns>やり直した場合は <see langword="true"/>。</returns>
    public bool Redo() => ApplyHistory(_history.Redo(_document));

    /// <summary>履歴から取り出した文書を現在の文書にする（選択を引き継ぐ）。</summary>
    /// <param name="document">適用する文書（<see langword="null"/> は何もしない）。</param>
    /// <returns>適用した場合は <see langword="true"/>。</returns>
    private bool ApplyHistory(AnnotationDocument? document)
    {
        _undoCandidate = null;
        if (document is null)
        {
            return false;
        }

        var id = SelectedId;
        _document = document;
        ClampCaretToText();

        if (id is not null && _document.Find(id) is { } item)
        {
            SelectedId = id;
            if (_isEditingText)
            {
                // 編集中は選択枠を出さない（本文の見た目を優先する）
                _marquee.ResetSelection();
            }
            else
            {
                _marquee.SetSelection(item.Bounds);
            }
        }
        else
        {
            SelectedId = null;
            _marquee.ResetSelection();
        }

        // 履歴の操作自体は記録しない（RaiseDocumentChanged を経由しない）
        DocumentChanged?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
        return true;
    }

    /// <summary>外観と書式の編集フライアウトを表示する（右クリック）。</summary>
    /// <remarks>選択中の注釈を編集する。 項目は枠線・吹き出しの外観、本文の書式、重なり順である。</remarks>
    public void ShowStyleEditor()
    {
        _flyout?.Hide();
        _flyout = AnnotationStyleFlyout.Create(this);
        _flyout.ShowAt(this, showAtPointer: true);
    }

    /// <summary>選択中の吹き出しの本文の矩形（DIP）を取得する。</summary>
    /// <returns>本文の矩形（DIP）。 吹き出しを選んでいない場合は空の矩形。</returns>
    /// <remarks>ホストが入力欄を重ねる位置に使う（コントロールは子要素を持たない）。</remarks>
    public Rect GetSelectedBodyRect()
    {
        if (Selected is not BubbleAnnotation bubble)
        {
            return default;
        }

        var body = bubble.ResolveBodyRect();
        var topLeft = ToClientDip(body.TopLeft);
        var bottomRight = ToClientDip(body.BottomRight);
        return new Rect(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);
    }

    /// <summary>しっぽのハンドルの中心（DIP）を取得する（描画と当たり判定で共有する）。</summary>
    /// <returns>中心（DIP）。 吹き出しを選んでいない場合は <see langword="null"/>。</returns>
    internal Point? GetTailHandleCenterDip()
    {
        if (Selected is not BubbleAnnotation bubble)
        {
            return null;
        }

        var tip = ToClientDip(bubble.Tail.Tip);
        return new Point(tip.X, tip.Y);
    }

    /// <summary>Marquee のカーソル形状を適用する。</summary>
    /// <param name="kind">カーソル種別。</param>
    internal void ApplyCursor(MarqueeCursorKind kind)
        => Cursor = new Cursor(AnnotationMarqueeEffects.ToStandardCursor(kind));

    /// <summary>Marquee の要求でポインター キャプチャを取得する（押下時にコントロールが取得済みのため何もしない）。</summary>
    internal void CapturePointerForMarquee() => _ = this;

    /// <summary>文書が変わったことを通知する（フライアウトの編集結果もホストへ伝える）。</summary>
    internal void NotifyDocumentChanged()
    {
        RaiseDocumentChanged();
        InvalidateVisual();
    }

    /// <summary>コントロールの寸法が変わったら再描画する。</summary>
    /// <param name="e">サイズ変更の引数。</param>
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        InvalidateVisual();
    }

    /// <summary>画像と注釈を元画像ピクセルの 1:1 で合成したビットマップを作る（保存用）。</summary>
    /// <returns>合成したビットマップ（<see cref="SourcePixelWidth"/> × <see cref="SourcePixelHeight"/>）。</returns>
    /// <remarks>
    /// <b>選択枠・ハンドル・キャレットは描かない</b>（編集中の一時的な表示であり、保存する絵ではない）。<br/>
    /// <b>座標は元画像ピクセル</b>である: 表示倍率を掛けず、注釈を 1:1 で描く（どの表示倍率でも同じ絵になる）。
    /// 呼び出し側は返したビットマップを破棄する。
    /// </remarks>
    public RenderTargetBitmap RenderComposedImage()
    {
        var width = Math.Max(1, SourcePixelWidth);
        var height = Math.Max(1, SourcePixelHeight);

        var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        using (var context = bitmap.CreateDrawingContext())
        {
            if (Source is { } image)
            {
                context.DrawImage(image, new Rect(0, 0, width, height));
            }

            if (_document.Items.Length > 0)
            {
                var plan = _presenter.ResolveDrawPlan(_document, MeasureText, selectedId: null);
                foreach (var shape in plan.Shapes)
                {
                    AnnotationPainter.DrawShape(context, shape);
                }
            }
        }

        return bitmap;
    }

    /// <summary>描画する（画像 → 注釈 → 選択枠 → しっぽのハンドルの順）。</summary>
    /// <param name="context">描画先。</param>
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var (scale, offset) = ResolveImageTransform();
        if (ShowImage && Source is { } image)
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

                // 本文の編集中は、選択範囲とキャレットを注釈と同じ座標系で描く（表示中のレイアウトのまま編集する）
                DrawTextEditing(context, scale);
            }
        }

        DrawSelection(context);
        DrawTailHandle(context);
    }

    /// <summary>ポインターを押している間かどうか（離した後に注釈の外なら入力を受け取らない）。</summary>
    private bool _isPointerDown;

    /// <summary>ポインターが押されたときの処理（左: 選択・移動・サイズ変更。 右: 編集メニュー）。</summary>
    /// <param name="e">ポインターの引数。</param>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var point = e.GetCurrentPoint(this);
        _isPointerDown = true;

        if (point.Properties.IsRightButtonPressed)
        {
            HandleRightButton(point.Position);
            e.Handled = true;
            return;
        }

        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        Focus();
        e.Pointer.Capture(this);

        // 押下から始まる操作（移動・サイズ変更・しっぽの移動）を 1 手として記録する。
        // 実際に文書が変わらなければ履歴へは積まれない（選択だけのクリックで手数が増えない）。
        PrepareUndo();

        var source = ToSource(point.Position);

        // 本文の編集中は、本文の内側の押下をキャレットの移動・選択に使う（外側なら編集を終える）
        if (_isEditingText)
        {
            if (Selected is BubbleAnnotation editingBubble && editingBubble.Bounds.Contains(source))
            {
                _undoCandidate = null;
                _isSelectingText = true;
                SetCaretFromPoint(source, (e.KeyModifiers & KeyModifiers.Shift) != 0);
                InvalidateVisual();
                return;
            }

            EndTextEditing();
        }

        if (Selected is not null)
        {
            if (Selected is BubbleAnnotation selectedBubble && IsNearTailTip(selectedBubble, source))
            {
                _isDraggingTail = true;
                InvalidateVisual();
                return;
            }

            // ハンドルの当たり判定（カーソルが変わる範囲と同じ判定）を注釈の当たり判定より先に見る。
            // ここを飛ばすと、ハンドルの外周を押したときに「注釈の外側」と判定されて選択が解除される。
            if (HitsSelectionHandle(point.Position))
            {
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
                BeginTextEditing();
                e.Handled = true;
                return;
            }

            _marquee.Press(source, e.ClickCount);
        }
        else
        {
            Select(null);
        }

        InvalidateVisual();
    }

    /// <summary>ポインターが動いたときの処理（ドラッグとカーソル形状）。</summary>
    /// <param name="e">ポインターの引数。</param>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        var position = e.GetPosition(this);
        var source = ToSource(position);
        _lastPointerSource = source;

        // 本文の編集中にドラッグしている間は、キャレットを伸ばして選択する
        if (_isEditingText && _isSelectingText)
        {
            SetCaretFromPoint(source, extend: true);
            InvalidateVisual();
            return;
        }

        if (_isDraggingTail)
        {
            if (Selected is BubbleAnnotation bubble)
            {
                bubble.Tail = bubble.Tail with { Tip = source };
                RaiseDocumentChanged();
                InvalidateVisual();
            }

            return;
        }

        _marquee.Move(source);

        // 注釈の外へ出たら入力を受け取らない（プレビューのズーム・パンへ渡す）
        if (!_isPointerDown && !_isEditingText && !HitTestAt(e.GetPosition(this)))
        {
            IsHitTestVisible = false;
        }

        // しっぽのハンドルの上では専用のカーソルにする（Marquee の Move が設定した形状を上書きする）
        if (Selected is BubbleAnnotation selectedBubble && IsNearTailTip(selectedBubble, source))
        {
            Cursor = new Cursor(StandardCursorType.Hand);
        }

        InvalidateVisual();
    }

    /// <summary>ポインターが離されたときの処理。</summary>
    /// <param name="e">ポインターの引数。</param>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_isSelectingText)
        {
            _isSelectingText = false;
            e.Pointer.Capture(null);
            InvalidateVisual();
            return;
        }

        if (_isDraggingTail)
        {
            _isDraggingTail = false;
            return;
        }

        _isPointerDown = false;
        _marquee.Release(ToSource(e.GetPosition(this)));
        e.Pointer.Capture(null);
        InvalidateVisual();
    }

    /// <summary>キーが押されたときの処理（Delete・Enter・方向キーの nudge）。</summary>
    /// <param name="e">キーの引数。</param>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        // 本文の編集中は、キー入力を本文の編集へ渡す（Delete で注釈を消さない）
        if (_isEditingText && HandleTextEditingKey(e))
        {
            e.Handled = true;
            return;
        }

        // Ctrl+Z / Ctrl+Y（元に戻す・やり直す）。 本文の編集中はホストの入力欄が受け取る。
        if ((e.KeyModifiers & KeyModifiers.Control) != 0 && e.Key is Key.Z or Key.Y)
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

    /// <summary>右クリックを処理する（選択してから編集メニューを出す）。</summary>
    /// <param name="position">位置（DIP）。</param>
    private void HandleRightButton(Point position)
    {
        var source = ToSource(position);
        if (AnnotationHitTest.HitTest(_document, source) is { } hit
            && !string.Equals(SelectedId, hit.Id, StringComparison.Ordinal))
        {
            Select(hit.Id);
        }

        if (Selected is null)
        {
            return;
        }

        Focus();
        ShowStyleEditor();
    }

    /// <summary>選択中の注釈のいずれかのハンドル（8 方向）の当たり判定に含まれるかどうかを判定する。</summary>
    /// <param name="dip">位置（DIP）。</param>
    /// <returns>ハンドルの範囲内の場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// <b>Marquee と同じ判定にする</b>: ハンドルの中心は <c>MarqueeDrawPlan.Handles</c> の矩形の中心、半径は
    /// <c>MarqueeOptions.HandleHitRadius</c>（DIP）を使う。 中心や半径を自前で計算すると、カーソルが変わる範囲と
    /// 押して操作できる範囲がずれる（実測: ハンドルの外周を押すと選択が解除されていた）。
    /// </remarks>
    private bool HitsSelectionHandle(Point dip)
    {
        var plan = _marquee.ResolveDrawPlan(
            new EsSize(Bounds.Width, Bounds.Height), (text, size) => new EsSize(text.Length * size * 0.5, size));
        if (plan.Handles is not { } handles)
        {
            return false;
        }

        var radius = _marquee.Options.HandleHitRadius;
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

    /// <summary>本文の実寸を測る（フォントの実寸に依存する部分だけを UI が担う）。</summary>
    /// <param name="text">文字列。</param>
    /// <param name="style">書式。</param>
    /// <returns>実寸（元画像ピクセル）。</returns>
    /// <remarks>
    /// <b>行の高さは斜体で変えない</b>: 幅（折り返しの判定）は指定どおりの書式で測り、高さだけは斜体を外した
    /// 書式で測る。 斜体は字を傾けるだけであり、行の高さを変えると 1 行ごとに間隔が変わる（実測 2026-11-18）。
    /// </remarks>
    private static EsSize MeasureText(string text, AnnotationTextStyle style)
    {
        var formatted = AnnotationPainter.CreateText(text, style);
        var lineHeight = AnnotationPainter.CreateLineMetrics(style).Height;
        return new EsSize(formatted.Width, lineHeight);
    }

    /// <summary>書式を適用する範囲を求める（長さ 0 は全体）。</summary>
    /// <returns>開始位置と長さ。</returns>
    private (int Start, int Length) EffectiveTextRange()
    {
        var range = TextSelectionRange;
        return range.Length <= 0 ? (0, 0) : (Math.Max(0, range.Start), range.Length);
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
        // 別の注釈を選んだら本文の編集を終える（編集中の本文を見失わないようにする）
        if (_isEditingText && !string.Equals(SelectedId, id, StringComparison.Ordinal))
        {
            EndTextEditing();
        }

        SelectedId = id;
        TextSelectionRange = default;
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

        var plan = _marquee.ResolveDrawPlan(
            new EsSize(Bounds.Width, Bounds.Height), (text, size) => new EsSize(text.Length * size * 0.5, size));
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

    /// <summary>しっぽのハンドル（円）を描く。</summary>
    /// <param name="context">描画先。</param>
    /// <remarks>
    /// <b>当たり判定と同じ半径で描く</b>（<see cref="TailHandleRadiusDip"/>）。 表示倍率の影響を受けないよう、
    /// 注釈の座標変換の外（DIP）で描く。
    /// </remarks>
    private void DrawTailHandle(DrawingContext context)
    {
        if (GetTailHandleCenterDip() is not { } center)
        {
            return;
        }

        // 本体のハンドルと同じ外観にする（白の塗り + 灰色の枠。 色は Marquee の既定値を共有する）
        var size = HandleSize;
        var fill = AnnotationPainter.ToBrush(MarqueeAppearance.Default.HandleColor);
        var pen = new Pen(AnnotationPainter.ToBrush(MarqueeAppearance.Default.HandleBorderColor), 1);
        var rect = new Rect(center.X - (size / 2), center.Y - (size / 2), size, size);
        context.DrawRectangle(fill, pen, rect);
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

    /// <summary>しっぽの先端の近くかどうかを判定する（描画したハンドルと同じ半径）。</summary>
    /// <param name="bubble">対象の吹き出し。</param>
    /// <param name="source">位置（元画像ピクセル）。</param>
    /// <returns>近くの場合は <see langword="true"/>。</returns>
    /// <remarks>半径は画面上で一定（DIP）にするため、表示倍率で割った値を対象座標の半径として使う。</remarks>
    private bool IsNearTailTip(BubbleAnnotation bubble, EsPoint source)
    {
        var (scale, _) = ResolveImageTransform();
        var radius = TailHandleRadiusDip / Math.Max(scale, 0.0001);
        var dx = bubble.Tail.Tip.X - source.X;
        var dy = bubble.Tail.Tip.Y - source.Y;
        return (dx * dx) + (dy * dy) <= radius * radius;
    }

    /// <summary>これから変更することを履歴へ準備する（変更の直前に呼ぶ）。</summary>
    /// <remarks>
    /// <b>記録は変更が起きたときだけ行う</b>: 押しただけで動かなかった操作（選択だけのクリック）で
    /// 元に戻す手数が増えないよう、直前の文書を控えておき、実際に文書が変わった時点で履歴へ積む。
    /// </remarks>
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
}
