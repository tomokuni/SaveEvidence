using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.MarqueeCore.Decoration;

/// <summary>選択矩形の外側・内側に描く装飾（暗転マスク・穴あけ枠・ラベル・ハイライト）の設定をまとめた不変の値。</summary>
/// <remarks>
/// 個別の依存関係プロパティとして公開していた装飾設定を 1 つの値にまとめ、UI 層の公開 API を 1 プロパティへ縮小する。<br/>
/// 設定の既定値も本型が単一所有する（各 UI 実装は既定値を持たず、<see cref="Default"/> を基点に部分変更する）。<br/>
/// <br/>
/// <b>装飾は選択矩形コントロール単体の概念ではない</b>: 「画面全体を暗転する」「穴あけ枠を描く」「寸法・操作を文字で示す」は、<br/>
/// 選択コントロールを撮影オーバーレイ等へ組み込むときにだけ必要になる表示である。<br/>
/// そのため本型は選択コア（<c>Feature.MarqueeCore</c>）ではなく本層が所有する（選択の判断はコア、装飾は本層）。<br/>
/// <br/>
/// <b>状態（選択矩形・ハイライト矩形）は持たない</b>: 選択矩形はコアのセッションが単一所有し、<br/>
/// ハイライト矩形はホストが与える実行時状態として <see cref="OuterHighlightRect"/> に持つ<br/>
/// （null が「ハイライトしない」を意味する）。<br/>
/// <br/>
/// <b>色の型</b>: UI 非依存の <see cref="System.Drawing.Color"/>（アルファで不透明度を表す）を使用し、<br/>
/// 各 UI の色型への変換は境界（UI 層）で 1 行行う。
/// </remarks>
public sealed record MarqueeDecoOptions
{
    /// <summary>すべての項目を既定値にした設定。</summary>
    /// <value>既定の装飾（暗転・枠線・3 行のラベル・内側は描かない）。</value>
    /// <remarks>部分変更の基点として使用する（<c>MarqueeDecoOptions.Default with { ... }</c>）。</remarks>
    public static MarqueeDecoOptions Default { get; } = new();

    /// <summary>外側の装飾の外観。</summary>
    /// <value>暗転色・穴あけ枠の色と太さ・3 種のラベルの色とフォントサイズ。既定は暗転と枠線とラベルを描く外観。</value>
    /// <remarks>
    /// 穴あけ枠は選択矩形の内側に同じ矩形で描かれるため、<b>枠線の色と太さは本値が単一所有する</b><br/>
    /// （選択枠線と暗転マスクの枠を別々に持つと重なりの見た目が崩れる）。<br/>
    /// アルファが 0 の枠線色を指定した場合は枠線を描かない（描画の要否はフレーム解決が判断する）。
    /// </remarks>
    public OuterDecoAppearance OuterAppearance { get; init; } = new();

    /// <summary>穴あけ矩形が無くても全面を暗転するかどうか。</summary>
    /// <value>true の場合は選択が無くても画面全体を暗転する。既定は false。</value>
    /// <remarks>ハイライトのみを表示するモード（全画面の暗転 + ハイライト矩形）で使用する。</remarks>
    public bool OuterShowFullOverlay { get; init; }

    /// <summary>ハイライトする矩形（ドメイン座標）。</summary>
    /// <value>ハイライト矩形。null（既定）の場合はハイライトしない。</value>
    /// <remarks>
    /// ホストが与える値である（選択矩形と異なり選択コントロールの操作では変化しない）。<br/>
    /// <b>null が「ハイライトしない」を意味する</b>（有効フラグは持たない。ハイライトをやめる場合は null にする）。
    /// </remarks>
    public EsRect? OuterHighlightRect { get; init; }

    /// <summary>内側の装飾（穴あけ矩形の内側の塗りと枠線）の外観。</summary>
    /// <value>塗り色・枠線色・太さ。既定はどちらも透明（描かない）。</value>
    /// <remarks>
    /// 対象の穴あけ矩形は外側の装飾と同じ（ハイライト優先 → 選択矩形）である。<br/>
    /// 選択矩形（ドラッグ中の領域）の内側を塗る用途と、<see cref="OuterHighlightRect"/> で与えたハイライト矩形を<br/>
    /// 「枠線 + 半透明の塗り」で強調する用途に使う（後者は外側の枠線色を透明にして本値だけで表現する）。<br/>
    /// 外側の枠線（<see cref="OuterAppearance"/>）と同時に指定すると同じ位置へ二重に描画される点に注意する。
    /// </remarks>
    public InnerDecoAppearance InnerAppearance { get; init; } = new();
}
