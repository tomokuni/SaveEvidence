using System.Collections.Immutable;
using EsUtil.Feature.Annotation;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.WinUI3.Annotation;

/// <summary>注釈レイヤーの本文の編集（WinUI 3 版）。</summary>
/// <remarks>
/// <b>リッチ テキストの入力欄を本文の矩形へ重ねる</b>: WinUI 3 の <see cref="RichEditBox"/> が IME・選択・
/// 書式の適用・クリップボードを担う（自前でキャレットと変換を実装すると、OS の入力規則から外れる）。<br/>
/// <b>モデルは入力欄から写す</b>: 入力のたびに本文と文字単位の書式を読み取り、コアの <see cref="RichText"/> へ写す
/// （保存・描画・元に戻すは常にコアのモデルを見る）。<br/>
/// <b>編集中は本文を描かない</b>: 入力欄が本文を表示するため、下の注釈の本文は隠す（二重に表示しない）。
/// </remarks>
public sealed partial class AnnotationLayerControl
{
    /// <summary>本文の入力欄を並べる層。</summary>
    private readonly Canvas _editorLayer = new();

    /// <summary>本文の入力欄（編集中だけ保持する）。</summary>
    private RichEditBox? _editor;

    /// <summary>入力欄の変更をモデルへ写している最中かどうか（再入防止）。</summary>
    private bool _isSyncingEditor;

    /// <summary>本文の選択範囲（キャレットの位置）が変わったときに発生する。</summary>
    public event EventHandler? TextSelectionChanged;

    /// <summary>本文の編集中に発生する（開始・終了の両方）。</summary>
    public event EventHandler? EditingChanged;

    /// <summary>本文を編集中かどうかを取得する。</summary>
    public bool IsEditingText => _editor is not null;

    /// <summary>本文の選択範囲の開始位置（文字数）を取得する。</summary>
    public int TextSelectionStart => _editor is null
        ? 0
        : Math.Max(0, _editor.Document.Selection.StartPosition);

    /// <summary>本文の選択範囲の長さ（文字数）を取得する。</summary>
    public int TextSelectionLength => _editor is null
        ? 0
        : Math.Max(0, _editor.Document.Selection.EndPosition - _editor.Document.Selection.StartPosition);

    /// <summary>本文の選択範囲（開始位置と長さ）を取得する。</summary>
    /// <value>編集中でないときは (0, 0)。</value>
    public (int Start, int Length) TextSelectionRange => (TextSelectionStart, TextSelectionLength);

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

