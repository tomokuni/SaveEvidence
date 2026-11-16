using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using EsUtil.Feature.Annotation;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.AvaloniaUI.Annotation;

/// <summary>注釈の外観・本文の書式・重なり順を編集するフライアウト（右クリック）。</summary>
/// <remarks>
/// <b>選択中の注釈だけを編集する</b>: 既存の注釈の外観・書式は注釈ごとに持つ（既定値を変えても既存は変わらない）。<br/>
/// <b>変更は即時に反映する</b>: 各入力の変更で <see cref="AnnotationLayerControl"/> の文書を書き換える
/// （ホストは <c>DocumentChanged</c> で保存できる）。<br/>
/// <b>色は <c>#AARRGGBB</c> の入力 + 見本</b>にする（色ダイアログを持たないサンプルでも同じ UI を使えるようにする）。
/// 不正な文字列は適用しない（入力中に色が消えないようにする）。
/// </remarks>
internal static class AnnotationStyleFlyout
{
    /// <summary>色の見本（枠線・塗り・文字色の共通の候補）。</summary>
    private static readonly (string Name, DrawingColor Color)[] s_swatches =
    [
        ("黒", DrawingColor.FromArgb(255, 0, 0, 0)),
        ("白", DrawingColor.FromArgb(255, 255, 255, 255)),
        ("赤", DrawingColor.FromArgb(255, 220, 38, 38)),
        ("橙", DrawingColor.FromArgb(255, 234, 88, 12)),
        ("黄", DrawingColor.FromArgb(255, 250, 204, 21)),
        ("緑", DrawingColor.FromArgb(255, 22, 163, 74)),
        ("青", DrawingColor.FromArgb(255, 37, 99, 235)),
        ("紫", DrawingColor.FromArgb(255, 147, 51, 234)),
    ];

