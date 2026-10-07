using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using DrawingColor = System.Drawing.Color;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Crop;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.SelectionOverlay;

/// <summary>ルーペ画像上へ選択矩形の破線オーバーレイを描画する静的クラス。</summary>
/// <remarks>
/// 選択矩形（対象座標）をルーペ画像ピクセル座標へ変換し、4 辺を 2 色の破線で描画する。<br/>
/// 破線の幾何（位置・長さ・色の種別）は <see cref="EnumerateDashes"/> が単一所有し、<br/>
/// 描画先（<see cref="EsBitmap"/> へ直接書き込む / 描画 API へ渡す）は呼び出し側が選べる<br/>
/// （Avalonia 版は描画完了イベント経由で <see cref="EsBitmap"/> へ、MewUI 版は描画直後にキャンバスへ描く）。<br/>
/// ウィンドウを必要としない純粋計算のため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
internal static class SelectionOutlineRasterizer
{
    /// <summary>破線の描画長（ピクセル）。</summary>
    private const int DashLength = 1;

    /// <summary>破線の空白長（ピクセル）。</summary>
    private const int GapLength = 1;

    /// <summary>破線を描画する最小のズーム倍率（1 倍では破線が潰れるため描画しない）。</summary>
    private const double MinimumScale = 2.0;

