using System.ComponentModel;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Logging;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.ZoomView;
using EsUtil.Ui.WinForms.Interop.Imaging;
using Microsoft.Extensions.Logging;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.WinForms.ZoomView;

/// <summary>ズーム・パン可能な画像表示コントロール（WinForms 実装。 GDI+ へ直接描画する）。</summary>
/// <remarks>
/// <b>UI 層の責務は 3 つだけ</b>である（判断・値・計算はコアの <see cref="ZoomViewSession"/> が単一所有する）。<br/>
/// 1. <b>入力ブリッジ</b>: マウス・ホイールのイベントをコア型へ変換してセッションへ渡し、返された効果（再描画・ポインタキャプチャ）を適用する。<br/>
/// 2. <b>描画</b>: セッションの値（画像表示矩形・スクロールバー・外観）を GDI+ の描画 API へ写す（外観値・判断を持たない）。<br/>
/// 3. <b>ホスト結線</b>: プロパティ・DPI 追従・マウス キャプチャ・再描画。<br/>
/// <br/>
/// <b>座標系</b>: コアのセッションは DIP で値を扱う。 WinForms のクライアント座標とマウス座標は物理ピクセルであるため、<br/>
/// 本コントロールが <see cref="DpiScaler"/> で DIP へ換算してからセッションへ渡す（逆に描画は DIP の値を<br/>
/// 描画面のスケーリングで拡大して写す）。<br/>
/// <br/>
/// <b>中間ビットマップを持たない</b>: 取得済みの画像は <see cref="Source"/> の設定時に 1 度だけ GDI+ のビットマップへ複製し、<br/>
/// 描画のたびに複製しない（GDI+ はコアの画素を直接描けないため。 複製は全面の画素のコピーであり高価になる）。<br/>
/// <br/>
/// <b>スクロールバーはオーバーレイとして自前描画する</b>（スクロール可否・配置・太さはコアが確定する）。
/// </remarks>
public sealed class ZoomViewControl : Control
{
    /// <summary>ズームの状態・判断・計算を単一所有するセッション。</summary>
    private readonly ZoomViewSession _session;

    /// <summary>表示する画像（コアの画像。 所有権はホストにある）。</summary>
    private EsReadOnlyBitmap? _source;

    /// <summary>描画に使う GDI+ のビットマップ（<see cref="Source"/> の設定時に 1 度だけ作る）。</summary>
    private Bitmap? _sourceBitmap;

    /// <summary>表示 DIP を物理ピクセルへ変換するスケーリング係数（DPI の通知から保持する値）。</summary>
    private double _clientToPixelScaling = DpiScaler.StandardScaling;

    /// <summary>ログの接頭辞（未設定は null。 既定値は定義クラスが持つ）。</summary>
    private string? _logPrefix;

    /// <summary>ズームビューコントロールを既定の設定で初期化する。</summary>
    public ZoomViewControl() : this(ZoomOptions.Default) { }

    /// <summary>ズームビューコントロールを指定の設定で初期化する。</summary>
    /// <param name="config">ズーム遷移の設定。 null の場合は既定値。</param>
    /// <remarks>
    /// 画像は静止画として扱うため、再描画はセッションの効果（<see cref="ZoomFeedback"/>）に従って要求する。<br/>
    /// セッションの通知（ズーム状態・ビューポートの変化）を購読し、自 UI の公開イベントへ転送する。
    /// </remarks>
    public ZoomViewControl(ZoomOptions? config)
    {
        SetStyle(
            ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw,
            true);

        _session = new ZoomViewSession(config);
        _session.Subscribe(OnSessionNotified);
    }

