using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using EsUtil.SaveEvidence.App.Core.Services;
using EsUtil.SaveEvidence.Parts.CapturePreview.AvaloniaUI.Crop;

namespace EsUtil.SaveEvidence.App.AvaloniaUI.Controls;

/// <summary>キャプチャページの画像の 4 辺・4 隅へ影を描く重ね合わせ（Avalonia 実装）。</summary>
/// <remarks>
/// <b>適用パターン</b>: 描画層（画像の外側へ影を描くだけの重ね合わせ）。<br/>
/// 影の幅・濃さの変化は <see cref="ShadowPlan"/>（App.Core）が単一所有するため、本クラスは<br/>
/// 「計画の 4 辺を線形グラデーションで、4 隅を放射グラデーションで塗る」手順だけを持つ<br/>
/// （4 UI 版で同じ結果になる）。<br/>
/// <b><c>BoxShadow</c> を使わない理由</b>: 一覧のサムネイル（<see cref="ThumbnailList"/>）は <c>BoxShadow</c> を<br/>
/// 使うが、キャプチャページの画像は<b>他の UI 版と同じ合成</b>にする必要がある（<c>BoxShadow</c> の<br/>
/// ぼかしは画像の端で濃い輪を作り、画像との間に線として見える）。<br/>
/// <b>追従</b>: ズーム・パン・ウィンドウの大きさの変更で画像の表示矩形が変わるため、<br/>
/// <see cref="CropViewControl.ViewportChanged"/> を購読して描き直す。
/// </remarks>
internal sealed class ImageShadowOverlay : Control
{
    /// <summary>影を落とす画像の表示矩形を提供するコントロール。</summary>
    private CropViewControl? _crop;

    /// <summary>重ね合わせを初期化する（マウス操作は下のコントロールへ通す）。</summary>
    public ImageShadowOverlay()
    {
        IsHitTestVisible = false;

        // 帯の矩形は物理ピクセルに一致しているため、端をアンチエイリアスしない（半端な画素を作らない）
        RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);

        // 影は画像の外側へ 10 DIP 広がるため、プレビュー領域の外（ツールバー・ステータスバー）へ
        // 描かないよう自分の領域で切り取る（画像が Fit より大きいと影の位置が領域の外へ出る）
        ClipToBounds = true;
    }

    /// <summary>影を落とす対象のコントロールを設定する。</summary>
    /// <param name="crop">画像を表示するコントロール。</param>
    public void Attach(CropViewControl crop)
    {
        ArgumentNullException.ThrowIfNull(crop);

        _crop = crop;
        crop.ViewportChanged += (_, _) => InvalidateVisual();
        InvalidateVisual();
    }

    /// <summary>影を描画する。</summary>
    /// <param name="context">描画先。</param>
    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (_crop is null)
        {
            return;
        }

        var image = _crop.ImageRectViewport();
        if (image.Width <= 0 || image.Height <= 0)
        {
            return;
        }

        // 物理ピクセルの格子へ合わせる（半端な位置に描くと端の画素が半透明になり、
        // 画像との間に「白い線」「黒い線」として見える）
        var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;

        // 隅を先に、辺を後に描く（隅は外側へ丸めて広げるため、辺と重なった部分は辺の単色で上書きする。
        // 画像へはみ出した分は、本コントロールが画像の背後にあるため見えない）
        foreach (var fill in ShadowPlan.BuildCornerFills(image.X, image.Y, image.Width, image.Height))
        {
            context.DrawRectangle(
                CreateCornerBrush(fill),
                null,
                SnapCorner(fill, scale));
        }

        // 4 辺は 1 画素の帯を単色で塗る（グラデーション ブラシの端の扱いに依存しない）
        foreach (var band in ShadowPlan.BuildDeviceBands(image.X, image.Y, image.Width, image.Height, scale))
        {
            context.DrawRectangle(
                new SolidColorBrush(Color.FromUInt32(band.Argb)),
                null,
                ToDip(band.X, band.Y, band.Width, band.Height, scale));
        }
    }

    /// <summary>隅の矩形を物理ピクセルの格子へ丸めて DIP へ変換する。</summary>
    /// <param name="fill">隅の計画。</param>
    /// <param name="scale">DIP → 物理ピクセルの倍率。</param>
    /// <returns>DIP の矩形（格子に一致する）。</returns>
    private static Rect SnapCorner(ShadowCornerFill fill, double scale)
    {
        var left = (int)Math.Round(fill.X * scale);
        var top = (int)Math.Round(fill.Y * scale);
        var right = left + (int)Math.Round(fill.Size * scale);
        var bottom = top + (int)Math.Round(fill.Size * scale);

        // 画像に接する辺は 1 画素だけ広げる（この描画側は端の画素を 1 つ手前で切り捨てるため、
        // 広げないと画像との間に 1 画素の白い線が残る。 広げた分は画像の背後に隠れる）
        if (fill.CenterY >= 1)
        {
            bottom++;
        }
        else
        {
            top--;
        }

        return ToDip(left, top, right - left, bottom - top, scale);
    }
    /// <summary>物理ピクセルの矩形を DIP へ変換する。</summary>
    /// <param name="x">左端（ピクセル）。</param>
    /// <param name="y">上端（ピクセル）。</param>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <param name="scale">DIP → 物理ピクセルの倍率。</param>
    /// <returns>DIP の矩形（格子に一致する）。</returns>
    /// <remarks>
    /// 端数を持たないよう<b>わずかに内側へ寄せる</b>（<paramref name="x"/> ÷ 倍率は二進小数で表せず、
    /// 掛け戻すと 1 画素の手前へ落ちて端の画素が塗られない。 実測: 隅の内側に 1 画素の白い線）。
    /// </remarks>
    private static Rect ToDip(int x, int y, int width, int height, double scale)
    {
        const double Bias = 0.001;
        return new(
            (x + Bias) / scale,
            (y + Bias) / scale,
            (width - (Bias * 2)) / scale,
            (height - (Bias * 2)) / scale);
    }
    /// <summary>隅の放射グラデーションを生成する（中心は画像の隅の頂点）。</summary>
    /// <param name="fill">塗る矩形と隅の計画。</param>
    /// <returns>放射グラデーションのブラシ。</returns>
    /// <remarks>
    /// 位置は矩形に対する比率で与える（絶対座標の半径は描画側の解釈が揺れ、端の画素が最も薄い色で<br/>
    /// 塗られて影の内側に白い線として見えたため）。 中心は画像の隅の頂点 = 矩形の内側の角である。
    /// </remarks>
    private static IBrush CreateCornerBrush(ShadowCornerFill fill)
    {
        var brush = new RadialGradientBrush
        {
            Center = new RelativePoint(fill.CenterX, fill.CenterY, RelativeUnit.Relative),
            GradientOrigin = new RelativePoint(fill.CenterX, fill.CenterY, RelativeUnit.Relative),
            RadiusX = new RelativeScalar(1, RelativeUnit.Relative),
            RadiusY = new RelativeScalar(1, RelativeUnit.Relative),
        };

        AddStops(brush, fill.Stops);
        return brush;
    }
    /// <summary>停止点をグラデーションへ追加する。</summary>
    /// <param name="brush">追加先のグラデーション。</param>
    /// <param name="stops">停止点（位置 0〜1 と色）。</param>
    private static void AddStops(GradientBrush brush, IReadOnlyList<ShadowStop> stops)
    {
        foreach (var stop in stops)
        {
            brush.GradientStops.Add(new GradientStop(
                Color.FromUInt32(stop.Argb),
                Math.Clamp(stop.Offset, 0, 1)));
        }
    }
}
