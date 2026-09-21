using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Rect = Avalonia.Rect;
using Estable.AvaloniaUI.FadeMask.Logging;
using EsUtil.Platform.AvaloniaUI;
using EsUtil.Platform.Core.Coordinate;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Estable.AvaloniaUI.FadeMask;

/// <summary>領域選択時の暗転オーバーレイ（穴あけマスク）＋サイズラベルを描画するコントロール。</summary>
/// <remarks>
/// <see cref="Render(DrawingContext)"/> のオーバーライドで直接描画する軽量なコントロール。<br/>
/// AXAML に配置して依存関係プロパティ経由で制御し、外部レンダラー（<see cref="FadeMaskRenderer"/>）を
/// <see cref="ExternalRenderer"/> に設定すると描画を完全委譲する（描画ロジックの統一）。<br/>
/// <br/>
/// **公開 API 一覧**:<br/>
/// - 選択矩形: <see cref="LogicalSelectionRect"/>（ドメイン座標 + <see cref="SetCoordinateTransform"/> で DIP 変換）<br/>
/// - 暗転オーバーレイ: <see cref="OverlayColor"/><br/>
/// - サイズラベル: <see cref="TextColor"/> / <see cref="TextBackgroundColor"/> / <see cref="FontSize"/> / <see cref="ShowSizeLabel"/><br/>
/// - 外部レンダラー委譲: <see cref="ExternalRenderer"/><br/>
/// 色の不透明度は色のアルファに統合する（例: 黒 50% = <c>#80000000</c>）。
/// <br/>
/// **構成**: 本クラスは公開 API（StyledProperty・公開プロパティ）と描画実装（静的コンストラクタ・
/// <see cref="Render(DrawingContext)"/> オーバーライド）を単一ファイルに集約する。<br/>
/// 暗転マスク・サイズラベルの共通描画ロジックは <see cref="FadeMaskDrawer"/>（internal）に委譲する。<br/>
/// XAML（axaml）を使用しないため、partial 分割は行わない（単一クラス）。<br/>
/// </remarks>
public class FadeMaskControl : Control
{
    // ====================================================================
    // 依存関係プロパティ（StyledProperty）
    // ====================================================================

    /// <summary><see cref="OverlayColor"/> 依存関係プロパティ。</summary>
    /// <remarks>既定は黒 50%（<c>#80000000</c>。設定画面の暗転オーバーレイのデフォルトと一致）。</remarks>
    public static readonly StyledProperty<Color> OverlayColorProperty =
        AvaloniaProperty.Register<FadeMaskControl, Color>(nameof(OverlayColor), Color.FromAColor(128, Colors.Black));

    /// <summary><see cref="TextColor"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<Color> TextColorProperty =
        AvaloniaProperty.Register<FadeMaskControl, Color>(nameof(TextColor), Colors.White);

    /// <summary><see cref="TextBackgroundColor"/> 依存関係プロパティ（不透明度は色のアルファに統合。既定は黒 60%）。</summary>
    public static readonly StyledProperty<Color> TextBackgroundColorProperty =
        AvaloniaProperty.Register<FadeMaskControl, Color>(nameof(TextBackgroundColor), Color.FromAColor(153, Colors.Black));

    /// <summary><see cref="FontSize"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<FadeMaskControl, double>(nameof(FontSize), 14.0);

    /// <summary><see cref="ShowSizeLabel"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<bool> ShowSizeLabelProperty =
        AvaloniaProperty.Register<FadeMaskControl, bool>(nameof(ShowSizeLabel), true);

    // ====================================================================
    // 暗転オーバーレイ
    // ====================================================================

    /// <summary>暗転オーバーレイの色を取得または設定する。</summary>
    /// <value>アルファ値を含む色（例: Color.FromAColor(150, Colors.Black)）。</value>
    public Color OverlayColor { get => GetValue(OverlayColorProperty); set => SetValue(OverlayColorProperty, value); }

    // ====================================================================
    // サイズラベル
    // ====================================================================

    /// <summary>ラベルテキストの色を取得または設定する。</summary>
    public Color TextColor { get => GetValue(TextColorProperty); set => SetValue(TextColorProperty, value); }

    /// <summary>ラベル背景の色（不透明度は色のアルファに統合）を取得または設定する。</summary>
    public Color TextBackgroundColor { get => GetValue(TextBackgroundColorProperty); set => SetValue(TextBackgroundColorProperty, value); }

    /// <summary>ラベルのフォントサイズを取得または設定する。</summary>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>サイズラベルを表示するかどうかを取得または設定する。</summary>
    public bool ShowSizeLabel { get => GetValue(ShowSizeLabelProperty); set => SetValue(ShowSizeLabelProperty, value); }

    // ====================================================================
    // ログ
    // ====================================================================

    /// <summary>内部ロガー（コンストラクタ注入用。null の場合は出力なし）。</summary>
    /// <value>null を設定した場合は <see cref="NullLogger"/> へフォールバックする。</value>
    /// <remarks>
    /// C#14 の <c>field</c> キーワードでバッキングフィールドを暗黙定義する。<br/>
    /// getter / setter とも null を <see cref="NullLogger"/> へ安全に変換する。
    /// </remarks>
    public ILogger Log
    {
        get => field ?? NullLogger.Instance;
        set => field = value ?? NullLogger.Instance;
    }

    // ====================================================================
    // 外部レンダラー委譲（FadeMaskRenderer を AXAML 配置コントロールで使用するため）
    // ====================================================================

