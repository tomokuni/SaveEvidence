using Estable.SaveEvidence.Models;
using Estable.SaveEvidence.Services;
using Estable.SaveEvidence.ViewModels;

namespace SaveEvidence.MewUI;

/// <summary>アプリ全体で共有するコアオブジェクト（MewUI 版のサービス束）。</summary>
/// <param name="Main">アプリのメイン ViewModel（プレビュー・コマンド）。</param>
/// <param name="Settings">設定サービス（UI 非依存の共有実装）。</param>
/// <param name="UndoStack">Undo / Redo 履歴（キャプチャページと Undo 一覧で共有する）。</param>
/// <param name="FolderView">フォルダビューの表示状態（表示モード・ソート順・フォルダパス）。</param>
/// <remarks>
/// MewUI 版は DI コンテナを持たないため、起動時に生成したコアオブジェクトを本レコードで束ねて渡す。<br/>
/// 各ページは必要なメンバーのみを受け取る（本レコードをそのまま引き回さない）。<br/>
/// UI 層（コントロール・ウィンドウ）は含めない（生成は各ページが行う）。
/// </remarks>
internal sealed record AppServices(
    MainViewModel Main,
    ISettingsService Settings,
    UndoStack UndoStack,
    FolderViewViewModel FolderView);
