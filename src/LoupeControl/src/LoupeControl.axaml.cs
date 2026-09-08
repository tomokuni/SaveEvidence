using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Estable.AvaloniaUI.Loupe.Imaging;
using Estable.AvaloniaUI.Loupe.Logging;
using Estable.AvaloniaUI.Loupe.Rendering;
using Estable.AvaloniaUI.Platform;
using Estable.AvaloniaUI.Platform.Coordinate;
using Estable.AvaloniaUI.Platform.Geometry;
using Estable.AvaloniaUI.Platform.Imaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using static Estable.AvaloniaUI.Loupe.LoupeValueCoercer;

namespace Estable.AvaloniaUI.Loupe;

/// <summary>拡大鏡（ルーペ）コントロール。</summary>
/// <remarks>
/// 領域選択時とプレビュー画像切出し時の両方で<br/>
/// 共通利用するための単一のコントロール。<br/>
/// 入力はすべて StyledProperty（<see cref="Source"/> / <see cref="CenterPoint"/> / 外観）として公開され、<br/>
/// バインディングまたはコードから制御できる。<br/>
/// <see cref="Source"/>（元画像）と <see cref="CenterPoint"/>（拡大中心の元画像ピクセル座標）を与えると、<br/>
/// 中心周辺領域を切り出して拡大描画する（Binding 方式）。<br/>
/// 表示は内部の <see cref="Image"/> 要素へ生成Bitmapを設定する方式に統一する。<br/>
/// <br/>
/// <b>描画の委譲</b>: 描画（抽出→拡大→十字線→市松→外枠）は <see cref="LoupeRenderer"/> に委譲し、<br/>
/// 本コントロールは状態（DP）とライフサイクル（ビットマップ生成/解放）に専念する。<br/>
/// <br/>
/// <b>再描画のバッチ化</b>: 複数プロパティの連続変更は <see cref="Dispatcher"/> へのポストにより<br/>
/// 1フレームに1回の再描画へ結合される（複数プロパティの多重再描画を防止）。<br/>
/// <b>表示状態</b>: 表示・非表示の指示は標準プロパティ <see cref="Visual.IsVisible"/> で行う。<br/>
/// 表示時（true）は最新の画像を非同期生成し、非表示時（false）はビットマップを解放する。<br/>
/// 非表示中のプロパティ変更では画像を生成しない。<br/>
/// <br/>
/// <b>基底クラス</b>: 外観が生成ビットマップのみで構成される単一用途のコントロールであるため、<br/>
/// <see cref="UserControl"/> を採用する（テンプレート差し替え余地を提供する <c>TemplatedControl</c> は不要であり、KISS に従う）。<br/>
/// <br/>
/// <b>色指定</b>: 色は <see cref="IBrush"/> で指定する（文字列解析は行わない）。<br/>
/// 不透明度は色のアルファに統合する（例: 十字線の赤 40% = <c>#66FF0000</c> の <see cref="SolidColorBrush"/>）。<br/>
/// 数値プロパティ（<see cref="RequestedSize"/> / <see cref="ZoomLevel"/> / <see cref="RenderScaling"/> /<br/>
/// <see cref="CheckerSize"/> / <see cref="FrameWidth"/> / <see cref="CoordLabelFontSize"/>）は範囲外の値を安全域へ補正（coerce）する。<br/>
/// <br/>
/// **公開 API 一覧**:<br/>
/// - 入力: <see cref="Source"/> / <see cref="CenterPoint"/><br/>
/// - サイズ: <see cref="RequestedSize"/> / <see cref="RenderScaling"/> / <see cref="ZoomLevel"/><br/>
/// - 外観: <see cref="CrossColor"/> / <see cref="CheckerSize"/> / <see cref="CheckerColorA"/> / <see cref="CheckerColorB"/> / <see cref="FrameColor"/> / <see cref="FrameWidth"/><br/>
/// - ログ: <see cref="Log"/><br/>
/// - イベント: <see cref="Rendered"/>（描画完了時に描画済み Bitmap と表示情報を通知。ホストが Bitmap に直接オーバーレイを描画できる）<br/>
/// - 操作: <see cref="Clear()"/> / <see cref="ForceUpdate()"/> / <see cref="RenderAsync()"/><br/>
/// </remarks>
public sealed partial class LoupeControl : UserControl
{
    /// <summary>座標ラベル用ブラシキャッシュ（色 → ブラシ。容量制限付き・PlatformFactory で共通化）。</summary>
    private static readonly BrushCache s_brushCache = new();

    // ====================================================================
    // 数値プロパティの安全域・既定値（LoupeValueCoercer から委譲）
    // ====================================================================

    /// <summary><see cref="LoupeControl"/> クラスの新しいインスタンスを初期化します。</summary>
    /// <remarks>
    /// <see cref="InitializeComponent()"/> を呼び出して AXAML で定義された子要素を生成します。<br/>
    /// </remarks>
    public LoupeControl()
    {
        // InitializeComponent() は IsVisible 変更イベントを発火するため、コーディネーターを先に初期化する
        _renderCoordinator = CreateRenderCoordinator();
        InitializeComponent();
        // 初期状態は非表示（表示指示は IsVisible=true で行う。XAML 属性ではなくコンストラクタで設定する）
        IsVisible = false;
        UpdateCoordLabel();
    }

