using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rect = Avalonia.Rect;
using Estable.AvaloniaUI.FadeMask;
using Estable.AvaloniaUI.Platform;
using Estable.AvaloniaUI.Platform.Geometry;
using Estable;

namespace FadeMaskControl.Sample.ViewModels;

/// <summary>メインウィンドウの ViewModel。</summary>
/// <remarks>
/// FadeMaskControl の公開プロパティ（SelectionRect / OverlayColor / BorderColor /
/// BorderThickness / TextColor / TextBackgroundColor / FontSize / ShowSizeLabel）を双方向バインドで公開する。<br/>
/// 色の不透明度は色のアルファに統合する。<br/>
/// 選択矩形の座標は個別プロパティ（X / Y / W / H）として編集し、<see cref="SelectionRect"/> へ反映する。<br/>
/// 変更通知とコマンドは CommunityToolkit.Mvvm（<see cref="ObservableObject"/> / <c>[RelayCommand]</c>）で提供する。
/// </remarks>
public partial class MainWindowViewModel : ObservableObject
{
    private Rect _selectionRect = new(50, 40, 220, 160);
    private Color _overlayColor = Color.FromAColor(150, Colors.Black);
    private Color _borderColor = Colors.Red;
    private double _borderThickness = 2.0;
    private Color _textColor = Colors.White;
    private Color _textBackgroundColor = Color.FromAColor(128, Colors.Black);
    private double _fontSize = 14.0;
    private bool _showSizeLabel = true;
    private int _selectedModeIndex;

    /// <summary>選択矩形（FadeMaskControl と双方向バインド）。</summary>
    public Rect SelectionRect
    {
        get => _selectionRect;
        set
        {
            if (_selectionRect == value) return;
            _selectionRect = value;
            OnPropertyChanged(nameof(SelectionRect));
            OnPropertyChanged(nameof(RectX));
            OnPropertyChanged(nameof(RectY));
            OnPropertyChanged(nameof(RectWidth));
            OnPropertyChanged(nameof(RectHeight));
            // 選択矩形の変更をレンダラー（AreaSelect / Crop の穴あけ位置）へ反映する
            ReflectSelectionRectToRenderer();
        }
    }

    /// <summary>選択矩形の X 座標（編集用）。</summary>
    public string RectX
    {
        get => ((int)_selectionRect.X).ToString();
        set => UpdateRect(x: value);
    }

    /// <summary>選択矩形の Y 座標（編集用）。</summary>
    public string RectY
    {
        get => ((int)_selectionRect.Y).ToString();
        set => UpdateRect(y: value);
    }

    /// <summary>選択矩形の幅（編集用）。</summary>
    public string RectWidth
    {
        get => ((int)_selectionRect.Width).ToString();
        set => UpdateRect(w: value);
    }

    /// <summary>選択矩形の高さ（編集用）。</summary>
    public string RectHeight
    {
        get => ((int)_selectionRect.Height).ToString();
        set => UpdateRect(h: value);
    }

    /// <summary>選択矩形の編集値を反映する。</summary>
    /// <param name="x">新しい X 座標。null の場合は現在値を維持。</param>
    /// <param name="y">新しい Y 座標。null の場合は現在値を維持。</param>
    /// <param name="w">新しい幅。null の場合は現在値を維持。</param>
    /// <param name="h">新しい高さ。null の場合は現在値を維持。</param>
    /// <remarks>
    /// 指定された項目のみ現在値から更新する。パース不能な値（空文字・非数値）は現在値を維持する。<br/>
    /// 更新後は <see cref="SelectionRect"/> へ反映し、全座標のプロパティ変更を通知する（他 TextBox への反映）。
    /// </remarks>
    private void UpdateRect(string? x = null, string? y = null, string? w = null, string? h = null)
    {
        double nx = _selectionRect.X, ny = _selectionRect.Y, nw = _selectionRect.Width, nh = _selectionRect.Height;

        if (x is not null && double.TryParse(x, out var px)) nx = px;
        if (y is not null && double.TryParse(y, out var py)) ny = py;
        if (w is not null && double.TryParse(w, out var pw)) nw = pw;
        if (h is not null && double.TryParse(h, out var ph)) nh = ph;

        SelectionRect = new Rect(nx, ny, nw, nh);
    }

    /// <summary>選択矩形をクリアするコマンド。</summary>
    [RelayCommand]
    private void ClearRect() => SelectionRect = new Rect(0, 0, 0, 0);

