using Estable.DragRect.Selection;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;

namespace Estable.DragRect;

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
/// カーソル操作（<see cref="ICursorService"/>）も抽象にのみ依存し、未指定の場合は何もしない（UI 実装側が既定実装を解決して渡す）。
/// </remarks>
internal sealed class DragRectState
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
    public DragHandle ActiveHandle { get; set; }

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

    /// <summary>カーソルサービス（nudge 時のカーソル移動・表示切替）。null はカーソル操作なし。</summary>
    public ICursorService? Cursor { get; }

    /// <summary>DragRectState を初期化する。</summary>
    /// <param name="cursor">カーソルサービス。null 可（null の場合はカーソル操作を行わない）。</param>
    public DragRectState(ICursorService? cursor)
    {
        Cursor = cursor;
    }

    /// <summary>現在の対話状態（公開イベントの Snapshot 用）を算出する。</summary>
    /// <returns>対話状態（NoSelection / StartPointNudging / Dragging / DraggingNudging / Selected / SelectedNudging）。</returns>
    /// <remarks>上位の描画モード判定用に、nudge・ドラッグ・選択の各状態を合成した状態を返す。</remarks>
    public DragRectInteractionState GetInteractionState()
    {
        // 優先順位: nudge 状態を最優先で合成し、次にドラッグ/選択状態を判定する
        if (NudgeActive)
        {
            return HasSelection ? DragRectInteractionState.SelectedNudging
                : IsDragging ? DragRectInteractionState.DraggingNudging
                : DragRectInteractionState.StartPointNudging;
        }

        return IsDragging ? DragRectInteractionState.Dragging
            : HasSelection ? DragRectInteractionState.Selected
            : DragRectInteractionState.NoSelection;
    }

    /// <summary>公開イベント用のスナップショットを生成する。</summary>
    /// <param name="mousePosition">通知時点のマウス位置（ドメイン座標）。省略時は最終位置。</param>
    /// <returns>状態・選択矩形・アクティブハンドル・マウス位置・ボタン状態・ドラッグ状態・nudge 状態を含むスナップショット。</returns>
    public DragRectSnapshot CreateSnapshot(EsPoint? mousePosition = null)
        => new(
            GetInteractionState(),
            SelectionRect,
            ActiveHandle,
            mousePosition ?? LastMousePosition,
            IsMouseDown,
            IsDragging,
            NudgeActive);

    /// <summary>選択状態と nudge 状態を初期化する。</summary>
    /// <remarks>
    /// nudge 中だった場合はマウスポインターを再表示する（非表示残留防止）。<br/>
    /// nudge の終了処理は <see cref="EndNudgeIfActive"/> に委譲する（重複排除）。
    /// </remarks>
    public void Reset()
    {
        EndNudgeIfActive();
        SelectionRect = default;
        StartPoint = default;
        LastMousePosition = default;
        IsDragging = false;
        IsMouseDown = false;
        ActiveHandle = DragHandle.None;
        DragStartRect = default;
        DragStartPoint = default;
        NudgeBase = default;
        CrosshairVisible = false;
        SuppressEchoAt = null;
    }

    /// <summary>選択矩形を外部状態から復元する。</summary>
    /// <param name="rect">選択矩形（ドメイン座標）。</param>
    /// <remarks>
    /// nudge 中だった場合はマウスポインターを再表示する（非表示残留防止）。<br/>
    /// nudge の終了処理は <see cref="EndNudgeIfActive"/> に委譲する（重複排除）。
    /// </remarks>
    public void SetSelection(EsRect rect)
    {
        EndNudgeIfActive();
        SelectionRect = rect;
        IsDragging = false;
        ActiveHandle = DragHandle.None;
    }

    /// <summary>nudge 中ならカーソルを再表示して nudge 状態を終了する。</summary>
    /// <returns>nudge 中だった場合は true。</returns>
    /// <remarks>
    /// <see cref="Reset"/> / <see cref="SetSelection"/> で共通利用する（DRY）。<br/>
    /// nudge 中（カーソル非表示）のまま状態を破棄・復元すると、カーソルが非表示のまま残るため再表示する。
    /// </remarks>
    private bool EndNudgeIfActive()
    {
        if (!NudgeActive) return false;
        NudgeActive = false;
        NudgeOffsetX = 0;
        NudgeOffsetY = 0;
        Cursor?.ShowCursor(true);
        return true;
    }

    /// <summary>指定座標がどのドラッグハンドルに当たるかを判定する。</summary>
    /// <param name="pos">判定する座標（ドメイン座標）。</param>
    /// <param name="hitRadius">ヒットテスト半径（ドメイン座標）。</param>
    /// <returns>ヒットしたハンドル種別。選択矩形が空の場合は常に <see cref="DragHandle.None"/>。</returns>
    /// <remarks>
    /// ハンドル位置は <see cref="SelectionGeometry.GetHandlePosition"/> から取得し、各ハンドルへの距離判定でヒットを確定する（ハンドル位置計算の DRY）。<br/>
    /// 判定順は「角（4 方向）→ 辺（4 方向）→ 矩形内（Move）」で、角が辺より優先される。
    /// </remarks>
    public DragHandle HitTest(EsPoint pos, double hitRadius)
    {
        var r = SelectionRect;
        if (!HasSelection) return DragHandle.None;

        // 8 方向のハンドル位置を列挙してヒット判定する（角優先 → 辺 → Move の順）
        if (SelectionGeometry.IsNear(pos, SelectionGeometry.GetHandlePosition(r, DragHandle.TopLeft), hitRadius)) return DragHandle.TopLeft;
        if (SelectionGeometry.IsNear(pos, SelectionGeometry.GetHandlePosition(r, DragHandle.TopRight), hitRadius)) return DragHandle.TopRight;
        if (SelectionGeometry.IsNear(pos, SelectionGeometry.GetHandlePosition(r, DragHandle.BottomLeft), hitRadius)) return DragHandle.BottomLeft;
        if (SelectionGeometry.IsNear(pos, SelectionGeometry.GetHandlePosition(r, DragHandle.BottomRight), hitRadius)) return DragHandle.BottomRight;
        if (SelectionGeometry.IsNear(pos, SelectionGeometry.GetHandlePosition(r, DragHandle.TopCenter), hitRadius)) return DragHandle.TopCenter;
        if (SelectionGeometry.IsNear(pos, SelectionGeometry.GetHandlePosition(r, DragHandle.BottomCenter), hitRadius)) return DragHandle.BottomCenter;
        if (SelectionGeometry.IsNear(pos, SelectionGeometry.GetHandlePosition(r, DragHandle.MiddleLeft), hitRadius)) return DragHandle.MiddleLeft;
        if (SelectionGeometry.IsNear(pos, SelectionGeometry.GetHandlePosition(r, DragHandle.MiddleRight), hitRadius)) return DragHandle.MiddleRight;
        if (r.Contains(pos)) return DragHandle.Move;
        return DragHandle.None;
    }

    /// <summary>新規選択のドラッグを開始する。</summary>
    /// <param name="startPos">始点座標（ドメイン座標）。</param>
    public void StartNewSelection(EsPoint startPos)
    {
        StartPoint = startPos;
        SelectionRect = new EsRect(startPos.X, startPos.Y, 0, 0);
        IsDragging = true;
        ActiveHandle = DragHandle.None;
        DragStartPoint = startPos;
        DragStartRect = SelectionRect;
        LastMousePosition = startPos;
    }

    /// <summary>既存ハンドルによるリサイズ/移動のドラッグを開始する。</summary>
    /// <param name="handle">アクティブにするハンドル。</param>
    /// <param name="pos">現在のマウス位置（ドメイン座標）。</param>
    public void StartDrag(DragHandle handle, EsPoint pos)
    {
        ActiveHandle = handle;
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

        if (ActiveHandle != DragHandle.None)
        {
            // ハンドルによるリサイズ/移動（変形・クリップは SelectionGeometry へ委譲・単一所有）
            double dx = currentPos.X - DragStartPoint.X;
            double dy = currentPos.Y - DragStartPoint.Y;
            var rect = SelectionGeometry.ComputeDragRect(DragStartRect, dx, dy, ActiveHandle, minSize);
            SelectionRect = bounds is { } b ? SelectionGeometry.ClampRectToBounds(rect, b, ActiveHandle) : rect;
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
        ActiveHandle = DragHandle.None;
    }

    /// <summary>キーボード微調整（カーソルキー 1 回分）を適用する。</summary>
    /// <param name="dx">X 方向の移動量（ドメイン座標）。</param>
    /// <param name="dy">Y 方向の移動量（ドメイン座標）。</param>
    /// <param name="minSize">選択矩形の最小サイズ。</param>
    /// <param name="clampToBounds">ドメイン境界内にクリップするかどうか。</param>
    /// <remarks>
    /// 状態に応じて動作が変化する:<br/>
    /// 1. ハンドル選択中（<see cref="ActiveHandle"/> 有効）: ハンドル方向にリサイズする。<br/>
    /// 2. ドラッグ中（<see cref="IsDragging"/>）: 終点を移動する（新規選択の延長線上）。<br/>
    /// 3. 選択確定後（<see cref="HasSelection"/>）: 終点（StartPoint でない方の隅）を移動する。<br/>
    /// 4. 未選択: 始点を移動する（次回クリック時の始点となる）。
    /// </remarks>
    public void Nudge(double dx, double dy, double minSize, bool clampToBounds)
    {
        var bounds = GetBoundsRect(clampToBounds);

        if (ActiveHandle != DragHandle.None)
        {
            // ハンドルドラッグ中 → ハンドル方向にリサイズ（変形・クリップは SelectionGeometry へ委譲・単一所有）
            var rect = SelectionGeometry.ComputeDragRect(SelectionRect, dx, dy, ActiveHandle, minSize);
            SelectionRect = bounds is { } b ? SelectionGeometry.ClampRectToBounds(rect, b, ActiveHandle) : rect;
            // ドラッグ基準を nudge 後の状態に合わせる（次の UpdateDrag で nudge が失われるのを防ぐ）
            DragStartRect = SelectionRect;
            DragStartPoint = SelectionGeometry.GetHandlePosition(SelectionRect, ActiveHandle);
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

    /// <summary>十字マーカー・ルーペが追従する位置（ドメイン座標）を取得する。</summary>
    /// <returns>ドラッグ中のハンドル位置（ドメイン座標）。</returns>
    /// <remarks>
    /// ドラッグ・nudge はカーソルを動かしながら矩形を変形するため、十字マーカー・ルーペは「動いている側のハンドル」を追従する:<br/>
    /// 1. ハンドル操作中（リサイズ）: アクティブハンドルの現在位置。<br/>
    /// 2. ハンドル操作中（移動）: 掴んだ位置（マウス位置）を追従する（矩形中心へ飛ばないようにする）。<br/>
    /// 3. 新規選択ドラッグ中・nudge: 終点（クリップ・最小サイズ保証後の矩形の角）。<br/>
    /// 4. 未選択（始点決め nudge）: 始点。<br/>
    /// 固定コーナー（<see cref="GetOppositeCorner"/>）ではなく動いている側を返すことで、マーカー・ルーペが固定位置に留まる問題を解消する。
    /// </remarks>
    public EsPoint GetMarkerPosition()
    {
        // ハンドル操作中:
        // - リサイズ（8 方向）: ドラッグ中のハンドル位置（ポインタ・矩形の角・ルーペを一体にする）
        // - 移動（Move）: 掴んだ位置（マウス位置）を追従（ルーペが矩形中心に飛ばず、マウスポインタに追従する）
        if (ActiveHandle != DragHandle.None)
        {
            return ActiveHandle == DragHandle.Move
                ? LastMousePosition
                : SelectionGeometry.GetHandlePosition(SelectionRect, ActiveHandle);
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
