namespace EsUtil.SaveEvidence.App.AvaloniaUI.Views.Pages;

/// <summary>ファイル一覧表示用のアイテム。</summary>
/// <remarks>
/// <b>表示に使うサムネイルだけを保持する</b>: 原寸の画像は保持しない。 原寸が必要な操作（ダブル クリックで開く）は
/// <see cref="FilePath"/> からファイルを読み込む。<br/>
/// 表示（枠・影・名前の行）は共通の <see cref="ThumbnailListItem"/> と <c>ThumbnailList</c> が担う。
/// </remarks>
public sealed class FileListItem : ThumbnailListItem
{
    /// <summary>ファイルの完全パス。</summary>
    public required string FilePath { get; init; }

    /// <summary>ファイル名（表示用）。</summary>
    public required string FileName { get; init; }

    /// <inheritdoc/>
    public override string Label => FileName;
}
