using EsUtil.SaveEvidence.App.Core.Models;

namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>JSON ファイルによる設定管理を提供するサービスインターフェース。</summary>
public interface ISettingsService
{
    /// <summary>現在の設定オブジェクトを取得する。</summary>
    Settings Current { get; }

    /// <summary>設定の保存に失敗したときに発生する。</summary>
    /// <remarks>
    /// 保存の失敗は例外にせず本イベントで通知する（アプリは処理を継続する）。<br/>
    /// 記録（ログ）はホスト（アプリ）が行う。 ハンドラーは保存の排他制御の外で呼ばれる。
    /// </remarks>
    event EventHandler<SaveFailedEventArgs>? SaveFailed;

    /// <summary>設定をファイルに保存する。</summary>
    void Save();
}
