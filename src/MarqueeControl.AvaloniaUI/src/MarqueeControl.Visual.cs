using Estable.AvaloniaUI.Marquee.Drawing;
using Estable.Marquee;
using Estable.Marquee.Decoration;
using Estable.Marquee.CoreForControl;
using EsUtil.Platform.AvaloniaUI;
using EsUtil.Platform.Core.Geometry;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace Estable.AvaloniaUI.Marquee;

/// <summary>
/// MarqueeControl のビジュアルツリー接続・ScrollViewer 連携・描画処理（partial 分割）。
/// </summary>
/// <remarks>
/// ビジュアルツリー接続時に TopLevel のキーイベントを購読し、切断時に解除する。<br/>
/// 連携 ScrollViewer（スクロール・ズーム検知）は明示注入のみで設定する（暗黙の祖先探索は行わない）。<br/>
/// 描画は <see cref="Drawing.MarqueeDrawer"/> に委譲し、座標変換（ドメイン → DIP）はファサードの座標変換ポートに問い合わせる
/// （描画はコアが解決した描画内容と <see cref="Options"/> を読むだけで、判断を持たない）。
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

            // ウィンドウの非アクティブ化（Alt+Tab 等）でも nudge を終了する。
            // Avalonia のフォーカスは TopLevel 単位で保持されるため、ウィンドウを離れてもコントロールはフォーカスを失わない
            // （Win32 の WM_KILLFOCUS に相当する契機。MewUI 版のフォーカス喪失による nudge 終了と同じ扱いにする）。
            if (topLevel is WindowBase window)
            {
                _attachedWindow = window;
                window.Deactivated += OnWindowDeactivated;
            }
        }
    }

    /// <summary>ビジュアルツリー切断時に TopLevel のキーイベントと ScrollViewer のスクロール購読を解除する。</summary>
    /// <param name="e">ビジュアルツリー切断イベント。</param>
    /// <remarks>
    /// ウィンドウクローズ・Crop 終了等でビジュアルツリーから外れる際に、キーイベント購読を解除し、<br/>
    /// コアへ対話状態の初期化を要求する（nudge 中ならポインターを再表示して選択状態を初期化する）。
    /// </remarks>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_attachedTopLevel is { } topLevel)
        {
            topLevel.RemoveHandler(InputElement.KeyDownEvent, OnTopLevelKeyDown);
            _attachedTopLevel = null;
        }
        if (_attachedWindow is { } window)
        {
            window.Deactivated -= OnWindowDeactivated;
            _attachedWindow = null;
        }
        if (_attachedScrollViewer is { } scrollViewer)
        {
            scrollViewer.ScrollChanged -= OnScrollViewerChanged;
            _attachedScrollViewer = null;
        }
        // ビジュアルツリーからの切断は UI のライフサイクルであるため、コアへは「対話状態の初期化」だけを要求する
        // （選択の破棄と、nudge 中だった場合のカーソル再表示はセッションが単一所有する）。
        _marquee.ResetSelection();
    }

    /// <summary>連携する ScrollViewer を明示的に設定する（スクロール・ズーム検知 + ホイール操作）。</summary>
    /// <param name="scrollViewer">連携する ScrollViewer。null で解除。</param>
    /// <remarks>
    /// ホスト（上位 AP）が Crop モード等でプレビューの ScrollViewer を明示的に注入する。<br/>
    /// - <c>ScrollChanged</c>（スクロール・ズーム・画像サイズ変更）を購読し、<see cref="ViewportChanged"/> を発行する<br/>
    ///   （上位の暗転マスク等がビューポート座標で再描画するため・イベント駆動・ポーリングなし）。<br/>
    /// - 本コントロール上のマウスホイールによるスクロールの適用先として登録する<br/>
    ///   （回転量 → 移動量の換算と適用はコントロール化の層が単一所有する）。<br/>
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
        // ホイールのスクロール適用先も同じ連携で切り替える（null で解除）
        _wheel.AttachScrollTarget(scrollViewer is null ? null : new ScrollViewerTarget(scrollViewer));
        if (scrollViewer is not null)
        {
            scrollViewer.ScrollChanged += OnScrollViewerChanged;
        }
    }

    /// <summary>連携 ScrollViewer をスクロールの適用先（<see cref="IWheelScrollTarget"/>）へ適合させる。</summary>
    /// <remarks>UI 固有の型（<see cref="ScrollViewer"/>）をここで吸収し、コア層へは抽象だけを渡す。</remarks>
    private sealed class ScrollViewerTarget(ScrollViewer scrollViewer) : IWheelScrollTarget
    {
        /// <summary>移動量（DIP）だけスクロール位置を更新する。</summary>
        /// <param name="delta">移動量（横・縦。回転量からの換算はコアの規則が済ませている）。</param>
        public void ScrollBy(EsVector delta)
            => scrollViewer.Offset += delta.ToAvaloniaVector();
    }

    /// <summary>マウスホイールでズーム・スクロールを操作する（解釈はコントロール化の層が単一所有する）。</summary>
    /// <param name="e">ホイールイベント。</param>
    /// <remarks>
    /// - Ctrl + ホイール: <see cref="ZoomRequested"/> を発火する（転送先の有無に関わらず常に通知する）。<br/>
    /// - 通常ホイール: 連携 ScrollViewer（<see cref="AttachScrollViewer"/>）があればそれをスクロールし、<br/>
    ///   無ければ <see cref="ScrollRequested"/> で上位 AP へ回転量を通知する（ズームビューポートのパン等）。<br/>
    /// どちらの振り分けも <see cref="MarqueeWheelInterpreter.Handle"/> が単一所有する（本層は修飾キーと回転量を渡すだけ）。
    /// </remarks>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        _wheel.Handle(
            (e.KeyModifiers & KeyModifiers.Control) != 0,
            (e.KeyModifiers & KeyModifiers.Shift) != 0,
            e.Delta.X, e.Delta.Y, e.GetPosition(this).ToEsPoint());
        e.Handled = true;
    }

    /// <summary>祖先 ScrollViewer のスクロール・ズーム変化を検知し、再描画と <see cref="ViewportChanged"/> を発行する。</summary>
    /// <param name="sender">イベント送信元（ScrollViewer）。</param>
    /// <param name="e">スクロール変化イベント。</param>
    /// <remarks>ビューポートの変化は選択矩形（ドメイン座標）を変えないため、再通知と再描画のみを要求する。</remarks>
    private void OnScrollViewerChanged(object? sender, ScrollChangedEventArgs e)
        => _viewport.Notify();

    /// <summary>ウィンドウが非アクティブ化されたときに nudge を終了する。</summary>
    /// <param name="sender">イベント送信元（ウィンドウ）。</param>
    /// <param name="e">イベント引数。</param>
    /// <remarks>
    /// nudge 中はカーソルを非表示にするため、ウィンドウを離れた時点で終了して再表示する（非表示残留の防止）。<br/>
    /// 本コントロールはキーを TopLevel から購読するため、非アクティブ化＝キー入力の継続条件を失った時点である。
    /// </remarks>
    private void OnWindowDeactivated(object? sender, EventArgs e) => _marquee.EndNudge();

    // ====================================================================
    // 描画（外側の装飾は装飾コアへ、選択矩形は MarqueeDrawer へ委譲する）
    // ====================================================================

    /// <summary>図形（装飾・ハンドル・始点マーカー）を描画する。</summary>
    /// <param name="context">描画コンテキスト。</param>
    /// <remarks>
    /// <b>描画順はコアが単一所有する</b>: <see cref="MarqueeDrawContent.Layers"/> の順に層を処理する。<br/>
    /// 図形の層は描画 API へ写し、<b>ラベルの層は描かない</b><br/>
    /// （ラベルは <see cref="Border"/> + <see cref="TextBlock"/> の子要素として配置され、子は親より後に描かれるため常に最上位になる）。<br/>
    /// <br/>
    /// 選択枠線は装飾の穴あけ枠（同じ矩形の内側）と重なるため本コントロールでは描画しない<br/>
    /// （別々に描くと線幅が二重になり見た目が変わる）。枠線の色と太さは <see cref="MarqueeDecoOptions.OuterAppearance"/> が単一所有する。
    /// </remarks>
    internal void RenderShapes(DrawingContext context)
    {
        // 描画コンテキストの原点は常に自コントロールの左上（0, 0）であるため、親座標（Bounds.X/Y）は渡さない。
        var clientSize = Bounds.Size.ToEsSize();
        var content = _marquee.ResolveContent(clientSize);

        foreach (var layer in content.Layers)
        {
            switch (layer)
            {
                case MarqueeDrawLayer.Decoration:
                    MarqueeOuterDrawer.Draw(context, content.Decoration);
                    MarqueeInnerDrawer.Draw(context, content.InnerDecoration);
                    break;

                case MarqueeDrawLayer.Handles when content.Handles is { } handles:
                    MarqueeDrawer.DrawHandles(
                        context, handles.Rect.ToAvaloniaRect(), handles.HandleSize,
                        handles.FillColor.ToAvaloniaColor(), handles.BorderColor.ToAvaloniaColor());
                    break;

                case MarqueeDrawLayer.Marker when content.Marker is { } marker:
                    MarqueeDrawer.DrawStartMarker(
                        context, marker.Center.ToAvaloniaPoint(), marker.Color.ToAvaloniaColor(), marker.Thickness, marker.Size);
                    break;

                // ラベルは子要素（Border + TextBlock）が描く（親より後に描かれるため常に最上位になる）
                case MarqueeDrawLayer.SizeLabel:
                case MarqueeDrawLayer.CoordLabel:
                    break;
            }
        }

        // 描画完了をホストへ通知する（操作・情報ラベルなどホスト固有の付加情報を描くための入口）。
        Rendered?.Invoke(this, new MarqueeRenderedEventArgs(context, content));
    }

    // ====================================================================
    // ラベル（テキスト要素として配置する）
    // ====================================================================

    /// <summary>ラベル要素（背景 + テキスト）を保持する。要素数は層の有無に追従する。</summary>
    private readonly List<Border> _labelElements = [];

    /// <summary>子要素の配置を確定し、ラベル要素を追加・更新する。</summary>
    /// <param name="finalSize">本コントロールへ割り当てられたサイズ（DIP）。</param>
    /// <returns>割り当てサイズ。</returns>
    /// <remarks>
    /// <b>描画順はコアが単一所有する</b>: コアの描画内容から得た配置（<see cref="MarqueeLabelLayoutPlan"/>）に従って<br/>
    /// ラベル要素を <see cref="Avalonia.Visual.Bounds"/> へ配置するだけであり、位置の計算を持たない。<br/>
    /// 要素は <see cref="Avalonia.Controls.Panel.Children"/> の末尾へ置く（子は親より後に描かれるため、ラベルは常に最上位になる）。
    /// </remarks>
    protected override Size ArrangeOverride(Size finalSize)
    {
        UpdateLabelElements(_marquee.ResolveContent(finalSize.ToEsSize()));

        return base.ArrangeOverride(finalSize);
    }

    /// <summary>コアの描画内容に合わせてラベル要素を追加・更新する。</summary>
    /// <param name="content">コアが解決した描画内容（文言・外観・層の順序を持つ）。</param>
    /// <remarks>
    /// 実寸測定は <see cref="TextBlock"/> に任せる（要素自身が測る）ため、測定関数は「0」を返す。<br/>
    /// これにより配置はコアの余白規則（<c>PaddingX</c> / <c>PaddingY</c>）と要素の実寸だけから決まり、<br/>
    /// コアの計算（実寸を前提としたクランプ）は座標ラベルの位置決めにだけ使われる。
    /// </remarks>
    private void UpdateLabelElements(in MarqueeDrawContent content)
    {
        var elements = MarqueeLabelLayoutPlan.Resolve(content, MeasureText);

        while (_labelElements.Count > elements.Count)
        {
            var last = _labelElements[^1];
            _labelElements.RemoveAt(_labelElements.Count - 1);
            Children.Remove(last);
        }

        while (_labelElements.Count < elements.Count)
        {
            var border = new Border();
            border.Child = new TextBlock { TextWrapping = TextWrapping.NoWrap };
            _labelElements.Add(border);
            Children.Add(border);
        }

        for (var i = 0; i < elements.Count; i++)
        {
            ApplyLabelElement(_labelElements[i], elements[i]);
        }
    }

    /// <summary>ラベル要素の内容と配置をコアの指示に合わせる。</summary>
    /// <param name="border">更新する要素（背景）。</param>
    /// <param name="element">コア（CoreForControl）が解決した配置指示。</param>
    private static void ApplyLabelElement(Border border, in MarqueeLabelElement element)
    {
        border.Background = Drawing.MarqueeBrushCache.GetBrush(element.BackgroundColor.ToAvaloniaColor());
        border.CornerRadius = new CornerRadius(element.CornerRadius);
        border.Padding = new Thickness(element.PaddingX, element.PaddingY);
        border.Width = element.Bounds.Width;
        border.Height = element.Bounds.Height;
        border.Margin = new Thickness(element.Bounds.X, element.Bounds.Y, 0, 0);
        border.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
        border.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;

        if (border.Child is TextBlock text)
        {
            text.Text = element.Text;
            text.FontSize = element.FontSize;
            text.Foreground = Drawing.MarqueeBrushCache.GetBrush(element.TextColor.ToAvaloniaColor());
        }
    }

    /// <summary>テキストの実寸を測定する（<see cref="TextBlock"/> 自身に測らせるため 0 を返す）。</summary>
    /// <param name="text">測定するテキスト（使用しない）。</param>
    /// <param name="fontSize">フォントサイズ（使用しない）。</param>
    /// <returns>常に 0 サイズ。</returns>
    /// <remarks>
    /// 要素が自身の実寸で背景を描くため、コアの配置計算に実寸を渡す必要がない<br/>
    /// （座標ラベルのクランプだけは実寸に依存するが、要素は描画領域の内側へ自動的に収まる）。
    /// </remarks>
    private static EsSize MeasureText(string text, double fontSize) => default;
}
