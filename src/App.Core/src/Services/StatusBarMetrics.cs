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

    /// <summary>固定幅の項目の幅に足す余裕（DIP）。</summary>
    /// <remarks>
    /// 測った文字の幅と実際の描画の幅は一致しない（文字のヒンティングと、コントロールが内部で足す余白のため<br/>
    /// 実描画の方が 1〜3 px 広くなる）。 実測 2026-11-13: 125% の環境で、測った幅をそのまま使うと<br/>
    /// 「3840 x 2160」の末尾が 1〜4 px 欠けた（WinForms 版・MewUI 版）。<br/>
    /// 余裕を足しても各項目の幅は固定のままで、値によって動かない。
    /// </remarks>
    public const double FieldWidthSafetyDips = 4;

    /// <summary>固定幅の項目（マウスポインタ位置・画像サイズ）の寸法を求める。</summary>
    /// <param name="measureText">文字列の描画幅を測る関数（DIP で返す。 ステータス バーの文字で測ること。 コントロールが描画時に足す余白を含めること）。</param>
    /// <returns>各項目の表示幅と右側の余白。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="measureText"/> が null の場合。</exception>
    /// <remarks>
    /// <b>測るのは UI の仕事</b>: 文字の送り幅はフォントと文字整形の実装で決まるため、コアは測れない。<br/>
    /// そのためコアは<b>基準の文字列と規則</b>だけを持ち、幅の計算は本メソッドへ集約する
    /// （UI 版ごとに「どの文字列で測るか」を書き分けると、幅が版ごとにずれる）。<br/>
    /// 右側の余白には <paramref name="measureText"/> をそのまま使う（コントロールの余白を含めない
    /// 送り幅で測りたい場合は、余白を含まない関数を渡す overload を使う）。
    /// </remarks>
    public static StatusBarFieldWidths ResolveFieldWidths(Func<string, double> measureText)
        => ResolveFieldWidths(measureText, measureText);

    /// <summary>固定幅の項目の寸法を、項目の幅と右側の余白で別々の関数を使って求める。</summary>
    /// <param name="measureFieldText">項目の幅を測る関数（コントロールが描画時に足す余白を含めること）。</param>
    /// <param name="measureGapText">右側の余白を測る関数（文字の送り幅だけを返すこと）。</param>
    /// <returns>各項目の表示幅と右側の余白。</returns>
    /// <exception cref="ArgumentNullException">いずれかの引数が null の場合。</exception>
    /// <remarks>
    /// <b>余白だけ送り幅で測る理由</b>: 項目の幅は文字の欠けを防ぐためコントロールの内部余白を含める必要が<br/>
    /// あるが、余白（区切り）まで内部余白を含めると、見た目の空きが「WW」より広くなる。<br/>
    /// 実測 2026-11-13: WinForms 版の <c>Label</c> は左右に余白を足すため、同じ関数で測ると区切りだけが 8 px 広かった。
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
