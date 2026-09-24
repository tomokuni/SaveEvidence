using Estable.Marquee.Selection;
using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;

namespace Estable.Marquee;

/// <summary>選択・ドラッグ・nudge の状態を保持し、純粋な更新ロジックを提供する内部クラス。</summary>
/// <remarks>
/// 選択矩形（ドメイン座標）・ドラッグ状態・アクティブハンドル・nudge 状態（基点＋累積オフセット）を単一クラスに集約する。<br/>
/// <br/>
/// 座標系:<br/>
/// - 選択矩形・nudge はドメイン座標（Forward = UI 論理座標→ドメイン / Inverse = ドメイン→UI 論理座標）で動作する。<br/>
/// - マウス入力・描画は各 UI 実装が UI 論理座標で受け取り、座標変換ポート（<see cref="ICoordTransform"/>）で変換する。<br/>
/// <br/>
/// イベント駆動: 状態変更は UI 実装側のコントロールが公開イベントで通知し、本クラスは状態の保持と更新ロジックのみを担う（ポーリング不要）。<br/>
/// <b>依存分離</b>: コア型（<see cref="EsRect"/> / <see cref="EsPoint"/>）のみに依存し、UI フレームワークへ依存しない。<br/>
/// カーソルの表示切替（nudge 中の非表示・終了時の再表示）は<b>効果</b>としてセッションが UI 層へ要求するため、本クラスはカーソル API を知らない。<br/>
/// <b>可視性</b>: 本クラスは <see cref="MarqueeSession"/> の内部実装であり、外部が直接操作するとセッションの不変条件が壊れる<br/>
/// （可変プロパティを多数持つ）。外部は <see cref="MarqueeSnapshot"/> を通して状態を読む。
/// </remarks>
internal sealed class MarqueeState
{
    /// <summary>選択矩形（ドメイン座標）。未選択時は 0x0。</summary>
    public EsRect SelectionRect { get; set; }

    /// <summary>選択矩形が有効かどうか（幅・高さが 0 より大きい）。</summary>
    public bool HasSelection => SelectionRect.Width > 0 && SelectionRect.Height > 0;

    /// <summary>ドラッグの始点（ドメイン座標）。</summary>
    public EsPoint StartPoint { get; set; }

    /// <summary>直近のマウス位置（ドメイン座標）。nudge の基点精度向上に使用。</summary>
    public EsPoint LastMousePosition { get; set; }

    /// <summary>ドラッグ中かどうか（新規選択・移動・リサイズ）。</summary>
    public bool IsDragging { get; set; }

    /// <summary>マウスボタン（左）が押下中かどうか。</summary>
    public bool IsMouseDown { get; set; }

    /// <summary>アクティブなドラッグハンドル（リサイズ中）。None は新規選択・移動。</summary>
    public HitTarget ActiveTarget { get; set; }

    /// <summary>ドラッグ開始時の矩形（移動・リサイズの基準）。</summary>
    public EsRect DragStartRect { get; set; }

    /// <summary>ドラッグ開始時のマウス位置（移動・リサイズの基準・ドメイン座標）。</summary>
    public EsPoint DragStartPoint { get; set; }

    /// <summary>nudge がアクティブかどうか。</summary>
    public bool NudgeActive { get; set; }

    /// <summary>nudge の基点（ドメイン座標）。</summary>
    public EsPoint NudgeBase { get; set; }

    /// <summary>nudge の累積オフセット X（ドメイン座標・整数加算）。</summary>
    public int NudgeOffsetX { get; set; }

    /// <summary>nudge の累積オフセット Y（ドメイン座標・整数加算）。</summary>
    public int NudgeOffsetY { get; set; }

    /// <summary>nudge 中にマウスカーソルを非表示にするかどうか。</summary>
    public bool CrosshairVisible { get; set; }

    /// <summary>座標変換ポート（Forward = UI 論理座標→ドメイン / Inverse = ドメイン→UI 論理座標 / Bounds = ドメイン境界）。</summary>
    public ICoordTransform Port { get; set; } = CoordTransform.Identity;

    /// <summary>nudge 終了（カーソル位置移動）直後のエコー移動イベントの抑制位置（ドメイン座標）。null は抑制なし。</summary>
    public EsPoint? SuppressEchoAt { get; set; }

