using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using EsUtil.Feature.Annotation;
using EsUtil.Ui.AvaloniaUI.ColorSelector;

namespace EsUtil.Ui.AvaloniaUI.Annotation.Sample;

/// <summary>本文を編集中に入力欄の上へ出す書式バー（文字単位の書式）。</summary>
/// <remarks>
/// <b>行を分けて崩れを防ぐ</b>: 1 行に全部並べると幅が足りず、数値の入力欄が切れる（実測で指摘）。
/// 1 行目は説明と「完了」、2 行目は適用先、3 行目は書式の操作にする。<br/>
/// <b>適用しても閉じない</b>: 選択した文字に対して続けて書式を変えられる（結果は確定後に本文へ出る）。
/// </remarks>
internal sealed class FormatBar : Border
{
    /// <summary>バーの幅（DIP。画像の内側へ収める計算に使う）。</summary>
    internal const double BarWidth = 720;

    /// <summary>バーのおおよその高さ（DIP。入力欄の上へ置く位置の計算に使う）。</summary>
    internal const double EstimatedHeight = 92;

    /// <summary>編集対象のコントロール。</summary>
    private readonly AnnotationLayerControl _layer;

    /// <summary>書式を適用する処理。</summary>
    private readonly Action<Func<AnnotationTextStyle, AnnotationTextStyle>> _apply;

    /// <summary>編集を確定する処理。</summary>
    private readonly Action _commit;

    /// <summary>適用先の表示。</summary>
    private readonly TextBlock _target = new() { FontSize = 11, Opacity = 0.75, TextWrapping = TextWrapping.Wrap };

    /// <summary>フォントの選択。</summary>
    private readonly ComboBox _font;

    /// <summary>文字の大きさの入力。</summary>
    private readonly NumericUpDown _size;

    /// <summary>文字色の選択。</summary>
    private readonly ColorSelectorControl _color;

    /// <summary>太字の切り替え。</summary>
    private readonly ToggleButton _bold;

    /// <summary>斜体の切り替え。</summary>
    private readonly ToggleButton _italic;

    /// <summary>下線の切り替え。</summary>
    private readonly ToggleButton _underline;

    /// <summary>取り消し線の切り替え。</summary>
    private readonly ToggleButton _strike;

    /// <summary>表示を更新している最中かどうか（操作の再入を防ぐ）。</summary>
    private bool _isRefreshing;

    /// <summary>書式バーを初期化する。</summary>
    /// <param name="layer">編集対象のコントロール。</param>
    /// <param name="apply">書式を適用する処理。</param>
    /// <param name="commit">編集を確定する処理。</param>
    public FormatBar(
        AnnotationLayerControl layer,
        Action<Func<AnnotationTextStyle, AnnotationTextStyle>> apply,
        Action commit)
    {
        _layer = layer;
        _apply = apply;
        _commit = commit;

        var fonts = AnnotationFonts.All;
        _font = new ComboBox { Width = 150, ItemsSource = fonts, SelectedIndex = 0 };
        _font.SelectionChanged += (_, _) =>
        {
            if (!_isRefreshing && _font.SelectedIndex >= 0)
            {
                var family = fonts[_font.SelectedIndex];
                _apply(style => style with { FontFamily = family });
            }
        };

        _size = new NumericUpDown
        {
            Width = 112,
            Minimum = 6m,
            Maximum = 512m,
            Increment = 1m,
            FormatString = "0.#",
        };
        _size.ValueChanged += (_, _) =>
        {
            if (!_isRefreshing && _size.Value is { } value)
            {
                _apply(style => style with { FontSize = (double)value });
            }
        };

        _color = new ColorSelectorControl
        {
            Width = 150,
            TextFontSize = 12,
            HighlightBorderOnError = true,
        };
        _color.PropertyChanged += (_, e) =>
        {
            if (!_isRefreshing
                && e.Property == ColorSelectorControl.SelectedColorProperty
                && EsUtil.Platform.Core.Imaging.DrawingColorExtensions.TryParseHexText(_color.SelectedColor, out var color))
            {
                _apply(style => style with { Color = color });
            }
        };

        _bold = Toggle("B", bold: true);
        _italic = Toggle("I", italic: true);
        _underline = Toggle("U", underline: true);
        _strike = Toggle("S", strike: true);

        _bold.Click += (_, _) => ToggleDecoration(style => style.IsBold, (style, value) => style with { IsBold = value });
        _italic.Click += (_, _) => ToggleDecoration(style => style.IsItalic, (style, value) => style with { IsItalic = value });
        _underline.Click += (_, _) => ToggleDecoration(style => style.IsUnderline, (style, value) => style with { IsUnderline = value });
        _strike.Click += (_, _) => ToggleDecoration(style => style.IsStrike, (style, value) => style with { IsStrike = value });

        var done = new Button { Content = "完了" };
        done.Click += (_, _) => _commit();

        var header = new DockPanel();
        var hint = new TextBlock
        {
            Text = "書式: 文字を選んで押します（選択が無ければ本文全体）。 Ctrl+B / I / U も同じです。",
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
        };
        DockPanel.SetDock(done, Dock.Right);
        header.Children.Add(done);
        header.Children.Add(hint);

        var controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        controls.Children.Add(new TextBlock { Text = "フォント", VerticalAlignment = VerticalAlignment.Center, FontSize = 11 });
        controls.Children.Add(_font);
        controls.Children.Add(new TextBlock { Text = "大きさ", VerticalAlignment = VerticalAlignment.Center, FontSize = 11, Margin = new Thickness(4, 0, 0, 0) });
        controls.Children.Add(_size);
        controls.Children.Add(new TextBlock { Text = "色", VerticalAlignment = VerticalAlignment.Center, FontSize = 11, Margin = new Thickness(4, 0, 0, 0) });
        controls.Children.Add(_color);
        controls.Children.Add(_bold);
        controls.Children.Add(_italic);
        controls.Children.Add(_underline);
        controls.Children.Add(_strike);

        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(header);
        panel.Children.Add(_target);
        panel.Children.Add(controls);

        Width = BarWidth;
        Background = Brushes.White;
        BorderBrush = Brushes.DodgerBlue;
        BorderThickness = new Thickness(1);
        Padding = new Thickness(6, 4);
        Child = panel;
    }

