namespace EsUtil.SaveEvidence.App.Core.Models;

/// <summary>サムネイル生成の優先度（値が小さいほど先に実行する）。</summary>
/// <remarks>
/// <b>2 つの帯</b>: 一覧で<b>見えている項目</b>（<see cref="VisibleBand"/>）を先に実行し、
/// <b>見えていない項目</b>（<see cref="BackgroundBand"/>）を後に回す。<br/>
/// <b>帯の中は上方優先</b>: 同じ帯では <see cref="Index"/> の小さい順（一覧の上から）に実行する
/// （要件: ページで見えている部分を優先的にサムネイル化し、見えている部分の中では上方に表示されているものを優先する）。<br/>
/// <b>作り方</b>: 項目の表示範囲と可視領域から <see cref="FromRange"/> で作る（可視判定は純粋関数であり、
/// 画面のない環境でも検証できる）。 可視領域が確定しない場合（レイアウト前など）は
/// <see cref="Background"/> を使い、表示順（<see cref="Index"/>）だけで並べる（上から順になる）。
/// </remarks>
internal readonly record struct ThumbnailPriority(int Band, int Index)
{
    /// <summary>見えている項目の帯。</summary>
    public const int VisibleBand = 0;

    /// <summary>見えていない項目の帯。</summary>
    public const int BackgroundBand = 1;

    /// <summary>見えている項目の優先度を作る。</summary>
    /// <param name="index">一覧内の位置（0 が最上部）。</param>
    /// <returns>優先度。</returns>
    public static ThumbnailPriority Visible(int index) => new(VisibleBand, index);

    /// <summary>見えていない項目の優先度を作る。</summary>
    /// <param name="index">一覧内の位置（0 が最上部）。</param>
    /// <returns>優先度。</returns>
    public static ThumbnailPriority Background(int index) => new(BackgroundBand, index);

    /// <summary>項目の表示範囲と可視領域から優先度を求める。</summary>
    /// <param name="itemTop">項目の上端（表示領域の座標）。</param>
    /// <param name="itemBottom">項目の下端（表示領域の座標）。</param>
    /// <param name="viewportTop">見えている範囲の上端（表示領域の座標）。</param>
    /// <param name="viewportBottom">見えている範囲の下端（表示領域の座標）。</param>
    /// <param name="index">一覧内の位置（0 が最上部）。</param>
    /// <returns><b>一部でも見えている</b>場合は <see cref="VisibleBand"/>、それ以外は <see cref="BackgroundBand"/> の優先度。</returns>
    /// <remarks>
    /// 「一部でも見えている」は上下の範囲が重なることで判定する（端が一致するだけの場合は重なりとみなさない）。
    /// </remarks>
    public static ThumbnailPriority FromRange(
        double itemTop, double itemBottom, double viewportTop, double viewportBottom, int index)
        => itemBottom > viewportTop && itemTop < viewportBottom ? Visible(index) : Background(index);

    /// <summary>2 つの優先度を比べる（小さいほど先に実行する）。</summary>
    /// <param name="left">左の優先度。</param>
    /// <param name="right">右の優先度。</param>
    /// <returns>負なら <paramref name="left"/> が先、正なら <paramref name="right"/> が先。</returns>
    /// <remarks>比較演算子を実装せずに済むよう、比較は本メソッド（<see cref="IComparer{T}"/> 経由）へ一本化する。</remarks>
    public static int Compare(ThumbnailPriority left, ThumbnailPriority right)
        => left.Band != right.Band ? left.Band.CompareTo(right.Band) : left.Index.CompareTo(right.Index);
}
