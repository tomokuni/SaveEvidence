using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Estable.AvaloniaUI.Loupe.Imaging;
using Estable.AvaloniaUI.Loupe.Logging;
using Estable.AvaloniaUI.Loupe.Rendering;
using Estable.AvaloniaUI.Platform;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Estable.AvaloniaUI.Loupe;

/// <summary>拡大鏡（ルーペ）コントロール。</summary>
/// <remarks>
/// 領域選択時（OverlayWindow）とプレビュー画像切出し時（MainWindow）の両方で<br/>
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
/// 数値プロパティ（<see cref="RequestedSize"/> / <see cref="ZoomLevel"/> /<br/>
/// <see cref="CheckerSize"/> / <see cref="FrameWidth"/>）は範囲外の値を安全域へ補正（coerce）する。<br/>
/// <br/>
/// **公開 API 一覧**:<br/>
/// - 入力: <see cref="Source"/> / <see cref="CenterPoint"/><br/>
/// - サイズ: <see cref="RequestedSize"/> / <see cref="RenderScaling"/> / <see cref="ZoomLevel"/><br/>
/// - 外観: <see cref="CrossColor"/> / <see cref="CheckerSize"/> / <see cref="CheckerColorA"/> / <see cref="CheckerColorB"/> / <see cref="FrameColor"/> / <see cref="FrameWidth"/><br/>
/// - 選択矩形: <see cref="SelectionRect"/> / <see cref="SelectionRectVisible"/> / <see cref="SelectionRectColorA"/> / <see cref="SelectionRectColorB"/>（選択枠をルーペ上に 2 色破線で表示）<br/>
/// - ログ: <see cref="Log"/><br/>
/// - 操作: <see cref="Clear()"/> / <see cref="ForceUpdate()"/> / <see cref="RenderAsync()"/><br/>
/// </remarks>
public sealed partial class LoupeControl : UserControl
{
    /// <summary><see cref="LoupeControl"/> クラスの新しいインスタンスを初期化します。</summary>
    /// <remarks>
    /// <see cref="InitializeComponent()"/> を呼び出して AXAML で定義された子要素を生成します。<br/>
    /// </remarks>
    public LoupeControl()
    {
        InitializeComponent();
        UpdateCoordLabel();
    }

    // ====================================================================
    // 座標変換ポート（DragRectControl と同方式）
    // ====================================================================

    /// <summary>論理座標（ドメイン）↔ 親パネル DIP の座標変換ポート。</summary>
    private ICoordinatePort _coordinatePort = CoordinatePort.Identity;

    /// <summary>ルーペ中心（<see cref="CenterPoint"/>）からの右下オフセット（DIP）。</summary>
    private double _positionOffset;

