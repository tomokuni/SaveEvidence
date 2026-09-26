using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using static EsUtil.Feature.LoupeCore.Loupe.Limits;
using EsUtil.Feature.LoupeCore.Setting;
using EsUtil.Feature.LoupeCore.Drawing;
using EsUtil.Feature.LoupeCore.Decoration;
using EsUtil.Feature.LoupeCore.Placement;


namespace EsUtil.Feature.LoupeCore;

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
/// - 描画完了通知の内容（切り出し領域・スケール）の解決<br/>
/// <br/>
/// <b>描画は本コアの関心事ではない</b>: 本コアは描画 API も描画面も知らない<br/>
/// （<see cref="LoupeDrawContent"/> とその <see cref="LoupeDrawContent.Layers"/> というデータを返すだけである）。<br/>
/// 描画は UI 層が行い、層の順序に従った描画手順は UI 側の共有層（<c>LoupeControl.UIBase</c> の <c>LoupeRendererBase</c>）が単一所有する。<br/>
/// <br/>
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
    private ICoordTransform? _coordinatePort;

    /// <summary>ファサードを初期化する。</summary>
    /// <param name="options">初期設定。null 可（null の場合は <see cref="LoupeOptions.Default"/>）。</param>
    public Loupe(LoupeOptions? options = null)
    {
        Options = options ?? LoupeOptions.Default;
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
    /// 環境値（DPI）のため <see cref="Options"/> には含めない。補正の規則は <see cref="Loupe.Limits"/> が単一所有し、<br/>
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
        var size = parentSize.ClampToArea(Options.RequestedSize);

        if (CenterPoint is not { } center)
        {
            return new LoupeLayout(size, null);
        }

        var centerDip = _coordinatePort.OrIdentity().Inverse(center);
        return new LoupeLayout(size, LoupePlacementGeometry.ComputePosition(size, parentSize, centerDip, PositionOffset));
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

    /// <summary>描画入力から 1 フレーム分の描画内容を解決する。</summary>
    /// <param name="request">描画入力（描画のたびに決まる値。物理ピクセルサイズ・寸法の換算結果を含む）。</param>
    /// <returns>描画内容（描画面・タイル列挙子・矩形・拡大画像の幾何・テキスト配置・層の順序）。</returns>
    /// <remarks>
    /// 処理フロー:<br/>
    /// 1. 描画面の全体を物理ピクセルサイズから決める。<br/>
    /// 2. 市松模様の暗色タイルを列挙する構造体を換算済みのタイルサイズから作る。<br/>
    /// 3. 十字線と外枠の矩形を、物理ピクセルサイズと換算済みの太さから導出する。<br/>
    /// 4. 拡大画像の切り出しと描画の矩形を解決する（`LoupeRenderRequest` の `MagnifiedImage`）。<br/>
    /// 5. テキスト（中心座標ラベルの文言と基準点）を解決する。<br/>
    /// 6. 描く層の一覧（テキストの有無で末尾が変わる）を添えて描画内容を返す。<br/>
    /// <br/>
    /// 色はコアが持たないため本メソッドは扱わない（描画時のアルファ判定は描画実装の責務であり、<br/>
    /// アルファ 0 の色は描かない）。<br/>
    /// 拡大画像は切り出しと描画の矩形（<see cref="LoupeMagnifiedImage"/>）として本値に含める<br/>
    /// （切り出し元の画像そのものは型が UI に依存するため、描画の起動時に引数で受け取る）。
    /// </remarks>
    public LoupeDrawContent ResolveContent(in LoupeRenderRequest request)
    {
        var surface = request.SurfaceBounds;
        var coordLabelText = ResolveCoordLabelText(request);

        return new LoupeDrawContent(
            surface,
            new LoupeCheckerTiles(request.PixelSize, request.CheckerSizePixels),
            LoupeDecorationGeometry.ComputeCrosshairRects(request.PixelSize, request.ZoomLevel),
            LoupeDecorationGeometry.ComputeFrameRects(request.PixelSize, request.FrameWidthPixels),
            request.MagnifiedImage,
            coordLabelText,
            coordLabelText is null ? s_layersWithoutLabel : s_layersWithLabel);
    }

    /// <summary>実際に描画した入力から描画完了通知の内容を解決する。</summary>
    /// <param name="request">実際に描画した描画入力（<see cref="CreateRequest"/> の結果）。</param>
    /// <returns>切り出し領域の左上とスケール。</returns>
    /// <remarks>
    /// 切り出し領域の一辺と左上は <see cref="LoupeRenderRequest"/> が単一所有する（拡大中心が未設定の場合は左上が null）。<br/>
    /// 描画に使った値を受け取る（本メソッドが DIP から再換算すると、描画先の領域によるクランプで実際の描画と値が食い違うため）。<br/>
    /// スケールは拡大率と同値である（描画は切り出し領域を拡大率倍で描く）。
    /// </remarks>
    public LoupeRenderNotification ResolveNotification(in LoupeRenderRequest request)
        => new(request.CaptureTopLeft, request.ZoomLevel);
}
