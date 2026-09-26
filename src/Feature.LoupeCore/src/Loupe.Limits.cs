using EsUtil.Feature.LoupeCore.Setting;

namespace EsUtil.Feature.LoupeCore;

public sealed partial class Loupe
{
        /// <summary>ルーペの数値パラメータの安全域（ファサードが公開する設定のスキーマ）。</summary>
        /// <remarks>
        /// <b>位置付け</b>: ルーペの数値パラメータ（表示サイズ・拡大率・スケーリング・市松模様・外枠・ラベル文字サイズ）の<br/>
        /// 既定値と安全域（最小・最大）、および範囲外の値の補正を単一所有する。<br/>
        /// <b>ファサードの入れ子である理由</b>: 利用者（UI 実装）はプロパティの既定値と補正を登録するため本値を参照する。<br/>
        /// ファサード（<see cref="Loupe"/>）の入れ子とすることで、UI がファサード以外のコア型を直接呼ぶ経路を作らない<br/>
        /// （利用者はルーペの数値を得るのに <c>Loupe</c> だけを参照すればよい）。<br/>
        /// <b>規則の所在</b>: 設定値の補正は設定型（<see cref="LoupeOptions"/> の生成時の <c>init</c> アクセサ）と<br/>
        /// ファサードの環境値（<see cref="Loupe.RenderScaling"/> の setter）も本値へ委譲する（補正の規則を二重に持たない）。<br/>
        /// ラベルのフォントサイズの安全域は、他のコントロールと共通の規則のため描画層（<c>LoupeControl.UIBase</c> の <c>LoupePaint</c>）が単一所有する<br/>
        /// （本クラスは既定値を添えて委譲するだけ）。<br/>
        /// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
        /// </remarks>
    public static class Limits
        {
        /// <summary>表示サイズ要求値の最小値（DIP）。</summary>
        public const int RequestedSizeMin = 1;

        /// <summary>表示サイズ要求値の最大値（DIP）。</summary>
        public const int RequestedSizeMax = 4096;

        /// <summary>表示サイズ要求値の既定値（DIP）。</summary>
        public const int RequestedSizeDefault = 160;

        /// <summary>拡大率の最小値。</summary>
        public const int ZoomLevelMin = 1;

        /// <summary>拡大率の最大値。</summary>
        public const int ZoomLevelMax = 64;

        /// <summary>拡大率の既定値。</summary>
        public const int ZoomLevelDefault = 4;

        /// <summary>スケーリング係数の最小値。</summary>
        public const double RenderScalingMin = 0.1;

        /// <summary>スケーリング係数の最大値。</summary>
        public const double RenderScalingMax = 8.0;

        /// <summary>スケーリング係数の既定値（補正不能時のフォールバックも兼ねる）。</summary>
        public const double RenderScalingDefault = 1.0;

        /// <summary>市松模様の 1 タイルサイズの最小値（ピクセル）。</summary>
        public const int CheckerSizeMin = 1;

        /// <summary>市松模様の 1 タイルサイズの最大値（ピクセル）。</summary>
        public const int CheckerSizeMax = 128;

        /// <summary>市松模様の 1 タイルサイズの既定値（ピクセル）。</summary>
        public const int CheckerSizeDefault = 6;

        /// <summary>外枠の太さの最小値（ピクセル）。</summary>
        public const int FrameWidthMin = 1;

        /// <summary>外枠の太さの最大値（ピクセル）。</summary>
        public const int FrameWidthMax = 64;

        /// <summary>外枠の太さの既定値（ピクセル）。</summary>
        public const int FrameWidthDefault = 2;

        /// <summary>表示サイズ要求値を安全域へ補正する（1〜4096）。</summary>
        /// <param name="value">補正する値（DIP）。</param>
        /// <returns>安全域にクランプした値。</returns>
        public static int CoerceRequestedSize(int value) => Math.Clamp(value, RequestedSizeMin, RequestedSizeMax);

        /// <summary>拡大率を安全域へ補正する（1〜64）。</summary>
        /// <param name="value">補正する値。</param>
        /// <returns>安全域にクランプした値。</returns>
        public static int CoerceZoomLevel(int value) => Math.Clamp(value, ZoomLevelMin, ZoomLevelMax);

        /// <summary>スケーリング係数を安全域へ補正する（0.1〜8.0。非有限は既定値）。</summary>
        /// <param name="value">補正する値。</param>
        /// <returns>安全域にクランプした値。非有限の場合は既定値。</returns>
        public static double CoerceRenderScaling(double value)
        => double.IsFinite(value) ? Math.Clamp(value, RenderScalingMin, RenderScalingMax) : RenderScalingDefault;

        /// <summary>市松模様の 1 タイルサイズを安全域へ補正する（1〜128）。</summary>
        /// <param name="value">補正する値（ピクセル）。</param>
        /// <returns>安全域にクランプした値。</returns>
        public static int CoerceCheckerSize(int value) => Math.Clamp(value, CheckerSizeMin, CheckerSizeMax);

        /// <summary>外枠の太さを安全域へ補正する（1〜64）。</summary>
        /// <param name="value">補正する値（ピクセル）。</param>
        /// <returns>安全域にクランプした値。</returns>
        public static int CoerceFrameWidth(int value) => Math.Clamp(value, FrameWidthMin, FrameWidthMax);
    }
}
