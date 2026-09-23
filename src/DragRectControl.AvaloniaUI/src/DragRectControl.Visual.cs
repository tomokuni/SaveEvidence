using Estable.AvaloniaUI.Marquee.Drawing;
using Estable.Marquee;
using EsUtil.Platform.AvaloniaUI;
using EsUtil.Platform.Core.Geometry;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace Estable.AvaloniaUI.Marquee;

/// <summary>
/// DragRectControl のビジュアルツリー接続・ScrollViewer 連携・描画処理（partial 分割）。
/// </summary>
/// <remarks>
/// ビジュアルツリー接続時に TopLevel のキーイベントを購読し、切断時に解除する。<br/>
/// 連携 ScrollViewer（スクロール・ズーム検知）は明示注入のみで設定する（暗黙の祖先探索は行わない）。<br/>
/// 描画は <see cref="Drawing.MarqueeDrawer"/> に委譲し、座標変換（ドメイン → DIP）はセッションのポートに問い合わせる
/// （描画はセッションの状態と <see cref="Options"/> を読むだけで、判断を持たない）。
/// </remarks>
public sealed partial class MarqueeControl
{
    // ====================================================================
    // ビジュアルツリー接続・切断（ScrollViewer 購読）
    // ====================================================================

    /// <summary>ビジュアルツリー接続時に、属する TopLevel のキーイベントを購読する。</summary>
    /// <param name="e">ビジュアルツリー接続イベント。</param>
    /// <remarks>
    /// Drag 開始前（マウス押下前）は本コントロールにフォーカスがなく、コントロール自身のキーハンドラが呼ばれないため、<br/>
    /// 属するウィンドウのキーイベントを購読して矢印キー nudge を自己完結で処理する。<br/>
    /// <b>トンネル（Tunnel）で購読する</b>: バブリングでは祖先の <c>ScrollViewer</c> が先に矢印キーを処理して
    /// キーボードスクロールしてしまう（Crop モードのプレビューは ScrollViewer 内にある）。<br/>
    /// トンネルはルートから下降するため、祖先より先に受け取って処理済みにできる。
    /// これにより「コントロール自身のハンドラ（フォーカス時）と TopLevel 購読（フォーカス非依存）」の
    /// 2 経路を持たずに済み、同一キーの二重処理を防ぐ仕組みが不要になる（配送は 1 経路）。<br/>
    /// 連携 ScrollViewer（スクロール・ズーム検知）は <see cref="AttachScrollViewer"/> による明示注入のみで設定する<br/>
    /// （暗黙の祖先探索は行わない。ホストが連携対象を明示的に指定する）。
    /// </remarks>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (TopLevel.GetTopLevel(this) is { } topLevel)
        {
            _attachedTopLevel = topLevel;
            // handledEventsToo: true で購読する。
            // 他のコントロール（ScrollViewer 等）が e.Handled=true にしたキーでも呼ばれるようにするため。
            topLevel.AddHandler(InputElement.KeyDownEvent, OnTopLevelKeyDown, RoutingStrategies.Tunnel, true);
        }
    }

    /// <summary>ビジュアルツリー切断時に TopLevel のキーイベントと ScrollViewer のスクロール購読を解除する。</summary>
    /// <param name="e">ビジュアルツリー切断イベント。</param>
    /// <remarks>
    /// ウィンドウクローズ・Crop 終了等でビジュアルツリーから外れる際に、キーイベント購読を解除し、<br/>
    /// セッションへ対話の終了を通知する（nudge 中ならポインターを再表示して選択状態を初期化する）。
    /// </remarks>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_attachedTopLevel is { } topLevel)
        {
            topLevel.RemoveHandler(InputElement.KeyDownEvent, OnTopLevelKeyDown);
            _attachedTopLevel = null;
        }
        if (_attachedScrollViewer is { } scrollViewer)
        {
            scrollViewer.ScrollChanged -= OnScrollViewerChanged;
            _attachedScrollViewer = null;
        }
        ApplyFeedback(_session.Detach());
    }

    /// <summary>連携する ScrollViewer を明示的に設定する（スクロール・ズーム検知 + ホイール操作）。</summary>
    /// <param name="scrollViewer">連携する ScrollViewer。null で解除。</param>
    /// <remarks>
    /// ホスト（上位 AP）が Crop モード等でプレビューの ScrollViewer を明示的に注入する。<br/>
    /// - <c>ScrollChanged</c>（スクロール・ズーム・画像サイズ変更）を購読し、<see cref="ViewportChanged"/> を発行する<br/>
    ///   （上位の暗転マスク等がビューポート座標で再描画するため・イベント駆動・ポーリングなし）。<br/>
    /// - 本コントロール上のマウスホイールを ScrollViewer へ転送する（Ctrl+ホイールは <see cref="ZoomRequested"/>）。<br/>
    /// 祖先探索は行わない（明示注入が唯一の連携方式・暗黙の依存を排除）。
    /// </remarks>
    public void AttachScrollViewer(ScrollViewer? scrollViewer)
    {
        if (ReferenceEquals(_attachedScrollViewer, scrollViewer)) return;
        if (_attachedScrollViewer is { } old)
        {
            old.ScrollChanged -= OnScrollViewerChanged;
        }
        _attachedScrollViewer = scrollViewer;
        if (scrollViewer is not null)
        {
            scrollViewer.ScrollChanged += OnScrollViewerChanged;
        }
    }

    /// <summary>祖先 ScrollViewer のスクロール・ズーム変化を検知し、再描画と <see cref="ViewportChanged"/> を発行する。</summary>
    /// <param name="sender">イベント送信元（ScrollViewer）。</param>
    /// <param name="e">スクロール変化イベント。</param>
    /// <remarks>ビューポートの変化は選択矩形（ドメイン座標）を変えないため、再描画と通知のみをセッションへ要求する。</remarks>
    private void OnScrollViewerChanged(object? sender, ScrollChangedEventArgs e)
        => ApplyFeedback(_session.NotifyViewportChanged());

    // ====================================================================
    // 描画（外側の装飾は装飾コアへ、選択矩形は DragRectDrawer へ委譲する）
    // ====================================================================

    /// <summary>コントロールを描画する。</summary>
    /// <param name="context">描画コンテキスト。</param>
    /// <remarks>
    /// 描画は 2 層に分かれる。<br/>
    /// - <b>外側の装飾</b>（暗転マスク・穴あけ枠・ラベル）: 装飾コアのフレームを <see cref="MarqueeOuterDrawer.Draw"/> へ渡して描く。<br/>
    /// - <b>選択矩形そのもの</b>（ドラッグハンドル・始点マーカー・座標ラベル）: <see cref="Drawing.DragRectDrawer"/> で描く。<br/>
    /// <br/>
    /// 選択枠線は外側の装飾の穴あけ枠（同じ矩形の内側）と重なるため本コントロールでは描画しない<br/>
    /// （別々に描くと線幅が二重になり見た目が変わる）。枠線の色と太さは <see cref="MarqueeOptions.OuterAppearance"/> が単一所有する。<br/>
    /// セッションの状態（選択矩形・nudge・マウス位置）を読み、DIP へ変換して描画層へ渡すだけである。<br/>
    /// <br/>
    /// <b>ヒットテスト</b>: <see cref="Control"/> は背景が描画されない場合にポインターイベントを受け取れないため、<br/>
    /// 領域全体を透明ブラシで塗ってヒットテスト対象を確保する（選択なしの初期状態でもドラッグを開始できる）。<br/>
    /// 外側の装飾（暗転マスク）は選択の有無に関係なく描画する（ハイライトのみを表示するモードを含む）。
    /// </remarks>
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        // ヒットテスト領域の確保（透明ブラシ）。選択なしの初期状態でもポインター操作を受けるため。
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));

        var options = Options;
        var bounds = Bounds;

        // 選択矩形の外側に描く装飾（暗転マスク・穴あけ枠・ラベル）。
        // どの矩形を暗転するか・枠線を描くか・ラベルをどう組むかは装飾コアが持つため、本層はフレームを描画 API へ写すだけとする。
        MarqueeOuterDrawer.Draw(
            context,
            MarqueeFrame.Resolve(
                _session,
                new EsRect(0, 0, bounds.Width, bounds.Height),
                _coordinatePort,
                Scaling,
                SizeLabelOverrideProvider,
                DisplayScaleScaling));

        if (options.ShowHandles && _session.HasSelection)
        {
            // 選択矩形はドメインピクセルで保持されるため、描画前にローカル DIP に変換する（閉区間 +1 の規則はコアが単一所有）。
            var dipRect = _session.ToDipRect(_session.SelectionRect);
            var rect = new Rect(dipRect.X, dipRect.Y, dipRect.Width, dipRect.Height);

            // 色は UI 非依存の型（System.Drawing.Color）で保持されるため、描画時に Avalonia の色型へ変換する
            MarqueeDrawer.DrawHandles(
                context, rect, options.HandleSize, options.HandleColor.ToAvaloniaColor(),
                options.OuterAppearance.BorderColor.ToAvaloniaColor());
        }

        // nudge 中はドラッグ中のハンドル位置に十字マーカーを表示する（仕様 4.4）
        if (_session.NudgeActive && _session.CrosshairVisible)
        {
            var markerDip = _session.ToDip(_session.MarkerPosition);
            MarqueeDrawer.DrawStartMarker(
                context, markerDip.ToAvaloniaPoint(), options.MarkerColor.ToAvaloniaColor(),
                MarqueeAppearance.MarkerThickness, options.MarkerLength / 2);
        }

        // カーソルの右側にカーソルのドメイン座標ラベル（"(x, y)"）を表示する
        if (options.ShowCoordLabel)
        {
            var cursorDip = _session.ToDip(_session.LastMousePosition);
            MarqueeDrawer.DrawCoordLabel(
                context, cursorDip.ToAvaloniaPoint(), _session.LastMousePosition.ToAvaloniaPoint(), Bounds.Size,
                options.CoordLabelColor.ToAvaloniaColor(), options.CoordLabelBackgroundColor.ToAvaloniaColor(),
                options.CoordLabelFontSize);
        }
    }
}
