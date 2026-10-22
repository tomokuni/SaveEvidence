using System.ComponentModel;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using EsUtil.Feature.Loupe;
using EsUtil.Ui.AvaloniaUI.Loupe;
using EsUtil.Platform.Core.Coord;
using EsUtil.SaveEvidence.Parts.LineDetection.Sample.ViewModels;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.SaveEvidence.Parts.LineDetection.Sample.Views;

/// <summary>画像 + 線分オーバーレイ + ルーペの共通表示ビュー（線分検出・データ検証ページで共有）。</summary>
/// <remarks>
/// 画像は Viewbox（Stretch=Uniform, DownOnly）で表示され、表示領域より大きい場合は縮小される。<br/>
/// このため ImageHost ローカル座標（縮小座標）と画像ピクセル座標は一致しない。<br/>
/// ポインタ位置は <see cref="ImageDisplayScale"/> で除算して画像ピクセル座標へ変換してから<br/>
/// ホバー判定（<see cref="DetectionContext.UpdateHover"/>）とルーペ中心（<see cref="DetectionContext.LoupeCenter"/>）に渡す。<br/>
/// ルーペは画像ピクセル座標 → ImageHost ローカル座標のスケーリングポート（<see cref="CoordTransform.ClientToSource"/>）で配置し、<br/>
/// マウス位置（= ルーペ中心）の右下に表示する。<br/>
/// ルーペ内の線分オーバーレイ（選択中/ホバー中の線分の破線）は、LoupeControl の <see cref="LoupeControl.Rendered"/> イベントの<br/>
/// 引数で受け取った Bitmap（WriteableBitmap）に直接破線を描画する（LoupeControl の内部計算を複製しない）。<br/>
/// ホバー/選択の変化（<see cref="DetectionContext.LoupeOverlayLines"/>）では <see cref="LoupeControl.RequestRender"/> で<br/>
/// ルーペを再生成し、新しい Bitmap に破線を再描画する。
/// </remarks>
public partial class LineCanvasView : UserControl
{
    /// <summary>ルーペをカーソルの右下へ配置するオフセット量（DIP）。</summary>
    private const double LoupePositionOffset = 24;

    /// <summary>Shared ViewModel（線分検出・データ検証ページ 共通の検出コンテキスト）。</summary>
    private DetectionContext? _detectionContext;

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

        _detectionContext = ctx;

        // ルーペの表示位置・サイズを自己計算させる（ImageHost ローカル DIP → 画像ピクセル座標の倍率ポート）
        Loupe.SetCoordTransform(CoordTransform.ClientToSource(() => 1.0 / ImageDisplayScale));
        Loupe.PositionOffset = LoupePositionOffset;

        // 設定はコアの単一値（Options）で指定する（計算に必要な値）
        Loupe.Options = Loupe.Options with
        {
            RequestedSize = 220,
            ZoomLevel = 4,
        };
        // 色・枠線の太さ・ラベル表示の可否は描画にだけ必要な設定のため、外観（Appearance）で指定する
        // （十字線を 20% の赤、外枠をライトグレーにする）
        Loupe.Appearance = new LoupeAppearance
        {
            CrossColor = DrawingColor.FromArgb(0x33, 0xFF, 0x00, 0x00),
            BorderColor = DrawingColor.FromArgb(0xFF, 0xCC, 0xCC, 0xCC),
            BorderThickness = 1,
            ShowCoordLabel = false,
        };

        // 描画完了イベントでルーペの Bitmap に線分オーバーレイ（破線）を描画する
        Loupe.Rendered += OnLoupeRendered;
        // ホバー/選択の変化（画像上・一覧・再検出）では、ルーペを再生成して破線を再描画する
        ctx.PropertyChanged += OnContextPropertyChanged;