    /// <summary>ルーペ中心（<see cref="CenterPoint"/>）からの右下オフセット（DIP）を取得または設定する。</summary>
    /// <value>カーソル位置（ルーペ中心）からの右下方向へのオフセット量（DIP）。</value>
    /// <remarks>ホスト（上位 AP）が設定する。設定後の位置反映は <see cref="UpdatePosition"/> で行う。</remarks>
    public double PositionOffset
    {
        get => _positionOffset;
        set
        {
            _positionOffset = value;
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
    public void SetCoordinateTransform(ICoordinatePort? port)
    {
        _coordinatePort = port ?? CoordinatePort.Identity;
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

        // 表示サイズ: 基本サイズ（RequestedSize）を親パネルサイズにクランプ（クランプはコントロールが内包）
        double size = RequestedSize;
        if (parentSize.Width > 0 && parentSize.Height > 0)
            size = Math.Min(size, Math.Min(parentSize.Width, parentSize.Height));
        _displaySize = (int)size;
        Width = size;
        Height = size;

        // 表示位置: 中心 → 親パネルDIP → カーソル右下 + オフセット → クランプ
        var dip = port.Inverse(center);
        double mx = dip.X + _positionOffset;
        double my = dip.Y + _positionOffset;
        if (parentSize.Width > 0 && parentSize.Height > 0)
        {
            mx = Math.Clamp(mx, 0, Math.Max(0, parentSize.Width - size));
            my = Math.Clamp(my, 0, Math.Max(0, parentSize.Height - size));
        }
        RenderTransform = new TranslateTransform(mx, my);
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
            LogMessage.LogSettingChanged(o.Log, nameof(Source), e.OldValue?.ToString() ?? "null", e.NewValue?.ToString() ?? "null");
            o.RequestRender();
        });
        // CenterPoint 変更時は再描画に加えて表示位置（RenderTransform）も更新する（座標変換ポート + 親パネルサイズから算出）
        CenterPointProperty.Changed.AddClassHandler<LoupeControl>(static (o, e) =>
        {
            LogMessage.LogSettingChanged(o.Log, nameof(CenterPoint), e.OldValue?.ToString() ?? "null", e.NewValue?.ToString() ?? "null");
            o.RequestRender();
            o.UpdatePosition();
            o.UpdateCoordLabel();
        });
        RequestedSizeProperty.Changed.AddClassHandler<LoupeControl>(static (o, e) =>
        {
            // OldValue / NewValue は StyledProperty&lt;int&gt; により常に非 null（boxed int）
            LogMessage.LogSettingChanged(o.Log, nameof(RequestedSize), e.OldValue!.ToString()!, e.NewValue!.ToString()!);
            o.RequestRender();
            o.UpdatePosition();
        });
        RenderScalingProperty.Changed.AddClassHandler<LoupeControl>(static (o, e) =>
        {
            // OldValue / NewValue は StyledProperty&lt;double&gt; により常に非 null（boxed double）
            LogMessage.LogSettingChanged(o.Log, nameof(RenderScaling), e.OldValue!.ToString()!, e.NewValue!.ToString()!);
            o.RequestRender();
        });
        ZoomLevelProperty.Changed.AddClassHandler<LoupeControl>(static (o, e) =>
        {
            // OldValue / NewValue は StyledProperty&lt;int&gt; により常に非 null（boxed int）
            LogMessage.LogSettingChanged(o.Log, nameof(ZoomLevel), e.OldValue!.ToString()!, e.NewValue!.ToString()!);
            o.RequestRender();
        });
        CrossColorProperty.Changed.AddClassHandler<LoupeControl>(static (o, _) => o.RequestRender());
        CheckerSizeProperty.Changed.AddClassHandler<LoupeControl>(static (o, _) => o.RequestRender());
        CheckerColorAProperty.Changed.AddClassHandler<LoupeControl>(static (o, _) => o.RequestRender());
        CheckerColorBProperty.Changed.AddClassHandler<LoupeControl>(static (o, _) => o.RequestRender());
        FrameColorProperty.Changed.AddClassHandler<LoupeControl>(static (o, _) => o.RequestRender());
        FrameWidthProperty.Changed.AddClassHandler<LoupeControl>(static (o, _) => o.RequestRender());
        SelectionRectVisibleProperty.Changed.AddClassHandler<LoupeControl>(static (o, _) => o.RequestRender());
        SelectionRectColorAProperty.Changed.AddClassHandler<LoupeControl>(static (o, _) => o.RequestRender());
        SelectionRectColorBProperty.Changed.AddClassHandler<LoupeControl>(static (o, _) => o.RequestRender());
        SelectionRectProperty.Changed.AddClassHandler<LoupeControl>(static (o, _) => o.RequestRender());
        ShowCoordLabelProperty.Changed.AddClassHandler<LoupeControl>(static (o, _) => o.UpdateCoordLabel());
        CoordLabelColorProperty.Changed.AddClassHandler<LoupeControl>(static (o, _) => o.UpdateCoordLabel());
        CoordLabelBackgroundColorProperty.Changed.AddClassHandler<LoupeControl>(static (o, _) => o.UpdateCoordLabel());
        CoordLabelFontSizeProperty.Changed.AddClassHandler<LoupeControl>(static (o, _) => o.UpdateCoordLabel());
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
        AvaloniaProperty.Register<LoupeControl, int>(nameof(RequestedSize), defaultValue: 160, coerce: CoerceRequestedSize);

    /// <summary>表示DIPを物理ピクセルへ変換するスケーリング係数。</summary>
    public static readonly StyledProperty<double> RenderScalingProperty =
        AvaloniaProperty.Register<LoupeControl, double>(nameof(RenderScaling), defaultValue: 1.0, coerce: CoerceRenderScaling);

    /// <summary><see cref="ZoomLevel"/> 依存関係プロパティ（1〜64 に補正）。</summary>
    public static readonly StyledProperty<int> ZoomLevelProperty =
        AvaloniaProperty.Register<LoupeControl, int>(nameof(ZoomLevel), defaultValue: 4, coerce: CoerceZoomLevel);

    /// <summary>十字線の色の既定ブラシ（赤 40% = アルファ 102）。</summary>
    private static readonly IBrush DefaultCrossColor = new SolidColorBrush(Color.FromArgb(102, 255, 0, 0));

    /// <summary><see cref="CrossColor"/> 依存関係プロパティ（不透明度は色のアルファに統合）。</summary>
    public static readonly StyledProperty<IBrush?> CrossColorProperty =
        AvaloniaProperty.Register<LoupeControl, IBrush?>(nameof(CrossColor), defaultValue: DefaultCrossColor);

    /// <summary><see cref="CheckerSize"/> 依存関係プロパティ（1〜128 に補正）。</summary>
    public static readonly StyledProperty<int> CheckerSizeProperty =
        AvaloniaProperty.Register<LoupeControl, int>(nameof(CheckerSize), defaultValue: 6, coerce: CoerceCheckerSize);

    /// <summary>市松模様の色Aの既定ブラシ（Gray 30% = #4C808080。設定画面の市松模様 A と一致）。全インスタンスで共有する不変ブラシ。</summary>
    /// <remarks>
    /// static フィールド初期化子は宣言順に実行されるため、<see cref="CheckerColorAProperty"/> より先に宣言する<br/>
    /// （逆順だとプロパティ登録時に null がデフォルト値として固定される）。<br/>
    /// </remarks>
    private static readonly IBrush DefaultCheckerColorA = new SolidColorBrush(Color.FromArgb(0x4C, 0x80, 0x80, 0x80));

    /// <summary><see cref="CheckerColorA"/> 依存関係プロパティ。</summary>
    /// <remarks>既定は Gray 30%（<c>#4C808080</c>。設定画面の市松模様 A と一致）。</remarks>
    public static readonly StyledProperty<IBrush?> CheckerColorAProperty =
        AvaloniaProperty.Register<LoupeControl, IBrush?>(nameof(CheckerColorA), defaultValue: DefaultCheckerColorA);

    /// <summary>市松模様の色Bの既定ブラシ（Silver 30% = #4CA9A9A9。設定画面の市松模様 B と一致）。全インスタンスで共有する不変ブラシ。</summary>
    /// <remarks>
    /// static フィールド初期化子は宣言順に実行されるため、<see cref="CheckerColorBProperty"/> より先に宣言する<br/>
    /// （逆順だとプロパティ登録時に null がデフォルト値として固定される）。<br/>
    /// </remarks>
    private static readonly IBrush DefaultCheckerColorB = new SolidColorBrush(Color.FromArgb(0x4C, 0xA9, 0xA9, 0xA9));

    /// <summary><see cref="CheckerColorB"/> 依存関係プロパティ。</summary>
    /// <remarks>既定は Silver 30%（<c>#4CA9A9A9</c>。設定画面の市松模様 B と一致）。</remarks>
    public static readonly StyledProperty<IBrush?> CheckerColorBProperty =
        AvaloniaProperty.Register<LoupeControl, IBrush?>(nameof(CheckerColorB), defaultValue: DefaultCheckerColorB);

    /// <summary><see cref="FrameColor"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<IBrush?> FrameColorProperty =
        AvaloniaProperty.Register<LoupeControl, IBrush?>(nameof(FrameColor), defaultValue: Brushes.LightGray);

    /// <summary><see cref="FrameWidth"/> 依存関係プロパティ（1〜64 に補正）。</summary>
    public static readonly StyledProperty<int> FrameWidthProperty =
        AvaloniaProperty.Register<LoupeControl, int>(nameof(FrameWidth), defaultValue: 2, coerce: CoerceFrameWidth);

    /// <summary>選択矩形破線の色A（明色）の既定ブラシ（白 100% = <c>#FFFFFFFF</c>）。</summary>
    /// <summary>選択矩形破線の色B（暗色）の既定ブラシ（黒 100% = <c>#FF000000</c>）。</summary>
    /// <remarks>
    /// 選択矩形の破線は色A（白）と色B（黒）をダッシュごとに交互に塗り、市松風の視認性を確保する。<br/>
    /// 不透明度（既定 100%）は色のアルファに統合する。<br/>
    /// static フィールド初期化子は宣言順に実行されるため、<see cref="SelectionRectColorAProperty"/> より先に宣言する<br/>
    /// （逆順だとプロパティ登録時に null がデフォルト値として固定される）。<br/>
    /// </remarks>
    private static readonly IBrush DefaultSelectionRectColorA = Brushes.White;
    private static readonly IBrush DefaultSelectionRectColorB = Brushes.Black;

    /// <summary><see cref="SelectionRect"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<Rect?> SelectionRectProperty =
        AvaloniaProperty.Register<LoupeControl, Rect?>(nameof(SelectionRect));

    /// <summary><see cref="SelectionRectVisible"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<bool> SelectionRectVisibleProperty =
        AvaloniaProperty.Register<LoupeControl, bool>(nameof(SelectionRectVisible), defaultValue: false);

    /// <summary><see cref="SelectionRectColorA"/> 依存関係プロパティ（不透明度は色のアルファに統合）。</summary>
    public static readonly StyledProperty<IBrush?> SelectionRectColorAProperty =
        AvaloniaProperty.Register<LoupeControl, IBrush?>(nameof(SelectionRectColorA), defaultValue: DefaultSelectionRectColorA);

    /// <summary><see cref="SelectionRectColorB"/> 依存関係プロパティ（不透明度は色のアルファに統合）。</summary>
    public static readonly StyledProperty<IBrush?> SelectionRectColorBProperty =
        AvaloniaProperty.Register<LoupeControl, IBrush?>(nameof(SelectionRectColorB), defaultValue: DefaultSelectionRectColorB);

    /// <summary><see cref="ShowCoordLabel"/> 依存関係プロパティ。</summary>
    /// <remarks>既定は true（設定画面のルーペ座標ラベル表示のデフォルトと一致）。</remarks>
    public static readonly StyledProperty<bool> ShowCoordLabelProperty =
        AvaloniaProperty.Register<LoupeControl, bool>(nameof(ShowCoordLabel), defaultValue: true);

    /// <summary><see cref="CoordLabelColor"/> 依存関係プロパティ（座標ラベルの文字色）。</summary>
    public static readonly StyledProperty<IBrush?> CoordLabelColorProperty =
        AvaloniaProperty.Register<LoupeControl, IBrush?>(nameof(CoordLabelColor), defaultValue: Brushes.White);

    /// <summary>中心座標ラベルの背景色の既定ブラシ（黒 60% = アルファ 153）。</summary>
    private static readonly IBrush DefaultCoordLabelBackgroundColor = new SolidColorBrush(Color.FromArgb(153, 0, 0, 0));

    /// <summary><see cref="CoordLabelBackgroundColor"/> 依存関係プロパティ（座標ラベルの背景色・不透明度はアルファに統合）。</summary>
    public static readonly StyledProperty<IBrush?> CoordLabelBackgroundColorProperty =
        AvaloniaProperty.Register<LoupeControl, IBrush?>(nameof(CoordLabelBackgroundColor), defaultValue: DefaultCoordLabelBackgroundColor);

    /// <summary><see cref="CoordLabelFontSize"/> 依存関係プロパティ（座標ラベルのフォントサイズ 6〜48 に補正）。</summary>
    public static readonly StyledProperty<double> CoordLabelFontSizeProperty =
        AvaloniaProperty.Register<LoupeControl, double>(nameof(CoordLabelFontSize), defaultValue: 12.0, coerce: CoerceCoordLabelFontSize);

    /// <summary><see cref="RequestedSize"/> を安全域へ補正する（1〜4096）。</summary>
    private static int CoerceRequestedSize(AvaloniaObject o, int v) => Math.Clamp(v, 1, 4096);

    /// <summary><see cref="ZoomLevel"/> を安全域へ補正する。</summary>
    private static int CoerceZoomLevel(AvaloniaObject o, int v) => Math.Clamp(v, 1, 64);

    /// <summary>描画スケーリング係数を安全域へ補正する。</summary>
    private static double CoerceRenderScaling(AvaloniaObject o, double v) => double.IsFinite(v) ? Math.Clamp(v, 0.1, 8.0) : 1.0;

    /// <summary><see cref="CheckerSize"/> を安全域へ補正する。</summary>
    private static int CoerceCheckerSize(AvaloniaObject o, int v) => Math.Clamp(v, 1, 128);

    /// <summary><see cref="FrameWidth"/> を安全域へ補正する。</summary>
    private static int CoerceFrameWidth(AvaloniaObject o, int v) => Math.Clamp(v, 1, 64);

    /// <summary><see cref="CoordLabelFontSize"/> を安全域へ補正する。</summary>
    private static double CoerceCoordLabelFontSize(AvaloniaObject o, double v) => double.IsFinite(v) ? Math.Clamp(v, 6, 48) : 12.0;

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
    private int _displaySize = 160;

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

    /// <summary>ルーペ画像上に破線でオーバーレイ表示する選択矩形（ドメイン座標系 = 元画像ピクセル座標）を取得または設定する。</summary>
    /// <remarks>
    /// <see cref="CenterPoint"/> と同じ座標系（元画像ピクセル座標）で指定する。<br/>
    /// ルーペの切り出し領域内に選択矩形が含まれる場合、その枠線を破線で描画する（選択位置の視覚化）。<br/>
    /// 領域選択 / Crop のドラッグ中・矩形確定後に、現在の選択矩形を確認するために使用する。
    /// </remarks>
    public Rect? SelectionRect
    {
        get => GetValue(SelectionRectProperty);
        set => SetValue(SelectionRectProperty, value);
    }

    /// <summary>選択矩形の破線オーバーレイを表示するかどうかを取得または設定する。</summary>
    /// <remarks>既定は false（非表示）。true にすると <see cref="SelectionRect"/> の枠線を破線で描画する。</remarks>
    public bool SelectionRectVisible
    {
        get => GetValue(SelectionRectVisibleProperty);
        set => SetValue(SelectionRectVisibleProperty, value);
    }

    /// <summary>選択矩形の破線オーバーレイの色A（明色）を取得または設定する。</summary>
    /// <remarks>既定は白 80%（<c>#CCFFFFFF</c>）。色B（<see cref="SelectionRectColorB"/>）と交互に破線のダッシュを塗る。<br/>
    /// 不透明度は色のアルファに統合する。</remarks>
    public IBrush? SelectionRectColorA
    {
        get => GetValue(SelectionRectColorAProperty);
        set => SetValue(SelectionRectColorAProperty, value);
    }

    /// <summary>選択矩形の破線オーバーレイの色B（暗色）を取得または設定する。</summary>
    /// <remarks>既定は黒 80%（<c>#CC000000</c>）。色A（<see cref="SelectionRectColorA"/>）と交互に破線のダッシュを塗る。<br/>
    /// 不透明度は色のアルファに統合する。</remarks>
    public IBrush? SelectionRectColorB
    {
        get => GetValue(SelectionRectColorBProperty);
        set => SetValue(SelectionRectColorBProperty, value);
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

        PART_CoordLabel.IsVisible = ShowCoordLabel && CenterPoint is not null;
        if (!PART_CoordLabel.IsVisible) return;

        var c = CenterPoint!.Value;
        PART_CoordText.Text = $"({c.X:F0}, {c.Y:F0})";
        PART_CoordText.Foreground = CoordLabelColor;
        PART_CoordText.FontSize = CoordLabelFontSize;
        // 背景色（不透明度は色のアルファに統合）を Border へ反映する
        var bg = CoordLabelBackgroundColor ?? Brushes.Black;
        PART_CoordLabel.Background = bg is SolidColorBrush scb
            ? new SolidColorBrush(scb.Color)
            : bg;
    }

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
        _renderQueued = false;
        IsVisible = false;
        ReleaseBitmap();
    }

