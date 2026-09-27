using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using EsUtil.Feature.Marquee.Core.Contract;
using EsUtil.Feature.Marquee.Core.Setting;

namespace EsUtil.Feature.Marquee.Presenter.Tests;

/// <summary><see cref="MarqueePresenter"/> の単体テスト（外観の保持・描画計画の解決・コアへの委譲）。</summary>
/// <remarks>
/// 提示層が単一所有するもの（外観・装飾の矩形・ラベルの文言と配置・層の順序）を、UI フレームワークなしで検証する。<br/>
/// 描画の実行は各 UI の描画クラスのテストが画素で検証するため、本テストは「計画の値」に焦点を当てる。
/// </remarks>
public class MarqueePresenterTests
{
    /// <summary>提示層と効果のスパイを生成する。</summary>
    /// <param name="effects">生成した効果のスパイ。</param>
    /// <param name="options">初期設定。null の場合は既定値。</param>
    /// <param name="appearance">初期の外観。null の場合は既定値。</param>
    /// <returns>提示層。</returns>
    private static MarqueePresenter CreatePresenter(
        out EffectsSpy effects, MarqueeOptions? options = null, MarqueeAppearance? appearance = null)
    {
        effects = new EffectsSpy();
        var presenter = new MarqueePresenter(effects, cursor: null, options) { Log = NullLogger.Instance };

        if (appearance is not null)
        {
            presenter.Appearance = appearance;
        }

        return presenter;
    }

    /// <summary>テキストの実寸を測る関数（描画 API 依存のためテストでは固定値を返す）。</summary>
    /// <param name="text">測定する文言。</param>
    /// <param name="fontSize">フォントサイズ。</param>
    /// <returns>固定の実寸。</returns>
    private static EsSize MeasureText(string text, double fontSize) => new(40.0, 14.0);

    // ====================================================================
    // 外観と環境値の保持
    // ====================================================================

    /// <summary>表示スケーリングが 0 以下の場合は倍率と同値になることを検証する。</summary>
    /// <param name="displayScale">指定する表示スケーリング。</param>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    public void DisplayScaleScaling_0以下は倍率と同値になる(double displayScale)
    {
        var presenter = CreatePresenter(out _);
        presenter.DomainToDipScaling = 1.25;

        presenter.DisplayScaleScaling = displayScale;

        Assert.Equal(1.25, presenter.DisplayScaleScaling);
    }

    /// <summary>正の表示スケーリングがそのまま保持されることを検証する。</summary>
    [Fact]
    public void DisplayScaleScaling_正の値はそのまま保持する()
    {
        var presenter = CreatePresenter(out _);
        presenter.DomainToDipScaling = 1.25;

        presenter.DisplayScaleScaling = 2.0;

        Assert.Equal(2.0, presenter.DisplayScaleScaling);
    }

    /// <summary>外観が既定で設定され、差し替えが保持されることを検証する。</summary>
    [Fact]
    public void 外観_既定はDefaultで差し替えが保持される()
    {
        var presenter = CreatePresenter(out _);
        Assert.Equal(MarqueeAppearance.Default, presenter.Appearance);

        var appearance = MarqueeAppearance.Default with { ShowHandles = false, OverlayColor = System.Drawing.Color.Red };
        presenter.Appearance = appearance;

        Assert.Equal(appearance, presenter.Appearance);
    }

    /// <summary>ハイライト矩形がコアへ委譲され、穴あけ矩形として優先されることを検証する。</summary>
    [Fact]
    public void HighlightRect_コアへ委譲しハイライトを優先する()
    {
        var presenter = CreatePresenter(out _);
        presenter.SetSelection(new EsRect(10, 20, 30, 40));
        presenter.HighlightRect = new EsRect(1, 2, 3, 4);

        var plan = presenter.ResolveDrawPlan(new EsSize(200, 100), MeasureText);

        // ハイライト矩形が穴あけ矩形になる（優先順はコアが単一所有する）
        Assert.Equal(new EsRect(1, 2, 3, 4), plan.HoleRect);
    }

