using Avalonia;
using Estable.AvaloniaUI.DragRect.Selection;
using Estable.AvaloniaUI.Platform;

namespace Estable.AvaloniaUI.DragRect;

/// <summary>DragRectControl の選択・nudge 状態を保持する内部クラス。</summary>
/// <remarks>
/// 選択矩形（ドメイン座標）・ドラッグ状態・アクティブハンドル・nudge 状態（基点＋累積オフセット）を
/// 単一クラスに集約する（既存の SelectionService / NudgeContext の 10 クラス分割を 1 クラスに統合）。<br/>
/// <br/>
/// 座標系:<br/>
/// - 選択矩形・nudge はドメイン座標（Forward = DIP→ドメイン / Inverse = ドメイン→DIP）で動作する。<br/>
/// - マウス入力・描画はコントロールローカル DIP で受け取り、座標変換ポート（<see cref="ICoordinatePort"/>）で変換する。<br/>
/// <br/>
/// イベント駆動: 状態変更はコントロール（<see cref="DragRectControl"/>）が公開イベントで通知し、
/// 本クラスは状態の保持と純粋な更新ロジックのみを担う（ポーリング不要）。
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
    public ICoordinatePort Port { get; set; } = CoordinatePort.Identity;

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
    /// <remarks>nudge 中だった場合はマウスポインターを再表示する（非表示残留防止）。</remarks>
    public void Reset()
    {
        bool wasNudging = NudgeActive;
        SelectionRect = default;
        StartPoint = default;
        LastMousePosition = default;
        IsDragging = false;
        IsMouseDown = false;
        ActiveHandle = DragHandle.None;
        DragStartRect = default;
        DragStartPoint = default;
        NudgeActive = false;
        NudgeBase = default;
        NudgeOffsetX = 0;
        NudgeOffsetY = 0;
        CrosshairVisible = false;
        SuppressEchoAt = null;

        // nudge 中（カーソル非表示）なら再表示する（ResetSelection 経由で破棄される際の非表示残留防止）
        if (wasNudging)
        {
            Cursor.ShowCursor(true);
        }
    }

    /// <summary>選択矩形を外部状態から復元する。</summary>
    /// <param name="rect">選択矩形（ドメイン座標）。</param>
    /// <remarks>nudge 中だった場合はマウスポインターを再表示する（非表示残留防止）。</remarks>
    public void SetSelection(Rect rect)
    {
        bool wasNudging = NudgeActive;
        SelectionRect = rect;
        IsDragging = false;
        ActiveHandle = DragHandle.None;
        NudgeActive = false;
        NudgeOffsetX = 0;
        NudgeOffsetY = 0;

        // nudge 中（カーソル非表示）なら再表示する（外部からの復元時の非表示残留防止）
        if (wasNudging)
        {
            Cursor.ShowCursor(true);
        }
    }

    /// <summary>指定座標がどのドラッグハンドルに当たるかを判定する。</summary>
    /// <param name="pos">判定する座標（ドメイン座標）。</param>
    /// <param name="hitRadius">ヒットテスト半径（ドメイン座標）。</param>
    /// <returns>ヒットしたハンドル種別。選択矩形が空の場合は常に <see cref="DragHandle.None"/>。</returns>
    public DragHandle HitTest(Point pos, double hitRadius)
    {
        var r = SelectionRect;
        if (!HasSelection) return DragHandle.None;

        if (SelectionGeometry.IsNear(pos, r.TopLeft, hitRadius)) return DragHandle.TopLeft;
        if (SelectionGeometry.IsNear(pos, r.TopRight, hitRadius)) return DragHandle.TopRight;
        if (SelectionGeometry.IsNear(pos, r.BottomLeft, hitRadius)) return DragHandle.BottomLeft;
        if (SelectionGeometry.IsNear(pos, r.BottomRight, hitRadius)) return DragHandle.BottomRight;
        if (SelectionGeometry.IsNear(pos, new Point(r.Center.X, r.Top), hitRadius)) return DragHandle.TopCenter;
        if (SelectionGeometry.IsNear(pos, new Point(r.Center.X, r.Bottom), hitRadius)) return DragHandle.BottomCenter;
        if (SelectionGeometry.IsNear(pos, new Point(r.Left, r.Center.Y), hitRadius)) return DragHandle.MiddleLeft;
        if (SelectionGeometry.IsNear(pos, new Point(r.Right, r.Center.Y), hitRadius)) return DragHandle.MiddleRight;
        if (r.Contains(pos)) return DragHandle.Move;
        return DragHandle.None;
    }

    /// <summary>新規選択のドラッグを開始する。</summary>
    /// <param name="startPos">始点座標（ドメイン座標）。</param>
    public void StartNewSelection(Point startPos)
    {
        StartPoint = startPos;
        SelectionRect = new Rect(startPos, new Size(0, 0));
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
    /// <param name="hitRadius">ヒットテスト半径（ドメイン座標）。</param>
    /// <param name="minSize">選択矩形の最小サイズ。</param>
    /// <param name="clampToBounds">ドメイン境界内にクリップするかどうか。</param>
    /// <remarks>
    /// クリップ有効時（<paramref name="clampToBounds"/>）はドメイン境界（<see cref="Port.Bounds"/>）内に収める:<br/>
    /// - 新規選択: 始点・終点の両方を境界内にクリップしてから矩形を作成（始点が画像外の場合にはみ出さない）。<br/>
    /// - ハンドル変形: 固定角を動かさず、動く辺のみ境界内に制限。<br/>
    /// - 移動（Move）: 矩形全体が境界内に収まるようにシフト。<br/>
    /// </remarks>
    public void UpdateDrag(Point currentPos, double hitRadius, double minSize, bool clampToBounds)
    {
        var bounds = clampToBounds && Port.Bounds is { } size ? new Rect(0, 0, size.Width, size.Height) : (Rect?)null;

        if (ActiveHandle != DragHandle.None)
        {
            // ハンドルによるリサイズ/移動
            double dx = currentPos.X - DragStartPoint.X;
            double dy = currentPos.Y - DragStartPoint.Y;
            var rect = ComputeDragRect(DragStartRect, dx, dy, ActiveHandle, minSize);
            SelectionRect = bounds is { } b ? ClampRectToBounds(rect, b, ActiveHandle) : rect;
        }
        else if (IsDragging)
        {
            // 新規選択（始点→終点）: 始点・終点の両方を境界内にクリップしてから矩形を作成する
            // （始点が画像外（レターボックス等）で押下された場合でも、矩形が画像外にはみ出さないようにする）
            var start = bounds is { } b ? ClampPointToBounds(StartPoint, b) : StartPoint;
            var end = bounds is { } b2 ? ClampPointToBounds(currentPos, b2) : currentPos;
            SelectionRect = SelectionGeometry.CreateFromPoints(start, end);
        }
        LastMousePosition = currentPos;
    }

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
        var bounds = clampToBounds && Port.Bounds is { } size ? new Rect(0, 0, size.Width, size.Height) : (Rect?)null;

        if (ActiveHandle != DragHandle.None)
        {
            // ハンドルドラッグ中 → ハンドル方向にリサイズ
            var rect = ComputeDragRect(SelectionRect, dx, dy, ActiveHandle, minSize);
            SelectionRect = bounds is { } b ? ClampRectToBounds(rect, b, ActiveHandle) : rect;
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
            end = bounds is { } b ? ClampPointToBounds(end, b) : end;
            // 始点も境界内にクリップしてから矩形を作成する（画像外の始点によるはみ出し防止）
            var start = bounds is { } b2 ? ClampPointToBounds(StartPoint, b2) : StartPoint;
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
    /// 矩形全体を境界内に収める（<see cref="SetSelection"/> 等の外部復元・設定変更時の即時適用用）。
    /// </remarks>
    public Rect ClipRectToBounds(Rect rect)
    {
        if (Port.Bounds is not { } size) return rect;
        var bounds = new Rect(0, 0, size.Width, size.Height);
        var l = Math.Clamp(rect.Left, bounds.Left, bounds.Right);
        var t = Math.Clamp(rect.Top, bounds.Top, bounds.Bottom);
        var r = Math.Clamp(rect.Right, bounds.Left, bounds.Right);
        var b = Math.Clamp(rect.Bottom, bounds.Top, bounds.Bottom);
        return new Rect(l, t, Math.Max(0, r - l), Math.Max(0, b - t));
    }

    /// <summary>点をドメイン境界内にクリップする。</summary>
    /// <param name="p">クリップする点。</param>
    /// <param name="bounds">クリップ範囲。</param>
    /// <returns>境界内に収めた点。</returns>
    private static Point ClampPointToBounds(Point p, Rect bounds)
        => new(
            Math.Clamp(p.X, bounds.Left, bounds.Right),
            Math.Clamp(p.Y, bounds.Top, bounds.Bottom));

    /// <summary>矩形をドメイン境界内にクリップする（ハンドル種別に応じて固定角を維持）。</summary>
    /// <param name="rect">クリップする矩形。</param>
    /// <param name="bounds">クリップ範囲。</param>
    /// <param name="handle">アクティブなハンドル。</param>
    /// <returns>境界内に収めた矩形。</returns>
    /// <remarks>
    /// <see cref="DragHandle.Move"/> の場合は矩形全体が境界内に収まるようにシフトする（サイズは維持）。<br/>
    /// それ以外のハンドル変形は固定角（ハンドルと反対側の角）を動かさず、動く辺のみ境界内に制限する。
    /// </remarks>
    private static Rect ClampRectToBounds(Rect rect, Rect bounds, DragHandle handle)
    {
        double l = rect.Left;
        double t = rect.Top;
        double r = rect.Right;
        double b = rect.Bottom;

        if (handle == DragHandle.Move)
        {
            double shiftX = l < bounds.Left ? bounds.Left - l : (r > bounds.Right ? bounds.Right - r : 0);
            double shiftY = t < bounds.Top ? bounds.Top - t : (b > bounds.Bottom ? bounds.Bottom - b : 0);
            l += shiftX;
            r += shiftX;
            t += shiftY;
            b += shiftY;
        }
        else
        {
            if (l < bounds.Left) l = bounds.Left;
            if (t < bounds.Top) t = bounds.Top;
            if (r > bounds.Right) r = bounds.Right;
            if (b > bounds.Bottom) b = bounds.Bottom;
        }

        return new Rect(l, t, r - l, b - t);
    }

    /// <summary>ドラッグハンドル操作による矩形の変形を計算する。</summary>
    /// <param name="original">ドラッグ開始時の矩形。</param>
    /// <param name="dx">X 方向の移動量。</param>
    /// <param name="dy">Y 方向の移動量。</param>
    /// <param name="handle">アクティブなハンドル。</param>
    /// <param name="minSize">選択矩形の最小サイズ。</param>
    /// <returns>変形後の矩形（反転を正規化・最小サイズ保証）。</returns>
    /// <remarks>
    /// ハンドルを反対側までドラッグした場合（l&gt;r または t&gt;b）、矩形を反転（正規化）する。<br/>
    /// 最小サイズは幅・高さが <paramref name="minSize"/> 未満にならないよう保証する。
    /// </remarks>
    private static Rect ComputeDragRect(Rect original, double dx, double dy, DragHandle handle, double minSize)
    {
        double l = original.Left;
        double t = original.Top;
        double r = original.Right;
        double b = original.Bottom;

        switch (handle)
        {
            case DragHandle.TopLeft: l += dx; t += dy; break;
            case DragHandle.TopCenter: t += dy; break;
            case DragHandle.TopRight: r += dx; t += dy; break;
            case DragHandle.MiddleLeft: l += dx; break;
            case DragHandle.MiddleRight: r += dx; break;
            case DragHandle.BottomLeft: l += dx; b += dy; break;
            case DragHandle.BottomCenter: b += dy; break;
            case DragHandle.BottomRight: r += dx; b += dy; break;
            case DragHandle.Move: l += dx; t += dy; r += dx; b += dy; break;
        }

        double x1 = Math.Min(l, r);
        double x2 = Math.Max(l, r);
        double y1 = Math.Min(t, b);
        double y2 = Math.Max(t, b);

        var m = Math.Max(minSize, 1.0);
        if (x2 - x1 < m) x2 = x1 + m;
        if (y2 - y1 < m) y2 = y1 + m;

        return new Rect(x1, y1, x2 - x1, y2 - y1);
    }
}
