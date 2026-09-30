using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Microsoft.Extensions.Logging;
using EsUtil.Feature.Loupe.Core.Setting;
using EsUtil.Feature.Loupe.Core.Drawing;
using EsUtil.Feature.Loupe.Core.Logging;
using static EsUtil.Feature.Loupe.Core.Loupe.Limits;


namespace EsUtil.Feature.Loupe.Core;

/// <summary>Loupe（拡大鏡）の操作を UI 層・ホストへ提供するファサード。</summary>
/// <remarks>
/// <b>適用パターン</b>: Facade（表示レイアウトの解決・描画入力の生成・通知内容の解決・設定を 1 つの窓口へ集約する）。<br/>
/// <br/>
/// <b>責務</b>: ルーペの「状態（設定・環境値・拡大中心・表示位置のオフセット）」を保持し、<br/>
/// 「表示レイアウトの解決」「描画 1 フレーム分の幾何の解決」を提供する。<br/>
/// UI 層とホストは本クラスだけを参照する（コアの計算クラスを個別に呼び出さない）。<br/>
/// <br/>
/// <b>本クラスが単一所有するもの</b>:<br/>
/// - 表示サイズの決定（要求サイズを親領域の最小辺へクランプする規則と、その唯一の書き込み元）<br/>
/// - 表示位置の決定（拡大中心 ＋ オフセット ＋ 親領域内へのクランプ）<br/>
/// - 描画の幾何（<see cref="LoupeGeometry"/>）の解決（DIP → 物理ピクセルの換算と描画先へのクランプ）<br/>
/// <br/>
/// <b>描画は本コアの関心事ではない</b>: 本コアは描画 API も描画面も知らない<br/>
/// （<see cref="LoupeGeometry"/> という幾何のデータを返すだけである）。<br/>
/// 装飾（十字線・枠線）・文言・表示可否・描画順は描画の都合として描画層（本コアを参照する上位の層）が解決し、<br/>
/// 描画は UI 層が行う（描画 API の抽象化はしない）。<br/>
/// /// <br/>
/// <b>UI 層の責務</b>: 拡大元画像の解決（UI ごとに型が異なる）、描画の実行（ビットマップ合成・キャンバス描画）、<br/>
/// 再描画のバッチ化（UI のディスパッチャー）、公開プロパティ・公開イベントへの写像。<br/>
/// <br/>
/// <b>依存分離</b>: UI 非依存のコア型（<see cref="EsPoint"/> / <see cref="EsSize"/> / <see cref="EsBitmap"/>）と<br/>
/// 座標変換ポート（<see cref="ICoordTransform"/>）にのみ依存する。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で検証できる。
/// </remarks>
public sealed partial class Loupe
{
    /// <summary>外部から指定された座標変換ポート（null = 未指定。未指定時は等価変換として扱う）。</summary>
    private ICoordTransform? _coordPort;

    /// <summary>ルーペの設定（既定は <see cref="LoupeOptions.Default"/>）。</summary>
    private LoupeOptions _options = LoupeOptions.Default;

    /// <summary>表示 DIP を物理ピクセルへ変換するスケーリング係数（既定は 1.0）。</summary>
    private double _dipToDeviceScaling = DipToDeviceScalingDefault;

    /// <summary>拡大表示する中心座標（ドメイン座標。未設定は null）。</summary>
    private EsPoint? _centerPoint;

    /// <summary>拡大中心からの右下オフセット（DIP。既定は 0）。</summary>
    private double _positionOffset;

    /// <summary>ファサードを初期化する。</summary>
    /// <param name="options">初期設定。null 可（null の場合は <see cref="LoupeOptions.Default"/>）。</param>
    /// <remarks>初期値は本フィールドへ直接保持する（変更ログの経路を通さない。構築時点では出力先が未設定である）。</remarks>
    public Loupe(LoupeOptions? options = null)
    {
        _options = options ?? LoupeOptions.Default;
    }

    /// <summary>設定の変更ログの出力先を取得または設定する。</summary>
    /// <value>ロガー。null を設定した場合は出力なし（<see cref="LoggerFallback"/> が吸収する）。</value>
    /// <remarks>
    /// ログ基盤（ファイル出力・レベル制御）はホストが所有し、本コアは定義（<see cref="LoupeLogMessage"/>）だけを持つ。<br/>
    /// 設定・環境値・拡大中心の変更は本コアが検出して出力する（UI 層に同値判定・差分検出・変換を持たせない）。
    /// </remarks>
    public ILogger Log
    {
        get => field.OrNullLogger();
        set => field = value.OrNullLogger();
    }

    // ====================================================================
    // 設定・環境値（設定・DPI・拡大中心・表示位置のオフセット）
    // ====================================================================

