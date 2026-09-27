using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using EsUtil.SaveEvidence.Loupe.UIBase;
using EsUtil.SaveEvidence.Loupe.Sample.Imaging;
using DrawingColor = System.Drawing.Color;
using EsUtil.Feature.LoupeCore.Setting;

namespace EsUtil.SaveEvidence.Loupe.Sample.ViewModels;

/// <summary>メインウィンドウのビューモデル。</summary>
/// <remarks>
/// テスト画像・ルーペの外観設定・中心座標を保持する。<br/>
/// マウス操作（PointerMoved 等）による中心座標の更新は View（コードビハインド）が行う。<br/>
/// </remarks>
public sealed class MainWindowViewModel : ObservableObject
{
    /// <summary>テスト画像（ルーペの <c>Source</c> にバインドする）。</summary>
    public IImage SourceImage { get; } = TestImageFactory.Create();

    /// <summary>十字線の色選択肢。</summary>
    public IReadOnlyList<BrushOption> CrossColorOptions { get; } = BrushOptions.All;

    /// <summary>市松模様 A の色選択肢。</summary>
    public IReadOnlyList<BrushOption> CheckerColorAOptions { get; } = BrushOptions.All;

    /// <summary>市松模様 B の色選択肢。</summary>
    public IReadOnlyList<BrushOption> CheckerColorBOptions { get; } = BrushOptions.All;

    /// <summary>枠線の色選択肢。</summary>
    public IReadOnlyList<BrushOption> BorderColorOptions { get; } = BrushOptions.All;

    // ====================================================================
    // 表示制御
    // ====================================================================

    private bool _isLoupeVisible = true;

    /// <summary>ルーペの表示状態（LoupeControl.IsVisible と TwoWay バインド）。</summary>
    public bool IsLoupeVisible
    {
        get => _isLoupeVisible;
        set => SetProperty(ref _isLoupeVisible, value);
    }

    // ====================================================================
    // ルーペ状態
    // ====================================================================

    private Point? _centerPoint;

    /// <summary>拡大中心（元画像ピクセル座標）。画像の外では null。</summary>
    public Point? CenterPoint
    {
        get => _centerPoint;
        set
        {
            if (SetProperty(ref _centerPoint, value))
            {
                OnPropertyChanged(nameof(CoordinateText));
            }
        }
    }

    // ====================================================================
    // 外観設定
    // ====================================================================

    private int _zoomLevel = 4;

    /// <summary>拡大率（1〜16）。</summary>
    public int ZoomLevel
    {
        get => _zoomLevel;
        set
        {
            if (SetProperty(ref _zoomLevel, value))
            {
                OnPropertyChanged(nameof(ZoomText));
                OnPropertyChanged(nameof(CoordinateText));
                OnPropertyChanged(nameof(Options));
                OnPropertyChanged(nameof(Paint));
            }
        }
    }

    private int _requestedSize = 240;

    /// <summary>ルーペの基本サイズ（DIP・ユーザー設定値）。表示サイズはコントロール（LoupeControl）がクランプを内包して導出する。</summary>
    public int RequestedSize
    {
        get => _requestedSize;
        set
        {
            if (SetProperty(ref _requestedSize, value))
            {
                OnPropertyChanged(nameof(RequestedSizeText));
                OnPropertyChanged(nameof(CoordinateText));
                OnPropertyChanged(nameof(Options));
                OnPropertyChanged(nameof(Paint));
            }
        }
    }

    // ====================================================================
    // 選択矩形の破線オーバーレイ（SelectionRect 系）
    // ====================================================================

    private bool _selectionRectVisible;

    /// <summary>選択矩形の破線オーバーレイを表示するかどうか（SelectionRectVisible）。</summary>
    /// <remarks>
    /// true にすると <see cref="SelectionRect"/>（ドメイン座標系 = 元画像ピクセル座標）の枠線を
    /// ルーペ画像上に破線で描画する（ズーム 2 倍以上で有効）。<br/>
    /// </remarks>
    public bool SelectionRectVisible
    {
        get => _selectionRectVisible;
        set
        {
            if (SetProperty(ref _selectionRectVisible, value))
            {
                OnPropertyChanged(nameof(SelectionRectLabel));
            }
        }
    }

