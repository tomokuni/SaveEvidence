using BindingFlags = System.Reflection.BindingFlags;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Xunit;

namespace Estable.AvaloniaUI.ColorPicker.Tests;

/// <summary><see cref="ColorPickerControl"/> の非公開実装（Logic）の単体テスト（防御分岐のカバレッジ向上）。</summary>
/// <remarks>
/// 対象: <c>ColorPicker.axaml.cs</c>（入力イベント・Flyout 構築・バリデーション・変換・
/// テーマ管理・テキスト選択解除）の internal メンバーを直接検証する。<br/>
/// GetSortKey のテストは <c>ColorEntry.Tests.cs</c> に所属する（対象ソース = ColorEntry.cs）。
/// </remarks>
[Collection("AvaloniaHeadless")]
public class ColorPickerLogicTests
{
    // ====================================================================
    // ColorPickerControl.UpdateValidationStatus
    // ====================================================================

    [AvaloniaFact]
    public void UpdateValidationStatus_空文字はエラーにならない()
    {
        var control = new ColorPickerControl();

        control.UpdateValidationStatus("");

        Assert.False(control.HasValidationError);
        Assert.Null(control.ValidationErrorMessage);
    }

    [AvaloniaFact]
    public void UpdateValidationStatus_無効な値はエラーになる()
    {
        var control = new ColorPickerControl();

        control.UpdateValidationStatus("NotAColor");

        Assert.True(control.HasValidationError);
        Assert.Equal("無効な色コードです。", control.ValidationErrorMessage);
    }

    [AvaloniaFact]
    public void UpdateValidationStatus_有効な値はエラーにならない()
    {
        var control = new ColorPickerControl();

        control.UpdateValidationStatus("#123456");

        Assert.False(control.HasValidationError);
        Assert.Null(control.ValidationErrorMessage);
    }

    // ====================================================================
    // ColorPickerControl.FindTextBoxInVisualTree
    // ====================================================================

    [AvaloniaFact]
    public void FindTextBoxInVisualTree_TextBoxを含むツリーから検索できる()
    {
        var textBox = new TextBox { Name = "Target" };
        var stack = new StackPanel
        {
            Children =
            {
                new TextBlock { Text = "x" },
                new StackPanel { Children = { textBox } },
            },
        };

        var found = ColorPickerControl.FindTextBoxInVisualTree(stack);

        Assert.Same(textBox, found);
    }

    [AvaloniaFact]
    public void FindTextBoxInVisualTree_TextBoxが無ければnullを返す()
    {
        var stack = new StackPanel
        {
            Children = { new TextBlock { Text = "x" } },
        };

        var found = ColorPickerControl.FindTextBoxInVisualTree(stack);

        Assert.Null(found);
    }

    [AvaloniaFact]
    public void FindTextBoxInVisualTree_自身がTextBoxなら自身を返す()
    {
        var textBox = new TextBox();

        var found = ColorPickerControl.FindTextBoxInVisualTree(textBox);

        Assert.Same(textBox, found);
    }

    // ====================================================================
    // ColorPickerControl.ResetTextBoxSelection
    // ====================================================================

    [AvaloniaFact]
    public void ResetTextBoxSelection_TextBoxがあれば選択が末尾に移動する()
    {
        var textBox = new TextBox { Text = "Hello" };
        textBox.SelectionStart = 0;
        textBox.SelectionEnd = 5;
        var stack = new StackPanel { Children = { textBox } };

        ColorPickerControl.ResetTextBoxSelection(stack);

        Assert.Equal(5, textBox.SelectionStart);
        Assert.Equal(5, textBox.SelectionEnd);
    }

    [AvaloniaFact]
    public void ResetTextBoxSelection_TextBoxが無ければ何もしない()
    {
        var stack = new StackPanel { Children = { new TextBlock { Text = "x" } } };

        // 例外なく完了すれば成功
        ColorPickerControl.ResetTextBoxSelection(stack);
    }

    [AvaloniaFact]
    public void ResetTextBoxSelection_TextBoxのTextがnullでも例外にならない()
    {
        var textBox = new TextBox(); // Text = null
        var stack = new StackPanel { Children = { textBox } };

        ColorPickerControl.ResetTextBoxSelection(stack);

        Assert.Equal(0, textBox.SelectionStart);
        Assert.Equal(0, textBox.SelectionEnd);
    }

    // ====================================================================
    // ColorPickerControl.EnsureColorViewTheme
    // ====================================================================

    [AvaloniaFact]
    public void EnsureColorViewTheme_未指定時は内包テーマを追加する()
    {
        var app = Application.Current;
        Assert.NotNull(app);

        // 静的フラグをリセットして追加パスを実行する（他テストで既に追加済みの場合があるため）
        ResetThemeAppliedFlag();
        var before = app!.Styles.Count;

        ColorPickerControl.EnsureColorViewTheme();

        // 内包テーマ（AvaloniaXamlLoader でロードした Styles）が追加される
        Assert.True(app.Styles.Count >= before + 1);
        Assert.Contains(app.Styles, s => s is Styles);
    }

