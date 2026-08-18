using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Microsoft.Extensions.Logging;

namespace Estable.AvaloniaUI.Loupe.Tests;

/// <summary>テスト用の共通ヘルパー。</summary>
public static class TestHelper
{
    /// <summary>単色 WriteableBitmap を生成する（BGRA8888・不透明）。</summary>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <returns>生成した単色 Bitmap（青）。</returns>
    /// <remarks>
    /// 切り出し対象の Bitmap として使用する（Skia 初期化済みが必要）。<br/>
    /// </remarks>
    public static WriteableBitmap CreateSolidBitmap(int width, int height)
    {
        var wb = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        using var locked = wb.Lock();
        var stride = locked.RowBytes;
        unsafe
        {
            var ptr = (byte*)locked.Address;
            for (var i = 0; i < stride * height; i += 4)
            {
                ptr[i] = 255;     // B
                ptr[i + 1] = 0;   // G
                ptr[i + 2] = 0;   // R
                ptr[i + 3] = 255; // A
            }
        }
        return wb;
    }

    /// <summary>ログ出力を収集するテスト用 ILogger 実装（例外ログの検証用）。</summary>
    public sealed class ListLogger : ILogger
    {
        /// <summary>収集したログメッセージ。</summary>
        public List<string> Messages { get; } = [];

        /// <summary>Error レベルのログが記録されたか。</summary>
        public bool HasError { get; private set; }

        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            // LoggerMessage 生成コードが state を IReadOnlyList として走査する経路を実行する（Count・インデクサ・列挙・範囲外）
            if (state is IReadOnlyList<KeyValuePair<string, object?>> list)
            {
                _ = list.Count;
                for (int i = 0; i < list.Count; i++)
                {
                    _ = list[i];
                }
                foreach (var kv in list)
                {
                    _ = kv.Key;
                }
                try
                {
                    _ = list[list.Count];
                }
                catch (IndexOutOfRangeException)
                {
                    // 範囲外アクセスの例外経路も生成コードの網羅対象として実行する
                }
            }

            Messages.Add(formatter(state, exception));
            if (logLevel == LogLevel.Error)
            {
                HasError = true;
            }
        }
    }
}

