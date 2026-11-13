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
    /// ポインタ位置は <c>pt: 8888, 8888</c> の書式で表示するため、その最も長い形を基準にする。
    /// </remarks>
    public const string PositionWidthReferenceText =
        StatusBarTexts.PointerPrefix + StatusBarTexts.PointerReferenceValueText;

    /// <summary>画像サイズの表示幅を決める基準の文字列（最も長い値）。</summary>
    /// <remarks>画像サイズは <c>size: 8888 x 8888</c> の書式で表示するため、その最も長い形を基準にする。</remarks>
    public const string ImageSizeWidthReferenceText =
        StatusBarTexts.ImageSizePrefix + StatusBarTexts.ImageSizeReferenceValueText;

    /// <summary>固定幅の項目の右側に置く空きの基準の文字列。</summary>
    /// <remarks>
    /// 区切りの幅を「W」1 文字分にする（空白文字の送り幅は UI の文字整形に依存し、幅が揃わない）。
    /// 幅は<b>プロポーショナル フォントでの送り幅</b>を測る（文字数では決まらない）。
    /// </remarks>
    public const string RightPaddingReferenceText = "W";

    /// <summary>固定幅の項目（マウスポインタ位置・画像サイズ）の寸法を求める。</summary>
    /// <param name="measureAdvance">
    /// 文字の送り幅（= 実際に描画される文字の幅。 内部余白を含めない）を測る関数（DIP）。
    /// </param>
    /// <returns>各項目の幅（コントロールへ設定する値）と、右側の空きの幅。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="measureAdvance"/> が null の場合。</exception>
    /// <remarks>
    /// <b>幅 = 基準の文字列の送り幅 + 「W」の送り幅</b>。 文字は左寄せで表示する。<br/>
    /// 基準の文字列と同じ長さの値なら、右側の空きはちょうど「W」になる（基準より短い値では、<br/>
    /// その余りが右側に出る。 左寄せのため）。<br/>
    /// <b>幅を文字数で決めない理由</b>: 文字はプロポーショナル フォントで描くため、文字数が同じでも<br/>
    /// 幅は同じにならない（<paramref name="measureAdvance"/> で実際の文字列を測る）。<br/>
    /// <b>内部余白を足さない理由</b>: コントロールの測定値（内部余白込みの推奨幅）を使うと、余白の分だけ<br/>
    /// 右の空きが「W」より広くなる（実測 2026-11-13: WinForms 版で 19 px = 「W」11 px + 内部余白 8 px）。<br/>
    /// 文字は左寄せで描くため、送り幅で足りる。<br/>
    /// <b>幅は基準の文字列で決まる</b>ため、値が変わっても右側の項目は動かない。<br/>
    /// <br/>
    /// <b>測るのは UI の仕事</b>: 送り幅はフォントと描画の実装で決まるため、コアは測れない。
    /// </remarks>
    public static StatusBarFieldWidths ResolveFieldWidths(Func<string, double> measureAdvance)
    {
        ArgumentNullException.ThrowIfNull(measureAdvance);

        var gap = measureAdvance(RightPaddingReferenceText);

        return new StatusBarFieldWidths(
            measureAdvance(PositionWidthReferenceText) + gap,
            measureAdvance(ImageSizeWidthReferenceText) + gap,
            gap);
    }
}