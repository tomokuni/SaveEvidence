using Estable.AvaloniaUI.DragRect.Logging;
using Estable.DragRect;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using EsUtil.Platform.AvaloniaUI;
using EsUtil.Platform.Core;
using EsUtil.Platform.Native;
using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Estable.AvaloniaUI.DragRect;

/// <summary>リサイズ可能なドラッグ矩形の UI コントロール（Avalonia 版）。</summary>
/// <remarks>
/// <b>層の構成</b>: 選択・ドラッグ・nudge の判断は UI 非依存の状態管理クラス（<c>DragRectControl.Core</c> の
/// <see cref="DragRectSession"/>）が担い、本コントロールは「入力の受付」「効果の適用」「通知の転送」「描画」のみを行う薄い層である。<br/>
/// <br/>
/// 本クラスの責務:<br/>
/// 1. ポインター / キー / ホイール入力をコア型（<see cref="EsPoint"/> / <see cref="EsKey"/>）へ変換してセッションへ通知する。<br/>
/// 2. セッションが返す効果（再描画・フォーカス・カーソル操作）を適用する。<br/>
/// 3. セッションからの通知を公開イベントへ転送する（判断はしない）。<br/>
/// 4. セッションの状態を描画する（枠線・ハンドル・十字マーカー・座標ラベル。計算は描画層へ委譲）。<br/>
/// <br/>
/// <b>自己完結</b>: 矢印キーの検出・nudge 後の再描画・カーソル表示/非表示・ダブルクリックの確定/キャンセル判定を
/// コアが担うため、ホスト依存のない動作になっている。ホストは <see cref="SelectionStarted"/>（始点決定）/
/// <see cref="SelectionCompleted"/>（終点決定）/ <see cref="SelectionCancelled"/>（領域キャンセル）/
/// <see cref="ConfirmRequested"/> / <see cref="CancelRequested"/> イベントを購読して利用する。<br/>
/// <br/>
/// 外観・動作の設定は依存関係プロパティ（<see cref="BorderColor"/> / <see cref="BorderThickness"/> /
/// <see cref="HandleColor"/> / <see cref="HandleSize"/> / <see cref="HandleHitRadius"/> / <see cref="MinSelectionSize"/> /
/// <see cref="MarkerColor"/> / <see cref="MarkerLength"/>）で公開し、外部バインディングで制御できる（色の不透明度は色のアルファに統合）。<br/>
/// 動作に影響する設定（<see cref="MinSelectionSize"/> / <see cref="ClampToDomainBounds"/> / <see cref="HandleHitRadius"/>）は
/// セッションへ伝搬し、判断はセッションが行う。<br/>
/// ログは <see cref="Log"/> プロパティで注入する（デフォルトは <see cref="NullLogger"/>）。<br/>
/// <br/>
/// 選択矩形の変更は <see cref="SelectionChanged"/> イベントで外部（上位 Mediator）に通知する。
/// </remarks>
public sealed partial class DragRectControl : Control
{
    // ====================================================================
    // 内部状態（コンポジション）
    // ====================================================================

    /// <summary>選択・ドラッグ・nudge の状態を管理するセッション（入力の解釈と状態遷移の判断を担う）。</summary>
    private readonly DragRectSession _session;

    /// <summary>カーソルサービス（nudge 時のカーソル移動・表示切替。既定実装の解決は UI 側で行う）。</summary>
    private readonly ICursorService _cursor;

    /// <summary>フォーカス非依存の矢印キー nudge 用に購読した TopLevel。解除時に使用する。</summary>
    private TopLevel? _attachedTopLevel;

    /// <summary>ズーム・スクロール検知用に購読した祖先 ScrollViewer。解除時に使用する。</summary>
    private ScrollViewer? _attachedScrollViewer;

