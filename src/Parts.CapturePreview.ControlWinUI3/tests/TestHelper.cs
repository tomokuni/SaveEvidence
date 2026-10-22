using EsUtil.Platform.Core.Imaging;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.WinUI3.Tests;

/// <summary>テストで共用するユーティリティ。</summary>
/// <remarks>
/// カテゴリごとのテスト（<c>Tests.Crop</c> / <c>Tests.SelectionOverlay</c> / <c>Tests.ZoomView</c>）は<br/>
/// 親名前空間（<c>Tests</c>）の本型をそのまま参照できる（同名クラスの複製を作らない）。<br/>
/// 描画の検証は画素ではなく矩形の列（<c>DrawList</c>）で行うため、描画先（キャンバス）の用意は不要である。
/// </remarks>
internal static class TestHelper
{
    /// <summary>指定サイズの単色画像（読み取りの面）を生成する。</summary>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <param name="color">塗りつぶす色。</param>
    /// <returns>生成した画像（読み取りの面）。 破棄は呼び出し側。</returns>
    /// <remarks>
    /// テスト画像は生成後に書き換えない（表示の入力）ため、凍結して 0 コピーで読み取り専用の面へ渡す。
    /// </remarks>
    public static EsReadOnlyBitmap CreateBitmap(int width, int height, DrawingColor color)
    {
        using var created = EsBitmap.Create(width, height, color);
        created.SetReadOnly();
        return EsReadOnlyBitmap.FromBitmap(created);
    }
}
