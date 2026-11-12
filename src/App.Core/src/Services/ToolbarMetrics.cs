namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>ツールバーの文字サイズ（4 つの UI 版で揃えるための単一所有者）。</summary>
/// <remarks>
/// MewUI の既定（12 DIP）を基準にする。 AvaloniaUI と WinUI 3 のテーマ既定は 14 DIP のため、<br/>
/// 値を明示しないとテキスト入力欄とボタンの文字が大きく見える。<br/>
/// WinForms の <c>Font</c> はポイント単位のため、使う側で <c>DIP × 72 ÷ 96</c> に換算する。
/// </remarks>
public static class ToolbarMetrics
{
    /// <summary>ツールバー ボタンの高さ（DIP）。</summary>
    public const double ButtonHeight = 32;

    /// <summary>ツールバー アイコンの一辺（DIP）。</summary>
    public const double IconSize = NavPanelMetrics.IconSize;

    /// <summary>ツールバー ボタンの水平内側余白（DIP）。</summary>
    public const double PaddingX = 8;

    /// <summary>ツールバー ボタンの垂直内側余白（DIP）。</summary>
    public const double PaddingY = 4;

    /// <summary>ツールバー アイコン ボタンの角丸（DIP）。</summary>
    public const double CornerRadius = AppPalette.ToolbarButtonCornerRadius;

    /// <summary>ツールバーの文字サイズ（DIP）。</summary>
    public const double FontSize = 12;

    /// <summary>自動保存のトグルの高さ（DIP。 4 UI 版で同じ値を使う）。</summary>
    /// <remarks>
    /// 各 UI のボタンの既定の最小高は 16〜32 DIP とばらつくため、高さを明示して揃える。<br/>
    /// 値は MewUI で表示されていた高さ（ボタンの既定の最小高 28 DIP）の 80% とする。
    /// </remarks>
    public const double AutoSaveHeight = 22.4;
}
