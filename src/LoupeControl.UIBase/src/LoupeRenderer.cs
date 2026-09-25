using EsUtil.Feature.LoupeCore;
using LoupeFacade = EsUtil.Feature.LoupeCore.Loupe;

namespace EsUtil.SaveEvidence.Loupe.UIBase;

/// <summary>ルーペの描画手順の既定実装（色を 1 値で保持し、描画手順は基底クラスが単一所有する）。</summary>
/// <remarks>
/// <b>適用パターン</b>: Template Method（手順は <see cref="LoupeRendererBase"/> が所有し、本クラスは色を与える）と<br/>
/// Strategy（<see cref="ILoupeRenderer"/> の実装として差し替えられる）。<br/>
/// <br/>
/// <b>生成</b>: 色（<see cref="LoupePaint"/>）を必須引数で受け取る。色は<b>描画にだけ必要な設定</b>であり、<br/>
/// コアの計算には入らないためコアには置かない（本層が単一所有する）。<br/>
/// <b>共有しない理由</b>: 色はコントロールごとに異なる（各自の設定色で描く）。<br/>
/// 色を変更する場合はインスタンスを差し替える（同じ実装を共有したまま書き換えると、他のコントロールの見た目まで変わる）。<br/>
/// <br/>
/// <b>色の差し替え</b>: 別の色で描く場合は本クラスを新しく生成してファサード（<see cref="LoupeFacade.Renderer"/>）へ<br/>
/// 設定し直す。描画手順そのものを差し替える場合は <see cref="ILoupeRenderer"/> を実装した別の型を設定する。
/// </remarks>
public sealed class LoupeRenderer : LoupeRendererBase
{
    /// <summary>描画に使う色。</summary>
    private readonly LoupePaint _paint;

    /// <summary>描画に使う色を指定して生成する。</summary>
    /// <param name="paint">描画に使う色（<see cref="LoupePaint.Default"/> を基点に組み立てる）。null 不可。</param>
    /// <exception cref="ArgumentNullException"><paramref name="paint"/> が null の場合。</exception>
    public LoupeRenderer(LoupePaint paint)
    {
        ArgumentNullException.ThrowIfNull(paint);

        _paint = paint;
    }

    /// <inheritdoc/>
    protected override LoupePaint Paint => _paint;
}