        // 画像ホストのサイズ変更（Viewbox スケール変化）でルーペの座標変換ポートを更新する
        ImageHost.GetObservable(BoundsProperty).Subscribe(_ => OnImageHostSizeChanged());
    }

    /// <summary>画像ホストのサイズ変更時（Viewbox スケール変化）にルーペの座標変換ポートを更新する。</summary>
    private void OnImageHostSizeChanged()
        => Loupe.SetCoordTransform(CoordTransform.ClientToSource(() => 1.0 / ImageDisplayScale));

    /// <summary>画像の表示スケール（画像ピクセル座標 → ImageHost ローカル座標）を計算する。</summary>
    /// <remarks>
    /// Viewbox が画像を縮小表示する場合、ImageHost ローカル座標は画像ピクセル座標と一致しない。<br/>
    /// ポインタ位置の画像ピクセル座標への変換と、ルーペの座標変換ポート（<see cref="CoordTransform.ClientToSource"/>）に使用する。<br/>
    /// 画像未設定・サイズ 0 の場合は 1.0（等価）を返す。
    /// </remarks>
    private double ImageDisplayScale
    {
        get
        {
            if (_detectionContext?.Image is not { } image)
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

    /// <summary>ルーペの描画完了時に、ルーペの描画面へ線分オーバーレイ（破線）を描画する。</summary>
    /// <param name="sender">イベント送信元（LoupeControl）。</param>
    /// <param name="e">描画先の描画コンテキストと実際の描画計画。</param>
    /// <remarks>
    /// イベントはルーペの内容を描いた直後に発火するため、ここで描いた内容がルーペの上に重なる。<br/>
    /// 対象座標（画像ピクセル）は <c>(対象座標 − CaptureTopLeft) × Scale</c> でルーペ画像ピクセル座標へ変換する。
    /// </remarks>
    private void OnLoupeRendered(object? sender, LoupeRenderedEventArgs e)
    {
        DrawLoupeOverlay(e);
    }

    /// <summary>検出コンテキストのホバー/選択状態の変化でルーペを再描画する。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">プロパティ変更イベント。</param>
    /// <remarks>
    /// 一覧のホバー・クリック・再検出など、ルーペの中心が変わらない経路でも、<br/>
    /// 破線を描き直すために <see cref="LoupeControl.RequestRender"/> でルーペの再描画を要求する<br/>
    /// （再描画後に <see cref="OnLoupeRendered"/> が発火し、新しい描画で破線が描かれる）。
    /// </remarks>
    private void OnContextPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DetectionContext.LoupeOverlayLines))
        {
            Loupe.RequestRender();
        }
    }

    /// <summary>ルーペの描画面に線分オーバーレイ（選択中/ホバー中の線分の破線）を描画する。</summary>
    /// <param name="e">ルーペの描画完了通知（描画先の描画コンテキストと描画計画）。</param>
    /// <remarks>
    /// 対象の線分一覧（<see cref="DetectionContext.LoupeOverlayLines"/>）を対象座標 → ルーペ画像ピクセル座標へ変換し、<br/>
    /// 選択中 = オレンジ / ホバー中 = 赤の破線を描画 API で描く。<br/>
    /// 切り出し領域の左上・対象の線分が無い場合は何も描画しない。
    /// </remarks>
    private void DrawLoupeOverlay(LoupeRenderedEventArgs e)
    {
        if (e.Content.CaptureTopLeft is not { } topLeft)
        {
            return;
        }

        if (_detectionContext?.LoupeOverlayLines is not { Count: > 0 } lines)
        {
            return;
        }

        foreach (var line in lines)
        {
            var start = new Point((line.Start.X - topLeft.X) * e.Content.ZoomLevel, (line.Start.Y - topLeft.Y) * e.Content.ZoomLevel);
            var end = new Point((line.End.X - topLeft.X) * e.Content.ZoomLevel, (line.End.Y - topLeft.Y) * e.Content.ZoomLevel);

            // 検出された線分の座標をそのまま使って破線を描画する。
            // 線分の座標は LogicLineDetector がウィンドウ枠の境界（外縁）に補正済みのため、
            // ルーペ内でも枠の線上に表示される（見た目だけのスナップは行わない）
            DrawDashedLine(e.Context, start, end, line.IsSelected ? s_selectedOverlayColor : s_hoverOverlayColor, e.Content.ZoomLevel);
        }
    }

    /// <summary>ホバー中の線分のルーペ内表示色（赤）。</summary>
    private static readonly Color s_hoverOverlayColor = Color.Parse("#FF3B30");

    /// <summary>選択中の線分のルーペ内表示色（オレンジ）。</summary>
    private static readonly Color s_selectedOverlayColor = Color.Parse("#FF8C00");

    /// <summary>ルーペ内の線分オーバーレイの破線パターン（描画 4px・間隔 3px・ルーピクセル）。</summary>
    private static readonly double[] s_dashPattern = [4.0, 3.0];

    /// <summary>ルーペの描画面へ破線の線分を描画する。</summary>
    /// <param name="context">描画先の描画コンテキスト（描画中のみ有効）。</param>
    /// <param name="start">線分の始点（ルーペ画像ピクセル座標）。</param>
    /// <param name="end">線分の終点（ルーペ画像ピクセル座標）。</param>
    /// <param name="color">描画色。</param>
    /// <param name="thickness">線の太さ（ルーペピクセル）。 対象 1 ピクセル = ズーム倍率（<see cref="LoupeDrawPlan.ZoomLevel"/>）分。</param>
    /// <remarks>
    /// 描画 API へ破線を指定して線分を描く（<see cref="s_dashPattern"/> のパターンを線幅単位で与える）。<br/>
    /// 描画面は物理ピクセルで倍率が適用済みのため、太さ・破線の単位はルーペピクセルである。
    /// </remarks>
    private static void DrawDashedLine(
        DrawingContext context, Point start, Point end, Color color, double thickness)
    {
        var pen = new Pen(new SolidColorBrush(color), thickness)
        {
            DashStyle = new DashStyle(s_dashPattern, 0),
        };

        context.DrawLine(pen, start, end);
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
        if (_detectionContext is null)
        {
            return;
        }

        var pos = ToImagePixel(e.GetPosition(ImageHost));
        _detectionContext.UpdateHover(pos);
        _detectionContext.LoupeCenter = pos;
        _detectionContext.IsLoupeVisible = true;
    }

    /// <summary>画像上にマウスが入ったらルーペを表示する。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">ポインタイベント。</param>
    private void OnImageHostPointerEntered(object? sender, PointerEventArgs e)
    {
        _detectionContext?.IsLoupeVisible = true;
    }

    /// <summary>画像上からマウスが出たらルーペと座標表示を非表示にする。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">ポインタイベント。</param>
    private void OnImageHostPointerExited(object? sender, PointerEventArgs e)
        => _detectionContext?.ClearPointerOverlay();

    /// <summary>画像上のクリックで線分の選択状態をトグルする。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">ポインタイベント。</param>
    private void OnOverlayPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _detectionContext?.ToggleSelectAt(ToImagePixel(e.GetPosition(ImageHost)));
    }
}
