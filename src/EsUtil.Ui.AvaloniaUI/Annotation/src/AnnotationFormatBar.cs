using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using EsUtil.Feature.Annotation;
using EsUtil.Ui.AvaloniaUI.ColorSelector;

namespace EsUtil.Ui.AvaloniaUI.Annotation;

/// <summary>本文を編集中に本文の上へ出す書式バー（文字単位の書式）。</summary>
/// <remarks>
/// <b>本文はコントロールが直接編集する</b>ため、このバーは「選択中の範囲へ書式を適用する」だけを行う。
/// 適用しても編集は続き、結果はその場で本文へ反映される。<br/>
/// <b>項目名は出さない</b>: 幅が限られるため、意味はツールチップで示す（フォント名・大きさ・装飾名とショートカット）。<br/>
/// <b>同じ値でも適用できる</b>: 文字単位で書式が混ざっている範囲を一律に揃えるため、選び直しただけでも適用する。
/// </remarks>
internal sealed class AnnotationFormatBar : Border
{
    /// <summary>バーの幅（DIP。画像の内側へ収める計算に使う）。</summary>
    internal const double BarWidth = 720;

    /// <summary>色の選択の幅（DIP。「■ + 全角 4 文字の色名」が入る幅。 16 色のうち最も長い色名で決める）。</summary>
    internal const double ColorSelectorWidth = 112;

    /// <summary>バーのおおよその高さ（DIP。本文の上へ置く位置の計算に使う）。</summary>
    internal const double EstimatedHeight = 64;

    /// <summary>取り消し線のショートカット（一般的な表記に合わせる）。</summary>
    internal const string StrikeShortcut = "Alt+Shift+5";

    /// <summary>編集対象のコントロール。</summary>
    private readonly AnnotationLayerControl _layer;

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

    /// <summary>代表色の並べ方（色名付きの一覧 / 色の四角だけを 6 列）。</summary>
    private ColorSelectorTabLayout _paletteLayout = ColorSelectorTabLayout.List;

    /// <summary>書式バーに出す代表色の組。</summary>
    private AnnotationPaletteKind _palette = AnnotationPaletteKind.Representative;

    /// <summary>書式バーを初期化する。</summary>
    /// <param name="layer">編集対象のコントロール。</param>
    /// <param name="close">編集を終える処理。</param>
    public AnnotationFormatBar(AnnotationLayerControl layer, Action close)
    {
        _layer = layer;

        var fonts = AnnotationFonts.All;
        _font = new ComboBox { Width = 150, ItemsSource = fonts, SelectedIndex = 0 };
        _font.SelectionChanged += (_, _) => ApplyFont();

        // 同じ値を選び直したときは SelectionChanged が来ないため、閉じたときにも適用する
        _font.DropDownClosed += (_, _) => ApplyFont();

        _size = new NumericUpDown
        {
            Width = 112,
            Minimum = 6m,
            Maximum = 512m,
            Increment = 1m,
            FormatString = "0.#",
        };
        _size.ValueChanged += (_, _) => ApplySize();

        _color = new ColorSelectorControl
        {
            // 「■ + 色名」が出る幅（16 進の入力や色名の直接入力もできるだけの幅を残す）
            Width = ColorSelectorWidth,
            HighlightBorderOnError = true,
        };

        // 代表色のタブとカラーピッカーのタブを出す（既存のタブは出さない。 タブが 1 つなら題名も出さない）
        ConfigurePalette();
        _color.PropertyChanged += (_, e) =>
        {
            if (e.Property == ColorSelectorControl.SelectedColorProperty)
            {
                ApplyColor();
            }
        };

        // 同じ色を選び直したときは値が変わらず変更通知が出ないため、一覧・カラーピッカーの確定でも適用する
        // （文字単位で色が混ざった範囲を一律にそろえるため。 入力欄へ打ち込んだ場合は LostFocus で適用する）
        _color.ColorApplied += (_, _) => ApplyColor();

        // 色の入力欄から離れたら、値が変わっていなくても適用する
        _color.LostFocus += (_, _) => ApplyColor();

        _bold = Toggle("B", "太字（Ctrl+B）", bold: true);
        _italic = Toggle("I", "斜体（Ctrl+I）", italic: true);
        _underline = Toggle("U", "下線（Ctrl+U）", underline: true);
        _strike = Toggle("S", $"取り消し線（{StrikeShortcut}）", strike: true);

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
            Text = "本文をその場で編集しています。 文字を選んで書式を押すと、その範囲だけに反映されます（選択が無ければ全体）。",
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
        };
        DockPanel.SetDock(done, Dock.Right);
        header.Children.Add(done);
        header.Children.Add(hint);

        var controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        controls.Children.Add(_font);
        controls.Children.Add(_size);
        controls.Children.Add(_color);
        controls.Children.Add(_bold);
        controls.Children.Add(_italic);
        controls.Children.Add(_underline);
        controls.Children.Add(_strike);

        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(header);
        panel.Children.Add(controls);

        Width = BarWidth;
        Background = Brushes.White;
        BorderBrush = Brushes.DodgerBlue;
        BorderThickness = new Thickness(1);
        Padding = new Thickness(8, 5);
        Child = panel;