    /// <summary>再描画コーディネーターを、現在の表示状態・ロガー・解放処理を注入して生成する。</summary>
    private LoupeRenderCoordinator CreateRenderCoordinator()
        => new(
            isVisible: () => IsVisible,
            releaseBitmap: ReleaseBitmap,
            startRender: StartRender,
            logRenderCore: generation => Log.LogRenderCore(generation),
            isLogEnabled: () => Log is not NullLogger);

    // ====================================================================
    // 座標変換ポート（DragRectControl と同方式）
    // ====================================================================

    /// <summary>論理座標（ドメイン）↔ 親パネル DIP の座標変換ポート。</summary>
    private ICoordTransform _coordinatePort = CoordTransform.Identity;

    /// <summary>表示位置（RenderTransform）のインスタンス（座標更新時は値を書き換えて再利用する）。</summary>
    private readonly TranslateTransform _positionTransform = new();

    /// <summary>再描画のバッチ化コーディネーター（Dispatcher へのポストで 1 フレームに 1 回に結合）。</summary>
    private LoupeRenderCoordinator _renderCoordinator;

    /// <summary>ルーペ中心（<see cref="CenterPoint"/>）からの右下オフセット（DIP）を取得または設定する。</summary>
    /// <value>カーソル位置（ルーペ中心）からの右下方向へのオフセット量（DIP）。</value>
    /// <remarks>
    /// ホスト（上位 AP）が設定する。設定後の位置反映は <see cref="UpdatePosition"/> で行う。<br/>
    /// C#14 の <c>field</c> キーワードでバッキングフィールドを暗黙定義する。
    /// </remarks>
    public double PositionOffset
    {
        get => field;
        set
        {
            field = value;
            UpdatePosition();
        }
    }

    /// <summary>論理座標（ドメイン）↔ 親パネル DIP の座標変換ポートを設定する。</summary>
    /// <param name="port">座標変換ポート（Inverse = 論理→親パネルDIP）。null の場合は等価変換。</param>
    /// <remarks>
    /// DragRectControl と同様に、外部から与えたポートで表示位置（<see cref="Visual.RenderTransform"/>）と<br/>
    /// 表示サイズ（親パネルサイズによるクランプ）を計算する。<br/>
    /// 変換関数は呼び出し毎に評価されるため、ウィンドウサイズ変更・ズーム・スクロール等の<br/>
    /// レイアウト変化に追従できる（イベント時の DIP キャッシュは使わない）。
    /// </remarks>
    public void SetCoordinateTransform(ICoordTransform? port)
    {
        _coordinatePort = port ?? CoordTransform.Identity;
        UpdatePosition();
    }

    /// <summary>表示位置と表示サイズを最新のレイアウトから再計算する（外部からの更新要求）。</summary>
    /// <remarks>
    /// パン・ズーム等のレイアウト非変更なビューポート変化時に、ホスト（上位 AP）が呼び出す。<br/>
    /// 座標変換ポートは呼び出し毎に最新の表示状態を評価するため、再計算だけで追従する。
    /// </remarks>
    public void RefreshPosition() => UpdatePosition();

    /// <summary>表示位置と表示サイズを最新のレイアウトから再計算する。</summary>
    /// <remarks>
    /// 座標変換ポート（<see cref="SetCoordinateTransform"/>）・<see cref="CenterPoint"/>・親パネルサイズから、<br/>
    /// ルーペの表示サイズ（親パネルより大きい場合はクランプ）と表示位置（カーソル右下 + オフセット + クランプ）を<br/>
    /// 計算して反映する。<br/>
    /// 呼び出しタイミング: <see cref="CenterPoint"/> 変更・ポート設定・<see cref="PositionOffset"/> 変更・レイアウト（Arrange）。<br/>
    /// 非表示・ポート未設定・中心未設定の場合は何もしない。
    /// </remarks>
    private void UpdatePosition()
    {
        if (!IsVisible || _coordinatePort is not { } port || CenterPoint is not { } center) return;
        if (Parent is not Panel parent) return;

        var parentSize = parent.Bounds.Size;
        var hasParentSize = parentSize.Width > 0 && parentSize.Height > 0;

        // 座標変換（ドメイン → 親パネル DIP）は ICoordTransform に委譲し、以降のサイズ・位置計算は純粋関数に委譲する
        var centerDip = port.Inverse(center.ToPoint());
        var (size, position) = LoupePositionCalculator.Compute(
            RequestedSize, parentSize, hasParentSize, centerDip, PositionOffset);

        _displaySize = (int)size.Width;
        Width = size.Width;
        Height = size.Height;

        // 同一インスタンスの値を更新して再利用する（毎回の生成を回避。StyledProperty 変更で再適用される）
        _positionTransform.X = position.X;
        _positionTransform.Y = position.Y;
        RenderTransform = _positionTransform;
    }

    /// <summary>レイアウト（Arrange）時に表示位置・サイズを再計算する（ウィンドウサイズ変更等に追従）。</summary>
    /// <param name="finalRect">最終レイアウト矩形。</param>
    protected override void ArrangeCore(Rect finalRect)
    {
        base.ArrangeCore(finalRect);
        UpdatePosition();
    }

