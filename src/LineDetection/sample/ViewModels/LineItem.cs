using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Estable.AvaloniaUI.LineDetection;

namespace LineDetectionSample.ViewModels;

/// <summary>検出された辺（線分）の表示アイテム（画像オーバーレイ + 一覧の両方で使用）。</summary>
/// <remarks>
/// <see cref="IsHovered"/> / <see cref="IsSelected"/> が変わると、画像上の線分（<see cref="Stroke"/>）と
/// 一覧の行（<see cref="RowBackground"/> / <see cref="IndicatorBrush"/>）が連動して強調表示される。<br/>
/// 色の優先順位: 選択（オレンジ） &gt; ホバー（赤） &gt; 通常（水色）。
/// </remarks>
public sealed partial class LineItem : ObservableObject
{
    /// <summary>通常時（水色）の線分ブラシ。</summary>
    private static readonly IBrush s_normalBrush = new SolidColorBrush(Color.Parse("#87CEFA"));

    /// <summary>ホバー時（赤）の線分ブラシ。</summary>
    private static readonly IBrush s_hoverBrush = new SolidColorBrush(Color.Parse("#FF3B30"));

    /// <summary>選択時（オレンジ）の線分ブラシ。</summary>
    private static readonly IBrush s_selectedBrush = new SolidColorBrush(Color.Parse("#FF8C00"));

    /// <summary>通常時（透明）の行背景ブラシ。</summary>
    private static readonly IBrush s_rowNormalBrush = Brushes.Transparent;

    /// <summary>ホバー時（薄い赤）の行背景ブラシ。</summary>
    private static readonly IBrush s_rowHoverBrush = new SolidColorBrush(Color.Parse("#FFE0E0"));

    /// <summary>選択時（薄いオレンジ）の行背景ブラシ。</summary>
    private static readonly IBrush s_rowSelectedBrush = new SolidColorBrush(Color.Parse("#FFE8CC"));

    /// <summary>アイテムを初期化する。</summary>
    /// <param name="result">検出結果（線分）。</param>
    /// <param name="index">一覧内の表示インデックス（垂直・水平を合わせた通し番号）。</param>
    public LineItem(LineDetectionResult result, int index)
    {
        Index = index;
        Start = result.Start;
        End = result.End;
        Orientation = result.Orientation;
        Score = result.Score;
        Length = Math.Sqrt((End.X - Start.X) * (End.X - Start.X) + (End.Y - Start.Y) * (End.Y - Start.Y));
    }

    /// <summary>一覧内の表示インデックス。</summary>
    public int Index { get; }

    /// <summary>始点（画像座標・ピクセル）。</summary>
    public Point Start { get; }

    /// <summary>終点（画像座標・ピクセル）。</summary>
    public Point End { get; }

    /// <summary>線分の向き（水平 / 垂直）。</summary>
    public LineOrientation Orientation { get; }

    /// <summary>確からしさ（長さ・信頼度等）。</summary>
    public double Score { get; }

    /// <summary>線分の長さ（ピクセル）。</summary>
    public double Length { get; }

    /// <summary>マウスオーバー中かどうか。</summary>
    [ObservableProperty]
    private bool _isHovered;

    /// <summary>クリックで選択中かどうか（再クリックで解除）。</summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>線分の色（選択 = オレンジ / ホバー = 赤 / 通常 = 水色）。</summary>
    public IBrush Stroke => IsSelected ? s_selectedBrush : IsHovered ? s_hoverBrush : s_normalBrush;

    /// <summary>線分の太さ（ホバー・選択時に太くして強調）。</summary>
    public double StrokeThickness => IsSelected || IsHovered ? 3.0 : 2.0;

    /// <summary>一覧の行背景（選択 = 薄いオレンジ / ホバー = 薄い赤 / 通常 = 透明）。</summary>
    public IBrush RowBackground => IsSelected ? s_rowSelectedBrush : IsHovered ? s_rowHoverBrush : s_rowNormalBrush;

    /// <summary>一覧のインジケーター色（選択 = オレンジ / ホバー = 赤 / 通常 = 水色）。</summary>
    public IBrush IndicatorBrush => IsSelected ? s_selectedBrush : IsHovered ? s_hoverBrush : s_normalBrush;

    /// <summary>一覧表示用テキスト（インデックス + 座標 + 長さ）。</summary>
    public string Summary => $"#{Index} ({Start.X:0}, {Start.Y:0}) → ({End.X:0}, {End.Y:0})  長さ {Length:0}px";

    /// <summary>ホバー状態の変化時に表示色を更新する。</summary>
    partial void OnIsHoveredChanged(bool value)
    {
        OnPropertyChanged(nameof(Stroke));
        OnPropertyChanged(nameof(StrokeThickness));
        OnPropertyChanged(nameof(RowBackground));
        OnPropertyChanged(nameof(IndicatorBrush));
    }

    /// <summary>選択状態の変化時に表示色を更新する。</summary>
    partial void OnIsSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(Stroke));
        OnPropertyChanged(nameof(StrokeThickness));
        OnPropertyChanged(nameof(RowBackground));
        OnPropertyChanged(nameof(IndicatorBrush));
    }
}
