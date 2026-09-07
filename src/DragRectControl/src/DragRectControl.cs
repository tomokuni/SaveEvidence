using Estable.AvaloniaUI.DragRect.Logging;
using Estable.AvaloniaUI.DragRect.Selection;
using Estable.AvaloniaUI.Platform.Geometry;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Estable.AvaloniaUI.Platform;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Estable.AvaloniaUI.DragRect;

/// <summary>リサイズ可能なドラッグ矩形の UI コントロール（シンプル実装版）。</summary>
/// <remarks>
/// 公開 API は <see cref="DragRectControl"/> と同一（互換）である。<br/>
/// マウスイベント（押下・移動・解放）とキーボード（矢印キー nudge・Escape/Enter）の処理、<br/>
/// <see cref="Drawing.DragRectDrawer"/> による描画（枠線・ハンドル・始点マーカー）を提供する。<br/>
/// <br/>
/// **自己完結**: 矢印キーの検出・nudge 後の再描画（InvalidateVisual）・カーソル表示/非表示・<br/>
/// ダブルクリックの確定/キャンセル判定（矩形内 = <see cref="ConfirmRequested"/> / 矩形外 = <see cref="CancelRequested"/>）を<br/>
/// 本コントロール自身が担う（ホスト依存なし）。ホストは <see cref="SelectionStarted"/>（始点決定）/ <br/>
/// <see cref="SelectionCompleted"/>（終点決定）/ <see cref="SelectionCancelled"/>（領域キャンセル）/ <br/>
/// <see cref="ConfirmRequested"/> / <see cref="CancelRequested"/> イベントを購読して利用する。<br/>
/// <br/>
/// 外観・動作の設定は依存関係プロパティ（<see cref="BorderColor"/> / <see cref="BorderThickness"/> /<br/>
/// <see cref="HandleColor"/> / <see cref="HandleSize"/> / <see cref="HandleHitRadius"/> / <see cref="MinSelectionSize"/> /<br/>
/// <see cref="MarkerColor"/> / <see cref="MarkerLength"/>）で公開し、<br/>
/// 外部バインディングで制御できる（色の不透明度は色のアルファに統合）。<br/>
/// ログは <see cref="Log"/> プロパティで注入する（デフォルトは <see cref="NullLogger"/>）。<br/>
/// <br/>
/// 選択矩形の変更は <see cref="SelectionChanged"/> イベントで外部（上位 Mediator）に通知する。<br/>
/// <br/>
/// **構成（シンプル実装）**: 状態（選択矩形・ドラッグ・nudge）は <see cref="DragRectState"/> に集約し、<br/>
/// 本クラスは公開 API・イベントオーバーライド・描画・ビジュアルツリー購読を担う（コンポジション）。<br/>
/// 純粋計算（ヒットテスト・矩形変形・クリップ）は <see cref="DragRectState"/> に統合し、<br/>
/// 描画は <see cref="Drawing.DragRectDrawer"/> に委譲する。<br/>
/// </remarks>
public sealed partial class DragRectControl : Control
{
    // ====================================================================
    // 内部状態（コンポジション）
    // ====================================================================

    /// <summary>選択・nudge 状態（状態保持 + 純粋ロジック）。</summary>
    private readonly DragRectState _state;

    /// <summary>フォーカス非依存の矢印キー nudge 用に購読した TopLevel。解除時に使用する。</summary>
    private TopLevel? _attachedTopLevel;

    /// <summary>ズーム・スクロール検知用に購読した祖先 ScrollViewer。解除時に使用する。</summary>
    private ScrollViewer? _attachedScrollViewer;

    /// <summary>nudge のカーソル位置追跡（SetCursorPos のエコー PointerMoved 抑制用）。</summary>
    private bool _nudgeHandledInKeyDown;

    /// <summary>座標変換ポート（Forward = DIP→ドメイン / Inverse = ドメイン→DIP / Bounds = ドメイン境界）。</summary>
    private ICoordTransform _coordinatePort = CoordTransform.Identity;

    // 標準カーソルは Lazy で遅延生成する（呼び出し時に ICursorFactory を解決する）。
    // static readonly フィールドとして型初期化時に new Cursor(...) すると、Avalonia ヘッドレス初期化前に
    // 本コントロールが参照された場合に Unable to locate 'ICursorFactory' で失敗するため（並列テストの競合）。
    // Lazy を使うことで、実際にカーソルを設定する時点（UpdateCursor）まで生成を遅延し、AvaloniaLocator 解決後に行う。
    /// <summary>左上/右下ハンドル用カーソル（遅延生成・静的共有）。</summary>
    private static readonly Lazy<Cursor> s_topLeftCornerCursor = new(() => new Cursor(StandardCursorType.TopLeftCorner));

    /// <summary>右上/左下ハンドル用カーソル（遅延生成・静的共有）。</summary>
    private static readonly Lazy<Cursor> s_topRightCornerCursor = new(() => new Cursor(StandardCursorType.TopRightCorner));

    /// <summary>上下リサイズ用カーソル（遅延生成・静的共有）。</summary>
    private static readonly Lazy<Cursor> s_sizeNorthSouthCursor = new(() => new Cursor(StandardCursorType.SizeNorthSouth));

    /// <summary>左右リサイズ用カーソル（遅延生成・静的共有）。</summary>
    private static readonly Lazy<Cursor> s_sizeWestEastCursor = new(() => new Cursor(StandardCursorType.SizeWestEast));

    /// <summary>移動用カーソル（遅延生成・静的共有）。</summary>
    private static readonly Lazy<Cursor> s_sizeAllCursor = new(() => new Cursor(StandardCursorType.SizeAll));

    // ====================================================================
    // コンストラクタ
    // ====================================================================