    /// <summary>画像の周囲（外側）へ影などを描く描画フックを取得または設定する。</summary>
    /// <value>描画フック。 null の場合は何も描かない（既定）。</value>
    /// <remarks>
    /// <b>同じ描画面へ描くための入口</b>である: 画像の外側へ別のコントロールを重ねると、画像の端の<br/>
    /// 画素（画像と背景の混色）の下地が背景になり、画像と影の間に白い線として見える（実測）。<br/>
    /// 本フックは<b>下地の塗りつぶし後・画像の描画前</b>に呼ばれるため、混色の下地を影にできる。<br/>
    /// 引数は画像の表示矩形（DIP）と DIP → 物理ピクセルの倍率。 拡大は適用しない<br/>
    /// （画素の格子に一致させるため。 画素への変換は描画計画が担う）。
    /// </remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Action<Graphics, RectangleF, double>? SurroundRenderer { get; set; }
    /// <summary>ズーム率が変更されたときに発火する（実効ズーム率・Fit 状態・ズーム可否を通知する）。</summary>
    public event EventHandler<ZoomChangedEventArgs>? ZoomChanged;

    /// <summary>ビューポート（パン・ズーム・表示サイズ）が変化したときに発火する。</summary>
    /// <remarks>
    /// ホストが重ねたオーバーレイ（選択枠・ルーペ等）を追従させるために使用する。<br/>
    /// ズーム率の変更・パン・ビューポートのサイズ変更で発火する。
    /// </remarks>
    public event EventHandler? ViewportChanged;

    /// <summary>ズームの状態・判断・計算を単一所有するセッションを取得する（テスト用アクセサ）。</summary>
    /// <value>セッション。 ホストは本プロパティを使わず、コントロールの公開 API を使用する。</value>
    internal ZoomViewSession Session => _session;

    /// <summary>ズーム操作ログの出力先を取得または設定する。</summary>
    /// <value>ロガー。 null を設定した場合は出力なしとして扱う。</value>
    /// <remarks>null の吸収（<c>?? NullLogger.Instance</c>）の規則はコアのセッションが単一所有する。</remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ILogger Log
    {
        get => _session.Log;
        set => _session.Log = value;
    }

    /// <summary>ログの接頭辞を取得または設定する（ロガーと一緒に設定する）。</summary>
    /// <value>接頭辞（例: <c>[CapturePreview]</c>）。 null または空白を設定すると既定値へ戻す。</value>
    /// <remarks>本アセンブリのログ定義（<see cref="CapturePreviewLogMessage"/>）の接頭辞を設定する。</remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? LogPrefix
    {
        get => _logPrefix;
        set
        {
            _logPrefix = value;
            CapturePreviewLogMessage.SetPrefix(value);
        }
    }

    /// <summary>表示する画像（ズーム対象）を取得または設定する。</summary>
    /// <value>ズーム対象の画像。 null または破棄済みの場合は何も表示しない。 設定すると Fit 表示へリセットする。</value>
    /// <remarks>
    /// 設定すると GDI+ のビットマップへ複製し直し、直前の複製を破棄する（同じインスタンスの再設定では複製しない）。<br/>
    /// 画像の所有権はホストにある（本コントロールは複製だけを所有し、引数の画像は破棄しない）。
    /// </remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public EsReadOnlyBitmap? Source
    {
        get => _source;
        set
        {
            if (ReferenceEquals(_source, value))
            {
                return;
            }

            _source = value;
            ReplaceBitmap(value is { IsDisposed: false } source ? EsBitmapDrawingInterop.ToDrawingBitmap(source) : null);
            Apply(_session.SetSource(_sourceBitmap is not null, SourceSize));
        }
    }

    /// <summary>画像の中央寄せ / 左上寄せを取得または設定する（画像がビューポートより小さい軸にのみ作用する）。</summary>
    /// <value>true の場合は中央寄せ、false の場合は左上寄せ。</value>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool CenterAlign
    {
        get => _session.CenterAlign;
        set => Apply(_session.SetCenterAlign(value));
    }

