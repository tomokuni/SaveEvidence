using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.SaveEvidence.MewUI.Views;

/// <summary>子コントロールを親領域の任意の位置へ重ねる領域（親の残余全体を占める）。</summary>
/// <remarks>
/// <b>適用パターン</b>: カスタム Panel（子の配置を自前で決める）。<br/>
/// <br/>
/// <b>必要性</b>: MewUI の <c>Grid</c> は子の整列（<c>HorizontalAlignment</c> / <c>VerticalAlignment</c>）で位置を決め、<br/>
/// <c>Margin</c> と <c>Canvas</c> の絶対配置はいずれも指定した値が反映されない（実測）。<br/>
/// また <c>ActualWidth</c> / <c>ActualHeight</c> は測定結果（本クラスは残余を埋めるため 0）であり、<br/>
/// 実際の割り当てサイズは配置引数からしか得られない。<br/>
/// そのため「親領域のサイズを知って位置を決める」処理は配置フェーズで実行する。<br/>
/// <br/>
/// <b>位置の決定</b>: 位置は <c>Func&lt;親サイズ, 位置&gt;</c> として渡す。 呼び出し側は都度最新の基準（カーソル位置等）を<br/>
/// 参照でき、クランプ等の計算はコアの計算クラスへ委譲できる。
/// </remarks>
internal sealed class OverlayHost : Panel
{
    /// <summary>重ねる子コントロール（本クラスが配置を決める唯一の子）。</summary>
    private readonly Element _child;

    /// <summary>子のサイズ（DIP）。</summary>
    private EsSize _childSize;

    /// <summary>親領域のサイズから子の位置を求める関数。 null の場合は左上へ置く。</summary>
    private Func<EsSize, EsPoint>? _position;

    /// <summary>重ねる子を指定して領域を初期化する。</summary>
    /// <param name="child">重ねる子コントロール（本クラスの子として追加する）。</param>
    /// <remarks>
    /// 本クラスは親領域の残余全体を占めるため、ヒットテストを有効にすると下の要素（プレビュー等）へ<br/>
    /// 入力が届かなくなる。 重ねるのは表示専用の要素（ルーペ）に限るため、入力は常に下へ通す。
    /// </remarks>
    public OverlayHost(Element child)
    {
        _child = child;
        Add(child);
        IsHitTestVisible = false;
    }

    /// <summary>子のサイズと位置の決め方を設定する。</summary>
    /// <param name="childSize">子のサイズ（DIP）。</param>
    /// <param name="position">親領域のサイズから子の位置（親領域の左上を原点とする DIP）を求める関数。</param>
    /// <remarks>
    /// 位置は配置のたびに <paramref name="position"/> を評価して求めるため、<br/>
    /// 呼び出し側が保持する最新の基準（カーソル位置）が自動的に反映される。<br/>
    /// 設定のたびに配置と描画を無効化する（子は連続再描画を無効化した Skia キャンバスのため、<br/>
    /// 位置だけを変えても再描画されないと前の位置の画素が残る）。
    /// </remarks>
    public void Place(EsSize childSize, Func<EsSize, EsPoint> position)
    {
        _childSize = childSize;
        _position = position;
        InvalidateArrange();
        InvalidateVisual();
    }

    /// <summary>子の希望サイズを測定し、自身は親の残余を埋める（サイズ 0 を返す）。</summary>
    /// <param name="availableSize">親から提示された利用可能サイズ。</param>
    /// <returns>常に <c>Size.Empty</c>。</returns>
    /// <remarks>子の希望サイズを合算しない（親が割り当てた領域全体を重ねる対象にするため）。</remarks>
    protected override Size MeasureContent(Size availableSize)
    {
        _child.Measure(availableSize);
        return Size.Empty;
    }

    /// <summary>子を親領域の指定位置へ配置する。</summary>
    /// <param name="bounds">本コントロールへ割り当てられた矩形（ウィンドウ絶対座標・DIP）。</param>
    /// <remarks>位置は親領域のローカル座標のため、親の絶対座標（<c>bounds</c> の左上）を加算する。</remarks>
    protected override void ArrangeContent(Rect bounds)
    {
        var offset = _position?.Invoke(new EsSize(bounds.Width, bounds.Height)) ?? default;

        _child.Arrange(new Rect(
            bounds.X + offset.X,
            bounds.Y + offset.Y,
            _childSize.Width,
            _childSize.Height));
    }
}
