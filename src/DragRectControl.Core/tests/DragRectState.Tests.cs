using Estable.Platform;
using Estable.Platform.Coordinate;
using Estable.Platform.Geometry;
using Xunit;

namespace Estable.DragRect.Tests;

/// <summary>テストで使用するカーソルサービスのスパイ（呼び出しを記録する）。</summary>
/// <remarks>位置の取得・移動は未使用のため記録のみ行い、表示切替の呼び出し履歴を検証に使用する。</remarks>
internal sealed class CursorServiceSpy : ICursorService
{
    /// <summary><see cref="ShowCursor"/> に渡された値の履歴（呼び出し順）。</summary>
    public List<bool> ShowCalls { get; } = [];

    /// <summary><see cref="ICursorService.GetCursorScreenPoint"/> の戻り値。</summary>
    public EsPixelPoint CursorPoint { get; set; }

    /// <inheritdoc/>
    public EsPixelPoint GetCursorScreenPoint() => CursorPoint;

    /// <inheritdoc/>
    public void SetCursorScreenPoint(EsPixelPoint point) => CursorPoint = point;

    /// <inheritdoc/>
    public void ShowCursor(bool show) => ShowCalls.Add(show);
}

/// <summary><see cref="DragRectState"/> の単体テスト（状態遷移・ヒットテスト・ドラッグ・nudge・境界クリップ）。</summary>
/// <remarks>
/// UI フレームワークに依存しないため <c>[Fact]</c> で並列検証できる。<br/>
/// 境界（<see cref="ICoordTransform"/> の Bounds）が不要なケースは既定の <see cref="CoordTransform.Identity"/>（境界なし）を使用する。
/// </remarks>
public class DragRectStateTests
{
    /// <summary>ドメイン境界 200x200 を持つ状態を生成する（境界クリップ検証用）。</summary>
    /// <param name="cursor">カーソルサービス。null 可。</param>
    /// <returns>境界 200x200 の状態。</returns>
    private static DragRectState CreateBounded(ICursorService? cursor = null)
        => new(cursor) { Port = new CoordTransform(p => p, p => p, () => new EsSize(200, 200)) };

    /// <summary>ドメイン境界 200x200 と指定の選択矩形を持つ状態を生成する（境界クリップ検証用）。</summary>
    /// <param name="selectionRect">初期の選択矩形（ドメイン座標）。</param>
    /// <returns>境界 200x200 の状態。</returns>
    private static DragRectState CreateBounded(EsRect selectionRect)
    {
        var state = CreateBounded();
        state.SelectionRect = selectionRect;
        return state;
    }

    // ====================================================================
    // GetInteractionState（nudge・ドラッグ・選択の合成）
    // ====================================================================

    [Fact]
    public void GetInteractionState_nudgeと選択とドラッグの組合せで状態を合成する()
    {
        var state = new DragRectState(null);

        // 未選択・非ドラッグ・非 nudge
        Assert.Equal(DragRectInteractionState.NoSelection, state.GetInteractionState());

        // ドラッグ中
        state.IsDragging = true;
        Assert.Equal(DragRectInteractionState.Dragging, state.GetInteractionState());

        // 選択あり（ドラッグ終了後）
        state.IsDragging = false;
        state.SelectionRect = new EsRect(0, 0, 10, 10);
        Assert.Equal(DragRectInteractionState.Selected, state.GetInteractionState());
    }

    [Fact]
    public void GetInteractionState_nudge中はドラッグと選択より優先される()
    {
        var state = new DragRectState(null) { NudgeActive = true };

        // 未選択・非ドラッグ → 始点決め nudge
        Assert.Equal(DragRectInteractionState.StartPointNudging, state.GetInteractionState());

        // ドラッグ中 → ドラッグ中 nudge
        state.IsDragging = true;
        Assert.Equal(DragRectInteractionState.DraggingNudging, state.GetInteractionState());

        // 選択あり → 選択 nudge（選択がドラッグより優先される）
        state.SelectionRect = new EsRect(0, 0, 10, 10);
        Assert.Equal(DragRectInteractionState.SelectedNudging, state.GetInteractionState());
    }

