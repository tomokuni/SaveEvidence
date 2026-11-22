using EsUtil.Platform.Core.Geometry;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.System;
using Windows.UI.Text.Core;

namespace EsUtil.Ui.WinUI3.Annotation;

/// <summary>注釈レイヤーの本文の編集（WinUI 3 版）。</summary>
/// <remarks>
/// <b>入力欄を重ねない</b>: 本文はコントロールが <see cref="TextBlock"/> で描いているため、入力欄を重ねると
/// 折り返し・行の高さ・余白が入力欄の規則になり、<b>表示と編集で見た目が変わる</b>。 表示中のレイアウトのまま
/// キャレットと選択範囲を描いて入力する（Avalonia UI 版・MewUI 版と同じ規則）。<br/>
/// <b>日本語入力（IME）</b>: <see cref="CoreTextEditContext"/> を本文へ繋ぎ、変換候補の位置と周囲の文字列を渡す。
/// 変換中の文字列は文書へ入れず、キャレットの位置へ下線付きで描く（確定した文字列だけを入れる）。<br/>
/// <b>座標の対応</b>: 折り返しは <see cref="AnnotationPresenter"/> が解決した行を
/// <see cref="AnnotationTextLineIndex"/> で文字位置へ対応させる。
/// </remarks>
public sealed partial class AnnotationLayerControl
{
    /// <summary>キャレットの太さ（DIP）。</summary>
    private const double CaretThicknessDip = 1.6;

    /// <summary>選択範囲の色（本文の上に薄く重ねる）。</summary>
    private static readonly Windows.UI.Color s_textSelectionColor = Windows.UI.Color.FromArgb(72, 0, 120, 215);

    /// <summary>キャレットの色。</summary>
    private static readonly Windows.UI.Color s_caretColor = Windows.UI.Color.FromArgb(255, 20, 20, 20);

    /// <summary>編集の表示（選択範囲・キャレット・変換中の文字列）を並べる層。</summary>
    private readonly Canvas _editing = new() { IsHitTestVisible = false };

    /// <summary>本文を編集中かどうか。</summary>
    private bool _isEditingText;

    /// <summary>キャレットの位置（文字数。 0 は先頭）。</summary>
    private int _caretIndex;

    /// <summary>選択の起点（<see cref="_caretIndex"/> と同じなら選択なし）。</summary>
    private int _selectionAnchor;

    /// <summary>IME の変換中文字列（文書へは入れない）。</summary>
    private string _preeditText = string.Empty;

    /// <summary>IME の変換中かどうか。</summary>
    private bool _isComposing;

    /// <summary>IME との窓口（編集中だけ保持する）。</summary>
    private CoreTextEditContext? _ime;

    /// <summary>IME の窓口を作るときのサービス（初回に取得して使い回す）。</summary>
    private CoreTextServicesManager? _imeManager;

    /// <summary>本文の編集中に発生する（開始・終了の両方）。</summary>
    public event EventHandler? EditingChanged;

    /// <summary>本文の選択範囲（キャレットの位置）が変わったときに発生する。</summary>
    public event EventHandler? TextSelectionChanged;

    /// <summary>本文を編集中かどうかを取得する。</summary>
    public bool IsEditingText => _isEditingText;

    /// <summary>キャレットの位置（文字数）を取得する。</summary>
    public int CaretIndex => _caretIndex;

    /// <summary>本文の文字を選んでいるかどうかを取得する（書式の適用先があるか）。</summary>
    public bool HasTextSelection => _isEditingText && TextSelectionLength > 0;

    /// <summary>選択範囲の開始位置（文字数）を取得する。</summary>
    public int TextSelectionStart => Math.Min(_selectionAnchor, _caretIndex);

    /// <summary>選択範囲の終了位置（文字数）を取得する。</summary>
    public int TextSelectionEnd => Math.Max(_selectionAnchor, _caretIndex);

    /// <summary>選択範囲の長さ（文字数）を取得する。</summary>
    public int TextSelectionLength => TextSelectionEnd - TextSelectionStart;

