using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using EsUtil.SaveEvidence.App.AvaloniaUI.Views.Pages;
using EsUtil.SaveEvidence.App.Core.Services;

namespace EsUtil.SaveEvidence.App.AvaloniaUI.Controls;

/// <summary>サムネイル一覧（フォルダビュー / Undo 一覧）の共通コントロール。</summary>
/// <remarks>
/// <b>2 つのページで同じ見た目にする</b>: 枠・影・名前の行・ホバーの表現をここ 1 か所に置き、<br/>
/// ページは並べる項目（<see cref="ThumbnailListItem"/>）を渡すだけにする。<br/>
/// <b>並べ方</b>: <see cref="BottomAlignWrapPanel"/> が行の高さを「その行の最大アイテム高さ」に決め、<br/>
/// アイテムを下端揃えで並べる（AvaloniaUI 版の標準の見た目）。<br/>
/// <b>寸法</b>: 枠・影・名前の行は <see cref="ThumbnailTileMetrics"/>（App.Core）が単一所有する。<br/>
/// <b>ホバー</b>: 一覧の項目の背景（Fluent の <c>ListBoxItem</c>）に加えて、枠をアクセント色にし、影を消す。
/// </remarks>
public sealed class ThumbnailList : ListBox
{
    /// <summary>テーマの適用に使う型を取得する。</summary>
    /// <value><see cref="ListBox"/>。 派生クラスでも標準のテンプレート（スクロール・項目の生成）を使う。</value>
    /// <remarks>上書きしないと派生クラス自身の型でテーマを探し、テンプレートが見つからず項目が表示されない。</remarks>
    protected override Type StyleKeyOverride => typeof(ListBox);

    /// <summary>コントロールを生成する。</summary>
    public ThumbnailList()
    {
        SelectionMode = SelectionMode.Single;

        // 行の高さは行内の最大アイテム高さ・アイテムは下端揃え（標準の WrapPanel では行内が上端揃えになる）
        ItemsPanel = new FuncTemplate<Panel?>(static () => new BottomAlignWrapPanel());

        ItemTemplate = new FuncDataTemplate<ThumbnailListItem>(
            static (item, _) => item is null ? null : BuildTile(item), supportsRecycling: true);
    }

    /// <summary>影の定義（幅・濃さの変化は <see cref="ShadowPlan"/>（App.Core）が単一所有する）。</summary>
    /// <remarks>
    /// AvaloniaUI 版の <c>BoxShadow</c> が全 UI 版の影の基準であり、計画はこの値を写したものである。<br/>
    /// 位置をずらさない（4 辺の影を同じ濃さにする。 ずらすと上辺だけ薄くなり、画像との間に白っぽい隙間が見える）。
    /// </remarks>
    private static BoxShadows Shadow => ShadowFactory.Create();

    /// <summary>サムネイル 1 件分の要素を生成する。</summary>
    /// <param name="item">並べる項目。</param>
    /// <returns>サムネイル 1 件分の要素。</returns>
    /// <remarks>
    /// <b>画像の周りは影だけ</b>: 枠線・下地・角丸は描かない（影は画像へ付ける）。<br/>
    /// ホバー中は影を消す（帯は一覧の項目（<c>ListBoxItem</c>）が描く）。
    /// </remarks>
    private static Control BuildTile(ThumbnailListItem item)
    {
        var image = new Image
        {
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            // 幅だけを与えて縦横比のまま縮小する（高さは画像の縦横比で決まる）
            Width = item.ThumbnailWidth,
            Source = item.Thumbnail,
        };

        // バインド（ReflectionBinding）は AOT / トリミングの解析が拒否する（IL2026 / IL3050）ため、
        // 後から設定されるサムネイルは PropertyChanged の購読で反映する
        item.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(ThumbnailListItem.Thumbnail), StringComparison.Ordinal))
            {
                image.Source = item.Thumbnail;
            }
        };

        var frame = new Border
        {
            BoxShadow = Shadow,
            Child = image,
        };

        frame.PointerEntered += (_, _) => frame.BoxShadow = default;
        frame.PointerExited += (_, _) => frame.BoxShadow = Shadow;

        var label = new TextBlock
        {
            Text = item.Label,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            FontSize = ThumbnailTileMetrics.LabelFontSize,
            FontWeight = item.LabelFontWeight,
            Height = ThumbnailTileMetrics.LabelHeight,
            Padding = new Thickness(
                ThumbnailTileMetrics.LabelHorizontalPadding, 1, ThumbnailTileMetrics.LabelHorizontalPadding, 1),
        };

        var tile = new StackPanel { Orientation = Orientation.Vertical, Width = item.ThumbnailWidth };
        tile.Children.Add(frame);
        tile.Children.Add(label);

        // 画像の外側の余白（AvaloniaUI 版の ListBoxItem の余白と合わせる）
        return new Border
        {
            Margin = new Thickness(ThumbnailTileMetrics.ItemMargin),
            Child = tile,
        };
    }
}