    /// <summary>強制的にルーペ画像を再生成する（同値の再設定では PropertyChanged が発火しない場合など）。</summary>
    /// <remarks>
    /// 未実行の再描画要求を同期実行に置き換え、即座に最新のビットマップを生成する。<br/>
    /// </remarks>
    public void ForceUpdate()
    {
        _renderQueued = false;
        if (!IsVisible)
        {
            ReleaseBitmap();
            return;
        }

        var generation = Interlocked.Increment(ref _renderGeneration);
        StartRender(generation);
    }

    /// <summary>現在の状態から画像を非同期に生成します。</summary>
    /// <returns>生成完了を表すタスク。</returns>
    public Task RenderAsync()
    {
        _renderQueued = false;
        if (!IsVisible)
        {
            ReleaseBitmap();
            return Task.CompletedTask;
        }

        var generation = Interlocked.Increment(ref _renderGeneration);
        return GenerateAsync(generation);
    }

    // ====================================================================
    // 出力（internal）
    // ====================================================================

    /// <summary>内部描画結果。所有権は本コントロールが保持し、外部へ公開しない。</summary>
    internal Bitmap? LoupeBitmap { get; private set; }

    /// <summary>座標ラベルの Border（テスト・デザインツール用の内部アクセサ）。</summary>
    internal Border? CoordLabelBorder => PART_CoordLabel;