    // ====================================================================
    // CreateSnapshot
    // ====================================================================

    [Fact]
    public void CreateSnapshot_現在の状態を全項目で返す()
    {
        var state = new DragRectState(null)
        {
            SelectionRect = new EsRect(10, 10, 50, 40),
            ActiveHandle = DragHandle.TopRight,
            LastMousePosition = new EsPoint(7, 8),
            IsMouseDown = true,
            IsDragging = true,
            NudgeActive = true,
        };

        var snapshot = state.CreateSnapshot();

        // nudge 中は選択がドラッグより優先される（GetInteractionState の優先順位）
        Assert.Equal(DragRectInteractionState.SelectedNudging, snapshot.State);
        Assert.Equal(new EsRect(10, 10, 50, 40), snapshot.SelectionRect);
        Assert.Equal(DragHandle.TopRight, snapshot.ActiveHandle);
        Assert.Equal(new EsPoint(7, 8), snapshot.MousePosition);
        Assert.True(snapshot.IsMouseDown);
        Assert.True(snapshot.IsDragging);
        Assert.True(snapshot.NudgeActive);
        Assert.True(snapshot.HasSelection);
    }

    [Fact]
    public void CreateSnapshot_マウス位置を明示指定できる()
    {
        var state = new DragRectState(null) { LastMousePosition = new EsPoint(1, 1) };

        var snapshot = state.CreateSnapshot(new EsPoint(100, 200));

        Assert.Equal(new EsPoint(100, 200), snapshot.MousePosition);
    }

    [Fact]
    public void DragRectSnapshot_HasSelection_幅か高さが0ならfalseを返す()
    {
        // 幅のみ（高さ 0）→ 選択として無効
        Assert.False(new DragRectSnapshot(DragRectInteractionState.Selected, new EsRect(0, 0, 100, 0), DragHandle.None, default, false, false, false).HasSelection);
        // 高さのみ（幅 0）→ 選択として無効
        Assert.False(new DragRectSnapshot(DragRectInteractionState.Selected, new EsRect(0, 0, 0, 100), DragHandle.None, default, false, false, false).HasSelection);
    }

    // ====================================================================
    // HitTest
    // ====================================================================

    [Fact]
    public void HitTest_未選択は常にNoneを返す()
    {
        var state = new DragRectState(null);

        Assert.Equal(DragHandle.None, state.HitTest(new EsPoint(0, 0), 12.0));
    }

    [Fact]
    public void HitTest_角は辺より優先され矩形内はMoveを返す()
    {
        var state = new DragRectState(null) { SelectionRect = new EsRect(10, 20, 100, 80) };

        // 角（左上・右下）
        Assert.Equal(DragHandle.TopLeft, state.HitTest(new EsPoint(11, 21), 12.0));
        Assert.Equal(DragHandle.BottomRight, state.HitTest(new EsPoint(109, 99), 12.0));
        // 左上の近傍は角が辺（MiddleLeft/TopCenter）より優先される
        Assert.Equal(DragHandle.TopLeft, state.HitTest(new EsPoint(14, 24), 12.0));
        // 辺（上中央・中右）
        Assert.Equal(DragHandle.TopCenter, state.HitTest(new EsPoint(60, 20), 12.0));
        Assert.Equal(DragHandle.MiddleRight, state.HitTest(new EsPoint(110, 60), 12.0));
        // 矩形内 → Move
        Assert.Equal(DragHandle.Move, state.HitTest(new EsPoint(60, 60), 12.0));
        // 矩形外かつハンドル半径外 → None
        Assert.Equal(DragHandle.None, state.HitTest(new EsPoint(300, 300), 12.0));
    }