    // ====================================================================
    // 描画計画（1 フレーム分）
    // ====================================================================

    /// <summary>選択もハイライトも無ければハンドルもラベルも返さないことを検証する。</summary>
    [Fact]
    public void ResolveDrawPlan_選択なしではハンドルとラベルを返さない()
    {
        // 座標ラベルとサイズラベルは既定で表示されるため、明示的に無効化して「選択なしの結果」を見る
        var presenter = CreatePresenter(
            out _,
            appearance: MarqueeAppearance.Default with { ShowCoordLabel = false });

        var plan = presenter.ResolveDrawPlan(new EsSize(200, 100), MeasureText);

        Assert.Null(plan.Handles);
        Assert.Null(plan.Marker);
        Assert.Empty(plan.Labels);
        Assert.False(plan.HasText);
    }

    /// <summary>選択がある場合はハンドルの幾何を返すことを検証する。</summary>
    [Fact]
    public void ResolveDrawPlan_選択ありではハンドルを返す()
    {
        var presenter = CreatePresenter(out _);
        presenter.SetSelection(new EsRect(10, 20, 100, 50));

        var plan = presenter.ResolveDrawPlan(new EsSize(200, 100), MeasureText);

        // ハンドル矩形は選択領域の閉区間（終点 +1）の左上を中心に置く（幾何はコアが単一所有する）
        Assert.NotNull(plan.Handles);
        Assert.Equal(8, plan.Handles!.Value.Rects.Count);
        Assert.Equal(new EsRect(5, 15, 10, 10), plan.Handles!.Value.Rects[0]);
    }

    /// <summary>ハンドルを隠す外観ではハンドルが null になることを検証する。</summary>
    [Fact]
    public void ResolveDrawPlan_ハンドルを隠す外観ではnullになる()
    {
        var presenter = CreatePresenter(out _, appearance: MarqueeAppearance.Default with { ShowHandles = false });
        presenter.SetSelection(new EsRect(10, 20, 100, 50));

        var plan = presenter.ResolveDrawPlan(new EsSize(200, 100), MeasureText);

        Assert.Null(plan.Handles);
    }

    /// <summary>カーソル座標ラベルの文言がマウス位置から解決されることを検証する。</summary>
    [Fact]
    public void ResolveDrawPlan_座標ラベルはマウス位置から解決する()
    {
        var presenter = CreatePresenter(out _);
        presenter.Move(new EsPoint(30, 40));

        var plan = presenter.ResolveDrawPlan(new EsSize(200, 100), MeasureText);

        // 選択が無いためサイズラベルは無く、座標ラベルのみが作られる
        var label = Assert.Single(plan.Labels);
        Assert.Equal("(30, 40)", label.Text);
        Assert.True(plan.HasText);
    }

    /// <summary>座標変換ポートがハンドルの幾何へ適用されることを検証する。</summary>
    [Fact]
    public void ResolveDrawPlan_座標変換ポートを幾何へ適用する()
    {
        var presenter = CreatePresenter(out _);
        // Inverse（ドメイン → DIP）は 2 倍
        presenter.SetCoordinateTransform(new CoordTransform(
            p => new EsPoint(p.X / 2, p.Y / 2), p => new EsPoint(p.X * 2, p.Y * 2), () => null));
        presenter.SetSelection(new EsRect(10, 20, 100, 50));

        // 描画領域のサイズは使わない（座標変換ポートだけが結果に影響する）ため既定値で解決する
        var plan = presenter.ResolveDrawPlan(default, MeasureText);

        // 選択領域は閉区間（終点 +1）で変換されるため、左上ハンドルは (15, 35) から始まる
        Assert.Equal(new EsRect(15, 35, 10, 10), plan.Handles!.Value.Rects[0]);
    }

