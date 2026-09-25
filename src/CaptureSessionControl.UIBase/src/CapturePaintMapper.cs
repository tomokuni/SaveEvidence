using EsUtil.SaveEvidence.Loupe.UIBase;
using EsUtil.SaveEvidence.Marquee.UIBase;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.SaveEvidence.CaptureSession.UIBase;

/// <summary>キャプチャ設定（<see cref="CaptureSettings"/>）から描画の設定（色）を組み立てる。</summary>
/// <remarks>
/// <b>なぜ本層が持つか</b>: 色は描画にだけ必要な設定であり、描画の設定（<c>MarqueePaint</c> / <c>LoupePaint</c>）が<br/>
/// 単一所有する。コアの設定マッパー（<c>FadeMaskDragRectOptionsMapper</c>）は <c>CaptureSessionControl.Core</c> にあり<br/>
/// 描画の設定を参照できない（Core → UIBase の逆流になる）ため、色の写像は本層が担う。<br/>
/// <br/>
/// <b>なぜセッション側の共有層か</b>: 写像元がセッションの設定型（<see cref="CaptureSettings"/>）であるため、<br/>
/// 再利用ライブラリ（<c>FadeMaskDragRectControl.UIBase</c>）に置くと後者が製品の設定型へ依存する。<br/>
/// 本層はセッションの設定と両 UI の中間にあり、両 UI のホストが同じ写像を共有できる。<br/>
/// <br/>
/// <b>寸法・表示可否は写像しない</b>: それらはコアの設定値（<c>FadeMaskDragRectOptions</c>）が持ち、<br/>
/// <c>FadeMaskDragRectOptionsMapper</c>（コア）が単一所有する（本層は色だけを扱う）。
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
    /// 3. サイズラベルの背景はラベル共通の背景色（<see cref="CaptureSettings.TextBackgroundColor"/>）を使う。<br/>
    /// 4. 内側の塗り・内側の枠線は写像しない（描画の可否は <c>MarqueeDecoOptions</c> のフラグが単一所有するため、既定の透明を保つ）。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> が null の場合。</exception>
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
    /// <param name="background">市松模様の下地色（ホストが決める。撮影オーバーレイは背後の撮影画像を透かすため透明、プレビューの切り出しはテーマ背景色）。</param>
    /// <returns>ルーペの描画に使う色。</returns>
    /// <remarks>市松模様の色は半透明のため、下地を塗らないと背後の要素（撮影画像・画面・プレビュー）が透ける。</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> が null の場合。</exception>
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
