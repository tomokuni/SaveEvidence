using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.Platform.Skia;
using SkiaSharp;


namespace EsUtil.SaveEvidence.Platform.MewUI.Imaging;

/// <summary><see cref="IEsBitmap"/> を Skia キャンバスへ 1 回の描画で敷く描画層。</summary>
/// <remarks>
/// <b>適用パターン</b>: 描画層（コントロールから描画手順を分離する）。<br/>
/// コントロールの <c>PaintSurface</c> から呼ぶ描画手順をコントロール内の private メソッドに置くと、<br/>
/// ウィンドウを必要とするため画素を検証できない。 本クラスへ分離するとキャンバスと画素だけで検証できる。<br/>
/// <br/>
/// <b>手順</b>:<br/>
/// 1. キャンバスを透明でクリアする（余白は背景をそのまま見せる）。<br/>
/// 2. 画像が無い・破棄済みの場合は描画しない。<br/>
/// 3. 配置の矩形（<see cref="ImageDrawMath"/>）が空の場合は描画しない。<br/>
/// 4. 矩形へ画像を 1 回の <c>DrawBitmap</c> で描く。<br/>
/// <br/>
/// <b>所有権</b>: 描画に使う Skia ビットマップは <see cref="IEsBitmap"/> のピクセルを指す<b>非所有ビュー</b>である。
/// ビュー自体は本クラスが作り、描画の直後に破棄する（コアのピクセルは破棄されない。 ビューは <see cref="SKBitmap"/> の
/// ファイナライザ対象のため、毎描画で残すと不要な GC 負荷になる）。
/// </remarks>
internal static class SkiaImageRenderer
{
    /// <summary><see cref="IEsBitmap"/> をキャンバス全体へ配置して描画する。</summary>
    /// <param name="canvas">描画先のキャンバス（左上を原点とする）。</param>
    /// <param name="info">キャンバスのピクセル情報（幅・高さを使用する）。</param>
    /// <param name="source">描画する画像。 null・破棄済みの場合は何も描画しない。</param>
    /// <param name="mode">配置方法（縦横比を保つ / 領域全体へ引き伸ばす）。</param>
    /// <remarks>
    /// 前提条件の判定（未設定・破棄済み・配置が空）をすべて本メソッドが担うため、<br/>
    /// 呼び出し側は判定を持たない。
    /// </remarks>
    public static void Render(SKCanvas canvas, SKImageInfo info, IEsBitmap? source, ImageDrawMode mode)
    {
        canvas.Clear(SKColors.Transparent);

        // 未設定・破棄済みの場合は描画しない（破棄済みの検出はコアの実体だけが持つ。
        // 読み取りの面は破棄状態を公開しないため、破棄済みは実体の型で判定する）
        if (source is not { } bitmap || bitmap is EsBitmapBase { IsDisposed: true })
        {
            return;
        }

        var destination = ImageDrawMath.ComputeDestination(
            bitmap.Width, bitmap.Height, info.Width, info.Height, mode);

        if (destination.IsEmpty)
        {
            return;
        }

        // ToSKBitmap は呼び出しごとに新しい非所有ビューを作る（ピクセルは複製しないが、SKBitmap は
        // ファイナライザを持つため、破棄しないと毎描画でファイナライザ キューへ積まれる）。
        // 描画は即時（DrawBitmap の呼び出しで完結する）ため、直後に破棄して構わない。
        using var view = bitmap.ToSKBitmap();
        canvas.DrawBitmap(view, destination.ToSKRect());
    }
}

