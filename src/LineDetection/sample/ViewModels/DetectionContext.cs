using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Estable.AvaloniaUI.LineDetection;
using Estable.AvaloniaUI.Platform.Imaging;
using System.Collections.ObjectModel;

namespace LineDetectionSample.ViewModels;

/// <summary>
/// 線分検出・データ検証ページで共有する「検出コンテキスト」。
/// 表示中の画像・検出された線分一覧・ルーペ・ポインタ状態を一元的に保持する。
/// </summary>
/// <remarks>
/// 「線分検出」ページで貼り付けた画像と検出結果を、「データ検証」ページでも同じ内容で表示できるよう、
/// 両ページが同一の <see cref="DetectionContext"/> を参照する。<br/>
/// 画像上のホバーは <see cref="UpdateHover"/>、一覧のホバーは <see cref="SetLineHover"/> で
/// 同一の <see cref="LineItem.IsHovered"/> を切り替え、線分と一覧を連動して強調表示する。<br/>
/// マウス位置はルーペ（LoupeControl）へ <see cref="LoupeCenter"/> として渡し、
/// ホバー中または選択中の線分をルーペ内に破線表示する。<br/>
/// クリック（画像上・一覧）は <see cref="ToggleSelect"/> で選択トグルし、
/// 選択中の線分はオレンジ色で強調表示する（再クリックで解除・垂直 / 水平それぞれ 2 つまで）。
/// </remarks>
public sealed partial class DetectionContext : ObservableObject
{
    /// <summary>線分をホバーとみなす最大距離（画像ピクセル）。</summary>
    private const double HoverDistance = 8.0;

    /// <summary>オレンジ強調（選択）できる線分の上限（垂直・水平それぞれ）。</summary>
    private const int MaxSelectionPerOrientation = 2;

    /// <summary>ホバー中の線分のルーペ内表示色（赤）。</summary>
    private static readonly IBrush s_hoverOverlayBrush = new SolidColorBrush(Color.Parse("#FF3B30"));

    /// <summary>選択中の線分のルーペ内表示色（オレンジ）。</summary>
    private static readonly IBrush s_selectedOverlayBrush = new SolidColorBrush(Color.Parse("#FF8C00"));

    /// <summary>表示中の画像（クリップボードから貼り付け）。</summary>
    [ObservableProperty]
    private Bitmap? _image;

    /// <summary>ステータスバーの表示テキスト。</summary>
    [ObservableProperty]
    private string _statusText = "クリップボードから画像を貼り付けてください。";

    /// <summary>ルーペの拡大中心（マウス位置・画像ピクセル座標）。</summary>
    [ObservableProperty]
    private Point _loupeCenter;

    /// <summary>ルーペの表示状態（画像あり && マウスが画像上）。</summary>
    [ObservableProperty]
    private bool _isLoupeVisible;

    /// <summary>マウス位置の画像ドメイン座標の表示テキスト（ステータスバー表示用）。</summary>
    [ObservableProperty]
    private string _pointerDomainText = "";

    /// <summary>画像オーバーレイ表示用の線分一覧（垂直 → 水平の順）。</summary>
    public ObservableCollection<LineItem> OverlayLines { get; } = [];

    /// <summary>垂直線分の一覧（左ペイン表示用・スコア降順）。</summary>
    public IReadOnlyList<LineItem> VerticalLines { get; private set; } = [];

    /// <summary>水平線分の一覧（左ペイン表示用・スコア降順）。</summary>
    public IReadOnlyList<LineItem> HorizontalLines { get; private set; } = [];

    /// <summary>ルーペ内に破線表示する線分一覧（選択中を優先し、なければホバー中）。</summary>
    public IReadOnlyList<LineItem> LoupeOverlayLines { get; private set; } = [];

    /// <summary>選択順の追跡リスト（先頭が最も古い選択）。上限超過時の解除対象の判定に使用する。</summary>
    private readonly List<LineItem> _selectionOrder = [];

    /// <summary>画像が設定済みかどうか（画像プレースホルダーの表示切替に使用）。</summary>
    public bool HasImage => Image is not null;

    /// <summary>選択中の検出方式。</summary>
    /// <remarks>使用側（ページ ViewModel）が設定する。既定値は null。</remarks>
    public LineDetectorOption? SelectedDetector { get; set; }

