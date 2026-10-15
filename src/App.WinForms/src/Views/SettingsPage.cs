using System.ComponentModel;
using System.Reflection;
using EsUtil.Platform.Core;
using EsUtil.SaveEvidence.App.Core.Enums;
using EsUtil.SaveEvidence.App.Core.Models;
using EsUtil.SaveEvidence.App.Core.Services;
using EsUtil.SaveEvidence.App.Core.ViewModels;
using EsUtil.SaveEvidence.App.WinForms.Services;

namespace EsUtil.SaveEvidence.App.WinForms.Views;

/// <summary>設定ページ（7 セクションの縦 1 列。 変更はその場で設定ファイルへ保存する）。</summary>
/// <remarks>
/// <b>セクション</b>: 外観テーマ / 保存 / 画像 / キャプチャ / 矩形選択範囲の制限 / ホットキー / 外観設定。<br/>
/// <b>即時保存</b>: 入力のたびに <see cref="ISettingsService.Save"/> を呼ぶ（「適用」ボタンを持たない）。<br/>
/// <b>色の入力</b>: 色の選択は GDI+ の色ダイアログで行う（色と不透明度は設定の <c>#AARRGGBB</c> 形式で保持する）。<br/>
/// <b>反映</b>: 外観設定の変更は設定の変更通知を通じてキャプチャページの撮影セッションへ反映される。
/// </remarks>
internal static class SettingsPage
{
    /// <summary>値の列の幅（DIP）。</summary>
    private const int ValueColumnWidth = 260;

