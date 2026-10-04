using EsUtil.SaveEvidence.App.Core.ViewModels;
using Xunit;

namespace EsUtil.SaveEvidence.App.Core.Tests;

/// <summary>
/// FolderViewViewModel（フォルダビューの表示モード・ソート状態）の単体テスト。
/// </summary>
/// <remarks>
/// 表示モード（特大/大/中）とソート順（昇順/降順）の初期値・変更通知を検証する。<br/>
/// フォルダビューの表示モード・ソート順は設定（Settings）へ保存・起動時復元されるため、
/// 本 ViewModel が正しく状態を保持・通知できることが前提となる。
/// </remarks>
public class FolderViewViewModelTests
{
    [Fact]
    internal void 初期値_表示モードは大でソートは昇順である()
    {
        var vm = new FolderViewViewModel();

        // 既定の表示モード = 大（インデックス 1）、ソート = 昇順
        Assert.Equal(1, vm.SelectedViewModeIndex);
        Assert.Equal("大", vm.ViewModeText);
        Assert.True(vm.IsSortAscending);
        Assert.Equal("名前の昇順(▲)", vm.SortStatusText);
    }

    [Fact]
    internal void 表示モード変更でViewModeTextとPropertyChangedが通知される()
    {
        var vm = new FolderViewViewModel();
        var notified = new List<string?>();
        vm.PropertyChanged += (_, e) => notified.Add(e.PropertyName);

        // 特大（0）へ変更
        vm.SelectedViewModeIndex = 0;

        Assert.Contains(nameof(FolderViewViewModel.SelectedViewModeIndex), notified);
        Assert.Contains(nameof(FolderViewViewModel.ViewModeText), notified);
        Assert.Equal("特大", vm.ViewModeText);
    }

    [Fact]
    internal void 表示モード不正値のViewModeTextは空文字になる()
    {
        var vm = new FolderViewViewModel
        {
            // 範囲外のインデックス（表示名が存在しない）→ 空文字
            SelectedViewModeIndex = 5
        };

        Assert.Equal("", vm.ViewModeText);
    }

    [Fact]
    internal void ソート変更でSortStatusTextとPropertyChangedが通知される()
    {
        var vm = new FolderViewViewModel();
        var notified = new List<string?>();
        vm.PropertyChanged += (_, e) => notified.Add(e.PropertyName);

        // 降順へ変更
        vm.IsSortAscending = false;

        Assert.Contains(nameof(FolderViewViewModel.IsSortAscending), notified);
        Assert.Contains(nameof(FolderViewViewModel.SortStatusText), notified);
        Assert.Equal("名前の降順(▼)", vm.SortStatusText);
    }

    [Fact]
    internal void アイテム数変更でItemCountTextとPropertyChangedが通知される()
    {
        var vm = new FolderViewViewModel();
        var notified = new List<string?>();
        vm.PropertyChanged += (_, e) => notified.Add(e.PropertyName);

        vm.ItemCount = 10;

        Assert.Contains(nameof(FolderViewViewModel.ItemCount), notified);
        Assert.Contains(nameof(FolderViewViewModel.ItemCountText), notified);
        Assert.Equal("10 個のアイテム", vm.ItemCountText);
    }
}
