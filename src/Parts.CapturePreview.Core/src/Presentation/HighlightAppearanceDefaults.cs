using EsUtil.Feature.Marquee;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.Presentation;

/// <summary>切り出しビューの「強調（ハイライト）」の既定の外観を単一所有する。</summary>
/// <remarks>
/// 強調は「対象矩形を目立たせる」用途であり、選択の対話（ハンドル・始点マーカー）や全面暗転は意味を持たないため、<br/>
/// 内側の装飾だけを既定とする。 色（<see cref="MarqueeAppearance.InnerFillColor"/> / <see cref="MarqueeAppearance.InnerBorderColor"/>）は<br/>
/// ホストの用途で決まるため、既定は透明のままとする（ホストがコントロールの強調の外観プロパティで与える）。<br/>
/// 両 UI の複合コントロールとアプリが同じ既定を使うため、本層が単一所有する<br/>
/// （UI ごとに同じ <c>with</c> 式を書くと、片方だけ既定が変わる余地が残る）。
/// </remarks>
public static class HighlightAppearanceDefaults
{
    /// <summary>強調の既定の外観（内側の塗りと枠線だけを描く）。</summary>
    /// <value>ラベル・ハンドル・始点マーカー・暗転・外枠を描かない外観。</value>
    public static MarqueeAppearance Default { get; } = MarqueeAppearance.Default with
    {
        ShowHandles = false,
        ShowMarker = false,
        ShowSizeLabel = false,
        ShowCoordLabel = false,
        ShowOverlay = false,
        ShowOuterBorder = false,
        ShowInnerFill = true,
        ShowInnerBorder = true,
    };
}
