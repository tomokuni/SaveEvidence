using app.Models;

namespace app.Services;

/// <summary>JSON ファイルによる設定管理を提供するサービスインターフェース。</summary>
public interface ISettingsService
{
    /// <summary>現在の設定オブジェクトを取得する。</summary>
    Settings Current { get; }

    /// <summary>設定をファイルに保存する。</summary>
    void Save();

    /// <summary>設定をファイルから再読み込みする。</summary>
    void Reload();
}