    /// <summary>表示 DIP を物理ピクセルへ変換するスケーリング係数を取得または設定する。</summary>
    /// <value>スケーリング係数。 DPI 変更時に自動更新される（描画面から実測できない場合の代替値）。</value>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double ClientToPixelScaling
    {
        get => _clientToPixelScaling;
        set
        {
            _clientToPixelScaling = value;
            Apply(_session.SetViewport(CurrentViewport()));
        }
    }

    /// <summary>ズームビューの外観（下地・スクロールバー）を取得または設定する。</summary>
    /// <value>外観の単一値。 既定は暗色の下地・スクロールバー表示。</value>
    /// <remarks>
    /// 外観はコア（<see cref="ZoomViewAppearance"/>）が単一所有する。<br/>
    /// 本プロパティはコアへ渡す窓口であり、本コントロールは外観値を保持しない（同値の再設定では再描画しない）。
    /// </remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ZoomViewAppearance Appearance
    {
        get => _session.Appearance;
        set => Apply(_session.SetAppearance(value));
    }

    /// <summary>現在の実効ズーム率（%）を取得または設定する。</summary>
    /// <value>Fit 中は実効倍率（ビューポートと元画像サイズから算出）、固定倍率中はズーム率。 0 にはならない。</value>
    /// <remarks>
    /// <b>setter</b> は 0 より大きい値のみ受け付ける（Fit へは <see cref="ZoomFit"/> を使用する）。<br/>
    /// 設定値は <see cref="ZoomOptions"/> の範囲へクランプされ、ズーム中心を維持したまま倍率を変更する。
    /// </remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double ZoomPercent
    {
        get => _session.ZoomPercent;
        set => Apply(_session.SetFixedZoom(value));
    }

    /// <summary>現在 Fit 表示中かどうかを取得する。</summary>
    /// <value>Fit 表示中なら true。</value>
    /// <remarks><see cref="ZoomPercent"/> は実効値のため 0 にならない。 Fit 判定には本プロパティを使用する。</remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsFit => _session.IsFit;

    /// <summary>ズームイン可能かどうかを取得する。</summary>
    /// <value>ズームイン可能なら true。 画像未設定時は false。</value>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool CanZoomIn => _session.CanZoomIn;

    /// <summary>ズームアウト可能かどうかを取得する。</summary>
    /// <value>ズームアウト可能なら true。 画像未設定時は false。</value>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool CanZoomOut => _session.CanZoomOut;

    /// <summary>画像の表示サイズ（ビューポート座標・DIP）を取得する。</summary>
    /// <returns>表示サイズ。 画像未設定時は default。</returns>
    public EsSize DisplaySize()
    {
        try
        {
            return _session.DisplaySize;
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 公開ファサードとして内部の例外を集約して記録し、呼び出し側へ伝搬する（例外処理規約 §4）
            Log.LogOperationError(ex, nameof(DisplaySize));
            throw;
        }
    }

    /// <summary>画像の表示矩形（ビューポート座標・DIP）を取得する。</summary>
    /// <returns>画像の表示矩形。 画像未設定時は default。</returns>
    public EsRect ImageRectViewport()
    {
        try
        {
            return _session.ImageRect;
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 公開ファサードとして内部の例外を集約して記録し、呼び出し側へ伝搬する（例外処理規約 §4）
            Log.LogOperationError(ex, nameof(ImageRectViewport));
            throw;
        }
    }

    /// <summary>ビューポート座標をコンテンツ座標（元画像ピクセル）へ変換する（画像表示矩形の原点基準）。</summary>
    /// <param name="viewportPos">ビューポート座標（DIP）。</param>
    /// <returns>コンテンツ座標（元画像ピクセル）。</returns>
    /// <remarks>Fit 中央寄せの原点補正を含む。 変換はコアのセッションへ委譲する。</remarks>
    public EsPoint ClientToDisplayedSource(EsPoint viewportPos)
    {
        try
        {
            return _session.ClientToDisplayedSource(viewportPos);
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 公開ファサードとして内部の例外を集約して記録し、呼び出し側へ伝搬する（例外処理規約 §4）
            Log.LogOperationError(ex, nameof(ClientToDisplayedSource));
            throw;
        }
    }

