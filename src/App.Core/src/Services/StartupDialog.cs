using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;

namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>起動できない理由を利用者へ示す（XAML の初期化前に使えるエラー表示）。</summary>
/// <remarks>
/// <b>なぜ必要か</b>: UI フレームワークの初期化に失敗する状況（例: アンパッケージの WinUI 3 アプリを
/// .NET ホストから起動した）では、ログだけでは利用者が気づけない。 <c>MessageBox</c> はウィンドウも XAML も
/// 必要としないため、<b>起動処理の最初</b>から使える。<br/>
/// <b>本クラスが持つ理由</b>: 4 つの UI 版が同じ文言で同じ見た目にするため（UI 版ごとに P/Invoke を複製しない）。
/// <b>Win32 の宣言は CsWin32 が生成する</b>（<c>src/NativeMethods.txt</c>。 宣言を手で書かない）。<br/>
/// <b>失敗しても例外にしない</b>: 表示できない状況（対話セッションでない等）でも起動処理を妨げない。
/// </remarks>
internal static class StartupDialog
{
    /// <summary>エラーのメッセージ ボックスを表示する。</summary>
    /// <param name="caption">表題（アプリ名）。</param>
    /// <param name="text">本文。</param>
    /// <remarks>
    /// 戻り値は使わない（利用者が閉じるまで待つが、選択されたボタンで分岐しない）。
    /// 戻り値の型（<c>MESSAGEBOX_RESULT</c>）も呼び出し側へ出さない。
    /// </remarks>
    public static void ShowError(string caption, string text)
        => _ = PInvoke.MessageBox(default, text, caption, MESSAGEBOX_STYLE.MB_ICONERROR);
}
