using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.App.Core.Models;
using EsUtil.SaveEvidence.App.Core.ViewModels;

namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>プレビュー画像の編集履歴（Undo / Redo）の操作を担う窓口。</summary>
/// <remarks>
/// <b>4 つの UI 版で 1 つ</b>: 「履歴から画像を取り出してプレビューへ差し替える」手順は UI に依存しない
/// （<see cref="UndoHistory"/> と <see cref="MainViewModel"/> はどちらも本層の型である）。 UI 版ごとに
/// 書き分けると、<b>片方だけ直したときに履歴の扱いがずれる</b>。<br/>
/// <b>所有権</b>: 履歴の画像はスタックが保持し、復元した画像の所有権はプレビューへ移る
/// （差し替え前のプレビュー画像は破棄する。 履歴側は独立した画素の複製を持つため影響しない）。<br/>
/// <b>スタックが空の場合</b>: 何もしない（プレビューを変更しない。 例外にもしない）。<br/>
/// <b>Redo の破棄</b>: Redo で戻すと現在の画像は履歴へ退避されない（Undo の逆操作として扱う）。
/// </remarks>
internal static class EditHistory
{
    /// <summary>Undo を実行し、直前の画像をプレビューへ復元する。</summary>
    /// <param name="main">アプリのメイン ViewModel（プレビューの所有者）。</param>
    /// <param name="history">Undo / Redo の履歴（キャプチャページと Undo 一覧で共有する）。</param>
    /// <param name="restore">復元した画像をプレビューへ設定した後の後処理（切出しの座標系の再計算など。 省略可）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="main"/> または <paramref name="history"/> が null の場合。</exception>
    /// <remarks>
    /// 1. スタックが空の場合は何もしない。<br/>
    /// 2. 現在の画像を Redo 側へ退避し、直前の画像を取り出す。<br/>
    /// 3. 復元した画像をプレビューへ設定する（保存状態は <see cref="MainViewModel"/> が未保存へ戻す）。
    /// </remarks>
    public static void Undo(MainViewModel main, UndoHistory history, Action? restore = null)
    {
        ArgumentNullException.ThrowIfNull(main);
        ArgumentNullException.ThrowIfNull(history);

        var previous = history.Undo(main.PreviewImage);
        if (previous is null)
        {
            return;
        }

        Replace(main, previous, restore);
    }

    /// <summary>Redo を実行し、取り消した画像をプレビューへ復元する。</summary>
    /// <param name="main">アプリのメイン ViewModel（プレビューの所有者）。</param>
    /// <param name="history">Undo / Redo の履歴。</param>
    /// <param name="restore">復元した画像をプレビューへ設定した後の後処理（省略可）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="main"/> または <paramref name="history"/> が null の場合。</exception>
    /// <remarks>
    /// 1. スタックが空の場合は何もしない。<br/>
    /// 2. Redo 側から画像を取り出す（現在の画像は退避しない）。<br/>
    /// 3. 復元した画像をプレビューへ設定する。
    /// </remarks>
    public static void Redo(MainViewModel main, UndoHistory history, Action? restore = null)
    {
        ArgumentNullException.ThrowIfNull(main);
        ArgumentNullException.ThrowIfNull(history);

        var restored = history.Redo();
        if (restored is null)
        {
            return;
        }

        Replace(main, restored, restore);
    }

    /// <summary>復元した画像へプレビューを差し替える。</summary>
    /// <param name="main">アプリのメイン ViewModel。</param>
    /// <param name="image">復元する画像。 所有権がプレビューへ移る。</param>
    /// <param name="restore">差し替え後の後処理（省略可）。</param>
    private static void Replace(MainViewModel main, EsReadOnlyBitmap image, Action? restore)
    {
        main.SetPreviewImage(image);
        restore?.Invoke();
    }
}
