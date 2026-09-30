using Microsoft.Extensions.Logging;

namespace EsUtil.SaveEvidence.CaptureSession;

/// <summary>設定の適用を記録するログ定義。</summary>
/// <remarks>
/// 設定の変更は「事象」であり、項目が 45 個あるため値そのものはログに載せない。<br/>
/// ログの有効・無効の判定は生成コード（<c>IsEnabled</c>）が担うため、呼び出し側でゲートしない。
/// </remarks>
public static partial class CaptureSettingsLogMessage
{
    /// <summary>設定の変更を適用したことを記録する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <remarks>ログレベル: Debug。 出力書式: <c>[CaptureSession] SettingApplied</c></remarks>
    [LoggerMessage(EventId = 7, Level = LogLevel.Debug,
        Message = "[CaptureSession] SettingApplied")]
    public static partial void LogSettingApplied(this ILogger logger);
}
