using Estable.DragRect.Selection;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Estable.DragRect;

/// <summary>DragRect の対話（選択・ドラッグ・nudge）の状態を管理し、入力を通知されて状態を更新する状態管理クラス（Mediator パターン: 入力と状態の仲介）。</summary>
/// <remarks>
/// 提供機能: UI 層から入力（ポインター押下・移動・解放・ホイール以外のキー）を通知されると、状態を更新し、<br/>
/// 「上位へ伝える通知（<see cref="Notified"/>）」と「画面へ反映する効果（<see cref="DragRectFeedback"/>）」を返す。<br/>
/// <br/>
/// 責務の境界:<br/>
/// - 本クラス: 入力の解釈と状態遷移の判断（nudge セッションの開始・終了、ダブルクリックの確定/キャンセル、<br/>
///   ハンドルのヒットテストとスナップ、クリック検出、最小サイズによるキャンセル、エコー移動の抑制、カーソル種別の決定）。<br/>
/// - UI 層: 入力イベントの受付（座標の取得）と、本クラスが返す効果の適用・通知の公開イベントへの転送・状態の描画のみ。<br/>
/// <br/>
/// 座標系:<br/>
/// 選択矩形・nudge はドメイン座標で扱う。UI 層は入力座標を <see cref="ToDomain"/> で変換し、描画座標を <see cref="ToDip"/> で得る。<br/>
/// <br/>
/// 依存分離: コア型（<see cref="EsRect"/> / <see cref="EsPoint"/> / <see cref="EsKey"/>）と座標変換ポート（<see cref="ICoordTransform"/>）・<br/>
/// カーソルサービス（<see cref="ICursorService"/>）の抽象にのみ依存し、UI フレームワークへ依存しない。<br/>
/// UI 側の概念（マウスボタン・キー種別・コントロールへのフォーカス・画面座標）は、UI 層が本クラスの入出力へ変換する。<br/>
/// <br/>
/// 状態機械の保持と純粋計算は <see cref="DragRectState"/> が担い、本クラスは入力の解釈と通知の組み立てに専念する（単一責任）。<br/>
/// </remarks>
internal sealed class DragRectSession
{
    /// <summary>選択・ドラッグ・nudge の状態と純粋計算。</summary>
    private readonly DragRectState _state;

    /// <summary>マウスダウンからアップまでの間にドラッグ移動（選択状態の変化）が発生したかどうか（クリック検出用）。</summary>
    private bool _dragPerformed;

    /// <summary>最後のマウスダウン位置（ドメイン座標。クリック通知・ログ用）。</summary>
    private EsPoint _pressPosition;

    /// <summary>内部ロガー（未設定は null。読み取り時に <see cref="LoggerFallback"/> が吸収する）。</summary>
    private ILogger? _log;

    /// <summary>状態管理クラスを初期化する。</summary>
    /// <param name="cursor">カーソルサービス。null 可（null の場合はカーソル操作を行わない）。</param>
    /// <param name="options">初期設定。null 可（null の場合は <see cref="DragRectOptions.Default"/>）。</param>
    public DragRectSession(ICursorService? cursor, DragRectOptions? options = null)
    {
        _state = new DragRectState(cursor);
        Options = options ?? DragRectOptions.Default;
    }

    /// <summary>上位（UI 層）へ伝える通知。UI 層は自 UI の公開イベントへ転送する。</summary>
    public event EventHandler<DragRectNotice>? Notified;

    /// <summary>選択・ドラッグ・nudge の状態（描画用に読み取る。UI 層からの書き換えは想定しない）。</summary>
    public DragRectState State => _state;

    /// <summary>ロガー。null を設定した場合は出力なし。</summary>
    /// <value>注入されたロガー。null 可（null の場合は <see cref="NullLogger"/> へフォールバックする）。</value>
    public ILogger Log
    {
        get => _log.OrNullLogger();
        set => _log = value;
    }

    /// <summary>現在の設定（外観・対話パラメータ）。</summary>
    /// <value>反映済みの設定。既定は <see cref="DragRectOptions.Default"/>。</value>
    /// <remarks>本クラスは対話パラメータのみを使用する（外観は UI 層が描画時に読む）。</remarks>
    public DragRectOptions Options { get; private set; } = DragRectOptions.Default;