    /// <summary>コンテンツ座標（元画像ピクセル）をビューポート座標へ変換する（画像表示矩形の原点基準）。</summary>
    /// <param name="contentPos">コンテンツ座標（元画像ピクセル）。</param>
    /// <returns>ビューポート座標（DIP）。</returns>
    /// <remarks>Fit 中央寄せの原点補正を含む。 変換はコアのセッションへ委譲する。</remarks>
    public EsPoint DisplayedSourceToClient(EsPoint contentPos)
    {
        try
        {
            return _session.DisplayedSourceToClient(contentPos);
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 公開ファサードとして内部の例外を集約して記録し、呼び出し側へ伝搬する（例外処理規約 §4）
            Log.LogOperationError(ex, nameof(DisplayedSourceToClient));
            throw;
        }
    }

    /// <summary>ホイールの回転量でパンする（回転量から移動量への換算を単一所有する）。</summary>
    /// <param name="delta">ホイールの回転量（X = 横 / Y = 縦。 1 ノッチ = 1.0）。</param>
    /// <param name="shift">Shift 併用かどうか（縦回転を横パンとして扱う）。</param>
    /// <remarks>ホストが他のコントロール（選択オーバーレイ等）で受けたホイールを転送する場合にも使用する。</remarks>
    public void PanByWheel(EsVector delta, bool shift)
    {
        try
        {
            Apply(_session.PanByWheel(delta, shift));
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 公開ファサードとして内部の例外を集約して記録し、呼び出し側へ伝搬する（例外処理規約 §4）
            Log.LogOperationError(ex, nameof(PanByWheel));
            throw;
        }
    }

    /// <summary>Ctrl+ホイールのズームを適用する（ホイール操作の公開 API）。</summary>
    /// <param name="zoomIn">true = ズームイン / false = ズームアウト。</param>
    /// <param name="pointerPosition">ズーム中心とする位置（DIP）。 null の場合はビューポート中心。</param>
    /// <remarks>ホストが他のコントロールで受けたホイールを転送する場合にも使用する。</remarks>
    public void ZoomByWheel(bool zoomIn, EsPoint? pointerPosition = null)
        => Apply(_session.ZoomByWheel(zoomIn, pointerPosition));

    /// <summary>ズームインする（段階遷移。 次のレベルへ移動する）。</summary>
    /// <remarks>移動先がなければ何もしない（現在のズーム率を維持する）。</remarks>
    public void ZoomIn()
    {
        try
        {
            Apply(_session.ZoomIn());
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 公開ファサードとして内部の例外を集約して記録し、呼び出し側へ伝搬する（例外処理規約 §4）
            Log.LogOperationError(ex, nameof(ZoomIn));
            throw;
        }
    }

    /// <summary>ズームアウトする（段階遷移。 前のレベルへ移動する）。</summary>
    /// <remarks>移動先がなければ何もしない（現在のズーム率を維持する）。</remarks>
    public void ZoomOut()
    {
        try
        {
            Apply(_session.ZoomOut());
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 公開ファサードとして内部の例外を集約して記録し、呼び出し側へ伝搬する（例外処理規約 §4）
            Log.LogOperationError(ex, nameof(ZoomOut));
            throw;
        }
    }

    /// <summary>Fit 表示（ビューポートに収まる自動倍率）へ戻す。</summary>
    /// <remarks>Fit へ移行する唯一の経路（<see cref="ZoomPercent"/> の setter は 0 を受け付けない）。</remarks>
    public void ZoomFit()
    {
        try
        {
            Apply(_session.ZoomFit());
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 公開ファサードとして内部の例外を集約して記録し、呼び出し側へ伝搬する（例外処理規約 §4）
            Log.LogOperationError(ex, nameof(ZoomFit));
            throw;
        }
    }

