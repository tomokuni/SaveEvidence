using System.Collections.Immutable;

namespace EsUtil.Feature.Annotation;

/// <summary>書式付きの文字列（<see cref="TextRun"/> の並び）。</summary>
/// <remarks>
/// <b>1 文字だけ書式を変えられる</b>（run を分割して書式を変える）。 空の run は描画されない。<br/>
/// <b>改行は <c>\n</c> で表す</b>（吹き出し内の明示改行。 折り返しは <see cref="AnnotationPresenter"/> が行う）。
/// </remarks>
public sealed class RichText
{
    /// <summary>文字を持たない <see cref="RichText"/> を取得する。</summary>
    public static RichText Empty { get; } = new([]);

    /// <summary>空の文字列で初期化する。</summary>
    public RichText()
    {
        Runs = [];
    }

    /// <summary>run の並びで初期化する。</summary>
    /// <param name="runs">run の並び。</param>
    public RichText(ImmutableArray<TextRun> runs)
    {
        Runs = runs.IsDefault ? [] : runs;
    }

    /// <summary>run の並びを取得または設定する。</summary>
    public ImmutableArray<TextRun> Runs { get; set; }

    /// <summary>描く文字を持たないかどうかを取得する。</summary>
    /// <value>すべての run が空の場合は <see langword="true"/>。</value>
    public bool IsEmpty
    {
        get
        {
            foreach (var run in Runs)
            {
                if (!string.IsNullOrEmpty(run.Text))
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>書式を除いた文字列を取得する。</summary>
    /// <value>run を連結した文字列。</value>
    public string PlainText => string.Concat(Runs.Select(run => run.Text));

    /// <summary>単一の書式の文字列から生成する。</summary>
    /// <param name="text">文字列。</param>
    /// <param name="style">書式。</param>
    /// <returns>1 つの run からなる <see cref="RichText"/>。</returns>
    public static RichText Create(string text, AnnotationTextStyle style)
        => string.IsNullOrEmpty(text) ? Empty : new RichText([new TextRun(text, style)]);

    /// <summary>指定された範囲の書式を差し替えた新しい <see cref="RichText"/> を返す（文字単位の書式変更）。</summary>
    /// <param name="start">範囲の開始位置（0 起点。 文字数で数える）。</param>
    /// <param name="length">範囲の長さ（文字数。 0 以下は「全体」）。</param>
    /// <param name="style">適用する書式。</param>
    /// <returns>書式を差し替えた新しい値（元の値は変更しない）。</returns>
    /// <remarks>
    /// <b>run を境界で分割する</b>: 1 つの run の途中だけ書式を変える場合は、その位置で run を 2 つ（または 3 つ）に分ける。
    /// 挿入・削除で境界がずれないよう、run が自分の文字列を持つ設計と対になっている。<br/>
    /// 範囲が文字数を超える場合は末尾までに丸める（例外にしない）。
    /// </remarks>
    public RichText ApplyStyle(int start, int length, AnnotationTextStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);

        if (IsEmpty)
        {
            return this;
        }

        var total = PlainText.Length;
        var from = Math.Clamp(start, 0, total);
        var to = length <= 0 ? total : Math.Clamp(from + length, from, total);
        if (from == to)
        {
            return this;
        }

        var runs = ImmutableArray.CreateBuilder<TextRun>();
        var position = 0;
        foreach (var run in Runs)
        {
            var runStart = position;
            var runEnd = position + run.Text.Length;
            position = runEnd;

            if (runEnd <= from || runStart >= to)
            {
                AddMerged(runs, run);
                continue;
            }

            // 範囲の手前（書式を変えない部分）
            var localFrom = Math.Max(from, runStart) - runStart;
            var localTo = Math.Min(to, runEnd) - runStart;
            if (localFrom > 0)
            {
                AddMerged(runs, run with { Text = run.Text[..localFrom] });
            }

            AddMerged(runs, new TextRun(run.Text[localFrom..localTo], style));

            if (localTo < run.Text.Length)
            {
                AddMerged(runs, run with { Text = run.Text[localTo..] });
            }
        }

        return new RichText(runs.ToImmutable());
    }

    /// <summary>書式を位置で引き継いで本文を置き換えた新しい <see cref="RichText"/> を返す。</summary>
    /// <param name="text">新しい文字列（明示改行は <c>\n</c>）。</param>
    /// <returns>書式を引き継いだ文字列。</returns>
    /// <remarks>
    /// <b>書式は文字位置で引き継ぐ</b>: 元の文字列と同じ範囲は元の run の書式を使い、増えた部分は末尾の書式にする
    /// （編集のたびにすべての書式が消えないようにする）。 書式そのものを変えるときは <see cref="ApplyStyle"/> を使う。
    /// </remarks>
    public RichText WithPlainText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.Length == 0)
        {
            return Empty;
        }

        var previous = PlainText;
        var last = Math.Max(0, previous.Length - 1);
        var runs = ImmutableArray.CreateBuilder<TextRun>();

        for (var i = 0; i < text.Length; i++)
        {
            var style = StyleAt(Math.Min(i, last));
            if (runs.Count > 0 && Equals(runs[^1].Style, style))
            {
                runs[^1] = runs[^1] with { Text = runs[^1].Text + text[i] };
                continue;
            }

            runs.Add(new TextRun(text[i].ToString(), style));
        }

        return new RichText(runs.ToImmutable());
    }

    /// <summary>指定位置の文字の書式を取得する。</summary>
    /// <param name="index">文字の位置（0 起点）。</param>
    /// <returns>その文字の書式。 範囲外・空の場合は <see cref="AnnotationTextStyle.Default"/>。</returns>
    /// <remarks>書式ツールバーの初期表示（選択位置の現在値）に使う。</remarks>
    public AnnotationTextStyle StyleAt(int index)
    {
        var position = 0;
        foreach (var run in Runs)
        {
            position += run.Text.Length;
            if (index < position)
            {
                return run.Style;
            }
        }

        return Runs.LastOrDefault()?.Style ?? AnnotationTextStyle.Default;
    }

    /// <summary>末尾の run と同じ書式なら連結して追加する（run が細かく分かれないようにする）。</summary>
    /// <param name="runs">追加先。</param>
    /// <param name="run">追加する run。</param>
    private static void AddMerged(ImmutableArray<TextRun>.Builder runs, TextRun run)
    {
        if (string.IsNullOrEmpty(run.Text))
        {
            return;
        }

        if (runs.Count > 0 && Equals(runs[^1].Style, run.Style))
        {
            runs[^1] = runs[^1] with { Text = runs[^1].Text + run.Text };
            return;
        }

        runs.Add(run);
    }
}