    /// <summary>ルーペの設定（表示サイズ・拡大率）。</summary>
    /// <value>設定の単一値。既定は <see cref="LoupeOptions.Default"/>。</value>
    /// <remarks>
    /// 設定の値・既定値・数値項目の安全域への補正は <see cref="LoupeOptions"/> が単一所有する。<br/>
    /// 変更すると表示サイズ（要求サイズのクランプ結果）も変わるため、UI 層は <see cref="ResolveLayout"/> を呼び直す。<br/>
    /// 変更の検出（変化した項目だけの出力）は <see cref="LoupeLogMessage.LogOptionsChanged"/> が単一所有し、UI 層は前後の値を渡さない。
    /// </remarks>
    public LoupeOptions Options
    {
        get => _options;
        set
        {
            if (SetIfChanged(ref _options, value, out var previous))
            {
                Log.LogOptionsChanged(previous, value);
            }
        }
    }

    /// <summary>表示 DIP を物理ピクセルへ変換するスケーリング係数。</summary>
    /// <value>スケーリング係数（0.1〜8.0）。範囲外・非有限は補正済みの値を設定する。既定は 1.0。</value>
    /// <remarks>
    /// 環境値（DPI）のため <see cref="Options"/> には含めない。補正の規則は <see cref="Loupe.Limits"/> が単一所有し、<br/>
    /// どの設定経路（初期化・UI の依存関係プロパティ・直接代入）でも範囲内の値だけが保持される。<br/>
    /// 変更は本コアが検出して出力する（UI 層に同値判定を置かない）。
    /// </remarks>
    public double DipToDeviceScaling
    {
        get => _dipToDeviceScaling;
        set
        {
            if (SetIfChanged(ref _dipToDeviceScaling, CoerceDipToDeviceScaling(value), out var previous))
            {
                Log.LogSettingChanged(nameof(DipToDeviceScaling), previous, _dipToDeviceScaling);
            }
        }
    }

    /// <summary>拡大表示する中心座標（ドメイン座標）。</summary>
    /// <value>拡大中心（元画像ピクセル座標）。未設定は null（切り出し領域を決められないため、拡大画像を描かず配置もしない）。</value>
    /// <remarks>
    /// UI 層が設定する状態であり、解決した 1 フレーム分の写しは <see cref="LoupeGeometry.Center"/> が持つ。<br/>
    /// 変更は本コアが検出して出力する（マウス移動ごとに UI が設定しても、変化したときだけ記録される）。
    /// </remarks>
    public EsPoint? CenterPoint
    {
        get => _centerPoint;
        set
        {
            if (SetIfChanged(ref _centerPoint, value, out var previous))
            {
                Log.LogCenterPointChanged(previous, value);
            }
        }
    }

    /// <summary>拡大中心からの右下オフセット（DIP）。</summary>
    /// <value>カーソル位置（拡大中心）からの右下方向へのオフセット量（DIP）。既定は 0。</value>
    /// <remarks>
    /// 表示位置をカーソルから離して配置するためのホスト指定値である（位置のクランプはコアが行う）。<br/>
    /// 変更は本コアが検出して出力する（UI 層に同値判定を置かない）。
    /// </remarks>
    public double PositionOffset
    {
        get => _positionOffset;
        set
        {
            if (SetIfChanged(ref _positionOffset, value, out var previous))
            {
                Log.LogSettingChanged(nameof(PositionOffset), previous, _positionOffset);
            }
        }
    }

    /// <summary>ドメイン ↔ 親領域 DIP の座標変換ポートを設定する。</summary>
    /// <param name="port">座標変換ポート（Inverse = ドメイン → 親領域 DIP）。null の場合は等価変換。</param>
    /// <remarks>
    /// 変換関数は呼び出し毎に評価されるため、ウィンドウサイズ変更・ズーム・スクロール等のレイアウト変化に追従できる<br/>
    /// （イベント時の DIP キャッシュは使わない）。
    /// </remarks>
    public void SetCoordTransform(ICoordTransform? port) => _coordPort = port;

    // ====================================================================
    // 内部ヘルパー
    // ====================================================================

    /// <summary>新しい値を保持する（同値なら何もしない）。</summary>
    /// <typeparam name="T">値の型。</typeparam>
    /// <param name="field">保持先のフィールド。</param>
    /// <param name="value">設定する値。</param>
    /// <param name="previous">変更前の値（同値の場合は現在値）。</param>
    /// <returns>値を変更した場合は true（同値だった場合は false）。</returns>
    /// <remarks>
    /// 「同値なら何もしない」という規則を単一所有する<br/>
    /// （各プロパティが同じ数行を書くと、片方だけ同値判定が抜けて無駄なログ・再評価が溢れる）。<br/>
    /// 変更前の値も返すため、変更を検出した側（設定の変更ログ）が前後を比較できる。<br/>
    /// 本クラスは <c>field</c> キーワードを使わず明示的なフィールドを持つ（<c>field</c> は <c>ref</c> 引数へ渡せないため）。
    /// </remarks>
    private bool SetIfChanged<T>(ref T field, T value, out T previous)
    {
        previous = field;
        if (EqualityComparer<T>.Default.Equals(previous, value))
        {
            return false;
        }

        field = value;
        return true;
    }
}
