namespace EsUtil.SaveEvidence.App.WinUI3.Views;

/// <summary>一定の大きさの項目を幅に合わせて折り返す配置の計算。</summary>
/// <remarks>
/// <b>役割</b>: 一覧（フォルダビュー / Undo 一覧）の列数を求める。 WinUI 3 の標準パネルには<br/>
/// 任意の大きさの子を折り返すパネルが無いため、ホストが列数を計算して <c>Grid</c> へ配置する。<br/>
/// <b>純粋関数</b>: ウィンドウを必要としないため、境界値（幅 0・1 列に満たない幅）をヘッドレスで検証できる。
/// </remarks>
internal static class WrapLayoutMath
{
    /// <summary>指定された幅に収まる列数を求める。</summary>
    /// <param name="availableWidth">配置できる幅（DIP）。</param>
    /// <param name="itemWidth">1 項目の幅（DIP）。</param>
    /// <param name="spacing">項目間の余白（DIP）。</param>
    /// <returns>列数（最低 1）。</returns>
    /// <remarks>
    /// 幅が未確定（NaN）・0 以下の場合は 1 列とする（幅の確定後に呼び直す）。
    /// </remarks>
    public static int Columns(double availableWidth, double itemWidth, double spacing)
    {
        if (double.IsNaN(availableWidth) || availableWidth <= 0 || itemWidth <= 0)
        {
            return 1;
        }

        var stride = itemWidth + spacing;
        if (stride <= 0)
        {
            return 1;
        }

        // 最後の 1 列には右側の余白が無いため、余白 1 つ分を加えてから割る
        return Math.Max(1, (int)Math.Floor((availableWidth + spacing) / stride));
    }

    /// <summary>項目の横位置（DIP）を求める。</summary>
    /// <param name="index">項目の並び順（0 起点）。</param>
    /// <param name="columns">列数。</param>
    /// <param name="itemWidth">1 項目の幅（DIP）。</param>
    /// <param name="spacing">項目間の余白（DIP）。</param>
    /// <returns>左端からの横位置（DIP）。</returns>
    public static double Left(int index, int columns, double itemWidth, double spacing)
        => (index % Math.Max(1, columns)) * (itemWidth + spacing);

    /// <summary>項目の縦位置（DIP）を求める。</summary>
    /// <param name="index">項目の並び順（0 起点）。</param>
    /// <param name="columns">列数。</param>
    /// <param name="itemHeight">1 項目の高さ（DIP）。</param>
    /// <param name="spacing">項目間の余白（DIP）。</param>
    /// <returns>上端からの縦位置（DIP）。</returns>
    public static double Top(int index, int columns, double itemHeight, double spacing)
        => (index / Math.Max(1, columns)) * (itemHeight + spacing);
}
