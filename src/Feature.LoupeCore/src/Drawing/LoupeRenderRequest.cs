using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Feature.LoupeCore.Checkerboard;
using EsUtil.Feature.LoupeCore.Setting;

namespace EsUtil.Feature.LoupeCore.Drawing;

/// <summary>ルーペの 1 フレーム分の描画入力（コア⇔UI の契約）。</summary>
/// <remarks>
/// <b>UI 層は本値を自分で組み立てない</b>: 設定・環境値から描画に必要な形へ写す組み立て順は<br/>
/// <see cref="Loupe.CreateRequest"/> が単一所有する（UI は描画先のサイズと表示サイズを渡すだけである）。<br/>
/// UI 層は本値を <see cref="Loupe.ResolveContent"/> と描画層へ渡すだけで、<br/>
/// 描画に必要な値の意味・単位・換算（DIP → 物理ピクセル）を持たない。<br/>
/// <br/>
/// <b>役割の分担</b>: ホストが設定する入力（設定値・環境値）は UI の公開プロパティが持ち、本値はそれらから<br/>
/// 描画 1 フレーム分に必要な形へ<b>解決したスナップショット</b>である（描画層は公開プロパティを読まない）。<br/>
/// - 表示サイズ（親領域のクランプ結果。<see cref="Loupe.ResolveLayout"/> の DIP）+ <see cref="Loupe.DipToDeviceScaling"/>（環境値）→ <see cref="PixelSize"/><br/>
/// - <see cref="LoupeOptions"/>（設定）→ <see cref="Options"/>（そのまま。拡大率は <see cref="ZoomLevel"/> として読める）<br/>
/// 拡大元画像は UI ごとに型が異なるため本値に含めず、<br/>
/// 描画層が解決した <see cref="EsBitmap"/> を別引数として受け取る（両 UI が同じ手順で本値を組み立てられる）。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
/// <param name="Center">拡大中心（ドメイン座標 = 元画像ピクセル座標）。未設定は null（市松模様のみを描画する）。</param>
/// <param name="DisplaySize">ルーペの一辺の表示サイズ（DIP。ラベルの位置の基準であり、物理ピクセルサイズの換算元）。</param>
/// <param name="PixelSize">ルーペの一辺のサイズ（物理ピクセル。切り上げ・最低 1）。</param>
/// <param name="Options">ルーペの設定（拡大率・寸法。色は描画の設定が単一所有する）。</param>
/// <param name="DipToDeviceScaling">表示 DIP → 物理ピクセルのスケーリング係数。</param>
/// <param name="CheckerSizePixels">市松模様の 1 タイルの一辺（物理ピクセル。換算済みで最低 1。描くかどうかは <see cref="LoupeOptions.ShowCheckerboard"/> が単一所有する）。</param>
/// <param name="BorderThicknessPixels">枠線の太さ（物理ピクセル。換算済みで最低 1。描くかどうかは <see cref="LoupeOptions.ShowBorder"/> が単一所有する）。</param>
/// <remarks>
/// <b>寸法は換算済みの値を保持する</b>: DIP の寸法（<see cref="LoupeOptions.CheckerSize"/> / <see cref="LoupeOptions.BorderThickness"/>）を描画に使う物理ピクセルへ換算する手順は<br/>
/// コアが単一所有する（描画実装が換算を持たないため、描画側には物理ピクセルの値だけが渡る）。<br/>
/// <b>値 0 を「描かない」の代用にしない</b>: 描画の可否は設定の明示フラグが単一所有し、非表示の項目は描画内容として空が渡る。<br/>
/// <b>表示サイズ（DIP）も保持する</b>: ラベルの位置は DIP で決まるため、描画内容の解決が本値だけで完結する。
/// </remarks>
public readonly record struct LoupeRenderRequest(
    EsPoint? Center,
    double DisplaySize,
    int PixelSize,
    LoupeOptions Options,
    double DipToDeviceScaling,
    int CheckerSizePixels,
    int BorderThicknessPixels)
{
    /// <summary>拡大率を取得する。</summary>
    /// <value>拡大率（1〜64。設定の補正済みの値）。</value>
    /// <remarks>設定（<see cref="Options"/>）から導出する（同じ真実を 2 つ持たない）。</remarks>
    public int ZoomLevel => Options.ZoomLevel;

    /// <summary>描画先の全領域（ルーペの一辺が作る正方形）を取得する。</summary>
    /// <value>左上を原点（0, 0）とし、幅・高さが <see cref="PixelSize"/> の矩形（物理ピクセル）。</value>
    /// <remarks>
    /// 下地・市松模様は描画先の全面へ描くため、各 UI の描画層が矩形を組み立てないよう本値が導出する<br/>
    /// （描画先の原点は常に 0 であり、UI 側で組み立てると原点の取り違えが生じる）。<br/>
    /// 描画層へは描画内容（<see cref="LoupeDrawContent.SurfaceBounds"/>）として渡るため本値は公開しない
    /// （外部参照は無く、追加描画のための座標変換は公開している <see cref="CaptureTopLeft"/> と <see cref="ZoomLevel"/> で足りる）。
    /// </remarks>
    internal EsRect SurfaceBounds => new(0, 0, PixelSize, PixelSize);

    /// <summary>切り出し領域の一辺（ドメインピクセル）を取得する。</summary>
    /// <value>切り出し領域の一辺。<see cref="PixelSize"/> を <see cref="ZoomLevel"/> で割った切り上げ。</value>
    /// <remarks>
    /// <b>切り上げる理由</b>: 拡大で埋めきれない余り（最大 <c>ZoomLevel - 1</c> ピクセル）を透明パディングで補うためである<br/>
    /// （切り捨てるとルーペの右端・下端が埋まらない）。切り上げの規則は <see cref="LoupeDivMath.CeilDiv"/> が単一所有する。<br/>
    /// 十字線の交点（<c>CaptureSize / 2</c>）も本値を使う。
    /// </remarks>
    internal int CaptureSize => LoupeDivMath.CeilDiv(PixelSize, ZoomLevel);

    /// <summary>拡大中心から切り出し領域の左上（ドメイン座標）を取得する。</summary>
    /// <value>切り出し領域の左上（ドメイン座標）。<see cref="Center"/> が未設定の場合は null（描画しない）。</value>
    /// <remarks>
    /// 中心の整数化（<c>(int)</c>）は切り出し（<c>EsBitmap.Crop</c>）と同じ丸めにする<br/>
    /// （切り出しの実行は描画層が、切り出し領域の通知は本値が行い、同じ矩形を指す必要があるため）。<br/>
    /// 公開である理由: UI 層が描画完了通知（ホストが描画済み画像へオーバーレイを描くための情報）を組み立てるのに使う。
    /// </remarks>
    public EsPoint? CaptureTopLeft => Center is { } center
        ? new EsPoint((int)center.X - CaptureSize / 2, (int)center.Y - CaptureSize / 2)
        : null;

    /// <summary>拡大画像の切り出しと描画の矩形を取得する。</summary>
    /// <value>切り出しと描画の矩形。<see cref="Center"/> が未設定の場合は null（描画しない）。</value>
    /// <remarks>
    /// 「切り出した領域を左上原点から <see cref="ZoomLevel"/> 倍の大きさで描く」規則を単一所有する。<br/>
    /// 切り出し元の画像（<c>EsBitmap</c>）は含めない（切り出しの実行は描画層が <c>EsBitmap.Crop</c> で行う）。<br/>
    /// <b>描画先は切り上げ分だけ表面より大きくなりうる</b>（最大 <c>ZoomLevel - 1</c> 物理ピクセル）。<br/>
    /// はみ出した部分は描画先のクリップに任せる（切り捨てると拡大の倍率が整数でなくなり画素がぼやける）。
    /// </remarks>
    internal LoupeMagnifiedImage? MagnifiedImage => CaptureTopLeft is not { } topLeft
        ? null
        : new LoupeMagnifiedImage(
            new EsRectInt((int)topLeft.X, (int)topLeft.Y, CaptureSize, CaptureSize),
            new EsRect(0, 0, CaptureSize, CaptureSize),
            new EsRect(0, 0, CaptureSize * ZoomLevel, CaptureSize * ZoomLevel));

    /// <summary>設定・環境値と DIP 値から描画入力を組み立てる（描画入力の唯一の生成元）。</summary>
    /// <param name="center">拡大中心（ドメイン座標 = 元画像ピクセル座標）。未設定は null。</param>
    /// <param name="displaySize">ルーペの表示サイズ（DIP。一辺）。</param>
    /// <param name="dipToDeviceScaling">表示 DIP → 物理ピクセルのスケーリング係数。</param>
    /// <param name="options">ルーペの設定（拡大率・外観。補正済みの値を渡す）。</param>
    /// <param name="availableSize">描画先の領域サイズ（物理ピクセル）。未確定（幅または高さが 0 以下）の場合はクランプしない。</param>
    /// <returns>描画入力（物理ピクセルサイズを解決済み）。</returns>
    /// <remarks>
    /// 表示サイズ（DIP）を物理ピクセルへ換算し、描画先の領域が指定された場合はそれへクランプする。<br/>
    /// 換算とクランプの規則は <see cref="ResolvePixelSize"/> が単一所有する（描画と通知で同じ値を使う）。<br/>
    /// 寸法（市松タイル・枠線の太さ）も併せて物理ピクセルへ換算する（換算の規則は <see cref="LoupeMetricsScaler"/> が単一所有する）。<br/>
    /// 表示サイズは設定（<see cref="LoupeOptions.RequestedSize"/>）ではなく、親領域でクランプした後の値を渡す<br/>
    /// （クランプの基準が親パネルか描画先の表面かで異なるため、値の決定は呼び出し側が担う）。<br/>
    /// <b>公開である理由</b>: ファサード（<see cref="Loupe.CreateRequest"/>）と独自 UI が描画入力を組み立てる唯一の入口である<br/>
    /// （描画先の領域を知るのは呼び出し側だけであり、コアは描画面を知らない）。
    /// </remarks>
    public static LoupeRenderRequest Create(
        EsPoint? center,
        double displaySize,
        double dipToDeviceScaling,
        LoupeOptions options,
        EsSize? availableSize = null)
        => new(center,
               displaySize,
               ResolvePixelSize(displaySize, dipToDeviceScaling, availableSize),
               options,
               dipToDeviceScaling,
               LoupeMetricsScaler.ScaleCheckerSize(options.CheckerSize, dipToDeviceScaling),
               LoupeMetricsScaler.ScaleBorderThickness(options.BorderThickness, dipToDeviceScaling));

    /// <summary>表示サイズ（DIP）から描画に使う物理ピクセルサイズを求める。</summary>
    /// <param name="size">表示サイズ（DIP・一辺）。</param>
    /// <param name="dipToDeviceScaling">表示 DIP → 物理ピクセルのスケーリング係数。</param>
    /// <param name="availableSize">描画先の領域サイズ。未確定（幅または高さが 0 以下）の場合はクランプしない。</param>
    /// <returns>描画に使う一辺の物理ピクセルサイズ（切り上げ・最低 1、領域指定時はクランプ済み）。</returns>
    /// <remarks>
    /// 「表示サイズを物理ピクセルへ換算し、描画先より大きい場合は描画先に合わせる」手順を単一所有する。<br/>
    /// 換算は <see cref="DpiScaler.ScaleToPixel(double, double)"/>、クランプは <see cref="EsSize.ClampToArea(double)"/> へ委譲する。<br/>
    /// 描画と描画引数（イベント通知）が同じ値を使うため、UI 層は本値（<see cref="PixelSize"/>）を両方へ渡す<br/>
    /// （それぞれの計算で値がずれると、ホストへ通知する切り出し領域と実際の描画が食い違う）。
    /// </remarks>
    internal static int ResolvePixelSize(double size, double dipToDeviceScaling, EsSize? availableSize = null)
        => (int)availableSize.GetValueOrDefault().ClampToArea(DpiScaler.ScaleToPixel(size, dipToDeviceScaling));
}
