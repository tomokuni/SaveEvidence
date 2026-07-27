using Avalonia;
using Avalonia.Media;
using Estable.AvaloniaUI.Platform;

namespace Estable.AvaloniaUI.FadeMask;

/// <summary>OverlayWindow の暗転オーバーレイ描画ロジックを提供する。</summary>
/// <remarks>
/// 穴あけマスク（暗転＋選択枠＋サイズラベル）、ウィンドウタイトル（WindowSelect）、
/// スクリーンラベル（SelectScreen）、ルーペ（AreaSelect）の描画を担当する。<br/>
/// <see cref="FadeMaskControl"/> の <c>Render()</c>（ExternalRenderer）から呼び出されることを想定し、
/// 描画に必要な全ての状態をプロパティとして外部から注入する。<br/>
/// </remarks>
public sealed class FadeMaskRenderer
{
    // ====================================================================
    // 依存関係（OverlayWindow / FadeMaskControl から注入）
    // ====================================================================

    /// <summary>拡大鏡管理（AreaSelect モードの拡大鏡描画用）。拡大鏡は Visual Tree 上の
    /// LoupeControl 自身が描画するため、ここではルーペ非表示判定にのみ使用する。</summary>
    // 注: ルーペ描画は LoupeControl（Visual Tree 配置）自身が行うため、本レンダラーはルーペに依存しない。

    // ====================================================================
    // 描画状態（Render 前に設定）
    // ====================================================================

    /// <summary>DPI スケーリング係数。</summary>
    public double Scaling { get; set; } = 1.0;

    /// <summary>サイズラベル表示用の DPI スケーリング係数のバッキングフィールド（マルチスクリーン補正）。</summary>
    private double _displayScaleScaling;

    /// <summary>サイズラベル表示用の DPI スケーリング係数（マルチスクリーン補正）。</summary>
    /// <value>未設定（0以下）の場合は <see cref="Scaling"/> と同じ値が使用される。</value>
    /// <remarks>
    /// マルチスクリーン環境では、選択矩形が存在するスクリーンのスケーリング係数が設定される（仕様 1.6）。<br/>
    /// サイズラベルの物理ピクセル換算に使用する。<br/>
    /// </remarks>
    public double DisplayScaleScaling
    {
        get => _displayScaleScaling > 0 ? _displayScaleScaling : Scaling;
        set => _displayScaleScaling = value;
    }

    /// <summary>ラベルテキストの色。</summary>
    public Color TextColor { get; set; } = Colors.White;

    /// <summary>サイズラベル（1 行目）の背景の色（不透明度は色のアルファに統合。既定は黒 60%）。</summary>
    public Color TextBackgroundColor { get; set; } = Color.FromArgb(153, 0, 0, 0);

    /// <summary>ラベルテキストのフォントサイズ（DIP）。</summary>
    public double FontSize { get; set; } = 14.0;

    /// <summary>暗転オーバーレイの色（アルファを含む。既定は黒 50% = アルファ 128）。</summary>
    public Color OverlayArgbColor { get; set; } = Color.FromArgb(128, 0, 0, 0);

    /// <summary>選択枠線の色（不透明度は色のアルファに統合。既定は DarkGray = 設定画面の選択枠の色）。</summary>
    public Color BorderColor { get; set; } = Color.FromArgb(0xFF, 0xA9, 0xA9, 0xA9);

    /// <summary>選択枠線の太さ（DIP）。</summary>
    public double BorderThickness { get; set; } = 2.0;

    /// <summary>情報ラベルの文字色。</summary>
    public Color InfoLabelColor { get; set; } = Colors.White;

    /// <summary>情報ラベルの背景の色（不透明度は色のアルファに統合。既定は黒 60%）。</summary>
    public Color InfoLabelBackgroundColor { get; set; } = Color.FromArgb(153, 0, 0, 0);

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
    public Rect? LogicalHighlightRect { get; set; }

