using Aprillz.MewUI;
using Aprillz.MewUI.Skia.Controls;
using Estable.FadeMask;
using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;
using EsUtil.Platform.Core;

namespace Estable.MewUI.FadeMask;

/// <summary>領域外を暗転させ、選択領域をハイライトするオーバーレイコントロール（MewUI 版）。</summary>
/// <remarks>
/// Avalonia 版 <c>FadeMaskControl</c> と同じ UI 非依存コア（<c>FadeMaskControl.Core</c>）を使い、<br/>
/// 描画層のみを <see cref="SkiaCanvasView"/> で実装する（公開プロパティの名前・意味も Avalonia 版と同一）。<br/>
/// <br/>
/// <b>入力しない</b>。本コントロールは装飾専用であり、ホスト側で <c>IsHitTestVisible = false</c> にして<br/>
/// 下のコントロールへ入力を通す（Avalonia 版と同じ使い方）。<br/>
/// <br/>
/// <b>ラベル</b>: サイズラベル（1 行目）とサブライン（2 行目以降）を穴あけ矩形の左上へ縦に積む。<br/>
/// 記載は <see cref="Appearance"/>（コアの <see cref="FadeMaskAppearance"/>）が、サイズラベルの文言は<br/>
/// <see cref="SizeLabelOverrideProvider"/> が与える。背景・文字原点・次の行の位置はコアが単一所有する。<br/>
/// <br/>
/// <b>Avalonia 版との違い（フレームワーク固有）</b>: DPI 追従（<see cref="RenderScaling"/>）と<br/>
/// 非 ASCII 文言のフォントフォールバック（豆腐防止）を持つ（Avalonia 版はコントロールの DIP 座標系・自前のテキスト計量のため不要）。
/// </remarks>
public sealed class FadeMaskControl : SkiaCanvasView
{
    /// <summary><see cref="LogicalSelectionRect"/> プロパティ（論理座標＝ドメイン座標の選択矩形）。</summary>
    public static readonly MewProperty<EsRect?> LogicalSelectionRectProperty =
        MewProperty<EsRect?>.Register<FadeMaskControl>(
            nameof(LogicalSelectionRect), null, MewPropertyOptions.AffectsRender,
            changed: static (owner, oldValue, newValue) => owner.Log.LogSelectionRectChanged(oldValue, newValue));

    /// <summary><see cref="LogicalHighlightRect"/> プロパティ（論理座標＝ドメイン座標のハイライト矩形）。</summary>
    public static readonly MewProperty<EsRect?> LogicalHighlightRectProperty =
        MewProperty<EsRect?>.Register<FadeMaskControl>(
            nameof(LogicalHighlightRect), null, MewPropertyOptions.AffectsRender,
            changed: static (owner, oldValue, newValue) => owner.Log.LogHighlightRectChanged(oldValue, newValue));