    /// <summary><see cref="DragRectControl"/> の新しいインスタンスを初期化します（AXAML および既定構成向け）。</summary>
    /// <remarks>
    /// カーソル操作はデフォルトの Win32 実装（<c>WinCursorService</c>。生成は <see cref="PlatformFactory.CreateCursorService"/>）で動作する。<br/>
    /// nudge 後の再描画要求は本コントロール自身の <see cref="Control.InvalidateVisual"/> に配線し、<br/>
    /// 矢印キーを受け取れるようフォーカス可能にする（自己完結・ホスト依存なし）。<br/>
    /// </remarks>
    public DragRectControl()
        : this(null)
    {
    }

    /// <summary>カーソルサービスを注入して初期化する。</summary>
    /// <param name="cursorService">カーソルサービス。null の場合は Windows 既定実装を使用する。</param>
    public DragRectControl(ICursorService? cursorService)
    {
        Focusable = true;
        _state = new DragRectState(cursorService);
        _state.Port = _coordinatePort;
    }

    /// <summary>静的コンストラクタ。外観・動作設定（StyledProperty）の変更を再描画へ同期する。</summary>
    /// <remarks>
    /// 外観プロパティの変更は <see cref="Control.InvalidateVisual"/> で再描画を要求する。<br/>
    /// 動作設定（ヒットテスト・最小サイズ・クリップ）の変更も再描画へ反映する。
    /// </remarks>
    static DragRectControl()
    {
        BorderColorProperty.Changed.AddClassHandler<DragRectControl>(static (c, _) => c.InvalidateVisual());
        BorderThicknessProperty.Changed.AddClassHandler<DragRectControl>(static (c, _) => c.InvalidateVisual());
        HandleColorProperty.Changed.AddClassHandler<DragRectControl>(static (c, _) => c.InvalidateVisual());
        HandleSizeProperty.Changed.AddClassHandler<DragRectControl>(static (c, _) => c.InvalidateVisual());
        HandleHitRadiusProperty.Changed.AddClassHandler<DragRectControl>(static (c, _) => c.InvalidateVisual());
        MinSelectionSizeProperty.Changed.AddClassHandler<DragRectControl>(static (c, _) => c.InvalidateVisual());
        // 「画像外の選択を禁止」の ON 切替時、既存の選択矩形を境界内へ即時クリップする（ドラッグ開始を待たない）。
        // 変更前後の値をログ出力する。
        ClampToDomainBoundsProperty.Changed.AddClassHandler<DragRectControl>(static (c, e) =>
        {
            c.Log.LogSettingChanged(nameof(ClampToDomainBounds),
                e.OldValue?.ToString() ?? "", e.NewValue?.ToString() ?? "");
            c.ApplyClampToBounds();
        });
        MarkerColorProperty.Changed.AddClassHandler<DragRectControl>(static (c, _) => c.InvalidateVisual());
        MarkerLengthProperty.Changed.AddClassHandler<DragRectControl>(static (c, _) => c.InvalidateVisual());
        ShowCoordLabelProperty.Changed.AddClassHandler<DragRectControl>(static (c, _) => c.InvalidateVisual());
        CoordLabelColorProperty.Changed.AddClassHandler<DragRectControl>(static (c, _) => c.InvalidateVisual());
        CoordLabelBackgroundColorProperty.Changed.AddClassHandler<DragRectControl>(static (c, _) => c.InvalidateVisual());
        CoordLabelFontSizeProperty.Changed.AddClassHandler<DragRectControl>(static (c, _) => c.InvalidateVisual());
    }

    // ====================================================================
    // ログ
    // ====================================================================

    /// <summary>内部ロガー（コンストラクタ注入用。null の場合は出力なし）。</summary>
    /// <value>null を設定した場合は <see cref="NullLogger"/> へフォールバックする。</value>
    /// <remarks>
    /// C#14 の <c>field</c> キーワードでバッキングフィールドを暗黙定義する。<br/>
    /// getter / setter とも null を <see cref="NullLogger"/> へ安全に変換する。
    /// </remarks>
    public ILogger Log
    {
        get => field ?? NullLogger.Instance;
        set => field = value ?? NullLogger.Instance;
    }

    // ====================================================================
    // イベント
    // ====================================================================

    /// <summary>ドラッグの始点が決定されたときに発生するイベント（新規選択のドラッグ開始。座標は DIP）。</summary>
    public event EventHandler<DragRectPointEventArgs>? SelectionStarted;

    /// <summary>選択矩形が変更されたときに発生するイベント（上位 Mediator への通知）。</summary>
    public event EventHandler<SelectionRectEventArgs>? SelectionChanged;

    /// <summary>ドラッグ後の終点が決定され、選択矩形が確定したときに発生するイベント。</summary>
    public event EventHandler<SelectionRectEventArgs>? SelectionCompleted;

    /// <summary>領域がキャンセルされたときに発生するイベント（枠外クリック・小さすぎる矩形等）。</summary>
    public event EventHandler? SelectionCancelled;

    /// <summary>ポインター押下（左ボタン）時に発生するイベント（ドメイン座標）。</summary>
    public event EventHandler<DragRectPointEventArgs>? MouseDown;

    /// <summary>ポインター解放（左ボタン）時に発生するイベント（ドメイン座標）。</summary>
    public event EventHandler<DragRectPointEventArgs>? MouseUp;

    /// <summary>クリック（ドラッグ移動なしの押下→解放）時に発生するイベント（ドメイン座標）。</summary>
    public event EventHandler<DragRectPointEventArgs>? MouseClicked;

    /// <summary>ポインターのダブルクリックが検出されたときに発生するイベント（汎用公開。直接の消費者なし）。</summary>
    /// <remarks>
    /// 確定/キャンセルの判定は DragRect 自身が行う（矩形内 = <see cref="ConfirmRequested"/> / 矩形外 = <see cref="CancelRequested"/>）。<br/>
    /// 本イベントは「ダブルクリックが発生した事実」を汎用的に通知する（座標はドメイン）。
    /// </remarks>
    public event EventHandler<DragRectPointEventArgs>? MouseDoubleClicked;

