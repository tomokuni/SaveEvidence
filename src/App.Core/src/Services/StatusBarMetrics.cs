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
    /// <returns>各項目の表示幅（右側の「WW」の空きを含む）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="measureTight"/> が null の場合。</exception>
    /// <remarks>
    /// <b>右側の空きを幅に含める理由</b>: 利用者の要求は「『8888 x 8888』の右の文字描画開始位置までの<br/>
    /// スペースを『WW』の幅にする」である。 余白（Margin）とコントロールの内部余白（Padding）を<br/>
    /// 別々に足すと、UI 版ごとに差が出て「WW」より広くなる（実測 2026-11-13: 余白 28 px に<br/>
    /// 内部余白とマージンが加わり 36 px になっていた）。<br/>
    /// <b>幅 = 文字の送り幅 + 「WW」の送り幅</b> とし、UI 版は右の余白と次の項目の左の余白を 0 にする。<br/>
    /// 文字はコントロールの内部余白の分だけ右へずれて描かれ、次の項目も同じだけ左へずれるため、<br/>
    /// 見た目の空き（文字の右端から次の文字の左端まで）はちょうど「WW」の送り幅になる。<br/>
    /// 内部余白を文字の左に持たないコントロール（<c>TextBlock</c> など）でも同じ式で成り立つ。<br/>
    /// <br/>
    /// <b>測るのは UI の仕事</b>: 文字の送り幅はフォントと文字整形の実装で決まるため、コアは測れない。<br/>
    /// そのためコアは<b>基準の文字列と規則</b>だけを持ち、幅の計算は本メソッドへ集約する。
    /// </remarks>
    public static StatusBarFieldWidths ResolveFieldWidths(Func<string, double> measureTight)
    {
        ArgumentNullException.ThrowIfNull(measureTight);

        // 区切りの「WW」の送り幅を各項目の幅へ含める（本文の文字幅 + 区切り = 次の文字までの空き）
        var gap = measureTight(RightPaddingReferenceText);

        return new StatusBarFieldWidths(
            measureTight(PositionWidthReferenceText) + gap,
            measureTight(ImageSizeWidthReferenceText) + gap);
    }
}