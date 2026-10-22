using System.Drawing.Imaging;
using Microsoft.Extensions.Logging;

namespace EsUtil.SaveEvidence.App.WinForms.Tests;

/// <summary>テストで共用するユーティリティ。</summary>
internal static class TestHelper
{
    /// <summary>単色のビットマップを生成する。</summary>
    /// <param name="width">幅（1 以上）。</param>
    /// <param name="height">高さ（1 以上）。</param>
    /// <param name="color">塗りつぶす色。 null の場合は赤。</param>
    /// <returns>生成したビットマップ。 所有権は呼び出し側。</returns>
    public static Bitmap CreateBitmap(int width, int height, Color? color = null)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(bitmap);
        using var brush = new SolidBrush(color ?? Color.FromArgb(255, 255, 0, 0));
        graphics.FillRectangle(brush, 0, 0, width, height);
        return bitmap;
    }

    /// <summary>コアの画像を生成する（単色）。</summary>
    /// <param name="width">幅（1 以上）。</param>
    /// <param name="height">高さ（1 以上）。</param>
    /// <returns>生成した画像。 所有権は呼び出し側。</returns>
    public static EsReadOnlyBitmap CreateEsBitmap(int width, int height)
    {
        using var bitmap = CreateBitmap(width, height);
        return EsUtil.SaveEvidence.App.WinForms.Services.GdiBitmapInterop.ToEsReadOnlyBitmap(bitmap);
    }

    /// <summary>画像ファイルを一時領域へ書き出す（拡張子が形式を決める）。</summary>
    /// <param name="width">幅。</param>
    /// <param name="height">高さ。</param>
    /// <param name="extension">拡張子（例: <c>.png</c>）。</param>
    /// <returns>書き出したファイルのパス。</returns>
    /// <remarks>テストの後始末は呼び出し側が行う（<see cref="DeleteFile"/>）。</remarks>
    public static string WriteImageFile(int width, int height, string extension = ".png")
    {
        var path = CreateTempPath(extension);
        using var bitmap = CreateBitmap(width, height);
        bitmap.Save(path, ImageFormat.Png);
        return path;
    }

    /// <summary>一時ファイルのパスを作る（ファイルは作成しない）。</summary>
    /// <param name="extension">拡張子（例: ".png"）。</param>
    /// <returns>一時ディレクトリ配下の一意なパス。</returns>
    public static string CreateTempPath(string extension)
        => Path.Combine(Path.GetTempPath(), $"saveevidence-winforms-test-{Guid.NewGuid():N}{extension}");

    /// <summary>ファイルを削除する（存在しない場合は何もしない）。</summary>
    /// <param name="path">対象のパス。</param>
    public static void DeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // 一時ファイルの削除失敗はテストの合否に影響しない
        }
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
        }

        Messages.Add(formatter(state, exception));
    }
}
