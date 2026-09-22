using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using EsUtil.Platform.AvaloniaUI;
using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using Estable.FadeMask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Rect = Avalonia.Rect;

namespace Estable.AvaloniaUI.FadeMask;

/// <summary>領域選択時の暗転オーバーレイ（穴あけマスク）＋ラベルを描画するコントロール。</summary>
/// <remarks>
/// <see cref="Render(DrawingContext)"/> のオーバーライドで直接描画する軽量なコントロール。<br/>
/// 描画要素は「暗転マスク（穴あけ）」「選択枠線」「ラベル（サイズ・操作・情報）」で、共通ロジックは<br/>
/// <see cref="FadeMaskDrawer"/>（internal）とコア（<c>FadeMaskControl.Core</c>）へ委譲する。<br/>
/// <br/>
/// <b>入力しない</b>: ホスト側で <c>IsHitTestVisible = false</c> にして下のコントロールへ入力を通す。<br/>
/// <br/>
/// <b>状態と外観</b>: 外観は <see cref="Appearance"/>（コアの <see cref="FadeMaskAppearance"/>）が単一所有し、<br/>
/// 選択矩形・ハイライト矩形・全面暗転・描画モードなどの状態を個別プロパティで受け取る。<br/>
/// 色の不透明度は色のアルファに統合する（例: 黒 50% = <c>#80000000</c>）。<br/>
/// <br/>
/// <b>座標系</b>: 選択矩形・ハイライト矩形はドメイン座標（元画像ピクセル / 仮想スクリーンピクセル）で受け取り、<br/>
/// <see cref="SetCoordinateTransform"/> の座標変換ポートで描画時に DIP へ変換する（レイアウト変化に追従）。
/// </remarks>
public class FadeMaskControl : Control
{
    // ====================================================================
    // 依存関係プロパティ（StyledProperty）
    // ====================================================================

    /// <summary><see cref="Appearance"/> 依存関係プロパティ（外観の単一所有）。</summary>
    /// <remarks>既定は <see cref="FadeMaskAppearance"/> の既定値（黒 50% の暗転・ダークグレーの枠線・白のラベル）。</remarks>
    public static readonly StyledProperty<FadeMaskAppearance> AppearanceProperty =
        AvaloniaProperty.Register<FadeMaskControl, FadeMaskAppearance>(nameof(Appearance), new FadeMaskAppearance());

    /// <summary><see cref="LogicalSelectionRect"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<Rect?> LogicalSelectionRectProperty =
        AvaloniaProperty.Register<FadeMaskControl, Rect?>(nameof(LogicalSelectionRect));

    /// <summary><see cref="LogicalHighlightRect"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<Rect?> LogicalHighlightRectProperty =
        AvaloniaProperty.Register<FadeMaskControl, Rect?>(nameof(LogicalHighlightRect));

    /// <summary><see cref="HasSelection"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<bool> HasSelectionProperty =
        AvaloniaProperty.Register<FadeMaskControl, bool>(nameof(HasSelection));

    /// <summary><see cref="HasHighlight"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<bool> HasHighlightProperty =
        AvaloniaProperty.Register<FadeMaskControl, bool>(nameof(HasHighlight));

    /// <summary><see cref="WindowTitle"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<string> WindowTitleProperty =
        AvaloniaProperty.Register<FadeMaskControl, string>(nameof(WindowTitle), "");

    /// <summary><see cref="Mode"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<FadeMaskRenderMode> ModeProperty =
        AvaloniaProperty.Register<FadeMaskControl, FadeMaskRenderMode>(nameof(Mode), FadeMaskRenderMode.AreaSelect);

    /// <summary><see cref="Scaling"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<double> ScalingProperty =
        AvaloniaProperty.Register<FadeMaskControl, double>(nameof(Scaling), 1.0);

    /// <summary><see cref="DisplayScaleScaling"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<double> DisplayScaleScalingProperty =
        AvaloniaProperty.Register<FadeMaskControl, double>(nameof(DisplayScaleScaling));

    /// <summary><see cref="ShowFullOverlay"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<bool> ShowFullOverlayProperty =
        AvaloniaProperty.Register<FadeMaskControl, bool>(nameof(ShowFullOverlay));

    // ====================================================================
    // 外観
    // ====================================================================

