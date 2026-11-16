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
}
