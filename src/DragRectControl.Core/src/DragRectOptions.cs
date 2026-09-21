using DrawingColor = System.Drawing.Color;

namespace Estable.DragRect;

/// <summary>ドラッグ矩形コントロールの設定（外観・対話パラメータ）をまとめた不変の値オブジェクト。</summary>
/// <remarks>
/// 個別の依存関係プロパティとして公開していた設定を 1 つの値にまとめ、UI 層の公開 API を 1 プロパティへ縮小する。<br/>
/// 設定の既定値も本型が単一所有する（各 UI 実装は既定値を持たず、本型を UI の色型・プロパティ型へ橋渡しするだけ）。<br/>
/// <br/>
/// 設定は 2 つの性格に分かれる:<br/>
/// - <b>外観</b>: 枠線・ハンドル・十字マーカー・座標ラベルの色とサイズ（描画のみに使用する）。<br/>
/// - <b>対話パラメータ</b>: 最小選択サイズ・ハンドルのヒットテスト半径・境界外選択の禁止（入力の解釈に使用し、セッションへ伝搬する）。<br/>
/// <br/>
/// <b>既定値</b>: 既定値を持つのは <see cref="Default"/> と <see cref="DragRectOptions()"/> のみである。<br/>
/// <see cref="Default"/> を基点に部分変更する（<c>DragRectOptions.Default with { BorderThickness = 3.0 }</c>）と、<br/>
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
    // 外観（描画のみに使用する）
    // ----------------------------------------------------------------

    /// <summary>選択枠線の色。</summary>
    /// <value>枠線の色（不透明度は色のアルファに統合）。既定は <c>#FFA9A9A9</c>（DarkGray）。</value>
    public DrawingColor BorderColor { get; init; } = DrawingColor.FromArgb(0xFF, 169, 169, 169);

    /// <summary>選択枠線の太さ。</summary>
    /// <value>枠線の太さ（DIP）。既定は 2.0。</value>
    public double BorderThickness { get; init; } = 2.0;

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
    /// <value>フォントサイズ（DIP）。6〜48 の範囲へ補正される。既定は 12.0。</value>
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
}
