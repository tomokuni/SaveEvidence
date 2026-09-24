using EsUtil.Platform.Core.Geometry;

namespace Estable.Marquee;

/// <summary>ラベル 1 件の配置（背景矩形と文字の描画原点）を表す値。</summary>
/// <param name="Background">背景矩形（DIP。コントロールの左上を原点とする）。</param>
/// <param name="TextOrigin">文字の描画原点（左上・DIP）。</param>
/// <remarks>
/// <b>文字の描画は UI 層（またはホスト）が担う</b>: テキストの実寸は描画 API でしか測れないため、<br/>
/// 実寸を <c>ComputeFrame</c> へ渡して配置を得る。本値はその結果のみを持つ。<br/>
/// サイズラベル（<see cref="MarqueeSizeLabel"/>）とカーソル座標ラベル（<see cref="MarqueeCoordLabel"/>）が<br/>
/// 同じ型を使う（UI 層は 1 種類の配置処理で両方を扱える）。
/// </remarks>
public readonly record struct MarqueeLabelFrame(
    EsRect Background,
    EsPoint TextOrigin);
