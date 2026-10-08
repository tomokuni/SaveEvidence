using Aprillz.MewUI;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.Platform.MewUI.Imaging;

namespace EsUtil.SaveEvidence.App.MewUI.Views;

/// <summary>画像をアスペクト比を保って領域いっぱいへ収める表示ビュー（MewUI 版）。</summary>
/// <remarks>
/// 一覧のサムネイルのように「幅を固定して元画像の縦横比のまま見せる」用途に使う。<br/>
/// <see cref="EsWritableBitmap"/> を保持するだけで UI 用ビットマップへ変換せず、Skia のキャンバスへ<br/>
/// 1 回の <c>DrawBitmap</c> で描画する（中間バッファの生成とコピーを行わない）。<br/>
/// <br/>
/// <b>役割の分担</b>: 描画（クリア・配置の計算・描画）は基底の <see cref="SkiaImageView"/> が単一所有する。<br/>
/// 本クラスは「幅を固定し、高さを画像の縦横比から決める」計測だけを担う。<br/>
/// <br/>
/// <b>余白</b>: 画像の縦横比と領域の縦横比が異なる場合、余白は透明のままにする（親の背景がそのまま見える）。<br/>
/// <b>責務の境界</b>: 拡大率の制御（ズーム・パン）は持たない。 表示中の画像を「収める」計算のみを担う。<br/>
/// <b>再描画</b>: 静止画のため連続再描画を無効化し、<see cref="SkiaImageView.Source"/> の変更時のみ描画する。
/// </remarks>
internal sealed class ThumbnailView : SkiaImageView
{
    /// <summary><see cref="RequestedSize"/> プロパティ（幅。 DIP）。</summary>
    /// <remarks>
    /// 変更はレイアウトと描画の両方に影響する（物理ピクセル数が変わるため、描画側も再実行が必要）。
    /// </remarks>
    public static readonly MewProperty<int> RequestedSizeProperty =
        MewProperty<int>.Register<ThumbnailView>(
            nameof(RequestedSize), 0,
            MewPropertyOptions.AffectsRender | MewPropertyOptions.AffectsLayout);

    /// <summary>サムネイルの幅（DIP）を取得または設定する。</summary>
    /// <value>幅（DIP）。 0 以下は 0 として扱い、領域を要求しない。</value>
    /// <remarks>高さは画像の縦横比から決めるため保持しない。</remarks>
    public int RequestedSize
    {
        get => GetValue(RequestedSizeProperty);
        set => SetValue(RequestedSizeProperty, value < 0 ? 0 : value);
    }

    /// <summary>縦横比を保って領域へ収めるかどうかを取得する。</summary>
    /// <value>常に true（サムネイルは縦横比を保って収める）。</value>
    /// <remarks>基底の描画が本値を参照する（本クラスは描画の手順を持たない）。</remarks>
    protected override bool UniformDraw => true;

    /// <summary>画像の縦横比に応じた要求サイズを返す。</summary>
    /// <param name="availableSize">親から提示された利用可能サイズ（使用しない）。</param>
    /// <returns>幅が <see cref="RequestedSize"/>、高さが縦横比で決まるサイズ。</returns>
    /// <remarks>
    /// <c>SkiaCanvasView</c> の既定は <c>Size.Empty</c>（親の残余を埋める）だが、<br/>
    /// サムネイルは一覧側が大きさを決めるため、自身の要求サイズを返す。<br/>
    /// 高さを縦横比に合わせることで、高さが異なるアイテムが行内に混在する表示（Avalonia 版の<br/>
    /// <c>Image</c> + <c>Stretch="Uniform"</c> と同じ見え方）になる。<br/>
    /// 画像が無い・破棄済みの場合は正方形（<see cref="RequestedSize"/> の一辺）を要求する。
    /// </remarks>
    protected override Size MeasureContent(Size availableSize)
    {
        // 破棄済みの検出はコアの実体（EsBitmapBase）だけが持つ（読み取りの面は破棄状態を公開しない）
        if (Source is not { } source || source is EsBitmapBase { IsDisposed: true } || source.Width <= 0 || RequestedSize <= 0)
        {
            return new Size(RequestedSize, RequestedSize);
        }

        return new Size(RequestedSize, RequestedSize * (double)source.Height / source.Width);
    }
}
