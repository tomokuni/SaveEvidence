using System.Text.Json;
using EsUtil.Platform.Core.Geometry;
using EsUtil.SaveEvidence.App.Core.Models;
using EsUtil.SaveEvidence.App.Core.Services;
using Xunit;

namespace EsUtil.SaveEvidence.App.Core.Tests;

/// <summary><see cref="EsRectIntJsonConverter"/> の単体テスト（設定ファイルの JSON 互換）。</summary>
/// <remarks>
/// JSON の読み書きのみを検証するため、UI 初期化に依存せず <c>[Fact]</c> で並列実行する。<br/>
/// 移設前（<c>Avalonia.PixelRect</c>）と同一の JSON 形式（<c>X</c> / <c>Y</c> / <c>Width</c> / <c>Height</c>）を<br/>
/// 保つことを検証する（既存の設定ファイルをそのまま読み込めることの保証）。
/// </remarks>
public class EsRectIntJsonConverterTests
{
    /// <summary>本コンバーターを登録したシリアライズ オプション。</summary>
    private static readonly JsonSerializerOptions s_options = new()
    {
        Converters = { new EsRectIntJsonConverter() },
    };

    [Fact]
    public void Write_XYWidthHeightのみを書き出す()
    {
        var rect = new EsRectInt(444, 230, 1284, 1032);

        var json = JsonSerializer.Serialize(rect, s_options);

        Assert.Equal("""{"X":444,"Y":230,"Width":1284,"Height":1032}""", json);
    }

    [Fact]
    public void Read_XYWidthHeightを読み取る()
    {
        var rect = JsonSerializer.Deserialize<EsRectInt>(
            """{"X":444,"Y":230,"Width":1284,"Height":1032}""", s_options);

        Assert.Equal(new EsRectInt(444, 230, 1284, 1032), rect);
    }

    [Fact]
    public void 往復変換で値が一致する()
    {
        var rect = new EsRectInt(-100, -50, 1920, 1080);

        var json = JsonSerializer.Serialize(rect, s_options);
        var back = JsonSerializer.Deserialize<EsRectInt>(json, s_options);

        Assert.Equal(rect, back);
    }

    [Fact]
    public void Read_欠落したプロパティは0になる()
    {
        var rect = JsonSerializer.Deserialize<EsRectInt>("""{"X":10,"Width":20}""", s_options);

        Assert.Equal(new EsRectInt(10, 0, 20, 0), rect);
    }

    [Fact]
    public void Read_未知のプロパティは無視する()
    {
        var rect = JsonSerializer.Deserialize<EsRectInt>(
            """{"X":1,"Y":2,"Width":3,"Height":4,"Right":4,"Unknown":99}""", s_options);

        Assert.Equal(new EsRectInt(1, 2, 3, 4), rect);
    }

    [Fact]
    public void Read_順序が異なっても読み取る()
    {
        var rect = JsonSerializer.Deserialize<EsRectInt>(
            """{"Height":40,"Width":30,"Y":20,"X":10}""", s_options);

        Assert.Equal(new EsRectInt(10, 20, 30, 40), rect);
    }

    [Fact]
    public void 設定モデル_複合型のプロパティとして往復変換できる()
    {
        // 実際の利用形態（Settings.MainWindowBounds）と同じくプロパティ内で往復する
        var source = new Settings { MainWindowBounds = new EsRectInt(1, 2, 3, 4) };

        var json = JsonSerializer.Serialize(source, s_options);
        var back = JsonSerializer.Deserialize<Settings>(json, s_options);

        Assert.Equal(new EsRectInt(1, 2, 3, 4), back!.MainWindowBounds);
    }

    [Fact]
    public void 設定モデル_MainWindowBounds未設定はnullのまま往復する()
    {
        var source = new Settings { MainWindowBounds = null };

        var json = JsonSerializer.Serialize(source, s_options);
        var back = JsonSerializer.Deserialize<Settings>(json, s_options);

        Assert.Null(back!.MainWindowBounds);
    }
}