    /// <summary>座標ラベルの TextBlock（テスト・デザインツール用の内部アクセサ）。</summary>
    internal TextBlock? CoordLabelText => PART_CoordText;

    // ====================================================================
    // ログ
    // ====================================================================

    /// <summary>内部ロガー（描画エラーの記録用）。</summary>
    public ILogger Log { get; set; } = NullLogger.Instance;

    // ====================================================================
    // 再描画のバッチ化
    // ====================================================================

    /// <summary>再描画要求の結合フラグ（true の間は追加要求を無視して1回のポストにまとめる）。</summary>
    private bool _renderQueued;
    private long _renderGeneration;

    /// <summary>再描画を要求する（複数要求は Dispatcher のポストにより1回に結合される）。</summary>
    /// <remarks>
    /// プロパティ変更（PropertyChanged）から呼ばれる。<br/>
    /// 複数プロパティの連続変更でもポストは1回となり、<br/>
    /// 1フレームに1回の再描画に抑える（バッチ化）。<br/>
    /// ポストされたジョブは次のフレーム描画前に実行されるため、描画遅延は生じない。<br/>
    /// </remarks>
    private void RequestRender()
    {
        LogMessage.LogRequestRender(Log, IsVisible, _renderQueued);
        if (!IsVisible)
        {
            Interlocked.Increment(ref _renderGeneration);
            ReleaseBitmap();
            return;
        }

        if (_renderQueued)
        {
            return;
        }

        _renderQueued = true;
        Dispatcher.UIThread.Post(RenderCore);
    }