    /// <summary>本コントロール自身のキーハンドラで処理したキーかどうか（TopLevel 購読との二重処理を防ぐ）。</summary>
    /// <remarks>本コントロールと TopLevel の双方がキーを受け取る（Avalonia のルーティング）ため、処理済みのキーを識別する。</remarks>
    private bool _nudgeHandledInKeyDown;

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
    /// <remarks>
    /// カーソルサービスの既定実装（Win32）の解決は UI 側で行い、コア（状態管理クラス）は抽象のみに依存する。<br/>
    /// コアからの通知は公開イベントへ転送し、動作に影響する設定値をセッションへ伝搬する。
    /// </remarks>
    public DragRectControl(ICursorService? cursorService)
    {
        Focusable = true;
        _cursor = cursorService ?? PlatformFactory.CreateCursorService();
        _session = new DragRectSession(_cursor)
        {
            MinSelectionSize = MinSelectionSize,
            ClampToDomainBounds = ClampToDomainBounds,
            HandleHitRadius = HandleHitRadius,
        };
        _session.Notified += OnSessionNotified;
    }

    /// <summary>静的コンストラクタ。外観・動作設定（StyledProperty）の変更を再描画とセッションへ同期する。</summary>
    /// <remarks>
    /// 外観プロパティの変更は <see cref="Control.InvalidateVisual"/> で再描画を要求する。<br/>
    /// 動作設定（最小サイズ・ヒットテスト半径・クリップ）はセッションへ伝搬し、判断はセッションが行う。
    /// </remarks>
    static DragRectControl()
    {
        BorderColorProperty.Changed.AddClassHandler<DragRectControl>(static (c, _) => c.InvalidateVisual());
        BorderThicknessProperty.Changed.AddClassHandler<DragRectControl>(static (c, _) => c.InvalidateVisual());
        HandleColorProperty.Changed.AddClassHandler<DragRectControl>(static (c, _) => c.InvalidateVisual());
        HandleSizeProperty.Changed.AddClassHandler<DragRectControl>(static (c, _) => c.InvalidateVisual());
        HandleHitRadiusProperty.Changed.AddClassHandler<DragRectControl>(static (c, e) =>
        {
            // StyledProperty<double> のため常に double（分岐なし）
            c._session.HandleHitRadius = e.GetNewValue<double>();
            c.InvalidateVisual();
        });
        MinSelectionSizeProperty.Changed.AddClassHandler<DragRectControl>(static (c, e) =>
        {
            c._session.MinSelectionSize = e.GetNewValue<double>();
            c.InvalidateVisual();
        });
        // 「画像外の選択を禁止」の ON 切替時、既存の選択矩形を境界内へ即時クリップする（ドラッグ開始を待たない）。
        // 変更前後の値をログ出力する。
        ClampToDomainBoundsProperty.Changed.AddClassHandler<DragRectControl>(static (c, e) =>
        {
            c._session.ClampToDomainBounds = e.GetNewValue<bool>();
            c.Log.LogSettingChanged(nameof(ClampToDomainBounds),
                e.OldValue!.ToString()!, e.NewValue!.ToString()!);
            c.ApplyFeedback(c._session.ApplyClampToBounds());
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
    /// getter / setter とも null を <see cref="NullLogger"/> へ安全に変換する。<br/>
    /// 入力の判断に伴うログはコア（セッション）が出力するため、同じロガーをセッションへ伝搬する。
    /// </remarks>
    public ILogger Log
    {
        get => field ?? NullLogger.Instance;
        set
        {
            field = value ?? NullLogger.Instance;
            _session.Log = field;
        }
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
    /// ドメイン座標系への変換はセッションが行う（設定は <see cref="DragRectSession"/> へ伝搬する）。
    /// </remarks>
    public double HandleHitRadius
    {
        get => GetValue(HandleHitRadiusProperty);
        set => SetValue(HandleHitRadiusProperty, value);
    }

    /// <summary>選択矩形の最小サイズ（ドメイン座標）。</summary>
    /// <remarks>ドラッグ完了時にこれ未満の矩形はキャンセル扱いになる（判断はセッションが行う）。</remarks>
    public double MinSelectionSize
    {
        get => GetValue(MinSelectionSizeProperty);
        set => SetValue(MinSelectionSizeProperty, value);
    }

    /// <summary>イメージ（ドメイン境界）の外側を矩形選択可能にするかどうか。</summary>
    /// <remarks>
    /// <see langword="true"/>（既定）: 選択を画像範囲内に制限する。<br/>
    /// <see langword="false"/>: 画像外も選択可能（ドラッグで画像外に矩形を作成・移動できる）。<br/>
    /// クリップ範囲は <see cref="SetCoordinateTransform"/> の <c>domainBounds</c>（<see cref="EsUtil.Platform.AvaloniaUI.ICoordTransform.Bounds"/>）で指定する。
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
    public DragRectSnapshot CurrentSnapshot => _session.CreateSnapshot();

    /// <summary>ドメイン ↔ コントロールローカル DIP の座標変換ポートを設定する。</summary>
    /// <param name="port">座標変換ポート（Forward = DIP→ドメイン / Inverse = ドメイン→DIP / Bounds = ドメイン境界）。null の場合は等価変換。</param>
    /// <remarks>
    /// ホスト（上位 AP）がモードに応じた変換ポートを設定する（AreaSelect: 仮想スクリーンピクセル / Crop: 元画像ピクセル）。<br/>
    /// ポートの生成は PlatformFactory の <see cref="EsUtil.Platform.AvaloniaUI.CoordTransform"/> のファクトリ（<c>Compose</c> / <c>ScrollOffset</c> / <c>DomainScale</c> / <c>ImageLayout</c> 等）で行う。<br/>
    /// 未設定時は等価変換（1 ドメインピクセル = 1 DIP・無制限）。<br/>
    /// 変換関数は呼び出し毎に評価されるため、ズーム・スクロール等の動的変化を反映できる。<br/>
    /// 座標変換の変更は描画（選択矩形・ハンドル・十字線）へ即時反映されるため、再描画を要求する。
    /// </remarks>
    public void SetCoordinateTransform(ICoordTransform? port)
    {
        _session.SetPort(port);
        InvalidateVisual();
    }

    /// <summary>選択矩形を外部状態から復元する。</summary>
    /// <param name="rect">選択矩形（ドメイン座標）。</param>
    /// <remarks>
    /// 状態の更新と通知はセッションが担う（<see cref="ClampToDomainBounds"/> が有効な場合は境界内へクリップしてから設定する）。<br/>
    /// 設定後は <see cref="SelectionChanged"/> が発火し、再描画される。
    /// </remarks>
    public void SetSelection(Rect rect) => ApplyFeedback(_session.SetSelection(rect.ToEsRect()));

    /// <summary>選択状態と nudge 状態を初期化する。</summary>
    /// <remarks>状態の初期化はセッションが担う（再描画される）。</remarks>
    public void ResetSelection() => ApplyFeedback(_session.Reset());

    /// <summary>キャンセル要求を処理する（Escape キーと同じ条件）。</summary>
    /// <remarks>
    /// 外部（ホスト）が選択操作をキャンセルする際に呼ぶ。<br/>
    /// ドラッグ中 / 選択ありの場合は選択破棄を実施した後に <see cref="CancelRequested"/> を発火する。
    /// </remarks>
    public void RequestCancel() => ApplyFeedback(_session.RequestCancel());

    /// <summary>外部（ホスト）からの確定要求を処理する（Enter キーと同じ条件）。</summary>
    /// <remarks>
    /// UI ボタン等から DragRect の選択を確定する際に呼ぶ。<br/>
    /// ドラッグ中は無視し、選択確定後のみ <see cref="ConfirmRequested"/>（選択矩形 + Snapshot 付き）を発行する。<br/>
    /// 確定後に選択状態をクリアする（確定後の再ドラッグ・再確定を防ぐ）。
    /// </remarks>
    public void RequestConfirm() => ApplyFeedback(_session.RequestConfirm());

    // ====================================================================
    // コアとの連携（効果の適用・通知の転送）
    // ====================================================================

    /// <summary>コア（状態管理クラス）が返す効果を画面へ適用する。</summary>
    /// <param name="feedback">適用する効果（再描画・フォーカス・カーソル操作）。</param>
    /// <remarks>
    /// カーソル操作は「移動 → 非表示 → 再表示」の順で適用する（nudge の終了では移動してから再表示する必要がある）。
    /// </remarks>
    private void ApplyFeedback(DragRectFeedback feedback)
    {
        if (feedback.RequestFocus)
        {
            Focus();
        }

        if (feedback.Cursor.HasFlag(DragRectCursorAction.MoveTo))
        {
            _cursor.SetCursorScreenPoint(ToScreen(feedback.CursorDomainPosition));
        }

        if (feedback.Cursor.HasFlag(DragRectCursorAction.Hide))
        {
            _cursor.ShowCursor(false);
        }

        if (feedback.Cursor.HasFlag(DragRectCursorAction.Show))
        {
            _cursor.ShowCursor(true);
        }

        if (feedback.UpdateCursorShape)
        {
            ApplyCursorShape(feedback.CursorHandle);
        }

        if (feedback.Redraw)
        {
            InvalidateVisual();
        }
    }

    /// <summary>コアからの通知を公開イベントへ転送する（判断はしない）。</summary>
    /// <param name="sender">送信元（セッション）。</param>
    /// <param name="notice">通知の種別・座標・スナップショット。</param>
    /// <remarks>
    /// 転送のみを担う（確定/キャンセルの判断・状態の更新はコアが済ませている）。<br/>
    /// 各 UI のイベント引数の型（Avalonia の <see cref="Rect"/> / <see cref="Point"/>）へ境界変換する。
    /// </remarks>
    private void OnSessionNotified(object? sender, DragRectNotice notice)
    {
        switch (notice.Kind)
        {
            case DragRectNoticeKind.SelectionStarted:
                SelectionStarted?.Invoke(this, ToPointArgs(notice));
                break;
            case DragRectNoticeKind.SelectionChanged:
                SelectionChanged?.Invoke(this, ToRectArgs(notice));
                break;
            case DragRectNoticeKind.SelectionCompleted:
                SelectionCompleted?.Invoke(this, ToRectArgs(notice));
                break;
            case DragRectNoticeKind.SelectionCancelled:
                SelectionCancelled?.Invoke(this, EventArgs.Empty);
                break;
            case DragRectNoticeKind.MouseDown:
                MouseDown?.Invoke(this, ToPointArgs(notice));
                break;
            case DragRectNoticeKind.MouseUp:
                MouseUp?.Invoke(this, ToPointArgs(notice));
                break;
            case DragRectNoticeKind.MouseClicked:
                MouseClicked?.Invoke(this, ToPointArgs(notice));
                break;
            case DragRectNoticeKind.MouseDoubleClicked:
                MouseDoubleClicked?.Invoke(this, ToPointArgs(notice));
                break;
            case DragRectNoticeKind.MouseMoved:
                MouseMoved?.Invoke(this, ToPointArgs(notice));
                break;
            case DragRectNoticeKind.LoupePositionMoved:
                LoupePositionMoved?.Invoke(this, ToPointArgs(notice));
                break;
            case DragRectNoticeKind.ConfirmRequested:
                ConfirmRequested?.Invoke(this, ToRectArgs(notice));
                break;
            case DragRectNoticeKind.CancelRequested:
                CancelRequested?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    /// <summary>通知を点座標のイベント引数へ変換する。</summary>
    /// <param name="notice">通知。</param>
    /// <returns>通知座標（DIP）とスナップショットを含むイベント引数。</returns>
    private static DragRectPointEventArgs ToPointArgs(DragRectNotice notice)
        => new(notice.Position.ToAvaloniaPoint(), notice.Snapshot);

    /// <summary>通知を選択矩形のイベント引数へ変換する。</summary>
    /// <param name="notice">通知。</param>
    /// <returns>選択矩形（DIP）とスナップショットを含むイベント引数。</returns>
    private static SelectionRectEventArgs ToRectArgs(DragRectNotice notice)
        => new(notice.Snapshot.SelectionRect.ToAvaloniaRect(), notice.Snapshot);

    /// <summary><see cref="ViewportChanged"/> を発火する。</summary>
    private void RaiseViewportChanged()
        => ViewportChanged?.Invoke(this, new SelectionRectEventArgs(_session.SelectionRect.ToAvaloniaRect(), _session.CreateSnapshot()));

    /// <summary>カーソル形状をハンドル種別に応じて更新する。</summary>
    /// <param name="handle">カーソル形状の基になるハンドル種別。</param>
    /// <remarks>ハンドル上ではリサイズ方向に応じたカーソル、枠内では移動カーソル、それ以外は既定カーソルを表示する。</remarks>
    private void ApplyCursorShape(DragHandle handle)
    {
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

    // ====================================================================
    // イベントオーバーライド（ポインター入力）
    // ====================================================================

    /// <summary>ポインター押下時の処理。左ボタンのみ処理する。</summary>
    /// <param name="e">ポインターイベント。</param>
    /// <remarks>
    /// 左ボタン以外（右クリック・中ボタン等）は無視する。<br/>
    /// 座標はコントロールローカル DIP で取得し、ドメイン座標へ変換してセッションへ通知する（解釈はコアが行う）。<br/>
    /// ダブルクリックの確定/キャンセル判定・nudge の終了・ハンドルのスナップはコアの判断に従う。
    /// </remarks>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        var feedback = _session.Press(ToDomain(e), e.ClickCount);
        e.Handled = true;
        ApplyFeedback(feedback);
    }

    /// <summary>ポインター移動時の処理。</summary>
    /// <param name="e">ポインターイベント。</param>
    /// <remarks>
    /// 座標をドメイン座標へ変換してセッションへ通知する。<br/>
    /// ドラッグの適用・nudge 中のエコー移動の抑制・カーソル形状の決定はコアの判断に従う。
    /// </remarks>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        ApplyFeedback(_session.Move(ToDomain(e)));
    }

    /// <summary>ポインター解放時の処理。</summary>
    /// <param name="e">ポインターイベント。</param>
    /// <remarks>
    /// 座標をドメイン座標へ変換してセッションへ通知する。<br/>
    /// 最小サイズによるキャンセル・確定・クリック検出・nudge の終了はコアの判断に従う。
    /// </remarks>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        ApplyFeedback(_session.Release(ToDomain(e)));
    }

    /// <summary>マウスホイールでズーム・スクロールを操作する。</summary>
    /// <param name="e">ホイールイベント。</param>
    /// <remarks>
    /// 祖先 <see cref="ScrollViewer"/>（<see cref="_attachedScrollViewer"/>）が存在する場合（Crop モード）のみ処理する:<br/>
    /// - Ctrl + ホイール: <see cref="ZoomRequested"/> を発火する（ズームの実処理は上位 AP が実施）。<br/>
    /// - 通常ホイール: 祖先 ScrollViewer をスクロールする（Shift 併用で横スクロール）。<br/>
    /// ScrollViewer が存在しない場合（範囲選択（AreaSelect）等）は何もせず既定の動作に委ねる。<br/>
    /// ホイールはビュー（スクロール・ズーム率）の操作であり、選択矩形の状態を変えないため UI 層が担う。
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

    /// <summary>ポインターイベントの座標をドメイン座標へ変換する。</summary>
    /// <param name="e">ポインターイベント。</param>
    /// <returns>ドメイン座標。</returns>
    /// <remarks>コントロールローカル DIP で取得した座標を、セッションの座標変換ポートで変換する（境界の 1 行変換）。</remarks>
    private EsPoint ToDomain(PointerEventArgs e) => _session.ToDomain(e.GetPosition(this).ToEsPoint());

    /// <summary>ドメイン座標を画面物理座標へ変換する（カーソル移動用）。</summary>
    /// <param name="domainPos">ドメイン座標。</param>
    /// <returns>画面物理座標（UI 非依存のコア型）。</returns>
    private EsPixelPoint ToScreen(EsPoint domainPos)
        => ScreenConverter.LocalDipToScreen(this, _session.ToDip(domainPos).ToAvaloniaPoint()).ToEsPixelPoint();
}

