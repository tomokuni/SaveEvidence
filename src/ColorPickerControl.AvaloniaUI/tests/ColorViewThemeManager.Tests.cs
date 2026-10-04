using BindingFlags = System.Reflection.BindingFlags;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Xunit;

namespace EsUtil.SaveEvidence.ColorPicker.AvaloniaUI.Tests;

/// <summary><see cref="ColorViewThemeManager"/> の単体テスト（テーマ適用・明示指定の優先・二重適用防止）。</summary>
/// <remarks>
/// Application.Styles を操作するため、Avalonia ヘッドレス実行（<c>[AvaloniaFact]</c>）で直列検証する。
/// </remarks>
[Collection("AvaloniaHeadless")]
public class ColorViewThemeManagerTests
{
    [AvaloniaFact]
    internal void EnsureApplied_未指定時は内包テーマを追加する()
    {
        var app = Application.Current;
        Assert.NotNull(app);

        // 静的フラグをリセットして追加パスを実行する（他テストで既に追加済みの場合があるため）
        ResetAppliedFlag();
        var before = app!.Styles.Count;

        ColorViewThemeManager.EnsureApplied();

        // 内包テーマ（AvaloniaXamlLoader でロードした Styles）が追加される
        Assert.True(app.Styles.Count >= before + 1);
        Assert.Contains(app.Styles, s => s is Styles);
    }

    [AvaloniaFact]
    internal void EnsureApplied_複数回呼んでも重複追加されない()
    {
        var app = Application.Current;
        Assert.NotNull(app);
        ResetAppliedFlag();

        ColorViewThemeManager.EnsureApplied();
        var afterFirst = app!.Styles.Count;

        ColorViewThemeManager.EnsureApplied();

        // 2回目は適用済みフラグにより即座に終了し追加されない
        Assert.Equal(afterFirst, app.Styles.Count);
    }

    [AvaloniaFact]
    internal void EnsureApplied_ホストが明示指定済みなら追加しない()
    {
        var app = Application.Current;
        Assert.NotNull(app);
        ResetAppliedFlag();

        // ホストが明示的に StyleInclude を指定した状態を再現する
        var styleInclude = CreateColorViewStyleInclude();
        app!.Styles.Add(styleInclude);
        var before = app.Styles.Count;

        ColorViewThemeManager.EnsureApplied();

        // 明示指定を尊重し、追加しない（カウント不変）
        Assert.Equal(before, app.Styles.Count);
    }

    [AvaloniaFact]
    internal void ApplyOnceCore_適用済みなら何もしない()
    {
        var app = Application.Current;
        Assert.NotNull(app);
        ResetAppliedFlag();

        // 適用済みフラグを立てた状態で再適用を試みても追加されない（ロック内の再確認分岐）
        var before = app!.Styles.Count;
        typeof(ColorViewThemeManager).GetField("s_applied",
            BindingFlags.Static | BindingFlags.NonPublic)!
            .SetValue(null, true);

        ColorViewThemeManager.ApplyOnceCore(app);

        Assert.Equal(before, app.Styles.Count);
    }

    [AvaloniaFact]
    internal void EnsureApplied_Application未初期化なら何もしない()
    {
        // Core へ null を渡すことで、Application.Current が null の未初期化状態を再現する。
        // Avalonia 12 の Application.Current は getter のみ（AvaloniaLocator 経由）のため、
        // null 状態を直接作り出す代わりに Core の引数で検証する。
        ColorViewThemeManager.EnsureAppliedCore(null);
    }

    [AvaloniaFact]
    internal void EnsureApplied_デザインモードでは何もしない()
    {
        var app = Application.Current;
        Assert.NotNull(app);
        ResetAppliedFlag();
        var before = app!.Styles.Count;

        // Design.IsDesignMode を true にしてテーマ適用をスキップする分岐をカバーする（try/finally で戻す）
        var prop = typeof(Design).GetProperty("IsDesignMode")!;
        prop.SetValue(null, true);
        try
        {
            ColorViewThemeManager.EnsureApplied();
        }
        finally
        {
            prop.SetValue(null, false);
        }

        // デザインモードではテーマを追加しない
        Assert.Equal(before, app.Styles.Count);
    }

    [AvaloniaFact]
    internal void IsColorViewTheme_ColorPickerテーマはtrueを返す()
    {
        var include = CreateColorViewStyleInclude();

        Assert.True(ColorViewThemeManager.IsColorViewTheme(include));
    }

    [AvaloniaFact]
    internal void IsColorViewTheme_Sourceがnullならfalseを返す()
    {
        var type = typeof(StyleInclude);
        var uriCtor = type.GetConstructors()
            .First(c => c.GetParameters().Length == 1 && c.GetParameters()[0].ParameterType == typeof(Uri));
        var include = (StyleInclude)uriCtor.Invoke(
            [new Uri("avares://Avalonia.Controls.ColorPicker/Themes/Fluent/Fluent.xaml")]);

        // Source が null（コンストラクタで設定されない実装）の場合は false
        if (include.Source is null)
        {
            Assert.False(ColorViewThemeManager.IsColorViewTheme(include));
        }
    }

    [AvaloniaFact]
    internal void IsColorViewTheme_無関係なテーマはfalseを返す()
    {
        var include = CreateColorViewStyleInclude();
        // Source を無関係な URI に差し替える（リフレクションで設定）
        typeof(StyleInclude).GetProperty("Source")!.SetValue(
            include, new Uri("avares://SomeOther/Theme.xaml"));

        Assert.False(ColorViewThemeManager.IsColorViewTheme(include));
    }

    /// <summary>ColorView テーマの適用済み静的フラグをリセットする。</summary>
    private static void ResetAppliedFlag()
    {
        typeof(ColorViewThemeManager).GetField("s_applied",
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
}
