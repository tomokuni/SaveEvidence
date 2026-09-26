using EsUtil.Platform.Core.Geometry;
using EsUtil.Feature.MarqueeCore.Decoration;
using EsUtil.SaveEvidence.Common.Imaging;


namespace EsUtil.Feature.MarqueeCore;

/// <summary>ドラッグハンドルの描画内容（描画に必要な値のみをコアが決定した結果）。</summary>
/// <param name="Rects">ハンドルの矩形（DIP。ハンドル 8 方向の位置と大きさをコアが解決済み）。</param>
/// <param name="BorderThickness">ハンドル枠の線の太さ（DIP）。</param>
/// <remarks>
/// ハンドルの中心位置（8 方向）と一辺サイズの適用は <see cref="Marquee.ResolveFrame"/> が単一所有するため、<br/>
/// 描画側は本値を渡して描くだけとする（位置を描画層が計算すると、ハンドルのヒットテストと同じ式が 2 箇所になる）。<br/>
/// <b>色は含まない</b>: 色は描画にだけ必要な設定であり、描画の設定（<c>MarqueePaint</c>）が単一所有する。
/// </remarks>
public readonly record struct MarqueeHandles(
    IReadOnlyList<EsRect> Rects,
    double BorderThickness);

/// <summary>始点マーカー（十字線）の描画内容（描画に必要な値のみをコアが決定した結果）。</summary>
/// <param name="Center">十字線の中心（DIP）。</param>
/// <param name="Thickness">十字線の太さ（DIP）。</param>
/// <param name="Size">十字線の半幅（DIP）。</param>
/// <remarks><b>色は含まない</b>: 色は描画にだけ必要な設定であり、描画の設定（<c>MarqueePaint</c>）が単一所有する。</remarks>
public readonly record struct MarqueeMarker(
    EsPoint Center,
    double Thickness,
    double Size);

/// <summary>カーソル座標ラベル（表示する文言と配置に必要な値）を表す描画契約。</summary>
/// <param name="Text">表示する文言（<c>(x, y)</c> 形式。コアが文字列化する）。</param>
/// <param name="CursorDip">カーソルのローカル DIP 座標（ラベルはこの右側へ置く）。</param>
/// <param name="ClientSize">描画領域のサイズ（DIP。はみ出しのクランプに使う。0 以下の軸はクランプしない）。</param>
/// <param name="FontSize">フォントサイズ（DIP）。</param>
/// <remarks>
/// 文言（<see cref="EsPoint.Format(int)"/>）と配置（<see cref="EsLabelLayout"/> と <see cref="MarqueeOptions.CoordLabelOffset"/>）は<br/>
/// コアが単一所有する。<br/>
/// <b>色は含まない</b>: 色は描画にだけ必要な設定であり、描画の設定（<c>MarqueePaint</c>）が単一所有する。<br/>
/// <b>文字の描画は UI 層が担う</b>: テキストの実寸は描画 API でしか測れないため、<br/>
/// UI 層が実寸を <see cref="ComputeFrame"/> へ渡し、返された配置へテキスト要素を置く<br/>
/// （要素への写像は <see cref="EsLabelExtensions.ToElement"/> が単一所有する）。<br/>
/// <b>単位は DIP</b>である。
/// </remarks>
public readonly record struct MarqueeCoordLabel(
    string Text,
    EsPoint CursorDip,
    EsSize ClientSize,
    double FontSize) : IEsLabel
{
    /// <summary>テキストの実寸からラベルの配置（背景矩形・文字の描画原点）を求める。</summary>
    /// <param name="textSize">テキストの実寸（描画 API で測定した値・DIP）。</param>
    /// <returns>ラベルの配置（カーソルの右側へ置き、描画領域の端ではみ出す場合は領域内へクランプする）。</returns>
    /// <remarks>
    /// 背景の大きさ・位置・文字の原点は <see cref="EsLabelLayout"/> が単一所有する。<br/>
    /// 内側余白はサイズラベルと共通の <see cref="EsLabelLayout.Padding"/> を使う。<br/>
    /// カーソルからの離隔（<see cref="MarqueeOptions.CoordLabelOffset"/>）は本コントロール固有の値である。
    /// </remarks>
    public EsLabelFrame ComputeFrame(EsSize textSize)
    {
        var box = EsLabelLayout.ComputeCursorLabelBox(
            CursorDip, ClientSize, textSize,
            EsLabelLayout.Padding, EsLabelLayout.Padding, MarqueeOptions.CoordLabelOffset);
        return new EsLabelFrame(
            box, EsLabelLayout.ComputeOffsetTextOrigin(box.TopLeft, EsLabelLayout.Padding, EsLabelLayout.Padding));
    }
}

/// <summary>選択矩形コントロールの 1 フレーム分の描画内容（描画に必要な値のみをコアが決定した結果）。</summary>
/// <param name="Decoration">選択矩形の外側に描く装飾（暗転マスク・穴あけ枠・枠線）。描くものが無い場合は各項目が空。</param>
/// <param name="InnerDecoration">穴あけ矩形の内側に描く装飾（塗り・内枠）。描くものが無い場合は各項目が空。</param>
/// <param name="HoleRect">穴あけ矩形（選択またはハイライトの領域・DIP）。選択もハイライトも無い場合は null。</param>
/// <param name="Handles">ドラッグハンドル。null の場合は描かない（非表示の設定または選択なし）。</param>
/// <param name="Marker">始点マーカー（十字線）。null の場合は描かない（nudge 中のみ表示する）。</param>
/// <param name="SizeLabel">サイズラベル。null の場合は描かない（非表示の設定または穴あけ矩形なし）。</param>
/// <param name="CoordLabel">カーソル座標ラベル。null の場合は描かない（非表示の設定）。</param>
/// <param name="Layers">描く層とその順序（<b>描画順はコアが単一所有する</b>）。先頭から順に処理する。</param>
/// <remarks>
/// UI 実装は <see cref="Layers"/> の順に各層を処理するだけにする（描くかどうかの判断・外観値の解決はすべて済んでいる）。<br/>
/// 図形の層は自 UI の描画 API へ写し、文字の層（<see cref="SizeLabel"/> / <see cref="CoordLabel"/>）は<br/>
/// UI 要素（テキスト要素）を配置する。文字の層は必ず最後に置くため、<b>ラベルは常に最上位</b>に表示される。<br/>
/// <br/>
/// <see cref="HoleRect"/> はホストが付加情報（操作ラベル等）を対象領域へ重ねるための基準である（DIP）。<br/>
/// テキストの実寸は描画 API でしか測れないため、配置は各契約の <c>ComputeFrame</c> が実寸を受け取って返す<br/>
/// （実寸の測定と要素への反映は UI 層が担う）。
/// </remarks>
public readonly record struct MarqueeDrawContent(
    OuterDecoFrame Decoration,
    InnerDecoFrame InnerDecoration,
    EsRect? HoleRect,
    MarqueeHandles? Handles,
    MarqueeMarker? Marker,
    MarqueeSizeLabel? SizeLabel,
    MarqueeCoordLabel? CoordLabel,
    IReadOnlyList<MarqueeDrawLayer> Layers)
{
    /// <summary>文字（ラベル）を描く必要があるかどうかを取得する。</summary>
    /// <value>サイズラベルまたはカーソル座標ラベルを持つ場合は true。</value>
    public bool HasText => SizeLabel is not null || CoordLabel is not null;
}
