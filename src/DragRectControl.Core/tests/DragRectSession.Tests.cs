using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Estable.DragRect.Tests;

/// <summary><see cref="DragRectSession"/> の単体テスト（入力の解釈・状態遷移の判断・通知・効果）。</summary>
/// <remarks>
/// UI 層から切り出した判断（nudge セッション・ダブルクリックの確定/キャンセル・ハンドルのスナップ・<br/>
/// クリック検出・最小サイズによるキャンセル・エコー移動の抑制・カーソル種別）を、UI フレームワークなしで検証する。<br/>
/// 境界（<see cref="ICoordTransform"/> の Bounds）が不要なケースは既定の <see cref="CoordTransform.Identity"/>（境界なし）を使用する。
/// </remarks>
public class DragRectSessionTests
{
    /// <summary>セッションと通知の収集先を生成する。</summary>
    /// <param name="cursor">カーソルサービス。null 可。</param>
    /// <returns>セッションと収集した通知のリスト。</returns>
    private static DragRectSession CreateSession(ICursorService? cursor, out List<DragRectNotice> notices)
    {
        var session = new DragRectSession(cursor) { Log = NullLogger.Instance };
        var collected = new List<DragRectNotice>();
        session.Notified += (_, notice) => collected.Add(notice);
        notices = collected;
        return session;
    }

    /// <summary>ドメイン境界 200x200 を持つセッションを生成する（境界クリップ検証用）。</summary>
    /// <param name="cursor">カーソルサービス。null 可。</param>
    /// <param name="notices">収集した通知のリスト。</param>
    /// <returns>境界 200x200 のセッション。</returns>
    private static DragRectSession CreateBoundedSession(ICursorService? cursor, out List<DragRectNotice> notices)
    {
        var session = CreateSession(cursor, out notices);
        session.SetPort(new CoordTransform(p => p, p => p, () => new EsSize(200, 200)));
        return session;
    }

    /// <summary>通知種別の一覧を取得する。</summary>
    /// <param name="notices">通知のリスト。</param>
    /// <returns>通知種別の一覧。</returns>
    private static List<DragRectNoticeKind> Kinds(List<DragRectNotice> notices)
        => [.. notices.Select(n => n.Kind)];

    /// <summary>指定種別の最初の通知を取得する。</summary>
    /// <param name="notices">通知のリスト。</param>
    /// <param name="kind">通知種別。</param>
    /// <returns>一致する通知。</returns>
    private static DragRectNotice Find(List<DragRectNotice> notices, DragRectNoticeKind kind)
        => notices.First(n => n.Kind == kind);

    // ====================================================================
    // Press（押下）
    // ====================================================================

    [Fact]
    public void Press_新規選択を開始しMouseDownとSelectionStartedを通知する()
    {
        var session = CreateSession(null, out var notices);

        var feedback = session.Press(new EsPoint(10, 10), clickCount: 1);

        Assert.True(session.IsDragging);
        Assert.False(session.HasSelection);
        Assert.Equal(new EsRect(10, 10, 0, 0), session.SelectionRect);
        Assert.Equal([DragRectNoticeKind.MouseDown, DragRectNoticeKind.SelectionStarted], Kinds(notices));
        Assert.True(feedback.Redraw);
        Assert.True(feedback.RequestFocus);
        Assert.True(feedback.CaptureMouse);
    }

    [Fact]
    public void Press_ハンドルヒットでポインターをハンドル点へスナップする()
    {
        var session = CreateSession(null, out var notices);
        session.SetSelection(new EsRect(50, 50, 100, 80));

        // 右下ハンドル（150, 130）付近を押下する（ヒット半径内だが角とずれた位置）
        var feedback = session.Press(new EsPoint(148, 128), clickCount: 1);

        // スナップ: ドラッグ基準点とマウス位置がハンドル点（150, 130）になり、カーソルもそこへ移動する
        Assert.Equal(new EsPoint(150, 130), session.State.DragStartPoint);
        Assert.Equal(new EsPoint(150, 130), session.LastMousePosition);
        Assert.True(feedback.Cursor.HasFlag(DragRectCursorAction.MoveTo));
        Assert.Equal(new EsPoint(150, 130), feedback.CursorDomainPosition);
        Assert.Equal(DragRectNoticeKind.LoupePositionMoved, notices[^1].Kind);
        Assert.Equal(new EsPoint(150, 130), notices[^1].Position);
    }

