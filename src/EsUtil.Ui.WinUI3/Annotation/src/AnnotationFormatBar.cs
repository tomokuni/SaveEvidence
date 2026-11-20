using EsUtil.Feature.Annotation;
using EsUtil.Platform.Core.Palette;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.WinUI3.Annotation;

/// <summary>本文の書式（文字単位）を操作するバー（WinUI 3 版）。</summary>
/// <remarks>
/// <b>適用先は選択範囲</b>: 本文を編集中は入力欄の選択範囲、編集していないときは本文全体へ適用する
/// （適用先が決まらない操作は無効にする）。<br/>
/// <b>色は色選択コントロールを使う</b>: 代表色パレット・代表色・カラーピッカーから選ぶ（自前の色入力を作らない）。
/// </remarks>
internal sealed class AnnotationFormatBar : ContentControl
{
    /// <summary>適用先のレイヤー。</summary>
    private readonly AnnotationLayerControl _layer;

    /// <summary>太字の切り替え。</summary>
    private readonly ToggleButton _bold;

    /// <summary>斜体の切り替え。</summary>
    private readonly ToggleButton _italic;

    /// <summary>下線の切り替え。</summary>
    private readonly ToggleButton _underline;

    /// <summary>取り消し線の切り替え。</summary>
    private readonly ToggleButton _strike;

    /// <summary>フォントの選択。</summary>
    private readonly ComboBox _font;

    /// <summary>文字サイズの入力。</summary>
    private readonly NumberBox _size;

    /// <summary>色の入力。</summary>
    private readonly EsUtil.Ui.WinUI3.ColorSelector.ColorSelectorControl _color;

    /// <summary>通知による更新中かどうか（利用者の操作と区別する）。</summary>
    private bool _refreshing;

    /// <summary>書式バーを初期化する。</summary>
    /// <param name="layer">適用先のレイヤー。</param>
    internal AnnotationFormatBar(AnnotationLayerControl layer)
    {
        _layer = layer;

        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;

        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        _bold = AnnotationEditorControls.Toggle("B", false, value => ApplyDecoration(style => style with { IsBold = value }), bold: true);
        _italic = AnnotationEditorControls.Toggle("I", false, value => ApplyDecoration(style => style with { IsItalic = value }), italic: true);
        _underline = AnnotationEditorControls.Toggle("U", false, value => ApplyDecoration(style => style with { IsUnderline = value }));
        _strike = AnnotationEditorControls.Toggle("S", false, value => ApplyDecoration(style => style with { IsStrike = value }));

        _font = new ComboBox
        {
            ItemsSource = AnnotationFonts.All,
            SelectedIndex = 0,
            Width = 130,
            FontSize = AnnotationEditorControls.FontSize,
        };

        _font.SelectionChanged += (_, _) =>
        {
            if (_refreshing || _font.SelectedItem is not string family)
            {
                return;
            }

            ApplyFull(style => style with { FontFamily = family });
        };

        _size = new NumberBox
        {
            Minimum = 6,
            Maximum = 512,
            SmallChange = 1,
            Value = AnnotationDefaults.FontSizeDefault,
            Width = 90,
            FontSize = AnnotationEditorControls.FontSize,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
        };

        _size.ValueChanged += (_, e) =>
        {
            if (_refreshing || double.IsNaN(e.NewValue))
            {
                return;
            }

            ApplyFull(style => style with { FontSize = e.NewValue });
        };

        _color = AnnotationEditorControls.CreateSelector(
            AnnotationEditorControls.ToHex(AnnotationDefaults.TextStyle.Color), ColorTextFormat.NameWithAlpha);
        _color.SelectedColorChanged += (_, _) =>
        {
            if (_refreshing)
            {
                return;
            }

            var color = ParseColor(_color.SelectedColor);
            ApplyFull(style => style with { Color = color });
        };

        panel.Children.Add(_bold);
        panel.Children.Add(_italic);
        panel.Children.Add(_underline);
        panel.Children.Add(_strike);
        panel.Children.Add(new Border { Width = 1, Background = new SolidColorBrush(Windows.UI.Color.FromArgb(60, 0, 0, 0)) });
        panel.Children.Add(_font);
        panel.Children.Add(_size);
        panel.Children.Add(_color);

        // 枠・余白・下地はコアの外観（AnnotationPanelAppearance）が単一所有する。
        // ここで持つと、パネルの枠と二重になって「余計な辺枠」に見える。
        Content = panel;

        Refresh();
    }

    /// <summary>書式バーの色の入力欄へ出す表記を取得または設定する。</summary>
    internal ColorTextFormat TextFormat
    {
        get => _color.TextFormat;
        set => _color.TextFormat = value;
    }

    /// <summary>現在の選択範囲の書式を各操作へ反映する。</summary>
    internal void Refresh()
    {
        _refreshing = true;
        try
        {
            var style = _layer.SelectedTextStyle;
            _bold.IsChecked = _layer.SelectedTextMatches(static candidate => candidate.IsBold);
            _italic.IsChecked = _layer.SelectedTextMatches(static candidate => candidate.IsItalic);
            _underline.IsChecked = _layer.SelectedTextMatches(static candidate => candidate.IsUnderline);
            _strike.IsChecked = _layer.SelectedTextMatches(static candidate => candidate.IsStrike);

            var index = AnnotationFonts.All.ToList().IndexOf(style.FontFamily);
            _font.SelectedIndex = index >= 0 ? index : 0;
            _size.Value = style.FontSize;
            _color.SelectedColor = AnnotationEditorControls.ToHex(style.Color);
        }
        finally
        {
            _refreshing = false;
        }
    }

    /// <summary>装飾を切り替える（選択範囲の本文へ適用する）。</summary>
    /// <param name="applyToModel">本文へ適用する処理。</param>
    private void ApplyDecoration(Func<AnnotationTextStyle, AnnotationTextStyle> applyToModel)
    {
        _layer.TransformSelectionStyle(applyToModel);
        Refresh();
    }

    /// <summary>書式を丸ごと適用する（色・大きさ・フォント）。</summary>
    /// <param name="change">書式を変える処理。</param>
    private void ApplyFull(Func<AnnotationTextStyle, AnnotationTextStyle> change)
    {
        _layer.ApplyFullStyle(change(_layer.SelectedTextStyle));
        Refresh();
    }

    /// <summary>保存値（<c>#AARRGGBB</c>）を色へ変換する。</summary>
    /// <param name="text">保存値。</param>
    /// <returns>色。 解釈できない場合は既定の文字色。</returns>
    private static DrawingColor ParseColor(string? text)
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

        return AnnotationDefaults.TextStyle.Color;
    }
}
