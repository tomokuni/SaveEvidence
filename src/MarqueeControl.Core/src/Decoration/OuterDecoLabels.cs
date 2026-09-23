using EsUtil.Platform.Core.Geometry;

namespace Estable.Marquee.Decoration;

/// <summary>暗転オーバーレイのラベル表示の判定とテキスト選定（純粋関数）。</summary>
/// <remarks>
/// サイズラベルの下に表示するサブライン（操作ラベル・情報ラベル）の決定を単一所有する。<br/>
/// 各 UI（Avalonia / MewUI）は同じ判定・同じ文言を使う。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
internal static class OuterDecoLabels
{
    /// <summary>確定（Enter 系）モードの操作ラベル。</summary>
    private const string EnterOperationText = "Enter / クリック: 確定  Esc: キャンセル";

    /// <summary>確定（ダブルクリック系）モードの操作ラベル。</summary>
    private const string DoubleClickOperationText = "ダブルクリック: 確定  Esc / 矩形外クリック: キャンセル";

    /// <summary>情報ラベル（ウィンドウタイトル）を表示するかどうかを判定する。</summary>
    /// <param name="mode">オーバーレイ描画モード。</param>
    /// <param name="infoLabelVisible">情報ラベルの表示が有効か。</param>
    /// <param name="windowTitle">ウィンドウタイトル。</param>
    /// <returns>情報ラベルを表示する場合は true。</returns>
    /// <remarks>WindowSelect モードで表示が有効かつウィンドウタイトルが非空の場合のみ表示する。</remarks>
    public static bool ShouldShowInfoLabel(OuterDecoRenderMode mode, bool infoLabelVisible, string windowTitle)
        => infoLabelVisible && mode == OuterDecoRenderMode.WindowSelect && !string.IsNullOrEmpty(windowTitle);

    /// <summary>操作ラベル（確定・キャンセル方法）のテキストを選定する。</summary>
    /// <param name="mode">オーバーレイ描画モード。</param>
    /// <returns>モードに応じた操作ラベルのテキスト。</returns>
    /// <remarks>
    /// 確定操作の文字列は 2 系統（Enter 系 / ダブルクリック系）に集約する。<br/>
    /// スクリーン選択・ウィンドウ選択は Enter 系、範囲選択・切出しはダブルクリック系とする。
    /// </remarks>
    public static string GetOperationText(OuterDecoRenderMode mode)
        => mode is OuterDecoRenderMode.ScreenSelect or OuterDecoRenderMode.WindowSelect
            ? EnterOperationText
            : DoubleClickOperationText;

    /// <summary>サイズラベルの下に表示するサブライン（操作ラベル + 情報ラベル）を組み立てる。</summary>
    /// <param name="mode">オーバーレイ描画モード（操作ラベルの文言選定に使用する）。</param>
    /// <param name="showSubLines">操作ラベルを表示するか。</param>
    /// <param name="infoLabelVisible">情報ラベルの表示が有効か。</param>
    /// <param name="windowTitle">ウィンドウタイトル（情報ラベルの文言）。null 可。</param>
    /// <param name="operationStyle">操作ラベルの外観。</param>
    /// <param name="infoStyle">情報ラベルの外観。</param>
    /// <returns>サブラインの一覧（上から順）。表示する行がない場合は空。</returns>
    /// <remarks>
    /// 1. 2 行目 = 操作ラベル（モード別の確定・キャンセル方法）。<paramref name="showSubLines"/> で表示可否を制御する。<br/>
    /// 2. 3 行目 = 情報ラベル（ウィンドウタイトル）。<see cref="ShouldShowInfoLabel"/> の判定に従う。<br/>
    /// <br/>
    /// 本メソッドが行の組み立てを単一所有する（両 UI で同一の文言・順序・外観になる）。<br/>
    /// 呼び出し元は <see cref="OuterDecoAppearance.BuildSubLines"/>（外観の公開面）に集約する。
    /// </remarks>
    public static IReadOnlyList<OuterDecoSubLine> BuildSubLines(
        OuterDecoRenderMode mode,
        bool showSubLines,
        bool infoLabelVisible,
        string windowTitle,
        OuterDecoLabelStyle operationStyle,
        OuterDecoLabelStyle infoStyle)
    {
        var lines = new List<OuterDecoSubLine>(2);

        if (showSubLines)
        {
            lines.Add(ToSubLine(GetOperationText(mode), operationStyle));
        }

        if (ShouldShowInfoLabel(mode, infoLabelVisible, windowTitle))
        {
            lines.Add(ToSubLine(windowTitle, infoStyle));
        }

        return lines;
    }

