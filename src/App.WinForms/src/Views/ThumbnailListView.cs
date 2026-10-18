using System.ComponentModel;
using EsUtil.SaveEvidence.App.Core.Services;

namespace EsUtil.SaveEvidence.App.WinForms.Views;

/// <summary>サムネイルを格子状に並べる一覧（スクロール対応）。</summary>
/// <remarks>
/// <b>自前の並べ方</b>: 標準の一覧コントロールはアイコンの寸法を表示モードごとに変えられず、<br/>
/// 画像の縦横比も保てない。 項目は実コントロール（<see cref="ThumbnailItem"/>）として置き、<br/>
/// 本クラスが<b>格子へ並べる</b>（項目の高さが揃うため、行内でファイル名の位置が揃う）。<br/>
/// <b>スクロール</b>: <see cref="ScrollableControl.AutoScroll"/> を使い、仮想サイズを内容の高さに合わせる。<br/>
/// <b>可視範囲</b>: 背景の生成は「見えている項目が先」に実行するため、可視範囲を問い合わせできる。
/// </remarks>
internal sealed class ThumbnailListView : Panel
{
    /// <summary>並べた項目の順序（子コントロールの順序とは独立に保つ）。</summary>
    private readonly List<ThumbnailItem> _items = [];

    /// <summary>表示モードごとのアイコンの一辺（DIP）。</summary>
    private int _iconSize = 160;

    /// <summary>選択中の項目。</summary>
    private ThumbnailItem? _selected;

    /// <summary>一覧を生成する。</summary>
    public ThumbnailListView()
    {
        // 親の領域いっぱいに広げる（これがないと既定の 200x100 のまま残り、一覧が 1 列だけになる）
        Dock = DockStyle.Fill;
        AutoScroll = true;
        BackColor = AppStyles.ListBackground;
        DoubleBuffered = true;
        Padding = new Padding(2);
    }

    /// <summary>項目が 2 回押されたときに発生する。</summary>
    public event EventHandler? ItemActivated;

    /// <summary>選択が変わったときに発生する。</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>アイコンの一辺（DIP）を取得または設定する。</summary>
    /// <value>表示モード（特大 / 大 / 中）に対応するサイズ。 設定すると並べ直す。</value>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int IconSize
    {
        get => _iconSize;
        set
        {
            var size = Math.Max(32, value);
            if (_iconSize == size)
            {
                return;
            }

            _iconSize = size;
            foreach (var item in _items)
            {
                item.SetIconSize(size);
            }

            Relayout();
        }
    }

    /// <summary>並べている項目の一覧を取得する。</summary>
    public IReadOnlyList<ThumbnailItem> Items => _items;

    /// <summary>選択中の項目を取得する。</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ThumbnailItem? SelectedItem => _selected;

