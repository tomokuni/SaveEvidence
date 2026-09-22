using Aprillz.MewUI;
using Aprillz.MewUI.Skia.Controls;
using Estable.Loupe;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;
using static Estable.Loupe.LoupeValueCoercer;

namespace Estable.MewUI.Loupe;

/// <summary>拡大鏡（ルーペ）コントロール（MewUI 版）。</summary>
/// <remarks>
/// Avalonia 版 <c>LoupeControl</c> と同一の UI 非依存コア（<c>LoupeControl.Core</c>）を使い、<br/>
/// 描画層のみを <see cref="SkiaCanvasView"/> で実装する（同じ設定・同じ見た目になる）。<br/>
/// <br/>
/// <b>入力</b>: <see cref="Source"/>（拡大元の 100% 画像）と <see cref="CenterPoint"/>（元画像ピクセル座標）。<br/>
/// <b>設定</b>: <see cref="Options"/>（コアの <see cref="LoupeOptions"/> が単一所有する表示サイズ・拡大率・外観）。<br/>
/// <b>環境</b>: <see cref="RenderScaling"/>（DPI 追従）。<br/>
/// <b>描画</b>: 中間ビットマップを生成せず、画面のキャンバスへ直接描画する（Avalonia 版との差）。<br/>
/// 数値の安全域への補正は <see cref="LoupeOptions"/> / <see cref="LoupeAppearance"/>（コア）が、描画サイズの決定は<br/>
/// <see cref="LoupePositionCalculator.ResolvePixelSize"/>（コア）が、外観の寸法値（タイルサイズ・外枠の太さ・ラベル文字サイズ）と<br/>
/// 下地の描画可否は <see cref="LoupeAppearanceScaler"/> / <see cref="LoupeAppearance"/>（コア）が単一所有する。<br/>
/// <br/>
/// <b>ホスト側の操作</b>: <see cref="Source"/> を設定して <see cref="CenterPoint"/> を現在位置へ更新すると、<br/>
/// ルーペがその位置を拡大表示する（描画はプロパティ変更で自動的に無効化される）。<br/>
/// <b>座標ラベル</b>: 表示可否は <see cref="LoupeAppearance.ShouldShowCoordLabel"/>（コア）が判断し、<br/>
/// 余白と位置は <see cref="LoupeLabelLayout"/>（コア）が単一所有する。
/// </remarks>
public sealed class LoupeControl : SkiaCanvasView
{
    /// <summary><see cref="Source"/> プロパティ（拡大元の 100% 画像）。</summary>
    /// <remarks>変更ログはコアのログ定義が単一所有する（本コントロールは変更通知で呼ぶだけ）。</remarks>
    public static readonly MewProperty<EsBitmap?> SourceProperty =
        MewProperty<EsBitmap?>.Register<LoupeControl>(
            nameof(Source), null, MewPropertyOptions.AffectsRender,
            changed: static (o, oldValue, newValue) => o.Log.LogSourceChanged(oldValue, newValue));

    /// <summary><see cref="CenterPoint"/> プロパティ（拡大中心の元画像ピクセル座標）。</summary>
    /// <remarks>変更ログはコアのログ定義が単一所有する（本コントロールは変更通知で呼ぶだけ）。</remarks>
    public static readonly MewProperty<EsPoint?> CenterPointProperty =
        MewProperty<EsPoint?>.Register<LoupeControl>(
            nameof(CenterPoint), null, MewPropertyOptions.AffectsRender,
            changed: static (o, oldValue, newValue) => o.Log.LogCenterPointChanged(oldValue, newValue));

