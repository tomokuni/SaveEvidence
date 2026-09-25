using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EsUtil.Feature.MarqueeCore.Tests;

/// <summary>テストで使用する効果（フォーカス・再描画・カーソル形状・キャプチャ・座標変換）のスパイ。</summary>
/// <remarks>
/// UI 層の代わりに <see cref="IMarqueeEffects"/> を実装し、呼び出しを発生順に記録する。<br/>
/// 座標変換は固定の倍率（<see cref="ScreenScale"/>）で行い、カーソル移動先の計算を検証できるようにする。
/// </remarks>
internal sealed class EffectsSpy : IMarqueeEffects
{
    /// <summary>呼び出しの履歴（種類と値。発生順の検証に使用する）。</summary>
    public List<string> Calls { get; } = [];

    /// <summary>再描画の要求回数。</summary>
    public int RedrawCount => Calls.Count(c => c == "redraw");

    /// <summary>ドメイン座標 → 画面物理座標の変換倍率。</summary>
    public int ScreenScale { get; set; } = 2;

    /// <inheritdoc/>
    public void RequestFocus() => Calls.Add("focus");

    /// <inheritdoc/>
    public void RequestRedraw() => Calls.Add("redraw");

    /// <inheritdoc/>
    public void SetCursorShape(MarqueeCursorKind kind) => Calls.Add($"cursor:{kind}");

    /// <inheritdoc/>
    public void SetPointerCapture(bool capture) => Calls.Add($"capture:{capture}");

    /// <inheritdoc/>
    public EsPixelPoint ToScreenPoint(EsPoint domainPoint)
        => new((int)domainPoint.X * ScreenScale, (int)domainPoint.Y * ScreenScale);
}

/// <summary><see cref="Marquee"/>（公開ファサード）の単体テスト（操作・効果の適用・設定の伝搬・描画内容の解決）。</summary>
/// <remarks>
/// UI 層とホストが使う唯一の窓口であるファサードの振る舞いを、UI フレームワークなしで検証する。<br/>
/// 入力の解釈そのもの（状態遷移の判断）は <see cref="MarqueeSession"/> のテストが検証するため、<br/>
/// 本テストは「効果の適用順」「設定の伝搬」「描画内容の委譲」「通知の転送」に焦点を当てる。
/// </remarks>
public class MarqueeTests
{
    /// <summary>ファサードと効果のスパイを生成する。</summary>
    /// <param name="cursor">カーソルサービス。null 可。</param>
    /// <param name="effects">生成した効果のスパイ。</param>
    /// <param name="options">初期設定。null の場合は既定値。</param>
    /// <returns>ファサード。</returns>
    private static Marquee CreateMarquee(ICursorService? cursor, out EffectsSpy effects, MarqueeOptions? options = null)
    {
        effects = new EffectsSpy();
        return new Marquee(new StubMarqueeRenderer(), effects, cursor, options) { Log = NullLogger.Instance };
    }

    // ====================================================================
    // 生成
    // ====================================================================

    [Fact]
    public void 生成_描画実装がnullなら例外を投げる()
    {
        Assert.Throws<ArgumentNullException>(() => new Marquee(null!, new EffectsSpy()));
    }

    [Fact]
    public void 生成_効果がnullなら例外を投げる()
    {
        Assert.Throws<ArgumentNullException>(() => new Marquee(new StubMarqueeRenderer(), null!));
    }

    [Fact]
    public void 生成_初期設定を保持する()
    {
        var options = MarqueeOptions.Default with { HandleSize = 20.0 };

        var marquee = CreateMarquee(null, out _, options);

        Assert.Same(options, marquee.Options);
    }

    // ====================================================================
    // 描画の差し替え（Strategy）
    // ====================================================================

    [Fact]
    public void 生成_注入した描画実装を保持する()
    {
        var renderer = new StubMarqueeRenderer();

        var marquee = new Marquee(renderer, new EffectsSpy());

        Assert.Same(renderer, marquee.Renderer);
    }

    [Fact]
    public void 描画実装を差し替えると再描画する()
    {
        var effects = new EffectsSpy();
        var marquee = new Marquee(new StubMarqueeRenderer(), effects);
        var before = effects.RedrawCount;

        marquee.Renderer = new StubMarqueeRenderer();

        Assert.True(effects.RedrawCount > before, "描画実装の差し替えで再描画が要求されていない");
    }

