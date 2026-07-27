using Avalonia;

namespace Estable.AvaloniaUI.Platform;

/// <summary>カーソル下の要素（ウィンドウ・スクリーン）検出を抽象化するインターフェイス。</summary>
/// <remarks>
/// カーソル下ウィンドウ / カーソル下スクリーンの検出を提供し、結果（矩形・表示名・識別子）を
/// <see cref="ElementFinderResults"/> として返す。<br/>
/// 実装は <see cref="WinElementFinder"/>（Win32 API ベース）。<br/>
/// <br/>
/// 公開 API はプラットフォーム非依存の <see cref="ElementFinderResults"/> を使用し、<br/>
/// Win32 ハンドル（<see cref="IntPtr"/>）等のプラットフォーム固有型を公開しない。<br/>
/// <br/>
/// **現状の注意（Windows 専用）**: 共有プラットフォームには Linux / macOS の実装は提供しておらず、<br/>
/// 実質的な価値はテスト容易性（モック）とカプセル化（Win32 詳細の隠蔽）にある。<br/>
/// <br/>
/// 具象実装ではなく本インターフェイスへ依存させることで、以下を達成する（DIP）:<br/>
/// - テスト時のモックが可能（IElementFinder の単体テスト）<br/>
/// <br/>
/// ホスト（上位 AP）が要素検出を注入するための公開 API。
/// </remarks>
public interface IElementFinder
{
    /// <summary>現在のカーソル位置を画面物理座標で取得する。</summary>
    /// <returns>現在のカーソル位置（画面物理座標）。</returns>
    /// <remarks>カーソル位置を内部取得するパラメーターなし API（<see cref="FindWindowUnderCursor()"/> / <see cref="FindScreenUnderCursor()"/>）の入力として使用する。</remarks>
    PixelPoint GetCursorScreenPoint();

    /// <summary>カーソル位置の最前面ウィンドウを検出する（カーソル位置は内部で取得）。</summary>
    /// <returns>ウィンドウの検出結果（矩形・タイトル・識別子）。未検出時は <see cref="ElementFinderResults.IsDetected"/> = false。</returns>
    /// <remarks>
    /// カーソル位置は <see cref="GetCursorScreenPoint"/> で内部取得し、<see cref="FindWindowUnderCursor(Avalonia.PixelPoint)"/> に委譲する。<br/>
    /// 自ウィンドウ（自プロセスのウィンドウ）は実装が自己解決して除外する。
    /// </remarks>
    ElementFinderResults FindWindowUnderCursor();

    /// <summary>カーソル位置が含まれるスクリーンを検出する（カーソル位置は内部で取得）。</summary>
    /// <returns>スクリーンの検出結果（矩形・「Screen {インデックス}」）。未検出時は <see cref="ElementFinderResults.IsDetected"/> = false。</returns>
    /// <remarks>
    /// カーソル位置は <see cref="GetCursorScreenPoint"/> で内部取得し、<see cref="FindScreenUnderCursor(Avalonia.PixelPoint)"/> に委譲する。<br/>
    /// スクリーン境界は実装が自己解決する（Win32 では EnumDisplayMonitors）。
    /// </remarks>
    ElementFinderResults FindScreenUnderCursor();

    /// <summary>指定位置の最前面ウィンドウを検出する。</summary>
    /// <param name="screenPoint">判定する位置（画面物理座標）。</param>
    /// <returns>ウィンドウの検出結果（矩形・タイトル・識別子）。未検出時は <see cref="ElementFinderResults.IsDetected"/> = false。</returns>
    /// <remarks>自ウィンドウ（自プロセスのウィンドウ）は実装が自己解決して除外する。</remarks>
    ElementFinderResults FindWindowUnderCursor(PixelPoint screenPoint);

    /// <summary>指定位置が含まれるスクリーンを検出する。</summary>
    /// <param name="screenPoint">判定する位置（画面物理座標）。</param>
    /// <returns>スクリーンの検出結果（矩形・「Screen {インデックス}」）。未検出時は <see cref="ElementFinderResults.IsDetected"/> = false。</returns>
    /// <remarks>スクリーン境界は実装が自己解決する（Win32 では EnumDisplayMonitors）。</remarks>
    ElementFinderResults FindScreenUnderCursor(PixelPoint screenPoint);

    /// <summary>全スクリーンの一覧を返す（仮想スクリーンの1枚画像に対するスナップショット管理用）。</summary>
    /// <returns>全スクリーンの検出結果一覧（矩形・「Screen {インデックス}」）。列挙できない場合は空リスト。</returns>
    /// <remarks>
    /// 全スクリーンを1枚の画像（仮想スクリーン）として取得した際に、その画像内のスクリーン境界を<br/>
    /// 座標系（画像ローカル）で特定するための一覧を返す。<br/>
    /// スクリーン境界は実装が自己解決する（Win32 では EnumDisplayMonitors）。<br/>
    /// 要素の矩形は画面物理座標で統一される（座標変換は呼び出し側の責務）。
    /// </remarks>
    IReadOnlyList<ElementFinderResults> ListScreens();

    /// <summary>全ウィンドウの一覧を返す（仮想スクリーンの1枚画像に対するスナップショット管理用）。</summary>
    /// <returns>全ウィンドウの検出結果一覧（矩形・タイトル・識別子）。列挙できない場合は空リスト。</returns>
    /// <remarks>
    /// 全スクリーンを1枚の画像（仮想スクリーン）として取得した際に、その画像内のウィンドウ境界を<br/>
    /// 座標系（画像ローカル）で特定するための一覧を返す。<br/>
    /// <see cref="FindWindowUnderCursor(PixelPoint)"/> と同じ除外条件（自プロセス・不可視・無効・最小化・<br/>
    /// 0x0サイズ・タスクバー/デスクトップ）を適用する。<br/>
    /// 要素の矩形は画面物理座標で統一される（座標変換は呼び出し側の責務）。
    /// </remarks>
    IReadOnlyList<ElementFinderResults> ListWindows();
}
