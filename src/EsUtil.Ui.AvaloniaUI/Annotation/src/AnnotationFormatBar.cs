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
using System.Globalization;

namespace EsUtil.Ui.AvaloniaUI.Annotation;

/// <summary>本文を編集中に本文の上へ出す書式バー（文字単位の書式）。</summary>
/// <remarks>
/// <b>本文はコントロールが直接編集する</b>ため、このバーは「選択中の範囲へ書式を適用する」だけを行う。
/// 適用しても編集は続き、結果はその場で本文へ反映される。<br/>
/// <b>項目名は出さない</b>: 幅が限られるため、意味はツールチップで示す（フォント名・大きさ・装飾名とショートカット）。<br/>
/// <b>同じ値でも適用できる</b>: 文字単位で書式が混ざっている範囲を一律に揃えるため、選び直しただけでも適用する。<br/>
/// <b>文字を選んでいない間は操作できない</b>: 適用先が決まらないため、書式の操作をすべて無効にする（実測で指摘）。<br/>
/// <b>大きさは中身に合わせる</b>: 説明の行と「完了」は置かず、書式の操作だけを 1 行に並べる（編集の邪魔をしない）。
/// </remarks>
internal sealed class AnnotationFormatBar : Border
{
    /// <summary>バーの幅（DIP。画像の内側へ収める計算に使う）。</summary>
    internal const double BarWidth = 720;

    /// <summary>バーのおおよその高さ（DIP。本文の上へ置く位置の計算に使う）。</summary>
    internal const double EstimatedHeight = 40;

    /// <summary>取り消し線のショートカット（一般的な表記に合わせる）。</summary>
    internal const string StrikeShortcut = "Alt+Shift+5";

    /// <summary>色の入力欄の左の余白（DIP。 現在色の四角の分。 <c>ColorSelectorControl</c> と同じ値）。</summary>
    private const double ColorTextIndent = 24;

    /// <summary>色の入力欄の展開ボタンの幅（DIP。 <c>ColorSelectorControl</c> と同じ値）。</summary>
    private const double ColorToggleWidth = 34;

    /// <summary>色の幅を測る文字サイズ（DIP）。</summary>
    private const double ColorFontSize = 12;

    /// <summary>色の幅を測れなかったときの幅（DIP。 実測値の近似）。</summary>
    private const double ColorSelectorWidthFallback = 84;

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

    /// <summary>代表色パレットの列数（■ を何列で並べるか）。</summary>
    private int _paletteColumns = AnnotationPalette.DefaultPaletteColumns;

    /// <summary>色の選択の幅（DIP。 <see cref="ResolveColorSelectorWidth"/> が決める）。</summary>
    private readonly double _colorWidth;

    /// <summary>書式バーを初期化する。</summary>
    /// <param name="layer">編集対象のコントロール。</param>
    public AnnotationFormatBar(AnnotationLayerControl layer)
    {
        _layer = layer;

        var fonts = AnnotationFonts.All;
        _font = new ComboBox { Width = FontSelectorWidth, ItemsSource = fonts, SelectedIndex = 0 };
        _font.SelectionChanged += (_, _) => ApplyFont();

        // 同じ値を選び直したときは SelectionChanged が来ないため、閉じたときにも適用する
        _font.DropDownClosed += (_, _) => ApplyFont();
        ToolTip.SetTip(_font, "フォント");

        _size = new NumericUpDown
        {
            Width = SizeSelectorWidth,
            Minimum = 6m,
            Maximum = 512m,
            Increment = 1m,
            FormatString = "0.#",
        };
        _size.ValueChanged += (_, _) => ApplySize();
        ToolTip.SetTip(_size, "大きさ");

        _colorWidth = ResolveColorSelectorWidth();
        _color = new ColorSelectorControl
        {
            Width = _colorWidth,
            HighlightBorderOnError = true,
        };

        // ■ だけの「代表色パレット」と色名の「代表色」を出し、カラーピッカーも使えるようにする
        ConfigurePalette();
        _color.PropertyChanged += (_, e) =>
        {
            if (e.Property == ColorSelectorControl.SelectedColorProperty)
            {
                ApplyColor();
            }
        };

        // 同じ色を選び直したときは値が変わらず変更通知が出ないため、一覧・カラーピッカーの確定でも適用する
        _color.ColorApplied += (_, _) => ApplyColor();

        // 色の入力欄から離れたら、値が変わっていなくても適用する
        _color.LostFocus += (_, _) => ApplyColor();
        ToolTip.SetTip(_color, "色");

        _bold = Toggle("B", "太字（Ctrl+B）", bold: true);
        _italic = Toggle("I", "斜体（Ctrl+I）", italic: true);
        _underline = Toggle("U", "下線（Ctrl+U）", underline: true);
        _strike = Toggle("S", $"取り消し線（{StrikeShortcut}）", strike: true);

        _bold.Click += (_, _) => ToggleDecoration(style => style.IsBold, (style, value) => style with { IsBold = value });
        _italic.Click += (_, _) => ToggleDecoration(style => style.IsItalic, (style, value) => style with { IsItalic = value });
        _underline.Click += (_, _) => ToggleDecoration(style => style.IsUnderline, (style, value) => style with { IsUnderline = value });
        _strike.Click += (_, _) => ToggleDecoration(style => style.IsStrike, (style, value) => style with { IsStrike = value });

        var controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = ControlSpacing };
        controls.Children.Add(_font);
        controls.Children.Add(_size);
        controls.Children.Add(_color);
        controls.Children.Add(_bold);
        controls.Children.Add(_italic);
        controls.Children.Add(_underline);
        controls.Children.Add(_strike);

