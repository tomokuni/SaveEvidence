namespace app.ViewModels;

/// <summary>表示モード（特大 / 大 / 中）の表示名とアイテム数テキストの単一所有（FolderView と Undo 一覧で共有）。</summary>
internal static class ViewModeDisplay
{
    /// <summary>表示モード名の一覧（0=特大, 1=大, 2=中）。</summary>
    private static readonly string[] s_names = ["特大", "大", "中"];

    /// <summary>表示モードインデックスに対応する表示名を取得する。</summary>
    /// <param name="index">表示モード（0=特大, 1=大, 2=中）。範囲外は空文字。</param>
    /// <returns>表示名（例: "大"）。範囲外は空文字。</returns>
    public static string GetText(int index)
        => (uint)index < s_names.Length ? s_names[index] : "";

    /// <summary>表示モードインデックスを有効範囲（0〜2）に収める。不正値は既定（大=1）にフォールバックする。</summary>
    /// <param name="index">表示モード（0=特大, 1=大, 2=中）。</param>
    /// <returns>有効範囲内のインデックス。範囲外は 1（大）。</returns>
    public static int ClampIndex(int index)
        => index is >= 0 and <= 2 ? index : 1;

    /// <summary>アイテム数テキストを生成する。</summary>
    /// <param name="count">アイテム数。</param>
    /// <returns>アイテム数テキスト（例: "10 個のアイテム"）。</returns>
    public static string GetItemCountText(int count)
        => $"{count} 個のアイテム";
}
