using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Geometry;
using Estable.Marquee;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Estable.Marquee.CoreForControl.Tests;

/// <summary>テストで使用する効果（フォーカス・再描画・カーソル形状・キャプチャ・座標変換）のスパイ。</summary>
/// <remarks>本テストはホイールの解釈と適用のみを検証するため、効果は再描画の回数を数える以外は何もしない最小実装とする。</remarks>
internal sealed class EffectsSpy : IMarqueeEffects
{
    /// <summary>再描画の要求回数。</summary>
    public int RedrawCount { get; private set; }

    /// <inheritdoc/>
    public void RequestFocus() { }

    /// <inheritdoc/>
    public void RequestRedraw() => RedrawCount++;

    /// <inheritdoc/>
    public void SetCursorShape(MarqueeCursorKind kind) { }

    /// <inheritdoc/>
    public void SetPointerCapture(bool capture) { }

    /// <inheritdoc/>
    public EsPixelPoint ToScreenPoint(EsPoint domainPoint) => default;
}

/// <summary>テストで使用するスクロールの適用先（呼び出しを記録する）。</summary>
/// <remarks>連携スクロール先が設定されている場合、通常ホイールのスクロールは本クラスへ適用される。</remarks>
internal sealed class ScrollTargetSpy : IWheelScrollTarget
{
    /// <summary>適用された移動量の履歴（呼び出し順）。</summary>
    public List<EsVector> Deltas { get; } = [];

    /// <inheritdoc/>
    public void ScrollBy(EsVector delta) => Deltas.Add(delta);
}

/// <summary><see cref="MarqueeWheelInterpreter"/>（ホイール入力の解釈と要求・適用）の単体テスト。</summary>
/// <remarks>
/// 「Ctrl+ホイール = ズーム要求 / それ以外 = スクロール（連携先があれば適用）」というコントロール化の規則を、<br/>
/// UI フレームワークなしで検証する（UI 層は修飾キーの真偽と回転量を渡すだけになる）。
/// </remarks>
public class MarqueeWheelInterpreterTests
{
    /// <summary>ホイール補助クラスと、通知された要求の収集先を生成する。</summary>
    /// <param name="logger">ロガー。null の場合は設定しない（ファサードが NullLogger へ吸収する）。</param>
    /// <returns>ホイール補助クラスと、ズーム / スクロール要求の収集先。</returns>
    private static (MarqueeWheelInterpreter Wheel, List<WheelZoomRequest> Zooms, List<WheelScrollRequest> Scrolls) CreateWheel(
        ILogger? logger = null)
    {
        var marquee = new Marquee(new EffectsSpy());
        if (logger is not null)
        {
            marquee.Log = logger;
        }

        var wheel = new MarqueeWheelInterpreter(marquee);
        var zooms = new List<WheelZoomRequest>();
        var scrolls = new List<WheelScrollRequest>();
        wheel.ZoomRequested += (_, request) => zooms.Add(request);
        wheel.ScrollRequested += (_, request) => scrolls.Add(request);
        return (wheel, zooms, scrolls);
    }

    [Fact]
    public void 生成_コアのファサードがnullなら例外を投げる()
    {
        Assert.Throws<ArgumentNullException>(() => new MarqueeWheelInterpreter(null!));
    }

    [Fact]
    public void Handle_Ctrl併用はズーム要求を通知する()
    {
        var (wheel, zooms, scrolls) = CreateWheel();

        wheel.Handle(controlKey: true, shiftKey: false, deltaX: 0, deltaY: 1, new EsPoint(30, 40));

        Assert.Equal(new WheelZoomRequest(true, new EsPoint(30, 40)), Assert.Single(zooms));
        Assert.Empty(scrolls);
    }

    [Fact]
    public void Handle_上方向でない回転はズームアウトになる()
    {
        var (wheel, zooms, _) = CreateWheel();

        wheel.Handle(controlKey: true, shiftKey: false, deltaX: 0, deltaY: -1, new EsPoint(30, 40));

        Assert.False(Assert.Single(zooms).ZoomIn);
    }

    [Fact]
    public void Handle_Ctrlなしはスクロール要求を通知する()
    {
        var (wheel, zooms, scrolls) = CreateWheel();

        wheel.Handle(controlKey: false, shiftKey: true, deltaX: 0.5, deltaY: 2, new EsPoint(30, 40));

        var scroll = Assert.Single(scrolls);
        Assert.Equal(new WheelScrollRequest(0.5, 2, true, new EsPoint(30, 40)), scroll);
        Assert.Equal(new EsVector(0.5, 2), scroll.Delta);
        Assert.Empty(zooms);
    }

    [Fact]
    public void Handle_回転量が0でもスクロール要求を通知する()
    {
        var (wheel, _, scrolls) = CreateWheel();

        // 回転量 0（サブピクセルの端数など）でも通知し、単位の解釈は受け手へ委ねる
        wheel.Handle(controlKey: false, shiftKey: false, deltaX: 0, deltaY: 0, default);

        Assert.Single(scrolls);
    }

