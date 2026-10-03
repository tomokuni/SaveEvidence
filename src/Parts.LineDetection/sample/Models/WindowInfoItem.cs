using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;

namespace EsUtil.SaveEvidence.Parts.LineDetection.Sample.Models;

/// <summary>領域内ウィンドウの一覧表示アイテム（GUI で正解を選択するための行）。</summary>
/// <remarks>
/// キャプチャ領域と交差するウィンドウ 1 件分の表示情報と、<br/>
/// 「正解として選択するかどうか」の選択状態（<see cref="IsSelected"/>）を保持する。<br/>
/// <see cref="Bounds"/> はキャプチャ画像のローカル座標（キャプチャ領域との交差部分ではなく、
/// ウィンドウ全体の矩形をキャプチャ領域基準へ変換したもの）。
/// </remarks>
public sealed partial class WindowInfoItem : ObservableObject
{
    /// <summary>選択状態が変更されたときに呼ばれるコールバック（親 ViewModel への通知）。</summary>
    private readonly Action? _onSelectionChanged;

    /// <summary>ウィンドウの矩形（キャプチャ画像のローカル座標）。</summary>
    public PixelRect Bounds { get; }

    /// <summary>ウィンドウタイトル。</summary>
    public string Title { get; }

    /// <summary>ウィンドウクラス名。</summary>
    public string ClassName { get; }

    /// <summary>正解として選択するかどうか。</summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>WindowInfoItem を初期化する。</summary>
    /// <param name="bounds">ウィンドウの矩形（キャプチャ画像のローカル座標）。</param>
    /// <param name="title">ウィンドウタイトル。</param>
    /// <param name="className">ウィンドウクラス名。</param>
    /// <param name="onSelectionChanged">選択状態変更時の通知先（null 可）。</param>
    public WindowInfoItem(PixelRect bounds, string title, string className, Action? onSelectionChanged = null)
    {
        Bounds = bounds;
        Title = title;
        ClassName = className;
        _onSelectionChanged = onSelectionChanged;
    }

    /// <summary>選択状態の変更を親 ViewModel へ通知する。</summary>
    partial void OnIsSelectedChanged(bool value) => _onSelectionChanged?.Invoke();

    /// <summary>一覧表示用の説明文字列（例: "800×600 (120,80)"）。</summary>
    public string Summary => $"{Title} [{ClassName}] {Bounds.Width}×{Bounds.Height} ({Bounds.X},{Bounds.Y})";
}
