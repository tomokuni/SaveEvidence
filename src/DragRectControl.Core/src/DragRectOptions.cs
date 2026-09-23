using EsUtil.Platform.Core.Geometry;
using DrawingColor = System.Drawing.Color;
using Estable.DragRect.Decoration;

namespace Estable.DragRect;

/// <summary>ドラッグ矩形コントロールの設定（外観・対話パラメータ）をまとめた不変の値オブジェクト。</summary>
/// <remarks>
/// 個別の依存関係プロパティとして公開していた設定を 1 つの値にまとめ、UI 層の公開 API を 1 プロパティへ縮小する。<br/>
/// 設定の既定値も本型が単一所有する（各 UI 実装は既定値を持たず、本型を UI の色型・プロパティ型へ橋渡しするだけ）。<br/>
/// <br/>
/// 設定は 3 つの性格に分かれる:<br/>
/// - <b>外観</b>: ハンドル・十字マーカー・座標ラベルの色とサイズ（描画のみに使用する）。<br/>
/// - <b>対話パラメータ</b>: 最小選択サイズ・ハンドルのヒットテスト半径・境界外選択の禁止（入力の解釈に使用し、セッションへ伝搬する）。<br/>
/// - <b>外側の装飾（<c>Outer</c> 接頭辞）</b>: 選択矩形の外側に描く暗転マスク・穴あけ枠・ラベルと、その表示ポリシー・ハイライト状態。<br/>
/// <br/>
/// <b>外側の装飾は選択矩形の状態を持たない</b>: 選択矩形と選択の有無はセッション（選択の真のソース）から導出するため、<br/>
/// 本値には持たせない（<see cref="OuterHighlightRect"/> はホストが与えるハイライトのみ）。<br/>
/// <b>枠線の色・太さは <see cref="OuterAppearance"/> が単一所有する</b>（選択枠線と暗転マスクの穴あけ枠は同一矩形に描かれるため、<br/>
/// 2 つの値に分けると見た目が崩れる）。
/// <br/>
/// <b>既定値</b>: 既定値を持つのは <see cref="Default"/> と <see cref="DragRectOptions()"/> のみである。<br/>
/// <see cref="Default"/> を基点に部分変更する（<c>DragRectOptions.Default with { HandleColor = ... }</c>）と、<br/>
/// 指定しない項目は既定値を保つ（プロパティ初期化子を持つため、部分指定でもゼロ値にはならない）。<br/>
/// <br/>
/// <b>等価性</b>: レコードの値等価性により、同値の設定を再代入しても UI 側の変更通知（再描画）は発生しない。<br/>
/// <br/>
/// <b>色の型</b>: UI 非依存の <see cref="DrawingColor"/>（アルファで不透明度を表す）を使用する。<br/>
/// 各 UI の色型への変換は境界（UI 層）で 1 行行う。
/// </remarks>
public sealed record DragRectOptions
{
    /// <summary>すべての項目を既定値にした設定。</summary>
    /// <value>既定の外観・対話パラメータ（各 UI のプロパティの初期値）。</value>
    /// <remarks>部分変更の基点として使用する（<c>Default with { ... }</c>）。</remarks>
    public static DragRectOptions Default { get; } = new();

    // ----------------------------------------------------------------
    // 外側の装飾（選択矩形の外側に描く暗転マスク・穴あけ枠・ラベル）
    // ----------------------------------------------------------------

    /// <summary>外側の装飾の外観。</summary>
    /// <value>暗転色・穴あけ枠の色と太さ・3 種のラベルの色とフォントサイズ。既定は枠線のみ（暗転とラベルを行わない）。</value>
    /// <remarks>
    /// 穴あけ枠は選択矩形の内側に同じ矩形で描かれるため、<b>枠線の色と太さは本値が単一所有する</b>（選択枠線と暗転マスクの枠を別々に持つと重なりの見た目が崩れる）。<br/>
    /// アルファが 0 の枠線色を指定した場合は枠線を描かない（描画の要否は暗転マスクコアのフレーム解決が判断する）。<br/>
    /// <b>既定で暗転しない</b>のは、本コントロールが汎用の選択矩形コントロールだからである（暗転・ラベルは用途依存のため、<br/>
    /// 領域選択・切出しのホストが設定値から明示的に与える）。
    /// </remarks>
    public OuterDecoAppearance OuterAppearance { get; init; } = CreateDefaultOuterAppearance();

    /// <summary>外側の装飾の描画モード。</summary>
    /// <value>モード（操作ラベルの文言選定に使用する）。既定は <see cref="OuterDecoRenderMode.AreaSelect"/>。</value>
    public OuterDecoRenderMode OuterRenderMode { get; init; } = OuterDecoRenderMode.AreaSelect;

    /// <summary>外側の装飾の操作ラベルに表示するウィンドウタイトル。</summary>
    /// <value>タイトル（空文字（既定）の場合は操作ラベルへタイトル行を出さない）。</value>
    public string OuterWindowTitle { get; init; } = "";

    /// <summary>外側の装飾で画面全体を暗転するかどうか。</summary>
    /// <value>true の場合は穴あけを行わず画面全体を暗転色で塗る。既定は false。</value>
    /// <remarks>ハイライトのみを表示するモード（全画面の暗転 + ハイライト矩形）で使用する。</remarks>
    public bool OuterShowFullOverlay { get; init; }

    /// <summary>外側の装飾でハイライトする矩形（ドメイン座標）。</summary>
    /// <value>ハイライト矩形（null（既定）の場合はハイライトしない）。</value>
    /// <remarks>ホストが与える値である（選択矩形と異なり本コントロールの操作では変化しない）。</remarks>
    public EsRect? OuterHighlightRect { get; init; }

