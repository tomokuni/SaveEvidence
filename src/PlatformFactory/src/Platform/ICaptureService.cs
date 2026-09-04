using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Estable.AvaloniaUI.Platform;

/// <summary>画面/ウィンドウのキャプチャ機能を抽象化するインターフェイス。</summary>
/// <remarks>
/// 画面領域のキャプチャ（画面コピー）とウィンドウのキャプチャ（直接レンダリング / 画面コピー）を提供する。<br/>
/// 実装は <see cref="WinCaptureService"/>（Windows: System.Drawing + Win32 API）。<br/>
/// <br/>
/// **現状の注意（Windows 専用）**: 共有プラットフォームには Linux / macOS の実装は提供しておらず、<br/>
/// 実質的な価値はテスト容易性（モック）とカプセル化（System.Drawing 詳細の隠蔽）にある。<br/>
/// <br/>
/// 公開 API はプラットフォーム非依存である:<br/>
/// - 画像は Avalonia <see cref="Bitmap"/> で受け渡しする（System.Drawing 型は公開しない）。<br/>
///   WriteableBitmap の生成・ピクセル書き込みはスレッド非依存であり、バックグラウンドスレッドからも安全に実行できる。<br/>
/// - ウィンドウ識別は Avalonia <see cref="IPlatformHandle"/>（プラットフォーム非依存の識別子）を使用する。<br/>
/// <br/>
/// 具象実装ではなく本インターフェイスへ依存させることで、以下を達成する（DIP）:<br/>
/// - テスト時のモックが可能（上位 AP の単体テスト）<br/>
/// </remarks>
public interface ICaptureService
{
    /// <summary>指定された画面領域をキャプチャする（Avalonia Bitmap で返す）。</summary>
    /// <param name="bounds">キャプチャする画面領域（画面物理座標）。</param>
    /// <returns>キャプチャした Bitmap。失敗時は null。</returns>
    /// <remarks>
    /// WriteableBitmap の生成とピクセル書き込みはスレッド非依存のため、<br/>
    /// 画面キャプチャ（GDI 同期コール等）と同一のバックグラウンドスレッドで実行可能である。<br/>
    /// </remarks>
    Bitmap? CaptureArea(PixelRect bounds);

    /// <summary>仮想スクリーン（全モニタを包含する領域）全体を 1 回のキャプチャで取得する。</summary>
    /// <param name="origin">仮想スクリーンの原点（全モニタのバウンディングボックス左上、画面物理座標。プライマリモニタが (0,0) にない場合は負値）。</param>
    /// <returns>仮想スクリーン全体の Bitmap。キャプチャ失敗時は null。</returns>
    /// <remarks>
    /// 仮想スクリーン境界の取得（Windows: GetSystemMetrics）と全体キャプチャをプラットフォーム実装に委ね、<br/>
    /// 利用側（選択サブシステム）はプラットフォーム固有 API に依存しない（DIP）。<br/>
    /// WriteableBitmap の生成とピクセル書き込みはスレッド非依存のため、
    /// 画面キャプチャ（GDI 同期コール等）と同一のバックグラウンドスレッドで実行可能である。<br/>
    /// </remarks>
    Bitmap? CaptureVirtualScreen(out PixelPoint origin);

    /// <summary>指定されたウィンドウをキャプチャする（Avalonia Bitmap で返す）。</summary>
    /// <param name="handle">キャプチャ対象のウィンドウ識別子（プラットフォーム非依存）。</param>
    /// <param name="mode">ウィンドウキャプチャの取得方式。</param>
    /// <returns>キャプチャした画像。失敗時は null。</returns>
    Bitmap? CaptureWindow(IPlatformHandle handle, WindowCaptureMode mode = WindowCaptureMode.RedrawDirect);
}
