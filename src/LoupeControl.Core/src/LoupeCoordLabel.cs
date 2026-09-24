using EsUtil.Platform.Core.Geometry;
using DrawingColor = System.Drawing.Color;

namespace Estable.Loupe;

/// <summary>ルーペの中心座標ラベル 1 件の配置（背景矩形と文字の描画原点）。</summary>
/// <param name="Background">背景矩形（DIP。ルーペの表示領域の左上を原点とする）。</param>
/// <param name="TextOrigin">文字の描画原点（左上・DIP）。</param>
/// <remarks>UI 層（テキスト要素を配置する側）は本値に従って要素を置く（配置の計算を持たない）。</remarks>
public readonly record struct LoupeCoordLabelFrame(
    EsRect Background,
    EsPoint TextOrigin);

/// <summary>ルーペの中心座標ラベル（表示する文言と配置に必要な値）を表す描画契約。</summary>
/// <param name="Text">表示する文言（<c>(x, y)</c> 形式。コアが文字列化する）。</param>
/// <param name="DisplaySize">ルーペの一辺のサイズ（DIP。ラベルの水平位置の基準）。</param>
/// <param name="FontSize">フォントサイズ（DIP）。</param>
/// <param name="CornerRadius">背景の角丸半径（DIP。0 は四角形）。</param>
/// <param name="TextColor">文字色。</param>
/// <param name="BackgroundColor">背景色（不透明度は色のアルファに統合）。</param>
/// <remarks>
/// 文言（<see cref="CoordinateFormatter"/>）・表示可否（<see cref="LoupeAppearance.ShouldShowCoordLabel"/>）・<br/>
/// 配置（<see cref="LoupeLabelLayout"/>）はコアが単一所有する。<br/>
/// <b>文字の描画は UI 層が担う</b>: テキストの実寸は描画 API でしか測れないため、<br/>
/// UI 層が実寸を <see cref="ComputeFrame"/> へ渡し、返された配置へテキスト要素を置く。<br/>
/// <b>単位は DIP</b>である（描画領域・ルーペの表示サイズと同じ座標系。物理ピクセルへの換算は描画時に行う）。
/// </remarks>
public readonly record struct LoupeCoordLabel(
    string Text,
    double DisplaySize,
    double FontSize,
    double CornerRadius,
    DrawingColor TextColor,
    DrawingColor BackgroundColor)
{
    /// <summary>描画入力からラベルの描画契約を解決する。</summary>
    /// <param name="request">描画入力（中心・DIP の表示サイズ・外観）。</param>
    /// <returns>ラベルの描画契約。中心が未設定または非表示の設定の場合は null。</returns>
    /// <remarks>
    /// 1. 表示可否を <see cref="LoupeAppearance.ShouldShowCoordLabel"/> で判定する（中心が未設定なら null）。<br/>
    /// 2. 文言は <see cref="CoordinateFormatter"/> で文字列化する。<br/>
    /// 3. 寸法は設定（DIP）をそのまま使う（物理ピクセルへの換算は描画時に行う）。
    /// </remarks>
    public static LoupeCoordLabel? Resolve(in LoupeRenderRequest request)
    {
        var appearance = request.Appearance;
        if (!appearance.ShouldShowCoordLabel(request.Center is not null) || request.Center is not { } center)
        {
            return null;
        }

        return new LoupeCoordLabel(
            CoordinateFormatter.Format(center),
            request.DisplaySize,
            appearance.CoordLabelFontSize,
            appearance.CoordLabelCornerRadius,
            appearance.CoordLabelColor,
            appearance.CoordLabelBackgroundColor);
    }

    /// <summary>テキストの実寸からラベルの配置（背景矩形・文字の描画原点）を求める。</summary>
    /// <param name="textSize">テキストの実寸（描画 API で測定した値・DIP）。</param>
    /// <returns>ラベルの配置（ルーペ上端の中央）。</returns>
    /// <remarks>背景矩形と文字原点は <see cref="LoupeLabelLayout"/> が単一所有する（両 UI で同一の見た目）。</remarks>
    public LoupeCoordLabelFrame ComputeFrame(EsSize textSize)
        => new(
            LoupeLabelLayout.ComputeBackground(DisplaySize, textSize.Width, FontSize),
            LoupeLabelLayout.ComputeTextOrigin(DisplaySize, textSize.Width));
}
