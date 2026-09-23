using EsUtil.Platform.Core.Geometry;

namespace Estable.CaptureSession;

/// <summary>切り出し（プレビュー内の Crop）のサイズ情報ラベルの文言を単一所有する。</summary>
/// <remarks>
/// 撮影の選択 UI は矩形の座標・サイズ・画面の表示倍率を表示するが、切り出しは<b>含まれるピクセル数</b>と<br/>
/// <b>表示中のズーム率</b>を表示する（何ピクセル切り出せるかが操作の関心であるため）。<br/>
/// この差は用途の差であり UI の差ではないため、両 UI が本クラスを使う<br/>
/// （UI ごとに書式文字列を持つと、同じ切り出し操作で表示が食い違う）。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public static class CropLabelTexts
{
    /// <summary>切り出しのサイズ情報ラベルを生成する。</summary>
    /// <param name="rect">選択矩形（論理座標 = 元画像ピクセル）。</param>
    /// <param name="zoomPercent">内包するビューポートの実効ズーム率（%）。</param>
    /// <returns><c>LT: (l, t),  RB: (r, b),  WH: (w+1, h+1), Zoom: (pz%)</c> 形式の文字列。</returns>
    /// <remarks>
    /// 1. 座標は整数へ切り捨てる（表示のみに使うため丸めの一貫性より読みやすさを優先する）。<br/>
    /// 2. 幅・高さは<b>閉区間</b>のピクセル数（終点のピクセルを含むため <c>+1</c>）で表示する<br/>
    ///    （切り出し範囲は始点・終点のピクセルを含む。<c>CropBoundsMath.ComputeCropBounds</c> と同じ規則）。<br/>
    /// 3. ズーム率は四捨五入した整数で表示する。<br/>
    /// <br/>
    /// 書式の区切りは半角スペース 2 個とする（既存の表示と一致させる）。
    /// </remarks>
    public static string FormatCropSizeLabel(EsRect rect, double zoomPercent)
    {
        var left = (int)rect.X;
        var top = (int)rect.Y;
        var right = (int)rect.Right;
        var bottom = (int)rect.Bottom;
        var width = right - left + 1;
        var height = bottom - top + 1;
        var zoom = (int)Math.Round(zoomPercent);

        return $"LT: ({left}, {top}),  RB: ({right}, {bottom}),  WH: ({width}, {height}), Zoom: ({zoom}%)";
    }

    /// <summary>切り出しのサイズ情報ラベルを解決する（未選択の場合は描画しない）。</summary>
    /// <param name="hasSelection">選択矩形があるかどうか。</param>
    /// <param name="selectionRect">選択矩形（論理座標 = 元画像ピクセル）。選択が無い場合は参照しない。</param>
    /// <param name="zoomPercent">内包するビューポートの実効ズーム率（%）。</param>
    /// <returns>サイズ情報ラベル文字列。未選択の場合は null（描画しない）。</returns>
    /// <remarks>
    /// 「未選択なら描かない」という規則を単一所有する<br/>
    /// （UI が同じ三項演算を書くと、同じ状態で表示が食い違う）。
    /// </remarks>
    public static string? ResolveSizeLabel(bool hasSelection, EsRect selectionRect, double zoomPercent)
        => hasSelection ? FormatCropSizeLabel(selectionRect, zoomPercent) : null;
}
