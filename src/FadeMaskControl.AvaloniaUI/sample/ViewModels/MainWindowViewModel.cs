using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EsUtil.Platform.AvaloniaUI;
using Estable.FadeMask;
using Rect = Avalonia.Rect;
using DrawingColor = System.Drawing.Color;

namespace FadeMaskControl.Sample.ViewModels;

/// <summary>メインウィンドウの ViewModel。</summary>
/// <remarks>
/// コントロールへ渡す設定と描画状態はコアの <see cref="FadeMaskOptions"/>（1 値）として <see cref="Options"/> から公開する（AXAML は 1 本のバインドで足りる）。<br/>
/// 編集項目（色・サイズ・太さ）は <see cref="Appearance"/> を、モード・選択矩形・ハイライト・全面暗転は個別プロパティを編集し、<br/>
/// 各 setter が <c>with</c> またはフィールドの更新で <see cref="Options"/> の変更通知を発行する。<br/>
/// 選択矩形の座標は個別プロパティ（X / Y / W / H）として編集し、論理選択矩形へ反映する。
/// </remarks>
public partial class MainWindowViewModel : ObservableObject
{
    /// <summary>外観（コアの値。編集項目の setter が <c>with</c> で更新する）。</summary>
    private FadeMaskAppearance _appearance = new();

    /// <summary>選択矩形（ドメイン座標）。</summary>
    private Rect _selectionRect = new(50, 40, 220, 160);

    /// <summary>描画モードの選択インデックス。</summary>
    private int _selectedModeIndex;

    /// <summary>サイズラベルの明示指定（空文字は自動生成）。</summary>
    private string _sizeLabelOverride = "";

    // ====================================================================
    // 外観（コアの FadeMaskAppearance を単一の値として保持する）
    // ====================================================================

    /// <summary>暗転マスクの外観（暗転色・枠線・ラベル）。</summary>
    /// <remarks>各編集項目の setter が本値を部分更新し、<c>Appearance</c> の変更通知でコントロールへ伝わる。</remarks>
    public FadeMaskAppearance Appearance
    {
        get => _appearance;
        private set
        {
            _appearance = value;
            OnPropertyChanged(nameof(Appearance));
            OnPropertyChanged(nameof(Options));
        }
    }

    /// <summary>コントロールへ渡す設定と描画状態（コアの <see cref="FadeMaskOptions"/>）。</summary>
    /// <remarks>
    /// 編集項目から毎回組み立てる（項目ごとの既定値を持たない。既定値はコアの <see cref="FadeMaskOptions.Default"/> が単一所有する）。<br/>
    /// 編集項目の setter が本プロパティの変更通知を発行するため、AXAML は <c>Options="{Binding Options}"</c> の 1 本で足りる。<br/>
    /// DPI スケーリング（<see cref="Scaling"/> / <see cref="DisplayScaleScaling"/>）とサイズラベルの文言プロバイダは<br/>
    /// <see cref="FadeMaskOptions"/> に含まれないため、別途コントロールへ設定する。
    /// </remarks>
    public FadeMaskOptions Options => new()
    {
        Appearance = Appearance,
        Mode = Mode,
        WindowTitle = WindowTitle,
        ShowFullOverlay = ShowFullOverlay,
        LogicalSelectionRect = LogicalSelectionRect?.ToEsRect(),
        LogicalHighlightRect = LogicalHighlightRect?.ToEsRect(),
        HasSelection = HasSelection,
        HasHighlight = HasHighlight,
    };

    /// <summary>暗転オーバーレイの色。</summary>
    public Color OverlayColor
    {
        get => _appearance.OverlayColor.ToAvaloniaColor();
        set
        {
            UpdateAppearance(a => a with { OverlayColor = value.ToDrawingColor() });
            OnPropertyChanged(nameof(OverlayColor));
            OnPropertyChanged(nameof(OverlayOpacity));
            OnPropertyChanged(nameof(OverlayOpacityText));
        }
    }

    /// <summary>暗転オーバーレイのアルファ値（0〜255、編集用）。</summary>
    public int OverlayOpacity
    {
        get => _appearance.OverlayColor.A;
        set
        {
            var alpha = (byte)Math.Clamp(value, 0, 255);
            var color = _appearance.OverlayColor;
            UpdateAppearance(a => a with { OverlayColor = DrawingColor.FromArgb(alpha, color.R, color.G, color.B) });
            OnPropertyChanged(nameof(OverlayOpacity));
            OnPropertyChanged(nameof(OverlayOpacityText));
            OnPropertyChanged(nameof(OverlayColor));
        }
    }

