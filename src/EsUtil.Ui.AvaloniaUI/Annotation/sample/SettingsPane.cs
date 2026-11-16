using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using EsUtil.Feature.Annotation;
using EsUtil.Platform.Core.Imaging;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.AvaloniaUI.Annotation.Sample;

/// <summary>既定値（新規作成時の外観・書式）と全体設定を編集する左側のペイン。</summary>
/// <remarks>
/// <b>既定値はコントロールの <c>DraftDefaults</c> へ書き戻す</b>: 既存の注釈は変わらず、これから作る注釈にだけ効く。
/// 個々の注釈の編集は、選択して右クリック（<c>AnnotationLayerControl</c> のフライアウト）で行う。
/// </remarks>
internal sealed class SettingsPane : StackPanel
{
    /// <summary>見本の色（枠線・塗り・文字色の共通の候補）。</summary>
    private static readonly DrawingColor[] s_swatches =
    [
        DrawingColor.FromArgb(255, 0, 0, 0),
        DrawingColor.FromArgb(255, 255, 255, 255),
        DrawingColor.FromArgb(255, 220, 38, 38),
        DrawingColor.FromArgb(255, 234, 88, 12),
        DrawingColor.FromArgb(255, 250, 204, 21),
        DrawingColor.FromArgb(255, 22, 163, 74),
        DrawingColor.FromArgb(255, 37, 99, 235),
        DrawingColor.FromArgb(255, 147, 51, 234),
    ];

    /// <summary>編集対象のコントロール。</summary>
    private readonly AnnotationLayerControl _layer;

    /// <summary>現在の既定値（編集のたびに新しい値へ置き換える）。</summary>
    private AnnotationDraftDefaults _defaults = AnnotationDraftDefaults.Default;

    /// <summary>ペインを初期化する。</summary>
    /// <param name="layer">既定値を適用するコントロール。</param>
    public SettingsPane(AnnotationLayerControl layer)
    {
        _layer = layer;
        Width = 300;
        Spacing = 6;
        Margin = new Thickness(8);
        Build();
    }

    /// <summary>ペインの中身を組み立てる。</summary>
    private void Build()
    {
        Children.Add(Heading("既定値（新規作成時に使う）"));

        Children.Add(SubHeading("枠線"));
        Children.Add(ColorRow("枠線の色", _defaults.FrameAppearance.Border,
            color => SetFrame(_defaults.FrameAppearance with { Border = color })));
        Children.Add(NumberRow("枠線の太さ", _defaults.FrameAppearance.BorderThickness,
            AnnotationLimits.BorderThicknessMin, AnnotationLimits.BorderThicknessMax,
            value => SetFrame(_defaults.FrameAppearance with { BorderThickness = value })));
        Children.Add(ColorRow("内側の色", _defaults.FrameAppearance.Fill,
            color => SetFrame(_defaults.FrameAppearance with { Fill = color })));
        Children.Add(NumberRow("角丸", _defaults.FrameAppearance.CornerRadius,
            0, AnnotationLimits.CornerRadiusMax,
            value => SetFrame(_defaults.FrameAppearance with { CornerRadius = value })));

        Children.Add(SubHeading("吹き出し"));
        Children.Add(ColorRow("枠線の色", _defaults.BubbleAppearance.Border,
            color => SetBubble(_defaults.BubbleAppearance with { Border = color })));
        Children.Add(NumberRow("枠線の太さ", _defaults.BubbleAppearance.BorderThickness,
            AnnotationLimits.BorderThicknessMin, AnnotationLimits.BorderThicknessMax,
            value => SetBubble(_defaults.BubbleAppearance with { BorderThickness = value })));
        Children.Add(ColorRow("内側の色", _defaults.BubbleAppearance.Fill,
            color => SetBubble(_defaults.BubbleAppearance with { Fill = color })));
        Children.Add(NumberRow("角丸", _defaults.BubbleAppearance.CornerRadius,
            0, AnnotationLimits.CornerRadiusMax,
            value => SetBubble(_defaults.BubbleAppearance with { CornerRadius = value })));
        Children.Add(NumberRow("本文の余白", _defaults.BodyPadding,
            0, AnnotationLimits.BodyPaddingMax,
            value => SetDefaults(_defaults with { BodyPadding = value })));
        Children.Add(NumberRow("しっぽの幅", _defaults.TailWidth,
            AnnotationLimits.TailWidthMin, AnnotationLimits.TailWidthMax,
            value => SetDefaults(_defaults with { TailWidth = value })));

        Children.Add(SubHeading("本文の書式"));
        Children.Add(ColorRow("文字色", _defaults.TextStyle.Color,
            color => SetText(_defaults.TextStyle with { Color = color })));
        Children.Add(NumberRow("大きさ", _defaults.TextStyle.FontSize,
            AnnotationLimits.FontSizeMin, AnnotationLimits.FontSizeMax,
            value => SetText(_defaults.TextStyle with { FontSize = value })));
        Children.Add(TextStyleToggles());

        Children.Add(SubHeading("全体"));
        Children.Add(NumberRow("行間の倍率", _layer.LineHeightFactor, 0.8, 3.0,
            value => _layer.LineHeightFactor = value, increment: 0.05m));
        Children.Add(NumberRow("ハンドルの掴む半径", _layer.HandleHitRadius, 2, 64,
            value => _layer.HandleHitRadius = value));

        var edit = new Button { Content = "選択中の注釈を編集（右クリックと同じ）" };
        edit.Click += (_, _) => _layer.ShowStyleEditor();
        Children.Add(edit);
    }

