namespace EsUtil.SaveEvidence.Parts.CapturePreview.WinForms.Overlay;

/// <summary>重ね表示のコントロールで入力を受け取らないためのヒットテスト規則を単一所有する。</summary>
/// <remarks>
/// <b>なぜ必要か</b>: WinForms には「ヒットテストだけ無効にする」設定が無いため、<br/>
/// <b>無効状態（<see cref="Control.Enabled"/> が false）</b>を入力の透過として扱う<br/>
/// （無効なコントロールは <c>WM_NCHITTEST</c> に <c>HTTRANSPARENT</c> を返し、同じスレッドの下のウィンドウへ入力が通る）。<br/>
/// 本クラスはその規則（対象メッセージと戻り値）を単一所有し、各コントロールは <c>WndProc</c> の上書きで本クラスへ問い合わせる<br/>
/// （<see cref="Control.WndProc(ref Message)"/> は仮想メソッドであるため、共有できるのは規則だけになる）。<br/>
/// <br/>
/// <b>重ね表示に必要なスタイル設定</b>は <see cref="Control.SetStyle"/> が <c>protected</c> であるため各コントロールが行う<br/>
/// （<see cref="ControlStyles.UserPaint"/> / <see cref="ControlStyles.AllPaintingInWmPaint"/> /<br/>
/// <see cref="ControlStyles.OptimizedDoubleBuffer"/> / <see cref="ControlStyles.ResizeRedraw"/> /<br/>
/// <see cref="ControlStyles.SupportsTransparentBackColor"/> と <c>BackColor = Color.Transparent</c>）。
/// </remarks>
internal static class OverlayHitTest
{
    /// <summary>ヒットテストの問い合わせ（<c>WM_NCHITTEST</c>）。</summary>
    public const int Message = 0x0084;

    /// <summary>入力を持たないウィンドウを表す戻り値（<c>HTTRANSPARENT</c>）。</summary>
    public const int Transparent = -1;

    /// <summary>受信したメッセージを下のウィンドウへ通すかどうかを判定する。</summary>
    /// <param name="message">受信したウィンドウ メッセージ。</param>
    /// <param name="enabled">コントロールが入力を受け取れる状態かどうか（<see cref="Control.Enabled"/>）。</param>
    /// <returns>入力の透過を指示する場合は true。</returns>
    /// <remarks>誤って入力が遮られると下のコントロール（ズームビュー等）が操作できなくなるため、<br/>規則を 1 箇所に閉じて各コントロールの判定の食い違いを防ぐ。</remarks>
    public static bool ShouldPassThrough(int message, bool enabled) => !enabled && message == Message;
}
