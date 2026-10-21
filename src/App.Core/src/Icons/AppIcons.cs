using EsUtil.Platform.Core.Svg;

namespace EsUtil.SaveEvidence.App.Core.Icons;

/// <summary>本アプリのアイコンの読み込み元（<c>App.Core</c> に埋め込んだ SVG）を単一所有する。</summary>
/// <remarks>
/// <b>アイコンの出所を 1 か所にする</b>ため、Fluent System Icons 形式の SVG（viewBox は 24x24 の
/// 単一 <c>&lt;path d="..."&gt;</c>）を本プロジェクトの <c>Icons/</c> に置き、埋め込みリソースとして読む。
/// UI 版ごとに SVG を複製すると、片方だけ直したときに見た目がずれる。<br/>
/// <b>解析は <see cref="SvgIconSource"/>（コア）が担う</b>: 本クラスはリソースの論理名だけを知る
/// （埋め込みリソースの前置きは <c>App.Core.csproj</c> の <c>LogicalName</c> と一致させる）。
/// </remarks>
internal static class AppIcons
{
    /// <summary>埋め込みリソースの論理名プレフィックス（<c>App.Core.csproj</c> の論理名と一致させる）。</summary>
    private const string ResourcePrefix = "EsUtil.SaveEvidence.App.Core.Icons.";

    /// <summary>アイコンの読み込み元。</summary>
    /// <remarks>解析結果は読み込み元が共有する（同じアイコンを表示のたびに解析しない）。</remarks>
    public static SvgIconSource Source { get; } = SvgIconSource.FromEmbeddedResource(typeof(AppIcons).Assembly, ResourcePrefix);
}