    /// <summary>選択矩形の最小サイズ（ドメイン座標）。</summary>
    /// <value>ドメイン座標での最小サイズ。ドラッグ完了時にこれ未満の矩形はキャンセル扱いになる。</value>
    public double MinSelectionSize => Options.MinSelectionSize;

    /// <summary>ドメイン境界外の選択を禁止するかどうか。</summary>
    /// <value>true の場合は選択をドメイン境界内に制限する。false の場合は境界外も選択できる。</value>
    public bool ClampToDomainBounds => Options.ClampToDomainBounds;

    /// <summary>ドラッグハンドルのヒットテスト半径（DIP）。</summary>
    /// <value>DIP 座標での半径。ドメイン座標への換算は内部で行う（ズームしても画面上の大きさが一定になる）。</value>
    public double HandleHitRadius => Options.HandleHitRadius;

    /// <summary>現在の選択矩形（ドメイン座標）。</summary>
    /// <value>選択矩形。未選択時は 0x0。</value>
    public EsRect SelectionRect => _state.SelectionRect;

    /// <summary>選択矩形が有効かどうか（幅・高さが 0 より大きい）。</summary>
    /// <value>選択ありの場合は true。</value>
    public bool HasSelection => _state.HasSelection;

    /// <summary>ドラッグ中かどうか（新規選択・移動・リサイズ）。</summary>
    /// <value>ドラッグ中の場合は true。</value>
    public bool IsDragging => _state.IsDragging;

    /// <summary>nudge（矢印キー微調整）中かどうか。</summary>
    /// <value>nudge 中の場合は true。</value>
    public bool NudgeActive => _state.NudgeActive;

    /// <summary>nudge 中に十字マーカーを表示するかどうか。</summary>
    /// <value>表示する場合は true。</value>
    public bool CrosshairVisible => _state.CrosshairVisible;

    /// <summary>直近のマウス位置（ドメイン座標。カーソル座標ラベルの描画に使用する）。</summary>
    /// <value>直近のマウス位置。未移動時は既定値。</value>
    public EsPoint LastMousePosition => _state.LastMousePosition;

    /// <summary>十字マーカー・ルーペが追従する位置（ドメイン座標）。</summary>
    /// <value>ドラッグ中のハンドル位置・新規選択中の終点・未選択時の始点。</value>
    public EsPoint MarkerPosition => _state.GetMarkerPosition();

    /// <summary>公開イベント用の状態スナップショットを生成する。</summary>
    /// <param name="mousePosition">スナップショットに含めるマウス位置（ドメイン座標）。null の場合は直近の位置。</param>
    /// <returns>状態・選択矩形・アクティブハンドル・マウス位置・操作フラグを含むスナップショット。</returns>
    public DragRectSnapshot CreateSnapshot(EsPoint? mousePosition = null) => _state.CreateSnapshot(mousePosition);

    /// <summary>ドメイン ↔ ローカル DIP の座標変換ポートを設定する。</summary>
    /// <param name="port">座標変換ポート。null の場合は等価変換（1 ドメインピクセル = 1 DIP・無制限）。</param>
    public void SetPort(ICoordTransform? port) => _state.Port = port ?? CoordTransform.Identity;

    /// <summary>ローカル DIP 座標をドメイン座標へ変換する。</summary>
    /// <param name="localDip">ローカル DIP 座標。</param>
    /// <returns>ドメイン座標。</returns>
    public EsPoint ToDomain(EsPoint localDip) => _state.Port.Forward(localDip);

    /// <summary>ドメイン座標をローカル DIP 座標へ変換する。</summary>
    /// <param name="domain">ドメイン座標。</param>
    /// <returns>ローカル DIP 座標。</returns>
    public EsPoint ToDip(EsPoint domain) => _state.Port.Inverse(domain);

    /// <summary>選択矩形（ドメイン座標）を閉区間としてローカル DIP 矩形へ変換する（描画用）。</summary>
    /// <param name="domainRect">選択矩形（ドメイン座標）。</param>
    /// <returns>ローカル DIP の矩形。</returns>
    /// <remarks>
    /// 選択領域は閉区間（始点・終点とも含む）を意図する仕様のため、終点（Right / Bottom）に +1 してから DIP へ変換する<br/>
    /// （終点のピクセルも枠線・ハンドルに含める。キャプチャの閉区間 +1 と一致）。<br/>
    /// 変換規則は本メソッドが単一所有する（描画層は結果を使うだけ）。
    /// </remarks>
    public EsRect ToDipRect(EsRect domainRect)
    {
        var topLeft = ToDip(domainRect.TopLeft);
        var bottomRight = ToDip(new EsPoint(domainRect.Right + 1, domainRect.Bottom + 1));
        return new EsRect(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);
    }

