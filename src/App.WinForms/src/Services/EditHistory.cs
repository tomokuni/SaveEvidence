
namespace EsUtil.SaveEvidence.App.WinForms.Services;

/// <summary>プレビュー画像の編集履歴（戻す / やり直し）の操作を担う窓口。</summary>
/// <remarks>
/// <b>役割</b>: <c>App.Core</c> の <c>UndoHistory</c> と <c>MainViewModel</c> の間で<br/>
/// 「履歴から画像を取り出してプレビューへ差し替える」操作を 1 箇所に集約する。<br/>
/// 撮影（<see cref="CaptureFlow"/>）と切り出し（ファサード）は履歴の退避のみを担う。<br/>
/// <br/>
/// <b>所有権</b>: 履歴の画像はスタックが保持し、復元した画像の所有権はプレビューへ移る<br/>
/// （差し替え前のプレビュー画像は破棄する。 履歴側は独立した画素の複製を持つため影響しない）。<br/>
/// <b>スタックが空の場合</b>: 何もしない（プレビューを変更しない。 例外にもしない）。<br/>
/// <b>やり直しの破棄</b>: 取り消しを取り消すと現在の画像は履歴へ退避されない（取り消しの逆操作として扱う）。
/// </remarks>
internal static class EditHistory
{
    /// <summary>取り消しを実行し、直前の画像をプレビューへ復元する。</summary>
    /// <param name="services">アプリ全体で共有するコアオブジェクト。</param>
    /// <param name="restore">復元した画像をプレビューへ設定した後の処理（ホスト固有の後処理）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> が null の場合。</exception>
    /// <remarks>
    /// 1. スタックが空の場合は何もしない。<br/>
    /// 2. 現在の画像をやり直し側へ退避し、直前の画像を取り出す。<br/>
    /// 3. 復元した画像をプレビューへ設定する（保存状態は <c>MainViewModel</c> が未保存へ戻す）。
    /// </remarks>
    public static void Undo(AppServices services, Action? restore = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var previous = services.UndoHistory.Undo(services.Main.PreviewImage);
        if (previous is null)
        {
            return;
        }

        Replace(services, previous, restore);
    }

    /// <summary>やり直しを実行し、取り消した画像をプレビューへ復元する。</summary>
    /// <param name="services">アプリ全体で共有するコアオブジェクト。</param>
    /// <param name="restore">復元した画像をプレビューへ設定した後の処理（ホスト固有の後処理）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> が null の場合。</exception>
    /// <remarks>
    /// 1. スタックが空の場合は何もしない。<br/>
    /// 2. やり直し側から画像を取り出す（現在の画像は退避しない）。<br/>
    /// 3. 復元した画像をプレビューへ設定する。
    /// </remarks>
    public static void Redo(AppServices services, Action? restore = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var restored = services.UndoHistory.Redo();
        if (restored is null)
        {
            return;
        }

        Replace(services, restored, restore);
    }

    /// <summary>復元した画像へプレビューを差し替える。</summary>
    /// <param name="services">アプリ全体で共有するコアオブジェクト。</param>
    /// <param name="image">復元する画像。 所有権がプレビューへ移る。</param>
    /// <param name="restore">差し替え後の後処理（省略可）。</param>
    /// <remarks>
    /// 差し替え前の画像の破棄と未保存化は <c>MainViewModel</c>（<c>PreviewImage</c> の所有者）が行う。<br/>
    /// 履歴の画像は独立した画素の複製のため、差し替え前の画像を破棄しても履歴の内容には影響しない。
    /// </remarks>
    private static void Replace(AppServices services, EsReadOnlyBitmap image, Action? restore)
    {
        services.Main.SetPreviewImage(image);
        restore?.Invoke();
    }
}
