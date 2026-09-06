namespace Estable.AvaloniaUI.Platform;

/// <summary>座標変換の純粋計算ヘルパー（プラットフォーム共通・単一実装・非公開）。</summary>
/// <remarks>
/// 画像プレビュー等の表示レイアウトに関わる座標計算を提供する。<br/>
/// 純粋演算のみを提供し、UI 要素・状態に依存しない（単体テスト容易）。<br/>
/// <b>公開 API ではない</b>: 座標変換は <see cref="ICoordTransform"/>（<see cref="CoordTransform"/> の
/// <c>ImageLayout</c> ファクトリ / <c>ScreenConverter</c>）を通して行う（カプセル化）。<br/>
/// 本クラスは Media 内部の単一実装として、各ポートが利用する（DRY 原則）。
/// </remarks>
internal static class CoordMath
{
    /// <summary>Stretch.Uniform での表示サイズとレターボックスオフセットを算出する。</summary>
    /// <param name="viewportWidth">表示領域の幅（DIP）。</param>
    /// <param name="viewportHeight">表示領域の高さ（DIP）。</param>
    /// <param name="sourceWidth">元画像の幅（ピクセル）。</param>
    /// <param name="sourceHeight">元画像の高さ（ピクセル）。</param>
    /// <returns>表示幅（DIP）、表示高さ（DIP）、オフセット X（DIP）、オフセット Y（DIP）。</returns>
    /// <remarks>
    /// アスペクト比を維持して表示領域に収まるサイズと、余白（レターボックス）のオフセットを算出する。<br/>
    /// 純粋演算であり、UI 要素に依存しない（各プロジェクトの単一実装・DRY 原則）。
    /// </remarks>
    public static (double Width, double Height, double OffsetX, double OffsetY) CalculateUniformLayout(
        double viewportWidth, double viewportHeight, double sourceWidth, double sourceHeight)
    {
        double a = sourceWidth / sourceHeight, da = viewportWidth / viewportHeight;
        if (da > a)
        {
            // 表示領域が横長 → 高さでフィット
            double rh = viewportHeight;
            double rw = viewportHeight * a;
            return (rw, rh, (viewportWidth - rw) / 2.0, 0.0);
        }
        else
        {
            // 表示領域が縦長または同一 → 幅でフィット
            double rw = viewportWidth;
            double rh = viewportWidth / a;
            return (rw, rh, 0.0, (viewportHeight - rh) / 2.0);
        }
    }

    /// <summary>画像表示領域の描画情報（表示サイズ・レターボックスオフセット・スケール）を算出する。</summary>
    /// <param name="viewportWidth">表示領域の幅（DIP）。</param>
    /// <param name="viewportHeight">表示領域の高さ（DIP）。</param>
    /// <param name="sourceWidth">元画像の幅（ピクセル）。</param>
    /// <param name="sourceHeight">元画像の高さ（ピクセル）。</param>
    /// <param name="uniform">Stretch.Uniform 相当（true = アスペクト比維持、false = Fill 相当）。</param>
    /// <returns>表示幅（DIP）、表示高さ（DIP）、オフセット X（DIP）、オフセット Y（DIP）、スケール X（元画像ピクセル/DIP）、スケール Y。</returns>
    /// <remarks>
    /// <see cref="CalculateUniformLayout"/> にスケール係数（元画像ピクセル ÷ 表示 DIP）の導出を加えた<br/>
    /// 表示レイアウト計算の一元化 API である（各プロジェクトの座標変換で共通利用する・DRY 原則）。<br/>
    /// 表示領域・元画像サイズのいずれかが 0 以下の場合は等倍（sx = sy = 1.0）を返す。<br/>
    /// プレビューコントロールの表示状態（Bounds / Stretch）の取得は呼び出し側の責務であり、<br/>
    /// 本メソッドは純粋演算のみを提供する（UI 要素に依存しない・単体テスト容易）。
    /// </remarks>
    public static (double Width, double Height, double OffsetX, double OffsetY, double ScaleX, double ScaleY) ComputeImageRenderInfo(
        double viewportWidth, double viewportHeight, double sourceWidth, double sourceHeight, bool uniform = true)
    {
        double dw = viewportWidth, dh = viewportHeight, sw = sourceWidth, sh = sourceHeight;
        if (dw <= 0 || dh <= 0 || sw <= 0 || sh <= 0) return (dw, dh, 0.0, 0.0, 1.0, 1.0);

        var (rw, rh, ox, oy) = uniform
            ? CalculateUniformLayout(dw, dh, sw, sh)
            : (dw, dh, 0.0, 0.0);
        return (rw, rh, ox, oy, sw / Math.Max(rw, 1), sh / Math.Max(rh, 1));
    }

    /// <summary>指定点をスクロールオフセット分平行移動する（純粋関数）。</summary>
    /// <param name="x">X 座標。</param>
    /// <param name="y">Y 座標。</param>
    /// <param name="offsetX">X オフセット。</param>
    /// <param name="offsetY">Y オフセット。</param>
    /// <returns>オフセット加算（移動）後の座標。</returns>
    public static (double X, double Y) OffsetPoint(double x, double y, double offsetX, double offsetY)
        => (x - offsetX, y - offsetY);

    /// <summary>指定点を逆方向にスクロールオフセット分平行移動する（純粋関数）。</summary>
    /// <param name="x">X 座標。</param>
    /// <param name="y">Y 座標。</param>
    /// <param name="offsetX">X オフセット。</param>
    /// <param name="offsetY">Y オフセット。</param>
    /// <returns>オフセット減算（逆移動）後の座標。</returns>
    public static (double X, double Y) InverseOffsetPoint(double x, double y, double offsetX, double offsetY)
        => (x + offsetX, y + offsetY);

    /// <summary>指定点を等方スケーリングする（純粋関数・原点基準）。</summary>
    /// <param name="x">X 座標。</param>
    /// <param name="y">Y 座標。</param>
    /// <param name="factor">スケール倍率（Forward = 除算 / Inverse = 乗算）。</param>
    /// <returns>スケーリング後の座標。</returns>
    public static (double X, double Y) ScalePoint(double x, double y, double factor)
        => (x / factor, y / factor);

    /// <summary>指定点を逆方向に等方スケーリングする（純粋関数・原点基準）。</summary>
    /// <param name="x">X 座標。</param>
    /// <param name="y">Y 座標。</param>
    /// <param name="factor">スケール倍率。</param>
    /// <returns>スケーリング後の座標。</returns>
    public static (double X, double Y) InverseScalePoint(double x, double y, double factor)
        => (x * factor, y * factor);
}
