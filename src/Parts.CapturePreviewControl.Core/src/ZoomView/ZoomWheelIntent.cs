namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.ZoomView;

/// <summary>ホイール入力をどの操作（ズーム / パン）へ割り当てるかを表す値。</summary>
/// <param name="IsZoom">ズーム（Ctrl 併用）の場合は true。 それ以外はパン。</param>
/// <param name="ZoomIn">ズームする場合の向き（前方向 = 拡大）。 パンの場合は参照しない。</param>
/// <param name="ScrollHorizontal">パンする場合に横方向へスクロールするか（Shift 併用）。</param>
/// <remarks>
/// 「Ctrl 併用 = ズーム / それ以外 = パン」という解釈を単一所有する<br/>
/// （UI ごとに論理式を書くと、片方だけ Shift の扱いや回転量の向きがずれる）。<br/>
/// 回転量 → 移動量の換算と適用は <see cref="ZoomViewSession.ZoomByWheel"/> / <see cref="ZoomViewSession.PanByWheel"/> が担う。
/// </remarks>
public readonly record struct ZoomWheelIntent(bool IsZoom, bool ZoomIn, bool ScrollHorizontal)
{
    /// <summary>修飾キーと回転量からホイールの操作を決める。</summary>
    /// <param name="isControlPressed">Ctrl が押されているかどうか。</param>
    /// <param name="isShiftPressed">Shift が押されているかどうか。</param>
    /// <param name="deltaY">縦方向の回転量（正 = 前方向 = 拡大）。</param>
    /// <returns>割り当てる操作。</returns>
    /// <remarks>
    /// Ctrl 併用はズーム（回転量の符号で向きを決める）、それ以外はパン（Shift 併用は横方向）とする。<br/>
    /// ズームの中心は呼び出し側がポインタ位置を渡して決める（位置の決め方は UI の入力経路に属する）。
    /// </remarks>
    public static ZoomWheelIntent Resolve(bool isControlPressed, bool isShiftPressed, double deltaY)
        => isControlPressed
            ? new ZoomWheelIntent(IsZoom: true, ZoomIn: deltaY > 0, ScrollHorizontal: false)
            : new ZoomWheelIntent(IsZoom: false, ZoomIn: false, ScrollHorizontal: isShiftPressed);
}