    /// <summary>垂直線分のグループ見出しテキスト。</summary>
    public string VerticalCountText => $"垂直線分（{VerticalLines.Count}）";

    /// <summary>水平線分のグループ見出しテキスト。</summary>
    public string HorizontalCountText => $"水平線分（{HorizontalLines.Count}）";

    /// <summary>選択中の線分の表示テキスト（一覧・改行区切り）。未選択時は「なし」。</summary>
    public string SelectedLineText { get; private set; } = "選択中: なし";

    /// <summary>画像の変更時に表示状態を更新する。</summary>
    partial void OnImageChanged(Bitmap? value)
    {
        OnPropertyChanged(nameof(HasImage));
        IsLoupeVisible = false;
        LoupeOverlayLines = [];
        OnPropertyChanged(nameof(LoupeOverlayLines));
        PointerDomainText = "";
    }

    /// <summary>クリップボードから取得した画像を表示画像として設定し、選択中の方式で再検出する。</summary>
    /// <param name="bitmap">クリップボードから取得した画像。null の場合は何もしない。</param>
    /// <remarks>画像の所有権は本コンテキストへ移る（次回の画像更新時に <see cref="Image"/> を破棄する）。</remarks>
    public void SetPastedImage(Bitmap? bitmap)
    {
        if (bitmap is null)
        {
            StatusText = "クリップボードに画像がありません。";
            return;
        }

        Image?.Dispose();
        Image = bitmap;
        Detect();
    }

    /// <summary>選択中の検出方式で辺（線分）を検出する。</summary>
    public void Detect()
    {
        if (Image is null)
        {
            StatusText = "画像がありません。クリップボードから貼り付けてください。";
            return;
        }

        if (SelectedDetector is not { } detector)
        {
            StatusText = "検出方式が選択されていません。";
            return;
        }

        // 検出器は PbBitmap を入力として受け取る（Bitmap → PbBitmap 変換）
        using var unified = Image.ToPbBitmap();
        var results = detector.Detector.Detect(unified);

        // モデル依存方式のエラー（モデル未配置等）をステータスへ反映する
        if (detector.Detector.ErrorMessage is { } error)
        {
            StatusText = error;
            UpdateLines([]);
            return;
        }

        // 垂直・水平のグループに分けて更新（スコア降順・垂直を先に表示）
        var verticals = results
            .Where(r => r.Orientation == LineOrientation.Vertical)
            .OrderByDescending(r => r.Score)
            .ToList();
        var horizontals = results
            .Where(r => r.Orientation == LineOrientation.Horizontal)
            .OrderByDescending(r => r.Score)
            .ToList();

        var allLines = new List<LineItem>(verticals.Count + horizontals.Count);
        allLines.AddRange(verticals.Select((r, i) => new LineItem(r, i)));
        allLines.AddRange(horizontals.Select((r, i) => new LineItem(r, i + verticals.Count)));

        UpdateLines(allLines);

        StatusText = results.Count > 0
            ? $"{detector.Name}: 垂直線分 {verticals.Count} 本・水平線分 {horizontals.Count} 本 を検出"
            : $"{detector.Name}: 線分を検出できませんでした";
    }

    /// <summary>検出結果の線分一覧を各グループとオーバーレイへ反映する。</summary>
    /// <param name="lines">検出結果の線分アイテム一覧（垂直 → 水平の順でスコア降順）。</param>
    private void UpdateLines(List<LineItem> lines)
    {
        OverlayLines.Clear();
        foreach (var line in lines)
        {
            OverlayLines.Add(line);
        }

        // グループは入力の並び（垂直 → 水平）を保ったまま一括で参照する（追加の Where 反復を避ける）
        VerticalLines = lines
            .Where(l => l.Orientation == LineOrientation.Vertical)
            .ToList();
        HorizontalLines = lines
            .Where(l => l.Orientation == LineOrientation.Horizontal)
            .ToList();

        OnPropertyChanged(nameof(VerticalLines));
        OnPropertyChanged(nameof(HorizontalLines));
        OnPropertyChanged(nameof(VerticalCountText));
        OnPropertyChanged(nameof(HorizontalCountText));

        // 再検出で線分が再構築されたため、選択順・選択中テキスト・ルーペ内の線分オーバーレイをクリアする
        _selectionOrder.Clear();
        UpdateSelectedLineText();
        UpdateLoupeOverlay();
    }

