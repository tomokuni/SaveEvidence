using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Feature.Loupe.Core.Drawing;
using EsUtil.Feature.Loupe.Core.Placement;

namespace EsUtil.Feature.Loupe.Core;

/// <summary>ルーペの幾何（表示レイアウトと描画 1 フレーム分）の解決を担う部分。</summary>
/// <remarks>
/// <b>本コアの公開する幾何の入口は <see cref="ResolveLayout"/> と <see cref="ResolveGeometry"/> である</b>:<br/>
/// 装飾（十字線・枠線）・文言・表示可否・描画順は描画の都合として描画層（<c>Feature.Loupe.Presenter</c> の <c>LoupePresenter</c>）が解決するため、<br/>
/// コアは値を決定した幾何だけを返す（描画 API も色も持たない）。<br/>
/// <b>本クラスは親領域のサイズを保持しない</b>: 保持すると「クランプの基準」が暗黙の状態になり、値をいつ解決したかに依存する<br/>
/// （基準は毎回引数で受け取る）。<br/>
/// <b>可視性</b>: 公開の入口は上記 2 つだけで、内部手順（表示位置のクランプ）は本 partial の private static に閉じる。
/// </remarks>
public sealed partial class Loupe
{
    // ====================================================================
    // 表示レイアウト（表示サイズ・表示位置）
    // ====================================================================

    /// <summary>親領域のサイズを通知して表示レイアウト（表示サイズ・表示位置）を解決する。</summary>
    /// <param name="parentSize">親領域のサイズ（DIP）。未確定（幅または高さが 0 以下）の場合はクランプしない。</param>
    /// <returns>表示レイアウト（表示サイズと表示位置。拡大中心が未設定の場合は位置が null）。</returns>
    /// <remarks>
    /// 処理フロー:<br/>
    /// 1. 要求サイズを親領域の最小辺へクランプして表示サイズを決める（クランプは 1 回だけ行い、要素サイズと位置で同じ値を使う）。<br/>
    /// 2. 拡大中心が未設定の場合は位置を null として返す（配置しない）。<br/>
    /// 3. 拡大中心を座標変換ポートで親領域 DIP へ変換し、オフセットを加えて親領域内へクランプした位置を返す。<br/>
    /// 親領域が未確定（未接続・レイアウト前）の場合、位置はクランプしない（クランプ可否の判断はコアが単一所有する）。
    /// </remarks>
    public LoupeLayout ResolveLayout(EsSize parentSize)
    {
        var size = parentSize.ClampToArea(Options.RequestedSize);

        if (CenterPoint is not { } center)
        {
            return new LoupeLayout(size, null);
        }

        var centerDip = _coordPort.OrIdentity().Inverse(center);
        return new LoupeLayout(size, ComputePosition(size, parentSize, centerDip, PositionOffset));
    }

    // ====================================================================
    // 描画の幾何（1 フレーム分）
    // ====================================================================

    /// <summary>現在の状態から描画 1 フレーム分の幾何を解決する。</summary>
    /// <param name="displaySize">ルーペの一辺の表示サイズ（DIP。通常は <see cref="ResolveLayout"/> の結果）。</param>
    /// <param name="availableSize">描画先の領域サイズ（物理ピクセル）。未確定（幅または高さが 0 以下）の場合はクランプしない。</param>
    /// <returns>描画の幾何（描画面・拡大画像の切り出しと描画の矩形・切り出し領域の一辺を解決済み）。</returns>
    /// <remarks>
    /// 表示サイズは呼び出し側が決めて渡す（親領域へのクランプは <see cref="ResolveLayout"/> が、<br/>
    /// 実際の描画面からの導出は描画先を知る UI 層が担う）。本クラスは親領域のサイズを保持しない<br/>
    /// （保持すると「クランプの基準」が暗黙の状態になり、値をいつ解決したかに依存する）。<br/>
    /// <b>本メソッドがコアの公開する描画の唯一の入口である</b>: 装飾（十字線・枠線）・文言・表示可否・描画順は<br/>
    /// 描画の都合として描画層（<c>Feature.Loupe.Presenter</c>）が解決するため、コアは幾何だけを返す。<br/>
    /// DIP → 物理ピクセルの換算と描画先の領域へのクランプは本メソッドが単一所有する。
    /// </remarks>
    public LoupeGeometry ResolveGeometry(double displaySize, EsSize? availableSize = null)
    {
        var zoomLevel = Options.ZoomLevel;
        var pixelSize = (int)availableSize.GetValueOrDefault().ClampToArea(DpiScaler.ScaleToPixel(displaySize, DipToDeviceScaling));

        return new LoupeGeometry(
            CenterPoint, displaySize, pixelSize, DipToDeviceScaling, zoomLevel);
    }

    // ====================================================================
    // 内部ヘルパー
    // ====================================================================

    /// <summary>ルーペの表示位置（左上）を計算する（オフセット加算・親領域内へのクランプ）。</summary>
    /// <param name="displaySize">ルーペの表示サイズ（DIP・一辺。クランプ済みの値を渡す）。</param>
    /// <param name="parentSize">親領域のサイズ（DIP）。</param>
    /// <param name="centerDip">ルーペ中心（変換後の親領域 DIP 座標）。</param>
    /// <param name="offset">表示オフセット（カーソル右下方向）。</param>
    /// <returns>表示位置（左上座標）。</returns>
    /// <remarks>
    /// 表示位置は中心（DIP）+ オフセットとし、親領域サイズ内にクランプする。<br/>
    /// 親領域のサイズが未確定（幅または高さが 0 以下 = レイアウト前・親未接続）の場合はクランプしない<br/>
    /// （「親サイズが確定しているか」の判定は <see cref="EsSize.HasArea"/>、クランプは<br/>
    /// <see cref="EsSize.ClampOffset(double, double, EsAxis)"/> が単一所有する）。<br/>
    /// 表示サイズはクランプ済みの値を渡す（要素のサイズと位置のクランプで同じ値を使い、<br/>
    /// 別々にクランプすると丸めの違いで要素が親領域からはみ出しうる）。
    /// </remarks>
    private static EsPoint ComputePosition(double displaySize, EsSize parentSize, EsPoint centerDip, double offset)
    {
        double mx = centerDip.X + offset;
        double my = centerDip.Y + offset;
        if (!parentSize.HasArea)
        {
            return new EsPoint(mx, my);
        }

        return new EsPoint(
            parentSize.ClampOffset(mx, displaySize, EsAxis.X),
            parentSize.ClampOffset(my, displaySize, EsAxis.Y));
    }
}
