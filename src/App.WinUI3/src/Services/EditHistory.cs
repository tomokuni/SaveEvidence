
namespace EsUtil.SaveEvidence.App.WinUI3.Services;

/// <summary>プレビュー画像の編集履歴（Undo / Redo）の操作を担う窓口。</summary>
/// <remarks>
/// <b>役割</b>: 編集履歴（<c>App.Core</c> の <c>UndoHistory</c>）とアプリのメイン ViewModel の間で<br/>
/// 「履歴から画像を取り出してプレビューへ差し替える」操作を 1 箇所に集約する。<br/>
/// 撮影と切り出しは履歴の退避のみを担う。<br/>
/// <br/>
/// <b>所有権</b>: 履歴の画像はスタックが保持し、復元した画像の所有権はプレビューへ移る<br/>
/// （差し替え前のプレビュー画像は破棄する。 履歴側は独立した画素の複製を持つため影響しない）。<br/>
/// <b>スタックが空の場合</b>: 何もしない（プレビューを変更しない。 例外にもしない）。<br/>
/// <b>Redo の破棄</b>: Redo で戻すと現在の画像は履歴へ退避されない（Undo の逆操作として扱う）。
/// </remarks>
internal static class EditHistory
{
    /// <summary>Undo を実行し、直前の画像をプレビューへ復元する。</summary>
    /// <param name="services">アプリ全体で共有するコアオブジェクト。</param>
    /// <param name="restore">復元した画像をプレビューへ設定した後の後処理（省略可）。</param>
    /// <remarks>
    /// 1. スタックが空の場合は何もしない。<br/>
    /// 2. 現在の画像を Redo 側へ退避し、直前の画像を取り出す。<br/>
    /// 3. 復元した画像をプレビューへ設定する（保存状態は ViewModel が未保存へ戻す）。
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

    /// <summary>Redo を実行し、取り消した画像をプレビューへ復元する。</summary>
    /// <param name="services">アプリ全体で共有するコアオブジェクト。</param>
    /// <param name="restore">復元した画像をプレビューへ設定した後の後処理（省略可）。</param>
    /// <remarks>
    /// 1. スタックが空の場合は何もしない。<br/>
    /// 2. Redo 側から画像を取り出す（現在の画像は退避しない）。<br/>
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
    private static void Replace(AppServices services, EsReadOnlyBitmap image, Action? restore)
    {
        services.Main.SetPreviewImage(image);
        restore?.Invoke();
    }
}
