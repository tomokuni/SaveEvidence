using EsUtil.Feature.Loupe.Core.Setting;
using Xunit;
using DrawingColor = System.Drawing.Color;
using EsUtil.Feature.Loupe.Presenter.Setting;

namespace EsUtil.Feature.Loupe.Presenter.Tests;

/// <summary><see cref="LoupePresenter"/> の単体テスト（外観の保持と変更の記録）。</summary>
/// <remarks>
/// 提示層が単一所有するもの（外観・描画計画）のうち、外観の保持と変更ログを検証する<br/>
/// （描画計画の解決は <see cref="LoupeDrawPlan.Tests"/> が担う）。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public class LoupePresenterTests
{
    /// <summary>外観が既定で設定され、差し替えが保持されることを検証する。</summary>
    [Fact]
    public void 外観_既定はDefaultで差し替えが保持される()
    {
        var presenter = new LoupePresenter();
        Assert.Equal(LoupeAppearance.Default, presenter.Appearance);

        var appearance = LoupeAppearance.Default with { ShowCrosshair = false, CrossColor = DrawingColor.Red };
        presenter.Appearance = appearance;

        Assert.Equal(appearance, presenter.Appearance);
    }

    /// <summary>外観の変更が変化した項目として記録されることを検証する。</summary>
    /// <remarks>
    /// 「値を所有する主体が記録する」規則により、提示層が保持する外観の変更は提示層が記録する<br/>
    /// （ホストは前後の値を渡さず、同値判定・差分検出を持たない）。
    /// </remarks>
    [Fact]
    public void 外観_変更した項目だけを記録する()
    {
        var presenter = new LoupePresenter();
        var logger = new RecordingLogger();
        presenter.Log = logger;

        presenter.Appearance = LoupeAppearance.Default with { ShowCrosshair = false };

        Assert.Equal("[Feature.Loupe.Presenter] Setting: ShowCrosshair True → False", Assert.Single(logger.Messages));
    }

    /// <summary>同値の再設定では記録しないことを検証する。</summary>
    [Fact]
    public void 外観_同値の再設定では記録しない()
    {
        var presenter = new LoupePresenter();
        var logger = new RecordingLogger();
        presenter.Log = logger;

        presenter.Appearance = LoupeAppearance.Default with { };

        Assert.Empty(logger.Messages);
    }

    /// <summary>設定（<see cref="LoupeOptions"/>）の変更がコアのログへ流れることを検証する。</summary>
    /// <remarks>
    /// 設定を所有するのはコアであり、記録もコアが行う（提示層は委譲するだけである）。<br/>
    /// ログの出力先は提示層の <c>Log</c> からコアへ伝搬する。
    /// </remarks>
    [Fact]
    public void 設定_コアのログ定義で記録する()
    {
        var presenter = new LoupePresenter();
        var logger = new RecordingLogger();
        presenter.Log = logger;

        presenter.Options = LoupeOptions.Default with { ZoomLevel = 8 };

        Assert.Equal("[Feature.Loupe.Core] Setting: ZoomLevel 4 → 8", Assert.Single(logger.Messages));
    }
}
