using System.Globalization;
using EsUtil.SaveEvidence.App.WinForms.Icons;

namespace EsUtil.SaveEvidence.App.WinForms.Views;

/// <summary>ページ共通のコントロール生成（配色・余白・ツールヒントの適用を 1 箇所に集約する）。</summary>
/// <remarks>
/// 同じ見た目のコントロールをページごとに組むと、配色と余白が食い違う。 生成と配色の適用を<br/>
/// 本クラスへ集約し、ページは「何を置くか」だけを書く。<br/>
/// <b>ツールヒント</b>: ツールバーのボタンはアイコンのみで意味が読み取れないため、必ず文言を付ける。<br/>
/// ツールヒントはプロセスで 1 つの部品を共用する（コントロールごとに作ると資源が増える）。
/// </remarks>
internal static class UiFactory
{
    /// <summary>ツールバーのコントロールの高さ（DIP）。</summary>
    public const int ToolbarHeight = 30;

    /// <summary>ステータスバーの高さ（DIP）。</summary>
    public const int StatusBarHeight = 26;

    /// <summary>プロセスで共用するツールヒントの部品。</summary>
    private static readonly ToolTip s_toolTip = new()
    {
        AutoPopDelay = 8000,
        InitialDelay = 300,
        ReshowDelay = 100,
        ShowAlways = true,
    };

    /// <summary>コントロールへツールヒントを設定する。</summary>
    /// <param name="control">対象のコントロール。</param>
    /// <param name="text">表示する文言。</param>
    public static void AttachToolTip(Control control, string text)
    {
        ArgumentNullException.ThrowIfNull(control);
        s_toolTip.SetToolTip(control, text);
    }

    /// <summary>ツールバー用のボタンを生成する。</summary>
    /// <param name="icon">表示するアイコン。</param>
    /// <param name="toolTip">ツールヒントの文言。</param>
    /// <param name="onClick">押下時の処理。</param>
    /// <param name="caption">アイコンの右へ表示する文字（省略時はアイコンのみ）。</param>
    /// <returns>生成したボタン。</returns>
    public static ToolButton CreateToolButton(AppIcon icon, string toolTip, Action onClick, string caption = "")
    {
        ArgumentNullException.ThrowIfNull(onClick);

        var button = new ToolButton(icon, toolTip, caption);
        button.Click += (_, _) => onClick();
        AttachToolTip(button, toolTip);
        return button;
    }

    /// <summary>ツールバーの区切り線を生成する。</summary>
    /// <returns>生成した区切り線。</returns>
    public static Panel CreateSeparator()
        => new()
        {
            Width = 1,
            Height = ToolbarHeight - 8,
            BackColor = AppStyles.Border,
            Margin = new Padding(4, 4, 4, 4),
        };