    /// <summary>選択矩形（ドメイン座標 = 元画像ピクセル / 仮想スクリーンピクセル）。未設定の場合は null。</summary>
    /// <remarks>
    /// <see cref="CoordinateTransform"/> と組み合わせて、描画時に最新のレイアウトから DIP 矩形を算出する<br/>
    /// （DragRectControl と同方式。ウィンドウサイズ変更・ズーム・スクロールに追従）。<br/>
    /// <see cref="CoordinateTransform"/> 未設定（null）の場合はドメイン = DIP として等価変換する。
    /// </remarks>
    public Rect? LogicalSelectionRect { get; set; }

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
    /// <summary>サブライン（サイズラベルの下の 2 行目以降）の文字色。</summary>
    public Color SubLineColor { get; set; } = Color.FromRgb(0xDC, 0xDC, 0xDC);

    /// <summary>サブライン（2 行目以降）の背景の色（不透明度は色のアルファに統合。既定は黒 60%）。</summary>
    public Color SubLineBackgroundColor { get; set; } = Color.FromArgb(153, 0, 0, 0);

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
    /// - OverlayWindow: 設定値（<c>CaptureSettings.SizeLabelOverride</c>）を返す。<br/>
    /// ホストが選択矩形（<see cref="LogicalSelectionRect"/>）と選択状態（<see cref="HasSelection"/>）を真のソースとして参照できる。
    /// </remarks>
    public Func<string?>? SizeLabelOverrideProvider { get; set; }

