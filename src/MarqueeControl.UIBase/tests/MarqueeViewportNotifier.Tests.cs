using EsUtil.Platform.Core.Geometry;
using Xunit;
using MarqueeFacade = EsUtil.Feature.MarqueeCore.Marquee;
using EsUtil.Feature.MarqueeCore.Interaction;

namespace EsUtil.SaveEvidence.Marquee.UIBase.Tests;

/// <summary><see cref="MarqueeViewportNotifier"/>（ビューポート変化の通知と再描画要求）の単体テスト。</summary>
/// <remarks>
/// コントロールをビューポートへ組み込んだときにだけ必要な「変化の再通知 + 再描画」の手順を、<br/>
/// UI フレームワークなしで検証する（UI 層は自 UI の公開イベントへ転送するだけになる）。
/// </remarks>
public class MarqueeViewportNotifierTests
{
    /// <summary>通知クラスと、通知されたスナップショットの収集先を生成する。</summary>
    /// <param name="effects">生成した効果のスパイ（効果は呼び出しのみ検証する）。</param>
    /// <param name="marquee">生成したコアの公開ファサード。</param>
    /// <returns>通知クラスと、通知されたスナップショットの収集先。</returns>
    private static (MarqueeViewportNotifier Notifier, List<MarqueeSnapshot> Snapshots) CreateNotifier(
        out EffectsSpy effects, out MarqueeFacade marquee)
    {
        effects = new EffectsSpy();
        marquee = new MarqueeFacade(effects);
        var notifier = new MarqueeViewportNotifier(marquee, effects);
        var snapshots = new List<MarqueeSnapshot>();
        notifier.ViewportChanged += (_, snapshot) => snapshots.Add(snapshot);
        return (notifier, snapshots);
    }

    [Fact]
    public void Notify_現在のスナップショットを通知し再描画を要求する()
    {
        var (notifier, snapshots) = CreateNotifier(out var effects, out var marquee);
        marquee.SetSelection(new EsRect(10, 20, 30, 40));
        var redrawsBefore = effects.RedrawCount;

        notifier.Notify();

        var snapshot = Assert.Single(snapshots);
        Assert.Equal(new EsRect(10, 20, 30, 40), snapshot.SelectionRect);
        // 選択の設定でも再描画が要求されるため、通知 1 回あたりの増分で検証する
        Assert.Equal(redrawsBefore + 1, effects.RedrawCount);
    }

    [Fact]
    public void Notify_選択なしでも通知する()
    {
        var (notifier, snapshots) = CreateNotifier(out var effects, out _);

        notifier.Notify();

        var snapshot = Assert.Single(snapshots);
        Assert.False(snapshot.HasSelection);
        Assert.Equal(1, effects.RedrawCount);
    }

    [Fact]
    public void Notify_状態を変更しない()
    {
        var (notifier, _) = CreateNotifier(out _, out var marquee);
        marquee.SetSelection(new EsRect(10, 20, 30, 40));

        notifier.Notify();

        // ビューポートの変化は選択矩形（ドメイン座標）を変えない
        Assert.Equal(new EsRect(10, 20, 30, 40), marquee.SelectionRect);
        Assert.True(marquee.HasSelection);
    }

    [Fact]
    public void Notify_購読者がいなくても再描画を要求する()
    {
        var (notifier, _) = CreateNotifier(out var effects, out _);

        // 通知先が無い場合でも再描画の要求は行う（? 呼び出しの null 分岐）
        notifier.Notify();

        Assert.Equal(1, effects.RedrawCount);
    }

    [Fact]
    public void 生成_引数がnullなら例外を投げる()
    {
        var marquee = new MarqueeFacade(new EffectsSpy());

        Assert.Throws<ArgumentNullException>(() => new MarqueeViewportNotifier(null!, new EffectsSpy()));
        Assert.Throws<ArgumentNullException>(() => new MarqueeViewportNotifier(marquee, null!));
    }
}
