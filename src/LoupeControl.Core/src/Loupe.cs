using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using static Estable.Loupe.LoupeValueCoercer;

namespace Estable.Loupe;

/// <summary>ルーペの表示レイアウト（DIP。表示サイズと表示位置）。</summary>
/// <param name="DisplaySize">ルーペの一辺の表示サイズ（DIP。親領域の最小辺へクランプ済み）。</param>
/// <param name="Position">ルーペの表示位置（左上・親領域の DIP 座標）。配置できない場合は null。</param>
/// <remarks>
/// <see cref="Position"/> が null になるのは、拡大中心が未設定の場合である<br/>
/// （「配置しない」の判断をコアが単一所有し、UI 層はセルフサイズの反映だけを行う）。<br/>
/// MewUI 版のように配置をホストが行う UI では <see cref="Position"/> をそのまま配置に使う。
/// </remarks>
public readonly record struct LoupeLayout(int DisplaySize, EsPoint? Position);

/// <summary>ルーペの描画完了時に通知する表示情報（UI 層が描画に必要とする値）。</summary>
/// <param name="CaptureTopLeft">切り出し領域の左上（ドメイン座標）。中心未設定時は null。</param>
/// <param name="Scale">ドメイン座標 → ルーペ画像ピクセル座標の変換係数（拡大率と同値）。</param>
/// <param name="CoordLabel">中心座標ラベルの描画契約。非表示の設定または中心未設定時は null。</param>
/// <param name="Layers">描く層とその順序（<b>描画順はコアが単一所有する</b>）。先頭から順に処理する。</param>
/// <remarks>
/// UI 層は <see cref="Layers"/> の順に各層を処理し、図形は自 UI の描画 API へ写し、
/// 中心座標ラベルはテキスト要素を配置する（ラベルは必ず最後 = 常に最上位）。<br/>
/// 切り出し領域・スケールの計算元は <see cref="LoupeRenderArgsCalculator"/> が、座標ラベルの契約は<br/>
/// <see cref="LoupeCoordLabel.Resolve"/> が単一所有し、本値はそれらを 1 つにまとめた通知用の値である。
/// </remarks>
public readonly record struct LoupeRenderNotification(
    EsPoint? CaptureTopLeft,
    double Scale,
    LoupeCoordLabel? CoordLabel,
    IReadOnlyList<LoupeDrawLayer> Layers);

/// <summary>Loupe（拡大鏡）の操作を UI 層・ホストへ提供するファサード。</summary>
/// <remarks>
/// <b>適用パターン</b>: Facade（表示レイアウトの解決・描画入力の生成・通知内容の解決・設定を 1 つの窓口へ集約する）。<br/>
/// <br/>
/// <b>責務</b>: ルーペの「状態（設定・環境値・拡大中心・表示位置のオフセット）」を保持し、<br/>
/// 「表示レイアウトの解決」「描画 1 フレーム分の入力の生成」「描画完了通知の内容の解決」を提供する。<br/>
/// UI 層とホストは本クラスだけを参照する（コアの計算クラスを個別に呼び出さない）。<br/>
/// <br/>
/// <b>本クラスが単一所有するもの</b>:<br/>
/// - 表示サイズの決定（要求サイズを親領域の最小辺へクランプする規則と、その唯一の書き込み元）<br/>
/// - 表示位置の決定（拡大中心 ＋ オフセット ＋ 親領域内へのクランプ）<br/>
/// - 描画入力（<see cref="LoupeRenderRequest"/>）の組み立て（DIP → 物理ピクセルの換算と描画先へのクランプ）<br/>
/// - 描画完了通知の内容（切り出し領域・スケール・座標ラベル）の解決<br/>
/// <br/>
/// <b>UI 層の責務</b>: 拡大元画像の解決（UI ごとに型が異なる）、描画の実行（ビットマップ合成・キャンバス描画）、<br/>
/// 再描画のバッチ化（UI のディスパッチャー）、公開プロパティ・公開イベントへの写像。<br/>
/// <br/>
/// <b>依存分離</b>: UI 非依存のコア型（<see cref="EsPoint"/> / <see cref="EsSize"/> / <see cref="EsBitmap"/>）と<br/>
/// 座標変換ポート（<see cref="ICoordTransform"/>）にのみ依存する。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で検証できる。
/// </remarks>
public sealed class Loupe
{
    /// <summary>外部から指定された座標変換ポート（null = 未指定。未指定時は等価変換として扱う）。</summary>
    private ICoordTransform? _coordinatePort;