    [Fact]
    public void Press_移動ハンドルではポインターをスナップしない()
    {
        var session = CreateSession(null, out _);
        session.SetSelection(new EsRect(50, 50, 100, 80));

        // 矩形内（中心）を押下する → 移動（Move）
        var feedback = session.Press(new EsPoint(100, 90), clickCount: 1);

        Assert.False(feedback.Cursor.HasFlag(DragRectCursorAction.MoveTo));
        Assert.Equal(new EsPoint(100, 90), session.State.DragStartPoint);
    }

    [Fact]
    public void Press_ダブルクリック_矩形内は確定を通知し選択を破棄する()
    {
        var session = CreateSession(null, out var notices);
        session.SetSelection(new EsRect(50, 50, 100, 80));
        notices.Clear();

        var feedback = session.Press(new EsPoint(100, 90), clickCount: 2);

        Assert.Equal(
            [DragRectNoticeKind.MouseDown, DragRectNoticeKind.MouseDoubleClicked, DragRectNoticeKind.ConfirmRequested],
            Kinds(notices));
        Assert.False(session.HasSelection);
        Assert.True(feedback.Redraw);
        Assert.False(feedback.RequestFocus);
    }

    [Fact]
    public void Press_ダブルクリック_矩形外はキャンセルを通知し選択を維持する()
    {
        var session = CreateSession(null, out var notices);
        session.SetSelection(new EsRect(50, 50, 100, 80));
        notices.Clear();

        var feedback = session.Press(new EsPoint(10, 10), clickCount: 2);

        Assert.Equal(
            [DragRectNoticeKind.MouseDown, DragRectNoticeKind.MouseDoubleClicked, DragRectNoticeKind.CancelRequested],
            Kinds(notices));
        Assert.True(session.HasSelection);
        Assert.False(feedback.Redraw);
    }

    [Fact]
    public void Press_nudge中はnudge位置を押下位置としカーソルを再表示する()
    {
        var cursor = new CursorServiceSpy();
        var session = CreateSession(cursor, out var notices);

        // 未選択のまま右へ nudge（始点が 1 だけ進む）
        session.NudgeByKey(EsKey.Right, new EsPoint(100, 100));
        notices.Clear();

        var feedback = session.Press(session.State.StartPoint, clickCount: 1);

        Assert.False(session.NudgeActive);
        Assert.True(feedback.Cursor.HasFlag(DragRectCursorAction.Show));
        Assert.Equal([DragRectNoticeKind.LoupePositionMoved, DragRectNoticeKind.MouseDown, DragRectNoticeKind.SelectionStarted], Kinds(notices));
    }

    // ====================================================================
    // Move（移動）
    // ====================================================================

    [Fact]
    public void Move_ドラッグ中は選択矩形を更新しMouseMovedとSelectionChangedを通知する()
    {
        var session = CreateSession(null, out var notices);
        session.Press(new EsPoint(10, 10), clickCount: 1);
        notices.Clear();

        var feedback = session.Move(new EsPoint(50, 50));

        Assert.Equal(new EsRect(10, 10, 40, 40), session.SelectionRect);
        Assert.Equal(
            [DragRectNoticeKind.MouseMoved, DragRectNoticeKind.SelectionChanged, DragRectNoticeKind.LoupePositionMoved],
            Kinds(notices));
        Assert.True(feedback.Redraw);
        Assert.True(feedback.UpdateCursorShape);
    }

    [Fact]
    public void Move_新規選択中はルーペ位置が終点を追従する()
    {
        var session = CreateSession(null, out var notices);
        session.Press(new EsPoint(10, 10), clickCount: 1);
        notices.Clear();

        session.Move(new EsPoint(50, 30));

        var loupe = Find(notices, DragRectNoticeKind.LoupePositionMoved);
        Assert.Equal(new EsPoint(50, 30), loupe.Position);
    }

    [Fact]
    public void Move_ドラッグ移動なしではクリックとして扱われる()
    {
        var session = CreateSession(null, out var notices);
        session.Press(new EsPoint(60, 60), clickCount: 1);
        // 押下と同じ位置への移動（矩形は変化しない）
        session.Move(new EsPoint(60, 60));
        notices.Clear();

        session.Release(new EsPoint(60, 60));

        // 矩形は 0x0 のためキャンセルされたうえで、クリックとして通知される
        Assert.Contains(DragRectNoticeKind.SelectionCancelled, Kinds(notices));
        Assert.Contains(DragRectNoticeKind.MouseClicked, Kinds(notices));
        Assert.True(notices.Single(n => n.Kind == DragRectNoticeKind.MouseClicked).Position == new EsPoint(60, 60));
    }

