using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Aprillz.MewUI.Text;
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

        _size = new NumericUpDown().Minimum(AnnotationLimits.FontSizeMin).Maximum(AnnotationLimits.FontSizeMax).Step(1);
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

        // 高さはコア（AnnotationFormatBarLayout）が単一所有する。 項目ごとに高さが違うと縦位置が揃わない
        _font.Height = AnnotationFormatBarLayout.ItemHeight;
        _size.Height = AnnotationFormatBarLayout.ItemHeight;
        _color.Height = AnnotationFormatBarLayout.ItemHeight;

        // **中の入力欄も同じ高さにする**: 色の選択の中の ComboBox は自分の高さを持ち、外側の高さでは
        // 伸びない。 揃えないと色の入力欄だけが低く見える（実測 2026-11-22）
        AnnotationEditorControls.AlignInputHeight(_color, AnnotationFormatBarLayout.ItemHeight);

        _bold = CreateToggle("B", bold: true);
        _italic = CreateToggle("I", italic: true);
        _underline = CreateToggle("U", underline: true);
        _strike = CreateToggle("S", strike: true);

        _bold.OnCheckedChanged(_ => ToggleDecoration(
            style => style.IsBold, static (style, value) => style with { IsBold = value }));
        _italic.OnCheckedChanged(_ => ToggleDecoration(
            style => style.IsItalic, static (style, value) => style with { IsItalic = value }));
        _underline.OnCheckedChanged(_ => ToggleDecoration(
            style => style.IsUnderline, static (style, value) => style with { IsUnderline = value }));
        _strike.OnCheckedChanged(_ => ToggleDecoration(
            style => style.IsStrike, static (style, value) => style with { IsStrike = value }));

        var controls = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = ControlSpacing,

            // 高さの違う項目が混ざっても縦位置を揃える（規則の所有者はコアの ItemHeight）
            VerticalAlignment = VerticalAlignment.Center,
        };

        // 並び順はコア（AnnotationFormatBarLayout.Order）が単一所有する。
        // ここで順を決めると 4 つの UI 版で操作の位置がずれる（実測 2026-11-20）。
        var items = new Dictionary<AnnotationFormatBarItem, Element>
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
            controls.Add(element);
        }

        // 枠・余白・下地はコアの外観（AnnotationPanelAppearance）が単一所有する。
        // ここで持つと、パネルの枠と二重になって「余計な辺枠」に見える。
        Content = controls;
    }

    /// <summary>フォントの選択の幅（DIP。 値はコアが単一所有する）。</summary>
    internal const double FontSelectorWidth = AnnotationFormatBarLayout.FontSelectorWidth;

    /// <summary>大きさの入力の幅（DIP。 値はコアが単一所有する）。</summary>
    internal const double SizeSelectorWidth = AnnotationFormatBarLayout.SizeSelectorWidth;

    /// <summary>色の選択の幅（DIP。 規則はコアが単一所有する）。</summary>
    internal static readonly double ColorSelectorWidth =
        AnnotationFormatBarLayout.ResolveColorSelectorWidth(AnnotationTextMetrics.Measure);

    /// <summary>操作の間の余白（DIP）。</summary>
    private const double ControlSpacing = AnnotationEditorLayout.RowSpacing;

    /// <summary>装飾の切り替えボタンの幅（DIP）。</summary>
    private const double ToggleWidth = AnnotationEditorLayout.ToggleWidth;

    /// <summary>色の入力欄へ出す表記を取得または設定する。</summary>
    /// <value>常にカラーコード（<c>#AARRGGBB</c>）。</value>
    /// <remarks>
    /// <b>書式バーは常に 8 桁のカラーコードで出す</b>: 色名の表記では、名前の無い色が 6 桁（<c>#RRGGBB</c>）で
    /// 出て不透明度が分からず、色名の有無で桁数も変わる（実測 2026-11-22）。 色名は右クリックのフライアウトで確認できる。
    /// </remarks>
    public ColorTextFormat TextFormat
    {
        get => ColorTextFormat.HexCode;
        set => _color.TextFormat = ColorTextFormat.HexCode;
    }

    /// <summary>現在の書式を表示へ反映する（選択範囲の先頭の書式）。</summary>
    /// <remarks>
    /// <b>文字を選んでいなくても操作できる</b>: 選択が無いときに選んだ書式は、以降の入力へ効く
    /// （無効にすると「書式を変えられない」と見える。 実測 2026-11-22）。
    /// </remarks>
    public void Refresh()
    {
        var style = _layer.SelectedTextStyle;

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
    /// <param name="underline">下線を引いて表示するかどうか。</param>
    /// <param name="strike">取り消し線を引いて表示するかどうか。</param>
    /// <returns>切り替えボタン。</returns>
    private static ToggleButton CreateToggle(
        string text, bool bold = false, bool italic = false, bool underline = false, bool strike = false)
    {
        const double height = AnnotationFormatBarLayout.ItemHeight;

        // **記号自体にも装飾を描く**: 「U」「S」の文字だけでは意味が伝わらない（実測 2026-11-21）。
        // 線は文字へ持たせる（MewUI の TextBlock は文字単位の装飾を Run で持てる）。 図形を重ねると
        // 文字との位置がずれ、線だけが別の場所に見える（実測 2026-11-21）。
        var label = new TextBlock
        {
            Width = ToggleWidth,
            Height = height,

            // **記号は行の文字より一回り大きくする**（値はコアの AnnotationEditorLayout が単一所有する）
            FontSize = AnnotationEditorLayout.ToggleFontSize,
            FontFamily = AnnotationEditorLayout.ToggleFontFamily,
            TextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
        };

        label.Inlines.Add(new Run
        {
            Text = text,
            FontWeight = bold ? FontWeight.Bold : FontWeight.Normal,
            Italic = italic,
            Decoration = strike
                ? TextDecoration.Strikethrough
                : underline ? TextDecoration.Underline : TextDecoration.None,
        });

        return new ToggleButton
        {
            Content = label,
            Width = ToggleWidth,
            Height = height,
            Padding = new Thickness(0),
        };
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
    /// <remarks>
    /// <b>選択が無いときは以降の入力へ回す</b>（判定はレイヤーが単一所有するので、ここでは分けない）。
    /// </remarks>
    private void Apply(Func<AnnotationTextStyle, AnnotationTextStyle> change)
    {
        _layer.TransformTextStyle(change);
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