    /// <summary>現在の対話状態（公開イベントの Snapshot 用）を算出する。</summary>
    /// <returns>対話状態（NoSelection / StartPointNudging / Dragging / DraggingNudging / Selected / SelectedNudging）。</returns>
    /// <remarks>上位の描画モード判定用に、nudge・ドラッグ・選択の各状態を合成した状態を返す。</remarks>
    public MarqueeInteractionState GetInteractionState()
    {
        // 優先順位: nudge 状態を最優先で合成し、次にドラッグ/選択状態を判定する
        if (NudgeActive)
        {
            return HasSelection ? MarqueeInteractionState.SelectedNudging
                : IsDragging ? MarqueeInteractionState.DraggingNudging
                : MarqueeInteractionState.StartPointNudging;
        }

        return IsDragging ? MarqueeInteractionState.Dragging
            : HasSelection ? MarqueeInteractionState.Selected
            : MarqueeInteractionState.NoSelection;
    }

    /// <summary>公開イベント用のスナップショットを生成する。</summary>
    /// <param name="mousePosition">通知時点のマウス位置（ドメイン座標）。省略時は最終位置。</param>
    /// <returns>状態・選択矩形・アクティブハンドル・マウス位置・ボタン状態・ドラッグ状態・nudge 状態を含むスナップショット。</returns>
    public MarqueeSnapshot CreateSnapshot(EsPoint? mousePosition = null)
        => new(
            GetInteractionState(),
            SelectionRect,
            ActiveTarget,
            mousePosition ?? LastMousePosition,
            IsMouseDown,
            IsDragging,
            NudgeActive);

    /// <summary>選択状態と nudge 状態を初期化する。</summary>
    /// <remarks>
    /// nudge 中だった場合は nudge 状態を終了する（カーソルの再表示はセッションが効果として要求する）。<br/>
    /// nudge の終了処理は <see cref="EndNudge"/> に委譲する（重複排除）。
    /// </remarks>
    public void Reset()
    {
        EndNudge();
        SelectionRect = default;
        StartPoint = default;
        LastMousePosition = default;
        IsDragging = false;
        IsMouseDown = false;
        ActiveTarget = HitTarget.None;
        DragStartRect = default;
        DragStartPoint = default;
        NudgeBase = default;
        CrosshairVisible = false;
        SuppressEchoAt = null;
    }

    /// <summary>選択矩形を外部状態から復元する。</summary>
    /// <param name="rect">選択矩形（ドメイン座標）。</param>
    /// <remarks>
    /// nudge 中だった場合は nudge 状態を終了する（カーソルの再表示はセッションが効果として要求する）。<br/>
    /// nudge の終了処理は <see cref="EndNudge"/> に委譲する（重複排除）。
    /// </remarks>
    public void SetSelection(EsRect rect)
    {
        EndNudge();
        SelectionRect = rect;
        IsDragging = false;
        ActiveTarget = HitTarget.None;
    }

    /// <summary>nudge 状態を終了する。</summary>
    /// <returns>nudge 中だった場合は true（カーソルの再表示が必要かどうかの判定材料）。</returns>
    /// <remarks>
    /// 本メソッドは状態のみを更新する（カーソルの再表示は効果としてセッションが UI 層へ要求する）。<br/>
    /// 戻り値は「nudge 中だった」の報告であり、本クラス内（<see cref="Reset"/> / <see cref="SetSelection"/>）と、
    /// 入力の判断を担う状態管理クラスの双方から利用する（DRY）。
    /// </remarks>
    public bool EndNudge()
    {
        if (!NudgeActive) return false;
        NudgeActive = false;
        NudgeOffsetX = 0;
        NudgeOffsetY = 0;
        return true;
    }

