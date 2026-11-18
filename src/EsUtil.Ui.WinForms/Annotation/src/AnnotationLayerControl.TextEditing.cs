using EsUtil.Feature.Annotation;

namespace EsUtil.Ui.WinForms.Annotation;

/// <summary>注釈レイヤーの本文の編集（WinForms 版）。</summary>
/// <remarks>
/// <b>リッチ テキストの入力欄を本文の矩形へ重ねる</b>: WinForms の <see cref="RichTextBox"/> が IME・選択・
/// 書式の適用・クリップボードを担う。<br/>
/// <b>モデルは入力欄から写す</b>: 入力のたびに本文と文字単位の書式を読み取り、コアの <see cref="RichText"/> へ写す
/// （保存・描画・元に戻すは常にコアのモデルを見る）。<br/>
/// <b>編集中は本文を描かない</b>: 入力欄が本文を表示するため、下の注釈の本文は隠す（二重に表示しない）。
/// </remarks>
public sealed partial class AnnotationLayerControl
{
    /// <summary>本文の編集を始める（吹き出しを選んでいるときだけ）。</summary>
    /// <returns>編集を始めた場合は <see langword="true"/>。 枠線を選んでいる場合は <see langword="false"/>。</returns>
    public bool BeginTextEditing()
    {
        if (Selected is not BubbleAnnotation bubble)
        {
            return false;
        }

        if (_editor is not null)
        {
            return true;
        }

        var editor = new RichTextBox
        {
            Parent = this,
            BorderStyle = BorderStyle.None,
            Multiline = true,
            WordWrap = true,
            ScrollBars = RichTextBoxScrollBars.None,
            AcceptsTab = false,
            DetectUrls = false,
            Font = new Font(AnnotationDefaults.PreferredFontFamilies[0], 12, FontStyle.Regular, GraphicsUnit.Pixel),
        };

        _editor = editor;
        _isSyncingEditor = true;
        try
        {
            editor.Text = bubble.Text.PlainText;
            ApplyTextToEditor(bubble.Text);
            editor.Select(bubble.Text.PlainText.Length, 0);
        }
        finally
        {
            _isSyncingEditor = false;
        }

        editor.TextChanged += OnEditorTextChanged;
        editor.KeyDown += OnEditorKeyDown;
        editor.SelectionChanged += OnEditorSelectionChanged;

        UpdateEditorBounds();
        editor.Focus();

        RaiseEditingChanged();
        Invalidate();
        return true;
    }

    /// <summary>本文の編集を終える（内容は入力のたびにモデルへ写している）。</summary>
    public void EndTextEditing()
    {
        if (_editor is not { } editor)
        {
            return;
        }

        CommitFromEditor();
        _editor = null;

        editor.TextChanged -= OnEditorTextChanged;
        editor.KeyDown -= OnEditorKeyDown;
        editor.SelectionChanged -= OnEditorSelectionChanged;
        Controls.Remove(editor);
        editor.Dispose();

        RaiseEditingChanged();
        Invalidate();
    }

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

        if (_editor is { } editor)
        {
            return match(ReadEditorStyle(editor));
        }