    private double _selectionRectX = 100;

    /// <summary>選択矩形の X 座標（ドメイン座標系 = 元画像ピクセル）。</summary>
    public double SelectionRectX
    {
        get => _selectionRectX;
        set
        {
            if (SetProperty(ref _selectionRectX, value))
            {
                OnPropertyChanged(nameof(SelectionRectLabel));
            }
        }
    }

    private double _selectionRectY = 80;

    /// <summary>選択矩形の Y 座標（ドメイン座標系 = 元画像ピクセル）。</summary>
    public double SelectionRectY
    {
        get => _selectionRectY;
        set
        {
            if (SetProperty(ref _selectionRectY, value))
            {
                OnPropertyChanged(nameof(SelectionRectLabel));
            }
        }
    }

    private double _selectionRectWidth = 200;

    /// <summary>選択矩形の幅（ドメイン座標系 = 元画像ピクセル）。</summary>
    public double SelectionRectWidth
    {
        get => _selectionRectWidth;
        set
        {
            if (SetProperty(ref _selectionRectWidth, value))
            {
                OnPropertyChanged(nameof(SelectionRectLabel));
            }
        }
    }

    private double _selectionRectHeight = 120;

    /// <summary>選択矩形の高さ（ドメイン座標系 = 元画像ピクセル）。</summary>
    public double SelectionRectHeight
    {
        get => _selectionRectHeight;
        set
        {
            if (SetProperty(ref _selectionRectHeight, value))
            {
                OnPropertyChanged(nameof(SelectionRectLabel));
            }
        }
    }

    /// <summary>選択矩形の座標ラベル（テスト画像上のオーバーレイ枠に表示）。</summary>
    public string SelectionRectLabel =>
        $"({SelectionRectX:F0}, {SelectionRectY:F0}) {SelectionRectWidth:F0}x{SelectionRectHeight:F0}";

    private int _checkerSize = 8;

    /// <summary>市松模様のタイルサイズ。</summary>
    public int CheckerSize
    {
        get => _checkerSize;
        set
        {
            if (SetProperty(ref _checkerSize, value))
            {
                OnPropertyChanged(nameof(CheckerSizeText));
                OnPropertyChanged(nameof(Options));
                OnPropertyChanged(nameof(Paint));
            }
        }
    }

    private int _borderThickness = 2;

    /// <summary>枠線の太さ（ピクセル）。</summary>
    public int BorderThickness
    {
        get => _borderThickness;
        set
        {
            if (SetProperty(ref _borderThickness, value))
            {
                OnPropertyChanged(nameof(BorderThicknessText));
                OnPropertyChanged(nameof(Options));
                OnPropertyChanged(nameof(Paint));
            }
        }
    }

    private double _dipToDeviceScaling = 1.0;

    /// <summary>DIP→物理ピクセル変換のスケーリング係数。</summary>
    public double DipToDeviceScaling
    {
        get => _dipToDeviceScaling;
        set
        {
            if (SetProperty(ref _dipToDeviceScaling, value))
            {
                OnPropertyChanged(nameof(DipToDeviceScalingText));
            }
        }
    }

    // ====================================================================
    // 色
    // ====================================================================

    private BrushOption _crossColorOption = BrushOptions.Red;

    /// <summary>十字線の色選択。</summary>
    public BrushOption CrossColorOption
    {
        get => _crossColorOption;
        set
        {
            if (SetProperty(ref _crossColorOption, value))
            {
                OnPropertyChanged(nameof(Options));
                OnPropertyChanged(nameof(Paint));
            }
        }
    }

    private BrushOption _checkerColorAOption = BrushOptions.White;

    /// <summary>市松模様 A の色選択。</summary>
    public BrushOption CheckerColorAOption
    {
        get => _checkerColorAOption;
        set
        {
            if (SetProperty(ref _checkerColorAOption, value))
            {
                OnPropertyChanged(nameof(Options));
                OnPropertyChanged(nameof(Paint));
            }
        }
    }

    private BrushOption _checkerColorBOption = BrushOptions.CheckerB;

