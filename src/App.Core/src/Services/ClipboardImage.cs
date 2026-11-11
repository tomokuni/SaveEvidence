using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.App.Core.Native;

namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>クリップボードとの画像の受け渡しを単一所有する。</summary>
/// <remarks>
/// <b>画像クリップボードの API を持たない UI（MewUI）のために共通基盤が担う</b>（Windows の <c>CF_DIB</c> を使う）。<br/>
/// 失敗は例外にせず戻り値で表す（他アプリがクリップボードを使用中の場合がある）。 記録は呼び出し側が行う。<br/>
/// 実装は <see cref="WinClipboardDib"/> が単一所有する（解析・書き込みの手順を UI 側へ漏らさない）。
/// </remarks>
public static class ClipboardImage
{
    /// <summary>クリップボードの画像を読み取る。</summary>
    /// <returns>読み取った画像（所有権は呼び出し側へ移る。 貼り付けでそのまま扱えるよう書き換え可能な型）。 画像が無い・他アプリが使用中の場合は null。</returns>
    public static EsBitmap? TryGet() => WinClipboardDib.TryRead();

    /// <summary>画像をクリップボードへ書き込む。</summary>
    /// <param name="image">書き込む画像。</param>
    /// <param name="ownerWindow">クリップボードの所有者にするウィンドウ（<c>SetClipboardData</c> の要件）。</param>
    /// <returns>書き込めた場合は true。 他アプリが使用中・確保できない場合は false。</returns>
    public static bool TryCopy(EsReadOnlyBitmap image, IntPtr ownerWindow) => WinClipboardDib.TryWrite(image, ownerWindow);
}
