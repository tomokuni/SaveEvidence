using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using static EsUtil.Feature.LoupeCore.LoupeValueCoercer;

namespace EsUtil.Feature.LoupeCore;

/// <summary>ルーペの表示レイアウト（DIP。表示サイズと表示位置）。</summary>
/// <param name="DisplaySize">ルーペの一辺の表示サイズ（DIP。親領域の最小辺へクランプ済み）。</param>
/// <param name="Position">ルーペの表示位置（左上・親領域の DIP 座標）。配置できない場合は null。</param>
/// <remarks>
/// <see cref="Position"/> が null になるのは、拡大中心が未設定の場合である<br/>
/// （「配置しない」の判断をコアが単一所有し、UI 層はセルフサイズの反映だけを行う）。<br/>
/// 配置をホストが行う UI では <see cref="Position"/> をそのまま配置に使う。<br/>
/// <b><see cref="DisplaySize"/> は整数化しない</b>: 要素のサイズ（幅・高さ）と描画入力は同じ値でなければならず、<br/>
/// 途中で切り捨てると同じ表示サイズを求める 2 経路が最大 1 DIP ずれる（描画ビットマップと要素の大きさが食い違う）。
/// </remarks>
public readonly record struct LoupeLayout(double DisplaySize, EsPoint? Position);