    /// <summary>選択範囲（開始位置と長さ）を取得する。</summary>
    public (int Start, int Length) TextSelectionRange
    {
        get => (TextSelectionStart, TextSelectionLength);
        set
        {
            // 範囲はキャレットと選択の起点で表す（範囲から位置を戻す規則はここ 1 か所）
            var length = TextForInput.Length;
            var start = Math.Clamp(value.Start, 0, length);
            var end = Math.Clamp(start + Math.Max(0, value.Length), start, length);
            _selectionAnchor = start;
            _caretIndex = end;
            SyncTextSelectionRange();
            Redraw();
        }
    }

    /// <summary>キャレットの点滅を始めた時刻。</summary>
    private DateTimeOffset _caretBlinkStart = DateTimeOffset.UtcNow;

    /// <summary>キャレットの点滅を進めるタイマー（編集中だけ動かす）。</summary>
    private Microsoft.UI.Xaml.DispatcherTimer? _caretTimer;

    /// <summary>いまキャレットを点灯して描くかどうかを取得する。</summary>
    /// <value>点滅の位相が点灯の間は <see langword="true"/>。</value>
    private bool IsCaretVisible => AnnotationCaretBlink.IsVisible(DateTimeOffset.UtcNow - _caretBlinkStart);

    /// <summary>キャレットの点滅を点灯から始め直す（文字入力・キャレット移動の直後に呼ぶ）。</summary>
    private void RestartCaretBlink()
    {
        _caretBlinkStart = DateTimeOffset.UtcNow;
        if (_caretTimer is null)
        {
            var timer = new Microsoft.UI.Xaml.DispatcherTimer { Interval = AnnotationCaretBlink.Interval };
            timer.Tick += (_, _) =>
            {
                if (_isEditingText)
                {
                    Redraw();
                }
                else
                {
                    timer.Stop();
                }
            };

            _caretTimer = timer;
        }

        if (!_caretTimer.IsEnabled)
        {
            _caretTimer.Start();
        }
    }

    /// <summary>キャレットの点滅を止める（編集を終えたとき）。</summary>
    private void StopCaretBlink() => _caretTimer?.Stop();
    /// <summary>本文の文字列（IME へ渡す）。</summary>
    internal string TextForInput => Selected is BubbleAnnotation bubble ? bubble.Text.PlainText : string.Empty;

    /// <summary>本文の編集を始める（吹き出しを選んでいるときだけ）。</summary>
    /// <returns>編集を始めた場合は <see langword="true"/>。 枠線を選んでいる場合は <see langword="false"/>。</returns>
    public bool BeginTextEditing()
    {
        if (Selected is not BubbleAnnotation bubble)
        {
            return false;
        }

        if (_isEditingText)
        {
            return true;
        }

        _isEditingText = true;
        _preeditText = string.Empty;
        _isComposing = false;
        _caretIndex = bubble.Text.PlainText.Length;
        _selectionAnchor = _caretIndex;
        _marquee.ResetSelection();
        _ = Focus(FocusState.Programmatic);
        AttachIme();
        RestartCaretBlink();
        RaiseEditingChanged();
        Redraw();
        return true;
    }

    /// <summary>本文の編集を終える（内容は既に文書へ入っている）。</summary>
    public void EndTextEditing()
    {
        if (!_isEditingText)
        {
            return;
        }

        _isEditingText = false;
        _preeditText = string.Empty;
        _isComposing = false;
        StopCaretBlink();
        DetachIme();

        if (Selected is { } item)
        {
            _marquee.SetSelection(item.Bounds);
        }

        RaiseEditingChanged();
        Redraw();
    }

    /// <summary>IME の窓口を本文へ繋ぐ。</summary>
    private void AttachIme()
    {
        if (_ime is not null)
        {
            return;
        }

        try
        {
            _imeManager ??= CoreTextServicesManager.GetForCurrentView();
            var context = _imeManager.CreateEditContext();
            context.InputScope = CoreTextInputScope.Text;
            context.TextRequested += OnImeTextRequested;
            context.SelectionRequested += OnImeSelectionRequested;
            context.TextUpdating += OnImeTextUpdating;
            context.CompositionStarted += OnImeCompositionStarted;
            context.CompositionCompleted += OnImeCompositionCompleted;
            context.LayoutRequested += OnImeLayoutRequested;
            context.FocusRemoved += OnImeFocusRemoved;
            _ime = context;

            // **IME を有効にする**: 窓口を作っただけでは IME は入力モードへ切り替わらない。
            // フォーカスを得たことを知らせないと、日本語入力モードへ切り替わらないままになる
            // （実測 2026-11-22: WinUI 3 版だけ IME モードが切り替わらなかった）。
            // `CoreTextEditContext` に入力を有効にするプロパティは無い（`IsInputActive` は
            // この SDK に存在しない。 実測 2026-11-23）。 フォーカスと本文の通知で有効にする。
            context.NotifyFocusEnter();
            NotifyImeSelection();
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidCastException)
        {
            // XAML の窓口が無い環境（ウィンドウを持たない検証）では IME を使わない。
            // 入力はキーボードの文字入力（CharacterReceived）で継続できるため、ここでは窓口だけを諦める
            _ime = null;
        }
    }

