using Avalonia;
using Estable.AvaloniaUI.DragRect.Selection;
using Estable.AvaloniaUI.Platform;
using Estable.AvaloniaUI.Platform.Geometry;

namespace Estable.AvaloniaUI.DragRect;

/// <summary>DragRectControl の選択・nudge 状態を保持する内部クラス。</summary>
/// <remarks>
/// 選択矩形（ドメイン座標）・ドラッグ状態・アクティブハンドル・nudge 状態（基点＋累積オフセット）を
/// 単一クラスに集約する（既存の SelectionService / NudgeContext の 10 クラス分割を 1 クラスに統合）。<br/>
/// <br/>
/// 座標系:<br/>
/// - 選択矩形・nudge はドメイン座標（Forward = DIP→ドメイン / Inverse = ドメイン→DIP）で動作する。<br/>
/// - マウス入力・描画はコントロールローカル DIP で受け取り、座標変換ポート（<see cref="ICoordTransform"/>）で変換する。<br/>
/// <br/>
/// イベント駆動: 状態変更はコントロール（<see cref="DragRectControl"/>）が公開イベントで通知し、
/// 本クラスは状態の保持と純粋な更新ロジックのみを担う（ポーリング不要）。<br/>
/// <b>依存分離</b>: ドメイン座標は UI フレームワーク非依存の <see cref="Point"/> / <see cref="Rect"/> で保持する。<br/>
/// 公開イベント引数（<see cref="DragRectSnapshot"/> 等）は UI 境界のため、生成時に <see cref="AvaloniaGeometryAdapters"/> で変換する。
/// </remarks>
internal sealed class DragRectState
{
    /// <summary>選択矩形（ドメイン座標）。未選択時は 0x0。</summary>
    public Rect SelectionRect { get; set; }

    /// <summary>選択矩形が有効かどうか（幅・高さが 0 より大きい）。</summary>
    public bool HasSelection => SelectionRect.Width > 0 && SelectionRect.Height > 0;

    /// <summary>ドラッグの始点（ドメイン座標）。</summary>
    public Point StartPoint { get; set; }

    /// <summary>直近のマウス位置（ドメイン座標）。nudge の基点精度向上に使用。</summary>
    public Point LastMousePosition { get; set; }

    /// <summary>ドラッグ中かどうか（新規選択・移動・リサイズ）。</summary>
    public bool IsDragging { get; set; }

    /// <summary>マウスボタン（左）が押下中かどうか。</summary>
    public bool IsMouseDown { get; set; }

    /// <summary>アクティブなドラッグハンドル（リサイズ中）。None は新規選択・移動。</summary>
    public DragHandle ActiveHandle { get; set; }

    /// <summary>ドラッグ開始時の矩形（移動・リサイズの基準）。</summary>
    public Rect DragStartRect { get; set; }

    /// <summary>ドラッグ開始時のマウス位置（移動・リサイズの基準・ドメイン座標）。</summary>
    public Point DragStartPoint { get; set; }

    /// <summary>nudge がアクティブかどうか。</summary>
    public bool NudgeActive { get; set; }

    /// <summary>nudge の基点（ドメイン座標）。</summary>
    public Point NudgeBase { get; set; }

    /// <summary>nudge の累積オフセット X（ドメイン座標・整数加算）。</summary>
    public int NudgeOffsetX { get; set; }

    /// <summary>nudge の累積オフセット Y（ドメイン座標・整数加算）。</summary>
    public int NudgeOffsetY { get; set; }

    /// <summary>nudge 中にマウスカーソルを非表示にするかどうか（仕様 4.4）。</summary>
    public bool CrosshairVisible { get; set; }

    /// <summary>座標変換ポート（Forward = DIP→ドメイン / Inverse = ドメイン→DIP / Bounds = ドメイン境界）。</summary>
    public ICoordTransform Port { get; set; } = CoordTransform.Identity;

    /// <summary>nudge 終了（SetCursorPos）直後のエコー PointerMoved の抑制位置（ドメイン座標）。null は抑制なし。</summary>
    public Point? SuppressEchoAt { get; set; }

