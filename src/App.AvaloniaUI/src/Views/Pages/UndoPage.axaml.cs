using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using EsUtil.Platform.Core;
using EsUtil.SaveEvidence.App.Core.Models;
using EsUtil.SaveEvidence.App.Core.ViewModels;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Imaging;
using Microsoft.Extensions.DependencyInjection;

namespace EsUtil.SaveEvidence.App.AvaloniaUI.Views.Pages;

/// <summary>
/// Undo / Redo スタックの画像履歴を一覧表示するページ。
/// </summary>
/// <remarks>
/// Undo / Redo スタック（<see cref="UndoHistory"/>・DI シングルトン）と現在のプレビュー画像
/// （<see cref="MainViewModel.PreviewImage"/>・DI シングルトン）を
/// 時系列順（新しい → 古い）にサムネイル表示する。<br/>
/// 表示順は「Redo（新しい）→ 現在 → Undo（古い）」の固定。<br/>
/// 現在のキャプチャページに表示中の画像は青枠（IsCurrent）で強調する。<br/>
/// 表示モード（特大 / 大 / 中）は <see cref="Settings.UndoViewModeIndex"/> に保存・復元される。<br/>
/// 表示モードの復元・保存、ToggleButton 同期、ページ表示時の再構築は <see cref="ViewModePageBase"/> に共通化する。<br/>
/// サムネイルのダブルクリックで一時ファイルに保存し、OS の既定アプリで開く。
/// </remarks>
public sealed partial class UndoPage : ViewModePageBase
{
    private readonly UndoViewModel _viewModel = null!;

    /// <summary>Undo / Redo スタック（DI から取得するシングルトン。 CapturePage と共有）。</summary>
    private readonly UndoHistory _undoHistory = null!;

    /// <summary>UndoPage を初期化する。</summary>
    public UndoPage()
    {
        InitializeComponent();

        if (Design.IsDesignMode) return;

        _viewModel = Program.ServiceProvider.GetRequiredService<UndoViewModel>();
        _undoHistory = Program.ServiceProvider.GetRequiredService<UndoHistory>();
        DataContext = _viewModel;

        InitializeViewModePage(
            Program.SettingsService,
            Program.ServiceProvider.GetRequiredService<MainViewModel>(),
            ExtraLargeButton, LargeButton, MediumButton);
    }

    /// <summary>現在の表示モードのインデックス（0=特大, 1=大, 2=中）。</summary>
    protected override int SelectedViewModeIndex
    {
        get => _viewModel.SelectedViewModeIndex;
        set => _viewModel.SelectedViewModeIndex = value;
    }

    /// <summary>保存済みの表示モードを ViewModel へ復元する。</summary>
    /// <remarks>
    /// <see cref="Settings.UndoViewModeIndex"/> を
    /// <see cref="UndoViewModel.SelectedViewModeIndex"/> へ反映する。<br/>
    /// 値の範囲チェックは <see cref="ViewModeDisplay.ClampIndex"/> に委譲する。
    /// </remarks>
    protected override void RestoreViewModeSettings()
    {
        _viewModel.SelectedViewModeIndex = ViewModeDisplay.ClampIndex(SettingsService.Current.UndoViewModeIndex);
    }

    /// <summary>現在の表示モードを設定へ保存する。</summary>
    /// <remarks>表示モード切替のたびに呼び出し、設定ファイルへ反映する。</remarks>
    protected override void SaveViewModeSettings()
    {
        SettingsService.Current.UndoViewModeIndex = _viewModel.SelectedViewModeIndex;
        SettingsService.Save();
    }

    /// <summary>Undo / Redo 履歴と現在の画像を読み込んで一覧を再構築する。</summary>
    protected override void ReloadItems() => RebuildList();

    /// <summary>一覧を再構築する（表示モード変更時・ページ表示時に呼び出し）。</summary>
    private void RebuildList()
    {
        if (_undoHistory is null) return;

        var size = GetIconSize();
        var items = new List<UndoListItem>();
        void AddItem(IImage thumbnail, string label, bool isCurrent)
            => items.Add(new UndoListItem
            {
                Thumbnail = thumbnail,
                Label = label,
                IsCurrent = isCurrent,
                ThumbnailWidth = size
            });

        // 1. Redo スタック（新しい順。 _redoStack[0] が現在に最も近い未来）
        // UndoHistory は EsBitmap を保持し、表示境界で非所有アダプタ（IImage）へ変換する。
        // 変換した IImage は UndoListItem が参照する（画像の所有は UndoHistory / ViewModel が保持し、
        // アダプタは本ページが一覧の差し替え時に破棄する）。
        var redo = _undoHistory.GetRedoHistory();
        for (var i = 0; i < redo.Count; i++)
        {
            AddItem(new EsImageSourceAdapter(redo[i]), $"Redo {i + 1}", isCurrent: false);
        }

        // 2. 現在の画像（青枠で強調）
        var currentImage = MainViewModel.PreviewImage;
        if (currentImage is not null)
        {
            // 表示境界で IImage へ変換してサムネイルへ設定する。
            AddItem(new EsImageSourceAdapter(currentImage), "現在", isCurrent: true);
        }

        // 3. Undo スタック（新しい順。 _stack[^1] が現在の直前）
        var undo = _undoHistory.GetUndoHistory();
        for (var i = undo.Count - 1; i >= 0; i--)
        {
            AddItem(new EsImageSourceAdapter(undo[i]), $"Undo {undo.Count - i}", isCurrent: false);
        }

        _viewModel.ItemCount = items.Count;

        // 旧一覧のサムネイル（IImage は非所有のため、保持する本ページが破棄する）を解放してから差し替える
        if (UndoListBox.ItemsSource is IEnumerable<UndoListItem> previous)
        {
            foreach (var old in previous)
            {
                (old.Thumbnail as IDisposable)?.Dispose();
            }
        }

        UndoListBox.ItemsSource = items;
    }

    /// <summary>
    /// サムネイルのダブルクリック時に一時ファイルへ保存し、OS の既定アプリ（外部ツール）で開く。
    /// </summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">タップイベント。</param>
    /// <remarks>
    /// Undo / Redo スタックの画像はファイルではなく Bitmap のため、
    /// 一時フォルダに PNG として保存してから <see cref="Process.Start()"/> で開く。
    /// </remarks>
    private void OnItemDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (UndoListBox.SelectedItem is not UndoListItem item) return;

        try
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"SaveEvidence_undo_{Guid.NewGuid():N}.png");

            // サムネイルは画像源アダプタ（IImage）のため、保存用に Avalonia Bitmap を借りる（破棄はアダプタの所有者）。
            if (item.Thumbnail is EsImageSourceAdapter adapter)
            {
                adapter.GetAvaloniaBitmap().Save(tempFile, PngBitmapEncoderOptions.Default);
            }
            else
            {
                return;
            }

            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = tempFile,
                UseShellExecute = true
            };
            process.Start();
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 外部ツールで開けなくてもユーザー操作を妨げない
        }
    }
}
