using Avalonia;
using Avalonia.Media;

namespace Estable.AvaloniaUI.CropView;

/// <summary>オーバーレイとして表示する矩形の描画情報。</summary>
/// <param name="Bounds">矩形（ドメイン座標 = 元画像ピクセル座標）。</param>
/// <param name="Style">枠線スタイル。</param>
/// <param name="Foreground">前景色（透明度を含む）。</param>
/// <param name="Background">背景色（透明度を含む）。破線の市松風 2 色目。</param>
/// <param name="Thickness">枠線の太さ（ピクセル）。</param>
/// <remarks>
/// ホスト（上位 AP）から <see cref="CropViewControl.Rects"/> に設定することで、<br/>
/// <see cref="CropViewControl"/> が矩形を View（ビューポート）とルーペ画像の両方へ描画する。<br/>
/// 破線（<see cref="OverlayLineStyle.Dashed"/> / <see cref="OverlayLineStyle.Dotted"/>）の場合は<br/>
/// <see cref="Foreground"/> と <see cref="Background"/> をダッシュごとに交互に塗る（市松風の視認性）。
/// </remarks>
public readonly record struct OverlayRect(
    Rect Bounds, OverlayLineStyle Style, Color Foreground, Color Background, double Thickness);