    /// <summary>描画に使うスケーリング係数（DIP → 物理ピクセル）を解決する。</summary>
    /// <returns>スケーリング係数（0.1〜8.0 へ補正済み）。</returns>
    /// <remarks>
    /// 描画面の実サイズ（物理ピクセル）と、同じ辺の割り当て領域（DIP。 コントロールの DPI から求める）の比を採る。<br/>
    /// 割り当て領域が確定しない場合（クライアント領域が 0 の間）は、DPI の通知から保持している係数を使う。
    /// </remarks>
    internal double ResolveClientScaling()
        => DpiScaler.ResolveClientScale(
            ClientSize.Width,
            DpiScaler.ToDip(ClientSize.Width, DpiScaler.ToScaling(DeviceDpi)),
            _clientToPixelScaling);

    /// <summary>画像を描画する（下地 → 画像 → スクロールバーの順）。</summary>
    /// <param name="e">描画のイベント引数（描画面を持つ）。</param>
    /// <remarks>
    /// 描画面へ DPI の拡大を適用してから DIP 座標で描画する（コアの計算と座標系を一致させる）。<br/>
    /// 描画順は本メソッドが単一所有し、各描画内容はコアの値に従う。
    /// </remarks>
    protected override void OnPaint(PaintEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPaint(e);

        var appearance = _session.Appearance;
        e.Graphics.Clear(appearance.BackgroundColor);

        var scaling = ResolveClientScaling();

        // 画像の外側（影など）を先に描く（画像の端の混色の下地を影にする。 拡大は適用しない）
        if (SurroundRenderer is { } surround)
        {
            var area = _session.ImageRect;
            surround(
                e.Graphics,
                RectangleF.FromLTRB(
                    (float)area.X,
                    (float)area.Y,
                    (float)(area.X + area.Width),
                    (float)(area.Y + area.Height)),
                scaling);
        }

        var state = e.Graphics.Save();
        e.Graphics.ScaleTransform((float)scaling, (float)scaling);
        try
        {
            ZoomGraphicsRenderer.Render(e.Graphics, _sourceBitmap, _session);
            DrawScrollBars(e.Graphics, appearance);
        }
        finally
        {
            e.Graphics.Restore(state);
        }
    }

    /// <summary>ビューポートのサイズ変更をセッションへ通知する。</summary>
    /// <param name="e">イベントの情報。</param>
    /// <remarks>Fit 実効倍率・実効ズーム率はビューポートに依存するため、セッションがキャッシュを破棄して再通知する。</remarks>
    protected override void OnResize(EventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnResize(e);

        Apply(_session.SetViewport(CurrentViewport()));
    }

    /// <summary>DPI の変更時に表示スケーリング係数を追従させる。</summary>
    /// <param name="deviceDpiOld">変更前の DPI。</param>
    /// <param name="deviceDpiNew">変更後の DPI。</param>
    /// <remarks>モニター間の移動で DPI が変わっても物理ピクセル基準の見た目を保つ。</remarks>
    protected override void RescaleConstantsForDpi(int deviceDpiOld, int deviceDpiNew)
    {
        base.RescaleConstantsForDpi(deviceDpiOld, deviceDpiNew);
        ClientToPixelScaling = DpiScaler.ToScaling(deviceDpiNew);
    }

    /// <summary>ハンドルの生成時にスケーリング係数を DPI へ追随させる。</summary>
    /// <param name="e">イベントの情報。</param>
    /// <remarks>
    /// ハンドルの生成前は DPI を取得できないため、生成時に実効倍率を解決して保持する<br/>
    /// （DPI の変更が届かないまま描画を始めると、図形が 100% で描かれて画像とずれる）。
    /// </remarks>
    protected override void OnHandleCreated(EventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnHandleCreated(e);

        ClientToPixelScaling = DpiScaler.ToScaling(DeviceDpi);
    }