    // ====================================================================
    // 入力の通知（UI 層 → コア）
    // ====================================================================

    /// <summary>ポインター押下（左ボタン）を通知する。</summary>
    /// <param name="position">押下位置（ドメイン座標）。</param>
    /// <param name="clickCount">クリック数（2 以上 = ダブルクリック）。</param>
    /// <returns>UI 層が適用する効果（フォーカス取得・再描画・カーソル操作）。</returns>
    /// <remarks>
    /// 処理フロー:<br/>
    /// 1. nudge 中なら nudge を終了し、押下位置を nudge 後の始点に置き換える（ずれの防止）。<br/>
    /// 2. ダブルクリックは選択矩形の内側なら確定要求、外側ならキャンセル要求を通知して終了する。<br/>
    /// 3. ハンドルにヒットした場合はリサイズ / 移動を開始する（移動以外はポインターをハンドル点へスナップする）。<br/>
    /// 4. ヒットしない場合は新規選択を開始する。<br/>
    /// ハンドルが無い場合も <c>DragStart: handle=None</c> として記録する（新規選択のドラッグ開始も同じ経過として扱う）。
    /// </remarks>
    public DragRectFeedback Press(EsPoint position, int clickCount)
    {
        _state.IsMouseDown = true;
        Log.LogPositionBeforeDown(position.X, position.Y);
        Log.LogMouseDown(position.X, position.Y, clickCount, _state.HasSelection);

        // nudge 中は nudge 位置を押下位置として扱い、ポインターを再表示する（nudge 位置と押下位置のずれを防ぐ）
        var cursor = DragRectCursorAction.None;
        if (_state.NudgeActive)
        {
            position = _state.StartPoint;
            if (_state.EndNudge()) { cursor = DragRectCursorAction.Show; }
            NotifyAt(DragRectNoticeKind.LoupePositionMoved, position);
        }

        _pressPosition = position;
        _dragPerformed = false;
        NotifyAt(DragRectNoticeKind.MouseDown, position);

        if (clickCount >= 2)
        {
            return DoubleClick(position, cursor);
        }

        var handle = _state.HitTest(position, ToDomainRadius());
        Log.LogDragStart(handle.ToString(), position.X, position.Y);
        var feedback = new DragRectFeedback
        {
            Redraw = true,
            RequestFocus = true,
            CaptureMouse = true,
            Cursor = cursor,
        };

        if (handle != DragHandle.None)
        {
            _state.StartDrag(handle, position);

            // リサイズハンドル（8 方向）ではポインターをハンドル点へスナップし、ルーペ中心もハンドル点へ揃える
            // （ポインター・矩形の角・ルーペを一体にする）。ドラッグ基準点もハンドル点に揃えることで、
            // スナップ後の最初の移動がハンドル点からの差分として正しく矩形へ反映される。
            // 移動（Move）は掴んだ位置を維持するのが自然なためスナップしない。
            if (handle != DragHandle.Move)
            {
                var handlePosition = SelectionGeometry.GetHandlePosition(_state.SelectionRect, handle);
                _state.DragStartPoint = handlePosition;
                _state.LastMousePosition = handlePosition;
                feedback = feedback with
                {
                    Cursor = cursor | DragRectCursorAction.MoveTo,
                    CursorDomainPosition = handlePosition,
                };
                NotifyAt(DragRectNoticeKind.LoupePositionMoved, handlePosition);
            }
        }
        else
        {
            _state.StartNewSelection(position);
            NotifyAt(DragRectNoticeKind.SelectionStarted, position);
        }

        return feedback;
    }