    /// <summary>暗転オーバーレイのアルファ値の表示テキスト。</summary>
    public string OverlayOpacityText => $"{(int)(_appearance.OverlayColor.A / 255.0 * 100)}%";

    /// <summary>選択枠線の色。</summary>
    public Color BorderColor
    {
        get => _appearance.BorderColor.ToAvaloniaColor();
        set
        {
            UpdateAppearance(a => a with { BorderColor = value.ToDrawingColor() });
            OnPropertyChanged(nameof(BorderColor));
        }
    }

    /// <summary>選択枠線の太さ（DIP）。</summary>
    public double BorderThickness
    {
        get => _appearance.BorderThickness;
        set
        {
            UpdateAppearance(a => a with { BorderThickness = value });
            OnPropertyChanged(nameof(BorderThickness));
        }
    }

    /// <summary>サイズラベルの文字色。</summary>
    public Color TextColor
    {
        get => _appearance.SizeLabelColor.ToAvaloniaColor();
        set
        {
            UpdateAppearance(a => a with { SizeLabelColor = value.ToDrawingColor() });
            OnPropertyChanged(nameof(TextColor));
        }
    }

    /// <summary>サイズラベルの背景色（不透明度は色のアルファに統合）。</summary>
    public Color TextBackgroundColor
    {
        get => _appearance.SizeLabelBackgroundColor.ToAvaloniaColor();
        set
        {
            UpdateAppearance(a => a with { SizeLabelBackgroundColor = value.ToDrawingColor() });
            OnPropertyChanged(nameof(TextBackgroundColor));
            OnPropertyChanged(nameof(TextBackgroundColorText));
        }
    }

    /// <summary>サイズラベルの背景色の編集用テキスト（16進コード）。</summary>
    public string TextBackgroundColorText
    {
        get => ToHex(_appearance.SizeLabelBackgroundColor);
        set
        {
            if (Color.TryParse(value, out var c))
            {
                TextBackgroundColor = c;
            }
        }
    }

    /// <summary>サイズラベルのフォントサイズ（DIP）。</summary>
    public double FontSize
    {
        get => _appearance.SizeLabelFontSize;
        set
        {
            UpdateAppearance(a => a with { SizeLabelFontSize = value });
            OnPropertyChanged(nameof(FontSize));
        }
    }

    /// <summary>サイズラベルを表示するかどうか。</summary>
    public bool ShowSizeLabel
    {
        get => _appearance.ShowSizeLabel;
        set
        {
            UpdateAppearance(a => a with { ShowSizeLabel = value });
            OnPropertyChanged(nameof(ShowSizeLabel));
        }
    }

    /// <summary>情報ラベルの表示有無。</summary>
    /// <remarks>情報ラベルは ScreenSelect / WindowSelect のハイライト時のみ表示される。</remarks>
    public bool InfoLabelVisible
    {
        get => _appearance.InfoLabelVisible;
        set
        {
            UpdateAppearance(a => a with { InfoLabelVisible = value });
            OnPropertyChanged(nameof(InfoLabelVisible));
        }
    }

    /// <summary>情報ラベルの文字色。</summary>
    public Color InfoLabelColor
    {
        get => _appearance.InfoLabelColor.ToAvaloniaColor();
        set
        {
            UpdateAppearance(a => a with { InfoLabelColor = value.ToDrawingColor() });
            OnPropertyChanged(nameof(InfoLabelColor));
            OnPropertyChanged(nameof(InfoLabelColorText));
        }
    }

    /// <summary>情報ラベルの文字色の編集用テキスト（16進コード）。</summary>
    public string InfoLabelColorText
    {
        get => ToHex(_appearance.InfoLabelColor);
        set
        {
            if (Color.TryParse(value, out var c))
            {
                InfoLabelColor = c;
            }
        }
    }

    /// <summary>情報ラベルの背景色（不透明度は色のアルファに統合）。</summary>
    public Color InfoLabelBackgroundColor
    {
        get => _appearance.InfoLabelBackgroundColor.ToAvaloniaColor();
        set
        {
            UpdateAppearance(a => a with { InfoLabelBackgroundColor = value.ToDrawingColor() });
            OnPropertyChanged(nameof(InfoLabelBackgroundColor));
            OnPropertyChanged(nameof(InfoLabelBackgroundColorText));
        }
    }

    /// <summary>情報ラベルの背景色の編集用テキスト（16進コード）。</summary>
    public string InfoLabelBackgroundColorText
    {
        get => ToHex(_appearance.InfoLabelBackgroundColor);
        set
        {
            if (Color.TryParse(value, out var c))
            {
                InfoLabelBackgroundColor = c;
            }
        }
    }

