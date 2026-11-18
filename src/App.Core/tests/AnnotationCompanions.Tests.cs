using EsUtil.SaveEvidence.App.Core.Models;
using Xunit;

namespace EsUtil.SaveEvidence.App.Core.Tests;

/// <summary><see cref="AnnotationCompanions"/> のテスト（付随ファイルの判定）。</summary>
public class AnnotationCompanionsTests
{
    /// <summary>注釈の文書ファイルを付随ファイルと判定する。</summary>
    [Fact]
    internal void 注釈の文書ファイルは付随ファイルと判定する()
    {
        Assert.True(AnnotationCompanions.IsCompanion(@"C:\shots\shot.annotations.json"));
    }

    /// <summary>合成画像（<c>.annotated.</c>）を付随ファイルと判定する。</summary>
    [Fact]
    internal void 合成画像は付随ファイルと判定する()
    {
        Assert.True(AnnotationCompanions.IsCompanion(@"C:\shots\shot.annotated.png"));
    }

    /// <summary>元の画像は付随ファイルと判定しない。</summary>
    [Fact]
    internal void 元の画像は付随ファイルと判定しない()
    {
        Assert.False(AnnotationCompanions.IsCompanion(@"C:\shots\shot.png"));
        Assert.False(AnnotationCompanions.IsCompanion(@"C:\shots\annotated.png"));
    }
}
