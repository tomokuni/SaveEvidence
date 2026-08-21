using Avalonia.Media.Imaging;

namespace Estable.AvaloniaUI.ZoomView;

/// <summary>ズーム操作の対象となるホスト（ViewModel 等）の抽象。</summary>
/// <remarks>
/// <see cref="ZoomManager"/> がズーム率・画像・表示状態を読み書きするための最小インターフェース。<br/>
/// ホスト（app の MainViewModel 等）は本インターフェースを実装して <see cref="ZoomManager"/> に注入する。
/// </remarks>
public interface IZoomHost
{
    /// <summary>現在のズーム率（%）。0 = Fit（自動）。</summary>
    int ZoomPercent { get; set; }

    /// <summary>ズーム対象のプレビュー画像。null の場合は画像なし。</summary>
    Bitmap? PreviewImage { get; }

    /// <summary>ズームイン可能かどうか。</summary>
    bool CanZoomIn { get; set; }

    /// <summary>ズームアウト可能かどうか。</summary>
    bool CanZoomOut { get; set; }

    /// <summary>ステータスバー等に表示する拡大率テキスト。</summary>
    string ZoomDisplayText { get; set; }

    /// <summary>Fit 時の画像の中央寄せ / 左上寄せ。</summary>
    bool CenterAlign { get; }
}
