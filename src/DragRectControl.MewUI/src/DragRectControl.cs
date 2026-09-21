using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Aprillz.MewUI.Skia.Controls;
using Estable.DragRect;
using Estable.MewUI.DragRect.Logging;
using EsUtil.Platform.Core;
using EsUtil.Platform.MewUI;
using EsUtil.Platform.Native;
using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;
using DrawingColor = System.Drawing.Color;

namespace Estable.MewUI.DragRect;

/// <summary>リサイズ可能なドラッグ矩形のコントロール（MewUI 版）。</summary>
/// <remarks>
/// Avalonia 版と同じ UI 非依存の状態管理クラス（<c>DragRectControl.Core</c> の <see cref="DragRectSession"/>）を使い、<br/>
/// 本コントロールは「入力の受付」「効果の適用」「通知の転送」「描画」のみを行う薄い層である。<br/>
/// <br/>
/// <b>座標系</b>:<br/>
/// - ドメイン座標 = 選択対象（画像ピクセル・仮想スクリーンピクセル等）。セッションはこの座標で動作する。<br/>
/// - ローカル DIP = 本コントロールのローカル座標。入力と描画はこの座標で行い、境界でポート（<see cref="ICoordTransform"/>）により変換する。<br/>
/// <br/>
/// <b>操作</b>:<br/>
/// - 左ドラッグ: 新規選択、またはハンドルのリサイズ・移動<br/>
/// - 矢印キー: nudge（1 ドメインピクセル単位の微調整。カーソルを非表示にして十字マーカーを表示する）<br/>
/// - Escape: キャンセル要求 / Enter: 確定要求<br/>
/// <br/>
/// <b>Avalonia 版との違い</b>:<br/>
/// - 描画は中間ビットマップを経由せず <see cref="SkiaCanvasView"/> のキャンバスへ直接行う。<br/>
/// - イベント引数の座標はコア型（<see cref="EsPoint"/> / <see cref="EsRect"/>）で公開する（境界変換が不要）。<br/>
/// - <c>ScrollViewer</c> の自動連携は行わない。ビューポートの変化はホストが <see cref="NotifyViewportChanged"/> で通知する。<br/>
/// - 矢印キーの nudge はフォーカスを必要とする（Avalonia 版の TopLevel 購読によるフォーカス非依存とは異なる）。<br/>
/// - ドラッグ中はマウスキャプチャを行う（コントロール外への移動でもイベントを受け取る）。<br/>
/// - ログ出力は <see cref="Log"/> プロパティで注入する（デフォルトは <see cref="NullLogger"/>。Avalonia 版と同一の書式）。
/// </remarks>
public sealed class DragRectControl : SkiaCanvasView
{
    /// <summary>十字マーカーの線の太さ（DIP）。</summary>
    private const double MarkerThickness = 2.0;

    /// <summary>カーソル座標ラベルのフォントサイズの最小値。</summary>
    private const double CoordLabelFontSizeMin = 6.0;

    /// <summary>カーソル座標ラベルのフォントサイズの最大値。</summary>
    private const double CoordLabelFontSizeMax = 48.0;

    /// <summary>カーソル座標ラベルのフォントサイズの既定値。</summary>
    private const double CoordLabelFontSizeDefault = 12.0;

    /// <summary><see cref="BorderColor"/> プロパティ（選択枠線の色。不透明度はアルファに統合する）。</summary>
    public static readonly MewProperty<DrawingColor> BorderColorProperty =
        MewProperty<DrawingColor>.Register<DragRectControl>(
            nameof(BorderColor), DrawingColor.FromArgb(255, 169, 169, 169), MewPropertyOptions.AffectsRender);

