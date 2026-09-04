using Avalonia.Media.Imaging;

namespace Estable.AvaloniaUI.ZoomView;

/// <summary>ズーム操作の対象となるホスト（ViewModel 等）の抽象。</summary>
/// <remarks>
/// <see cref="ZoomManager"/> がズーム対象の画像・表示状態を読み書きするための最小インターフェース。<br/>
/// ホスト（上位 AP の ViewModel 等）は本インターフェースを実装して <see cref="ZoomManager"/> に注入する。<br/>
/// <br/>
/// <b>最小限の提供</b>: ホストはズーム対象の画像と Fit 時の配置設定のみを提供する。<br/>
/// ズーム率・可否・表示テキストの状態はコントロール（<see cref="ZoomManager"/> / <see cref="ZoomableView"/>）が
/// 単一所有し、<see cref="ZoomManager.ZoomChanged"/> でホストへ通知する（ズーム状態の単一所有）。
/// </remarks>
public interface IZoomHost
{
    /// <summary>ズーム対象のプレビュー画像。null の場合は画像なし。</summary>
    Bitmap? PreviewImage { get; }

    /// <summary>ステータスバー等に表示する拡大率テキスト。</summary>
    string ZoomDisplayText { get; set; }

    /// <summary>Fit 時の画像の中央寄せ / 左上寄せ。</summary>
    bool CenterAlign { get; }
}
