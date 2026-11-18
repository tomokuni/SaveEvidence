using EsUtil.Platform.Core.Palette;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace EsUtil.Ui.WinUI3.Annotation;

/// <summary>注釈の本文（表示とその場編集）と書式バーを 1 つにまとめたコントロール（WinUI 3 版）。</summary>
/// <remarks>
/// <b>セットで使う</b>: レイヤー（画像・注釈・入力）と書式バーを重ねて持つ。 2 つを別々に置くと、
/// 位置合わせと表示の切り替えをホストが書くことになる。<br/>
/// <b>レイヤーは <see cref="Layer"/> から触る</b>: 画像・文書・選択・元に戻すなどは
/// <see cref="AnnotationLayerControl"/> の API を使う（本コントロールは配置だけを担う）。
/// </remarks>
public sealed class AnnotationTextEditor : Grid
{
    /// <summary>注釈の表示と編集を担うレイヤー。</summary>
    private readonly AnnotationLayerControl _layer;

    /// <summary>本文の書式を操作するバー。</summary>
    private readonly AnnotationFormatBar _formatBar;

    /// <summary>外観・重なり順の編集パネル（開いている間だけ保持する）。</summary>
    private AnnotationStylePanel? _stylePanel;

    /// <summary>注釈の本文編集と書式バーをまとめたコントロールを初期化する。</summary>
    public AnnotationTextEditor()
    {
        _layer = new AnnotationLayerControl();
        _formatBar = new AnnotationFormatBar(_layer)
        {
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(8),
        };

        Children.Add(_layer);
        Children.Add(_formatBar);

        _layer.EditingChanged += (_, _) =>
        {
            _formatBar.Visibility = _layer.IsEditingText ? Visibility.Visible : Visibility.Collapsed;
            if (_layer.IsEditingText)
            {
                _formatBar.Refresh();
            }
        };

        _layer.TextSelectionChanged += (_, _) =>
        {
            if (_layer.IsEditingText)
            {
                _formatBar.Refresh();
            }
        };

        _layer.SelectionChanged += (_, _) =>
        {
            if (_layer.IsEditingText)
            {
                _formatBar.Refresh();
            }

            if (_stylePanel is not null)
            {
                CloseStylePanel();
                ShowStylePanel();
            }
        };

        _layer.StyleEditorRequested += (_, _) => ShowStylePanel();
    }

    /// <summary>注釈の表示と編集を担うレイヤーを取得する。</summary>
    public AnnotationLayerControl Layer => _layer;

    /// <summary>本文を編集中かどうかを取得する。</summary>
    public bool IsEditingText => _layer.IsEditingText;

    /// <summary>書式バーの色の入力欄へ出す表記を取得または設定する。</summary>
    public ColorTextFormat TextFormat
    {
        get => _formatBar.TextFormat;
        set
        {
            _formatBar.TextFormat = value;
            _layer.ColorTextFormat = value;
        }
    }

    /// <summary>外観・重なり順の編集パネルを表示する（右クリックと同じ）。</summary>
    public void ShowStylePanel()
    {
        CloseStylePanel();

        var panel = new AnnotationStylePanel(_layer, CloseStylePanel);
        _stylePanel = panel;
        Children.Add(panel);
    }

    /// <summary>外観・重なり順の編集パネルを閉じる。</summary>
    public void CloseStylePanel()
    {
        if (_stylePanel is not { } panel)
        {
            return;
        }

        _stylePanel = null;
        Children.Remove(panel);
    }
}