    /// <summary>情報ラベルのフォントサイズ（DIP）。</summary>
    public double InfoLabelFontSize
    {
        get => _appearance.InfoLabelFontSize;
        set
        {
            UpdateAppearance(a => a with { InfoLabelFontSize = value });
            OnPropertyChanged(nameof(InfoLabelFontSize));
        }
    }

    /// <summary>外観を部分更新する。</summary>
    /// <param name="change">現在の外観から新しい外観を作る関数。</param>
    private void UpdateAppearance(Func<FadeMaskAppearance, FadeMaskAppearance> change) => Appearance = change(_appearance);

    // ====================================================================
    // 描画状態
    // ====================================================================

    /// <summary>DPI スケーリング係数（サイズラベルの物理ピクセル換算）。</summary>
    public double Scaling
    {
        get => field;
        set
        {
            if (Math.Abs(field - value) < 0.001)
            {
                return;
            }

            field = value;
            OnPropertyChanged(nameof(Scaling));
            OnPropertyChanged(nameof(ScalingText));
        }
    } = 1.0;

    /// <summary>DPI スケーリング係数の表示文字列。</summary>
    public string ScalingText => $"{Scaling:F2}";

    /// <summary>サイズラベル表示用のスケーリング（0 以下は <see cref="Scaling"/> と同値）。</summary>
    public double DisplayScaleScaling
    {
        get => field;
        set
        {
            if (Math.Abs(field - value) < 0.001)
            {
                return;
            }

            field = value;
            OnPropertyChanged(nameof(DisplayScaleScaling));
            OnPropertyChanged(nameof(DisplayScaleScalingText));
        }
    }

    /// <summary>サイズラベル用スケーリングの表示文字列（0 は自動 = Scaling と同値）。</summary>
    public string DisplayScaleScalingText =>
        DisplayScaleScaling > 0 ? $"{DisplayScaleScaling:F2}" : "自動（Scaling と同値）";

    /// <summary>選択矩形（ドメイン座標）。</summary>
    public Rect SelectionRect
    {
        get => _selectionRect;
        set
        {
            if (_selectionRect == value)
            {
                return;
            }

            _selectionRect = value;
            OnPropertyChanged(nameof(SelectionRect));
            OnPropertyChanged(nameof(RectX));
            OnPropertyChanged(nameof(RectY));
            OnPropertyChanged(nameof(RectWidth));
            OnPropertyChanged(nameof(RectHeight));
            // 選択矩形の変更を穴あけ位置（AreaSelect / Crop）へ反映する
            ApplySelectionRect();
        }
    }

