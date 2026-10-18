namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>ウィンドウの標準メニューの 1 項目。</summary>
/// <param name="Action">実行する操作。</param>
/// <param name="Label">表示名。</param>
/// <param name="IsEnabled">選択できるかどうか。</param>
/// <param name="IsSeparatorBefore">この項目の前に区切り線を置くかどうか。</param>
public readonly record struct WindowMenuEntry(
    WindowMenuAction Action,
    string Label,
    bool IsEnabled,
    bool IsSeparatorBefore);