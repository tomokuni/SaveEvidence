using System.Collections.Concurrent;
using EsUtil.Platform.Core.Imaging;

namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>一覧のサムネイル生成が終わるまで表示する代替画像を提供する。</summary>
/// <remarks>
/// <b>なぜ本層が持つか</b>: 「生成が終わるまで表示する透明な正方形」という判断は UI に依存しない
/// （3 つの UI 版が同じ生成・同じ寸法・同じ共有規則を持つ）。 UI 版ごとに書き分けると、
/// <b>片方だけ直したときに見た目がずれる</b>（実測 2026-11-13: 3 か所に同じ実装があった）。<br/>
/// <b>正方形で場所を確保する</b>: 生成が終わるまで実体の縦横比は分からないため、要求寸法の正方形を表示して
/// 行の高さを先に決める（生成後に行の高さが確定する）。<br/>
/// <b>透明にする理由</b>: 枠の背景（テーマ色）が見えるため、ライト / ダークのどちらでも色が衝突しない。<br/>
/// <b>寸法ごとに 1 枚を共有する</b>: 呼び出し側は借用して表示するだけで、<b>破棄しない</b>
/// （破棄すると他の行の表示が壊れる。 一覧の差し替えで破棄されるのは実サムネイルだけである）。
/// そのためプロセスの生存期間で保持する。<br/>
/// <b>UI の画像型への変換は各 UI が行う</b>（本層は UI の型を持たない）。
/// </remarks>
internal static class ThumbnailPlaceholder
{
    /// <summary>寸法ごとの代替画像（共有するためプロセス内で保持する）。</summary>
    private static readonly ConcurrentDictionary<int, EsReadOnlyBitmap> s_images = new();

    /// <summary>指定寸法の代替画像を取得する。</summary>
    /// <param name="size">サムネイルの一辺（ピクセル）。 1 未満は 1 として扱う。</param>
    /// <returns>代替画像。<b>借用</b>（破棄しない）。</returns>
    /// <remarks>
    /// 凍結して読み取り専用の面へ渡す（表示は読むだけであり、書き換えない）。
    /// </remarks>
    public static EsReadOnlyBitmap Get(int size)
        => s_images.GetOrAdd(Math.Max(1, size), static s =>
        {
            using var bitmap = EsBitmap.Create(s, s);
            bitmap.SetReadOnly();
            return EsReadOnlyBitmap.FromBitmap(bitmap);
        });
}
