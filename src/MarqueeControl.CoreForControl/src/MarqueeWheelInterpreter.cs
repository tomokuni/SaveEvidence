using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;

namespace Estable.Marquee.CoreForControl;

/// <summary>ホイールによるスクロールの適用先（ビューポート実装の抽象）。</summary>
/// <remarks>
/// UI 層が自 UI のスクロール機構を本インターフェースへ適合させて注入する。<br/>
/// 回転量 → 移動量の換算（1 ノッチ = 48 DIP・Shift で横）は本層の <c>WheelScrollDelta</c> が単一所有するため、<br/>
/// 実装は受け取った移動量を自身のスクロール位置へ加算するだけでよい。<br/>
/// 適用先（祖先のスクロールコンテナ・ズームビューポート等）の決定は UI 層とホストが担う。
/// </remarks>
public interface IWheelScrollTarget
{
    /// <summary>移動量（DIP）だけスクロールする。</summary>
    /// <param name="delta">移動量（横・縦。1 ノッチあたりの換算は本層の <c>WheelScrollDelta</c> が済ませている）。</param>
    void ScrollBy(EsVector delta);
}

/// <summary>ホイールの回転量を移動量へ換算する（スクロールの移動量規則の単一所有）。</summary>
/// <remarks>
/// 1 ノッチあたりの移動量は <see cref="ScrollMath.WheelScrollUnit"/>（<c>Platform.Core</c>）が単一所有する。<br/>
/// Shift 併用時は縦の回転量を横の移動量として扱う（横スクロールの意図）。<br/>
/// UI 層・ホストが <c>回転量 × 48</c> を自前で書かないための型である。<br/>
/// <b>外部公開しない</b>: スクロールの適用先（<see cref="IWheelScrollTarget"/>）は換算済みの移動量を受け取るため、
/// 換算規則は本層の内部手順に限定する。
/// </remarks>
internal static class WheelScrollDelta
{
    /// <summary>回転量と修飾キーからスクロールの移動量（DIP）を求める。</summary>
    /// <param name="deltaX">横方向の回転量（1 ノッチ = 1.0）。</param>
    /// <param name="deltaY">縦方向の回転量（1 ノッチ = 1.0。正 = 上方向）。</param>
    /// <param name="shift">Shift 併用かどうか（true の場合は縦の回転量を横の移動量として扱う）。</param>
    /// <returns>移動量（DIP）。</returns>
    public static EsVector Offset(double deltaX, double deltaY, bool shift)
    {
        var unit = ScrollMath.WheelScrollUnit;

        return shift
            ? new EsVector(deltaY * unit, 0)
            : new EsVector(deltaX * unit, deltaY * unit);
    }
}

/// <summary>ホイール入力を解釈し、ビューポート操作の要求（ズーム / スクロール）として通知する（コントロール化のための転送規則）。</summary>
/// <remarks>
/// <b>位置付け</b>: コアの公開ファサード（<see cref="Marquee"/>）へホイール入力を橋渡しする補助クラスである。<br/>
/// <br/>
/// <b>なぜ Core 層ではなく CoreForControl 層にあるか</b>: 「Ctrl+ホイール = ズーム」は選択（マーキー）の判断ではなく、<br/>
/// 選択コントロールを<b>ビューポートへ組み込む</b>ときにだけ必要な規則である。単体で使う Marquee には<br/>
/// ズームもスクロールも存在しないため、Core 層（<c>MarqueeControl.Core</c>）には置かず本層が単一所有する。<br/>
/// UI フレームワークには依存しない（修飾キーの真偽と回転量だけを受け取る）ため、UI 初期化なしの <c>[Fact]</c> で検証できる。<br/>
/// <br/>
/// <b>責務</b>:<br/>
/// - 解釈: Ctrl 併用はズーム要求（方向 = 回転量の符号）、それ以外はスクロール。<br/>
/// - 適用: 連携スクロール先（<see cref="IWheelScrollTarget"/>）がある場合は本クラスがスクロールを完結させる<br/>
///   （回転量 → 移動量の換算は本層の <c>WheelScrollDelta</c> が単一所有する）。<br/>
/// - 通知: 適用先が無い場合のスクロール、およびズームは要求として通知する（<see cref="ZoomRequested"/> / <see cref="ScrollRequested"/>）。<br/>
/// - ログ: ズーム要求の記録（書式は本層の <c>MarqueeWheelLogMessage</c> が単一所有する）。<br/>
/// <br/>
/// <b>実処理は受け手（ホスト）</b>: ズーム率の決定・表示の適用・スクロール先の決定は要求の受け手が行う。<br/>
/// 本クラスは要求を出すか連携先へ適用するだけで、ビューの状態（ズーム率・スクロール位置）を保持しない。<br/>
/// 選択状態を変更しないため、コアのセッション（選択の判断）には関与しない。
/// </remarks>
public sealed class MarqueeWheelInterpreter
{
    /// <summary>ログの出力先（ズーム要求の記録）を取得するコアの窓口。</summary>
    /// <remarks>
    /// ロガーはコアのファサードが保持するため、本クラスは自前のロガーを持たない<br/>
    /// （ホストは <see cref="Marquee.Log"/> を 1 回設定するだけで、選択のログとホイール要求のログの両方が同じ基盤へ出る）。
    /// </remarks>
    private readonly Marquee _marquee;

