using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Estable.AvaloniaUI.Loupe;
using Estable.AvaloniaUI.Platform;
using Estable.AvaloniaUI.Platform.Imaging;
using LineDetectionSample.ViewModels;
using System.ComponentModel;
using System.Reactive.Linq;

namespace LineDetectionSample.Views;

/// <summary>画像 + 線分オーバーレイ + ルーペの共通表示ビュー（線分検出・データ検証ページで共有）。</summary>
/// <remarks>
/// 画像は Viewbox（Stretch=Uniform, DownOnly）で表示され、表示領域より大きい場合は縮小される。<br/>
/// このため ImageHost ローカル座標（縮小座標）と画像ピクセル座標は一致しない。<br/>
/// ポインタ位置は <see cref="ImageDisplayScale"/> で除算して画像ピクセル座標へ変換してから<br/>
/// ホバー判定（<see cref="DetectionContext.UpdateHover"/>）とルーペ中心（<see cref="DetectionContext.LoupeCenter"/>）に渡す。<br/>
/// ルーペは画像ピクセル座標 → ImageHost ローカル座標のスケーリングポート（<see cref="CoordinatePort.Scale"/>）で配置し、<br/>
/// マウス位置（= ルーペ中心）の右下に表示する。<br/>
/// ルーペ内の線分オーバーレイ（選択中/ホバー中の線分の破線）は、LoupeControl の <see cref="LoupeControl.Rendered"/> イベントの<br/>
/// 引数で受け取った Bitmap（WriteableBitmap）に直接破線を描画する（LoupeControl の内部計算を複製しない）。<br/>
/// ホバー/選択の変化（<see cref="DetectionContext.LoupeOverlayLines"/>）では <see cref="LoupeControl.ForceUpdate"/> で<br/>
/// ルーペを再生成し、新しい Bitmap に破線を再描画する。
/// </remarks>
public partial class LineCanvasView : UserControl
{
    /// <summary>ルーペをカーソルの右下へ配置するオフセット量（DIP）。</summary>
    private const double LoupePositionOffset = 24;

    /// <summary>Shared ViewModel（線分検出・データ検証ページ共通の検出コンテキスト）。</summary>
    private DetectionContext? _ctx;

    /// <summary>LineCanvasView を初期化する。</summary>
    public LineCanvasView()
    {
        InitializeComponent();
    }

    /// <summary>DataContext が設定されたとき、ルーペの座標変換ポートと描画イベントを初期化する。</summary>
    /// <param name="e">依存プロパティ変更イベント。</param>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is not DetectionContext ctx)
        {
            return;
        }

        _ctx = ctx;

        // ルーペの表示位置・サイズを自己計算させる（画像ピクセル座標 → ImageHost ローカル座標のスケーリングポート）
        Loupe.SetCoordinateTransform(CoordinatePort.Scale(ImageDisplayScale));
        Loupe.PositionOffset = LoupePositionOffset;

        // 描画完了イベントでルーペの Bitmap に線分オーバーレイ（破線）を描画する
        Loupe.Rendered += OnLoupeRendered;
        // ホバー/選択の変化（画像上・一覧・再検出）では、ルーペを再生成して破線を再描画する
        ctx.PropertyChanged += OnContextPropertyChanged;