    /// <summary>左ボタン押下でパンまたはスクロールバーのドラッグを開始する。</summary>
    /// <param name="e">マウスの情報。</param>
    /// <remarks>
    /// スクロールバーのサム・トラック上ならドラッグを開始する（パンより優先する。 判断はコア）。<br/>
    /// それ以外は grab 方式のパンを開始する（掴んだ位置のコンテンツ座標をセッションが保存する）。
    /// </remarks>
    protected override void OnMouseDown(MouseEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnMouseDown(e);

        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        Focus();
        var position = ToDipPoint(e.Location);
        Apply(_session.TryBeginScrollBarDrag(position)
            ? new ZoomFeedback { ShouldCapturePointer = true }
            : _session.Press(position));
    }

    /// <summary>ドラッグ中は掴んだ位置がポインタに追従するようパンする（スクロールバーのドラッグも処理する）。</summary>
    /// <param name="e">マウスの情報。</param>
    /// <remarks>ドラッグ中でない場合（単なる移動）は何もしない（セッションが空の効果を返す）。</remarks>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnMouseMove(e);

        var position = ToDipPoint(e.Location);
        var feedback = _session.IsDraggingScrollBar
            ? _session.UpdateScrollBarDrag(position)
            : _session.Move(position);

        Apply(feedback);
    }

    /// <summary>ボタン解放でパンまたはスクロールバーのドラッグを終了する。</summary>
    /// <param name="e">マウスの情報。</param>
    protected override void OnMouseUp(MouseEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnMouseUp(e);

        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        Apply(_session.IsDraggingScrollBar ? _session.ReleaseScrollBarDrag() : _session.Release());
    }

    /// <summary>ホイールでパンまたはズームする。</summary>
    /// <param name="e">ホイールの情報。</param>
    /// <remarks>
    /// Ctrl+ホイールはポインタ位置をズーム中心としたズーム、それ以外はパン（Shift 併用で横パン）とする（判断はコア）。<br/>
    /// WinForms の <see cref="MouseEventArgs.Delta"/> は 1 ノッチ = 120 であるため、コアの契約（1 ノッチ = 1.0）へ換算する。<br/>
    /// 横方向の回転（横ホイール）は WinForms の <see cref="MouseEventArgs"/> から取得できないため、常に 0 とする。
    /// </remarks>
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnMouseWheel(e);

        var delta = e.Delta / (double)SystemInformation.MouseWheelScrollDelta;
        var intent = ZoomWheelIntent.Resolve(
            (ModifierKeys & Keys.Control) != 0, (ModifierKeys & Keys.Shift) != 0, delta);

        if (intent.IsZoom)
        {
            ZoomByWheel(intent.ZoomIn, ToDipPoint(e.Location));
        }
        else
        {
            PanByWheel(new EsVector(0, delta), intent.ScrollHorizontal);
        }
    }

    /// <summary>保持している資源を解放する。</summary>
    /// <param name="disposing">マネージドの資源も解放する場合は true。</param>
    /// <remarks>複製したビットマップは本コントロールが所有するため破棄する（引数の画像は破棄しない）。</remarks>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ReplaceBitmap(null);
        }

        base.Dispose(disposing);
    }

    /// <summary>オーバーレイ スクロールバーを描画する。</summary>
    /// <param name="graphics">描画先（DIP 座標で描画できる状態であること）。</param>
    /// <param name="appearance">外観（スクロールバーの太さ・余白・色）。</param>
    /// <remarks>
    /// スクロール不要な軸（画像がビューポートに収まる軸）には描画しない（セッションが null を返す）。<br/>
    /// トラックの表示可否はコア（<see cref="ZoomViewAppearance.HasScrollBarTrack"/>）が単一所有する。<br/>
    /// サムはトラックの後に描く（サムがトラックの上に載る）。
    /// </remarks>
    private void DrawScrollBars(Graphics graphics, ZoomViewAppearance appearance)
    {
        var (horizontal, vertical) = _session.CurrentScrollBars();
        if (horizontal is null && vertical is null)
        {
            return;
        }

        var drawTrack = appearance.HasScrollBarTrack;
        var borderThickness = appearance.HasScrollBarBorder ? appearance.ScrollBarBorderThickness : 0;
        ZoomGraphicsRenderer.RenderScrollBar(
            graphics,
            horizontal,
            appearance.ScrollBarTrackColor,
            appearance.ScrollBarColor,
            drawTrack,
            appearance.ScrollBarBorderColor,
            borderThickness);
        ZoomGraphicsRenderer.RenderScrollBar(
            graphics,
            vertical,
            appearance.ScrollBarTrackColor,
            appearance.ScrollBarColor,
            drawTrack,
            appearance.ScrollBarBorderColor,
            borderThickness);
    }

    /// <summary>セッションの通知を自 UI の公開イベントへ転送する。</summary>
    /// <param name="notice">通知（種別と通知時点の値）。</param>
    /// <remarks>判断を含まない種別 → イベントの対応表とする（通知値はコアが確定済み）。</remarks>
    private void OnSessionNotified(ZoomNotice notice)
    {
        switch (notice.Kind)
        {
            case ZoomNoticeKind.ZoomChanged:
                RaiseZoomChanged(notice.State);
                break;
            case ZoomNoticeKind.ViewportChanged:
                RaiseViewportChanged();
                break;
        }
    }

    /// <summary>ズーム率の変更をホストへ通知する。</summary>
    /// <param name="state">通知時点のズーム状態。</param>
    private void RaiseZoomChanged(ZoomState state)
    {
        var handler = ZoomChanged;
        handler?.Invoke(this, new ZoomChangedEventArgs(state));
    }

    /// <summary>ビューポートの変化をホストへ通知する。</summary>
    private void RaiseViewportChanged()
    {
        var handler = ViewportChanged;
        handler?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>元画像のサイズ（コンテンツ座標）を取得する。</summary>
    /// <returns>元画像のサイズ。 画像未設定時は default。</returns>
    private EsSize SourceSize
    {
        get
        {
            if (_source is not { } source)
            {
                return default;
            }

            var size = source.Size;
            return new EsSize(size.Width, size.Height);
        }
    }

    /// <summary>ビューポートのサイズ（DIP）を取得する。</summary>
    /// <returns>ビューポートのサイズ。 未レイアウト時は default。</returns>
    private EsSize CurrentViewport()
    {
        var size = DpiScaler.ToDip(new EsSize(ClientSize.Width, ClientSize.Height), ResolveClientScaling());
        return size.Width > 0 && size.Height > 0 ? size : default;
    }

    /// <summary>物理ピクセルのクライアント座標を DIP の座標へ変換する。</summary>
    /// <param name="location">クライアント座標（物理ピクセル）。</param>
    /// <returns>DIP の座標（コアのセッションが扱う座標系）。</returns>
    private EsPoint ToDipPoint(Point location)
    {
        var scaling = ResolveClientScaling();
        return new EsPoint(DpiScaler.ToDip(location.X, scaling), DpiScaler.ToDip(location.Y, scaling));
    }

    /// <summary>セッションが返した効果を適用する。</summary>
    /// <param name="feedback">セッションが返した効果（再描画・ポインタキャプチャ）。</param>
    /// <remarks>適用順（再描画 → キャプチャ／解放）は効果の型が単一所有する（UI 層で判断しない）。</remarks>
    private void Apply(ZoomFeedback feedback)
    {
        feedback.ApplyVisual(Invalidate);
        feedback.ApplyPointer(() => Capture = true, () => Capture = false);
    }

    /// <summary>表示に使うビットマップを差し替える（直前の複製を破棄する）。</summary>
    /// <param name="bitmap">新しいビットマップ。 null の場合は複製を破棄して描画しない。</param>
    private void ReplaceBitmap(Bitmap? bitmap)
    {
        var previous = _sourceBitmap;
        _sourceBitmap = bitmap;
        previous?.Dispose();
    }
}
