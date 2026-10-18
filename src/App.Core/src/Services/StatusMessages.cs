using System.Globalization;

namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>ステータスバーへ出す通知メッセージ（操作の結果）の文言。</summary>
/// <remarks>
/// 文言は本クラスが単一所有する（UI 版ごとに文言・有無がずれないようにする）。<br/>
/// 実際の設定は <see cref="ViewModels.MainViewModel"/> が行い、UI 層は結果の表示だけを担う。<br/>
/// 表記は全 UI 版で共通（例: <c>キャプチャしました（640 x 480）</c>）。
/// </remarks>
public static class StatusMessages
{
    /// <summary>撮影が確定したことを通知する文言を取得する。</summary>
    /// <param name="width">確定した画像の幅（ピクセル）。</param>
    /// <param name="height">確定した画像の高さ（ピクセル）。</param>
    /// <returns>通知の文言。</returns>
    public static string CaptureCompleted(int width, int height) => $"キャプチャしました（{Size(width, height)}）";

    /// <summary>切り出しが確定したことを通知する文言を取得する。</summary>
    /// <param name="width">切り出した範囲の幅（ピクセル）。</param>
    /// <param name="height">切り出した範囲の高さ（ピクセル）。</param>
    /// <returns>通知の文言。</returns>
    public static string CropCompleted(int width, int height) => $"切り出しました（{Size(width, height)}）";

    /// <summary>クリップボードから画像を貼り付けたことを通知する文言を取得する。</summary>
    /// <returns>通知の文言。</returns>
    public static string PasteCompleted() => "クリップボードの画像を貼り付けました。";

    /// <summary>クリップボードに画像が無いことを通知する文言を取得する。</summary>
    /// <returns>通知の文言。</returns>
    public static string PasteUnavailable() => "クリップボードに画像がありません。";

    /// <summary>画像の保存に失敗したことを通知する文言を取得する。</summary>
    /// <returns>通知の文言。</returns>
    public static string SaveFailed() => "保存できませんでした（保存先フォルダを確認してください）。";

    /// <summary>保存を実行できない状態であることを通知する文言を取得する。</summary>
    /// <param name="hasInvalidTemplate">ファイル名テンプレートが無効かどうか。</param>
    /// <param name="validationMessage">テンプレートの検証結果（有効時は null）。</param>
    /// <returns>通知の文言。</returns>
    public static string SaveUnavailable(bool hasInvalidTemplate, string? validationMessage)
        => hasInvalidTemplate
            ? $"ファイル名テンプレートが無効です: {validationMessage}"
            : "保存できる状態ではありません（画像が無い・保存済み・保存先が不正）。";

    /// <summary>自動切り出しで矩形を検出したことを通知する文言を取得する。</summary>
    /// <param name="count">検出した矩形の数。</param>
    /// <returns>通知の文言。</returns>
    public static string AutoCropDetected(int count)
        => $"自動切り出し: {count} 個のウィンドウを検出しました。ダブルクリックで確定してください。";

    /// <summary>自動切り出しで矩形を検出できなかったことを通知する文言を取得する。</summary>
    /// <returns>通知の文言。</returns>
    public static string AutoCropNotFound() => "自動切り出し: ウィンドウを検出できませんでした。";

    /// <summary>自動切り出しをキャンセルしたことを通知する文言を取得する。</summary>
    /// <returns>通知の文言。</returns>
    public static string AutoCropCanceled() => "自動切り出しをキャンセルしました。";

    /// <summary>「幅 x 高さ」の表記を取得する（カルチャに依存させない）。</summary>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <returns>表記。</returns>
    private static string Size(int width, int height)
        => string.Create(CultureInfo.InvariantCulture, $"{width} x {height}");
}
