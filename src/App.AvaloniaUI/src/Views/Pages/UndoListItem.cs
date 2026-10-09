using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace EsUtil.SaveEvidence.App.AvaloniaUI.Views.Pages;

/// <summary>Undo 一覧表示用のアイテム。</summary>
/// <remarks>
/// <b>サムネイルは後から設定される</b>: 表示用の射影（アイコンサイズへの縮小）の生成は 背景で行い、
/// 生成が終わった 1 件から <see cref="Thumbnail"/> へ設定する（生成が終わるまでは 代替画像を表示する）。
/// 反映のため本クラスは <see cref="ObservableObject"/> を継承する。
/// </remarks>
public sealed partial class UndoListItem : ObservableObject
{
    /// <summary>表示する画像（生成が終わるまでは代替画像）。</summary>
    /// <remarks><see cref="IImage"/> として公開する（<see cref="EsUtil.SaveEvidence.Platform.AvaloniaUI.Imaging.EsBitmapAvaloniaAdapter"/> など。
    /// 画像の所有は生成側が保持し、アダプタの破棄は一覧を保持する側が行う）。</remarks>
    [ObservableProperty]
    private IImage? _thumbnail;

    /// <summary>ラベル（例: "現在" / "Undo 1" / "Redo 1"）。</summary>
    public required string Label { get; init; }

    /// <summary>現在のキャプチャページに表示中の画像かどうか（true のとき青枠で強調）。</summary>
    public required bool IsCurrent { get; init; }

    /// <summary>表示幅（アイコンサイズから設定）。</summary>
    public required int ThumbnailWidth { get; init; }
}
