using EsUtil.Platform.Core.Imaging;
using EsUtil.Platform.Core.Palette;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.WinUI3.Annotation;

/// <summary>本文の書式（文字単位）を操作するバー（WinUI 3 版）。</summary>
/// <remarks>
/// <b>適用先は選択範囲</b>: 選んだ書式は選択範囲へ適用し、選択が無いときは以降の入力へ回す
/// （既存の文字は変えない。 判定はレイヤーが単一所有する）。<br/>
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

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,

            // 高さの違う項目が混ざっても縦位置を揃える（規則の所有者はコアの ItemHeight）
            VerticalAlignment = VerticalAlignment.Center,
        };
        _bold = AnnotationEditorControls.Toggle("B", false, value => ApplyDecoration(style => style with { IsBold = value }), bold: true);
        _italic = AnnotationEditorControls.Toggle("I", false, value => ApplyDecoration(style => style with { IsItalic = value }), italic: true);
        _underline = AnnotationEditorControls.Toggle(
            "U", false, value => ApplyDecoration(style => style with { IsUnderline = value }), underline: true);
        _strike = AnnotationEditorControls.Toggle(
            "S", false, value => ApplyDecoration(style => style with { IsStrike = value }), strike: true);

        _font = new ComboBox
        {
            ItemsSource = AnnotationFonts.All,
            SelectedIndex = 0,
            Width = AnnotationFormatBarLayout.FontSelectorWidth,

            // **高さを明示する**: 既定の高さでは文字の上が欠ける（実測 2026-11-20）
            MinHeight = AnnotationFormatBarLayout.ItemHeight,
            Height = AnnotationFormatBarLayout.ItemHeight,
            FontSize = AnnotationEditorControls.FontSize,
            VerticalContentAlignment = VerticalAlignment.Center,
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
            Minimum = AnnotationLimits.FontSizeMin,
            Maximum = AnnotationLimits.FontSizeMax,
            SmallChange = 1,
            Value = AnnotationDefaults.FontSizeDefault,
            Width = AnnotationFormatBarLayout.SizeSelectorWidth,
            MinHeight = AnnotationFormatBarLayout.ItemHeight,
            Height = AnnotationFormatBarLayout.ItemHeight,
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

        // 並び順はコア（AnnotationFormatBarLayout.Order）が単一所有する。
        // ここで順を決めると 4 つの UI 版で操作の位置がずれる（実測 2026-11-20）。
        var items = new Dictionary<AnnotationFormatBarItem, UIElement>
        {
            [AnnotationFormatBarItem.Bold] = _bold,
            [AnnotationFormatBarItem.Italic] = _italic,
            [AnnotationFormatBarItem.Underline] = _underline,
            [AnnotationFormatBarItem.Strike] = _strike,
            [AnnotationFormatBarItem.Font] = _font,
            [AnnotationFormatBarItem.Size] = _size,
            [AnnotationFormatBarItem.Color] = _color,
        };

        foreach (var item in AnnotationFormatBarLayout.Order)
        {
            // **高さは項目ごとに違ってはいけない**: 揃えないと縦位置がずれ、押す位置も変わる
            // （実測 2026-11-21）。 値はコアの AnnotationFormatBarLayout.ItemHeight が単一所有する
            var element = (FrameworkElement)items[item];
            element.Height = AnnotationFormatBarLayout.ItemHeight;
            element.MinHeight = AnnotationFormatBarLayout.ItemHeight;
            element.VerticalAlignment = VerticalAlignment.Center;
            panel.Children.Add(element);
        }

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
    /// <remarks>文字列の解釈は <see cref="DrawingColorExtensions.TryParseHexText"/> が単一所有する（自前で桁数を数えない）。</remarks>
    private static DrawingColor ParseColor(string? text)
        => DrawingColorExtensions.TryParseHexText(text, out var color) ? color : AnnotationDefaults.TextStyle.Color;
}
