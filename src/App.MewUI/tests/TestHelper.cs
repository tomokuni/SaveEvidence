using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.Platform.Skia;
using Microsoft.Extensions.Logging;
using SkiaSharp;


namespace EsUtil.SaveEvidence.App.MewUI.Tests;

/// <summary>テストで共用するユーティリティ。</summary>
internal static class TestHelper
{
    /// <summary>指定サイズの画像を生成する（内容は単色）。</summary>
    /// <param name="width">幅（1 以上）。</param>
    /// <param name="height">高さ（1 以上）。</param>
    /// <returns>生成した画像。 所有権は呼び出し側。</returns>
    public static EsWritableBitmap CreateBitmap(int width = 4, int height = 4)
    {
        // 所有権は FromSKBitmap へ移るため using を付けない
        var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.CornflowerBlue);

        return bitmap.ToEsBitmap();
    }

    /// <summary>一時ファイルのパスを作る（ファイルは作成しない）。</summary>
    /// <param name="extension">拡張子（例: ".png"）。</param>
    /// <returns>一時ディレクトリ配下の一意なパス。</returns>
    public static string CreateTempPath(string extension)
        => Path.Combine(Path.GetTempPath(), $"saveevidence-test-{Guid.NewGuid():N}{extension}");

    /// <summary>ファイルの実際の保存形式を読み取る。</summary>
    /// <param name="filePath">対象のファイルパス。</param>
    /// <returns>判別した保存形式。 判別できない場合は null。</returns>
    public static SKEncodedImageFormat? ReadFormat(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var codec = SKCodec.Create(stream);

        return codec?.EncodedFormat;
    }
}

/// <summary>ログ出力を収集するテスト用 <see cref="ILogger"/> 実装。</summary>
/// <remarks>
/// LoggerMessage ソース生成コードの state 走査（Count・インデクサ・列挙・範囲外アクセス）を実行させつつ、<br/>
/// 整形済みのメッセージを収集して内容を検証できるようにする。
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

