using System.Drawing.Drawing2D;
using System.Globalization;

namespace EsUtil.SaveEvidence.App.WinForms.Icons;

/// <summary>SVG のパス データ（<c>d</c> 属性）を GDI+ のパスへ変換する。</summary>
/// <remarks>
/// <b>なぜ自前で解析するか</b>: 他の UI 版（Avalonia / MewUI / WinUI 3）は Fluent System Icons の SVG を<br/>
/// 共有して描くが、本 UI は GDI+ だけで描く方針であり、SVG の解析は GDI+ に無い。<br/>
/// 本クラスは<b>共有する SVG ファイルを唯一の出所にする</b>ために最小限の部分集合だけを解析する<br/>
/// （アイコンは <c>M</c> / <c>L</c> / <c>H</c> / <c>V</c> / <c>C</c> / <c>Z</c> の絶対コマンドだけで構成される）。<br/>
/// <br/>
/// <b>塗りの規則</b>: SVG の既定は <c>nonzero</c>（交差の向きで穴を表す）ため、GDI+ の<br/>
/// <see cref="FillMode.Winding"/> で塗る（<see cref="FillMode.Alternate"/> では穴が埋まる）。<br/>
/// <b>未対応のコマンド</b>（円弧 <c>A</c> 等）が現れた場合は解析を失敗させ、呼び出し側が空のパスで代替する<br/>
/// （誤った形を描くより、描かない方が安全である）。
/// </remarks>
internal static class SvgPathData
{
    /// <summary>SVG のパス データを GDI+ のパスへ変換する。</summary>
    /// <param name="data">パス データ（<c>d</c> 属性の値）。</param>
    /// <returns>変換したパス。 解析できない場合は空のパス（図形を含まない）。</returns>
    /// <remarks>例外は投げない（アイコンの欠落・書式の相違で UI を壊さないため）。 空のパスは描画しても何も現れない。</remarks>
    public static GraphicsPath Parse(string? data)
    {
        if (string.IsNullOrWhiteSpace(data))
        {
            return new GraphicsPath();
        }

        var path = new GraphicsPath(FillMode.Winding);

        try
        {
            Build(path, data);
        }
        catch (FormatException)
        {
            // 未対応・壊れたパス データは空のパスで代替する（描画は何も現れない）
            path.Dispose();
            return new GraphicsPath();
        }

        return path;
    }

    /// <summary>パス データを読み、図形を組み立てる。</summary>
    /// <param name="path">組み立て先のパス。</param>
    /// <param name="data">パス データ。</param>
    /// <exception cref="FormatException">未対応のコマンドが現れた場合、または数値を読み取れない場合。</exception>
    private static void Build(GraphicsPath path, string data)
    {
        // 現在位置（現在点）と部分パスの開始位置（Z で戻る先）
        double x = 0, y = 0, startX = 0, startY = 0;

        // 直前の 3 次ベジェの第 2 制御点（S の第 1 制御点はこの点の反射）
        double cubicX = 0, cubicY = 0;

        // 直前の 2 次ベジェの制御点（T の制御点はこの点の反射）
        double quadX = 0, quadY = 0;

        var index = 0;
        var lastCommand = '\0';

        while (true)
        {
            SkipSeparators(data, ref index);
            if (index >= data.Length)
            {
                break;
            }

            var command = data[index];
            if (!char.IsAsciiLetter(command))
            {
                throw new FormatException("コマンドの位置に文字以外があります。");
            }

            index++;
            var relative = char.IsAsciiLetterLower(command);
            switch (char.ToUpperInvariant(command))
            {
                case 'M':
                    ReadMoveTo(path, data, ref index, relative, ref x, ref y, ref startX, ref startY);
                    break;
                case 'L':
                    ReadLineTo(path, data, ref index, relative, ref x, ref y);
                    break;
                case 'H':
                    while (TryReadNumber(data, ref index, out var hx))
                    {
                        var nx = relative ? x + hx : hx;
                        AddLine(path, x, y, nx, y);
                        x = nx;
                    }

                    break;
                case 'V':
                    while (TryReadNumber(data, ref index, out var vy))
                    {
                        var ny = relative ? y + vy : vy;
                        AddLine(path, x, y, x, ny);
                        y = ny;
                    }

                    break;
                case 'C':
                    while (TryReadPoint(data, ref index, out var c1x, out var c1y))
                    {
                        if (!TryReadPoint(data, ref index, out var c2x, out var c2y)
                            || !TryReadPoint(data, ref index, out var ex, out var ey))
                        {
                            throw new FormatException("3 次ベジェの座標が不足しています。");
                        }

                        if (relative)
                        {
                            c1x += x; c1y += y; c2x += x; c2y += y; ex += x; ey += y;
                        }

                        path.AddBezier((float)x, (float)y, (float)c1x, (float)c1y, (float)c2x, (float)c2y, (float)ex, (float)ey);
                        cubicX = c2x; cubicY = c2y; x = ex; y = ey;
                    }

                    break;
                case 'S':
                    while (TryReadPoint(data, ref index, out var s2x, out var s2y))
                    {
                        if (!TryReadPoint(data, ref index, out var sex, out var sey))
                        {
                            throw new FormatException("3 次ベジェの座標が不足しています。");
                        }

                        if (relative)
                        {
                            s2x += x; s2y += y; sex += x; sey += y;
                        }

                        // 第 1 制御点は直前の第 2 制御点の現在点に対する反射（直前が 3 次でない場合は現在点）
                        var s1x = IsCurve(lastCommand, 'C', 'S') ? (2 * x) - cubicX : x;
                        var s1y = IsCurve(lastCommand, 'C', 'S') ? (2 * y) - cubicY : y;
                        path.AddBezier((float)x, (float)y, (float)s1x, (float)s1y, (float)s2x, (float)s2y, (float)sex, (float)sey);
                        cubicX = s2x; cubicY = s2y; x = sex; y = sey;
                    }

                    break;
                case 'Q':
                    while (TryReadPoint(data, ref index, out var qx, out var qy))
                    {
                        if (!TryReadPoint(data, ref index, out var qex, out var qey))
                        {
                            throw new FormatException("2 次ベジェの座標が不足しています。");
                        }

                        if (relative)
                        {
                            qx += x; qy += y; qex += x; qey += y;
                        }

                        AddQuadratic(path, x, y, qx, qy, qex, qey);
                        quadX = qx; quadY = qy; x = qex; y = qey;
                    }

                    break;
                case 'T':
                    while (TryReadPoint(data, ref index, out var tex, out var tey))
                    {
                        if (relative)
                        {
                            tex += x; tey += y;
                        }

                        // 制御点は直前の制御点の現在点に対する反射（直前が 2 次でない場合は現在点）
                        var tx = IsCurve(lastCommand, 'Q', 'T') ? (2 * x) - quadX : x;
                        var ty = IsCurve(lastCommand, 'Q', 'T') ? (2 * y) - quadY : y;
                        AddQuadratic(path, x, y, tx, ty, tex, tey);
                        quadX = tx; quadY = ty; x = tex; y = tey;
                    }

                    break;
                case 'Z':
                    path.CloseFigure();
                    x = startX;
                    y = startY;
                    break;
                default:
                    throw new FormatException("未対応のコマンドです。");
            }

            lastCommand = char.ToUpperInvariant(command);
        }
    }

