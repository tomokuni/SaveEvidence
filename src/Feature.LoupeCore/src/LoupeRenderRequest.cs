using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;

namespace EsUtil.Feature.LoupeCore;

/// <summary>ルーペの 1 フレーム分の描画入力（コア⇔UI の契約）。</summary>
/// <remarks>
/// UI 層は自 UI の公開プロパティから本値を組み立てて描画層・コアの計算へ渡すだけで、<br/>
/// 描画に必要な値の意味・単位・組み立て順（DIP → 物理ピクセルの換算とクランプ）を持たない。<br/>
/// <br/>
/// <b>役割の分担</b>: ホストが設定する入力（設定値・環境値）は UI の公開プロパティが持ち、本値はそれらから<br/>
/// 描画 1 フレーム分に必要な形へ<b>解決したスナップショット</b>である（描画層は公開プロパティを読まない）。<br/>
/// - 表示サイズ（親領域のクランプ結果。<see cref="Loupe.ResolveLayout"/> の DIP）+ `RenderScaling`（環境）→ <see cref="PixelSize"/><br/>
/// - <see cref="LoupeOptions.ZoomLevel"/>（設定）→ <see cref="ZoomLevel"/>（そのまま）<br/>
/// - <see cref="LoupeOptions.Metrics"/>（設定）→ <see cref="Metrics"/>（そのまま）<br/>
/// 拡大元画像は UI ごとに型が異なるため本値に含めず、<br/>
/// 描画層が解決した <see cref="EsBitmap"/> を別引数として受け取る（両 UI が同じ手順で本値を組み立てられる）。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
/// <param name="Center">拡大中心（ドメイン座標 = 元画像ピクセル座標）。未設定は null（市松模様のみを描画する）。</param>
/// <param name="PixelSize">ルーペの一辺のサイズ（物理ピクセル。切り上げ・最低 1）。</param>
/// <param name="ZoomLevel">拡大率（1〜64。1 未満は不可）。</param>
/// <param name="Metrics">ルーペの寸法（市松模様のタイル・外枠の太さ。色は描画の設定が単一所有する）。</param>
/// <param name="Scaling">表示 DIP → 物理ピクセルのスケーリング係数。</param>
public readonly record struct LoupeRenderRequest(
    EsPoint? Center,
    int PixelSize,
    int ZoomLevel,
    LoupeMetrics Metrics,
    double Scaling)
{
    /// <summary>描画先の全領域（ルーペの一辺が作る正方形）を取得する。</summary>
    /// <value>左上を原点（0, 0）とし、幅・高さが <see cref="PixelSize"/> の矩形（物理ピクセル）。</value>
    /// <remarks>
    /// 下地・市松模様は描画先の全面へ描くため、各 UI の描画層が矩形を組み立てないよう契約が単一所有する<br/>
    /// （描画先の原点は常に 0 であり、UI 側で組み立てると原点の取り違えが生じる）。
    /// </remarks>
    public EsRect SurfaceBounds => new(0, 0, PixelSize, PixelSize);

    /// <summary>UI 層が保持する DIP 値から描画入力を組み立てる。</summary>
    /// <param name="center">拡大中心（ドメイン座標 = 元画像ピクセル座標）。未設定は null。</param>
    /// <param name="displaySize">ルーペの表示サイズ（DIP。一辺）。</param>
    /// <param name="renderScaling">表示 DIP → 物理ピクセルのスケーリング係数。</param>
    /// <param name="options">ルーペの設定（拡大率・外観。補正済みの値を渡す）。</param>
    /// <param name="availableSize">描画先の領域サイズ（物理ピクセル）。未確定（幅または高さが 0 以下）の場合はクランプしない。</param>
    /// <returns>描画入力（物理ピクセルサイズを解決済み）。</returns>
    /// <remarks>
    /// 表示サイズ（DIP）を物理ピクセルへ換算し、描画先の領域が指定された場合はそれへクランプする。<br/>
    /// 換算とクランプの規則は <see cref="LoupePositionCalculator.ResolvePixelSize"/> が単一所有する（描画と通知で同じ値を使う）。<br/>
    /// 表示サイズは設定（<see cref="LoupeOptions.RequestedSize"/>）ではなく、親領域でクランプした後の値を渡す<br/>
    /// （クランプの基準が親パネルか描画先の表面かで異なるため、値の決定は呼び出し側が担う）。
    /// </remarks>
    public static LoupeRenderRequest Create(
        EsPoint? center,
        double displaySize,
        double renderScaling,
        LoupeOptions options,
        EsSize? availableSize = null)
        => new(center,
               LoupePositionCalculator.ResolvePixelSize(displaySize, renderScaling, availableSize),
               options.ZoomLevel,
               options.Metrics,
               renderScaling);
}
