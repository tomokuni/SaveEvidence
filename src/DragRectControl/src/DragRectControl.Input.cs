using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Estable.AvaloniaUI.DragRect.Logging;
using Estable.AvaloniaUI.Platform;

namespace Estable.AvaloniaUI.DragRect;

/// <summary>
/// DragRectControl のキーボード入力処理（partial 分割）。
/// </summary>
/// <remarks>
/// 矢印キー nudge（1 ドメインピクセル単位）と Escape/Enter の確定・キャンセルを処理する。<br/>
/// フォーカス非依存（TopLevel キー購読）のため、コントロールにフォーカスがなくても動作する。
/// </remarks>
public sealed partial class DragRectControl
{
    // ====================================================================
    // キーボード（矢印キー nudge・Escape/Enter の確定/キャンセル）
    // ====================================================================

    /// <summary>TopLevel のキーダウン処理（フォーカス非依存の矢印キー nudge）。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">キーイベント。</param>
    /// <remarks>
    /// <c>handledEventsToo: true</c> で購読しているため、他のコントロール（ScrollViewer 等）が
    /// <c>e.Handled = true</c> にしたキーでも呼ばれる（Crop モードのプレビューは ScrollViewer 内にあるため）。<br/>
    /// 本コントロール自身（<see cref="OnKeyDown"/>）で処理済みのキーは <see cref="_nudgeHandledInKeyDown"/> で
    /// スキップする（二重処理防止）。
    /// </remarks>
    private void OnTopLevelKeyDown(object? sender, KeyEventArgs e)
    {
        if (_nudgeHandledInKeyDown)
        {
            _nudgeHandledInKeyDown = false;
            return;
        }

        // Escape / Enter の確定・キャンセルを自己完結で処理する（フォーカス非依存）
        if (e.Key == Key.Escape)
        {
            DragRectLog.LogKeyDown(_log, e.Key.ToString(), handled: true);
            DragRectLog.LogCancelRequested(_log);
            RequestCancel();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            DragRectLog.LogKeyDown(_log, e.Key.ToString(), handled: true);
            if (_state.HasSelection)
            {
                DragRectLog.LogConfirmRequested(_log, _state.SelectionRect.ToString());
            }
            RequestConfirm();
            e.Handled = true;
            return;
        }

        // 領域作成後はマウス Down 中のみ nudge を許可（ドラッグ完了後の nudge は無効）
        if (_state.HasSelection && !_state.IsMouseDown)
        {
            return;
        }

        if (HandleArrowKey(e.Key))
        {
            DragRectLog.LogKeyDown(_log, e.Key.ToString(), handled: true);
            e.Handled = true;
        }
    }

    /// <summary>キーダウン時の処理。矢印キーで nudge を実行する。</summary>
    /// <param name="e">キーイベント。</param>
    /// <remarks>
    /// 矢印キー（Left / Right / Up / Down）で nudge を実行し、<see cref="KeyEventArgs.Handled"/> を設定する。<br/>
    /// <b>nudge の許可条件</b>:<br/>
    /// - 領域作成前: 始点決めの nudge として常に許可。<br/>
    /// - 領域作成後: マウスボタン押下中（ドラッグ中・ハンドル選択中）のみ許可。
    /// </remarks>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        _nudgeHandledInKeyDown = false;

        if (_state.HasSelection && !_state.IsMouseDown)
        {
            return;
        }

