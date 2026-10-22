using EsUtil.Platform.Core;
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

    /// <summary>サブグループの区切り行の高さ（ピクセル）。 線（1）と上下の余白を含む。</summary>
    private const int SeparatorHeight = 11;

    /// <summary>サブグループの区切り線。</summary>
    /// <remarks>
    /// ページの配色を一括適用する処理（ローカル関数 <c>ApplyThemeTo</c>）が <see cref="Panel"/> の背景を<br/>
    /// ページの背景色で塗りつぶすため、区切り線は <see cref="Control"/> 派生の専用型にして対象外にする<br/>
    /// （<see cref="Panel"/> のままだと線の色が消える。 実測）。
    /// </remarks>
    private sealed class SeparatorLine : Control
    {
        /// <summary>区切り線を生成する。</summary>
        public SeparatorLine()
        {
            Dock = DockStyle.Bottom;
            Height = 1;
            BackColor = AppStyles.Border;
        }
    }

    /// <summary>ページの内容を生成する。</summary>
    /// <param name="services">アプリ全体で共有するコアオブジェクト。</param>
    /// <returns>ページの内容。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> が null の場合。</exception>
    public static PageContent Build(AppServices services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var settingsService = services.Settings;
        var settings = settingsService.Current;

        var host = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = AppStyles.PageBackground,
            // 文字サイズは AvaloniaUI 版（14 DIP）に合わせる（DIP → pt へ換算する）。
            // WinForms の子は親のフォントを継承するため、ページのルートへ 1 回設定すれば全体へ伝わる
            Font = AppStyles.SettingsFont,
        };
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

        // 本 UI 版はダークテーマを使用しないため、テーマの選択は表示しない（設定ファイルの値も反映しない）
        stack.Controls.Add(BuildSaveFolderSection(settingsService));
        stack.Controls.Add(BuildImageSection(settingsService));
        stack.Controls.Add(BuildCaptureSection(settingsService));
        stack.Controls.Add(BuildSelectionClampSection(settingsService));
        stack.Controls.Add(BuildHotKeySection(settingsService));
        stack.Controls.Add(BuildAppearanceSection(settingsService));

        // セクションの幅をウィンドウ幅へ追従させる（FlowLayoutPanel は子を引き伸ばさないため明示する）
        void LayoutSections()
        {
            var width = host.ClientSize.Width - stack.Padding.Horizontal - 2;
            if (width <= 0)
            {
                return;
            }

            foreach (Control section in stack.Controls)
            {
                section.Width = width;
            }
        }

        host.SizeChanged += (_, _) => LayoutSections();
        LayoutSections();
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
                // 区切り線（SeparatorLine）は専用の色を持つため塗り替えない
                control.BackColor = AppStyles.PageBackground;
            }

            foreach (Control child in control.Controls)
            {
                ApplyThemeTo(child);
            }
        }
    }


    /// <summary>保存設定のセクションを生成する。</summary>
    /// <param name="settingsService">設定サービス。</param>
    /// <returns>生成したセクション。</returns>
    private static Control BuildSaveFolderSection(ISettingsService settingsService)
    {
        var section = CreateSection("保存設定");
        var text = new TextBox
        {
            Text = settingsService.Current.SaveFolderPath,
            Height = AppStyles.SettingsControlHeight,
        };
        var browse = new Button
        {
            Text = "参照...",
            Width = 76,
            Height = AppStyles.SettingsControlHeight,
            FlatStyle = FlatStyle.Flat,
            TabStop = false,
        };
        browse.Click += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog { SelectedPath = text.Text };
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                text.Text = dialog.SelectedPath;
            }
        };

        // 保存先の欄は残り幅いっぱい（ラベルと参照ボタンを除いた分）にし、ウィンドウ幅へ追従させる。
        // FlowLayoutPanel は子を引き伸ばさないため、入れ物の幅が変わったら幅を計算し直す
        var row = new Panel
        {
            AutoSize = false,
            Height = AppStyles.SettingsControlHeight + 4,
            Margin = new Padding(0, 2, 0, 2),
        };
        var label = new Label
        {
            Text = "保存先フォルダ",
            AutoSize = true,
            Margin = new Padding(0, 0, 8, 0),
        };
        row.Controls.AddRange([label, text, browse]);
        section.Body.Controls.Add(row);

        // プレースホルダの説明（文言は SettingsMetrics（App.Core）が単一所有する）
        var hint = new Label
        {
            Text = SettingsMetrics.SaveFolderHint,
            AutoSize = true,
            Font = AppStyles.SettingsHintFont,
            ForeColor = AppStyles.Foreground,
            Margin = new Padding(0, 0, 0, 4),
        };
        section.Body.Controls.Add(hint);

        void LayoutRow()
        {
            var width = section.Body.ClientSize.Width;
            if (width <= 0)
            {
                return;
            }

            row.Width = width;
            label.Top = Math.Max(0, (row.Height - label.Height) / 2);
            text.Left = label.Right + 6;
            text.Width = Math.Max(120, width - text.Left - browse.Width - 6);
            text.Top = Math.Max(0, (row.Height - text.Height) / 2);
            browse.Left = text.Right + 6;
            browse.Top = Math.Max(0, (row.Height - browse.Height) / 2);
        }

        section.Body.SizeChanged += (_, _) => LayoutRow();
        LayoutRow();

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
        section.Body.Controls.Add(CreateCheckBox(
            "画像を中央に配置（オフの場合は左上に配置）",
            settingsService.Current.CenterAlign,
            value =>
            {
                settingsService.Current.CenterAlign = value;
                settingsService.Save();
            }));

        // 起動直後の画像の選択肢（表示名は列挙の説明属性が単一所有する）
        var modes = MainViewModel.StartupPreviewModes;
        var combo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 200,
        };

        foreach (var mode in modes)
        {
            combo.Items.Add(EnumDisplayNames.Get(mode));
        }

        combo.SelectedIndex = Math.Max(0, modes.ToList().IndexOf(settingsService.Current.StartupPreview));
        combo.SelectedIndexChanged += (_, _) =>
        {
            if (combo.SelectedIndex < 0)
            {
                return;
            }

            settingsService.Current.StartupPreview = modes[combo.SelectedIndex];
            settingsService.Save();
        };

        section.Body.Controls.Add(UiFactory.CreateRow(
            "起動時の画像", combo, "起動直後のキャプチャ ページに置く画像（次回起動から反映）"));

        return section;
    }

    /// <summary>キャプチャ設定のセクションを生成する。</summary>
    /// <param name="settingsService">設定サービス。</param>
    /// <returns>生成したセクション。</returns>
    private static Control BuildCaptureSection(ISettingsService settingsService)
    {
        var section = CreateSection("キャプチャ設定");
        section.Body.Controls.Add(CreateCheckBox(
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
        section.Body.Controls.Add(CreateCheckBox(
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
            var text = new TextBox { Width = 200, Text = FormatHotKey(get()), Height = AppStyles.SettingsControlHeight };
            var clear = new Button
            {
                Text = "クリア",
                Width = 76,
                Height = AppStyles.SettingsControlHeight,
                FlatStyle = FlatStyle.Flat,
                TabStop = false,
            };

            var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 2, 0, 2) };
            var label = new Label { Text = caption, AutoSize = true, Width = 180, Margin = new Padding(0, 4, 8, 0) };
            row.Controls.AddRange([label, text, clear]);
            section.Body.Controls.Add(row);

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
        section.AddHeaderAction(reset);

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
        section.Body.Controls.Add(table);

        var category = "";

        bool Next(string name)
        {
            var started = category.Length > 0 && !string.Equals(category, name, StringComparison.Ordinal);
            if (started)
            {
                // サブグループの区切り線。 行の高さを固定しないと線の太さが行の高さに伸びる
                // （Dock=Fill は行の高さいっぱいへ広がる）。
                var separator = new SeparatorLine();
                var separatorRow = table.RowCount;
                table.RowCount++;
                table.RowStyles.Add(new RowStyle(SizeType.Absolute, SeparatorHeight));
                table.Controls.Add(separator, 0, separatorRow);
                table.SetColumnSpan(separator, 3);
            }

            var isFirst = category.Length == 0;
            category = name;
            return isFirst || started;
        }

        void AddRow(string name, string caption, Control input)
        {
            // グループ名は各グループの先頭行にだけ表示する（Avalonia 版の行連結と同じ見た目）。
            // 全行へ繰り返すと、どの行の項目かが読み取りにくく、縦にも長くなる
            var showCategory = Next(name);
            var row = table.RowCount;
            table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(
                showCategory
                    ? new Label { Text = name, AutoSize = true, ForeColor = AppStyles.Foreground, Margin = new Padding(0, 6, 12, 0) }
                    : new Label { Text = "", AutoSize = true },
                0, row);
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
    /// <remarks>
    /// <b>中身は <see cref="SectionBox.Body"/> へ追加する理由</b>:<br/>
    /// <see cref="GroupBox"/> に直接子を追加すると、子は枠の内側の余白（<see cref="Control.Padding"/>）を<br/>
    /// 無視して左上（枠線の上）へ置かれ、複数の子が同じ位置へ重なる。<br/>
    /// 本メソッドは中身専用の入れ物（<see cref="SectionBox.Body"/>）を持たせ、子はそこへ追加する。
    /// </remarks>
    private static SectionBox CreateSection(string title) => new(title);

    /// <summary>見出し（グループ名）と枠を持ち、中身を縦 1 列で並べるセクション。</summary>
    /// <remarks>
    /// <b>中身専用の入れ物を持つ理由</b>: <see cref="GroupBox"/> へ直接追加した子は、枠の内側の余白を無視して<br/>
    /// 左上へ置かれ、複数の子が同じ位置へ重なる（実測）。 <see cref="Body"/> は枠の内側（見出しの下）へ<br/>
    /// ドッキングするため、子は重ならず、枠線にも重ならない。<br/>
    /// <b>上余白を小さくする理由</b>: <see cref="GroupBox"/> は見出しの分を <see cref="Control.DisplayRectangle"/><br/>
    /// で既に確保している。 さらに大きな上余白を足すと、グループ名と中身が離れすぎる（実測）。<br/>
    /// <b>高さを自前で決める理由</b>: <c>AutoSize = true</c> の <see cref="GroupBox"/> は、この環境では<br/>
    /// 見出しの文字を描かない（実測）。 <c>AutoSize = false</c> にして中身の実際の下端から高さを求める。
    /// </remarks>
    private sealed class SectionBox : GroupBox
    {
        /// <summary>見出しの下へ中身を並べる入れ物。</summary>
        private readonly FlowLayoutPanel _body = new()
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(0),
            Margin = new Padding(0),
        };

        /// <summary>高さの再計算中かどうか（再入防止）。</summary>
        private bool _adjusting;

        /// <summary>セクションを生成する。</summary>
        /// <param name="title">見出しの文言。</param>
        public SectionBox(string title)
        {
            Text = title;
            AutoSize = false;
            Width = 700;
            Height = 40;
            Margin = new Padding(0, 0, 0, 8);
            ForeColor = AppStyles.Foreground;
            // 文字サイズは AvaloniaUI 版と同じにする（親の継承に頼らず明示する）
            Font = AppStyles.SettingsFont;
            // 見出しは GroupBox が確保するため、上下は小さく取る（中身をグループ名のすぐ下へ置く）
            Padding = new Padding(10, 4, 10, 6);
            Controls.Add(_body);

            // 中身の高さに合わせて枠の高さを決める
            _body.ControlAdded += (_, _) => AdjustHeight();
            _body.ControlRemoved += (_, _) => AdjustHeight();
            _body.Layout += (_, _) => AdjustHeight();
            AdjustHeight();
        }

        /// <summary>中身を追加する入れ物を取得する。</summary>
        /// <value>縦 1 列で子を並べる入れ物。</value>
        public FlowLayoutPanel Body => _body;

        /// <summary>見出しの右端に置く操作（「デフォルト値に戻す」など）を追加する。</summary>
        /// <param name="action">見出しの右端へ置くコントロール。</param>
        /// <remarks>
        /// <see cref="GroupBox"/> へ直接追加した子は枠の内側の余白を無視して左上へ置かれるため、<br/>
        /// 位置を明示する（見出しの行の右端・中身の上端）。
        /// </remarks>
        public void AddHeaderAction(Control action)
        {
            ArgumentNullException.ThrowIfNull(action);

            action.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            Controls.Add(action);
            action.BringToFront();
            Layout += (_, _) => action.Location = new Point(
                Math.Max(Padding.Left, ClientSize.Width - Padding.Right - action.Width), 2);
        }

        /// <summary>中身の高さに合わせてセクションの高さを更新する。</summary>
        /// <remarks>
        /// <b>中身の実際の下端から求める理由</b>: <c>AutoSize = true</c> にすると見出しが描かれず（実測）、<br/>
        /// <see cref="Control.PreferredSize"/> はレイアウト前だと実際より小さく、最後の行が枠に隠れる（実測）。<br/>
        /// 中身は見出しの下（<see cref="Control.DisplayRectangle"/>）へ置かれるため、その下端に下余白を足す。<br/>
        /// <b>足りないときだけ広げる理由</b>: 縮めると、フォント変更直後の一瞬だけ行が欠ける（ちらつき）。
        /// </remarks>
        private void AdjustHeight()
        {
            if (_adjusting || !IsHandleCreated)
            {
                return;
            }

            _adjusting = true;
            try
            {
                var desired = _body.Bottom + Padding.Bottom;
                if (desired > Height)
                {
                    Height = desired;
                }
            }
            finally
            {
                _adjusting = false;
            }
        }
    }

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

    /// <summary>ホットキー設定を表示用の文字列へ整形する。</summary>
    /// <param name="setting">整形する設定。</param>
    /// <returns>表示用の文字列（例: <c>Ctrl + Shift + Q</c>）。 未設定の場合は空文字。</returns>
    /// <remarks>整形はコアの <c>HotKeySetting.ToString()</c> へ委譲する（受理される書式と表示を一致させる）。</remarks>
    internal static string FormatHotKey(HotKeySetting setting)
        => setting.Key == EsKey.None && setting.Modifiers == HotKeyModifiers.None ? "" : setting.ToString();
}