    /// <summary><see cref="Options"/> プロパティ（表示サイズ・拡大率・外観。コアの <see cref="LoupeOptions"/> が単一所有）。</summary>
    /// <remarks>
    /// 個別に公開していた設定（表示サイズ・拡大率・外観）を 1 つの不変オブジェクトへ統合し、公開プロパティを 1 つに縮小する。<br/>
    /// <see cref="MeasureContent"/> が要求サイズを返すため、変更はレイアウトにも影響する（<c>AffectsLayout</c>）。<br/>
    /// 変更ログはコアのログ定義が単一所有する（変化した項目の判定（差分検出）もコアが行う）。<br/>
    /// 設定値の既定値と数値項目の安全域への補正はコアの <see cref="LoupeOptions"/> が単一所有するため、<br/>
    /// 本コントロールは既定値を持たず、補正の登録も行わない（どの設定経路でも範囲内の値だけが保持される）。
    /// </remarks>
    public static readonly MewProperty<LoupeOptions> OptionsProperty =
        MewProperty<LoupeOptions>.Register<LoupeControl>(
            nameof(Options), LoupeOptions.Default,
            MewPropertyOptions.AffectsRender | MewPropertyOptions.AffectsLayout,
            changed: static (o, oldValue, newValue) => o.Log.LogOptionsChanged(oldValue, newValue));

    /// <summary><see cref="RenderScaling"/> プロパティ（表示 DIP → 物理ピクセルのスケーリング係数）。</summary>
    /// <remarks>
    /// 環境値（DPI）のため設定（<see cref="LoupeOptions"/>）には含めず、補正と変更ログは本プロパティへ登録する<br/>
    /// （setter を経由しない設定でも安全域が保たれ、変更が記録される）。
    /// </remarks>
    public static readonly MewProperty<double> RenderScalingProperty =
        MewProperty<double>.Register<LoupeControl>(
            nameof(RenderScaling), RenderScalingDefault, MewPropertyOptions.AffectsRender,
            changed: static (o, oldValue, newValue) => o.Log.LogRenderScalingChanged(oldValue, newValue),
            coerce: static (_, value) => CoerceRenderScaling(value));

    /// <summary>拡大元の 100% 画像を取得または設定する。</summary>
    /// <value>拡大元の画像。null の場合は市松模様のみを表示する。</value>
    public EsBitmap? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>拡大表示する中心座標（元画像ピクセル座標）を取得または設定する。</summary>
    /// <value>拡大中心。null の場合は市松模様のみを表示する。</value>
    public EsPoint? CenterPoint
    {
        get => GetValue(CenterPointProperty);
        set => SetValue(CenterPointProperty, value);
    }