    [Fact]
    public void Move_ドラッグ移動があればクリックとして扱わない()
    {
        var session = CreateSession(null, out var notices);
        session.Press(new EsPoint(10, 10), clickCount: 1);
        session.Move(new EsPoint(80, 80));
        notices.Clear();

        session.Release(new EsPoint(80, 80));

        Assert.Contains(DragRectNoticeKind.SelectionCompleted, Kinds(notices));
        Assert.DoesNotContain(DragRectNoticeKind.MouseClicked, Kinds(notices));
    }

    [Fact]
    public void Move_nudge中はマウス位置を上書きしない()
    {
        var session = CreateSession(null, out _);
        session.Press(new EsPoint(10, 10), clickCount: 1);
        session.Move(new EsPoint(80, 80));
        session.NudgeByKey(EsKey.Right, new EsPoint(80, 80));
        var nudged = session.LastMousePosition;

        // nudge 中（SetCursorPos 由来のエコー移動）はマウス位置・選択矩形を上書きしない
        session.Move(new EsPoint(500, 500));

        Assert.Equal(nudged, session.LastMousePosition);
    }

    [Fact]
    public void Move_エコー移動は1回だけ抑制する()
    {
        var session = CreateSession(null, out var notices);
        session.Press(new EsPoint(10, 10), clickCount: 1);
        session.Move(new EsPoint(80, 80));
        session.NudgeByKey(EsKey.Right, new EsPoint(80, 80));
        var nudgePosition = session.MarkerPosition;
        session.Release(new EsPoint(80, 80));
        notices.Clear();

        // 1 回目: 抑制されて通知なし（カーソル形状のみ更新）
        var first = session.Move(nudgePosition);
        Assert.Empty(notices);
        Assert.True(first.UpdateCursorShape);

        // 2 回目: 抑制は解除済みで通知される
        session.Move(nudgePosition);
        Assert.Contains(DragRectNoticeKind.MouseMoved, Kinds(notices));
    }

    // ====================================================================
    // Release（解放）
    // ====================================================================

    [Fact]
    public void Release_最小サイズ未満の矩形はキャンセルする()
    {
        var session = CreateSession(null, out var notices);
        session.MinSelectionSize = 10.0;
        session.Press(new EsPoint(10, 10), clickCount: 1);
        session.Move(new EsPoint(15, 15));
        notices.Clear();

        session.Release(new EsPoint(15, 15));

        Assert.Contains(DragRectNoticeKind.SelectionCancelled, Kinds(notices));
        Assert.DoesNotContain(DragRectNoticeKind.SelectionCompleted, Kinds(notices));
        Assert.False(session.HasSelection);
        Assert.False(session.IsDragging);
    }

    [Fact]
    public void Release_ドラッグ完了でSelectionCompletedを通知する()
    {
        var session = CreateSession(null, out var notices);
        session.Press(new EsPoint(10, 10), clickCount: 1);
        session.Move(new EsPoint(80, 80));
        notices.Clear();

        session.Release(new EsPoint(80, 80));

        Assert.Equal(DragRectNoticeKind.MouseUp, notices[0].Kind);
        Assert.Contains(DragRectNoticeKind.SelectionCompleted, Kinds(notices));
        Assert.Equal(new EsRect(10, 10, 70, 70), Find(notices, DragRectNoticeKind.SelectionCompleted).Snapshot.SelectionRect);
        Assert.False(session.IsDragging);
    }

    [Fact]
    public void Release_nudge中はカーソルをnudge位置へ戻して再表示する()
    {
        var cursor = new CursorServiceSpy();
        var session = CreateSession(cursor, out var notices);
        session.SetSelection(new EsRect(50, 50, 100, 80));
        session.Press(new EsPoint(100, 90), clickCount: 1);
        session.NudgeByKey(EsKey.Right, new EsPoint(100, 90));
        var nudgePosition = session.MarkerPosition;

        var feedback = session.Release(new EsPoint(500, 500));

        Assert.False(session.NudgeActive);
        Assert.True(feedback.Cursor.HasFlag(DragRectCursorAction.MoveTo));
        Assert.Equal(nudgePosition, feedback.CursorDomainPosition);
        Assert.True(feedback.Cursor.HasFlag(DragRectCursorAction.Show));
        Assert.True(feedback.UpdateCursorShape);
        Assert.Equal(nudgePosition, session.State.SuppressEchoAt);
    }