    /// <summary>入力（Source / CenterPoint / 外観）の変更で再描画を要求する。</summary>
    /// <remarks>
    /// 各プロパティの変更は <see cref="RequestRender"/> を呼び出し、<br/>
    /// 複数プロパティの連続変更を1回の再描画に結合する（バッチ化）。<br/>
    /// </remarks>
    static LoupeControl()
    {
        SourceProperty.Changed.AddClassHandler<LoupeControl>(static (o, e) =>
        {
            o.Log.LogSettingChanged(nameof(Source), e.OldValue?.ToString() ?? "null", e.NewValue?.ToString() ?? "null");
            o.RequestRender();
        });
        // CenterPoint 変更時は再描画に加えて表示位置（RenderTransform）も更新する（座標変換ポート + 親パネルサイズから算出）
        CenterPointProperty.Changed.AddClassHandler<LoupeControl>(static (o, e) =>
        {
            o.Log.LogSettingChanged(nameof(CenterPoint), e.OldValue?.ToString() ?? "null", e.NewValue?.ToString() ?? "null");
            o.RequestRender();
            o.UpdatePosition();
            o.UpdateCoordLabel();
        });
        RequestedSizeProperty.Changed.AddClassHandler<LoupeControl>(static (o, e) =>
        {
            // OldValue / NewValue は StyledProperty&lt;int&gt; により常に非 null（boxed int）
            o.Log.LogSettingChanged(nameof(RequestedSize), e.OldValue!.ToString()!, e.NewValue!.ToString()!);
            o.RequestRender();
            o.UpdatePosition();
        });
        RenderScalingProperty.Changed.AddClassHandler<LoupeControl>(static (o, e) =>
        {
            // OldValue / NewValue は StyledProperty&lt;double&gt; により常に非 null（boxed double）
            o.Log.LogSettingChanged(nameof(RenderScaling), e.OldValue!.ToString()!, e.NewValue!.ToString()!);
            o.RequestRender();
        });
        ZoomLevelProperty.Changed.AddClassHandler<LoupeControl>(static (o, e) =>
        {
            // OldValue / NewValue は StyledProperty&lt;int&gt; により常に非 null（boxed int）
            o.Log.LogSettingChanged(nameof(ZoomLevel), e.OldValue!.ToString()!, e.NewValue!.ToString()!);
            o.RequestRender();
        });
        // 再描画のみが必要な外観プロパティは一括登録する（同一ハンドラの定義重複を排除）
        AvaloniaProperty[] renderOnlyProperties =
        [
            CrossColorProperty,
            CheckerSizeProperty,
            CheckerColorAProperty,
            CheckerColorBProperty,
            FrameColorProperty,
            FrameWidthProperty,
        ];
        foreach (var property in renderOnlyProperties)
        {
            property.Changed.AddClassHandler<LoupeControl>(static (o, _) => o.RequestRender());
        }

        // 座標ラベルのみを更新するプロパティも一括登録する
        AvaloniaProperty[] coordLabelProperties =
        [
            ShowCoordLabelProperty,
            CoordLabelColorProperty,
            CoordLabelBackgroundColorProperty,
            CoordLabelFontSizeProperty,
        ];
        foreach (var property in coordLabelProperties)
        {
            property.Changed.AddClassHandler<LoupeControl>(static (o, _) => o.UpdateCoordLabel());
        }

        IsVisibleProperty.Changed.AddClassHandler<LoupeControl>(static (o, e) => o.OnIsVisibleChanged(e));
    }

    // ====================================================================
    // 依存関係プロパティ
    // ====================================================================

    /// <summary><see cref="Source"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<IImage?> SourceProperty =
        AvaloniaProperty.Register<LoupeControl, IImage?>(nameof(Source));

    /// <summary><see cref="CenterPoint"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<Point?> CenterPointProperty =
        AvaloniaProperty.Register<LoupeControl, Point?>(nameof(CenterPoint));

    /// <summary><see cref="RequestedSize"/> 依存関係プロパティ（1〜4096 に補正）。</summary>
    public static readonly StyledProperty<int> RequestedSizeProperty =
        AvaloniaProperty.Register<LoupeControl, int>(nameof(RequestedSize), defaultValue: RequestedSizeDefault, coerce: CoerceRequestedSize);

    /// <summary>表示DIPを物理ピクセルへ変換するスケーリング係数。</summary>
    public static readonly StyledProperty<double> RenderScalingProperty =
        AvaloniaProperty.Register<LoupeControl, double>(nameof(RenderScaling), defaultValue: RenderScalingDefault, coerce: CoerceRenderScaling);

    /// <summary><see cref="ZoomLevel"/> 依存関係プロパティ（1〜64 に補正）。</summary>
    public static readonly StyledProperty<int> ZoomLevelProperty =
        AvaloniaProperty.Register<LoupeControl, int>(nameof(ZoomLevel), defaultValue: ZoomLevelDefault, coerce: CoerceZoomLevel);

    /// <summary>十字線の色の既定ブラシ（赤 40% = アルファ 102）。</summary>
    private static readonly IBrush DefaultCrossColor = new SolidColorBrush(Color.FromAColor(102, Colors.Red));

    /// <summary><see cref="CrossColor"/> 依存関係プロパティ（不透明度は色のアルファに統合）。</summary>
    public static readonly StyledProperty<IBrush?> CrossColorProperty =
        AvaloniaProperty.Register<LoupeControl, IBrush?>(nameof(CrossColor), defaultValue: DefaultCrossColor);

    /// <summary><see cref="CheckerSize"/> 依存関係プロパティ（1〜128 に補正）。</summary>
    public static readonly StyledProperty<int> CheckerSizeProperty =
        AvaloniaProperty.Register<LoupeControl, int>(nameof(CheckerSize), defaultValue: CheckerSizeDefault, coerce: CoerceCheckerSize);