    /// <summary>マウスポインタの位置を通知するイベント（ドメイン座標）。</summary>
    /// <remarks>
    /// nudge 関係なく、マウス移動のたびに現在のポインタ位置を通知する。<br/>
    /// 要素検出・ルーペ追従等の汎用用途に使用する（名前は Avalonia の
    /// <see cref="InputElement.PointerMoved"/>（RoutedEvent）と衝突しないよう付けた）。<br/>
    /// </remarks>
    public event EventHandler<DragRectPointEventArgs>? MouseMoved;

    /// <summary>ルーペが表示する中心座標を通知するイベント（ドメイン座標）。</summary>
    /// <remarks>
    /// nudge 前後関係なく通知する（通常追従: <see cref="OnPointerMoved"/> / nudge 後: nudge 処理）。<br/>
    /// 上位（ホスト）は nudge 中かどうかを判断せず、この座標でルーペを更新するだけである。
    /// </remarks>
    public event EventHandler<DragRectPointEventArgs>? LoupePositionMoved;

    /// <summary>ビューポート（ズーム・スクロール）が変化したときに発生するイベント。</summary>
    /// <remarks>
    /// 本コントロールが属する <see cref="ScrollViewer"/> の <c>ScrollChanged</c>（スクロール・ズーム・
    /// 画像サイズ変更による Extent/Viewport 変化）を検知して発行する。<br/>
    /// 選択矩形（ドメイン座標）はズーム・スクロールで不変だが、上位（FadeMask 暗転マスク等）は<br/>
    /// ビューポート座標で再描画する必要があるため、現在の選択矩形 + Snapshot を再通知する。<br/>
    /// </remarks>
    public event EventHandler<SelectionRectEventArgs>? ViewportChanged;

    /// <summary>Ctrl+ホイールによるズーム要求イベント（true = ズームイン / false = ズームアウト）。</summary>
    /// <remarks>
    /// 祖先 <see cref="ScrollViewer"/> が存在する場合（Crop モード等）に、<see cref="OnPointerWheelChanged"/> から発火する。<br/>
    /// ズームの実処理（画像サイズ変更・ズーム率管理）は上位 AP が実施する（本コントロールは要求のみ通知）。<br/>
    /// </remarks>
    public event EventHandler<bool>? ZoomRequested;

    /// <summary>Enter キーによる確定要求イベント（選択確定後のみ発火。ドラッグ中は発火しない）。</summary>
    /// <remarks>
    /// 本コントロールが Escape / Enter キーを自己処理し（確定・キャンセルの基本動作を内包）、<br/>
    /// モード差分（確定/キャンセル後の後始末）は上位が本イベントを購読して行う。<br/>
    /// </remarks>
    public event EventHandler<SelectionRectEventArgs>? ConfirmRequested;

    /// <summary>Escape キーによるキャンセル要求イベント（選択破棄は本コントロールが実施済み）。</summary>
    /// <remarks>
    /// 本コントロールが Escape / Enter キーを自己処理し（確定・キャンセルの基本動作を内包）、<br/>
    /// モード差分（確定/キャンセル後の後始末）は上位が本イベントを購読して行う。<br/>
    /// </remarks>
    public event EventHandler? CancelRequested;

    // ====================================================================
    // 外観・動作設定（StyledProperty）
    // ====================================================================

    /// <summary><see cref="BorderColor"/> 依存関係プロパティ（不透明度は色のアルファに統合）。</summary>
    /// <remarks>既定は DarkGray（<c>#FFA9A9A9</c>。設定画面の選択枠の色と一致）。</remarks>
    public static readonly StyledProperty<Color> BorderColorProperty =
        AvaloniaProperty.Register<DragRectControl, Color>(nameof(BorderColor), defaultValue: Color.FromAColor(0xFF, Colors.DarkGray));

    /// <summary><see cref="BorderThickness"/> 依存関係プロパティ。</summary>
    /// <remarks>デフォルトは FadeMaskRenderer / App（Settings）の既定（2.0）と一致させる。</remarks>
    public static readonly StyledProperty<double> BorderThicknessProperty =
        AvaloniaProperty.Register<DragRectControl, double>(nameof(BorderThickness), defaultValue: 2.0);

    /// <summary><see cref="HandleColor"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<Color> HandleColorProperty =
        AvaloniaProperty.Register<DragRectControl, Color>(nameof(HandleColor), defaultValue: Colors.White);

    /// <summary><see cref="HandleSize"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<double> HandleSizeProperty =
        AvaloniaProperty.Register<DragRectControl, double>(nameof(HandleSize), defaultValue: 10.0);

    /// <summary><see cref="HandleHitRadius"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<double> HandleHitRadiusProperty =
        AvaloniaProperty.Register<DragRectControl, double>(nameof(HandleHitRadius), defaultValue: 12.0);

    /// <summary><see cref="MinSelectionSize"/> 依存関係プロパティ。</summary>
    /// <remarks>既定は 6（設定画面の選択矩形の最小サイズのデフォルトと一致）。</remarks>
    public static readonly StyledProperty<double> MinSelectionSizeProperty =
        AvaloniaProperty.Register<DragRectControl, double>(nameof(MinSelectionSize), defaultValue: 6.0);

    /// <summary><see cref="ClampToDomainBounds"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<bool> ClampToDomainBoundsProperty =
        AvaloniaProperty.Register<DragRectControl, bool>(nameof(ClampToDomainBounds), defaultValue: true);

    /// <summary><see cref="MarkerColor"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<Color> MarkerColorProperty =
        AvaloniaProperty.Register<DragRectControl, Color>(nameof(MarkerColor), defaultValue: Colors.White);

    /// <summary><see cref="MarkerLength"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<double> MarkerLengthProperty =
        AvaloniaProperty.Register<DragRectControl, double>(nameof(MarkerLength), defaultValue: 16.0);