    /// <summary>IME の窓口を外す。</summary>
    private void DetachIme()
    {
        if (_ime is not { } context)
        {
            return;
        }

        context.TextRequested -= OnImeTextRequested;
        context.SelectionRequested -= OnImeSelectionRequested;
        context.TextUpdating -= OnImeTextUpdating;
        context.CompositionStarted -= OnImeCompositionStarted;
        context.CompositionCompleted -= OnImeCompositionCompleted;
        context.LayoutRequested -= OnImeLayoutRequested;
        context.FocusRemoved -= OnImeFocusRemoved;

        // **IME を解放する**: 知らせないと、本文の編集を終えた後も IME が入力モードのまま残る
        context.NotifyFocusLeave();
        _ime = null;
    }

    /// <summary>IME へ本文の選択範囲を知らせる。</summary>
    private void NotifyImeSelection()
    {
        if (_ime is not { } context)
        {
            return;
        }

        var length = TextForInput.Length;
        context.NotifySelectionChanged(new CoreTextRange
        {
            StartCaretPosition = Math.Clamp(TextSelectionStart, 0, length),
            EndCaretPosition = Math.Clamp(TextSelectionEnd, 0, length),
        });
    }

    /// <summary>IME へ本文が変わったことを知らせる（周囲の文字列を渡し直す）。</summary>
    /// <param name="start">変えた範囲の開始位置（文字数）。</param>
    /// <param name="oldLength">変える前の長さ（文字数）。</param>
    /// <param name="newLength">変えた後の長さ（文字数）。</param>
    /// <remarks>
    /// <b>知らせないと変換の基準が古いままになる</b>: IME は周囲の文字列を問い合わせて変換するため、
    /// 本文が変わったことを知らせないと、確定した文字が重複したり、キャレットの位置がずれます
    /// （実測 2026-11-22: 日本語入力で既存の文字と位置が合わない、と見えました）。
    /// </remarks>
    private void NotifyImeTextChanged(int start, int oldLength, int newLength)
    {
        if (_ime is not { } context)
        {
            return;
        }

        var length = TextForInput.Length;
        var from = Math.Clamp(start, 0, length);
        context.NotifyTextChanged(
            new CoreTextRange { StartCaretPosition = from, EndCaretPosition = from + Math.Max(0, newLength) },
            Math.Max(0, oldLength),
            new CoreTextRange
            {
                StartCaretPosition = Math.Clamp(TextSelectionStart, 0, length),
                EndCaretPosition = Math.Clamp(TextSelectionEnd, 0, length),
            });
    }
    /// <summary>IME から周囲の文字列を求められたときに返す。</summary>
    /// <param name="sender">送信元。</param>
    /// <param name="e">要求の引数。</param>
    private void OnImeTextRequested(CoreTextEditContext sender, CoreTextTextRequestedEventArgs e)
    {
        _ = sender;

        var text = TextForInput;
        var range = e.Request.Range;
        var start = Math.Clamp(range.StartCaretPosition, 0, text.Length);
        var end = Math.Clamp(range.EndCaretPosition, 0, text.Length);
        if (end < start)
        {
            (start, end) = (end, start);
        }

        e.Request.Text = text[start..end];
    }

    /// <summary>IME から選択範囲を変えられたときに反映する。</summary>
    /// <param name="sender">送信元。</param>
    /// <param name="e">要求の引数。</param>
    private void OnImeSelectionRequested(CoreTextEditContext sender, CoreTextSelectionRequestedEventArgs e)
    {
        _ = sender;

        var length = TextForInput.Length;
        _selectionAnchor = Math.Clamp(e.Request.Selection.StartCaretPosition, 0, length);
        _caretIndex = Math.Clamp(e.Request.Selection.EndCaretPosition, 0, length);
        SyncTextSelectionRange();
        Redraw();
        NotifyImeSelection();
    }

