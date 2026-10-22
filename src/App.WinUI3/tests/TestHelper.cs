using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.App.Core.Models;
using EsUtil.SaveEvidence.App.Core.Services;
using Microsoft.Extensions.Logging;

namespace EsUtil.SaveEvidence.App.WinUI3.Tests;

/// <summary>テストで共用するユーティリティ。</summary>
internal static class TestHelper
{
    /// <summary>指定サイズの画像を生成する（内容は単色）。</summary>
    /// <param name="width">幅（1 以上）。</param>
    /// <param name="height">高さ（1 以上）。</param>
    /// <returns>生成した画像（読み取りの面）。 所有権は呼び出し側。</returns>
    public static EsReadOnlyBitmap CreateReadOnlyBitmap(int width = 4, int height = 4)
    {
        using var bitmap = EsBitmap.Create(width, height);
        bitmap.SetReadOnly();
        return EsReadOnlyBitmap.FromBitmap(bitmap);
    }

    /// <summary>画像ファイルを書き出す。</summary>
    /// <param name="filePath">書き出し先のパス（拡張子で形式が決まる）。</param>
    /// <param name="width">幅（1 以上）。</param>
    /// <param name="height">高さ（1 以上）。</param>
    /// <returns>書き出したファイルのパス。</returns>
    public static string WriteImage(string filePath, int width = 64, int height = 32)
    {
        using var image = CreateReadOnlyBitmap(width, height);
        new EsBitmapImageWriter().Save(image, filePath);
        return filePath;
    }

    /// <summary>一時ファイルのパスを作る（ファイルは作成しない）。</summary>
    /// <param name="extension">拡張子（例: ".png"）。</param>
    /// <returns>一時ディレクトリ配下の一意なパス。</returns>
    public static string CreateTempPath(string extension)
        => Path.Combine(Path.GetTempPath(), $"saveevidence-winui3-test-{Guid.NewGuid():N}{extension}");
}

/// <summary>ログ出力を収集するテスト用 <see cref="ILogger"/> 実装。</summary>
/// <remarks>
/// LoggerMessage ソース生成コードの state 走査（Count・インデクサ・列挙・範囲外アクセス）を<br/>
/// 実行させつつ、整形済みのメッセージを収集して内容を検証できるようにする。
/// </remarks>
internal sealed class RecordingLogger : ILogger
{
    /// <summary>収集したログメッセージ。</summary>
    public List<string> Messages { get; } = [];

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        // LoggerMessage 生成コードが state を走査する経路を実行する（Count・インデクサ・列挙・範囲外）
        if (state is IReadOnlyList<KeyValuePair<string, object?>> list)
        {
            _ = list.Count;
            for (var i = 0; i < list.Count; i++)
            {
                _ = list[i];
            }

            try
            {
                // 範囲外アクセスは例外になる（生成コードの防御経路の確認）
                _ = list[list.Count];
            }
            catch (IndexOutOfRangeException)
            {
                // 期待どおり（範囲外は例外）
            }
        }

        Messages.Add(formatter(state, exception));
    }
}

/// <summary>ファイルへ書き込まないテスト用の設定サービス。</summary>
internal sealed class TestSettingsService : ISettingsService
{
    /// <summary>保持する設定。</summary>
    public Settings Current { get; } = new();

    /// <summary>保存が要求された回数。</summary>
    public int SaveCount { get; private set; }

    /// <inheritdoc />
    public void Save() => SaveCount++;
}
