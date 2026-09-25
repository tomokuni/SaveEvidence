using EsUtil.Feature.MarqueeCore;
using MarqueeFacade = EsUtil.Feature.MarqueeCore.Marquee;

namespace EsUtil.SaveEvidence.Marquee.CoreForControl;

/// <summary>ビューポート（パン・ズーム・表示サイズ）の変化を上位へ通知する（コントロール化のための転送規則）。</summary>
/// <remarks>
/// <b>位置付け</b>: コアの公開ファサード（<see cref="MarqueeFacade"/>）へビューポートの変化を橋渡しする補助クラスである。<br/>
/// <br/>
/// <b>なぜ Core 層ではなく CoreForControl 層にあるか</b>: ビューポートは選択コントロールを<b>他の UI へ組み込む</b>ときに<br/>
/// 初めて存在する概念である（単体で使う Marquee にはビューポートもスクロールも無い）。<br/>
/// Core 層（<c>Feature.MarqueeCore</c>）には置かず本層が単一所有する。<br/>
/// UI フレームワークには依存しない（通知の受け手はコールバックで受け取る）ため、UI 初期化なしの <c>[Fact]</c> で検証できる。<br/>
/// <br/>
/// <b>責務</b>: ビューポートの変化を「現在の状態スナップショットの通知」と「再描画の要求」へ変換する。<br/>
/// 選択の状態は変更しない（ビューポートが変わっても選択矩形＝ドメイン座標は不変のため）。<br/>
/// <br/>
/// <b>実処理は受け手（ホスト）</b>: 通知を受けた上位（暗転マスク等）がビューポート座標で再描画する。
/// </remarks>
public sealed class MarqueeViewportNotifier
{
    /// <summary>通知に載せる状態の取得元（コアの公開ファサード）。</summary>
    private readonly MarqueeFacade _marquee;

    /// <summary>再描画の要求先（UI 層が実装する効果の窓口）。</summary>
    private readonly IMarqueeEffects _effects;

    /// <summary>ビューポート変化の通知を初期化する。</summary>
    /// <param name="marquee">コアの公開ファサード（状態スナップショットの取得元）。null 不可。</param>
    /// <param name="effects">再描画の要求先（UI 層が実装する効果）。null 不可。</param>
    /// <exception cref="ArgumentNullException"><paramref name="marquee"/> または <paramref name="effects"/> が null の場合。</exception>
    public MarqueeViewportNotifier(MarqueeFacade marquee, IMarqueeEffects effects)
    {
        ArgumentNullException.ThrowIfNull(marquee);
        ArgumentNullException.ThrowIfNull(effects);

        _marquee = marquee;
        _effects = effects;
    }

    /// <summary>ビューポートの変化の通知（現在の状態スナップショットを運ぶ）。</summary>
    /// <remarks>UI 層は本イベントを自 UI の公開イベント（ビューポート変化の通知）へ転送するだけでよい。</remarks>
    public event EventHandler<MarqueeSnapshot>? ViewportChanged;

    /// <summary>ビューポート（パン・ズーム・表示サイズ）の変化を通知し、再描画を要求する。</summary>
    /// <remarks>
    /// 処理フロー:<br/>
    /// 1. 現在の状態スナップショットを <see cref="ViewportChanged"/> で通知する（上位がビューポート座標で再描画するため）。<br/>
    /// 2. 再描画を要求する（選択矩形・ハンドル・十字線を新しいビューポートで描き直す）。<br/>
    /// 状態（選択矩形・ドラッグ・nudge）は変更しない。
    /// </remarks>
    public void Notify()
    {
        ViewportChanged?.Invoke(this, _marquee.CreateSnapshot());
        _effects.RequestRedraw();
    }
}