    /// <summary>IME（またはキーボード）から文字の置き換えを求められたときに本文へ入れる。</summary>
    /// <param name="sender">送信元。</param>
    /// <param name="e">要求の引数。</param>
    private void OnImeTextUpdating(CoreTextEditContext sender, CoreTextTextUpdatingEventArgs e)
    {
        _ = sender;

        if (!_isEditingText)
        {
            e.Result = CoreTextTextUpdatingResult.Failed;
            return;
        }

        var text = TextForInput;
        var start = Math.Clamp(e.Range.StartCaretPosition, 0, text.Length);
        var end = Math.Clamp(e.Range.EndCaretPosition, 0, text.Length);
        if (end < start)
        {
            (start, end) = (end, start);
        }

        // **変換中かどうかを先に控える**: `ReplaceRange` は変換の状態を落とすため、呼ぶ前に控えます。
        // ここで控えた値を「IME へ知らせるか」の判断に使います（変換中に知らせると変換が中断され、
        // 1 キーごとに確定してしまう）。
        var composing = _isComposing;
        ReplaceRange(start, end, e.Text ?? string.Empty);
        e.Result = CoreTextTextUpdatingResult.Succeeded;

        // **変換中の文字列として覚える**: 確定の通知で取りこぼさないようにする。
        // `_isComposing` は書き戻さない（変換の開始・終了の通知だけが決める。 書き戻すと
        // IME の状態と食い違い、日本語入力へ切り替わらなくなる。 実測 2026-11-23）。
        if (composing)
        {
            _preeditText = e.Text ?? string.Empty;
            Redraw();
        }

        NotifyImeSelection();
    }

    /// <summary>IME の変換が始まったときに変換中の表示へ切り替える。</summary>
    /// <param name="sender">送信元。</param>
    /// <param name="e">変換の引数。</param>
    private void OnImeCompositionStarted(CoreTextEditContext sender, CoreTextCompositionStartedEventArgs e)
    {
        _ = sender;
        _ = e;
        _isComposing = true;
        _preeditText = string.Empty;
        Redraw();
    }

    /// <summary>IME の変換が終わったときに確定した文字列を本文へ入れる。</summary>
    /// <param name="sender">送信元。</param>
    /// <param name="e">変換の引数。</param>
    private void OnImeCompositionCompleted(CoreTextEditContext sender, CoreTextCompositionCompletedEventArgs e)
    {
        _ = sender;
        _ = e;

        // 確定した文字列は TextUpdating で届く（本イベントでは変換中の表示だけを消す）。
        // **届いていない場合はここで入れる**: IME によっては確定の通知だけが来る（実測 2026-11-22）。
        var preedit = _preeditText;
        _isComposing = false;
        _preeditText = string.Empty;

        if (preedit.Length > 0)
        {
            var text = TextForInput;
            var start = TextSelectionStart - preedit.Length;
            var applied = start >= 0
                && start + preedit.Length <= text.Length
                && string.CompareOrdinal(text, start, preedit, 0, preedit.Length) == 0;
            if (!applied)
            {
                ReplaceSelection(preedit);
            }
        }

        Redraw();
    }

    /// <summary>IME から変換候補の位置を求められたときに返す。</summary>
    /// <param name="sender">送信元。</param>
    /// <param name="e">要求の引数。</param>
    private void OnImeLayoutRequested(CoreTextEditContext sender, CoreTextLayoutRequestedEventArgs e)
    {
        _ = sender;

        var caret = ResolveCaretDip();
        var bounds = e.Request.LayoutBounds;
        bounds.TextBounds = new Windows.Foundation.Rect(
            caret.X, caret.Y, Math.Max(1, CaretThicknessDip), Math.Max(1, caret.Height));
        bounds.ControlBounds = new Windows.Foundation.Rect(0, 0, Math.Max(0, ActualWidth), Math.Max(0, ActualHeight));
    }