    /// <summary>暗転オーバーレイのアルファ値（0〜255、編集用）。</summary>
    public int OverlayOpacity
    {
        get => _overlayColor.A;
        set
        {
            _overlayColor = Color.FromAColor((byte)Math.Clamp(value, 0, 255), _overlayColor);
            // 外部レンダラーへ反映（ExternalRenderer 設定時は FadeMaskControl の StyledProperty が無視されるため）
            Renderer.OverlayArgbColor = _overlayColor;
            OnPropertyChanged(nameof(OverlayOpacity));
            OnPropertyChanged(nameof(OverlayOpacityText));
            OnPropertyChanged(nameof(OverlayColor));
        }
    }

    /// <summary>暗転オーバーレイのアルファ値の表示テキスト。</summary>
    public string OverlayOpacityText => $"{(int)(_overlayColor.A / 255.0 * 100)}%";

    /// <summary>暗転オーバーレイの色。</summary>
    public Color OverlayColor
    {
        get => _overlayColor;
        set
        {
            _overlayColor = value;
            // 外部レンダラーへ反映（ExternalRenderer 設定時は FadeMaskControl の StyledProperty が無視されるため）
            Renderer.OverlayArgbColor = value;
            OnPropertyChanged(nameof(OverlayColor));
            OnPropertyChanged(nameof(OverlayOpacity));
            OnPropertyChanged(nameof(OverlayOpacityText));
        }
    }

    /// <summary>選択枠線の色。</summary>
    public Color BorderColor
    {
        get => _borderColor;
        set
        {
            _borderColor = value;
            // 外部レンダラーへ反映（ExternalRenderer 設定時は FadeMaskControl の StyledProperty が無視されるため）
            Renderer.BorderColor = value;
            OnPropertyChanged(nameof(BorderColor));
        }
    }

    /// <summary>選択枠線の太さ。</summary>
    public double BorderThickness
    {
        get => _borderThickness;
        set
        {
            _borderThickness = value;
            // 外部レンダラーへ反映（ExternalRenderer 設定時は FadeMaskControl の StyledProperty が無視されるため）
            Renderer.BorderThickness = value;
            OnPropertyChanged(nameof(BorderThickness));
        }
    }

    /// <summary>ラベルテキストの色。</summary>
    public Color TextColor
    {
        get => _textColor;
        set
        {
            _textColor = value;
            // 外部レンダラーへ反映（ExternalRenderer 設定時は FadeMaskControl の StyledProperty が無視されるため）
            Renderer.TextColor = value;
            OnPropertyChanged(nameof(TextColor));
        }
    }

    /// <summary>ラベル背景の色（不透明度は色のアルファに統合）。</summary>
    public Color TextBackgroundColor
    {
        get => _textBackgroundColor;
        set
        {
            _textBackgroundColor = value;
            // 外部レンダラーへ反映（ExternalRenderer 設定時は FadeMaskControl の StyledProperty が無視されるため）
            Renderer.TextBackgroundColor = value;
            OnPropertyChanged(nameof(TextBackgroundColor));
            OnPropertyChanged(nameof(TextBackgroundColorText));
        }
    }

    /// <summary>ラベル背景の色の編集用テキスト（16進コード）。</summary>
    public string TextBackgroundColorText
    {
        get => $"#{_textBackgroundColor.A:X2}{_textBackgroundColor.R:X2}{_textBackgroundColor.G:X2}{_textBackgroundColor.B:X2}";
        set
        {
            if (Color.TryParse(value, out var c))
            {
                TextBackgroundColor = c;
            }
        }
    }

    /// <summary>ラベルのフォントサイズ。</summary>
    public double FontSize
    {
        get => _fontSize;
        set
        {
            _fontSize = value;
            // 外部レンダラーへ反映（ExternalRenderer 設定時は FadeMaskControl の StyledProperty が無視されるため）
            Renderer.FontSize = value;
            OnPropertyChanged(nameof(FontSize));
        }
    }

    /// <summary>サイズラベルを表示するかどうか。</summary>
    public bool ShowSizeLabel
    {
        get => _showSizeLabel;
        set
        {
            _showSizeLabel = value;
            // 外部レンダラーへ反映（ExternalRenderer 設定時は FadeMaskControl の StyledProperty が無視されるため）
            Renderer.ShowSizeLabel = value;
            OnPropertyChanged(nameof(ShowSizeLabel));
        }
    }

    private string _sizeLabelOverride = "";

    /// <summary>サイズラベルのテキスト明示指定（SizeLabelOverride）。空文字は自動生成。</summary>
    /// <remarks>null の場合は <see cref="SelectionRect"/> から自動生成される。空文字入力は自動生成扱い（null）へ変換する。</remarks>
    public string SizeLabelOverride
    {
        get => _sizeLabelOverride;
        set
        {
            _sizeLabelOverride = value;
            // 外部レンダラーへ反映（空文字は自動生成扱い。Provider 方式で描画時に評価）
            Renderer.SizeLabelOverrideProvider = () => string.IsNullOrWhiteSpace(value) ? null : value;
            OnPropertyChanged(nameof(SizeLabelOverride));
        }
    }