    /// <summary>市松模様の色Aの既定ブラシ（Gray 30% = #4C808080。設定画面の市松模様 A と一致）。全インスタンスで共有する不変ブラシ。</summary>
    /// <remarks>
    /// static フィールド初期化子は宣言順に実行されるため、<see cref="CheckerColorAProperty"/> より先に宣言する<br/>
    /// （逆順だとプロパティ登録時に null がデフォルト値として固定される）。<br/>
    /// </remarks>
    private static readonly IBrush DefaultCheckerColorA = new SolidColorBrush(Color.FromAColor(0x4C, Colors.Gray));

    /// <summary><see cref="CheckerColorA"/> 依存関係プロパティ。</summary>
    /// <remarks>既定は Gray 30%（<c>#4C808080</c>。設定画面の市松模様 A と一致）。</remarks>
    public static readonly StyledProperty<IBrush?> CheckerColorAProperty =
        AvaloniaProperty.Register<LoupeControl, IBrush?>(nameof(CheckerColorA), defaultValue: DefaultCheckerColorA);

    /// <summary>市松模様の色Bの既定ブラシ（Silver 30% = #4CA9A9A9。設定画面の市松模様 B と一致）。全インスタンスで共有する不変ブラシ。</summary>
    /// <remarks>
    /// static フィールド初期化子は宣言順に実行されるため、<see cref="CheckerColorBProperty"/> より先に宣言する<br/>
    /// （逆順だとプロパティ登録時に null がデフォルト値として固定される）。<br/>
    /// </remarks>
    private static readonly IBrush DefaultCheckerColorB = new SolidColorBrush(Color.FromAColor(0x4C, Colors.DarkGray));

    /// <summary><see cref="CheckerColorB"/> 依存関係プロパティ。</summary>
    /// <remarks>既定は Silver 30%（<c>#4CA9A9A9</c>。設定画面の市松模様 B と一致）。</remarks>
    public static readonly StyledProperty<IBrush?> CheckerColorBProperty =
        AvaloniaProperty.Register<LoupeControl, IBrush?>(nameof(CheckerColorB), defaultValue: DefaultCheckerColorB);

    /// <summary><see cref="FrameColor"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<IBrush?> FrameColorProperty =
        AvaloniaProperty.Register<LoupeControl, IBrush?>(nameof(FrameColor), defaultValue: Brushes.LightGray);

    /// <summary><see cref="FrameWidth"/> 依存関係プロパティ（1〜64 に補正）。</summary>
    public static readonly StyledProperty<int> FrameWidthProperty =
        AvaloniaProperty.Register<LoupeControl, int>(nameof(FrameWidth), defaultValue: FrameWidthDefault, coerce: CoerceFrameWidth);

    /// <summary><see cref="ShowCoordLabel"/> 依存関係プロパティ。</summary>
    /// <remarks>既定は true（設定画面のルーペ座標ラベル表示のデフォルトと一致）。</remarks>
    public static readonly StyledProperty<bool> ShowCoordLabelProperty =
        AvaloniaProperty.Register<LoupeControl, bool>(nameof(ShowCoordLabel), defaultValue: true);

    /// <summary><see cref="CoordLabelColor"/> 依存関係プロパティ（座標ラベルの文字色）。</summary>
    public static readonly StyledProperty<IBrush?> CoordLabelColorProperty =
        AvaloniaProperty.Register<LoupeControl, IBrush?>(nameof(CoordLabelColor), defaultValue: Brushes.White);

    /// <summary>中心座標ラベルの背景色の既定ブラシ（黒 60% = アルファ 153）。</summary>
    private static readonly IBrush DefaultCoordLabelBackgroundColor = new SolidColorBrush(Color.FromAColor(153, Colors.Black));

    /// <summary><see cref="CoordLabelBackgroundColor"/> 依存関係プロパティ（座標ラベルの背景色・不透明度はアルファに統合）。</summary>
    public static readonly StyledProperty<IBrush?> CoordLabelBackgroundColorProperty =
        AvaloniaProperty.Register<LoupeControl, IBrush?>(nameof(CoordLabelBackgroundColor), defaultValue: DefaultCoordLabelBackgroundColor);

    /// <summary><see cref="CoordLabelFontSize"/> 依存関係プロパティ（座標ラベルのフォントサイズ 6〜48 に補正）。</summary>
    public static readonly StyledProperty<double> CoordLabelFontSizeProperty =
        AvaloniaProperty.Register<LoupeControl, double>(nameof(CoordLabelFontSize), defaultValue: CoordLabelFontSizeDefault, coerce: CoerceCoordLabelFontSize);

    /// <summary><see cref="RequestedSize"/> を安全域へ補正する（1〜4096）。</summary>
    private static int CoerceRequestedSize(AvaloniaObject o, int v) => LoupeValueCoercer.CoerceRequestedSize(v);

    /// <summary><see cref="ZoomLevel"/> を安全域へ補正する。</summary>
    private static int CoerceZoomLevel(AvaloniaObject o, int v) => LoupeValueCoercer.CoerceZoomLevel(v);

    /// <summary>描画スケーリング係数を安全域へ補正する。</summary>
    private static double CoerceRenderScaling(AvaloniaObject o, double v) => LoupeValueCoercer.CoerceRenderScaling(v);