        var range = TextSelectionRange;
        return range.Length <= 0
            ? match(bubble.Text.StyleAt(Math.Max(0, range.Start)))
            : bubble.Text.AllMatch(range.Start, range.Length, match);
    }

    /// <summary>選択範囲の書式を変換する（編集していないときはモデルの本文を変える）。</summary>
    /// <param name="transform">書式を変える処理。</param>
    internal void TransformSelectionStyle(Func<AnnotationTextStyle, AnnotationTextStyle> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);

        if (_editor is { } editor)
        {
            if (editor.SelectionLength == 0)
            {
                return;
            }

            var style = transform(ReadEditorStyle(editor));
            WriteFormat(editor, style);
            CommitFromEditor();
            Invalidate();
            return;
        }

        var range = TextSelectionRange;
        TransformTextStyle(range.Start, range.Length, transform);
    }

    /// <summary>選択範囲へ書式を丸ごと適用する（色・大きさ・フォント）。</summary>
    /// <param name="style">書式。</param>
    internal void ApplyFullStyle(AnnotationTextStyle style)
    {
        if (_editor is { } editor)
        {
            WriteFormat(editor, style);
            CommitFromEditor();
            Invalidate();
            return;
        }

        ApplyTextStyle(style);
    }

    /// <summary>入力欄の位置と大きさを本文の矩形へ合わせる。</summary>
    private void UpdateEditorBounds()
    {
        if (_editor is not { } editor)
        {
            return;
        }

        var body = GetSelectedBodyRect();
        if (body.Width <= 0 || body.Height <= 0)
        {
            editor.Visible = false;
            return;
        }

        editor.Visible = true;
        editor.Bounds = new Rectangle(
            (int)Math.Round(body.X), (int)Math.Round(body.Y),
            Math.Max(1, (int)Math.Round(body.Width)), Math.Max(1, (int)Math.Round(body.Height)));
    }

    /// <summary>入力欄が変わったらモデルへ写す。</summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">変更の引数。</param>
    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        if (_isSyncingEditor)
        {
            return;
        }

        CommitFromEditor();
        Invalidate();
    }

    /// <summary>選択範囲が変わったら書式バーへ通知する。</summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">変更の引数。</param>
    private void OnEditorSelectionChanged(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        if (!_isSyncingEditor)
        {
            TextSelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Esc で編集を終える。</summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">キーの引数。</param>
    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        _ = sender;

        if (e.KeyCode == Keys.Escape)
        {
            EndTextEditing();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    /// <summary>入力欄の本文と書式をモデルへ写す。</summary>
    private void CommitFromEditor()
    {
        if (_editor is not { } editor || Selected is not BubbleAnnotation bubble)
        {
            return;
        }

        _isSyncingEditor = true;
        try
        {
            var text = editor.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
            var runs = ReadEditorRuns(editor, text);
            PrepareUndo();
            _document = _document.WithText(bubble, new RichText(runs));
            RaiseDocumentChanged();
        }
        finally
        {
            _isSyncingEditor = false;
        }
    }

    /// <summary>入力欄の文字単位の書式を run の並びへ写す。</summary>
    /// <param name="editor">入力欄。</param>
    /// <param name="text">本文。</param>
    /// <returns>run の並び。</returns>
    private static System.Collections.Immutable.ImmutableArray<TextRun> ReadEditorRuns(RichTextBox editor, string text)
    {
        if (text.Length == 0)
        {
            return [];
        }

        var start = editor.SelectionStart;
        var length = editor.SelectionLength;
        var builder = System.Collections.Immutable.ImmutableArray.CreateBuilder<TextRun>();
        var runStart = 0;
        var current = ReadEditorStyleAt(editor, 0);
        for (var i = 1; i < text.Length; i++)
        {
            var style = ReadEditorStyleAt(editor, i);
            if (!style.Equals(current))
            {
                builder.Add(new TextRun(text[runStart..i], current));
                runStart = i;
                current = style;
            }
        }

        builder.Add(new TextRun(text[runStart..], current));
        editor.Select(start, length);
        return builder.ToImmutable();
    }

    /// <summary>指定した文字位置の書式を入力欄から読み取る。</summary>
    /// <param name="editor">入力欄。</param>
    /// <param name="index">文字位置。</param>
    /// <returns>書式。</returns>
    private static AnnotationTextStyle ReadEditorStyleAt(RichTextBox editor, int index)
    {
        editor.Select(index, 1);
        return ReadEditorStyle(editor);
    }

    /// <summary>選択範囲の書式を入力欄から読み取る。</summary>
    /// <param name="editor">入力欄。</param>
    /// <returns>書式。</returns>
    /// <remarks>選択が無い場合はキャレットの位置の書式を読む（読み取った後は選択を元へ戻す）。</remarks>
    private static AnnotationTextStyle ReadEditorStyle(RichTextBox editor)
    {
        var start = editor.SelectionStart;
        var length = editor.SelectionLength;
        if (length == 0)
        {
            editor.Select(Math.Max(0, start - 1), Math.Min(1, Math.Max(0, start)));
        }

        var font = editor.SelectionFont;
        var color = editor.SelectionColor;
        var style = new AnnotationTextStyle(
            font?.FontFamily.Name ?? AnnotationDefaults.PreferredFontFamilies[0],
            font?.Size ?? (float)AnnotationDefaults.FontSizeDefault,
            Color.FromArgb(255, color.R, color.G, color.B),
            font?.Bold ?? false,
            font?.Italic ?? false,
            font?.Underline ?? false,
            font?.Strikeout ?? false);

        editor.Select(start, length);
        return style;
    }

    /// <summary>コアの本文を入力欄へ写す（本文と書式）。</summary>
    /// <param name="text">本文。</param>
    private void ApplyTextToEditor(RichText text)
    {
        if (_editor is not { } editor)
        {
            return;
        }

        var index = 0;
        foreach (var run in text.Runs)
        {
            var end = Math.Min(index + run.Text.Length, text.PlainText.Length);
            editor.Select(index, Math.Max(0, end - index));
            ApplyFormatToSelection(editor, run.Style);
            index = end;
        }

        editor.Select(text.PlainText.Length, 0);
    }

    /// <summary>入力欄の選択範囲へ書式を書き込む。</summary>
    /// <param name="editor">入力欄。</param>
    /// <param name="style">書式。</param>
    private static void WriteFormat(RichTextBox editor, AnnotationTextStyle style)
    {
        ApplyFormatToSelection(editor, style);
    }

    /// <summary>入力欄の選択範囲へ書式を適用する。</summary>
    /// <param name="editor">入力欄。</param>
    /// <param name="style">書式。</param>
    private static void ApplyFormatToSelection(RichTextBox editor, AnnotationTextStyle style)
    {
        using var font = AnnotationTextMetrics.CreateFont(style with { IsUnderline = false, IsStrike = false });
        editor.SelectionFont = font;
        editor.SelectionColor = style.Color;
        if (style.IsUnderline || style.IsStrike)
        {
            // WinForms の SelectionFont は装飾を含められる（サイズとファミリはそのまま）
            using var decorated = new Font(
                font.FontFamily,
                font.Size,
                font.Style
                | (style.IsUnderline ? FontStyle.Underline : FontStyle.Regular)
                | (style.IsStrike ? FontStyle.Strikeout : FontStyle.Regular),
                GraphicsUnit.Pixel);
            editor.SelectionFont = decorated;
        }
    }

    /// <summary>編集の開始・終了を通知する。</summary>
    private void RaiseEditingChanged() => EditingChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>モデルが変わったときに入力欄の本文と書式を写し直す。</summary>
    private void SyncEditorFromModel()
    {
        if (_editor is not { } editor || Selected is not BubbleAnnotation bubble)
        {
            return;
        }

        _isSyncingEditor = true;
        try
        {
            if (!string.Equals(editor.Text, bubble.Text.PlainText, StringComparison.Ordinal))
            {
                editor.Text = bubble.Text.PlainText;
            }

            ApplyTextToEditor(bubble.Text);
        }
        finally
        {
            _isSyncingEditor = false;
        }
    }
}
