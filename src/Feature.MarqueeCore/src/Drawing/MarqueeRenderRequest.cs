using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Feature.MarqueeCore.Setting;

namespace EsUtil.Feature.MarqueeCore.Drawing;

/// <summary>1 フレーム分の描画内容を解決するための入力（設定・状態と、描画時に決まる値を 1 つにまとめた値）。</summary>
/// <param name="Options">選択矩形の設定（ハンドル・十字マーカー・カーソル座標ラベルの外観）。</param>
/// <param name="DecoOptions">装飾（暗転マスク・穴あけ枠・内側の塗り）の設定。</param>
/// <param name="ClientSize">描画領域のサイズ（DIP。原点は常に 0）。</param>
/// <param name="SelectionRect">選択矩形（ドメイン座標）。選択なしの場合は null（<b>null が「選択なし」を意味する</b>）。</param>
/// <param name="MarkerActive">始点マーカー（十字線）を描くかどうか（nudge 中かつ十字線が可視の場合に true）。</param>
/// <param name="MarkerPosition">始点マーカーの中心（ドメイン座標）。</param>
/// <param name="MousePosition">直近のマウス位置（ドメイン座標。カーソル座標ラベルの基準）。</param>
/// <param name="Port">論理座標 → DIP の座標変換ポート。null の場合は <paramref name="DomainToDipScaling"/> を等倍スケールとして適用する。</param>
/// <param name="DomainToDipScaling">ポート未指定の場合に適用する倍率（0 以下・等倍は等価変換）。</param>
/// <param name="SizeLabelOverrideProvider">サイズラベルの文言プロバイダ。null 可（未指定・空文字の場合は自動生成する）。</param>
/// <param name="DisplayScaleScaling">サイズラベルの自動生成に使う表示スケーリング（0 以下は等倍）。</param>
/// <param name="SizeLabelDisplayScalingProvider">サイズラベルの表示スケーリングを矩形ごとに解決するプロバイダ（引数はドメイン座標の矩形）。null 可（その場合は <paramref name="DisplayScaleScaling"/> を使う）。</param>
/// <remarks>
/// 設定と状態の単一値（<see cref="Options"/> / <see cref="DecoOptions"/>）に、描画時にしか決まらない値<br/>
/// （描画領域のサイズ・座標変換ポート・倍率・文言プロバイダ）を加えたものである。<br/>
/// <b>UI 層は本値を自分で組み立てない</b>: セッションの状態を写す組み立て順は <see cref="Marquee.CreateRequest"/> が単一所有する。<br/>
/// UI 層は本値を <see cref="Marquee.ResolveContent"/> へ渡すだけで、描画に必要な値の意味・単位を持たない。
/// </remarks>
public readonly record struct MarqueeRenderRequest(
    MarqueeOptions Options,
    Decoration.MarqueeDecoOptions DecoOptions,
    EsSize ClientSize,
    EsRect? SelectionRect,
    bool MarkerActive,
    EsPoint MarkerPosition,
    EsPoint MousePosition,
    ICoordTransform? Port,
    double DomainToDipScaling,
    Func<string?>? SizeLabelOverrideProvider,
    double DisplayScaleScaling,
    Func<EsRect, double>? SizeLabelDisplayScalingProvider = null);
