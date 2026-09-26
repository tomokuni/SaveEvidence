namespace EsUtil.Feature.LoupeCore.Setting;

/// <summary>ルーペの寸法（市松模様のタイル・外枠の太さ）とラベルの表示可否をまとめた不変の設定値。</summary>
/// <remarks>
/// <b>本型が持つもの</b>: 計算に必要な寸法と、中心座標ラベルの表示可否である
/// （市松模様のタイルサイズと外枠の太さは描画の矩形計算に入るため、コアが単一所有する）。<br/>
/// <br/>
/// <b>本型が持たないもの（描画にだけ必要な設定）</b>: 色・フォントサイズ・角丸・余白は、<br/>
/// ラベルをどの体裁で描くかという描画の設定であり、描画の設定（<c>LoupePaint</c>）が単一所有する。<br/>
/// コアは描画の体裁を決めず、描画の可否の判定にも使わない。<br/>
/// <br/>
/// <b>状態は含めない</b>: 拡大元画像・拡大中心・拡大率・基本サイズ・スケーリング係数・表示オフセットは本型に含めない。<br/>
/// 中心座標ラベルの表示可否（設定値と中心の有無の組合せ）は <see cref="ShouldShowCoordLabel"/> が単一所有する。<br/>
/// <b>数値の安全域への補正</b>は本型の生成時に適用し、範囲は <c>Loupe.Limits</c> が単一所有する<br/>
/// （値の設定経路（オブジェクト初期化子・<c>with</c> 式・逆シリアル化）によらず、範囲内の値だけが保持される）。
/// </remarks>
public sealed record LoupeMetrics
{
    /// <summary>市松模様の 1 タイルサイズ（DIP）を取得する。</summary>
    /// <value>タイルサイズ（DIP）。既定は 6。設定時は 1〜128 へ補正される。</value>
    /// <remarks>安全域への補正は本型が単一所有する（設定の経路によらず範囲内の値だけが保持される）。</remarks>
    public int CheckerSize
    {
        get => field;
        init => field = Loupe.Limits.CoerceCheckerSize(value);
    } = Loupe.Limits.CheckerSizeDefault;

    /// <summary>外枠の太さ（DIP）を取得する。</summary>
    /// <value>枠線の太さ（DIP）。既定は 2。設定時は 1〜64 へ補正される。</value>
    /// <remarks>安全域への補正は本型が単一所有する（設定の経路によらず範囲内の値だけが保持される）。</remarks>
    public int FrameWidth
    {
        get => field;
        init => field = Loupe.Limits.CoerceFrameWidth(value);
    } = Loupe.Limits.FrameWidthDefault;

    /// <summary>中心座標ラベルを表示するかどうかを取得する。</summary>
    /// <value>true の場合はルーペ上端中央へ中心座標を表示する。既定は true。</value>
    public bool ShowCoordLabel { get; init; } = true;

    /// <summary>中心座標ラベルを描画するかどうかを判定する。</summary>
    /// <param name="hasCenterPoint">拡大中心が設定されているかどうか。</param>
    /// <returns>描画する場合は true。</returns>
    /// <remarks>
    /// ラベルの文言は中心座標から決まるため、中心が未設定のときは描画しない。<br/>
    /// 表示可否の判断（設定値と中心の有無の組合せ）を本メソッドが単一所有し、UI 層は判定結果で描画するだけにする。
    /// </remarks>
    public bool ShouldShowCoordLabel(bool hasCenterPoint) => ShowCoordLabel && hasCenterPoint;
}