    /// <summary><see cref="CheckerSize"/> を安全域へ補正する。</summary>
    private static int CoerceCheckerSize(AvaloniaObject o, int v) => LoupeValueCoercer.CoerceCheckerSize(v);

    /// <summary><see cref="FrameWidth"/> を安全域へ補正する。</summary>
    private static int CoerceFrameWidth(AvaloniaObject o, int v) => LoupeValueCoercer.CoerceFrameWidth(v);

    /// <summary><see cref="CoordLabelFontSize"/> を安全域へ補正する。</summary>
    private static double CoerceCoordLabelFontSize(AvaloniaObject o, double v) => LoupeValueCoercer.CoerceCoordLabelFontSize(v);

    // ====================================================================
    // 公開プロパティ（入力）
    // ====================================================================

    /// <summary>拡大元の元画像（100%画像）。Binding で与える。</summary>
    public IImage? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>拡大表示する中心座標（元画像ピクセル座標）。Binding で与える。</summary>
    public Point? CenterPoint
    {
        get => GetValue(CenterPointProperty);
        set => SetValue(CenterPointProperty, value);
    }

    // ====================================================================
    // 公開プロパティ（サイズ）
    // ====================================================================

    /// <summary>実際の表示サイズ（DIP）。クランプ済みの値（<see cref="RequestedSize"/> を親パネルサイズにクランプ）。</summary>
    /// <remarks>
    /// 基本サイズ（<see cref="RequestedSize"/>）から <see cref="UpdatePosition"/> が親パネルサイズでクランプし、<br/>
    /// 本フィールドへ反映する（表示サイズはコントロールが自己管理。ホストは関与しない）。
    /// </remarks>
    private int _displaySize = RequestedSizeDefault;

    /// <summary>ルーペの基本サイズ（DIP・ユーザー設定値）を取得または設定する（1〜4096）。</summary>
    /// <remarks>
    /// 表示サイズは本値（基本サイズ）を <see cref="UpdatePosition"/> が親パネルサイズにクランプして導出する<br/>
    /// （クランプはコントロールが内包し、ホストは <see cref="RequestedSize"/> を設定するだけ）。<br/>
    /// 親パネル未接続時（配置前・テスト等）のフォールバックとして、表示サイズへ基本サイズを同期する。
    /// </remarks>
    public int RequestedSize
    {
        get => GetValue(RequestedSizeProperty);
        set
        {
            SetValue(RequestedSizeProperty, value);
            // 表示サイズのフォールバック: 親パネル未接続（UpdatePosition 未実行）でも基本サイズで描画する
            _displaySize = GetValue(RequestedSizeProperty);
        }
    }

    /// <summary>表示DIPを物理ピクセルへ変換するスケーリング係数を取得または設定します。</summary>
    public double RenderScaling
    {
        get => GetValue(RenderScalingProperty);
        set => SetValue(RenderScalingProperty, value);
    }

    /// <summary>拡大率を取得または設定する（1〜64、偶数推奨）。</summary>
    public int ZoomLevel
    {
        get => GetValue(ZoomLevelProperty);
        set => SetValue(ZoomLevelProperty, value);
    }

    /// <summary>十字線の色を取得または設定する。</summary>
    /// <remarks>不透明度は色のアルファに統合する（例: 赤 40% = <c>#66FF0000</c>）。</remarks>
    public IBrush? CrossColor
    {
        get => GetValue(CrossColorProperty);
        set => SetValue(CrossColorProperty, value);
    }

    /// <summary>市松模様の1タイルサイズ（DIP）を取得または設定する（1〜128）。</summary>
    public int CheckerSize
    {
        get => GetValue(CheckerSizeProperty);
        set => SetValue(CheckerSizeProperty, value);
    }

    /// <summary>市松模様の色A（明色）を取得または設定する。</summary>
    public IBrush? CheckerColorA
    {
        get => GetValue(CheckerColorAProperty);
        set => SetValue(CheckerColorAProperty, value);
    }

    /// <summary>市松模様の色B（暗色）を取得または設定する。</summary>
    public IBrush? CheckerColorB
    {
        get => GetValue(CheckerColorBProperty);
        set => SetValue(CheckerColorBProperty, value);
    }

    /// <summary>外枠の色を取得または設定する。</summary>
    public IBrush? FrameColor
    {
        get => GetValue(FrameColorProperty);
        set => SetValue(FrameColorProperty, value);
    }

    /// <summary>外枠の太さ（ピクセル）を取得または設定する（1〜64）。</summary>
    public int FrameWidth
    {
        get => GetValue(FrameWidthProperty);
        set => SetValue(FrameWidthProperty, value);
    }

    /// <summary>ルーペの上側に中心座標ラベルを表示するかどうかを取得または設定する。</summary>
    /// <remarks>既定は false（非表示）。true にすると <see cref="CenterPoint"/>（ドメイン座標）を "(x, y)" 形式でルーペの上端に表示する。</remarks>
    public bool ShowCoordLabel
    {
        get => GetValue(ShowCoordLabelProperty);
        set
        {
            SetValue(ShowCoordLabelProperty, value);
            UpdateCoordLabel();
        }
    }

    /// <summary>中心座標ラベルの文字色を取得または設定する。</summary>
    public IBrush? CoordLabelColor
    {
        get => GetValue(CoordLabelColorProperty);
        set
        {
            SetValue(CoordLabelColorProperty, value);
            UpdateCoordLabel();
        }
    }

