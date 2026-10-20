namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>ツールバーの文字サイズ（4 つの UI 版で揃えるための単一所有者）。</summary>
/// <remarks>
/// MewUI の既定（12 DIP）を基準にする。 AvaloniaUI と WinUI 3 のテーマ既定は 14 DIP のため、<br/>
/// 値を明示しないとテキスト入力欄とボタンの文字が大きく見える。<br/>
/// WinForms の <c>Font</c> はポイント単位のため、使う側で <c>DIP × 72 ÷ 96</c> に換算する。
/// </remarks>
public static class ToolbarMetrics
{
    /// <summary>ツールバーの文字サイズ（DIP）。</summary>
    public const double FontSize = 12;
}