    /// <summary>ツールバーの入れ物を生成する（横幅に収まらない場合は折り返す）。</summary>
    /// <returns>生成した入れ物。</returns>
    public static FlowLayoutPanel CreateToolbar()
    {
        return new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(4, 1, 4, 1),
            BackColor = AppStyles.BarBackground,
        };
    }

    /// <summary>ステータスバーの入れ物を生成する。</summary>
    /// <returns>生成した入れ物。</returns>
    public static Panel CreateStatusBar()
    {
        return new Panel
        {
            Dock = DockStyle.Bottom,
            Height = StatusBarHeight,
            Padding = new Padding(4, 1, 4, 1),
            BackColor = AppStyles.PageBackground,
        };
    }

    /// <summary>ステータスバーの項目を生成する（幅を固定して値の変化で揺れないようにする）。</summary>
    /// <param name="width">幅（DIP）。 0 以下は自動。</param>
    /// <param name="toolTip">ツールヒントの文言（省略可）。</param>
    /// <returns>生成したラベル。</returns>
    public static Label CreateStatusLabel(int width, string toolTip = "")
    {
        var label = new Label
        {
            AutoSize = width <= 0,
            Width = Math.Max(1, width),
            Height = StatusBarHeight - 4,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = AppStyles.Foreground,
            BackColor = Color.Transparent,
            Margin = new Padding(2, 0, 6, 0),
        };

        if (toolTip.Length > 0)
        {
            AttachToolTip(label, toolTip);
        }

        return label;
    }

    /// <summary>書式を固定した数値入力（小数）を生成する。</summary>
    /// <param name="value">初期値。</param>
    /// <param name="minimum">最小値。</param>
    /// <param name="maximum">最大値。</param>
    /// <param name="step">増分。</param>
    /// <param name="decimals">小数部の桁数。</param>
    /// <returns>生成した入力。</returns>
    public static NumericUpDown CreateDecimalInput(
        double value, double minimum, double maximum, double step, int decimals)
    {
        var input = new NumericUpDown
        {
            Minimum = (decimal)minimum,
            Maximum = (decimal)maximum,
            Increment = (decimal)step,
            DecimalPlaces = decimals,
            Width = 72,
            ForeColor = AppStyles.Foreground,
            BackColor = AppStyles.PageBackground,
        };
        input.Value = Math.Clamp((decimal)value, input.Minimum, input.Maximum);
        return input;
    }

    /// <summary>色を選ぶボタンを生成する（押下で色の選択ダイアログを開く）。</summary>
    /// <param name="color">初期の色。</param>
    /// <param name="onSelected">色が選ばれたときの処理。</param>
    /// <param name="toolTip">ツールヒントの文言（省略可）。</param>
    /// <returns>生成したボタン。</returns>
    /// <remarks>
    /// <b>不透明度は保つ</b>: 標準の色ダイアログは不透明度を扱えないため、選ばれた RGB に<b>現在の不透明度</b>を<br/>
    /// 付け直す（暗転オーバーレイのような半透明の設定が、色を選んだだけで不透明にならないようにする）。
    /// </remarks>
    public static Button CreateColorButton(Color color, Action<Color> onSelected, string toolTip = "")
    {
        ArgumentNullException.ThrowIfNull(onSelected);

        var button = new Button
        {
            Width = 72,
            Height = 24,
            FlatStyle = FlatStyle.Flat,
            BackColor = color,
            ForeColor = IsDark(color) ? Color.White : Color.Black,
            Text = FormatColor(color),
            UseVisualStyleBackColor = false,
            TabStop = false,
        };
        button.FlatAppearance.BorderColor = AppStyles.Border;
        button.Click += (_, _) =>
        {
            var alpha = button.BackColor.A;
            using var dialog = new ColorDialog
            {
                Color = Color.FromArgb(255, button.BackColor.R, button.BackColor.G, button.BackColor.B),
                FullOpen = true,
                AnyColor = true,
            };

            if (dialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            var selected = Color.FromArgb(alpha, dialog.Color.R, dialog.Color.G, dialog.Color.B);
            button.BackColor = selected;
            button.ForeColor = IsDark(selected) ? Color.White : Color.Black;
            button.Text = FormatColor(selected);
            onSelected(selected);
        };

        if (toolTip.Length > 0)
        {
            AttachToolTip(button, toolTip);
        }

        return button;
    }

    /// <summary>設定の行（見出し + 入力）を生成する。</summary>
    /// <param name="caption">見出しの文言。</param>
    /// <param name="input">入力のコントロール。</param>
    /// <param name="toolTip">ツールヒントの文言（省略可）。</param>
    /// <returns>生成した行。</returns>
    public static Panel CreateRow(string caption, Control input, string toolTip = "")
    {
        ArgumentNullException.ThrowIfNull(input);

        var row = new Panel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0, 2, 0, 2) };
        var label = new Label
        {
            Text = caption,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = AppStyles.Foreground,
            Margin = new Padding(0, 4, 8, 0),
        };
        input.Location = new Point(label.Width + 8, 2);
        row.Controls.Add(label);
        row.Controls.Add(input);
        row.Height = Math.Max(label.Height, input.Height) + 6;

        if (toolTip.Length > 0)
        {
            AttachToolTip(label, toolTip);
        }

        return row;
    }

    /// <summary>色を 16 進の文字列（<c>#AARRGGBB</c>）へ整形する。</summary>
    /// <param name="color">整形する色。</param>
    /// <returns><c>#AARRGGBB</c> 形式の文字列。</returns>
    public static string FormatColor(Color color)
        => $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";

    /// <summary>背景色に対して暗い色かどうかを判定する（文字色の選択に使う）。</summary>
    /// <param name="color">判定する色。</param>
    /// <returns>暗い場合は true。</returns>
    private static bool IsDark(Color color)
        => ((0.299 * color.R) + (0.587 * color.G) + (0.114 * color.B)) < 140;

    /// <summary>数値を不変の書式で文字列化する。</summary>
    /// <param name="value">対象の数値。</param>
    /// <param name="format">書式（例: "0.##"）。</param>
    /// <returns>整形した文字列。</returns>
    public static string FormatNumber(double value, string format)
        => value.ToString(format, CultureInfo.InvariantCulture);
}