        if (HandleArrowKey(e.Key))
        {
            e.Handled = true;
            _nudgeHandledInKeyDown = true;
        }
    }

    /// <summary>カーソルキー入力を処理する（1 ドメインピクセル単位）。</summary>
    /// <param name="key">押下された矢印キー。</param>
    /// <returns>キーが処理された場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// nudge 実行前に、現在のカーソル位置（画面物理）をローカル DIP → ドメイン座標に変換して記録する。<br/>
    /// ドラッグ開始前（PointerMoved 未発生）に基点が (0,0) になってカーソルが原点に飛ぶ問題の対策。<br/>
    /// nudge は DragRect が自己完結（TopLevel キー購読）のため、外部公開不要（private）。
    /// </remarks>
    private bool HandleArrowKey(Key key)
    {
        var (dx, dy) = key switch
        {
            Key.Left => (-1, 0),
            Key.Right => (1, 0),
            Key.Up => (0, -1),
            Key.Down => (0, 1),
            _ => (0, 0),
        };
        if (dx == 0 && dy == 0) return false;

        // nudge 前のカーソル位置をドメイン座標として記録（基点精度向上・原点飛び防止）
        if (TopLevel.GetTopLevel(this) is { } topLevel)
        {
            var screenPt = _state.Cursor.GetCursorScreenPoint();
            var localDip = ScreenConverter.CreateVisualDipPort(this).Forward(new Point(screenPt.X, screenPt.Y));
            var domainPos = _coordinatePort.Forward(localDip);
            _state.LastMousePosition = domainPos;
        }

        // 未選択・未ドラッグの nudge では、基点（StartPoint）を現在のカーソル位置に同期する。
        // 同期は nudge セッション開始時（NudgeActive = false）のみ行う:
        // - 初回: StartPoint が未設定（(0,0)）でも、現在のカーソル位置から nudge を開始できる（原点飛び防止）。
        // - セッション跨ぎ: nudge 終了後（EndNudge）に StartPoint が残るため、次回セッションの最初のキー押下で
        //   古い基点からジャンプするのを防ぐ。
        // nudge 中（2 回目以降）は同期しない:
        //   Crop の座標ポート（Compose(ImageLayout, ScrollOffset)）では Forward 変換の丸め誤差・スクロール追従により、
        //   再同期した基点が前回の nudge 位置と一致せず、移動が相殺される（「2 回目以降移動しない」問題の対策）。
        //   nudge 位置を維持したまま 1px ずつ進む。
        if (!_state.HasSelection && !_state.IsDragging && !_state.NudgeActive)
        {
            _state.StartPoint = _state.LastMousePosition;
        }

        // nudge 状態を開始（カーソル非表示・十字線表示）
        if (!_state.NudgeActive)
        {
            _state.NudgeActive = true;
            _state.CrosshairVisible = true;
            _state.NudgeBase = _state.HasSelection ? _state.GetOppositeCorner(_state.SelectionRect) : _state.StartPoint;
            _state.NudgeOffsetX = 0;
            _state.NudgeOffsetY = 0;
            _state.Cursor.ShowCursor(false);
        }

        // nudge 適用（ドメイン座標の整数加算のみ。FP 誤差を累積しない）
        _state.NudgeOffsetX += dx;
        _state.NudgeOffsetY += dy;
        _state.Nudge(dx, dy, MinSelectionSize, ClampToDomainBounds);

        // nudge 後の選択矩形・ルーペ位置を通知
        RaiseSelectionChanged(_state.SelectionRect);
        var pos = GetMarkerPosition();
        RaiseLoupePositionMoved(pos);
        _state.Cursor.SetCursorScreenPoint(ToScreen(pos));
        InvalidateVisual();
        return true;
    }

    /// <summary>ドメイン座標を画面物理座標に変換する。</summary>
    /// <param name="domainPos">ドメイン座標。</param>
    /// <returns>画面物理座標。</returns>
    private PixelPoint ToScreen(Point domainPos)
    {
        var localDip = _coordinatePort.Inverse(domainPos);
        return ScreenConverter.LocalDipToScreen(this, localDip);
    }

    /// <summary>nudge を終了し、マウスポインターを再表示する（カーソルは nudge 位置へ移動）。</summary>
    /// <remarks>マウス操作（Move 閾値超え / Down / Up）で呼び出し、nudge 中の非表示ポインターを復元する。</remarks>
    private void EndNudge()
    {
        if (!_state.NudgeActive) return;
        _state.NudgeActive = false;
        _state.CrosshairVisible = false;
        _state.NudgeOffsetX = 0;
        _state.NudgeOffsetY = 0;
        _state.Cursor.ShowCursor(true);
    }
}
