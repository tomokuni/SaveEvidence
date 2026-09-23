using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;

namespace Estable.Loupe;

/// <summary>ルーペの 1 フレーム分の描画入力（コア⇔UI の契約）。</summary>
/// <remarks>
/// UI 層は自 UI の公開プロパティから本値を組み立てて描画層・コアの計算へ渡すだけで、<br/>
/// 描画に必要な値の意味・単位・組み立て順（DIP → 物理ピクセルの換算とクランプ）を持たない。<br/>
/// <br/>
/// <b>役割の分担</b>: ホストが設定する入力（設定値・環境値）は UI の公開プロパティが持ち、本値はそれらから<br/>
/// 描画 1 フレーム分に必要な形へ<b>解決したスナップショット</b>である（描画層は公開プロパティを読まない）。<br/>
/// - 表示サイズ（設定を UI が親領域・描画先へクランプした DIP）+ `RenderScaling`（環境）→ <see cref="PixelSize"/><br/>
/// - <see cref="LoupeOptions.ZoomLevel"/>（設定）→ <see cref="ZoomLevel"/>（そのまま）<br/>
/// - <see cref="LoupeOptions.Appearance"/>（設定）→ <see cref="Appearance"/>（そのまま）<br/>
/// 拡大元画像は UI ごとに型が異なるため本値に含めず、<br/>
/// 描画層が解決した <see cref="EsBitmap"/> を別引数として受け取る（両 UI が同じ手順で本値を組み立てられる）。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
/// <param name="Center">拡大中心（ドメイン座標 = 元画像ピクセル座標）。未設定は null（市松模様のみを描画する）。</param>
/// <param name="PixelSize">ルーペの一辺のサイズ（物理ピクセル。切り上げ・最低 1）。</param>
/// <param name="ZoomLevel">拡大率（1〜64。1 未満は不可）。</param>
/// <param name="Appearance">外観（下地・色・寸法・描画可否を単一所有する）。</param>
/// <param name="Scaling">表示 DIP → 物理ピクセルのスケーリング係数。</param>
public readonly record struct LoupeRenderRequest(
    EsPoint? Center,
    int PixelSize,
    int ZoomLevel,
    LoupeAppearance Appearance,
    double Scaling)
{
    /// <summary>UI 層が保持する DIP 値から描画入力を組み立てる。</summary>
    /// <param name="center">拡大中心（ドメイン座標 = 元画像ピクセル座標）。未設定は null。</param>
    /// <param name="displaySize">ルーペの表示サイズ（DIP。一辺）。</param>
    /// <param name="renderScaling">表示 DIP → 物理ピクセルのスケーリング係数。</param>
    /// <param name="options">ルーペの設定（拡大率・外観。補正済みの値を渡す）。</param>
    /// <param name="availableSize">描画先の領域サイズ。未確定（幅または高さが 0 以下）の場合はクランプしない。</param>
    /// <returns>描画入力（物理ピクセルサイズを解決済み）。</returns>
    /// <remarks>
    /// 表示サイズ（DIP）を物理ピクセルへ換算し、描画先の領域が指定された場合はそれへクランプする。<br/>
    /// 換算とクランプの規則は <see cref="LoupePositionCalculator.ResolvePixelSize"/> が単一所有する（描画と通知で同じ値を使う）。<br/>
    /// 表示サイズは設定（<see cref="LoupeOptions.RequestedSize"/>）ではなく、UI 層が親領域・描画先にクランプした後の値を渡す<br/>
    /// （クランプの基準（親パネル / 描画先の表面）は UI ごとに異なるため、値の決定は UI 層が担う）。
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
               options.Appearance,
               renderScaling);
}
