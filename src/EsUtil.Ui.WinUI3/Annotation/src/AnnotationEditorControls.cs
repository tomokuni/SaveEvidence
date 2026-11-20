using EsUtil.Platform.Core.Palette;
using EsUtil.Ui.WinUI3.ColorSelector;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.WinUI3.Annotation;

/// <summary>編集 UI の行の部品（書式バーと外観パネルが共有する）。</summary>
/// <remarks>
/// <b>見た目を 1 か所へ集める</b>: 行の組み立てを各パネルへ書き写すと、余白や文字サイズが panel ごとにずれる。
/// </remarks>
internal static class AnnotationEditorControls
{
    /// <summary>行の文字サイズ（DIP）。</summary>
    internal const double FontSize = 12;

    /// <summary>項目間の余白（DIP）。</summary>
    internal const double Gap = 6;

    /// <summary>見出しを作る。</summary>
    /// <param name="text">見出しの文字列。</param>
    /// <returns>見出しの要素。</returns>
    internal static TextBlock Heading(string text) => new()
    {
        Text = text,
        FontSize = FontSize,
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        Margin = new Thickness(0, 0, 0, 2),
    };

    /// <summary>項目名を作る。</summary>
    /// <param name="text">項目名。</param>
    /// <returns>項目名の要素。</returns>
    internal static TextBlock Label(string text) => new()
    {
        Text = text,
        FontSize = FontSize,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 0, Gap, 0),
    };

    /// <summary>装飾の切り替え（B / I / U / S）を作る。</summary>
    /// <param name="text">ボタンの文字。</param>
    /// <param name="isChecked">押された状態にする場合は <see langword="true"/>。</param>
    /// <param name="apply">押された状態が変わったときに呼ぶ処理。</param>
    /// <param name="bold">太字で表示する場合は <see langword="true"/>。</param>
    /// <param name="italic">斜体で表示する場合は <see langword="true"/>。</param>
    /// <returns>切り替えボタン。</returns>
    internal static ToggleButton Toggle(string text, bool isChecked, Action<bool> apply, bool bold = false, bool italic = false)
    {
        var button = new ToggleButton
        {
            Content = new TextBlock
            {
                Text = text,
                FontWeight = bold ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Normal,
                FontStyle = italic ? Windows.UI.Text.FontStyle.Italic : Windows.UI.Text.FontStyle.Normal,
                VerticalAlignment = VerticalAlignment.Center,
            },
            IsChecked = isChecked,
            MinWidth = 28,

            // **高さを明示する**: 既定の高さでは 12 DIP の文字の上が欠ける（実測 2026-11-20）
            MinHeight = 26,
            Padding = new Thickness(4, 1, 4, 1),
            FontSize = FontSize,
        };

        button.Checked += (_, _) => apply(true);
        button.Unchecked += (_, _) => apply(false);
        return button;
    }

    /// <summary>色の行（見出し + 色の入力）を作る。</summary>
    /// <param name="label">項目名。</param>
    /// <param name="color">現在の色（<c>#AARRGGBB</c>）。</param>
    /// <param name="textFormat">入力欄の表記。</param>
    /// <param name="apply">色が変わったときに呼ぶ処理。</param>
    /// <returns>行の要素。</returns>
    internal static FrameworkElement ColorRow(
        string label, string color, ColorTextFormat textFormat, Action<string> apply)
    {
        var selector = CreateSelector(color, textFormat);
        selector.SelectedColorChanged += (_, _) => apply(selector.SelectedColor);
        return BuildRow(label, selector);
    }

    /// <summary>色の入力を作る（代表色のタブを出す）。</summary>
    /// <param name="color">現在の色（<c>#AARRGGBB</c>）。</param>
    /// <param name="textFormat">入力欄の表記。</param>
    /// <returns>色の入力。</returns>
    internal static ColorSelectorControl CreateSelector(string color, ColorTextFormat textFormat)
    {
        var selector = new ColorSelectorControl
        {
            SelectedColor = color,
            TextFormat = textFormat,
            Width = 200,
            FontSize = FontSize,
        };

        AnnotationPalette.Configure(selector, showPicker: true);
        return selector;
    }

    /// <summary>数値の行を作る。</summary>
    /// <param name="label">項目名。</param>
    /// <param name="value">現在の値。</param>
    /// <param name="minimum">最小値。</param>
    /// <param name="maximum">最大値。</param>
    /// <param name="apply">値が変わったときに呼ぶ処理。</param>
    /// <returns>行の要素。</returns>
    internal static FrameworkElement NumberRow(
        string label, double value, double minimum, double maximum, Action<double> apply)
    {
        var box = new NumberBox
        {
            Value = value,
            Minimum = minimum,
            Maximum = maximum,
            SmallChange = 1,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            Width = 120,
            FontSize = FontSize,
        };

        box.ValueChanged += (_, e) => apply(double.IsNaN(e.NewValue) ? value : e.NewValue);
        return BuildRow(label, box);
    }

    /// <summary>選択肢の行を作る。</summary>
    /// <param name="label">項目名。</param>
    /// <param name="options">選択肢。</param>
    /// <param name="selectedIndex">選ぶ位置。</param>
    /// <param name="apply">選ばれたときに呼ぶ処理。</param>
    /// <returns>行の要素。</returns>
    internal static FrameworkElement ChoiceRow(
        string label, IReadOnlyList<string> options, int selectedIndex, Action<int> apply)
    {
        var combo = new ComboBox
        {
            ItemsSource = options,
            SelectedIndex = Math.Clamp(selectedIndex, 0, Math.Max(0, options.Count - 1)),
            Width = 140,
            FontSize = FontSize,
        };

        combo.SelectionChanged += (_, _) => apply(combo.SelectedIndex);
        return BuildRow(label, combo);
    }

    /// <summary>項目名と入力欄を 1 行に並べる。</summary>
    /// <param name="label">項目名。</param>
    /// <param name="control">入力欄。</param>
    /// <returns>行の要素。</returns>
    private static FrameworkElement BuildRow(string label, FrameworkElement control)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            Margin = new Thickness(0, 0, 0, 2),
        };

        if (label.Length > 0)
        {
            row.Children.Add(Label(label));
        }

        row.Children.Add(control);
        return row;
    }

    /// <summary>色を保存値（<c>#AARRGGBB</c>）へ変換する。</summary>
    /// <param name="color">色。</param>
    /// <returns>保存値の文字列。</returns>
    internal static string ToHex(DrawingColor color)
        => $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";

    /// <summary>パネルの背景を作る。</summary>
    /// <returns>背景のブラシ。</returns>
    internal static Brush PanelBackground()
        => new SolidColorBrush(Windows.UI.Color.FromArgb(242, 250, 250, 250));

    /// <summary>区切り線を作る。</summary>
    /// <returns>区切り線の要素。</returns>
    internal static Rectangle Divider() => new()
    {
        Height = 1,
        Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(60, 0, 0, 0)),
        Margin = new Thickness(0, 3, 0, 3),
    };
}
