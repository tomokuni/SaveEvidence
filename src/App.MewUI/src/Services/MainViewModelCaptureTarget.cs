using System.ComponentModel;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.App.Core.ViewModels;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Capture;

namespace EsUtil.SaveEvidence.App.MewUI.Services;

/// <summary><see cref="MainViewModel"/> を切り出しセッションの適用先（<see cref="ICaptureTarget"/>）として公開する。</summary>
/// <remarks>
/// 切り出しセッション（<c>CaptureSessionControl.MewUI</c> の <c>CaptureSessionControl</c>）はアプリの型を参照しないため、<br/>
/// 本アダプタが境界になる。<br/>
/// - <see cref="CurrentImage"/>: プレビュー画像（切り出しの対象・ルーペのソース）。<br/>
/// - <see cref="ImageChanged"/>: <c>PreviewImage</c> の変更通知を中継する。<br/>
/// - <see cref="ApplyCrop"/>: <c>CropPreviewCommand</c> へ委譲する（Undo 履歴への退避・未保存化・画像差し替えは<br/>
///   ViewModel が単一所有する）。
/// </remarks>
internal sealed class MainViewModelCaptureTarget : ICaptureTarget
{
    /// <summary>アプリのメイン ViewModel。</summary>
    private readonly MainViewModel _viewModel;

    /// <summary>プレビュー画像の変更を切り出しセッションへ中継する。</summary>
    /// <remarks>購読は本インスタンスの生存期間中有効にする（切り出しセッションはページと同期間生きる）。</remarks>
    /// <summary>指定した ViewModel を適用先として公開する。</summary>
    /// <param name="viewModel">アプリのメイン ViewModel。</param>
    public MainViewModelCaptureTarget(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        _viewModel.PropertyChanged += OnPropertyChanged;
    }

    /// <inheritdoc/>
    public EsBitmap? CurrentImage => _viewModel.PreviewImage;

    /// <inheritdoc/>
    public event EventHandler? ImageChanged;

    /// <inheritdoc/>
    /// <remarks>
    /// 切り出しの適用は <c>CropPreviewCommand</c> へ委譲する（Undo 履歴への退避・未保存化・画像差し替えが<br/>
    /// キャプチャページと共通の経路になる）。
    /// </remarks>
    public void ApplyCrop(EsRectInt bounds) => _viewModel.CropPreviewCommand.Execute(bounds);

    /// <summary>ViewModel のプロパティ変更を切り出しセッション向けのイベントへ変換する。</summary>
    /// <param name="sender">通知元。</param>
    /// <param name="e">変更されたプロパティ。</param>
    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(MainViewModel.PreviewImage), StringComparison.Ordinal))
        {
            ImageChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
