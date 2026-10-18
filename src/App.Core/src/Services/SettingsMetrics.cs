namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>設定ページの寸法と共通の文言（4 つの UI 版で揃えるための単一所有者）。</summary>
/// <remarks>
/// 見た目を UI 版ごとに書き分けると、文字の大きさや補足の有無がずれる。<br/>
/// 文字サイズは AvaloniaUI 版（Fluent テーマの既定 14 DIP）を基準にし、他 UI 版が同じ値を使う。<br/>
/// <br/>
/// <b>注意</b>: 文字サイズは DIP で保持する。 WinForms の <c>Font</c> はポイント単位のため、<br/>
/// 使う側で <c>DIP × 72 ÷ 96</c> に換算する。
/// </remarks>
public static class SettingsMetrics
{
    /// <summary>設定ページの文字サイズ（DIP）。 AvaloniaUI 版の既定と同じ。</summary>
    public const double FontSize = 14;

    /// <summary>補足（例・説明）の文字サイズ（DIP）。</summary>
    public const double HintFontSize = 11;

    /// <summary>保存先フォルダの入力欄の下へ出す補足の文言。</summary>
    /// <remarks>
    /// プレースホルダの説明であり、解決規則は <c>MainViewModel.ResolveSaveFolderPath</c> が単一所有する。<br/>
    /// 文言は 4 つの UI 版で共通にする（UI ごとに書き分けない）。
    /// </remarks>
    public const string SaveFolderHint =
        "例: {MyPictures}\\SaveEvidence（{MyPictures} はピクチャフォルダへ展開されます）";
}