    // ====================================================================
    // KeyDown / NudgeByKey（キーボード）
    // ====================================================================

    [Fact]
    public void KeyDown_Escapeでキャンセル要求を通知し選択を破棄する()
    {
        var session = CreateSession(null, out var notices);
        session.SetSelection(new EsRect(50, 50, 100, 80));
        notices.Clear();

        var feedback = session.KeyDown(EsKey.Escape, null);

        Assert.True(feedback.Handled);
        Assert.True(feedback.Redraw);
        Assert.Equal([DragRectNoticeKind.CancelRequested], Kinds(notices));
        Assert.False(session.HasSelection);
    }

    [Theory]
    [InlineData(EsKey.Enter)]
    [InlineData(EsKey.Return)]
    public void KeyDown_EnterとReturnで確定要求を通知する(EsKey key)
    {
        var session = CreateSession(null, out var notices);
        session.SetSelection(new EsRect(50, 50, 100, 80));
        notices.Clear();

        var feedback = session.KeyDown(key, null);

        Assert.True(feedback.Handled);
        Assert.Equal([DragRectNoticeKind.ConfirmRequested], Kinds(notices));
        Assert.False(session.HasSelection);
    }

    [Fact]
    public void KeyDown_選択なしのEnterでも処理済みとして扱う()
    {
        var session = CreateSession(null, out var notices);

        var feedback = session.KeyDown(EsKey.Enter, null);

        // 確定要求は発火しないが、キーは処理済み（上位が既定動作へ流さない）
        Assert.True(feedback.Handled);
        Assert.Empty(notices);
        Assert.False(feedback.Redraw);
    }

    [Fact]
    public void KeyDown_矢印キーでnudgeしカーソルを非表示にする()
    {
        var session = CreateSession(new CursorServiceSpy(), out var notices);

        var feedback = session.KeyDown(EsKey.Left, new EsPoint(100, 100));

        Assert.True(feedback.Handled);
        Assert.True(feedback.Redraw);
        Assert.True(feedback.Cursor.HasFlag(DragRectCursorAction.Hide));
        Assert.True(feedback.Cursor.HasFlag(DragRectCursorAction.MoveTo));
        Assert.True(session.NudgeActive);
        Assert.True(session.CrosshairVisible);
        Assert.Contains(DragRectNoticeKind.SelectionChanged, Kinds(notices));
        Assert.Contains(DragRectNoticeKind.LoupePositionMoved, Kinds(notices));
    }

    [Fact]
    public void KeyDown_選択確定後はnudgeしない()
    {
        var session = CreateSession(null, out var notices);
        session.SetSelection(new EsRect(50, 50, 100, 80));
        notices.Clear();

        var feedback = session.KeyDown(EsKey.Left, null);

        Assert.False(feedback.Handled);
        Assert.False(session.NudgeActive);
        Assert.Empty(notices);
    }

    [Fact]
    public void KeyDown_矢印キー以外は処理しない()
    {
        var session = CreateSession(null, out _);

        var feedback = session.KeyDown(EsKey.A, null);

        Assert.False(feedback.Handled);
        Assert.False(session.NudgeActive);
    }

    [Fact]
    public void NudgeByKey_未選択は基点をカーソル位置へ同期する()
    {
        var session = CreateSession(null, out _);

        // カーソル位置 (100, 100) を基点として右へ 1 ドメインピクセル進む
        session.NudgeByKey(EsKey.Right, new EsPoint(100, 100));

        Assert.Equal(new EsPoint(101, 100), session.State.StartPoint);
    }

    [Fact]
    public void NudgeByKey_nudge中の2回目以降は基点を再同期しない()
    {
        var session = CreateSession(null, out _);
        session.NudgeByKey(EsKey.Right, new EsPoint(100, 100));

        // nudge 中にカーソルが別の位置へ動いても基点を再同期しない（移動が相殺されない）
        session.NudgeByKey(EsKey.Right, new EsPoint(500, 500));

        Assert.Equal(new EsPoint(102, 100), session.State.StartPoint);
    }

    [Fact]
    public void NudgeByKey_nudge中はカーソル位置を記録する()
    {
        var session = CreateSession(null, out _);

        session.NudgeByKey(EsKey.Right, new EsPoint(100, 100));

        Assert.Equal(new EsPoint(100, 100), session.LastMousePosition);
    }