    /// <summary>ページの内容を生成する。</summary>
    /// <param name="services">アプリ全体で共有するコアオブジェクト。</param>
    /// <returns>ページの内容。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> が null の場合。</exception>
    public static PageContent Build(AppServices services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var settingsService = services.Settings;
        var settings = settingsService.Current;

        var host = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = AppStyles.PageBackground };
        var stack = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(2),
        };
        host.Controls.Add(stack);

        stack.Controls.Add(BuildThemeSection(settingsService));
        stack.Controls.Add(BuildSaveFolderSection(settingsService));
        stack.Controls.Add(BuildImageSection(settingsService));
        stack.Controls.Add(BuildCaptureSection(settingsService));
        stack.Controls.Add(BuildSelectionClampSection(settingsService));
        stack.Controls.Add(BuildHotKeySection(settingsService));
        stack.Controls.Add(BuildAppearanceSection(settingsService));

        AppStyles.ThemeChanged += (_, _) => ApplyAppearance();
        ApplyAppearance();

        return new PageContent(host, OnActivated: ApplyAppearance);

        // ────────────────────────────────────────────────────────────────

        // 配色を現在のテーマへ合わせる。
        void ApplyAppearance()
        {
            host.BackColor = AppStyles.PageBackground;
            stack.BackColor = AppStyles.PageBackground;
            foreach (Control section in stack.Controls)
            {
                ApplyThemeTo(section);
            }
        }

        // 子孫のコントロールへテーマの配色を再帰的に適用する。
        void ApplyThemeTo(Control control)
        {
            if (control is GroupBox || control is Label || control is CheckBox)
            {
                control.ForeColor = AppStyles.Foreground;
            }

            if (control is TextBox || control is NumericUpDown || control is ComboBox)
            {
                control.ForeColor = AppStyles.Foreground;
                control.BackColor = AppStyles.PageBackground;
            }

            if (control is FlowLayoutPanel or TableLayoutPanel or Panel)
            {
                control.BackColor = AppStyles.PageBackground;
            }

            foreach (Control child in control.Controls)
            {
                ApplyThemeTo(child);
            }
        }
    }

    /// <summary>外観テーマのセクションを生成する。</summary>
    /// <param name="settingsService">設定サービス。</param>
    /// <returns>生成したセクション。</returns>
    private static Control BuildThemeSection(ISettingsService settingsService)
    {
        var section = CreateSection("外観テーマ設定");
        var combo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 200,
        };

        foreach (var mode in MainViewModel.ThemeModes)
        {
            combo.Items.Add(GetDescription(mode));
        }

        combo.SelectedIndex = Math.Max(0, IndexOfThemeMode(settingsService.Current.ThemeMode));
        combo.SelectedIndexChanged += (_, _) =>
        {
            if (combo.SelectedIndex < 0)
            {
                return;
            }

            var mode = MainViewModel.ThemeModes[combo.SelectedIndex];
            settingsService.Current.ThemeMode = mode;
            settingsService.Save();
            AppStyles.ApplyTheme(mode);
        };

        section.Controls.Add(UiFactory.CreateRow("テーマ", combo, "ライト / ダーク / システム（OS の設定に追従）"));
        return section;
    }

    /// <summary>保存設定のセクションを生成する。</summary>
    /// <param name="settingsService">設定サービス。</param>
    /// <returns>生成したセクション。</returns>
    private static Control BuildSaveFolderSection(ISettingsService settingsService)
    {
        var section = CreateSection("保存設定");
        var text = new TextBox
        {
            Width = 420,
            Text = settingsService.Current.SaveFolderPath,
        };
        var browse = new Button { Text = "参照...", Width = 64, FlatStyle = FlatStyle.Flat, TabStop = false };
        browse.Click += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog { SelectedPath = text.Text };
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                text.Text = dialog.SelectedPath;
            }
        };

        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 2, 0, 2) };
        var label = new Label { Text = "保存先フォルダ", AutoSize = true, Margin = new Padding(0, 6, 8, 0) };
        row.Controls.AddRange([label, text, browse]);
        section.Controls.Add(row);

        void Save()
        {
            settingsService.Current.SaveFolderPath = text.Text;
            settingsService.Save();
            var resolved = MainViewModel.ResolveSaveFolderPath(text.Text);
            var valid = !string.IsNullOrEmpty(resolved) && Directory.Exists(resolved);
            text.BackColor = valid ? AppStyles.PageBackground : Color.FromArgb(0x50, 0x20, 0x20);
            UiFactory.AttachToolTip(text, valid ? resolved : $"フォルダが見つかりません: {resolved}");
        }

        text.Leave += (_, _) => Save();
        Save();
        return section;
    }

    /// <summary>画像設定のセクションを生成する。</summary>
    /// <param name="settingsService">設定サービス。</param>
    /// <returns>生成したセクション。</returns>
    private static Control BuildImageSection(ISettingsService settingsService)
    {
        var section = CreateSection("画像設定");
        section.Controls.Add(CreateCheckBox(
            "画像を中央に配置（オフの場合は左上に配置）",
            settingsService.Current.CenterAlign,
            value =>
            {
                settingsService.Current.CenterAlign = value;
                settingsService.Save();
            }));
        return section;
    }

    /// <summary>キャプチャ設定のセクションを生成する。</summary>
    /// <param name="settingsService">設定サービス。</param>
    /// <returns>生成したセクション。</returns>
    private static Control BuildCaptureSection(ISettingsService settingsService)
    {
        var section = CreateSection("キャプチャ設定");
        section.Controls.Add(CreateCheckBox(
            "キャプチャ時にメイン ウィンドウを隠す",
            settingsService.Current.HideMainWindowOnCapture,
            value =>
            {
                settingsService.Current.HideMainWindowOnCapture = value;
                settingsService.Save();
            }));
        return section;
    }

    /// <summary>矩形選択範囲の制限のセクションを生成する。</summary>
    /// <param name="settingsService">設定サービス。</param>
    /// <returns>生成したセクション。</returns>
    private static Control BuildSelectionClampSection(ISettingsService settingsService)
    {
        var section = CreateSection("矩形選択範囲の制限");
        section.Controls.Add(CreateCheckBox(
            "画像外の選択を禁止（境界内にクリップ）",
            settingsService.Current.ClampToSourceBounds,
            value =>
            {
                settingsService.Current.ClampToSourceBounds = value;
                settingsService.Save();
            }));
        return section;
    }

    /// <summary>ホットキー設定のセクションを生成する。</summary>
    /// <param name="settingsService">設定サービス。</param>
    /// <returns>生成したセクション。</returns>
    private static Control BuildHotKeySection(ISettingsService settingsService)
    {
        var section = CreateSection("ホットキー設定");

        Add("全画面キャプチャ", () => settingsService.Current.SelectScreenHotKey, value => settingsService.Current.SelectScreenHotKey = value);
        Add("ウィンドウ選択キャプチャ", () => settingsService.Current.WindowSelectHotKey, value => settingsService.Current.WindowSelectHotKey = value);
        Add("範囲選択キャプチャ", () => settingsService.Current.AreaSelectHotKey, value => settingsService.Current.AreaSelectHotKey = value);
        return section;

        void Add(string caption, Func<HotKeySetting> get, Action<HotKeySetting> set)
        {
            var text = new TextBox { Width = 200, Text = FormatHotKey(get()) };
            var clear = new Button { Text = "クリア", Width = 64, FlatStyle = FlatStyle.Flat, TabStop = false };

            var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 2, 0, 2) };
            var label = new Label { Text = caption, AutoSize = true, Width = 180, Margin = new Padding(0, 6, 8, 0) };
            row.Controls.AddRange([label, text, clear]);
            section.Controls.Add(row);

            void Commit()
            {
                if (string.IsNullOrWhiteSpace(text.Text))
                {
                    // クリアは「未設定」を意味する（登録側が未設定をスキップする）
                    text.BackColor = AppStyles.PageBackground;
                    set(new HotKeySetting(HotKeyModifiers.None, EsKey.None));
                    settingsService.Save();
                    return;
                }

                if (!HotKeyParser.TryParse(text.Text, out var parsed))
                {
                    text.BackColor = Color.FromArgb(0x50, 0x20, 0x20);
                    UiFactory.AttachToolTip(text, "解釈できません（例: Ctrl + Shift + Q。 修飾キーが最低 1 つ必要です）");
                    return;
                }

                text.BackColor = AppStyles.PageBackground;
                text.Text = FormatHotKey(parsed);
                UiFactory.AttachToolTip(text, "修飾キー + キー（例: Ctrl + Shift + Q）");
                set(parsed);

                // 変更通知を通じてホットキーを登録し直す（起動時と同じ経路を使う）
                settingsService.Save();
            }

            text.Leave += (_, _) => Commit();
            clear.Click += (_, _) =>
            {
                text.Text = "";
                Commit();
            };
        }
    }

    /// <summary>外観設定のセクションを生成する。</summary>
    /// <param name="settingsService">設定サービス。</param>
    /// <returns>生成したセクション。</returns>
    /// <remarks>
    /// 3 列（カテゴリ / 項目名 / 設定値）の表にし、カテゴリの切れ目へ横線を入れる。<br/>
    /// 見出しの右端に「デフォルト値に戻す」を置く。
    /// </remarks>
    private static Control BuildAppearanceSection(ISettingsService settingsService)
    {
        var settings = settingsService.Current;
        var section = CreateSection("外観設定");

        var reset = new Button
        {
            Text = "デフォルト値に戻す",
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            TabStop = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        reset.Click += (_, _) =>
        {
            settings.ResetAppearanceToDefaults();
            settingsService.Save();
            MessageBox.Show(
                "外観設定をデフォルト値へ戻しました。 表示中の値は反映されません。",
                "外観設定",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        };
        section.Controls.Add(reset);

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
            Padding = new Padding(0, 4, 0, 0),
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ValueColumnWidth));
        section.Controls.Add(table);

        var category = "";

        void Next(string name)
        {
            if (category.Length > 0 && !string.Equals(category, name, StringComparison.Ordinal))
            {
                var line = new Panel { Height = 1, BackColor = AppStyles.Border, Dock = DockStyle.Fill };
                table.RowCount++;
                table.Controls.Add(new Label { Text = "", AutoSize = true }, 0, table.RowCount - 1);
                table.Controls.Add(line, 1, table.RowCount - 1);
                table.SetColumnSpan(line, 2);
            }

            category = name;
        }

        void AddRow(string name, string caption, Control input)
        {
            Next(name);
            table.RowCount++;
            var row = table.RowCount - 1;
            table.Controls.Add(new Label { Text = name, AutoSize = true, Margin = new Padding(0, 6, 8, 0) }, 0, row);
            table.Controls.Add(new Label { Text = caption, AutoSize = true, Margin = new Padding(0, 6, 8, 0) }, 1, row);
            table.Controls.Add(input, 2, row);
        }

        void AddCheck(string name, string caption, Func<bool> get, Action<bool> set)
        {
            var box = new CheckBox { Checked = get(), AutoSize = true, Margin = new Padding(0, 3, 0, 3) };
            box.CheckedChanged += (_, _) =>
            {
                set(box.Checked);
                settingsService.Save();
            };
            AddRow(name, caption, box);
        }

        void AddColor(string name, string caption, Func<string?> get, Action<string> set)
        {
            var color = SettingsColorMath.ToDrawingColor(get(), Color.Transparent);
            AddRow(name, caption, UiFactory.CreateColorButton(color, selected =>
            {
                set(UiFactory.FormatColor(selected));
                settingsService.Save();
            }));
        }

        void AddInt(string name, string caption, Func<int> get, Action<int> set, int minimum, int maximum, int step)
        {
            var input = UiFactory.CreateDecimalInput(get(), minimum, maximum, step, decimals: 0);
            input.ValueChanged += (_, _) =>
            {
                set((int)input.Value);
                settingsService.Save();
            };
            AddRow(name, caption, input);
        }

        void AddDouble(string name, string caption, Func<double> get, Action<double> set, double minimum, double maximum, double step, int decimals)
        {
            var input = UiFactory.CreateDecimalInput(get(), minimum, maximum, step, decimals);
            input.ValueChanged += (_, _) =>
            {
                set((double)input.Value);
                settingsService.Save();
            };
            AddRow(name, caption, input);
        }

        const string Labels = "ラベル設定";
        AddCheck(Labels, "選択矩形のサイズを表示", () => settings.SizeLabelVisible, value => settings.SizeLabelVisible = value);
        AddCheck(Labels, "矩形操作を矩形内に表示（サイズの下の行）", () => settings.ShowSubLines, value => settings.ShowSubLines = value);
        AddCheck(Labels, "選択中のウィンドウのタイトルを表示", () => settings.InfoLabelVisible, value => settings.InfoLabelVisible = value);
        AddCheck(Labels, "カーソル位置の座標をカーソル横に表示", () => settings.ShowCoordLabel, value => settings.ShowCoordLabel = value);
        AddCheck(Labels, "ルーペの中心座標をルーペ上部に表示", () => settings.LoupeShowCoordLabel, value => settings.LoupeShowCoordLabel = value);

        const string Frame = "選択枠・暗転";
        AddColor(Frame, "選択枠の色", () => settings.CaptureBorderColor, value => settings.CaptureBorderColor = value);
        AddDouble(Frame, "選択枠の太さ（DIP）", () => settings.CaptureBorderThickness, value => settings.CaptureBorderThickness = value, 0.5, 20, 0.5, 1);
        AddInt(Frame, "選択矩形の最小サイズ（px）", () => settings.MinSelectionSize, value => settings.MinSelectionSize = value, 1, 200, 1);
        AddColor(Frame, "暗転オーバーレイの色", () => settings.CaptureOverlayColor, value => settings.CaptureOverlayColor = value);

        const string Handle = "ドラッグハンドル・マーカー";
        AddColor(Handle, "ハンドルの色", () => settings.HandleColor, value => settings.HandleColor = value);
        AddDouble(Handle, "ハンドルのサイズ（DIP）", () => settings.HandleSize, value => settings.HandleSize = value, 1, 64, 1, 1);
        AddDouble(Handle, "ハンドルの判定半径（DIP）", () => settings.HandleHitRadius, value => settings.HandleHitRadius = value, 1, 64, 1, 1);
        AddColor(Handle, "マーカーの色", () => settings.MarkerColor, value => settings.MarkerColor = value);
        AddDouble(Handle, "マーカーの長さ（DIP）", () => settings.MarkerLength, value => settings.MarkerLength = value, 2, 128, 2, 1);

        const string Loupe = "ルーペ";
        AddColor(Loupe, "枠色", () => settings.LoupeFrameColor, value => settings.LoupeFrameColor = value);
        AddInt(Loupe, "枠の太さ（px）", () => settings.LoupeFrameWidth, value => settings.LoupeFrameWidth = value, 1, 16, 1);
        AddColor(Loupe, "十字線の色", () => settings.LoupeCrossColor, value => settings.LoupeCrossColor = value);
        AddColor(Loupe, "選択矩形の破線の色 A", () => settings.LoupeSelectionRectColorA, value => settings.LoupeSelectionRectColorA = value);
        AddColor(Loupe, "選択矩形の破線の色 B", () => settings.LoupeSelectionRectColorB, value => settings.LoupeSelectionRectColorB = value);
        AddInt(Loupe, "拡大率", () => settings.LoupeZoomLevel, value => settings.LoupeZoomLevel = value, 2, 32, 2);
        AddInt(Loupe, "サイズ（px）", () => settings.LoupeSize, value => settings.LoupeSize = value, 64, 512, 16);
        AddDouble(Loupe, "カーソルからのオフセット（DIP）", () => settings.LoupeOffset, value => settings.LoupeOffset = value, 0, 200, 4, 1);

        const string Pasteboard = "ルーペの画像外の色";
        AddColor(Pasteboard, "画像外の色", () => settings.LoupePasteboardColor, value => settings.LoupePasteboardColor = value);

        const string SelectionLabels = "選択矩形内のラベル";
        AddColor(SelectionLabels, "1 行目（サイズ）のテキスト色", () => settings.CaptureTextColor, value => settings.CaptureTextColor = value);
        AddColor(SelectionLabels, "1 行目（サイズ）の背景色", () => settings.SizeLabelBackgroundColor, value => settings.SizeLabelBackgroundColor = value);
        AddDouble(SelectionLabels, "1 行目（サイズ）のフォント サイズ", () => settings.SizeLabelFontSize, value => settings.SizeLabelFontSize = value, 6, 48, 1, 1);
        AddColor(SelectionLabels, "2 行目のテキスト色", () => settings.SubLineColor, value => settings.SubLineColor = value);
        AddColor(SelectionLabels, "2 行目の背景色", () => settings.SubLineBackgroundColor, value => settings.SubLineBackgroundColor = value);
        AddDouble(SelectionLabels, "2 行目のフォント サイズ", () => settings.SubLineFontSize, value => settings.SubLineFontSize = value, 6, 48, 1, 1);
        AddColor(SelectionLabels, "3 行目（情報）のテキスト色", () => settings.InfoLabelColor, value => settings.InfoLabelColor = value);
        AddColor(SelectionLabels, "3 行目（情報）の背景色", () => settings.InfoLabelBackgroundColor, value => settings.InfoLabelBackgroundColor = value);
        AddDouble(SelectionLabels, "3 行目（情報）のフォント サイズ", () => settings.InfoLabelFontSize, value => settings.InfoLabelFontSize = value, 6, 48, 1, 1);

        const string CursorLabel = "カーソル位置の座標ラベル";
        AddColor(CursorLabel, "テキスト色", () => settings.CoordLabelColor, value => settings.CoordLabelColor = value);
        AddColor(CursorLabel, "背景色", () => settings.CoordLabelBackgroundColor, value => settings.CoordLabelBackgroundColor = value);
        AddDouble(CursorLabel, "フォント サイズ", () => settings.CoordLabelFontSize, value => settings.CoordLabelFontSize = value, 6, 48, 1, 1);

        const string LoupeLabel = "ルーペの中心座標ラベル";
        AddColor(LoupeLabel, "テキスト色", () => settings.LoupeCoordLabelColor, value => settings.LoupeCoordLabelColor = value);
        AddColor(LoupeLabel, "背景色", () => settings.LoupeCoordLabelBackgroundColor, value => settings.LoupeCoordLabelBackgroundColor = value);
        AddDouble(LoupeLabel, "フォント サイズ", () => settings.LoupeCoordLabelFontSize, value => settings.LoupeCoordLabelFontSize = value, 6, 48, 1, 1);

        return section;
    }

    /// <summary>セクションの枠（見出し付きの入れ物）を生成する。</summary>
    /// <param name="title">見出しの文言。</param>
    /// <returns>生成したセクション。</returns>
    private static GroupBox CreateSection(string title)
        => new()
        {
            Text = title,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Width = 700,
            Padding = new Padding(8),
            Margin = new Padding(0, 0, 0, 6),
            ForeColor = AppStyles.Foreground,
        };

    /// <summary>チェック ボックスの行を生成する。</summary>
    /// <param name="caption">文言。</param>
    /// <param name="value">初期値。</param>
    /// <param name="onChanged">変更時の処理。</param>
    /// <returns>生成したチェック ボックス。</returns>
    private static CheckBox CreateCheckBox(string caption, bool value, Action<bool> onChanged)
    {
        var box = new CheckBox
        {
            Text = caption,
            Checked = value,
            AutoSize = true,
            Margin = new Padding(0, 3, 0, 3),
        };
        box.CheckedChanged += (_, _) => onChanged(box.Checked);
        return box;
    }

    /// <summary>テーマ モードの表示名（<see cref="DescriptionAttribute"/>）を取得する。</summary>
    /// <param name="mode">テーマ モード。</param>
    /// <returns>表示名。 属性が無い場合は列挙の名前。</returns>
    private static string GetDescription(ThemeMode mode)
    {
        var field = typeof(ThemeMode).GetField(mode.ToString());
        var attribute = field?.GetCustomAttribute<DescriptionAttribute>();
        return attribute?.Description ?? mode.ToString();
    }

    /// <summary>テーマ モードの一覧内の位置を求める。</summary>
    /// <param name="mode">対象のテーマ モード。</param>
    /// <returns>位置。 見つからない場合は 0。</returns>
    private static int IndexOfThemeMode(ThemeMode mode)
    {
        var modes = MainViewModel.ThemeModes;
        for (var i = 0; i < modes.Count; i++)
        {
            if (modes[i] == mode)
            {
                return i;
            }
        }

        return 0;
    }

    /// <summary>ホットキー設定を表示用の文字列へ整形する。</summary>
    /// <param name="setting">整形する設定。</param>
    /// <returns>表示用の文字列（例: <c>Ctrl + Shift + Q</c>）。 未設定の場合は空文字。</returns>
    /// <remarks>整形はコアの <c>HotKeySetting.ToString()</c> へ委譲する（受理される書式と表示を一致させる）。</remarks>
    internal static string FormatHotKey(HotKeySetting setting)
        => setting.Key == EsKey.None && setting.Modifiers == HotKeyModifiers.None ? "" : setting.ToString();
}
