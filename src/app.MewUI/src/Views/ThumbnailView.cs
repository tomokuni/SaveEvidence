using Aprillz.MewUI;
using Aprillz.MewUI.Skia.Controls;
using EsUtil.Platform.Core.Imaging;
using Estable.CaptureSession;
using SkiaSharp;

namespace SaveEvidence.MewUI.Views;

/// <summary>画像をアスペクト比を保って領域いっぱいへ収める表示ビュー（MewUI 版）。</summary>
/// <remarks>
/// 一覧のサムネイルのように「幅を固定して元画像の縦横比のまま見せる」用途に使う。<br/>
/// <see cref="EsBitmap"/> を保持するだけで UI 用ビットマップへ変換せず、<see cref="SkiaCanvasView"/> のキャンバスへ<br/>
/// 1 回の <c>DrawBitmap</c> で描画する（中間バッファの生成とコピーを行わない）。<br/>
/// <br/>
/// <b>余白</b>: 画像の縦横比と領域の縦横比が異なる場合、余白は透明のままにする（親の背景がそのまま見える）。<br/>
/// <b>責務の境界</b>: 拡大率の制御（ズーム・パン）は持たない。表示中の画像を「収める」計算のみを担う。<br/>
/// <b>再描画</b>: 静止画のため <see cref="SkiaCanvasView.ContinuousAnimation"/> を無効化し、
/// <see cref="Source"/> の変更時のみ描画する。
/// </remarks>
internal sealed class ThumbnailView : SkiaCanvasView
{
    /// <summary><see cref="Source"/> プロパティ（表示する画像）。</summary>
    /// <remarks>変更は縦横比に応じた高さ（計測）と描画の両方に影響する。</remarks>
    public static readonly MewProperty<EsBitmap?> SourceProperty =
        MewProperty<EsBitmap?>.Register<ThumbnailView>(
            nameof(Source), null, MewPropertyOptions.AffectsRender | MewPropertyOptions.AffectsLayout);

    /// <summary><see cref="RequestedSize"/> プロパティ（幅。DIP）。</summary>
    /// <remarks>
    /// 変更はレイアウトと描画の両方に影響する（物理ピクセル数が変わるため、描画側も再実行が必要）。
    /// </remarks>
    public static readonly MewProperty<int> RequestedSizeProperty =
        MewProperty<int>.Register<ThumbnailView>(
            nameof(RequestedSize), 0,
            MewPropertyOptions.AffectsRender | MewPropertyOptions.AffectsLayout);

    /// <summary>サムネイルビューを初期化する。</summary>
    /// <remarks>静止画のため連続再描画を無効化し、描画は <see cref="Source"/> の変更時のみ行う。</remarks>
    public ThumbnailView()
    {
        ContinuousAnimation = false;
        PaintSurface += OnPaintSurface;
    }

    /// <summary>表示する画像を取得または設定する。</summary>
    /// <value>表示する画像。null の場合は何も描画しない（余白のみ）。</value>
    public EsBitmap? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>サムネイルの幅（DIP）を取得または設定する。</summary>
    /// <value>幅（DIP）。0 以下は 0 として扱い、領域を要求しない。</value>
    /// <remarks>高さは画像の縦横比から決めるため保持しない。</remarks>
    public int RequestedSize
    {
        get => GetValue(RequestedSizeProperty);
        set => SetValue(RequestedSizeProperty, value < 0 ? 0 : value);
    }

    /// <summary>画像の縦横比に応じた要求サイズを返す。</summary>
    /// <param name="availableSize">親から提示された利用可能サイズ（使用しない）。</param>
    /// <returns>幅が <see cref="RequestedSize"/>、高さが縦横比で決まるサイズ。</returns>
    /// <remarks>
    /// <see cref="SkiaCanvasView"/> の既定は <c>Size.Empty</c>（親の残余を埋める）だが、<br/>
    /// サムネイルは一覧側が大きさを決めるため、自身の要求サイズを返す。<br/>
    /// 高さを縦横比に合わせることで、高さが異なるアイテムが行内に混在する表示（Avalonia 版の
    /// <c>Image</c> + <c>Stretch="Uniform"</c> と同じ見え方）になる。<br/>
    /// 画像が無い・破棄済みの場合は正方形（<see cref="RequestedSize"/> の一辺）を要求する。
    /// </remarks>
    protected override Size MeasureContent(Size availableSize)
    {
        if (Source is not { IsDisposed: false } source || source.Width <= 0 || RequestedSize <= 0)
        {
            return new Size(RequestedSize, RequestedSize);
        }

        return new Size(RequestedSize, RequestedSize * (double)source.Height / source.Width);
    }

    /// <summary>画像を領域いっぱいへアスペクト比を保って描画する。</summary>
    /// <param name="canvas">描画先のキャンバス（左上を原点とし、物理ピクセルで寸法付けられている）。</param>
    /// <param name="info">キャンバスのピクセル情報（幅・高さを使用する）。</param>
    /// <remarks>
    /// 1. 余白を親の背景へ委ねるため、キャンバスを透明でクリアする。<br/>
    /// 2. 画像が無い、破棄済み、または領域が空の場合は描画しない（キャッシュの容量超過で破棄された画像を安全に扱う）。<br/>
    /// 3. 配置は <see cref="ImageDrawMath"/> が決める（縦横比を保って中央へ収める）。
    /// </remarks>
    private void OnPaintSurface(SKCanvas canvas, SKImageInfo info)
    {
        canvas.Clear(SKColors.Transparent);

        if (Source is not { } source || source.IsDisposed)
        {
            return;
        }

        var destination = ImageDrawMath.ComputeDestination(
            source.Width, source.Height, info.Width, info.Height, uniform: true);

        if (destination.IsEmpty)
        {
            return;
        }

        // ToSKBitmap は内部インスタンスの共有参照を返すため破棄しない
        canvas.DrawBitmap(source.ToSKBitmap(), destination);
    }
}
