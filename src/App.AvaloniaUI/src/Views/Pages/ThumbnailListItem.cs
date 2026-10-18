using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace EsUtil.SaveEvidence.App.AvaloniaUI.Views.Pages;

/// <summary>サムネイル一覧（フォルダビュー / Undo 一覧）に並べる 1 件。</summary>
/// <remarks>
/// <b>2 つのページで同じ見た目にする</b>: 一覧は <c>ThumbnailList</c>（共通のコントロール）が描くため、<br/>
/// ページごとに表示の作りを持たない。 本クラスは表示に必要な値だけを持つ。<br/>
/// <b>サムネイルは後から設定される</b>: 生成は背景で行い、生成が終わった 1 件から <see cref="Thumbnail"/> へ設定する<br/>
/// （生成が終わるまでは代替画像を表示する）。 反映のため本クラスは <see cref="ObservableObject"/> を継承する。
/// </remarks>
public abstract partial class ThumbnailListItem : ObservableObject
{
    /// <summary>サムネイルの下へ表示する名前。</summary>
    public virtual string Label { get; init; } = string.Empty;

    /// <summary>表示幅（アイコンサイズから設定）。</summary>
    public required int ThumbnailWidth { get; init; }

    /// <summary>現在のキャプチャページに表示中の画像かどうか。</summary>
    /// <value>true のときは名前を太字にする（枠・影は変えない）。</value>
    public virtual bool IsCurrent { get; init; }

    /// <summary>名前の太さを取得する。</summary>
    /// <value>現在表示中の画像のときは太字。</value>
    /// <remarks>現在表示中の画像も枠・影・ホバーは他と同じにするため、区別は名前の太さだけで行う。</remarks>
    public FontWeight LabelFontWeight => IsCurrent ? FontWeight.Bold : FontWeight.Normal;

    /// <summary>サムネイル画像（生成が終わるまでは代替画像）。</summary>
    [ObservableProperty]
    private IImage? _thumbnail;
}



