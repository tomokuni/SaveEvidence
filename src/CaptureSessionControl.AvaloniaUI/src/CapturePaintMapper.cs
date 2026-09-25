using EsUtil.SaveEvidence.Loupe.UIBase;
using EsUtil.SaveEvidence.Marquee.UIBase;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.SaveEvidence.CaptureSession.AvaloniaUI;

/// <summary>キャプチャ設定から描画の設定（色）を組み立てる（Avalonia 版のホスト専用）。</summary>
/// <remarks>
/// <b>なぜホスト（UI 層）が持つか</b>: 色は<b>描画にだけ必要な設定</b>であり、描画の設定（<c>MarqueePaint</c> / <c>LoupePaint</c>）が<br/>
/// 単一所有する。コアの設定マッパー（<c>FadeMaskDragRectOptionsMapper</c>）は <c>CaptureSessionControl.Core</c> にあり<br/>
/// 描画の設定（UIBase）を参照できない（Core → UIBase の逆流になる）ため、色の写像は UI 層のホストが担う。<br/>
/// <b>両 UI で重複する</b>: 同じ写像が MewUI 版にもある（描画の設定は UI 非依存の型のため、本来は共有できる）。<br/>
/// 共有の置き場所（<c>FadeMaskDragRectControl.UIBase</c>）を作る段階で集約する。
/// </remarks>
internal static class CapturePaintMapper
{
    /// <summary>キャプチャ設定から選択矩形コントロールの色を組み立てる。</summary>
    /// <param name="settings">キャプチャ設定（全メンバー non-null。<c>ToNonNull</c> 済みを渡す）。</param>
    /// <returns>選択矩形コントロールの描画に使う色。</returns>
    /// <remarks>
    /// 1. ハンドル・始点マーカー・ラベル・暗転マスクの色を写像する。<br/>
    /// 2. 枠線（<see cref="CaptureSettings.BorderColor"/>）は、暗転マスクの穴あけ枠（外側の装飾）とハンドルの枠線の両方へ同じ値を割り当てる<br/>
    ///    （両者は同じ矩形に重なるため、別々の値にすると見た目が崩れる）。<br/>
    /// 3. サイズラベルの背景はラベル共通の背景色（<see cref="CaptureSettings.TextBackgroundColor"/>）を使う。
    /// </remarks>
    public static MarqueePaint ToMarqueePaint(CaptureSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new MarqueePaint
        {
            HandleColor = settings.HandleColor!.Value,
            HandleBorderColor = settings.BorderColor!.Value,
            MarkerColor = settings.MarkerColor!.Value,
            SizeLabelColor = settings.SizeLabelColor!.Value,
            SizeLabelBackgroundColor = settings.TextBackgroundColor!.Value,
            CoordLabelColor = settings.CoordLabelColor!.Value,
            CoordLabelBackgroundColor = settings.CoordLabelBackgroundColor!.Value,
            OverlayColor = settings.OverlayColor!.Value,
            OuterBorderColor = settings.BorderColor!.Value,
        };
    }

    /// <summary>キャプチャ設定からルーペの色を組み立てる。</summary>
    /// <param name="settings">キャプチャ設定（全メンバー non-null。<c>ToNonNull</c> 済みを渡す）。</param>
    /// <param name="background">市松模様の下地色（ホストが決める。撮影オーバーレイは白、プレビューの切り出しはテーマ背景色）。</param>
    /// <returns>ルーペの描画に使う色。</returns>
    /// <remarks>市松模様の色は半透明のため、下地を塗らないと背後の要素（撮影画像・画面・プレビュー）が透ける。</remarks>
    public static LoupePaint ToLoupePaint(CaptureSettings settings, DrawingColor background)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new LoupePaint
        {
            BackgroundColor = background,
            CheckerColorA = settings.LoupeCheckerColorA!.Value,
            CheckerColorB = settings.LoupeCheckerColorB!.Value,
            FrameColor = settings.LoupeFrameColor!.Value,
            CrossColor = settings.LoupeCrossColor!.Value,
            CoordLabelColor = settings.LoupeCoordLabelColor!.Value,
            CoordLabelBackgroundColor = settings.LoupeCoordLabelBackgroundColor!.Value,
        };
    }
}
