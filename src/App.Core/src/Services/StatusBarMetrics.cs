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
    /// <param name="measureAdvance">文字の送り幅（= 描画される文字の幅）だけを測る関数（DIP）。</param>
    /// <param name="measureBox">
    /// コントロールが必要とする幅（内部余白込みの推奨幅。 DIP）。 文字より小さくすると折り返すため、
    /// 幅にはこの値を使う。 内部余白を持たないコントロールでは <paramref name="measureAdvance"/> と同じ値を返す。
    /// </param>
    /// <returns>各項目の幅（コントロールへ設定する値）と、右側の余白（<c>Margin</c> の右へ設定する値）。</returns>
    /// <exception cref="ArgumentNullException">いずれかの引数が null の場合。</exception>
    /// <remarks>
    /// <b>幅 = コントロールの推奨幅（基準の文字列）、右の余白 = 「WW」の送り幅 - 内部余白</b>。<br/>
    /// 文字を右端へ寄せると、コントロールの内部余白（右）の分だけ内側で止まり、次の項目は余白の分だけ離れる。<br/>
    /// そのため内部余白を余白の値から差し引くと、「文字の右端から次の項目の文字の開始位置まで」が<br/>
    /// 値の長さによらず「WW」になる（短い値の余りは文字の左側へ逃げる）。<br/>
    /// <b>幅に推奨幅を使う理由</b>: 送り幅だけで幅を決めると、値が基準の文字列よりわずかに広い場合に<br/>
    /// 折り返す（実測 2026-11-13: WinForms 版で「1920 x 1200」が 2 行になった）。<br/>
    /// <b>合計は一定</b>: 幅 + 余白 = 基準の文字列の送り幅 + 「WW」の送り幅 になり、値が変わっても列は動かない。<br/>
    /// <br/>
    /// <b>測るのは UI の仕事</b>: 文字の送り幅と内部余白はフォントと描画の実装で決まるため、コアは測れない。
    /// </remarks>
    public static StatusBarFieldWidths ResolveFieldWidths(
        Func<string, double> measureAdvance, Func<string, double> measureBox)
    {
        ArgumentNullException.ThrowIfNull(measureAdvance);
        ArgumentNullException.ThrowIfNull(measureBox);

        // 内部余白 = 推奨幅 - 送り幅（コントロールが文字の左右に足す分の合計）
        var innerPadding = measureBox(PositionWidthReferenceText) - measureAdvance(PositionWidthReferenceText);

        // 右の余白 = 「WW」の送り幅 - 内部余白（合計が「基準の文字幅 + WW」になるようにする）
        var margin = Math.Max(0, measureAdvance(RightPaddingReferenceText) - innerPadding);

        return new StatusBarFieldWidths(
            measureBox(PositionWidthReferenceText),
            measureBox(ImageSizeWidthReferenceText),
            margin);
    }
}