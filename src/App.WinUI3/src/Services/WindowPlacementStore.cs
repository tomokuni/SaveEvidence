using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Native.Win;
using EsUtil.Platform.Core.Window;
using EsUtil.Platform.Core;
using EsUtil.SaveEvidence.App.Core.Models;
using EsUtil.SaveEvidence.App.Core.Services;
using EsUtil.SaveEvidence.App.WinUI3.Logging;
using EsUtil.Ui.WinUI3.Interop;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace EsUtil.SaveEvidence.App.WinUI3.Services;

/// <summary>メイン ウィンドウの位置・サイズ・表示状態を設定へ保存し、起動時に復元する。</summary>
/// <remarks>
/// <b>単位</b>: WinUI 3 の <c>AppWindow</c> は画面物理ピクセルで位置と大きさを表すため、<br/>
/// 設定（画面物理ピクセルの整数矩形）との変換は不要である（他の UI 版のような DIP 変換を行わない）。<br/>
/// <b>復元できない値</b>: 幅・高さが 0 以下の値（壊れた設定ファイル・手編集の誤り）では復元しない<br/>
/// （0 サイズのウィンドウが作られて操作不能になるのを防ぐ。 判定は <see cref="EsWindowPlacementMath.IsUsable"/>）。<br/>
/// <b>保存の失敗</b>: 終了処理はウィンドウ メッセージの境界であり、例外がそのまま伝播するとプロセスが<br/>
/// 異常終了する。 保存できなくても終了は妨げず、原因だけを Warning で記録する。
/// </remarks>
internal static class WindowPlacementStore
{
    /// <summary>ロガー（保存の失敗を記録する）。</summary>
    private static readonly ILogger s_logger = AppLog.CreateLogger(nameof(WindowPlacementStore));

    /// <summary>設定に保存された位置・サイズ・表示状態をウィンドウへ復元する。</summary>
    /// <param name="window">対象のウィンドウ。</param>
    /// <param name="settings">復元する設定。</param>
    /// <remarks>
    /// 復元してよいかの判定（壊れたサイズ・画面外の座標・最小化）は共有規則（<see cref="EsWindowPlacementMath"/>）が<br/>
    /// 単一所有する。 画面外の座標を復元するとウィンドウが見えないまま起動するため、その場合は位置だけを<br/>
    /// 表示領域の内側へ収める。
    /// </remarks>
    public static bool Restore(Window window, Settings settings)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(settings);

        var appWindow = window.AppWindow;
        RestorePresenter(appWindow, EsWindowPlacementMath.ToRestorableState(settings.MainWindowState));

        var visibleArea = WinVirtualScreen.Bounds;
        if (!EsWindowPlacementMath.CanRestore(settings.MainWindowBounds, visibleArea))
        {
            return false;
        }

        var bounds = EsWindowPlacementMath.ClampToVisibleArea(settings.MainWindowBounds!.Value, visibleArea);
        appWindow.MoveAndResize(new RectInt32(bounds.X, bounds.Y, bounds.Width, bounds.Height));
        return true;
    }

    /// <summary>現在の位置・サイズ・表示状態を設定へ保存する。</summary>
    /// <param name="window">対象のウィンドウ。</param>
    /// <param name="settingsService">保存先の設定サービス。</param>
    /// <remarks>
    /// 最小化中の位置・サイズは復元に使えないため、位置・サイズは「最小化以外」のときだけ更新する<br/>
    /// （表示状態は常に更新する）。 保存は最小化・最大化の状態も含めて 1 回で行う。
    /// </remarks>
    public static void Save(Window window, ISettingsService settingsService)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(settingsService);

        try
        {
            var appWindow = window.AppWindow;
            var settings = settingsService.Current;

            settings.MainWindowState = ReadWindowState(appWindow);

            if (settings.MainWindowState != EsWindowState.Minimized)
            {
                var position = appWindow.Position;
                var size = appWindow.Size;
                settings.MainWindowBounds = new EsRectInt(position.X, position.Y, size.Width, size.Height);
            }

            settingsService.Save();
        }
        catch (Exception ex) when (EsErrors.IsRecoverable(ex))
        {
            // 終了処理の境界のため、保存できなくても終了は妨げない（原因だけを残す）
            s_logger.LogWindowPlacementSaveError(ex);
        }
    }

    /// <summary>保存された表示状態をウィンドウへ反映する。</summary>
    /// <param name="appWindow">対象のアプリ ウィンドウ。</param>
    /// <param name="state">復元する表示状態。</param>
    /// <remarks>
    /// 全画面は表示方法（<c>AppWindowPresenterKind</c>）で、通常・最小化・最大化は重ね表示の状態で表す<br/>
    /// （写像は <see cref="EsWindowStateInterop"/> が単一所有する）。
    /// </remarks>
    private static void RestorePresenter(AppWindow appWindow, EsWindowState state)
    {
        appWindow.SetPresenter(state.ToAppWindowPresenterKind());
        if (state == EsWindowState.FullScreen || appWindow.Presenter is not OverlappedPresenter presenter)
        {
            return;
        }

        switch (state.ToOverlappedPresenterState())
        {
            case OverlappedPresenterState.Maximized:
                presenter.Maximize();
                break;
            case OverlappedPresenterState.Minimized:
                presenter.Minimize();
                break;
            default:
                presenter.Restore();
                break;
        }
    }

    /// <summary>ウィンドウの現在の表示状態を読み取る。</summary>
    /// <param name="appWindow">対象のアプリ ウィンドウ。</param>
    /// <returns>読み取った表示状態。</returns>
    private static EsWindowState ReadWindowState(AppWindow appWindow)
        => appWindow.Presenter switch
        {
            { Kind: AppWindowPresenterKind.FullScreen } => EsWindowState.FullScreen,
            OverlappedPresenter presenter => presenter.State.ToEsWindowState(),
            _ => EsWindowState.Normal,
        };
}