    [Fact]
    public void NudgeByKey_カーソル位置が取得できない場合は基点を同期しない()
    {
        var session = CreateSession(null, out _);

        session.NudgeByKey(EsKey.Right, null);

        // カーソル位置が不明な場合は基点（既定値）から動かさない
        Assert.Equal(new EsPoint(1, 0), session.State.StartPoint);
    }

    // ====================================================================
    // EndNudge / Detach（対話の終了）
    // ====================================================================

    [Fact]
    public void EndNudge_nudge中はカーソルを再表示し再描画する()
    {
        var cursor = new CursorServiceSpy();
        var session = CreateSession(cursor, out _);
        session.NudgeByKey(EsKey.Right, new EsPoint(10, 10));

        var feedback = session.EndNudge();

        Assert.False(session.NudgeActive);
        Assert.True(feedback.Redraw);
        Assert.True(feedback.Cursor.HasFlag(DragRectCursorAction.Show));
        // nudge 開始時のカーソル非表示は効果（Hide）として UI 層が適用するため、ここでは再表示のみが記録される
        Assert.Equal([true], cursor.ShowCalls);
    }

    [Fact]
    public void EndNudge_nudge中でなければ何もしない()
    {
        var session = CreateSession(null, out _);

        var feedback = session.EndNudge();

        Assert.False(feedback.Redraw);
        Assert.Equal(DragRectCursorAction.None, feedback.Cursor);
    }

    [Fact]
    public void Detach_nudgeを終了し選択を初期化する()
    {
        var cursor = new CursorServiceSpy();
        var session = CreateSession(cursor, out _);
        session.SetSelection(new EsRect(50, 50, 100, 80));
        session.Press(new EsPoint(100, 90), clickCount: 1);
        session.NudgeByKey(EsKey.Right, new EsPoint(100, 90));

        var feedback = session.Detach();

        Assert.False(session.NudgeActive);
        Assert.False(session.HasSelection);
        Assert.False(session.IsDragging);
        Assert.True(feedback.Redraw);
        Assert.True(feedback.Cursor.HasFlag(DragRectCursorAction.Show));
    }

    // ====================================================================
    // 外部操作（復元・初期化・確定・キャンセル・境界クリップ）
    // ====================================================================

    [Fact]
    public void SetSelection_選択矩形を復元しSelectionChangedを通知する()
    {
        var session = CreateSession(null, out var notices);

        var feedback = session.SetSelection(new EsRect(50, 50, 100, 80));

        Assert.Equal(new EsRect(50, 50, 100, 80), session.SelectionRect);
        Assert.True(session.HasSelection);
        Assert.Equal([DragRectNoticeKind.SelectionChanged], Kinds(notices));
        Assert.True(feedback.Redraw);
    }

    [Fact]
    public void SetSelection_境界内にクリップしてから設定する()
    {
        var session = CreateBoundedSession(null, out _);
        session.ClampToDomainBounds = true;

        session.SetSelection(new EsRect(150, 150, 100, 100));

        // 境界 200x200 に収まるように切り詰められる
        Assert.Equal(new EsRect(150, 150, 50, 50), session.SelectionRect);
    }

    [Fact]
    public void SetSelection_クリップ無効時は境界外の矩形もそのまま設定する()
    {
        var session = CreateBoundedSession(null, out _);
        session.ClampToDomainBounds = false;

        session.SetSelection(new EsRect(150, 150, 100, 100));

        Assert.Equal(new EsRect(150, 150, 100, 100), session.SelectionRect);
    }

    [Fact]
    public void Reset_全状態を初期化する()
    {
        var session = CreateSession(null, out _);
        session.Press(new EsPoint(10, 10), clickCount: 1);
        session.Move(new EsPoint(80, 80));

        var feedback = session.Reset();

        Assert.False(session.HasSelection);
        Assert.False(session.IsDragging);
        Assert.False(session.NudgeActive);
        Assert.True(feedback.Redraw);
    }

    [Fact]
    public void RequestConfirm_選択確定後は矩形付きで通知し選択をクリアする()
    {
        var session = CreateSession(null, out var notices);
        session.SetSelection(new EsRect(50, 50, 100, 80));
        notices.Clear();

        var feedback = session.RequestConfirm();

        var notice = Assert.Single(notices);
        Assert.Equal(DragRectNoticeKind.ConfirmRequested, notice.Kind);
        Assert.Equal(new EsRect(50, 50, 100, 80), notice.Snapshot.SelectionRect);
        Assert.False(session.HasSelection);
        Assert.True(feedback.Redraw);
    }

