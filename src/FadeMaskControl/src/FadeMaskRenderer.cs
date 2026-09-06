using Avalonia;
using Avalonia.Media;
using AvaloniaRect = Avalonia.Rect;
using AvaloniaSize = Avalonia.Size;
using Estable.AvaloniaUI.Platform;
using Estable.AvaloniaUI.Platform.Geometry;

namespace Estable.AvaloniaUI.FadeMask;

/// <summary>領域選択オーバーレイの暗転描画ロジックを提供する。</summary>
/// <remarks>
/// 穴あけマスク（暗転＋選択枠＋サイズラベル）とサブライン（操作・情報ラベル）の描画を担当する。<br/>
/// <see cref="FadeMaskControl"/> の <c>Render()</c>（ExternalRenderer）から呼び出されることを想定し、
/// 描画に必要な全ての状態をプロパティとして外部から注入する。<br/>
/// 拡大鏡は Visual Tree に配置した LoupeControl 自身が描画する（本レンダラーの責務外）。<br/>
/// </remarks>
public sealed class FadeMaskRenderer
{
    // ====================================================================
    // 描画状態（Render 前に設定）
    // ====================================================================

    /// <summary>DPI スケーリング係数。</summary>
    public double Scaling { get; set; } = 1.0;

    /// <summary>サイズラベル表示用の DPI スケーリング係数（マルチスクリーン補正）。</summary>
    /// <value>未設定（0以下）の場合は <see cref="Scaling"/> と同じ値が使用される。</value>
    /// <remarks>
    /// マルチスクリーン環境では、選択矩形が存在するスクリーンのスケーリング係数が設定される（仕様 1.6）。<br/>
    /// サイズラベルの物理ピクセル換算に使用する。<br/>
    /// </remarks>
    public double DisplayScaleScaling
    {
        get => field > 0 ? field : Scaling;
        set => field = value;
    }

    /// <summary>ラベルテキストの色。</summary>
    public Color TextColor { get; set; } = Colors.White;

    /// <summary>サイズラベル（1 行目）の背景の色（不透明度は色のアルファに統合。既定は黒 60%）。</summary>
    public Color TextBackgroundColor { get; set; } = Color.FromAColor(153, Colors.Black);

    /// <summary>ラベルテキストのフォントサイズ（DIP）。</summary>
    public double FontSize { get; set; } = 14.0;

    /// <summary>暗転オーバーレイの色（アルファを含む。既定は黒 50% = アルファ 128）。</summary>
    public Color OverlayArgbColor { get; set; } = Color.FromAColor(128, Colors.Black);

    /// <summary>選択枠線の色（不透明度は色のアルファに統合。既定は DarkGray = 設定画面の選択枠の色）。</summary>
    public Color BorderColor { get; set; } = Color.FromAColor(0xFF, Colors.DarkGray);

    /// <summary>選択枠線の太さ（DIP）。</summary>
    public double BorderThickness { get; set; } = 2.0;

    /// <summary>情報ラベルの文字色。</summary>
    public Color InfoLabelColor { get; set; } = Colors.White;

    /// <summary>情報ラベルの背景の色（不透明度は色のアルファに統合。既定は黒 60%）。</summary>
    public Color InfoLabelBackgroundColor { get; set; } = Color.FromAColor(153, Colors.Black);

    /// <summary>情報ラベルのフォントサイズ（DIP）。</summary>
    public double InfoLabelFontSize { get; set; } = 14.0;

    // ====================================================================
    // 描画状態（モード別の表示制御）
    // ====================================================================

    /// <summary>ハイライト矩形（WindowSelect / ScreenSelect・ドメイン座標 = 仮想スクリーンピクセル）。未設定の場合は null。</summary>
    /// <remarks>
    /// <see cref="LogicalSelectionRect"/> と同様に、<see cref="CoordinateTransform"/> と組み合わせて<br/>
    /// 描画時に最新のレイアウトから DIP 矩形を算出する（座標系の統一・レイアウト変化に追従）。
    /// </remarks>
    public AvaloniaRect? LogicalHighlightRect { get; set; }

