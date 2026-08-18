using Avalonia;
using Avalonia.Controls;

namespace Estable.AvaloniaUI.Platform;

/// <summary>コントロールローカル DIP と画面物理座標の変換を提供するヘルパー。</summary>
/// <remarks>
/// nudge のカーソル移動等の用途で、コントロールが属する <see cref="TopLevel"/> のスケーリングと画面上位置から変換する。<br/>
/// <see cref="TopLevel"/> は境界でのみ解決し、コントロール本体は本ヘルパーへ委譲する。<br/>
/// ビジュアルツリー未接続時は等価変換（画面座標をそのまま使用）する。<br/>
/// <br/>
/// OS 依存の画面物理変換（<see cref="TopLevel"/> の RenderScaling / 画面上位置 / PointToScreen / PointToClient）を
/// 集約する唯一の場所である。<br/>
/// ローカル DIP ↔ 画面物理の双方向変換は <see cref="CreateDipPort"/> / <see cref="CreateVisualDipPort"/> で<br/>
/// <see cref="ICoordinatePort"/> として提供する。<br/>
/// <br/>
/// 元は DragRectControl の内部実装（DragRectScreenConverter）として提供されていたが、<br/>
/// 汎用的な画面座標変換のため共有プラットフォーム（本プロジェクト）の Coordinate へ集約した。
/// </remarks>
public static class ScreenConverter
{
    /// <summary>コントロールローカル DIP を画面物理座標に変換する。</summary>
    /// <param name="source">変換元のコントロール。</param>
    /// <param name="local">コントロールローカル DIP 座標。</param>
    /// <returns>画面物理座標。</returns>
    /// <remarks><see cref="TopLevel.PointToScreen"/> に委譲する。</remarks>
    public static PixelPoint LocalDipToScreen(Visual source, Point local)
    {
        var topLevel = TopLevel.GetTopLevel(source);
        if (topLevel is null) return new PixelPoint((int)local.X, (int)local.Y);
        var inTopLevel = VisualExtensions.TranslatePoint(source, local, topLevel) ?? local;
        return topLevel.PointToScreen(inTopLevel);
    }

    /// <summary>画面物理座標をコントロールローカル DIP に変換する。</summary>
    /// <param name="source">変換元のコントロール。</param>
    /// <param name="screen">画面物理座標。</param>
    /// <returns>コントロールローカル DIP 座標。</returns>
    /// <remarks>
    /// <see cref="TopLevel.PointToClient"/> と <see cref="VisualExtensions.TranslatePoint"/> に委譲する。<br/>
    /// ビジュアルツリー未接続時は等価変換（画面座標をそのまま返す）。
    /// </remarks>
    public static Point ScreenToLocalDip(Visual source, PixelPoint screen)
    {
        var topLevel = TopLevel.GetTopLevel(source);
        if (topLevel is null) return new Point(screen.X, screen.Y);
        var clientDip = topLevel.PointToClient(screen);
        var origin = VisualExtensions.TranslatePoint(source, default, topLevel) ?? default;
        return clientDip - origin;
    }

    /// <summary>ローカル DIP ↔ 画面物理ピクセル変換ポートを生成する。</summary>
    /// <param name="origin">ローカル座標系の原点の画面物理座標（例: 親パネルの画面上位置・ウィンドウ位置）。</param>
    /// <param name="renderScaling">DIP → 物理のスケーリング（<see cref="TopLevel.RenderScaling"/>。0 以下の場合は 1.0 として扱う）。</param>
    /// <returns>Forward = 物理→DIP、Inverse = DIP→物理のポート。</returns>
    /// <remarks>
    /// <c>Forward(p) = (p - origin) / renderScaling</c>、<c>Inverse(p) = p × renderScaling + origin</c> を提供する。<br/>
    /// Loupe の画面物理位置計算・カーソル位置の DIP 変換など、<see cref="TopLevel"/> 由来の<br/>
    /// スケーリングと原点を持つ座標変換を <see cref="ICoordinatePort"/> として集約する。
    /// </remarks>
    public static ICoordinatePort CreateDipPort(PixelPoint origin, double renderScaling)
    {
        double s = renderScaling > 0 ? renderScaling : 1.0;
        return new CoordinatePort(
            p => new Point((p.X - origin.X) / s, (p.Y - origin.Y) / s),
            p => new Point(p.X * s + origin.X, p.Y * s + origin.Y),
            () => null);
    }

    /// <summary>コントロールローカル DIP ↔ 画面物理ピクセル変換ポートを生成する。</summary>
    /// <param name="source">変換元のコントロール。</param>
    /// <returns>Forward = 画面物理→コントロールローカル DIP、Inverse = コントロールローカル DIP→画面物理のポート。</returns>
    /// <remarks>
    /// <see cref="TopLevel"/> を呼び出し毎に解決するため、ウィンドウ移動・スクロールの動的変化を反映できる。<br/>
    /// ビジュアルツリー未接続時は等価変換（画面座標 = ローカル DIP）する。<br/>
    /// nudge のカーソル位置変換（DragRectControl）等で使用する（単一実装・DRY 原則）。
    /// </remarks>
    public static ICoordinatePort CreateVisualDipPort(Visual source)
        => new CoordinatePort(
            p => ScreenToLocalDip(source, new PixelPoint((int)p.X, (int)p.Y)),
            p =>
            {
                // 同一スコープの同値計算は一度だけ実行して共有する（TopLevel 解決コストを繰り返さない）
                var screen = LocalDipToScreen(source, p);
                return new Point(screen.X, screen.Y);
            },
            () => null);
}