    /// <summary>中心座標ラベルの背景色を取得または設定する。</summary>
    /// <remarks>不透明度は色のアルファに統合する（既定: 黒 50% = <c>#80000000</c>）。</remarks>
    public IBrush? CoordLabelBackgroundColor
    {
        get => GetValue(CoordLabelBackgroundColorProperty);
        set
        {
            SetValue(CoordLabelBackgroundColorProperty, value);
            UpdateCoordLabel();
        }
    }

    /// <summary>中心座標ラベルのフォントサイズを取得または設定する（6〜48）。</summary>
    public double CoordLabelFontSize
    {
        get => GetValue(CoordLabelFontSizeProperty);
        set
        {
            SetValue(CoordLabelFontSizeProperty, value);
            UpdateCoordLabel();
        }
    }

    /// <summary>中心座標ラベルの表示を更新する（テキスト・色・背景・サイズ・表示状態）。</summary>
    /// <remarks>
    /// <see cref="CenterPoint"/>（ドメイン座標 = 元画像ピクセル座標）を "(x, y)" 形式で表示する。<br/>
    /// <see cref="ShowCoordLabel"/> が false の場合は非表示にする。<br/>
    /// 座標は整数に丸めて表示する（ピクセル座標として可読性を優先）。
    /// </remarks>
    private void UpdateCoordLabel()
    {
        if (PART_CoordLabel is null || PART_CoordText is null) return;

        // CenterPoint は一度だけ取得し、有無判定と座標参照で共有する（null-forgiving 演算子を使わない）
        var centerPoint = CenterPoint;
        PART_CoordLabel.IsVisible = ShowCoordLabel && centerPoint is not null;
        if (!PART_CoordLabel.IsVisible) return;

        var c = centerPoint!.Value;
        PART_CoordText.Text = CoordinateFormatter.Format(c);
        PART_CoordText.Foreground = CoordLabelColor;
        PART_CoordText.FontSize = CoordLabelFontSize;
        // 背景色（不透明度は色のアルファに統合）を Border へ反映する（SolidColorBrush は静的キャッシュで共有）
        var bg = CoordLabelBackgroundColor ?? Brushes.Black;
        PART_CoordLabel.Background = bg is SolidColorBrush scb
            ? GetSharedBrush(scb.Color)
            : bg;
    }

    /// <summary>色に対応するブラシを静的キャッシュから取得する（未登録なら生成して登録）。</summary>
    /// <param name="color">ブラシの色。</param>
    /// <returns>色に対応する <see cref="SolidColorBrush"/>。</returns>
    /// <remarks>容量超過時は全クリアして無制限の増加を防ぐ（<see cref="BrushCache"/> へ委譲）。</remarks>
    private static SolidColorBrush GetSharedBrush(Color color)
        => s_brushCache.GetOrCreate(color);

    // ====================================================================
    // 公開メソッド（操作）
    // ====================================================================

    /// <summary>ルーペを非表示にしてリソースを解放する。</summary>
    /// <remarks>
    /// <see cref="Visual.IsVisible"/> を false に設定し、ビットマップを解放する。<br/>
    /// 既に非表示の場合は <see cref="OnIsVisibleChanged"/> が発火しないため、直接解放する（冪等）。<br/>
    /// </remarks>
    public void Clear()
    {
        _renderCoordinator.CancelPending();
        IsVisible = false;
        ReleaseBitmap();
    }

    /// <summary>強制的にルーペ画像を再生成する（同値の再設定では PropertyChanged が発火しない場合など）。</summary>
    /// <remarks>
    /// 未実行の再描画要求を同期実行に置き換え、即座に最新のビットマップを生成する。<br/>
    /// </remarks>
    public void ForceUpdate()
    {
        if (TryBeginRender(out var generation))
        {
            StartRender(generation);
        }
    }

    /// <summary>現在の状態から画像を非同期に生成します。</summary>
    /// <returns>生成完了を表すタスク。</returns>
    public Task RenderAsync()
    {
        return TryBeginRender(out var generation)
            ? GenerateAsync(generation)
            : Task.CompletedTask;
    }

    /// <summary>再描画を開始できるか判定し、開始できる場合は新しい世代番号を採番する。</summary>
    /// <param name="generation">採番した世代番号（開始できない場合は 0）。</param>
    /// <returns>再描画を開始できる場合は true。</returns>
    /// <remarks>
    /// <see cref="ForceUpdate"/> と <see cref="RenderAsync"/> で共用する（未実行要求の破棄・非表示時の解放・世代採番の重複排除）。<br/>
    /// 非表示時はビットマップを解放して false を返す（再描画は行わない）。
    /// </remarks>
    private bool TryBeginRender(out long generation)
    {
        _renderCoordinator.CancelPending();
        if (!IsVisible)
        {
            ReleaseBitmap();
            generation = 0;
            return false;
        }

        generation = _renderCoordinator.NextGeneration();
        return true;
    }

    // ====================================================================
    // 出力（internal）
    // ====================================================================

    /// <summary>表示用の画像アダプタ（PbBitmapAvaloniaImage）。所有権は本コントロールが保持し、外部へ公開しない。</summary>
    /// <remarks>
    /// <see cref="PART_Image"/> の Source へ設定する <see cref="PbBitmapAvaloniaImage"/> を保持する。<br/>
    /// <c>WriteableBitmap</c> を生成せず Skia 直描画するため、破棄は <see cref="ReleaseBitmap"/> で行う。
    /// </remarks>
    private PbBitmapAvaloniaImage? _loupeImage;

