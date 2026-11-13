using EsUtil.Platform.Core.Capture;
using EsUtil.Platform.Core.Input;
using Microsoft.Extensions.Logging;

namespace EsUtil.SaveEvidence.App.Core.Logging;

/// <summary>4 つの UI 版で共通のログ定義（LoggerMessage ソース生成・拡張メソッド）。</summary>
/// <remarks>
/// <b>ここに置く理由</b>: 同じ出来事を UI 版ごとに書き分けると、<b>同じ障害の文言が版ごとに食い違う</b>
/// （実測: ホットキーの登録失敗は 2 通りの書式、一覧の読み込み失敗は 2 通りの書式があった）。
/// 記録する内容は UI に依存しないため、<b>共通のメッセージは 1 か所が単一所有する</b>
/// （規則の所有者は <c>.github/ログ規約.md</c> §3・§5）。<br/>
/// <b>接頭辞は引数で受ける</b>: 接頭辞（<c>[App.WinForms]</c> 等）はアセンブリごとに 1 つであり、
/// 各 UI の <c>AppLog</c> が自分の <c>Prefix</c> を渡す（本クラスは接頭辞を持たない）。<br/>
/// <b>UI に固有のメッセージはここへ置かない</b>（各 UI の <c>AppLog</c> が持つ）。
/// </remarks>
internal static partial class AppLogMessage
{
    /// <summary>アプリケーション起動のログ。</summary>
    /// <remarks>
    /// LogLevel: Information <br/>
    /// 出力書式: <c>[App.&lt;UI&gt;] アプリケーション起動 (version={Version})</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（ホストの <c>AppLog</c> が自分の接頭辞を渡す）。</param>
    /// <param name="version">アプリケーションのバージョン。</param>
    [LoggerMessage(LogLevel.Information, "{Prefix} アプリケーション起動 (version={Version})")]
    internal static partial void LogAppStartCore(this ILogger logger, string prefix, string version);

    /// <summary>アプリケーション終了のログ。</summary>
    /// <remarks>
    /// LogLevel: Information <br/>
    /// 出力書式: <c>[App.&lt;UI&gt;] アプリケーション終了 (exitCode={ExitCode})</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞。</param>
    /// <param name="exitCode">終了コード。</param>
    [LoggerMessage(LogLevel.Information, "{Prefix} アプリケーション終了 (exitCode={ExitCode})")]
    internal static partial void LogAppExitCore(this ILogger logger, string prefix, int exitCode);

    /// <summary>設定変更の撮影セッションへの反映で例外が発生したことを記録する。</summary>
    /// <remarks>
    /// LogLevel: Error <br/>
    /// 出力書式: <c>[App.&lt;UI&gt;] 設定変更の撮影セッション反映で例外が発生: {PropertyName}</c>（例外はスタックトレース込み）<br/>
    /// 設定の検証（範囲外の値）で例外になっても UI を壊さないため、反映は例外保護付きで行う。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞。</param>
    /// <param name="ex">発生した例外。</param>
    /// <param name="propertyName">変更された設定プロパティ名。</param>
    [LoggerMessage(LogLevel.Error, "{Prefix} 設定変更の撮影セッション反映で例外が発生: {PropertyName}")]
    internal static partial void LogSettingsApplyErrorCore(this ILogger logger, string prefix, Exception ex, string propertyName);

    /// <summary>終了時のウィンドウ配置の保存で例外が発生したことを記録する。</summary>
    /// <remarks>
    /// LogLevel: Warning <br/>
    /// 出力書式: <c>[App.&lt;UI&gt;] ウィンドウ配置の保存で例外が発生</c>（例外はスタックトレース込み）<br/>
    /// ウィンドウの終了は UI フレームワークの境界であり、例外がそのまま伝播すると終了ログも保存結果も残らない。
    /// 保存に失敗しても終了は妨げない。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞。</param>
    /// <param name="ex">発生した例外。</param>
    [LoggerMessage(LogLevel.Warning, "{Prefix} ウィンドウ配置の保存で例外が発生")]
    internal static partial void LogWindowPlacementSaveErrorCore(this ILogger logger, string prefix, Exception ex);

    /// <summary>グローバル ホットキーの登録に失敗したことを記録する。</summary>
    /// <remarks>
    /// LogLevel: Warning <br/>
    /// 出力書式: <c>[App.&lt;UI&gt;] ホットキーを登録できませんでした: {CaptureType}, key={Key}</c><br/>
    /// 他のアプリが同じ組み合わせを使用している場合にも起こるため、1 件の競合でアプリを止めず、
    /// 失敗した組み合わせだけを記録する。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞。</param>
    /// <param name="captureType">登録しようとした撮影の種別。</param>
    /// <param name="key">登録しようとしたキー。</param>
    [LoggerMessage(LogLevel.Warning, "{Prefix} ホットキーを登録できませんでした: {CaptureType}, key={Key}")]
    internal static partial void LogHotKeyRegisterFailedCore(this ILogger logger, string prefix, CaptureType captureType, EsKey key);

    /// <summary>一覧のファイル列挙で例外が発生したことを記録する。</summary>
    /// <remarks>
    /// LogLevel: Error <br/>
    /// 出力書式: <c>[App.&lt;UI&gt;] 一覧の読み込みで例外が発生: {FolderPath}</c>（例外はスタックトレース込み）<br/>
    /// 一覧が空になる原因（未解決のパス・アクセス拒否等）を追跡できるようにする。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞。</param>
    /// <param name="ex">発生した例外。</param>
    /// <param name="folderPath">対象のフォルダパス。</param>
    [LoggerMessage(LogLevel.Error, "{Prefix} 一覧の読み込みで例外が発生: {FolderPath}")]
    internal static partial void LogFolderLoadErrorCore(this ILogger logger, string prefix, Exception ex, string folderPath);

