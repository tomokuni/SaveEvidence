using EsUtil.Feature.Annotation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.WinUI3.Annotation;

/// <summary>注釈の外観（枠線・内側・角丸・しっぽ・重なり順）を編集するパネル（WinUI 3 版）。</summary>
/// <remarks>
/// <b>選択中の注釈だけに効く</b>（設定画面の既定値は新しく追加する注釈へ効く）。<br/>
/// <b>本文の書式は書式バーが担う</b>（このパネルは外観と重なり順だけを扱う）。
/// </remarks>
internal sealed class AnnotationStylePanel : ContentControl
{
    /// <summary>しっぽの向きの選択肢（毎回作らない）。</summary>
    private static readonly string[] s_edgeOptions = ["下", "上", "左", "右"];

    /// <summary>対象のレイヤー。</summary>
    private readonly AnnotationLayerControl _layer;

    /// <summary>外観パネルを初期化する。</summary>
    /// <param name="layer">対象のレイヤー。</param>
    internal AnnotationStylePanel(AnnotationLayerControl layer)
    {
        _layer = layer;

        // 枠・余白・幅はコアの外観（AnnotationPanelAppearance）が単一所有する。
        // ここで持つと、ウィンドウの枠と二重になって「余計な辺枠」に見える。
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Top;

        Content = Build();
    }

    /// <summary>パネルの中身を組み立てる。</summary>
    /// <returns>縦に積んだ要素。</returns>
    private FrameworkElement Build()
    {
        var panel = new StackPanel { Spacing = 2 };

        panel.Children.Add(AnnotationEditorControls.Heading("外観"));
        panel.Children.Add(AnnotationEditorControls.Divider());

        if (_layer.Selected is not { } item)
        {
            panel.Children.Add(AnnotationEditorControls.Label("注釈を選んでください。"));
            return panel;
        }

        var appearance = item.Appearance;
        panel.Children.Add(AnnotationEditorControls.ColorRow(
            "枠線の色", AnnotationEditorControls.ToHex(appearance.Border), _layer.ColorTextFormat,
            value => _layer.UpdateSelectedAppearance(current => current with { Border = ParseColor(value, current.Border) })));
        panel.Children.Add(AnnotationEditorControls.NumberRow(
            "枠線の太さ", appearance.BorderThickness, 0.5, 64,
            value => _layer.UpdateSelectedAppearance(current => current with { BorderThickness = value })));
        panel.Children.Add(AnnotationEditorControls.ColorRow(
            "内側の色", AnnotationEditorControls.ToHex(appearance.Fill), _layer.ColorTextFormat,
            value => _layer.UpdateSelectedAppearance(current => current with { Fill = ParseColor(value, current.Fill) })));
        panel.Children.Add(AnnotationEditorControls.NumberRow(
            "角丸", appearance.CornerRadius, 0, 4096,
            value => _layer.UpdateSelectedAppearance(current => current with { CornerRadius = value })));

        if (item is BubbleAnnotation bubble)
        {
            panel.Children.Add(AnnotationEditorControls.Divider());
            panel.Children.Add(AnnotationEditorControls.NumberRow(
                "本文の余白", bubble.BodyPadding, 0, 4096, value => _layer.SetSelectedBodyPadding(value)));
            panel.Children.Add(AnnotationEditorControls.NumberRow(
                "しっぽの幅", bubble.Tail.Width, 1, 4096,
                value => _layer.UpdateSelectedTail(tail => tail with { Width = value })));
            panel.Children.Add(AnnotationEditorControls.ChoiceRow(
                "しっぽの向き",
                s_edgeOptions,
                (int)bubble.Tail.BaseEdge,
                value => _layer.UpdateSelectedTail(tail => tail with { BaseEdge = (BubbleEdge)value })));
        }

        panel.Children.Add(AnnotationEditorControls.Divider());
        var order = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        var front = new Button { Content = "最前面へ", FontSize = AnnotationEditorControls.FontSize };
        front.Click += (_, _) => _layer.MoveSelectedToFront();
        var back = new Button { Content = "最背面へ", FontSize = AnnotationEditorControls.FontSize };
        back.Click += (_, _) => _layer.MoveSelectedToBack();
        order.Children.Add(front);
        order.Children.Add(back);
        panel.Children.Add(order);

        return panel;
    }

    /// <summary>保存値（<c>#AARRGGBB</c>）を色へ変換する。</summary>
    /// <param name="text">保存値。</param>
    /// <param name="fallback">解釈できない場合の色。</param>
    /// <returns>色。</returns>
    private static DrawingColor ParseColor(string? text, DrawingColor fallback)
    {
        if (text is { Length: 9 } && text[0] == '#'
            && uint.TryParse(text[1..], System.Globalization.NumberStyles.HexNumber, null, out var value))
        {
            return DrawingColor.FromArgb(
                (byte)((value >> 24) & 0xFF),
                (byte)((value >> 16) & 0xFF),
                (byte)((value >> 8) & 0xFF),
                (byte)(value & 0xFF));
        }

        return fallback;
    }
}
