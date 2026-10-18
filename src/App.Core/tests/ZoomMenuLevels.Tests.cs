using EsUtil.SaveEvidence.App.Core.Services;
using Xunit;

namespace EsUtil.SaveEvidence.App.Core.Tests;

/// <summary><see cref="ZoomMenuLevels"/> の仕様テスト（4 つの UI 版で共通の拡大率の選択肢）。</summary>
/// <remarks>
/// 選択肢と並びをコアが単一所有するため、値の範囲と昇順を本テストで固定する<br/>
/// （UI 版ごとに選択肢が変わる事故の防止）。 UI 初期化を必要としないため並列実行できる。
/// </remarks>
public class ZoomMenuLevelsTests
{
    /// <summary>選択肢が空でなく、昇順であることを検証する。</summary>
    [Fact]
    internal void All_は空でなく昇順()
    {
        Assert.NotEmpty(ZoomMenuLevels.All);
        for (var i = 1; i < ZoomMenuLevels.All.Count; i++)
        {
            Assert.True(
                ZoomMenuLevels.All[i - 1] < ZoomMenuLevels.All[i],
                $"{i - 1} 番目の値が {i} 番目以上です。");
        }
    }

    /// <summary>選択肢が拡大率として妥当な範囲（10〜500%）に収まることを検証する。</summary>
    [Fact]
    internal void All_は拡大率の範囲に収まる()
    {
        Assert.All(ZoomMenuLevels.All, static level => Assert.InRange(level, 10, 500));
    }
}