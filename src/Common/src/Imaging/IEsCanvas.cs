using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.SaveEvidence.Common.Imaging;

/// <summary>描画内容を描画面へ写す即時描画ポート（UI フレームワーク非依存）。</summary>
/// <remarks>
/// <b>位置付け</b>: 「何を描くか」を決めるコアと「どう描くか」を担う UI の境界である。<br/>
/// コア側（<c>Feature.Marquee.Presenter</c> / <c>Feature.Loupe.Presenter</c>）は本ポートだけを呼び、<br/>
/// 各 UI は自 UI の描画 API を本ポートへ適合させる（アダプタ）。<br/>
/// これにより描画の手順はコア側の 1 実装に集約され、UI の追加・描画方式の差し替えはアダプタの実装だけで済む。<br/>
/// <br/>
/// <b>座標系</b>: 呼び出し側が定める単位（DIP または物理ピクセル）で描く。単位の換算はアダプタが担う<br/>
/// （例: 物理ピクセルで描く UI はキャンバスへスケーリングを適用してから本ポートへ渡す）。<br/>
/// <b>描画順</b>: 呼び出し順に上書きする（半透明は重ね塗りになる）。<br/>
/// <b>破棄</b>: 本ポートは描画面を所有しない（アダプタが保持する描画面の寿命に従う）。<br/>
/// <b>例外</b>: 引数の不正は <see cref="ArgumentNullException"/> 等を投げてよい（実装の任意）。<br/>
/// UI フレームワークに依存しないため、テストでは Skia のビットマップ（<c>SkiaEsCanvas</c>）を使い、<br/>
/// UI 初期化とウィンドウなしで描画結果の画素を検証できる。
/// </remarks>
public interface IEsCanvas
{
    /// <summary>描画面の全体を指定色で置き換える（前フレームの残像を消す）。</summary>
    /// <param name="surface">描画面の矩形（描画単位）。</param>
    /// <param name="color">置き換える色（不透明度はアルファ）。</param>
    /// <remarks>
    /// 「毎フレーム新しい描画面が渡される UI」（フレームワークが消去する場合）では呼ぶ必要はない。<br/>
    /// アダプタは自 UI の消去 API へ写す（Skia は <c>Clear</c>、ベクター描画は矩形の塗り）。<br/>
    /// <b><paramref name="surface"/> を渡す理由</b>: 全面消去の API を持たない描画基盤では描画面の大きさが必要になるため、<br/>
    /// 描画面を知っている呼び出し側から渡す（アダプタが描画面の大きさを状態として保持しない）。
    /// </remarks>
    void Clear(EsRect surface, DrawingColor color);

    /// <summary>矩形を塗る。</summary>
    /// <param name="rect">塗る矩形。</param>
    /// <param name="color">塗り色（不透明度はアルファ）。</param>
    void FillRect(EsRect rect, DrawingColor color);

    /// <summary>複数の矩形を同じ色で塗る。</summary>
    /// <param name="rects">塗る矩形の一覧。空の場合は何もしない。</param>
    /// <param name="color">塗り色（不透明度はアルファ）。</param>
    /// <remarks>同色の矩形（4 辺の枠線・十字線等）をまとめて塗る（実装はペイントの生成を 1 回に抑える）。</remarks>
    void FillRects(IReadOnlyList<EsRect> rects, DrawingColor color);

    /// <summary>角丸矩形を塗る。</summary>
    /// <param name="rect">塗る矩形。</param>
    /// <param name="cornerRadius">角の半径（描画単位）。0 の場合は四角形として塗る。</param>
    /// <param name="color">塗り色（不透明度はアルファ）。</param>
    /// <remarks>角丸なし（0）は「描かない」ではなく「四角形として描く」指定である（呼び出し側の分岐を不要にする）。</remarks>
    void FillRoundRect(EsRect rect, double cornerRadius, DrawingColor color);

    /// <summary>矩形の枠線を描く（線は矩形の辺の上に中心を置く）。</summary>
    /// <param name="rect">枠の矩形。</param>
    /// <param name="color">線の色（不透明度はアルファ）。</param>
    /// <param name="thickness">線の太さ（描画単位）。</param>
    void StrokeRect(EsRect rect, DrawingColor color, double thickness);

    /// <summary>線分を描く。</summary>
    /// <param name="from">始点。</param>
    /// <param name="to">終点。</param>
    /// <param name="color">線の色（不透明度はアルファ）。</param>
    /// <param name="thickness">線の太さ（描画単位）。</param>
    void DrawLine(EsPoint from, EsPoint to, DrawingColor color, double thickness);

    /// <summary>ラベル 1 件（背景 + 文字）を描く。</summary>
    /// <param name="background">背景の矩形。</param>
    /// <param name="backgroundColor">背景色（不透明度はアルファ）。</param>
    /// <param name="text">描画する文字列。</param>
    /// <param name="textOrigin">文字の左上原点（背景の内側）。</param>
    /// <param name="fontSize">フォントサイズ（描画単位）。</param>
    /// <param name="textColor">文字色。</param>
    /// <param name="cornerRadius">背景の角丸半径（描画単位）。0 の場合は四角形。</param>
    /// <remarks>
    /// <b>背景を塗ってから文字を描く順序は本メソッドの契約である</b>（呼び出し側が 2 つのメソッドへ分けて呼ぶと<br/>
    /// 重なりが崩れ、実装ごとに順序が変わる）。順序の単一所有を保つため、ポートに含める。
    /// </remarks>
    void DrawLabel(
        EsRect background,
        DrawingColor backgroundColor,
        string text,
        EsPoint textOrigin,
        double fontSize,
        DrawingColor textColor,
        double cornerRadius);

    /// <summary>画像の一部を描画先の矩形へ描く（拡大・縮小は実装の補間方式に従う）。</summary>
    /// <param name="image">描く画像。</param>
    /// <param name="source">画像側の切り出し矩形（画像ピクセル座標）。</param>
    /// <param name="destination">描画先の矩形（描画単位）。</param>
    /// <remarks>
    /// 補間方式（最近傍・線形）は実装の描画方式に依存する（画素をそのまま拡大する UI と滑らかに拡大する UI がある）。<br/>
    /// 画像は破棄しない（所有権は呼び出し側に残る）。
    /// </remarks>
    void DrawImage(EsBitmap image, EsRect source, EsRect destination);

    /// <summary>テキストの実寸を測定する。</summary>
    /// <param name="text">測定するテキスト。</param>
    /// <param name="fontSize">フォントサイズ（描画単位）。</param>
    /// <returns>テキストの実寸（描画単位）。</returns>
    /// <remarks>
    /// 実寸は描画 API でしか測れないため、配置の計算（文字の実寸から背景の大きさと位置を求める処理）へ<br/>
    /// 実寸を渡す入口となる。
    /// </remarks>
    EsSize MeasureText(string text, double fontSize);
}
