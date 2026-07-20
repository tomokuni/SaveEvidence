using app.Models;

namespace app.Services;

/// <summary>
/// アプリケーション設定の読み書き機能を提供するサービスのインターフェース。
/// </summary>
/// <remarks>
/// 設定の保存形式（JSONファイル等）や保存場所を呼び出し元から隠蔽する。<br/>
/// <see cref="Current"/> で現在の設定オブジェクトを取得し、<see cref="Save"/> で永続化する。<br/>
/// </remarks>
public interface ISettingsService
{
    /// <summary>
    /// 現在の設定オブジェクトを取得する。
    /// </summary>
    Settings Current { get; }

    /// <summary>
    /// 設定をファイルに保存する。
    /// </summary>
    void Save();

    /// <summary>
    /// 設定をファイルから再読み込みする。
    /// </summary>
    void Reload();
}