    /// <summary>ポインター移動を通知する。</summary>
    /// <param name="position">マウス位置（ドメイン座標）。</param>
    /// <returns>UI 層が適用する効果（再描画・カーソル形状）。</returns>
    /// <remarks>
    /// 処理フロー:<br/>
    /// 1. nudge 終了直後のエコー移動は、選択矩形を上書きせずカーソル形状のみ更新する（抑制は 1 回で解除する）。<br/>
    /// 2. マウス位置を通知し、カーソル形状を更新する。<br/>
    /// 3. nudge 中は選択矩形・マウス位置を更新しない（エコー移動で nudge の結果が戻るのを防ぐ）。<br/>
    /// 4. ドラッグ中は選択矩形を更新し、変更通知とルーペ位置（動いている側のハンドル）を通知する。<br/>
    /// 5. ドラッグ中でなければホバー位置をルーペ位置として通知する。
    /// </remarks>
    public DragRectFeedback Move(EsPoint position)
    {
        var hitRadius = ToDomainRadius();

        // nudge 終了（カーソル移動）に伴うエコー移動をスキップする（消費したら解除する）
        if (_state.SuppressEchoAt is { } echoPosition && SelectionGeometry.IsNear(position, echoPosition, hitRadius))
        {
            _state.SuppressEchoAt = null;
            return new DragRectFeedback
            {
                UpdateCursorShape = true,
                CursorKind = SelectionGeometry.ToCursorKind(_state.HitTest(position, hitRadius)),
            };
        }

        NotifyAt(DragRectNoticeKind.MouseMoved, position);
        var cursor = new DragRectFeedback
        {
            UpdateCursorShape = true,
            CursorKind = SelectionGeometry.ToCursorKind(_state.HitTest(position, hitRadius)),
        };

        // nudge 中はエコー移動で選択矩形・マウス位置を上書きしない（解放時のルーペ位置が実マウス位置へ飛ぶのを防ぐ）
        if (_state.NudgeActive)
        {
            return cursor;
        }

        _state.LastMousePosition = position;

        if (_state.IsDragging)
        {
            _state.UpdateDrag(position, MinSelectionSize, ClampToDomainBounds);
            // 押下中の移動で矩形が変化した場合はドラッグ発生とする（ドラッグ移動なしの押下→解放をクリックとして検出する）
            if (_state.SelectionRect != _state.DragStartRect)
            {
                _dragPerformed = true;
            }
            NotifyState(DragRectNoticeKind.SelectionChanged);
            NotifyAt(DragRectNoticeKind.LoupePositionMoved, _state.GetMarkerPosition());
        }
        else
        {
            NotifyAt(DragRectNoticeKind.LoupePositionMoved, position);
        }

        return cursor with { Redraw = true };
    }

    /// <summary>ポインター解放（左ボタン）を通知する。</summary>
    /// <param name="position">解放位置（ドメイン座標）。</param>
    /// <returns>UI 層が適用する効果（再描画・カーソル操作）。</returns>
    /// <remarks>
    /// 処理フロー:<br/>
    /// 1. ドラッグ中だった場合はドラッグを終了し、最小サイズ未満なら選択を破棄してキャンセル、それ以外は確定を通知する。<br/>
    /// 2. ドラッグ移動なしの押下→解放だった場合はクリックを通知する。<br/>
    /// 3. nudge 中だった場合はカーソルを nudge 位置へ戻して再表示し、その位置への直後の移動（エコー）を抑制する。<br/>
    /// nudge 位置はドラッグ終了処理の前に求める（終了処理がドラッグ状態を解除するため、後から求めると固定コーナーを返す）。
    /// </remarks>
    public DragRectFeedback Release(EsPoint position)
    {
        _state.IsMouseDown = false;
        NotifyAt(DragRectNoticeKind.MouseUp, position);
        Log.LogMouseUp(position.X, position.Y, _state.IsDragging, _state.HasSelection);
        Log.LogPositionAfterUp(position.X, position.Y);

        var wasDragging = _state.IsDragging;
        var wasNudging = _state.NudgeActive;
        var pressPosition = _pressPosition;
        var nudgePosition = wasNudging ? _state.GetMarkerPosition() : default;

        if (wasDragging)
        {
            var cancelled = _state.SelectionRect.Width < MinSelectionSize
                || _state.SelectionRect.Height < MinSelectionSize;
            _state.EndDrag();

            if (cancelled)
            {
                _state.Reset();
                NotifyEmpty(DragRectNoticeKind.SelectionCancelled);
            }
            else
            {
                NotifyState(DragRectNoticeKind.SelectionCompleted);
            }
        }

        if (wasDragging && !_dragPerformed)
        {
            Log.LogClick(pressPosition.X, pressPosition.Y);
            NotifyAt(DragRectNoticeKind.MouseClicked, pressPosition);
        }

        var feedback = new DragRectFeedback { Redraw = true };
        if (wasNudging)
        {
            // nudge 中にユーザーが物理マウスを動かしても、カーソルは nudge 位置へ戻してから再表示する
            // （nudge から解放までのマウス移動はキャンセルする。SetCursorScreenPoint は再表示前に行う）
            if (_state.EndNudge()) { feedback = feedback with { Cursor = DragRectCursorAction.Show }; }
            NotifyAt(DragRectNoticeKind.LoupePositionMoved, nudgePosition);
            _state.SuppressEchoAt = nudgePosition;

            // カーソルは nudge 位置（ハンドル上）にあるため、ハンドルに応じた形状へ即時更新する
            // （マウスを動かすまで移動イベントが発火せず、形状が更新されないため）
            feedback = feedback with
            {
                Cursor = feedback.Cursor | DragRectCursorAction.MoveTo,
                CursorDomainPosition = nudgePosition,
                UpdateCursorShape = true,
                CursorKind = SelectionGeometry.ToCursorKind(_state.HitTest(nudgePosition, ToDomainRadius())),
            };
        }

        return feedback;
    }

