using System.ComponentModel;

namespace EsUtil.SaveEvidence.Enum;

/// <summary>アプリ全体のテーマモード。</summary>
/// <remarks><c>RequestedThemeVariant</c> へ反映される。<c>System</c> は OS の設定に追従する。</remarks>
public enum ThemeMode
{
    /// <summary>ライトテーマ。</summary>
    [Description("ライト")]
    Light = 0,

    /// <summary>ダークテーマ。</summary>
    [Description("ダーク")]
    Dark = 1,

    /// <summary>OS の設定（ダーク / ライト）に追従する。</summary>
    [Description("システム")]
    System = 2,
}