    /// <summary>論理選択矩形（穴あけ位置。選択を使うモードでのみ設定される）。</summary>
    public Rect? LogicalSelectionRect
    {
        get => field;
        private set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            OnPropertyChanged(nameof(LogicalSelectionRect));
            OnPropertyChanged(nameof(Options));
        }
    }

    /// <summary>論理ハイライト矩形（ハイライトを使うモードでのみ設定される）。</summary>
    public Rect? LogicalHighlightRect
    {
        get => field;
        private set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            OnPropertyChanged(nameof(LogicalHighlightRect));
            OnPropertyChanged(nameof(Options));
        }
    }

    /// <summary>選択が有効かどうか。</summary>
    public bool HasSelection
    {
        get => field;
        private set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(Options));
        }
    }

    /// <summary>ハイライトが有効かどうか。</summary>
    public bool HasHighlight
    {
        get => field;
        private set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            OnPropertyChanged(nameof(HasHighlight));
            OnPropertyChanged(nameof(Options));
        }
    }

    /// <summary>情報ラベルに表示するウィンドウタイトル。</summary>
    public string WindowTitle
    {
        get => field;
        private set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(Options));
        }
    } = "";

    /// <summary>選択なしでも全面を暗転するかどうか。</summary>
    public bool ShowFullOverlay
    {
        get => field;
        private set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            OnPropertyChanged(nameof(ShowFullOverlay));
            OnPropertyChanged(nameof(Options));
        }
    }

    /// <summary>オーバーレイ描画モード。</summary>
    public FadeMaskRenderMode Mode
    {
        get => field;
        private set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            OnPropertyChanged(nameof(Mode));
            OnPropertyChanged(nameof(Options));
        }
    } = FadeMaskRenderMode.AreaSelect;

    /// <summary>サイズラベルの文字列を描画時に生成するプロバイダ（Func のためコードビハインドで設定する）。</summary>
    public Func<string?>? SizeLabelOverrideProvider =>
        string.IsNullOrWhiteSpace(_sizeLabelOverride) ? null : () => _sizeLabelOverride;

    /// <summary>サイズラベルの明示指定（空文字は自動生成）。</summary>
    public string SizeLabelOverride
    {
        get => _sizeLabelOverride;
        set
        {
            _sizeLabelOverride = value;
            OnPropertyChanged(nameof(SizeLabelOverride));
            OnPropertyChanged(nameof(SizeLabelOverrideProvider));
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

    /// <summary>描画モードの一覧（表示用）。</summary>
    public IReadOnlyList<string> ModeItems { get; } =
        [nameof(FadeMaskRenderMode.AreaSelect), nameof(FadeMaskRenderMode.ScreenSelect), nameof(FadeMaskRenderMode.WindowSelect), nameof(FadeMaskRenderMode.Crop)];

    /// <summary>描画モードの選択インデックス。</summary>
    /// <remarks>変更時にモード別の描画状態（ハイライト・選択矩形・全面暗転）を切り替える。</remarks>
    public int SelectedModeIndex
    {
        get => _selectedModeIndex;
        set
        {
            if (_selectedModeIndex == value)
            {
                return;
            }

            _selectedModeIndex = value;
            ApplyMode((FadeMaskRenderMode)value);
            OnPropertyChanged(nameof(SelectedModeIndex));
        }
    }

    /// <summary>選択矩形をクリアするコマンド。</summary>
    [RelayCommand]
    private void ClearRect() => SelectionRect = new Rect(0, 0, 0, 0);

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

    /// <summary>現在の選択矩形を論理選択矩形（穴あけ位置）へ反映する。</summary>
    /// <remarks>選択を使わないモード（ScreenSelect / WindowSelect）では選択矩形を反映しない。</remarks>
    private void ApplySelectionRect()
    {
        if (Mode is FadeMaskRenderMode.AreaSelect or FadeMaskRenderMode.Crop)
        {
            LogicalSelectionRect = _selectionRect.Width > 0 && _selectionRect.Height > 0 ? _selectionRect : null;
        }
    }

    /// <summary>モードに応じた描画状態を適用する。</summary>
    /// <param name="mode">適用する描画モード。</param>
    /// <remarks>
    /// 各モードの描画要素を設定する:<br/>
    /// - AreaSelect: 選択矩形 + 操作ラベル（ルーペ・ハンドルは DragRect の責務のため本サンプルでは表示なし）<br/>
    /// - ScreenSelect: ハイライト（+ 操作ラベル）<br/>
    /// - WindowSelect: ハイライト + ウィンドウタイトル（情報ラベル）<br/>
    /// - Crop: 全面暗転 + 選択矩形
    /// </remarks>
    private void ApplyMode(FadeMaskRenderMode mode)
    {
        Mode = mode;

        switch (mode)
        {
            case FadeMaskRenderMode.AreaSelect:
                HasSelection = true;
                HasHighlight = false;
                LogicalHighlightRect = null;
                ShowFullOverlay = false;
                WindowTitle = "";
                ApplySelectionRect();
                break;

            case FadeMaskRenderMode.ScreenSelect:
                HasSelection = false;
                LogicalSelectionRect = null;
                HasHighlight = true;
                LogicalHighlightRect = new Rect(80, 60, 300, 180);
                ShowFullOverlay = false;
                WindowTitle = "";
                break;

            case FadeMaskRenderMode.WindowSelect:
                HasSelection = false;
                LogicalSelectionRect = null;
                HasHighlight = true;
                LogicalHighlightRect = new Rect(80, 60, 300, 180);
                ShowFullOverlay = false;
                WindowTitle = "Sample Window";
                break;

            case FadeMaskRenderMode.Crop:
                HasSelection = true;
                HasHighlight = false;
                LogicalHighlightRect = null;
                ShowFullOverlay = true; // 全面暗転 + 選択矩形
                WindowTitle = "";
                ApplySelectionRect();
                break;
        }
    }

    /// <summary>色を 16 進コード（<c>#AARRGGBB</c>）へ整形する。</summary>
    /// <param name="color">整形する色。</param>
    /// <returns><c>#AARRGGBB</c> 形式の文字列。</returns>
    private static string ToHex(DrawingColor color)
        => $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";

    /// <summary>MainWindowViewModel を初期化する。</summary>
    public MainWindowViewModel()
    {
        // 初期モード（AreaSelect）の描画状態を適用する
        ApplyMode(FadeMaskRenderMode.AreaSelect);
    }
}
