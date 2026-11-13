namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>ステータス バーの寸法（4 つの UI 版で揃えるための単一所有者）。</summary>
/// <remarks>
/// 文字サイズを UI 版ごとに決めると、同じ行でも文字の大きさがずれて見える<br/>
/// （実測: AvaloniaUI 版だけがテーマの既定 14 DIP を使い、他 UI 版より 1 回り大きく見えた）。<br/>
/// <br/>
/// <b>注意</b>: 文字サイズは DIP で保持する。 WinForms の <c>Font</c> はポイント単位のため、<br/>
/// 使う側で <c>DIP × 72 ÷ 96</c> に換算する。
/// </remarks>
public static class StatusBarMetrics
{
    /// <summary>ステータス バーの文字サイズ（DIP）。</summary>
    public const double FontSize = 12;

    /// <summary>保存したファイル名と通知メッセージの間隔（DIP）。</summary>
    /// <remarks>
    /// 全角 1 文字分にする。 全角スペースの送り幅は文字サイズ（em）と同じため、<br/>
    /// 空白文字を並べるのではなく、この幅の空き要素を置く（UI 版ごとの余白の付け方が混ざらない）。
    /// </remarks>
    public const double NameMessageGapWidth = FontSize;

    /// <summary>マウスポインタ位置の表示幅を決める基準の文字列（最も長い値）。</summary>
    /// <remarks>
    /// 値の長さで幅が変わると、座標が変わるたびに右側の項目が動いて読みにくい（実測）。<br/>
    /// 4 桁どうしの座標（最大 <c>3840 x 2160</c> のような表示）が入る幅で固定する。
    /// </remarks>
    public const string PositionWidthReferenceText = "8888 x 8888";

    /// <summary>画像サイズの表示幅を決める基準の文字列（最も長い値）。</summary>
    /// <remarks>マウスポインタ位置と同じ書式・同じ桁数で表示するため、同じ基準を使う。</remarks>
    public const string ImageSizeWidthReferenceText = "8888 x 8888";

    /// <summary>固定幅の項目の右側に置く余白の基準の文字列。</summary>
    /// <remarks>
    /// 区切りの幅を「W」2 文字分にする（空白文字の送り幅は UI の文字整形に依存し、幅が揃わない）。
    /// </remarks>
    public const string RightPaddingReferenceText = "WW";

    /// <summary>固定幅の項目（マウスポインタ位置・画像サイズ）の寸法を求める。</summary>
    /// <param name="measureTight">文字の送り幅（= 描画される文字の幅）だけを測る関数（DIP で返す。 ステータス バーの文字で測ること）。</param>
    /// <returns>各項目の表示幅と、項目の右側に置く空きの幅。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="measureTight"/> が null の場合。</exception>
    /// <remarks>
    /// <b>空きを項目の幅に含めない理由</b>: 幅に含めると、値が短いとき（例: <c>640 x 400</c> や <c>-</c>）に<br/>
    /// 文字の右側の余りがそのまま空きへ加わり、「WW」より広く見える（実測 2026-11-13: <c>-</c> の右が 103 px）。<br/>
    /// 幅は「8888 x 8888」ちょうどに固定し、<b>文字を項目の右端へ寄せ</b>、<b>空きは独立した要素</b>にする。<br/>
    /// これで値の長さによらず「文字の右端から次の文字の開始位置まで」が常に「WW」の送り幅になる。<br/>
    /// <br/>
    /// <b>測るのは UI の仕事</b>: 文字の送り幅はフォントと文字整形の実装で決まるため、コアは測れない。<br/>
    /// そのためコアは<b>基準の文字列と規則</b>だけを持ち、幅の計算は本メソッドへ集約する。
    /// </remarks>
    public static StatusBarFieldWidths ResolveFieldWidths(Func<string, double> measureTight)
        => ResolveFieldWidths(measureTight, measureTight);

    /// <summary>固定幅の項目の寸法を、項目の幅と右側の空きで別々の関数を使って求める。</summary>
    /// <param name="measureFieldText">項目の幅を測る関数（コントロールが描画時に足す余白を含めること。 含めないと文字が欠ける）。</param>
    /// <param name="measureGapText">右側の空きを測る関数（文字の送り幅だけを返すこと）。</param>
    /// <returns>各項目の表示幅と、項目の右側に置く空きの幅。</returns>
    /// <exception cref="ArgumentNullException">いずれかの引数が null の場合。</exception>
    /// <remarks>
    /// <b>幅と空きで測り方を変える理由</b>: 幅は文字が欠けないようコントロールの内部余白を含める必要がある。<br/>
    /// 一方、空きは「文字の右端から次の文字の開始位置まで」を「WW」にするため、内部余白を含めない<br/>
    /// 送り幅で測る（含めると空きが「WW」より広くなる）。<br/>
    /// コントロールが描画時に文字を右端から内側へ寄せる分は <c>GapCompensation</c> として UI が差し引く。
    /// </remarks>
    public static StatusBarFieldWidths ResolveFieldWidths(
        Func<string, double> measureFieldText, Func<string, double> measureGapText)
    {
        ArgumentNullException.ThrowIfNull(measureFieldText);
        ArgumentNullException.ThrowIfNull(measureGapText);

        return new StatusBarFieldWidths(
            measureFieldText(PositionWidthReferenceText),
            measureFieldText(ImageSizeWidthReferenceText),
            measureGapText(RightPaddingReferenceText));
    }
}