    /// <summary>選択矩形（ドメイン座標 = 元画像ピクセル / 仮想スクリーンピクセル）。未設定の場合は null。</summary>
    /// <remarks>
    /// <see cref="CoordinateTransform"/> と組み合わせて、描画時に最新のレイアウトから DIP 矩形を算出する<br/>
    /// （DragRectControl と同方式。ウィンドウサイズ変更・ズーム・スクロールに追従）。<br/>
    /// <see cref="CoordinateTransform"/> 未設定（null）の場合はドメイン = DIP として等価変換する。
    /// </remarks>
    public AvaloniaRect? LogicalSelectionRect { get; set; }

    /// <summary>論理座標（ドメイン）↔ DIP の座標変換ポート。</summary>
    /// <remarks>
    /// <see cref="LogicalSelectionRect"/> / <see cref="LogicalHighlightRect"/> と組み合わせて描画時に DIP 矩形を算出する（呼び出し毎に評価）。<br/>
    /// null の場合は等価変換（ドメイン = DIP）。
    /// </remarks>
    public ICoordinatePort? CoordinateTransform { get; set; }

    /// <summary>ハイライトが有効かどうか。</summary>
    public bool HasHighlight { get; set; }

    /// <summary>選択が有効かどうか。</summary>
    public bool HasSelection { get; set; }

    /// <summary>ハイライトされたウィンドウのタイトル（WindowSelect）。</summary>
    public string WindowTitle { get; set; } = "";

    /// <summary>選択されたスクリーンのインデックス（SelectScreen）。-1 は未選択。</summary>
    public int ScreenIndex { get; set; } = -1;

    /// <summary>サブライン（2 行目以降）のフォントサイズ（DIP）。1 行目（サイズラベル）と同じ既定値。</summary>
    /// <remarks>操作方法・スクリーン番号・ウィンドウタイトルなどのサブラインに適用される。</remarks>
    public double SubLineFontSize { get; set; } = 14.0;

    /// <summary>サブライン（2 行目以降）の文字色。1 行目（サイズラベル）と同じ既定値。</summary>
    /// <remarks>操作方法・スクリーン番号・ウィンドウタイトルなどのサブラインに適用される。</remarks>
    public Color SubLineColor { get; set; } = Colors.Gainsboro;

    /// <summary>サブライン（2 行目以降）の背景の色（不透明度は色のアルファに統合。既定は黒 60%）。</summary>
    public Color SubLineBackgroundColor { get; set; } = Color.FromAColor(153, Colors.Black);

    /// <summary>サブライン（2 行目以降）の表示有無。</summary>
    /// <remarks>false の場合はサブラインを描画しない（サイズラベルのみ表示）。</remarks>
    public bool ShowSubLines { get; set; } = true;

    /// <summary>オーバーレイ描画モード（描画要素の切り替えに使用）。</summary>
    /// <remarks>描画要素の切り替えはモード（<see cref="FadeMaskRenderMode"/>）で判定する（フラグ分岐なし）。</remarks>
    public FadeMaskRenderMode Mode { get; set; } = FadeMaskRenderMode.WindowSelect;

    // ====================================================================
    // 表示制御フラグ
    // ====================================================================

    /// <summary>情報ラベルの表示有無（仕様 6.2.1 InfoLabelVisible）。デフォルト true。</summary>
    /// <remarks>
    /// 情報ラベルは ScreenSelect / WindowSelect のハイライト時のみ表示される（AreaSelect / Crop では非表示）。<br/>
    /// </remarks>
    public bool InfoLabelVisible { get; set; } = true;

    /// <summary>サイズラベルを表示するかどうか。デフォルト true。</summary>
    public bool ShowSizeLabel { get; set; } = true;