    // ====================================================================
    // StartNewSelection / UpdateDrag / EndDrag
    // ====================================================================

    [Fact]
    public void StartNewSelection_始点を保持し0x0の矩形でドラッグを開始する()
    {
        var state = new DragRectState(null);

        state.StartNewSelection(new EsPoint(10, 20));

        Assert.True(state.IsDragging);
        Assert.Equal(DragHandle.None, state.ActiveHandle);
        Assert.Equal(new EsPoint(10, 20), state.StartPoint);
        Assert.Equal(new EsPoint(10, 20), state.LastMousePosition);
        Assert.Equal(new EsRect(10, 20, 0, 0), state.SelectionRect);
        Assert.False(state.HasSelection);
    }

    [Fact]
    public void UpdateDrag_新規選択は始点と現在位置から矩形を作成する()
    {
        var state = new DragRectState(null);
        state.StartNewSelection(new EsPoint(10, 20));

        state.UpdateDrag(new EsPoint(60, 80), 10.0, clampToBounds: false);

        Assert.Equal(new EsRect(10, 20, 50, 60), state.SelectionRect);
        Assert.Equal(new EsPoint(60, 80), state.LastMousePosition);
    }

    [Fact]
    public void UpdateDrag_ハンドル変形は開始時の矩形を基準にする()
    {
        var state = new DragRectState(null) { SelectionRect = new EsRect(10, 20, 100, 80) };
        state.StartDrag(DragHandle.BottomRight, new EsPoint(110, 100));

        state.UpdateDrag(new EsPoint(130, 130), 10.0, clampToBounds: false);

        Assert.Equal(new EsRect(10, 20, 120, 110), state.SelectionRect);
        Assert.Equal(DragHandle.BottomRight, state.ActiveHandle);
    }

    [Fact]
    public void UpdateDrag_境界内クリップ時は新規選択の始点と終点をともにクリップする()
    {
        var state = CreateBounded();
        state.StartNewSelection(new EsPoint(-50, -50));

        state.UpdateDrag(new EsPoint(300, 300), 10.0, clampToBounds: true);

        // 始点・終点とも境界内へクリップされるため、矩形は画像外へはみ出さない
        Assert.Equal(new EsRect(0, 0, 200, 200), state.SelectionRect);
    }

    [Fact]
    public void UpdateDrag_境界内クリップ時はMoveで矩形全体がシフトされる()
    {
        var state = CreateBounded(new EsRect(180, 180, 50, 40));
        state.StartDrag(DragHandle.Move, new EsPoint(200, 200));

        state.UpdateDrag(new EsPoint(260, 260), 10.0, clampToBounds: true);

        // サイズを維持したまま境界内へシフトする（Right=230 Bottom=220 → シフト量は X=-30 / Y=-20）
        Assert.Equal(new EsRect(150, 160, 50, 40), state.SelectionRect);
    }

    [Fact]
    public void EndDrag_ドラッグ状態とハンドルを解除する()
    {
        var state = new DragRectState(null) { SelectionRect = new EsRect(10, 20, 100, 80) };
        state.StartDrag(DragHandle.TopLeft, new EsPoint(10, 20));

        state.EndDrag();

        Assert.False(state.IsDragging);
        Assert.Equal(DragHandle.None, state.ActiveHandle);
        // 矩形は維持される（ドラッグ終了は選択を破棄しない）
        Assert.Equal(new EsRect(10, 20, 100, 80), state.SelectionRect);
    }

    // ====================================================================
    // Nudge
    // ====================================================================

    [Fact]
    public void Nudge_未選択は始点を移動する()
    {
        var state = new DragRectState(null) { StartPoint = new EsPoint(10, 20) };

        state.Nudge(3, -4, 10.0, clampToBounds: false);

        Assert.Equal(new EsPoint(13, 16), state.StartPoint);
        Assert.False(state.HasSelection);
    }

