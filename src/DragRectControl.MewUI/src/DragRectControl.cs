using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Aprillz.MewUI.Skia.Controls;
using Estable.DragRect;
using Estable.DragRect.Decoration;
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
    /// <summary><see cref="Options"/> プロパティ（外観・対話パラメータをまとめた設定）。</summary>
    /// <remarks>
    /// 個別に公開していた設定（外観 10 項目・対話パラメータ 3 項目）を 1 つの不変オブジェクトへ統合し、公開プロパティを 1 つに縮小する。<br/>
    /// 設定値は不変（<see cref="DragRectOptions"/>）であるため、同値の再設定では変更通知が発生しない（無駄な再描画を防ぐ）。<br/>
    /// 対話パラメータ（最小サイズ・ヒットテスト半径・境界外選択の禁止）はセッションへ伝搬し、判断と変更ログはコアが単一所有する。
    /// </remarks>
    public static readonly MewProperty<DragRectOptions> OptionsProperty =
        MewProperty<DragRectOptions>.Register<DragRectControl>(
            nameof(Options), DragRectOptions.Default, MewPropertyOptions.AffectsRender,
            changed: static (owner, _, newValue) => owner.ApplyFeedback(owner._session.ApplyOptions(newValue)));

    /// <summary><see cref="RenderScaling"/> プロパティ（DIP → 物理ピクセルのスケーリング係数）。</summary>
    public static readonly MewProperty<double> RenderScalingProperty =
        MewProperty<double>.Register<DragRectControl>(nameof(RenderScaling), 1.0, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="Scaling"/> プロパティ（座標変換ポートへ適用する倍率）。</summary>
    /// <remarks><see cref="SetCoordinateTransform"/> でポートを設定した場合は使用しない（等倍スケール専用の簡易指定）。</remarks>
    public static readonly MewProperty<double> ScalingProperty =
        MewProperty<double>.Register<DragRectControl>(nameof(Scaling), 1.0, MewPropertyOptions.AffectsRender);

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
    /// コアからの通知は公開イベントへ転送し、設定（<see cref="Options"/>）をセッションへ伝搬する。
    /// </remarks>
    public DragRectControl(ICursorService? cursorService)
    {
        Focusable = true;
        _cursor = cursorService ?? PlatformFactory.CreateCursorService();
        _session = new DragRectSession(_cursor, Options);
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

    /// <summary>Ctrl+ホイールによるズーム要求時に発火する（方向と発生位置）。</summary>
    /// <remarks>
    /// ズームの実処理（画像サイズ変更・ズーム率管理）は上位 AP が実施する（本コントロールは要求のみ通知する）。<br/>
    /// <see cref="DragRectZoomRequest.Position"/> はズーム中心（カーソル位置）として使用できる。
    /// </remarks>
    public event EventHandler<DragRectZoomRequest>? ZoomRequested;

    /// <summary>通常ホイール（Ctrl なし）によるスクロール要求時に発火する（生の回転量・修飾キー・発生位置）。</summary>
    /// <remarks>
    /// スクロール先（ズームビューポートのパン等）の決定は上位 AP が実施する（本コントロールは回転量のみ通知する）。<br/>
    /// 選択矩形はドメイン座標で不変のため、本操作は選択状態を変更しない（ビューの操作）。
    /// </remarks>
    public event EventHandler<DragRectScrollRequest>? ScrollRequested;

    /// <summary>Enter キーによる確定要求時に発火する（選択確定後のみ。ドラッグ中は発火しない）。</summary>
    public event EventHandler<SelectionRectEventArgs>? ConfirmRequested;

    /// <summary>Escape キーによるキャンセル要求時に発火する（選択の破棄は本コントロールが実施済み）。</summary>
    public event EventHandler? CancelRequested;

    /// <summary>外観・対話パラメータをまとめた設定を取得または設定する。</summary>
    /// <value>設定（枠線・ハンドル・十字マーカー・座標ラベルの外観、最小選択サイズ・ヒットテスト半径・境界外選択の禁止）。既定は <see cref="DragRectOptions.Default"/>。</value>
    /// <remarks>
    /// 設定を変更すると再描画され、対話パラメータ（最小サイズ・ヒットテスト半径・境界外選択の禁止）はセッションへ伝搬する。<br/>
    /// 一部の項目だけを変える場合は <c>DragRectOptions.Default with { ShowHandles = false }</c> の形で指定する（指定しない項目は既定値を保つ）。<br/>
    /// 色は UI 非依存の <see cref="DrawingColor"/> で受け取り、描画（Skia）への変換は描画層が行う。
    /// </remarks>
    public DragRectOptions Options
    {
        get => GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    /// <summary>座標変換ポートへ適用する倍率を取得または設定する。</summary>
    /// <value>ドメイン → DIP の倍率。既定は 1.0。</value>
    /// <remarks>
    /// 環境値（DPI・マルチスクリーン）のため <see cref="Options"/> には含めない。<br/>
    /// <see cref="SetCoordinateTransform"/> が未設定の場合のみ使われる（ポート優先）。<br/>
    /// 選択矩形と十字マーカーの描画はセッションのポートのみを使うため本値の影響を受けない<br/>
    /// （本値が影響するのは外側の装飾のサイズラベル・ラベル位置）。
    /// </remarks>
    public double Scaling
    {
        get => GetValue(ScalingProperty);
        set => SetValue(ScalingProperty, value);
    }

    /// <summary>サイズラベル表示用のスケーリング係数を取得または設定する（マルチスクリーン補正）。</summary>
    /// <value>サイズラベルの自動生成に使う係数。未設定（0 以下）の場合は <see cref="Scaling"/> と同じ値が使用される。</value>
    /// <remarks>0 以下を <see cref="Scaling"/> と同値として扱う規則はコア（<c>OuterDecoLabels.ResolveDisplayScaling</c>）が単一所有する。</remarks>
    public double DisplayScaleScaling
    {
        get => OuterDecoLabels.ResolveDisplayScaling(field, Scaling);
        set => field = value;
    }

    /// <summary>サイズラベルのテキストを提供する関数を取得または設定する。</summary>
    /// <value>サイズラベルのテキストを返す関数。null または空文字の場合は自動生成する。</value>
    /// <remarks>
    /// デリゲート（ホスト結線）のため <see cref="Options"/> には含めない（値の等価比較の対象外にするため）。<br/>
    /// 描画のたびに評価されるため、ドラッグ中の選択矩形の変化にも追従できる。<br/>
    /// 設定（変更）時は再描画を要求する。
    /// </remarks>
    public Func<string?>? SizeLabelOverrideProvider
    {
        get => field;
        set
        {
            field = value;
            InvalidateVisual();
        }
    }

    /// <summary>ドメイン ↔ ローカル DIP の座標変換ポート（null はコアが <see cref="Scaling"/> から解決する）。</summary>
    /// <remarks>
    /// セッションは null を等価変換に置換して保持するため、<b>指定の有無（null かどうか）は本フィールドだけが保持する</b>。<br/>
    /// この区別が必要なのは、ポート未指定時に倍率（<see cref="Scaling"/>）を使う規則をコアが単一所有するためである。
    /// </remarks>
    private ICoordTransform? _coordinatePort;

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

    /// <summary>選択矩形があるかどうかを取得する。</summary>
    /// <value>選択中の場合は true。</value>
    /// <remarks>描画のたびに参照されるため、スナップショットの生成を伴わない軽量なプロパティとする。</remarks>
    public bool HasSelection => _session.HasSelection;

    /// <summary>現在の選択矩形（ドメイン座標）を取得する。</summary>
    /// <value>選択矩形（ドメイン座標）。未選択時は 0x0 の空矩形。</value>
    /// <remarks>描画のたびに参照されるため、スナップショットの生成を伴わない軽量なプロパティとする。</remarks>
    public EsRect SelectionRect => _session.SelectionRect;

    /// <summary>操作ログの出力先を取得または設定する。</summary>
    /// <value>ロガー。null を設定した場合は <see cref="NullLogger"/> へフォールバックする（出力なし）。</value>
    /// <remarks>
    /// 保持とフォールバック、セッションへの伝搬はコア（<see cref="DragRectSession.Log"/>）が単一所有する（本層は委譲するだけ）。<br/>
    /// 入力の判断に伴うログはセッションが同じロガーで出力する。
    /// </remarks>
    public ILogger Log
    {
        get => _session.Log;
        set => _session.Log = value;
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
        _coordinatePort = port;
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
    public void NotifyViewportChanged() => ApplyFeedback(_session.NotifyViewportChanged());

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
        RenderScaling = DpiScaler.ToScaling(newDpi);
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

    /// <summary>ホイールでズーム要求・スクロール要求を行う。</summary>
    /// <param name="e">ホイールの情報。</param>
    /// <remarks>
    /// Ctrl+ホイールは <see cref="ZoomRequested"/>（ズームの実処理は上位 AP）、それ以外は <see cref="ScrollRequested"><br/>
    /// （スクロールの実処理は上位 AP）を発火する。回転量はそのまま通知し、スクロール量の解釈は転送先が単一所有する。<br/>
    /// ホイールはビューの操作であり選択矩形の状態を変えないため UI 層が担う。
    /// </remarks>
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);

        if (e.ControlKey)
        {
            Log.LogZoomRequested(e.Delta.Y > 0);
            ZoomRequested?.Invoke(this, new DragRectZoomRequest(e.Delta.Y > 0, e.GetPosition(this).ToEsPoint()));
            e.Handled = true;
            return;
        }

        ScrollRequested?.Invoke(this, new DragRectScrollRequest(e.Delta.X, e.Delta.Y, e.ShiftKey, e.GetPosition(this).ToEsPoint()));
        e.Handled = true;
    }

    /// <summary>キーダウン処理（矢印キーの nudge・Escape のキャンセル・Enter の確定）。</summary>
    /// <param name="e">キーイベント。</param>
    /// <remarks>
    /// キーをコア型（<see cref="EsKey"/>）へ変換して通知し、返された効果（再描画・カーソル操作）を適用するだけである。<br/>
    /// 矢印キー nudge・Escape・Enter の判断（nudge の許可条件を含む）はコアが行う。<br/>
    /// <c>|| e.Handled</c> は、先に誰かが処理済みにしたフラグを false へ戻さないため。<br/>
    /// 本コントロールにフォーカスがある場合のみ呼ばれる（Avalonia 版のフォーカス非依存 nudge とは異なる）。
    /// </remarks>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        var feedback = _session.KeyDown(e.Key.ToEsKey(), CurrentCursorDomainPosition());
        ApplyFeedback(feedback);
        e.Handled = feedback.Handled || e.Handled;
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
    /// <param name="feedback">適用する効果（カーソル操作・フォーカス・カーソル形状・再描画）。</param>
    /// <remarks>
    /// 適用する効果の選択・順序はコア（<see cref="DragRectFeedback.ApplyCursor"/> / <see cref="DragRectFeedback.ApplyVisual"/>）が<br/>
    /// 単一所有する（本層は自 UI の操作へ接続するだけ。マウスキャプチャはハンドラ側で行う）。
    /// </remarks>
    private void ApplyFeedback(DragRectFeedback feedback)
    {
        feedback.ApplyCursor(_cursor, ToScreen);
        feedback.ApplyVisual(() => Focus(), InvalidateVisual, ApplyCursorShape);
    }

    /// <summary>コアからの通知を公開イベントへ転送する（判断はしない）。</summary>
    /// <param name="sender">送信元（セッション）。</param>
    /// <param name="notice">通知の種別・座標・スナップショット。</param>
    /// <remarks>
    /// 転送のみを担う（確定/キャンセルの判断・状態の更新はコアが済ませている）。<br/>
    /// 本 UI は押下・解放の公開イベントを持たない（基底 <c>UIElement</c> の同名メンバーがサービスを提供済みのため）。
    /// </remarks>
    private void OnSessionNotified(object? sender, DragRectNotice notice)
    {
        switch (notice.Kind)
        {
            // 選択（始点・変更・確定・キャンセル）
            case DragRectNoticeKind.SelectionStarted: SelectionStarted?.Invoke(this, ToPointArgs(notice)); break;
            case DragRectNoticeKind.SelectionChanged: SelectionChanged?.Invoke(this, ToRectArgs(notice)); break;
            case DragRectNoticeKind.SelectionCompleted: SelectionCompleted?.Invoke(this, ToRectArgs(notice)); break;
            case DragRectNoticeKind.SelectionCancelled: SelectionCancelled?.Invoke(this, EventArgs.Empty); break;

            // ポインター（クリック・ダブルクリック・移動）
            case DragRectNoticeKind.MouseClicked: MouseClicked?.Invoke(this, ToPointArgs(notice)); break;
            case DragRectNoticeKind.MouseDoubleClicked: MouseDoubleClicked?.Invoke(this, ToPointArgs(notice)); break;
            case DragRectNoticeKind.MouseMoved: MouseMoved?.Invoke(this, ToPointArgs(notice)); break;

            // ルーペ・ビューポート・確定要求・キャンセル要求
            case DragRectNoticeKind.LoupePositionMoved: LoupePositionMoved?.Invoke(this, ToPointArgs(notice)); break;
            case DragRectNoticeKind.ViewportChanged: ViewportChanged?.Invoke(this, ToRectArgs(notice)); break;
            case DragRectNoticeKind.ConfirmRequested: ConfirmRequested?.Invoke(this, ToRectArgs(notice)); break;
            case DragRectNoticeKind.CancelRequested: CancelRequested?.Invoke(this, EventArgs.Empty); break;
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
    /// <summary>カーソル形状をコアが示す種別に応じて更新する。</summary>
    /// <param name="kind">カーソル形状の種別（コア提供）。</param>
    /// <remarks>種別と自 UI のカーソルの対応のみを持つ（どのハンドルが同じカーソルになるかはコアが判断済み）。</remarks>
    private void ApplyCursorShape(DragCursorKind kind)
    {
        Cursor = kind switch
        {
            DragCursorKind.DiagonalNWSE => CursorType.SizeNWSE,
            DragCursorKind.DiagonalNESW => CursorType.SizeNESW,
            DragCursorKind.Vertical => CursorType.SizeNS,
            DragCursorKind.Horizontal => CursorType.SizeWE,
            DragCursorKind.Move => CursorType.SizeAll,
            _ => null,
        };
    }

    /// <summary>選択矩形の外側に描く装飾・ハンドル・十字マーカー・座標ラベルを描画する。</summary>
    /// <param name="canvas">描画先のキャンバス（物理ピクセル）。</param>
    /// <param name="info">描画先の情報（幅・高さ）。</param>
    /// <remarks>
    /// 描画は 2 層に分かれる。<br/>
    /// - <b>外側の装飾</b>（暗転マスク・穴あけ枠・ラベル）: 装飾コアのフレームを <see cref="DragRectOuterSkiaRenderer.Draw"/> へ渡して描く。<br/>
    /// - <b>選択矩形そのもの</b>（ドラッグハンドル・始点マーカー・座標ラベル）: <see cref="DragRectSkiaRenderer"/> で描く。<br/>
    /// <br/>
    /// 選択枠線は外側の装飾の穴あけ枠（同じ矩形の内側）と重なるため、本コントロールは描画しない<br/>
    /// （別々に描くと線幅が二重になり見た目が変わる）。枠線の色と太さは <see cref="DragRectOptions.OuterAppearance"/> が単一所有する。<br/>
    /// セッションの状態（選択矩形・nudge・マウス位置）を読み、DIP へ変換して描画層へ渡すだけである。<br/>
    /// キャンバスを DPI スケーリングしてから DIP 座標で描画する（コアの計算と座標系を一致させる）。<br/>
    /// 背景は塗らない（透明のままにして下の画像を透かす）。<br/>    /// 外側の装飾（暗転マスク）は選択の有無に関係なく描画する（ハイライトのみを表示するモードを含む）。
    /// </remarks>
    private void OnPaintSurface(SKCanvas canvas, SKImageInfo info)
    {
        var scaling = RenderScaling > 0 ? RenderScaling : 1.0;
        var options = Options;

        canvas.Save();
        canvas.Scale((float)scaling);
        try
        {
            // 前フレームの描画（選択矩形・暗転マスク）を残さないため、自分の描画面を消してから描く
            canvas.Clear(SKColors.Transparent);

            // 選択矩形の外側に描く装飾（暗転マスク・穴あけ枠・ラベル）。
            // どの矩形を暗転するか・枠線を描くか・ラベルをどう組むかは装飾コアが持つため、本層はフレームを描画 API へ写すだけとする。
            DragRectOuterSkiaRenderer.Draw(
                canvas,
                DragRectFrame.Resolve(
                    _session,
                    new EsRect(0, 0, info.Width / scaling, info.Height / scaling),
                    _coordinatePort,
                    Scaling,
                    SizeLabelOverrideProvider,
                    DisplayScaleScaling));

            if (options.ShowHandles && _session.HasSelection)
            {
                var dipRect = _session.ToDipRect(_session.SelectionRect);
                DragRectSkiaRenderer.DrawHandles(
                    canvas, dipRect, options.HandleSize, options.HandleColor, options.OuterAppearance.BorderColor);
            }

            if (_session.NudgeActive && _session.CrosshairVisible)
            {
                var markerDip = _session.ToDip(_session.MarkerPosition);
                DragRectSkiaRenderer.DrawStartMarker(
                    canvas, markerDip, options.MarkerColor, DragRectAppearance.MarkerThickness, options.MarkerLength / 2);
            }

            if (options.ShowCoordLabel)
            {
                var cursorDip = _session.ToDip(_session.LastMousePosition);
                var clientSize = new EsSize(info.Width / scaling, info.Height / scaling);
                DragRectSkiaRenderer.DrawCoordLabel(
                    canvas, cursorDip, _session.LastMousePosition, clientSize,
                    options.CoordLabelColor, options.CoordLabelBackgroundColor, options.CoordLabelFontSize);
            }
        }
        finally
        {
            canvas.Restore();
        }
    }
}