    /// <summary>サイズラベルのテキストを描画時に評価するプロバイダ。</summary>
    /// <remarks>
    /// 描画のたびに呼び出され、最新の状態（選択矩形・ズーム率等）からサイズラベル文字列を生成する。<br/>
    /// null の場合は <see cref="FadeMaskDrawer.DrawOverlay"/> が DIP 矩形 + <see cref="DisplayScaleScaling"/> から自動生成する。<br/>
    /// - Crop: 論理座標（元画像ピクセル）+ 実表示ズーム率で生成（レイアウト変化に追従）。<br/>
    /// - 範囲選択（AreaSelect）等: 設定値（<c>CaptureSettings.SizeLabelOverride</c>）を返す。<br/>
    /// ホストが選択矩形（<see cref="LogicalSelectionRect"/>）と選択状態（<see cref="HasSelection"/>）を真のソースとして参照できる。
    /// </remarks>
    public Func<string?>? SizeLabelOverrideProvider { get; set; }

    /// <summary>選択なしでも全面暗転オーバーレイを表示する（切出しモード・領域選択用）。</summary>
    /// <remarks>
    /// true かつ <see cref="HasSelection"/> / <see cref="HasHighlight"/> がいずれも false の場合、
    /// 全面を暗転表示する。<br/>
    /// - 範囲選択（AreaSelect）: ドラッグ前（選択なし）から全面暗転を表示するために使用する。<br/>
    /// - 切出しモード（Crop）: 選択なしの初期状態を全面暗転で表示するために使用する。<br/>
    /// 選択あり（HasSelection / HasHighlight）の場合は穴あけ暗転（<see cref="LogicalSelectionRect"/> / <see cref="LogicalHighlightRect"/>）が優先される。
    /// </remarks>
    public bool ShowFullOverlay { get; set; }

    // ====================================================================
    // 公開メソッド
    // ====================================================================

    /// <summary>暗転オーバーレイと付随する全ての描画要素を描画する。</summary>
    /// <param name="context">描画コンテキスト</param>
    /// <param name="boundsSize">ウィンドウのクライアントサイズ（DIP）</param>
    /// <remarks>
    /// 以下の順で描画する:<br/>
    /// 1. 暗転マスク ＋ サイズラベル ＋ サブライン（<see cref="FadeMaskDrawer.DrawOverlay"/>）<br/>
    /// 2. 選択枠線（<see cref="GetBorderRects"/>。穴あけ矩形がある場合のみ）<br/>
    /// 拡大鏡は Visual Tree に配置した LoupeControl 自身が描画する（本レンダラーの責務外）。<br/>
    /// </remarks>
    public void Draw(DrawingContext context, AvaloniaSize boundsSize)
    {
        var clientRect = new AvaloniaRect(0, 0, boundsSize.Width, boundsSize.Height);
        var textColor = TextColor;

        // 穴あけ矩形の決定（論理矩形 + 座標変換ポートを描画時に評価。ポート未設定は等価変換）
        // 選択矩形は閉区間（+1）で DIP 変換する。ハイライト矩形（スクリーン/ウィンドウ境界）は
        // 既に全ピクセルを含む閉区間表現（PixelRect 由来）のため +1 しない（1px 大きく表示されるのを防ぐ）。
        var port = CoordinateTransform ?? CoordinatePort.Identity;
        var selectionRect = LogicalSelectionRect is { } logical
            ? ToDipRect(logical, port)
            : (AvaloniaRect?)null;
        var highlightRect = LogicalHighlightRect is { } hl
            ? ToDipRect(hl, port, closedInterval: false)
            : (AvaloniaRect?)null;
        var holeRect = HasHighlight ? highlightRect
                    : HasSelection ? selectionRect
                    : (AvaloniaRect?)null;

        if (holeRect.HasValue || ShowFullOverlay)
        {
            // サイズラベルのプロバイダはラベル表示時のみ評価する（非表示時・描画しないケースでの無駄な呼び出しを回避）
            var sizeLabelOverride = ShowSizeLabel ? SizeLabelOverrideProvider?.Invoke() : null;

            // サブライン（サイズラベルの下に表示する 2 行目以降）は穴あけ矩形がある場合のみ生成する
            // - 2 行目 = 操作ラベル（モード別の確定・キャンセル方法）
            // - 3 行目 = 情報ラベル（WindowSelect のウィンドウタイトル）
            var subLines = holeRect.HasValue ? BuildSubLines() : null;

            // 暗転マスク＋サイズラベル（Selection の責務。穴あけ矩形が null の場合は全面暗転）
            FadeMaskDrawer.DrawOverlay(
                context,
                clientRect,
                holeRect,
                OverlayArgbColor,
                textColor,
                FontSize,
                ShowSizeLabel,
                sizeLabelOverride,
                DisplayScaleScaling,
                subLines,
                TextBackgroundColor);

            // 枠線（ハイライト/選択矩形の輪郭）: DragRectControl が非表示のモード
            // （ScreenSelect / WindowSelect）でも枠線を表示するため、ここで描画する。
            // AreaSelect では DragRectControl が枠線・ハンドルを描画するが、二重描画しても
            // 同色・同太さのため視覚的な問題はない（重なりは同一位置）。
            if (holeRect is { } hr)
            {
                // 不透明度は BorderColor のアルファに統合済み（SolidColorBrush がアルファを反映する）
                var borderBrush = FadeMaskDrawer.GetBrush(BorderColor);
                // 矩形の四辺に枠線を描画する（半辺ずつ外側へ広げて矩形内に収める。矩形計算は純粋関数に委譲）
                foreach (var rect in GetBorderRects(hr, BorderThickness))
                {
                    context.DrawRectangle(borderBrush, null, rect);
                }
            }
        }

        // 拡大鏡はVisual Treeへ配置したLoupeControl自身が描画する。
    }

