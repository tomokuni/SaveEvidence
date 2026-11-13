using EsUtil.SaveEvidence.App.Core.Models;
using EsUtil.SaveEvidence.App.Core.ViewModels;

namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>アプリ全体で共有するコア オブジェクト（4 つの UI 版で同じ束）。</summary>
/// <param name="Main">アプリのメイン ViewModel（プレビュー・コマンド）。</param>
/// <param name="Settings">設定サービス（UI 非依存の共有実装）。</param>
/// <param name="UndoHistory">Undo / Redo 履歴（キャプチャ ページと Undo 一覧で共有する）。</param>
/// <param name="FolderView">フォルダビューの表示状態（表示モード・ソート順・フォルダパス）。</param>
/// <param name="Undo">Undo 一覧の表示状態（表示モード）。</param>
/// <remarks>
/// <b>なぜ本層が持つか</b>: 束ねる型はすべて本層の型であり、UI 版ごとに書き分ける理由が無い
/// （実測 2026-11-13: 3 つの UI 版にほぼ同じレコードがあった。 WinForms 版だけ <c>Undo</c> が多かった）。<br/>
/// <b>UI の型は含めない</b>: コントロール・ウィンドウは各 UI が生成する。<br/>
/// <b>DI を使わない UI 版のための束</b>: 各 UI 版は起動時に本レコードを 1 つ生成し、ページへ渡す
/// （ページは必要なメンバーだけを使う。 本レコードをそのまま引き回さない）。
/// </remarks>
internal sealed record AppServices(
    MainViewModel Main,
    ISettingsService Settings,
    UndoHistory UndoHistory,
    FolderViewViewModel FolderView,
    UndoViewModel Undo);