    private double _rendererScaling = 1.0;

    /// <summary>DPI スケーリング係数（FadeMaskRenderer.Scaling）。</summary>
    public double RendererScaling
    {
        get => _rendererScaling;
        set
        {
            if (Math.Abs(_rendererScaling - value) < 0.001) return;
            _rendererScaling = value;
            // 外部レンダラーへ反映（サイズラベルの物理ピクセル換算）
            Renderer.Scaling = value;
            OnPropertyChanged(nameof(RendererScaling));
            OnPropertyChanged(nameof(RendererScalingText));
        }
    }

    /// <summary>DPI スケーリング係数の表示文字列。</summary>
    public string RendererScalingText => $"{RendererScaling:F2}";

    private double _displayScaleScaling;

    /// <summary>サイズラベル表示用のスケーリング（FadeMaskRenderer.DisplayScaleScaling・マルチスクリーン補正）。0 以下は Scaling と同値。</summary>
    public double DisplayScaleScaling
    {
        get => _displayScaleScaling;
        set
        {
            if (Math.Abs(_displayScaleScaling - value) < 0.001) return;
            _displayScaleScaling = value;
            // 外部レンダラーへ反映（0 以下は Scaling と同値扱い）
            Renderer.DisplayScaleScaling = value;
            OnPropertyChanged(nameof(DisplayScaleScaling));
            OnPropertyChanged(nameof(DisplayScaleScalingText));
        }
    }

    /// <summary>サイズラベル用スケーリングの表示文字列（0 は自動 = Scaling と同値）。</summary>
    public string DisplayScaleScalingText =>
        _displayScaleScaling > 0 ? $"{_displayScaleScaling:F2}" : "自動（Scaling と同値）";

    private bool _infoLabelVisible = true;

    /// <summary>情報ラベルの表示有無（FadeMaskRenderer.InfoLabelVisible）。</summary>
    /// <remarks>情報ラベルは ScreenSelect / WindowSelect のハイライト時のみ表示される。</remarks>
    public bool InfoLabelVisible
    {
        get => _infoLabelVisible;
        set
        {
            _infoLabelVisible = value;
            // 外部レンダラーへ反映
            Renderer.InfoLabelVisible = value;
            OnPropertyChanged(nameof(InfoLabelVisible));
        }
    }

    private Color _infoLabelColor = Colors.White;

    /// <summary>情報ラベルの文字色（FadeMaskRenderer.InfoLabelColor）。</summary>
    public Color InfoLabelColor
    {
        get => _infoLabelColor;
        set
        {
            _infoLabelColor = value;
            // 外部レンダラーへ反映
            Renderer.InfoLabelColor = value;
            OnPropertyChanged(nameof(InfoLabelColor));
            OnPropertyChanged(nameof(InfoLabelColorText));
        }
    }

    /// <summary>情報ラベルの文字色の編集用テキスト（16進コード）。</summary>
    /// <remarks>TextBox で編集するため、色コード文字列（例: <c>#FFFFFFFF</c>）で公開する。</remarks>
    public string InfoLabelColorText
    {
        get => $"#{_infoLabelColor.A:X2}{_infoLabelColor.R:X2}{_infoLabelColor.G:X2}{_infoLabelColor.B:X2}";
        set
        {
            if (Color.TryParse(value, out var c))
            {
                InfoLabelColor = c;
            }
        }
    }

    private Color _infoLabelBackgroundColor = Color.FromAColor(128, Colors.Black);

    /// <summary>情報ラベルの背景色（FadeMaskRenderer.InfoLabelBackgroundColor・不透明度はアルファに統合）。</summary>
    public Color InfoLabelBackgroundColor
    {
        get => _infoLabelBackgroundColor;
        set
        {
            _infoLabelBackgroundColor = value;
            // 外部レンダラーへ反映
            Renderer.InfoLabelBackgroundColor = value;
            OnPropertyChanged(nameof(InfoLabelBackgroundColor));
            OnPropertyChanged(nameof(InfoLabelBackgroundColorText));
        }
    }

    /// <summary>情報ラベルの背景色の編集用テキスト（16進コード）。</summary>
    public string InfoLabelBackgroundColorText
    {
        get => $"#{_infoLabelBackgroundColor.A:X2}{_infoLabelBackgroundColor.R:X2}{_infoLabelBackgroundColor.G:X2}{_infoLabelBackgroundColor.B:X2}";
        set
        {
            if (Color.TryParse(value, out var c))
            {
                InfoLabelBackgroundColor = c;
            }
        }
    }

    private double _infoLabelFontSize = 14.0;