    /// <summary>論理座標の矩形をポート経由で DIP 矩形へ変換する。</summary>
    /// <param name="logical">論理座標（ドメイン）の矩形。</param>
    /// <param name="port">座標変換ポート。</param>
    /// <param name="closedInterval">閉区間（終点 +1）として扱うか。既定は true。</param>
    /// <returns>DIP 座標の矩形。</returns>
    /// <remarks>
    /// 選択領域は閉区間（始点・終点とも含む）を意図する仕様のため、<br/>
    /// 終点（Right / Bottom）に +1 してから DIP 変換する（終点のピクセルも非暗転領域に含める）。<br/>
    /// これにより暗転領域は始点・終点のいずれも含まない（キャプチャの閉区間 +1 と一致）。<br/>
    /// ハイライト矩形（ScreenSelect / WindowSelect のスクリーン・ウィンドウ境界）は最初から<br/>
    /// 全ピクセルを含む閉区間表現（PixelRect 由来）のため、<c>closedInterval: false</c> で +1 しない<br/>
    /// （+1 すると実際より 1px 大きい枠・サイズラベルになる）。
    /// </remarks>
    internal static AvaloniaRect ToDipRect(AvaloniaRect logical, ICoordinatePort port, bool closedInterval = true)
    {
        var tl = port.Inverse(logical.TopLeft.ToPointD());
        var br = port.Inverse(closedInterval
            ? new PointD(logical.Right + 1, logical.Bottom + 1)
            : logical.BottomRight.ToPointD());
        return new AvaloniaRect(tl.ToAvaloniaPoint(), br.ToAvaloniaPoint());
    }

