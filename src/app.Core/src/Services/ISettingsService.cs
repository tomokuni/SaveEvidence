using Estable.SaveEvidence.Models;

namespace Estable.SaveEvidence.Services;

/// <summary>JSON ファイルによる設定管理を提供するサービスインターフェース。</summary>
public interface ISettingsService
{
    /// <summary>現在の設定オブジェクトを取得する。</summary>
    Settings Current { get; }

    /// <summary>設定をファイルに保存する。</summary>
    void Save();
}
