using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using EsUtil.Feature.Annotation;

namespace EsUtil.Ui.MewUI.Annotation;

/// <summary>注釈の外観・吹き出し・重なり順を編集するパネル（右クリック）。</summary>
/// <remarks>
/// <b>選択中の注釈だけを編集する</b>: 既存の注釈の外観・書式は注釈ごとに持つ（既定値を変えても既存は変わらない）。<br/>
/// <b>行の部品は <see cref="AnnotationEditorControls"/> を共有する</b>（ホストの設定ペインと同じ見た目・同じ色の解釈）。
/// </remarks>
public sealed class AnnotationStylePanel : ContentControl
{
    /// <summary>編集パネルを初期化する。</summary>
    /// <param name="layer">編集対象のコントロール。</param>
    /// <param name="close">閉じる処理。</param>
    public AnnotationStylePanel(AnnotationLayerControl layer, Action close)
    {
        ArgumentNullException.ThrowIfNull(layer);
        ArgumentNullException.ThrowIfNull(close);

        var content = new StackPanel { Spacing = 8, Width = 340 };
        content.Add(BuildAppearanceGroup(layer));

        if (layer.Selected is BubbleAnnotation)
        {
            content.Add(Separator());
            content.Add(BuildBubbleGroup(layer));
        }

        content.Add(Separator());
        content.Add(BuildOrderGroup(layer, close));

        Background = new Color(255, 250, 250, 250);
        BorderBrush = new Color(255, 160, 160, 160);
        BorderThickness = 1;
        CornerRadius = 4;
        Padding = new Thickness(8);
        Content = new ScrollViewer { Content = content, MaxHeight = 460 };
    }

    /// <summary>区切り線を作る。</summary>
    /// <returns>区切り線。</returns>
    private static Element Separator() => new Border
    {
        Height = 1,
        Background = new Color(64, 128, 128, 128),
    };

    /// <summary>文字のボタンを作る（MewUI のボタンは文字列を内容に持てない）。</summary>
    /// <param name="text">文言。</param>
    /// <returns>ボタン。</returns>
    private static Button CreateButton(string text) => new() { Content = new TextBlock { Text = text } };

    /// <summary>外観（枠線・内側・角丸）の編集を作る。</summary>
    /// <param name="layer">編集対象のコントロール。</param>
    /// <returns>外観の編集。</returns>
    private static Element BuildAppearanceGroup(AnnotationLayerControl layer)
    {
        var appearance = layer.Selected?.Appearance ?? AnnotationAppearance.Default;
        var panel = new StackPanel { Spacing = 6 };

        panel.Add(AnnotationEditorControls.Heading("外観"));
        panel.Add(AnnotationEditorControls.ColorRow(
            "枠線の色",
            appearance.Border,
            color => layer.UpdateSelectedAppearance(a => a with { Border = color }),
            layer.ColorTextFormat));
        panel.Add(AnnotationEditorControls.NumberRow(
            "枠線の太さ",
            appearance.BorderThickness,
            AnnotationLimits.BorderThicknessMin,
            AnnotationLimits.BorderThicknessMax,
            value => layer.UpdateSelectedAppearance(a => a with { BorderThickness = value })));
        panel.Add(AnnotationEditorControls.ColorRow(
            "内側の色",
            appearance.Fill,
            color => layer.UpdateSelectedAppearance(a => a with { Fill = color }),
            layer.ColorTextFormat));
        panel.Add(AnnotationEditorControls.NumberRow(
            "角丸",
            appearance.CornerRadius,
            0,
            AnnotationLimits.CornerRadiusMax,
            value => layer.UpdateSelectedAppearance(a => a with { CornerRadius = value })));

        return panel;
    }

    /// <summary>吹き出し（本文の余白・しっぽ）の編集を作る。</summary>
    /// <param name="layer">編集対象のコントロール。</param>
    /// <returns>吹き出しの編集。</returns>
    private static Element BuildBubbleGroup(AnnotationLayerControl layer)
    {
        var bubble = (BubbleAnnotation)layer.Selected!;
        var panel = new StackPanel { Spacing = 6 };

        panel.Add(AnnotationEditorControls.Heading("吹き出し"));
        panel.Add(AnnotationEditorControls.NumberRow(
            "本文の余白",
            bubble.BodyPadding,
            0,
            AnnotationLimits.BodyPaddingMax,
            value => layer.SetSelectedBodyPadding(value)));
        panel.Add(AnnotationEditorControls.NumberRow(
            "しっぽの幅",
            bubble.Tail.Width,
            AnnotationLimits.TailWidthMin,
            AnnotationLimits.TailWidthMax,
            value => layer.UpdateSelectedTail(tail => tail with { Width = value })));
        panel.Add(AnnotationEditorControls.ChoiceRow(
            "しっぽの辺",
            ["上", "右", "下", "左"],
            (int)bubble.Tail.BaseEdge,
            index => layer.UpdateSelectedTail(tail => tail with { BaseEdge = (BubbleEdge)index })));

        return panel;
    }

    /// <summary>重なり順の編集を作る。</summary>
    /// <param name="layer">編集対象のコントロール。</param>
    /// <param name="close">閉じる処理。</param>
    /// <returns>重なり順の編集。</returns>
    private static Element BuildOrderGroup(AnnotationLayerControl layer, Action close)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        var front = CreateButton("最前面へ");
        front.OnClick(() => layer.MoveSelectedToFront());
        var back = CreateButton("最背面へ");
        back.OnClick(() => layer.MoveSelectedToBack());
        var closeButton = CreateButton("閉じる");
        closeButton.OnClick(close);

        row.Add(front);
        row.Add(back);
        row.Add(closeButton);
        return row;
    }
}