    [Fact]
    public void RequestConfirm_選択なしでは何も通知しない()
    {
        var session = CreateSession(null, out var notices);

        var feedback = session.RequestConfirm();

        Assert.Empty(notices);
        Assert.False(feedback.Redraw);
    }

    [Fact]
    public void RequestConfirm_ドラッグ中は何も通知しない()
    {
        var session = CreateSession(null, out var notices);
        session.Press(new EsPoint(10, 10), clickCount: 1);
        notices.Clear();

        session.RequestConfirm();

        Assert.Empty(notices);
    }

    [Fact]
    public void RequestCancel_選択ありなら破棄して通知する()
    {
        var session = CreateSession(null, out var notices);
        session.SetSelection(new EsRect(50, 50, 100, 80));
        notices.Clear();

        var feedback = session.RequestCancel();

        Assert.Equal([DragRectNoticeKind.CancelRequested], Kinds(notices));
        Assert.False(session.HasSelection);
        Assert.True(feedback.Redraw);
    }

    [Fact]
    public void RequestCancel_選択なしでも通知する()
    {
        var session = CreateSession(null, out var notices);

        var feedback = session.RequestCancel();

        Assert.Equal([DragRectNoticeKind.CancelRequested], Kinds(notices));
        Assert.False(feedback.Redraw);
    }

    [Fact]
    public void ApplyClampToBounds_境界を超える選択矩形をクリップし通知する()
    {
        var session = CreateBoundedSession(null, out var notices);
        session.SetSelection(new EsRect(150, 150, 100, 100));
        notices.Clear();

        var feedback = session.ApplyClampToBounds();

        Assert.Equal(new EsRect(150, 150, 50, 50), session.SelectionRect);
        Assert.Equal([DragRectNoticeKind.SelectionChanged], Kinds(notices));
        Assert.True(feedback.Redraw);
    }

    [Fact]
    public void ApplyClampToBounds_境界内の矩形は通知しない()
    {
        var session = CreateBoundedSession(null, out var notices);
        session.SetSelection(new EsRect(10, 10, 50, 50));
        notices.Clear();

        session.ApplyClampToBounds();

        Assert.Empty(notices);
    }

    // ====================================================================
    // 座標変換（ポート・ヒットテスト半径）
    // ====================================================================

    [Fact]
    public void ToDomainとToDip_ポートの変換を適用する()
    {
        var session = CreateSession(null, out _);
        // 表示スケール 2.0（1 DIP = 0.5 ドメイン）のポート
        session.SetPort(CoordTransform.Scale(2.0));

        Assert.Equal(new EsPoint(5, 10), session.ToDomain(new EsPoint(10, 20)));
        Assert.Equal(new EsPoint(20, 40), session.ToDip(new EsPoint(10, 20)));
    }

    [Fact]
    public void ToDomain_ポート未設定時は等価変換になる()
    {
        var session = CreateSession(null, out _);

        Assert.Equal(new EsPoint(10, 20), session.ToDomain(new EsPoint(10, 20)));
    }

    [Fact]
    public void Press_スケール変換ではヒットテスト半径もドメイン単位へ換算する()
    {
        var session = CreateSession(null, out _);
        // 表示スケール 2.0（1 DIP = 0.5 ドメイン）にヒット半径 12 DIP を設定する（ドメイン半径 6）
        session.SetPort(CoordTransform.Scale(2.0));
        session.HandleHitRadius = 12.0;
        session.SetSelection(new EsRect(50, 50, 100, 80));

        // 角から 5 ドメイン離れた位置はヒットする（ドメイン半径 6 以内）
        var hit = session.Press(new EsPoint(150 + 5, 130 + 5), clickCount: 1);
        Assert.True(hit.Cursor.HasFlag(DragRectCursorAction.MoveTo));

        // 角から 20 ドメイン離れた位置はハンドルにヒットしない（矩形外のため新規選択）
        session.Reset();
        session.SetSelection(new EsRect(50, 50, 100, 80));
        var miss = session.Press(new EsPoint(150 + 20, 130 + 20), clickCount: 1);
        Assert.False(miss.Cursor.HasFlag(DragRectCursorAction.MoveTo));
    }
}
