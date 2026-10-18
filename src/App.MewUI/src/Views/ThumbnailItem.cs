using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using EsUtil.SaveEvidence.App.Core.Services;
using MewUIColor = Aprillz.MewUI.Color;

namespace EsUtil.SaveEvidence.App.MewUI.Views;

/// <summary>一覧（フォルダビュー / Undo 一覧）のサムネイル 1 件分の要素を組み立てる。</summary>
/// <remarks>
/// <b>画像の周りは影だけ</b>: 枠線・下地・角丸は描かない。 影は画像のビュー（<see cref="ThumbnailView"/>）が<br/>
/// <see cref="ShadowPlan"/>（App.Core）に従って描くため、本クラスは高さをそろえるだけにする。<br/>
/// <b>構成</b>: 画像（影を含む）+ ラベルを縦に並べ、外側を <see cref="Border"/> で包んでホバー時の帯を描く。<br/>
/// <b>ホバー</b>: 影の配下では <c>StateTrigger</c>（ホバー）が効かない（実測）ため、<br/>
/// <see cref="UIElement.MouseEnter"/> / <see cref="UIElement.MouseLeave"/> で帯を切り替える。
/// </remarks>
internal static class ThumbnailItem
{
    /// <summary>サムネイルの画像とラベルを縦に並べた要素を生成する。</summary>
    /// <param name="view">画像のビュー（影を含む）。</param>
    /// <param name="label">ファイル名またはラベルの要素（高さ・幅は呼び出し側が指定する）。</param>
    /// <param name="onDoubleClick">ダブルクリック時の処理。</param>
    /// <returns>アイテムの要素。</returns>
    /// <remarks>
    /// 1. 画像とラベルを縦に並べる（間隔は 0。 影は画像の内側の余白として確保済み）。<br/>
    /// 2. ホバー中は項目全体へ薄い帯を表示する（AvaloniaUI 版の一覧の項目と同じ）。
    /// </remarks>
    public static FrameworkElement Create(ThumbnailView view, Element label, Action onDoubleClick)
    {
        var item = new StackPanel()
            .Vertical()
            .Spacing(0)
            .Children(view, label);

        // ホバー帯を描くために外側を Border で包む（Panel は背景を持てない）
        var host = new Border { Child = item };

        host.MouseEnter += () => host.Background = AppStyles.CurrentThumbnailHoverBackground;
        host.MouseLeave += () => host.Background = MewUIColor.Transparent;

        // ダブルクリックはサムネイルの外（ラベル上）でも受け付ける
        host.OnMouseDoubleClick(_ => onDoubleClick());

        return host;
    }
}