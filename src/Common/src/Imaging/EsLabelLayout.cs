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

    /// <summary>文字の左上原点を基準に、背景矩形を求める（文字の実寸へ内側余白を四方へ加える）。</summary>
    /// <param name="textOrigin">文字の左上原点（DIP）。</param>
    /// <param name="textSize">テキストの実寸（描画 API で測定した値・DIP）。</param>
    /// <param name="paddingX">左右の内側余白（DIP）。0 以上。</param>
    /// <param name="paddingY">上下の内側余白（DIP）。0 以上。</param>
    /// <returns>背景矩形（文字の左上原点から内側余白の分だけ四方へ広げた矩形）。</returns>
    public static EsRect ComputeBox(EsPoint textOrigin, EsSize textSize, double paddingX, double paddingY)
        => new(
            textOrigin.X - paddingX,
            textOrigin.Y - paddingY,
            textSize.Width + paddingX * 2,
            textSize.Height + paddingY * 2);

    /// <summary>基準点を背景の左上に置き、背景矩形を求める（文字の実寸へ内側余白を四方へ加える）。</summary>
    /// <param name="anchor">背景の左上に置く基準点（DIP）。</param>
    /// <param name="textSize">テキストの実寸（描画 API で測定した値・DIP）。</param>
    /// <param name="paddingX">左右の内側余白（DIP）。0 以上。</param>
    /// <param name="paddingY">上下の内側余白（DIP）。0 以上。</param>
    /// <returns>背景矩形（基準点を左上とする）。</returns>
    /// <remarks>
    /// 穴あけ矩形の左上へラベルを積む場合に使う（基準点が背景の位置そのものになる）。<br/>
    /// 文字の原点は <see cref="ComputeOffsetTextOrigin"/> が同じ余白から求める（背景と文字の余白が一致する）。
    /// </remarks>
    public static EsRect ComputeBoxAtAnchor(EsPoint anchor, EsSize textSize, double paddingX, double paddingY)
        => new(anchor.X, anchor.Y, textSize.Width + paddingX * 2, textSize.Height + paddingY * 2);

    /// <summary>基準点から、文字の左上原点を求める（背景の内側余白だけ空ける）。</summary>
    /// <param name="anchor">背景の左上に置いた基準点（DIP）。</param>
    /// <param name="paddingX">左右の内側余白（DIP）。0 以上。</param>
    /// <param name="paddingY">上下の内側余白（DIP）。0 以上。</param>
    /// <returns>文字の左上原点（DIP）。</returns>
    public static EsPoint ComputeOffsetTextOrigin(EsPoint anchor, double paddingX, double paddingY)
        => new(anchor.X + paddingX, anchor.Y + paddingY);

    /// <summary>カーソル位置を基準に、ラベルの背景矩形を求める（右下へオフセットし、描画領域内へクランプする）。</summary>
    /// <param name="cursor">カーソルの座標（DIP。描画領域と同じ座標系）。</param>
    /// <param name="areaSize">描画領域のサイズ（DIP）。0 以下の軸はクランプしない。</param>
    /// <param name="textSize">テキストの実寸（描画 API で測定した値・DIP）。</param>
    /// <param name="paddingX">左右の内側余白（DIP）。0 以上。</param>
    /// <param name="paddingY">上下の内側余白（DIP）。0 以上。</param>
    /// <param name="offset">カーソルからラベルを離すオフセット（DIP）。0 以上。</param>
    /// <returns>背景矩形（DIP）。</returns>
    /// <remarks>
    /// カーソルの右下へ <paramref name="offset"/> だけ離して配置し、描画領域の右端・下端をはみ出す場合は<br/>
    /// 領域内へクランプする（ラベル全体が見えるよう左寄せ・上寄せ）。<br/>
    /// 領域サイズが 0 以下（未確定）の軸はクランプしない（レイアウト前の配置を勝手に動かさない）。<br/>
    /// 文字の原点は <see cref="ComputeOffsetTextOrigin"/> が同じ余白から求める。
    /// </remarks>
    public static EsRect ComputeCursorLabelBox(
        EsPoint cursor, EsSize areaSize, EsSize textSize, double paddingX, double paddingY, double offset)
    {
        var width = textSize.Width + paddingX * 2;
        var height = textSize.Height + paddingY * 2;
        var x = cursor.X + offset;
        var y = cursor.Y + offset;

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

    /// <summary>領域の上端中央に置くラベルの、背景矩形を求める。</summary>
    /// <param name="areaWidth">領域の幅（DIP）。</param>
    /// <param name="textSize">テキストの実寸（描画 API で測定した値・DIP）。</param>
    /// <param name="paddingX">左右の内側余白（DIP）。0 以上。</param>
    /// <param name="paddingY">上下の内側余白（DIP）。0 以上。</param>
    /// <param name="topMargin">領域の上端から背景までの距離（DIP）。0 以上。</param>
    /// <returns>背景矩形（水平位置は領域の中央、垂直位置は上端から <paramref name="topMargin"/>）。</returns>
    /// <remarks>
    /// 水平位置は背景の幅（文字の実寸 + 左右の余白）でセンタリングする<br/>
    /// （文字幅でセンタリングすると余白の分だけ左右へずれる）。<br/>
    /// 文字の原点は <see cref="ComputeOffsetTextOrigin"/> が同じ余白から求める。
    /// </remarks>
    public static EsRect ComputeTopCenterBox(
        double areaWidth, EsSize textSize, double paddingX, double paddingY, double topMargin)
    {
        var width = textSize.Width + paddingX * 2;
        return new EsRect((areaWidth - width) / 2, topMargin, width, textSize.Height + paddingY * 2);
    }

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