    [Fact]
    public void Handle_発生位置をそのまま通知する()
    {
        var (wheel, zooms, scrolls) = CreateWheel();

        // ズーム要求はズーム中心（カーソル位置）として使えるよう発生位置を保持する
        wheel.Handle(controlKey: true, shiftKey: false, deltaX: 0, deltaY: 1, new EsPoint(11, 22));
        Assert.Equal(new EsPoint(11, 22), Assert.Single(zooms).Position);

        wheel.Handle(controlKey: false, shiftKey: false, deltaX: 0, deltaY: 1, new EsPoint(33, 44));
        Assert.Equal(new EsPoint(33, 44), Assert.Single(scrolls).Position);
    }

    [Fact]
    public void Handle_連携先がある場合は自らスクロールを適用し通知しない()
    {
        var (wheel, zooms, scrolls) = CreateWheel();
        var target = new ScrollTargetSpy();
        wheel.AttachScrollTarget(target);

        wheel.Handle(controlKey: false, shiftKey: false, deltaX: 0, deltaY: 1, new EsPoint(30, 40));

        // 移動量はコアの規則（1 ノッチ = 48 DIP）で換算される
        Assert.Equal(new EsVector(0, 48), Assert.Single(target.Deltas));
        Assert.Empty(scrolls);
        Assert.Empty(zooms);
    }

    [Fact]
    public void Handle_連携先がある場合はShift併用で横スクロールになる()
    {
        var (wheel, _, _) = CreateWheel();
        var target = new ScrollTargetSpy();
        wheel.AttachScrollTarget(target);

        wheel.Handle(controlKey: false, shiftKey: true, deltaX: 0, deltaY: 2, new EsPoint(30, 40));

        // Shift 併用時は縦の回転量を横の移動量として扱う
        Assert.Equal(new EsVector(96, 0), Assert.Single(target.Deltas));
    }

    [Fact]
    public void Handle_連携先があってもCtrl併用はズーム要求になる()
    {
        var (wheel, zooms, _) = CreateWheel();
        var target = new ScrollTargetSpy();
        wheel.AttachScrollTarget(target);

        wheel.Handle(controlKey: true, shiftKey: false, deltaX: 0, deltaY: 1, new EsPoint(30, 40));

        Assert.Single(zooms);
        Assert.Empty(target.Deltas);
    }

    [Fact]
    public void AttachScrollTarget_nullで解除するとスクロール要求を通知する()
    {
        var (wheel, _, scrolls) = CreateWheel();
        var target = new ScrollTargetSpy();
        wheel.AttachScrollTarget(target);

        wheel.AttachScrollTarget(null);
        wheel.Handle(controlKey: false, shiftKey: false, deltaX: 0, deltaY: 1, default);

        Assert.Single(scrolls);
        Assert.Empty(target.Deltas);
    }

    [Fact]
    public void Handle_ズーム要求をログへ記録する()
    {
        var logger = new RecordingLogger();
        var (wheel, _, _) = CreateWheel(logger);

        wheel.Handle(controlKey: true, shiftKey: false, deltaX: 0, deltaY: 1, default);

        Assert.Equal("[Marquee] ZoomRequested: zoomIn=True", Assert.Single(logger.Messages));
    }

    [Fact]
    public void Handle_スクロール要求はログへ記録しない()
    {
        var logger = new RecordingLogger();
        var (wheel, _, _) = CreateWheel(logger);

        // スクロールの実処理は受け手（または連携先）が担うため、本層は記録しない
        wheel.Handle(controlKey: false, shiftKey: false, deltaX: 0, deltaY: 1, default);

        Assert.Empty(logger.Messages);
    }

    [Fact]
    public void Handle_要求が連続してもその都度通知する()
    {
        var (wheel, zooms, scrolls) = CreateWheel();

        wheel.Handle(controlKey: true, shiftKey: false, deltaX: 0, deltaY: 1, default);
        wheel.Handle(controlKey: false, shiftKey: false, deltaX: 0, deltaY: 1, default);

        Assert.Single(zooms);
        Assert.Single(scrolls);
    }

    [Fact]
    public void Handle_購読者も連携先も無い場合は何も起きない()
    {
        var marquee = new Marquee(new EffectsSpy());
        var wheel = new MarqueeWheelInterpreter(marquee);

        // 例外なく完了する（通知は null 条件付き）
        wheel.Handle(controlKey: true, shiftKey: false, deltaX: 0, deltaY: 1, default);
        wheel.Handle(controlKey: false, shiftKey: false, deltaX: 0, deltaY: 1, default);
    }

    [Fact]
    public void Handle_ロガー未設定でも安全に動作する()
    {
        var marquee = new Marquee(new EffectsSpy());
        var wheel = new MarqueeWheelInterpreter(marquee);

        // ロガーはコアのファサードが NullLogger へ吸収する
        wheel.Handle(controlKey: true, shiftKey: false, deltaX: 0, deltaY: 1, default);
    }
}