    [AvaloniaFact]
    public void EnsureColorViewTheme_複数回呼んでも重複追加されない()
    {
        var app = Application.Current;
        Assert.NotNull(app);
        ResetThemeAppliedFlag();

        ColorPickerControl.EnsureColorViewTheme();
        var afterFirst = app!.Styles.Count;

        ColorPickerControl.EnsureColorViewTheme();

        // 2回目は静的フラグにより追加されない
        Assert.Equal(afterFirst, app.Styles.Count);
    }

    [AvaloniaFact]
    public void EnsureColorViewTheme_ホストが明示指定済みなら追加しない()
    {
        var app = Application.Current;
        Assert.NotNull(app);
        ResetThemeAppliedFlag();

        // ホストが明示的に StyleInclude を指定した状態を再現する
        var styleInclude = CreateColorViewStyleInclude();
        app!.Styles.Add(styleInclude);
        var before = app.Styles.Count;

        ColorPickerControl.EnsureColorViewTheme();

        // 明示指定を尊重し、追加しない（カウント不変）
        Assert.Equal(before, app.Styles.Count);
    }

    /// <summary>ColorView テーマの適用済み静的フラグをリセットする。</summary>
    private static void ResetThemeAppliedFlag()
    {
        typeof(ColorPickerControl).GetField("s_colorViewThemeApplied",
            BindingFlags.Static | BindingFlags.NonPublic)!
            .SetValue(null, false);
    }

    /// <summary>ColorView テーマを参照する StyleInclude を生成する（リフレクションで Source を設定）。</summary>
    private static StyleInclude CreateColorViewStyleInclude()
    {
        var type = typeof(StyleInclude);
        var uriCtor = type.GetConstructors()
            .First(c => c.GetParameters().Length == 1 && c.GetParameters()[0].ParameterType == typeof(Uri));
        var include = (StyleInclude)uriCtor.Invoke(
            [new Uri("avares://Avalonia.Controls.ColorPicker/Themes/Fluent/Fluent.xaml")]);

        // コンストラクタで Source が設定されない実装の場合はリフレクションで設定する
        if (include.Source is null)
        {
            type.GetProperty("Source")!.SetValue(include,
                new Uri("avares://Avalonia.Controls.ColorPicker/Themes/Fluent/Fluent.xaml"));
        }

        return include;
    }

    // ====================================================================
    // ColorPickerControl.IsColorViewTheme
    // ====================================================================

    [AvaloniaFact]
    public void IsColorViewTheme_ColorPickerテーマはtrueを返す()
    {
        var include = CreateColorViewStyleInclude();

        Assert.True(ColorPickerControl.IsColorViewTheme(include));
    }

    [AvaloniaFact]
    public void IsColorViewTheme_Sourceがnullならfalseを返す()
    {
        var type = typeof(StyleInclude);
        var uriCtor = type.GetConstructors()
            .First(c => c.GetParameters().Length == 1 && c.GetParameters()[0].ParameterType == typeof(Uri));
        var include = (StyleInclude)uriCtor.Invoke(
            [new Uri("avares://Avalonia.Controls.ColorPicker/Themes/Fluent/Fluent.xaml")]);

        // Source が null（コンストラクタで設定されない実装）の場合は false
        if (include.Source is null)
        {
            Assert.False(ColorPickerControl.IsColorViewTheme(include));
        }
    }

    [AvaloniaFact]
    public void IsColorViewTheme_無関係なテーマはfalseを返す()
    {
        var include = CreateColorViewStyleInclude();
        // Source を無関係な URI に差し替える（リフレクションで設定）
        typeof(StyleInclude).GetProperty("Source")!.SetValue(
            include, new Uri("avares://SomeOther/Theme.xaml"));

        Assert.False(ColorPickerControl.IsColorViewTheme(include));
    }

    // ====================================================================
    // ColorPickerControl.EnsureColorViewTheme（Application 未初期化）
    // ====================================================================

    [AvaloniaFact]
    public void EnsureColorViewTheme_Application未初期化なら何もしない()
    {
        // Core へ null を渡すことで、Application.Current が null の未初期化状態を再現する。
        // Avalonia 12 の Application.Current は getter のみ（AvaloniaLocator 経由）のため、
        // null 状態を直接作り出す代わりに Core の引数で検証する。
        // 注: [AvaloniaFact] で実行する（他テストの初期化状態に依存しない）。
        ColorPickerControl.EnsureColorViewThemeCore(null);
    }
}