    /// <summary>フライアウトを生成する。</summary>
    /// <param name="owner">編集対象の注釈を持つコントロール。</param>
    /// <returns>表示用のフライアウト。</returns>
    internal static Flyout Create(AnnotationLayerControl owner)
    {
        var content = new StackPanel { Spacing = 6, Width = 320 };
        content.Children.Add(BuildAppearanceGroup(owner));

        if (owner.Selected is BubbleAnnotation)
        {
            content.Children.Add(new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Color.FromArgb(64, 128, 128, 128)),
                Margin = new Thickness(0, 2),
            });
            content.Children.Add(BuildBubbleGroup(owner));
            content.Children.Add(BuildTextGroup(owner));
        }

        content.Children.Add(new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Color.FromArgb(64, 128, 128, 128)),
            Margin = new Thickness(0, 2),
        });
        content.Children.Add(BuildOrderGroup(owner));

        return new Flyout
        {
            // 画面の高さに収める（はみ出すと上端が切れる。 実測: 640 では画面外へ出た）
            Content = new ScrollViewer { Content = content, MaxHeight = 420 },
            Placement = PlacementMode.Pointer,
        };
    }

    /// <summary>外観（枠線・内側・角丸）の編集を作る。</summary>
    /// <param name="owner">編集対象の注釈を持つコントロール。</param>
    /// <returns>外観の編集。</returns>
    private static Control BuildAppearanceGroup(AnnotationLayerControl owner)
    {
        var appearance = owner.Selected?.Appearance ?? AnnotationAppearance.Default;
        var panel = new StackPanel { Spacing = 4 };

        panel.Children.Add(Heading("外観"));
        panel.Children.Add(ColorRow(
            "枠線の色",
            appearance.Border,
            color => owner.UpdateSelectedAppearance(a => a with { Border = color })));
        panel.Children.Add(NumberRow(
            "枠線の太さ",
            appearance.BorderThickness,
            AnnotationLimits.BorderThicknessMin,
            AnnotationLimits.BorderThicknessMax,
            value => owner.UpdateSelectedAppearance(a => a with { BorderThickness = value })));
        panel.Children.Add(ColorRow(
            "内側の色",
            appearance.Fill,
            color => owner.UpdateSelectedAppearance(a => a with { Fill = color })));
        panel.Children.Add(NumberRow(
            "角丸",
            appearance.CornerRadius,
            0,
            AnnotationLimits.CornerRadiusMax,
            value => owner.UpdateSelectedAppearance(a => a with { CornerRadius = value })));

        return panel;
    }

    /// <summary>吹き出し（本文の余白・しっぽ）の編集を作る。</summary>
    /// <param name="owner">編集対象の注釈を持つコントロール。</param>
    /// <returns>吹き出しの編集。</returns>
    private static Control BuildBubbleGroup(AnnotationLayerControl owner)
    {
        var bubble = (BubbleAnnotation)owner.Selected!;
        var panel = new StackPanel { Spacing = 4 };

        panel.Children.Add(Heading("吹き出し"));
        panel.Children.Add(NumberRow(
            "本文の余白",
            bubble.BodyPadding,
            0,
            AnnotationLimits.BodyPaddingMax,
            value => owner.SetSelectedBodyPadding(value)));
        panel.Children.Add(NumberRow(
            "しっぽの幅",
            bubble.Tail.Width,
            AnnotationLimits.TailWidthMin,
            AnnotationLimits.TailWidthMax,
            value => owner.UpdateSelectedTail(tail => tail with { Width = value })));
        panel.Children.Add(ChoiceRow(
            "しっぽの辺",
            ["上", "右", "下", "左"],
            (int)bubble.Tail.BaseEdge,
            index => owner.UpdateSelectedTail(tail => tail with { BaseEdge = (BubbleEdge)index })));

        return panel;
    }

    /// <summary>本文の書式（フォント・大きさ・色・装飾）の編集を作る。</summary>
    /// <param name="owner">編集対象の注釈を持つコントロール。</param>
    /// <returns>書式の編集。</returns>
    /// <remarks>適用範囲は <see cref="AnnotationLayerControl.TextSelectionRange"/>（長さ 0 は全体）である。</remarks>
    private static Control BuildTextGroup(AnnotationLayerControl owner)
    {
        var style = owner.SelectedTextStyle;
        var panel = new StackPanel { Spacing = 4 };

        panel.Children.Add(Heading("本文の書式"));
        panel.Children.Add(ChoiceRow(
            "フォント",
            [.. Fonts.All],
            Math.Max(0, Fonts.All.ToList().IndexOf(style.FontFamily)),
            index => owner.UpdateTextStyle(current => current with { FontFamily = Fonts.All[index] })));
        panel.Children.Add(NumberRow(
            "大きさ",
            style.FontSize,
            AnnotationLimits.FontSizeMin,
            AnnotationLimits.FontSizeMax,
            value => owner.UpdateTextStyle(current => current with { FontSize = value })));
        panel.Children.Add(ColorRow(
            "文字色",
            style.Color,
            color => owner.UpdateTextStyle(current => current with { Color = color })));
        panel.Children.Add(ToggleRow(style, owner));

        return panel;
    }

    /// <summary>太字・斜体・下線・取り消し線の切り替えを作る。</summary>
    /// <param name="style">現在の書式。</param>
    /// <param name="owner">編集対象の注釈を持つコントロール。</param>
    /// <returns>切り替えの行。</returns>
    private static Control ToggleRow(AnnotationTextStyle style, AnnotationLayerControl owner)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        row.Children.Add(new TextBlock
        {
            Text = "装飾",
            Width = 110,
            VerticalAlignment = VerticalAlignment.Center,
        });

        row.Children.Add(Toggle("B", style.IsBold, FontWeight.Bold, owner, current => current with { IsBold = !current.IsBold }));
        row.Children.Add(Toggle("I", style.IsItalic, FontWeight.Normal, owner, current => current with { IsItalic = !current.IsItalic }, italic: true));
        row.Children.Add(Toggle("U", style.IsUnderline, FontWeight.Normal, owner, current => current with { IsUnderline = !current.IsUnderline }));
        row.Children.Add(Toggle("S", style.IsStrike, FontWeight.Normal, owner, current => current with { IsStrike = !current.IsStrike }));
        return row;
    }

    /// <summary>装飾の切り替えボタンを作る。</summary>
    /// <param name="text">ボタンの文言。</param>
    /// <param name="isChecked">現在の状態。</param>
    /// <param name="weight">文字の太さ。</param>
    /// <param name="owner">編集対象の注釈を持つコントロール。</param>
    /// <param name="toggle">切り替えの関数（現在の書式を受け取る）。</param>
    /// <param name="italic">斜体で表示するかどうか。</param>
    /// <returns>切り替えボタン。</returns>
    private static Control Toggle(
        string text, bool isChecked, FontWeight weight, AnnotationLayerControl owner,
        Func<AnnotationTextStyle, AnnotationTextStyle> toggle, bool italic = false)
    {
        var button = new ToggleButton
        {
            Content = text,
            IsChecked = isChecked,
            Width = 34,
            FontWeight = weight,
            FontStyle = italic ? FontStyle.Italic : FontStyle.Normal,
        };
        button.IsCheckedChanged += (_, _) => owner.UpdateTextStyle(toggle);
        return button;
    }

    /// <summary>重なり順の編集を作る。</summary>
    /// <param name="owner">編集対象の注釈を持つコントロール。</param>
    /// <returns>重なり順の編集。</returns>
    private static Control BuildOrderGroup(AnnotationLayerControl owner)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        var front = new Button { Content = "最前面へ" };
        front.Click += (_, _) => owner.MoveSelectedToFront();
        var back = new Button { Content = "最背面へ" };
        back.Click += (_, _) => owner.MoveSelectedToBack();
        row.Children.Add(front);
        row.Children.Add(back);
        return row;
    }

    /// <summary>見出しを作る。</summary>
    /// <param name="text">見出しの文言。</param>
    /// <returns>見出し。</returns>
    private static Control Heading(string text) => new TextBlock
    {
        Text = text,
        FontWeight = FontWeight.SemiBold,
        Margin = new Thickness(0, 2, 0, 2),
    };

    /// <summary>色の入力（<c>#AARRGGBB</c>）と見本を作る。</summary>
    /// <param name="label">項目名。</param>
    /// <param name="initial">現在の色。</param>
    /// <param name="apply">適用する処理。</param>
    /// <returns>色の入力。</returns>
    private static Control ColorRow(string label, DrawingColor initial, Action<DrawingColor> apply)
    {
        var panel = new StackPanel { Spacing = 2 };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        row.Children.Add(new TextBlock { Text = label, Width = 110, VerticalAlignment = VerticalAlignment.Center });

        var box = new TextBox
        {
            Text = ToHex(initial),
            Width = 100,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        box.LostFocus += (_, _) => ApplyColor(box.Text, apply);
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                ApplyColor(box.Text, apply);
                e.Handled = true;
            }
        };
        row.Children.Add(box);

        var clear = new Button { Content = "透明" };
        clear.Click += (_, _) =>
        {
            box.Text = ToHex(DrawingColor.Transparent);
            apply(DrawingColor.Transparent);
        };
        row.Children.Add(clear);
        panel.Children.Add(row);

        var swatches = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Margin = new Thickness(114, 0, 0, 0) };
        foreach (var (name, color) in s_swatches)
        {
            var swatch = new Button
            {
                Width = 26,
                Height = 18,
                Padding = new Thickness(0),
                Background = AnnotationPainter.ToBrush(color),
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, 0, 0, 0)),
                BorderThickness = new Thickness(1),
                Content = new TextBlock { Text = name, FontSize = 9, Foreground = AnnotationPainter.ToBrush(ContrastText(color)) },
            };
            swatch.Click += (_, _) =>
            {
                box.Text = ToHex(color);
                apply(color);
            };
            swatches.Children.Add(swatch);
        }

        panel.Children.Add(swatches);
        return panel;
    }

    /// <summary>色の文字列を解釈して適用する（解釈できない場合は何もしない）。</summary>
    /// <param name="text">入力された文字列。</param>
    /// <param name="apply">適用する処理。</param>
    private static void ApplyColor(string? text, Action<DrawingColor> apply)
    {
        if (EsUtil.Platform.Core.Imaging.DrawingColorExtensions.TryParseHexText(text, out var color))
        {
            apply(color);
        }
    }

    /// <summary>数値の入力を作る。</summary>
    /// <param name="label">項目名。</param>
    /// <param name="initial">現在の値。</param>
    /// <param name="minimum">下限。</param>
    /// <param name="maximum">上限。</param>
    /// <param name="apply">適用する処理。</param>
    /// <returns>数値の入力。</returns>
    private static Control NumberRow(string label, double initial, double minimum, double maximum, Action<double> apply)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        row.Children.Add(new TextBlock { Text = label, Width = 110, VerticalAlignment = VerticalAlignment.Center });

        var box = new NumericUpDown
        {
            Value = (decimal)initial,
            Minimum = (decimal)minimum,
            Maximum = (decimal)maximum,
            Increment = 1,
            FormatString = "0.#",
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

    /// <summary>選択肢の入力を作る。</summary>
    /// <param name="label">項目名。</param>
    /// <param name="options">選択肢。</param>
    /// <param name="selectedIndex">初期の選択位置。</param>
    /// <param name="apply">適用する処理。</param>
    /// <returns>選択肢の入力。</returns>
    private static Control ChoiceRow(string label, string[] options, int selectedIndex, Action<int> apply)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        row.Children.Add(new TextBlock { Text = label, Width = 110, VerticalAlignment = VerticalAlignment.Center });

        var combo = new ComboBox { Width = 180, ItemsSource = options, SelectedIndex = Math.Max(0, selectedIndex) };
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedIndex >= 0)
            {
                apply(combo.SelectedIndex);
            }
        };
        row.Children.Add(combo);
        return row;
    }

    /// <summary>色を <c>#AARRGGBB</c> の文字列へ変換する。</summary>
    /// <param name="color">色。</param>
    /// <returns><c>#AARRGGBB</c> 形式の文字列。</returns>
    private static string ToHex(DrawingColor color)
        => EsUtil.Platform.Core.Imaging.DrawingColorExtensions.ToHexText(color);

    /// <summary>背景色に対して読みやすい文字色を求める。</summary>
    /// <param name="color">背景色。</param>
    /// <returns>黒または白。</returns>
    private static DrawingColor ContrastText(DrawingColor color)
        => (((color.R * 299) + (color.G * 587) + (color.B * 114)) / 1000) > 140
            ? DrawingColor.FromArgb(255, 0, 0, 0)
            : DrawingColor.FromArgb(255, 255, 255, 255);

    /// <summary>入力候補のフォント一覧（インストール済みのフォント）。</summary>
    private static class Fonts
    {
        /// <summary>フォント名の一覧を取得する（優先フォントを先頭に置く）。</summary>
        internal static IReadOnlyList<string> All { get; } = Build();

        /// <summary>フォント名の一覧を作る。</summary>
        /// <returns>フォント名の一覧。</returns>
        private static IReadOnlyList<string> Build()
        {
            var names = FontManager.Current.SystemFonts
                .Select(font => font.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.CurrentCulture)
                .ToList();

            foreach (var preferred in AnnotationDefaults.PreferredFontFamilies.Reverse())
            {
                var index = names.FindIndex(name => string.Equals(name, preferred, StringComparison.OrdinalIgnoreCase));
                if (index >= 0)
                {
                    names.RemoveAt(index);
                }

                names.Insert(0, preferred);
            }

            return names;
        }
    }
}
