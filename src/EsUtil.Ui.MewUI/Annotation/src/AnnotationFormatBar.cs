using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using EsUtil.Feature.Annotation;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Platform.Core.Palette;
using EsUtil.Ui.MewUI.ColorSelector;

namespace EsUtil.Ui.MewUI.Annotation;

/// <summary>本文を編集中に本文の上へ出す書式バー（文字単位の書式）。</summary>
/// <remarks>
/// <b>本文はコントロールが直接編集する</b>ため、このバーは「選択中の範囲へ書式を適用する」だけを行う。
/// 適用しても編集は続き、結果はその場で本文へ反映される。<br/>
/// <b>項目名は出さない</b>: 幅が限られるため、意味は現在値（フォント名・大きさ）と装飾ボタンの文言で示す。<br/>
/// <b>同じ値でも適用できる</b>: 文字単位で書式が混ざっている範囲を一律に揃えるため、選び直しただけでも適用する。
/// </remarks>
public sealed class AnnotationFormatBar : ContentControl
{
    /// <summary>バーのおおよその高さ（DIP。本文の上へ置く位置の計算に使う）。</summary>
    internal const double EstimatedHeight = 40;

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

    /// <summary>書式バーを初期化する。</summary>
    /// <param name="layer">編集対象のコントロール。</param>
    public AnnotationFormatBar(AnnotationLayerControl layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        _layer = layer;

        _font = new ComboBox().Items([.. AnnotationFonts.All]);
        _font.Width = FontSelectorWidth;
        _font.SelectedIndex = 0;
        _font.OnSelectionChanged(_ => ApplyFont());

        _size = new NumericUpDown().Minimum(6).Maximum(512).Step(1);
        _size.Width = SizeSelectorWidth;
        _size.Value = AnnotationDefaults.FontSizeDefault;
        _size.OnValueChanged(_ => ApplySize());

        _color = new ColorSelectorControl
        {
            Width = ColorSelectorWidth,
            HighlightBorderOnError = true,
        };
        AnnotationPalette.Configure(_color, showPicker: true);
        _color.SelectedColorChanged += (_, _) => ApplyColor();

        _bold = CreateToggle("B", bold: true);
        _italic = CreateToggle("I", italic: true);
        _underline = CreateToggle("U");
        _strike = CreateToggle("S");

        _bold.OnCheckedChanged(_ => ToggleDecoration(
            style => style.IsBold, static (style, value) => style with { IsBold = value }));
        _italic.OnCheckedChanged(_ => ToggleDecoration(
            style => style.IsItalic, static (style, value) => style with { IsItalic = value }));
        _underline.OnCheckedChanged(_ => ToggleDecoration(
            style => style.IsUnderline, static (style, value) => style with { IsUnderline = value }));
        _strike.OnCheckedChanged(_ => ToggleDecoration(
            style => style.IsStrike, static (style, value) => style with { IsStrike = value }));

        var controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = ControlSpacing };
        controls.Add(_font);
        controls.Add(_size);
        controls.Add(_color);
        controls.Add(_bold);
        controls.Add(_italic);
        controls.Add(_underline);
        controls.Add(_strike);

        // 枠・余白・下地はコアの外観（AnnotationPanelAppearance）が単一所有する。
        // ここで持つと、パネルの枠と二重になって「余計な辺枠」に見える。
        Content = controls;
    }

    /// <summary>フォントの選択の幅（DIP）。</summary>
    internal const double FontSelectorWidth = 210;

    /// <summary>大きさの入力の幅（DIP）。</summary>
    internal const double SizeSelectorWidth = 104;

    /// <summary>色の選択の幅（DIP）。</summary>
    /// <summary>色の選択の幅（DIP。 AvaloniaUI 版と同じ計算で求める）。</summary>
    internal static readonly double ColorSelectorWidth = ResolveColorSelectorWidth();

    /// <summary>色の選択の幅を求める（「■ + 全角 4 文字 + 100%」と「■ + #FFFFFFFF」の広いほう）。</summary>
    /// <returns>幅（DIP）。</returns>
    private static double ResolveColorSelectorWidth()
    {
        var style = AnnotationDefaults.TextStyle with { FontSize = 12 };
        var nameWidth = AnnotationTextMetrics.Measure("薄い灰色+100%", style).Width;
        var hexWidth = AnnotationTextMetrics.Measure("#FFFFFFFF", style).Width;
        return Math.Ceiling(24 + Math.Max(nameWidth, hexWidth) + 34);
    }

    /// <summary>操作の間の余白（DIP）。</summary>
    private const double ControlSpacing = 4;

    /// <summary>色の入力欄へ出す表記を取得または設定する。</summary>
    public ColorTextFormat TextFormat
    {
        get => _color.TextFormat;
        set => _color.TextFormat = value;
    }

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
            _size.Value = style.FontSize;
            _color.SelectedColor = AnnotationEditorControls.ToHex(style.Color);

            // 装飾は「選択範囲のすべてが同じ属性か」で押下状態を決める（一部だけ太字の範囲で押されて見えないように）
            _bold.IsChecked = _layer.SelectedTextMatches(static textStyle => textStyle.IsBold);
            _italic.IsChecked = _layer.SelectedTextMatches(static textStyle => textStyle.IsItalic);
            _underline.IsChecked = _layer.SelectedTextMatches(static textStyle => textStyle.IsUnderline);
            _strike.IsChecked = _layer.SelectedTextMatches(static textStyle => textStyle.IsStrike);

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

        InvalidateMeasure();
    }

    /// <summary>装飾の切り替えボタンを作る。</summary>
    /// <param name="text">文言。</param>
    /// <param name="bold">太字で表示するかどうか。</param>
    /// <param name="italic">斜体で表示するかどうか。</param>
    /// <returns>切り替えボタン。</returns>
    private static ToggleButton CreateToggle(string text, bool bold = false, bool italic = false)
    {
        var content = new TextBlock
        {
            Text = text,
            FontWeight = bold ? FontWeight.Bold : FontWeight.Normal,
            FontStyle = italic ? FontStyle.Italic : FontStyle.Normal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        return new ToggleButton { Content = content, Width = 34, Padding = new Thickness(0) };
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
    }

    /// <summary>選択中の範囲へ大きさを適用する（同じ値でも適用する）。</summary>
    private void ApplySize()
    {
        if (_isRefreshing)
        {
            return;
        }

        var value = _size.Value;
        Apply(style => style with { FontSize = value });
    }

    /// <summary>選択中の範囲へ色を適用する（同じ色を選び直しても適用する）。</summary>
    private void ApplyColor()
    {
        if (_isRefreshing || !DrawingColorExtensions.TryParseHexText(_color.SelectedColor, out var color))
        {
            return;
        }

        Apply(style => style with { Color = color });
    }

    /// <summary>選択中の範囲へ書式を適用し、表示とフォーカスを戻す。</summary>
    /// <param name="change">書式を新しい値へ変える処理。</param>
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

    /// <summary>装飾を切り替える（適用先の先頭の書式で切り替えの向きを決める）。</summary>
    /// <param name="read">属性を読む関数。</param>
    /// <param name="write">属性を書く関数。</param>
    private void ToggleDecoration(
        Func<AnnotationTextStyle, bool> read,
        Func<AnnotationTextStyle, bool, AnnotationTextStyle> write)
    {
        if (_isRefreshing)
        {
            return;
        }

        Apply(style => write(style, !read(_layer.SelectedTextStyle)));
    }
}
