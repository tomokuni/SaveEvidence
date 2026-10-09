using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace EsUtil.SaveEvidence.App.AvaloniaUI.Views.Pages;

/// <summary>ファイル一覧表示用のアイテム。</summary>
/// <remarks>
/// <b>表示に使うサムネイルだけを保持する</b>: 原寸の画像は保持しない。 原寸が必要な操作（ダブル クリックで開く）は
/// <see cref="FilePath"/> からファイルを読み込む。<br/>
/// <b>サムネイルは後から設定される</b>: 生成は背景で行い、生成が終わった 1 件から <see cref="Thumbnail"/> へ設定する
/// （生成が終わるまでは代替画像を表示する）。 反映のため本クラスは <see cref="ObservableObject"/> を継承する。
/// </remarks>
public sealed partial class FileListItem : ObservableObject
{
    /// <summary>ファイルの完全パス。</summary>
    public required string FilePath { get; init; }

    /// <summary>ファイル名（表示用）。</summary>
    public required string FileName { get; init; }

    /// <summary>表示幅（アイコンサイズから設定）。</summary>
    public required int ThumbnailWidth { get; init; }

    /// <summary>サムネイル画像（生成が終わるまでは代替画像）。</summary>
    [ObservableProperty]
    private Bitmap? _thumbnail;
}