    /// <summary>枠線の既定値を差し替える。</summary>
    /// <param name="appearance">新しい外観。</param>
    private void SetFrame(AnnotationAppearance appearance)
        => SetDefaults(_defaults with { FrameAppearance = appearance });

    /// <summary>吹き出しの既定値を差し替える。</summary>
    /// <param name="appearance">新しい外観。</param>
    private void SetBubble(AnnotationAppearance appearance)
        => SetDefaults(_defaults with { BubbleAppearance = appearance });

    /// <summary>本文の既定の書式を差し替える。</summary>
    /// <param name="style">新しい書式。</param>
    private void SetText(AnnotationTextStyle style)
        => SetDefaults(_defaults with { TextStyle = style });

    /// <summary>既定値を差し替えてコントロールへ反映する。</summary>
    /// <param name="defaults">新しい既定値。</param>
    private void SetDefaults(AnnotationDraftDefaults defaults)
    {
        _defaults = defaults;
        _layer.DraftDefaults = defaults;
    }

    /// <summary>本文の装飾（太字・斜体・下線・取り消し線）の切り替えを作る。</summary>
    /// <returns>切り替えの行。</returns>
    private Control TextStyleToggles()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        row.Children.Add(new TextBlock { Text = "装飾", Width = 110, VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(Toggle("B", _defaults.TextStyle.IsBold, style => style with { IsBold = !style.IsBold }, bold: true));
        row.Children.Add(Toggle("I", _defaults.TextStyle.IsItalic, style => style with { IsItalic = !style.IsItalic }, italic: true));
        row.Children.Add(Toggle("U", _defaults.TextStyle.IsUnderline, style => style with { IsUnderline = !style.IsUnderline }));
        row.Children.Add(Toggle("S", _defaults.TextStyle.IsStrike, style => style with { IsStrike = !style.IsStrike }));
        return row;
    }

    /// <summary>装飾の切り替えボタンを作る。</summary>
    /// <param name="text">ボタンの文言。</param>
    /// <param name="isChecked">現在の状態。</param>
    /// <param name="toggle">切り替えの関数。</param>
    /// <param name="bold">太字で表示するかどうか。</param>
    /// <param name="italic">斜体で表示するかどうか。</param>
    /// <returns>切り替えボタン。</returns>
    private Control Toggle(
        string text, bool isChecked, Func<AnnotationTextStyle, AnnotationTextStyle> toggle,
        bool bold = false, bool italic = false)
    {
        var button = new ToggleButton
        {
            Content = text,
            IsChecked = isChecked,
            Width = 34,
            FontWeight = bold ? FontWeight.Bold : FontWeight.Normal,
            FontStyle = italic ? FontStyle.Italic : FontStyle.Normal,
        };
        button.IsCheckedChanged += (_, _) => SetText(toggle(_defaults.TextStyle));
        return button;
    }

    /// <summary>見出しを作る。</summary>
    /// <param name="text">文言。</param>
    /// <returns>見出し。</returns>
    private static Control Heading(string text) => new TextBlock
    {
        Text = text,
        FontWeight = FontWeight.Bold,
        Margin = new Thickness(0, 0, 0, 2),
    };

    /// <summary>小見出しを作る。</summary>
    /// <param name="text">文言。</param>
    /// <returns>小見出し。</returns>
    private static Control SubHeading(string text) => new TextBlock
    {
        Text = text,
        FontWeight = FontWeight.SemiBold,
        Margin = new Thickness(0, 6, 0, 0),
    };

    /// <summary>色の入力（<c>#AARRGGBB</c> と見本）を作る。</summary>
    /// <param name="label">項目名。</param>
    /// <param name="initial">現在の色。</param>
    /// <param name="apply">適用する処理。</param>
    /// <returns>色の入力。</returns>
    private static Control ColorRow(string label, DrawingColor initial, Action<DrawingColor> apply)
    {
        var panel = new StackPanel { Spacing = 2 };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        row.Children.Add(new TextBlock { Text = label, Width = 110, VerticalAlignment = VerticalAlignment.Center });

        var box = new TextBox { Text = initial.ToHexText(), Width = 100, VerticalContentAlignment = VerticalAlignment.Center };
        box.LostFocus += (_, _) => Apply(box.Text);
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                Apply(box.Text);
                e.Handled = true;
            }
        };
        row.Children.Add(box);
        panel.Children.Add(row);