    /// <summary><see cref="ShowCoordLabel"/> 依存関係プロパティ（カーソル座標ラベルの表示有無）。</summary>
    public static readonly StyledProperty<bool> ShowCoordLabelProperty =
        AvaloniaProperty.Register<DragRectControl, bool>(nameof(ShowCoordLabel), defaultValue: false);

    /// <summary><see cref="CoordLabelColor"/> 依存関係プロパティ（カーソル座標ラベルの文字色）。</summary>
    public static readonly StyledProperty<Color> CoordLabelColorProperty =
        AvaloniaProperty.Register<DragRectControl, Color>(nameof(CoordLabelColor), defaultValue: Colors.White);

    /// <summary><see cref="CoordLabelBackgroundColor"/> 依存関係プロパティ（カーソル座標ラベルの背景色・不透明度はアルファに統合）。</summary>
    public static readonly StyledProperty<Color> CoordLabelBackgroundColorProperty =
        AvaloniaProperty.Register<DragRectControl, Color>(nameof(CoordLabelBackgroundColor), defaultValue: Color.FromAColor(153, Colors.Black));

    /// <summary><see cref="CoordLabelFontSize"/> 依存関係プロパティ（カーソル座標ラベルのフォントサイズ 6〜48 に補正）。</summary>
    public static readonly StyledProperty<double> CoordLabelFontSizeProperty =
        AvaloniaProperty.Register<DragRectControl, double>(nameof(CoordLabelFontSize), defaultValue: 12.0, coerce: CoerceCoordLabelFontSize);

    /// <summary>選択枠線の色（不透明度は色のアルファに統合）。</summary>
    public Color BorderColor
    {
        get => GetValue(BorderColorProperty);
        set => SetValue(BorderColorProperty, value);
    }

