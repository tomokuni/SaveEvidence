using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Aprillz.MewUI.Skia.Controls;
using Estable.DragRect;
using Estable.DragRect.Selection;
using Estable.MewUI.DragRect.Logging;
using Estable.Platform;
using Estable.Platform.Coordinate;
using Estable.Platform.Geometry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;
using DrawingColor = System.Drawing.Color;

namespace Estable.MewUI.DragRect;

/// <summary>リサイズ可能なドラッグ矩形のコントロール（MewUI 版）。</summary>
/// <remarks>
/// Avalonia 版 <c>DragRectControl</c> と同じ UI 非依存コア（<c>DragRectControl.Core</c>）を使い、<br/>
/// 描画層・入力処理・カーソル制御のみを MewUI で実装する。<br/>
/// <br/>
/// <b>座標系</b>:<br/>
/// - ドメイン座標 = 選択対象（画像ピクセル・仮想スクリーンピクセル等）。コアの状態機械はこの座標で動作する。<br/>
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
            changed: static (owner, _, _) => owner.ApplyClampToBounds());

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

    /// <summary>選択・ドラッグ・nudge の状態（コアの状態機械）。</summary>
    private readonly DragRectState _state;

    /// <summary>カーソルサービス（nudge のカーソル非表示・移動）。</summary>
    /// <remarks>既定実装（Win32）の解決は本 UI 層が行う（コアは抽象と <see langword="null"/> のみを扱う）。</remarks>
    private readonly ICursorService _cursor;

    /// <summary>座標変換ポート（Forward = ローカル DIP→ドメイン / Inverse = ドメイン→ローカル DIP / Bounds = ドメイン境界）。</summary>
    private ICoordTransform _coordinatePort = CoordTransform.Identity;

    /// <summary>マウスダウンからアップまでの間にドラッグ移動が発生したか（Click 判定に使用する）。</summary>
    private bool _dragPerformed;

    /// <summary>最後のマウスダウン位置（ドメイン座標。Click 通知用）。</summary>
    private EsPoint _lastPressPos;

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
    /// 矢印キーの nudge はフォーカスを必要とする（Avalonia 版の TopLevel 購読によるフォーカス非依存とは異なる）。
    /// </remarks>
    public DragRectControl(ICursorService? cursorService)
    {
        Focusable = true;
        _cursor = cursorService ?? PlatformFactory.CreateCursorService();
        _state = new DragRectState(_cursor) { Port = _coordinatePort };

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
    /// <value>ヒットテスト半径（DIP）。ドメイン座標への変換は内部で行う。</value>
    public double HandleHitRadius
    {
        get => GetValue(HandleHitRadiusProperty);
        set => SetValue(HandleHitRadiusProperty, value);
    }

    /// <summary>選択矩形の最小サイズを取得または設定する。</summary>
    /// <value>最小サイズ（ドメイン座標）。これ未満の矩形はキャンセル扱いになる。</value>
    public double MinSelectionSize
    {
        get => GetValue(MinSelectionSizeProperty);
        set => SetValue(MinSelectionSizeProperty, value);
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
    public DragRectSnapshot CurrentSnapshot => _state.CreateSnapshot();

    /// <summary>操作ログの出力先を取得または設定する。</summary>
    /// <value>ロガー。null を設定した場合は <see cref="NullLogger"/> へフォールバックする（出力なし）。</value>
    /// <remarks>
    /// C#14 の <c>field</c> キーワードでバッキング フィールドを暗黙定義する。<br/>
    /// getter / setter とも null を <see cref="NullLogger"/> へ安全に変換する（呼び出し側で null 判定しない）。
    /// </remarks>
    public ILogger Log
    {
        get => field ?? NullLogger.Instance;
        set => field = value ?? NullLogger.Instance;
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
        _coordinatePort = port ?? CoordTransform.Identity;
        _state.Port = _coordinatePort;
        InvalidateVisual();
    }

    /// <summary>選択矩形を外部状態から復元する。</summary>
    /// <param name="rect">選択矩形（ドメイン座標）。</param>
    /// <remarks>
    /// <see cref="ClampToDomainBounds"/> が有効な場合は、はみ出す矩形を境界内へクリップしてから設定する。<br/>
    /// 設定後は <see cref="SelectionChanged"/> を発火し、再描画を要求する。
    /// </remarks>
    public void SetSelection(EsRect rect)
    {
        var selectionRect = ClampToDomainBounds ? _state.ClipRectToBounds(rect) : rect;
        _state.SetSelection(selectionRect);
        RaiseSelectionChanged(selectionRect);
        InvalidateVisual();
    }

    /// <summary>選択状態と nudge 状態を初期化する。</summary>
    /// <remarks>nudge 中だった場合はカーソルを再表示する（非表示残留の防止）。</remarks>
    public void ResetSelection()
    {
        _state.Reset();
        InvalidateVisual();
    }

    /// <summary>キャンセル要求を処理する（Escape キーと同じ条件）。</summary>
    /// <remarks>
    /// ドラッグ中 / 選択ありの場合は選択を破棄した後に <see cref="CancelRequested"/> を発火する。<br/>
    /// モード差分（キャンセル後の後始末）は上位が本イベントを購読して行う。
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

    /// <summary>確定要求を処理する（Enter キーと同じ条件）。</summary>
    /// <remarks>
    /// ドラッグ中は無視し、選択確定後のみ <see cref="ConfirmRequested"/> を発火する。<br/>
    /// 確定後に選択状態をクリアする（確定後の再ドラッグ・再確定を防ぐ）。
    /// </remarks>
    public void RequestConfirm()
    {
        if (_state.IsDragging || !_state.HasSelection)
        {
            return;
        }

        Log.LogConfirmRequested(FormatRect(_state.SelectionRect));
        ConfirmRequested?.Invoke(this, new SelectionRectEventArgs(_state.SelectionRect, _state.CreateSnapshot()));
        ResetSelection();
    }

    /// <summary>ログ用に選択矩形を整形する。</summary>
    /// <param name="rect">選択矩形（ドメイン座標）。</param>
    /// <returns><c>(X, Y, Width, Height)</c> 形式の文字列。</returns>
    /// <remarks>Avalonia 版の <c>Rect.ToString()</c> と同じ表記に揃える（ログの突き合わせを可能にする）。</remarks>
    private static string FormatRect(EsRect rect) => $"({rect.X}, {rect.Y}, {rect.Width}, {rect.Height})";

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
    /// 1. nudge 中なら nudge を終了し、押下位置を nudge 後の始点に置き換える（ずれの防止）。<br/>
    /// 2. ダブルクリックは矩形内なら確定、矩形外ならキャンセルを要求する。<br/>
    /// 3. ハンドルにヒットした場合はリサイズ / 移動、それ以外は新規選択を開始する。
    /// </remarks>
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (!e.LeftButton)
        {
            return;
        }

        _state.IsMouseDown = true;
        var domainPos = ToDomain(e.GetPosition(this));
        Log.LogPositionBeforeDown(domainPos.X, domainPos.Y);
        Log.LogMouseDown(domainPos.X, domainPos.Y, e.ClickCount, _state.HasSelection);

        // nudge 中ならリセット（カーソル再表示・基点クリア）してから押下処理する
        if (_state.NudgeActive)
        {
            domainPos = _state.StartPoint;
            EndNudge();
            RaiseLoupePositionMoved(domainPos);
        }

        _lastPressPos = domainPos;
        _dragPerformed = false;
        RaiseMouseDown(domainPos);

        if (e.ClickCount >= 2)
        {
            var inside = _state.HasSelection && _state.SelectionRect.Contains(domainPos);
            RaiseMouseDoubleClicked(domainPos);
            Log.LogDoubleClick(domainPos.X, domainPos.Y, inside);

            if (inside)
            {
                Log.LogConfirmRequested(FormatRect(_state.SelectionRect));
                ConfirmRequested?.Invoke(this, new SelectionRectEventArgs(_state.SelectionRect, _state.CreateSnapshot(domainPos)));
                ResetSelection();
            }
            else
            {
                Log.LogCancelRequested();
                CancelRequested?.Invoke(this, EventArgs.Empty);
            }

            e.Handled = true;
            return;
        }

        var handle = _state.HitTest(domainPos, ToDomainRadius());
        Log.LogDragStart(handle.ToString(), domainPos.X, domainPos.Y);
        if (handle != DragHandle.None)
        {
            _state.StartDrag(handle, domainPos);

            // リサイズハンドルではポインタをハンドル点へスナップし、ルーペ中心もハンドル点へ揃える
            // （ポインタ・矩形の角・ルーペを一体にする）。移動（Move）は掴んだ位置を維持する。
            if (handle != DragHandle.Move)
            {
                var handlePos = SelectionGeometry.GetHandlePosition(_state.SelectionRect, handle);
                _state.DragStartPoint = handlePos;
                _state.LastMousePosition = handlePos;
                _cursor.SetCursorScreenPoint(ToScreen(handlePos));
                RaiseLoupePositionMoved(handlePos);
            }
        }
        else
        {
            _state.StartNewSelection(domainPos);
            RaiseSelectionStarted(domainPos);
        }

        CaptureMouse(true);
        Focus();
        e.Handled = true;
        InvalidateVisual();
    }

    /// <summary>ドラッグ中のマウス移動を処理する。</summary>
    /// <param name="e">マウスの情報。</param>
    /// <remarks>
    /// nudge 終了直後のエコー移動は矩形を上書きしない（消費したら抑制を解除する）。<br/>
    /// ドラッグ中はハンドル位置（リサイズ中のハンドル点・移動中のマウス位置・新規選択の終点）を<br/>
    /// ルーペ中心として通知する（クリップや最小サイズ補正でマウス位置と角がずれる場合も実位置を追従できる）。
    /// </remarks>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var domainPos = ToDomain(e.GetPosition(this));
        var hitRadius = ToDomainRadius();

        if (_state.SuppressEchoAt is { } echoPos && SelectionGeometry.IsNear(domainPos, echoPos, hitRadius))
        {
            _state.SuppressEchoAt = null;
            UpdateCursor(domainPos, hitRadius);
            return;
        }

        RaiseMouseMoved(domainPos);
        UpdateCursor(domainPos, hitRadius);

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
            RaiseLoupePositionMoved(_state.GetMarkerPosition());
        }
        else
        {
            RaiseLoupePositionMoved(domainPos);
        }

        InvalidateVisual();
    }

    /// <summary>左ボタン解放時にドラッグを終了する。</summary>
    /// <param name="e">マウスの情報。</param>
    /// <remarks>
    /// nudge 位置はドラッグ終了処理の前に計算する（終了処理がドラッグ状態を解除するため、<br/>
    /// 後からマーカー位置を求めると固定コーナーを返してしまう）。
    /// </remarks>
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        _state.IsMouseDown = false;
        CaptureMouse(false);

        var domainPos = ToDomain(e.GetPosition(this));
        RaiseMouseUp(domainPos);
        Log.LogMouseUp(domainPos.X, domainPos.Y, _state.IsDragging, _state.HasSelection);
        Log.LogPositionAfterUp(domainPos.X, domainPos.Y);

        var wasDragging = _state.IsDragging;
        var wasNudging = _state.NudgeActive;
        var nudgePos = wasNudging ? _state.GetMarkerPosition() : default;

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

        // Click 検出: ドラッグ移動なしの押下→解放（シングルクリック）
        if (wasDragging && !_dragPerformed)
        {
            Log.LogClick(_lastPressPos.X, _lastPressPos.Y);
            RaiseMouseClicked(_lastPressPos);
        }

        if (wasNudging)
        {
            // カーソルは nudge 位置へ戻してから再表示する（nudge から解放までのマウス移動はキャンセルする）
            _cursor.SetCursorScreenPoint(ToScreen(nudgePos));
            EndNudge();
            RaiseLoupePositionMoved(nudgePos);
            _state.SuppressEchoAt = nudgePos;

            // マウスを動かすまで移動イベントが発火しないため、ハンドル形状を即時更新する
            UpdateCursor(nudgePos, ToDomainRadius());
        }

        e.Handled = true;
        InvalidateVisual();
    }

    /// <summary>Ctrl+ホイールでズームを要求する。</summary>
    /// <param name="e">ホイールの情報。</param>
    /// <remarks>
    /// Ctrl+ホイールは <see cref="ZoomRequested"/>（ズームの実処理は上位 AP）を発火する。<br/>
    /// それ以外のホイールは処理しない（スクロールの実処理は上位 AP が担う）。
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

    /// <summary>矢印キーで nudge を実行し、Escape / Enter で確定・キャンセルを要求する。</summary>
    /// <param name="e">キーの情報。</param>
    /// <remarks>
    /// 領域作成後はマウス押下中（ドラッグ中・ハンドル選択中）のみ nudge を許可する。<br/>
    /// 本コントロールにフォーカスがある場合のみ呼ばれる（Avalonia 版のフォーカス非依存 nudge とは異なる）。
    /// </remarks>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Escape)
        {
            Log.LogKeyDown(e.Key.ToString(), handled: true);
            RequestCancel();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            Log.LogKeyDown(e.Key.ToString(), handled: true);
            RequestConfirm();
            e.Handled = true;
            return;
        }

        if (_state.HasSelection && !_state.IsMouseDown)
        {
            return;
        }

        if (HandleArrowKey(e.Key))
        {
            Log.LogKeyDown(e.Key.ToString(), handled: true);
            e.Handled = true;
        }
    }

    /// <summary>カーソルキー入力を処理する（1 ドメインピクセル単位）。</summary>
    /// <param name="key">押下されたキー。</param>
    /// <returns>キーが処理された場合は true。</returns>
    /// <remarks>
    /// 1. nudge 前に現在のカーソル位置（画面物理）をドメイン座標へ変換して記録する（原点飛びの防止）。<br/>
    /// 2. 未選択・未ドラッグで nudge セッション開始時のみ、基点（<see cref="DragRectState.StartPoint"/>）をカーソル位置へ同期する。<br/>
    /// 3. nudge 状態を開始する（カーソル非表示・十字線表示）。<br/>
    /// 4. 整数加算のみで矩形を更新する（浮動小数の誤差を累積しない）。
    /// </remarks>
    private bool HandleArrowKey(Key key)
    {
        var (dx, dy) = key switch
        {
            Key.Left => (-1.0, 0.0),
            Key.Right => (1.0, 0.0),
            Key.Up => (0.0, -1.0),
            Key.Down => (0.0, 1.0),
            _ => (0.0, 0.0),
        };

        if (dx == 0 && dy == 0)
        {
            return false;
        }

        // nudge 前のカーソル位置をドメイン座標として記録する（基点精度の向上・原点飛びの防止）
        if (FindVisualRoot() is Window window)
        {
            var screen = _cursor.GetCursorScreenPoint();
            var local = window.ScreenToClient(new Point(screen.X, screen.Y));
            _state.LastMousePosition = _coordinatePort.Forward(new EsPoint(local.X, local.Y));
        }

        if (!_state.HasSelection && !_state.IsDragging && !_state.NudgeActive)
        {
            _state.StartPoint = _state.LastMousePosition;
        }

        if (!_state.NudgeActive)
        {
            _state.NudgeActive = true;
            _state.CrosshairVisible = true;
            _state.NudgeBase = _state.HasSelection ? _state.GetOppositeCorner(_state.SelectionRect) : _state.StartPoint;
            _state.NudgeOffsetX = 0;
            _state.NudgeOffsetY = 0;
            _cursor.ShowCursor(false);
        }

        _state.NudgeOffsetX += (int)dx;
        _state.NudgeOffsetY += (int)dy;
        _state.Nudge(dx, dy, MinSelectionSize, ClampToDomainBounds);

        RaiseSelectionChanged(_state.SelectionRect);

        var position = _state.GetMarkerPosition();
        RaiseLoupePositionMoved(position);
        _cursor.SetCursorScreenPoint(ToScreen(position));
        InvalidateVisual();
        return true;
    }

    /// <summary>nudge を終了し、マウスポインターを再表示する。</summary>
    private void EndNudge()
    {
        if (!_state.NudgeActive)
        {
            return;
        }

        _state.NudgeActive = false;
        _state.CrosshairVisible = false;
        _state.NudgeOffsetX = 0;
        _state.NudgeOffsetY = 0;
        _cursor.ShowCursor(true);
        InvalidateVisual();
    }

    /// <summary>「ドメイン境界外の選択を禁止」の変更を現在の選択矩形へ適用する。</summary>
    /// <remarks>ON へ切り替わった瞬間に既存の選択矩形を境界内へクリップし、変更があった場合のみ通知する。</remarks>
    private void ApplyClampToBounds()
    {
        if (_state.ClampSelectionToBounds())
        {
            RaiseSelectionChanged(_state.SelectionRect);
        }

        InvalidateVisual();
    }

    /// <summary>ドラッグハンドル上のカーソル形状を更新する。</summary>
    /// <param name="domainPos">判定するドメイン座標。</param>
    /// <param name="hitRadius">ヒットテスト半径（ドメイン座標。呼び出し側で事前計算済み）。</param>
    /// <remarks>
    /// ハンドル上ではリサイズ方向に応じたカーソル、枠内では移動カーソルを表示する。<br/>
    /// ヒットテスト半径を引数で受け取り、内部で再計算しない（マウス移動毎の変換の重複を排除）。
    /// </remarks>
    private void UpdateCursor(EsPoint domainPos, double hitRadius)
    {
        var handle = _state.HitTest(domainPos, hitRadius);

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

    /// <summary>ローカル DIP 座標をドメイン座標へ変換する。</summary>
    /// <param name="localDip">ローカル DIP 座標。</param>
    /// <returns>ドメイン座標。</returns>
    private EsPoint ToDomain(Point localDip) => _coordinatePort.Forward(new EsPoint(localDip.X, localDip.Y));

    /// <summary>DIP 半径をドメイン半径に変換する（ヒットテスト用）。</summary>
    /// <returns>ドメイン座標系でのヒットテスト半径。</returns>
    /// <remarks>
    /// Forward の差を取ることでオフセット（レターボックス等）をキャンセルし、スケールのみを得る。<br/>
    /// スケールがほぼ 0（等価変換）の場合は DIP 半径をそのまま使用する。
    /// </remarks>
    private double ToDomainRadius()
    {
        var port = _coordinatePort;
        var origin = port.Forward(default);
        var offset = port.Forward(new EsPoint(HandleHitRadius, 0));
        var radius = Math.Abs(offset.X - origin.X);
        return radius < 0.001 ? HandleHitRadius : radius;
    }

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

        var localDip = _coordinatePort.Inverse(domainPos);
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
        var topLeft = _coordinatePort.Inverse(domainRect.TopLeft);
        var bottomRight = _coordinatePort.Inverse(new EsPoint(domainRect.Right + 1, domainRect.Bottom + 1));
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

    /// <summary>カーソル座標ラベルのフォントサイズを安全域へ補正する。</summary>
    /// <param name="value">補正する値。</param>
    /// <returns>6〜48 に補正した値。非数（NaN / ∞）の場合は既定値。</returns>
    private static double CoerceCoordLabelFontSize(double value)
        => double.IsFinite(value) ? Math.Clamp(value, CoordLabelFontSizeMin, CoordLabelFontSizeMax) : CoordLabelFontSizeDefault;

    /// <summary>選択枠線・ハンドル・十字マーカー・座標ラベルを描画する。</summary>
    /// <param name="canvas">描画先のキャンバス（物理ピクセル）。</param>
    /// <param name="info">描画先の情報（幅・高さ）。</param>
    /// <remarks>
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
            if (_state.HasSelection)
            {
                var dipRect = ToDipRect(_state.SelectionRect);
                DragRectSkiaRenderer.DrawBorder(canvas, dipRect, BorderColor, BorderThickness);
                DragRectSkiaRenderer.DrawHandles(canvas, dipRect, HandleSize, HandleColor, BorderColor);
            }

            if (_state.NudgeActive && _state.CrosshairVisible)
            {
                var markerDip = _coordinatePort.Inverse(_state.GetMarkerPosition());
                DragRectSkiaRenderer.DrawStartMarker(canvas, markerDip, MarkerColor, MarkerThickness, MarkerLength / 2);
            }

            if (ShowCoordLabel)
            {
                var cursorDip = _coordinatePort.Inverse(_state.LastMousePosition);
                var clientSize = new EsSize(info.Width / scaling, info.Height / scaling);
                DragRectSkiaRenderer.DrawCoordLabel(
                    canvas, cursorDip, _state.LastMousePosition, clientSize,
                    CoordLabelColor, CoordLabelBackgroundColor, CoordLabelFontSize);
            }
        }
        finally
        {
            canvas.Restore();
        }
    }

    /// <summary><see cref="SelectionStarted"/> を発火する。</summary>
    /// <param name="domainPos">通知するドメイン座標。</param>
    private void RaiseSelectionStarted(EsPoint domainPos)
        => SelectionStarted?.Invoke(this, new DragRectPointEventArgs(domainPos, _state.CreateSnapshot(domainPos)));

    /// <summary><see cref="SelectionChanged"/> を発火する。</summary>
    /// <param name="rect">通知する選択矩形（ドメイン座標）。</param>
    private void RaiseSelectionChanged(EsRect rect)
        => SelectionChanged?.Invoke(this, new SelectionRectEventArgs(rect, _state.CreateSnapshot()));

    /// <summary><see cref="SelectionCompleted"/> を発火する。</summary>
    /// <param name="rect">確定した選択矩形（ドメイン座標）。</param>
    private void RaiseSelectionCompleted(EsRect rect)
        => SelectionCompleted?.Invoke(this, new SelectionRectEventArgs(rect, _state.CreateSnapshot()));

    /// <summary><see cref="SelectionCancelled"/> を発火する。</summary>
    private void RaiseSelectionCancelled() => SelectionCancelled?.Invoke(this, EventArgs.Empty);

    /// <summary><see cref="DragMouseDown"/> を発火する。</summary>
    /// <param name="domainPos">通知するドメイン座標。</param>
    private void RaiseMouseDown(EsPoint domainPos)
        => DragMouseDown?.Invoke(this, new DragRectPointEventArgs(domainPos, _state.CreateSnapshot(domainPos)));

    /// <summary><see cref="DragMouseUp"/> を発火する。</summary>
    /// <param name="domainPos">通知するドメイン座標。</param>
    private void RaiseMouseUp(EsPoint domainPos)
        => DragMouseUp?.Invoke(this, new DragRectPointEventArgs(domainPos, _state.CreateSnapshot(domainPos)));

    /// <summary><see cref="MouseClicked"/> を発火する。</summary>
    /// <param name="domainPos">通知するドメイン座標。</param>
    private void RaiseMouseClicked(EsPoint domainPos)
        => MouseClicked?.Invoke(this, new DragRectPointEventArgs(domainPos, _state.CreateSnapshot(domainPos)));

    /// <summary><see cref="MouseDoubleClicked"/> を発火する。</summary>
    /// <param name="domainPos">通知するドメイン座標。</param>
    private void RaiseMouseDoubleClicked(EsPoint domainPos)
        => MouseDoubleClicked?.Invoke(this, new DragRectPointEventArgs(domainPos, _state.CreateSnapshot(domainPos)));

    /// <summary><see cref="MouseMoved"/> を発火する。</summary>
    /// <param name="domainPos">通知するドメイン座標。</param>
    private void RaiseMouseMoved(EsPoint domainPos)
        => MouseMoved?.Invoke(this, new DragRectPointEventArgs(domainPos, _state.CreateSnapshot(domainPos)));

    /// <summary><see cref="LoupePositionMoved"/> を発火する。</summary>
    /// <param name="domainPos">通知するドメイン座標。</param>
    private void RaiseLoupePositionMoved(EsPoint domainPos)
        => LoupePositionMoved?.Invoke(this, new DragRectPointEventArgs(domainPos, _state.CreateSnapshot(domainPos)));

    /// <summary><see cref="ViewportChanged"/> を発火する。</summary>
    private void RaiseViewportChanged()
        => ViewportChanged?.Invoke(this, new SelectionRectEventArgs(_state.SelectionRect, _state.CreateSnapshot()));
}
