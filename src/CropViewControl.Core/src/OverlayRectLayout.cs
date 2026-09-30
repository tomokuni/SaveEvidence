using EsUtil.Platform.Core.Geometry;

namespace EsUtil.SaveEvidence.CropView;

/// <summary>オーバーレイ矩形を描画するための配置と外観の計算を単一所有する（純粋関数）。</summary>
/// <remarks>
/// オーバーレイ矩形は 3 つの描画経路で描かれる（Avalonia の図形要素 / MewUI の Skia キャンバス / ルーペ画像のラスタ）。<br/>
/// 経路ごとに「負サイズをどう扱うか」「太さの下限」「破線かどうか」を判断すると見た目がずれるため、本クラスへ集約する。<br/>
/// 描画経路は本クラスが確定した <see cref="OverlayRectFrame"/> を描画先の型へ写すだけで、式と判断を持たない。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public static class OverlayRectLayout
{
    /// <summary>枠線の太さの下限（ピクセル）。</summary>
    /// <remarks>太さ 0 以下を要求されても枠線が消えないよう、1 ピクセルは描く（両 UI とラスタ経路で同じ規則）。</remarks>
    public const double MinimumThickness = 1.0;

    /// <summary>枠線の太さを有効値へ補正する。</summary>
    /// <param name="thickness">要求された太さ（ピクセル。 0 以下も可）。</param>
    /// <returns>補正後の太さ（<see cref="MinimumThickness"/> 以上）。</returns>
    public static double EffectiveThickness(double thickness) => Math.Max(MinimumThickness, thickness);

    /// <summary>ビューポート座標の 2 点（左上・右下）から矩形を組み立てる（負サイズは 0 へクランプ）。</summary>
    /// <param name="topLeft">左上（ビューポート座標）。</param>
    /// <param name="bottomRight">右下（ビューポート座標）。</param>
    /// <returns>幅・高さを 0 以上へクランプした矩形。</returns>
    /// <remarks>
    /// ズームの縮小・中央寄せにより右下が左上を逆転し得るため、幅・高さは負にしない（負のサイズを描画先へ渡すと例外になる）。<br/>
    /// MewUI の値型は負のサイズを 0 へクランプするが Avalonia は保持するため、本メソッドを通して両 UI の挙動を揃える。
    /// </remarks>
    public static EsRect ViewportRectFromCorners(EsPoint topLeft, EsPoint bottomRight)
        => new(topLeft.X,
               topLeft.Y,
               Math.Max(0, bottomRight.X - topLeft.X),
               Math.Max(0, bottomRight.Y - topLeft.Y));

    /// <summary>矩形情報（ドメイン座標）をビューポート座標の描画入力へ解決する。</summary>
    /// <param name="rect">矩形情報（ドメイン座標 = 元画像ピクセル座標）。</param>
    /// <param name="viewportTopLeft">矩形左上のビューポート座標。</param>
    /// <param name="viewportBottomRight">矩形右下のビューポート座標。</param>
    /// <returns>描画経路がそのまま使える値（配置・太さ・破線の判定と長さ）を確定した描画入力。</returns>
    /// <remarks>
    /// 座標変換（ドメイン座標 → ビューポート座標）は UI 層が担う（ズームビューの変換を経由するため）。<br/>
    /// 本メソッドは変換済みの 2 点を受け取り、クランプ・太さ補正・破線の判定を済ませる。
    /// </remarks>
    public static OverlayRectFrame Resolve(OverlayRect rect, EsPoint viewportTopLeft, EsPoint viewportBottomRight)
    {
        var (dashLength, gapLength) = OverlayLineDash.Get(rect.Style);

        return new OverlayRectFrame(
            ViewportRectFromCorners(viewportTopLeft, viewportBottomRight),
            rect.Foreground,
            rect.Background,
            EffectiveThickness(rect.Thickness),
            OverlayLineDash.IsDashed(rect.Style),
            dashLength,
            gapLength);
    }
}