    /// <summary><see cref="HasSelection"/> プロパティ（選択が有効か）。</summary>
    public static readonly MewProperty<bool> HasSelectionProperty =
        MewProperty<bool>.Register<FadeMaskControl>(nameof(HasSelection), false, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="HasHighlight"/> プロパティ（ハイライトが有効か）。</summary>
    public static readonly MewProperty<bool> HasHighlightProperty =
        MewProperty<bool>.Register<FadeMaskControl>(nameof(HasHighlight), false, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="Appearance"/> プロパティ（暗転色・枠線・ラベルの外観をまとめた単一の設定値）。</summary>
    public static readonly MewProperty<FadeMaskAppearance> AppearanceProperty =
        MewProperty<FadeMaskAppearance>.Register<FadeMaskControl>(
            nameof(Appearance), new FadeMaskAppearance(), MewPropertyOptions.AffectsRender);

    /// <summary><see cref="WindowTitle"/> プロパティ（情報ラベルの文言）。</summary>
    public static readonly MewProperty<string> WindowTitleProperty =
        MewProperty<string>.Register<FadeMaskControl>(nameof(WindowTitle), "", MewPropertyOptions.AffectsRender);

    /// <summary><see cref="Mode"/> プロパティ（オーバーレイ描画モード）。</summary>
    public static readonly MewProperty<FadeMaskRenderMode> ModeProperty =
        MewProperty<FadeMaskRenderMode>.Register<FadeMaskControl>(
            nameof(Mode), FadeMaskRenderMode.AreaSelect, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="ShowFullOverlay"/> プロパティ（選択なしでも全面を暗転するか）。</summary>
    public static readonly MewProperty<bool> ShowFullOverlayProperty =
        MewProperty<bool>.Register<FadeMaskControl>(nameof(ShowFullOverlay), false, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="RenderScaling"/> プロパティ（DIP → 物理ピクセルのスケーリング係数）。</summary>
    public static readonly MewProperty<double> RenderScalingProperty =
        MewProperty<double>.Register<FadeMaskControl>(nameof(RenderScaling), 1.0, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="Scaling"/> プロパティ（座標変換ポートへ適用する倍率）。</summary>
    /// <remarks><see cref="SetCoordinateTransform"/> でポートを設定した場合は使用しない（等倍スケール専用の簡易指定）。</remarks>
    public static readonly MewProperty<double> ScalingProperty =
        MewProperty<double>.Register<FadeMaskControl>(nameof(Scaling), 1.0, MewPropertyOptions.AffectsRender);

    /// <summary>論理座標（ドメイン）の選択矩形を取得または設定する。</summary>
    /// <value>選択矩形。null の場合は穴あけなし。</value>
    public EsRect? LogicalSelectionRect
    {
        get => GetValue(LogicalSelectionRectProperty);
        set => SetValue(LogicalSelectionRectProperty, value);
    }

    /// <summary>論理座標（ドメイン）のハイライト矩形を取得または設定する。</summary>
    /// <value>ハイライト矩形。null の場合は穴あけなし。選択矩形より優先される。</value>
    public EsRect? LogicalHighlightRect
    {
        get => GetValue(LogicalHighlightRectProperty);
        set => SetValue(LogicalHighlightRectProperty, value);
    }

    /// <summary>選択が有効かどうかを取得または設定する。</summary>
    /// <value>true の場合、<see cref="LogicalSelectionRect"/> を穴あけ矩形として使う。</value>
    public bool HasSelection
    {
        get => GetValue(HasSelectionProperty);
        set => SetValue(HasSelectionProperty, value);
    }

    /// <summary>ハイライトが有効かどうかを取得または設定する。</summary>
    /// <value>true の場合、<see cref="LogicalHighlightRect"/> を穴あけ矩形として使う（選択矩形より優先）。</value>
    public bool HasHighlight
    {
        get => GetValue(HasHighlightProperty);
        set => SetValue(HasHighlightProperty, value);
    }

    /// <summary>暗転マスクの外観（暗転色・枠線・ラベル）を取得または設定する。</summary>
    /// <value>外観の単一値。既定はコアの既定値（<see cref="FadeMaskAppearance"/>）。</value>
    /// <remarks>設定（変更）すると自動で再描画される（<c>MewPropertyOptions.AffectsRender</c>）。</remarks>
    public FadeMaskAppearance Appearance
    {
        get => GetValue(AppearanceProperty);
        set => SetValue(AppearanceProperty, value);
    }

    /// <summary>ハイライト対象のウィンドウタイトル（情報ラベルの文言）を取得または設定する。</summary>
    /// <value>ウィンドウタイトル。未検出時は空文字。</value>
    /// <remarks>表示の可否はモードと <see cref="FadeMaskAppearance.InfoLabelVisible"/> からコアが判定する。</remarks>
    public string WindowTitle
    {
        get => GetValue(WindowTitleProperty);
        set => SetValue(WindowTitleProperty, value);
    }

    /// <summary>オーバーレイ描画モードを取得または設定する。</summary>
    /// <value>描画モード。操作ラベルの文言選定と情報ラベルの表示可否に使用する。</value>
    public FadeMaskRenderMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
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
    /// <remarks>
    /// マルチスクリーン補正等でホストが設定する（<see cref="Port"/> が未設定の場合のみ使われる）。<br/>
    /// サイズラベル（DisplayScale）の換算係数は <see cref="DisplayScaleScaling"/>。
    /// </remarks>
    public double Scaling
    {
        get => GetValue(ScalingProperty);
        set => SetValue(ScalingProperty, value);
    }

    /// <summary>論理座標 → DIP の座標変換ポート。</summary>
    /// <remarks>null は「未設定」を意味し、コアが <see cref="Scaling"/> から等倍スケールを解決する。</remarks>
    private ICoordTransform? _coordinatePort;

    /// <summary>ドメイン ↔ コントロールローカル DIP の座標変換ポートを設定する。</summary>
    /// <param name="port">座標変換ポート。null の場合は <see cref="Scaling"/> による等倍スケールを使う。</param>
    /// <remarks>
    /// Avalonia 版と同名・同形のメソッドとする（ポート設定はリポジトリ全体でメソッドに統一されている）。<br/>
    /// null（未設定）を <see cref="Scaling"/> と同値として扱う規則はコア（<c>FadeMaskOverlayGeometry.ResolvePort</c>）が単一所有する<br/>
    /// （ここで等価変換へ置換するとアプリ側の判断になり、Avalonia 版と規則が二重化する）。
    /// </remarks>
    public void SetCoordinateTransform(ICoordTransform? port)
    {
        _coordinatePort = port;
        Log.LogCoordinateTransform(port is not null);
        InvalidateVisual();
    }

    /// <summary>サイズラベル表示用のスケーリング係数を取得または設定する（マルチスクリーン補正）。</summary></summary>
    /// <value>サイズラベルの自動生成に使う係数。未設定（0 以下）の場合は <see cref="Scaling"/> と同じ値が使用される。</value>
    /// <remarks>
    /// サイズラベルの文言をホストが与える場合は使用されない（自動生成のフォールバック時のみ参照する）。<br/>
    /// 0 以下を <see cref="Scaling"/> と同値として扱う規則はコア（<c>FadeMaskLabels.ResolveDisplayScaling</c>）が単一所有する。
    /// </remarks>
    public double DisplayScaleScaling
    {
        get => FadeMaskLabels.ResolveDisplayScaling(field, Scaling);
        set => field = value;
    }

    /// <summary>サイズラベルのテキストを提供する関数を取得または設定する。</summary>
    /// <value>サイズラベルのテキストを返す関数。null または空文字の場合はサイズラベルを描画しない。</value>
    /// <remarks>
    /// 座標系はホストが知るため（画面物理ピクセル / 画像ピクセル / DIP）、文言の生成はホストへ委譲する。<br/>
    /// 描画のたびに評価されるため、ドラッグ中の選択矩形の変化にも追従できる。<br/>
    /// 文言の生成にはコアの <c>FadeMaskLabels.FormatSizeLabel</c> を使う（両 UI で同じ書式になる）。<br/>
    /// 設定（変更）時は再描画を要求する（デリゲートは変更通知を持たないため）。
    /// </remarks>
    public Func<string?>? SizeLabelOverrideProvider
    {
        get => field;
        set
        {
            field = value;
            InvalidateVisual();
        }
    }

    /// <summary>状態変更ログの出力先を取得または設定する。</summary>
    /// <value>ロガー。null を設定した場合は <see cref="NullLogger"/> へフォールバックする（出力なし）。</value>
    /// <remarks>C#14 の <c>field</c> キーワードでバッキングフィールドを暗黙定義する。</remarks>
    public ILogger Log
    {
        get => field.OrNullLogger();
        set => field = value.OrNullLogger();
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
        RenderScaling = DpiScaler.ToScaling(newDpi);
    }

    /// <summary>暗転マスク・枠線・ラベルを描画する。</summary>
    /// <param name="canvas">描画先のキャンバス（物理ピクセル）。</param>
    /// <param name="info">描画先の情報（幅・高さ）。</param>
    /// <remarks>
    /// キャンバスを DPI スケーリングしてから DIP 座標で描画する（コアの計算と座標系を一致させる）。<br/>
    /// 外観と状態からコアの <see cref="FadeMaskFrame"/> を解決し（暗転領域・枠線・ラベルの判断はコアが持つ）、<br/>
    /// それを <see cref="FadeMaskSkiaRenderer.Draw"/> へ渡して描画するだけとする（本 UI 層は判断を持たない）。
    /// </remarks>
    private void OnPaintSurface(SKCanvas canvas, SKImageInfo info)
    {
        canvas.Clear(SKColors.Transparent);

        var scaling = RenderScaling > 0 ? RenderScaling : 1.0;
        var client = new EsRect(0, 0, info.Width / scaling, info.Height / scaling);

        var frame = FadeMaskFrame.Resolve(new FadeMaskRequest(
            Appearance,
            Mode,
            WindowTitle,
            client,
            LogicalSelectionRect,
            LogicalHighlightRect,
            HasSelection,
            HasHighlight,
            ShowFullOverlay,
            _coordinatePort,
            Scaling,
            SizeLabelOverrideProvider,
            DisplayScaleScaling));

        canvas.Save();
        canvas.Scale((float)scaling);
        try
        {
            FadeMaskSkiaRenderer.Draw(canvas, frame);
        }
        finally
        {
            canvas.Restore();
        }
    }
}
