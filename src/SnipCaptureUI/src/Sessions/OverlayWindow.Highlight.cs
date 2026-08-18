using Avalonia;
using Avalonia.Input;
using Avalonia.Platform;
using Estable.AvaloniaUI.Platform;
using Estable.AvaloniaUI.SnipCaptureUI.Logging;

namespace Estable.AvaloniaUI.SnipCaptureUI;

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
        var screenPoint = ScreenConverter.LocalDipToScreen(this, e.GetPosition(this));
        if (_captureType == CaptureType.ScreenSelect)
            UpdateSelectScreenHighlight(screenPoint);
        else
            UpdateWindowSelectHighlight(screenPoint);
        // ハイライト検出結果を FadeMask の描画へ反映する（再描画要求）
        RequestOverlayRender();
        // ホバー中の枠（ハイライト）を事前切り出しする（確定時の高速化。同じ矩形ならスキップ）
        if (SelectionOverlay.LogicalHighlightRect is { } highlightLogical)
        {
            // ドメイン（ウィンドウローカル物理ピクセル = 画面物理 − Position）→ 画面物理座標へ変換
            var highlightScreen = new Rect(
                highlightLogical.X + Position.X,
                highlightLogical.Y + Position.Y,
                highlightLogical.Width,
                highlightLogical.Height);
            PreExtractClip(highlightScreen);
        }
    }

    /// <summary>ScreenSelect のハイライトを更新する。</summary>
    private void UpdateSelectScreenHighlight(PixelPoint screenPoint)
    {
        var detection = _detector.FindScreenUnderCursor(screenPoint);
        SelectionOverlay.LogicalHighlightRect = detection.IsDetected ? ToWindowPixel(detection.Bounds) : null;
        SelectionOverlay.HasHighlight = detection.IsDetected;
        SelectionOverlay.ScreenIndex = detection.IsDetected ? ParseScreenIndex(detection.Title) : -1;
        // サイズラベルの物理ピクセル換算は、ドメイン（画面物理 − Position）を DIP 変換した
        // DomainScale(_scaling) と往復一致するよう _scaling（オーバーレイの表示スケール）を維持する。
        // マルチモニタで異なるスケーリングのスクリーンを選択しても、選択矩形はドメイン（物理ピクセル）
        // のまま保持され、DIP 変換（÷ _scaling）と物理換算（× _scaling）で正しい物理サイズを表示する。
        LogMessage.LogHighlight(_log, "ScreenSelect", detection.IsDetected,
            detection.IsDetected ? ToWindowPixel(detection.Bounds).ToString() : "",
            detection.Title ?? "", "");
    }

    /// <summary>点を含むスクリーンのスケーリング係数を返す（純粋関数）。該当なしはフォールバックを返す。</summary>
    /// <param name="point">判定する点（画面物理座標）。</param>
    /// <param name="fallback">該当スクリーンが無い場合のスケーリング係数。</param>
    /// <param name="screens">スクリーン情報（Bounds と Scaling）。</param>
    /// <returns>点を含むスクリーンのスケーリング係数。該当なしは <paramref name="fallback"/>。</returns>
    /// <remarks><see cref="Screen"/> 抽象クラスに依存しないよう、スクリーン情報は <c>(Bounds, Scaling)</c> のタプル列として受け取る。</remarks>
    private static double FindScalingForPoint(
        PixelPoint point, double fallback, IEnumerable<(PixelRect Bounds, double Scaling)> screens)
    {
        foreach (var (bounds, scaling) in screens)
        {
            if (bounds.Contains(point)) return scaling;
        }
        return fallback;
    }

    /// <summary>ドメイン矩形の中心が含まれるモニタのスケーリング係数を返す（サイズラベルの DisplayScale 表示用）。</summary>
    /// <param name="domainRect">ドメイン矩形（= 画面物理 − Position。仮想スクリーンピクセル）。</param>
    /// <returns>矩形中心のモニタのスケーリング係数。該当なしはオーバーレイのスケーリング（<see cref="_scaling"/>）。</returns>
    /// <remarks>
    /// ドメイン矩形の中心に <see cref="Position"/> を加算して画面物理座標へ戻し、<see cref="FindScalingForPoint"/> で
    /// モニタを特定する。これにより、マルチモニタ（異なる DPI）でも選択中のモニタの実スケーリングを表示できる。
    /// </remarks>
    private double GetMonitorScalingForRect(Rect domainRect)
    {
        var center = new PixelPoint(
            (int)(domainRect.X + domainRect.Width / 2 + Position.X),
            (int)(domainRect.Y + domainRect.Height / 2 + Position.Y));
        return FindScalingForPoint(center, _scaling, Screens.All.Select(s => (s.Bounds, s.Scaling)));
    }

    /// <summary>ドメイン矩形からサイズラベルを生成する（物理ピクセル + DisplayScale）。</summary>
    /// <param name="domainRect">ドメイン矩形（= 画面物理 − Position。物理ピクセル）。</param>
    /// <param name="scaling">DisplayScale 表示用のスケーリング係数（選択中のモニタの実スケーリング）。</param>
    /// <returns>"(L, T) - (R, B),  (W×H), DisplayScale: (P%)" 形式のテキスト。</returns>
    /// <remarks>
    /// ドメインは画面物理座標のため、座標・サイズはスケーリングに依存しない物理ピクセルとして直接取得できる。<br/>
    /// DIP 変換（÷ _scaling）→ 物理換算（× DisplayScaleScaling）の往復誤差（マルチモニタでのサイズ・DisplayScale の
    /// 不一致）を排除するため、ドメインから直接生成する（単一実装・DRY）。
    /// </remarks>
    private static string FormatDomainSizeLabel(Rect domainRect, double scaling)
    {
        int l = (int)domainRect.X, t = (int)domainRect.Y;
        int r = (int)domainRect.Right, b = (int)domainRect.Bottom;
        int w = (int)domainRect.Width, h = (int)domainRect.Height;
        int pct = (int)(scaling * 100);
        return $"({l}, {t}) - ({r}, {b}),  ({w}×{h}), DisplayScale: ({pct}%)";
    }

    /// <summary>検出結果のタイトル（「Screen {インデックス}」形式）からインデックスを取得する。</summary>
    /// <param name="title">スクリーン検出結果のタイトル。パース不能な場合は null。</param>
    /// <returns>スクリーンインデックス。パース不能な場合は -1。</returns>
    private static int ParseScreenIndex(string? title)
    {
        // "Screen " のプレフィックスを除去して数値を解析する（短すぎる場合はパース不能）
        const string prefix = "Screen ";
        if (title is null || title.Length <= prefix.Length) return -1;
        return int.TryParse(title.AsSpan(prefix.Length), out var idx) ? idx : -1;
    }

    /// <summary>WindowSelect のハイライトを更新する。</summary>
    private void UpdateWindowSelectHighlight(PixelPoint screenPoint)
    {
        var detection = _detector.FindWindowUnderCursor(screenPoint);
        SelectionOverlay.LogicalHighlightRect = detection.IsDetected ? ToWindowPixel(detection.Bounds) : null;
        SelectionOverlay.HasHighlight = detection.IsDetected;
        SelectionOverlay.WindowTitle = detection.Title ?? "";
        // サイズラベルの物理ピクセル換算は _scaling（オーバーレイの表示スケール）を維持する
        // （ScreenSelect と同一。マルチモニタでもドメイン（物理ピクセル）の往復が一致する）。
        LogMessage.LogHighlight(_log, "WindowSelect", detection.IsDetected,
            detection.IsDetected ? ToWindowPixel(detection.Bounds).ToString() : "",
            detection.Title ?? "", detection.ClassName ?? "");
    }

    /// <summary>画面物理座標の矩形をウィンドウローカル DIP に変換する。</summary>
    private Rect ToWindowDip(PixelRect bounds)
        => new(
            (bounds.X - Position.X) / _scaling,
            (bounds.Y - Position.Y) / _scaling,
            bounds.Width / _scaling,
            bounds.Height / _scaling);

    /// <summary>画面物理座標の矩形をウィンドウローカル物理ピクセル（ドメイン座標）に変換する。</summary>
    /// <remarks>
    /// FadeMask の <c>LogicalHighlightRect</c> はドメイン座標（= ウィンドウローカル物理ピクセル）で設定する。<br/>
    /// 描画時には座標変換ポート（DomainScale）が DIP へ変換する。<br/>
    /// 画面物理座標 → ドメイン: Position を減算する（scaling では割らない）。
    /// </remarks>
    private Rect ToWindowPixel(PixelRect bounds)
        => new(
            bounds.X - Position.X,
            bounds.Y - Position.Y,
            bounds.Width,
            bounds.Height);

    /// <summary>クリック確定型の押下時処理（ハイライトを固定する）。</summary>
    private void OnClickPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        // 画面物理座標への変換は ScreenConverter（PlatformFactory の OS 依存変換集約）に委譲する
        var screenPoint = ScreenConverter.LocalDipToScreen(this, e.GetPosition(this));
        var targetRect = _captureType == CaptureType.ScreenSelect
            ? _detector.FindScreenUnderCursor(screenPoint).Bounds
            : _detector.FindWindowUnderCursor(screenPoint).Bounds;
        if (targetRect.Width <= 0) return;
        _clickFixedRect = ToWindowDip(targetRect);
    }

    /// <summary>クリック確定型のリリース時処理（固定したハイライトで確定する）。</summary>
    private void OnClickReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_clickFixedRect is not { } fixedRect) return;
        _clickFixedRect = null;
        // 固定矩形（ウィンドウローカル DIP）を画面物理座標へ変換して確定する（× scaling + Position）
        var bounds = new Rect(
            fixedRect.X * _scaling + Position.X,
            fixedRect.Y * _scaling + Position.Y,
            fixedRect.Width * _scaling,
            fixedRect.Height * _scaling);
        CompleteSelection(bounds);
    }
}
