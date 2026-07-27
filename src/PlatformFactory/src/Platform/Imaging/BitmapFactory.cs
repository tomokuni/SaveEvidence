using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

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
    /// <summary>サイズ 0x0 の空 Bitmap を生成する（キャンセル時の戻り値用）。生成に失敗した場合は null。</summary>
    /// <returns>0x0 の空 Bitmap。生成できない環境では null。</returns>
    /// <remarks>
    /// 0x0 の WriteableBitmap がプラットフォームで生成できない場合に備えて例外を捕捉し、<br/>
    /// null を返す（呼び出し側でキャンセル扱いとして処理する）。<br/>
    /// </remarks>
    public static Bitmap? CreateEmpty()
    {
        try
        {
            return new WriteableBitmap(new PixelSize(0, 0), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        }
        catch
        {
            // 0x0 の WriteableBitmap が生成できない環境では null を返す（呼び出し側でキャンセル扱い）
            return null;
        }
    }
}