    /// <summary>描画領域のサイズが座標ラベルのクランプへ渡されることを検証する。</summary>
    [Fact]
    public void ResolveDrawPlan_選択矩形の大きさを座標ラベルのクランプへ渡す()
    {
        var presenter = CreatePresenter(out _);
        presenter.Move(new EsPoint(190, 95));

        var plan = presenter.ResolveDrawPlan(new EsSize(200, 100), MeasureText);

        // ラベルは描画領域の外へ出ない（クランプ規則は EsSize.ClampOffset が単一所有する）
        var label = Assert.Single(plan.Labels);
        Assert.True(label.Bounds.Right <= 200, $"右端が領域外: {label.Bounds.Right}");
        Assert.True(label.Bounds.Bottom <= 100, $"下端が領域外: {label.Bounds.Bottom}");
    }

    /// <summary>サイズラベルと座標ラベルが両方ある場合はサイズラベルが先になることを検証する。</summary>
    [Fact]
    public void ResolveDrawPlan_ラベルはサイズラベルが先になる()
    {
        var presenter = CreatePresenter(out _);
        presenter.SetSelection(new EsRect(10, 20, 100, 50));
        presenter.Move(new EsPoint(30, 40));

        var plan = presenter.ResolveDrawPlan(new EsSize(200, 100), MeasureText);

        Assert.Equal(2, plan.Labels.Count);
        Assert.Contains("(10, 20)", plan.Labels[0].Text);
        Assert.Equal("(30, 40)", plan.Labels[1].Text);
    }

    /// <summary>装飾（暗転・穴あけ枠）が外観の表示可否に従って解決されることを検証する。</summary>
    [Fact]
    public void ResolveDrawPlan_装飾は外観の表示可否に従う()
    {
        var presenter = CreatePresenter(out _, appearance: MarqueeAppearance.Default with { ShowOverlay = false });
        presenter.SetSelection(new EsRect(10, 20, 100, 50));

        var plan = presenter.ResolveDrawPlan(new EsSize(200, 100), MeasureText);

        // 暗転を描かない設定では暗転領域が空になる（他は既定どおり描く）
        Assert.Empty(plan.OverlayRegions);
        Assert.NotEmpty(plan.OuterBorderRects);
        Assert.True(plan.HasDecoration);
    }

    /// <summary>サイズラベルの文言プロバイダが優先されることを検証する。</summary>
    [Fact]
    public void ResolveDrawPlan_サイズラベルの文言プロバイダを優先する()
    {
        var presenter = CreatePresenter(out _);
        presenter.SetSelection(new EsRect(10, 20, 100, 50));
        presenter.SizeLabelOverrideProvider = () => "ホストの文言";

        var plan = presenter.ResolveDrawPlan(new EsSize(200, 100), MeasureText);

        Assert.Equal("ホストの文言", plan.Labels[0].Text);
    }

    /// <summary>測定関数が null の場合は例外になることを検証する。</summary>
    [Fact]
    public void ResolveDrawPlan_測定関数がnullなら例外()
        => Assert.Throws<ArgumentNullException>(
            () => CreatePresenter(out _).ResolveDrawPlan(new EsSize(200, 100), null!));

    // ====================================================================
    // 入力の転送
    // ====================================================================

    /// <summary>入力の転送がコアの状態を変えることを検証する。</summary>
    [Fact]
    public void 入力_操作がコアの状態を変える()
    {
        var presenter = CreatePresenter(out var effects);

        presenter.Press(new EsPoint(10, 10), clickCount: 1);
        presenter.Move(new EsPoint(50, 40));
        presenter.Release(new EsPoint(50, 40));

        Assert.True(presenter.HasSelection);
        Assert.Equal(new EsRect(10, 10, 40, 30), presenter.SelectionRect);
        Assert.True(effects.RedrawCount > 0, "再描画が要求されていない");
    }
}