    /// <summary>ルーペの設定（表示サイズ・拡大率・外観）を取得または設定する。</summary>
    /// <value>設定の単一値。既定はコアの既定値（<see cref="LoupeOptions.Default"/>）。</value>
    /// <remarks>
    /// 一部の項目だけを変える場合は <c>LoupeOptions.Default with { ZoomLevel = 8 }</c> の形で指定する（指定しない項目は既定値を保つ）。<br/>
    /// 設定の値・既定値・数値項目の安全域への補正はコアの <see cref="LoupeOptions"/> が単一所有する。<br/>
    /// 変更すると再描画される（本プロパティは <c>AffectsRender</c> / <c>AffectsLayout</c> で登録されている）。
    /// </remarks>
    public LoupeOptions Options
    {
        get => GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    /// <summary>表示 DIP を物理ピクセルへ変換するスケーリング係数を取得または設定する。</summary>
    /// <value>スケーリング係数。範囲外・非有限は 0.1〜8.0 または既定値へ補正される。</value>
    public double RenderScaling
    {
        get => GetValue(RenderScalingProperty);
        set => SetValue(RenderScalingProperty, value);
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
    /// C#14 の <c>field</c> キーワードでバッキングフィールドを暗黙定義する。<br/>
    /// getter / setter とも null を <see cref="NullLogger"/> へ安全に変換する。
    /// </remarks>
    public ILogger Log
    {
        get => field.OrNullLogger();
        set => field = value.OrNullLogger();
    }

    /// <summary>ルーペの要求サイズ（DIP）を返す。</summary>
    /// <param name="availableSize">親から提示された利用可能サイズ（使用しない）。</param>
    /// <returns>常に <see cref="LoupeOptions.RequestedSize"/> の正方形。</returns>
    /// <remarks>
    /// <see cref="SkiaCanvasView"/> は既定で <c>Size.Empty</c>（親の残余を埋める）を返すが、<br/>
    /// ルーペは一辺が <see cref="LoupeOptions.RequestedSize"/> の正方形であるため、自身の要求サイズを返す。<br/>
    /// 表示位置はホストが <c>LoupePositionCalculator</c>（コア）で計算して配置する（本コントロールは位置を持たない）。
    /// </remarks>
    protected override Size MeasureContent(Size availableSize) => new(Options.RequestedSize, Options.RequestedSize);

    /// <summary>DPI の変更時に表示スケーリング係数を追従させる。</summary>
    /// <param name="oldDpi">変更前の DPI。</param>
    /// <param name="newDpi">変更後の DPI。</param>
    /// <remarks>
    /// モニター間の移動で DPI が変わっても物理ピクセル基準の見た目を保つため、<see cref="RenderScaling"/> を<br/>
    /// DPI から換算した値へ更新する（ホストが明示設定した値も DPI 変更時は上書きされる）。<br/>
    /// 換算規則は <see cref="DpiScaler.ToScaling"/>（コア）が単一所有する。<br/>
    /// ホストがスケーリングを明示する必要はない（DPI はコントロールが自動取得する）。
    /// </remarks>
    protected override void OnDpiChanged(uint oldDpi, uint newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        RenderScaling = DpiScaler.ToScaling(newDpi);
    }

    /// <summary>ルーペを描画する。</summary>
    /// <param name="canvas">描画先のキャンバス（物理ピクセル）。</param>
    /// <param name="info">描画先の情報（幅・高さ）。</param>
    /// <remarks>
    /// 描画入力（コア⇔UI の契約。<see cref="LoupeRenderRequest"/>）を自 UI の公開プロパティから組み立てて描画層へ渡す<br/>
    /// （DIP → 物理ピクセルの換算と描画先へのクランプはコアが単一所有する）。<br/>
    /// 描画の順序と内容（下地・市松模様・拡大画像・十字線・外枠・座標ラベル）は <see cref="LoupeSkiaRenderer"/> が所有する。
    /// </remarks>
    private void OnPaintSurface(SKCanvas canvas, SKImageInfo info)
    {
        // 描画先の表面サイズを渡し、下地の塗りとクランプをコアの契約で解決する。
        // 表示サイズは本コントロールが自己配置しない（ホストが配置する）ため、要求サイズをそのまま渡す
        var canvasSize = new EsSize(info.Width, info.Height);
        var request = LoupeRenderRequest.Create(CenterPoint, Options.RequestedSize, RenderScaling, Options, canvasSize);

        try
        {
            LoupeSkiaRenderer.Render(canvas, Source, request);
        }
        catch (Exception ex)
        {
            // 描画の例外はアプリを落とさずログへ記録する（キャンバスは中途半端な状態になりうるが、次の描画で復帰する）
            Log.LogUpdateError(ex);
            return;
        }

        RaiseRendered(canvas, request);
    }

    /// <summary><see cref="Rendered"/> イベントを発火する（描画完了と表示情報を通知する）。</summary>
    /// <param name="canvas">ルーペを描画したキャンバス。</param>
    /// <param name="request">実際に描画した描画入力（通知する切り出し領域・スケールの算出元）。</param>
    /// <remarks>
    /// 切り出し領域の左上とスケールはコア（<see cref="LoupeRenderArgsCalculator"/>）が単一所有する（実際に描画した値から求める）。<br/>
    /// 中心が未設定の場合は <see cref="LoupeRenderedEventArgs.CaptureTopLeft"/> が null になり、ホストはオーバーレイを描画できない。
    /// </remarks>
    private void RaiseRendered(SKCanvas canvas, LoupeRenderRequest request)
    {
        var args = LoupeRenderArgsCalculator.Compute(request);

        Rendered?.Invoke(this, new LoupeRenderedEventArgs(
            canvas, request.PixelSize, args.CaptureTopLeft, args.Scale));
    }
}