        // 画像ホストのサイズ変更（Viewbox スケール変化）でルーペの座標変換ポートを更新する
        ImageHost.GetObservable(BoundsProperty).Subscribe(_ => OnImageHostSizeChanged());
    }

    /// <summary>画像ホストのサイズ変更時（Viewbox スケール変化）にルーペの座標変換ポートを更新する。</summary>
    private void OnImageHostSizeChanged()
        => Loupe.SetCoordinateTransform(CoordinatePort.Scale(ImageDisplayScale));

    /// <summary>画像の表示スケール（画像ピクセル座標 → ImageHost ローカル座標）を計算する。</summary>
    /// <remarks>
    /// Viewbox が画像を縮小表示する場合、ImageHost ローカル座標は画像ピクセル座標と一致しない。<br/>
    /// ポインタ位置の画像ピクセル座標への変換と、ルーペの座標変換ポート（<see cref="CoordinatePort.Scale"/>）に使用する。<br/>
    /// 画像未設定・サイズ 0 の場合は 1.0（等価）を返す。
    /// </remarks>
    private double ImageDisplayScale
    {
        get
        {
            if (_ctx?.Image is not { } image)
            {
                return 1.0;
            }

            var width = ImageHost.Bounds.Width;
            return width > 0 ? width / image.PixelSize.Width : 1.0;
        }
    }

    /// <summary>ImageHost ローカル座標（縮小座標）を画像ピクセル座標へ変換する。</summary>
    /// <param name="pos">ImageHost ローカル座標。</param>
    /// <returns>画像ピクセル座標。</returns>
    /// <remarks>Viewbox スケール（<see cref="ImageDisplayScale"/>）で除算する。</remarks>
    private Point ToImagePixel(Point pos)
    {
        var scale = ImageDisplayScale;
        return new Point(pos.X / scale, pos.Y / scale);
    }

    /// <summary>ルーペの描画完了時に、描画済み Bitmap へ線分オーバーレイ（破線）を描画する。</summary>
    /// <param name="sender">イベント送信元（LoupeControl）。</param>
    /// <param name="e">描画済み Bitmap と表示情報。</param>
    /// <remarks>
    /// イベントは表示へ反映する前に同期的に発火するため、ここで Bitmap に描画した内容がそのまま表示される。<br/>
    /// ドメイン座標（画像ピクセル）は <c>(ドメイン座標 − CaptureTopLeft) × Scale</c> でルーペ画像ピクセル座標へ変換する。
    /// </remarks>
    private void OnLoupeRendered(object? sender, LoupeRenderedEventArgs e)
    {
        DrawLoupeOverlay(e);
    }

    /// <summary>検出コンテキストのホバー/選択状態の変化でルーペを再生成する。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">プロパティ変更イベント。</param>
    /// <remarks>
    /// 一覧のホバー・クリック・再検出など、ルーペの中心が変わらない経路でも、<br/>
    /// 破線を描き直すために <see cref="LoupeControl.ForceUpdate"/> でルーペを再生成する<br/>
    /// （再生成後に <see cref="OnLoupeRendered"/> が発火し、新しい Bitmap へ破線が描画される）。
    /// </remarks>
    private void OnContextPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DetectionContext.LoupeOverlayLines))
        {
            Loupe.ForceUpdate();
        }
    }

    /// <summary>ルーペの Bitmap に線分オーバーレイ（選択中/ホバー中の線分の破線）を描画する。</summary>
    /// <param name="e">ルーペの表示情報（描画済み Bitmap・切り出し左上・スケール）。</param>
    /// <remarks>
    /// 対象の線分一覧（<see cref="DetectionContext.LoupeOverlayLines"/>）をドメイン座標 → ルーペ画像ピクセル座標へ変換し、<br/>
    /// 選択中 = オレンジ / ホバー中 = 赤の破線を Bitmap（WriteableBitmap）のピクセルバッファへ直接描画する。<br/>
    /// 切り出し領域の左上・対象の線分が無い場合は何も描画しない。
    /// </remarks>
    private void DrawLoupeOverlay(LoupeRenderedEventArgs e)
    {
        if (e.CaptureTopLeft is not { } topLeft)
        {
            return;
        }

        if (_ctx?.LoupeOverlayLines is not { Count: > 0 } lines)
        {
            return;
        }

        // 選択中 = オレンジ / ホバー中 = 赤。色は事前に選んでから描画する（ループ内のキャストを避ける）
        foreach (var line in lines)
        {
            var start = new Point((line.Start.X - topLeft.X) * e.Scale, (line.Start.Y - topLeft.Y) * e.Scale);
            var end = new Point((line.End.X - topLeft.X) * e.Scale, (line.End.Y - topLeft.Y) * e.Scale);

            // 検出された線分の座標をそのまま使って破線を描画する。
            // 線分の座標は LogicLineDetector がウィンドウ枠の境界（外縁）に補正済みのため、
            // ルーペ内でも枠の線上に表示される（見た目だけのスナップは行わない）
            DrawDashedLine(e.Bitmap, start, end, line.IsSelected ? s_selectedOverlayColor : s_hoverOverlayColor, DashPattern, e.Scale);
        }
    }

    /// <summary>ホバー中の線分のルーペ内表示色（赤）。</summary>
    private static readonly Color s_hoverOverlayColor = Color.Parse("#FF3B30");

    /// <summary>選択中の線分のルーペ内表示色（オレンジ）。</summary>
    private static readonly Color s_selectedOverlayColor = Color.Parse("#FF8C00");

    /// <summary>ルーペ内の線分オーバーレイの破線パターン（描画 4px・間隔 3px・ルーピクセル）。</summary>
    private static readonly double[] DashPattern = [4.0, 3.0];

    /// <summary>PbBitmap 上に破線の線分を描画する。</summary>
    /// <param name="bitmap">描画対象の PbBitmap。</param>
    /// <param name="start">線分の始点（Bitmap ピクセル座標）。</param>
    /// <param name="end">線分の終点（Bitmap ピクセル座標）。</param>
    /// <param name="color">描画色（不透明）。</param>
    /// <param name="dash">破線パターン（描画長・間隔長の交互リスト・ルーピクセル）。</param>
    /// <param name="thickness">線の太さ（ルーピクセル）。1 ドメインピクセル = ズーム倍率（<see cref="LoupeRenderedEventArgs.Scale"/>）分。</param>
    /// <remarks>
    /// 既存ピクセルを保持したまま、<paramref name="dash"/> のパターンで線分上のピクセルを塗る。<br/>
    /// 線の太さは <paramref name="thickness"/> の分だけ線分の両側へ広げて塗る（中心線の法線方向）。<br/>
    /// 描画は <see cref="PbBitmap.SetPixel"/> で行う（BGRA 変換はヘルパーに委譲）。
    /// </remarks>
    private static void DrawDashedLine(
        PbBitmap bitmap, Point start, Point end, Color color, IReadOnlyList<double> dash, double thickness)
    {
        var width = bitmap.Size.Width;
        var height = bitmap.Size.Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        // 破線パターンの合計長（周期）。正でない場合は何も描画しない
        double dashTotal = 0;
        foreach (var d in dash)
        {
            dashTotal += d;
        }
        if (dashTotal <= 0)
        {
            return;
        }

        // 線分の整数座標
        int x0 = (int)Math.Round(start.X);
        int y0 = (int)Math.Round(start.Y);
        int x1 = (int)Math.Round(end.X);
        int y1 = (int)Math.Round(end.Y);
        double dx = x1 - x0;
        double dy = y1 - y0;
        double length = Math.Sqrt(dx * dx + dy * dy);
        if (length < 1.0)
        {
            return;
        }

        // 線の太さ（最小 1px）。法線方向へ half ピクセルずつ広げる
        var half = Math.Max(0, (int)Math.Ceiling(thickness / 2.0) - 1);
        var ux = -dy / length; // 法線 X
        var uy = dx / length;  // 法線 Y

        // 線分を 1 ピクセル刻みで辿り、ダッシュパターンに応じて塗る
        int steps = Math.Max(1, (int)Math.Ceiling(length));
        for (int i = 0; i <= steps; i++)
        {
            double t = (double)i / steps;
            int px = (int)Math.Round(x0 + dx * t);
            int py = (int)Math.Round(y0 + dy * t);

            // 累積距離から描画区間（on）か間隔区間（off）かを判定する
            double pos = length * t % dashTotal;
            double acc = 0;
            bool draw = false;
            for (int j = 0; j + 1 < dash.Count; j += 2)
            {
                double onEnd = acc + dash[j];
                if (pos < onEnd)
                {
                    draw = true;
                    break;
                }
                acc = onEnd + dash[j + 1];
            }
            if (!draw)
            {
                continue;
            }

            // 中心点 + 法線方向へ太さ分のピクセルを塗る（範囲外は PbBitmapExtensions.TrySetPixel が無視）
            for (int n = -half; n <= half; n++)
            {
                int ox = (int)Math.Round(px + ux * n);
                int oy = (int)Math.Round(py + uy * n);
                bitmap.TrySetPixel(ox, oy, color);
            }
        }
    }

    /// <summary>画像上のマウス移動で線分のホバー状態とルーペ中心を更新する。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">ポインタイベント。</param>
    /// <remarks>
    /// ImageHost ローカル座標（Viewbox 縮小座標）を画像ピクセル座標へ変換してから<br/>
    /// ホバー判定（<see cref="DetectionContext.UpdateHover"/>）とルーペ中心に渡す。<br/>
    /// ルーペ上の線分オーバーレイは、ルーペの再描画完了（<see cref="OnLoupeRendered"/>）で更新される。
    /// </remarks>
    private void OnOverlayPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_ctx is null)
        {
            return;
        }

        var pos = ToImagePixel(e.GetPosition(ImageHost));
        _ctx.UpdateHover(pos);
        _ctx.LoupeCenter = pos;
        _ctx.IsLoupeVisible = true;
    }

    /// <summary>画像上にマウスが入ったらルーペを表示する。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">ポインタイベント。</param>
    private void OnImageHostPointerEntered(object? sender, PointerEventArgs e)
    {
        if (_ctx is not null)
        {
            _ctx.IsLoupeVisible = true;
        }
    }

    /// <summary>画像上からマウスが出たらルーペと座標表示を非表示にする。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">ポインタイベント。</param>
    private void OnImageHostPointerExited(object? sender, PointerEventArgs e)
        => _ctx?.ClearPointerOverlay();

    /// <summary>画像上のクリックで線分の選択状態をトグルする。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">ポインタイベント。</param>
    private void OnOverlayPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_ctx is not null)
        {
            _ctx.ToggleSelectAt(ToImagePixel(e.GetPosition(ImageHost)));
        }
    }
}
