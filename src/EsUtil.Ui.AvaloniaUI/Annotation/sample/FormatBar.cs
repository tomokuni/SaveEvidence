using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using EsUtil.Feature.Annotation;
using EsUtil.Ui.AvaloniaUI.ColorSelector;

namespace EsUtil.Ui.AvaloniaUI.Annotation.Sample;

/// <summary>本文を編集中に入力欄の代わりに出す書式バー（文字単位の書式）。</summary>
/// <remarks>
/// <b>本文はコントロールが直接編集する</b>ため、このバーは「選択中の範囲へ書式を適用する」だけを行う。
/// 適用しても編集は続き、結果はその場で本文へ反映される（入力欄を重ねない）。<br/>
/// <b>押した後は本文へフォーカスを戻す</b>: バーの操作でキーボードの入力先が移ると、続けて入力できない。
/// </remarks>
internal sealed class FormatBar : Border
{
    /// <summary>バーの幅（DIP。画像の内側へ収める計算に使う）。</summary>
    internal const double BarWidth = 760;

    /// <summary>バーのおおよその高さ（DIP。本文の上へ置く位置の計算に使う）。</summary>
    internal const double EstimatedHeight = 96;

    /// <summary>編集対象のコントロール。</summary>
    private readonly AnnotationLayerControl _layer;

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
    /// <param name="close">編集を終える処理。</param>
    public FormatBar(AnnotationLayerControl layer, Action close)
    {
        _layer = layer;

        var fonts = AnnotationFonts.All;
        _font = new ComboBox { Width = 150, ItemsSource = fonts, SelectedIndex = 0 };
        _font.SelectionChanged += (_, _) =>
        {
            if (!_isRefreshing && _font.SelectedIndex >= 0)
            {
                var family = fonts[_font.SelectedIndex];
                Apply(style => style with { FontFamily = family });
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
                Apply(style => style with { FontSize = (double)value });
            }
        };

        _color = new ColorSelectorControl
        {
            Width = 160,
            TextFontSize = 12,
            HighlightBorderOnError = true,
        };
        _color.PropertyChanged += (_, e) =>
        {
            if (!_isRefreshing
                && e.Property == ColorSelectorControl.SelectedColorProperty
                && EsUtil.Platform.Core.Imaging.DrawingColorExtensions.TryParseHexText(_color.SelectedColor, out var color))
            {
                Apply(style => style with { Color = color });
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
        done.Click += (_, _) =>
        {
            close();
            layer.Focus();
        };

        var header = new DockPanel();
        var hint = new TextBlock
        {
            Text = "本文をその場で編集しています。 文字を選んで書式を押すと、その範囲だけに反映されます（選択が無ければ全体）。 Ctrl+B / I / U も同じです。",
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
        Padding = new Thickness(8, 5);
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

    /// <summary>選択中の範囲へ書式を適用し、表示とフォーカスを戻す。</summary>
    /// <param name="change">書式を新しい値へ変える処理（変えない属性は <c>TransformTextStyle</c> が保つ）。</param>
    private void Apply(Func<AnnotationTextStyle, AnnotationTextStyle> change)
    {
        var (start, length) = _layer.TextSelectionRange;
        _layer.TransformTextStyle(start, length, change);
        Refresh();

        // バーの操作でフォーカスが移るため、本文へ戻して続けて入力できるようにする
        _layer.Focus();
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
        => Apply(style => write(style, !read(_layer.SelectedTextStyle)));
}