    /// <summary>内部描画結果（PbBitmap）。所有権は本コントロールが保持し、外部へ公開しない。</summary>
    /// <remarks><see cref="_loupeImage"/> が内包する PbBitmap を返す（共有参照。破棄はしないこと）。</remarks>
    internal PbBitmap? LoupeBitmap => _loupeImage?.Bitmap;

    /// <summary>座標ラベルの Border（テスト・デザインツール用の内部アクセサ）。</summary>
    internal Border? CoordLabelBorder => PART_CoordLabel;

    /// <summary>座標ラベルの TextBlock（テスト・デザインツール用の内部アクセサ）。</summary>
    internal TextBlock? CoordLabelText => PART_CoordText;

    // ====================================================================
    // ログ
    // ====================================================================

    /// <summary>内部ロガー（描画エラーの記録用）。</summary>
    public ILogger Log
    {
        get => _log;
        set => _log = value ?? NullLogger.Instance;
    }

    private ILogger _log = NullLogger.Instance;

    // ====================================================================
    // イベント
    // ====================================================================

    /// <summary>ルーペの描画完了を通知するイベント（描画済み Bitmap と表示情報を渡す）。</summary>
    /// <remarks>
    /// 拡大画像の描画結果（<see cref="LoupeRenderedEventArgs.Bitmap"/>）と表示状態を通知する。<br/>
    /// イベントは表示へ反映する前に同期的に発火するため、ホスト側はイベントハンドラ内で<br/>
    /// Bitmap を <c>Lock</c> してオーバーレイ（線分の破線等）を直接描画できる（描画後は表示へ反映される）。<br/>
    /// 描画は非同期・バッチ化されるため、プロパティ変更より 1 フレーム遅れて発火する。
    /// </remarks>
    public event EventHandler<LoupeRenderedEventArgs>? Rendered;

    /// <summary><see cref="Rendered"/> イベントを発火する（描画済み PbBitmap と表示情報を構築して通知）。</summary>
    /// <param name="bitmap">描画済みのルーペ画像（PbBitmap）。ホストがピクセルにオーバーレイを描画できる。</param>
    /// <param name="size">表示サイズ（DIP）。</param>
    /// <remarks>
    /// 表示位置（<see cref="_positionTransform"/>）・表示サイズ（<paramref name="size"/>）・拡大率・中心・
    /// 切り出し領域（<see cref="LoupeImageExtractor.ComputeCaptureSize"/>）・スケール（ドメイン → ルーペ画像ピクセル）を
    /// 一度だけ取得してイベント引数を生成する（イベント発火形式の集約）。<br/>
    /// 中心未設定時は <see cref="LoupeRenderedEventArgs.CenterPoint"/> / <see cref="LoupeRenderedEventArgs.CaptureTopLeft"/> が null になる（オーバーレイ描画不可）。
    /// </remarks>
    private void RaiseRendered(PbBitmap bitmap, int size)
    {
        var zoom = ZoomLevel;
        var center = CenterPoint;
        var args = LoupeRenderArgsCalculator.Compute(size, RenderScaling, zoom, center?.ToPoint());

        Rendered?.Invoke(this, new LoupeRenderedEventArgs(
            bitmap,
            new Point(_positionTransform.X, _positionTransform.Y),
            size,
            zoom,
            center?.ToPoint(),
            args.CaptureTopLeft,
            args.Scale));
    }

    // ====================================================================
    // 再描画のバッチ化
    // ====================================================================

    /// <summary>再描画を要求する（複数要求は <see cref="LoupeRenderCoordinator"/> により 1 回に結合される）。</summary>
    /// <remarks>
    /// プロパティ変更（PropertyChanged）から呼ばれる。<br/>
    /// 複数プロパティの連続変更でもポストは1回となり、<br/>
    /// 1フレームに1回の再描画に抑える（バッチ化は LoupeRenderCoordinator が担う）。
    /// </remarks>
    private void RequestRender()
    {
        _renderCoordinator.RequestRender();
    }

    // ====================================================================
    // 表示制御（IsVisible による表示・非表示の指示）
    // ====================================================================

    /// <summary>表示状態（<see cref="Visual.IsVisible"/>）が変更された時の処理。</summary>
    /// <param name="e">変更イベント引数。</param>
    /// <remarks>
    /// 表示・非表示の指示は標準プロパティ <see cref="Visual.IsVisible"/> に一元化する。<br/>
    /// - 表示時: 最新の <see cref="Source"/> / <see cref="CenterPoint"/> からビットマップを同期生成する。<br/>
    /// - 非表示時: 未実行の再描画要求を破棄し、ビットマップを解放する。<br/>
    /// </remarks>
    private void OnIsVisibleChanged(AvaloniaPropertyChangedEventArgs e)
    {
        _renderCoordinator.CancelPending();
        if (e.GetNewValue<bool>())
        {
            RequestRender();
            UpdatePosition();
        }
        else
        {
            ReleaseBitmap();
        }
    }

    /// <summary>ビットマップを解放して画像表示をクリアする（冪等）。</summary>
    private void ReleaseBitmap()
    {
        // 破棄済み PbBitmap を Image.Source が参照し続けないよう、先に Source を解除してから解放する。
        PART_Image?.Source = null;
        _loupeImage?.Dispose();
        _loupeImage = null;
    }