    /// <summary>選択枠線の太さ。</summary>
    public double BorderThickness
    {
        get => GetValue(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    /// <summary>ドラッグハンドルの色。既定値は白。</summary>
    public Color HandleColor
    {
        get => GetValue(HandleColorProperty);
        set => SetValue(HandleColorProperty, value);
    }

    /// <summary>ドラッグハンドルの一辺サイズ。</summary>
    public double HandleSize
    {
        get => GetValue(HandleSizeProperty);
        set => SetValue(HandleSizeProperty, value);
    }
    /// <summary>ドラッグハンドルのヒットテスト半径（DIP 座標）。</summary>
    /// <remarks>
    /// <b>DIP 座標での半径</b>（ユーザー指定・画面上の大きさを DIP で固定）。<br/>
    /// ズーム・縮小表示（Crop の Fit 等）を変えてもヒットテスト半径の画面上の大きさが一定になる。<br/>
    /// ドメイン座標系への変換は <see cref="ToDomainRadius"/> が行う。
    /// </remarks>
    public double HandleHitRadius
    {
        get => GetValue(HandleHitRadiusProperty);
        set => SetValue(HandleHitRadiusProperty, value);
    }

    /// <summary>選択矩形の最小サイズ（ドメイン座標）。</summary>
    public double MinSelectionSize
    {
        get => GetValue(MinSelectionSizeProperty);
        set => SetValue(MinSelectionSizeProperty, value);
    }

    /// <summary>イメージ（ドメイン境界）の外側を矩形選択可能にするかどうか。</summary>
    /// <remarks>
    /// <see langword="true"/>（既定）: 選択を画像範囲内に制限する。<br/>
    /// <see langword="false"/>: 画像外も選択可能（ドラッグで画像外に矩形を作成・移動できる）。<br/>
    /// クリップ範囲は <see cref="SetCoordinateTransform"/> の <c>domainBounds</c>（<see cref="Estable.AvaloniaUI.Platform.ICoordTransform.Bounds"/>）で指定する。
    /// </remarks>
    public bool ClampToDomainBounds
    {
        get => GetValue(ClampToDomainBoundsProperty);
        set => SetValue(ClampToDomainBoundsProperty, value);
    }

    /// <summary>nudge 中に表示する十字マーカーの色。</summary>
    public Color MarkerColor
    {
        get => GetValue(MarkerColorProperty);
        set => SetValue(MarkerColorProperty, value);
    }

    /// <summary>十字マーカーの線の太さ（DIP）。</summary>
    private const double MarkerThickness = 2.0;

    /// <summary>nudge 中に表示する十字マーカーの長さ（全長。中心から両端までの長さ・DIP 座標）。</summary>
    /// <remarks>既定値は 16.0（半幅 8.0 の十字線。ドラッグハンドルより大きいサイズ）。</remarks>
    public double MarkerLength
    {
        get => GetValue(MarkerLengthProperty);
        set => SetValue(MarkerLengthProperty, value);
    }

    /// <summary>カーソルの右側にカーソルのドメイン座標ラベル（"(x, y)"）を表示するかどうかを取得または設定する。</summary>
    /// <remarks>既定は false（非表示）。true にするとマウス位置のドメイン座標をカーソルの右側に表示する。</remarks>
    public bool ShowCoordLabel
    {
        get => GetValue(ShowCoordLabelProperty);
        set => SetValue(ShowCoordLabelProperty, value);
    }

    /// <summary>カーソル座標ラベルの文字色を取得または設定する。</summary>
    public Color CoordLabelColor
    {
        get => GetValue(CoordLabelColorProperty);
        set => SetValue(CoordLabelColorProperty, value);
    }

    /// <summary>カーソル座標ラベルの背景色を取得または設定する。</summary>
    /// <remarks>不透明度は色のアルファに統合する（既定: 黒 60% = <c>#99000000</c>）。</remarks>
    public Color CoordLabelBackgroundColor
    {
        get => GetValue(CoordLabelBackgroundColorProperty);
        set => SetValue(CoordLabelBackgroundColorProperty, value);
    }

    /// <summary>カーソル座標ラベルのフォントサイズを取得または設定する（6〜48）。</summary>
    public double CoordLabelFontSize
    {
        get => GetValue(CoordLabelFontSizeProperty);
        set => SetValue(CoordLabelFontSizeProperty, value);
    }

    /// <summary><see cref="CoordLabelFontSize"/> を安全域へ補正する。</summary>
    private static double CoerceCoordLabelFontSize(AvaloniaObject o, double v)
        => double.IsFinite(v) ? Math.Clamp(v, CoordLabelFontSizeMin, CoordLabelFontSizeMax) : CoordLabelFontSizeDefault;

    /// <summary>カーソル座標ラベルのフォントサイズの最小値。</summary>
    private const double CoordLabelFontSizeMin = 6.0;

    /// <summary>カーソル座標ラベルのフォントサイズの最大値。</summary>
    private const double CoordLabelFontSizeMax = 48.0;

    /// <summary>カーソル座標ラベルのフォントサイズの既定値。</summary>
    private const double CoordLabelFontSizeDefault = 12.0;

    /// <summary>マウスホイール 1 段あたりのスクロール量（DIP）。</summary>
    private const double WheelScrollUnit = 48.0;

    // ====================================================================
    // 公開操作 API
    // ====================================================================

    /// <summary>現在の選択状態のスナップショット（ポーリング不要の完全状態）。</summary>
    /// <remarks>
    /// <see cref="DragRectSnapshot"/> の全項目（State / SelectionRect / ActiveHandle / MousePosition /<br/>
    /// IsMouseDown / IsDragging / NudgeActive / HasSelection）を公開する。ホスト（上位 AP）はイベント引数の<br/>
    /// Snapshot と同一形式で現在の状態を取得できる（状態のポーリングは本プロパティで完結）。<br/>
    /// 選択状態の判定（確定・キャンセル等の操作分岐）は <see cref="DragRectSnapshot.HasSelection"/> を使用する。
    /// </remarks>
    public DragRectSnapshot CurrentSnapshot => _state.CreateSnapshot();

    /// <summary>「画像外の選択を禁止」（<see cref="ClampToDomainBounds"/>）の変更を現在の選択矩形へ適用する。</summary>
    /// <remarks>
    /// ON に切り替わった瞬間に、既存の選択矩形をドメイン境界内へクリップして即時反映する。<br/>
    /// クリップで矩形が変更された場合のみ <see cref="SelectionChanged"/> を発火し、再描画を要求する。<br/>
    /// <see cref="ClampToDomainBoundsProperty"/> の Changed ハンドラから呼ばれる（プロパティ変更 → 状態適用の一元化）。
    /// </remarks>
    private void ApplyClampToBounds()
    {
        // 状態適用（クリップ + 変更検出）は DragRectState が担い、通知・再描画は本コントロールが担う
        if (_state.ClampSelectionToBounds())
        {
            RaiseSelectionChanged(_state.SelectionRect);
        }
        InvalidateVisual();
    }

    /// <summary>ドメイン ↔ コントロールローカル DIP の座標変換ポートを設定する。</summary>
    /// <param name="port">座標変換ポート（Forward = DIP→ドメイン / Inverse = ドメイン→DIP / Bounds = ドメイン境界）。null の場合は等価変換。</param>
    /// <remarks>
    /// ホスト（上位 AP）がモードに応じた変換ポートを設定する（AreaSelect: 仮想スクリーンピクセル / Crop: 元画像ピクセル）。<br/>
    /// ポートの生成は PlatformFactory の <see cref="Estable.AvaloniaUI.Platform.CoordTransform"/> のファクトリ（<c>Compose</c> / <c>ScrollOffset</c> / <c>DomainScale</c> / <c>ImageLayout</c> 等）で行う。<br/>
    /// 未設定時は等価変換（1 ドメインピクセル = 1 DIP・無制限）。<br/>
    /// 変換関数は呼び出し毎に評価されるため、ズーム・スクロール等の動的変化を反映できる。<br/>
    /// 座標変換の変更は描画（選択矩形・ハンドル・十字線）へ即時反映されるため、再描画を要求する。
    /// </remarks>
    public void SetCoordinateTransform(ICoordTransform? port)
    {
        _coordinatePort = port ?? CoordTransform.Identity;
        _state.Port = _coordinatePort;
        InvalidateVisual();
    }

    /// <summary>選択矩形を外部状態から復元する。</summary>
    /// <param name="rect">選択矩形（ドメイン座標）。</param>
    /// <remarks>
    /// 状態変更後に再描画を即時要求し、<see cref="SelectionChanged"/> で通知する。<br/>
    /// <see cref="ClampToDomainBounds"/> が有効な場合は、画像外にはみ出す矩形を境界内にクリップしてから設定する<br/>
    /// （外部からの復元でも「画像外の選択を禁止」を尊重する）。
    /// </remarks>
    public void SetSelection(Rect rect)
    {
        var selectionRect = rect;
        // ClampToDomainBounds 有効時は画像外にはみ出す矩形を境界内にクリップしてから設定する
        if (ClampToDomainBounds)
        {
            selectionRect = _state.ClipRectToBounds(selectionRect);
        }
        _state.SetSelection(selectionRect);
        RaiseSelectionChanged(selectionRect);
        InvalidateVisual();
    }

    /// <summary>選択状態と nudge 状態を初期化する。</summary>
    /// <remarks>状態変更後に再描画を即時要求する。</remarks>
    public void ResetSelection()
    {
        _state.Reset();
        InvalidateVisual();
    }

    /// <summary>キャンセル要求を処理する（Escape キーと同じ条件）。</summary>
    /// <remarks>
    /// 外部（ホスト）が選択操作をキャンセルする際に呼ぶ。<br/>
    /// ドラッグ中 / 選択ありの場合は選択破棄（<see cref="ResetSelection"/>）を実施した後に
    /// <see cref="CancelRequested"/> を発火する。
    /// </remarks>
    public void RequestCancel()
    {
        Log.LogCancelRequested();
        if (_state.IsDragging || _state.HasSelection)
        {
            ResetSelection();
        }
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>外部（ホスト）からの確定要求を処理する（Enter キーと同じ条件）。</summary>
    /// <remarks>
    /// UI ボタン等から DragRect の選択を確定する際に呼ぶ。<br/>
    /// ドラッグ中は無視し、選択確定後のみ <see cref="ConfirmRequested"/>（選択矩形 + Snapshot 付き）を発行する。
    /// </remarks>
    public void RequestConfirm()
    {
        if (_state.IsDragging || !_state.HasSelection) return;
        Log.LogConfirmRequested(_state.SelectionRect.ToString());
        ConfirmRequested?.Invoke(this, new SelectionRectEventArgs(_state.SelectionRect, _state.CreateSnapshot()));
    }

    // ====================================================================
    // イベント発火ヘルパー（公開イベントへ状態を通知）
    // ====================================================================

    /// <summary><see cref="SelectionStarted"/> を発火する。</summary>
    private void RaiseSelectionStarted(Point domainPos)
        => SelectionStarted?.Invoke(this, new DragRectPointEventArgs(domainPos, _state.CreateSnapshot(domainPos)));

    /// <summary><see cref="SelectionChanged"/> を発火する。</summary>
    private void RaiseSelectionChanged(Rect rect)
        => SelectionChanged?.Invoke(this, new SelectionRectEventArgs(rect, _state.CreateSnapshot()));

    /// <summary><see cref="SelectionCompleted"/> を発火する。</summary>
    private void RaiseSelectionCompleted(Rect rect)
        => SelectionCompleted?.Invoke(this, new SelectionRectEventArgs(rect, _state.CreateSnapshot()));

    /// <summary><see cref="SelectionCancelled"/> を発火する。</summary>
    private void RaiseSelectionCancelled()
        => SelectionCancelled?.Invoke(this, EventArgs.Empty);

    /// <summary><see cref="MouseDown"/> を発火する。</summary>
    private void RaiseMouseDown(Point domainPos)
        => MouseDown?.Invoke(this, new DragRectPointEventArgs(domainPos, _state.CreateSnapshot(domainPos)));

    /// <summary><see cref="MouseUp"/> を発火する。</summary>
    private void RaiseMouseUp(Point domainPos)
        => MouseUp?.Invoke(this, new DragRectPointEventArgs(domainPos, _state.CreateSnapshot(domainPos)));

    /// <summary><see cref="MouseClicked"/> を発火する。</summary>
    private void RaiseMouseClicked(Point domainPos)
        => MouseClicked?.Invoke(this, new DragRectPointEventArgs(domainPos, _state.CreateSnapshot(domainPos)));

    /// <summary><see cref="MouseDoubleClicked"/> を発火する。</summary>
    private void RaiseMouseDoubleClicked(Point domainPos)
        => MouseDoubleClicked?.Invoke(this, new DragRectPointEventArgs(domainPos, _state.CreateSnapshot(domainPos)));

    /// <summary><see cref="MouseMoved"/> を発火する。</summary>
    private void RaiseMouseMoved(Point domainPos)
        => MouseMoved?.Invoke(this, new DragRectPointEventArgs(domainPos, _state.CreateSnapshot(domainPos)));

    /// <summary><see cref="LoupePositionMoved"/> を発火する。</summary>
    private void RaiseLoupePositionMoved(Point domainPos)
        => LoupePositionMoved?.Invoke(this, new DragRectPointEventArgs(domainPos, _state.CreateSnapshot(domainPos)));

    /// <summary><see cref="ViewportChanged"/> を発火する。</summary>
    private void RaiseViewportChanged()
        => ViewportChanged?.Invoke(this, new SelectionRectEventArgs(_state.SelectionRect, _state.CreateSnapshot()));

    /// <summary>DIP 半径をドメイン半径に変換する（ヒットテスト用）。</summary>
    /// <returns>ドメイン座標系でのヒットテスト半径。</returns>
    /// <remarks>
    /// Forward の差を取ることでオフセット（レターボックス等）をキャンセルし、スケールのみを得る。<br/>
    /// スケールがほぼ 0（等価変換）の場合は DIP 半径をそのまま使用する。
    /// </remarks>
    private double ToDomainRadius()
    {
        var port = _coordinatePort;
        var p0 = port.Forward(default);
        var p1 = port.Forward(new Point(HandleHitRadius, 0));
        double radius = Math.Abs(p1.X - p0.X);
        return radius < 0.001 ? HandleHitRadius : radius;
    }

    // ====================================================================
    // イベントオーバーライド（ポインター入力）
    // ====================================================================

    /// <summary>ポインター押下時の処理。左ボタンのみ処理する。</summary>
    /// <param name="e">ポインターイベント。</param>
    /// <remarks>
    /// 左ボタン以外（右クリック・中ボタン等）は無視する。<br/>
    /// 座標はコントロールローカル DIP で取得し、ドメイン座標に変換して処理する。<br/>
    /// nudge 中は押下位置を nudge 後の始点に置き換えてずれを防ぐ。<br/>
    /// 矢印キーを受け取れるようフォーカスを獲得する。
    /// </remarks>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        _state.IsMouseDown = true;
        var domainPos = _coordinatePort.Forward(e.GetPosition(this).ToPoint());

        // マウス Down 直前の位置をログ（高頻度の移動ログは行わない）
        Log.LogPositionBeforeDown(domainPos.X, domainPos.Y);
        Log.LogMouseDown(domainPos.X, domainPos.Y, e.ClickCount, _state.HasSelection);

        // nudge 中ならリセット（ポインター再表示・基点クリア）してから押下処理する
        if (_state.NudgeActive)
        {
            domainPos = _state.StartPoint;
            EndNudge();
            RaiseLoupePositionMoved(domainPos);
        }

        _lastPressPos = domainPos;
        _dragPerformed = false;
        RaiseMouseDown(domainPos);

        // ダブルクリック検出: ドラッグ処理は行わず、矩形内判定で確定/キャンセル要求を DragRect 自身が発行する
        if (e.ClickCount >= 2)
        {
            var inside = _state.HasSelection && _state.SelectionRect.Contains(domainPos);
            Log.LogDoubleClick(domainPos.X, domainPos.Y, inside);
            RaiseMouseDoubleClicked(domainPos);
            if (inside)
            {
                Log.LogConfirmRequested(_state.SelectionRect.ToString());
                ConfirmRequested?.Invoke(this, new SelectionRectEventArgs(_state.SelectionRect, _state.CreateSnapshot(domainPos)));
            }
            else
            {
                Log.LogCancelRequested();
                CancelRequested?.Invoke(this, EventArgs.Empty);
            }
            e.Handled = true;
            return;
        }

        // ハンドルヒットテスト → リサイズ/移動 or 新規選択の開始
        var handle = _state.HitTest(domainPos, ToDomainRadius());
        if (handle != DragHandle.None)
        {
            Log.LogDragStart(handle.ToString(), domainPos.X, domainPos.Y);
            _state.StartDrag(handle, domainPos);

            // C案: リサイズハンドル（8 方向）では、ポインタ自体をハンドル点へスナップし、
            // ルーペ中心もハンドル点へ揃える（ポインタ・矩形の角・ルーペを一体にする）。
            // ドラッグ基準点（DragStartPoint）もハンドル点に揃えることで、スナップ後の最初の移動が
            // ハンドル点からの差分として正しく矩形へ反映される（Down 位置のズレ分の飛びを防ぐ）。
            // 移動（Move）は「掴んだ位置」を維持するのが自然なためスナップしない。
            if (handle != DragHandle.Move)
            {
                var handlePos = SelectionGeometry.GetHandlePosition(_state.SelectionRect, handle);
                _state.DragStartPoint = handlePos;
                _state.LastMousePosition = handlePos;
                _state.Cursor.SetCursorScreenPoint(ToScreen(handlePos));
                RaiseLoupePositionMoved(handlePos);
            }
        }
        else
        {
            _state.StartNewSelection(domainPos);
            RaiseSelectionStarted(domainPos);
        }

        e.Handled = true;
        Focus();
        InvalidateVisual();
    }

    /// <summary>ポインター移動時の処理。</summary>
    /// <param name="e">ポインターイベント。</param>
    /// <remarks>
    /// ボタン押下中のドラッグ処理を <see cref="DragRectState.UpdateDrag"/> に委譲する。<br/>
    /// ドラッグハンドルに近づいたときはハンドル種別に応じたカーソルへ変更する。<br/>
    /// nudge 中は SetCursorPos 由来の PointerMoved で矩形を上書きしない（単押し nudge が戻る問題の対策）。<br/>
    /// ヒットテスト半径（<see cref="ToDomainRadius"/>）は 1 回だけ計算して、エコー判定・カーソル更新・ドラッグ更新で共有する
    /// （ポインター移動毎の Forward 変換の重複呼び出しを排除）。
    /// </remarks>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var domainPos = _coordinatePort.Forward(e.GetPosition(this).ToPoint());
        var hitRadius = ToDomainRadius();

        // nudge 終了（EndNudge の SetCursorPos）直後のエコー PointerMoved をスキップする
        if (_state.SuppressEchoAt is { } echoPos && SelectionGeometry.IsNear(domainPos, echoPos, hitRadius))
        {
            // エコー抑制は最初の 1 回のみ（Reset まで残留すると、同じ位置へのホバーでカーソル形状の更新が
            // 妨げられるため、消費したらクリアする）。
            _state.SuppressEchoAt = null;
            // 抑制時でもカーソル形状はハンドル位置に応じて更新する（nudge 後のホバーで矢印のままになる問題の対策）
            UpdateCursor(domainPos, hitRadius);
            return;
        }

        // マウスポインタの位置を通知（nudge 関係なく常時。要素検出等の汎用用途）
        RaiseMouseMoved(domainPos);

        // ドラッグハンドルに近づいたときのカーソル変更
        UpdateCursor(domainPos, hitRadius);

        // nudge 中は SetCursorPos 由来の PointerMoved で矩形・基点（LastMousePosition）を上書きしない
        // （LastMousePosition は nudge 適用後の終点を保持し、解放時のルーペ位置が実マウス位置に飛ぶのを防ぐ）
        if (_state.NudgeActive)
        {
            return;
        }

        _state.LastMousePosition = domainPos;

        if (_state.IsDragging)
        {
            _state.UpdateDrag(domainPos, MinSelectionSize, ClampToDomainBounds);
            // Click 判定用: 押下中のドラッグ移動で矩形が変化した場合はドラッグ発生
            if (_state.SelectionRect != _state.DragStartRect)
            {
                _dragPerformed = true;
            }
            RaiseSelectionChanged(_state.SelectionRect);

            // ドラッグ中はアクティブハンドルの位置（リサイズ中のハンドル点・移動中の矩形中心・
            // 新規選択の終点）をルーペ中心に通知する（nudge と同じ挙動）。
            // クリップ（ClampToDomainBounds）や最小サイズ保証でマウス位置と矩形の角がずれる場合も、
            // 実際のハンドル点を追従して矩形の微調整位置を確認できる。
            RaiseLoupePositionMoved(_state.GetMarkerPosition());
        }
        else
        {
            // ルーペの中心座標を通知（通常追従: ホバー位置）
            RaiseLoupePositionMoved(domainPos);
        }
        InvalidateVisual();
    }

    /// <summary>ポインター解放時の処理。</summary>
    /// <param name="e">ポインターイベント。</param>
    /// <remarks>
    /// <see cref="DragRectState.EndDrag"/> に委譲し、選択確定・解除を処理する。<br/>
    /// nudge 中は nudge を必ず終了する（ポインタを nudge 位置へ移動・十字線を消す・ポインタ再表示）。<br/>
    /// nudge 位置はドラッグ終了処理の前に計算する（<see cref="DragRectState.EndDrag"/> が
    /// <see cref="DragRectState.IsDragging"/> / <see cref="DragRectState.ActiveHandle"/> をリセットするため、
    /// 後から <see cref="GetMarkerPosition"/> を呼ぶと固定コーナー（StartPoint）を返してしまう）。
    /// </remarks>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _state.IsMouseDown = false;
        var domainPos = _coordinatePort.Forward(e.GetPosition(this).ToPoint());
        RaiseMouseUp(domainPos);

        // マウス Up 直後の位置をログ（高頻度の移動ログは行わない）
        Log.LogMouseUp(domainPos.X, domainPos.Y, _state.IsDragging, _state.HasSelection);
        Log.LogPositionAfterUp(domainPos.X, domainPos.Y);

        var wasDragging = _state.IsDragging;
        var wasNudging = _state.NudgeActive;
        var pressPos = _lastPressPos;
        // nudge 中のマーカー位置（EndDrag が IsDragging / ActiveHandle をリセットする前に計算）
        var nudgePos = wasNudging ? _state.GetMarkerPosition() : default;

        // ドラッグ終了（確定 or キャンセル判定）。wasDragging は EndDrag 前の状態と同値のため再利用する
        if (wasDragging)
        {
            var cancelled = _state.SelectionRect.Width < MinSelectionSize || _state.SelectionRect.Height < MinSelectionSize;
            _state.EndDrag();
            if (cancelled)
            {
                _state.Reset();
                RaiseSelectionCancelled();
            }
            else
            {
                RaiseSelectionCompleted(_state.SelectionRect);
            }
        }

        // Click 検出: ドラッグ移動なしの Down→Up 完結（シングルクリック）
        if (wasDragging && !_dragPerformed)
        {
            Log.LogClick(pressPos.X, pressPos.Y);
            RaiseMouseClicked(pressPos);
        }

        // nudge 中なら終了する（ポインタを nudge 位置へ移動・十字線を消す・ポインタ再表示）
        if (wasNudging)
        {
            // nudge 中にユーザーが物理マウスを動かしても、カーソルは nudge 位置へ戻してから再表示する
            // （nudge から Up までのマウス移動はキャンセルし、nudge 位置で表示する。SetCursorPos は再表示前に行う）
            _state.Cursor.SetCursorScreenPoint(ToScreen(nudgePos));
            EndNudge();
            RaiseLoupePositionMoved(nudgePos);
            _state.SuppressEchoAt = nudgePos;
            // カーソルは nudge 位置（ハンドル上）にあるため、ハンドルに応じたカーソル形状へ即時更新する
            // （マウスを動かさない限り PointerMoved が発火せず、形状が更新されないため）。
            UpdateCursor(nudgePos, ToDomainRadius());
        }

        InvalidateVisual();
    }

