using EsUtil.Platform.Core.Geometry;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.SaveEvidence.CropView;

/// <summary>オーバーレイとして表示する矩形の描画情報。</summary>
/// <param name="Bounds">矩形（ドメイン座標 = 元画像ピクセル座標）。</param>
/// <param name="Style">枠線スタイル。</param>
/// <param name="Foreground">前景色（透明度を含む）。</param>
/// <param name="Background">背景色（透明度を含む）。破線の市松風 2 色目。</param>
/// <param name="Thickness">枠線の太さ（ピクセル）。</param>
/// <remarks>
/// UI 実装（Avalonia / MewUI のクロップビュー）の <c>Rects</c> に設定すると、<br/>
/// コントロールが矩形を View（ビューポート）へ描画する（Avalonia 版はルーペ画像へも描画する）。<br/>
/// 破線（<see cref="OverlayLineStyle.Dashed"/> / <see cref="OverlayLineStyle.Dotted"/>）の場合は<br/>
/// <see cref="Foreground"/> と <see cref="Background"/> をダッシュごとに交互に塗る（市松風の視認性）。<br/>
/// <b>依存分離</b>: 座標は <see cref="EsRect"/>、色は <see cref="DrawingColor"/>（UI フレームワーク非依存）で扱い、<br/>
/// UI 境界で各 UI フレームワークの型へ変換する。<br/>
/// 破線のダッシュ長は <see cref="OverlayLineDash"/> が単一所有する（ベクター描画とラスタ描画で共有）。
/// </remarks>
public readonly record struct OverlayRect(
    EsRect Bounds, OverlayLineStyle Style, DrawingColor Foreground, DrawingColor Background, double Thickness);