    /// <summary>テキストと外観からサブライン 1 行を生成する。</summary>
    /// <param name="text">行のテキスト。</param>
    /// <param name="style">行の外観。</param>
    /// <returns>生成したサブライン（背景色は実効色へ解決済み）。</returns>
    /// <remarks>
    /// 背景色は <see cref="OuterDecoAppearance.ResolveLabelBackground"/> で実効色へ解決する（アルファ 0 は黒 50%）。<br/>
    /// UI 層は背景色の解決規則を持たず、<see cref="OuterDecoSubLine.BackgroundColor"/> をそのまま描画できる。
    /// </remarks>
    private static OuterDecoSubLine ToSubLine(string text, OuterDecoLabelStyle style)
        => new(text, style.FontSize, style.Color, OuterDecoAppearance.ResolveLabelBackground(style.BackgroundColor));
    /// <summary>サイズラベルの表示スケール（DisplayScale）の実効値を解決する。</summary>
    /// <param name="displayScaling">指定された表示スケール。0 以下の場合は <paramref name="scaling"/> を使う。</param>
    /// <param name="scaling">表示スケール未指定時の代替値（座標換算の倍率）。</param>
    /// <returns>実効の表示スケール。</returns>
    /// <remarks>
    /// 「DisplayScaleScaling が 0 以下の場合は Scaling と同じ値として扱う」という規則を単一所有する。<br/>
    /// 各 UI の <c>DisplayScaleScaling</c> プロパティと <see cref="FormatSizeLabel"/> が本メソッドを使う（重複排除）。
    /// </remarks>
    public static double ResolveDisplayScaling(double displayScaling, double scaling)
        => displayScaling > 0 ? displayScaling : scaling;

    /// <summary>サイズラベルのテキストを生成する（DIP → 物理ピクセル変換）。</summary>
    /// <param name="dipRect">ラベルの対象矩形（DIP 座標）。</param>
    /// <param name="scaling">座標・サイズを物理ピクセルへ換算する係数（1.0=100%, 1.25=125%）。0 以下は 1.0 として扱う。</param>
    /// <param name="displayScaling">DisplayScale 表示用の係数（画面の実表示スケール）。null または 0 以下は <paramref name="scaling"/> を使用する。</param>
    /// <returns>"(L, T) - (R, B),  (W×H), DisplayScale: (P%)" 形式のテキスト。</returns>
    /// <remarks>
    /// 座標・サイズは物理ピクセル（<c>DIP × scaling</c> の整数切り捨て）で表示する。<br/>
    /// 値がドメイン座標（画像ピクセル）の場合は <paramref name="scaling"/> に 1.0 を渡す。<br/>
    /// このとき <paramref name="displayScaling"/> へ画面の実表示スケールを渡すと、座標は画像ピクセルのまま<br/>
    /// DisplayScale の百分率だけを実表示スケールで表示できる（画像ピクセル = 1:1 の表示でも画面が 125% なら 125% と表示する）。
    /// </remarks>
    public static string FormatSizeLabel(EsRect dipRect, double scaling, double? displayScaling = null)
    {
        var s = scaling > 0 ? scaling : 1.0;
        var d = ResolveDisplayScaling(displayScaling ?? 0, s);
        var left = (int)(dipRect.X * s);
        var top = (int)(dipRect.Y * s);
        var right = (int)(dipRect.Right * s);
        var bottom = (int)(dipRect.Bottom * s);
        var width = (int)(dipRect.Width * s);
        var height = (int)(dipRect.Height * s);
        var percent = (int)(d * 100);
        return $"({left}, {top}) - ({right}, {bottom}),  ({width}×{height}), DisplayScale: ({percent}%)";
    }}
