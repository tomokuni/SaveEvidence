using System.Collections.Concurrent;
using Avalonia.Media.Imaging;
using EsUtil.Ui.AvaloniaUI.Interop.Imaging;
using CorePlaceholder = EsUtil.SaveEvidence.App.Core.Services.ThumbnailPlaceholder;

namespace EsUtil.SaveEvidence.App.AvaloniaUI.Views.Pages;

/// <summary>サムネイルの生成が終わるまで一覧に表示する代替画像を提供する。</summary>
/// <remarks>
/// <b>画像の生成と共有はコアが単一所有する</b>（コアの <c>ThumbnailPlaceholder</c>。 3 つの UI 版で同じ見た目）。<br/>
/// 本クラスは<b>自 UI の画像型への変換と、その変換結果のキャッシュ</b>だけを持つ
/// （変換は UI の型を必要とするため、コアには置けない）。
/// </remarks>
internal static class ThumbnailPlaceholder
{
    /// <summary>寸法ごとの変換済み画像（共有するためプロセス内で保持する）。</summary>
    private static readonly ConcurrentDictionary<int, Bitmap> s_images = new();

    /// <summary>指定寸法の代替画像を取得する。</summary>
    /// <param name="size">サムネイルの一辺（ピクセル）。 1 未満は 1 として扱う。</param>
    /// <returns>代替画像。<b>借用</b>（破棄しない）。</returns>
    /// <remarks>コアの画像は読み取り専用のため、<b>変換（複製）</b>してから保持する。</remarks>
    public static Bitmap Get(int size)
        => s_images.GetOrAdd(size, static s => CorePlaceholder.Get(s).ToAvaloniaBitmap());
}