    /// <summary>指定座標がどのドラッグハンドルに当たるかを判定する。</summary>
    /// <param name="pos">判定する座標（ドメイン座標）。</param>
    /// <param name="hitRadius">ヒットテスト半径（ドメイン座標）。</param>
    /// <returns>ヒットしたハンドル種別。選択矩形が空の場合は常に <see cref="HitTarget.None"/>。</returns>
    /// <remarks>
    /// ハンドル位置は <see cref="SelectionGeometry.GetHandlePosition"/> から取得し、各ハンドルへの距離判定でヒットを確定する（ハンドル位置計算の DRY）。<br/>
    /// 判定順は「角（4 方向）→ 辺（4 方向）→ 矩形内（Move）」で、角が辺より優先される。
    /// </remarks>
    public HitTarget HitTest(EsPoint pos, double hitRadius)
    {
        var r = SelectionRect;
        if (!HasSelection) return HitTarget.None;

        // 8 方向のハンドル位置を列挙してヒット判定する（角優先 → 辺 → Move の順）
        if (SelectionGeometry.IsNear(pos, SelectionGeometry.GetHandlePosition(r, HitTarget.TopLeft), hitRadius)) return HitTarget.TopLeft;
        if (SelectionGeometry.IsNear(pos, SelectionGeometry.GetHandlePosition(r, HitTarget.TopRight), hitRadius)) return HitTarget.TopRight;
        if (SelectionGeometry.IsNear(pos, SelectionGeometry.GetHandlePosition(r, HitTarget.BottomLeft), hitRadius)) return HitTarget.BottomLeft;
        if (SelectionGeometry.IsNear(pos, SelectionGeometry.GetHandlePosition(r, HitTarget.BottomRight), hitRadius)) return HitTarget.BottomRight;
        if (SelectionGeometry.IsNear(pos, SelectionGeometry.GetHandlePosition(r, HitTarget.TopCenter), hitRadius)) return HitTarget.TopCenter;
        if (SelectionGeometry.IsNear(pos, SelectionGeometry.GetHandlePosition(r, HitTarget.BottomCenter), hitRadius)) return HitTarget.BottomCenter;
        if (SelectionGeometry.IsNear(pos, SelectionGeometry.GetHandlePosition(r, HitTarget.MiddleLeft), hitRadius)) return HitTarget.MiddleLeft;
        if (SelectionGeometry.IsNear(pos, SelectionGeometry.GetHandlePosition(r, HitTarget.MiddleRight), hitRadius)) return HitTarget.MiddleRight;
        if (r.Contains(pos)) return HitTarget.Move;
        return HitTarget.None;
    }

    /// <summary>新規選択のドラッグを開始する。</summary>
    /// <param name="startPos">始点座標（ドメイン座標）。</param>
    public void StartNewSelection(EsPoint startPos)
    {
        StartPoint = startPos;
        SelectionRect = new EsRect(startPos.X, startPos.Y, 0, 0);
        IsDragging = true;
        ActiveTarget = HitTarget.None;
        DragStartPoint = startPos;
        DragStartRect = SelectionRect;
        LastMousePosition = startPos;
    }

    /// <summary>既存ハンドルによるリサイズ/移動のドラッグを開始する。</summary>
    /// <param name="handle">アクティブにするハンドル。</param>
    /// <param name="pos">現在のマウス位置（ドメイン座標）。</param>
    public void StartDrag(HitTarget handle, EsPoint pos)
    {
        ActiveTarget = handle;
        IsDragging = true;
        DragStartPoint = pos;
        DragStartRect = SelectionRect;
        LastMousePosition = pos;
    }

    /// <summary>ドラッグ中のマウス移動を処理する。</summary>
    /// <param name="currentPos">現在のマウス位置（ドメイン座標）。</param>
    /// <param name="minSize">選択矩形の最小サイズ。</param>
    /// <param name="clampToBounds">ドメイン境界内にクリップするかどうか。</param>
    /// <remarks>
    /// クリップ有効時（<paramref name="clampToBounds"/>）はドメイン境界（<see cref="Port"/> の Bounds）内に収める:<br/>
    /// 1. ハンドル変形: 固定角を動かさず、動く辺のみ境界内に制限する。<br/>
    /// 2. 移動（Move）: 矩形全体が境界内に収まるようにシフトする。<br/>
    /// 3. 新規選択: 始点・終点の両方を境界内にクリップしてから矩形を作成する（始点が画像外の場合にはみ出さない）。<br/>
    /// </remarks>
    public void UpdateDrag(EsPoint currentPos, double minSize, bool clampToBounds)
    {
        var bounds = GetBoundsRect(clampToBounds);

        if (ActiveTarget != HitTarget.None)
        {
            // ハンドルによるリサイズ/移動（変形・クリップは SelectionGeometry へ委譲・単一所有）
            double dx = currentPos.X - DragStartPoint.X;
            double dy = currentPos.Y - DragStartPoint.Y;
            var rect = SelectionGeometry.ComputeDragRect(DragStartRect, dx, dy, ActiveTarget, minSize);
            SelectionRect = bounds is { } b ? SelectionGeometry.ClampRectToBounds(rect, b, ActiveTarget) : rect;
        }
        else if (IsDragging)
        {
            // 新規選択（始点→終点）: 始点・終点の両方を境界内にクリップしてから矩形を作成する
            // （始点が画像外（レターボックス等）で押下された場合でも、矩形が画像外にはみ出さないようにする）
            var start = bounds is { } b ? SelectionGeometry.ClampPointToBounds(StartPoint, b) : StartPoint;
            var end = bounds is { } b2 ? SelectionGeometry.ClampPointToBounds(currentPos, b2) : currentPos;
            SelectionRect = SelectionGeometry.CreateFromPoints(start, end);
        }
        LastMousePosition = currentPos;
    }

