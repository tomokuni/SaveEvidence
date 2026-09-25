using DrawingColor = System.Drawing.Color;
using EsUtil.Feature.MarqueeCore;
using EsUtil.Feature.MarqueeCore.Decoration;
using EsUtil.Feature.LoupeCore;

namespace EsUtil.SaveEvidence.FadeMaskDragRect;

/// <summary>領域選択の複合コントロール（選択矩形 + 暗転マスク + ルーペ）の設定を 1 つにまとめた不変の値。</summary>
/// <remarks>
/// 内包するコントロールが公開する設定（<see cref="MarqueeOptions"/> / <see cref="LoupeOptions"/>）と、<br/>
/// 複合コントロール固有の設定（選択矩形破線の色・ルーペのオフセット）をまとめて所有する。<br/>
/// UI 層は本値を 1 プロパティとして公開するだけであり、既定値・項目の対応・子コントロールへの分配規則を持たない<br/>
/// （値の設定経路（オブジェクト初期化子・<c>with</c> 式・逆シリアル化）によらず同じ結果になる）。<br/>
/// <br/>
/// <b>装飾は別の値が単一所有する</b>: 暗転マスク・穴あけ枠・ラベル（サイズ・操作・情報）の外観は<br/>
/// <see cref="Deco"/>（<see cref="MarqueeDecoOptions"/>）が持ち、選択の設定（<see cref="Marquee"/>）とは分けている<br/>
/// （装飾は選択コントロール単体の概念ではなく、コントロールを撮影オーバーレイ等へ組み込むときの表示のため）。<br/>
/// <br/>
/// <b>実行時状態</b>（選択矩形・ハイライト矩形・ルーペ中心・拡大元画像）と<b>環境値</b>（DPI スケーリング）は<br/>
/// 本値に含めない（モード・ホストの結線・DPI から決まる値のため）。
/// </remarks>
public sealed record FadeMaskDragRectOptions
{
    /// <summary>既定の設定を取得する。</summary>
    /// <value>各内包コントロールの既定値（<see cref="MarqueeOptions.Default"/> / <see cref="MarqueeDecoOptions.Default"/> / <see cref="LoupeOptions.Default"/>）と複合コントロール固有の既定値。</value>
    /// <remarks>既定値は各コア層の型が単一所有する（UI 層・ホストに既定値の写しを置かない）。</remarks>
    public static FadeMaskDragRectOptions Default { get; } = new();

    /// <summary>選択矩形の設定を取得する（選択の判断に使う項目のみ）。</summary>
    /// <value>選択矩形の外観・対話パラメータ。既定は <see cref="MarqueeOptions.Default"/>。</value>
    /// <remarks>設定は 1 回の代入で子コントロールへ分配される（項目ごとの写し替えを行わない）。</remarks>
    public MarqueeOptions Marquee { get; init; } = MarqueeOptions.Default;

    /// <summary>装飾（暗転マスク・穴あけ枠・ラベル）の設定を取得する。</summary>
    /// <value>装飾の外観・表示ポリシー・ハイライト。既定は <see cref="MarqueeDecoOptions.Default"/>。</value>
    /// <remarks>選択枠線と暗転マスクの穴あけ枠は同じ矩形に重なって描画されるため、枠線の色・太さは本値（外側の装飾の外観）が単一所有する。</remarks>
    public MarqueeDecoOptions Deco { get; init; } = MarqueeDecoOptions.Default;

    /// <summary>ホスト固有の付加情報（操作ラベル・情報ラベル）の外観を取得する。</summary>
    /// <value>操作・情報ラベルの表示可否・寸法・色。既定は <see cref="FadeMaskDragRectLabelOptions.Default"/>。</value>
    /// <remarks>
    /// 操作ラベル・情報ラベルは選択矩形コントロールの概念ではない（ホストが必要とする付加情報）ため、<br/>
    /// 文言の生成と描画はホストが行う。本値はその描画に使う外観のみを保持する。
    /// </remarks>
    public FadeMaskDragRectLabelOptions Labels { get; init; } = FadeMaskDragRectLabelOptions.Default;

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
}