    /// <summary>項目を入れ替える（直前の項目は破棄する）。</summary>
    /// <param name="items">新しい項目の並び。</param>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> が null の場合。</exception>
    /// <remarks>破棄は <c>Controls.Clear</c> では行われないため、本メソッドが明示的に行う。</remarks>
    public void SetItems(IReadOnlyList<ThumbnailItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        SuspendLayout();
        try
        {
            Controls.Clear();
            foreach (var item in _items)
            {
                item.Dispose();
            }

            _items.Clear();
            _selected = null;
            _items.AddRange(items);

            foreach (var item in _items)
            {
                item.SetIconSize(_iconSize);
                item.Click += OnItemClick;
                item.DoubleClick += OnItemDoubleClick;
                item.SizeChanged += OnItemSizeChanged;
                Controls.Add(item);
            }

            Relayout();
        }
        finally
        {
            ResumeLayout(performLayout: true);
        }

        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>可視範囲（スクロール位置を考慮した表示中の上下端）を取得する。</summary>
    /// <returns>可視範囲の上端と下端（クライアント座標）。</returns>
    /// <remarks>背景の生成の優先度（見えている / 見えていない）の判定に使う。</remarks>
    public (double Top, double Bottom) ViewportRange()
    {
        var top = -AutoScrollPosition.Y;
        return (top, top + ClientSize.Height);
    }

    /// <summary>一覧が同時に表示できる項目数の目安を取得する。</summary>
    /// <returns>可視領域へ収まる項目数（最低 1）。</returns>
    /// <remarks>サムネイルのキャッシュ容量に使う（表示中の項目が破棄されないようにする）。</remarks>
    public int VisibleCapacity()
    {
        var item = ComputeItemSize();
        var columns = Math.Max(1, (ClientSize.Width - Padding.Horizontal) / Math.Max(1, item.Width));
        var rows = Math.Max(1, (ClientSize.Height - Padding.Vertical) / Math.Max(1, item.Height));
        return Math.Max(1, columns * rows);
    }

    /// <summary>項目を選択する。</summary>
    /// <param name="item">選択する項目。 null の場合は選択を解除する。</param>
    public void Select(ThumbnailItem? item)
    {
        if (ReferenceEquals(_selected, item))
        {
            return;
        }

        if (_selected is not null)
        {
            _selected.IsSelected = false;
            _selected.Invalidate();
        }

        _selected = item;
        if (_selected is not null)
        {
            _selected.IsSelected = true;
            _selected.Invalidate();
        }

        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>項目を格子へ並べる。</summary>
    /// <remarks>
    /// 行の高さは固定値とせず<b>その行の最大アイテム高さ</b>で決め、アイテムは行内で下端を揃えて配置する<br/>
    /// （画像の縦横比によって高さが変わるため、名前の 1 文字目の高さ位置が行内で揃う）。<br/>
    /// AvaloniaUI 版の <c>BottomAlignWrapPanel</c> と同じ並べ方。
    /// </remarks>
    public void Relayout()
    {
        var size = ComputeItemSize();
        var available = Math.Max(1, ClientSize.Width - Padding.Horizontal);
        var columns = Math.Max(1, available / Math.Max(1, size.Width));

        SuspendLayout();
        try
        {
            var y = Padding.Top;
            for (var start = 0; start < _items.Count; start += columns)
            {
                var end = Math.Min(start + columns, _items.Count);

                // 行の高さは行内の最大アイテム高さ
                var rowHeight = 0;
                for (var i = start; i < end; i++)
                {
                    rowHeight = Math.Max(rowHeight, _items[i].TileHeight);
                }

                for (var i = start; i < end; i++)
                {
                    var item = _items[i];
                    item.Location = new Point(
                        Padding.Left + ((i - start) * size.Width),
                        y + rowHeight - item.TileHeight);
                }

                y += rowHeight;
            }

            AutoScrollMinSize = new Size(0, (y - Padding.Top) + Padding.Vertical);
        }
        finally
        {
            ResumeLayout(performLayout: true);
        }
    }

    /// <summary>項目の幅と高さの目安を求める。</summary>
    /// <returns>項目の寸法（アイコンの一辺 + 影の余白 + 名前の行）。</returns>
    /// <remarks>実際の高さは画像の縦横比で変わるため、目安として正方形の寸法を返す。</remarks>
    private Size ComputeItemSize()
        => new(ThumbnailItem.WidthFor(_iconSize), _iconSize + (int)ThumbnailTileMetrics.LabelHeight);

    /// <summary>項目の大きさが変わったときに並べ直す。</summary>
    /// <param name="sender">大きさが変わった項目。</param>
    /// <param name="e">イベント引数。</param>
    private void OnItemSizeChanged(object? sender, EventArgs e)
        => Relayout();

    /// <summary>項目が押されたときに選択を移す。</summary>
    /// <param name="sender">押された項目。</param>
    /// <param name="e">イベント引数。</param>
    private void OnItemClick(object? sender, EventArgs e)
    {
        if (sender is ThumbnailItem item)
        {
            Select(item);
        }
    }

    /// <summary>項目が 2 回押されたときに通知する。</summary>
    /// <param name="sender">2 回押された項目。</param>
    /// <param name="e">イベント引数。</param>
    private void OnItemDoubleClick(object? sender, EventArgs e)
    {
        if (sender is ThumbnailItem item)
        {
            Select(item);
            ItemActivated?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>サイズが変わったときに並べ直す。</summary>
    /// <param name="e">イベント引数。</param>
    protected override void OnClientSizeChanged(EventArgs e)
    {
        base.OnClientSizeChanged(e);
        Relayout();
    }
}

