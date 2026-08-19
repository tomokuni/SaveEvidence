using Avalonia.Media.Imaging;

namespace app.Views.Pages;

/// <summary>Undo 一覧表示用のアイテム。</summary>
public sealed class UndoListItem
{
    /// <summary>表示する画像（Undo / Redo スタックの Bitmap または現在のプレビュー画像）。</summary>
    public required Bitmap Thumbnail { get; init; }

    /// <summary>ラベル（例: "現在" / "Undo 1" / "Redo 1"）。</summary>
    public required string Label { get; init; }

    /// <summary>現在のキャプチャページに表示中の画像かどうか（true のとき青枠で強調）。</summary>
    public required bool IsCurrent { get; init; }

    /// <summary>表示幅（アイコンサイズから設定）。</summary>
    public required int ThumbnailWidth { get; init; }
}