        // 大きさと色は Enter で確定する（同じ値でも適用する）
        AddHandler(KeyDownEvent, OnBarKeyDown, RoutingStrategies.Tunnel);
    }

    /// <summary>現在の書式を表示へ反映する（選択範囲の先頭の書式）。</summary>
    public void Refresh()
    {
        var style = _layer.SelectedTextStyle;

        _isRefreshing = true;
        try
        {
            var index = AnnotationFonts.All.ToList().IndexOf(style.FontFamily);
            _font.SelectedIndex = index >= 0 ? index : -1;
            _size.Value = (decimal)style.FontSize;
            _color.SelectedColor = AnnotationEditorControls.ToHex(style.Color);

            // 装飾は「選択範囲のすべてが同じ属性か」で押下状態を決める（一部だけ太字の範囲で押されて見えないように）
            _bold.IsChecked = _layer.SelectedTextMatches(textStyle => textStyle.IsBold);
            _italic.IsChecked = _layer.SelectedTextMatches(textStyle => textStyle.IsItalic);
            _underline.IsChecked = _layer.SelectedTextMatches(textStyle => textStyle.IsUnderline);
            _strike.IsChecked = _layer.SelectedTextMatches(textStyle => textStyle.IsStrike);

            // 幅が狭いとフォント名・大きさが読めないため、現在値をツールチップにも出す
            ToolTip.SetTip(_font, style.FontFamily);
            ToolTip.SetTip(_size, $"{style.FontSize:0.#}");
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    /// <summary>代表色の並べ方を取得または設定する（色名付きの一覧 / 色の四角だけを 6 列）。</summary>
    /// <value>既定は色名付きの一覧（<see cref="ColorSelectorTabLayout.List"/>）。</value>
    /// <remarks>色名を読まずに選びたいときは <see cref="ColorSelectorTabLayout.Grid"/> にする（スクロールせずに選べる）。</remarks>
    public ColorSelectorTabLayout PaletteLayout
    {
        get => _paletteLayout;
        set
        {
            if (_paletteLayout == value)
            {
                return;
            }

            _paletteLayout = value;
            ConfigurePalette();
        }
    }

    /// <summary>書式バーに出す代表色の組を取得または設定する（代表色 1 / 代表色 2）。</summary>
    /// <value>既定は代表色 1（<see cref="AnnotationPaletteKind.Representative"/>）。</value>
    public AnnotationPaletteKind Palette
    {
        get => _palette;
        set
        {
            if (_palette == value)
            {
                return;
            }

            _palette = value;
            ConfigurePalette();
        }
    }

    /// <summary>代表色の組と並べ方を色の選択へ反映する。</summary>
    private void ConfigurePalette() => AnnotationPalette.Configure(_color, _palette, _paletteLayout, showPicker: true);

    /// <summary>バーの中で Enter が押されたら、大きさと色を今の値で適用する。</summary>
    /// <param name="sender">送信元。</param>
    /// <param name="e">キーの引数。</param>
    private void OnBarKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || e.Source is not Visual source)
        {
            return;
        }

        if (IsInside(source, _color))
        {
            ApplyColor();
            e.Handled = true;
        }
        else if (IsInside(source, _size))
        {
            ApplySize();
            e.Handled = true;
        }
    }

    /// <summary>指定された要素が指定された祖先の内側にあるかどうかを判定する。</summary>
    /// <param name="visual">判定する要素。</param>
    /// <param name="ancestor">祖先。</param>
    /// <returns>内側にある場合は <see langword="true"/>。</returns>
    private static bool IsInside(Visual visual, Visual ancestor)
    {
        for (var current = visual; current is not null; current = current.GetVisualParent())
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>選択中の範囲へフォントを適用する（同じ値でも適用する）。</summary>
    private void ApplyFont()
    {
        if (_isRefreshing || _font.SelectedIndex < 0)
        {
            return;
        }

        var family = AnnotationFonts.All[_font.SelectedIndex];
        Apply(style => style with { FontFamily = family });
        ToolTip.SetTip(_font, family);
    }

    /// <summary>選択中の範囲へ大きさを適用する（同じ値でも適用する）。</summary>
    private void ApplySize()
    {
        if (_isRefreshing || _size.Value is not { } value)
        {
            return;
        }

        Apply(style => style with { FontSize = (double)value });
        ToolTip.SetTip(_size, $"{value:0.#}");
    }

    /// <summary>選択中の範囲へ色を適用する（同じ値でも適用する）。</summary>
    private void ApplyColor()
    {
        if (_isRefreshing
            || !EsUtil.Platform.Core.Imaging.DrawingColorExtensions.TryParseHexText(_color.SelectedColor, out var color))
        {
            return;
        }

        Apply(style => style with { Color = color });
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
    /// <param name="toolTip">ツールチップ（装飾名とショートカット）。</param>
    /// <param name="bold">太字で表示するかどうか。</param>
    /// <param name="italic">斜体で表示するかどうか。</param>
    /// <param name="underline">下線を引いて表示するかどうか。</param>
    /// <param name="strike">取り消し線を引いて表示するかどうか。</param>
    /// <returns>切り替えボタン。</returns>
    private static ToggleButton Toggle(
        string text, string toolTip, bool bold = false, bool italic = false, bool underline = false, bool strike = false)
    {
        var content = new TextBlock
        {
            Text = text,
            FontWeight = bold ? FontWeight.Bold : FontWeight.Normal,
            FontStyle = italic ? FontStyle.Italic : FontStyle.Normal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        if (underline)
        {
            content.TextDecorations = TextDecorations.Underline;
        }

        if (strike)
        {
            content.TextDecorations = TextDecorations.Strikethrough;
        }

        var button = new ToggleButton
        {
            Content = content,
            Width = 34,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        ToolTip.SetTip(button, toolTip);
        return button;
    }

    /// <summary>装飾を切り替える（適用先の先頭の書式で切り替えの向きを決める）。</summary>
    /// <param name="read">属性を読む関数。</param>
    /// <param name="write">属性を書く関数。</param>
    private void ToggleDecoration(
        Func<AnnotationTextStyle, bool> read,
        Func<AnnotationTextStyle, bool, AnnotationTextStyle> write)
        => Apply(style => write(style, !read(_layer.SelectedTextStyle)));
}