    /// <summary>連携するスクロールの適用先（null = 未連携。未連携時はスクロールを要求として通知する）。</summary>
    private IWheelScrollTarget? _scrollTarget;

    /// <summary>ホイール入力を解釈して要求を出す補助を初期化する。</summary>
    /// <param name="marquee">コアの公開ファサード（ロガーの保持元）。null 不可。</param>
    /// <exception cref="ArgumentNullException"><paramref name="marquee"/> が null の場合。</exception>
    public MarqueeWheelInterpreter(Marquee marquee)
    {
        ArgumentNullException.ThrowIfNull(marquee);
        _marquee = marquee;
    }

    /// <summary>Ctrl+ホイールによるズーム要求（方向と発生位置）。</summary>
    /// <remarks>ズーム率の決定と表示の適用は受け手（ズームビューポート等）が行う（本クラスは要求の通知のみを担う）。</remarks>
    public event EventHandler<WheelZoomRequest>? ZoomRequested;

    /// <summary>通常ホイール（Ctrl なし）によるスクロール要求（生の回転量・修飾キー・発生位置）。</summary>
    /// <remarks>連携スクロール先（<see cref="AttachScrollTarget"/>）がある場合は発火せず、本クラスがスクロールを適用する。</remarks>
    public event EventHandler<WheelScrollRequest>? ScrollRequested;

    /// <summary>連携するスクロールの適用先を設定する（ホイールでスクロールする対象）。</summary>
    /// <param name="target">スクロールの適用先。null で解除（解除後は <see cref="ScrollRequested"/> を発火する）。</param>
    /// <remarks>
    /// ホスト・UI 層がビューポート（祖先のスクロールコンテナ等）を適合させて注入する。<br/>
    /// 設定中は通常ホイールのスクロールを本クラスが完結させ、<see cref="ScrollRequested"/> は発火しない。
    /// </remarks>
    public void AttachScrollTarget(IWheelScrollTarget? target) => _scrollTarget = target;

    /// <summary>ホイール入力を解釈し、該当する要求（ズーム / スクロール）を通知する。</summary>
    /// <param name="controlKey">Ctrl（ズーム）を併用したかどうか。</param>
    /// <param name="shiftKey">Shift（横スクロールの意図）を併用したかどうか。</param>
    /// <param name="deltaX">横方向の回転量（1 ノッチ = 1.0）。</param>
    /// <param name="deltaY">縦方向の回転量（1 ノッチ = 1.0。正 = 上方向）。</param>
    /// <param name="position">ホイール操作の発生位置（コントロールローカル DIP）。</param>
    /// <remarks>
    /// 処理フロー:<br/>
    /// 1. Ctrl 併用の場合はズーム要求を通知して終了する（方向は回転量の符号で決まり、上方向 = ズームイン）。<br/>
    /// 2. 連携スクロール先がある場合は本クラスでスクロールを適用して終了する（通知しない）。<br/>
    /// 3. それ以外は回転量・Shift・発生位置をそのままスクロール要求として通知する（回転量が 0 でも通知する）。<br/>
    /// <br/>
    /// 解釈（どちらを出すか）・適用（連携先がある場合）・ログは本メソッドが単一所有する<br/>
    /// （UI 層は修飾キーの真偽と回転量を渡すだけ）。
    /// </remarks>
    public void Handle(bool controlKey, bool shiftKey, double deltaX, double deltaY, EsPoint position)
    {
        if (controlKey)
        {
            _marquee.Log.LogZoomRequested(deltaY > 0);
            ZoomRequested?.Invoke(this, new WheelZoomRequest(deltaY > 0, position));
            return;
        }

        // 連携先がある場合はスクロールを本層で完結させる（回転量 → 移動量の換算も本層が単一所有する）
        if (_scrollTarget is { } target)
        {
            target.ScrollBy(WheelScrollDelta.Offset(deltaX, deltaY, shiftKey));
            return;
        }

        ScrollRequested?.Invoke(this, new WheelScrollRequest(deltaX, deltaY, shiftKey, position));
    }
}
