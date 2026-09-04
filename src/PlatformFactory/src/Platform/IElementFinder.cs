using Avalonia;

namespace Estable.AvaloniaUI.Platform;

/// <summary>指定位置の要素（ウィンドウ・スクリーン）検出を抽象化するインターフェイス。</summary>
/// <remarks>
/// 指定位置のウィンドウ / スクリーンを検出し、結果（矩形・表示名・識別子）を
/// <see cref="ElementFinderResults"/> として返す。<br/>
/// 公開 API はプラットフォーム非依存の <see cref="ElementFinderResults"/> を使用し、
/// プラットフォーム固有型（Win32 ハンドル等）を公開しない。<br/>
/// 具象実装（<see cref="WinElementFinder"/>）ではなく本インターフェイスへ依存させることで、テスト時のモックを可能にする。
/// </remarks>
public interface IElementFinder
{
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
