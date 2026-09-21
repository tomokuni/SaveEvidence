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
/// 外観・対話パラメータの設定は 1 つの依存関係プロパティ（<see cref="Options"/>）で公開し、外部バインディングで制御できる<br/>
/// （色の不透明度は色のアルファに統合。設定値の既定値と範囲補正はコアの <see cref="DragRectOptions"/> が単一所有する）。<br/>
/// 対話パラメータ（最小選択サイズ・境界外選択の禁止・ハンドルのヒットテスト半径）はセッションへ伝搬し、判断はセッションが行う。<br/>
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

    /// <summary>通常ホイール 1 段あたりのスクロール量（DIP）。</summary>
    /// <remarks>ホイールの配送先（祖先 ScrollViewer）は UI 層が知る情報のため、本値も UI 層が持つ。</remarks>
    private const double WheelScrollUnit = 48.0;

    /// <summary>フォーカス非依存の矢印キー nudge 用に購読した TopLevel。解除時に使用する。</summary>
    private TopLevel? _attachedTopLevel;

    /// <summary>ズーム・スクロール検知用に購読した祖先 ScrollViewer。解除時に使用する。</summary>
    private ScrollViewer? _attachedScrollViewer;

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
    /// コアからの通知は公開イベントへ転送し、設定（<see cref="Options"/>）をセッションへ伝搬する。
    /// </remarks>
    public DragRectControl(ICursorService? cursorService)
    {
        Focusable = true;
        _cursor = cursorService ?? PlatformFactory.CreateCursorService();
        _session = new DragRectSession(_cursor, Options);
        _session.Notified += OnSessionNotified;
    }

    /// <summary>静的コンストラクタ。設定（<see cref="Options"/>）の変更を再描画とセッションへ同期する。</summary>
    /// <remarks>
    /// 外観の変更は <see cref="Visual.AffectsRender{T}"/> により再描画を要求する。<br/>
    /// 対話パラメータ（最小サイズ・ヒットテスト半径・境界外選択の禁止）はセッションへ伝搬し、判断はセッションが行う。
    /// </remarks>
    static DragRectControl()
    {
        // 設定（外観を含む）の変更は再描画を要求する
        AffectsRender<DragRectControl>(OptionsProperty);

        // 対話パラメータをセッションへ伝搬する（変更の影響判断・ログはコアが単一所有する）
        OptionsProperty.Changed.AddClassHandler<DragRectControl>(
            static (c, e) => c.ApplyFeedback(c._session.ApplyOptions(e.GetNewValue<DragRectOptions>())));
    }

    // ====================================================================
    // ログ
    // ====================================================================

    /// <summary>内部ロガー（null の場合は出力なし）。</summary>
    /// <value>null を設定した場合は <see cref="NullLogger"/> へフォールバックする。</value>
    /// <remarks>
    /// 保持とフォールバック、セッションへの伝搬はコア（<see cref="DragRectSession.Log"/>）が単一所有する（本層は委譲するだけ）。<br/>
    /// 入力の判断に伴うログはセッションが同じロガーで出力する。
    /// </remarks>
    public ILogger Log
    {
        get => _session.Log;
        set => _session.Log = value;
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

    /// <summary><see cref="Options"/> 依存関係プロパティ（外観・対話パラメータをまとめた設定）。</summary>
    /// <remarks>
    /// 個別に公開していた設定（外観 10 項目・対話パラメータ 3 項目）を 1 つの不変オブジェクトへ統合し、公開プロパティを 1 つに縮小する。<br/>
    /// 設定値は不変（<see cref="DragRectOptions"/>）であるため、同値の再設定では変更通知が発生しない（無駄な再描画を防ぐ）。
    /// </remarks>
    public static readonly StyledProperty<DragRectOptions> OptionsProperty =
        AvaloniaProperty.Register<DragRectControl, DragRectOptions>(nameof(Options), defaultValue: DragRectOptions.Default);

    /// <summary>外観・対話パラメータをまとめた設定を取得または設定する。</summary>
    /// <value>設定（枠線・ハンドル・十字マーカー・座標ラベルの外観、最小選択サイズ・ヒットテスト半径・境界外選択の禁止）。既定は <see cref="DragRectOptions.Default"/>。</value>
    /// <remarks>
    /// 設定を変更すると再描画され、対話パラメータ（最小サイズ・ヒットテスト半径・境界外選択の禁止）はセッションへ伝搬する。<br/>
    /// 一部の項目だけを変える場合は <c>DragRectOptions.Default with { BorderThickness = 3.0 }</c> の形で指定する（指定しない項目は既定値を保つ）。<br/>
    /// 色は UI 非依存の <see cref="System.Drawing.Color"/> で受け取り、描画時に Avalonia の色型へ変換する。
    /// </remarks>
    public DragRectOptions Options
    {
        get => GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

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
    /// <param name="feedback">適用する効果（カーソル操作・フォーカス・カーソル形状・再描画）。</param>
    /// <remarks>
    /// 適用する効果の選択・順序はコア（<see cref="DragRectFeedback.ApplyCursor"/> / <see cref="DragRectFeedback.ApplyVisual"/>）が<br/>
    /// 単一所有する（本層は自 UI の操作へ接続するだけ）。
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
    /// 各 UI のイベント引数の型（Avalonia の <see cref="Rect"/> / <see cref="Point"/>）へ境界変換する。
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

            // ポインター（押下・解放・クリック・ダブルクリック・移動）
            case DragRectNoticeKind.MouseDown: MouseDown?.Invoke(this, ToPointArgs(notice)); break;
            case DragRectNoticeKind.MouseUp: MouseUp?.Invoke(this, ToPointArgs(notice)); break;
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
    /// <returns>通知座標（DIP）とスナップショットを含むイベント引数。</returns>
    private static DragRectPointEventArgs ToPointArgs(DragRectNotice notice)
        => new(notice.Position.ToAvaloniaPoint(), notice.Snapshot);

    /// <summary>通知を選択矩形のイベント引数へ変換する。</summary>
    /// <param name="notice">通知。</param>
    /// <returns>選択矩形（DIP）とスナップショットを含むイベント引数。</returns>
    private static SelectionRectEventArgs ToRectArgs(DragRectNotice notice)
        => new(notice.Snapshot.SelectionRect.ToAvaloniaRect(), notice.Snapshot);

    /// <summary>カーソル形状をコアが示す種別に応じて更新する。</summary>
    /// <param name="kind">カーソル形状の種別（コア提供）。</param>
    /// <remarks>種別と自 UI のカーソルの対応のみを持つ（どのハンドルが同じカーソルになるかはコアが判断済み）。</remarks>
    private void ApplyCursorShape(DragCursorKind kind)
    {
        // 標準カーソルは Lazy で遅延共有する（ポインター移動毎の生成を排除しつつ、初回アクセスで ICursorFactory を解決）
        Cursor = kind switch
        {
            DragCursorKind.DiagonalNWSE => s_topLeftCornerCursor.Value,
            DragCursorKind.DiagonalNESW => s_topRightCornerCursor.Value,
            DragCursorKind.Vertical => s_sizeNorthSouthCursor.Value,
            DragCursorKind.Horizontal => s_sizeWestEastCursor.Value,
            DragCursorKind.Move => s_sizeAllCursor.Value,
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

        // 左ボタン以外（右クリック・中ボタン等）は処理しない
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        e.Handled = true;
        ApplyFeedback(_session.Press(ToDomain(e), e.ClickCount));
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