    /// <summary>UI 層が通知した親領域サイズ（null = 未通知。未通知の場合はクランプしない）。</summary>
    /// <remarks>
    /// 「親領域が未確定ならクランプしない」の判断は <see cref="LoupePositionCalculator.ClampSizeToArea"/> が単一所有する。<br/>
    /// 配置を行わない UI（MewUI 版はホストが配置する）は親領域を通知せず、要求サイズをそのまま表示サイズとして使う。
    /// </remarks>
    private EsSize? _parentSize;

    /// <summary>ファサードを初期化する。</summary>
    /// <param name="options">初期設定。null 可（null の場合は <see cref="LoupeOptions.Default"/>）。</param>
    public Loupe(LoupeOptions? options = null) => Options = options ?? LoupeOptions.Default;

    /// <summary>ルーペの設定（表示サイズ・拡大率・外観）。</summary>
    /// <value>設定の単一値。既定は <see cref="LoupeOptions.Default"/>。</value>
    /// <remarks>
    /// 設定の値・既定値・数値項目の安全域への補正は <see cref="LoupeOptions"/> が単一所有する。<br/>
    /// 変更すると表示サイズ（要求サイズのクランプ結果）も変わるため、UI 層は <see cref="ResolveLayout"/> を呼び直す。
    /// </remarks>
    public LoupeOptions Options { get; set; }

    /// <summary>表示 DIP を物理ピクセルへ変換するスケーリング係数。</summary>
    /// <value>スケーリング係数（0.1〜8.0）。範囲外・非有限は補正済みの値を設定する。既定は 1.0。</value>
    /// <remarks>
    /// 環境値（DPI）のため <see cref="Options"/> には含めない。補正の規則は <see cref="LoupeValueCoercer"/> が単一所有し、<br/>
    /// どの設定経路（初期化・UI の依存関係プロパティ・直接代入）でも範囲内の値だけが保持される。
    /// </remarks>
    public double RenderScaling
    {
        get => field;
        set => field = CoerceRenderScaling(value);
    } = RenderScalingDefault;

    /// <summary>拡大表示する中心座標（ドメイン座標）。</summary>
    /// <value>拡大中心（元画像ピクセル座標）。未設定は null（市松模様のみを描画し、配置も行わない）。</value>
    public EsPoint? CenterPoint { get; set; }

    /// <summary>拡大中心からの右下オフセット（DIP）。</summary>
    /// <value>カーソル位置（拡大中心）からの右下方向へのオフセット量（DIP）。既定は 0。</value>
    /// <remarks>表示位置をカーソルから離して配置するためのホスト指定値である（位置のクランプはコアが行う）。</remarks>
    public double PositionOffset { get; set; }

    /// <summary>ルーペの一辺の表示サイズ（DIP。要求サイズを親領域の最小辺へクランプ済み）。</summary>
    /// <value>表示サイズ（DIP）。親領域が未通知の場合は <see cref="LoupeOptions.RequestedSize"/> のまま。</value>
    /// <remarks>要素サイズ（Width / Height）と描画ビットマップは本値を基準に揃える（クランプは 1 回だけ行う）。</remarks>
    public double DisplaySize => LoupePositionCalculator.ClampSizeToArea(Options.RequestedSize, _parentSize ?? default);

    /// <summary>ドメイン ↔ 親領域 DIP の座標変換ポートを設定する。</summary>
    /// <param name="port">座標変換ポート（Inverse = ドメイン → 親領域 DIP）。null の場合は等価変換。</param>
    /// <remarks>
    /// 変換関数は呼び出し毎に評価されるため、ウィンドウサイズ変更・ズーム・スクロール等のレイアウト変化に追従できる<br/>
    /// （イベント時の DIP キャッシュは使わない）。
    /// </remarks>
    public void SetCoordinateTransform(ICoordTransform? port) => _coordinatePort = port;