    /// <summary>IME のフォーカスが外れたら編集を終える。</summary>
    /// <param name="sender">送信元。</param>
    /// <param name="e">引数。</param>
    private void OnImeFocusRemoved(CoreTextEditContext sender, object e)
    {
        _ = sender;
        _ = e;
        EndTextEditing();
    }

    /// <summary>選択範囲、またはキャレットの前後の 1 文字を削除する。</summary>
    /// <param name="forward">キャレットの次の文字を削除する場合は <see langword="true"/>。</param>
    private void DeleteAtCaret(bool forward)
    {
        if (TextSelectionEnd > TextSelectionStart)
        {
            ReplaceSelection(string.Empty);
            return;
        }

        var index = forward ? _caretIndex : _caretIndex - 1;
        if (index < 0 || index >= TextForInput.Length)
        {
            return;
        }

        ReplaceRange(index, index + 1, string.Empty);
        NotifyImeSelection();
    }

    /// <summary>選択範囲を指定された文字列で置き換える。</summary>
    /// <param name="text">置き換える文字列（空なら削除）。</param>
    /// <returns>文書を変えた場合は <see langword="true"/>。</returns>
    private bool ReplaceSelection(string text)
    {
        var start = TextSelectionStart;
        var end = TextSelectionEnd;
        if (start == end && text.Length == 0)
        {
            return false;
        }

        var changed = ReplaceRange(start, end, text);
        NotifyImeSelection();
        return changed;
    }

    /// <summary>指定した範囲を文字列で置き換えて文書へ反映する。</summary>
    /// <param name="start">開始位置（文字数）。</param>
    /// <param name="end">終了位置（文字数）。</param>
    /// <param name="text">置き換える文字列。</param>
    /// <returns>文書を変えた場合は <see langword="true"/>。</returns>
    private bool ReplaceRange(int start, int end, string text)
    {
        if (Selected is not BubbleAnnotation bubble)
        {
            return false;
        }

        // **変換中かどうかを先に控える**: ここで `_isComposing` を落としてから判定すると、
        // 変換中でも「本文が変わった」と IME へ伝えてしまい、変換が中断されて
        // **1 キーごとに確定**する（実測 2026-11-23: 代入の順序が逆になっていた）。
        var wasComposing = _isComposing;
        _isComposing = false;
        _preeditText = string.Empty;
        PrepareUndo();
        // 挿入した文字へ保留している書式を反映する（規則はコアが単一所有する）
        _document = _document.WithText(
            bubble,
            AnnotationTextStyleEdit.Insert(bubble.Text, start, end - start, text, _typingStyle.Value));
        RaiseDocumentChanged();

        // **変換中は知らせない**: 変換の途中で「本文が変わった」と伝えると IME の変換が中断され、
        // 1 キーごとに確定してしまう（実測 2026-11-22）。 確定したときだけ知らせる。
        if (!wasComposing)
        {
            NotifyImeTextChanged(start, end - start, text.Length);
        }

        _caretIndex = start + text.Length;
        _selectionAnchor = _caretIndex;
        SyncTextSelectionRange();
        Redraw();
        return true;
    }

    /// <summary>選択範囲の装飾（太字・斜体・下線・取り消し線）を切り替える。</summary>
    /// <param name="read">属性を読む関数。</param>
    /// <param name="write">属性を書く関数。</param>
    /// <returns>処理した場合は <see langword="true"/>。</returns>
    internal bool ToggleDecoration(
        Func<AnnotationTextStyle, bool> read,
        Func<AnnotationTextStyle, bool, AnnotationTextStyle> write)
        // 適用先（選択範囲・選択が無ければ以降の入力）の決定は Apply/Transform が単一所有する
        => TransformTextStyle(style => write(style, !read(style)));

    /// <summary>選択範囲の文字がすべて条件を満たすかどうかを取得する（書式ボタンの押下状態に使う）。</summary>
    /// <param name="match">書式が条件を満たすかどうかを返す関数。</param>
    /// <returns>すべて満たす場合は <see langword="true"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="match"/> が null の場合。</exception>
    public bool SelectedTextMatches(Func<AnnotationTextStyle, bool> match)
    {
        ArgumentNullException.ThrowIfNull(match);

        if (Selected is not BubbleAnnotation bubble)
        {
            return false;
        }

        var range = TextSelectionRange;
        return range.Length <= 0
            ? match(SelectedTextStyle)
            : bubble.Text.AllMatch(range.Start, range.Length, match);
    }

