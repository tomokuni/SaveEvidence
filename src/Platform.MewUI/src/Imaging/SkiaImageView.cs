using Aprillz.MewUI;
using Aprillz.MewUI.Skia.Controls;
using EsUtil.Platform.Core.Imaging;
using SkiaSharp;

namespace EsUtil.SaveEvidence.Platform.MewUI.Imaging;

/// <summary><see cref="EsBitmap"/> を親が割り当てた領域へ描画する表示ビュー（MewUI / Skia 版）。</summary>
/// <remarks>
/// <b>適用パターン</b>: Adapter（<see cref="EsBitmap"/> を MewUI の要素として適合させる）。<br/>
/// <b>用途</b>: 「割り当てられた領域いっぱいへ 1 枚の画像を敷く」表示に使う。 表示サイズは親のレイアウトに従う<br/>
/// （本クラス自身は要求サイズを持たない）。 縦横比から大きさを決める表示は、本クラスを継承して<br/>
/// <c>MeasureContent</c> を上書きする。<br/>
/// <br/>
/// <b>配置方法</b>: <see cref="UniformDraw"/> が false（既定）の場合は領域全体へ引き伸ばし、<br/>
/// true の場合は縦横比を保って中央へ収める（余白は描画しない）。 撮影画像のように領域と同じ縦横比の<br/>
/// 画像は引き伸ばし、縦横比の異なるサムネイルは収める使い分けになる。<br/>
/// <br/>
/// <b>再描画</b>: 静止画のため連続再描画を無効化し、<see cref="Source"/> の変更時のみ描画する。<br/>
/// 配置の矩形計算と描画手順は <see cref="ImageDrawMath"/> / <see cref="SkiaImageRenderer"/> が単一所有する<br/>
/// （本クラスはプロパティの配線だけを担う）。
/// </remarks>
public class SkiaImageView : SkiaCanvasView
{
    /// <summary><see cref="Source"/> プロパティ（表示する画像）。</summary>
    /// <remarks>
    /// 変更は描画とレイアウトの両方に影響しうる（縦横比から大きさを決める派生クラスがあるため）。<br/>
    /// 派生クラスが <c>MeasureContent</c> で <see cref="Source"/> を参照するので、両方を無効化対象にする。
    /// </remarks>
    public static readonly MewProperty<EsBitmap?> SourceProperty =
        MewProperty<EsBitmap?>.Register<SkiaImageView>(
            nameof(Source), null, MewPropertyOptions.AffectsRender | MewPropertyOptions.AffectsLayout);

    /// <summary>表示ビューを初期化する。</summary>
    /// <remarks>静止画のため連続再描画を無効化し、描画は <see cref="Source"/> の変更時のみ行う。</remarks>
    public SkiaImageView()
    {
        ContinuousAnimation = false;
        PaintSurface += OnPaintSurface;
    }

    /// <summary>表示する画像を取得または設定する。</summary>
    /// <value>表示する画像。 null の場合は何も描画しない（余白のみ）。</value>
    public EsBitmap? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>縦横比を保って領域へ収めるかどうかを取得する。</summary>
    /// <value>true の場合は縦横比を保って中央へ収める。 false の場合は領域全体へ引き伸ばす。 既定値は false。</value>
    /// <remarks>派生クラスが固定値を与える（プロパティにすると、使い分けのない側へ不要な設定面が増える）。</remarks>
    protected virtual bool UniformDraw => false;

    /// <summary>画像を領域へ描画する。</summary>
    /// <param name="canvas">描画先のキャンバス（左上を原点とし、物理ピクセルで寸法付けられている）。</param>
    /// <param name="info">キャンバスのピクセル情報（幅・高さを使用する）。</param>
    /// <remarks>前提条件の判定と配置の計算は <see cref="SkiaImageRenderer"/> が単一所有する。</remarks>
    private void OnPaintSurface(SKCanvas canvas, SKImageInfo info)
        => SkiaImageRenderer.Render(canvas, info, Source, UniformDraw ? ImageDrawMode.Uniform : ImageDrawMode.Fill);
}
