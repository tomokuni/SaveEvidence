using System.Collections.Concurrent;
using Avalonia.Media.Imaging;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Imaging;

namespace EsUtil.SaveEvidence.App.AvaloniaUI.Views.Pages;

/// <summary>サムネイルの生成が終わるまで一覧に表示する代替画像を提供する。</summary>
/// <remarks>
/// <b>正方形で場所を確保する</b>: 生成が終わるまで実体の縦横比は分からないため、要求寸法の正方形を表示して
/// 行の高さを先に決める（生成後に行の高さが確定する）。<br/>
/// <b>透明にする理由</b>: 枠の背景（テーマ色）が見えるため、ライト / ダークのどちらでも色が衝突しない。<br/>
/// <b>寸法ごとに 1 枚を共有する</b>: 呼び出し側は借用して表示するだけであり、<b>破棄しない</b>
/// （破棄すると他の行の表示が壊れる。 一覧の差し替えで破棄されるのは実サムネイルだけである）。
/// </remarks>
internal static class ThumbnailPlaceholder
{
    /// <summary>寸法ごとの代替画像（共有するためプロセス内で保持する）。</summary>
    private static readonly ConcurrentDictionary<int, Bitmap> s_images = new();

    /// <summary>指定寸法の代替画像を取得する。</summary>
    /// <param name="size">サムネイルの幅（ピクセル）。</param>
    /// <returns>代替画像。<b>借用</b>（破棄しない）。</returns>
    public static Bitmap Get(int size)
        => s_images.GetOrAdd(size, static s =>
        {
            using var blank = EsWritableBitmap.Create(s, s);
            return blank.ToAvaloniaBitmap();
        });
}