    /// <summary>選択範囲の書式を変換する（1 つの属性だけ変える）。</summary>
    /// <param name="transform">書式を変える処理。</param>
    /// <remarks>選択が無ければ以降の入力へ回す（判定は <see cref="TransformTextStyle(Func{AnnotationTextStyle, AnnotationTextStyle})"/> が持つ）。</remarks>
    internal void TransformSelectionStyle(Func<AnnotationTextStyle, AnnotationTextStyle> transform)
        => _ = TransformTextStyle(transform);

    /// <summary>選択範囲へ書式を丸ごと適用する（色・大きさ・フォント）。</summary>
    /// <param name="style">書式。</param>
    internal void ApplyFullStyle(AnnotationTextStyle style) => ApplyTextStyle(style);

    /// <summary>選択範囲を <see cref="TextSelectionRange"/> へ写す（書式の適用先に使う）。</summary>
    private void SyncTextSelectionRange()
    {
        TextSelectionChanged?.Invoke(this, EventArgs.Empty);
        RestartCaretBlink();
    }

    /// <summary>編集の開始・終了を通知する。</summary>
    private void RaiseEditingChanged() => EditingChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>本文の編集（選択範囲・キャレット・変換中の文字列）を描く。</summary>
    private void DrawEditing()
    {
        _editing.Children.Clear();
        if (!_isEditingText || ResolveLineIndex() is not { } index)
        {
            return;
        }

        var (scale, offset) = ResolveImageTransform();

        foreach (var rect in index.ResolveSelectionRects(TextSelectionStart, TextSelectionEnd, MeasureText))
        {
            var shape = new Rectangle
            {
                Width = Math.Max(1, rect.Width * scale),
                Height = Math.Max(1, rect.Height * scale),
                Fill = new SolidColorBrush(s_textSelectionColor),
            };

            Canvas.SetLeft(shape, (rect.X * scale) + offset.X);
            Canvas.SetTop(shape, (rect.Y * scale) + offset.Y);
            _editing.Children.Add(shape);
        }

        var caret = ResolveCaretDip();

        // **変換中の文字列はキャレットの位置へ置き、キャレットはその末尾へ移す**: 重ねると既存の文字と
        // 二重になり、キャレットが動かないため「入力が反映されていない」ように見える（実測 2026-11-22）。
        // 変換中の文字列には下線を引く（未確定であることを示す）。
        var caretX = caret.X;
        if (_preeditText.Length > 0)
        {
            var style = SelectedTextStyle with { IsUnderline = true };
            var preedit = new TextBlock
            {
                Text = _preeditText,
                FontFamily = new FontFamily(style.FontFamily),
                FontSize = style.FontSize * scale,
                FontWeight = style.IsBold ? FontWeights.Bold : FontWeights.Normal,
                FontStyle = style.IsItalic ? Windows.UI.Text.FontStyle.Italic : Windows.UI.Text.FontStyle.Normal,
                Foreground = AnnotationTextMetrics.ToBrush(style.Color),
                TextDecorations = Windows.UI.Text.TextDecorations.Underline,
                IsHitTestVisible = false,
            };

            preedit.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(preedit, caret.X);
            Canvas.SetTop(preedit, caret.Y);
            _editing.Children.Add(preedit);
            caretX = caret.X + preedit.DesiredSize.Width;
        }

        // **キャレットは一般的な速さで点滅させる**（規則はコアの AnnotationCaretBlink が単一所有する）
        if (!IsCaretVisible)
        {
            return;
        }

        var caretShape = new Rectangle
        {
            Width = CaretThicknessDip,
            Height = Math.Max(1, caret.Height),
            Fill = new SolidColorBrush(s_caretColor),
        };

        Canvas.SetLeft(caretShape, caretX);
        Canvas.SetTop(caretShape, caret.Y);
        _editing.Children.Add(caretShape);
    }

    /// <summary>キャレットの位置（DIP）を求める。</summary>
    /// <returns>キャレットの左上と行の高さ。</returns>
    private (double X, double Y, double Height) ResolveCaretDip()
    {
        if (ResolveLineIndex() is not { } index || index.ResolveCaret(_caretIndex, MeasureText) is not { } caret)
        {
            return default;
        }

        var (scale, offset) = ResolveImageTransform();
        return (
            (caret.X * scale) + offset.X,
            (caret.Y * scale) + offset.Y,
            Math.Max(1, caret.Height * scale));
    }