    [Fact]
    public void 描画実装が同じなら再描画しない()
    {
        var effects = new EffectsSpy();
        var renderer = new StubMarqueeRenderer();
        var marquee = new Marquee(renderer, effects);
        var before = effects.RedrawCount;

        marquee.Renderer = renderer;

        Assert.Equal(before, effects.RedrawCount);
    }

    [Fact]
    public void 描画実装にnullは設定できない()
    {
        var marquee = new Marquee(new StubMarqueeRenderer(), new EffectsSpy());

        Assert.Throws<ArgumentNullException>(() => marquee.Renderer = null!);
    }

    [Fact]
    public void ResolveLabels_は設定された描画実装へ委譲する()
    {
        var marquee = new Marquee(new StubMarqueeRenderer(), new EffectsSpy());
        var replacement = new StubMarqueeRenderer { Elements = [] };
        marquee.Renderer = replacement;
        var content = marquee.ResolveContent(new EsSize(120, 80));

        var elements = marquee.ResolveLabels(content, static (_, _) => new EsSize(10, 10));

        Assert.Empty(elements);
        Assert.Equal(1, replacement.ResolveLabelsCalls);
    }

    /// <summary>効果を何もしない実装（確保量の計測に使う）。</summary>
    private sealed class NoOpEffects : MarqueeEffectsBase
    {
    }

    /// <summary>ポインター移動の処理がヒープを確保しないことを検証する。</summary>
    /// <remarks>
    /// ポインター移動は操作中に連続して発生するため、通知・スナップショットを値型にして確保を避ける<br/>
    /// （デリゲートの生成も行わない）。計測は <see cref="GC.GetAllocatedBytesForCurrentThread"/> で行う。
    /// </remarks>
    [Fact]
    public void Move_ポインター移動はヒープを確保しない()
    {
        var marquee = new Marquee(new StubMarqueeRenderer(), new NoOpEffects());
        marquee.SetSelection(new EsRect(10, 10, 100, 80));
        marquee.Press(new EsPoint(50, 50), 1);
        marquee.Move(new EsPoint(51, 51));

        for (var i = 0; i < 100; i++)
        {
            marquee.Move(new EsPoint(60 + i, 60));
        }

        GC.Collect();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            marquee.Move(new EsPoint(60 + i, 60));
        }