    /// <summary>キー入力を通知する（キャンセル・確定・矢印キーの微調整）。</summary>
    /// <param name="key">押下されたキー。</param>
    /// <param name="cursorPosition">押下時点のカーソル位置（ドメイン座標）。取得できない場合は null。</param>
    /// <returns>UI 層が適用する効果（処理済み・再描画・カーソル操作）。</returns>
    /// <remarks>
    /// Escape はキャンセル要求、Enter は確定要求として扱い、常に処理済みとする。<br/>
    /// それ以外は矢印キーとして <see cref="NudgeByKey"/> へ委譲する。<br/>
    /// 「領域作成後はマウス押下中のみ nudge を許可する」条件も <see cref="NudgeByKey"/> が判断する。<br/>
    /// <b>Enter と Return は同一のキー</b>として扱う（Avalonia の <c>Key</c> は両者が同値のエイリアスのため、
    /// 名前ベースの相互変換では <c>Return</c> が得られ、UI ごとに届く値が異なる）。
    /// </remarks>
    public DragRectFeedback KeyDown(EsKey key, EsPoint? cursorPosition)
    {
        if (key == EsKey.Escape)
        {
            Log.LogKeyDown(key.ToString(), handled: true);
            return RequestCancel() with { Handled = true };
        }

        if (key is EsKey.Enter or EsKey.Return)
        {
            Log.LogKeyDown(key.ToString(), handled: true);
            return RequestConfirm() with { Handled = true };
        }

        var feedback = NudgeByKey(key, cursorPosition);
        if (feedback.Handled)
        {
            Log.LogKeyDown(key.ToString(), handled: true);
        }
        return feedback;
    }

