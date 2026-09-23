using EsUtil.Platform.Core.Geometry;

namespace Estable.DragRect.Decoration;

/// <summary>暗転オーバーレイのラベル（サイズラベル・サブライン）のレイアウト計算（純粋関数）。</summary>
/// <remarks>
/// ラベルは「テキスト + 内側余白の背景」を行として選択矩形の左上から縦に積む。<br/>
/// テキストの実寸（幅・高さ）は UI の描画 API でしか測れないため引数で受け取り、<br/>
/// 余白・背景矩形・次の行の位置は本クラスが単一所有する（両 UI の見た目を一致させる）。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
internal static class OuterDecoLabelLayout
{
    /// <summary>ラベルの内側余白（DIP）。</summary>
    public const double Padding = 4.0;

    /// <summary>ラベル背景の矩形を計算する。</summary>
    /// <param name="origin">ラベル行の原点（選択矩形の左上）。</param>
    /// <param name="textSize">テキストの実寸（描画 API で測定した値）。</param>
    /// <returns>背景の矩形（原点 + 内側余白 × 2）。</returns>
    /// <remarks>背景はテキストを内側余白で囲んだサイズとする。</remarks>
    public static EsRect ComputeBackground(EsPoint origin, EsSize textSize)
        => new(origin.X, origin.Y, textSize.Width + Padding * 2, textSize.Height + Padding * 2);

    /// <summary>ラベル文字の描画原点を計算する。</summary>
    /// <param name="origin">ラベル行の原点（背景の左上）。</param>
    /// <returns>文字の描画原点（左へ内側余白 × 2、上へ内側余白）。</returns>
    /// <remarks>横方向の余白を 2 倍にするのは Avalonia 版の描画と同一の見た目にするため。</remarks>
    public static EsPoint ComputeTextOrigin(EsPoint origin)
        => new(origin.X + Padding * 2, origin.Y + Padding);

    /// <summary>次のラベル行の原点 Y 座標を計算する。</summary>
    /// <param name="background">直前の行の背景矩形。</param>
    /// <returns>次の行の原点 Y 座標（背景の下端 + 内側余白）。</returns>
    public static double NextLineY(EsRect background) => background.Bottom + Padding;
}
