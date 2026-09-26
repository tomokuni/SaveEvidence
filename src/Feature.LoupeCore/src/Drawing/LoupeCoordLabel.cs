using EsUtil.Platform.Core.Geometry;
using EsUtil.SaveEvidence.Common.Imaging;
using EsUtil.Feature.LoupeCore.Setting;


namespace EsUtil.Feature.LoupeCore.Drawing;

/// <summary>ルーペの中心座標ラベル（表示する文言と配置に必要な値）を表す描画契約。</summary>
/// <param name="Text">表示する文言（<c>(x, y)</c> 形式。コアが文字列化する）。</param>
/// <param name="DisplaySize">ルーペの一辺のサイズ（DIP。ラベルの水平位置の基準）。</param>
/// <param name="FontSize">フォントサイズ（DIP）。</param>
/// <param name="CornerRadius">背景の角丸半径（DIP。0 は四角形）。</param>
/// <remarks>
/// 文言（<see cref="EsPoint.Format(int)"/>）・表示可否（<see cref="LoupeMetrics.ShouldShowCoordLabel"/>）・<br/>
/// 配置（<see cref="EsLabelLayout"/>）はコアが単一所有する。<br/>
/// <b>色は持たない</b>: 色は描画にだけ必要な設定であり、描画の設定（<c>LoupePaint</c>）が単一所有する。<br/>
/// <b>文字の描画は UI 層が担う</b>: テキストの実寸は描画 API でしか測れないため、<br/>
/// UI 層が実寸を <see cref="ComputeFrame"/> へ渡し、返された配置へテキスト要素を置く<br/>
/// （要素への写像は <see cref="EsLabelExtensions.ToElement"/> が単一所有する）。<br/>
/// <b>単位は DIP</b>である（描画領域・ルーペの表示サイズと同じ座標系。物理ピクセルへの換算は描画時に行う）。
/// </remarks>
public readonly record struct LoupeCoordLabel(
    string Text,
    double DisplaySize,
    double FontSize,
    double CornerRadius) : IEsLabel
{
    /// <summary>ラベル背景の上下の内側余白（DIP）。</summary>
    /// <remarks>左右（<see cref="EsLabelLayout.Padding"/>）より狭くする（ルーペ上端の帯へ収めるため）。</remarks>
    private const double PaddingY = 1.0;

    /// <summary>ルーペ上端からラベル背景までの距離（DIP）。</summary>
    private const double TopMargin = 2.0;

    /// <summary>ラベルの描画契約を解決する。</summary>
    /// <param name="displaySize">ルーペの一辺の表示サイズ（DIP。ラベルの水平位置の基準）。</param>
    /// <param name="center">拡大中心（ドメイン座標）。未設定の場合は null。</param>
    /// <param name="metrics">ルーペの寸法（表示可否・フォントサイズ・角丸を単一所有する）。</param>
    /// <returns>ラベルの描画契約。中心が未設定または非表示の設定の場合は null。</returns>
    /// <remarks>
    /// 1. 表示可否を <see cref="LoupeMetrics.ShouldShowCoordLabel"/> で判定する（中心が未設定なら null）。<br/>
    /// 2. 文言は <see cref="EsPoint.Format(int)"/> で文字列化する。<br/>
    /// 3. 寸法は設定（DIP）をそのまま使う（物理ピクセルへの換算は描画時に行う）。<br/>
    /// <br/>
    /// 表示サイズは要素の大きさを受け取る（描画の物理ピクセルから逆算すると、丸めにより要素と背景がずれる）。
    /// </remarks>
    public static LoupeCoordLabel? Resolve(double displaySize, EsPoint? center, LoupeMetrics metrics)
    {
        if (!metrics.ShouldShowCoordLabel(center is not null) || center is not { } centerPoint)
        {
            return null;
        }

        return new LoupeCoordLabel(
            centerPoint.Format(),
            displaySize,
            metrics.CoordLabelFontSize,
            metrics.CoordLabelCornerRadius);
    }

    /// <summary>テキストの実寸からラベルの配置（背景矩形・文字の描画原点）を求める。</summary>
    /// <param name="textSize">テキストの実寸（描画 API で測定した値・DIP）。</param>
    /// <returns>ラベルの配置（ルーペ上端の中央）。</returns>
    /// <remarks>
    /// 背景矩形と文字原点は <see cref="EsLabelLayout"/> が単一所有する（両 UI で同一の見た目）。<br/>
    /// 左右の余白はサイズラベルと共通の <see cref="EsLabelLayout.Padding"/> を使う。
    /// </remarks>
    public EsLabelFrame ComputeFrame(EsSize textSize)
    {
        var box = EsLabelLayout.ComputeTopCenterBox(
            DisplaySize, textSize, EsLabelLayout.Padding, PaddingY, TopMargin);
        return new EsLabelFrame(
            box, EsLabelLayout.ComputeOffsetTextOrigin(box.TopLeft, EsLabelLayout.Padding, PaddingY));
    }
}