    /// <summary>ポインタが画像上にある間の座標表示を更新し、ホバー状態を線分へ反映する。</summary>
    /// <param name="pos">ポインタ位置（画像ローカル座標・ピクセル）。</param>
    /// <remarks>
    /// ポインタから <see cref="HoverDistance"/> 以内にある最寄りの線分だけをホバー状態にし、<br/>
    /// それ以外の線分はホバー解除する（一覧側のホバーは <see cref="SetLineHover"/> が担当）。<br/>
    /// マウス位置の画像ドメイン座標を <see cref="PointerDomainText"/> へ反映する。<br/>
    /// ホバー対象が変化した場合のみルーペ内の表示を更新する（無変化時の再計算を避ける）。
    /// </remarks>
    public void UpdateHover(Point pos)
    {
        PointerDomainText = $"({pos.X:0}, {pos.Y:0})";

        LineItem? nearest = null;
        var bestSquared = HoverDistance * HoverDistance;

        foreach (var line in OverlayLines)
        {
            var squared = DistanceToSegmentSquared(pos, line.Start, line.End);
            if (squared < bestSquared)
            {
                bestSquared = squared;
                nearest = line;
            }
        }

        // ホバー対象が変化したときだけルーペの表示を更新する（ポインタ移動毎の無駄な再計算を防ぐ）
        var changed = false;
        foreach (var line in OverlayLines)
        {
            if (line.IsHovered != (line == nearest))
            {
                line.IsHovered = line == nearest;
                changed = true;
            }
        }

        if (changed)
        {
            UpdateLoupeOverlay();
        }
    }

    /// <summary>ポインタが画像上から離れたときの表示をクリアする。</summary>
    /// <remarks>
    /// マウス座標表示（<see cref="PointerDomainText"/>）とルーペ表示（<see cref="IsLoupeVisible"/>）を初期化する。<br/>
    /// 画像上でのみ有効な情報のため、画像外では表示しない（座標が残らないようにする）。
    /// </remarks>
    public void ClearPointerOverlay()
    {
        PointerDomainText = "";
        IsLoupeVisible = false;
    }

    /// <summary>一覧のホバー状態を指定した線分へ反映する。</summary>
    /// <param name="line">ホバー対象の線分アイテム。</param>
    /// <param name="isHovered">ホバー中かどうか。</param>
    /// <remarks>
    /// 左ペインの一覧をホバーしたときに、画像上の対応する線分と一覧の行を連動して強調表示する。<br/>
    /// ルーペ内の線分オーバーレイも同時に更新する。
    /// </remarks>
    public void SetLineHover(LineItem line, bool isHovered)
    {
        line.IsHovered = isHovered;
        UpdateLoupeOverlay();
    }

    /// <summary>クリック位置の線分の選択状態をトグルする（画像上クリック用）。</summary>
    /// <param name="pos">クリック位置（画像ローカル座標・ピクセル）。</param>
    /// <remarks>
    /// <see cref="HoverDistance"/> 以内に線分がある場合のみ選択トグルする（ない場合は何もしない）。
    /// </remarks>
    public void ToggleSelectAt(Point pos)
    {
        var hit = OverlayLines.FirstOrDefault(
            line => DistanceToSegmentSquared(pos, line.Start, line.End) < HoverDistance * HoverDistance);
        if (hit is not null)
        {
            ToggleSelect(hit);
        }
    }

