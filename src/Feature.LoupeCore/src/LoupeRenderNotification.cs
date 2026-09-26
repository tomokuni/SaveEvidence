using EsUtil.Platform.Core.Geometry;
using EsUtil.Feature.LoupeCore.Drawing;

namespace EsUtil.Feature.LoupeCore;

/// <summary>ルーペの描画完了時に通知する表示情報（UI 層が描画に必要とする値）。</summary>
/// <param name="CaptureTopLeft">切り出し領域の左上（ドメイン座標）。中心未設定時は null。</param>
/// <param name="Scale">ドメイン座標 → ルーペ画像ピクセル座標の変換係数（拡大率と同値）。</param>
/// <remarks>
/// UI 層は本値をホストの通知へ写す（描く層とその順序は描画内容（<see cref="LoupeDrawContent"/>）が持つ）。<br/>
/// 切り出し領域・スケールは <see cref="Loupe.ResolveNotification"/> が単一所有する。
/// </remarks>
public readonly record struct LoupeRenderNotification(EsPoint? CaptureTopLeft, double Scale);