        var swatches = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            Margin = new Thickness(114, 0, 0, 0),
        };
        foreach (var color in s_swatches)
        {
            var swatch = new Button
            {
                Width = 22,
                Height = 16,
                Padding = new Thickness(0),
                Background = new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B)),
            };
            swatch.Click += (_, _) =>
            {
                box.Text = color.ToHexText();
                apply(color);
            };
            swatches.Children.Add(swatch);
        }

        panel.Children.Add(swatches);
        return panel;

        void Apply(string? text)
        {
            if (DrawingColorExtensions.TryParseHexText(text, out var color))
            {
                apply(color);
            }
        }
    }

    /// <summary>数値の入力を作る。</summary>
    /// <param name="label">項目名。</param>
    /// <param name="initial">現在の値。</param>
    /// <param name="minimum">下限。</param>
    /// <param name="maximum">上限。</param>
    /// <param name="apply">適用する処理。</param>
    /// <param name="increment">増減の幅。</param>
    /// <returns>数値の入力。</returns>
    private static Control NumberRow(
        string label, double initial, double minimum, double maximum, Action<double> apply, decimal increment = 1m)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        row.Children.Add(new TextBlock { Text = label, Width = 110, VerticalAlignment = VerticalAlignment.Center });

        var box = new NumericUpDown
        {
            Value = (decimal)initial,
            Minimum = (decimal)minimum,
            Maximum = (decimal)maximum,
            Increment = increment,
            FormatString = "0.##",
            Width = 120,
        };
        box.ValueChanged += (_, _) =>
        {
            if (box.Value is { } value)
            {
                apply((double)value);
            }
        };
        row.Children.Add(box);
        return row;
    }
}