    /// <summary>親領域のサイズを通知して表示レイアウト（表示サイズ・表示位置）を解決する。</summary>
    /// <param name="parentSize">親領域のサイズ（DIP）。未確定（幅または高さが 0 以下）の場合はクランプしない。</param>
    /// <returns>表示レイアウト（表示サイズと表示位置。拡大中心が未設定の場合は位置が null）。</returns>
    /// <remarks>
    /// 処理フロー:<br/>
    /// 1. 要求サイズを親領域の最小辺へクランプして表示サイズを決める（クランプは 1 回だけ行い、要素サイズと位置で同じ値を使う）。<br/>
    /// 2. 拡大中心が未設定の場合は位置を null として返す（配置しない）。<br/>
    /// 3. 拡大中心を座標変換ポートで親領域 DIP へ変換し、オフセットを加えて親領域内へクランプした位置を返す。<br/>
    /// 親領域が未確定（未接続・レイアウト前）の場合、位置はクランプしない（クランプ可否の判断はコアが単一所有する）。
    /// </remarks>
    public LoupeLayout ResolveLayout(EsSize parentSize)
    {
        _parentSize = parentSize;
        var size = DisplaySize;

        if (CenterPoint is not { } center)
        {
            return new LoupeLayout((int)size, null);
        }

        var centerDip = _coordinatePort.OrIdentity().Inverse(center);
        return new LoupeLayout(
            (int)size,
            LoupePositionCalculator.ComputePosition(size, parentSize, centerDip, PositionOffset));
    }

    /// <summary>現在の状態から描画 1 フレーム分の入力を生成する。</summary>
    /// <param name="availableSize">描画先の領域サイズ。未確定（幅または高さが 0 以下）の場合はクランプしない。</param>
    /// <returns>描画入力（物理ピクセルサイズ・拡大率・外観・スケーリングを解決済み）。</returns>
    /// <remarks>
    /// DIP（表示サイズ・スケーリング）から物理ピクセルへの換算と、描画先の領域へのクランプは <see cref="LoupeRenderRequest"/> が単一所有する。<br/>
    /// 描画と通知（<see cref="ResolveNotification"/>）が同じ値を使うため、UI 層は本メソッドの結果を両方へ渡す。
    /// </remarks>
    public LoupeRenderRequest CreateRequest(EsSize? availableSize = null)
        => LoupeRenderRequest.Create(CenterPoint, DisplaySize, RenderScaling, Options, availableSize);

    /// <summary>実際に描画した入力から描画完了通知の内容を解決する。</summary>
    /// <param name="request">実際に描画した描画入力（<see cref="CreateRequest"/> の結果）。</param>
    /// <returns>切り出し領域の左上・スケール・座標ラベルの契約・描画層の順序。</returns>
    /// <remarks>
    /// 描画に使った値を受け取る（本メソッドが DIP から再換算すると、描画先の領域によるクランプで実際の描画と値が食い違うため）。<br/>
    /// 座標ラベルの表示可否・文言・配置はコアが単一所有する。<br/>
    /// <b>描画順もコアが単一所有する</b>: 層の順序は <see cref="ResolveLayers"/> が決める（ラベルは最後 = 常に最上位）。
    /// </remarks>
    public LoupeRenderNotification ResolveNotification(in LoupeRenderRequest request)
    {
        var args = LoupeRenderArgsCalculator.Compute(request);
        var coordLabel = LoupeCoordLabel.Resolve(request);

        return new LoupeRenderNotification(
            args.CaptureTopLeft, args.Scale, coordLabel, ResolveLayers(request, coordLabel));
    }

    /// <summary>描く層だけを描画順に並べる。</summary>
    /// <param name="request">描画入力（外観）。</param>
    /// <param name="coordLabel">中心座標ラベル。null の場合は層を含めない。</param>
    /// <returns>描く層の一覧（先頭から順に処理する）。</returns>
    /// <remarks>
    /// 描画可否（透明色は描かない・切り出せない場合は画像を描かない）はコアが判定するが、<br/>
    /// 拡大画像は「描けるかどうか」がソース（UI ごとに型が異なる）にも依存するため、<br/>
    /// 層としては常に含め、描画側がソース未解決なら何もしない（順序の判断だけをコアが持つ）。<br/>
    /// 中心座標ラベルは必ず末尾へ置き、常に最上位に表示する。
    /// </remarks>
    private static IReadOnlyList<LoupeDrawLayer> ResolveLayers(in LoupeRenderRequest request, LoupeCoordLabel? coordLabel)
    {
        // 明示的に組み立てる（列挙に層を追加しても描画順が勝手に変わらないようにするため）
        var layers = new List<LoupeDrawLayer>(6)
        {
            LoupeDrawLayer.Background,
            LoupeDrawLayer.Checkerboard,
            LoupeDrawLayer.Image,
            LoupeDrawLayer.Crosshair,
            LoupeDrawLayer.Frame,
        };

        if (coordLabel is not null)
        {
            layers.Add(LoupeDrawLayer.CoordLabel);
        }

        return layers;
    }
}