    /// <summary>矢印キーによる微調整（nudge）を通知する。</summary>
    /// <param name="key">押下されたキー。</param>
    /// <param name="cursorPosition">押下時点のカーソル位置（ドメイン座標）。取得できない場合は null。</param>
    /// <returns>UI 層が適用する効果（処理済み・再描画・カーソル操作）。</returns>
    /// <remarks>
    /// 処理フロー:<br/>
    /// 1. 領域作成後はマウス押下中のみ許可する（ドラッグ完了後の nudge は無効）。<br/>
    /// 2. 矢印キー以外は処理しない。<br/>
    /// 3. 押下時点のカーソル位置を記録する（基点の精度向上。未移動時に基点が原点になり飛ぶのを防ぐ）。<br/>
    /// 4. 未選択・未ドラッグで nudge セッション開始時のみ基点をカーソル位置へ同期する。<br/>
    /// 5. nudge セッションを開始する（カーソル非表示・十字マーカー表示）。<br/>
    /// 6. 1 ドメインピクセル単位の整数加算で矩形を更新し、変更通知とルーペ位置を通知する。<br/>
    /// nudge 中（2 回目以降）は基点を再同期しない（座標変換の丸め誤差・スクロール追従で移動が相殺されるのを防ぐ）。
    /// </remarks>
    public DragRectFeedback NudgeByKey(EsKey key, EsPoint? cursorPosition)
    {
        if (_state.HasSelection && !_state.IsMouseDown)
        {
            return default;
        }

        var (dx, dy) = key switch
        {
            EsKey.Left => (-1.0, 0.0),
            EsKey.Right => (1.0, 0.0),
            EsKey.Up => (0.0, -1.0),
            EsKey.Down => (0.0, 1.0),
            _ => (0.0, 0.0),
        };
        if (dx == 0 && dy == 0)
        {
            return default;
        }

        if (cursorPosition is { } current)
        {
            _state.LastMousePosition = current;
        }

        // 未選択・未ドラッグの nudge では基点を現在のカーソル位置へ同期する（nudge セッションの開始時のみ）
        if (!_state.HasSelection && !_state.IsDragging && !_state.NudgeActive)
        {
            _state.StartPoint = _state.LastMousePosition;
        }

        var feedback = new DragRectFeedback { Handled = true, Redraw = true };
        if (!_state.NudgeActive)
        {
            _state.NudgeActive = true;
            _state.CrosshairVisible = true;
            _state.NudgeBase = _state.HasSelection ? _state.GetOppositeCorner(_state.SelectionRect) : _state.StartPoint;
            _state.NudgeOffsetX = 0;
            _state.NudgeOffsetY = 0;
            feedback = feedback with { Cursor = DragRectCursorAction.Hide };
        }

        // nudge 適用（ドメイン座標の整数加算のみ。浮動小数の誤差を累積しない）
        _state.NudgeOffsetX += (int)dx;
        _state.NudgeOffsetY += (int)dy;
        _state.Nudge(dx, dy, MinSelectionSize, ClampToDomainBounds);
        NotifyState(DragRectNoticeKind.SelectionChanged);

        // カーソルを動かしている側のハンドル（マーカー位置）へ合わせる
        var marker = _state.GetMarkerPosition();
        NotifyAt(DragRectNoticeKind.LoupePositionMoved, marker);
        return feedback with
        {
            Cursor = feedback.Cursor | DragRectCursorAction.MoveTo,
            CursorDomainPosition = marker,
        };
    }

    /// <summary>nudge を終了する（カーソルを再表示する）。</summary>
    /// <returns>UI 層が適用する効果（nudge 中だった場合は再描画とカーソルの再表示）。</returns>
    /// <remarks>
    /// nudge 中の状態破棄（フォーカス喪失・ビジュアルツリーからの切断）で使用する。<br/>
    /// nudge 中でない場合は何もしない（<c>default</c> を返す）。
    /// </remarks>
    public DragRectFeedback EndNudge()
        => _state.EndNudge()
            ? new DragRectFeedback { Redraw = true, Cursor = DragRectCursorAction.Show }
            : default;

    // ====================================================================
    // 外部（ホスト）からの操作
    // ====================================================================

    /// <summary>選択矩形を外部状態から復元する。</summary>
    /// <param name="rect">選択矩形（ドメイン座標）。</param>
    /// <returns>UI 層が適用する効果（再描画）。</returns>
    /// <remarks>
    /// <see cref="ClampToDomainBounds"/> が有効な場合は、はみ出す矩形を境界内へクリップしてから設定する<br/>
    /// （外部からの復元でも「画像外の選択を禁止」を尊重する）。設定後は選択変更を通知する。
    /// </remarks>
    public DragRectFeedback SetSelection(EsRect rect)
    {
        var selectionRect = ClampToDomainBounds ? _state.ClipRectToBounds(rect) : rect;
        _state.SetSelection(selectionRect);
        NotifyState(DragRectNoticeKind.SelectionChanged);
        return new DragRectFeedback { Redraw = true };
    }

    /// <summary>選択状態と nudge 状態を初期化する。</summary>
    /// <returns>UI 層が適用する効果（再描画）。</returns>
    public DragRectFeedback Reset()
    {
        _state.Reset();
        return new DragRectFeedback { Redraw = true };
    }