    /// <summary>外側の装飾でハイライトを描くかどうか。</summary>
    /// <value>true の場合に <see cref="OuterHighlightRect"/> をハイライトする。既定は false。</value>
    public bool OuterHasHighlight { get; init; }

    // ----------------------------------------------------------------
    // 外観（描画のみに使用する）
    // ----------------------------------------------------------------

    /// <summary>ドラッグハンドルを表示するかどうか。</summary>
    /// <value>true（既定）の場合は選択中にハンドルを描く。false の場合は枠線と外側の装飾のみを描く。</value>
    /// <remarks>本値が制御するのはハンドルの表示のみである（外側の装飾と枠線は本値に関係なく描画する）。</remarks>
    public bool ShowHandles { get; init; } = true;

    /// <summary>ドラッグハンドルの塗り色。</summary>
    /// <value>ハンドルの塗り色（不透明度は色のアルファに統合）。既定は白。</value>
    /// <remarks>透明（<see cref="DrawingColor.Empty"/>）を指定した場合は描画側が白へフォールバックする。</remarks>
    public DrawingColor HandleColor { get; init; } = DrawingColor.White;

    /// <summary>ドラッグハンドルの一辺サイズ。</summary>
    /// <value>ハンドルの一辺サイズ（DIP）。既定は 10.0。</value>
    public double HandleSize { get; init; } = 10.0;

    /// <summary>十字マーカーの色。</summary>
    /// <value>十字マーカーの色（不透明度は色のアルファに統合）。既定は白。</value>
    public DrawingColor MarkerColor { get; init; } = DrawingColor.White;

    /// <summary>十字マーカーの全長。</summary>
    /// <value>十字マーカーの全長（DIP）。中心から両端までの半幅はこの 1/2。既定は 16.0。</value>
    public double MarkerLength { get; init; } = 16.0;

    /// <summary>カーソル座標ラベルを表示するかどうか。</summary>
    /// <value>true の場合はマウス位置のドメイン座標をカーソルの右側に表示する。既定は false。</value>
    public bool ShowCoordLabel { get; init; }

    /// <summary>カーソル座標ラベルの文字色。</summary>
    /// <value>ラベルの文字色（不透明度は色のアルファに統合）。既定は白。</value>
    public DrawingColor CoordLabelColor { get; init; } = DrawingColor.White;

    /// <summary>カーソル座標ラベルの背景色。</summary>
    /// <value>ラベルの背景色（不透明度は色のアルファに統合）。既定は黒 60%（<c>#99000000</c>）。</value>
    public DrawingColor CoordLabelBackgroundColor { get; init; } = DrawingColor.FromArgb(153, 0, 0, 0);

    /// <summary>カーソル座標ラベルのフォントサイズ。</summary>
    /// /// <value>フォントサイズ（DIP）。6〜48 の範囲へ補正される。既定は 12.0。</value>
    /// <remarks>非数（NaN / ∞）を指定した場合は既定値へ補正する（補正は本型が単一所有する）。</remarks>
    public double CoordLabelFontSize
    {
        get => field;
        init => field = DragRectAppearance.CoerceCoordLabelFontSize(value);
    } = DragRectAppearance.CoordLabelFontSizeDefault;

    // ----------------------------------------------------------------
    // 対話パラメータ（入力の解釈に使用し、セッションへ伝搬する）
    // ----------------------------------------------------------------

    /// <summary>ドラッグハンドルのヒットテスト半径。</summary>
    /// <value>ヒットテスト半径（DIP）。既定は 12.0。</value>
    /// <remarks>
    /// DIP 座標での半径であるため、ズーム・縮小表示を変えてもヒットテストの画面上の大きさが一定になる。<br/>
    /// ドメイン座標系への変換はセッションが行う。
    /// </remarks>
    public double HandleHitRadius { get; init; } = 12.0;

    /// <summary>選択矩形の最小サイズ。</summary>
    /// <value>最小サイズ（ドメイン座標）。既定は 6.0。</value>
    /// <remarks>ドラッグ完了時にこれ未満の矩形はキャンセル扱いになる（判断はセッションが行う）。</remarks>
    public double MinSelectionSize { get; init; } = 6.0;

    /// <summary>ドメイン境界外の選択を禁止するかどうか。</summary>
    /// <value>true（既定）の場合は選択をドメイン境界内に制限する。false の場合は境界外も選択できる。</value>
    /// <remarks>
    /// クリップ範囲は座標変換ポートの Bounds で指定する。<br/>
    /// true へ切り替えた場合は既存の選択矩形を即時に境界内へクリップする（セッションが判断する）。
    /// </remarks>
    public bool ClampToDomainBounds { get; init; } = true;

    /// <summary>既定の外側の装飾（枠線のみ）を生成する。</summary>
    /// <returns>暗転とラベルを行わず、枠線だけを描く外観。</returns>
    /// <remarks>
    /// 枠線の色は本コントロールの従来の既定（DarkGray・太さ 2.0）を保つ（選択矩形の可視性に必要な最小の外観）。<br/>
    /// 暗転・サイズラベル・サブライン・情報ラベルはモードやホストの用途に依存するため、既定では描かない。
    /// </remarks>
    private static OuterDecoAppearance CreateDefaultOuterAppearance() => new()
    {
        OverlayColor = DrawingColor.Transparent,
        BorderColor = DrawingColor.FromArgb(0xFF, 169, 169, 169),
        BorderThickness = 2.0,
        ShowSizeLabel = false,
        ShowSubLines = false,
        InfoLabelVisible = false,
    };
}
