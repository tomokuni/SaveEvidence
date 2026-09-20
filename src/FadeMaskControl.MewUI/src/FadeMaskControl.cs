using Aprillz.MewUI;
using Aprillz.MewUI.Skia.Controls;
using Estable.FadeMask;
using Estable.MewUI.FadeMask.Logging;
using Estable.Platform.Coordinate;
using Estable.Platform.Geometry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;
using DrawingColor = System.Drawing.Color;

namespace Estable.MewUI.FadeMask;

/// <summary>領域外を暗転させ、選択領域をハイライトするオーバーレイコントロール（MewUI 版）。</summary>
/// <remarks>
/// Avalonia 版 <c>FadeMaskControl</c> と同じ UI 非依存コア（<c>FadeMaskControl.Core</c>）を使い、<br/>
/// 描画層のみを <see cref="SkiaCanvasView"/> で実装する。<br/>
/// <br/>
/// <b>入力しない</b>。本コントロールは装飾専用であり、ホスト側で <c>IsHitTestVisible = false</c> にして<br/>
/// 下のコントロールへ入力を通す（Avalonia 版と同じ使い方）。<br/>
/// <br/>
/// <b>ラベル</b>: サイズラベル（1 行目）とサブライン（2 行目以降）を穴あけ矩形の左上へ縦に積む。<br/>
/// 文言はホストが <see cref="SizeLabelTextProvider"/> と <see cref="SubLines"/> で与え、<br/>
/// 背景・文字原点・次の行の位置はコアの <c>FadeMaskLabelLayout</c> が単一所有する。<br/>
/// 非 ASCII を含む文言は字形を持つフォントへフォールバックするため豆腐（□）にならない。
/// </remarks>
public sealed class FadeMaskControl : SkiaCanvasView
{
    /// <summary>暗転の既定色（黒 50%）。</summary>
    private static readonly DrawingColor s_defaultOverlayColor = DrawingColor.FromArgb(128, 0, 0, 0);

    /// <summary>枠線の既定色（ダークグレー・不透明）。</summary>
    private static readonly DrawingColor s_defaultBorderColor = DrawingColor.FromArgb(255, 169, 169, 169);