    /// <summary>確定要求を処理する（Enter キー・ダブルクリック・ホストからの API 呼び出しと同じ経路）。</summary>
    /// <returns>UI 層が適用する効果（確定した場合は再描画）。</returns>
    /// <remarks>
    /// ドラッグ中は無視し、選択確定後のみ確定要求を通知する（通知時点の選択矩形を含む）。<br/>
    /// 確定後は選択状態をクリアする（確定後の再ドラッグ・再確定を防ぐ）。
    /// </remarks>
    public DragRectFeedback RequestConfirm()
    {
        if (_state.IsDragging || !_state.HasSelection)
        {
            return default;
        }

        Log.LogConfirmRequested(FormatRect(_state.SelectionRect));
        NotifyState(DragRectNoticeKind.ConfirmRequested);
        _state.Reset();
        return new DragRectFeedback { Redraw = true };
    }

    /// <summary>キャンセル要求を処理する（Escape キー・枠外ダブルクリック・ホストからの API 呼び出しと同じ経路）。</summary>
    /// <returns>UI 層が適用する効果（選択を破棄した場合は再描画）。</returns>
    /// <remarks>ドラッグ中または選択ありの場合は選択を破棄した後にキャンセル要求を通知する。</remarks>
    public DragRectFeedback RequestCancel()
    {
        Log.LogCancelRequested();
        var redraw = false;
        if (_state.IsDragging || _state.HasSelection)
        {
            _state.Reset();
            redraw = true;
        }
        NotifyEmpty(DragRectNoticeKind.CancelRequested);
        return new DragRectFeedback { Redraw = redraw };
    }

    /// <summary>ドメイン境界外の選択の禁止設定の変更を現在の選択矩形へ適用する。</summary>
    /// <returns>UI 層が適用する効果（再描画）。</returns>
    /// <remarks>
    /// 設定が ON に切り替わった瞬間に、既存の選択矩形を境界内へクリップして即時反映する（ドラッグ開始を待たない）。<br/>
    /// クリップで矩形が変更された場合のみ選択変更を通知する。
    /// </remarks>
    public DragRectFeedback ApplyClampToBounds()
    {
        if (_state.ClampSelectionToBounds())
        {
            NotifyState(DragRectNoticeKind.SelectionChanged);
        }
        return new DragRectFeedback { Redraw = true };
    }

    /// <summary>ビューポート（ズーム・スクロール）の変化を通知する。</summary>
    /// <returns>UI 層が適用する効果（再描画）。</returns>
    /// <remarks>
    /// 選択矩形（ドメイン座標）はズーム・スクロールで変わらないが、上位（暗転マスク等）はビューポート座標で<br/>
    /// 再描画する必要があるため、現在の選択矩形 + スナップショットを再通知する（状態は変更しない）。<br/>
    /// 通知の組み立てはコアが単一所有するため、UI 層は呼び出すだけでよい。
    /// </remarks>
    public DragRectFeedback NotifyViewportChanged()
    {
        NotifyState(DragRectNoticeKind.ViewportChanged);
        return new DragRectFeedback { Redraw = true };
    }

    /// <summary>設定（<see cref="DragRectOptions"/>）を反映する。</summary>
    /// <param name="options">反映する設定（null 不可）。</param>
    /// <returns>UI 層が適用する効果（境界外選択の禁止が変化した場合のみ再描画）。</returns>
    /// <remarks>
    /// 1. 設定を取り込み、対話パラメータ（最小サイズ・ヒットテスト半径・境界外選択の禁止）を更新する。<br/>
    /// 2. 境界外選択の禁止が変化した場合は変更前後をログ出力し、既存の選択矩形へ即時適用する。<br/>
    /// 3. 外観（色・サイズ）はセッションが関与しない（UI 層が描画時に読む）。<br/>
    /// <br/>
    /// 同値の設定を再適用しても効果・ログを発生させない（UI 層は変更内容を確認せず委譲できる）。
    /// </remarks>
    public DragRectFeedback ApplyOptions(DragRectOptions options)
    {
        // ログと即時クリップは「境界外選択の禁止」が実際に変化した場合のみ行う
        bool clampChanged = ClampToDomainBounds != options.ClampToDomainBounds;
        Options = options;

        if (!clampChanged)
        {
            return default;
        }

        Log.LogSettingChanged(
            nameof(DragRectOptions.ClampToDomainBounds),
            (!options.ClampToDomainBounds).ToString(),
            options.ClampToDomainBounds.ToString());
        return ApplyClampToBounds();
    }

