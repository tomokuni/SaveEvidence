using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using EsUtil.Feature.Annotation;

namespace EsUtil.Ui.AvaloniaUI.Annotation;

/// <summary>注釈の外観・本文の書式・重なり順を編集するフライアウト（右クリック）。</summary>
/// <remarks>
/// <b>本クラスは Annotation が内包する</b>（<c>EsUtil.Ui.AvaloniaUI.Annotation</c> の internal）。 利用側は
/// <c>AnnotationLayerControl.ShowStyleEditor</c> を呼ぶだけで、編集 UI の組み立てを持たない。<br/>
/// <b>選択中の注釈だけを編集する</b>: 既存の注釈の外観・書式は注釈ごとに持つ（既定値を変えても既存は変わらない）。<br/>
/// <b>行の部品は <see cref="AnnotationEditorControls"/> を共有する</b>（ホストの設定ペインと同じ見た目・同じ色の解釈）。
/// </remarks>
internal static class AnnotationStyleFlyout
{
    /// <summary>フライアウトを生成する。</summary>
    /// <param name="owner">編集対象の注釈を持つコントロール。</param>
    /// <returns>表示用のフライアウト。</returns>
    internal static Flyout Create(AnnotationLayerControl owner)
    {
        var content = new StackPanel { Spacing = 8, Width = 340 };
        content.Children.Add(BuildAppearanceGroup(owner));

        if (owner.Selected is BubbleAnnotation)
        {
            content.Children.Add(Separator());
            content.Children.Add(BuildBubbleGroup(owner));
            content.Children.Add(Separator());
            content.Children.Add(BuildTextGroup(owner));
        }

        content.Children.Add(Separator());
        content.Children.Add(BuildOrderGroup(owner));

        return new Flyout
        {
            // 画面の高さに収める（はみ出すと上端が切れる。 実測: 640 では画面外へ出た）
            Content = new ScrollViewer { Content = content, MaxHeight = 460 },
            Placement = PlacementMode.Pointer,
        };
    }

    /// <summary>区切り線を作る。</summary>
    /// <returns>区切り線。</returns>
    private static Control Separator() => new Border
    {
        Height = 1,
        Background = new SolidColorBrush(Color.FromArgb(64, 128, 128, 128)),
    };

