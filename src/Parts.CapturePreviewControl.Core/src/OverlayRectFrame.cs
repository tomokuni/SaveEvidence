using EsUtil.Platform.Core.Geometry;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core;

/// <summary>オーバーレイ矩形 1 個分の描画入力（コア⇔UI の契約）。</summary>
/// <remarks>
/// UI 層は矩形のビューポート座標（左上・右下）を求めて本値を組み立て、描画先の型へ写すだけでよい。<br/>
/// 負サイズのクランプ・太さの下限・破線かどうかの判定・背景色を使うか・ダッシュ長は本値が確定済みであるため、<br/>
/// 描画経路は式と判断を持たない（2 つの UI 経路で見た目がずれる原因を無くす）。<br/>
/// 座標変換（対象座標 → ビューポート座標）は UI 層が担う（ズームビューの変換を経由するため）。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
/// <param name="Bounds">ビューポート座標（DIP）の矩形。 幅・高さは 0 以上へクランプ済み。</param>
/// <param name="Foreground">前景色（透明度を含む）。</param>
/// <param name="Background">背景色（透明度を含む）。 破線のときの下地。</param>
/// <param name="Thickness">枠線の太さ（ピクセル。<see cref="OverlayRectLayout.MinimumThickness"/> 以上）。</param>
/// <param name="IsDashed">破線（破線または点線）かどうか。 実線の場合は false。</param>
/// <param name="DashLength">ダッシュ長（単位は描画経路に従う）。 実線は 0。</param>
/// <param name="GapLength">ダッシュの間隔（単位は描画経路に従う）。 実線は 0。</param>
public readonly record struct OverlayRectFrame(
    EsRect Bounds,
    DrawingColor Foreground,
    DrawingColor Background,
    double Thickness,
    bool IsDashed,
    double DashLength,
    double GapLength)
{
    /// <summary>枠線の半幅（ピクセル）を取得する。</summary>
    /// <value>枠線を矩形の内側へ寄せる量（<see cref="Thickness"/> の半分）。</value>
    /// <remarks>枠線の中心線は矩形の辺の内側へ半幅ぶん寄せる（Avalonia の <c>Rectangle.Stroke</c> と同じ見た目にする）。</remarks>
    public double HalfThickness => Thickness / 2;

    /// <summary>背景色の下地を描くかどうかを取得する。</summary>
    /// <value>破線の場合は true。 実線の場合は false（下地を塗ると矩形内部が塗り潰される）。</value>
    public bool DrawsBackground => IsDashed;

    /// <summary>描画できる大きさかどうかを取得する。</summary>
    /// <value>幅と高さがともに 0 より大きい場合は true。</value>
    /// <remarks>幅・高さが 0 の矩形（画像の端に完全に寄った等）は描画しない（描画先によっては例外になる）。</remarks>
    public bool HasDrawableSize => Bounds.Width > 0 && Bounds.Height > 0;
}