    /// <summary>カーソルサービス（nudge 時のカーソル移動・表示切替）。</summary>
    public ICursorService Cursor { get; }

    /// <summary>DragRectState を初期化する。</summary>
    /// <param name="cursor">カーソルサービス。</param>
    public DragRectState(ICursorService? cursor)
    {
        Cursor = cursor ?? PlatformFactory.CreateCursorService();
    }

    /// <summary>現在の対話状態（公開イベントの Snapshot 用）を算出する。</summary>
    /// <returns>対話状態（NoSelection / StartPointNudging / Dragging / DraggingNudging / Selected / SelectedNudging）。</returns>
    /// <remarks>既存の <see cref="DragRectInteractionState"/> と互換の合成状態を返す（上位の描画モード判定用）。</remarks>
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
    public DragRectSnapshot CreateSnapshot(Point? mousePosition = null)
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
    public void SetSelection(Rect rect)
    {
        EndNudgeIfActive();
        SelectionRect = rect;
        IsDragging = false;
        ActiveHandle = DragHandle.None;
    }

    /// <summary>nudge 中ならカーソルを再表示して nudge 状態を終了する。</summary>
    /// <returns>nudge 中だった場合は <see langword="true"/>。</returns>
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
        Cursor.ShowCursor(true);
        return true;
    }

    /// <summary>指定座標がどのドラッグハンドルに当たるかを判定する。</summary>
    /// <param name="pos">判定する座標（ドメイン座標）。</param>
    /// <param name="hitRadius">ヒットテスト半径（ドメイン座標）。</param>
    /// <returns>ヒットしたハンドル種別。選択矩形が空の場合は常に <see cref="DragHandle.None"/>。</returns>
    /// <remarks>
    /// ハンドル位置は <see cref="SelectionGeometry.GetHandlePosition"/> から取得し、
    /// 各ハンドルへの距離判定でヒットを確定する（ハンドル位置計算の DRY・二重所有の排除）。<br/>
    /// 判定順は「角（8 方向）→ 矩形内（Move）」で、角が辺より優先される（従来の優先度を維持）。
    /// </remarks>
    public DragHandle HitTest(Point pos, double hitRadius)
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
    public void StartNewSelection(Point startPos)
    {
        StartPoint = startPos;
        SelectionRect = new Rect(startPos.X, startPos.Y, 0, 0);
        IsDragging = true;
        ActiveHandle = DragHandle.None;
        DragStartPoint = startPos;
        DragStartRect = SelectionRect;
        LastMousePosition = startPos;
    }

    /// <summary>既存ハンドルによるリサイズ/移動のドラッグを開始する。</summary>
    /// <param name="handle">アクティブにするハンドル。</param>
    /// <param name="pos">現在のマウス位置（ドメイン座標）。</param>
    public void StartDrag(DragHandle handle, Point pos)
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
    /// クリップ有効時（<paramref name="clampToBounds"/>）はドメイン境界（<see cref="Port.Bounds"/>）内に収める:<br/>
    /// - 新規選択: 始点・終点の両方を境界内にクリップしてから矩形を作成（始点が画像外の場合にはみ出さない）。<br/>
    /// - ハンドル変形: 固定角を動かさず、動く辺のみ境界内に制限。<br/>
    /// - 移動（Move）: 矩形全体が境界内に収まるようにシフト。<br/>
    /// </remarks>
    public void UpdateDrag(Point currentPos, double minSize, bool clampToBounds)
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
    /// <remarks>
    /// <see cref="UpdateDrag"/> / <see cref="Nudge"/> で共通利用する（境界 Rect 生成の重複排除）。
    /// </remarks>
    private Rect? GetBoundsRect(bool clampToBounds)
        => clampToBounds && Port.Bounds is { } size ? new Rect(0, 0, size.Width, size.Height) : null;

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
    /// - ハンドル選択中（<see cref="ActiveHandle"/> 有効）: ハンドル方向にリサイズ。<br/>
    /// - ドラッグ中（<see cref="IsDragging"/>）: 終点を移動（新規選択の延長線上）。<br/>
    /// - 選択確定後（<see cref="HasSelection"/>）: 終点（StartPoint でない方の隅）を移動。<br/>
    /// - 未選択: 始点を移動（次回クリック時の始点となる）。
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
            end = new Point(end.X + dx, end.Y + dy);
            end = bounds is { } b ? SelectionGeometry.ClampPointToBounds(end, b) : end;
            // 始点も境界内にクリップしてから矩形を作成する（画像外の始点によるはみ出し防止）
            var start = bounds is { } b2 ? SelectionGeometry.ClampPointToBounds(StartPoint, b2) : StartPoint;
            LastMousePosition = end;
            SelectionRect = SelectionGeometry.CreateFromPoints(start, end);
        }
        else
        {
            // 未選択・未ドラッグ → 始点を移動（次回クリック時の始点となる）
            StartPoint = new Point(StartPoint.X + dx, StartPoint.Y + dy);
        }
    }

    /// <summary>選択矩形の「StartPoint でない方の隅」（終点）を取得する。</summary>
    /// <param name="rect">選択矩形。</param>
    /// <returns>StartPoint と反対側の隅の座標。</returns>
    public Point GetOppositeCorner(Rect rect)
        => SelectionGeometry.GetOppositeCorner(rect, StartPoint);

    /// <summary>十字マーカー・ルーペが追従する位置（ドメイン座標）を取得する。</summary>
    /// <returns>ドラッグ中のハンドル位置（ドメイン座標）。</returns>
    /// <remarks>
    /// ドラッグ・nudge はカーソルを動かしながら矩形を変形するため、十字マーカー・ルーペは「動いている側のハンドル」を追従する:<br/>
    /// - ハンドル操作中（リサイズ・移動）: アクティブハンドルの現在位置（移動は矩形中心）。<br/>
    /// - 新規選択ドラッグ中・nudge: 終点（クリップ・最小サイズ保証後の矩形の角）。<br/>
    /// - 未選択（始点決め nudge）: 始点。<br/>
    /// 従来の固定コーナー（<see cref="GetOppositeCorner"/>）ではなく、ドラッグ中のハンドル位置を返すことで<br/>
    /// マーカー・ルーペが必ず左上等の固定位置に表示される問題を解消する。<br/>
    /// 純粋ロジック（状態 → マーカー位置）のため、UI 非依存の <c>[Fact]</c> で並列検証できる。
    /// </remarks>
    public Point GetMarkerPosition()
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

    /// <summary>現在の選択矩形をドメイン境界（<see cref="Port.Bounds"/>）内にクリップして適用する。</summary>
    /// <returns>クリップによって矩形が変更された場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// 「画像外の選択を禁止」（<see cref="DragRectControl.ClampToDomainBounds"/>）が ON に切り替わった瞬間や、<br/>
    /// 外部から復元した矩形が画像外にはみ出している場合に適用する。<br/>
    /// 純粋計算の <see cref="ClipRectToBounds"/> に委譲し、変更があった場合のみ状態へ適用する<br/>
    /// （純粋計算と状態適用の分離。StartPoint に依存しない包絡矩形方式）。
    /// </remarks>
    public bool ClampSelectionToBounds()
    {
        if (!HasSelection) return false;
        var clipped = ClipRectToBounds(SelectionRect);
        if (clipped == SelectionRect) return false;
        SelectionRect = clipped;
        return true;
    }

    /// <summary>指定した矩形をドメイン境界（<see cref="Port.Bounds"/>）内にクリップする。</summary>
    /// <param name="rect">クリップする矩形（ドメイン座標）。</param>
    /// <returns>境界内に収めた矩形。境界が未設定（null）の場合は元の矩形。</returns>
    /// <remarks>
    /// 矩形の四隅を境界内にクリップし、その包絡矩形（bounding box）を返す。<br/>
    /// 矩形全体を境界内に収める（<see cref="SetSelection"/> 等の外部復元・設定変更時の即時適用用）。<br/>
    /// クリップ計算は <see cref="SelectionGeometry.ClampRectToDomainBounds"/> へ委譲する（単一所有・DRY）。
    /// </remarks>
    public Rect ClipRectToBounds(Rect rect)
        => Port.Bounds is { } size
            ? SelectionGeometry.ClampRectToDomainBounds(rect, size)
            : rect;
}
