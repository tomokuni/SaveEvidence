using Avalonia;

namespace EsUtil.SaveEvidence.Parts.LineDetection.Sample.Models;

/// <summary>評価用データの保存形式（JSON シリアライズ用の不変データ）。</summary>
/// <remarks>
/// 線分検証・正解照合で読み込める形式で保存する。<br/>
/// <see cref="CaptureBounds"/> はキャプチャ領域（仮想画面座標）、<see cref="GroundTruthWindows"/> は
/// キャプチャ画像のローカル座標（X/Y はキャプチャ領域基準）の正解ウィンドウ矩形一覧。<br/>
/// 矩形は閉区間ベース（<see cref="ClosedIntervalRect"/>）で表す（始点・終点のピクセルを含む）。<br/>
/// 検証側では <c>PropertyNameCaseInsensitive = true</c> で読み込むことを前提とする。
/// </remarks>
public sealed record GroundTruthData(ClosedIntervalRect CaptureBounds, IReadOnlyList<GroundTruthWindow> GroundTruthWindows);

/// <summary>正解ウィンドウ 1 件分の情報（画像ローカル座標 + 識別情報）。</summary>
/// <param name="Bounds">ウィンドウ矩形（キャプチャ画像のローカル座標・閉区間ベース）。</param>
/// <param name="Title">ウィンドウタイトル。</param>
/// <param name="ClassName">ウィンドウクラス名。</param>
public sealed record GroundTruthWindow(ClosedIntervalRect Bounds, string Title, string ClassName);

/// <summary>閉区間ベースの矩形（始点・終点のピクセルを含む座標で表現）。</summary>
/// <remarks>
/// 幅・高さは「終点座標 − 始点座標」の座標差で表す（ピクセル数は幅 + 1）。<br/>
/// <see cref="Right"/> / <see cref="Bottom"/> は矩形に含まれる終点座標を返す。<br/>
/// アプリのキャプチャ・Crop は閉区間（始点・終点のピクセルを含む）を前提とするため、<br/>
/// 評価データ（JSON）も閉区間ベースで統一する。
/// </remarks>
public sealed record ClosedIntervalRect(int X, int Y, int Width, int Height)
{
    /// <summary>終点 X 座標（矩形に含まれる）。</summary>
    public int Right => X + Width;

    /// <summary>終点 Y 座標（矩形に含まれる）。</summary>
    public int Bottom => Y + Height;

    /// <summary>半開区間の PixelRect を閉区間ベースへ変換する。</summary>
    /// <param name="rect">半開区間の PixelRect（幅・高さ = ピクセル数）。</param>
    /// <returns>閉区間ベースの矩形（幅・高さ = 座標差）。</returns>
    /// <remarks>
    /// PixelRect の幅（ピクセル数）から 1 を引いて座標差へ変換する。<br/>
    /// 例: PixelRect(24, 21, 1095, 1193) → (24, 21, 1094, 1192)・Right = 1118
    /// </remarks>
    public static ClosedIntervalRect FromPixelRect(PixelRect rect)
        => new(rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
}
