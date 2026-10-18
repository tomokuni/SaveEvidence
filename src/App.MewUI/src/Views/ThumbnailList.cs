using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using EsUtil.SaveEvidence.App.Core.Services;

namespace EsUtil.SaveEvidence.App.MewUI.Views;

/// <summary>サムネイル一覧（フォルダビュー / Undo 一覧）の共通コントロール。</summary>
/// <remarks>
/// <b>2 つのページで同じ見た目にする</b>: 枠・影・名前の行・ホバー・並べ方をここ 1 か所に置き、<br/>
/// ページは並べるサムネイル（表示要素）と名前を渡すだけにする。<br/>
/// <b>並べ方</b>: <see cref="BottomAlignWrapPanel"/> が行の高さを「その行の最大アイテム高さ」に決め、<br/>
/// アイテムを下端揃えで並べる（AvaloniaUI 版と同じ）。<br/>
/// <b>寸法</b>: 枠・影・名前の行・間隔は <see cref="ThumbnailTileMetrics"/>（App.Core）が単一所有する。
/// </remarks>
internal sealed class ThumbnailList
{
    /// <summary>サムネイルを並べる折り返しパネル。</summary>
    private readonly BottomAlignWrapPanel _panel;

    /// <summary>並べた項目（並び順）。</summary>
    private readonly List<Element> _items = [];

    /// <summary>一覧を生成する。</summary>
    /// <param name="spacing">アイテム間の間隔（DIP）。 影が確保する余白を差し引いた値。</param>
    public ThumbnailList(double spacing)
    {
        // 影は画像のビューが内側に確保するため、一覧の余白は枠の外側の間隔だけにする
        _panel = new BottomAlignWrapPanel(spacing) { Padding = new Thickness(ThumbnailTileMetrics.ItemMargin) };
    }

    /// <summary>一覧のパネル（スクロール領域へ置く要素）を取得する。</summary>
    public Element Panel => _panel;

    /// <summary>並べた項目の要素を取得する（並び順。 生成の優先度の計算に使う）。</summary>
    public IReadOnlyList<Element> Items => _items;

    /// <summary>一覧を空にする。</summary>
    public void Clear()
    {
        _panel.Clear();
        _items.Clear();
    }

    /// <summary>サムネイルを 1 件追加する。</summary>
    /// <param name="view">サムネイルの表示要素。</param>
    /// <param name="label">サムネイルの下へ表示する名前。</param>
    /// <param name="iconSize">サムネイルの一辺（DIP）。</param>
    /// <param name="isCurrent">現在表示中の画像かどうか（true のとき名前を太字にする）。</param>
    /// <param name="onActivated">ダブルクリック時の処理。</param>
    /// <returns>追加した項目の要素。</returns>
    public Element Add(ThumbnailView view, string label, int iconSize, bool isCurrent, Action onActivated)
    {
        // ラベルは画像（影の余白を含む）と同じ幅にし、文字を画像の中央へ揃える
        var insets = ShadowPlan.Insets;
        var caption = new Label()
            .Text(label)
            .TextAlignment(TextAlignment.Center)
            .VerticalTextAlignment(TextAlignment.Top)
            .Width(iconSize + insets.Left + insets.Right)
            .Height(ThumbnailTileMetrics.LabelHeight)
            .FontSize(ThumbnailTileMetrics.LabelFontSize);
        caption.Padding = new Thickness(
            ThumbnailTileMetrics.LabelHorizontalPadding, 1, ThumbnailTileMetrics.LabelHorizontalPadding, 1);
        if (isCurrent)
        {
            caption.Bold();
        }

        var item = ThumbnailItem.Create(view, caption, onActivated);
        _panel.Add(item);
        _items.Add(item);
        return item;
    }
}