    /// <summary>外観（枠線・内側・角丸）の編集を作る。</summary>
    /// <param name="owner">編集対象の注釈を持つコントロール。</param>
    /// <returns>外観の編集。</returns>
    private static Control BuildAppearanceGroup(AnnotationLayerControl owner)
    {
        var appearance = owner.Selected?.Appearance ?? AnnotationAppearance.Default;
        var panel = new StackPanel { Spacing = 6 };

        panel.Children.Add(AnnotationEditorControls.Heading("外観"));
        panel.Children.Add(AnnotationEditorControls.ColorRow(
            "枠線の色",
            appearance.Border,
            color => owner.UpdateSelectedAppearance(a => a with { Border = color })));
        panel.Children.Add(AnnotationEditorControls.NumberRow(
            "枠線の太さ",
            appearance.BorderThickness,
            AnnotationLimits.BorderThicknessMin,
            AnnotationLimits.BorderThicknessMax,
            value => owner.UpdateSelectedAppearance(a => a with { BorderThickness = value })));
        panel.Children.Add(AnnotationEditorControls.ColorRow(
            "内側の色",
            appearance.Fill,
            color => owner.UpdateSelectedAppearance(a => a with { Fill = color })));
        panel.Children.Add(AnnotationEditorControls.NumberRow(
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
        var panel = new StackPanel { Spacing = 6 };

        panel.Children.Add(AnnotationEditorControls.Heading("吹き出し"));
        panel.Children.Add(AnnotationEditorControls.NumberRow(
            "本文の余白",
            bubble.BodyPadding,
            0,
            AnnotationLimits.BodyPaddingMax,
            value => owner.SetSelectedBodyPadding(value)));
        panel.Children.Add(AnnotationEditorControls.NumberRow(
            "しっぽの幅",
            bubble.Tail.Width,
            AnnotationLimits.TailWidthMin,
            AnnotationLimits.TailWidthMax,
            value => owner.UpdateSelectedTail(tail => tail with { Width = value })));
        panel.Children.Add(AnnotationEditorControls.ChoiceRow(
            "しっぽの辺",
            ["上", "右", "下", "左"],
            (int)bubble.Tail.BaseEdge,
            index => owner.UpdateSelectedTail(tail => tail with { BaseEdge = (BubbleEdge)index })));

        return panel;
    }

    /// <summary>本文の書式（フォント・大きさ・色・装飾）の編集を作る。</summary>
    /// <param name="owner">編集対象の注釈を持つコントロール。</param>
    /// <returns>書式の編集。</returns>
    /// <remarks>
    /// <b>適用先を画面に書く</b>: 本文を編集中なら選択範囲（選択が無ければ全体）、編集していなければ全体である。
    /// 範囲の指定方法が分かりにくいという指摘を受けて、フライアウトの中に適用先を明記する。
    /// </remarks>
    private static Control BuildTextGroup(AnnotationLayerControl owner)
    {
        var style = owner.SelectedTextStyle;
        var panel = new StackPanel { Spacing = 6 };
        var (start, length) = owner.TextSelectionRange;

        panel.Children.Add(AnnotationEditorControls.Heading("本文の書式"));
        panel.Children.Add(new TextBlock
        {
            Text = length > 0
                ? $"適用先: {start + 1}〜{start + length} 文字目（選択範囲）"
                : "適用先: 本文全体（本文を編集中に文字を選ぶと、その範囲だけに適用できます）",
            FontSize = 11,
            Opacity = 0.75,
            TextWrapping = TextWrapping.Wrap,
        });
        panel.Children.Add(AnnotationEditorControls.ChoiceRow(
            "フォント",
            [.. AnnotationFonts.All],
            Math.Max(0, AnnotationFonts.All.ToList().IndexOf(style.FontFamily)),
            index => owner.TransformTextStyle(current => current with { FontFamily = AnnotationFonts.All[index] })));
        panel.Children.Add(AnnotationEditorControls.NumberRow(
            "大きさ",
            style.FontSize,
            AnnotationLimits.FontSizeMin,
            AnnotationLimits.FontSizeMax,
            value => owner.TransformTextStyle(current => current with { FontSize = value })));
        panel.Children.Add(AnnotationEditorControls.ColorRow(
            "文字色",
            style.Color,
            color => owner.TransformTextStyle(current => current with { Color = color })));
        panel.Children.Add(BuildDecorations(owner, style));

        return panel;
    }

    /// <summary>太字・斜体・下線・取り消し線の切り替えを作る。</summary>
    /// <param name="owner">編集対象の注釈を持つコントロール。</param>
    /// <param name="style">現在の書式。</param>
    /// <returns>切り替えの行。</returns>
    private static Control BuildDecorations(AnnotationLayerControl owner, AnnotationTextStyle style)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        row.Children.Add(AnnotationEditorControls.Label("装飾"));

        row.Children.Add(AnnotationEditorControls.Toggle(
            "B", style.IsBold,
            () => ToggleDecoration(owner, style, s => s.IsBold, (s, value) => s with { IsBold = value }),
            bold: true));
        row.Children.Add(AnnotationEditorControls.Toggle(
            "I", style.IsItalic,
            () => ToggleDecoration(owner, style, s => s.IsItalic, (s, value) => s with { IsItalic = value }),
            italic: true));
        row.Children.Add(AnnotationEditorControls.Toggle(
            "U", style.IsUnderline,
            () => ToggleDecoration(owner, style, s => s.IsUnderline, (s, value) => s with { IsUnderline = value }),
            underline: true));
        row.Children.Add(AnnotationEditorControls.Toggle(
            "S", style.IsStrike,
            () => ToggleDecoration(owner, style, s => s.IsStrike, (s, value) => s with { IsStrike = value }),
            strike: true));

        return row;
    }

    /// <summary>装飾（B / I / U / S）を切り替える。</summary>
    /// <param name="owner">編集対象の注釈を持つコントロール。</param>
    /// <param name="current">現在の書式（切り替えの判断はこの値で 1 回だけ行う）。</param>
    /// <param name="read">属性を読む関数。</param>
    /// <param name="write">属性を書く関数。</param>
    /// <remarks>
    /// <b>範囲の全 run へ同じ値を書く</b>: run ごとに反転すると、太字と通常が混ざった範囲で入れ替わってしまう。
    /// 他の属性（色・大きさなど）は <c>TransformTextStyle</c> が保つ。
    /// </remarks>
    private static void ToggleDecoration(
        AnnotationLayerControl owner,
        AnnotationTextStyle current,
        Func<AnnotationTextStyle, bool> read,
        Func<AnnotationTextStyle, bool, AnnotationTextStyle> write)
        => owner.TransformTextStyle(style => write(style, !read(current)));

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
}