    /// <summary>自動再描画を開始し、失敗を観測済みタスクとして処理する。</summary>
    private void StartRender(long generation)
    {
        _ = GenerateAsync(generation).ContinueWith(
            static task => _ = task.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    // ====================================================================
    // 描画（LoupeRenderer への委譲）
    // ====================================================================

    /// <summary><see cref="Source"/> と <see cref="CenterPoint"/> からルーペ画像を生成して表示する。</summary>
    /// <remarks>
    /// 描画は <see cref="LoupeRenderer.Render"/> に一元化する（抽出・拡大・十字線・市松・外枠を一括生成）。<br/>
    /// <see cref="Source"/> / <see cref="CenterPoint"/> のいずれかが未設定の場合は市松模様のみを表示する。<br/>
    /// 例外は <see cref="LogMessage.LogUpdateError"/> で記録し、前回のビットマップを維持する。<br/>
    /// </remarks>
    private async Task GenerateAsync(long generation)
    {
        try
        {
            var source = Source;
            var center = CenterPoint;
            var size = _displaySize;
            var zoom = ZoomLevel;
            var crossColor = CrossColor;
            var checkerSize = CheckerSize;
            var checkerColorA = CheckerColorA;
            var checkerColorB = CheckerColorB;
            var frameColor = FrameColor;
            var frameWidth = FrameWidth;
            var scaling = RenderScaling;
            var pixelSize = DpiScaler.ScaleToPixel(size, scaling);

            var bitmap = await LoupeRenderPipeline.RenderAsync(
                source, center, pixelSize, zoom, crossColor,
                checkerSize, checkerColorA, checkerColorB, frameColor, frameWidth, scaling);

            // 描画反映（SetLoupeBitmap）は描画フェーズ（Render 優先度）で実行する。
            // マウス移動中の連続再描画では、既定の Normal 優先度の継続が入力イベント（Input 優先度）に
            // 後回しにされ、描画反映が移動停止まで実行されない（ルーペが移動中に更新されない）問題が生じる。
            // Render 優先度は描画フェーズで毎フレーム実行されるため、連続入力中でも描画が追従する。
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var current = _renderCoordinator.Generation;
                var visible = IsVisible;
                if (!_renderCoordinator.ShouldApply(generation))
                {
                    bitmap.Dispose();
                    Log.LogGenerateAsyncDone(false, true, visible, generation, current);
                    return;
                }

                SetLoupeBitmap(bitmap, size);
                Log.LogGenerateAsyncDone(true, false, visible, generation, current);
            }, DispatcherPriority.Render);
        }
        catch (Exception ex)
        {
            Log.LogUpdateError(ex);
            throw;
        }
    }

    /// <summary>生成したルーペビットマップをコントロールへ反映する。</summary>
    /// <param name="bmp">生成したルーペビットマップ（PbBitmap）。</param>
    /// <param name="size">表示サイズ（DIP）。</param>
    /// <remarks>
    /// 前回のビットマップを破棄し、<see cref="LoupeBitmap"/> と表示内容（サイズ・画像）を更新する。<br/>
    /// 表示状態（IsVisible）は変更しない（表示状態と再描画の分離）。<br/>
    /// 表示への反映前に <see cref="Rendered"/> イベントを発火し、ホスト側が PbBitmap のピクセルに<br/>
    /// オーバーレイ（線分の破線等）を描画できるようにする（イベント後に表示へ反映）。<br/>
    /// <b>チラつき防止</b>: <see cref="PART_Image.Source"/> が前回のアダプタを参照し続ける間に、<br/>
    /// その内包 <see cref="PbBitmap"/> を破棄すると、描画時に破棄済みピクセルへアクセスして例外・チラつきが生じる。<br/>
    /// そのため、新しいアダプタを先に <see cref="PART_Image.Source"/> へ設定し、旧アダプタは後から破棄する<br/>
    /// （描画が参照するアダプタを連続的に保ち、破棄済み Bitmap の参照を防ぐ）。
    /// </remarks>
    private void SetLoupeBitmap(PbBitmap bmp, int size)
    {
        Width = size;
        Height = size;

        // ホストが PbBitmap のピクセルへオーバーレイ（破線等）を描画するフック。イベント内でピクセルが更新される
        RaiseRendered(bmp, size);

        // イベント後の PbBitmap（ホストのオーバーレイ描画を含む）を表示境界で Avalonia 画像として反映する。
        // PbBitmapAvaloniaImage（Skia 直描画・ゼロコピー）を Source に設定する。
        // 旧アダプタを破棄する前に、新しいアダプタを先に Source へ設定する（破棄済み Bitmap の参照防止・チラつき防止）。
        var previousImage = _loupeImage;
        _loupeImage = new PbBitmapAvaloniaImage(bmp);
        PART_Image?.Source = _loupeImage;
        // Source 差し替えに加えて明示的に再描画を要求する（マウス移動中の連続描画で Image の再描画が
        // 抑制される場合があるため、確実に再描画してポインタ移動に追従させる）。
        PART_Image?.InvalidateVisual();
        // 旧アダプタは新しい Source 設定後に破棄する（PART_Image.Source が旧アダプタを参照しなくなった後）。
        previousImage?.Dispose();
    }
}
