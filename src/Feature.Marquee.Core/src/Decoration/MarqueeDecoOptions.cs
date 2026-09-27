using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Marquee.Core.Decoration;

/// <summary>選択矩形の外側・内側に描く装飾（暗転マスク・穴あけ枠・ラベル・ハイライト）の設定をまとめた不変の値。</summary>
/// <remarks>
/// 個別の依存関係プロパティとして公開していた装飾設定を 1 つの値にまとめ、UI 層の公開 API を 1 プロパティへ縮小する。<br/>
/// 設定の既定値も本型が単一所有する（各 UI 実装は既定値を持たず、<see cref="Default"/> を基点に部分変更する）。<br/>
/// <br/>
/// <b>装飾は選択矩形コントロール単体の概念ではない</b>: 「画面全体を暗転する」「穴あけ枠を描く」「寸法・操作を文字で示す」は、<br/>
/// 選択コントロールを撮影オーバーレイ等へ組み込むときにだけ必要になる表示である。<br/>
/// 選択の判断ではないため選択の設定（<c>MarqueeOptions</c>）とは分けるが、<b>Core 内のサブ概念</b>として同居する<br/>
/// （<c>src/Decoration/</c> に置き、別プロジェクトには分けない）。<br/>
/// <br/>
/// <b>状態（選択矩形・ハイライト矩形）は持たない</b>: 選択矩形はコアのセッションが単一所有し、<br/>
/// ハイライト矩形はホストが与える実行時状態として <see cref="OuterHighlightRect"/> に持つ<br/>
/// （null が「ハイライトしない」を意味する）。<br/>
/// <br/>
/// <b>色は含まない</b>: 色は<b>描画にだけ必要な設定</b>であり、描画の設定（<c>MarqueePaint</c>。UIBase が単一所有する）が持つ。<br/>
/// 描画の可否（暗転するか・枠線を描くか・内側を塗るか）は本型の明示フラグ（<see cref="ShowOverlay"/> 等）が単一所有する<br/>
/// （アルファ 0 を「描かない」の代用にしない）。
/// </remarks>
public sealed record MarqueeDecoOptions
{
    /// <summary>すべての項目を既定値にした設定。</summary>
    /// <value>既定の装飾（暗転・枠線を描き、内側は描かない）。</value>
    /// <remarks>部分変更の基点として使用する（<c>MarqueeDecoOptions.Default with { ... }</c>）。</remarks>
    public static MarqueeDecoOptions Default { get; } = new();

    /// <summary>外側の装飾の寸法を取得する。</summary>
    /// <value>穴あけ枠の太さ。既定は 2.0。</value>
    /// <remarks>穴あけ枠は選択矩形の内側に同じ矩形で描かれるため、<b>枠線の太さは本値が単一所有する</b>。</remarks>
    public MarqueeOuterDecoAppearance OuterAppearance { get; init; } = new();

    /// <summary>暗転マスクを描くかどうかを取得する。</summary>
    /// <value>true（既定）の場合は暗転する。false の場合は暗転を描かない。</value>
    /// <remarks>描画の可否を明示フラグで持つ（色のアルファを「描くかどうか」の代用にしない）。</remarks>
    public bool ShowOverlay { get; init; } = true;

    /// <summary>穴あけ枠（外側の枠線）を描くかどうかを取得する。</summary>
    /// <value>true（既定）の場合は枠線を描く。false の場合は描かない。</value>
    /// <remarks>描画の可否を明示フラグで持つ（色のアルファを「描くかどうか」の代用にしない）。</remarks>
    public bool ShowOuterBorder { get; init; } = true;

    /// <summary>穴あけ矩形が無くても全面を暗転するかどうかを取得する。</summary>
    /// <value>true の場合は選択が無くても画面全体を暗転する。既定は false。</value>
    /// <remarks>ハイライトのみを表示するモード（全画面の暗転 + ハイライト矩形）で使用する。</remarks>
    public bool OuterShowFullOverlay { get; init; }

    /// <summary>ハイライトする矩形（ドメイン座標）を取得する。</summary>
    /// <value>ハイライト矩形。null（既定）の場合はハイライトしない。</value>
    /// <remarks>
    /// ホストが与える値である（選択矩形と異なり選択コントロールの操作では変化しない）。<br/>
    /// <b>null が「ハイライトしない」を意味する</b>（有効フラグは持たない。ハイライトをやめる場合は null にする）。
    /// </remarks>
    public EsRect? OuterHighlightRect { get; init; }

    /// <summary>内側の装飾（穴あけ矩形の内側の塗りと枠線）の寸法を取得する。</summary>
    /// <value>枠線の太さ。既定は 2.0。</value>
    /// <remarks>
    /// 対象の穴あけ矩形は外側の装飾と同じ（ハイライト優先 → 選択矩形）である。<br/>
    /// 選択矩形（ドラッグ中の領域）の内側を塗る用途と、<see cref="OuterHighlightRect"/> で与えたハイライト矩形を<br/>
    /// 「枠線 + 半透明の塗り」で強調する用途に使う（後者は描画の設定で外側の枠線を透明にして本値だけで表現する）。
    /// </remarks>
    public MarqueeInnerDecoAppearance InnerAppearance { get; init; } = new();

    /// <summary>穴あけ矩形の内側を塗るかどうかを取得する。</summary>
    /// <value>true の場合は塗る。既定は false（塗らない）。</value>
    /// <remarks>描画の可否を明示フラグで持つ（色のアルファを「描くかどうか」の代用にしない）。</remarks>
    public bool ShowInnerFill { get; init; }

    /// <summary>穴あけ矩形の内側の枠線を描くかどうかを取得する。</summary>
    /// <value>true の場合は描く。既定は false（描かない）。</value>
    /// <remarks>描画の可否を明示フラグで持つ（色のアルファを「描くかどうか」の代用にしない）。</remarks>
    public bool ShowInnerBorder { get; init; }

    /// <summary>暗転オーバーレイを描画するかどうかを判定する。</summary>
    /// <param name="hasHoleRect">穴あけ矩形（選択またはハイライトの領域）があるかどうか。</param>
    /// <returns>描画する場合は true。</returns>
    /// <remarks>
    /// 穴あけ矩形がある場合、または全面暗転表示が有効な場合に描画する。<br/>
    /// 穴あけ矩形がある場合の暗転は「描画領域 − 穴あけ矩形」の 4 領域で表現する（穴あけ矩形を除いた帯状の矩形）。<br/>
    /// 表示可否の判断（設定値と矩形の有無の組合せ）を本メソッドが単一所有し、コアのフレーム解決は判定結果だけを使う。
    /// </remarks>
    public bool ShouldDrawOverlay(bool hasHoleRect) => hasHoleRect || OuterShowFullOverlay;
}
