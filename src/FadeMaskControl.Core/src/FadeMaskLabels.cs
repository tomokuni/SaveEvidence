using Estable.Platform.Geometry;

namespace Estable.FadeMask;

/// <summary>暗転オーバーレイのラベル表示の判定とテキスト選定（純粋関数）。</summary>
/// <remarks>
/// サイズラベルの下に表示するサブライン（操作ラベル・情報ラベル）の決定を単一所有する。<br/>
/// 各 UI（Avalonia / MewUI）は同じ判定・同じ文言を使う。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
internal static class FadeMaskLabels
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
    public static bool ShouldShowInfoLabel(FadeMaskRenderMode mode, bool infoLabelVisible, string windowTitle)
        => infoLabelVisible && mode == FadeMaskRenderMode.WindowSelect && !string.IsNullOrEmpty(windowTitle);

    /// <summary>操作ラベル（確定・キャンセル方法）のテキストを選定する。</summary>
    /// <param name="mode">オーバーレイ描画モード。</param>
    /// <returns>モードに応じた操作ラベルのテキスト。</returns>
    /// <remarks>
    /// 確定操作の文字列は 2 系統（Enter 系 / ダブルクリック系）に集約する。<br/>
    /// スクリーン選択・ウィンドウ選択は Enter 系、範囲選択・切出しはダブルクリック系とする。
    /// </remarks>
    public static string GetOperationText(FadeMaskRenderMode mode)
        => mode is FadeMaskRenderMode.ScreenSelect or FadeMaskRenderMode.WindowSelect
            ? EnterOperationText
            : DoubleClickOperationText;
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
        var d = displayScaling is > 0 ? displayScaling.Value : s;
        var left = (int)(dipRect.X * s);
        var top = (int)(dipRect.Y * s);
        var right = (int)(dipRect.Right * s);
        var bottom = (int)(dipRect.Bottom * s);
        var width = (int)(dipRect.Width * s);
        var height = (int)(dipRect.Height * s);
        var percent = (int)(d * 100);
        return $"({left}, {top}) - ({right}, {bottom}),  ({width}×{height}), DisplayScale: ({percent}%)";
    }}
