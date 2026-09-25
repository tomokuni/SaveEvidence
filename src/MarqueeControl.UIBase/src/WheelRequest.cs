using EsUtil.Platform.Core.Geometry;

namespace EsUtil.SaveEvidence.Marquee.UIBase;

/// <summary>通常ホイール（Ctrl なし）によるスクロール要求（生の回転量・修飾キー・発生位置）。</summary>
/// <remarks>
/// 本要求は選択コントロールをビューポートへ組み込む場合にだけ必要になる（UIBase 層が単一所有する）。<br/>
/// 回転量をそのまま通知し、スクロール量やスクロール先の決定は受け手（祖先 <c>ScrollViewer</c>・<br/>
/// ズームビューポートのパン等）に委ねる。単位（1 ノッチあたりの移動量）と方向（縦横）の解釈は受け手が単一所有する。<br/>
/// Ctrl 併用のホイールはズーム要求（<see cref="WheelZoomRequest"/>）として別に通知する。
/// </remarks>
/// <param name="DeltaX">横方向の回転量（1 ノッチ = 1.0）。</param>
/// <param name="DeltaY">縦方向の回転量（1 ノッチ = 1.0。正 = 上方向）。</param>
/// <param name="Shift">Shift 併用かどうか（横スクロールの意図を示す）。</param>
/// <param name="Position">ホイール操作の発生位置（コントロールローカル DIP）。</param>
public readonly record struct WheelScrollRequest(double DeltaX, double DeltaY, bool Shift, EsPoint Position)
{
    /// <summary>回転量を 1 つのベクターとして取得する。</summary>
    /// <value>横・縦の回転量をまとめたベクター（コアの値型）。</value>
    /// <remarks>
    /// 回転量をベクターで受け取る API（ズームビューポートのパン等）へ渡すために使う。<br/>
    /// <b>コアの型同士の変換は本契約が単一所有する</b>（UI が <c>new EsVector(...)</c> で組み立てると、
    /// 同じ変換が UI ごとに散る）。
    /// </remarks>
    public EsVector Delta => new(DeltaX, DeltaY);
}

/// <summary>Ctrl+ホイールによるズーム要求（方向と発生位置）。</summary>
/// <remarks>
/// 本要求は選択コントロールをビューポートへ組み込む場合にだけ必要になる（UIBase 層が単一所有する）。<br/>
/// ズーム率の決定（段階・倍率）と表示の適用は受け手（ズームビューポート等）が行う。<br/>
/// <see cref="Position"/> はズーム中心（カーソル位置）として使用できる（コントロールローカル DIP）。
/// </remarks>
/// <param name="ZoomIn">true = ズームイン / false = ズームアウト。</param>
/// <param name="Position">ホイール操作の発生位置（コントロールローカル DIP）。</param>
public readonly record struct WheelZoomRequest(bool ZoomIn, EsPoint Position);
