namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>ファイルの保存に失敗したことを通知するイベントの引数。</summary>
/// <remarks>
/// <b>役割</b>: 保存の失敗（例外）を、例外を投げずにホストへ伝える（アプリは処理を継続する）。<br/>
/// <b>記録はホストが行う</b>: ログの出力先とレベルはホスト（アプリ）が所有するため、コアは通知だけを行う<br/>
/// （[`.github/例外処理規約.md`](../../../../.github/例外処理規約.md) §4「捕捉したら必ずログへ記録する」の<br/>
/// 記録側をアプリが担う）。 設定（<see cref="ISettingsService.SaveFailed"/>）と<br/>
/// 画像（<c>MainViewModel.SaveFailed</c>）の両方が本型を使う。
/// </remarks>
/// <param name="error">発生した例外。</param>
/// <param name="filePath">保存しようとしたファイルのパス（特定できない場合は空文字）。</param>
public sealed class SaveFailedEventArgs(Exception error, string filePath) : EventArgs
{
    /// <summary>発生した例外を取得する。</summary>
    /// <value>保存に失敗した原因の例外。</value>
    public Exception Error { get; } = error;

    /// <summary>保存しようとしたファイルのパスを取得する。</summary>
    /// <value>パス。 特定できない場合は空文字。</value>
    public string FilePath { get; } = filePath;
}
