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
    /// <summary>外観フライアウトの中身を組み立てる。</summary>
    /// <param name="owner">編集対象の注釈を持つコントロール。</param>
    /// <returns>表示用の内容（パネルはホストが置く）。</returns>
    /// <remarks>
    /// <b>位置と移動はホストが担う</b>: 表示位置（右クリックした位置）とドラッグでの移動は
    /// <c>AnnotationTextEditor</c> が持つ。 本クラスは中身だけを返す。
    /// </remarks>
    internal static Control BuildContent(AnnotationLayerControl owner)
    {
        var content = new StackPanel { Spacing = 8, Width = 340 };
        content.Children.Add(BuildAppearanceGroup(owner));

        if (owner.Selected is BubbleAnnotation)
        {
            content.Children.Add(Separator());
            content.Children.Add(BuildBubbleGroup(owner));
        }

        content.Children.Add(Separator());
        content.Children.Add(BuildOrderGroup(owner));

        // 画面の高さに収める（はみ出すと操作できない項目が出る）
        return new ScrollViewer { Content = content, MaxHeight = 460 };
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
            color => owner.UpdateSelectedAppearance(a => a with { Border = color }),
            owner.ColorTextFormat));
        panel.Children.Add(AnnotationEditorControls.NumberRow(
            "枠線の太さ",
            appearance.BorderThickness,
            AnnotationLimits.BorderThicknessMin,
            AnnotationLimits.BorderThicknessMax,
            value => owner.UpdateSelectedAppearance(a => a with { BorderThickness = value })));
        panel.Children.Add(AnnotationEditorControls.ColorRow(
            "内側の色",
            appearance.Fill,
            color => owner.UpdateSelectedAppearance(a => a with { Fill = color }),
            owner.ColorTextFormat));
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