    /// <summary>選択した画像をごみ箱へ移動できなかったことを記録する。</summary>
    /// <remarks>
    /// LogLevel: Warning <br/>
    /// 出力書式: <c>[App.&lt;UI&gt;] 画像をごみ箱へ移動できませんでした: {FilePath}</c>（例外はスタックトレース込み）<br/>
    /// 移動できなかったファイルは一覧に残したまま継続する。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞。</param>
    /// <param name="ex">発生した例外。</param>
    /// <param name="filePath">対象のファイルパス。</param>
    [LoggerMessage(LogLevel.Warning, "{Prefix} 画像をごみ箱へ移動できませんでした: {FilePath}")]
    internal static partial void LogFileDeleteErrorCore(this ILogger logger, string prefix, Exception ex, string filePath);

    /// <summary>選択した画像をクリップボードへコピーできなかったことを記録する。</summary>
    /// <remarks>
    /// LogLevel: Warning <br/>
    /// 出力書式: <c>[App.&lt;UI&gt;] 画像をクリップボードへコピーできませんでした: {FilePath}</c><br/>
    /// 例外を伴わない失敗（クリップボードを開けなかった等）もあるため、<paramref name="ex"/> は省略できる。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞。</param>
    /// <param name="ex">発生した例外（無い場合は null）。</param>
    /// <param name="filePath">対象のファイルパス（履歴の画像など、パスを持たない場合は対象の名前）。</param>
    [LoggerMessage(LogLevel.Warning, "{Prefix} 画像をクリップボードへコピーできませんでした: {FilePath}")]
    internal static partial void LogClipboardCopyErrorCore(this ILogger logger, string prefix, Exception? ex, string filePath);

    /// <summary>履歴（Undo 一覧）の画像をクリップボードへコピーできなかったことを記録する。</summary>
    /// <remarks>
    /// LogLevel: Warning <br/>
    /// 出力書式: <c>[App.&lt;UI&gt;] 履歴の画像をクリップボードへコピーできませんでした</c><br/>
    /// 履歴の画像はファイルを持たない（画素だけ）ため、パスではなく「履歴の画像」であることを記録する。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞。</param>
    [LoggerMessage(LogLevel.Warning, "{Prefix} 履歴の画像をクリップボードへコピーできませんでした")]
    internal static partial void LogHistoryImageCopyErrorCore(this ILogger logger, string prefix);

    /// <summary>保存先フォルダをエクスプローラーで開けなかったことを記録する。</summary>
    /// <remarks>
    /// LogLevel: Warning <br/>
    /// 出力書式: <c>[App.&lt;UI&gt;] エクスプローラーを起動できませんでした: {FolderPath}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞。</param>
    /// <param name="ex">発生した例外。</param>
    /// <param name="folderPath">対象のフォルダパス。</param>
    [LoggerMessage(LogLevel.Warning, "{Prefix} エクスプローラーを起動できませんでした: {FolderPath}")]
    internal static partial void LogOpenFolderErrorCore(this ILogger logger, string prefix, Exception ex, string folderPath);

    /// <summary>背景のサムネイル生成で想定外の例外が発生したことを記録する。</summary>
    /// <remarks>
    /// LogLevel: Warning <br/>
    /// 出力書式: <c>[App.&lt;UI&gt;] サムネイルの生成で例外が発生</c>（例外はスタックトレース込み）<br/>
    /// 生成は背景で行うため、失敗しても一覧の表示は継続する（握りつぶさず記録する）。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞。</param>
    /// <param name="ex">発生した例外。</param>
    [LoggerMessage(LogLevel.Warning, "{Prefix} サムネイルの生成で例外が発生")]
    internal static partial void LogThumbnailGenerateErrorCore(this ILogger logger, string prefix, Exception ex);

    /// <summary>起動後のサムネイルの先読みを開始したことを記録する。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[App.&lt;UI&gt;] サムネイルの先読みを開始: {Target} 対象={Count} 件</c><br/>
    /// 起動後に 1 回だけ出力する（ページを開く前に低負荷で生成しておき、切り替えを待たせない）。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞。</param>
    /// <param name="target">先読みの対象（ページ名）。</param>
    /// <param name="count">先読みする件数。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix} サムネイルの先読みを開始: {Target} 対象={Count} 件")]
    internal static partial void LogThumbnailWarmUpCore(this ILogger logger, string prefix, string target, int count);

    /// <summary>未処理の例外（AppDomain / UI スレッド / TaskScheduler）を記録する。</summary>
    /// <remarks>
    /// LogLevel: Critical <br/>
    /// 出力書式: <c>[App.&lt;UI&gt;] 未処理の例外: {Source}</c>（例外はスタックトレース込みで出力される）<br/>
    /// アプリがクラッシュする直前の例外を確実に記録する（原因特定用）。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞。</param>
    /// <param name="ex">発生した例外。</param>
    /// <param name="source">例外の発生源（例: "AppDomain.UnhandledException"）。</param>
    [LoggerMessage(LogLevel.Critical, "{Prefix} 未処理の例外: {Source}")]
    internal static partial void LogUnhandledExceptionCore(this ILogger logger, string prefix, Exception ex, string source);
}