    /// <summary><see cref="SelectionRect"/> プロパティ（論理座標＝ドメイン座標の選択矩形）。</summary>
    public static readonly MewProperty<EsRect?> SelectionRectProperty =
        MewProperty<EsRect?>.Register<FadeMaskControl>(
            nameof(SelectionRect), null, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="HighlightRect"/> プロパティ（論理座標＝ドメイン座標のハイライト矩形）。</summary>
    public static readonly MewProperty<EsRect?> HighlightRectProperty =
        MewProperty<EsRect?>.Register<FadeMaskControl>(
            nameof(HighlightRect), null, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="HasSelection"/> プロパティ（選択が有効か）。</summary>
    public static readonly MewProperty<bool> HasSelectionProperty =
        MewProperty<bool>.Register<FadeMaskControl>(nameof(HasSelection), false, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="HasHighlight"/> プロパティ（ハイライトが有効か）。</summary>
    public static readonly MewProperty<bool> HasHighlightProperty =
        MewProperty<bool>.Register<FadeMaskControl>(nameof(HasHighlight), false, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="OverlayColor"/> プロパティ（暗転の色。不透明度はアルファに統合する）。</summary>
    public static readonly MewProperty<DrawingColor> OverlayColorProperty =
        MewProperty<DrawingColor>.Register<FadeMaskControl>(
            nameof(OverlayColor), s_defaultOverlayColor, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="BorderColor"/> プロパティ（枠線の色。不透明度はアルファに統合する）。</summary>
    public static readonly MewProperty<DrawingColor> BorderColorProperty =
        MewProperty<DrawingColor>.Register<FadeMaskControl>(
            nameof(BorderColor), s_defaultBorderColor, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="BorderThickness"/> プロパティ（枠線の太さ・DIP）。</summary>
    public static readonly MewProperty<double> BorderThicknessProperty =
        MewProperty<double>.Register<FadeMaskControl>(nameof(BorderThickness), 2.0, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="ShowFullOverlay"/> プロパティ（選択なしでも全面を暗転するか）。</summary>
    public static readonly MewProperty<bool> ShowFullOverlayProperty =
        MewProperty<bool>.Register<FadeMaskControl>(nameof(ShowFullOverlay), false, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="RenderScaling"/> プロパティ（DIP → 物理ピクセルのスケーリング係数）。</summary>
    public static readonly MewProperty<double> RenderScalingProperty =
        MewProperty<double>.Register<FadeMaskControl>(nameof(RenderScaling), 1.0, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="Scaling"/> プロパティ（座標変換ポートへ適用する倍率）。</summary>
    /// <remarks><see cref="Port"/> が設定されている場合は使用しない（等倍スケール専用の簡易指定）。</remarks>
    public static readonly MewProperty<double> ScalingProperty =
        MewProperty<double>.Register<FadeMaskControl>(nameof(Scaling), 1.0, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="Port"/> プロパティ（論理座標 → DIP の座標変換ポート）。</summary>
    /// <remarks>
    /// 設定すると <see cref="Scaling"/> より優先され、オフセットを含む変換（ズームビューのレターボックス等）でも<br/>
    /// 選択矩形と暗転マスクが一致する。null の場合は <see cref="Scaling"/> による等倍スケールを使用する。
    /// </remarks>
    public static readonly MewProperty<ICoordTransform?> PortProperty =
        MewProperty<ICoordTransform?>.Register<FadeMaskControl>(nameof(Port), null, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="ShowSizeLabel"/> プロパティ（サイズラベルを表示するか）。</summary>
    public static readonly MewProperty<bool> ShowSizeLabelProperty =
        MewProperty<bool>.Register<FadeMaskControl>(nameof(ShowSizeLabel), true, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="SizeLabelColor"/> プロパティ（サイズラベルの文字色）。</summary>
    public static readonly MewProperty<DrawingColor> SizeLabelColorProperty =
        MewProperty<DrawingColor>.Register<FadeMaskControl>(
            nameof(SizeLabelColor), DrawingColor.White, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="SizeLabelBackgroundColor"/> プロパティ（サイズラベルの背景色）。</summary>
    /// <remarks>アルファ 0 の場合は黒 50% へフォールバックする（Avalonia 版と同一）。</remarks>
    public static readonly MewProperty<DrawingColor> SizeLabelBackgroundColorProperty =
        MewProperty<DrawingColor>.Register<FadeMaskControl>(
            nameof(SizeLabelBackgroundColor), DrawingColor.FromArgb(153, 0, 0, 0), MewPropertyOptions.AffectsRender);

    /// <summary><see cref="SizeLabelFontSize"/> プロパティ（サイズラベルのフォントサイズ・DIP）。</summary>
    public static readonly MewProperty<double> SizeLabelFontSizeProperty =
        MewProperty<double>.Register<FadeMaskControl>(nameof(SizeLabelFontSize), 14.0, MewPropertyOptions.AffectsRender);

    /// <summary>論理座標（ドメイン）の選択矩形を取得または設定する。</summary>
    /// <value>選択矩形。null の場合は穴あけなし。</value>
    /// <remarks>設定（変更）時は変更前後をログ出力し、再描画を要求する。</remarks>
    public EsRect? SelectionRect
    {
        get => GetValue(SelectionRectProperty);
        set
        {
            var oldValue = GetValue(SelectionRectProperty);
            SetValue(SelectionRectProperty, value);

            if (oldValue != value)
            {
                Log.LogSelectionRectChanged(FormatRect(oldValue), FormatRect(value));
            }
        }
    }

    /// <summary>サイズラベルを表示するかどうかを取得または設定する。</summary>
    /// <value>true の場合は穴あけ矩形の左上にサイズラベル（1 行目）を描画する。</value>
    public bool ShowSizeLabel
    {
        get => GetValue(ShowSizeLabelProperty);
        set => SetValue(ShowSizeLabelProperty, value);
    }

    /// <summary>サイズラベルの文字色を取得または設定する。</summary>
    /// <value>文字色（不透明度はアルファに統合する）。</value>
    public DrawingColor SizeLabelColor
    {
        get => GetValue(SizeLabelColorProperty);
        set => SetValue(SizeLabelColorProperty, value);
    }

    /// <summary>サイズラベルの背景色を取得または設定する。</summary>
    /// <value>背景色（不透明度はアルファに統合する）。アルファ 0 の場合は黒 50% へフォールバックする。</value>
    public DrawingColor SizeLabelBackgroundColor
    {
        get => GetValue(SizeLabelBackgroundColorProperty);
        set => SetValue(SizeLabelBackgroundColorProperty, value);
    }

    /// <summary>サイズラベルのフォントサイズを取得または設定する。</summary>
    /// <value>フォントサイズ（DIP）。</value>
    public double SizeLabelFontSize
    {
        get => GetValue(SizeLabelFontSizeProperty);
        set => SetValue(SizeLabelFontSizeProperty, value);
    }

    /// <summary>サイズラベルのテキストを提供する関数を取得または設定する。</summary>
    /// <value>サイズラベルのテキストを返す関数。null または空文字の場合はサイズラベルを描画しない。</value>
    /// <remarks>
    /// 座標系はホストが知るため（画面物理ピクセル / 画像ピクセル / DIP）、文言の生成はホストへ委譲する。<br/>
    /// 描画のたびに評価されるため、ドラッグ中の選択矩形の変化にも追従できる。
    /// </remarks>
    public Func<string?>? SizeLabelTextProvider { get; set; }

    /// <summary>サイズラベルの下に表示する行（2 行目以降）を取得または設定する。</summary>
    /// <value>行の一覧。null または空の場合は描画しない。</value>
    /// <remarks>
    /// 操作ラベル（モード別の確定・キャンセル方法）と情報ラベル（ウィンドウタイトル）をホストが組み立てて渡す。<br/>
    /// 文言の選定は <c>FadeMaskLabels</c>（コア）が単一所有する。
    /// </remarks>
    public IReadOnlyList<FadeMaskSubLine>? SubLines { get; set; }

    /// <summary>論理座標 → DIP の座標変換ポートを取得または設定する。</summary>
    /// <value>座標変換ポート。null の場合は <see cref="Scaling"/> による等倍スケールを使用する。</value>
    /// <remarks>設定（変更）時は設定状態をログ出力し、再描画を要求する。</remarks>
    public ICoordTransform? Port
    {
        get => GetValue(PortProperty);
        set
        {
            var oldValue = GetValue(PortProperty);
            SetValue(PortProperty, value);

            if (!ReferenceEquals(oldValue, value))
            {
                Log.LogCoordinateTransform(value is not null);
            }
        }
    }

    /// <summary>論理座標（ドメイン）のハイライト矩形を取得または設定する。</summary>
    /// <value>ハイライト矩形。null の場合は穴あけなし。選択矩形より優先される。</value>
    /// <remarks>設定（変更）時は変更前後をログ出力し、再描画を要求する。</remarks>
    public EsRect? HighlightRect
    {
        get => GetValue(HighlightRectProperty);
        set
        {
            var oldValue = GetValue(HighlightRectProperty);
            SetValue(HighlightRectProperty, value);

            if (oldValue != value)
            {
                Log.LogHighlightRectChanged(FormatRect(oldValue), FormatRect(value));
            }
        }
    }

    /// <summary>状態変更ログの出力先を取得または設定する。</summary>
    /// <value>ロガー。null を設定した場合は <see cref="NullLogger"/> へフォールバックする（出力なし）。</value>
    /// <remarks>
    /// C#14 の <c>field</c> キーワードでバッキング フィールドを暗黙定義する。<br/>
    /// getter / setter とも null を <see cref="NullLogger"/> へ安全に変換する。
    /// </remarks>
    public ILogger Log
    {
        get => field ?? NullLogger.Instance;
        set => field = value ?? NullLogger.Instance;
    }

    /// <summary>ログ用に矩形を整形する。</summary>
    /// <param name="rect">矩形（未設定は null）。</param>
    /// <returns><c>(X, Y, Width, Height)</c> 形式の文字列。未設定は <c>null</c>。</returns>
    private static string FormatRect(EsRect? rect) => rect is { } r ? $"({r.X}, {r.Y}, {r.Width}, {r.Height})" : "null";

    /// <summary>選択が有効かどうかを取得または設定する。</summary>
    /// <value>true の場合、<see cref="SelectionRect"/> を穴あけ矩形として使う。</value>
    public bool HasSelection
    {
        get => GetValue(HasSelectionProperty);
        set => SetValue(HasSelectionProperty, value);
    }

    /// <summary>ハイライトが有効かどうかを取得または設定する。</summary>
    /// <value>true の場合、<see cref="HighlightRect"/> を穴あけ矩形として使う（選択矩形より優先）。</value>
    public bool HasHighlight
    {
        get => GetValue(HasHighlightProperty);
        set => SetValue(HasHighlightProperty, value);
    }

    /// <summary>暗転の色を取得または設定する。</summary>
    /// <value>暗転の色（アルファで不透明度を表す）。アルファ 0 の場合は暗転しない。</value>
    public DrawingColor OverlayColor
    {
        get => GetValue(OverlayColorProperty);
        set => SetValue(OverlayColorProperty, value);
    }

    /// <summary>枠線の色を取得または設定する。</summary>
    /// <value>枠線の色（アルファで不透明度を表す）。アルファ 0 の場合は枠線を描画しない。</value>
    public DrawingColor BorderColor
    {
        get => GetValue(BorderColorProperty);
        set => SetValue(BorderColorProperty, value);
    }

    /// <summary>枠線の太さ（DIP）を取得または設定する。</summary>
    /// <value>枠線の太さ（DIP）。0.5 未満は 0.5 に正規化される。</value>
    public double BorderThickness
    {
        get => GetValue(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    /// <summary>穴あけ矩形が無くても全面を暗転するかどうかを取得または設定する。</summary>
    /// <value>true の場合は選択が無くても暗転する。</value>
    public bool ShowFullOverlay
    {
        get => GetValue(ShowFullOverlayProperty);
        set => SetValue(ShowFullOverlayProperty, value);
    }

    /// <summary>表示 DIP を物理ピクセルへ変換するスケーリング係数を取得または設定する。</summary>
    /// <value>スケーリング係数。DPI 変更時に自動更新される。</value>
    public double RenderScaling
    {
        get => GetValue(RenderScalingProperty);
        set => SetValue(RenderScalingProperty, value);
    }

    /// <summary>座標変換ポートへ適用する倍率を取得または設定する。</summary>
    /// <value>論理座標（ドメイン）→ DIP の倍率。DPI スケーリングとは独立する。</value>
    /// <remarks>マルチスクリーン補正等でホストが設定する（Avalonia 版の <c>Scaling</c> と同じ用途）。</remarks>
    public double Scaling
    {
        get => GetValue(ScalingProperty);
        set => SetValue(ScalingProperty, value);
    }

    /// <summary>暗転オーバーレイコントロールを初期化する。</summary>
    /// <remarks>静止画のため連続再描画を無効化し、キャンバスの描画イベントを購読する。</remarks>
    public FadeMaskControl()
    {
        ContinuousAnimation = false;
        PaintSurface += OnPaintSurface;
    }

    /// <summary>要求サイズを返す（親の残余を埋める）。</summary>
    /// <param name="availableSize">親から提示された利用可能サイズ（使用しない）。</param>
    /// <returns>常に <c>Size.Empty</c>。</returns>
    /// <remarks>オーバーレイは親の領域全体を覆うため、自身の要求サイズを持たない。</remarks>
    protected override Size MeasureContent(Size availableSize) => Size.Empty;

    /// <summary>DPI の変更時に表示スケーリング係数を追従させる。</summary>
    /// <param name="oldDpi">変更前の DPI。</param>
    /// <param name="newDpi">変更後の DPI。</param>
    protected override void OnDpiChanged(uint oldDpi, uint newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        RenderScaling = newDpi / 96.0;
    }

    /// <summary>暗転オーバーレイと枠線を描画する。</summary>
    /// <param name="canvas">描画先のキャンバス（物理ピクセル）。</param>
    /// <param name="info">描画先の情報（幅・高さ）。</param>
    /// <remarks>
    /// キャンバスを DPI スケーリングしてから DIP 座標で描画する（コアの計算と座標系を一致させる）。<br/>
    /// 穴あけ矩形は論理座標から座標変換ポート経由で DIP へ変換する（選択は閉区間 +1、ハイライトは +0）。
    /// </remarks>
    private void OnPaintSurface(SKCanvas canvas, SKImageInfo info)
    {
        canvas.Clear(SKColors.Transparent);

        var scaling = RenderScaling > 0 ? RenderScaling : 1.0;
        var viewport = new EsSize(info.Width / scaling, info.Height / scaling);
        var client = new EsRect(0, 0, viewport.Width, viewport.Height);

        var port = CreatePort();
        var selectionRect = HasSelection && SelectionRect is { } selection
            ? FadeMaskOverlayGeometry.ToDipRect(selection, port)
            : (EsRect?)null;
        var highlightRect = HasHighlight && HighlightRect is { } highlight
            ? FadeMaskOverlayGeometry.ToDipRect(highlight, port, closedInterval: false)
            : (EsRect?)null;

        var holeRect = FadeMaskOverlayGeometry.ResolveHoleRect(
            HasHighlight, highlightRect, HasSelection, selectionRect);

        canvas.Save();
        canvas.Scale((float)scaling);
        try
        {
            FadeMaskSkiaRenderer.Render(
                canvas, client, holeRect, OverlayColor, BorderColor, BorderThickness, ShowFullOverlay);

            // ラベル（サイズラベル・サブライン）は穴あけ矩形の左上に縦へ積む（穴あけが無い場合は描画しない）
            if (holeRect is { } labels)
            {
                FadeMaskSkiaRenderer.DrawLabels(
                    canvas,
                    labels,
                    ShowSizeLabel,
                    SizeLabelTextProvider?.Invoke(),
                    SizeLabelFontSize,
                    SizeLabelColor,
                    SizeLabelBackgroundColor,
                    SubLines);
            }
        }
        finally
        {
            canvas.Restore();
        }
    }

    /// <summary>座標変換ポートを生成する（<see cref="Port"/> を優先し、未設定の場合は <see cref="Scaling"/> を等倍スケールとして適用する）。</summary>
    /// <returns>座標変換ポート。</returns>
    private ICoordTransform CreatePort()
    {
        if (Port is { } port)
        {
            return port;
        }

        return Scaling > 0 && Scaling != 1.0
            ? CoordTransform.Scale(Scaling)
            : CoordTransform.Identity;
    }
}
