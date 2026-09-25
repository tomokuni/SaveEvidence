using Avalonia.Input;
using EsUtil.Platform.AvaloniaUI;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.SaveEvidence.CaptureSession.AvaloniaUI;

/// <summary>
/// OverlayWindow のクリック確定フローとハイライト更新（partial 分割）。
/// </summary>
/// <remarks>
/// ScreenSelect / WindowSelect のマウス移動時のハイライト更新と、<br/>
/// クリック（押下 → 解放）による確定フローを処理する。<br/>
/// 画面物理座標 ↔ ウィンドウローカル DIP / ドメイン座標の変換もここに集約する。
/// </remarks>
internal sealed partial class OverlayWindow
{
    /// <summary>クリック確定型のマウス移動時のハイライト更新。</summary>
    private void OnClickMoved(object? sender, PointerEventArgs e)
    {
        // 画面物理座標への変換は ScreenConverter（PlatformFactory の OS 依存変換集約）に委譲する
        var screenPoint = ScreenConverter.LocalDipToScreen(this, e.GetPosition(this)).ToEsPointInt();
        // 検出関数の呼び分けはコアが単一所有する
        var detection = CaptureTypeTraits.FindUnderCursor(_detector, _captureType, screenPoint);
        var highlight = ApplyHighlight(detection);
        // ハイライト検出結果を外側の装飾（MarqueeControl）の描画へ反映する（再描画要求）
        RequestOverlayRender();
        // ホバー中の枠（ハイライト）を事前切り出しする（確定時の高速化。同じ矩形ならコアがスキップする）
        if (highlight.DomainRect is not null)
        {
            PreExtractClip(detection.Bounds);
        }
    }

    /// <summary>検出結果をハイライトへ反映する。</summary>
    /// <param name="detection">要素の検出結果。</param>
    /// <returns>ハイライトの状態（ハイライトの有無・矩形・表示名とその変化）。</returns>
    /// <remarks>
    /// スクリーン検出・ウィンドウ検出の双方で同じ反映手順（ハイライト矩形・表示可否・診断ログ）を取る。<br/>
    /// 矩形のドメイン座標への変換・未検出の正規化・表示名の保持・ログは<br/>
    /// コアの <see cref="CaptureHighlightSession"/> が単一所有する（本クラスは結果を設定するだけ）。<br/>
    /// サイズラベルの物理ピクセル換算は、ドメイン（画面物理 − Position）を DIP 変換した DomainScale(_scaling) と<br/>
    /// 往復一致するよう _scaling（オーバーレイの表示スケール）を維持する（マルチモニタでも往復が一致する）。
    /// </remarks>
    private CaptureHighlight ApplyHighlight(ElementFinderResults detection)
    {
        var highlight = _highlight.Update(_captureType, detection, Position.ToEsPointInt());
        // 未検出は null で表す（null が「ハイライトしない」を意味する）
        SelectionOverlay.LogicalHighlightRect = highlight.DomainRect is { } rect ? rect.ToAvaloniaRect() : null;
        return highlight;
    }

    /// <summary>ドメイン矩形の中心が含まれるモニタのスケーリング係数を返す（サイズラベルの DisplayScale 表示用）。</summary>
    /// <param name="domainRect">ドメイン矩形（= 画面物理 − Position。ウィンドウローカル物理ピクセル）。</param>
    /// <returns>矩形中心のモニタのスケーリング係数。該当なしはオーバーレイのスケーリング（<see cref="_scaling"/>）。</returns>
    /// <remarks>
    /// ドメイン矩形の中心に <see cref="Position"/> を加算して画面物理座標へ戻し、中心を含むモニタを特定する。<br/>
    /// 「原点の加算」「中心で判定する」「該当なしはフォールバック」という規則はコア（<see cref="MonitorScalingMath"/>）が単一所有する<br/>
    /// （UI ごとに判定点がずれると、同じ矩形で表示倍率が食い違う）。<br/>
    /// モニタ一覧は構築時に 1 回だけ解決したものを再利用する（描画のたびに列挙・配列生成をしない）。
    /// </remarks>
    private double GetMonitorScalingForRect(EsRect domainRect)
        => MonitorScalingMath.ResolveForDomainRect(
            domainRect, Position.ToEsPointInt(), _monitors, _scaling);

    /// <summary>サイズラベルの対象となる確定矩形（ドメイン座標）を解決する。</summary>
    /// <returns>確定対象の矩形。対象がない場合は null。</returns>
    /// <remarks>
    /// 「ハイライト型はハイライト矩形・それ以外は選択矩形」という優先順位はコア（<see cref="CaptureTypeTraits.ResolveRect"/>）が単一所有する。<br/>
    /// ハイライト矩形はコアのハイライト状態（<see cref="CaptureHighlightSession"/>）から読む<br/>
    /// （選択コントロールから読み戻すと、同じ値が 2 箇所に存在する）。
    /// </remarks>
    private EsRect? ResolveTargetRect()
        => CaptureTypeTraits.ResolveRect(
            _captureType,
            _highlight.DomainRect,
            SelectionOverlay.HasSelection ? SelectionOverlay.SelectionRect.ToEsRect() : null);

    /// <summary>クリック確定型の押下時処理（ハイライトを固定する）。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">ポインタイベント。</param>
    /// <remarks>
    /// 固定の規則（退化した検出結果は固定しない・前回の固定を置き換える）はコアの
    /// <see cref="CaptureClickSelection"/> が単一所有する。<br/>
    /// 左ボタンのみを確定の対象とする（入力の解釈は UI 固有）。
    /// </remarks>
    private void OnClickPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        // 画面物理座標への変換は ScreenConverter（PlatformFactory の OS 依存変換集約）に委譲する
        var screenPoint = ScreenConverter.LocalDipToScreen(this, e.GetPosition(this)).ToEsPointInt();
        // 検出関数の呼び分けはコアが単一所有する
        _clickSelection.Fix(CaptureTypeTraits.FindUnderCursor(_detector, _captureType, screenPoint).Bounds);
    }

    /// <summary>クリック確定型のリリース時処理（固定したハイライトで確定する）。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">ポインタイベント。</param>
    /// <remarks>
    /// 固定した矩形をそのまま確定する（押下時に確定した矩形を DIP へ戻さない）。<br/>
    /// 固定が無い解放では何もしない（押下を伴わない解放・退化した検出）。
    /// </remarks>
    private void OnClickReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_clickSelection.Take() is not { } fixedRect) return;
        // 固定した矩形は画面物理座標のため、そのまま確定する（DIP を経由した往復変換を行わない）
        CompleteSelection(fixedRect);
    }
}
