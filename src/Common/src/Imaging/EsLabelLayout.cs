using EsUtil.Platform.Core.Geometry;

namespace EsUtil.SaveEvidence.Common.Imaging;

/// <summary>ラベルの体裁（内側余白・行送り・配置基準）を単一所有する計算ロジック（純粋関数）。</summary>
/// <remarks>
/// 「背景は文字の実寸へ内側余白を加えた大きさ」「文字は背景の内側余白だけ空けて置く」「縦積みの行送り」は<br/>
/// コントロールに依存しない体裁の規則である。各コアが個別に書くと、同じ見た目を作る式が複数箇所へ散り<br/>
/// （余白の適用漏れ・非対称な余白）、UI ごとに見た目がずれる。<br/>
/// <br/>
/// <b>単位</b>: DIP（描画領域と同じ座標系）。物理ピクセルへの換算は描画時に行う。<br/>
/// <b>テキストの実寸測定は呼び出し側</b>が行う（描画 API 依存のためコアでは測れない）。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public static class EsLabelLayout
{
    /// <summary>ラベルの内側余白の既定値（DIP）。</summary>
    /// <value>背景を文字の実寸より四方へ広げる量。</value>
    /// <remarks>
    /// 「背景は文字の実寸へ内側余白を加える」という体裁の既定値である。<br/>
    /// 同じ体裁を共有するラベル（寸法表示・座標表示・付加情報）は本値を参照し、<br/>
    /// 各コントロール・各ホストが <c>4.0</c> を持つと余白の変更が 1 箇所で済まない。
    /// </remarks>
    public const double Padding = 4.0;

    /// <summary>基準点がラベルのどこを指すかを指定して、背景矩形を求める。</summary>
    /// <param name="point">基準点（DIP。コアが決める位置。<paramref name="horizontal"/> / <paramref name="vertical"/> に従って解釈する）。</param>
    /// <param name="areaSize">描画領域のサイズ（DIP）。0 以下の軸はクランプしない。</param>
    /// <param name="textSize">テキストの実寸（描画 API で測定した値・DIP）。</param>
    /// <param name="horizontal">基準点を合わせる水平位置（左端 / 中央 / 右端）。</param>
    /// <param name="vertical">基準点を合わせる垂直位置（上端 / 中央 / 下端）。</param>
    /// <param name="paddingX">左右の内側余白（DIP）。0 以上。</param>
    /// <param name="paddingY">上下の内側余白（DIP）。0 以上。</param>
    /// <returns>背景矩形（DIP。領域が確定している軸だけ領域内へクランプ済み）。</returns>
    /// <remarks>
    /// 背景は「文字の実寸 + 内側余白 × 2」であり、基準点を <paramref name="horizontal"/> / <paramref name="vertical"/> の位置へ合わせる。<br/>
    /// 描画領域の右端・下端をはみ出す場合は領域内へクランプする（ラベル全体が見えるよう左寄せ・上寄せ）。<br/>
    /// 領域サイズが 0 以下の軸はクランプしない（レイアウト前の配置を勝手に動かさない）。<br/>
    /// 文字の原点は <see cref="ComputeOffsetTextOrigin"/> が同じ余白から求める。<br/>
    /// <b>位置の指定はコア、実寸の反映は UI 層</b>: コアは「テキスト・基準点・アライン・領域」を渡し、<br/>
    /// UI 層が本メソッドへ実寸を渡す（ラベルを要素として置くか画像として描くかは UI 層が決める）。
    /// </remarks>
    public static EsRect ComputeAlignedBox(
        EsPoint point,
        EsSize areaSize,
        EsSize textSize,
        EsLabelAlignX horizontal,
        EsLabelAlignY vertical,
        double paddingX,
        double paddingY)
    {
        var width = textSize.Width + paddingX * 2;
        var height = textSize.Height + paddingY * 2;

        var x = horizontal switch
        {
            EsLabelAlignX.Center => point.X - width / 2,
            EsLabelAlignX.Right => point.X - width,
            _ => point.X,
        };

        var y = vertical switch
        {
            EsLabelAlignY.Middle => point.Y - height / 2,
            EsLabelAlignY.Bottom => point.Y - height,
            _ => point.Y,
        };

        if (areaSize.Width > 0)
        {
            x = Math.Min(x, Math.Max(0, areaSize.Width - width));
        }

        if (areaSize.Height > 0)
        {
            y = Math.Min(y, Math.Max(0, areaSize.Height - height));
        }

        return new EsRect(x, y, width, height);
    }

    /// <summary>背景の左上から、文字の左上原点を求める（背景の内側余白だけ空ける）。</summary>
    /// <param name="boxTopLeft">背景の左上（DIP）。</param>
    /// <param name="paddingX">左右の内側余白（DIP）。0 以上。</param>
    /// <param name="paddingY">上下の内側余白（DIP）。0 以上。</param>
    /// <returns>文字の左上原点（DIP）。</returns>
    /// <remarks>背景と文字の余白が一致する（<see cref="ComputeAlignedBox"/> が同じ余白で背景を求める）。</remarks>
    public static EsPoint ComputeOffsetTextOrigin(EsPoint boxTopLeft, double paddingX, double paddingY)
        => new(boxTopLeft.X + paddingX, boxTopLeft.Y + paddingY);

    /// <summary>縦積みしたラベルの次の行の上端を求める。</summary>
    /// <param name="top">現在の行の上端（DIP）。</param>
    /// <param name="lineHeight">現在の行の高さ（DIP。フォントサイズ等）。</param>
    /// <param name="paddingY">上下の内側余白（DIP）。0 以上。</param>
    /// <returns>次の行の上端（DIP。現在の行の高さ + 内側余白 × 2 だけ下）。</returns>
    /// <remarks>
    /// ラベルを同じ間隔で縦に積む規則を本メソッドが単一所有する（行送りを各 UI・各ホストへ書かせない）。<br/>
    /// <b>行送り ≠ 背景の下端</b>: 背景は基準点（アンカー）から求め、行送りは基準点の間隔である。
    /// </remarks>
    public static double NextTop(double top, double lineHeight, double paddingY)
        => top + lineHeight + paddingY * 2;
}