    /// <summary>移動（<c>M</c>）を読み、以降の座標対を直線として読む。</summary>
    /// <param name="path">組み立て先のパス。</param>
    /// <param name="data">パス データ。</param>
    /// <param name="index">読み取り位置（進める）。</param>
    /// <param name="relative">現在点からの相対座標かどうか。</param>
    /// <param name="x">現在点の X（更新する）。</param>
    /// <param name="y">現在点の Y（更新する）。</param>
    /// <param name="startX">部分パスの開始 X（更新する）。</param>
    /// <param name="startY">部分パスの開始 Y（更新する）。</param>
    /// <exception cref="FormatException">座標を読み取れない場合。</exception>
    private static void ReadMoveTo(
        GraphicsPath path, string data, ref int index, bool relative,
        ref double x, ref double y, ref double startX, ref double startY)
    {
        if (!TryReadPoint(data, ref index, out var px, out var py))
        {
            throw new FormatException("移動先の座標がありません。");
        }

        if (relative)
        {
            px += x; py += y;
        }

        x = px;
        y = py;
        startX = px;
        startY = py;
        path.StartFigure();

        // 直後に座標対が続く場合は直線として読む（SVG の仕様）
        ReadLineTo(path, data, ref index, relative, ref x, ref y);
    }

    /// <summary>直線（<c>L</c>）を読み、連続する座標対をすべて読む。</summary>
    /// <param name="path">組み立て先のパス。</param>
    /// <param name="data">パス データ。</param>
    /// <param name="index">読み取り位置（進める）。</param>
    /// <param name="relative">現在点からの相対座標かどうか。</param>
    /// <param name="x">現在点の X（更新する）。</param>
    /// <param name="y">現在点の Y（更新する）。</param>
    private static void ReadLineTo(
        GraphicsPath path, string data, ref int index, bool relative, ref double x, ref double y)
    {
        while (TryReadPoint(data, ref index, out var px, out var py))
        {
            if (relative)
            {
                px += x; py += y;
            }

            AddLine(path, x, y, px, py);
            x = px;
            y = py;
        }
    }

    /// <summary>線分を追加する。</summary>
    /// <param name="path">対象のパス。</param>
    /// <param name="x0">始点の X。</param>
    /// <param name="y0">始点の Y。</param>
    /// <param name="x1">終点の X。</param>
    /// <param name="y1">終点の Y。</param>
    /// <remarks>
    /// <b>現在点は線分の始点が兼ねる</b>（GDI+ のパスは開始点を別に持たないため、<c>M</c> の直後に<br/>
    /// 線分を追加すると開始点が確定する）。 <c>M</c> を欠く不正なデータでも図形は線分から始まる。
    /// </remarks>
    private static void AddLine(GraphicsPath path, double x0, double y0, double x1, double y1)
        => path.AddLine((float)x0, (float)y0, (float)x1, (float)y1);

