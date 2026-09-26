using EsUtil.Platform.Core.Geometry;
using static EsUtil.Feature.LoupeCore.Loupe.Limits;

namespace EsUtil.Feature.LoupeCore.Setting;

/// <summary>ルーペの設定（表示サイズ・拡大率・外観）をまとめた不変の値オブジェクト。</summary>
/// <remarks>
/// 個別の依存関係プロパティとして公開していた設定を 1 つの値にまとめ、UI 層の公開 API を 1 プロパティへ縮小する。<br/>
/// 設定の既定値も本型が単一所有する（各 UI 実装は既定値を持たず、本型を UI のプロパティ型へ橋渡しするだけ）。<br/>
/// <br/>
/// <b>設定とデータの区別</b>: ホストが稀に変更する値（表示サイズ・拡大率・外観）だけを本型へ含める。<br/>
/// マウス移動ごとに変わるデータ（拡大元画像・拡大中心）は UI の個別プロパティのままとする<br/>
/// （1 つの値へまとめると、中心の更新のたびに外観を含む値全体を組み立て直すことになる）。<br/>
/// DPI スケーリング係数（環境値）と表示位置のオフセット（ホスト結線）も含めない。<br/>
/// <br/>
/// <b>既定値</b>: 既定値を持つのは <see cref="Default"/> と <see cref="LoupeOptions()"/> のみである。<br/>
/// <see cref="Default"/> を基点に部分変更する（<c>LoupeOptions.Default with { ZoomLevel = 8 }</c>）と、<br/>
/// 指定しない項目は既定値を保つ（プロパティ初期化子を持つため、部分指定でもゼロ値にはならない）。<br/>
/// <br/>
/// <b>等価性</b>: レコードの値等価性により、同値の設定を再代入しても UI 側の変更通知（再描画）は発生しない。<br/>
/// <br/>
/// <b>安全域への補正</b>: 数値項目の補正は本型の生成時に適用する（<c>init</c> アクセサ。安全域は <c>Loupe.Limits</c> が単一所有する）。<br/>
/// 値の設定経路（オブジェクト初期化子・<c>with</c> 式・逆シリアル化）によらず、範囲内の値だけが保持される<br/>
/// （UI 側に補正の登録を要求すると、登録を忘れた UI だけ安全域外の値を保持する）。
/// </remarks>
public sealed record LoupeOptions
{
    /// <summary>すべての項目を既定値にした設定。</summary>
    /// <value>既定の表示サイズ・拡大率・外観（各 UI のプロパティの初期値）。</value>
    /// <remarks>部分変更の基点として使用する（<c>Default with { ... }</c>）。</remarks>
    public static LoupeOptions Default { get; } = new();

    /// <summary>ルーペの基本サイズ（DIP。一辺）を取得する。</summary>
    /// <value>表示サイズ（DIP）。既定は 160。設定時は 1〜4096 へ補正される。</value>
    /// <remarks>
    /// 表示サイズは本値（基本サイズ）を UI 層が親領域・描画先へクランプして導出する<br/>
    /// （クランプの規則は <see cref="EsSize.ClampToArea(double)"/> が単一所有する）。
    /// </remarks>
    public int RequestedSize
    {
        get => field;
        init => field = CoerceRequestedSize(value);
    } = RequestedSizeDefault;

    /// <summary>拡大率を取得する。</summary>
    /// <value>拡大率（偶数推奨）。既定は 4。設定時は 1〜64 へ補正される。</value>
    public int ZoomLevel
    {
        get => field;
        init => field = CoerceZoomLevel(value);
    } = ZoomLevelDefault;

    /// <summary>市松模様の 1 タイルサイズ（DIP）を取得する。</summary>
    /// <value>タイルサイズ（DIP）。既定は 6。設定時は 1〜128 へ補正される。</value>
    /// <remarks>安全域への補正は本型が単一所有する（設定の経路によらず範囲内の値だけが保持される）。</remarks>
    public int CheckerSize
    {
        get => field;
        init => field = CoerceCheckerSize(value);
    } = CheckerSizeDefault;

    /// <summary>外枠の太さ（DIP）を取得する。</summary>
    /// <value>枠線の太さ（DIP）。既定は 2。設定時は 1〜64 へ補正される。</value>
    /// <remarks>安全域への補正は本型が単一所有する（設定の経路によらず範囲内の値だけが保持される）。</remarks>
    public int FrameWidth
    {
        get => field;
        init => field = CoerceFrameWidth(value);
    } = FrameWidthDefault;

    /// <summary>中心座標ラベルを表示するかどうかを取得する。</summary>
    /// <value>true の場合はルーペ上端中央へ中心座標を表示する。既定は true。</value>
    public bool ShowCoordLabel { get; init; } = true;

    /// <summary>市松模様を表示するかどうかを取得する。</summary>
    /// <value>true（既定）の場合は画像の外側（透明領域）へ市松模様を描く。false の場合は描かない。</value>
    /// <remarks>描画の可否を本値が単一所有する（色のアルファを「描くかどうか」の代用にしない）。</remarks>
    public bool ShowCheckerboard { get; init; } = true;

    /// <summary>十字線を表示するかどうかを取得する。</summary>
    /// <value>true（既定）の場合は切り出し領域の中央へ十字線を描く。false の場合は描かない。</value>
    /// <remarks>描画の可否を本値が単一所有する（色のアルファを「描くかどうか」の代用にしない）。</remarks>
    public bool ShowCrosshair { get; init; } = true;

    /// <summary>外枠を表示するかどうかを取得する。</summary>
    /// <value>true（既定）の場合はルーペの四辺へ外枠を描く。false の場合は描かない。</value>
    /// <remarks>描画の可否を本値が単一所有する（色のアルファを「描くかどうか」の代用にしない）。</remarks>
    public bool ShowFrame { get; init; } = true;

    /// <summary>中心座標ラベルを描画するかどうかを判定する。</summary>
    /// <param name="hasCenterPoint">拡大中心が設定されているかどうか。</param>
    /// <returns>描画する場合は true。</returns>
    /// <remarks>
    /// ラベルの文言は中心座標から決まるため、中心が未設定のときは描画しない。<br/>
    /// 表示可否の判断（設定値と中心の有無の組合せ）を本メソッドが単一所有し、UI 層は判定結果で描画するだけにする。
    /// </remarks>
    public bool ShouldShowCoordLabel(bool hasCenterPoint) => ShowCoordLabel && hasCenterPoint;
}
