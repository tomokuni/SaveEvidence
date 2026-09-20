using Estable.Platform.Geometry;

namespace Estable.Platform;

/// <summary>ネイティブウィンドウ操作を抽象化するインターフェイス。</summary>
/// <remarks>
/// オーバーレイウィンドウに必要なネイティブウィンドウ操作を提供する。<br/>
/// 実装は各プラットフォーム（Windows: Win32 API）が提供する。<br/>
/// <br/>
/// **現状の注意（Windows 専用）**: 共有プラットフォームには Linux / macOS の実装は提供しておらず、<br/>
/// 実質的な価値はテスト容易性（モック）とカプセル化（Win32 詳細の隠蔽）にある。<br/>
/// <br/>
/// ウィンドウの特定は UI フレームワーク非依存の <see cref="EsPlatformHandle"/> で行う。<br/>
/// ハンドルの取得（各 UI のウィンドウオブジェクトからの変換）は UI 側の Interop 層が担う。<br/>
/// </remarks>
public interface IWindowService
{
    /// <summary>ウィンドウをタスクバー / Alt+Tab に表示しないよう設定する（選択オーバーレイ用）。</summary>
    /// <param name="handle">対象ウィンドウの識別子。</param>
    void HideFromTaskSwitcher(EsPlatformHandle handle);

    /// <summary>ウィンドウをキャプチャ（GDI CopyFromScreen / DXGI 等）から除外する/除外を解除する。</summary>
    /// <param name="handle">対象ウィンドウの識別子。</param>
    /// <param name="exclude">キャプチャ除外する場合は <see langword="true"/>、除外を解除する場合は <see langword="false"/>。</param>
    /// <returns>成功した場合は <see langword="true"/>。未対応プラットフォーム・無効なハンドルでは <see langword="false"/>。</returns>
    /// <remarks>
    /// キャプチャ前に除外を設定し、キャプチャ後に解除することで、オーナーウィンドウ（選択オーバーレイ等）が<br/>
    /// キャプチャ画像に写り込むのを防ぐ（フレームタイミングに依存しない決定的な除外）。<br/>
    /// Windows 10 2004（build 19041）以降で利用可能。それ以前では失敗（false）を返す。<br/>
    /// 除外設定が反映されるまで 1 フレーム待つ（設定直後のキャプチャを防ぐ）。
    /// </remarks>
    bool SetCaptureExclusion(EsPlatformHandle handle, bool exclude);

    /// <summary>ウィンドウを最前面解除し、非表示にして閉じる（UI ハング時の退避）。</summary>
    /// <param name="handle">対象ウィンドウの識別子。</param>
    /// <remarks>
    /// UI スレッドがハングした場合にバックグラウンドスレッドから呼び出され、<br/>
    /// タスクマネージャー等でプロセスを終了できるようにする。<br/>
    /// <see cref="EsPlatformHandle"/> は呼び出し側が UI スレッドで取得・キャッシュした UI 非依存の識別子であり、<br/>
    /// 本メソッドは UI フレームワークのオブジェクトに触れない。<br/>
    /// </remarks>
    void EvacuateWindow(EsPlatformHandle handle);
}