    /// <summary>指定した線分の選択状態をトグルする（画像上・一覧クリック共通）。</summary>
    /// <param name="line">選択トグルの対象の線分アイテム。</param>
    /// <remarks>
    /// 選択中の線分を再クリックすると解除し、未選択の線分をクリックすると選択する。<br/>
    /// 選択は垂直・水平のグループごとに <see cref="MaxSelectionPerOrientation"/>（2 つ）まで可能で、<br/>
    /// 上限を超えて選択した場合は同じ向きの最も古い選択を自動解除する（FIFO）。<br/>
    /// 選択状態は画像上の線分・一覧の行・ルーペ内の線分オーバーレイへ連動して反映される。
    /// </remarks>
    public void ToggleSelect(LineItem line)
    {
        if (line.IsSelected)
        {
            // 同じ線分の再クリックで選択を解除する
            line.IsSelected = false;
            _selectionOrder.Remove(line);
            StatusText = "選択を解除しました";
        }
        else
        {
            // 同じ向きの選択が上限（2 つ）に達している場合は、最も古い選択を解除してから選択する
            if (_selectionOrder.Count(l => l.Orientation == line.Orientation) >= MaxSelectionPerOrientation)
            {
                var oldest = _selectionOrder.First(l => l.Orientation == line.Orientation);
                oldest.IsSelected = false;
                _selectionOrder.Remove(oldest);
            }

            line.IsSelected = true;
            _selectionOrder.Add(line);
            StatusText = $"選択: {OrientationLabel(line.Orientation)} #{line.Index}（{line.Start.X:0}, {line.Start.Y:0}）→（{line.End.X:0}, {line.End.Y:0}）";
        }

        UpdateSelectedLineText();
        UpdateLoupeOverlay();
    }

    /// <summary>線分の向きの日本語ラベルを返す。</summary>
    /// <param name="orientation">線分の向き。</param>
    /// <returns>「垂直線分」または「水平線分」。</returns>
    private static string OrientationLabel(LineOrientation orientation)
        => orientation == LineOrientation.Vertical ? "垂直線分" : "水平線分";

    /// <summary>選択中の線分の表示テキストを更新する。</summary>
    /// <remarks>
    /// 選択中の線分を「向き #インデックス（始点）→（終点）」の形式で改行区切りで並べる。<br/>
    /// 選択が無い場合は「選択中: なし」を表示する。
    /// </remarks>
    private void UpdateSelectedLineText()
    {
        SelectedLineText = _selectionOrder.Count == 0
            ? "選択中: なし"
            : "選択中: " + string.Join("\n", _selectionOrder.Select(line =>
                $"{OrientationLabel(line.Orientation)} #{line.Index}（{line.Start.X:0}, {line.Start.Y:0}）→（{line.End.X:0}, {line.End.Y:0}）"));

        OnPropertyChanged(nameof(SelectedLineText));
    }

    /// <summary>ルーペ内に破線表示する線分一覧（選択中を優先し、なければホバー中）を更新する。</summary>
    /// <remarks>
    /// 選択中の線分があればそれを、なければホバー中の線分をルーペ内の表示対象とする。<br/>
    /// どちらも無い場合は空リスト（ルーペ内に破線を描画しない）。
    /// </remarks>
    private void UpdateLoupeOverlay()
    {
        // _selectionOrder は常に選択中の線分のみを保持するため、IsSelected の再判定は不要
        if (_selectionOrder.Count > 0)
        {
            LoupeOverlayLines = [.. _selectionOrder];
        }
        else
        {
            LoupeOverlayLines = OverlayLines.Where(l => l.IsHovered).ToList();
        }

        OnPropertyChanged(nameof(LoupeOverlayLines));
    }

    /// <summary>点から線分への最短距離の二乗値を計算する。</summary>
    /// <param name="p">判定する点。</param>
    /// <param name="a">線分の始点。</param>
    /// <param name="b">線分の終点。</param>
    /// <returns>点と線分の最短距離の二乗値。</returns>
    /// <remarks>近接判定（しきい値の二乗と比較）に使用するため、開平方を行わない（ホットパス高速化）。</remarks>
    private static double DistanceToSegmentSquared(Point p, Point a, Point b)
    {
        var abx = b.X - a.X;
        var aby = b.Y - a.Y;
        var apx = p.X - a.X;
        var apy = p.Y - a.Y;

        var length = abx * abx + aby * aby;
        if (length == 0)
        {
            return apx * apx + apy * apy;
        }

        var t = Math.Max(0, Math.Min(1, (apx * abx + apy * aby) / length));
        var dx = p.X - (a.X + t * abx);
        var dy = p.Y - (a.Y + t * aby);
        return dx * dx + dy * dy;
    }
}
