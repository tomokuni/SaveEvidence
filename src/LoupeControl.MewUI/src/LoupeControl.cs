using Aprillz.MewUI;
using Aprillz.MewUI.Skia.Controls;
using Estable.Loupe;
using Estable.MewUI.Loupe.Logging;
using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;
using MewUIColor = Aprillz.MewUI.Color;
using static Estable.Loupe.LoupeValueCoercer;
using EsUtil.Platform.Core;

namespace Estable.MewUI.Loupe;

/// <summary>拡大鏡（ルーペ）コントロール（MewUI 版）。</summary>
/// <remarks>
/// Avalonia 版 <c>LoupeControl</c> と同一の UI 非依存コア（<c>LoupeControl.Core</c>）を使い、<br/>
/// 描画層のみを <see cref="SkiaCanvasView"/> で実装する（同じ設定・同じ見た目になる）。<br/>
/// <br/>
/// <b>入力</b>: <see cref="Source"/>（拡大元の 100% 画像）と <see cref="CenterPoint"/>（元画像ピクセル座標）。<br/>
/// <b>外観</b>: 表示サイズ・拡大率・市松模様・十字線・外枠・座標ラベル。<br/>
/// <b>描画</b>: 中間ビットマップを生成せず、画面のキャンバスへ直接描画する（Avalonia 版との差）。<br/>
/// 数値の安全域への補正は <see cref="LoupeValueCoercer"/>（コア）が単一所有する。<br/>
/// <br/>
/// <b>ホスト側の操作</b>: <see cref="Source"/> を設定して <see cref="CenterPoint"/> を現在位置へ更新すると、<br/>
/// ルーペがその位置を拡大表示する（描画はプロパティ変更で自動的に無効化される）。
/// </remarks>
public sealed class LoupeControl : SkiaCanvasView
{
    /// <summary><see cref="Source"/> プロパティ（拡大元の 100% 画像）。</summary>
    public static readonly MewProperty<EsBitmap?> SourceProperty =
        MewProperty<EsBitmap?>.Register<LoupeControl>(nameof(Source), null, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="CenterPoint"/> プロパティ（拡大中心の元画像ピクセル座標）。</summary>
    public static readonly MewProperty<EsPoint?> CenterPointProperty =
        MewProperty<EsPoint?>.Register<LoupeControl>(nameof(CenterPoint), null, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="RequestedSize"/> プロパティ（1〜4096 に補正）。</summary>
    /// <remarks><see cref="MeasureContent"/> が要求サイズを返すため、変更はレイアウトにも影響する。</remarks>
    public static readonly MewProperty<int> RequestedSizeProperty =
        MewProperty<int>.Register<LoupeControl>(
            nameof(RequestedSize), RequestedSizeDefault,
            MewPropertyOptions.AffectsRender | MewPropertyOptions.AffectsLayout);

    /// <summary><see cref="RenderScaling"/> プロパティ（表示 DIP → 物理ピクセルのスケーリング係数）。</summary>
    public static readonly MewProperty<double> RenderScalingProperty =
        MewProperty<double>.Register<LoupeControl>(nameof(RenderScaling), RenderScalingDefault, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="ZoomLevel"/> プロパティ（1〜64 に補正）。</summary>
    public static readonly MewProperty<int> ZoomLevelProperty =
        MewProperty<int>.Register<LoupeControl>(nameof(ZoomLevel), ZoomLevelDefault, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="CrossColor"/> プロパティ（十字線の色。不透明度はアルファに統合）。</summary>
    public static readonly MewProperty<MewUIColor?> CrossColorProperty =
        MewProperty<MewUIColor?>.Register<LoupeControl>(nameof(CrossColor), null, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="CheckerSize"/> プロパティ（1〜128 に補正）。</summary>
    public static readonly MewProperty<int> CheckerSizeProperty =
        MewProperty<int>.Register<LoupeControl>(nameof(CheckerSize), CheckerSizeDefault, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="CheckerColorA"/> プロパティ（市松模様の明色）。</summary>
    public static readonly MewProperty<MewUIColor?> CheckerColorAProperty =
        MewProperty<MewUIColor?>.Register<LoupeControl>(nameof(CheckerColorA), null, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="CheckerColorB"/> プロパティ（市松模様の暗色）。</summary>
    public static readonly MewProperty<MewUIColor?> CheckerColorBProperty =
        MewProperty<MewUIColor?>.Register<LoupeControl>(nameof(CheckerColorB), null, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="FrameColor"/> プロパティ（外枠の色）。</summary>
    public static readonly MewProperty<MewUIColor?> FrameColorProperty =
        MewProperty<MewUIColor?>.Register<LoupeControl>(nameof(FrameColor), null, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="FrameWidth"/> プロパティ（1〜64 に補正）。</summary>
    public static readonly MewProperty<int> FrameWidthProperty =
        MewProperty<int>.Register<LoupeControl>(nameof(FrameWidth), FrameWidthDefault, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="ShowCoordLabel"/> プロパティ（上端中央の座標ラベルの表示可否）。</summary>
    public static readonly MewProperty<bool> ShowCoordLabelProperty =
        MewProperty<bool>.Register<LoupeControl>(nameof(ShowCoordLabel), true, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="CoordLabelColor"/> プロパティ（座標ラベルの文字色）。</summary>
    public static readonly MewProperty<MewUIColor?> CoordLabelColorProperty =
        MewProperty<MewUIColor?>.Register<LoupeControl>(nameof(CoordLabelColor), null, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="CoordLabelBackgroundColor"/> プロパティ（座標ラベルの背景色。不透明度はアルファに統合）。</summary>
    public static readonly MewProperty<MewUIColor?> CoordLabelBackgroundColorProperty =
        MewProperty<MewUIColor?>.Register<LoupeControl>(nameof(CoordLabelBackgroundColor), null, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="CoordLabelFontSize"/> プロパティ（6〜48 に補正）。</summary>
    public static readonly MewProperty<double> CoordLabelFontSizeProperty =
        MewProperty<double>.Register<LoupeControl>(nameof(CoordLabelFontSize), CoordLabelFontSizeDefault, MewPropertyOptions.AffectsRender);

    /// <summary>拡大元の 100% 画像を取得または設定する。</summary>
    /// <value>拡大元の画像。null の場合は市松模様のみを表示する。</value>
    /// <remarks>設定（変更）時は変更前後をログ出力する（画像は参照の同一性で比較する）。</remarks>
    public EsBitmap? Source
    {
        get => GetValue(SourceProperty);
        set
        {
            var oldValue = GetValue(SourceProperty);
            SetValue(SourceProperty, value);

            if (!ReferenceEquals(oldValue, value))
            {
                Log.LogSettingChanged(nameof(Source), DescribeImage(oldValue), DescribeImage(value));
            }
        }
    }

    /// <summary>拡大表示する中心座標（元画像ピクセル座標）を取得または設定する。</summary>
    /// <value>拡大中心。null の場合は市松模様のみを表示する。</value>
    /// <remarks>設定（変更）時は変更前後をログ出力する。</remarks>
    public EsPoint? CenterPoint
    {
        get => GetValue(CenterPointProperty);
        set
        {
            var oldValue = GetValue(CenterPointProperty);
            SetValue(CenterPointProperty, value);

            if (oldValue != value)
            {
                Log.LogSettingChanged(nameof(CenterPoint), FormatPoint(oldValue), FormatPoint(value));
            }
        }
    }

    /// <summary>ルーペの基本サイズ（DIP）を取得または設定する。</summary>
    /// <value>表示サイズ（DIP）。範囲外は 1〜4096 へ補正される。</value>
    /// <remarks>設定（変更）時は変更前後をログ出力する（補正後の値で記録する）。</remarks>
    public int RequestedSize
    {
        get => GetValue(RequestedSizeProperty);
        set
        {
            var oldValue = GetValue(RequestedSizeProperty);
            var coerced = CoerceRequestedSize(value);
            SetValue(RequestedSizeProperty, coerced);

            if (oldValue != coerced)
            {
                Log.LogSettingChanged(nameof(RequestedSize), oldValue.ToString(), coerced.ToString());
            }
        }
    }

    /// <summary>表示 DIP を物理ピクセルへ変換するスケーリング係数を取得または設定する。</summary>
    /// <value>スケーリング係数。範囲外・非有限は 0.1〜8.0 または既定値へ補正される。</value>
    /// <remarks>設定（変更）時は変更前後をログ出力する（補正後の値で記録する）。</remarks>
    public double RenderScaling
    {
        get => GetValue(RenderScalingProperty);
        set
        {
            var oldValue = GetValue(RenderScalingProperty);
            var coerced = CoerceRenderScaling(value);
            SetValue(RenderScalingProperty, coerced);

            if (oldValue != coerced)
            {
                Log.LogSettingChanged(nameof(RenderScaling), oldValue.ToString(), coerced.ToString());
            }
        }
    }

    /// <summary>拡大率を取得または設定する。</summary>
    /// <value>拡大率（偶数推奨）。範囲外は 1〜64 へ補正される。</value>
    /// <remarks>設定（変更）時は変更前後をログ出力する（補正後の値で記録する）。</remarks>
    public int ZoomLevel
    {
        get => GetValue(ZoomLevelProperty);
        set
        {
            var oldValue = GetValue(ZoomLevelProperty);
            var coerced = CoerceZoomLevel(value);
            SetValue(ZoomLevelProperty, coerced);

            if (oldValue != coerced)
            {
                Log.LogSettingChanged(nameof(ZoomLevel), oldValue.ToString(), coerced.ToString());
            }
        }
    }

    /// <summary>十字線の色を取得または設定する。</summary>
    /// <value>十字線の色。null の場合は描画しない。</value>
    public MewUIColor? CrossColor
    {
        get => GetValue(CrossColorProperty);
        set => SetValue(CrossColorProperty, value);
    }

    /// <summary>市松模様の 1 タイルサイズ（DIP）を取得または設定する。</summary>
    /// <value>タイルサイズ（DIP）。範囲外は 1〜128 へ補正される。</value>
    public int CheckerSize
    {
        get => GetValue(CheckerSizeProperty);
        set => SetValue(CheckerSizeProperty, CoerceCheckerSize(value));
    }

    /// <summary>市松模様の色A（明色）を取得または設定する。</summary>
    /// <value>明色。null の場合は透明。</value>
    public MewUIColor? CheckerColorA
    {
        get => GetValue(CheckerColorAProperty);
        set => SetValue(CheckerColorAProperty, value);
    }

    /// <summary>市松模様の色B（暗色）を取得または設定する。</summary>
    /// <value>暗色。null の場合は透明。</value>
    public MewUIColor? CheckerColorB
    {
        get => GetValue(CheckerColorBProperty);
        set => SetValue(CheckerColorBProperty, value);
    }

    /// <summary>外枠の色を取得または設定する。</summary>
    /// <value>外枠の色。null の場合は描画しない。</value>
    public MewUIColor? FrameColor
    {
        get => GetValue(FrameColorProperty);
        set => SetValue(FrameColorProperty, value);
    }

    /// <summary>外枠の太さを取得または設定する。</summary>
    /// <value>外枠の太さ（DIP）。範囲外は 1〜64 へ補正される。</value>
    public int FrameWidth
    {
        get => GetValue(FrameWidthProperty);
        set => SetValue(FrameWidthProperty, CoerceFrameWidth(value));
    }

    /// <summary>上端中央に中心座標ラベルを表示するかどうかを取得または設定する。</summary>
    /// <value>true の場合は <see cref="CenterPoint"/> を <c>(x, y)</c> 形式で表示する。</value>
    public bool ShowCoordLabel
    {
        get => GetValue(ShowCoordLabelProperty);
        set => SetValue(ShowCoordLabelProperty, value);
    }

    /// <summary>中心座標ラベルの文字色を取得または設定する。</summary>
    /// <value>文字色。null の場合は白。</value>
    public MewUIColor? CoordLabelColor
    {
        get => GetValue(CoordLabelColorProperty);
        set => SetValue(CoordLabelColorProperty, value);
    }

    /// <summary>中心座標ラベルの背景色を取得または設定する。</summary>
    /// <value>背景色（不透明度はアルファに統合）。null の場合は黒 60%。</value>
    public MewUIColor? CoordLabelBackgroundColor
    {
        get => GetValue(CoordLabelBackgroundColorProperty);
        set => SetValue(CoordLabelBackgroundColorProperty, value);
    }

    /// <summary>中心座標ラベルのフォントサイズを取得または設定する。</summary>
    /// <value>フォントサイズ（DIP）。範囲外・非有限は 6〜48 または既定値へ補正される。</value>
    public double CoordLabelFontSize
    {
        get => GetValue(CoordLabelFontSizeProperty);
        set => SetValue(CoordLabelFontSizeProperty, CoerceCoordLabelFontSize(value));
    }

    /// <summary><see cref="BackgroundColor"/> プロパティ（市松模様の下地色）。</summary>
    public static readonly MewProperty<MewUIColor> BackgroundColorProperty =
        MewProperty<MewUIColor>.Register<LoupeControl>(
            nameof(BackgroundColor), MewUIColor.FromArgb(255, 255, 255, 255), MewPropertyOptions.AffectsRender);

    /// <summary>市松模様の下地色を取得または設定する。</summary>
    /// <value>下地の色。既定は白。</value>
    /// <remarks>
    /// 市松模様の色（<see cref="CheckerColorA"/> / <see cref="CheckerColorB"/>）は半透明のため、<br/>
    /// 下地を塗らないと後ろの要素（プレビューの画像等）が透けて見える。<br/>
    /// Avalonia 版はウィンドウ背景の上へ重ねて描くため透けない（見た目を揃えるためホストがテーマの背景色を設定する）。
    /// </remarks>
    public MewUIColor BackgroundColor
    {
        get => GetValue(BackgroundColorProperty);
        set => SetValue(BackgroundColorProperty, value);
    }

    /// <summary>ルーペコントロールを初期化する。</summary>
    /// <remarks>
    /// 静的画像を描画するため連続再描画を無効化し、キャンバスの描画イベントを購読する。
    /// </remarks>
    public LoupeControl()
    {
        ContinuousAnimation = false;
        PaintSurface += OnPaintSurface;
    }

    /// <summary>ルーペの描画完了を通知するイベント（描画先キャンバスと表示情報を渡す）。</summary>
    /// <remarks>
    /// 拡大画像の描画結果（<see cref="LoupeRenderedEventArgs.Canvas"/>）と表示状態を通知する。<br/>
    /// イベントは本コントロールの描画直後（同一フレーム内）に発火するため、ホスト側は<br/>
    /// イベントハンドラ内でキャンバスへオーバーレイ（選択矩形の破線等）を追加描画できる。<br/>
    /// 描画位置の計算は <see cref="LoupeRenderedEventArgs.CaptureTopLeft"/> と <see cref="LoupeRenderedEventArgs.Scale"/> から求める。
    /// </remarks>
    public event EventHandler<LoupeRenderedEventArgs>? Rendered;

    /// <summary>設定変更ログの出力先を取得または設定する。</summary>
    /// <value>ロガー。null を設定した場合は <see cref="NullLogger"/> へフォールバックする（出力なし）。</value>
    /// <remarks>
    /// C#14 の <c>field</c> キーワードでバッキング フィールドを暗黙定義する。<br/>
    /// getter / setter とも null を <see cref="NullLogger"/> へ安全に変換する。
    /// </remarks>
    public ILogger Log
    {
        get => field.OrNullLogger();
        set => field = value.OrNullLogger();
    }

    /// <summary>ログ用に画像を整形する。</summary>
    /// <param name="image">画像（未設定は null）。</param>
    /// <returns><c>Width×Height</c> 形式の文字列。未設定は <c>null</c>。</returns>
    private static string DescribeImage(EsBitmap? image) => image is null ? "null" : $"{image.Width}x{image.Height}";

    /// <summary>ログ用に座標を整形する。</summary>
    /// <param name="point">座標（未設定は null）。</param>
    /// <returns><c>(X, Y)</c> 形式の文字列。未設定は <c>null</c>。</returns>
    private static string FormatPoint(EsPoint? point) => point is { } p ? $"({p.X}, {p.Y})" : "null";

    /// <summary>ルーペの要求サイズ（DIP）を返す。</summary>
    /// <param name="availableSize">親から提示された利用可能サイズ（使用しない）。</param>
    /// <returns>常に <see cref="RequestedSize"/> の正方形。</returns>
    /// <remarks>
    /// <see cref="SkiaCanvasView"/> は既定で <c>Size.Empty</c>（親の残余を埋める）を返すが、<br/>
    /// ルーペは一辺が <see cref="RequestedSize"/> の正方形であるため、自身の要求サイズを返す。<br/>
    /// 表示位置はホストが <c>LoupePositionCalculator</c>（コア）で計算して配置する（本コントロールは位置を持たない）。
    /// </remarks>
    protected override Size MeasureContent(Size availableSize) => new(RequestedSize, RequestedSize);

    /// <summary>DPI の変更時に表示スケーリング係数を追従させる。</summary>
    /// <param name="oldDpi">変更前の DPI。</param>
    /// <param name="newDpi">変更後の DPI。</param>
    /// <remarks>
    /// モニター間の移動で DPI が変わっても物理ピクセル基準の見た目を保つため、<see cref="RenderScaling"/> を<br/>
    /// <c>newDpi / 96</c> へ更新する（ホストが明示設定した値も DPI 変更時は上書きされる）。<br/>
    /// ホストがスケーリングを明示する必要はない（DPI はコントロールが自動取得する）。
    /// </remarks>
    protected override void OnDpiChanged(uint oldDpi, uint newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        RenderScaling = newDpi / 96.0;
    }

    /// <summary>ルーペを描画する。</summary>
    /// <param name="canvas">描画先のキャンバス（物理ピクセル）。</param>
    /// <param name="info">描画先の情報（幅・高さ）。</param>
    /// <remarks>
    /// 表示サイズは <see cref="RequestedSize"/>（DIP）を <see cref="RenderScaling"/> で物理ピクセルへ変換する。<br/>
    /// 市松模様のタイルサイズと外枠の太さも同様に物理ピクセルへ変換してから描画する（DPI 差で見た目を変えない）。<br/>
    /// 描画の順序と計算はコア（<c>LoupeControl.Core</c>）と <see cref="LoupeSkiaRenderer"/> が所有する。
    /// </remarks>
    private void OnPaintSurface(SKCanvas canvas, SKImageInfo info)
    {
        var scaling = RenderScaling;
        var pixelSize = Math.Min(DpiScaler.ScaleToPixel(RequestedSize, scaling), Math.Min(info.Width, info.Height));

        try
        {
            // 市松模様の色は半透明のため、下地を塗らないと後ろの要素（プレビューの画像等）が透けて見える。
            // Avalonia 版はウィンドウ背景の上へ重ねるため、ホストが設定した下地色で塗る。
            canvas.Clear(ToSKColor(BackgroundColor));

            LoupeSkiaRenderer.Render(
                canvas, Source, CenterPoint, pixelSize, ZoomLevel,
                CrossColor, DpiScaler.ScaleToPixel(CheckerSize, scaling),
                CheckerColorA, CheckerColorB, FrameColor, DpiScaler.ScaleToPixel(FrameWidth, scaling));

            DrawCoordLabel(canvas, pixelSize, scaling);
        }
        catch (Exception ex)
        {
            // 描画の例外はアプリを落とさずログへ記録する（キャンバスは中途半端な状態になりうるが、次の描画で復帰する）
            Log.LogUpdateError(ex);
            return;
        }

        RaiseRendered(canvas, pixelSize);
    }

    /// <summary><see cref="Rendered"/> イベントを発火する（描画完了と表示情報を通知する）。</summary>
    /// <param name="canvas">ルーペを描画したキャンバス。</param>
    /// <param name="pixelSize">ルーペの一辺のサイズ（物理ピクセル）。</param>
    /// <remarks>
    /// 切り出し領域の左上とスケールはコアの描画引数計算（Avalonia 版と同一）へ委譲する<br/>
    /// （<c>(ドメイン座標 − CaptureTopLeft) × Scale</c> が両 UI で同じ値になる）。<br/>
    /// 中心が未設定の場合は <see cref="LoupeRenderedEventArgs.CaptureTopLeft"/> が null になり、ホストはオーバーレイを描画できない。
    /// </remarks>
    private void RaiseRendered(SKCanvas canvas, int pixelSize)
    {
        var center = CenterPoint;
        var args = LoupeRenderArgsCalculator.Compute(RequestedSize, RenderScaling, ZoomLevel, center);

        Rendered?.Invoke(this, new LoupeRenderedEventArgs(
            canvas, pixelSize, ZoomLevel, center, args.CaptureTopLeft, args.Scale));
    }

    /// <summary>中心座標ラベルを上端中央へ描画する。</summary>
    /// <param name="canvas">描画先のキャンバス。</param>
    /// <param name="pixelSize">ルーペの一辺のサイズ（物理ピクセル）。</param>
    /// <param name="scaling">表示 DIP → 物理ピクセルのスケーリング係数。</param>
    /// <remarks>
    /// 表示条件（<see cref="ShowCoordLabel"/> が真かつ <see cref="CenterPoint"/> が設定済み）を満たす場合のみ描画する。<br/>
    /// 座標の文字列化は <see cref="CoordinateFormatter"/>（コア）へ委譲する（Avalonia 版と同一の表記）。
    /// </remarks>
    private void DrawCoordLabel(SKCanvas canvas, int pixelSize, double scaling)
    {
        if (!ShowCoordLabel || CenterPoint is not { } center)
        {
            return;
        }

        var text = CoordinateFormatter.Format(center);
        var fontSize = (float)(CoordLabelFontSize * scaling);
        var foreground = CoordLabelColor ?? new MewUIColor(255, 255, 255, 255);
        var background = CoordLabelBackgroundColor ?? new MewUIColor(153, 0, 0, 0);

        using var font = new SKFont(SKTypeface.Default, fontSize);
        using var textPaint = new SKPaint { IsAntialias = true, Color = ToSKColor(foreground) };
        using var backgroundPaint = new SKPaint { IsAntialias = false, Color = ToSKColor(background) };

        var width = font.MeasureText(text, textPaint);

        // 上端中央・余白 2 DIP（Avalonia 版の Margin=2 / Padding=4,1 と同一の見た目）
        var padding = (float)(4 * scaling);
        var top = (float)(2 * scaling);
        var height = fontSize + (float)(2 * scaling);
        var left = (pixelSize - width) / 2f;

        canvas.DrawRect(new SKRect(left - padding, top, left + width + padding, top + height), backgroundPaint);
        canvas.DrawText(text, left, top + fontSize, SKTextAlign.Left, font, textPaint);
    }

    /// <summary>MewUI の色を Skia の色へ変換する。</summary>
    /// <param name="color">変換元の色。</param>
    /// <returns>Skia の色。</returns>
    private static SKColor ToSKColor(MewUIColor color) => new(color.R, color.G, color.B, color.A);
}
