using Avalonia.Controls;
using Avalonia.Input;
using EsUtil.Platform.AvaloniaUI;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Geometry;

namespace Estable.AvaloniaUI.DragRect;

/// <summary>
/// DragRectControl のキーボード入力処理（partial 分割）。
/// </summary>
/// <remarks>
/// キー入力をコア型（<see cref="EsKey"/>）へ変換してセッションへ通知し、返された効果を適用するだけである。<br/>
/// 矢印キー nudge・Escape のキャンセル・Enter の確定の判断（許可条件を含む）はコアが行う。<br/>
/// キーボード（矢印キー nudge）はフォーカス非依存（TopLevel のキー購読）のため、コントロールにフォーカスがなくても動作する。
/// </remarks>
public sealed partial class DragRectControl
{
    // ====================================================================
    // キーボード（矢印キー nudge・Escape/Enter の確定/キャンセル）
    // ====================================================================

    /// <summary>TopLevel のキーダウン処理（フォーカス非依存のキー処理）。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">キーイベント。</param>
    /// <remarks>
    /// <c>handledEventsToo: true</c> で購読しているため、他のコントロール（ScrollViewer 等）が
    /// <c>e.Handled = true</c> にしたキーでも呼ばれる（Crop モードのプレビューは ScrollViewer 内にあるため）。<br/>
    /// 本コントロール自身（<see cref="OnKeyDown"/>）で処理済みのキーは <see cref="_nudgeHandledInKeyDown"/> で
    /// スキップする（Avalonia のルーティングで本ハンドラと <see cref="OnKeyDown"/> の双方が呼ばれるための二重処理防止）。
    /// </remarks>
    private void OnTopLevelKeyDown(object? sender, KeyEventArgs e)
    {
        if (_nudgeHandledInKeyDown)
        {
            _nudgeHandledInKeyDown = false;
            return;
        }

        var feedback = _session.KeyDown(e.Key.ToEsKey(), CurrentCursorDomainPosition());
        if (!feedback.Handled) return;

        e.Handled = true;
        ApplyFeedback(feedback);
    }

    /// <summary>キーダウン時の処理。矢印キーの nudge を処理する。</summary>
    /// <param name="e">キーイベント。</param>
    /// <remarks>
    /// 本コントロールにフォーカスがある場合は矢印キーのみを扱う（Escape / Enter は TopLevel 購読が処理する）。<br/>
    /// ここで Escape / Enter も扱うと、本ハンドラの後に TopLevel 購読も呼ばれて二重に処理される（処理済みの選択破棄が 2 回走る）。<br/>
    /// 処理したキーは <see cref="_nudgeHandledInKeyDown"/> に記録し、TopLevel 購読での再処理を防ぐ。
    /// </remarks>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        _nudgeHandledInKeyDown = false;

        var feedback = _session.NudgeByKey(e.Key.ToEsKey(), CurrentCursorDomainPosition());
        if (!feedback.Handled) return;

        e.Handled = true;
        _nudgeHandledInKeyDown = true;
        ApplyFeedback(feedback);
    }

    /// <summary>現在のカーソル位置（ドメイン座標）を取得する。</summary>
    /// <returns>カーソル位置（ドメイン座標）。TopLevel を特定できない場合は null。</returns>
    /// <remarks>
    /// nudge の基点精度を上げるために記録する（マウス移動が未発生でも現在位置から微調整を開始でき、原点へ飛ばない）。<br/>
    /// 画面物理座標 → ビジュアルの DIP → ドメイン座標の順に変換する（境界の 1 行変換）。
    /// </remarks>
    private EsPoint? CurrentCursorDomainPosition()
    {
        if (TopLevel.GetTopLevel(this) is null) return null;

        var screenPoint = _cursor.GetCursorScreenPoint();
        var localDip = ScreenConverter.CreateVisualDipPort(this).Forward(new EsPoint(screenPoint.X, screenPoint.Y));
        return _session.ToDomain(localDip);
    }
}
