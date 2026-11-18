using EsUtil.Feature.Annotation;
using EsUtil.Platform.Core.Palette;

namespace EsUtil.Ui.WinForms.Annotation;

/// <summary>本文の書式（文字単位）を操作するバー（WinForms 版）。</summary>
/// <remarks>
/// <b>適用先は選択範囲</b>: 本文を編集中は入力欄の選択範囲、編集していないときは本文全体へ適用する。<br/>
/// <b>色は代表色のメニューから選ぶ</b>（任意の色は色ダイアログ。 WinForms には色選択コントロールが無い）。
/// </remarks>
internal sealed class AnnotationFormatBar : Panel
{
    /// <summary>適用先のレイヤー。</summary>
    private readonly AnnotationLayerControl _layer;

    /// <summary>太字の切り替え。</summary>
    private readonly CheckBox _bold;

    /// <summary>斜体の切り替え。</summary>
    private readonly CheckBox _italic;

    /// <summary>下線の切り替え。</summary>
    private readonly CheckBox _underline;

    /// <summary>取り消し線の切り替え。</summary>
    private readonly CheckBox _strike;

    /// <summary>フォントの選択。</summary>
    private readonly ComboBox _font;

    /// <summary>文字サイズの入力。</summary>
    private readonly NumericUpDown _size;

    /// <summary>色のボタン。</summary>
    private readonly Button _color;

    /// <summary>通知による更新中かどうか（利用者の操作と区別する）。</summary>
    private bool _refreshing;

    /// <summary>書式バーを初期化する。</summary>
    /// <param name="layer">適用先のレイヤー。</param>
    internal AnnotationFormatBar(AnnotationLayerControl layer)
    {
        _layer = layer;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = Color.FromArgb(250, 250, 250);
        Padding = new Padding(4);

        _bold = AnnotationEditorControls.Toggle("B", false, value => Toggle(style => style with { IsBold = value }), bold: true);
        _italic = AnnotationEditorControls.Toggle("I", false, value => Toggle(style => style with { IsItalic = value }), italic: true);
        _underline = AnnotationEditorControls.Toggle("U", false, value => Toggle(style => style with { IsUnderline = value }));
        _strike = AnnotationEditorControls.Toggle("S", false, value => Toggle(style => style with { IsStrike = value }));

        _font = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 128,
            Font = new Font("Yu Gothic UI", AnnotationEditorControls.FontSize),
            Margin = new Padding(1),
        };

        foreach (var family in AnnotationFonts.All)
        {
            _font.Items.Add(family);
        }

        _font.SelectedIndex = 0;
        _font.SelectedIndexChanged += (_, _) =>
        {
            if (!_refreshing && _font.SelectedItem is string family)
            {
                ApplyFull(style => style with { FontFamily = family });
            }
        };

        _size = AnnotationEditorControls.NumberBox(
            AnnotationDefaults.FontSizeDefault, 6, 512,
            value => ApplyFull(style => style with { FontSize = value }));
        _color = AnnotationEditorControls.ColorButton(
            AnnotationDefaults.TextStyle.Color, color => ApplyFull(style => style with { Color = color }));

        Controls.Add(_bold);
        Controls.Add(_italic);
        Controls.Add(_underline);
        Controls.Add(_strike);
        Controls.Add(_font);
        Controls.Add(_size);
        Controls.Add(_color);
        LayoutRow();

        RefreshState();
    }

    /// <summary>入力欄の表記を取得または設定する（色のメニューは表記を持たないため保持だけする）。</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal ColorTextFormat TextFormat { get; set; } = ColorTextFormat.NameWithAlpha;

    /// <summary>現在の選択範囲の書式を各操作へ反映する。</summary>
    internal void RefreshState()
    {
        _refreshing = true;
        try
        {
            var style = _layer.SelectedTextStyle;
            _bold.Checked = _layer.SelectedTextMatches(static candidate => candidate.IsBold);
            _italic.Checked = _layer.SelectedTextMatches(static candidate => candidate.IsItalic);
            _underline.Checked = _layer.SelectedTextMatches(static candidate => candidate.IsUnderline);
            _strike.Checked = _layer.SelectedTextMatches(static candidate => candidate.IsStrike);

            var index = AnnotationFonts.All.ToList().IndexOf(style.FontFamily);
            _font.SelectedIndex = index >= 0 ? index : 0;
            _size.Value = (decimal)Math.Clamp(style.FontSize, 6, 512);
            _color.BackColor = style.Color;
        }
        finally
        {
            _refreshing = false;
        }
    }

    /// <summary>装飾を切り替える。</summary>
    /// <param name="change">書式を変える処理。</param>
    private void Toggle(Func<AnnotationTextStyle, AnnotationTextStyle> change)
    {
        if (_refreshing)
        {
            return;
        }

        _layer.TransformSelectionStyle(change);
        RefreshState();
    }

    /// <summary>書式を丸ごと適用する（色・大きさ・フォント）。</summary>
    /// <param name="change">書式を変える処理。</param>
    private void ApplyFull(Func<AnnotationTextStyle, AnnotationTextStyle> change)
    {
        if (_refreshing)
        {
            return;
        }

        _layer.ApplyFullStyle(change(_layer.SelectedTextStyle));
        RefreshState();
    }

    /// <summary>子要素を左から順に並べる。</summary>
    private void LayoutRow()
    {
        var x = Padding.Left;
        foreach (Control control in Controls)
        {
            control.Location = new Point(x, Padding.Top);
            x += control.Width + control.Margin.Horizontal;
        }
    }
}
