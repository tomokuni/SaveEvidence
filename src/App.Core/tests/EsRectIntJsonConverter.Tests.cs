using System.Text.Json;
using EsUtil.Platform.Core.Geometry;
using EsUtil.SaveEvidence.App.Core.Models;
using Xunit;

namespace EsUtil.SaveEvidence.App.Core.Tests;

/// <summary>設定モデルが <see cref="EsRectIntJsonConverter"/> を登録した状態で往復できることの検証。</summary>
/// <remarks>
/// コンバーター自体の仕様（保存形式）は EsUtil.Platform.Core のテストが検証する。<br/>
/// ここでは<b>実際の利用形態</b>（<c>Settings.MainWindowBounds</c> として往復する）だけを検証する。
/// </remarks>
public class EsRectIntJsonConverterTests
{
    /// <summary>本コンバーターを登録したシリアライズ オプション。</summary>
    private static readonly JsonSerializerOptions s_options = new()
    {
        Converters = { new EsRectIntJsonConverter() },
    };

    [Fact]
    internal void 設定モデル_複合型のプロパティとして往復変換できる()
    {
        // 実際の利用形態（Settings.MainWindowBounds）と同じくプロパティ内で往復する
        var source = new Settings { MainWindowBounds = new EsRectInt(1, 2, 3, 4) };

        var json = JsonSerializer.Serialize(source, s_options);
        var back = JsonSerializer.Deserialize<Settings>(json, s_options);

        Assert.Equal(new EsRectInt(1, 2, 3, 4), back!.MainWindowBounds);
    }

    [Fact]
    internal void 設定モデル_MainWindowBounds未設定はnullのまま往復する()
    {
        var source = new Settings { MainWindowBounds = null };

        var json = JsonSerializer.Serialize(source, s_options);
        var back = JsonSerializer.Deserialize<Settings>(json, s_options);

        Assert.Null(back!.MainWindowBounds);
    }
}