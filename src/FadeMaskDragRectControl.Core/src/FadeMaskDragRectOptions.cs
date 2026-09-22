using DrawingColor = System.Drawing.Color;
using Estable.DragRect;
using Estable.FadeMask;
using Estable.Loupe;

namespace Estable.FadeMaskDragRect;

/// <summary>領域選択の複合コントロール（選択矩形 + 暗転マスク + ルーペ）の設定を 1 つにまとめた不変の値。</summary>
/// <remarks>
/// 内包する 3 コントロールが公開する設定（<see cref="DragRectOptions"/> / <see cref="FadeMaskAppearance"/> / <see cref="LoupeOptions"/>）と、<br/>
/// 複合コントロール固有の設定（選択矩形破線の色・ルーペのオフセット）をまとめて所有する。<br/>
/// UI 層は本値を 1 プロパティとして公開するだけであり、既定値・項目の対応・子コントロールへの分配規則を持たない<br/>
/// （値の設定経路（オブジェクト初期化子・<c>with</c> 式・逆シリアル化）によらず同じ結果になる）。<br/>
/// <br/>
/// <b>枠線の共用</b>: 選択枠線（<see cref="DragRectOptions.BorderColor"/> / <see cref="DragRectOptions.BorderThickness"/>）は<br/>
/// 暗転マスクの穴あけ枠（<see cref="FadeMaskAppearance.BorderColor"/> / <see cref="FadeMaskAppearance.BorderThickness"/>）と<br/>
/// 同一の値を使う（2 つの枠が重なって見た目の太さが変わるため）。したがって本値では暗転マスク側だけを持ち、<br/>
/// 選択矩形側へは <see cref="ResolveDragRectOptions"/> が導出する（同じ値を 2 箇所に持たせない）。<br/>
/// <br/>
/// <b>実行時状態</b>（選択矩形・ハイライト矩形・ルーペ中心・拡大元画像）と<b>環境値</b>（DPI スケーリング）は<br/>
/// 本値に含めない（モード・ホストの結線・DPI から決まる値のため）。
/// </remarks>
public sealed record FadeMaskDragRectOptions
{
    /// <summary>既定の設定を取得する。</summary>
    /// <value>各内包コントロールの既定値（<see cref="DragRectOptions.Default"/> / <see cref="FadeMaskAppearance"/> / <see cref="LoupeOptions.Default"/>）と複合コントロール固有の既定値。</value>
    /// <remarks>既定値は各コア型が単一所有する（UI 層・ホストに既定値の写しを置かない）。</remarks>
    public static FadeMaskDragRectOptions Default { get; } = new();

    /// <summary>選択矩形の設定を取得する（枠線を除く。枠線は <see cref="ResolveDragRectOptions"/> が暗転マスクの外観から導出する）。</summary>
    /// <value>選択矩形の外観・対話パラメータ。既定は <see cref="DragRectOptions.Default"/>。</value>
    public DragRectOptions DragRect { get; init; } = DragRectOptions.Default;

    /// <summary>暗転マスクの外観を取得する（枠線は選択矩形と共用する）。</summary>
    /// <value>暗転色・枠線・サイズラベル／サブライン／情報ラベルの外観。既定はコアの既定値。</value>
    public FadeMaskAppearance Overlay { get; init; } = new();

    /// <summary>ルーペの設定を取得する（拡大率・表示サイズ・外観）。</summary>
    /// <value>ルーペの設定。既定は <see cref="LoupeOptions.Default"/>。</value>
    /// <remarks>市松模様の下地色（<see cref="LoupeAppearance.BackgroundColor"/>）も本値で与える（ホストが背後の要素を透かすかどうかを決める）。</remarks>
    public LoupeOptions Loupe { get; init; } = LoupeOptions.Default;

    /// <summary>ルーペ画像上の選択矩形破線の色 A（描画開始色）を取得する。</summary>
    /// <value>アルファを含む色。既定は白。</value>
    public DrawingColor SelectionRectColorA { get; init; } = DrawingColor.White;

    /// <summary>ルーペ画像上の選択矩形破線の色 B を取得する（色 A と交互に使用する）。</summary>
    /// <value>アルファを含む色。既定は黒。</value>
    public DrawingColor SelectionRectColorB { get; init; } = DrawingColor.Black;

    /// <summary>ルーペのカーソル中心からの右下オフセット（DIP）を取得する。</summary>
    /// <value>オフセット量（DIP）。既定は 20。</value>
    /// <remarks>
    /// ルーペがカーソルに重ならないようにする値。ホストがモード（プレビュー内の切り出し / 全画面の領域選択）に応じて与える。
    /// </remarks>
    public double LoupeOffset { get; init; } = 20.0;

    /// <summary>選択矩形コントロールへ適用する設定を求める（枠線は暗転マスクの外観から導出する）。</summary>
    /// <returns>枠線を暗転マスクの外観と揃えた選択矩形の設定。</returns>
    /// <remarks>
    /// 選択枠線と暗転マスクの穴あけ枠は同じ矩形に重なって描画されるため、太さ・色が異なると見た目が崩れる。<br/>
    /// 両者を同じ値に保つ規則を本メソッドが単一所有し、UI 層は本メソッドの結果を選択矩形コントロールへ渡すだけでよい。
    /// </remarks>
    public DragRectOptions ResolveDragRectOptions()
        => DragRect with
        {
            BorderColor = Overlay.BorderColor,
            BorderThickness = Overlay.BorderThickness,
        };
}