    /// <summary>情報ラベルのフォントサイズ（FadeMaskRenderer.InfoLabelFontSize・DIP）。</summary>
    public double InfoLabelFontSize
    {
        get => _infoLabelFontSize;
        set
        {
            _infoLabelFontSize = value;
            // 外部レンダラーへ反映
            Renderer.InfoLabelFontSize = value;
            OnPropertyChanged(nameof(InfoLabelFontSize));
        }
    }

    /// <summary>描画モードの一覧（表示用）。</summary>
    public IReadOnlyList<string> ModeItems { get; } =
        [nameof(FadeMaskRenderMode.AreaSelect), nameof(FadeMaskRenderMode.ScreenSelect), nameof(FadeMaskRenderMode.WindowSelect), nameof(FadeMaskRenderMode.Crop)];

    /// <summary>外部レンダラー（モード別描画の真のソース。FadeMaskControl.ExternalRenderer へバインド）。</summary>
    /// <remarks>モード切替（<see cref="SelectedModeIndex"/>）に応じて描画状態を更新する。</remarks>
    public FadeMaskRenderer Renderer { get; } = new();

    /// <summary>描画モードの選択インデックス。</summary>
    /// <remarks>変更時に <see cref="Renderer"/> のモードと描画状態を更新する（モード別描画の切り替え）。</remarks>
    public int SelectedModeIndex
    {
        get => _selectedModeIndex;
        set
        {
            if (_selectedModeIndex == value) return;
            _selectedModeIndex = value;
            ApplyMode((FadeMaskRenderMode)value);
            OnPropertyChanged(nameof(SelectedModeIndex));
        }
    }

    /// <summary>選択矩形の変更をレンダラー（AreaSelect / Crop の選択矩形）へ反映する。</summary>
    private void ReflectSelectionRectToRenderer()
    {
        // 選択矩形の変更は AreaSelect / Crop モードの描画（穴あけ位置）に反映する
        if (Renderer.Mode is FadeMaskRenderMode.AreaSelect or FadeMaskRenderMode.Crop)
        {
            Renderer.LogicalSelectionRect = SelectionRect.Width > 0 && SelectionRect.Height > 0 ? SelectionRect.ToRectD() : null;
        }
    }

    /// <summary>モードに応じたレンダラーの描画状態を適用する。</summary>
    /// <param name="mode">適用する描画モード。</param>
    /// <remarks>
    /// 各モードの描画要素を <see cref="Renderer"/> へ設定する:<br/>
    /// - AreaSelect: 選択矩形 + サイズラベル（ルーペ・ハンドルは DragRect の責務のため本サンプルでは表示なし）<br/>
    /// - ScreenSelect: ハイライト + スクリーン番号ラベル<br/>
    /// - WindowSelect: ハイライト + ウィンドウタイトルラベル<br/>
    /// - Crop: 全面暗転 + 選択矩形
    /// </remarks>
    private void ApplyMode(FadeMaskRenderMode mode)
    {
        var r = Renderer;
        r.Mode = mode;
        switch (mode)
        {
            case FadeMaskRenderMode.AreaSelect:
                r.HasSelection = true;
                r.LogicalSelectionRect = SelectionRect.Width > 0 && SelectionRect.Height > 0 ? SelectionRect.ToRectD() : null;
                r.HasHighlight = false;
                r.ShowFullOverlay = false;
                r.WindowTitle = "";
                r.ScreenIndex = -1;
                break;

            case FadeMaskRenderMode.ScreenSelect:
                r.HasHighlight = true;
                r.LogicalHighlightRect = new RectD(80, 60, 300, 180);
                r.ScreenIndex = 1;
                r.HasSelection = false;
                r.ShowFullOverlay = false;
                r.WindowTitle = "";
                break;

            case FadeMaskRenderMode.WindowSelect:
                r.HasHighlight = true;
                r.LogicalHighlightRect = new RectD(80, 60, 300, 180);
                r.WindowTitle = "Sample Window";
                r.HasSelection = false;
                r.ShowFullOverlay = false;
                r.ScreenIndex = -1;
                break;

            case FadeMaskRenderMode.Crop:
                r.HasSelection = true;
                r.LogicalSelectionRect = SelectionRect.Width > 0 && SelectionRect.Height > 0 ? SelectionRect.ToRectD() : null;
                r.HasHighlight = false;
                r.ShowFullOverlay = true; // 全面暗転 + 選択矩形
                r.WindowTitle = "";
                r.ScreenIndex = -1;
                break;
        }
    }

    /// <summary>MainWindowViewModel を初期化する。</summary>
    public MainWindowViewModel()
    {
        // 初期モード（AreaSelect）をレンダラーへ反映する
        ApplyMode(FadeMaskRenderMode.AreaSelect);
    }
}

