namespace EsUtil.SaveEvidence.App.AvaloniaUI.Views.Pages;

/// <summary>Undo 一覧表示用のアイテム。</summary>
/// <remarks>
/// 表示（枠・影・名前の行）は共通の <see cref="ThumbnailListItem"/> と <c>ThumbnailList</c> が担う。<br/>
/// 現在表示中の画像は名前を太字にして区別する（枠・影・ホバーはフォルダビューと同一）。
/// </remarks>
public sealed class UndoListItem : ThumbnailListItem
{
    /// <inheritdoc/>
    public override bool IsCurrent { get; init; }
}

