using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Capture;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.Crop;

/// <summary>仮想スクリーン画像からの切り出し計算（純粋関数）を単一所有する。</summary>
/// <remarks>
/// 画面物理座標の切り出し矩形を、仮想スクリーン画像（Bitmap）のローカル座標へ変換して切り出す手順は<br/>
/// UI フレームワークに依存しない規則であるため、本クラスが単一所有する<br/>
/// （UI ごとに「原点の減算」を書くと、マルチモニタ（原点が負値）で切り出し位置がずれる）。<br/>
/// <c>EsReadOnlyBitmap.Crop(int, int, int, int)</c> はピクセル読み取りのみで、バックグラウンドスレッドから安全に呼び出せる。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public static class CropClipMath
{
    /// <summary>画面物理座標の切り出し矩形を画像ローカル座標へ変換する。</summary>
    /// <param name="captureRect">切り出し矩形（画面物理ピクセル・閉区間）。</param>
    /// <param name="origin">仮想スクリーン画像の原点（画面物理ピクセル。 画像の左上が対応する座標）。</param>
    /// <returns>画像ローカル座標の矩形。</returns>
    /// <remarks>画像ローカル座標 = 画面物理座標 − 原点。 原点が負値（プライマリ以外が左・上）でも同じ式で扱える。</remarks>
    public static EsRectInt ToLocalRect(EsRectInt captureRect, EsPointInt origin)
        => new(
            captureRect.X - origin.X,
            captureRect.Y - origin.Y,
            captureRect.Width,
            captureRect.Height);

    /// <summary>画像ローカル座標の矩形を画面物理座標へ変換する（<see cref="ToLocalRect(EsRect, EsPointInt)"/> の逆変換）。</summary>
    /// <param name="localRect">画像ローカル座標の矩形。</param>
    /// <param name="origin">画像の原点（画面物理ピクセル）。</param>
    /// <returns>画面物理座標の矩形。</returns>
    /// <remarks>対象（画像ローカル）で保持している選択矩形を画面物理座標へ戻すときに使う（双方向の対称性を保つ）。<br/>
    /// 寸法は変わらないため区間の流儀（<see cref="EsRect.Interval"/>）はそのまま引き継ぐ。
    /// </remarks>
    public static EsRect ToScreenRect(EsRect localRect, EsPointInt origin)
        => new(
            localRect.X + origin.X,
            localRect.Y + origin.Y,
            localRect.Width,
            localRect.Height,
            localRect.Interval);

    /// <summary>画面物理座標の矩形を画像ローカル座標へ変換する（浮動小数版）。</summary>
    /// <param name="captureRect">画面物理座標の矩形。</param>
    /// <param name="origin">画像の原点（画面物理ピクセル）。</param>
    /// <returns>画像ローカル座標の矩形。</returns>
    /// <remarks>選択矩形（浮動小数）を画像ローカル座標へ戻して描画へ渡すときに使う。<br/>
    /// 寸法は変わらないため区間の流儀（<see cref="EsRect.Interval"/>）はそのまま引き継ぐ。
    /// </remarks>
    public static EsRect ToLocalRect(EsRect captureRect, EsPointInt origin)
        => new(
            captureRect.X - origin.X,
            captureRect.Y - origin.Y,
            captureRect.Width,
            captureRect.Height,
            captureRect.Interval);

    /// <summary>画像ローカル座標の整数矩形を画面物理座標へ変換する（<see cref="ToLocalRect(EsRectInt, EsPointInt)"/> の逆変換）。</summary>
    /// <param name="localRect">画像ローカル座標の矩形（整数ピクセル）。</param>
    /// <param name="origin">画像の原点（画面物理ピクセル）。</param>
    /// <returns>画面物理座標の矩形。</returns>
    /// <remarks>画面物理座標へ戻す計算を UI ごとに書かないよう、整数版の対称な変換として提供する。</remarks>
    public static EsRectInt ToScreenRect(EsRectInt localRect, EsPointInt origin)
        => new(
            localRect.X + origin.X,
            localRect.Y + origin.Y,
            localRect.Width,
            localRect.Height);

    /// <summary>画像のピクセルサイズを取得する。</summary>
    /// <param name="image">対象の画像。</param>
    /// <returns>画像のピクセルサイズ（コアの値型）。</returns>
    /// <remarks>コアの型同士の変換はコアが単一所有する（UI が <c>new EsSize(image.Width, image.Height)</c> を書かない）。</remarks>
    public static EsSize ImageSize(EsReadOnlyBitmap image) => new(image.Width, image.Height);

    /// <summary>画像の占有範囲（原点とサイズ）を求める。</summary>
    /// <param name="image">対象の画像。</param>
    /// <param name="origin">画像の左上が対応する画面物理座標。</param>
    /// <returns>画像の占有範囲（画面物理座標）。</returns>
    /// <remarks>
    /// 原点が負値（仮想スクリーンの左上がプライマリより外側）でも同じ式で扱える。<br/>
    /// 占有範囲は切り出しのクランプ境界になるため、UI が幅・高さを組み立てないよう本メソッドが単一所有する。
    /// </remarks>
    public static EsRectInt ImageBounds(EsReadOnlyBitmap image, EsPointInt origin)
        => new(origin.X, origin.Y, image.Width, image.Height);

    /// <summary>切り出し元の画像が切り出せる（サイズが正）であることを検証する。</summary>
    /// <param name="image">検証する画像。</param>
    /// <param name="paramName">例外に含める引数名。</param>
    /// <exception cref="ArgumentException">幅または高さが 0 以下の場合。</exception>
    /// <remarks>
    /// 「サイズが 0 の画像は切り出せない」という規則を単一所有する（公開 API の前提条件として検証する）。<br/>
    /// イベントハンドラから呼ばれる経路では例外を投げずに何もしない（呼び出し側が可否を判断する）。
    /// </remarks>
    public static void ValidateImage(EsReadOnlyBitmap image, string paramName)
    {
        if (image.Width <= 0 || image.Height <= 0)
        {
            throw new ArgumentException("画像のサイズが 0 です。", paramName);
        }
    }

    /// <summary>選択矩形（画面物理座標）から切り出し矩形（画面物理座標）を求める。</summary>
    /// <param name="captureType">キャプチャ種別。</param>
    /// <param name="screenRect">選択矩形（画面物理座標・整数ピクセル）。</param>
    /// <param name="imageBounds">切り出し元画像の占有範囲（画面物理座標）。 原点が負値でもよい。</param>
    /// <returns>切り出し矩形（画面物理ピクセル）。 切り出せない場合は <see cref="EsRectInt.IsEmpty"/> が true。</returns>
    /// <remarks>
    /// 検出矩形（クリック確定・ホバー）は整数で与えられるため、浮動小数版と同じ規則を適用する本オーバーロードを提供する<br/>
    /// （UI が整数矩形を浮動小数矩形へ広げる変換を書かないため）。
    /// </remarks>
    public static EsRectInt ResolveCaptureRect(CaptureType captureType, EsRectInt screenRect, EsRectInt imageBounds)
        => ResolveCaptureRect(
            captureType,
            new EsRect(screenRect.X, screenRect.Y, screenRect.Width, screenRect.Height),
            imageBounds);

    /// <summary>選択矩形（画面物理座標）から切り出し矩形（画面物理座標）を求める。</summary>
    /// <param name="captureType">キャプチャ種別。</param>
    /// <param name="screenRect">選択矩形（画面物理座標）。</param>
    /// <param name="imageBounds">切り出し元画像の占有範囲（画面物理座標）。 原点が負値でもよい。</param>
    /// <returns>切り出し矩形（画面物理ピクセル）。 切り出せない場合は <see cref="EsRectInt.IsEmpty"/> が true。</returns>
    /// <remarks>
    /// 1. 選択矩形を画像ローカル座標へ変換する（原点の減算）。<br/>
    /// 2. 種別が決める区間の流儀（<see cref="CaptureTypeTraits.ResolveInterval"/>）を矩形へ付け、<br/>
    ///    ドラッグ選択（<see cref="CaptureType.AreaSelect"/>）は終点のピクセルを含める（ハイライト型の検出矩形は全ピクセルを含むため加算しない）。<br/>
    /// 3. 端数を丸めて画像の範囲へクランプする（画像の右端・下端で 1 ピクセルはみ出すと、切り出し結果の端に透明な余白が付く）。<br/>
    /// 4. 画面物理座標へ戻す（原点の加算）。<br/>
    /// <br/>
    /// 最小サイズは判定しない（選択操作そのものの可否は <c>MarqueeOptions.MinSelectionSize</c> が所有する。<br/>
    /// 検出矩形は要素の大きさであり、小さい要素も切り出し対象とする）。<br/>
    /// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
    /// </remarks>
    public static EsRectInt ResolveCaptureRect(CaptureType captureType, EsRect screenRect, EsRectInt imageBounds)
    {
        var origin = imageBounds.TopLeft;
        var local = ToLocalRect(screenRect, origin).WithInterval(CaptureTypeTraits.ResolveInterval(captureType));
        var crop = CropBoundsMath.ComputeCropBounds(
            local,
            new EsSize(imageBounds.Width, imageBounds.Height),
            minSelectionSize: 0);

        return crop.IsEmpty ? default : ToScreenRect(crop, origin);
    }

    /// <summary>仮想スクリーン画像から切り出し矩形の領域を取り出す。</summary>
    /// <param name="virtualScreen">仮想スクリーン画像。 null の場合は切り出さない。</param>
    /// <param name="origin">仮想スクリーン画像の原点（画面物理ピクセル）。</param>
    /// <param name="captureRect">切り出し矩形（画面物理ピクセル・閉区間）。</param>
    /// <returns>切り出した画像（書き換え可能）。 切り出せない場合（未取得・破棄済み）は null。</returns>
    /// <remarks>
    /// 仮想スクリーン未取得（切り取り元が無い）場合は null を返し、呼び出し側が代替の取得経路<br/>
    /// （領域指定の画面キャプチャ）へフォールバックできるようにする。<br/>
    /// <b>破棄済みの画像も null</b>とする（ウィンドウクローズと背景の切り出しが競合するため、<br/>
    /// 「破棄済みは切り出せない」規則を本クラスが単一所有し、UI が例外を捕まえない）。<br/>
    /// 取り出した画像の所有権は呼び出し側へ移る。
    /// </remarks>
    public static EsBitmap? Extract(EsReadOnlyBitmap? virtualScreen, EsPointInt origin, EsRectInt captureRect)
    {
        if (virtualScreen is not { } source)
        {
            return null;
        }

        try
        {
            var local = ToLocalRect(captureRect, origin);
            return source.Crop(local.X, local.Y, local.Width, local.Height);
        }
        catch (ObjectDisposedException)
        {
            // 破棄済み（ウィンドウクローズとの競合）: 切り出せないため null
            return null;
        }
    }
}