    /// <summary>現在の書式と適用先を表示へ反映する。</summary>
    public void Refresh()
    {
        var style = _layer.SelectedTextStyle;
        var (start, length) = _layer.TextSelectionRange;

        _isRefreshing = true;
        try
        {
            var index = AnnotationFonts.All.ToList().IndexOf(style.FontFamily);
            _font.SelectedIndex = index >= 0 ? index : -1;
            _size.Value = (decimal)style.FontSize;
            _color.SelectedColor = AnnotationEditorControls.ToHex(style.Color);
            _bold.IsChecked = style.IsBold;
            _italic.IsChecked = style.IsItalic;
            _underline.IsChecked = style.IsUnderline;
            _strike.IsChecked = style.IsStrike;
            _target.Text = length > 0
                ? $"適用先: {start + 1}〜{start + length} 文字目（選択範囲）"
                : "適用先: 本文全体（文字を選ぶと、その範囲だけに適用します）";
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    /// <summary>装飾の切り替えボタンを作る。</summary>
    /// <param name="text">文言。</param>
    /// <param name="bold">太字で表示するかどうか。</param>
    /// <param name="italic">斜体で表示するかどうか。</param>
    /// <param name="underline">下線を引いて表示するかどうか。</param>
    /// <param name="strike">取り消し線を引いて表示するかどうか。</param>
    /// <returns>切り替えボタン。</returns>
    private static ToggleButton Toggle(string text, bool bold = false, bool italic = false, bool underline = false, bool strike = false)
    {
        var content = new TextBlock
        {
            Text = text,
            FontWeight = bold ? FontWeight.Bold : FontWeight.Normal,
            FontStyle = italic ? FontStyle.Italic : FontStyle.Normal,
        };

        if (underline)
        {
            content.TextDecorations = TextDecorations.Underline;
        }

        if (strike)
        {
            content.TextDecorations = TextDecorations.Strikethrough;
        }

        return new ToggleButton { Content = content, Width = 36 };
    }

    /// <summary>装飾を切り替える（適用先の先頭の書式で切り替えの向きを決める）。</summary>
    /// <param name="read">属性を読む関数。</param>
    /// <param name="write">属性を書く関数。</param>
    private void ToggleDecoration(
        Func<AnnotationTextStyle, bool> read,
        Func<AnnotationTextStyle, bool, AnnotationTextStyle> write)
    {
        var current = read(_layer.SelectedTextStyle);
        _apply(style => write(style, !current));
    }
}
