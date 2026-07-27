using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Estable.AvaloniaUI.ColorPicker.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Estable.AvaloniaUI.ColorPicker;

/// <summary>色選択コンボボックスとカラーピッカーを統合した再利用可能コントロール。</summary>
/// <remarks>
/// ComboBox（色名/16進入力＋ドロップダウン選択）のテキスト領域左端に
/// 選択中の色を示す色スウォッチ（四角）をオーバーレイ配置する。<br/>
/// ComboBox はデフォルトテーマを維持し、カスタム ControlTemplate は使用しない。<br/>
/// 右側にカラーピッカーダイアログ起動ボタンを配置する。<br/>
/// 内部レイアウトは Grid（*,Auto）で、ComboBox が残りの幅を埋める。<br/>
/// そのため UserControl 自体の <c>MinWidth</c>/<c>Width</c>/<c>MaxWidth</c> で
/// ComboBox＋ボタン＋スペースを含めた全体のサイズを制御できる。<br/>
/// ドロップダウン選択後はテキスト選択状態を自動解除する。<br/>
/// 無効な色が入力された場合は最終有効値に自動復帰する。<br/>
/// <see cref="HighlightBorderOnError"/> が true の場合、バリデーション失敗時に ComboBox の枠線が赤くなる。<br/>
/// ComboBox 単体のサイズ/パディング/マージンは <see cref="ComboBoxMinWidth"/>、
/// <see cref="ComboBoxMaxWidth"/>、<see cref="ComboBoxWidth"/>、
/// <see cref="ComboBoxPadding"/>、<see cref="ComboBoxMargin"/> で外部から制御できる。<br/>
/// <br/>
/// **公開 API 一覧**:<br/>
/// - コンストラクタ: <see cref="ColorPickerControl()"/><br/>
/// - 値: <see cref="SelectedColor"/> / <see cref="ColorSortMode"/><br/>
/// - 検証: <see cref="HasValidationError"/> / <see cref="ValidationErrorMessage"/> / <see cref="HighlightBorderOnError"/><br/>
/// - 外観（ComboBox サイズ）: <see cref="ComboBoxWidth"/> / <see cref="ComboBoxMinWidth"/> / <see cref="ComboBoxMaxWidth"/> /<br/>
///   <see cref="ComboBoxPadding"/> / <see cref="ComboBoxMargin"/><br/>
/// - 関連型: <see cref="ColorSortMode"/> / <see cref="StringToBrushConverter"/> / <see cref="ColorEntry"/>（internal）<br/>
/// <br/>
/// **構成**: 本クラスは <c>ColorPicker.axaml</c>（<c>x:Class</c>）と対になるコードビハインドであり、<br/>
/// 依存関係プロパティ（StyledProperty）・公開プロパティ・入力イベント処理・Flyout・テーマ管理・
/// バリデーションを単一ファイルに集約する。<br/>
/// 色一覧データ（<see cref="ColorEntry"/>）と色変換コンバーター（<see cref="StringToBrushConverter"/>）は
/// ルート直下の別ファイルに定義する。<br/>
/// </remarks>
public sealed partial class ColorPickerControl : UserControl
{
    // ====================================================================
    // 依存関係プロパティ（StyledProperty）
    // ====================================================================

    /// <summary><see cref="SelectedColor"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<string> SelectedColorProperty =
        AvaloniaProperty.Register<ColorPickerControl, string>(nameof(SelectedColor),
            defaultValue: "White", defaultBindingMode: BindingMode.TwoWay);

    /// <summary><see cref="ColorSortMode"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<ColorSortMode> ColorSortModeProperty =
        AvaloniaProperty.Register<ColorPickerControl, ColorSortMode>(nameof(ColorSortMode), defaultValue: ColorSortMode.ByHue);

    /// <summary><see cref="HasValidationError"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<bool> HasValidationErrorProperty =
        AvaloniaProperty.Register<ColorPickerControl, bool>(nameof(HasValidationError), defaultValue: false);

    /// <summary><see cref="ValidationErrorMessage"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<string?> ValidationErrorMessageProperty =
        AvaloniaProperty.Register<ColorPickerControl, string?>(nameof(ValidationErrorMessage));