        var editor = new RichEditBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            IsSpellCheckEnabled = false,
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            FontSize = 14,
        };

        _editor = editor;
        if (!_editorLayer.Children.Contains(editor))
        {
            _editorLayer.Children.Add(editor);
        }

        _isSyncingEditor = true;
        try
        {
            var plain = bubble.Text.PlainText;
            editor.Document.SetText(TextSetOptions.None, plain.Replace("\n", "\r", StringComparison.Ordinal));
            ApplyTextToEditor(bubble.Text);
            editor.Document.Selection.SetRange(plain.Length, plain.Length);
        }
        finally
        {
            _isSyncingEditor = false;
        }

        editor.TextChanged += OnEditorTextChanged;
        editor.KeyUp += OnEditorKeyUp;
        editor.KeyDown += OnEditorKeyDown;

        UpdateEditorBounds();
        _ = editor.Focus(FocusState.Programmatic);

        RaiseEditingChanged();
        Redraw();
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
        editor.KeyUp -= OnEditorKeyUp;
        editor.KeyDown -= OnEditorKeyDown;
        _editorLayer.Children.Remove(editor);

        RaiseEditingChanged();
        Redraw();
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

    /// <summary>書式を切り替える（選択範囲、または本文全体）。</summary>
    /// <param name="apply">入力欄の選択範囲へ書式を適用する処理。</param>
    /// <param name="applyToModel">モデルの本文へ書式を適用する処理。</param>
    internal void ToggleDecoration(
        Action<Microsoft.UI.Text.ITextCharacterFormat> apply,
        Func<AnnotationTextStyle, AnnotationTextStyle> applyToModel)
    {
        ArgumentNullException.ThrowIfNull(apply);
        ArgumentNullException.ThrowIfNull(applyToModel);

        if (_editor is { } editor)
        {
            apply(editor.Document.Selection.CharacterFormat);
            CommitFromEditor();
            Redraw();
            return;
        }

        var range = TextSelectionRange;
        TransformTextStyle(range.Start, range.Length, applyToModel);
    }

    /// <summary>選択範囲へ書式を丸ごと適用する（色・大きさ・フォント）。</summary>
    /// <param name="style">書式。</param>
    internal void ApplyFullStyle(AnnotationTextStyle style)
    {
        if (_editor is { } editor)
        {
            var start = editor.Document.Selection.StartPosition;
            var end = editor.Document.Selection.EndPosition;
            WriteFormat(editor.Document.GetRange(start, end).CharacterFormat, style);
            CommitFromEditor();
            Redraw();
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
            editor.Visibility = Visibility.Collapsed;
            return;
        }

        editor.Visibility = Visibility.Visible;
        editor.Width = body.Width;
        editor.Height = body.Height;
        Canvas.SetLeft(editor, body.X);
        Canvas.SetTop(editor, body.Y);
    }

    /// <summary>入力欄が変わったらモデルへ写す。</summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">変更の引数。</param>
    private void OnEditorTextChanged(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        if (_isSyncingEditor)
        {
            return;
        }

        CommitFromEditor();
        Redraw();
    }

    /// <summary>選択範囲が変わったら書式バーへ通知する。</summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">キーの引数。</param>
    private void OnEditorKeyUp(object sender, KeyRoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        TextSelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Esc で編集を終える。</summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">キーの引数。</param>
    private void OnEditorKeyDown(object sender, KeyRoutedEventArgs e)
    {
        _ = sender;

        if (e.Key == VirtualKey.Escape)
        {
            EndTextEditing();
            e.Handled = true;
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
            var text = ReadEditorText(editor);
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

    /// <summary>入力欄の本文を読み取る（段落記号を <c>\n</c> へ揃える）。</summary>
    /// <param name="editor">入力欄。</param>
    /// <returns>本文。</returns>
    private static string ReadEditorText(RichEditBox editor)
    {
        editor.Document.GetText(TextGetOptions.None, out var raw);
        if (raw.Length > 0 && raw[^1] == '\r')
        {
            raw = raw[..^1];
        }

        return raw.Replace("\r", "\n", StringComparison.Ordinal);
    }

    /// <summary>入力欄の文字単位の書式を run の並びへ写す。</summary>
    /// <param name="editor">入力欄。</param>
    /// <param name="text">本文（<c>\n</c> へ揃えたもの）。</param>
    /// <returns>run の並び。</returns>
    private static ImmutableArray<TextRun> ReadEditorRuns(RichEditBox editor, string text)
    {
        if (text.Length == 0)
        {
            return [];
        }

        var builder = ImmutableArray.CreateBuilder<TextRun>();
        var runStart = 0;
        var current = ReadEditorStyleAt(editor, 0);
        for (var i = 1; i < text.Length; i++)
        {
            var style = ReadEditorStyleAt(editor, i);
            if (style.Equals(current))
            {
                continue;
            }

            builder.Add(new TextRun(text[runStart..i], current));
            runStart = i;
            current = style;
        }

        builder.Add(new TextRun(text[runStart..], current));
        return builder.ToImmutable();
    }

    /// <summary>指定した文字位置の書式を入力欄から読み取る。</summary>
    /// <param name="editor">入力欄。</param>
    /// <param name="index">文字位置。</param>
    /// <returns>書式。</returns>
    private static AnnotationTextStyle ReadEditorStyleAt(RichEditBox editor, int index)
        => ToStyle(editor.Document.GetRange(index, index + 1).CharacterFormat);

    /// <summary>選択範囲の書式を入力欄から読み取る。</summary>
    /// <param name="editor">入力欄。</param>
    /// <returns>書式。</returns>
    private static AnnotationTextStyle ReadEditorStyle(RichEditBox editor)
        => ToStyle(editor.Document.Selection.CharacterFormat);

    /// <summary>入力欄の書式をコアの書式へ写す。</summary>
    /// <param name="format">入力欄の書式。</param>
    /// <returns>コアの書式。</returns>
    private static AnnotationTextStyle ToStyle(ITextCharacterFormat format)
    {
        var color = format.ForegroundColor;
        return new AnnotationTextStyle(
            string.IsNullOrWhiteSpace(format.Name) ? AnnotationDefaults.PreferredFontFamilies[0] : format.Name,
            format.Size > 0 ? format.Size : AnnotationDefaults.FontSizeDefault,
            DrawingColor.FromArgb(255, color.R, color.G, color.B),
            format.Bold == FormatEffect.On,
            format.Italic == FormatEffect.On,
            format.Underline != UnderlineType.None,
            format.Strikethrough == FormatEffect.On);
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
            var range = editor.Document.GetRange(index, end);
            WriteFormat(range.CharacterFormat, run.Style);
            index = end;
        }
    }

    /// <summary>書式を入力欄へ書き込む。</summary>
    /// <param name="format">書き込む先の書式。</param>
    /// <param name="style">コアの書式。</param>
    private static void WriteFormat(ITextCharacterFormat format, AnnotationTextStyle style)
    {
        format.Bold = style.IsBold ? FormatEffect.On : FormatEffect.Off;
        format.Italic = style.IsItalic ? FormatEffect.On : FormatEffect.Off;
        format.Underline = style.IsUnderline ? UnderlineType.Single : UnderlineType.None;
        format.Strikethrough = style.IsStrike ? FormatEffect.On : FormatEffect.Off;
        format.ForegroundColor = Color.FromArgb(255, style.Color.R, style.Color.G, style.Color.B);
        format.Size = (float)style.FontSize;
        format.Name = style.FontFamily;
    }

    /// <summary>編集の開始・終了を通知する。</summary>
    private void RaiseEditingChanged() => EditingChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>モデルが変わったときに入力欄の書式を写し直す。</summary>
    private void SyncEditorFromModel()
    {
        if (_editor is null || Selected is not BubbleAnnotation bubble)
        {
            return;
        }

        _isSyncingEditor = true;
        try
        {
            ApplyTextToEditor(bubble.Text);
        }
        finally
        {
            _isSyncingEditor = false;
        }
    }
}
