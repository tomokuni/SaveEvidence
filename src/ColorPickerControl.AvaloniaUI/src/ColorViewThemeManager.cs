using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;

namespace Estable.AvaloniaUI.ColorPicker;

/// <summary>ColorView（カラーピッカー）テーマのアプリケーションへの自動適用を管理する。</summary>
/// <remarks>
/// Flyout / Popup はオーバーレイレイヤーに表示されるため、コントロールのローカル Styles では
/// ColorView へテーマが伝播しない。そのため <see cref="Application.Current"/> の Styles へ追加する。<br/>
/// - アプリ側で既に ColorView の StyleInclude が指定されている場合は追加しない（明示指定を優先）。<br/>
/// - 未指定の場合のみ、内包テーマ（Fluent.xaml）を <see cref="AvaloniaXamlLoader.Load(Uri, Uri)"/> で
///   ロードしてデフォルトとして一度だけ追加する。<br/>
/// - デザインモード / Application 未初期化時は何もしない。<br/>
/// <br/>
/// <b>Native AOT</b>: 実行時の XAML 解決ができないため、内包テーマのロードは
/// <c>No precompiled XAML found</c> で失敗する（ホストはアプリの XAML で ColorView テーマを
/// <c>StyleInclude</c> として取り込むこと。起動は継続し、本クラスは何もしない）。<br/>
/// <br/>
/// <b>適用パターン</b>: シングルトン相当の静的管理（適用済みフラグと<b>二重チェックロック</b>で二重適用を防止）。<br/>
/// <br/>
/// <see cref="ColorPickerFlyoutFactory"/> からのみ参照される（<see cref="ColorPickerControl"/> から分離）。
/// </remarks>
internal static class ColorViewThemeManager
{
    /// <summary>ColorView テーマ（Fluent.xaml）の適用済みフラグ（複数インスタンスで重複追加しない）。</summary>
    /// <remarks>
    /// ロック外の先判定で読み取るため volatile（二重チェックロックの可視性保証）。<br/>
    /// ロードに失敗した場合（Native AOT）も立てて再試行しない（毎回の例外生成を避ける）。
    /// </remarks>
    private static volatile bool s_applied;

    /// <summary>ColorView テーマの同期オブジェクト（並行した Flyout 表示からの二重追加を防止）。</summary>
    private static readonly object s_lock = new();

    /// <summary>ColorView（カラーピッカー）のテーマ URI。</summary>
    private static readonly Uri ColorViewThemeUri =
        new("avares://Avalonia.Controls.ColorPicker/Themes/Fluent/Fluent.xaml");

    /// <summary>ColorView テーマをアプリケーションへ自動適用する（デフォルト内包・明示指定を優先）。</summary>
    /// <remarks>
    /// <see cref="Application.Current"/> が null（未初期化）の場合は何もしない。<br/>
    /// テスト（ColorPickerControl.AvaloniaUI.Tests）が app=null の分岐を検証するため Core を分離する。
    /// </remarks>
    internal static void EnsureApplied()
        => EnsureAppliedCore(Application.Current);

    /// <summary>ColorView テーマを指定アプリケーションへ自動適用する（デフォルト内包・明示指定を優先）。</summary>
    /// <param name="app">対象アプリケーション。null の場合は何もしない。</param>
    /// <remarks>
    /// - 適用済みフラグをロック外で先判定し、true なら即座に終了（Flyout 表示毎のロック取得を回避）。<br/>
    /// - アプリ側で明示的に ColorView テーマ（StyleInclude）が指定されている場合はそれに従う<br/>
    ///   （内包テーマは追加しない。フラグは立てず、後付けの明示指定も尊重する）。<br/>
    /// - 未指定の場合のみ、内包テーマを一度だけ追加する（ロック内で再確認する二重チェックロック）。<br/>
    /// - デザインモード / Application 未初期化時は何もしない。
    /// </remarks>
    internal static void EnsureAppliedCore(Application? app)
    {
        if (app is null || Design.IsDesignMode)
        {
            return;
        }

        // 適用済みならロックを取得せず即座に終了する（高速パス。volatile 読み取り）
        if (s_applied)
        {
            return;
        }

        lock (s_lock)
        {
            // ロック内で再確認する（並行した初回呼び出しの二重適用を防止）
            ApplyOnceCore(app);
        }
    }

    /// <summary>ロック保持状態で ColorView テーマを一度だけ適用する。</summary>
    /// <param name="app">対象アプリケーション。</param>
    /// <remarks>
    /// 二重チェックロックのロック内処理（適用済みの再確認を含む）。<br/>
    /// テスト（ColorPickerControl.AvaloniaUI.Tests）が適用済み分岐を検証するため internal 公開する。<br/>
    /// 処理フロー:<br/>
    /// 1. 適用済みフラグを再確認し、適用済みなら何もしない。<br/>
    /// 2. アプリ側で明示的に ColorView テーマが指定されている場合はそれに従う。<br/>
    /// 3. 未指定の場合のみ、内包テーマを一度だけ追加してフラグを立てる。<br/>
    /// 4. 追加に失敗した場合（Native AOT の実行時 XAML 解決不可）もフラグを立てて再試行しない。
    /// </remarks>
    internal static void ApplyOnceCore(Application app)
    {
        if (s_applied)
        {
            return;
        }

        // アプリ側で明示的に ColorView テーマが指定されている場合はそれに従う（内包テーマは追加しない）
        if (app.Styles.Any(IsColorViewTheme))
        {
            return;
        }

        try
        {
            // 未指定の場合のみ、内包テーマをデフォルトとして一度だけ追加する
            app.Styles.Add((IStyle)AvaloniaXamlLoader.Load(ColorViewThemeUri, ColorViewThemeUri)!);
        }
        catch (XamlLoadException)
        {
            // Native AOT では実行時の XAML 解決ができない（No precompiled XAML found）。
            // ホスト（アプリの XAML）が ColorView テーマを取り込んでいる前提に任せ、再試行しない。
        }

        s_applied = true;
    }

    /// <summary>指定スタイルが ColorView テーマ（ColorPicker の StyleInclude）であるか判定する。</summary>
    /// <param name="style">判定対象のスタイル。</param>
    /// <returns>ColorView テーマの場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// アプリ側の明示指定（StyleInclude）を検出するための判定。<br/>
    /// テスト（ColorPickerControl.AvaloniaUI.Tests）が全分岐（Source null / 無関係テーマ / ColorPicker テーマ）を
    /// 検証するため internal 公開する。
    /// </remarks>
    internal static bool IsColorViewTheme(IStyle style)
        => style is StyleInclude { Source: { } src }
            && src.ToString().Contains("Avalonia.Controls.ColorPicker", StringComparison.Ordinal);
}