    /// <summary><see cref="HighlightBorderOnError"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<bool> HighlightBorderOnErrorProperty =
        AvaloniaProperty.Register<ColorPickerControl, bool>(nameof(HighlightBorderOnError), defaultValue: false);

    /// <summary><see cref="ComboBoxMinWidth"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<double> ComboBoxMinWidthProperty =
        AvaloniaProperty.Register<ColorPickerControl, double>(nameof(ComboBoxMinWidth), defaultValue: 120d);

    /// <summary><see cref="ComboBoxPadding"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<Thickness?> ComboBoxPaddingProperty =
        AvaloniaProperty.Register<ColorPickerControl, Thickness?>(nameof(ComboBoxPadding));

    /// <summary><see cref="ComboBoxMargin"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<Thickness?> ComboBoxMarginProperty =
        AvaloniaProperty.Register<ColorPickerControl, Thickness?>(nameof(ComboBoxMargin));

    /// <summary><see cref="ComboBoxMaxWidth"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<double> ComboBoxMaxWidthProperty =
        AvaloniaProperty.Register<ColorPickerControl, double>(nameof(ComboBoxMaxWidth), defaultValue: double.PositiveInfinity);

    /// <summary><see cref="ComboBoxWidth"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<double> ComboBoxWidthProperty =
        AvaloniaProperty.Register<ColorPickerControl, double>(nameof(ComboBoxWidth), defaultValue: double.NaN);

    // ====================================================================
    // 値
    // ====================================================================