    /// <summary>ポストされた再描画を実行する。</summary>
    private void RenderCore()
    {
        if (!_renderQueued)
        {
            return;
        }

        _renderQueued = false;
        var generation = Interlocked.Increment(ref _renderGeneration);
        LogMessage.LogRenderCore(Log, generation);
        StartRender(generation);
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
        _renderQueued = false;
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
        LoupeBitmap?.Dispose();
        LoupeBitmap = null;
        PART_Image?.Source = null;
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
            var selectionRect = SelectionRectVisible ? SelectionRect : null;
            var selectionRectColorA = SelectionRectVisible ? SelectionRectColorA : null;
            var selectionRectColorB = SelectionRectVisible ? SelectionRectColorB : null;
            var scaling = RenderScaling;
            var pixelSize = Math.Max(1, (int)Math.Ceiling(size * scaling));

            var bitmap = await RenderBitmapAsync(
                source, center, pixelSize, zoom, crossColor,
                checkerSize, checkerColorA, checkerColorB, frameColor, frameWidth, scaling,
                selectionRect, selectionRectColorA, selectionRectColorB);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var current = Volatile.Read(ref _renderGeneration);
                if (!IsVisible || generation != current)
                {
                    bitmap.Dispose();
                    LogMessage.LogGenerateAsyncDone(Log, false, true, IsVisible, generation, current);
                    return;
                }

                SetLoupeBitmap(bitmap, size);
                LogMessage.LogGenerateAsyncDone(Log, true, false, IsVisible, generation, current);
            });
        }
        catch (Exception ex)
        {
            LogMessage.LogUpdateError(Log, ex);
            throw;
        }
    }

    /// <summary>画像抽出を非同期に実行し、Avaloniaの描画オブジェクトをUIスレッドで合成する。</summary>
    private static async Task<Bitmap> RenderBitmapAsync(
        IImage? source, Point? center, int pixelSize, int zoomLevel,
        IBrush? crossColor, int checkerSize,
        IBrush? checkerColorA, IBrush? checkerColorB, IBrush? frameColor,
        int frameWidth, double scaling,
        Rect? selectionRect, IBrush? selectionRectColorA, IBrush? selectionRectColorB)
    {
        if (source is not null and not Bitmap)
        {
            throw new NotSupportedException(
                $"LoupeControl は {source.GetType().FullName} をサポートしていません。Bitmap を指定してください。");
        }

        var checkerPixelSize = Math.Max(1, (int)Math.Ceiling(checkerSize * scaling));
        var framePixelWidth = Math.Max(1, (int)Math.Ceiling(frameWidth * scaling));

        if (source is Bitmap bitmap && center is { } centerPoint)
        {
            var captureSize = (int)Math.Ceiling((double)pixelSize / zoomLevel);

            // 抽出前に UI スレッドで Bitmap の生存を検証する。
            // Source（元画像）は UI スレッドで破棄される可能性がある（Clear / モード切替 / 別 Bitmap への
            // 置き換え）。検証なしで Task.Run に渡すと、抽出中に ObjectDisposedException が発生する。
            try
            {
                _ = bitmap.PixelSize;
            }
            catch (ObjectDisposedException)
            {
                // 破棄済み Source: 市松模様のみで描画する（前回のビットマップは維持）
                return await Dispatcher.UIThread.InvokeAsync(() => LoupeRenderer.DrawLoupeCheckerboardOnly(
                    pixelSize, crossColor, checkerPixelSize,
                    checkerColorA, checkerColorB, frameColor, framePixelWidth));
            }

            var region = await Task.Run(() =>
            {
                // Task.Run 実行中に破棄される場合に備え、抽出内でも ObjectDisposedException を捕捉する。
                // この場合も市松模様で描画する（例外を UI へ伝播させない）。
                try
                {
                    return LoupeImageExtractor.ExtractRegionAt(
                        bitmap, (int)centerPoint.X, (int)centerPoint.Y, captureSize);
                }
                catch (ObjectDisposedException)
                {
                    return null;
                }
            });

            if (region is null)
            {
                // 抽出中に Source が破棄された: 市松模様のみで描画する
                return await Dispatcher.UIThread.InvokeAsync(() => LoupeRenderer.DrawLoupeCheckerboardOnly(
                    pixelSize, crossColor, checkerPixelSize,
                    checkerColorA, checkerColorB, frameColor, framePixelWidth));
            }

            // WriteableBitmap生成・Lock・RenderTargetBitmap生成・DrawingContext操作はUIスレッドに限定する。
            return await Dispatcher.UIThread.InvokeAsync(() =>
            {
                using var cropped = LoupeImageExtractor.CreateBitmap(region);
                return LoupeRenderer.DrawLoupe(
                    cropped, pixelSize, zoomLevel, crossColor,
                    checkerPixelSize, checkerColorA, checkerColorB, frameColor, framePixelWidth,
                    selectionRect: selectionRect, centerPoint: centerPoint,
                    selectionRectColorA: selectionRectColorA, selectionRectColorB: selectionRectColorB);
            });
        }

        return await Dispatcher.UIThread.InvokeAsync(() => LoupeRenderer.DrawLoupeCheckerboardOnly(
            pixelSize, crossColor, checkerPixelSize,
            checkerColorA, checkerColorB, frameColor, framePixelWidth));
    }

    /// <summary>生成したルーペビットマップをコントロールへ反映する。</summary>
    /// <param name="bmp">生成したルーペビットマップ。</param>
    /// <remarks>
    /// 前回のビットマップを破棄し、<see cref="LoupeBitmap"/> と表示内容（サイズ・画像）を更新する。<br/>
    /// 表示状態（IsVisible）は変更しない（表示状態と再描画の分離）。<br/>
    /// </remarks>
    private void SetLoupeBitmap(Bitmap bmp, int size)
    {
        LoupeBitmap?.Dispose();
        LoupeBitmap = bmp;
        Width = size;
        Height = size;
        PART_Image?.Source = bmp;
    }
}