    /// <summary>選択矩形の 4 辺を 2 色の破線でルーペ画像へ描画する。</summary>
    /// <param name="bitmap">描画対象の画像（ルーペの表示画像）。</param>
    /// <param name="selectionSourceRect">選択矩形（対象座標系・半開区間）。</param>
    /// <param name="captureTopLeft">ルーペの切り出し領域の左上（対象座標系）。</param>
    /// <param name="scale">対象座標 → ルーペ画像ピクセル座標の変換係数。</param>
    /// <param name="colorA">破線の色 A（描画開始色）。</param>
    /// <param name="colorB">破線の色 B（色 A と交互に使用する）。</param>
    /// <remarks>
    /// 1. 破線の列挙は <see cref="EnumerateDashes"/> が単一所有する（MewUI 版と同じ幾何を使う）。<br/>
    /// 2. 画像外へはみ出す画素は無視する（例外にしない）。
    /// </remarks>
    public static void DrawSelectionRect(
        EsBitmap bitmap, EsRect selectionSourceRect, EsPoint captureTopLeft, double scale, DrawingColor colorA, DrawingColor colorB)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        foreach (var dash in EnumerateDashes(selectionSourceRect, captureTopLeft, scale, bitmap.Size.Width))
        {
            var color = dash.IsColorA ? colorA : colorB;

            // 範囲外は EsBitmap.TrySetPixel が無視する（画像端をはみ出す矩形でも例外にしない）
            for (var offset = 0; offset < dash.Width; offset++)
            {
                for (var row = 0; row < dash.Height; row++)
                {
                    bitmap.TrySetPixel(dash.X + offset, dash.Y + row, color);
                }
            }
        }
    }

    /// <summary>選択矩形の 4 辺の破線を 1 ピクセル厚の矩形として列挙する。</summary>
    /// <param name="selectionSourceRect">選択矩形（対象座標系。 区間の流儀（<see cref="EsRect.Interval"/>）で覆う画素数が決まる）。</param>
    /// <param name="captureTopLeft">ルーペの切り出し領域の左上（対象座標系）。</param>
    /// <param name="scale">対象座標 → ルーペ画像ピクセル座標の変換係数。</param>
    /// <param name="pixelSize">ルーペ画像の一辺のサイズ（ピクセル）。 切り出し領域との交差判定に使用する。 0 以下の場合は交差判定しない。</param>
    /// <returns>破線の一覧（上辺 → 下辺 → 左辺 → 右辺の順。 色は辺ごとに色 A から始まり交互に切り替わる）。</returns>
    /// <remarks>
    /// 1. 倍率が <see cref="MinimumScale"/> 未満の場合は空を返す（破線が潰れるため）。<br/>
    /// 2. 選択矩形（対象座標）をルーペ画像ピクセル座標へ変換する。 覆う画素数は区間の流儀が単一所有する<br/>
    /// （閉区間は終点を含むため 1 画素多い。 本メソッドは再判定しない）。<br/>
    /// 3. 切り出し領域と交差しない場合は空を返す。<br/>
    /// 4. <b>破線は選択が覆う画素の外側（+1 画素）へ引く</b>。<br/>
    /// ルーペの十字線はドラッグ中の点（＝選択の角）を指し、その画素から拡大率ぶんの幅を持つため、<br/>
    /// 破線を覆う画素の内側へ引くと十字線の帯に重なる（終点が左上のときに重なって見える）。<br/>
    /// 外側へ引けば<b>ドラッグの向きで見え方が変わらない</b>（常に十字線の帯の外側に接する）。<br/>
    /// 5. 各辺を 1 ピクセル描画 → 1 ピクセル空白の周期で列挙する（色は 1 ダッシュごとに交互に切り替える）。
    /// </remarks>
    public static IReadOnlyList<Dash> EnumerateDashes(
        EsRect selectionSourceRect, EsPoint captureTopLeft, double scale, int pixelSize)
    {
        if (scale < MinimumScale)
        {
            return [];
        }

        // 対象座標 → ルーペ画像ピクセル座標（覆う画素数に直す際の +1 は区間の流儀が単一所有する）
        double x = (selectionSourceRect.X - captureTopLeft.X) * scale;
        double y = (selectionSourceRect.Y - captureTopLeft.Y) * scale;
        double coveredWidth = PixelCount(selectionSourceRect.Width, selectionSourceRect.Interval) * scale;
        double coveredHeight = PixelCount(selectionSourceRect.Height, selectionSourceRect.Interval) * scale;

        // 選択が覆う画素の外側（左上は 1 画素外側、右下は覆う画素の終端 = どちらも 1 画素外側）
        double left = x - 1;
        double top = y - 1;
        double right = x + coveredWidth;
        double bottom = y + coveredHeight;

        // ルーペの切り出し領域（正方形）と交差しない場合は描画しない
        if (right < 0 || bottom < 0 || left > pixelSize || top > pixelSize)
        {
            return [];
        }

        var dashes = new List<Dash>(8);
        AppendEdge(dashes, new EsPoint(left, top), new EsPoint(right, top));
        AppendEdge(dashes, new EsPoint(left, bottom), new EsPoint(right, bottom));
        AppendEdge(dashes, new EsPoint(left, top), new EsPoint(left, bottom));
        AppendEdge(dashes, new EsPoint(right, top), new EsPoint(right, bottom));
        return dashes;
    }

    /// <summary>矩形の幅が覆う画素数を取得する。</summary>
    /// <param name="width">矩形の幅（対象座標）。</param>
    /// <param name="interval">区間の流儀。</param>
    /// <returns>覆う画素数。</returns>
    /// <remarks>
    /// <c>+1</c> の判断は区間の流儀が単一所有する（<see cref="EsRectInterval.Closed"/> は終点を含むため 1 画素多い）。<br/>
    /// 下流の層が再判定しないよう、本メソッドが唯一の入口になる。
    /// </remarks>
    private static double PixelCount(double width, EsRectInterval interval)
        => interval == EsRectInterval.Closed ? width + 1 : width;

    /// <summary>1 辺分の破線を列挙して追加する。</summary>
    /// <param name="dashes">追加先の一覧。</param>
    /// <param name="from">始点（画像ピクセル座標）。</param>
    /// <param name="to">終点（画像ピクセル座標）。</param>
    /// <remarks>
    /// 水平または垂直の辺を想定する。 色はダッシュごとに色 A → 色 B → 色 A … と交互に切り替える（辺ごとに色 A から始まる）。<br/>
    /// 座標が負の方向へ向かう辺（終点が始点より小さい）は破線を生成しない（矩形の幅・高さは常に 0 以上のため通常は発生しない）。
    /// </remarks>
    private static void AppendEdge(List<Dash> dashes, EsPoint from, EsPoint to)
    {
        bool horizontal = Math.Abs(to.Y - from.Y) < Math.Abs(to.X - from.X);
        double length = horizontal ? to.X - from.X : to.Y - from.Y;
        double start = horizontal ? from.X : from.Y;
        double fixedCoord = horizontal ? from.Y : from.X;
        double sign = length >= 0 ? 1 : -1;
        double distance = Math.Abs(length);
        const int step = DashLength + GapLength;

        int dashIndex = 0;
        for (double pos = 0; pos < distance; pos += step)
        {
            double segmentStart = start + sign * pos;
            double segmentEnd = start + sign * Math.Min(pos + DashLength, distance);
            // 画素座標の丸めは CropBoundsMath.RoundToPixel が単一所有する（0.5 は切り上げ。 十字線と同じ規則）
            int first = CropBoundsMath.RoundToPixel(segmentStart);
            int last = CropBoundsMath.RoundToPixel(segmentEnd);

            // 負の方向へ向かう辺はダッシュを生成しない（元のループ条件 px <= segmentEnd と同じ挙動）
            if (last < first)
            {
                continue;
            }

            var isColorA = dashIndex % 2 == 0;
            dashIndex++;

            int thicknessStart = CropBoundsMath.RoundToPixel(fixedCoord);
            dashes.Add(horizontal
                ? new Dash(first, thicknessStart, last - first + 1, 1, isColorA)
                : new Dash(thicknessStart, first, 1, last - first + 1, isColorA));
        }
    }

    /// <summary>破線の 1 ダッシュ（1 ピクセル厚の矩形と色の種別）。</summary>
    /// <param name="X">左上 X 座標（画像ピクセル）。</param>
    /// <param name="Y">左上 Y 座標（画像ピクセル）。</param>
    /// <param name="Width">幅（ピクセル。 最低 1）。</param>
    /// <param name="Height">高さ（ピクセル。 最低 1）。</param>
    /// <param name="IsColorA">true の場合は色 A、false の場合は色 B で描画する。</param>
    /// <remarks>水平な辺は高さ 1、垂直な辺は幅 1 になる。 画像の外側へはみ出す場合は描画側が無視する。</remarks>
    internal readonly record struct Dash(int X, int Y, int Width, int Height, bool IsColorA)
    {
        /// <summary>破線 1 個分の矩形（左上と寸法）を取得する。</summary>
        /// <value>破線の矩形（ピクセル座標）。</value>
        /// <remarks>描画層が Skia / Avalonia の矩形へ写すための入口（座標を 1 つずつ取り出さない）。</remarks>
        internal EsRect Bounds => new(X, Y, Width, Height);
    }
}