    /// <summary>対話を終了する（ビジュアルツリーからの切断・ウィンドウクローズ）。</summary>
    /// <returns>UI 層が適用する効果（再描画。nudge 中だった場合はカーソルの再表示）。</returns>
    /// <remarks>nudge を終了して選択状態を初期化する（nudge 中のポインター非表示を残留させない）。</remarks>
    public DragRectFeedback Detach()
    {
        var showed = _state.EndNudge();
        _state.Reset();
        return new DragRectFeedback
        {
            Redraw = true,
            Cursor = showed ? DragRectCursorAction.Show : DragRectCursorAction.None,
        };
    }

    // ====================================================================
    // 内部ヘルパー
    // ====================================================================

    /// <summary>ダブルクリックを処理する（選択矩形の内側なら確定、外側ならキャンセル）。</summary>
    /// <param name="position">ダブルクリック位置（ドメイン座標）。</param>
    /// <param name="cursor">適用済みのカーソル操作。</param>
    /// <returns>UI 層が適用する効果。</returns>
    /// <remarks>
    /// ドラッグ処理は行わない（ダブルクリックは確定/キャンセルの操作として扱う）。<br/>
    /// 確定した場合は <see cref="DragRectState.Reset"/> により選択を破棄するため再描画を要求する。
    /// </remarks>
    private DragRectFeedback DoubleClick(EsPoint position, DragRectCursorAction cursor)
    {
        var inside = _state.HasSelection && _state.SelectionRect.Contains(position);
        Log.LogDoubleClick(position.X, position.Y, inside);
        NotifyAt(DragRectNoticeKind.MouseDoubleClicked, position);

        if (inside)
        {
            Log.LogConfirmRequested(FormatRect(_state.SelectionRect));
            NotifyAt(DragRectNoticeKind.ConfirmRequested, position);
            _state.Reset();
            return new DragRectFeedback { Redraw = true, Cursor = cursor };
        }

        Log.LogCancelRequested();
        NotifyEmpty(DragRectNoticeKind.CancelRequested);
        return new DragRectFeedback { Cursor = cursor };
    }

    /// <summary>DIP 半径をドメイン半径へ変換する（ヒットテスト用）。</summary>
    /// <returns>ドメイン座標系でのヒットテスト半径。</returns>
    /// <remarks>
    /// Forward の差を取ることでオフセット（レターボックス等）をキャンセルし、スケールのみを得る。<br/>
    /// スケールがほぼ 0（等価変換）の場合は DIP 半径をそのまま使用する。
    /// </remarks>
    private double ToDomainRadius()
    {
        var port = _state.Port;
        var origin = port.Forward(default);
        var offset = port.Forward(new EsPoint(HandleHitRadius, 0));
        var radius = Math.Abs(offset.X - origin.X);
        return radius < 0.001 ? HandleHitRadius : radius;
    }

    /// <summary>ログ用に選択矩形を整形する。</summary>
    /// <param name="rect">選択矩形（ドメイン座標）。</param>
    /// <returns><c>(X, Y, Width, Height)</c> 形式の文字列。</returns>
    private static string FormatRect(EsRect rect) => $"({rect.X}, {rect.Y}, {rect.Width}, {rect.Height})";

    /// <summary>点座標付きの通知を発火する（スナップショットのマウス位置も指定座標とする）。</summary>
    /// <param name="kind">通知の種別。</param>
    /// <param name="position">通知する座標（ドメイン座標）。</param>
    private void NotifyAt(DragRectNoticeKind kind, EsPoint position)
        => Notified?.Invoke(this, new DragRectNotice(kind, position, _state.CreateSnapshot(position)));

    /// <summary>現在のマウス位置で通知を発火する（選択矩形の変更・確定など、通知座標を持たない通知）。</summary>
    /// <param name="kind">通知の種別。</param>
    private void NotifyState(DragRectNoticeKind kind)
        => Notified?.Invoke(this, new DragRectNotice(kind, _state.LastMousePosition, _state.CreateSnapshot()));

    /// <summary>座標を持たない通知を発火する（キャンセルなど）。</summary>
    /// <param name="kind">通知の種別。</param>
    private void NotifyEmpty(DragRectNoticeKind kind)
        => Notified?.Invoke(this, new DragRectNotice(kind, default, _state.CreateSnapshot()));
}