    /// <summary>ドメイン境界の矩形を取得する（クリップ有効時のみ）。</summary>
    /// <param name="clampToBounds">ドメイン境界内にクリップするかどうか。</param>
    /// <returns>ドメイン境界の矩形。クリップ無効または境界未設定（null）の場合は null。</returns>
    /// <remarks><see cref="UpdateDrag"/> / <see cref="Nudge"/> で共通利用する（境界 Rect 生成の重複排除）。</remarks>
    private EsRect? GetBoundsRect(bool clampToBounds)
        => clampToBounds && Port.Bounds is { } size ? new EsRect(0, 0, size.Width, size.Height) : null;

    /// <summary>ドラッグを終了する。</summary>
    public void EndDrag()
    {
        IsDragging = false;
        ActiveTarget = HitTarget.None;
    }

    /// <summary>キーボード微調整（カーソルキー 1 回分）を適用する。</summary>
    /// <param name="dx">X 方向の移動量（ドメイン座標）。</param>
    /// <param name="dy">Y 方向の移動量（ドメイン座標）。</param>
    /// <param name="minSize">選択矩形の最小サイズ。</param>
    /// <param name="clampToBounds">ドメイン境界内にクリップするかどうか。</param>
    /// <remarks>
    /// 状態に応じて動作が変化する:<br/>
    /// 1. ハンドル選択中（<see cref="ActiveTarget"/> 有効）: ハンドル方向にリサイズする。<br/>
    /// 2. ドラッグ中（<see cref="IsDragging"/>）: 終点を移動する（新規選択の延長線上）。<br/>
    /// 3. 選択確定後（<see cref="HasSelection"/>）: 終点（StartPoint でない方の隅）を移動する。<br/>
    /// 4. 未選択: 始点を移動する（次回クリック時の始点となる）。
    /// </remarks>
    public void Nudge(double dx, double dy, double minSize, bool clampToBounds)
    {
        var bounds = GetBoundsRect(clampToBounds);

        if (ActiveTarget != HitTarget.None)
        {
            // ハンドルドラッグ中 → ハンドル方向にリサイズ（変形・クリップは SelectionGeometry へ委譲・単一所有）
            var rect = SelectionGeometry.ComputeDragRect(SelectionRect, dx, dy, ActiveTarget, minSize);
            SelectionRect = bounds is { } b ? SelectionGeometry.ClampRectToBounds(rect, b, ActiveTarget) : rect;
            // ドラッグ基準を nudge 後の状態に合わせる（次の UpdateDrag で nudge が失われるのを防ぐ）
            DragStartRect = SelectionRect;
            DragStartPoint = SelectionGeometry.GetHandlePosition(SelectionRect, ActiveTarget);
            LastMousePosition = DragStartPoint;
        }
        else if (IsDragging || HasSelection)
        {
            // 新規選択ドラッグ中 / 選択確定後 → 終点を微調整
            var end = SelectionGeometry.GetOppositeCorner(SelectionRect, StartPoint);
            end = new EsPoint(end.X + dx, end.Y + dy);
            end = bounds is { } b ? SelectionGeometry.ClampPointToBounds(end, b) : end;
            // 始点も境界内にクリップしてから矩形を作成する（画像外の始点によるはみ出し防止）
            var start = bounds is { } b2 ? SelectionGeometry.ClampPointToBounds(StartPoint, b2) : StartPoint;
            LastMousePosition = end;
            SelectionRect = SelectionGeometry.CreateFromPoints(start, end);
        }
        else
        {
            // 未選択・未ドラッグ → 始点を移動（次回クリック時の始点となる）
            StartPoint = new EsPoint(StartPoint.X + dx, StartPoint.Y + dy);
        }
    }