    /// <summary>市松模様 B の色選択。</summary>
    public BrushOption CheckerColorBOption
    {
        get => _checkerColorBOption;
        set
        {
            if (SetProperty(ref _checkerColorBOption, value))
            {
                OnPropertyChanged(nameof(Options));
                OnPropertyChanged(nameof(Paint));
            }
        }
    }

    private BrushOption _borderColorOption = BrushOptions.LightGray;

    /// <summary>枠線の色選択。</summary>
    public BrushOption BorderColorOption
    {
        get => _borderColorOption;
        set
        {
            if (SetProperty(ref _borderColorOption, value))
            {
                OnPropertyChanged(nameof(Options));
                OnPropertyChanged(nameof(Paint));
            }
        }
    }

    /// <summary>ルーペの色（描画にだけ必要な設定）。</summary>
    /// <value>選択中の色から組み立てた色。依存する選択肢の変更で再評価される。</value>
    /// <remarks>色の既定値・型は描画の設定（<c>LoupePaint</c>）が単一所有する（コアは色を持たない）。</remarks>
    public LoupePaint Paint => new()
    {
        CrossColor = ToDrawingColor(CrossColorOption),
        CheckerColorA = ToDrawingColor(CheckerColorAOption),
        CheckerColorB = ToDrawingColor(CheckerColorBOption),
        BorderColor = ToDrawingColor(BorderColorOption),
    };

    /// <summary>ルーペの設定（表示サイズ・拡大率・寸法。コアの <see cref="LoupeOptions"/> が単一所有する）。</summary>
    /// <value>現在の設定（コントロールの <c>Options</c> へ一括でバインドする）。</value>
    /// <remarks>
    /// 設定を 1 つの値にまとめて公開する（コントロール側の公開プロパティも 1 つである）。<br/>
    /// 依存する値（拡大率・サイズ・外観の項目）の変更は各 setter が <c>Options</c> の変更を通知する。<br/>
    /// 色はコアの設定に含めない（描画にだけ必要な設定のため、コントロールの <c>Paint</c> へ与える）。
    /// </remarks>
    public LoupeOptions Options => new()
    {
        RequestedSize = RequestedSize,
        ZoomLevel = ZoomLevel,
        CheckerSize = CheckerSize,
        BorderThickness = BorderThickness,
    };

    /// <summary>色選択肢をコアの色型へ変換する（「なし」はアルファ 0 = 描画しない）。</summary>
    /// <param name="option">色選択肢。</param>
    /// <returns>コアの色。</returns>
    private static DrawingColor ToDrawingColor(BrushOption option)
        => option.Color is { } color ? DrawingColor.FromArgb(color.A, color.R, color.G, color.B) : DrawingColor.Transparent;

    // ====================================================================
    // 表示テキスト
    // ====================================================================

    /// <summary>拡大率の表示文字列。</summary>
    public string ZoomText => $"{ZoomLevel}x";

    /// <summary>ルーペサイズの表示文字列。</summary>
    public string RequestedSizeText => $"{RequestedSize} DIP";

    /// <summary>市松模様タイルサイズの表示文字列。</summary>
    public string CheckerSizeText => $"{CheckerSize} px";

    /// <summary>枠線の太さの表示文字列。</summary>
    public string BorderThicknessText => $"{BorderThickness} px";

    /// <summary>描画スケールの表示文字列。</summary>
    public string DipToDeviceScalingText => $"{DipToDeviceScaling:F2}";

    /// <summary>中心座標の表示文字列。</summary>
    /// <remarks>選択矩形の破線オーバーレイの表示状態（SelectionRectVisible）も付加する。</remarks>
    public string CoordinateText => CenterPoint is { } p
        ? $"中心座標: ({p.X:F0}, {p.Y:F0})  拡大率: {ZoomLevel}x  サイズ: {RequestedSize} DIP"
        + (SelectionRectVisible ? $"  選択矩形: ({SelectionRectX:F0}, {SelectionRectY:F0}) {SelectionRectWidth:F0}x{SelectionRectHeight:F0}" : "")
        : "画像の外です（市松模様のみ表示）";
}
