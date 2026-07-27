using Avalonia.Controls;
using Avalonia.Platform;

namespace Estable.AvaloniaUI.Platform;

/// <summary>ネイティブウィンドウ操作を抽象化するインターフェイス。</summary>
/// <remarks>
/// オーバーレイウィンドウに必要なネイティブウィンドウ操作を提供する。<br/>
/// 実装は <see cref="WinWindowService"/>（Win32 API ベース）。<br/>
/// <br/>
/// **現状の注意（Windows 専用）**: 共有プラットフォームには Linux / macOS の実装は提供しておらず、<br/>
/// 実質的な価値はテスト容易性（モック）とカプセル化（Win32 詳細の隠蔽）にある。<br/>
/// <br/>
/// ウィンドウの特定は <see cref="IPlatformHandle"/>（プラットフォーム非依存の識別子）で行う。<br/>
/// <see cref="TopLevel"/>（Avalonia 抽象）はハンドル取得時に UI スレッドでのみ使用する。<br/>
/// </remarks>
public interface IWindowService
{
    /// <summary>ウィンドウをタスクバー / Alt+Tab に表示しないよう設定する（選択オーバーレイ用、仕様 2.2）。</summary>
    /// <param name="handle">対象ウィンドウの識別子。</param>
    void HideFromTaskSwitcher(IPlatformHandle handle);

    /// <summary>ウィンドウをキャプチャ（GDI CopyFromScreen / DXGI 等）から除外する/除外を解除する（仕様 6.2.1）。</summary>
    /// <param name="handle">対象ウィンドウの識別子。</param>
    /// <param name="exclude">キャプチャ除外する場合は <see langword="true"/>、除外を解除する場合は <see langword="false"/>。</param>
    /// <returns>成功した場合は <see langword="true"/>。未対応プラットフォーム・無効なハンドルでは <see langword="false"/>。</returns>
    /// <remarks>
    /// キャプチャ前に除外を設定し、キャプチャ後に解除することで、オーナーウィンドウ（選択オーバーレイ等）が
    /// キャプチャ画像に写り込むのを防ぐ（フレームタイミングに依存しない決定的な除外）。<br/>
    /// Windows 10 2004（build 19041）以降で利用可能。それ以前では失敗（false）を返す。<br/>
    /// 除外設定が反映されるまで 1 フレーム待つ（設定直後のキャプチャを防ぐ）。
    /// </remarks>
    bool SetCaptureExclusion(IPlatformHandle handle, bool exclude);

    /// <summary>ウィンドウを最前面解除し、非表示にして閉じる（UI ハング時の退避、仕様 3.6）。</summary>
    /// <param name="handle">対象ウィンドウの識別子。</param>
    /// <remarks>
    /// UI スレッドがハングした場合にバックグラウンドスレッドから呼び出され、<br/>
    /// タスクマネージャー等でプロセスを終了できるようにする。<br/>
    /// ネイティブハンドル（<see cref="IPlatformHandle"/>）は呼び出し側が UI スレッドで取得・キャッシュした<br/>
    /// プラットフォーム非依存の識別子であり、本メソッドは Avalonia オブジェクトに触れない。<br/>
    /// </remarks>
    void EvacuateWindow(IPlatformHandle handle);
}