    /// <summary><see cref="BorderThickness"/> プロパティ（選択枠線の太さ・DIP）。</summary>
    public static readonly MewProperty<double> BorderThicknessProperty =
        MewProperty<double>.Register<DragRectControl>(nameof(BorderThickness), 2.0, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="HandleColor"/> プロパティ（ドラッグハンドルの塗り色）。</summary>
    public static readonly MewProperty<DrawingColor> HandleColorProperty =
        MewProperty<DrawingColor>.Register<DragRectControl>(nameof(HandleColor), DrawingColor.White, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="HandleSize"/> プロパティ（ドラッグハンドルの一辺サイズ・DIP）。</summary>
    public static readonly MewProperty<double> HandleSizeProperty =
        MewProperty<double>.Register<DragRectControl>(nameof(HandleSize), 10.0, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="HandleHitRadius"/> プロパティ（ドラッグハンドルのヒットテスト半径・DIP）。</summary>
    public static readonly MewProperty<double> HandleHitRadiusProperty =
        MewProperty<double>.Register<DragRectControl>(nameof(HandleHitRadius), 12.0, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="MinSelectionSize"/> プロパティ（選択矩形の最小サイズ・ドメイン座標）。</summary>
    public static readonly MewProperty<double> MinSelectionSizeProperty =
        MewProperty<double>.Register<DragRectControl>(nameof(MinSelectionSize), 6.0, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="ClampToDomainBounds"/> プロパティ（ドメイン境界外の選択を禁止するか）。</summary>
    /// <remarks>ON へ切り替えた瞬間に既存の選択矩形を境界内へクリップする（変更通知で適用する）。</remarks>
    public static readonly MewProperty<bool> ClampToDomainBoundsProperty =
        MewProperty<bool>.Register<DragRectControl>(
            nameof(ClampToDomainBounds), true, MewPropertyOptions.None,
            changed: static (owner, _, newValue) =>
            {
                owner._session.ClampToDomainBounds = newValue;
                owner.ApplyFeedback(owner._session.ApplyClampToBounds());
            });

    /// <summary><see cref="MarkerColor"/> プロパティ（十字マーカーの色）。</summary>
    public static readonly MewProperty<DrawingColor> MarkerColorProperty =
        MewProperty<DrawingColor>.Register<DragRectControl>(nameof(MarkerColor), DrawingColor.White, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="MarkerLength"/> プロパティ（十字マーカーの全長・DIP）。</summary>
    public static readonly MewProperty<double> MarkerLengthProperty =
        MewProperty<double>.Register<DragRectControl>(nameof(MarkerLength), 16.0, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="ShowCoordLabel"/> プロパティ（カーソル座標ラベルの表示有無）。</summary>
    public static readonly MewProperty<bool> ShowCoordLabelProperty =
        MewProperty<bool>.Register<DragRectControl>(nameof(ShowCoordLabel), false, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="CoordLabelColor"/> プロパティ（カーソル座標ラベルの文字色）。</summary>
    public static readonly MewProperty<DrawingColor> CoordLabelColorProperty =
        MewProperty<DrawingColor>.Register<DragRectControl>(nameof(CoordLabelColor), DrawingColor.White, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="CoordLabelBackgroundColor"/> プロパティ（カーソル座標ラベルの背景色。不透明度はアルファに統合する）。</summary>
    public static readonly MewProperty<DrawingColor> CoordLabelBackgroundColorProperty =
        MewProperty<DrawingColor>.Register<DragRectControl>(
            nameof(CoordLabelBackgroundColor), DrawingColor.FromArgb(153, 0, 0, 0), MewPropertyOptions.AffectsRender);

    /// <summary><see cref="CoordLabelFontSize"/> プロパティ（カーソル座標ラベルのフォントサイズ・6〜48）。</summary>
    public static readonly MewProperty<double> CoordLabelFontSizeProperty =
        MewProperty<double>.Register<DragRectControl>(
            nameof(CoordLabelFontSize), CoordLabelFontSizeDefault, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="RenderScaling"/> プロパティ（DIP → 物理ピクセルのスケーリング係数）。</summary>
    public static readonly MewProperty<double> RenderScalingProperty =
        MewProperty<double>.Register<DragRectControl>(nameof(RenderScaling), 1.0, MewPropertyOptions.AffectsRender);

    /// <summary>選択・ドラッグ・nudge の状態を管理するセッション（入力の解釈と状態遷移の判断を担う）。</summary>
    private readonly DragRectSession _session;

    /// <summary>カーソルサービス（nudge のカーソル非表示・移動）。</summary>
    /// <remarks>既定実装（Win32）の解決は本 UI 層が行う（コアは抽象と <see langword="null"/> のみを扱う）。</remarks>
    private readonly ICursorService _cursor;

    /// <summary>ドラッグ矩形コントロールを既定構成で初期化する。</summary>
    public DragRectControl()
        : this(null)
    {
    }

    /// <summary>カーソルサービスを注入してドラッグ矩形コントロールを初期化する。</summary>
    /// <param name="cursorService">カーソルサービス。null の場合は Windows 既定実装を使用する。</param>
    /// <remarks>
    /// 静止画として扱い連続再描画を無効化する（選択の変更時のみ再描画する）。<br/>
    /// フォーカスを失った時点で nudge を終了する（カーソルの非表示残留を防ぐ）。<br/>
    /// 矢印キーの nudge はフォーカスを必要とする（Avalonia 版の TopLevel 購読によるフォーカス非依存とは異なる）。<br/>
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

        ContinuousAnimation = false;
        PaintSurface += OnPaintSurface;

        // フォーカスを失ったら nudge を終了する（カーソル非表示の残留防止）
        this.OnLostFocus(EndNudge);
    }

    /// <summary>ドラッグの始点が決定されたときに発火する（新規選択のドラッグ開始。座標はドメイン座標）。</summary>
    public event EventHandler<DragRectPointEventArgs>? SelectionStarted;

    /// <summary>選択矩形が変更されたときに発火する。</summary>
    public event EventHandler<SelectionRectEventArgs>? SelectionChanged;

    /// <summary>ドラッグ後の終点が決定され、選択矩形が確定したときに発火する。</summary>
    public event EventHandler<SelectionRectEventArgs>? SelectionCompleted;

    /// <summary>領域がキャンセルされたときに発火する（枠外ダブルクリック・小さすぎる矩形等）。</summary>
    public event EventHandler? SelectionCancelled;

    /// <summary>ポインター押下（左ボタン）時に発火する（ドメイン座標）。</summary>
    /// <remarks>MewUI の基底 <c>UIElement.MouseDown</c> と衝突するため、本コントロールでは <c>DragMouseDown</c> の名で公開する。</remarks>
    public event EventHandler<DragRectPointEventArgs>? DragMouseDown;

    /// <summary>ポインター解放（左ボタン）時に発火する（ドメイン座標）。</summary>
    /// <remarks>MewUI の基底 <c>UIElement.MouseUp</c> と衝突するため、本コントロールでは <c>DragMouseUp</c> の名で公開する。</remarks>
    public event EventHandler<DragRectPointEventArgs>? DragMouseUp;

    /// <summary>クリック（ドラッグ移動なしの押下→解放）時に発火する（ドメイン座標）。</summary>
    public event EventHandler<DragRectPointEventArgs>? MouseClicked;

    /// <summary>ポインターのダブルクリックが検出されたときに発火する（確定/キャンセルの判定は本コントロールが行う）。</summary>
    public event EventHandler<DragRectPointEventArgs>? MouseDoubleClicked;

    /// <summary>マウスポインタの位置を通知する（ドメイン座標。nudge の有無に関わらず移動のたびに発火する）。</summary>
    /// <remarks>要素検出・ルーペ追従等の汎用用途に使用する。</remarks>
    public event EventHandler<DragRectPointEventArgs>? MouseMoved;

    /// <summary>ルーペが表示する中心座標を通知する（ドメイン座標）。</summary>
    /// <remarks>通常追従ではポインタ位置、ドラッグ・nudge 中は動いている側のハンドル位置を通知する。</remarks>
    public event EventHandler<DragRectPointEventArgs>? LoupePositionMoved;

    /// <summary>ビューポート（ズーム・スクロール）が変化したときに発火する。</summary>
    /// <remarks>
    /// 選択矩形（ドメイン座標）はズーム・スクロールで不変だが、上位（暗転マスク等）はビューポート座標で<br/>
    /// 再描画する必要がある。ホストが <see cref="NotifyViewportChanged"/> を呼ぶと、現在の選択矩形 + Snapshot を再通知する。
    /// </remarks>
    public event EventHandler<SelectionRectEventArgs>? ViewportChanged;

    /// <summary>Ctrl+ホイールによるズーム要求時に発火する（true = ズームイン / false = ズームアウト）。</summary>
    /// <remarks>ズームの実処理（画像サイズ変更・ズーム率管理）は上位 AP が実施する（本コントロールは要求のみ通知する）。</remarks>
    public event EventHandler<bool>? ZoomRequested;

    /// <summary>Enter キーによる確定要求時に発火する（選択確定後のみ。ドラッグ中は発火しない）。</summary>
    public event EventHandler<SelectionRectEventArgs>? ConfirmRequested;

    /// <summary>Escape キーによるキャンセル要求時に発火する（選択の破棄は本コントロールが実施済み）。</summary>
    public event EventHandler? CancelRequested;

    /// <summary>選択枠線の色を取得または設定する。</summary>
    /// <value>枠線の色（アルファで不透明度を表す）。</value>
    public DrawingColor BorderColor
    {
        get => GetValue(BorderColorProperty);
        set => SetValue(BorderColorProperty, value);
    }

    /// <summary>選択枠線の太さを取得または設定する。</summary>
    /// <value>枠線の太さ（DIP）。</value>
    public double BorderThickness
    {
        get => GetValue(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    /// <summary>ドラッグハンドルの塗り色を取得または設定する。</summary>
    /// <value>ハンドルの塗り色（アルファで不透明度を表す）。</value>
    public DrawingColor HandleColor
    {
        get => GetValue(HandleColorProperty);
        set => SetValue(HandleColorProperty, value);
    }

    /// <summary>ドラッグハンドルの一辺サイズを取得または設定する。</summary>
    /// <value>ハンドルの一辺サイズ（DIP）。</value>
    public double HandleSize
    {
        get => GetValue(HandleSizeProperty);
        set => SetValue(HandleSizeProperty, value);
    }

    /// <summary>ドラッグハンドルのヒットテスト半径を取得または設定する。</summary>
    /// <value>ヒットテスト半径（DIP）。ドメイン座標への変換はセッションが行う。</value>
    public double HandleHitRadius
    {
        get => GetValue(HandleHitRadiusProperty);
        set
        {
            SetValue(HandleHitRadiusProperty, value);
            _session.HandleHitRadius = value;
        }
    }

    /// <summary>選択矩形の最小サイズを取得または設定する。</summary>
    /// <value>最小サイズ（ドメイン座標）。これ未満の矩形はキャンセル扱いになる（判断はセッションが行う）。</value>
    public double MinSelectionSize
    {
        get => GetValue(MinSelectionSizeProperty);
        set
        {
            SetValue(MinSelectionSizeProperty, value);
            _session.MinSelectionSize = value;
        }
    }

    /// <summary>ドメイン境界外の選択を禁止するかどうかを取得または設定する。</summary>
    /// <value>true（既定）の場合は選択をドメイン境界内に制限する。false の場合は境界外も選択できる。</value>
    /// <remarks>クリップ範囲は <see cref="SetCoordinateTransform"/> のポートの Bounds で指定する（変更時は変更前後をログ出力する）。</remarks>
    public bool ClampToDomainBounds
    {
        get => GetValue(ClampToDomainBoundsProperty);
        set
        {
            var oldValue = GetValue(ClampToDomainBoundsProperty);
            SetValue(ClampToDomainBoundsProperty, value);
            _session.ClampToDomainBounds = value;

            // 実際に変化した場合のみログ出力する（同じ値の再設定でログが流れないようにする）
            if (oldValue != value)
            {
                Log.LogSettingChanged(nameof(ClampToDomainBounds), oldValue.ToString(), value.ToString());
            }
        }
    }

    /// <summary>十字マーカーの色を取得または設定する。</summary>
    /// <value>十字マーカーの色（アルファで不透明度を表す）。</value>
    public DrawingColor MarkerColor
    {
        get => GetValue(MarkerColorProperty);
        set => SetValue(MarkerColorProperty, value);
    }

    /// <summary>十字マーカーの全長を取得または設定する。</summary>
    /// <value>十字マーカーの全長（DIP）。中心から両端までの半幅はこの 1/2 になる。</value>
    public double MarkerLength
    {
        get => GetValue(MarkerLengthProperty);
        set => SetValue(MarkerLengthProperty, value);
    }

    /// <summary>カーソル座標ラベルを表示するかどうかを取得または設定する。</summary>
    /// <value>true の場合はマウス位置のドメイン座標をカーソルの右側に表示する。</value>
    public bool ShowCoordLabel
    {
        get => GetValue(ShowCoordLabelProperty);
        set => SetValue(ShowCoordLabelProperty, value);
    }

    /// <summary>カーソル座標ラベルの文字色を取得または設定する。</summary>
    /// <value>ラベルの文字色（アルファで不透明度を表す）。</value>
    public DrawingColor CoordLabelColor
    {
        get => GetValue(CoordLabelColorProperty);
        set => SetValue(CoordLabelColorProperty, value);
    }

    /// <summary>カーソル座標ラベルの背景色を取得または設定する。</summary>
    /// <value>ラベルの背景色（アルファで不透明度を表す。既定は黒 60%）。</value>
    public DrawingColor CoordLabelBackgroundColor
    {
        get => GetValue(CoordLabelBackgroundColorProperty);
        set => SetValue(CoordLabelBackgroundColorProperty, value);
    }

    /// <summary>カーソル座標ラベルのフォントサイズを取得または設定する。</summary>
    /// <value>フォントサイズ（DIP）。6〜48 の範囲へ補正される。</value>
    public double CoordLabelFontSize
    {
        get => GetValue(CoordLabelFontSizeProperty);
        set => SetValue(CoordLabelFontSizeProperty, CoerceCoordLabelFontSize(value));
    }

    /// <summary>表示 DIP を物理ピクセルへ変換するスケーリング係数を取得または設定する。</summary>
    /// <value>スケーリング係数。DPI 変更時に自動更新される。</value>
    public double RenderScaling
    {
        get => GetValue(RenderScalingProperty);
        set => SetValue(RenderScalingProperty, value);
    }

    /// <summary>現在の選択状態のスナップショットを取得する（ポーリング不要の完全状態）。</summary>
    /// <value>State / SelectionRect / ActiveHandle / MousePosition / IsMouseDown / IsDragging / NudgeActive / HasSelection を含むスナップショット。</value>
    public DragRectSnapshot CurrentSnapshot => _session.CreateSnapshot();

    /// <summary>操作ログの出力先を取得または設定する。</summary>
    /// <value>ロガー。null を設定した場合は <see cref="NullLogger"/> へフォールバックする（出力なし）。</value>
    /// <remarks>
    /// C#14 の <c>field</c> キーワードでバッキング フィールドを暗黙定義する。<br/>
    /// getter / setter とも null を <see cref="NullLogger"/> へ安全に変換する（呼び出し側で null 判定しない）。<br/>
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

    /// <summary>ドメイン ↔ ローカル DIP の座標変換ポートを設定する。</summary>
    /// <param name="port">座標変換ポート。null の場合は等価変換（1 ドメインピクセル = 1 DIP・無制限）。</param>
    /// <remarks>
    /// ホスト（上位 AP）がモードに応じた変換ポートを設定する（範囲選択: 仮想スクリーンピクセル / 切出し: 元画像ピクセル）。<br/>
    /// 変換関数は呼び出し毎に評価されるため、ズーム・スクロール等の動的変化を反映できる。<br/>
    /// 設定すると描画（選択矩形・ハンドル・十字線）へ即時反映する。
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
    public void SetSelection(EsRect rect) => ApplyFeedback(_session.SetSelection(rect));

    /// <summary>選択状態と nudge 状態を初期化する。</summary>
    /// <remarks>nudge 中だった場合はカーソルを再表示する（非表示残留の防止。状態の初期化はセッションが担う）。</remarks>
    public void ResetSelection() => ApplyFeedback(_session.Reset());

    /// <summary>キャンセル要求を処理する（Escape キーと同じ条件）。</summary>
    /// <remarks>
    /// ドラッグ中 / 選択ありの場合は選択を破棄した後に <see cref="CancelRequested"/> を発火する。<br/>
    /// モード差分（キャンセル後の後始末）は上位が本イベントを購読して行う。
    /// </remarks>
    public void RequestCancel() => ApplyFeedback(_session.RequestCancel());

    /// <summary>確定要求を処理する（Enter キーと同じ条件）。</summary>
    /// <remarks>
    /// ドラッグ中は無視し、選択確定後のみ <see cref="ConfirmRequested"/> を発火する。<br/>
    /// 確定後に選択状態をクリアする（確定後の再ドラッグ・再確定を防ぐ）。
    /// </remarks>
    public void RequestConfirm() => ApplyFeedback(_session.RequestConfirm());

    /// <summary>ビューポートの変化を通知する（<see cref="ViewportChanged"/> を発火する）。</summary>
    /// <remarks>
    /// 本コントロールは <c>ScrollViewer</c> の自動連携を行わないため、ホスト（上位 AP）が<br/>
    /// ズーム・スクロール・画像サイズ変更の検知時に呼ぶ（暗転マスク等の再描画トリガー）。
    /// </remarks>
    public void NotifyViewportChanged()
    {
        InvalidateVisual();
        RaiseViewportChanged();
    }

    /// <summary>要求サイズを返す（親の残余を埋める）。</summary>
    /// <param name="availableSize">親から提示された利用可能サイズ（使用しない）。</param>
    /// <returns>常に <c>Size.Empty</c>。</returns>
    /// <remarks>選択オーバーレイは下の画像と同じ領域を占めるため、自身の要求サイズを持たない。</remarks>
    protected override Size MeasureContent(Size availableSize) => Size.Empty;

    /// <summary>DPI の変更時に表示スケーリング係数を追従させる。</summary>
    /// <param name="oldDpi">変更前の DPI。</param>
    /// <param name="newDpi">変更後の DPI。</param>
    /// <remarks>モニター間の移動で DPI が変わっても、枠線・ハンドル・マーカーの画面上の大きさを保つ。</remarks>
    protected override void OnDpiChanged(uint oldDpi, uint newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        RenderScaling = newDpi / 96.0;
    }

    /// <summary>左ボタン押下時にドラッグを開始する。</summary>
    /// <param name="e">マウスの情報。</param>
    /// <remarks>
    /// 押下位置をドメイン座標へ変換してセッションへ通知する（ダブルクリックの確定/キャンセル・nudge の終了・<br/>
    /// ハンドルのスナップはコアの判断に従う）。<br/>
    /// ドラッグ中はポインターがコントロール外へ出ても移動イベントを受け取れるようマウスキャプチャを取得する。
    /// </remarks>
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (!e.LeftButton)
        {
            return;
        }

        var feedback = _session.Press(ToDomain(e.GetPosition(this)), e.ClickCount);
        if (feedback.CaptureMouse)
        {
            CaptureMouse(true);
        }

        e.Handled = true;
        ApplyFeedback(feedback);
    }

    /// <summary>ドラッグ中のマウス移動を処理する。</summary>
    /// <param name="e">マウスの情報。</param>
    /// <remarks>
    /// 押下位置をドメイン座標へ変換してセッションへ通知する。<br/>
    /// ドラッグの適用・nudge 中のエコー移動の抑制・カーソル形状の決定はコアの判断に従う。
    /// </remarks>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        ApplyFeedback(_session.Move(ToDomain(e.GetPosition(this))));
    }

    /// <summary>左ボタン解放時にドラッグを終了する。</summary>
    /// <param name="e">マウスの情報。</param>
    /// <remarks>
    /// 押下位置をドメイン座標へ変換してセッションへ通知し、マウスキャプチャを解放する。<br/>
    /// 最小サイズによるキャンセル・確定・クリック検出・nudge の終了はコアの判断に従う。
    /// </remarks>
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        CaptureMouse(false);
        ApplyFeedback(_session.Release(ToDomain(e.GetPosition(this))));
        e.Handled = true;
    }

    /// <summary>Ctrl+ホイールでズームを要求する。</summary>
    /// <param name="e">ホイールの情報。</param>
    /// <remarks>
    /// Ctrl+ホイールは <see cref="ZoomRequested"/>（ズームの実処理は上位 AP）を発火する。<br/>
    /// それ以外のホイールは処理しない（スクロールの実処理は上位 AP が担う）。<br/>
    /// ホイールはビューの操作であり選択矩形の状態を変えないため UI 層が担う。
    /// </remarks>
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);

        if (!e.ControlKey)
        {
            return;
        }

        Log.LogZoomRequested(e.Delta.Y > 0);
        ZoomRequested?.Invoke(this, e.Delta.Y > 0);
        e.Handled = true;
    }

    /// <summary>キー入力を処理する（矢印キーの nudge・Escape のキャンセル・Enter の確定）。</summary>
    /// <param name="e">キーの情報。</param>
    /// <remarks>
    /// キーをコア型（<see cref="EsKey"/>）へ変換してセッションへ通知し、返された効果を適用するだけである。<br/>
    /// 矢印キー nudge・Escape・Enter の判断（nudge の許可条件を含む）はコアが行う。<br/>
    /// 本コントロールにフォーカスがある場合のみ呼ばれる（Avalonia 版のフォーカス非依存 nudge とは異なる）。
    /// </remarks>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        var feedback = _session.KeyDown(e.Key.ToEsKey(), CurrentCursorDomainPosition());
        if (!feedback.Handled)
        {
            return;
        }

        e.Handled = true;
        ApplyFeedback(feedback);
    }

    /// <summary>現在のカーソル位置（ドメイン座標）を取得する。</summary>
    /// <returns>カーソル位置（ドメイン座標）。ウィンドウを特定できない場合は null。</returns>
    /// <remarks>
    /// nudge の基点精度を上げるために記録する（マウス移動が未発生でも現在位置から微調整を開始でき、原点へ飛ばない）。<br/>
    /// 画面物理座標 → ウィンドウのクライアント座標 → ドメイン座標の順に変換する（境界の 1 行変換）。
    /// </remarks>
    private EsPoint? CurrentCursorDomainPosition()
    {
        if (FindVisualRoot() is not Window window)
        {
            return null;
        }

        var screen = _cursor.GetCursorScreenPoint();
        var local = window.ScreenToClient(new Point(screen.X, screen.Y));
        return _session.ToDomain(new EsPoint(local.X, local.Y));
    }

    /// <summary>nudge を終了し、マウスポインターを再表示する。</summary>
    /// <remarks>フォーカス喪失時に呼び出し、nudge 中の非表示ポインターを復元する。</remarks>
    private void EndNudge() => ApplyFeedback(_session.EndNudge());

    /// <summary>ローカル DIP 座標をドメイン座標へ変換する。</summary>
    /// <param name="localDip">ローカル DIP 座標。</param>
    /// <returns>ドメイン座標。</returns>
    private EsPoint ToDomain(Point localDip) => _session.ToDomain(new EsPoint(localDip.X, localDip.Y));

    /// <summary>ドメイン座標を画面物理座標へ変換する。</summary>
    /// <param name="domainPos">ドメイン座標。</param>
    /// <returns>画面物理座標。ウィンドウを特定できない場合は default。</returns>
    /// <remarks>カーソルの移動（nudge）に使用する。変換できない場合は原点へカーソルが飛ぶため、呼び出し側は nudge 中のウィンドウ表示を前提とする。</remarks>
    private EsPixelPoint ToScreen(EsPoint domainPos)
    {
        if (FindVisualRoot() is not Window window)
        {
            return default;
        }

        var localDip = _session.ToDip(domainPos);
        var screen = window.ClientToScreen(new Point(localDip.X, localDip.Y));
        return new EsPixelPoint((int)Math.Round(screen.X), (int)Math.Round(screen.Y));
    }

    /// <summary>選択矩形をローカル DIP 矩形へ変換する。</summary>
    /// <param name="domainRect">選択矩形（ドメイン座標）。</param>
    /// <returns>ローカル DIP 矩形。</returns>
    /// <remarks>
    /// 選択領域は閉区間（始点・終点とも含む）を意図する仕様のため、終点（Right / Bottom）に +1 してから変換する<br/>
    /// （終点のピクセルも枠線・ハンドルに含める。キャプチャの閉区間 +1 と一致）。
    /// </remarks>
    private EsRect ToDipRect(EsRect domainRect)
    {
        var topLeft = _session.ToDip(domainRect.TopLeft);
        var bottomRight = _session.ToDip(new EsPoint(domainRect.Right + 1, domainRect.Bottom + 1));
        return new EsRect(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);
    }

    /// <summary>マウスキャプチャを取得または解放する。</summary>
    /// <param name="capture">true = 取得 / false = 解放。</param>
    /// <remarks>ドラッグ中にポインタがコントロール外へ出ても移動イベントを受け取れるようにする。</remarks>
    private void CaptureMouse(bool capture)
    {
        if (FindVisualRoot() is not Window window)
        {
            return;
        }

        if (capture)
        {
            window.CaptureMouse(this);
        }
        else
        {
            window.ReleaseMouseCapture();
        }
    }

    /// <summary>コア（状態管理クラス）が返す効果を画面へ適用する。</summary>
    /// <param name="feedback">適用する効果（再描画・フォーカス・マウスキャプチャ・カーソル操作）。</param>
    /// <remarks>
    /// カーソル操作は「移動 → 非表示 → 再表示」の順で適用する（nudge の終了では移動してから再表示する必要がある）。<br/>
    /// マウスキャプチャの解放はドラッグ終了時に本コントロールが行う（効果には含まれない）。
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
    /// MewUI では基底クラスの <c>MouseDown</c> / <c>MouseUp</c> と衝突するため、押下/解放は <c>DragMouseDown</c> / <c>DragMouseUp</c> へ転送する。
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
                DragMouseDown?.Invoke(this, ToPointArgs(notice));
                break;
            case DragRectNoticeKind.MouseUp:
                DragMouseUp?.Invoke(this, ToPointArgs(notice));
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
    /// <returns>通知座標（ドメイン座標）とスナップショットを含むイベント引数。</returns>
    private static DragRectPointEventArgs ToPointArgs(DragRectNotice notice) => new(notice.Position, notice.Snapshot);

    /// <summary>通知を選択矩形のイベント引数へ変換する。</summary>
    /// <param name="notice">通知。</param>
    /// <returns>選択矩形（ドメイン座標）とスナップショットを含むイベント引数。</returns>
    private static SelectionRectEventArgs ToRectArgs(DragRectNotice notice) => new(notice.Snapshot.SelectionRect, notice.Snapshot);

    /// <summary>カーソル形状をハンドル種別に応じて更新する。</summary>
    /// <param name="handle">カーソル形状の基になるハンドル種別。</param>
    /// <remarks>ハンドル上ではリサイズ方向に応じたカーソル、枠内では移動カーソル、それ以外は既定カーソルを表示する。</remarks>
    private void ApplyCursorShape(DragHandle handle)
    {
        Cursor = handle switch
        {
            DragHandle.TopLeft or DragHandle.BottomRight => CursorType.SizeNWSE,
            DragHandle.TopRight or DragHandle.BottomLeft => CursorType.SizeNESW,
            DragHandle.TopCenter or DragHandle.BottomCenter => CursorType.SizeNS,
            DragHandle.MiddleLeft or DragHandle.MiddleRight => CursorType.SizeWE,
            DragHandle.Move => CursorType.SizeAll,
            _ => null,
        };
    }

    /// <summary>カーソル座標ラベルのフォントサイズを安全域へ補正する。</summary>
    /// <param name="value">補正する値。</param>
    /// <returns>6〜48 に補正した値。非数（NaN / ∞）の場合は既定値。</returns>
    private static double CoerceCoordLabelFontSize(double value)
        => double.IsFinite(value) ? Math.Clamp(value, CoordLabelFontSizeMin, CoordLabelFontSizeMax) : CoordLabelFontSizeDefault;

    /// <summary>選択枠線・ハンドル・十字マーカー・座標ラベルを描画する。</summary>
    /// <param name="canvas">描画先のキャンバス（物理ピクセル）。</param>
    /// <param name="info">描画先の情報（幅・高さ）。</param>
    /// <remarks>
    /// セッションの状態（選択矩形・nudge・マウス位置）を読み、DIP へ変換して描画層へ渡すだけである。<br/>
    /// キャンバスを DPI スケーリングしてから DIP 座標で描画する（コアの計算と座標系を一致させる）。<br/>
    /// 背景は塗らない（透明のままにして下の画像を透かす）。
    /// </remarks>
    private void OnPaintSurface(SKCanvas canvas, SKImageInfo info)
    {
        var scaling = RenderScaling > 0 ? RenderScaling : 1.0;

        canvas.Save();
        canvas.Scale((float)scaling);
        try
        {
            if (_session.HasSelection)
            {
                var dipRect = ToDipRect(_session.SelectionRect);
                DragRectSkiaRenderer.DrawBorder(canvas, dipRect, BorderColor, BorderThickness);
                DragRectSkiaRenderer.DrawHandles(canvas, dipRect, HandleSize, HandleColor, BorderColor);
            }

            if (_session.NudgeActive && _session.CrosshairVisible)
            {
                var markerDip = _session.ToDip(_session.MarkerPosition);
                DragRectSkiaRenderer.DrawStartMarker(canvas, markerDip, MarkerColor, MarkerThickness, MarkerLength / 2);
            }

            if (ShowCoordLabel)
            {
                var cursorDip = _session.ToDip(_session.LastMousePosition);
                var clientSize = new EsSize(info.Width / scaling, info.Height / scaling);
                DragRectSkiaRenderer.DrawCoordLabel(
                    canvas, cursorDip, _session.LastMousePosition, clientSize,
                    CoordLabelColor, CoordLabelBackgroundColor, CoordLabelFontSize);
            }
        }
        finally
        {
            canvas.Restore();
        }
    }

    /// <summary><see cref="ViewportChanged"/> を発火する。</summary>
    private void RaiseViewportChanged()
        => ViewportChanged?.Invoke(this, new SelectionRectEventArgs(_session.SelectionRect, _session.CreateSnapshot()));
}
