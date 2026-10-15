using EsUtil.Platform.Core.Imaging;
using SkiaSharp.Views.Windows;

namespace EsUtil.SaveEvidence.Platform.WinUI3.Imaging;

/// <summary><see cref="EsReadOnlyBitmap"/> を親が割り当てた領域へ描画する表示ビュー（WinUI 3 / Skia 版）。</summary>
/// <remarks>
/// <b>適用パターン</b>: Adapter（<see cref="EsReadOnlyBitmap"/> を WinUI 3 の要素として適合させる）。<br/>
/// <b>用途</b>: 「割り当てられた領域いっぱいへ 1 枚の画像を敷く」表示に使う。 表示サイズは親のレイアウトに従う<br/>
/// （本クラス自身は要求サイズを持たない）。 縦横比から大きさを決める表示は、本クラスを継承して<br/>
/// <see cref="UniformDraw"/> を上書きする。<br/>
/// <br/>
/// <b>配置方法</b>: <see cref="UniformDraw"/> が false（既定）の場合は領域全体へ引き伸ばし、<br/>
/// true の場合は縦横比を保って中央へ収める（余白は描画しない）。 撮影画像のように領域と同じ縦横比の<br/>
/// 画像は引き伸ばし、縦横比の異なるサムネイルは収める使い分けになる。<br/>
/// <br/>
/// <b>プロパティは CLR プロパティである</b>: WinUI 3 の <c>DependencyProperty</c> は WinRT へ投影できる型だけを<br/>
/// 受け付けるため、コアの <see cref="EsReadOnlyBitmap"/> は依存関係プロパティとして登録できない<br/>
/// （ホストはコードビハインドから設定する。 XAML のバインディング対象にはしない）。<br/>
/// <br/>
/// <b>再描画</b>: 静止画のため連続再描画を要求せず、<see cref="Source"/> の変更時に <c>SKXamlCanvas.Invalidate</c> を呼ぶ。<br/>
/// 配置の矩形計算と描画手順は <see cref="ImageDrawMath"/> / <see cref="SkiaImageRenderer"/> が単一所有する<br/>
/// （本クラスはプロパティの配線だけを担う）。
/// </remarks>
public class SkiaImageView : SKXamlCanvas
{
    /// <summary>拡大元の画像（<see cref="Source"/> の保持値）。</summary>
    private EsReadOnlyBitmap? _source;

    /// <summary>表示ビューを初期化する。</summary>
    /// <remarks>描画は <see cref="Source"/> の変更時にのみ要求する（静止画のため）。</remarks>
    public SkiaImageView()
    {
        PaintSurface += OnPaintSurface;
    }

    /// <summary>表示する画像を取得または設定する。</summary>
    /// <value>表示する画像。 null の場合は何も描画しない（余白のみ）。</value>
    public EsReadOnlyBitmap? Source
    {
        get => _source;
        set
        {
            if (ReferenceEquals(_source, value))
            {
                return;
            }

            _source = value;
            // 縦横比から大きさを決める派生クラスがあるため、描画とレイアウトの両方を無効化する
            InvalidateMeasure();
            Invalidate();
        }
    }

    /// <summary>縦横比を保って領域へ収めるかどうかを取得する。</summary>
    /// <value>true の場合は縦横比を保って中央へ収める。 false の場合は領域全体へ引き伸ばす。 既定値は false。</value>
    /// <remarks>派生クラスが固定値を与える（プロパティにすると、使い分けのない側へ不要な設定面が増える）。</remarks>
    protected virtual bool UniformDraw => false;

    /// <summary>画像を領域へ描画する。</summary>
    /// <param name="sender">イベントの送信元（本コントロール）。</param>
    /// <param name="e">描画先の表面（Skia のキャンバス）と描画先の情報（物理ピクセルの大きさ）。</param>
    /// <remarks>前提条件の判定と配置の計算は <see cref="SkiaImageRenderer"/> が単一所有する。</remarks>
    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
        => SkiaImageRenderer.Render(
            e.Surface.Canvas, e.Info, _source, UniformDraw ? ImageDrawMode.Uniform : ImageDrawMode.Fill);
}