    /// <summary>マウスホイールでズーム・スクロールを操作する。</summary>
    /// <param name="e">ホイールイベント。</param>
    /// <remarks>
    /// 祖先 <see cref="ScrollViewer"/>（<see cref="_attachedScrollViewer"/>）が存在する場合（Crop モード）のみ処理する:<br/>
    /// - Ctrl + ホイール: <see cref="ZoomRequested"/> を発火する（ズームの実処理は上位 AP が実施）。<br/>
    /// - 通常ホイール: 祖先 ScrollViewer をスクロールする（Shift 併用で横スクロール）。<br/>
    /// ScrollViewer が存在しない場合（範囲選択（AreaSelect）等）は何もせず既定の動作に委ねる。<br/>
    /// </remarks>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        if (_attachedScrollViewer is not { } scrollViewer)
        {
            base.OnPointerWheelChanged(e);
            return;
        }

        // Ctrl + ホイール: ズーム要求（ズームは上位 AP が実施）
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            Log.LogZoomRequested(e.Delta.Y > 0);
            ZoomRequested?.Invoke(this, e.Delta.Y > 0);
            e.Handled = true;
            return;
        }

        // 通常ホイール: 祖先 ScrollViewer をスクロール（Shift 併用で横スクロール）
        double dx = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? e.Delta.Y * WheelScrollUnit : 0;
        double dy = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 0 : e.Delta.Y * WheelScrollUnit;
        scrollViewer.Offset = new Vector(scrollViewer.Offset.X + dx, scrollViewer.Offset.Y + dy);
        Log.LogWheelScroll(dx, dy, scrollViewer.Offset.X, scrollViewer.Offset.Y);
        e.Handled = true;
    }

    /// <summary>ドラッグハンドル上のカーソル形状を更新する。</summary>
    /// <param name="domainPos">判定するドメイン座標。</param>
    /// <param name="hitRadius">ヒットテスト半径（ドメイン座標。呼び出し側で事前計算済み）。</param>
    /// <remarks>ハンドル上ではリサイズ方向に応じたカーソル、枠内では移動カーソルを表示する。<br/>
    /// ヒットテスト半径は呼び出し側から受け取り、内部で <see cref="ToDomainRadius"/> を再計算しない（ホットパス高速化）。</remarks>
    private void UpdateCursor(Point domainPos, double hitRadius)
    {
        var handle = _state.HitTest(domainPos, hitRadius);
        // 標準カーソルは Lazy で遅延共有する（ポインター移動毎の生成を排除しつつ、初回アクセスで ICursorFactory を解決）
        Cursor = handle switch
        {
            DragHandle.TopLeft or DragHandle.BottomRight => s_topLeftCornerCursor.Value,
            DragHandle.TopRight or DragHandle.BottomLeft => s_topRightCornerCursor.Value,
            DragHandle.TopCenter or DragHandle.BottomCenter => s_sizeNorthSouthCursor.Value,
            DragHandle.MiddleLeft or DragHandle.MiddleRight => s_sizeWestEastCursor.Value,
            DragHandle.Move => s_sizeAllCursor.Value,
            _ => Cursor.Default,
        };
    }
}

