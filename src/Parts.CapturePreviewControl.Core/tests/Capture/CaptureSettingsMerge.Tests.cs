using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using DrawingColor = System.Drawing.Color;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Capture;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.Tests.Capture;

/// <summary><see cref="CaptureSettingsMerge"/> の仕様テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 「null は現在値の維持・非 null は上書き」という部分更新の規則と、<br/>
/// 同一値の再設定のスキップ（参照の差し替えなし）を固定する。
/// </remarks>
public class CaptureSettingsMergeTests
{
    /// <summary>現在値として使う設定（全メンバー non-null）。</summary>
    private static CaptureSettings Current() => new CaptureSettings
    {
        MinSelectionSize = 6,
        BorderThickness = 2.0,
        BorderColor = DrawingColor.FromArgb(0xFF, 169, 169, 169),
    }.ToNonNull();

    [Fact]
    internal void Merge_nullの項目は現在値を維持する()
    {
        var current = Current();

        var merged = CaptureSettingsMerge.Merge(new CaptureSettings(), current);

        Assert.Equal(current, merged);
    }

    [Fact]
    internal void Merge_非nullの項目は上書きする()
    {
        var current = Current();

        var merged = CaptureSettingsMerge.Merge(new CaptureSettings { MinSelectionSize = 12 }, current);

        Assert.Equal(12.0, merged.MinSelectionSize!.Value);
        // 他は現在値を維持する
        Assert.Equal(current.BorderThickness, merged.BorderThickness);
        Assert.Equal(current.BorderColor, merged.BorderColor);
    }

    [Fact]
    internal void Merge_現在値未指定なら既定値へマージする()
    {
        // 現在値が無い場合は ToNonNull の既定値を基点にする
        var expected = new CaptureSettings().ToNonNull();

        var merged = CaptureSettingsMerge.Merge(new CaptureSettings());

        Assert.Equal(expected, merged);
    }

    [Fact]
    internal void Merge_入力がnullなら既定値の設定を返す()
    {
        // 「入力が無い（null）は既定の設定」という規則をコアが単一所有する（UI が ?? を書かない）
        var expected = new CaptureSettings().ToNonNull();

        var merged = CaptureSettingsMerge.Merge(null);

        Assert.Equal(expected, merged);
    }

    [Fact]
    internal void Merge_入力がnullでも現在値があれば現在値を維持する()
    {
        // 部分更新の入力が無い場合は現在値をそのまま使う（既定値へ戻さない）
        var current = Current();
        var expected = CaptureSettingsMerge.Merge(new CaptureSettings(), current);

        var merged = CaptureSettingsMerge.Merge(null, current);

        Assert.Equal(expected, merged);
    }

    [Fact]
    internal void Apply_同一値の再設定はfalseを返し参照を差し替えない()
    {
        var current = Current();
        var before = current;

        // 現在値と同じ値だけを渡す
        var applied = CaptureSettingsMerge.Apply(
            new CaptureSettings { MinSelectionSize = before.MinSelectionSize }, ref current, NullLogger.Instance);

        Assert.False(applied);
        Assert.Same(before, current);
    }

    [Fact]
    internal void Apply_変更があればtrueを返し参照を差し替える()
    {
        var current = Current();
        var before = current;

        var applied = CaptureSettingsMerge.Apply(
            new CaptureSettings { MinSelectionSize = 8 }, ref current, NullLogger.Instance);

        Assert.True(applied);
        Assert.NotSame(before, current);
        Assert.Equal(8.0, current.MinSelectionSize!.Value);
    }

    [Fact]
    internal void Apply_範囲外の値は例外を投げる()
    {
        var current = Current();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CaptureSettingsMerge.Apply(new CaptureSettings { MinSelectionSize = 0 }, ref current, NullLogger.Instance));
    }

    [Fact]
    internal void Apply_ロガー未指定でも適用できる()
    {
        var current = Current();

        var applied = CaptureSettingsMerge.Apply(new CaptureSettings { MinSelectionSize = 9 }, ref current);

        Assert.True(applied);
    }

    [Fact]
    internal void Apply_全項目が現在値と一致する場合は更新しない()
    {
        var current = Current();
        var all = new CaptureSettings { MinSelectionSize = current.MinSelectionSize, BorderThickness = current.BorderThickness, BorderColor = current.BorderColor };

        Assert.False(CaptureSettingsMerge.Apply(all, ref current));
    }
}