    /// <summary>2 次ベジェを 3 次ベジェへ変換して追加する。</summary>
    /// <param name="path">対象のパス。</param>
    /// <param name="x">始点の X。</param>
    /// <param name="y">始点の Y。</param>
    /// <param name="cx">制御点の X。</param>
    /// <param name="cy">制御点の Y。</param>
    /// <param name="ex">終点の X。</param>
    /// <param name="ey">終点の Y。</param>
    /// <remarks>GDI+ に 2 次ベジェが無いため、等価な 3 次ベジェ（制御点を 2/3 の位置へ置く）へ変換する。</remarks>
    private static void AddQuadratic(
        GraphicsPath path, double x, double y, double cx, double cy, double ex, double ey)
    {
        var c1x = x + ((2.0 / 3.0) * (cx - x));
        var c1y = y + ((2.0 / 3.0) * (cy - y));
        var c2x = ex + ((2.0 / 3.0) * (cx - ex));
        var c2y = ey + ((2.0 / 3.0) * (cy - ey));
        path.AddBezier((float)x, (float)y, (float)c1x, (float)c1y, (float)c2x, (float)c2y, (float)ex, (float)ey);
    }

    /// <summary>直前のコマンドが制御点を持つ指定の種別かどうかを判定する。</summary>
    /// <param name="lastCommand">直前のコマンド（大文字）。</param>
    /// <param name="first">対象の種別（例: <c>C</c>）。</param>
    /// <param name="second">対象の種別（例: <c>S</c>）。</param>
    /// <returns>該当する場合は true。</returns>
    private static bool IsCurve(char lastCommand, char first, char second)
        => lastCommand == first || lastCommand == second;

    /// <summary>区切り（空白・カンマ）を飛ばす。</summary>
    /// <param name="data">パス データ。</param>
    /// <param name="index">読み取り位置（進める）。</param>
    private static void SkipSeparators(string data, ref int index)
    {
        while (index < data.Length && (char.IsWhiteSpace(data[index]) || data[index] == ','))
        {
            index++;
        }
    }

    /// <summary>座標対を読み取る。</summary>
    /// <param name="data">パス データ。</param>
    /// <param name="index">読み取り位置（読み取れた場合は進める）。</param>
    /// <param name="x">読み取った X。</param>
    /// <param name="y">読み取った Y。</param>
    /// <returns>読み取れた場合は true。 次のトークンがコマンドの場合は false。</returns>
    /// <exception cref="FormatException">X だけを読み取れた場合（壊れたパス データ）。</exception>
    private static bool TryReadPoint(string data, ref int index, out double x, out double y)
    {
        x = 0;
        y = 0;

        var position = index;
        if (!TryReadNumber(data, ref position, out x))
        {
            return false;
        }

        if (!TryReadNumber(data, ref position, out y))
        {
            throw new FormatException("座標対の 2 つ目の値がありません。");
        }

        index = position;
        return true;
    }

    /// <summary>数値を読み取る。</summary>
    /// <param name="data">パス データ。</param>
    /// <param name="index">読み取り位置（読み取れた場合は進める）。</param>
    /// <param name="value">読み取った値。</param>
    /// <returns>読み取れた場合は true。 次のトークンがコマンドの場合は false。</returns>
    private static bool TryReadNumber(string data, ref int index, out double value)
    {
        value = 0;

        var position = index;
        SkipSeparators(data, ref position);

        var start = position;
        if (position < data.Length && (data[position] == '+' || data[position] == '-'))
        {
            position++;
        }

        var digits = 0;
        while (position < data.Length && char.IsAsciiDigit(data[position]))
        {
            position++;
            digits++;
        }

        if (position < data.Length && data[position] == '.')
        {
            position++;
            while (position < data.Length && char.IsAsciiDigit(data[position]))
            {
                position++;
                digits++;
            }
        }

        if (digits == 0)
        {
            return false;
        }

        // 指数表記（1e-3 等）も数値の一部として読む
        if (position < data.Length && (data[position] == 'e' || data[position] == 'E'))
        {
            var exponent = position + 1;
            if (exponent < data.Length && (data[exponent] == '+' || data[exponent] == '-'))
            {
                exponent++;
            }

            var exponentDigits = 0;
            while (exponent < data.Length && char.IsAsciiDigit(data[exponent]))
            {
                exponent++;
                exponentDigits++;
            }

            if (exponentDigits > 0)
            {
                position = exponent;
            }
        }

        if (!double.TryParse(
                data.AsSpan(start, position - start), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            throw new FormatException("数値を解釈できません。");
        }

        index = position;
        return true;
    }
}