    /// <summary>現在選択されている色（双方向バインド用）。</summary>
    /// <value>保存値は <c>#AARRGGBB</c>（アルファ先頭16進）形式（例: "#80FF0000"）。<br/>
    /// 色名や <c>+{n}%</c> 形式を設定することもできる（正規化は <see cref="ColorString"/> で行う）。<br/>
    /// ComboBox の表示テキストは <see cref="ColorDisplayConverter"/> により「色名+不透明度%」形式へ変換される。</value>
    public string SelectedColor
    {
        get => GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    /// <summary>ドロップダウンの色の並び順を取得または設定する。</summary>
    /// <value>色名順 (<see cref="ColorSortMode.ByName"/>) または色相順 (<see cref="ColorSortMode.ByHue"/>)。</value>
    public ColorSortMode ColorSortMode
    {
        get => GetValue(ColorSortModeProperty);
        set => SetValue(ColorSortModeProperty, value);
    }

    // ====================================================================
    // 検証
    // ====================================================================

    /// <summary>バリデーションエラーが有効かどうかを取得する。</summary>
    /// <value>現在の <see cref="SelectedColor"/> が無効な場合 true。</value>
    public bool HasValidationError
    {
        get => GetValue(HasValidationErrorProperty);
        private set => SetValue(HasValidationErrorProperty, value);
    }

    /// <summary>バリデーションエラーメッセージを取得する。</summary>
    /// <value>無効な場合の理由説明。有効な場合は null。</value>
    public string? ValidationErrorMessage
    {
        get => GetValue(ValidationErrorMessageProperty);
        private set => SetValue(ValidationErrorMessageProperty, value);
    }

    /// <summary>バリデーション失敗時に ComboBox の枠線を赤くするかどうかを取得または設定する。</summary>
    /// <value>true の場合、バリデーション失敗時に ComboBox の BorderBrush が赤色になる。</value>
    public bool HighlightBorderOnError
    {
        get => GetValue(HighlightBorderOnErrorProperty);
        set => SetValue(HighlightBorderOnErrorProperty, value);
    }

    // ====================================================================
    // 外観（ComboBox サイズ・パディング・マージン）
    // ====================================================================

    /// <summary>ComboBox の明示的な幅を取得または設定する。</summary>
    /// <value>デフォルトは NaN（自動）。</value>
    public double ComboBoxWidth
    {
        get => GetValue(ComboBoxWidthProperty);
        set => SetValue(ComboBoxWidthProperty, value);
    }

    /// <summary>ComboBox の最小幅を取得または設定する。</summary>
    /// <value>デフォルトは 120。</value>
    public double ComboBoxMinWidth
    {
        get => GetValue(ComboBoxMinWidthProperty);
        set => SetValue(ComboBoxMinWidthProperty, value);
    }

    /// <summary>ComboBox の最大幅を取得または設定する。</summary>
    /// <value>デフォルトは Infinity（制限なし）。</value>
    public double ComboBoxMaxWidth
    {
        get => GetValue(ComboBoxMaxWidthProperty);
        set => SetValue(ComboBoxMaxWidthProperty, value);
    }

    /// <summary>ComboBox の内部パディングを取得または設定する。</summary>
    /// <value>null の場合はテーマ標準のパディングが適用される。</value>
    public Thickness? ComboBoxPadding
    {
        get => GetValue(ComboBoxPaddingProperty);
        set => SetValue(ComboBoxPaddingProperty, value);
    }

    /// <summary>ComboBox のマージンを取得または設定する。</summary>
    /// <value>null の場合はテーマ標準のマージンが適用される。</value>
    public Thickness? ComboBoxMargin
    {
        get => GetValue(ComboBoxMarginProperty);
        set => SetValue(ComboBoxMarginProperty, value);
    }

    // ====================================================================
    // 定数・静的フィールド
    // ====================================================================

    /// <summary>内部ロガー（null の場合は出力なし）。</summary>
    private ILogger _log = NullLogger.Instance;

    /// <summary>内部ロガー（横断的関心事）。</summary>
    public ILogger Log
    {
        get => _log;
        set => _log = value ?? NullLogger.Instance;
    }

    /// <summary>ColorView テーマ（Fluent.xaml）の適用済みフラグ（複数インスタンスで重複追加しない）。</summary>
    private static bool s_colorViewThemeApplied;

    /// <summary>ColorView テーマの同期オブジェクト。</summary>
    private static readonly object s_colorViewThemeLock = new();

    /// <summary>ColorView（カラーピッカー）のテーマ URI。</summary>
    private static readonly Uri ColorViewThemeUri =
        new("avares://Avalonia.Controls.ColorPicker/Themes/Fluent/Fluent.xaml");

    /// <summary>色スウォッチの幅＋両脇マージン（ピクセル）。Left Padding に加算される。</summary>
    private const double SwatchPaddingOffset = 20;

    /// <summary>スウォッチの縦位置をテキストと揃えるための微調整（ピクセル）。</summary>
    private const double SwatchTextYAxisOffset = 1; // スウォッチの縦位置をテキストと揃えるための微調整（ピクセル）

    // ====================================================================
    // コンストラクタ・初期化
    // ====================================================================

    /// <summary>直前に有効だった色（無効入力の復帰先）。コンストラクタ・入力処理で使用する。</summary>
    private string? _lastValidColor;

    /// <summary>ColorPickerControl を初期化する。</summary>
    public ColorPickerControl()
    {
        InitializeComponent();

        _comboBox.ItemsSource = ColorEntry.AllByHue;

        if (!Design.IsDesignMode)
        {
            _lastValidColor = SelectedColor;
            UpdateValidationStatus(SelectedColor);
        }

        // テーマテンプレート適用後に Padding を調整
        _comboBox.TemplateApplied += OnComboBoxTemplateApplied;
    }

    // ====================================================================
    // 入力イベント処理・非公開 UI ロジック
    // ====================================================================

    /// <summary>ComboBox テンプレート適用後、テーマ標準 Padding にスウォッチ分を加算する。</summary>
    private void OnComboBoxTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        _comboBox.TemplateApplied -= OnComboBoxTemplateApplied;
        ApplyPaddingWithSwatchOffset();
    }

    /// <summary>ComboBox の Padding にスウォッチ分の左余白を加算して設定する。</summary>
    private void ApplyPaddingWithSwatchOffset()
    {
        var basePadding = ComboBoxPadding ?? _comboBox.Padding;
        _comboBox.Padding = new Thickness(
            basePadding.Left + SwatchPaddingOffset,
            basePadding.Top + SwatchTextYAxisOffset,
            basePadding.Right,
            basePadding.Bottom - SwatchTextYAxisOffset);
    }

