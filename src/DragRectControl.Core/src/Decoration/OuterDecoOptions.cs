using EsUtil.Platform.Core.Geometry;

namespace Estable.Marquee.Decoration;

/// <summary>暗転マスクの設定と描画状態を 1 つにまとめた不変の値。</summary>
/// <remarks>
/// 外観（<see cref="Appearance"/>）・表示ポリシー（<see cref="Mode"/> / <see cref="ShowFullOverlay"/> / <see cref="WindowTitle"/>）と<br/>
/// 描画状態（<see cref="LogicalSelectionRect"/> / <see cref="LogicalHighlightRect"/> / <see cref="HasSelection"/> / <see cref="HasHighlight"/>）を<br/>
/// 1 つの値として所有する。UI 層は本値を 1 プロパティとして公開するだけであり、項目ごとの既定値・描画可否の判断を持たない。<br/>
/// <br/>
/// <b>環境値</b>（DPI スケーリング・座標変換ポート）と<b>ホスト結線</b>（サイズラベルの文言プロバイダ・ロガー）は本値に含めない<br/>
/// （DPI・ホストの状態から決まる値のため、ホストが個別に与える）。<br/>
/// <br/>
/// <b>選択矩形の扱い</b>: <see cref="LogicalSelectionRect"/> はドメイン座標（元画像ピクセル / 仮想スクリーンピクセル）で与える。<br/>
/// 論理座標 → DIP の変換は描画時（座標変換ポート）に行うため、レイアウト変化に自動で追従する。<br/>
/// <br/>
/// <b>可視性</b>: 本値は <see cref="MarqueeFrame"/>（同一アセンブリ）が組み立てるため、本値を公開しない<br/>
/// （選択矩形コントロールの公開設定は <c>DragRectOptions.Outer*</c> が担う）。
/// </remarks>
internal sealed record OuterDecoOptions
{
    /// <summary>既定の設定と状態を取得する。</summary>
    /// <value>外観はコアの既定値（黒 50% の暗転・ダークグレーの枠線・白のラベル）、モードは <see cref="OuterDecoRenderMode.AreaSelect"/>、選択・ハイライトなし。</value>
    /// <remarks>既定値は各コア型が単一所有する（UI 層・ホストに既定値の写しを置かない）。</remarks>
    public static OuterDecoOptions Default { get; } = new();

    /// <summary>暗転マスクの外観（暗転色・枠線・サイズラベル・サブライン・情報ラベル）を取得する。</summary>
    /// <value>外観の単一値。既定は <see cref="OuterDecoAppearance"/> の既定値。</value>
    public OuterDecoAppearance Appearance { get; init; } = new();

    /// <summary>オーバーレイの描画モードを取得する。</summary>
    /// <value>描画モード。既定は <see cref="OuterDecoRenderMode.AreaSelect"/>。</value>
    /// <remarks>操作ラベルの文言選定と情報ラベルの表示可否に使用する（判断はコアが単一所有する）。</remarks>
    public OuterDecoRenderMode Mode { get; init; } = OuterDecoRenderMode.AreaSelect;

    /// <summary>情報ラベル（3 行目）の文言を取得する。</summary>
    /// <value>ハイライト対象のウィンドウタイトル。未検出時は空文字（既定）。</value>
    public string WindowTitle { get; init; } = "";

    /// <summary>穴あけ矩形が無くても全面を暗転するかどうかを取得する。</summary>
    /// <value>true の場合は選択が無くても暗転する（範囲選択のドラッグ前・切出しの初期状態）。既定は false。</value>
    public bool ShowFullOverlay { get; init; }

    /// <summary>選択矩形（ドメイン座標）を取得する。</summary>
    /// <value>選択矩形。null（既定）の場合は選択なし。</value>
    /// <remarks><see cref="HasSelection"/> が true のときに穴あけ矩形として使う（ハイライトより優先度が低い）。</remarks>
    public EsRect? LogicalSelectionRect { get; init; }

    /// <summary>ハイライト矩形（ドメイン座標）を取得する。</summary>
    /// <value>ハイライト矩形。null（既定）の場合はハイライトなし。</value>
    /// <remarks><see cref="HasHighlight"/> が true のときに穴あけ矩形として使う（選択矩形より優先）。</remarks>
    public EsRect? LogicalHighlightRect { get; init; }

    /// <summary>選択が有効かどうかを取得する。</summary>
    /// <value>true の場合は <see cref="LogicalSelectionRect"/> を穴あけ矩形として使う。既定は false。</value>
    public bool HasSelection { get; init; }

    /// <summary>ハイライトが有効かどうかを取得する。</summary>
    /// <value>true の場合は <see cref="LogicalHighlightRect"/> を穴あけ矩形として使う（選択矩形より優先）。既定は false。</value>
    public bool HasHighlight { get; init; }
}
