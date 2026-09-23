using DrawingColor = System.Drawing.Color;

namespace Estable.Loupe;

/// <summary>ルーペの外観（下地・市松模様・外枠・十字線・中心座標ラベル）をまとめた不変の設定値。</summary>
/// <remarks>
/// 描画に使う外観 12 項目を 1 つの値として所有する（Procedure: Flyweight 的な値オブジェクト）。<br/>
/// 各 UI 実装は本型を 1 つ公開するだけで外観を設定でき、UI ごとに既定値がずれない。<br/>
/// 下地（<see cref="BackgroundColor"/>）も本型が所有するため、キャンバスへ直接描画する UI と<br/>
/// ウィンドウの上へ重ねる UI のどちらも同じ契約でベース色を指定できる（UI 層に固有のプロパティを置かない）。<br/>
/// <br/>
/// 状態（拡大元画像・拡大中心・拡大率・基本サイズ・スケーリング係数・表示オフセット）は本型に含めない。<br/>
/// 描画の判断（中心座標ラベルの表示可否）は <see cref="ShouldShowCoordLabel"/> が単一所有する。<br/>
/// 数値の安全域への補正は<b>本型の生成時</b>に適用し、範囲は <c>LoupeValueCoercer</c> が単一所有する<br/>
/// （値の設定経路（オブジェクト初期化子・<c>with</c> 式・逆シリアル化）によらず、範囲内の値だけが保持される）。<br/>
/// 色は UI 非依存の <see cref="DrawingColor"/> を使用し、各 UI の色型への変換は境界で行う。
/// </remarks>
public sealed record LoupeAppearance
{
    // ====================================================================
    // 下地
    // ====================================================================

    /// <summary>ルーペの下地（ベース）色を取得する。</summary>
    /// <value>アルファを含む色。既定は透明（背後の要素を透かす）。</value>
    /// <remarks>
    /// 市松模様・拡大画像より先に全面へ塗る色。<br/>
    /// 市松模様の色（<see cref="CheckerColorA"/> / <see cref="CheckerColorB"/>）は半透明のため、<br/>
    /// 背後の要素を透かしたくない場合はホストが不透明色を設定する。
    /// </remarks>
    public DrawingColor BackgroundColor { get; init; } = DrawingColor.Transparent;

    /// <summary>下地を塗るかどうかを取得する。</summary>
    /// <value>下地のアルファが 0 でない場合は true。false の場合は塗りを省略できる。</value>
    /// <remarks>アルファ 0 を描画しない扱いにする判断は本型が単一所有する（UI 層に持たせない）。</remarks>
    public bool HasBackground => BackgroundColor.A > 0;

    // ====================================================================
    // 市松模様
    // ====================================================================

    /// <summary>市松模様の 1 タイルサイズ（DIP）を取得する。</summary>
    /// <value>タイルサイズ（DIP）。既定は 6。設定時は 1〜128 へ補正される。</value>
    /// <remarks>安全域への補正は本型が単一所有する（設定の経路によらず範囲内の値だけが保持される）。</remarks>
    public int CheckerSize
    {
        get => field;
        init => field = LoupeValueCoercer.CoerceCheckerSize(value);
    } = LoupeValueCoercer.CheckerSizeDefault;

    /// <summary>市松模様の色A（明色）を取得する。</summary>
    /// <value>アルファを含む色。既定は Gray 30%（#4C808080）。</value>
    public DrawingColor CheckerColorA { get; init; } = DrawingColor.FromArgb(0x4C, 0x80, 0x80, 0x80);

    /// <summary>市松模様の色B（暗色）を取得する。</summary>
    /// <value>アルファを含む色。既定は DarkGray 30%（#4CA9A9A9）。</value>
    public DrawingColor CheckerColorB { get; init; } = DrawingColor.FromArgb(0x4C, 0xA9, 0xA9, 0xA9);

    // ====================================================================
    // 外枠
    // ====================================================================

    /// <summary>外枠の色を取得する。</summary>
    /// <value>アルファを含む色。既定は LightGray（#FFD3D3D3）。</value>
    public DrawingColor FrameColor { get; init; } = DrawingColor.FromArgb(255, 0xD3, 0xD3, 0xD3);

