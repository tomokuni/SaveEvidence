namespace Estable.SaveEvidence.Enum;

/// <summary>メインウィンドウのナビゲーションページを表す列挙型。</summary>
/// <remarks>
/// <see cref="Views.MainWindow.NavigateTo"/> の遷移先を識別するために使用する。<br/>
/// ページ追加時の index ずれによる誤遷移を防ぐため、型安全な enum で管理する。
/// </remarks>
public enum AppPage
{
    /// <summary>キャプチャ（画像プレビュー）ページ。</summary>
    Capture = 0,

    /// <summary>フォルダビューページ。</summary>
    FolderView = 1,

    /// <summary>Undo 一覧ページ。</summary>
    Undo = 2,

    /// <summary>設定ページ。</summary>
    Settings = 3,
}
