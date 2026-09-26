using EsUtil.SaveEvidence.Common.Imaging;


namespace EsUtil.Feature.MarqueeCore.Settings;

/// <summary>選択矩形コントロールの設定（外観・対話パラメータ）をまとめた不変の値オブジェクト。</summary>
/// <remarks>
/// 個別の依存関係プロパティとして公開していた設定を 1 つの値にまとめ、UI 層の公開 API を 1 プロパティへ縮小する。<br/>
/// 設定の既定値も本型が単一所有する（各 UI 実装は既定値を持たず、本型を UI の色型・プロパティ型へ橋渡しするだけ）。<br/>
/// <b>ラベルの体裁</b>（内側余白・背景・行送り・配置）は <see cref="EsLabelLayout"/> が単一所有する<br/>
/// （ホストが描く付加情報ラベルと同じ体裁を使うため、余白の値は本型ではなく同クラスを参照する）。<br/>
/// 本型が公開するのはコントロール固有の値（カーソルとラベルの距離等）のみである。<br/>
/// <br/>
/// 設定は 2 つの性格に分かれる:<br/>
/// - <b>外観</b>: ハンドル・十字マーカー・座標ラベルの色とサイズ（描画のみに使用する）。<br/>
/// - <b>対話パラメータ</b>: 最小選択サイズ・ハンドルのヒットテスト半径・境界外選択の禁止（入力の解釈に使用し、セッションへ伝搬する）。<br/>
/// <br/>
/// <b>装飾（暗転マスク・穴あけ枠）は本値に含まない</b>: 装飾は選択矩形コントロール単体の概念ではなく、<br/>
/// コントロールを撮影オーバーレイ等へ組み込むときにだけ必要になる表示のため、<br/>
/// 同じアセンブリの別フォルダ（<c>Decoration</c> の <c>MarqueeDecoOptions</c>）が単一所有する。<br/>
/// <br/>
/// <b>既定値</b>: 既定値を持つのは <see cref="Default"/> と <see cref="MarqueeOptions()"/> のみである。<br/>
/// <see cref="Default"/> を基点に部分変更する（<c>MarqueeOptions.Default with { HandleSize = ... }</c>）と、<br/>
/// 指定しない項目は既定値を保つ（プロパティ初期化子を持つため、部分指定でもゼロ値にはならない）。<br/>
/// <br/>
/// <b>等価性</b>: レコードの値等価性により、同値の設定を再代入しても UI 側の変更通知（再描画）は発生しない。<br/>
/// <br/>
/// <b>色は含まない</b>: 色は<b>描画にだけ必要な設定</b>であり、描画の設定（<c>MarqueePaint</c>。UIBase が単一所有する）が持つ。<br/>
/// 本型は寸法・表示可否・対話パラメータ（計算に必要な値）だけを持つ。
/// </remarks>
public sealed record MarqueeOptions
{
    /// <summary>すべての項目を既定値にした設定。</summary>
    /// <value>既定の寸法・表示可否・対話パラメータ（各 UI のプロパティの初期値）。</value>
    /// <remarks>部分変更の基点として使用する（<c>Default with { ... }</c>）。</remarks>
    public static MarqueeOptions Default { get; } = new();

    // ----------------------------------------------------------------
    // 外観（描画のみに使用する）
    // ----------------------------------------------------------------

    /// <summary>ドラッグハンドルを表示するかどうか。</summary>
    /// <value>true（既定）の場合は選択中にハンドルを描く。false の場合は枠線と十字マーカーのみを描く。</value>
    public bool ShowHandles { get; init; } = true;

    /// <summary>ドラッグハンドルの一辺サイズ。</summary>
    /// <value>ハンドルの一辺サイズ（DIP）。既定は 10.0。</value>
    public double HandleSize { get; init; } = 10.0;

    /// <summary>ドラッグハンドルの枠線の太さ（DIP）。</summary>
    /// <value>枠線の太さ。既定は 1.5。</value>
    /// <remarks>塗りの中に隠れない細さでありながら、拡大表示でも輪郭が判別できる太さである。</remarks>
    public const double HandleBorderThickness = 1.5;

    /// <summary>十字マーカーの全長。</summary>
    /// <value>十字マーカーの全長（DIP）。中心から両端までの半幅はこの 1/2。既定は 16.0。</value>
    public double MarkerLength { get; init; } = 16.0;

    /// <summary>カーソル座標ラベルを表示するかどうか。</summary>
    /// <value>true の場合はマウス位置のドメイン座標をカーソルの右側に表示する。既定は false。</value>
    public bool ShowCoordLabel { get; init; }

    /// <summary>カーソル座標ラベルのフォントサイズ。</summary>
    /// <value>フォントサイズ（DIP）。6〜48 の範囲へ補正される。既定は 12.0。</value>
    /// <remarks>非数（NaN / ∞）を指定した場合は既定値へ補正する（補正は本型が単一所有する）。</remarks>
    public double CoordLabelFontSize
    {
        get => field;
        init => field = EsLabelMetrics.CoerceFontSize(value, CoordLabelFontSizeDefault);
    } = CoordLabelFontSizeDefault;

    /// <summary>カーソル座標ラベルのフォントサイズの既定値。</summary>
    /// <value>既定のフォントサイズ（DIP）。</value>
    public const double CoordLabelFontSizeDefault = EsLabelDefaults.FontSize;

    /// <summary>カーソル座標ラベルをカーソルから離すオフセット（DIP）。</summary>
    /// <value>カーソルの右下へ離す距離。既定は 8.0。</value>
    /// <remarks>ラベルがカーソルに重ならないための余白である（配置の計算は <see cref="EsLabelLayout"/> が単一所有する）。</remarks>
    public const double CoordLabelOffset = 8.0;

    /// <summary>サイズラベルを表示するかどうか。</summary>
    /// <value>true（既定）の場合は穴あけ矩形の左上に位置と寸法を表示する。false の場合は描かない。</value>
    /// <remarks>他のラベル（操作・情報）はホストが自前で描くため、本設定には含まれない。</remarks>
    public bool ShowSizeLabel { get; init; } = true;

    /// <summary>サイズラベルのフォントサイズ。</summary>
    /// <value>フォントサイズ（DIP）。6〜48 の範囲へ補正される。既定は 14.0。</value>
    /// <remarks>非数（NaN / ∞）を指定した場合は既定値へ補正する（補正は本型が単一所有する）。</remarks>
    public double SizeLabelFontSize
    {
        get => field;
        init => field = EsLabelMetrics.CoerceFontSize(value, SizeLabelFontSizeDefault);
    } = SizeLabelFontSizeDefault;

    /// <summary>サイズラベルのフォントサイズの既定値。</summary>
    /// <value>既定のフォントサイズ（DIP）。</value>
    public const double SizeLabelFontSizeDefault = 14.0;

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
