using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.App.Core.Enums;
using EsUtil.SaveEvidence.App.Core.ViewModels;

namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>起動直後のキャプチャ ページに置くプレースホルダ画像の生成。</summary>
/// <remarks>
/// 4 つの UI 版で同じ画像を使う（UI ごとに別の画像を作らない）。 画素は UI 非依存の<br/>
/// <see cref="EsBitmap"/> へ直接書くため、フレームワーク（Skia / GDI+）に依存しない。
/// </remarks>
public static class StartupPreviewImage
{
    /// <summary>プレースホルダの幅（ピクセル）。</summary>
    public const int Width = 640;

    /// <summary>プレースホルダの高さ（ピクセル）。</summary>
    public const int Height = 400;

    /// <summary>左上の色（ドジャー ブルー）。</summary>
    private const byte StartR = 30;
    private const byte StartG = 144;
    private const byte StartB = 255;

    /// <summary>右下の色（ダーク オレンジ）。</summary>
    private const byte EndR = 255;
    private const byte EndG = 140;
    private const byte EndB = 0;

    /// <summary>斜めのグラデーション画像を生成する。</summary>
    /// <returns>生成した画像。 所有権は呼び出し側へ移る。</returns>
    /// <remarks>
    /// 左上から右下への線形グラデーション（画素は BGRA8888）。<br/>
    /// 単色でないため、撮影の前後を画素の実測で判定できる（README の実機検証手順で使う）。
    /// </remarks>
    public static EsBitmap CreateGradient()
        => EsBitmap.CreateFilled(Width, Height, pixels =>
        {
            for (var y = 0; y < Height; y++)
            {
                var row = pixels.Slice(y * Width * 4, Width * 4);
                for (var x = 0; x < Width; x++)
                {
                    // 左上 (0,0) から右下 (Width,Height) への位置を 0..1 の比で混ぜる
                    var ratio = (x + y) / (double)(Width + Height - 2);
                    var offset = x * 4;
                    row[offset] = Mix(StartB, EndB, ratio);
                    row[offset + 1] = Mix(StartG, EndG, ratio);
                    row[offset + 2] = Mix(StartR, EndR, ratio);
                    row[offset + 3] = 255;
                }
            }
        });

    /// <summary>設定に従って起動直後のプレビュー画像を設定する。</summary>
    /// <param name="viewModel">画像の設定先。</param>
    /// <param name="mode">起動直後に置く画像の種類。</param>
    /// <returns>画像を設定した場合は true。 <see cref="StartupPreviewMode.Blank"/> の場合は false。</returns>
    /// <remarks>
    /// 4 つの UI 版が起動時に 1 回だけ呼ぶ（判断を UI ごとに書かない）。<br/>
    /// 設定を切り替えたときに即座に反映はしない（次回起動から反映する。 撮影中の画像を勝手に差し替えないため）。
    /// </remarks>
    public static bool Apply(MainViewModel viewModel, StartupPreviewMode mode)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        if (mode != StartupPreviewMode.Gradient)
        {
            return false;
        }

        viewModel.SetPreviewImage(CreateGradient());
        return true;
    }

    /// <summary>2 つの色成分を比率で混ぜる。</summary>
    /// <param name="start">始点の成分（0〜255）。</param>
    /// <param name="end">終点の成分（0〜255）。</param>
    /// <param name="ratio">混ぜる比率（0 = 始点、1 = 終点）。</param>
    /// <returns>混ぜた成分。</returns>
    private static byte Mix(byte start, byte end, double ratio)
        => (byte)Math.Round(start + ((end - start) * ratio));
}