    /// <summary>選択矩形の「StartPoint でない方の隅」（終点）を取得する。</summary>
    /// <param name="rect">選択矩形。</param>
    /// <returns>StartPoint と反対側の隅の座標。</returns>
    public EsPoint GetOppositeCorner(EsRect rect)
        => SelectionGeometry.GetOppositeCorner(rect, StartPoint);

    /// <summary>十字マーカーが追従する位置（ドメイン座標）を取得する。</summary>
    /// <returns>ドラッグ中のハンドル位置（ドメイン座標）。</returns>
    /// <remarks>
    /// ドラッグ・nudge はカーソルを動かしながら矩形を変形するため、十字マーカーは「動いている側のハンドル」を追従する:<br/>
    /// 1. ハンドル操作中（リサイズ）: アクティブハンドルの現在位置。<br/>
    /// 2. ハンドル操作中（移動）: 掴んだ位置（マウス位置）を追従する（矩形中心へ飛ばないようにする）。<br/>
    /// 3. 新規選択ドラッグ中・nudge: 終点（クリップ・最小サイズ保証後の矩形の角）。<br/>
    /// 4. 未選択（始点決め nudge）: 始点。<br/>
    /// 固定コーナー（<see cref="GetOppositeCorner"/>）ではなく動いている側を返すことで、マーカーが固定位置に留まる問題を解消する。
    /// </remarks>
    public EsPoint GetMarkerPosition()
    {
        // ハンドル操作中:
        // - リサイズ（8 方向）: ドラッグ中のハンドル位置（ポインタ・矩形の角・追従位置を一体にする）
        // - 移動（Move）: 掴んだ位置（マウス位置）を追従（追従位置が矩形中心に飛ばず、マウスポインタに追従する）
        if (ActiveTarget != HitTarget.None)
        {
            return ActiveTarget == HitTarget.Move
                ? LastMousePosition
                : SelectionGeometry.GetHandlePosition(SelectionRect, ActiveTarget);
        }

        // 新規選択ドラッグ中・nudge: 終点（クリップ後の矩形の角。nudge で追従）
        if (IsDragging)
        {
            return GetOppositeCorner(SelectionRect);
        }

        // 未選択（始点決め nudge）: 始点
        return StartPoint;
    }

    /// <summary>現在の選択矩形をドメイン境界（<see cref="Port"/> の Bounds）内にクリップして適用する。</summary>
    /// <returns>クリップによって矩形が変更された場合は true。</returns>
    /// <remarks>
    /// 「画像外の選択を禁止」が ON に切り替わった瞬間や、外部から復元した矩形が画像外にはみ出している場合に適用する。<br/>
    /// 純粋計算の <see cref="ClipRectToBounds"/> に委譲し、変更があった場合のみ状態へ適用する（純粋計算と状態適用の分離）。
    /// </remarks>
    public bool ClampSelectionToBounds()
    {
        if (!HasSelection) return false;
        var clipped = ClipRectToBounds(SelectionRect);
        if (clipped == SelectionRect) return false;
        SelectionRect = clipped;
        return true;
    }

    /// <summary>指定した矩形をドメイン境界（<see cref="Port"/> の Bounds）内にクリップする。</summary>
    /// <param name="rect">クリップする矩形（ドメイン座標）。</param>
    /// <returns>境界内に収めた矩形。境界が未設定（null）の場合は元の矩形。</returns>
    /// <remarks>
    /// 矩形の四隅を境界内にクリップし、その包絡矩形（bounding box）を返す。<br/>
    /// 矩形全体を境界内に収める（<see cref="SetSelection"/> 等の外部復元・設定変更時の即時適用用）。<br/>
    /// クリップ計算は <see cref="SelectionGeometry.ClampRectToDomainBounds"/> へ委譲する（単一所有・DRY）。
    /// </remarks>
    public EsRect ClipRectToBounds(EsRect rect)
        => Port.Bounds is { } size
            ? SelectionGeometry.ClampRectToDomainBounds(rect, size)
            : rect;
}