    /// <summary>外枠の太さ（DIP）を取得する。</summary>
    /// <value>枠線の太さ（DIP）。既定は 2。設定時は 1〜64 へ補正される。</value>
    /// <remarks>安全域への補正は本型が単一所有する（設定の経路によらず範囲内の値だけが保持される）。</remarks>
    public int FrameWidth
    {
        get => field;
        init => field = LoupeValueCoercer.CoerceFrameWidth(value);
    } = LoupeValueCoercer.FrameWidthDefault;

    // ====================================================================
    // 十字線
    // ====================================================================

    /// <summary>十字線の色を取得する。</summary>
    /// <value>アルファを含む色。既定は赤 40%（#66FF0000）。</value>
    public DrawingColor CrossColor { get; init; } = DrawingColor.FromArgb(102, 255, 0, 0);

    /// <summary>十字線を描画するかどうかを取得する。</summary>
    /// <value>十字線の色のアルファが 0 でない場合は true。</value>
    /// <remarks>アルファ 0 を描画しない扱いにする判断は本型が単一所有する（UI 層に持たせない）。</remarks>
    public bool HasCrosshair => CrossColor.A > 0;

    /// <summary>外枠を描画するかどうかを取得する。</summary>
    /// <value>外枠の色のアルファが 0 でない場合は true。</value>
    public bool HasFrame => FrameColor.A > 0;

    /// <summary>市松模様の明色で下地を塗るかどうかを取得する。</summary>
    /// <value>色Aのアルファが 0 でない場合は true。false の場合は塗りを省略できる。</value>
    public bool HasCheckerColorA => CheckerColorA.A > 0;

    /// <summary>市松模様の暗色タイルを描画するかどうかを取得する。</summary>
    /// <value>色Bのアルファが 0 でない場合は true。</value>
    public bool HasCheckerColorB => CheckerColorB.A > 0;

    // ====================================================================
    // 中心座標ラベル
    // ====================================================================

    /// <summary>中心座標ラベルを表示するかどうかを取得する。</summary>
    /// <value>true の場合はルーペ上端中央へ中心座標を表示する。既定は true。</value>
    public bool ShowCoordLabel { get; init; } = true;

    /// <summary>中心座標ラベルの文字色を取得する。</summary>
    /// <value>アルファを含む色。既定は白。</value>
    public DrawingColor CoordLabelColor { get; init; } = DrawingColor.White;

    /// <summary>中心座標ラベルの背景色を取得する。</summary>
    /// <value>アルファを含む色。既定は黒 60%（#99000000）。</value>
    public DrawingColor CoordLabelBackgroundColor { get; init; } = DrawingColor.FromArgb(153, 0, 0, 0);

    /// <summary>中心座標ラベルのフォントサイズ（DIP）を取得する。</summary>
    /// <value>フォントサイズ（DIP）。既定は 12.0。設定時は 6〜48 へ補正される（非有限値は既定値）。</value>
    /// <remarks>安全域への補正（非有限値を含む）は本型が単一所有する（設定の経路によらず範囲内の値だけが保持される）。</remarks>
    public double CoordLabelFontSize
    {
        get => field;
        init => field = LoupeValueCoercer.CoerceCoordLabelFontSize(value);
    } = LoupeValueCoercer.CoordLabelFontSizeDefault;

    /// <summary>中心座標ラベルの背景の角丸半径（DIP）を取得する。</summary>
    /// <value>角丸半径（DIP）。既定は 2。設定時は 0〜64 へ補正される（0 は角丸なし）。</value>
    /// <remarks>安全域への補正は本型が単一所有する（設定の経路によらず範囲内の値だけが保持される）。</remarks>
    public int CoordLabelCornerRadius
    {
        get => field;
        init => field = LoupeValueCoercer.CoerceCoordLabelCornerRadius(value);
    } = LoupeValueCoercer.CoordLabelCornerRadiusDefault;

    /// <summary>中心座標ラベルを描画するかどうかを判定する。</summary>
    /// <param name="hasCenterPoint">拡大中心が設定されているかどうか。</param>
    /// <returns>描画する場合は true。</returns>
    /// <remarks>
    /// ラベルの文言は中心座標から決まるため、中心が未設定のときは描画しない。<br/>
    /// 表示可否の判断（設定値と中心の有無の組合せ）を本メソッドが単一所有し、UI 層は判定結果で描画するだけにする。
    /// </remarks>
    public bool ShouldShowCoordLabel(bool hasCenterPoint) => ShowCoordLabel && hasCenterPoint;
}