    /// <summary>選択中の吹き出しの行と文字位置の対応を解決する。</summary>
    /// <returns>対応。 解決できない場合は <see langword="null"/>。</returns>
    private AnnotationTextLineIndex? ResolveLineIndex()
    {
        if (Selected is not BubbleAnnotation bubble)
        {
            return null;
        }

        var plan = _presenter.ResolveDrawPlan(_document, MeasureText, SelectedId);
        var shape = plan.Shapes.FirstOrDefault(
            candidate => string.Equals(candidate.Id, bubble.Id, StringComparison.Ordinal));
        return shape is null ? null : AnnotationTextLineIndex.Create(shape, bubble.Text.PlainText);
    }

    /// <summary>本文の編集でキーが押されたときの処理。</summary>
    /// <param name="e">キーの引数。</param>
    /// <returns>処理した場合は <see langword="true"/>。</returns>
    private bool HandleTextEditingKey(KeyRoutedEventArgs e)
    {
        var control = IsDown(VirtualKey.Control);
        var shift = IsDown(VirtualKey.Shift);

        if (control && !shift)
        {
            switch (e.Key)
            {
                case VirtualKey.B:
                    return ToggleDecoration(style => style.IsBold, static (style, value) => style with { IsBold = value });
                case VirtualKey.I:
                    return ToggleDecoration(style => style.IsItalic, static (style, value) => style with { IsItalic = value });
                case VirtualKey.U:
                    return ToggleDecoration(style => style.IsUnderline, static (style, value) => style with { IsUnderline = value });
                case VirtualKey.C:
                    _ = CopySelectionAsync();
                    return true;
                case VirtualKey.X:
                    _ = CutSelectionAsync();
                    return true;
                case VirtualKey.V:
                    _ = PasteAsync();
                    return true;
                case VirtualKey.A:
                    _selectionAnchor = 0;
                    _caretIndex = TextForInput.Length;
                    SyncTextSelectionRange();
                    Redraw();
                    return true;
            }
        }

        switch (e.Key)
        {
            case VirtualKey.Escape:
                EndTextEditing();
                return true;
            case VirtualKey.Enter:
                ReplaceSelection("\n");
                return true;
            case VirtualKey.Back:
                DeleteAtCaret(forward: false);
                return true;
            case VirtualKey.Delete:
                DeleteAtCaret(forward: true);
                return true;
            case VirtualKey.Left:
                MoveCaret(shift ? _caretIndex - 1 : TextSelectionStart - 1, shift);
                return true;
            case VirtualKey.Right:
                MoveCaret(shift ? _caretIndex + 1 : TextSelectionEnd + 1, shift);
                return true;
            case VirtualKey.Up:
                MoveCaretToNeighbourLine(-1, shift);
                return true;
            case VirtualKey.Down:
                MoveCaretToNeighbourLine(1, shift);
                return true;
            case VirtualKey.Home:
                MoveCaretToLineEdge(first: true, shift);
                return true;
            case VirtualKey.End:
                MoveCaretToLineEdge(first: false, shift);
                return true;
            default:
                return false;
        }
    }

    /// <summary>選択中の文字をクリップボードへコピーする。</summary>
    /// <remarks>キー入力が注釈レイヤーへ届かない環境向けの入口（ホストのキー処理から呼ぶ）。</remarks>
    public void CopyTextSelection() => _ = CopySelectionAsync();

    /// <summary>選択中の文字を切り取る。</summary>
    /// <remarks>キー入力が注釈レイヤーへ届かない環境向けの入口（ホストのキー処理から呼ぶ）。</remarks>
    public void CutTextSelection() => _ = CutSelectionAsync();

    /// <summary>クリップボードの文字を挿入する。</summary>
    /// <remarks>キー入力が注釈レイヤーへ届かない環境向けの入口（ホストのキー処理から呼ぶ）。</remarks>
    public void PasteText() => _ = PasteAsync();