    /// <summary>暗転マスクの外観（暗転色・枠線・ラベル）を取得または設定する。</summary>
    /// <value>外観の単一値。既定はコアの既定値（<see cref="FadeMaskAppearance"/>）。</value>
    /// <remarks>設定（変更）すると自動で再描画される（<c>AffectsRender</c>）。</remarks>
    public FadeMaskAppearance Appearance
    {
        get => GetValue(AppearanceProperty);
        set => SetValue(AppearanceProperty, value);
    }

    // ====================================================================
    // 描画状態
    // ====================================================================

    /// <summary>選択矩形（ドメイン座標 = 元画像ピクセル / 仮想スクリーンピクセル）を取得または設定する。</summary>
    /// <value>選択矩形。null の場合は選択なし（<see cref="HasSelection"/> が true のとき穴あけ矩形になる）。</value>
    public Rect? LogicalSelectionRect
    {
        get => GetValue(LogicalSelectionRectProperty);
        set => SetValue(LogicalSelectionRectProperty, value);
    }

    /// <summary>ハイライト矩形（ドメイン座標 = 仮想スクリーンピクセル）を取得または設定する。</summary>
    /// <value>ハイライト矩形。null の場合はハイライトなし（<see cref="HasHighlight"/> が true のとき選択矩形より優先される）。</value>
    public Rect? LogicalHighlightRect
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
    /// <remarks>描画要素の切り替えはモード（<see cref="FadeMaskRenderMode"/>）で判定する（bool フラグによる分岐は行わない）。</remarks>
    public FadeMaskRenderMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    /// <summary>穴あけ矩形が無くても全面を暗転するかどうかを取得または設定する。</summary>
    /// <value>true の場合は選択が無くても暗転する（範囲選択のドラッグ前・切出しの初期状態）。</value>
    public bool ShowFullOverlay
    {
        get => GetValue(ShowFullOverlayProperty);
        set => SetValue(ShowFullOverlayProperty, value);
    }

    // ====================================================================
    // DPI スケーリング
    // ====================================================================

    /// <summary>DPI スケーリング係数（ドメイン → DIP 変換の基準倍率）を取得または設定する。</summary>
    /// <value>ドメイン → DIP 変換の基準倍率。既定は 1.0。</value>
    /// <remarks>
    /// <see cref="SetCoordinateTransform"/> でポートを設定した場合は使われない（ポート優先）。<br/>
    /// サイズラベル表示用の係数は <see cref="DisplayScaleScaling"/>（同じ用途だが独立して指定できる）。<br/>
    /// ポートと倍率の使い分け（ポート優先・倍率が 0 以下や等倍なら等価変換）はコアが単一所有する。
    /// </remarks>
    public double Scaling
    {
        get => GetValue(ScalingProperty);
        set => SetValue(ScalingProperty, value);
    }

    /// <summary>サイズラベル表示用の DPI スケーリング係数（マルチスクリーン補正）を取得または設定する。</summary>
    /// <value>物理ピクセル換算に使う係数。未設定（0 以下）の場合は <see cref="Scaling"/> と同じ値が使用される。</value>
    /// <remarks>
    /// マルチスクリーン環境では、選択矩形が存在するスクリーンのスケーリング係数を設定する。<br/>
    /// 0 以下を <see cref="Scaling"/> と同値として扱う規則はコア（<c>FadeMaskLabels.ResolveDisplayScaling</c>）が単一所有する。
    /// </remarks>
    public double DisplayScaleScaling
    {
        get => FadeMaskLabels.ResolveDisplayScaling(GetValue(DisplayScaleScalingProperty), Scaling);
        set => SetValue(DisplayScaleScalingProperty, value);
    }

    // ====================================================================
    // サイズラベルの供給・ログ
    // ====================================================================

    /// <summary>サイズラベルのテキストを描画時に評価するプロバイダを取得または設定する。</summary>
    /// <value>描画のたびに呼び出される関数。null の場合は穴あけ矩形と <see cref="DisplayScaleScaling"/> から自動生成する。</value>
    /// <remarks>
    /// 設定（変更）時は再描画を要求する。<br/>
    /// - 切出し（Crop）: 論理座標（元画像ピクセル）+ 実表示ズーム率で生成する。<br/>
    /// - 範囲選択（AreaSelect）等: 設定値（<c>CaptureSettings.SizeLabelOverride</c>）を返す。
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

    /// <summary>内部ロガーを取得または設定する。</summary>
    /// <value>null を設定した場合は <see cref="NullLogger"/> へフォールバックする（出力なし）。</value>
    /// <remarks>C#14 の <c>field</c> キーワードでバッキング フィールドを暗黙定義する。</remarks>
    public ILogger Log
    {
        get => field ?? NullLogger.Instance;
        set => field = value ?? NullLogger.Instance;
    }

