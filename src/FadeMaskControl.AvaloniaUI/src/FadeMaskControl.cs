using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using Estable.FadeMask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Estable.AvaloniaUI.FadeMask;

/// <summary>領域選択時の暗転オーバーレイ（穴あけマスク）＋ラベルを描画するコントロール。</summary>
/// <remarks>
/// <see cref="Render(DrawingContext)"/> のオーバーライドで直接描画する軽量なコントロール。<br/>
/// 描画要素は「暗転マスク（穴あけ）」「選択枠線」「ラベル（サイズ・操作・情報）」で、共通ロジックは<br/>
/// <see cref="FadeMaskDrawer"/>（internal）とコア（<c>FadeMaskControl.Core</c>）へ委譲する。<br/>
/// <br/>
/// <b>入力しない</b>: ホスト側で <c>IsHitTestVisible = false</c> にして下のコントロールへ入力を通す。<br/>
/// <br/>
/// <b>設定と状態</b>: 外観・表示ポリシー（モード・全面暗転・情報ラベルの文言）・描画状態（選択矩形・ハイライト矩形とその有効フラグ）を<br/>
/// <see cref="Options"/>（コアの <see cref="FadeMaskOptions"/>）が単一所有する（項目ごとの既定値・描画可否の判断を本 UI 層に置かない）。<br/>
/// 色の不透明度は色のアルファに統合する（例: 黒 50% = <c>#80000000</c>）。<br/>
/// <br/>
/// <b>Options に含めない値</b>: DPI スケーリング（<see cref="Scaling"/> / <see cref="DisplayScaleScaling"/>）は環境値のため、<br/>
/// サイズラベルの文言プロバイダ（<see cref="SizeLabelOverrideProvider"/>）はデリゲート（ホスト結線）のため個別に扱う。<br/>
/// <br/>
/// <b>座標系</b>: 選択矩形・ハイライト矩形はドメイン座標（元画像ピクセル / 仮想スクリーンピクセル）で受け取り、<br/>
/// <see cref="SetCoordinateTransform"/> の座標変換ポートで描画時に DIP へ変換する（レイアウト変化に追従）。
/// </remarks>
public class FadeMaskControl : Control
{
    // ====================================================================
    // 依存関係プロパティ（StyledProperty）
    // ====================================================================

    /// <summary><see cref="Options"/> 依存関係プロパティ（設定と描画状態の単一値）。</summary>
    /// <remarks>既定はコアの <see cref="FadeMaskOptions.Default"/>（黒 50% の暗転・ダークグレーの枠線・白のラベル・選択なし）。</remarks>
    public static readonly StyledProperty<FadeMaskOptions> OptionsProperty =
        AvaloniaProperty.Register<FadeMaskControl, FadeMaskOptions>(nameof(Options), FadeMaskOptions.Default);

    /// <summary><see cref="Scaling"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<double> ScalingProperty =
        AvaloniaProperty.Register<FadeMaskControl, double>(nameof(Scaling), 1.0);

    /// <summary><see cref="DisplayScaleScaling"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<double> DisplayScaleScalingProperty =
        AvaloniaProperty.Register<FadeMaskControl, double>(nameof(DisplayScaleScaling));

    // ====================================================================
    // 設定と描画状態
    // ====================================================================

    /// <summary>暗転マスクの設定と描画状態（外観・モード・論理矩形・有効フラグ）を取得または設定する。</summary>
    /// <value>設定と状態の単一値。既定はコアの既定値（<see cref="FadeMaskOptions.Default"/>）。</value>
    /// <remarks>
    /// 一部の項目だけを変える場合は <c>Options with { ... }</c> の形で指定する（指定しない項目は現在値を保つ）。<br/>
    /// 既定値・描画可否の判断・論理矩形の DIP 変換規則はコアが単一所有する。<br/>
    /// 同じ値の再設定では更新をスキップする（コアの値が構造的等価比較のため、変更通知が発生しない）。<br/>
    /// 変更すると再描画され、選択矩形・ハイライト矩形の変更はログにも出力される（差分検出はコアが行う）。
    /// </remarks>
    public FadeMaskOptions Options
    {
        get => GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    // ====================================================================
    // 環境値（DPI スケーリング）
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
    // ホスト結線（文言プロバイダ・ログ）
    // ====================================================================

    /// <summary>サイズラベルのテキストを描画時に評価するプロバイダを取得または設定する。</summary>
    /// <value>描画のたびに呼び出される関数。null の場合は穴あけ矩形と <see cref="DisplayScaleScaling"/> から自動生成する。</value>
    /// <remarks>
    /// デリゲート（ホスト結線）のため <see cref="Options"/> には含めない（値の等価比較の対象外にするため）。<br/>
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
    /// <remarks>C#14 の <c>field</c> キーワードでバッキングフィールドを暗黙定義する。</remarks>
    public ILogger Log
    {
        get => field.OrNullLogger();
        set => field = value.OrNullLogger();
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
    /// 設定・状態の変更は <see cref="Options"/> 1 つの変更通知として届くため、ログ出力（どの項目が変わったかの差分検出）は<br/>
    /// コアのログ定義（<c>LogMessage.LogOptionsChanged</c>）へ委譲する（バインディング経由の変更も記録される）。
    /// </remarks>
    static FadeMaskControl()
    {
        AffectsRender<FadeMaskControl>(
            OptionsProperty,
            ScalingProperty,
            DisplayScaleScalingProperty);

        OptionsProperty.Changed.AddClassHandler<FadeMaskControl>(
            static (control, e) => control.Log.LogOptionsChanged(
                e.GetOldValue<FadeMaskOptions>(), e.GetNewValue<FadeMaskOptions>()));
    }

    /// <summary>コントロールを描画する。</summary>
    /// <param name="context">描画コンテキスト。</param>
    /// <remarks>
    /// 設定・状態（<see cref="Options"/>）と環境値から描画入力を組み立ててコアの <see cref="FadeMaskFrame"/> を解決し<br/>
    /// （暗転領域・枠線・ラベルの判断はコアが持つ）、それを <see cref="FadeMaskDrawer.Draw"/> へ渡して描画するだけとする<br/>
    /// （本 UI 層は判断を持たない）。
    /// </remarks>
    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        var request = new FadeMaskRequest(
            Options,
            new EsRect(0, 0, bounds.Width, bounds.Height),
            _coordinatePort,
            Scaling,
            SizeLabelOverrideProvider,
            DisplayScaleScaling);

        FadeMaskDrawer.Draw(context, FadeMaskFrame.Resolve(request));
    }
}