    /// <summary>外部レンダラーが設定されている場合、<see cref="Render(DrawingContext)"/> での描画をこのレンダラーに完全委譲する（依存関係プロパティの描画は行われない）。</summary>
    /// <remarks>
    /// ホスト（上位 AP）が <see cref="FadeMaskRenderer"/> へ描画状態を設定し、<br/>
    /// 本プロパティ経由で描画ロジックを統一する（AXAML のレイアウトをそのまま利用しつつ、<br/>
    /// モード別の描画（ハイライト・選択矩形・全面暗転等）を <see cref="FadeMaskRenderer"/> に委譲できる）。<br/>
    /// 設定時は、既に設定済みの座標変換ポート（<see cref="SetCoordinateTransform"/>）と論理選択矩形<br/>
    /// （<see cref="LogicalSelectionRect"/>）をレンダラーへ同期する（設定順序に依存しない）。<br/>
    /// 設定後は <see cref="Control.InvalidateVisual"/> で再描画を要求する（呼び出し側の責務）。
    /// </remarks>
    public FadeMaskRenderer? ExternalRenderer
    {
        get => field;
        set
        {
            field = value;
            Log.LogExternalRenderer(value is not null);
            if (field is { } renderer)
            {
                // 既に設定済みの座標変換ポート・論理矩形をレンダラーへ同期する（CropSession の設定順序に依存させない）
                renderer.CoordinateTransform = _coordinatePort;
                renderer.LogicalSelectionRect = LogicalSelectionRect;
            }
            InvalidateVisual();
        }
    }

    // ====================================================================
    // 座標変換ポート（DragRectControl と同方式）
    // ====================================================================

    /// <summary>論理座標（ドメイン）↔ コントロールローカル DIP の座標変換ポート。</summary>
    private ICoordTransform _coordinatePort = CoordTransform.Identity;

    /// <summary>ドメイン ↔ コントロールローカル DIP の座標変換ポートを設定する。</summary>
    /// <param name="port">座標変換ポート（Inverse = ドメイン→DIP）。null の場合は等価変換。</param>
    /// <remarks>
    /// DragRectControl と同様に、外部から与えたポートで描画時に動的に矩形を算出する。<br/>
    /// 変換関数は呼び出し毎に評価されるため、ズーム・スクロール・ウィンドウサイズ変更等の<br/>
    /// レイアウト変化に追従できる（イベント時の DIP キャッシュは使わない）。<br/>
    /// ExternalRenderer が設定されている場合はレンダラーにも伝搬する。
    /// </remarks>
    public void SetCoordinateTransform(ICoordTransform? port)
    {
        _coordinatePort = port ?? CoordTransform.Identity;
        Log.LogCoordinateTransform();
        if (ExternalRenderer is { } renderer) renderer.CoordinateTransform = _coordinatePort;
        InvalidateVisual();
    }

    /// <summary>論理座標（ドメイン = 元画像ピクセル等）の選択矩形を取得または設定する。</summary>
    /// <value>選択矩形（ドメイン座標）。null の場合は選択なし。</value>
    /// <remarks>
    /// <see cref="SetCoordinateTransform"/> で設定した座標変換ポートで描画時に DIP へ変換される<br/>
    /// （ドメイン座標 + ポート変換の一本化。レイアウト変化に追従）。
    /// </remarks>
    public Rect? LogicalSelectionRect
    {
        get => field;
        set
        {
            var old = field;
            field = value;
            Log.LogSelectionRectChanged(old?.ToString() ?? "null", value?.ToString() ?? "null");
            if (ExternalRenderer is { } renderer) renderer.LogicalSelectionRect = value;
            InvalidateVisual();
        }
    }

    // ====================================================================
    // 描画（Render オーバーライド）
    // ====================================================================

    /// <summary>静的コンストラクタ。描画に影響する依存関係プロパティを登録する。</summary>
    /// <remarks>プロパティ変更時に自動で再描画が要求される（AffectsRender）。</remarks>
    static FadeMaskControl()
    {
        AffectsRender<FadeMaskControl>(
            OverlayColorProperty,
            TextColorProperty,
            TextBackgroundColorProperty,
            FontSizeProperty,
            ShowSizeLabelProperty);
    }

    /// <summary>コントロールを描画する。</summary>
    /// <param name="context">描画コンテキスト。</param>
    /// <remarks>
    /// 外部レンダラー（<see cref="ExternalRenderer"/>）が設定されている場合は描画を完全委譲する。<br/>
    /// 未設定の場合は選択矩形に応じた暗転マスク（穴あけ）＋サイズラベルを描画する。
    /// </remarks>
    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;

        // 外部レンダラーが設定されている場合は委譲する
        var externalRenderer = ExternalRenderer;
        if (externalRenderer is not null)
        {
            externalRenderer.Draw(context, bounds.Size);
            return;
        }

        var clientRect = new Rect(0, 0, bounds.Width, bounds.Height);

        // 選択矩形（DIP）: 論理矩形 + 座標変換ポートを描画時に評価（レイアウト変化に追従）
        // 変換は FadeMaskRenderer.ToDipRect に委譲する（閉区間 +1 変換の重複排除・単一所有）
        Rect? selRect = LogicalSelectionRect is { } logical
            ? FadeMaskRenderer.ToDipRect(logical, _coordinatePort)
            : null;
        var hasSelection = selRect is { Width: > 0, Height: > 0 };

        // 暗転マスク＋サイズラベル（Selection の責務。共通ロジックは FadeMaskDrawer に委譲）
        FadeMaskDrawer.DrawOverlay(
            context,
            clientRect,
            hasSelection ? selRect : null,
            OverlayColor,
            TextColor,
            FontSize,
            ShowSizeLabel && hasSelection,
            textBgColor: TextBackgroundColor);
    }
}
