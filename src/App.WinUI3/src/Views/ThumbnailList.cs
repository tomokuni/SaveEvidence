using EsUtil.SaveEvidence.App.Core.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace EsUtil.SaveEvidence.App.WinUI3.Views;

/// <summary>サムネイル一覧（フォルダビュー / Undo 一覧）の共通コントロール。</summary>
/// <remarks>
/// <b>2 つのページで同じ見た目にする</b>: 枠・影・名前の行・ホバー・並べ方をここ 1 か所に置き、<br/>
/// ページは並べるサムネイル（表示要素）と名前を渡すだけにする。<br/>
/// <b>並べ方</b>: 行の高さを「その行の最大アイテム高さ」にし（<c>RowDefinition</c> を <c>Auto</c> にする）、<br/>
/// アイテムを行内で下端揃えにする（AvaloniaUI 版の <c>BottomAlignWrapPanel</c> と同じ）。<br/>
/// <b>ホバー</b>: ポインターの位置から対象の項目を決める（子要素のヒット テストに依存しない）。
/// </remarks>
internal sealed class ThumbnailList
{
    /// <summary>サムネイルを並べる格子。</summary>
    private readonly Grid _root = new();

    /// <summary>並べた項目（並び順）。</summary>
    private readonly List<ThumbnailTileView> _tiles = [];

    /// <summary>最後に並べた列数（同じなら組み直さない）。</summary>
    private int _columns = -1;

    /// <summary>最後に並べた件数（同じなら組み直さない）。</summary>
    private int _count = -1;

    /// <summary>1 件分の幅（余白を含む。 DIP）。</summary>
    private double _tileWidth;

    /// <summary>マウスが重なっている項目。</summary>
    private ThumbnailTileView? _hovered;

    /// <summary>一覧を生成する。</summary>
    public ThumbnailList()
    {
        Scroll = new ScrollViewer
        {
            Padding = new Thickness(0),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,

            // 一覧の下地は 4 UI 版で共通の色にする（影は半透明のため、下地が白いと薄く見える）。
            // 背景が未設定の要素はヒット テストの対象にならず、ポインターの位置も取得できない
            Background = new SolidColorBrush(AppStyles.ToColor(AppPalette.ListBackground(AppStyles.IsDarkTheme))),
            Content = _root,
        };

        _root.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

        // ホバーは一覧の領域で位置から判定する（子要素のヒット テストに依存しない）
        Scroll.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler(OnPointerMoved), handledEventsToo: true);
        Scroll.PointerExited += (_, _) => ApplyHover(null);

        Host = new Grid();

        Host.Children.Add(Scroll);
    }

    /// <summary>一覧の外枠（ページの行へ置く要素）を取得する。</summary>
    public Grid Host { get; }

    /// <summary>一覧のスクロール領域を取得する（可視領域の取得に使う）。</summary>
    public ScrollViewer Scroll { get; }

    /// <summary>並べた項目の要素を取得する（並び順。 生成の優先度の計算に使う）。</summary>
    public IReadOnlyList<FrameworkElement> Tiles => _tiles;

    /// <summary>一覧を空にする。</summary>
    public void Clear()
    {
        _root.Children.Clear();
        _tiles.Clear();
        _hovered = null;
        _columns = -1;
        _count = -1;
    }

    /// <summary>サムネイルを 1 件追加する。</summary>
    /// <param name="view">サムネイルの表示要素（高さは呼び出し側が決める）。</param>
    /// <param name="label">サムネイルの下へ表示する名前。</param>
    /// <param name="width">サムネイルの幅（DIP）。</param>
    /// <param name="toolTip">ツールヒント。</param>
    /// <param name="onActivated">ダブルクリック時の処理。</param>
    /// <param name="isCurrent">現在表示中の画像かどうか（true のとき名前を太字にする）。</param>
    /// <returns>追加した項目の要素。</returns>
    public FrameworkElement Add(
        FrameworkElement view, string label, double width, string toolTip, Action onActivated, bool isCurrent = false)
    {
        var tile = new ThumbnailTileView(view, label, width, isCurrent);
        ToolTipService.SetToolTip(tile, toolTip);
        tile.DoubleTapped += (_, _) => onActivated();

        _tileWidth = width + ThumbnailTileMetrics.ItemGap;
        _root.Children.Add(tile);
        _tiles.Add(tile);
        return tile;
    }

    /// <summary>一覧を表示領域の幅に合わせて並べ直す。</summary>
    /// <param name="availableWidth">表示領域の幅（DIP）。</param>
    public void Layout(double availableWidth)
    {
        var columns = WrapLayoutMath.Columns(availableWidth, _tileWidth, 0);
        if (columns == _columns && _tiles.Count == _count)
        {
            return;
        }

        _columns = columns;
        _count = _tiles.Count;

        var rows = (_root.Children.Count + columns - 1) / columns;
        _root.ColumnDefinitions.Clear();
        _root.RowDefinitions.Clear();
        for (var column = 0; column < columns; column++)
        {
            _root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        }

        for (var row = 0; row < rows; row++)
        {
            _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        for (var i = 0; i < _root.Children.Count; i++)
        {
            var child = (FrameworkElement)_root.Children[i];
            Grid.SetColumn(child, i % columns);
            Grid.SetRow(child, i / columns);
        }
    }

    /// <summary>ポインターの位置から対象の項目を決め、ホバーの見た目を切り替える。</summary>
    /// <param name="sender">イベントの発生元。</param>
    /// <param name="e">ポインターのイベント引数。</param>
    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var position = e.GetCurrentPoint(_root).Position;
        ThumbnailTileView? target = null;
        foreach (var tile in _tiles)
        {
            var origin = tile.TransformToVisual(_root).TransformPoint(new Point(0, 0));
            if (position.X >= origin.X && position.X <= origin.X + tile.ActualWidth
                && position.Y >= origin.Y && position.Y <= origin.Y + tile.ActualHeight)
            {
                target = tile;
                break;
            }
        }

        ApplyHover(target);
    }

    /// <summary>ホバー中の項目を差し替える。</summary>
    /// <param name="tile">新しくホバー中の項目。 null の場合は解除する。</param>
    private void ApplyHover(ThumbnailTileView? tile)
    {
        if (ReferenceEquals(_hovered, tile))
        {
            return;
        }

        _hovered?.SetHover(false);
        _hovered = tile;
        _hovered?.SetHover(true);
    }
}



