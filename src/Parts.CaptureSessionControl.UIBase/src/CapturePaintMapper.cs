using EsUtil.Feature.Loupe;
using EsUtil.Feature.Marquee;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Setting;

namespace EsUtil.SaveEvidence.Parts.CaptureSession.UIBase;

/// <summary>キャプチャ設定（<see cref="CaptureSettings"/>）から描画の設定（色）を組み立てる。</summary>
/// <remarks>
/// <b>なぜ本層が持つか</b>: 色は描画にだけ必要な設定であり、描画の設定（<c>MarqueePaint</c> / <c>LoupeAppearance</c>）が<br/>
/// 単一所有する。 コアの設定マッパー（<c>SelectionOverlayOptionsMapper</c>）は <c>CaptureSessionControl.Core</c> にあり<br/>
/// 描画の設定を参照できない（Core → UIBase の逆流になる）ため、色の写像は本層が担う。<br/>
/// <br/>
/// <b>なぜセッション側の共有層か</b>: 写像元がセッションの設定型（<see cref="CaptureSettings"/>）であるため、<br/>
/// 再利用ライブラリ（<c>SelectionOverlayControl.UIBase</c>）に置くと後者が製品の設定型へ依存する。<br/>
/// 本層はセッションの設定と両 UI の中間にあり、両 UI のホストが同じ写像を共有できる。<br/>
/// <br/>
/// <b>寸法・表示可否は写像しない</b>: それらはコアの設定値（<c>SelectionOverlayOptions</c>）が持ち、<br/>
/// <c>SelectionOverlayOptionsMapper</c>（コア）が単一所有する（本層は色だけを扱う）。
/// </remarks>
internal static class CapturePaintMapper
{
    /// <summary>キャプチャ設定から選択矩形コントロールの色を組み立てる。</summary>
    /// <param name="settings">キャプチャ設定（全メンバー non-null。<c>ToNonNull</c> 済みを渡す）。</param>
    /// <returns>選択矩形コントロールの描画に使う色。</returns>
    /// <remarks>
    /// 1. ハンドル・始点マーカー・ラベル・暗転マスクの色を写像する。<br/>
    /// 2. 枠線（<see cref="CaptureSettings.BorderColor"/>）は、暗転マスクの外枠（外側の装飾）とハンドルの枠線の両方へ同じ値を割り当てる<br/>
    ///    （両者は同じ矩形に重なるため、別々の値にすると見た目が崩れる）。<br/>
    /// 3. サイズラベルの背景はラベル共通の背景色（<see cref="CaptureSettings.TextBackgroundColor"/>）を使う。<br/>
    /// 4. 内側の塗り・内側の枠線は写像しない（描画の可否は <see cref="MarqueeAppearance"/> のフラグが単一所有するため、既定の透明を保つ）。<br/>
    /// 5. 枠線の太さと表示可否（暗転・外枠・サイズラベル・座標ラベル）も写像する（描画にだけ必要な設定のため、コアの設定には含めない）。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> が null の場合。</exception>
    public static MarqueeAppearance ToMarqueeAppearance(CaptureSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new MarqueeAppearance
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
            OuterBorderThickness = settings.BorderThickness!.Value,
            // 色が透明でも描画の可否は設定のフラグが決める（従来の「アルファ 0 = 描かない」を明示フラグで表現する）
            ShowOverlay = settings.OverlayColor!.Value.IsVisible(),
            ShowOuterBorder = settings.BorderColor!.Value.IsVisible(),
            ShowSizeLabel = settings.SizeLabelVisible!.Value,
            ShowCoordLabel = settings.ShowCoordLabel!.Value,
        };
    }

    /// <summary>キャプチャ設定からルーペの外観を組み立てる。</summary>
    /// <param name="settings">キャプチャ設定（全メンバー non-null。<c>ToNonNull</c> 済みを渡す）。</param>
    /// <returns>ルーペの描画に使う外観。</returns>
    /// <remarks>
    /// 画像外（ペーストボード）の色は設定（<see cref="CaptureSettings.LoupePasteboardColor"/>）が単一所有する<br/>
    /// （描画層は本色で描画面全体を塗ってから拡大画像を重ねるため、画像の無い部分が本色の無地になる）。<br/>
    /// 透過させたい場合はアルファ付きの色を設定する（既定は不透明の中性グレー）。<br/>
    /// <b>枠線の太さとラベルの表示可否も本写像が担う</b>: これらは描画にだけ必要な設定であり、<br/>
    /// コアの設定（<c>LoupeOptions</c>）に含めないため、描画の設定（外観）へここで写像する。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> が null の場合。</exception>
    public static LoupeAppearance ToLoupeAppearance(CaptureSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new LoupeAppearance
        {
            PasteboardColor = settings.LoupePasteboardColor!.Value,
            BorderColor = settings.LoupeFrameColor!.Value,
            CrossColor = settings.LoupeCrossColor!.Value,
            CoordLabelColor = settings.LoupeCoordLabelColor!.Value,
            CoordLabelBackgroundColor = settings.LoupeCoordLabelBackgroundColor!.Value,
            BorderThickness = settings.LoupeFrameThickness!.Value,
            ShowCoordLabel = settings.LoupeShowCoordLabel!.Value,
        };
    }
}