        Background = Brushes.White;
        BorderBrush = Brushes.DodgerBlue;
        BorderThickness = new Thickness(1);
        Padding = new Thickness(6, 3);
        Child = controls;

        // 大きさと色は Enter で確定する（同じ値でも適用する）
        AddHandler(KeyDownEvent, OnBarKeyDown, RoutingStrategies.Tunnel);
    }

    /// <summary>フォントの選択の幅（DIP。 設定ペインのフォントの選択と同じ幅）。</summary>
    internal const double FontSelectorWidth = 210;

    /// <summary>大きさの入力の幅（DIP）。</summary>
    internal const double SizeSelectorWidth = 104;

    /// <summary>操作の間の余白（DIP）。</summary>
    private const double ControlSpacing = 4;

    /// <summary>代表色パレットの列数を取得または設定する（■ を何列で並べるか）。</summary>
    /// <value>1 以上の列数。 既定は <see cref="AnnotationPalette.DefaultPaletteColumns"/>。</value>
    public int PaletteColumns
    {
        get => _paletteColumns;
        set
        {
            var columns = Math.Max(1, value);
            if (_paletteColumns == columns)
            {
                return;
            }

            _paletteColumns = columns;
            ConfigurePalette();
        }
    }

    /// <summary>色の選択の幅（DIP）を取得する。</summary>
    /// <value>「■ + 全角 4 文字 + 100%」と「■ + <c>#FFFFFFFF</c>」の狭いほうの幅。</value>
    public double ColorWidth => _colorWidth;

    /// <summary>色の選択の幅を求める（「■ + 全角 4 文字 + 100%」と「■ + #FFFFFFFF」の狭いほう）。</summary>
    /// <returns>幅（DIP）。 測れない場合は <see cref="ColorSelectorWidthFallback"/>。</returns>
    /// <remarks>
    /// <b>狭いほうに合わせる</b>: 代表色で最も長い色名は全角 4 文字（「薄い灰色」）で、不透明度が付くと
    /// <c>+100%</c> が加わる。 16 進の入力（<c>#FFFFFFFF</c>）より長くなることはないため、
    /// 両方の実寸を測って狭いほうへ合わせる（余白を残さず、書式バーを細く保つ）。
    /// </remarks>
    internal static double ResolveColorSelectorWidth()
    {
        var typeface = new Typeface(FontFamily.Default);
        var nameWidth = MeasureText(typeface, "薄い灰色+100%");
        var hexWidth = MeasureText(typeface, "#FFFFFFFF");
        var text = Math.Min(nameWidth, hexWidth);
        if (text <= 0)
        {
            return ColorSelectorWidthFallback;
        }

        return Math.Ceiling(ColorTextIndent + text + ColorToggleWidth);
    }

    /// <summary>文字列の実寸の幅を測る。</summary>
    /// <param name="typeface">書体。</param>
    /// <param name="text">測る文字列。</param>
    /// <returns>幅（DIP）。 測れない場合は 0。</returns>
    private static double MeasureText(Typeface typeface, string text)
    {
        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            ColorFontSize,
            Brushes.Black);
        return formatted.WidthIncludingTrailingWhitespace;
    }

    /// <summary>代表色のタブを色の選択へ反映する。</summary>
    private void ConfigurePalette() => AnnotationPalette.Configure(_color, _paletteColumns, showPicker: true);

    /// <summary>現在の書式を表示へ反映する（選択範囲の先頭の書式）。</summary>
    public void Refresh()
    {
        var style = _layer.SelectedTextStyle;
        var enabled = _layer.HasTextSelection;

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

            // 文字を選んでいない間は適用先が決まらないため、操作をすべて無効にする
            _font.IsEnabled = enabled;
            _size.IsEnabled = enabled;
            _color.IsEnabled = enabled;
            _bold.IsEnabled = enabled;
            _italic.IsEnabled = enabled;
            _underline.IsEnabled = enabled;
            _strike.IsEnabled = enabled;
        }
        finally
        {
            _isRefreshing = false;
        }

        // 無効の切り替えで必要な幅が変わるため測り直させる
        InvalidateMeasure();
    }

    /// <summary>バーの中で Enter が押されたら、大きさと色を今の値で適用する。</summary>
    /// <param name="sender">送信元。</param>
    /// <param name="e">キーの引数。</param>
    private void OnBarKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || e.Source is not Visual source || !_layer.HasTextSelection)
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
        if (!_layer.HasTextSelection)
        {
            return;
        }

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
            Width = ToggleWidth,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        ToolTip.SetTip(button, toolTip);
        return button;
    }

    /// <summary>装飾の切り替えボタンの幅（DIP）。</summary>
    private const double ToggleWidth = 34;

    /// <summary>装飾を切り替える（適用先の先頭の書式で切り替えの向きを決める）。</summary>
    /// <param name="read">属性を読む関数。</param>
    /// <param name="write">属性を書く関数。</param>
    private void ToggleDecoration(
        Func<AnnotationTextStyle, bool> read,
        Func<AnnotationTextStyle, bool, AnnotationTextStyle> write)
        => Apply(style => write(style, !read(_layer.SelectedTextStyle)));
}