    [Fact]
    public void Nudge_選択確定後は終点を移動する()
    {
        var state = new DragRectState(null)
        {
            StartPoint = new EsPoint(10, 20),
            SelectionRect = new EsRect(10, 20, 50, 40),
        };

        state.Nudge(5, 5, 10.0, clampToBounds: false);

        // 始点（左上）は固定され、終点（右下）が移動する
        Assert.Equal(new EsRect(10, 20, 55, 45), state.SelectionRect);
        Assert.Equal(new EsPoint(65, 65), state.LastMousePosition);
    }

    [Fact]
    public void Nudge_ハンドル中はリサイズし次のUpdateDragの基準を更新する()
    {
        var state = new DragRectState(null) { SelectionRect = new EsRect(10, 20, 100, 80) };
        state.StartDrag(DragHandle.BottomRight, new EsPoint(110, 100));

        state.Nudge(4, 6, 10.0, clampToBounds: false);

        var expected = new EsRect(10, 20, 104, 86);
        Assert.Equal(expected, state.SelectionRect);
        // nudge 後はドラッグ基準（開始矩形・開始位置）が更新される（次の UpdateDrag で nudge が失われない）
        Assert.Equal(expected, state.DragStartRect);
        Assert.Equal(new EsPoint(114, 106), state.DragStartPoint);
    }

    [Fact]
    public void Nudge_始点が画像外でも矩形は画像外へはみ出さない()
    {
        var state = CreateBounded();
        state.StartPoint = new EsPoint(-100, -100);
        state.SelectionRect = new EsRect(-100, -100, 300, 300);

        state.Nudge(10, 10, 10.0, clampToBounds: true);

        // 始点は境界内へクリップされ、終点も境界内に収まる
        Assert.Equal(new EsRect(0, 0, 200, 200), state.SelectionRect);
    }

    // ====================================================================
    // GetOppositeCorner / GetMarkerPosition
    // ====================================================================

    [Fact]
    public void GetOppositeCorner_始点と反対側の隅を返す()
    {
        var state = new DragRectState(null) { StartPoint = new EsPoint(10, 20) };

        Assert.Equal(new EsPoint(60, 60), state.GetOppositeCorner(new EsRect(10, 20, 50, 40)));
    }

    [Fact]
    public void GetMarkerPosition_未選択は始点を返す()
    {
        var state = new DragRectState(null);
        state.StartPoint = new EsPoint(10, 20);

        Assert.Equal(new EsPoint(10, 20), state.GetMarkerPosition());
    }

    [Fact]
    public void GetMarkerPosition_新規選択ドラッグ中は終点を返す()
    {
        var state = new DragRectState(null);
        state.StartPoint = new EsPoint(10, 20);
        state.IsDragging = true;
        state.SelectionRect = new EsRect(10, 20, 50, 40); // 始点 (10,20)、終点 (60,60)

        Assert.Equal(new EsPoint(60, 60), state.GetMarkerPosition());
    }

    [Fact]
    public void GetMarkerPosition_リサイズハンドル中はハンドル位置を返す()
    {
        var state = new DragRectState(null);
        state.SelectionRect = new EsRect(10, 20, 100, 80);
        state.ActiveHandle = DragHandle.TopRight;

        Assert.Equal(new EsPoint(110, 20), state.GetMarkerPosition());
    }

    [Fact]
    public void GetMarkerPosition_移動ハンドル中はマウス位置を返す()
    {
        var state = new DragRectState(null);
        state.SelectionRect = new EsRect(10, 20, 100, 80);
        state.ActiveHandle = DragHandle.Move;
        state.LastMousePosition = new EsPoint(45, 67);

        Assert.Equal(new EsPoint(45, 67), state.GetMarkerPosition());
    }

    // ====================================================================
    // Reset / SetSelection（nudge 終了時のカーソル再表示）
    // ====================================================================