    /// <summary>選択なしでも全面暗転オーバーレイを表示する（切出しモード・領域選択用）。</summary>
    /// <remarks>
    /// true かつ <see cref="HasSelection"/> / <see cref="HasHighlight"/> がいずれも false の場合、
    /// 全面を暗転表示する。<br/>
    /// - OverlayWindow の AreaSelect: ドラッグ前（選択なし）から全面暗転を表示するために使用する。<br/>
    /// - MainWindow の切出しモード（Crop）: 選択なしの初期状態を全面暗転で表示するために使用する。<br/>
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
    /// 1. Fix モードの固定背景画像（全画面キャプチャの静止画）<br/>
    /// 2. 暗転マスク ＋ 選択枠線 ＋ サイズラベル（<see cref="FadeMaskDrawer.DrawOverlay"/>）<br/>
    /// 3. ウィンドウタイトルテキスト（WindowSelect のハイライト時）<br/>
    /// 4. スクリーン番号ラベル（SelectScreen の選択時）<br/>
    /// 5. 拡大鏡（AreaSelect の領域選択時、最前面）<br/>
    /// </remarks>
    public void Draw(DrawingContext context, Size boundsSize)
    {
        var clientRect = new Rect(0, 0, boundsSize.Width, boundsSize.Height);
        var textColor = TextColor;

        // ── 2. 穴あけ矩形の決定（論理矩形 + 座標変換ポートを描画時に評価。ポート未設定は等価変換） ──
        var selectionRect = LogicalSelectionRect is { } logical
            ? ToDipRect(logical, CoordinateTransform ?? CoordinatePort.Identity)
            : (Rect?)null;
        var highlightRect = LogicalHighlightRect is { } hl
            ? ToDipRect(hl, CoordinateTransform ?? CoordinatePort.Identity)
            : (Rect?)null;
        var sizeLabelOverride = SizeLabelOverrideProvider?.Invoke();
        var holeRect = HasHighlight ? highlightRect
                    : HasSelection ? selectionRect
                    : (Rect?)null;

        if (holeRect.HasValue)
        {
            var hr = holeRect.Value;

            // サブライン（サイズラベルの下に表示する 2 行目以降）
            // - ScreenSelect / WindowSelect: 2 行目 = スクリーン番号 / ウィンドウタイトル、3 行目 = 操作方法
            // - AreaSelect: 2 行目 = 操作方法
            // 情報ラベル（ウィンドウタイトル / スクリーン番号）は左上のラベル領域に集約する。
            var subLines = BuildSubLines();

            // 暗転マスク＋サイズラベル（Selection の責務）
            FadeMaskDrawer.DrawOverlay(
                context,
                clientRect,
                hr,
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
            // 不透明度は borderColor のアルファに統合済み（SolidColorBrush がアルファを反映する）
            var borderColor = BorderColor;
            var borderBrush = new SolidColorBrush(borderColor);
            var t = Math.Max(0.5, BorderThickness);
            // 矩形の四辺に枠線を描画する（半辺ずつ外側へ広げて矩形内に収める。矩形計算は純粋関数に委譲）
            foreach (var rect in GetBorderRects(hr, t))
            {
                context.DrawRectangle(borderBrush, null, rect);
            }

            // DragRectControl が選択枠・ハンドル・始点マーカーを描画する。

            // 注記: 拡大鏡は穴あけ矩形ブロック内では描画しない。
            // メソッド末尾で一括描画する（最前面・選択矩形の有無に関わらず AreaSelect では常時表示、仕様 5.3.1）。
        }
        else if (ShowFullOverlay)
        {
            // ── 選択なしで全面暗転（MainWindow 切出しモード用） ──
            FadeMaskDrawer.DrawOverlay(
                context,
                clientRect,
                null,    // 選択矩形なし → 全面暗転マスク
                OverlayArgbColor,
                textColor, FontSize,
                ShowSizeLabel, sizeLabelOverride, DisplayScaleScaling,
                textBgColor: TextBackgroundColor);

        }

        // 拡大鏡はVisual Treeへ配置したLoupeControl自身が描画する。
    }

    /// <summary>論理座標の矩形をポート経由で DIP 矩形へ変換する。</summary>
    /// <param name="logical">論理座標（ドメイン）の矩形。</param>
    /// <param name="port">座標変換ポート。</param>
    /// <returns>DIP 座標の矩形。</returns>
    /// <remarks>
    /// 選択領域は閉区間（始点・終点とも含む）を意図する仕様のため、<br/>
    /// 終点（Right / Bottom）に +1 してから DIP 変換する（終点のピクセルも非暗転領域に含める）。<br/>
    /// これにより暗転領域は始点・終点のいずれも含まない（キャプチャの閉区間 +1 と一致）。
    /// </remarks>
    internal static Rect ToDipRect(Rect logical, ICoordinatePort port)
    {
        var tl = port.Inverse(logical.TopLeft);
        var br = port.Inverse(new Point(logical.Right + 1, logical.Bottom + 1));
        return new Rect(tl, br);
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
    internal static IReadOnlyList<Rect> GetBorderRects(Rect rect, double thickness)
    {
        var t = Math.Max(0.5, thickness);
        return
        [
            new Rect(rect.X, rect.Y, rect.Width, t),                                                 // 上
            new Rect(rect.X, rect.Bottom - t, rect.Width, t),                                        // 下
            new Rect(rect.X, rect.Y + t, t, Math.Max(0, rect.Height - 2 * t)),                       // 左
            new Rect(rect.Right - t, rect.Y + t, t, Math.Max(0, rect.Height - 2 * t)),               // 右
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
        var lines = new List<FadeMaskDrawer.SubLine>(2);

        // 2 行目: 操作ラベル（キー・マウス操作の確定 / キャンセル方法）
        if (ShowSubLines)
        {
            string operationText = Mode switch
            {
                FadeMaskRenderMode.ScreenSelect => "Enter / クリック: 確定  Esc: キャンセル",
                FadeMaskRenderMode.WindowSelect => "Enter / クリック: 確定  Esc: キャンセル",
                FadeMaskRenderMode.AreaSelect => "ダブルクリック: 確定  Esc / 矩形外クリック: キャンセル",
                _ => "ダブルクリック: 確定  Esc / 矩形外クリック: キャンセル", // Crop
            };
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


