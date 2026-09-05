using Avalonia.Media;

namespace app.Views.Pages;

/// <summary>Undo 一覧表示用のアイテム。</summary>
public sealed class UndoListItem
{
    /// <summary>表示する画像（Undo / Redo スタックの画像または現在のプレビュー画像）。</summary>
    /// <remarks><see cref="IImage"/> として公開する（<see cref="PbBitmapAvaloniaImage"/> など。破棄は所有者に委ねる）。</remarks>
    public required IImage Thumbnail { get; init; }

    /// <summary>ラベル（例: "現在" / "Undo 1" / "Redo 1"）。</summary>
    public required string Label { get; init; }

    /// <summary>現在のキャプチャページに表示中の画像かどうか（true のとき青枠で強調）。</summary>
    public required bool IsCurrent { get; init; }

    /// <summary>表示幅（アイコンサイズから設定）。</summary>
    public required int ThumbnailWidth { get; init; }
}
