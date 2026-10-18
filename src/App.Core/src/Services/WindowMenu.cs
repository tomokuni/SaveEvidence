namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>ウィンドウの標準メニュー（左上のアイコンの押下で出す）の項目を組み立てる。</summary>
/// <remarks>
/// <b>4 つの UI 版で同じメニューにする</b>ため、項目・並び・区切り線・有効/無効の判断をここへ集約する。<br/>
/// 表示（メニュー コントロール）と実行（ウィンドウ操作）は UI フレームワークに依存するため、各 UI 版が担う。<br/>
/// 並びは Windows の標準（元のサイズに戻す / 移動 / サイズ変更 / 最小化 / 最大化 / 閉じる）に合わせる。
/// </remarks>
public static class WindowMenu
{
    /// <summary>メニューの項目を取得する。</summary>
    /// <param name="isMaximized">最大化しているかどうか。</param>
    /// <param name="canResize">サイズ変更できるかどうか。</param>
    /// <param name="canMinimize">最小化できるかどうか。</param>
    /// <param name="canMaximize">最大化できるかどうか。</param>
    /// <returns>メニューの項目（表示順）。</returns>
    public static IReadOnlyList<WindowMenuEntry> Items(
        bool isMaximized,
        bool canResize = true,
        bool canMinimize = true,
        bool canMaximize = true)
        =>
        [
            new(WindowMenuAction.Restore, "元のサイズに戻す", isMaximized, IsSeparatorBefore: false),
            new(WindowMenuAction.Move, "移動", canResize, IsSeparatorBefore: false),
            new(WindowMenuAction.Size, "サイズ変更", canResize, IsSeparatorBefore: false),
            new(WindowMenuAction.Minimize, "最小化", canMinimize, IsSeparatorBefore: false),
            new(WindowMenuAction.Maximize, "最大化", canMaximize && !isMaximized, IsSeparatorBefore: false),
            new(WindowMenuAction.Close, "閉じる", true, IsSeparatorBefore: true),
        ];
}