    /// <summary>選択中の文字をクリップボードへコピーする。</summary>
    /// <returns>コピーの非同期処理。</returns>
    private async Task CopySelectionAsync()
    {
        var text = SelectedText();
        if (text.Length == 0)
        {
            return;
        }

        var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
        package.SetText(text);
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
        await Task.CompletedTask;
    }

    /// <summary>選択中の文字を切り取る。</summary>
    /// <returns>切り取りの非同期処理。</returns>
    private async Task CutSelectionAsync()
    {
        if (SelectedText().Length == 0)
        {
            return;
        }

        await CopySelectionAsync();
        ReplaceSelection(string.Empty);
    }


    /// <summary>クリップボードの文字を挿入する。</summary>
    /// <returns>貼り付けの非同期処理。</returns>
    private async Task PasteAsync()
    {
        var content = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
        if (!content.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text))
        {
            return;
        }

        var text = await content.GetTextAsync();
        if (!string.IsNullOrEmpty(text))
        {
            ReplaceSelection(text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n'));
        }
    }

    /// <summary>選択中の文字を取得する。</summary>
    /// <returns>選択中の文字列（選択が無い場合は空）。</returns>
    private string SelectedText()
    {
        var text = TextForInput;
        var start = Math.Clamp(TextSelectionStart, 0, text.Length);
        var end = Math.Clamp(TextSelectionEnd, 0, text.Length);
        return end <= start ? string.Empty : text[start..end];
    }

    /// <summary>確定した文字列を受け取る（IME の変換中は受け取らない）。</summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">文字入力の引数。</param>
    private void OnCharacterReceived(UIElement sender, CharacterReceivedRoutedEventArgs e)
    {
        _ = sender;

        if (!_isEditingText || _isComposing)
        {
            return;
        }

        var text = e.Character.ToString();
        if (string.IsNullOrEmpty(text) || char.IsControl(e.Character))
        {
            return;
        }

        ReplaceSelection(text);
        e.Handled = true;
    }

    /// <summary>キャレットを移動する（<paramref name="extend"/> なら選択を広げる）。</summary>
    /// <param name="index">移動先（文字数）。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    private void MoveCaret(int index, bool extend)
    {
        _caretIndex = Math.Clamp(index, 0, TextForInput.Length);
        if (!extend)
        {
            _selectionAnchor = _caretIndex;
        }

        SyncTextSelectionRange();
        NotifyImeSelection();
        Redraw();
    }

    /// <summary>キャレットを上下の行の同じ桁へ移す。</summary>
    /// <param name="delta">移動する行数。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    private void MoveCaretToNeighbourLine(int delta, bool extend)
    {
        if (ResolveLineIndex() is not { } index || index.Lines.Count == 0)
        {
            return;
        }

        var lineIndex = index.LineIndexOf(_caretIndex);
        var target = Math.Clamp(lineIndex + delta, 0, index.Lines.Count - 1);
        var column = _caretIndex - index.LineStart(lineIndex);
        var length = AnnotationTextLineIndex.LineTextLength(index.Lines[target]);
        MoveCaret(index.LineStart(target) + Math.Min(column, length), extend);
    }

    /// <summary>キャレットを行の先頭・末尾へ移す。</summary>
    /// <param name="first">行の先頭へ移す場合は <see langword="true"/>。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    private void MoveCaretToLineEdge(bool first, bool extend)
    {
        if (ResolveLineIndex() is not { } index || index.Lines.Count == 0)
        {
            MoveCaret(first ? 0 : TextForInput.Length, extend);
            return;
        }

        var lineIndex = index.LineIndexOf(_caretIndex);
        var length = AnnotationTextLineIndex.LineTextLength(index.Lines[lineIndex]);
        MoveCaret(first ? index.LineStart(lineIndex) : index.LineStart(lineIndex) + length, extend);
    }

    /// <summary>点（元画像ピクセル）の位置へキャレットを移す。</summary>
    /// <param name="point">位置（元画像ピクセル）。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    private void SetCaretFromPoint(EsPoint point, bool extend)
    {
        if (ResolveLineIndex() is not { } index)
        {
            return;
        }

        _caretIndex = Math.Clamp(index.IndexAt(point, MeasureText), 0, TextForInput.Length);
        if (!extend)
        {
            _selectionAnchor = _caretIndex;
        }

        SyncTextSelectionRange();
        NotifyImeSelection();
        Redraw();
    }
}