/// <summary>ルーペの描画完了時に通知する表示情報（UI 層が描画に必要とする値）。</summary>
/// <param name="CaptureTopLeft">切り出し領域の左上（ドメイン座標）。中心未設定時は null。</param>
/// <param name="Scale">ドメイン座標 → ルーペ画像ピクセル座標の変換係数（拡大率と同値）。</param>
/// <param name="Layers">描く層とその順序（<b>描画順はコアが単一所有する</b>）。先頭から順に処理する。</param>
/// <remarks>
/// UI 層は <see cref="Layers"/> の順に各層を処理し、図形は自 UI の描画 API へ写す。<br/>
/// 中心座標ラベルは <see cref="Layers"/> の最後の層であり、UI のテキスト要素が描く（描画実装は当該の層を描かない。<br/>
/// ラベル要素は親より後に描かれるため常に最上位になる）。<br/>
/// 切り出し領域・スケールは <see cref="LoupeRenderArgsCalculator"/> が、層の順序はコアが単一所有する。
/// </remarks>
public readonly record struct LoupeRenderNotification(
    EsPoint? CaptureTopLeft,
    double Scale,
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
/// - 描画実装（<see cref="ILoupeRenderer"/>）の保持<br/>
/// <br/>
/// <b>描画の差し替え</b>: 描画は <see cref="ILoupeRenderer"/> の実装へ委譲する（<see cref="Renderer"/> で差し替え可能）。<br/>
/// コアは描画層を参照しないため、実装は注入で受け取る（コンストラクタで必須）。<br/>
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

    /// <summary>描画内容を描画面へ写す描画実装（既定は描画層の <c>LoupeRenderer.Default</c>）。</summary>
    private ILoupeRenderer _renderer;

    /// <summary>ファサードを初期化する。</summary>
    /// <param name="renderer">描画の実装（既定実装は描画層の <c>LoupeRenderer.Default</c>）。null 不可。</param>
    /// <param name="options">初期設定。null 可（null の場合は <see cref="LoupeOptions.Default"/>）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="renderer"/> が null の場合。</exception>
    public Loupe(ILoupeRenderer renderer, LoupeOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(renderer);

        _renderer = renderer;
        Options = options ?? LoupeOptions.Default;
    }

    /// <summary>描画の実装（描画内容を描画面へ写す戦略）。</summary>
    /// <value>現在の描画実装。既定は <see cref="ILoupeRenderer"/> の既定実装。</value>
    /// <remarks>
    /// 描画先（<see cref="IEsCanvas"/> の実装）と描画手順（本プロパティ）の両方を差し替えできる。<br/>
    /// 設定後の再描画は UI 層が要求する（本クラスは再描画のトリガーを持たない）。<br/>
    /// null は受け付けない（設定の有無で描画可否が変わると、UI の描画経路に分岐が生まれるため）。
    /// </remarks>
    /// <exception cref="ArgumentNullException">設定値が null の場合。</exception>
    public ILoupeRenderer Renderer
    {
        get => _renderer;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _renderer = value;
        }
    }

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
        var size = EsSizeMath.ClampToArea(Options.RequestedSize, parentSize);

        if (CenterPoint is not { } center)
        {
            return new LoupeLayout(size, null);
        }

        var centerDip = _coordinatePort.OrIdentity().Inverse(center);
        return new LoupeLayout(
            size,
            LoupePositionCalculator.ComputePosition(size, parentSize, centerDip, PositionOffset));
    }

    /// <summary>現在の状態から描画 1 フレーム分の入力を生成する。</summary>
    /// <param name="displaySize">ルーペの一辺の表示サイズ（DIP。通常は <see cref="ResolveLayout"/> の結果）。</param>
    /// <param name="availableSize">描画先の領域サイズ（物理ピクセル）。未確定（幅または高さが 0 以下）の場合はクランプしない。</param>
    /// <returns>描画入力（物理ピクセルサイズ・拡大率・外観・スケーリングを解決済み）。</returns>
    /// <remarks>
    /// 表示サイズは呼び出し側が決めて渡す（親領域へのクランプは <see cref="ResolveLayout"/> が、<br/>
    /// 実際の描画面からの導出は描画先を知る UI 層が担う）。本クラスは親領域のサイズを保持しない<br/>
    /// （保持すると「クランプの基準」が暗黙の状態になり、値をいつ解決したかに依存する）。<br/>
    /// DIP → 物理ピクセルの換算と描画先の領域へのクランプは <see cref="LoupeRenderRequest.Create"/> が単一所有する。<br/>
    /// 描画と通知（<see cref="ResolveNotification"/>）が同じ値を使うため、UI 層は本メソッドの結果を両方へ渡す。
    /// </remarks>
    public LoupeRenderRequest CreateRequest(double displaySize, EsSize? availableSize = null)
        => LoupeRenderRequest.Create(CenterPoint, displaySize, RenderScaling, Options, availableSize);

    /// <summary>実際に描画した入力から描画完了通知の内容を解決する。</summary>
    /// <param name="request">実際に描画した描画入力（<see cref="CreateRequest"/> の結果）。</param>
    /// <returns>切り出し領域の左上・スケール・描画層の順序。</returns>
    /// <remarks>
    /// 描画に使った値を受け取る（本メソッドが DIP から再換算すると、描画先の領域によるクランプで実際の描画と値が食い違うため）。<br/>
    /// <b>描画順もコアが単一所有する</b>: 層の一覧は固定であり（<see cref="s_layers"/>）、中心座標ラベルは常に最後（最上位）である。
    /// </remarks>
    public LoupeRenderNotification ResolveNotification(in LoupeRenderRequest request)
    {
        var args = LoupeRenderArgsCalculator.Compute(request);

        return new LoupeRenderNotification(args.CaptureTopLeft, args.Scale, s_layers);
    }

    /// <summary>中心座標ラベルの配置指示を解決する（UI 要素として配置する UI 用）。</summary>
    /// <param name="displaySize">ルーペの一辺の表示サイズ（DIP。通常は <see cref="ResolveLayout"/> の結果）。</param>
    /// <param name="measureText">テキストの実寸を測る関数（描画 API 依存のため UI 層が与える）。null 不可。</param>
    /// <returns>配置するラベル要素（1 要素）。中心未設定または非表示の設定の場合は空。</returns>
    /// <remarks>
    /// 文言（<see cref="CoordinateFormatter"/>）・表示可否（<see cref="LoupeMetrics.ShouldShowCoordLabel"/>）・<br/>
    /// 配置（<see cref="EsLabelLayout"/>）はコアが単一所有し、通知から要素への写像（実寸の反映）は<br/>
    /// <see cref="Renderer"/>（描画実装）へ委譲する。<br/>
    /// 表示サイズは要素の大きさ（＝ <see cref="ResolveLayout"/> の結果）を渡す<br/>
    /// （描画の物理ピクセルから逆算すると、丸めにより要素と背景が 1 DIP ずれうる）。
    /// </remarks>
    public IReadOnlyList<EsLabelElement> ResolveLabels(double displaySize, Func<string, double, EsSize> measureText)
        => _renderer.ResolveLabels(LoupeCoordLabel.Resolve(displaySize, CenterPoint, Options.Metrics), measureText);

    /// <summary>描く層とその順序（中心座標ラベルは必ず最後 = 常に最上位）。</summary>
    /// <remarks>
    /// 図形の層の順序は固定であり、層の有無で変わらないため 1 つの一覧を共有する（描画のたびの確保を避ける）。<br/>
    /// 中心座標ラベルは UI のテキスト要素が描くため、描画実装は当該の層を描かない<br/>
    /// （一覧に含めるのは「ラベルが最上位である」という契約の表現である）。
    /// </remarks>
    private static readonly IReadOnlyList<LoupeDrawLayer> s_layers =
    [
        LoupeDrawLayer.Background,
        LoupeDrawLayer.Checkerboard,
        LoupeDrawLayer.Image,
        LoupeDrawLayer.Crosshair,
        LoupeDrawLayer.Frame,
        LoupeDrawLayer.CoordLabel,
    ];
}