    /// <summary>選択矩形の四辺に描く枠線の矩形を計算する（純粋関数）。</summary>
    /// <param name="rect">選択矩形（DIP 座標）。</param>
    /// <param name="thickness">枠線の太さ（DIP）。0.5 未満は 0.5 に正規化される。</param>
    /// <returns>上・下・左・右の順の枠線矩形（4 個）。</returns>
    /// <remarks>
    /// UI 非依存の純粋計算のため、ヘッドレス不要の <c>[Fact]</c> で並列テスト可能。<br/>
    /// 各辺は矩形の内側に描く（上辺は Y、下辺は Bottom − t、左右は内側に t 幅）。<br/>
    /// 高さ・幅が負にならないよう <c>Math.Max(0, ...)</c> でクランプする。
    /// </remarks>
    internal static IReadOnlyList<AvaloniaRect> GetBorderRects(AvaloniaRect rect, double thickness)
    {
        var t = Math.Max(0.5, thickness);
        return
        [
            new AvaloniaRect(rect.X, rect.Y, rect.Width, t),                                                 // 上
            new AvaloniaRect(rect.X, rect.Bottom - t, rect.Width, t),                                        // 下
            new AvaloniaRect(rect.X, rect.Y + t, t, Math.Max(0, rect.Height - 2 * t)),                       // 左
            new AvaloniaRect(rect.Right - t, rect.Y + t, t, Math.Max(0, rect.Height - 2 * t)),               // 右
        ];
    }

    /// <summary>サイズラベルの下に表示するサブライン（2 行目以降）を生成する。</summary>
    /// <returns>サブラインのリスト。表示不要の場合は空リスト。</returns>
    /// <remarks>
    /// 各行は <see cref="FadeMaskDrawer.SubLine"/>（テキスト + フォントサイズ + 色）で個別に指定する。<br/>
    /// - 2 行目 = 操作ラベル（キー・マウス操作の確定 / キャンセル方法）。<see cref="ShowSubLines"/> で表示/非表示を制御。<br/>
    /// - 3 行目 = 情報ラベル（WindowSelect のウィンドウタイトル）。<see cref="InfoLabelVisible"/> で表示/非表示を制御。<br/>
    /// サイズラベル（1 行目）が非表示の場合は、本リストが 1 行目から描画される（繰り上げは <see cref="FadeMaskDrawer.DrawOverlay"/> が行う）。<br/>
    /// 情報ラベル（ウィンドウタイトル / スクリーン番号）は左上のラベル領域（3 行目）に集約する。
    /// </remarks>
    internal IReadOnlyList<FadeMaskDrawer.SubLine> BuildSubLines()
    {
        // 表示する行がない場合は空リストを返す（描画側は空リストをスキップする）
        if (!ShowSubLines
            && !(InfoLabelVisible && Mode == FadeMaskRenderMode.WindowSelect && !string.IsNullOrEmpty(WindowTitle)))
        {
            return Array.Empty<FadeMaskDrawer.SubLine>();
        }

        var lines = new List<FadeMaskDrawer.SubLine>(2);

        // 2 行目: 操作ラベル（キー・マウス操作の確定 / キャンセル方法）
        if (ShowSubLines)
        {
            // 確定操作の文字列は 2 系統（Enter 系 / ダブルクリック系）に集約する（モード別の重複排除）
            string operationText = Mode is FadeMaskRenderMode.ScreenSelect or FadeMaskRenderMode.WindowSelect
                ? "Enter / クリック: 確定  Esc: キャンセル"
                : "ダブルクリック: 確定  Esc / 矩形外クリック: キャンセル";
            lines.Add(new FadeMaskDrawer.SubLine(
                operationText, SubLineFontSize, SubLineColor,
                SubLineBackgroundColor));
        }

        // 3 行目: 情報ラベル（WindowSelect のウィンドウタイトル）。InfoLabelVisible で表示/非表示を制御
        if (InfoLabelVisible && Mode == FadeMaskRenderMode.WindowSelect && !string.IsNullOrEmpty(WindowTitle))
        {
            lines.Add(new FadeMaskDrawer.SubLine(
                WindowTitle, InfoLabelFontSize, InfoLabelColor,
                InfoLabelBackgroundColor));
        }

        return lines;
    }
}


