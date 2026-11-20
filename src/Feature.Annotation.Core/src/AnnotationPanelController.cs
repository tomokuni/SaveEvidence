using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Annotation;

/// <summary>フローティング パネル（書式バー・右クリックの外観フライアウト）の開閉とドラッグを単一所有する。</summary>
/// <remarks>
/// <b>この型が「いつ・どこへ出すか・どう動かすか」を決める</b>: 4 つの UI 版は
/// <see cref="IAnnotationPanelHost"/> を実装するだけで、同じ動作になる。 以前は UI ごとに
/// 「ウィンドウの内側へ収める」「ポインタが外れたら閉じる」「閉じるボタンを置く」といった差が出ていた。<br/>
/// <b>閉じるのは外側のクリックだけ</b>: パネルの外をクリックするとホスト（アプリ）のウィンドウが
/// アクティブになり、パネルが非アクティブになる。 UI はその通知で <see cref="Close"/> を呼ぶ。
/// ポインタが外れただけでは閉じない（閉じるかどうかは押下で決める）。<br/>
/// <b>1 つのコントローラーが持つパネルは 1 つ</b>: 書式バーと外観フライアウトは寿命が違うため、
/// UI は種別ごとにコントローラーを持つ（同じコントローラーで別の種別を出すと入れ替わる）。
/// </remarks>
public sealed class AnnotationPanelController
{
    /// <summary>パネルのウィンドウを操作する窓口。</summary>
    private readonly IAnnotationPanelHost _host;

    /// <summary>パネルの外観（ホストへ渡す値）。</summary>
    private readonly AnnotationPanelAppearance _appearance;

    /// <summary>ドラッグ開始時のパネルの左上（物理ピクセル）。</summary>
    private EsPointInt _dragOriginWindow;

    /// <summary>ドラッグ開始時のポインターの位置（物理ピクセル）。</summary>
    private EsPointInt _dragOriginCursor;

    /// <summary>フローティング パネルの制御を初期化する。</summary>
    /// <param name="host">パネルのウィンドウを操作する窓口。</param>
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

    /// <summary>パネルを表示してから、非アクティブ化を無視する時間（ミリ秒）。</summary>
    /// <remarks>
    /// 表示直後はクリックの入力の途中でいったん非アクティブになることがある（WinUI 3 で実測
    /// 2026-11-21）。 この猶予がないと、出した直後に閉じて「表示されない」ように見える。
    /// </remarks>
    public const long DeactivateGraceMs = 500;

    /// <summary>パネルを表示した時刻（<see cref="Environment.TickCount64"/>）。</summary>
    private long _shownAtTicks;

    /// <summary>パネルを作っている最中かどうか。</summary>
    private bool _isCreating;

    /// <summary>パネルを表示する（既に出ている場合は作り直す）。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <param name="anchorScreen">左上にする位置（物理ピクセル）。 アプリのウィンドウの内側へは収めない。</param>
    /// <remarks>
    /// <b>作成中の通知では閉じない</b>: ウィンドウを作ると、その過程で非アクティブ通知が同期的に届く
    /// ことがある（実測 2026-11-21: WinUI 3 版でパネルが一瞬も表示されなかった）。 作成中は閉じる要求を
    /// 無視する。
    /// </remarks>
    public void Show(AnnotationPanelKind kind, EsPointInt anchorScreen)
        => ShowAt(kind, AnnotationPanelPlacement.ResolveInitial(anchorScreen));

    /// <summary>対象（編集中の注釈）に被さらない位置へパネルを表示する。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <param name="targetScreen">被せたくない矩形（物理ピクセル）。</param>
    /// <param name="panelSize">パネルの大きさ（物理ピクセル）。</param>
    /// <param name="gap">対象とパネルの間の余白（物理ピクセル）。</param>
    /// <remarks>
    /// <b>書式バーはこれを使う</b>: 編集中の本文の上へ重ねると文字が隠れて読めない（実測 2026-11-20）。
    /// 位置の判断は <see cref="AnnotationPanelPlacement.ResolveOutside"/> が単一所有する。
    /// </remarks>
    public void ShowOutside(
        AnnotationPanelKind kind, EsRect targetScreen, EsSizeInt panelSize, int gap)
        => ShowAt(
            kind,
            AnnotationPanelPlacement.ResolveOutside(targetScreen, panelSize, _host.WorkArea, gap));

    /// <summary>位置を指定してパネルを表示する。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <param name="screenPosition">左上の位置（物理ピクセル）。</param>
    private void ShowAt(AnnotationPanelKind kind, EsPointInt screenPosition)
    {
        if (IsOpen)
        {
            Close();
        }

        _shownAtTicks = Environment.TickCount64;
        _isCreating = true;
        try
        {
            _host.CreatePanel(kind, _appearance, screenPosition);
        }
        finally
        {
            _isCreating = false;
        }

        IsOpen = true;
        Kind = kind;
    }

    /// <summary>非アクティブになったときにパネルを閉じる（外側のクリックで閉じる規則）。</summary>
    /// <remarks>
    /// <b>表示直後は閉じない</b>: 表示した直後は入力の途中でいったん非アクティブになることがある。
    /// 猶予（<see cref="DeactivateGraceMs"/>）の内側の通知は無視する。 UI はウィンドウが非アクティブに
    /// なったときだけこれを呼ぶ（ポインタが外れただけでは呼ばない）。
    /// </remarks>
    public void CloseOnDeactivate()
    {
        if (!IsOpen || Environment.TickCount64 - _shownAtTicks < DeactivateGraceMs)
        {
            return;
        }

        Close();
    }

    /// <summary>パネルを閉じる（出ていなければ何もしない）。</summary>
    public void Close()
    {
        if (!IsOpen || _isCreating)
        {
            return;
        }

        IsOpen = false;
        Kind = null;
        IsDragging = false;
        _host.DestroyPanel();
    }

    /// <summary>パネルのドラッグを始める。</summary>
    /// <param name="cursorScreen">押下した位置（物理ピクセル）。</param>
    /// <remarks>表示していない場合は何もしない（ドラッグの状態だけが残るのを防ぐ）。</remarks>
    public void BeginDrag(EsPointInt cursorScreen)
    {
        if (!IsOpen)
        {
            return;
        }

        IsDragging = true;
        _dragOriginWindow = _host.PanelPosition;
        _dragOriginCursor = cursorScreen;
    }

    /// <summary>パネルをドラッグして動かす。</summary>
    /// <param name="cursorScreen">現在のポインターの位置（物理ピクセル）。</param>
    /// <remarks>ドラッグしていない場合は何もしない。</remarks>
    public void DragTo(EsPointInt cursorScreen)
    {
        if (!IsOpen || !IsDragging)
        {
            return;
        }

        _host.MovePanel(AnnotationPanelPlacement.ResolveDrag(
            _dragOriginWindow, _dragOriginCursor, cursorScreen));
    }

    /// <summary>パネルのドラッグを終える。</summary>
    public void EndDrag() => IsDragging = false;
}
