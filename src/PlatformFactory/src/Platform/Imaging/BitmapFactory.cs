using Avalonia.Media.Imaging;

namespace Estable.AvaloniaUI.Platform.Imaging;

/// <summary>Avalonia Bitmap の生成ユーティリティ。</summary>
/// <remarks>
/// 特定の目的を持つ Bitmap の生成を提供する。<br/>
/// 領域切り出し（CopyPixels ベース）は <see cref="BitmapExtensions"/> が担う。<br/>
/// <br/>
/// 元は SnipCaptureUI の Imaging 内部実装として提供されていたが、汎用的な<br/>
/// Bitmap 生成のため共有プラットフォーム（本プロジェクト）へ移設した。
/// </remarks>
public static class BitmapFactory
{
    /// <summary>キャンセル時の空結果（null）を返す。</summary>
    /// <returns>常に null（0x0 の空 Bitmap を表すキャンセル意図の明示）。</returns>
    /// <remarks>
    /// Avalonia の <see cref="WriteableBitmap"/> はサイズ 1x1 以上が必須のため、<br/>
    /// 0x0 の空 Bitmap は生成できない。呼び出し側（<c>CaptureSession</c>）が null を<br/>
    /// キャンセル扱いとして処理するための戻り値メソッドである。<br/>
    /// </remarks>
    public static Bitmap? CreateEmpty() => null;
}