    [Fact]
    public void Reset_全状態を初期化する()
    {
        var state = new DragRectState(null)
        {
            SelectionRect = new EsRect(10, 20, 50, 40),
            StartPoint = new EsPoint(1, 2),
            LastMousePosition = new EsPoint(3, 4),
            ActiveHandle = DragHandle.Move,
            NudgeActive = false,
        };
        state.StartDrag(DragHandle.Move, new EsPoint(5, 6));

        state.Reset();

        Assert.Equal(DragHandle.None, state.ActiveHandle);
        Assert.False(state.HasSelection);
        Assert.False(state.IsDragging);
        Assert.False(state.IsMouseDown);
        Assert.False(state.NudgeActive);
        Assert.Null(state.SuppressEchoAt);
        Assert.Equal(default, state.StartPoint);
    }

    [Fact]
    public void Reset_nudge中はカーソルを再表示する()
    {
        var cursor = new CursorServiceSpy();
        var state = new DragRectState(cursor) { NudgeActive = true, NudgeOffsetX = 3, NudgeOffsetY = 4 };

        state.Reset();

        Assert.Equal([true], cursor.ShowCalls);
        Assert.Equal(0, state.NudgeOffsetX);
        Assert.Equal(0, state.NudgeOffsetY);
    }

    [Fact]
    public void SetSelection_矩形を復元しドラッグ状態を解除する()
    {
        var state = new DragRectState(null) { SelectionRect = new EsRect(0, 0, 10, 10) };
        state.StartDrag(DragHandle.TopLeft, new EsPoint(0, 0));

        state.SetSelection(new EsRect(30, 40, 50, 60));

        Assert.Equal(new EsRect(30, 40, 50, 60), state.SelectionRect);
        Assert.False(state.IsDragging);
        Assert.Equal(DragHandle.None, state.ActiveHandle);
    }

    [Fact]
    public void SetSelection_nudge中はカーソルを再表示する()
    {
        var cursor = new CursorServiceSpy();
        var state = new DragRectState(cursor) { NudgeActive = true };

        state.SetSelection(new EsRect(30, 40, 50, 60));

        Assert.Equal([true], cursor.ShowCalls);
        Assert.False(state.NudgeActive);
    }

    [Fact]
    public void Reset_nudge中でもカーソル未指定なら例外なく完了する()
    {
        var state = new DragRectState(null) { NudgeActive = true };

        state.Reset();

        Assert.False(state.NudgeActive);
    }

    [Fact]
    public void Reset_nudge中でなければカーソルを操作しない()
    {
        var cursor = new CursorServiceSpy();
        var state = new DragRectState(cursor);

        state.Reset();

        Assert.Empty(cursor.ShowCalls);
    }

    // ====================================================================
    // ClampSelectionToBounds / ClipRectToBounds
    // ====================================================================

    [Fact]
    public void ClampSelectionToBounds_境界を超える矩形をクリップする()
    {
        var state = CreateBounded(new EsRect(-50, -30, 300, 260));

        var changed = state.ClampSelectionToBounds();

        Assert.True(changed);
        Assert.Equal(new EsRect(0, 0, 200, 200), state.SelectionRect);
    }

    [Fact]
    public void ClampSelectionToBounds_境界内の矩形は変更しない()
    {
        var state = CreateBounded(new EsRect(20, 20, 100, 80));

        var changed = state.ClampSelectionToBounds();

        Assert.False(changed);
        Assert.Equal(new EsRect(20, 20, 100, 80), state.SelectionRect);
    }

    [Fact]
    public void ClampSelectionToBounds_未選択は変更しない()
    {
        var state = CreateBounded();

        Assert.False(state.ClampSelectionToBounds());
    }

    [Fact]
    public void ClipRectToBounds_境界が未設定なら元の矩形を返す()
    {
        var state = new DragRectState(null);
        var rect = new EsRect(-50, -30, 300, 260);

        Assert.Equal(rect, state.ClipRectToBounds(rect));
    }
}