    // ====================================================================
    // 座標変換ポート（DragRectControl と同方式）
    // ====================================================================

    /// <summary>論理座標（ドメイン）↔ コントロールローカル DIP の座標変換ポート（null はコアが <see cref="Scaling"/> から解決する）。</summary>
    private ICoordTransform? _coordinatePort;

    /// <summary>ドメイン ↔ コントロールローカル DIP の座標変換ポートを設定する。</summary>
    /// <param name="port">座標変換ポート（Inverse = ドメイン→DIP）。null の場合は <see cref="Scaling"/> による等倍スケールを使う。</param>
    /// <remarks>
    /// DragRectControl と同様に、外部から与えたポートで描画時に動的に矩形を算出する。<br/>
    /// 変換関数は呼び出し毎に評価されるため、ズーム・スクロール・ウィンドウサイズ変更等のレイアウト変化に追従する。<br/>
    /// null（未設定）を <see cref="Scaling"/> と同値として扱う規則はコア（<c>FadeMaskOverlayGeometry.ResolvePort</c>）が単一所有する<br/>
    /// （ここで等価変換へ置換するとアプリ側の判断になり、MewUI 版と規則が二重化する）。
    /// </remarks>
    public void SetCoordinateTransform(ICoordTransform? port)
    {
        _coordinatePort = port;
        Log.LogCoordinateTransform(port is not null);
        InvalidateVisual();
    }

    // ====================================================================
    // 描画（Render オーバーライド）
    // ====================================================================

    /// <summary>静的コンストラクタ。描画に影響する依存関係プロパティを登録する。</summary>
    /// <remarks>
    /// プロパティ変更時に自動で再描画が要求される（AffectsRender）。<br/>
    /// 選択矩形・ハイライト矩形の変更はログにも出力する（バインディング経由の変更も記録するため、<br/>
    /// CLR プロパティの setter ではなくプロパティ変更ハンドラで行う。MewUI 版の変更コールバックと同じ役割）。
    /// </remarks>
    static FadeMaskControl()
    {
        AffectsRender<FadeMaskControl>(
            AppearanceProperty,
            LogicalSelectionRectProperty,
            LogicalHighlightRectProperty,
            HasSelectionProperty,
            HasHighlightProperty,
            WindowTitleProperty,
            ModeProperty,
            ScalingProperty,
            DisplayScaleScalingProperty,
            ShowFullOverlayProperty);

        LogicalSelectionRectProperty.Changed.AddClassHandler<FadeMaskControl>(
            static (control, e) => control.Log.LogSelectionRectChanged(ToEsRect(e.OldValue as Rect?), ToEsRect(e.NewValue as Rect?)));
        LogicalHighlightRectProperty.Changed.AddClassHandler<FadeMaskControl>(
            static (control, e) => control.Log.LogHighlightRectChanged(ToEsRect(e.OldValue as Rect?), ToEsRect(e.NewValue as Rect?)));
    }

    /// <summary>nullable な矩形をコア矩形へ変換する。</summary>
    /// <param name="rect">変換元の矩形（未設定は null）。</param>
    /// <returns>コア矩形。未設定（null）の場合は null。</returns>
    private static EsRect? ToEsRect(Rect? rect) => rect is { } value ? value.ToEsRect() : null;

    /// <summary>コントロールを描画する。</summary>
    /// <param name="context">描画コンテキスト。</param>
    /// <remarks>
    /// 外観と状態からコアの <see cref="FadeMaskFrame"/> を解決し（暗転領域・枠線・ラベルの判断はコアが持つ）、<br/>
    /// それを <see cref="FadeMaskDrawer.Draw"/> へ渡して描画するだけとする（本 UI 層は判断を持たない）。
    /// </remarks>
    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        var frame = FadeMaskFrame.Resolve(new FadeMaskRequest(
            Appearance,
            Mode,
            WindowTitle,
            new EsRect(0, 0, bounds.Width, bounds.Height),
            ToEsRect(LogicalSelectionRect),
            ToEsRect(LogicalHighlightRect),
            HasSelection,
            HasHighlight,
            ShowFullOverlay,
            _coordinatePort,
            Scaling,
            SizeLabelOverrideProvider,
            DisplayScaleScaling));

        FadeMaskDrawer.Draw(context, frame);
    }
}
