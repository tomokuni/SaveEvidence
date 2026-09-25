using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.LoupeCore;

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
/// <b>安全域への補正</b>: 数値項目の補正は本型の生成時に適用する（<c>init</c> アクセサ）。<br/>
/// 値の設定経路（オブジェクト初期化子・<c>with</c> 式・逆シリアル化）によらず、範囲は<br/>
/// <see cref="LoupeValueCoercer"/> が単一所有する安全域内の値だけが保持される<br/>
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
    /// （クランプの規則は <see cref="EsSizeMath.ClampToArea"/> が単一所有する）。
    /// </remarks>
    public int RequestedSize
    {
        get => field;
        init => field = LoupeValueCoercer.CoerceRequestedSize(value);
    } = LoupeValueCoercer.RequestedSizeDefault;

    /// <summary>拡大率を取得する。</summary>
    /// <value>拡大率（偶数推奨）。既定は 4。設定時は 1〜64 へ補正される。</value>
    public int ZoomLevel
    {
        get => field;
        init => field = LoupeValueCoercer.CoerceZoomLevel(value);
    } = LoupeValueCoercer.ZoomLevelDefault;

    /// <summary>ルーペの寸法（市松模様のタイル・外枠の太さ・中心座標ラベルの寸法）を取得する。</summary>
    /// <value>寸法の単一値。既定は <see cref="LoupeMetrics"/> の既定値。</value>
    /// <remarks>
    /// 寸法の値・既定値・数値項目の安全域への補正は <see cref="LoupeMetrics"/> が単一所有する<br/>
    /// （本型は寸法をそのまま保持する）。色は描画にだけ必要な設定のため本型に含めない（描画の設定が単一所有する）。
    /// </remarks>
    public LoupeMetrics Metrics { get; init; } = new();
}
