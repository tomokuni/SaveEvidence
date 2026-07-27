using Avalonia;
using Avalonia.Platform;

namespace Estable.AvaloniaUI.Platform;

/// <summary>要素（スクリーン・ウィンドウ）検出の結果。</summary>
/// <param name="Bounds">検出した要素の矩形（画面物理座標）。</param>
/// <param name="IsDetected">要素を検出できたかどうか。</param>
/// <param name="Title">検出した要素の表示名。ウィンドウ検出時はウィンドウタイトル、スクリーン検出時は「Screen {インデックス}」。未検出時は null。</param>
/// <param name="Handle">検出した要素の識別子（ウィンドウ検出時のみ設定。将来の PrintWindow 等のキャプチャで使用。スクリーンは座標キャプチャのため不要）。</param>
/// <remarks>
/// 指定された位置（画面物理座標）のスクリーン / 最前面ウィンドウを判定した結果を保持する不変データ。<br/>
/// 座標系は入力と同じ<b>画面物理座標</b>で統一される（座標変換は呼び出し側の責務）。<br/>
/// 未検出時は <see cref="Bounds"/> = default、<see cref="IsDetected"/> = false、<see cref="Title"/> / <see cref="Handle"/> = null。<br/>
/// <see cref="IElementFinder"/> の実装（WinElementFinder 等）が返す。
/// </remarks>
public readonly record struct ElementFinderResults(
    PixelRect Bounds,
    bool IsDetected,
    string? Title = null,
    IPlatformHandle? Handle = null);
