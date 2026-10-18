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

    /// <summary>ステータスバーの高さ（DIP）。 置く要素（ボタン 30）より少しだけ広げる。</summary>
    public const int StatusBarHeight = 32;

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
    /// <param name="icon">表示するアイコン（null の場合は文字だけを表示する）。</param>
    /// <param name="toolTip">ツールヒントの文言。</param>
    /// <param name="onClick">押下時の処理。</param>
    /// <param name="caption">アイコンの右へ表示する文字（省略時はアイコンのみ）。</param>
    /// <returns>生成したボタン。</returns>
    public static ToolButton CreateToolButton(AppIcon? icon, string toolTip, Action onClick, string caption = "")
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

    /// <summary>ステータス バーの内側の高さ（DIP。 上下の余白を除いた高さ）。</summary>
    public const int StatusBarContentHeight = StatusBarHeight - 2;

    /// <summary>ステータス バーの中身を垂直方向中央へ揃える。</summary>
    /// <param name="control">対象のコントロール。</param>
    /// <param name="height">中身の高さ（DIP）。 コントロールの高さをこの値へ揃える。</param>
    /// <returns>対象のコントロール（呼び出し側で続けて設定できるようにする）。</returns>
    /// <remarks>
    /// <see cref="FlowLayoutPanel"/> は子を上端へ寄せるため、余白を上下均等に割り当てて中央揃えにする<br/>
    /// （行の高さと子の高さを一致させると、文字のベースラインが上へずれる）。
    /// </remarks>
    public static T CenterInStatusBar<T>(T control, int height = 24)
        where T : Control
    {
        ArgumentNullException.ThrowIfNull(control);

        control.Height = height;
        var space = Math.Max(0, StatusBarContentHeight - height);
        control.Margin = new Padding(control.Margin.Left, space / 2, control.Margin.Right, space - (space / 2));
        return control;
    }

    /// <summary>ステータス バーの項目を生成する（幅を固定して値の変化で揺れないようにする）。</summary>
    /// <param name="width">幅（DIP）。 0 以下は自動。</param>
    /// <param name="toolTip">ツールヒントの文言（省略可）。</param>
    /// <returns>生成したラベル。</returns>
    public static Label CreateStatusLabel(int width, string toolTip = "")
    {
        var autoWidth = width <= 0;
        var label = new Label
        {
            AutoSize = autoWidth,
            Width = Math.Max(1, width),
            // ステータス バーの内側の高さいっぱいに広げ、文字を垂直方向中央へ置く
            Height = StatusBarContentHeight,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = AppStyles.Foreground,
            BackColor = Color.Transparent,
            Margin = new Padding(2, 0, 6, 0),
        };

        if (autoWidth)
        {
            // 幅を自動にすると高さも文字の高さになり、上端へ寄る（実測）。
            // 高さは文字 1 行分のまま、上下の余白で中央へ揃える
            var space = Math.Max(0, StatusBarContentHeight - label.PreferredHeight);
            label.Margin = new Padding(2, space / 2, 6, space - (space / 2));
        }

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

    /// <summary>色ボタンの幅（ピクセル）。 値（<c>#AARRGGBB</c>）が設定フォントで収まる幅。</summary>
    /// <remarks>
    /// 固定値にすると文字サイズを変えたときに欠けるため、実測値から求める。<br/>
    /// <see cref="TextRenderer.MeasureText(string, Font)"/> は余白を含まないため、左右の余白を足す。
    /// </remarks>
    private static int ColorButtonWidth =>
        TextRenderer.MeasureText("#FFA9A9A9", AppStyles.SettingsFont).Width + 14;

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
            // 値は #AARRGGBB の 9 文字。 幅・高さが足りないと文字が欠けるため、
            // 設定フォントで実際に測った大きさに余白を足す（文字サイズを変えても欠けない）
            Width = ColorButtonWidth,
            Height = AppStyles.SettingsControlHeight,
            Font = AppStyles.SettingsFont,
            FlatStyle = FlatStyle.Flat,
            BackColor = color,
            ForeColor = IsDark(color) ? Color.White : Color.Black,
            Text = FormatColor(color),
            TextAlign = ContentAlignment.MiddleCenter,
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

        // 入力の下端（位置 2 + 高さ）が欠けないよう、下余白を多めに取る。
        // 設定ページは文字サイズが大きいため、入力の高さが確定する前でも収まるよう最小の高さを確保する
        // （固定の余白だけだと、コンボ ボックスの下端が枠線に重なる。 実測）
        row.Height = Math.Max(Math.Max(label.Height, input.Height + 2), AppStyles.SettingsControlHeight + 2) + 8;

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
