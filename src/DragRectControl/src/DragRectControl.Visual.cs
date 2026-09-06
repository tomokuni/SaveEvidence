using Estable.AvaloniaUI.DragRect.Drawing;
using Estable.AvaloniaUI.Platform.Geometry;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using AvaloniaRect = Avalonia.Rect;
using Estable.Primitives;

namespace Estable.AvaloniaUI.DragRect;

/// <summary>
/// DragRectControl のビジュアルツリー接続・ScrollViewer 連携・描画処理（partial 分割）。
/// </summary>
/// <remarks>
/// ビジュアルツリー接続時に TopLevel のキーイベントを購読し、切断時に解除する。<br/>
/// 連携 ScrollViewer（スクロール・ズーム検知）は明示注入のみで設定する（暗黙の祖先探索は行わない）。<br/>
/// 描画は <see cref="Drawing.DragRectDrawer"/> に委譲する。
/// </remarks>
public sealed partial class DragRectControl
{
    // ====================================================================
    // ビジュアルツリー接続・切断（ScrollViewer 購読）
    // ====================================================================

    /// <summary>ビジュアルツリー接続時に、属する TopLevel のキーイベントを購読する。</summary>
    /// <param name="e">ビジュアルツリー接続イベント。</param>
    /// <remarks>
    /// Drag 開始前（マウス押下前）は本コントロールにフォーカスがなく、<c>OnKeyDown</c> が呼ばれないため、<br/>
    /// 属するウィンドウのキーイベントを購読して矢印キー nudge を自己完結で処理する。<br/>
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
            // Crop モードではプレビューが ScrollViewer 内にあり、フォーカスがプレビューにあると
            // ScrollViewer が矢印キーを処理して e.Handled=true にする（キーボードスクロール）。
            topLevel.AddHandler(InputElement.KeyDownEvent, OnTopLevelKeyDown, RoutingStrategies.Bubble, true);
        }
    }

    /// <summary>ビジュアルツリー切断時に TopLevel のキーイベントと ScrollViewer のスクロール購読を解除する。</summary>
    /// <param name="e">ビジュアルツリー切断イベント。</param>
    /// <remarks>
    /// ウィンドウクローズ・Crop 終了等でビジュアルツリーから外れる際に、<br/>
    /// キーイベント購読を解除し、nudge 中ならポインターを再表示する（仕様 4.4）。<br/>
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
        if (_state.NudgeActive)
        {
            // nudge 中はポインターを再表示する（EndNudge に委譲して重複を排除）
            EndNudge();
        }
        _state.Reset();
        InvalidateVisual();
    }

    /// <summary>連携する ScrollViewer を明示的に設定する（スクロール・ズーム検知 + ホイール操作）。</summary>
    /// <param name="scrollViewer">連携する ScrollViewer。null で解除。</param>
    /// <remarks>
    /// ホスト（上位 AP）が Crop モード等でプレビューの ScrollViewer を明示的に注入する。<br/>
    /// - <c>ScrollChanged</c>（スクロール・ズーム・画像サイズ変更）を購読し、<see cref="ViewportChanged"/> を発行する<br/>
    ///   （上位の FadeMask 等がビューポート座標で再描画するため・イベント駆動・ポーリングなし）。<br/>
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
    private void OnScrollViewerChanged(object? sender, ScrollChangedEventArgs e)
    {
        InvalidateVisual();
        RaiseViewportChanged();
    }

    // ====================================================================
    // 描画（DragRectDrawer への委譲）
    // ====================================================================

    /// <summary>コントロールを描画する。</summary>
    /// <param name="context">描画コンテキスト。</param>
    /// <remarks>
    /// 選択矩形がある場合は枠線（<see cref="Drawing.DragRectDrawer.DrawBorder"/>）と<br/>
    /// ドラッグハンドル（<see cref="Drawing.DragRectDrawer.DrawHandles"/>）を描画する。<br/>
    /// 始点マーカー（<see cref="Drawing.DragRectDrawer.DrawStartMarker"/>）は表示中（未選択の nudge 中）に描画する。<br/>
    /// <br/>
    /// <b>ヒットテスト</b>: <see cref="Control"/> は背景が描画されない場合にポインターイベントを受け取れないため、<br/>
    /// 領域全体を透明ブラシで塗ってヒットテスト対象を確保する（選択なしの初期状態でもドラッグを開始できる）。<br/>
    /// </remarks>
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        // ヒットテスト領域の確保（透明ブラシ）。選択なしの初期状態でもポインター操作を受けるため。
        context.FillRectangle(Brushes.Transparent, new AvaloniaRect(Bounds.Size));

        // 選択矩形はドメインピクセルで保持されるため、描画前にローカル DIP に変換する。
        // 選択領域は閉区間（始点・終点とも含む）を意図する仕様のため、終点（Right / Bottom）に +1 してから変換する
        // （終点のピクセルも枠線・ハンドルに含める。キャプチャの閉区間 +1 と一致）。
        var domainRect = _state.SelectionRect;
        var dipTopLeft = _coordinatePort.Inverse(domainRect.TopLeft);
        var dipBottomRight = _coordinatePort.Inverse(new PointD(domainRect.Right + 1, domainRect.Bottom + 1));
        var rect = new AvaloniaRect(dipTopLeft.ToAvaloniaPoint(), dipBottomRight.ToAvaloniaPoint());
        if (_state.HasSelection)
        {
            DragRectDrawer.DrawBorder(context, rect, BorderColor, BorderThickness);
            DragRectDrawer.DrawHandles(context, rect, HandleSize, HandleColor, BorderColor);
        }

        // nudge 中はドラッグ中のハンドル位置に十字マーカーを表示する（仕様 4.4）
        if (_state.NudgeActive && _state.CrosshairVisible)
        {
            var markerDip = _coordinatePort.Inverse(GetMarkerPosition());
            DragRectDrawer.DrawStartMarker(context, markerDip.ToAvaloniaPoint(), MarkerColor, MarkerThickness, MarkerLength / 2);
        }

        // カーソルの右側にカーソルのドメイン座標ラベル（"(x, y)"）を表示する
        if (ShowCoordLabel)
        {
            var cursorDip = _coordinatePort.Inverse(_state.LastMousePosition);
            DragRectDrawer.DrawCoordLabel(
                context, cursorDip.ToAvaloniaPoint(), _state.LastMousePosition.ToAvaloniaPoint(), Bounds.Size,
                CoordLabelColor, CoordLabelBackgroundColor, CoordLabelFontSize);
        }
    }
}