        var perCall = (GC.GetAllocatedBytesForCurrentThread() - before) / 1000.0;
        Assert.True(perCall <= 16, $"ポインター移動 1 回の確保が {perCall} バイト（16 バイト以下を期待）");
    }

    [Fact]
    public void 生成_初期状態は未選択()
    {
        var marquee = CreateMarquee(null, out _);

        Assert.False(marquee.HasSelection);
        Assert.Equal(default, marquee.SelectionRect);
        Assert.Equal(HitTarget.None, marquee.CreateSnapshot().ActiveTarget);
    }

    // ====================================================================
    // 効果の適用（入力 → 効果）
    // ====================================================================

    [Fact]
    public void Press_フォーカスと再描画とキャプチャを要求する()
    {
        var marquee = CreateMarquee(null, out var effects);

        marquee.Press(new EsPoint(10, 10), clickCount: 1);

        // 適用順は「フォーカス → カーソル形状 → 再描画」で、キャプチャは最後に取得する
        Assert.Equal(["focus", "redraw", "capture:True"], effects.Calls);
    }

    [Fact]
    public void Release_ポインターキャプチャを解放する()
    {
        var marquee = CreateMarquee(null, out var effects);
        marquee.Press(new EsPoint(10, 10), clickCount: 1);
        effects.Calls.Clear();

        marquee.Release(new EsPoint(50, 40));

        Assert.Contains("capture:False", effects.Calls);
        Assert.Equal("capture:False", effects.Calls[^1]);
    }

    [Fact]
    public void Press_カーソルをハンドル点へ移動する()
    {
        var cursor = new CursorServiceSpy();
        var marquee = CreateMarquee(cursor, out _, MarqueeOptions.Default with { HandleHitRadius = 12.0 });
        marquee.SetSelection(new EsRect(50, 50, 100, 80));

        // 右下ハンドル（150, 130）付近を押下する → ハンドル点へスナップしてカーソルを移動する
        marquee.Press(new EsPoint(148, 128), clickCount: 1);

        // 画面物理座標は効果の座標変換（2 倍）で求まる
        Assert.Equal(new EsPixelPoint(300, 260), cursor.CursorPoint);
    }

    [Fact]
    public void Move_カーソル形状を更新する()
    {
        var marquee = CreateMarquee(null, out var effects);
        marquee.SetSelection(new EsRect(50, 50, 100, 80));
        effects.Calls.Clear();

        marquee.Move(new EsPoint(150, 90));

        // 右辺中央（MiddleRight）は左右リサイズの形状になる（種別の判断はコアが単一所有する）
        Assert.Contains("cursor:Horizontal", effects.Calls);
    }

    [Fact]
    public void Press_カーソル未指定でも効果を適用する()
    {
        var marquee = CreateMarquee(null, out var effects);
        marquee.SetSelection(new EsRect(50, 50, 100, 80));
        effects.Calls.Clear();

        // カーソルサービス未指定（null）でも例外にならず、カーソル移動以外の効果は適用される
        marquee.Press(new EsPoint(148, 128), clickCount: 1);

        Assert.Equal(["focus", "redraw", "capture:True"], effects.Calls);
    }

    // ====================================================================
    // キー入力
    // ====================================================================

    [Fact]
    public void KeyDown_Enterは確定を通知し処理済みを返す()
    {
        var marquee = CreateMarquee(null, out _);
        var notices = new List<MarqueeNotice>();
        marquee.Notified += (_, notice) => notices.Add(notice);
        marquee.SetSelection(new EsRect(10, 20, 100, 50));
        notices.Clear();

        var handled = marquee.KeyDown(EsKey.Enter, null);

        Assert.True(handled);
        Assert.Equal([MarqueeNoticeKind.ConfirmRequested], notices.Select(n => n.Kind));
        Assert.False(marquee.HasSelection);
    }

    [Fact]
    public void KeyDown_対象外のキーは処理しない()
    {
        var marquee = CreateMarquee(null, out var effects);

        var handled = marquee.KeyDown(EsKey.A, null);

        Assert.False(handled);
        Assert.Empty(effects.Calls);
    }

    [Fact]
    public void KeyDown_矢印キーはnudgeを開始し始点を移動する()
    {
        var marquee = CreateMarquee(null, out var effects);
        var notices = new List<MarqueeNotice>();
        marquee.Notified += (_, notice) => notices.Add(notice);

        var handled = marquee.KeyDown(EsKey.Right, new EsPoint(100, 100));

        // 未選択の nudge は始点を 1 ドメインピクセル移動する（カーソルの非表示管理もコアが単一所有する）
        Assert.True(handled);
        Assert.Equal(["redraw"], effects.Calls);
        Assert.Equal(
            [MarqueeNoticeKind.SelectionChanged, MarqueeNoticeKind.MarkerPositionMoved],
            notices.Select(n => n.Kind));
        Assert.Equal(new EsPoint(101, 100), notices[^1].Position);
        Assert.False(marquee.HasSelection);
    }

    // ====================================================================
    // 設定の伝搬
    // ====================================================================

    [Fact]
    public void Options_境界外選択の禁止を既存の選択矩形へ即時適用する()
    {
        var marquee = CreateMarquee(null, out _, MarqueeOptions.Default with { ClampToDomainBounds = false });
        marquee.SetCoordinateTransform(new CoordTransform(p => p, p => p, () => new EsSize(100, 100)));
        marquee.SetSelection(new EsRect(50, 50, 200, 200));

        // 設定を ON へ切り替えると既存の選択矩形が境界内へクリップされる
        marquee.Options = marquee.Options with { ClampToDomainBounds = true };

        Assert.Equal(new EsRect(50, 50, 50, 50), marquee.SelectionRect);
    }

    [Fact]
    public void Options_同値の再設定では再描画を要求しない()
    {
        var marquee = CreateMarquee(null, out var effects);
        effects.Calls.Clear();

        marquee.Options = marquee.Options;

        Assert.Empty(effects.Calls);
    }

    [Fact]
    public void Scaling_変更を保持し変更時のみ再描画する()
    {
        var marquee = CreateMarquee(null, out var effects);

        marquee.Scaling = 2.0;
        marquee.Scaling = 2.0;

        Assert.Equal(2.0, marquee.Scaling);
        Assert.Equal(1, effects.RedrawCount);
    }

    // ====================================================================
    // 環境値（座標変換ポート）
    // ====================================================================

    [Fact]
    public void SetCoordinateTransform_未設定は等価変換になる()
    {
        var marquee = CreateMarquee(null, out _);

        marquee.SetCoordinateTransform(null);

        Assert.Equal(new EsPoint(3, 4), marquee.ToDomain(new EsPoint(3, 4)));
        Assert.Equal(new EsPoint(3, 4), marquee.ToDip(new EsPoint(3, 4)));
    }

    [Fact]
    public void SetCoordinateTransform_設定したポートで変換する()
    {
        var marquee = CreateMarquee(null, out var effects);
        effects.Calls.Clear();

        // Forward（DIP → ドメイン）は 1/2 倍、Inverse（ドメイン → DIP）は 2 倍
        marquee.SetCoordinateTransform(new CoordTransform(
            p => new EsPoint(p.X / 2, p.Y / 2), p => new EsPoint(p.X * 2, p.Y * 2), () => null));

        Assert.Equal(new EsPoint(5, 10), marquee.ToDomain(new EsPoint(10, 20)));
        Assert.Equal(new EsPoint(20, 40), marquee.ToDip(new EsPoint(10, 20)));
        Assert.Contains("redraw", effects.Calls);
    }

    // ====================================================================
    // 描画内容（1 フレーム分）
    // ====================================================================

    [Fact]
    public void ResolveContent_選択なしではハンドルとラベルを返さない()
    {
        var marquee = CreateMarquee(null, out _);

        var content = marquee.ResolveContent(new EsSize(200, 100));

        Assert.Null(content.Handles);
        Assert.Null(content.Marker);
        Assert.Null(content.CoordLabel);
        Assert.False(content.HasText);
    }

    [Fact]
    public void ResolveContent_選択ありではハンドルを返す()
    {
        var marquee = CreateMarquee(null, out _);
        marquee.SetSelection(new EsRect(10, 20, 100, 50));

        var content = marquee.ResolveContent(new EsSize(200, 100));

        // ハンドル矩形は選択領域の閉区間（終点 +1）で求める（描画層は結果を使うだけ）
        Assert.NotNull(content.Handles);
        Assert.Equal(new EsRect(10, 20, 101, 51), content.Handles!.Value.Rect);
    }

    [Fact]
    public void ResolveContent_設定でハンドルを隠すとnullになる()
    {
        var marquee = CreateMarquee(null, out _, MarqueeOptions.Default with { ShowHandles = false });
        marquee.SetSelection(new EsRect(10, 20, 100, 50));

        var content = marquee.ResolveContent(new EsSize(200, 100));

        Assert.Null(content.Handles);
    }

    [Fact]
    public void ResolveContent_座標ラベルは設定とマウス位置から解決する()
    {
        var marquee = CreateMarquee(null, out _, MarqueeOptions.Default with { ShowCoordLabel = true });
        marquee.Move(new EsPoint(30, 40));

        var content = marquee.ResolveContent(new EsSize(200, 100));

        Assert.NotNull(content.CoordLabel);
        Assert.Equal("(30, 40)", content.CoordLabel!.Value.Text);
        Assert.True(content.HasText);
    }

    [Fact]
    public void ResolveContent_座標変換ポートを描画内容へ適用する()
    {
        var marquee = CreateMarquee(null, out _);
        // Inverse（ドメイン → DIP）は 2 倍
        marquee.SetCoordinateTransform(new CoordTransform(
            p => new EsPoint(p.X / 2, p.Y / 2), p => new EsPoint(p.X * 2, p.Y * 2), () => null));
        marquee.SetSelection(new EsRect(10, 20, 100, 50));

        var content = marquee.ResolveContent(default);

        // 選択領域は閉区間（終点 +1）で変換されるため、幅・高さは 2 倍 + 2 になる
        Assert.Equal(new EsRect(20, 40, 202, 102), content.Handles!.Value.Rect);
    }

    [Fact]
    public void ResolveContent_選択矩形の大きさを座標ラベルのクランプへ渡す()
    {
        var marquee = CreateMarquee(null, out _, MarqueeOptions.Default with { ShowCoordLabel = true });
        marquee.Move(new EsPoint(30, 40));

        var content = marquee.ResolveContent(new EsSize(200, 100));

        // 描画領域のサイズは座標ラベルの位置クランプに使う（領域外へ出さない）
        Assert.Equal(new EsSize(200, 100), content.CoordLabel!.Value.ClientSize);
    }

    // ====================================================================
    // 通知の転送
    // ====================================================================

    [Fact]
    public void Notified_送信元はファサード自身になる()
    {
        var marquee = CreateMarquee(null, out _);
        object? sender = null;
        var notices = new List<MarqueeNotice>();
        marquee.Notified += (s, notice) => { sender = s; notices.Add(notice); };

        marquee.Press(new EsPoint(10, 10), clickCount: 1);

        Assert.Same(marquee, sender);
        Assert.Equal([MarqueeNoticeKind.SelectionStarted], notices.Select(n => n.Kind));
    }

    [Fact]
    public void Notified_購読者がいない場合は何も起きない()
    {
        var marquee = CreateMarquee(null, out _);
        marquee.SetSelection(new EsRect(10, 20, 100, 50));

        // 例外なく完了する（通知の発火は null 条件付き）
        marquee.RequestCancel();

        Assert.False(marquee.HasSelection);
    }

    // ====================================================================
    // 外部からの操作
    // ====================================================================

    [Fact]
    public void SetSelection_選択を設定し通知する()
    {
        var marquee = CreateMarquee(null, out _);
        var notices = new List<MarqueeNotice>();
        marquee.Notified += (_, notice) => notices.Add(notice);

        marquee.SetSelection(new EsRect(10, 20, 100, 50));

        Assert.True(marquee.HasSelection);
        Assert.Equal([MarqueeNoticeKind.SelectionChanged], notices.Select(n => n.Kind));
    }

    [Fact]
    public void ResetSelection_選択とnudgeを初期化する()
    {
        var marquee = CreateMarquee(null, out _);
        marquee.SetSelection(new EsRect(10, 20, 100, 50));

        marquee.ResetSelection();

        Assert.False(marquee.HasSelection);
    }

    [Fact]
    public void RequestConfirm_選択確定を通知する()
    {
        var marquee = CreateMarquee(null, out _);
        var notices = new List<MarqueeNotice>();
        marquee.Notified += (_, notice) => notices.Add(notice);
        marquee.SetSelection(new EsRect(10, 20, 100, 50));
        notices.Clear();

        marquee.RequestConfirm();

        Assert.Equal([MarqueeNoticeKind.ConfirmRequested], notices.Select(n => n.Kind));
        Assert.False(marquee.HasSelection);
    }

    [Fact]
    public void RequestCancel_選択を破棄して通知する()
    {
        var marquee = CreateMarquee(null, out _);
        var notices = new List<MarqueeNotice>();
        marquee.Notified += (_, notice) => notices.Add(notice);
        marquee.SetSelection(new EsRect(10, 20, 100, 50));
        notices.Clear();

        marquee.RequestCancel();

        Assert.Equal([MarqueeNoticeKind.CancelRequested], notices.Select(n => n.Kind));
        Assert.False(marquee.HasSelection);
    }

    [Fact]

    public void EndNudge_nudge中でなければ何もしない()
    {
        var marquee = CreateMarquee(null, out var effects);

        marquee.EndNudge();

        Assert.Empty(effects.Calls);
    }

    [Fact]
    public void EndNudge_nudge中はカーソルを再表示する()
    {
        var cursor = new CursorServiceSpy();
        var marquee = CreateMarquee(cursor, out _);
        marquee.KeyDown(EsKey.Right, new EsPoint(100, 100));

        marquee.EndNudge();

        // nudge の開始で非表示にし、終了で再表示する（最初の呼び出しが非表示、最後が再表示）
        Assert.False(cursor.ShowCalls[0]);
        Assert.True(cursor.ShowCalls[^1]);
    }

    [Fact]
    public void Log_設定したロガーを保持する()
    {
        var marquee = CreateMarquee(null, out _);
        var logger = new RecordingLogger();

        marquee.Log = logger;

        Assert.Same(logger, marquee.Log);
    }

    [Fact]
    public void Log_nullを設定するとNullLoggerへフォールバックする()
    {
        var marquee = CreateMarquee(null, out _);

        marquee.Log = null!;

        Assert.Same(NullLogger.Instance, marquee.Log);
    }
}
