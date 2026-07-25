using System.Diagnostics;

namespace app.Helpers;

/// <summary>
/// キャプチャ処理の診断ログ出力を行う静的ユーティリティクラス。
/// </summary>
/// <remarks>
/// Debug.WriteLine（OutputDebugString）とファイル出力（%TEMP%\SaveEvidence_capture_yyyyMMdd.log）の<br/>
/// 両方に同一内容を書き込む。<br/>
/// スレッドセーフであり、複数のスレッドから同時に呼び出しても安全。<br/>
/// ファイル出力は追記モードで動作する。<br/>
/// 起動時に前々日以前のログファイルを自動削除する。<br/>
/// </remarks>
internal static class CaptureLogger
{
    /// <summary>ログファイル名のプレフィックス。</summary>
    private const string LogPrefix = "SaveEvidence_capture_";

    /// <summary>ログファイル名のサフィックス（拡張子）。</summary>
    private const string LogSuffix = ".log";

    /// <summary>ログファイルの検索パターン。</summary>
    private const string LogSearchPattern = "SaveEvidence_capture_*.log";

    /// <summary>保持する最大日数（前々日以前 = 2日以上前のファイルを削除）。</summary>
    private const int RetentionDays = 1;

    private static readonly string s_logDir = Path.GetTempPath();

    private static readonly string s_logPath = Path.Combine(
        s_logDir, $"{LogPrefix}{DateTime.Now:yyyyMMdd}{LogSuffix}");

    private static readonly object s_lock = new();

    /// <summary>
    /// ログメッセージを出力する。
    /// </summary>
    /// <param name="message">ログメッセージ</param>
    public static void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var line = $"[{timestamp}] {message}";

        // Debug.WriteLine は OutputDebugString 経由で DebugView 等でキャプチャ可能
        Debug.WriteLine(line);

        // ファイルにも出力（追記）
        lock (s_lock)
        {
            try
            {
                File.AppendAllText(s_logPath, line + Environment.NewLine);
            }
            catch
            {
                // ファイル出力失敗時は無視（デバッグ出力は確保されている）
            }
        }
    }

    /// <summary>
    /// 書式指定でログメッセージを出力する。
    /// </summary>
    /// <param name="format">複合書式指定文字列</param>
    /// <param name="args">書式指定に対応する値</param>
    public static void Log(string format, params object?[] args)
    {
        Log(string.Format(format, args));
    }

    /// <summary>
    /// ログファイルのパスを取得する。
    /// </summary>
    public static string LogFilePath => s_logPath;

    /// <summary>
    /// 前々日以前のログファイルを削除する。
    /// </summary>
    /// <remarks>
    /// %TEMP%\SaveEvidence_capture_*.log のうち、ファイル名の日付が<br/>
    /// 前々日（本日-2日）以前のものを削除する。<br/>
    /// アプリケーション起動時に呼び出されることを想定する。<br/>
    /// </remarks>
    public static void CleanupOldLogs()
    {
        var cutoff = DateTime.Today.AddDays(-RetentionDays - 1);
        try
        {
            var files = Directory.GetFiles(s_logDir, LogSearchPattern);
            foreach (var file in files)
            {
                // ファイル名から日付部分を抽出: "SaveEvidence_capture_20260723.log"
                var name = Path.GetFileNameWithoutExtension(file);
                var datePart = name[LogPrefix.Length..];
                if (DateTime.TryParseExact(datePart, "yyyyMMdd", null,
                    System.Globalization.DateTimeStyles.None, out var fileDate))
                {
                    if (fileDate <= cutoff)
                    {
                        try
                        {
                            File.Delete(file);
                            Debug.WriteLine($"[CaptureLogger] 古いログを削除: {file}");
                        }
                        catch
                        {
                            // 削除失敗時は無視
                        }
                    }
                }
            }
        }
        catch
        {
            // ディレクトリ列挙失敗時は無視
        }
    }
}
