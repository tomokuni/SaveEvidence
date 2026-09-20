using Avalonia.Media.Imaging;

namespace Estable.SaveEvidence.Views.Pages;

/// <summary>ファイル一覧表示用のアイテム。</summary>
public sealed class FileListItem
{
    /// <summary>ファイルの完全パス。</summary>
    public required string FilePath { get; init; }

    /// <summary>ファイル名（表示用）。</summary>
    public required string FileName { get; init; }

    /// <summary>サムネイル画像。</summary>
    public Bitmap? Thumbnail { get; init; }

    /// <summary>表示幅（アイコンサイズから設定）。</summary>
    public int ThumbnailWidth { get; init; }
}
