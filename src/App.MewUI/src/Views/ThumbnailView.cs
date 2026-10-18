using Aprillz.MewUI;
using Aprillz.MewUI.Skia.Controls;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.App.Core.Services;
using EsUtil.SaveEvidence.Platform.MewUI.Imaging;
using EsUtil.SaveEvidence.Platform.Skia;
using SkiaSharp;

namespace EsUtil.SaveEvidence.App.MewUI.Views;

/// <summary>画像をアスペクト比を保って表示し、4 辺へ影を落とす表示ビュー（MewUI 版）。</summary>
/// <remarks>
/// 一覧のサムネイルのように「幅を固定して元画像の縦横比のまま見せる」用途に使う。<br/>
/// <see cref="EsBitmap"/> を保持するだけで UI 用ビットマップへ変換せず、Skia のキャンバスへ<br/>
/// 1 回の <c>DrawBitmap</c> で描画する（中間バッファの生成とコピーを行わない）。<br/>
/// <br/>
/// <b>影は画像の外側だけ</b>: 影の幅・濃さは <see cref="ShadowPlan"/>（App.Core）が単一所有する。<br/>
/// 本クラスは画像の外側に影の分の余白を確保し、その内側へ画像を描く（影と画像の間に隙間・枠線は入らない）。<br/>
/// <br/>
/// <b>余白</b>: 画像の縦横比と領域の縦横比が異なる場合、余白は透明のままにする（親の背景がそのまま見える）。<br/>
/// <b>責務の境界</b>: 拡大率の制御（ズーム・パン）は持たない。 表示中の画像を「収める」計算のみを担う。<br/>
/// <b>再描画</b>: 静止画のため連続再描画を無効化し、<see cref="Source"/> の変更時のみ描画する。
/// </remarks>
internal sealed class ThumbnailView : SkiaCanvasView
{
    /// <summary><see cref="Source"/> プロパティ。</summary>
    /// <remarks>変更はレイアウトと描画の両方に影響する（高さが画像の縦横比で変わるため）。</remarks>
    public static readonly MewProperty<EsReadOnlyBitmap?> SourceProperty =
        MewProperty<EsReadOnlyBitmap?>.Register<ThumbnailView>(
            nameof(Source), null, MewPropertyOptions.AffectsRender | MewPropertyOptions.AffectsLayout);

    /// <summary><see cref="RequestedSize"/> プロパティ（幅。 DIP）。</summary>
    /// <remarks>
    /// 変更はレイアウトと描画の両方に影響する（物理ピクセル数が変わるため、描画側も再実行が必要）。
    /// </remarks>
    public static readonly MewProperty<int> RequestedSizeProperty =
        MewProperty<int>.Register<ThumbnailView>(
            nameof(RequestedSize), 0,
            MewPropertyOptions.AffectsRender | MewPropertyOptions.AffectsLayout);

    /// <summary>表示ビューを初期化する。</summary>
    /// <remarks>静止画のため連続再描画を無効化し、描画は <see cref="Source"/> の変更時のみ行う。</remarks>
    public ThumbnailView()
    {
        ContinuousAnimation = false;
        PaintSurface += OnPaintSurface;
    }

    /// <summary>表示する画像を取得または設定する。</summary>
    /// <value>表示する画像。 null の場合は影だけを描画する（余白のみ）。</value>
    public EsReadOnlyBitmap? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>サムネイルの幅（DIP）を取得または設定する。</summary>
    /// <value>幅（DIP）。 0 以下は 0 として扱い、領域を要求しない。</value>
    /// <remarks>高さは画像の縦横比から決めるため保持しない。</remarks>
    public int RequestedSize
    {
        get => GetValue(RequestedSizeProperty);
        set => SetValue(RequestedSizeProperty, value < 0 ? 0 : value);
    }

    /// <summary>画像の縦横比に応じた要求サイズを返す。</summary>
    /// <param name="availableSize">親から提示された利用可能サイズ（使用しない）。</param>
    /// <returns>影の余白を含むサイズ（幅は <see cref="RequestedSize"/> + 影の左右、高さは縦横比 + 影の上下）。</returns>
    /// <remarks>
    /// <c>SkiaCanvasView</c> の既定は <c>Size.Empty</c>（親の残余を埋める）だが、<br/>
    /// サムネイルは一覧側が大きさを決めるため、自身の要求サイズを返す。<br/>
    /// 高さを縦横比に合わせることで、高さが異なるアイテムが行内に混在する表示（Avalonia 版の<br/>
    /// <c>Image</c> + <c>Stretch="Uniform"</c> と同じ見え方）になる。<br/>
    /// 画像が無い・破棄済みの場合は正方形（<see cref="RequestedSize"/> の一辺）を要求する。
    /// </remarks>
    protected override Size MeasureContent(Size availableSize)
    {
        var insets = ShadowPlan.Insets;

        // 破棄済みの判定は読み取りの面（EsReadOnlyBitmap.IsDisposed）が公開する
        if (Source is not { } source || source.IsDisposed || source.Width <= 0 || RequestedSize <= 0)
        {
            return new Size(RequestedSize + insets.Left + insets.Right, RequestedSize + insets.Top + insets.Bottom);
        }

        var imageHeight = ThumbnailTileMetrics.ImageHeight(RequestedSize, source.Width, source.Height);
        return new Size(RequestedSize + insets.Left + insets.Right, imageHeight + insets.Top + insets.Bottom);
    }

    /// <summary>影と画像を描画する。</summary>
    /// <param name="canvas">描画先のキャンバス（物理ピクセル）。</param>
    /// <param name="info">描画先の情報（幅・高さ）。</param>
    /// <remarks>
    /// キャンバスを DPI スケーリングしてから DIP 座標で描画する（計画の値と表示座標を一致させる）。<br/>
    /// 描画順は「影 → 画像」（影は画像の外側にだけ置くため、画像が影を覆うことはない）。
    /// </remarks>
    private void OnPaintSurface(SKCanvas canvas, SKImageInfo info)
    {
        // 画像が無い部分は透明にする（親の背景をそのまま見せる）
        canvas.Clear(SKColors.Transparent);

        // スケーリングは描画面（物理ピクセル）と割り当て領域（DIP）の比から求める
        // （DPI の通知は追加直後のコントロールへ届かないため、保持値だけに頼ると 100% で描いてしまう）
        var scaling = ClientPixelScale.Resolve(info.Width, Bounds.Size.Width, fallback: 1.0);

        var insets = ShadowPlan.Insets;
        var width = Bounds.Size.Width;
        var height = Bounds.Size.Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var imageRect = new SKRect(
            (float)insets.Left,
            (float)insets.Top,
            (float)(width - insets.Right),
            (float)(height - insets.Bottom));

        canvas.Save();
        canvas.Scale((float)scaling);
        try
        {
            ShadowRenderer.Render(canvas, imageRect);

            if (Source is { } bitmap && !bitmap.IsDisposed && !imageRect.IsEmpty)
            {
                // ToSKBitmap は非所有ビューを作る（ピクセルは複製しない。 ファイナライザ対策で直後に破棄する）
                using var view = bitmap.ToSKBitmap();
                canvas.DrawBitmap(view, imageRect);
            }
        }
        finally
        {
            canvas.Restore();
        }
    }
}