    /// <summary>プロパティ変更時に有効な色の追跡とバリデーション状態を更新する。</summary>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SelectedColorProperty)
        {
            // change.NewValue / OldValue は StyledProperty&lt;string&gt; により常に string（分岐なし）
            var color = (string)change.NewValue!;
            ColorPickerLog.LogSelectedColorChanged(_log, (string)change.OldValue!, color);
            if (IsValidColor(color))
            {
                _lastValidColor = color;
            }
            UpdateValidationStatus(color);
        }
        else if (change.Property == HighlightBorderOnErrorProperty)
        {
            UpdateBorderValidationVisual();
        }
        else if (change.Property == ColorSortModeProperty)
        {
            // change.NewValue は StyledProperty&lt;ColorSortMode&gt; により常に ColorSortMode（分岐なし）
            var mode = (ColorSortMode)change.NewValue!;
            _comboBox.ItemsSource = mode == ColorSortMode.ByHue ? ColorEntry.AllByHue : ColorEntry.All;
        }
        else if (change.Property == ComboBoxPaddingProperty)
        {
            if (_comboBox is null) return;
            ApplyPaddingWithSwatchOffset();
        }
        else if (change.Property == ComboBoxMarginProperty)
        {
            if (_comboBox is null) return;

            if (change.NewValue is Thickness margin)
                _comboBox.Margin = margin;
            else
                _comboBox.ClearValue(ComboBox.MarginProperty);
        }
    }

    /// <summary>ドロップダウン選択後にテキスト選択状態を解除する。</summary>
    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox comboBox) return;

        Dispatcher.UIThread.Post(() => ResetTextBoxSelection(comboBox));
    }

    /// <summary>フォーカス喪失時、無効な色を最終有効値に戻す。</summary>
    private void OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (_lastValidColor is not null && !IsValidColor(SelectedColor))
        {
            SelectedColor = _lastValidColor;
        }
        UpdateValidationStatus(SelectedColor);
        ColorPickerLog.LogLostFocus(_log, SelectedColor, IsValidColor(SelectedColor));
    }

    /// <summary>Flyout 形式のカラーピッカーを開いて選択色を反映する。</summary>
    private void OnColorPickerButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;

        ColorPickerLog.LogOpenFlyout(_log, SelectedColor);
        var flyout = CreateColorPickerFlyout();
        flyout.ShowAt(button);
    }

    /// <summary>カラーピッカー Flyout を構築する。</summary>
    /// <returns>OK / キャンセル ボタンを含む Flyout。</returns>
    /// <remarks>
    /// テスト（ColorPickerControl.Tests）が OK / キャンセル ボタンの動作を検証するため internal 公開する。<br/>
    /// ColorView の初期色は <see cref="SelectedColor"/> をパースした色（失敗時は白）。<br/>
    /// OK ボタンは選択色を16進文字列に変換して <see cref="SelectedColor"/> へ反映する。<br/>
    /// </remarks>
    internal Flyout CreateColorPickerFlyout()
    {
        // ColorView テーマを確保する（ホストが明示指定していればそれに従い、未指定なら内包テーマを適用）
        EnsureColorViewTheme();

        // 初期色をパース（色 + 不透明度の統合形式に対応）
        var initColor = Colors.White;
        if (!string.IsNullOrWhiteSpace(SelectedColor) && ColorString.TryParse(SelectedColor, out var parsed, out _))
            initColor = parsed;

        // ColorView コントロール
        // 固定サイズ（Width/Height）は設定しない。ColorSpectrum は常に正方形で配置され、
        // タブ（Spectrum / Palette / Components）とスライダーを収めるには高さが必要なため、
        // 固定サイズ（280x240）ではコンテンツが左右下で見切れる。テーマの自然なサイズに任せる。
        // Hex 入力タブは #AARRGGBB（アルファ先頭）形式で表示する（Avalonia 標準の Color.Parse と整合）。
        var colorView = new ColorView
        {
            Color = initColor,
            MinWidth = 280,
            MinHeight = 320,
            HexInputAlphaPosition = AlphaComponentPosition.Leading,
        };

        // OK / キャンセル ボタン
        // 固定 Width を設定した Button は Fluent テーマでコンテンツが左寄せになるため、
        // HorizontalContentAlignment を明示的に中央揃えにする（短い "OK" でも中央に表示される）。
        var okButton = new Button
        {
            Content = "OK",
            Width = 70,
            Margin = new Thickness(3),
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };
        var cancelButton = new Button
        {
            Content = "キャンセル",
            Width = 70,
            Margin = new Thickness(3),
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };

        // Flyout コンテンツ
        var flyout = new Flyout
        {
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    colorView,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 5,
                        Children = { okButton, cancelButton },
                    },
                },
            },
            Placement = PlacementMode.BottomEdgeAlignedRight,
        };

        // OK: 色を適用して閉じる（SelectedColor = 保存値は #AARRGGBB 形式。不透明度はアルファへ合成済み）
        okButton.Click += (_, _) =>
        {
            // アルファを含む #AARRGGBB 形式へ整形して SelectedColor（保存値）へ反映する
            var formatted = ColorString.ToArgbHex(colorView.Color);
            ColorPickerLog.LogFlyoutOk(_log, formatted);
            SelectedColor = formatted;
            flyout.Hide();
        };

        // キャンセル: 何もせず閉じる
        cancelButton.Click += (_, _) =>
        {
            ColorPickerLog.LogFlyoutCancel(_log);
            flyout.Hide();
        };

        return flyout;
    }

    /// <summary>バリデーション状態に応じて ComboBox の枠線を更新する。</summary>
    /// <remarks>
    /// ComboBox の <c>BorderBrush</c> を直接設定する方式では、フォーカス・ホバー時（<c>:pointerover</c>）に
    /// Fluent テーマのテンプレートスタイル（<c>Border#Background</c> への上書き）が優先され、
    /// エラー枠が表示されない。<br/>
    /// そのため <c>validation-error</c> クラスを設定し、コントロールのローカルスタイル
    /// （<c>ColorPicker.axaml</c> の <c>ComboBox.validation-error /template/ Border#Background</c>）で
    /// 枠線色を上書きする（ローカルスタイルはテーマスタイルより優先される）。<br/>
    /// 疑似クラス（<c>PseudoClasses</c>）は protected のため外部から操作できず、<c>Classes</c> で設定する。
    /// </remarks>
    private void UpdateBorderValidationVisual()
    {
        if (_comboBox is null) return;

        _comboBox.Classes.Set("validation-error", HighlightBorderOnError && HasValidationError);
    }

    // ====================================================================
    // ColorView テーマ管理
    // ====================================================================

    /// <summary>ColorView テーマをアプリケーションへ自動適用する（デフォルト内包・明示指定を優先）。</summary>
    /// <remarks>
    /// Flyout / Popup はオーバーレイレイヤーに表示されるため、コントロールのローカル Styles では
    /// ColorView へテーマが伝播しない。そのため <see cref="Application.Current"/> の Styles へ追加する。<br/>
    /// - アプリ側で既に ColorView の StyleInclude が指定されている場合は追加しない（明示指定を優先・毎回判定）。<br/>
    /// - 未指定の場合のみ、内包テーマ（Fluent.xaml）を <see cref="AvaloniaXamlLoader.Load(Uri, Uri)"/> で
    ///   ロードしてデフォルトとして一度だけ追加する。<br/>
    /// - デザインモード / Application 未初期化時は何もしない。<br/>
    /// </remarks>
    internal static void EnsureColorViewTheme()
        => EnsureColorViewThemeCore(Application.Current);

    /// <summary>ColorView テーマを指定アプリケーションへ自動適用する（デフォルト内包・明示指定を優先）。</summary>
    /// <param name="app">対象アプリケーション。null の場合は何もしない。</param>
    /// <remarks>
    /// Flyout / Popup はオーバーレイレイヤーに表示されるため、コントロールのローカル Styles では
    /// ColorView へテーマが伝播しない。そのためアプリケーションの Styles へ追加する。<br/>
    /// - アプリ側で既に ColorView の StyleInclude が指定されている場合は追加しない（明示指定を優先・毎回判定）。<br/>
    /// - 未指定の場合のみ、内包テーマ（Fluent.xaml）を <see cref="AvaloniaXamlLoader.Load(Uri, Uri)"/> で
    ///   ロードしてデフォルトとして一度だけ追加する。<br/>
    /// - デザインモード / Application 未初期化時は何もしない。<br/>
    /// テスト（ColorPickerControl.Tests）が app=null の分岐を検証するため Core を分離する。<br/>
    /// </remarks>
    internal static void EnsureColorViewThemeCore(Application? app)
    {
        if (app is null || Design.IsDesignMode)
        {
            return;
        }

        lock (s_colorViewThemeLock)
        {
            // アプリ側で明示的に ColorView テーマが指定されている場合はそれに従う（内包テーマは追加しない）
            var hostThemeSpecified = app.Styles.Any(IsColorViewTheme);
            if (hostThemeSpecified)
            {
                return;
            }

            // 未指定の場合のみ、内包テーマをデフォルトとして一度だけ追加する
            if (!s_colorViewThemeApplied)
            {
                if (AvaloniaXamlLoader.Load(ColorViewThemeUri, ColorViewThemeUri) is IStyle theme)
                {
                    app.Styles.Add(theme);
                }

                s_colorViewThemeApplied = true;
            }
        }
    }

    /// <summary>指定スタイルが ColorView テーマ（ColorPicker の StyleInclude）であるか判定する。</summary>
    /// <param name="style">判定対象のスタイル。</param>
    /// <returns>ColorView テーマの場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// アプリ側の明示指定（StyleInclude）を検出するための判定。<br/>
    /// テスト（ColorPickerControl.Tests）が全分岐（Source null / 無関係テーマ / ColorPicker テーマ）を
    /// 検証するため internal 公開する。<br/>
    /// </remarks>
    internal static bool IsColorViewTheme(IStyle style)
        => style is StyleInclude { Source: { } src }
            && src.ToString().Contains("Avalonia.Controls.ColorPicker", StringComparison.Ordinal);

    /// <summary>指定ビジュアルツリー内の TextBox のテキスト選択を末尾に移動する。</summary>
    /// <param name="visual">検索対象のビジュアルツリー。</param>
    /// <remarks>
    /// ドロップダウン選択後、ComboBox 内のテキストが全選択されたままになるのを防ぐ。<br/>
    /// テスト（ColorPickerControl.Tests）がテキスト選択解除の分岐を検証するため internal 公開する。<br/>
    /// </remarks>
    internal static void ResetTextBoxSelection(Visual visual)
    {
        var textBox = FindTextBoxInVisualTree(visual);
        if (textBox is not null)
        {
            var len = textBox.Text?.Length ?? 0;
            textBox.SelectionStart = len;
            textBox.SelectionEnd = len;
        }
    }

    /// <summary>ビジュアルツリーから TextBox を再帰検索する。</summary>
    /// <remarks>テスト（ColorPickerControl.Tests）が検索分岐を検証するため internal 公開する。</remarks>
    internal static TextBox? FindTextBoxInVisualTree(Visual visual)
    {
        if (visual is TextBox tb) return tb;
        foreach (var child in visual.GetVisualChildren())
        {
            var result = FindTextBoxInVisualTree(child);
            if (result is not null) return result;
        }
        return null;
    }

    // ====================================================================
    // 色バリデーション・変換（純粋演算）
    // ====================================================================

    /// <summary>色の妥当性を検証する。</summary>
    /// <remarks>
    /// 色と不透明度を統合した形式（"Red", "Red+50%", "#336699+75%"）を <see cref="ColorString.TryParse"/> で検証する。<br/>
    /// テスト（ColorPickerControl.Tests）が null 分岐を検証するため internal 公開する。
    /// </remarks>
    internal static bool IsValidColor(string? color)
        => color is not null && ColorString.TryParse(color, out _, out _);

    /// <summary>バリデーション状態を更新する。</summary>
    /// <remarks>テスト（ColorPickerControl.Tests）が全分岐を検証するため internal 公開する。</remarks>
    internal void UpdateValidationStatus(string color)
    {
        if (string.IsNullOrEmpty(color))
        {
            HasValidationError = false;
            ValidationErrorMessage = null;
        }
        else if (!IsValidColor(color))
        {
            HasValidationError = true;
            ValidationErrorMessage = "無効な色コードです。";
        }
        else
        {
            HasValidationError = false;
            ValidationErrorMessage = null;
        }
        UpdateBorderValidationVisual();
    }
}

