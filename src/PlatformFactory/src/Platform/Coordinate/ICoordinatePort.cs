using Avalonia;

namespace Estable.AvaloniaUI.Platform;

/// <summary>2 つの座標系間の変換ポート（順変換・逆変換・境界）。</summary>
/// <remarks>
/// 座標系 A ↔ 座標系 B の双方向変換と、座標系 B の境界（クリップ範囲）を提供する汎用ポート（DIP）。<br/>
/// 具象実装はホスト（上位 AP）が変換関数と境界を提供して生成する（<see cref="CoordinatePort"/>）。<br/>
/// <br/>
/// 用途例:<br/>
/// - DragRectControl: ドメインピクセル ↔ コントロールローカル DIP（Forward = DIP→ドメイン / Inverse = ドメイン→DIP）<br/>
/// - 画像プレビュー: 元画像ピクセル（論理座標） ↔ 表示 DIP（Forward = DIP→論理 / Inverse = 論理→DIP）<br/>
/// <br/>
/// 変換関数は呼び出し毎に評価されるため、ズーム・スクロール等の動的変化を反映できる。<br/>
/// nudge 等の精度保証が必要な計算は<b>座標系 B の整数加算のみ</b>で行い、境界でのみ本ポートで変換する。
/// </remarks>
public interface ICoordinatePort
{
    /// <summary>順変換（座標系 A → 座標系 B）。</summary>
    /// <param name="p">座標系 A の点。</param>
    /// <returns>座標系 B の点。</returns>
    Point Forward(Point p);

    /// <summary>逆変換（座標系 B → 座標系 A）。</summary>
    /// <param name="p">座標系 B の点。</param>
    /// <returns>座標系 A の点。</returns>
    Point Inverse(Point p);

    /// <summary>座標系 B の境界（クリップ範囲）。null = 無制限。</summary>
    Size? Bounds { get; }
}
