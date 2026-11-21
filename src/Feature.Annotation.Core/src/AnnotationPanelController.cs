using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Annotation;

/// <summary>フローティング パネル（書式バー・右クリックの外観フライアウト）の開閉とドラッグを単一所有する。</summary>
/// <remarks>
/// <b>この型が「いつ・どこへ出すか・どう動かすか」を決める</b>: 4 つの UI 版は
/// <see cref="IAnnotationPanelHost"/> を実装するだけで、同じ動作になる。 以前は UI ごとに
/// 「ウィンドウの内側へ収める」「ポインタが外れたら閉じる」といった差が出ていた。<br/>
/// <b>閉じるのは外側のクリックだけ</b>: パネルの外を押したら UI が <see cref="Close"/> を呼ぶ。
/// ポインタが外れただけでは閉じない。<br/>
/// <b>1 つのコントローラーが持つパネルは 1 つ</b>: 書式バーと外観フライアウトは寿命が違うため、
/// UI は種別ごとにコントローラーを持つ。
/// </remarks>
public sealed class AnnotationPanelController
{
    /// <summary>パネルを置く窓口。</summary>
    private readonly IAnnotationPanelHost _host;

    /// <summary>パネルの外観（ホストへ渡す値）。</summary>
    private readonly AnnotationPanelAppearance _appearance;

    /// <summary>ドラッグ開始時のパネルの左上（DIP）。</summary>
    private EsPoint _dragOrigin;

    /// <summary>ドラッグ開始時のポインターの位置（DIP）。</summary>
    private EsPoint _dragOriginCursor;

    /// <summary>フローティング パネルの制御を初期化する。</summary>
    /// <param name="host">パネルを置く窓口。</param>
    /// <param name="appearance">パネルの外観（省略すると <see cref="AnnotationPanelAppearance.StyleEditor"/>）。</param>
    public AnnotationPanelController(
        IAnnotationPanelHost host,
        AnnotationPanelAppearance? appearance = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        _host = host;
        _appearance = appearance ?? AnnotationPanelAppearance.StyleEditor;
    }

    /// <summary>パネルを表示しているかどうかを取得する。</summary>
    /// <value>表示中の場合は <see langword="true"/>。</value>
    public bool IsOpen { get; private set; }

    /// <summary>表示中のパネルの種別を取得する。</summary>
    /// <value>表示していない場合は <see langword="null"/>。</value>
    public AnnotationPanelKind? Kind { get; private set; }

    /// <summary>パネルをドラッグしている最中かどうかを取得する。</summary>
    /// <value>ドラッグ中の場合は <see langword="true"/>。</value>
    public bool IsDragging { get; private set; }

    /// <summary>アンカーの位置へパネルを表示する（既に出ている場合は作り直す）。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <param name="anchor">左上にする位置（DIP）。</param>
    public void Show(AnnotationPanelKind kind, EsPoint anchor)
        => ShowAt(kind, AnnotationPanelPlacement.ResolveInitial(anchor));

    /// <summary>対象（編集中の注釈）に被さらない位置へパネルを表示する。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <param name="target">被せたくない矩形（DIP）。</param>
    /// <param name="panelSize">パネルの大きさ（DIP）。</param>
    /// <param name="gap">対象とパネルの間の余白（DIP）。</param>
    /// <remarks>
    /// <b>書式バーはこれを使う</b>: 編集中の本文の上へ重ねると文字が隠れて読めない（実測 2026-11-20）。
    /// 位置の判断は <see cref="AnnotationPanelPlacement.ResolveOutside"/> が単一所有する。
    /// </remarks>
    public void ShowOutside(AnnotationPanelKind kind, EsRect target, EsSize panelSize, double gap)
        => ShowAt(
            kind,
            AnnotationPanelPlacement.ResolveOutside(target, panelSize, _host.AvailableArea, gap));

    /// <summary>位置を指定してパネルを表示する。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <param name="position">左上の位置（DIP）。</param>
    private void ShowAt(AnnotationPanelKind kind, EsPoint position)
    {
        if (IsOpen)
        {
            Close();
        }

        _host.CreatePanel(kind, _appearance, position);
        IsOpen = true;
        Kind = kind;
    }

    /// <summary>パネルを閉じる（出ていなければ何もしない）。</summary>
    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        IsOpen = false;
        Kind = null;
        IsDragging = false;
        _host.DestroyPanel();
    }

    /// <summary>パネルのドラッグを始める。</summary>
    /// <param name="cursor">押下した位置（パネルを置く座標系の DIP）。</param>
    /// <remarks>表示していない場合は何もしない（ドラッグの状態だけが残るのを防ぐ）。</remarks>
    public void BeginDrag(EsPoint cursor)
    {
        if (!IsOpen)
        {
            return;
        }

        IsDragging = true;
        _dragOrigin = _host.PanelPosition;
        _dragOriginCursor = cursor;
    }

    /// <summary>パネルをドラッグして動かす。</summary>
    /// <param name="cursor">現在のポインターの位置（パネルを置く座標系の DIP）。</param>
    /// <remarks>ドラッグしていない場合は何もしない。</remarks>
    public void DragTo(EsPoint cursor)
    {
        if (!IsOpen || !IsDragging)
        {
            return;
        }

        _host.MovePanel(AnnotationPanelPlacement.ResolveDrag(_dragOrigin, _dragOriginCursor, cursor));
    }

    /// <summary>パネルのドラッグを終える。</summary>
    public void EndDrag() => IsDragging = false;
}
