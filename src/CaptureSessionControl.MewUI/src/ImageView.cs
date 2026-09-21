using Aprillz.MewUI;
using Aprillz.MewUI.Skia.Controls;
using Estable.CaptureSession;
using EsUtil.Platform.Core.Imaging;
using SkiaSharp;

namespace Estable.MewUI.CaptureSession;

/// <summary>画像を親が割り当てた領域いっぱいへ描画するビュー（MewUI 版）。</summary>
/// <remarks>
/// キャプチャのオーバーレイのように「画面全体へ 1 枚の画像を敷く」用途に使う。<br/>
/// 表示サイズは親のレイアウトに従う（自身では要求サイズを持たない）。<br/>
/// <br/>
/// <b>拡大の指定</b>: <see cref="IsUniform"/> が true の場合は縦横比を保って収め、false の場合は領域全体へ引き伸ばす。<br/>
/// キャプチャ画像は仮想スクリーンと等倍（同じ縦横比）のため <c>false</c> を使う（端数のずれで余白が出るのを避ける）。<br/>
/// <b>計算の単一所有</b>: 配置の矩形計算は <see cref="ImageDrawMath"/> が所有する（サムネイル表示と共通）。<br/>
/// <b>再描画</b>: 静止画のため連続再描画を無効化し、<see cref="Source"/> の変更時のみ描画する。
/// </remarks>
internal sealed class ImageView : SkiaCanvasView
{
    /// <summary><see cref="Source"/> プロパティ（表示する画像）。</summary>
    public static readonly MewProperty<EsBitmap?> SourceProperty =
        MewProperty<EsBitmap?>.Register<ImageView>(nameof(Source), null, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="IsUniform"/> プロパティ（縦横比を保つかどうか）。</summary>
    public static readonly MewProperty<bool> IsUniformProperty =
        MewProperty<bool>.Register<ImageView>(nameof(IsUniform), false, MewPropertyOptions.AffectsRender);

    /// <summary>画像ビューを初期化する。</summary>
    /// <remarks>静止画のため連続再描画を無効化し、描画は <see cref="Source"/> の変更時のみ行う。</remarks>
    public ImageView()
    {
        ContinuousAnimation = false;
        PaintSurface += OnPaintSurface;
    }

    /// <summary>表示する画像を取得または設定する。</summary>
    /// <value>表示する画像。null の場合は何も描画しない。</value>
    public EsBitmap? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>縦横比を保って領域へ収めるかどうかを取得または設定する。</summary>
    /// <value>true の場合は縦横比を保ち、false の場合は領域全体へ引き伸ばす。既定値は false。</value>
    public bool IsUniform
    {
        get => GetValue(IsUniformProperty);
        set => SetValue(IsUniformProperty, value);
    }

    /// <summary>画像を領域全体へ描画する。</summary>
    /// <param name="canvas">描画先のキャンバス（左上を原点とし、物理ピクセルで寸法付けられている）。</param>
    /// <param name="info">キャンバスのピクセル情報（幅・高さを使用する）。</param>
    /// <remarks>
    /// 1. キャンバスを透明でクリアする。<br/>
    /// 2. 画像が無い・破棄済みの場合は描画しない。<br/>
    /// 3. <see cref="ImageDrawMath"/> が求めた矩形へ 1 回の <c>DrawBitmap</c> で描画する。
    /// </remarks>
    private void OnPaintSurface(SKCanvas canvas, SKImageInfo info)
    {
        canvas.Clear(SKColors.Transparent);

        if (Source is not { } source || source.IsDisposed)
        {
            return;
        }

        var destination = ImageDrawMath.ComputeDestination(
            source.Width, source.Height, info.Width, info.Height, IsUniform);

        if (destination.IsEmpty)
        {
            return;
        }

        // ToSKBitmap は内部インスタンスの共有参照を返すため破棄しない
        canvas.DrawBitmap(source.ToSKBitmap(), destination);
    }
}
