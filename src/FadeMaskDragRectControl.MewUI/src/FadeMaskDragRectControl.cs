using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Estable.DragRect;
using Estable.FadeMaskDragRect;
using Estable.Loupe;
using Estable.MewUI.DragRect;
using Estable.MewUI.FadeMask;
using Estable.MewUI.Loupe;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace Estable.MewUI.FadeMaskDragRect;

/// <summary>領域選択の複合コントロール（選択矩形 + 暗転マスク + ルーペの組み立て・配線・状態同期を 1 箇所に集約）。</summary>
/// <remarks>
/// <b>適用パターン</b>: Facade / Composite（内包コントロールの公開 API を統合し、ホストへ統一インターフェースを提供）。<br/>
/// 3 コントロールの生成・座標変換注入・イベント購読・状態同期・後片付けの配線を本クラスに集約し、<br/>
/// ホストはライフサイクル（生成・配置・後片付け）とモード固有処理（キャプチャ・確定フロー）に専念できる（SRP）。<br/>
/// <br/>
/// <b>UI 層の責務（本クラス）</b>: 設定（<see cref="Options"/>）とモード（<see cref="Mode"/>）を内包コントロールへ分配し、<br/>
/// 入力の受付（内包コントロールが担う）・効果の適用・通知の転送・描画先の選択のみを行う。<br/>
/// モードごとの可否（選択矩形・ルーペ・全面暗転・破線）はコアの <c>FadeMaskDragRectModes</c> が、<br/>
/// 選択矩形の可否はコアの <c>FadeMaskDragRectSelection</c> が、枠線の共用規則はコアの<br/>
/// <see cref="FadeMaskDragRectOptions.ResolveDragRectOptions"/> が単一所有する（UI 層は判断を持たず、コアの結果を割り当てるだけになる）。<br/>
/// <br/>
/// <b>内部構造</b>:<br/>
/// - <see cref="DragRectControl"/>: ドラッグ選択の本体（ドメイン座標 + 座標変換ポート）。<br/>
/// - <see cref="FadeMaskControl"/>: 暗転マスク・選択枠・ハイライトの描画。<br/>
/// - <see cref="LoupeControl"/>: 拡大鏡（中心 = ドメイン座標、表示位置はコアの計算で本クラスが配置する）。<br/>
/// <br/>
/// <b>配線（本クラス内で完結）</b>:<br/>
/// - 選択矩形の変更 → 暗転マスクの LogicalSelectionRect / HasSelection を同期する。<br/>
/// - 選択の開始・キャンセル → 暗転マスクの選択状態をクリアする（前の矩形の残留防止）。<br/>
/// - ルーペ位置の通知 → ルーペの中心を更新し、表示位置を再計算する。<br/>
/// - 確定・キャンセル・ズーム要求 → ホストへ中継する（独自のイベント引数形式）。<br/>
/// <br/>
/// <b>ラベル</b>: サイズラベル（1 行目）とサブライン（2 行目以降）は暗転マスクへ委譲する。<br/>
/// 文言はホストが <see cref="SizeLabelOverrideProvider"/> で与える（表示位置はコアの計算が単一所有する）。<br/>
/// <br/>
/// <b>動作モード（<see cref="Mode"/>）</b>:<br/>
/// - <see cref="FadeMaskDragRectMode.Crop"/> / <see cref="FadeMaskDragRectMode.AreaSelect"/>: 選択矩形 + 暗転マスク + ルーペを有効化する。<br/>
/// - <see cref="FadeMaskDragRectMode.ScreenSelect"/> / <see cref="FadeMaskDragRectMode.WindowSelect"/>: 暗転マスクのみ有効化する<br/>
///   （ハイライト表示のみで、クリック確定フローはホストが担う）。<br/>
/// <br/>
/// <b>座標系</b>: 選択矩形・ルーペ中心はドメイン座標で扱う。ドメイン ↔ ローカル DIP の変換は<br/>
/// 座標変換ポート（<see cref="SetCoordinateTransform"/>）が担い、暗転マスクとルーペへも同じポートを適用する。<br/>
/// <br/>
/// <b>描画先の選択（フレームワーク差）</b>: ルーペ画像上の選択矩形の破線は、描画完了イベントで通知されたキャンバスへ<br/>
/// 本クラスが直接描く（Avalonia 版は描画完了イベントをホストへ中継し、ホストがルーペ画像へ描く）。<br/>
/// 破線の幾何はコアが単一所有するため両 UI の見た目は一致する。<br/>
/// <br/>
/// <b>UI 層に残す例外（UI 固有のホスト結線）</b>: 子コントロールの生成・重なり順・ヒットテストの有無・クランプ・<br/>
/// フレームワーク固有の描画先（Bitmap / キャンバス / レイアウト要素）は UI 層が担う。
/// </remarks>
public sealed class FadeMaskDragRectControl : Panel
{
    /// <summary>暗転マスクコントロール（入力は受け取らず、下のコントロールへ通す）。</summary>
    private readonly FadeMaskControl _overlay = new() { IsHitTestVisible = false };

    /// <summary>ドラッグ選択コントロール（選択矩形・ハンドル・nudge）。</summary>
    private readonly DragRectControl _dragRect = new() { IsHitTestVisible = true };

    /// <summary>ルーペコントロール（表示位置は本クラスが配置する）。</summary>
    private readonly LoupeControl _loupe = new() { IsHitTestVisible = false, IsVisible = false };

    /// <summary>設定（選択矩形・暗転マスク・ルーペ・選択矩形破線の外観）。</summary>
    private FadeMaskDragRectOptions _options = FadeMaskDragRectOptions.Default;

    /// <summary>動作モード（既定は範囲選択）。</summary>
    private FadeMaskDragRectMode _mode = FadeMaskDragRectMode.AreaSelect;

    /// <summary>座標変換ポート（Forward = ローカル DIP → ドメイン / Inverse = ドメイン → ローカル DIP）。未設定は null。</summary>
    /// <remarks>ルーペの配置計算に使う（未設定時は等価変換で代替する）。</remarks>
    private ICoordTransform? _port;

    /// <summary>領域選択の複合コントロールを初期化する（子コントロールの生成・配置・配線を実施する）。</summary>
    /// <remarks>
    /// 暗転マスク → ドラッグ選択 → ルーペの順に重ねる（後に追加した子が上へ描画される）。<br/>
    /// イベント配線（状態同期・ホストへのイベント中継）を行い、モードを既定（範囲選択）で適用する。
    /// </remarks>
    public FadeMaskDragRectControl()
    {
        Add(_overlay);
        Add(_dragRect);
        Add(_loupe);

        // ── 配線（DragRect のイベントを状態同期とホスト通知へ変換） ──
        _dragRect.SelectionChanged += (_, e) => SetSelection(e.Rect);
        _dragRect.SelectionStarted += (_, _) => ClearSelection();
        _dragRect.SelectionCancelled += (_, _) => ClearSelection();
        _dragRect.ViewportChanged += (_, _) => NotifyViewportChanged();
        _dragRect.LoupePositionMoved += (_, e) => OnLoupePositionMoved(e.Point);
        _dragRect.ConfirmRequested += (_, e) => RaiseConfirmRequested(e.Rect);
        _dragRect.CancelRequested += (_, _) => RaiseCancelRequested();
        _dragRect.ZoomRequested += (_, request) => RaiseZoomRequested(request);
        _dragRect.ScrollRequested += (_, request) => RaiseScrollRequested(request);
        // ルーペ画像上の選択矩形破線は、ルーペの描画完了直後に同一のキャンバスへ描く
        _loupe.Rendered += OnLoupeRendered;

        // 設定の既定値を子コントロールへ分配してから、モード（既定 AreaSelect）の状態を適用する
        ApplyOptions();
        ApplyMode();
    }

    /// <summary>Enter キーまたは <see cref="RequestConfirm"/> による確定要求イベント（選択確定後のみ発火する）。</summary>
    public event EventHandler<FadeMaskDragRectSelectionEventArgs>? ConfirmRequested;

    /// <summary>Escape キーまたは <see cref="RequestCancel"/> によるキャンセル要求イベント。</summary>
    public event EventHandler? CancelRequested;

    /// <summary>Ctrl+ホイールによるズーム要求イベント（方向と発生位置）。</summary>
    public event EventHandler<DragRectZoomRequest>? ZoomRequested;

    /// <summary>通常ホイール（Ctrl なし）によるスクロール要求イベント（生の回転量・修飾キー・発生位置）。</summary>
    /// <remarks>スクロール先（ズームビューポートのパン等）の決定はホストが実施する。</remarks>
    public event EventHandler<DragRectScrollRequest>? ScrollRequested;

    /// <summary>ルーペ中心の更新イベント（ドメイン座標。ホストの追加制御用フック）。</summary>
    public event EventHandler<FadeMaskDragRectPointEventArgs>? LoupePositionMoved;

    /// <summary>設定（選択矩形・暗転マスク・ルーペ・選択矩形破線の外観）を取得または設定する。</summary>
    /// <value>設定の単一値。既定は <see cref="FadeMaskDragRectOptions.Default"/>（内包コントロールの既定値）。</value>
    /// <remarks>
    /// 一部の項目だけを変える場合は <c>FadeMaskDragRectOptions.Default with { ... }</c> の形で指定する（指定しない項目は既定値を保つ）。<br/>
    /// 設定を 1 つの値にまとめているため、内包コントロールへ分配する規則（枠線の共用等）はコアが単一所有する。<br/>
    /// 同じ値の再設定では更新をスキップする（無駄な再適用を防ぐ）。
    /// </remarks>
    public FadeMaskDragRectOptions Options
    {
        get => _options;
        set
        {
            if (_options == value)
            {
                return;
            }

            _options = value;
            ApplyOptions();
        }
    }

    /// <summary>動作モードを取得または設定する（内包コントロールの有効・無効と全面暗転を一括で切り替える）。</summary>
    /// <value>動作モード。既定値は <see cref="FadeMaskDragRectMode.AreaSelect"/>。</value>
    /// <remarks>
    /// - Crop / AreaSelect: 選択矩形とルーペを表示し、全面暗転を有効化する。<br/>
    /// - ScreenSelect / WindowSelect: 選択矩形とルーペを非表示にし、全面暗転を無効化する（ハイライトのみ）。
    /// </remarks>
    public FadeMaskDragRectMode Mode
    {
        get => _mode;
        set
        {
            if (_mode == value)
            {
                return;
            }

            _mode = value;
            ApplyMode();
        }
    }

    /// <summary>現在の選択状態（選択矩形の有無）を取得する。</summary>
    /// <value>選択矩形が有効な場合は true。</value>
    /// <remarks>選択状態の真のソースは暗転マスクコントロールに集約し、ドラッグ選択の変更から同期する。</remarks>
    public bool HasSelection => _overlay.HasSelection;

    /// <summary>現在の選択矩形（ドメイン座標）を取得する。</summary>
    /// <value>選択矩形。未選択時は 0x0 の空矩形。</value>
    public EsRect SelectionRect => _overlay.LogicalSelectionRect ?? default;

    /// <summary>ハイライト矩形（ドメイン座標）を取得または設定する。</summary>
    /// <value>ハイライト矩形。未検出時は null。選択矩形より優先される。</value>
    public EsRect? LogicalHighlightRect
    {
        get => _overlay.LogicalHighlightRect;
        set => _overlay.LogicalHighlightRect = value;
    }

    /// <summary>ハイライト表示の有無を取得または設定する。</summary>
    /// <value>true の場合は <see cref="LogicalHighlightRect"/> を穴あけ矩形として使う（選択矩形より優先）。</value>
    public bool HasHighlight
    {
        get => _overlay.HasHighlight;
        set => _overlay.HasHighlight = value;
    }

    /// <summary>全面暗転（穴あけなし）を表示するかどうかを取得する。</summary>
    /// <value>全面暗転する場合は true。モードから導出される読み取り専用の値。</value>
    public bool ShowFullOverlay => _overlay.ShowFullOverlay;

    /// <summary>ルーペ中心（ドメイン座標）を取得する。</summary>
    /// <value>ルーペ中心。未設定時は null。</value>
    /// <remarks>中心の真のソースはルーペコントロールへ集約し、本プロパティはその参照のみを返す。</remarks>
    public EsPoint? LoupeCenterPoint => _loupe.CenterPoint;

    /// <summary>暗転マスクコントロール（テスト用アクセサ。外観・状態は <see cref="Options"/> / 公開プロパティ経由で設定する）。</summary>
    internal FadeMaskControl Overlay => _overlay;

    /// <summary>ドラッグ選択コントロール（テスト用アクセサ。設定は <see cref="Options"/> 経由で設定する）。</summary>
    internal DragRectControl DragRect => _dragRect;

    /// <summary>ルーペコントロール（テスト用アクセサ。配置の検証に使用する）。</summary>
    internal LoupeControl Loupe => _loupe;

    /// <summary>ハイライト対象のウィンドウタイトル（情報ラベルの文言）を取得または設定する。</summary>
    /// <value>ウィンドウタイトル。未検出時は空文字。</value>
    /// <remarks>表示の可否はモードと <c>FadeMaskAppearance.InfoLabelVisible</c> からコアが判定する。</remarks>
    public string WindowTitle
    {
        get => _overlay.WindowTitle;
        set => _overlay.WindowTitle = value;
    }

    /// <summary>サイズラベルの文言を描画時に評価するデリゲートを取得または設定する。</summary>
    /// <value>描画時に呼び出される関数。null または空文字を返す場合はサイズラベルを描画しない。</value>
    /// <remarks>
    /// ホストは最新の選択状態から文言を生成するクロージャを設定する（例: コアの
    /// <c>FadeMaskLabels.FormatSizeLabel</c> で選択矩形を整形する）。
    /// </remarks>
    public Func<string?>? SizeLabelOverrideProvider
    {
        get => _overlay.SizeLabelOverrideProvider;
        set => _overlay.SizeLabelOverrideProvider = value;
    }

    /// <summary>DPI スケーリング係数（ドメイン → DIP 変換の基準倍率）を取得または設定する。</summary>
    /// <value>スケーリング係数。ドメイン座標がローカル DIP と一致する場合は 1.0。</value>
    /// <remarks>
    /// 環境値（ユーザー設定ではない）のため設定（<see cref="Options"/>）には含めず、ホストが与える。<br/>
    /// 座標変換ポート（<see cref="SetCoordinateTransform"/>）が設定されている場合はそちらが優先される。
    /// </remarks>
    public double Scaling
    {
        get => _overlay.Scaling;
        set => _overlay.Scaling = value;
    }

    /// <summary>サイズラベル表示用の DPI スケーリング係数（マルチスクリーン補正。0 以下は <see cref="Scaling"/> と同値）を取得または設定する。</summary>
    /// <value>サイズラベルの DisplayScale に使う倍率。0 以下は <see cref="Scaling"/> から解決される。</value>
    /// <remarks>環境値のため設定（<see cref="Options"/>）には含めず、ホストが与える。</remarks>
    public double DisplayScaleScaling
    {
        get => _overlay.DisplayScaleScaling;
        set => _overlay.DisplayScaleScaling = value;
    }

    /// <summary>ルーペの DIP → 物理ピクセル倍率を取得または設定する。</summary>
    /// <value>スケーリング係数（0.1〜8.0）。既定は 1.0。</value>
    /// <remarks>環境値（DPI）のため設定（<see cref="Options"/>）には含めず、ホストが与える。</remarks>
    public double LoupeRenderScaling
    {
        get => _loupe.RenderScaling;
        set => _loupe.RenderScaling = value;
    }

    /// <summary>ドメイン ↔ ローカル DIP の座標変換ポートを全内包コントロールへ一括設定する。</summary>
    /// <param name="port">座標変換ポート。null の場合は <see cref="Scaling"/> 相当の等倍スケール（コアが解決）を使う。</param>
    /// <remarks>
    /// ドラッグ選択と暗転マスクへ同一ポートを分配する（1 回の呼び出しで設定完了）。<br/>
    /// 変換関数は呼び出し毎に評価されるため、ズーム・スクロール・ウィンドウサイズ変更等のレイアウト変化に追従する。<br/>
    /// null はそのまま渡す（未設定を倍率で解決する規則はコアが単一所有する）。ルーペの表示位置計算のみ本クラスが行うため、<br/>
    /// その用途では等価変換で代替する。
    /// </remarks>
    public void SetCoordinateTransform(ICoordTransform? port)
    {
        _port = port;
        _dragRect.SetCoordinateTransform(port);
        _overlay.SetCoordinateTransform(port);
        InvalidateArrange();
    }

    /// <summary>操作ログの出力先を取得または設定する（内包するコントロールへ伝搬する）。</summary>
    /// <value>ロガー。null を設定した場合は <see cref="NullLogger"/> へフォールバックする（出力なし）。</value>
    /// <remarks>
    /// ドラッグ選択コントロールとルーペへ伝搬する（Avalonia 版と同じ範囲）。<br/>
    /// 暗転マスクは選択のたびに状態変更ログを出すため対象外とする（必要なら内包コントロールの <c>Log</c> を直接設定する）。
    /// </remarks>
    public ILogger Log
    {
        get => _dragRect.Log;
        set
        {
            var logger = value.OrNullLogger();
            _dragRect.Log = logger;
            _loupe.Log = logger;
        }
    }

    /// <summary>選択確定要求をドラッグ選択コントロールへ委譲する（Enter キー相当）。</summary>
    public void RequestConfirm() => _dragRect.RequestConfirm();

    /// <summary>選択キャンセル要求をドラッグ選択コントロールへ委譲する（Escape キー相当）。</summary>
    public void RequestCancel() => _dragRect.RequestCancel();

    /// <summary>選択矩形をプログラム的に設定する（外部復元・テスト用）。</summary>
    /// <param name="rect">選択矩形（ドメイン座標）。</param>
    /// <remarks>
    /// ドラッグ選択コントロールへ委譲する。選択変更が発火し、暗転マスクとルーペ表示も同期される。
    /// </remarks>
    public void SetDragRectSelection(EsRect rect) => _dragRect.SetSelection(rect);

    /// <summary>ドラッグ選択コントロールへフォーカスを設定する（矢印キー nudge の受付開始）。</summary>
    public void FocusDragRect() => _dragRect.Focus();

    /// <summary>選択状態をオーバーレイ描画へ反映する（ホストが選択矩形を外部から設定する場合に使用する）。</summary>
    /// <param name="rect">選択矩形（ドメイン座標）。幅または高さが 0 以下の場合は選択なし。</param>
    /// <remarks>選択として成立するかどうかの判定はコアの <c>FadeMaskDragRectSelection</c> が単一所有する。</remarks>
    public void SetSelection(EsRect rect) => SyncSelection(FadeMaskDragRectSelection.IsUsable(rect.Width, rect.Height) ? rect : null);

    /// <summary>選択状態をクリアして全面暗転（穴あけなし）に戻す。</summary>
    /// <remarks>新規選択の開始時・キャンセル時に、前の選択矩形が暗転マスクに残らないようにする。</remarks>
    public void ClearSelection() => SyncSelection(null);

    /// <summary>ルーペの中心位置（ドメイン座標）を設定して表示を開始する。</summary>
    /// <param name="domainPos">ルーペ中心（ドメイン座標）。</param>
    /// <remarks>
    /// 表示位置・サイズは座標変換ポート（<see cref="SetCoordinateTransform"/>）とコアの計算から求める。<br/>
    /// <b>拡大元画像が未設定の間は表示しない</b>（未設定の市松模様を防ぐ。<see cref="SetLoupeSource"/> の後に呼び出す）。
    /// </remarks>
    public void SetLoupePosition(EsPoint domainPos) => UpdateLoupePosition(domainPos);

    /// <summary>ルーペの中心位置（ドメイン座標）を設定して表示可否を更新する。</summary>
    /// <param name="domainPos">ルーペ中心（ドメイン座標）。</param>
    /// <remarks>
    /// <see cref="SetLoupePosition"/> と <see cref="OnLoupePositionMoved"/> で共用する（重複排除）。<br/>
    /// 表示位置・サイズは座標変換ポート（<see cref="SetCoordinateTransform"/>）とコアの計算から求める。<br/>
    /// 表示可否の判断はコアの <c>FadeMaskDragRectState.IsLoupeVisible</c> が単一所有する。
    /// </remarks>
    private void UpdateLoupePosition(EsPoint domainPos)
    {
        _loupe.CenterPoint = domainPos;
        UpdateLoupeVisibility();
    }

    /// <summary>ルーペの拡大元画像を設定する。</summary>
    /// <param name="source">拡大元の画像。null の場合は解除する。</param>
    /// <remarks>画像を設定しただけでは表示しない（表示開始は <see cref="SetLoupePosition"/> が担う）。解除時は表示も解除する。</remarks>
    public void SetLoupeSource(EsBitmap? source)
    {
        _loupe.Source = source;

        if (source is null)
        {
            // 拡大元画像が無い状態で表示を続けない（次の SetLoupePosition まで非表示）。
            // 表示可否の判断はコアが単一所有するため、ここでは問い合わせるだけにする。
            UpdateLoupeVisibility();
        }

        InvalidateArrange();
    }

    /// <summary>ルーペを非表示にして後片付けする（拡大元画像・中心・Bitmap を解放する）。</summary>
    /// <remarks>セッション終了時の後片付けに使用する（表示状態・中心・拡大元画像をまとめて破棄する）。</remarks>
    public void ClearLoupe()
    {
        _loupe.Source = null;
        _loupe.CenterPoint = null;
        // 表示可否の判断はコアが単一所有するため、ここでは問い合わせるだけにする
        UpdateLoupeVisibility();
        InvalidateArrange();
    }

    /// <summary>暗転マスクの再描画を要求する。</summary>
    /// <remarks>ハイライト等の状態を変更した後に呼び出す。</remarks>
    public void InvalidateOverlay() => _overlay.InvalidateVisual();

    /// <summary>ビューポート（パン・ズーム・表示サイズ）の変化を内包コントロールへ通知する。</summary>
    /// <remarks>
    /// 本コントロールは <c>ScrollViewer</c> の自動連携を行わないため、ホストがビューポートの変化を検知した際に呼ぶ。<br/>
    /// 選択矩形（ドメイン座標）はビューポート変化で不変のため再同期は行わず、再描画とルーペ表示位置の再計算のみを要求する。
    /// </remarks>
    public void NotifyViewportChanged()
    {
        _dragRect.InvalidateVisual();
        _overlay.InvalidateVisual();
        InvalidateArrange();
    }

    /// <summary>要求サイズを返す（親の残余を埋める）。</summary>
    /// <param name="availableSize">親から提示された利用可能サイズ。</param>
    /// <returns>常に <c>Size.Empty</c>。</returns>
    /// <remarks>
    /// 本コントロールは親が割り当てた領域全体を選択対象にするため、自身の要求サイズを持たない<br/>
    /// （内包コントロールも同じ性質のため、子の希望サイズを合算しない）。
    /// </remarks>
    protected override Size MeasureContent(Size availableSize)
    {
        _overlay.Measure(availableSize);
        _dragRect.Measure(availableSize);
        _loupe.Measure(availableSize);
        return Size.Empty;
    }

    /// <summary>子コントロールを配置する（暗転マスクとドラッグ選択は全面、ルーペは中心 + オフセット）。</summary>
    /// <param name="bounds">本コントロールへ割り当てられた矩形（ウィンドウ絶対座標・DIP）。</param>
    /// <remarks>
    /// ルーペの表示サイズ・位置はコアの <see cref="LoupePositionCalculator.Compute"/> が単一所有する<br/>
    /// （親領域の最小辺へのクランプ・オフセット・親領域内へのクランプ。親サイズが 0 の場合の扱いもコアが判断する）。<br/>
    /// 非表示（未表示・画像未設定・中心未設定）の場合はサイズ 0 で配置する。
    /// </remarks>
    protected override void ArrangeContent(Rect bounds)
    {
        _overlay.Arrange(bounds);
        _dragRect.Arrange(bounds);

        if (!_loupe.IsVisible || _loupe.CenterPoint is not { } domainPos)
        {
            _loupe.Arrange(new Rect(bounds.X, bounds.Y, 0, 0));
            return;
        }

        var parentSize = new EsSize(bounds.Width, bounds.Height);
        var (size, position) = LoupePositionCalculator.Compute(
            _loupe.Options.RequestedSize,
            parentSize,
            // ルーペの表示位置は本クラスが計算するため、未設定（null）は等価変換として扱う（規則は共通ヘルパーが単一所有する）
            _port.OrIdentity().Inverse(domainPos),
            _options.LoupeOffset);

        _loupe.Arrange(new Rect(bounds.X + position.X, bounds.Y + position.Y, size.Width, size.Height));
    }

    /// <summary>設定を内包コントロールへ分配する。</summary>
    /// <remarks>
    /// 枠線の共用（選択枠線 ＝ 暗転マスクの穴あけ枠）はコアの <see cref="FadeMaskDragRectOptions.ResolveDragRectOptions"/> が解決する。<br/>
    /// 適用後は暗転マスク・選択矩形の再描画とルーペの再描画・再配置を要求する（選択状態・ルーペ中心は維持される）。
    /// </remarks>
    private void ApplyOptions()
    {
        _overlay.Appearance = _options.Overlay;
        _dragRect.Options = _options.ResolveDragRectOptions();
        _loupe.Options = _options.Loupe;

        _overlay.InvalidateVisual();
        _dragRect.InvalidateVisual();
        // ルーペは描画のたびに選択矩形の破線を組み立てるため、破線色の変更は再描画で反映される
        _loupe.InvalidateVisual();
        InvalidateArrange();
    }

    /// <summary>モードに応じて内包コントロールの状態（可視性・全面暗転・破線）を適用する。</summary>
    /// <remarks>
    /// モードと状態の対応はコアの <c>FadeMaskDragRectModes</c> が単一所有する。<br/>
    /// ルーペ画像上の破線の可否もモード依存のため、既存の描画から破線を消す（または描く）ためにルーペを再描画する。
    /// </remarks>
    private void ApplyMode()
    {
        var state = FadeMaskDragRectModes.Resolve(_mode);

        _overlay.Mode = state.RenderMode;
        _overlay.ShowFullOverlay = state.ShowFullOverlay;
        _dragRect.IsVisible = state.UsesSelection;

        UpdateLoupeVisibility();
        _loupe.InvalidateVisual();
    }

    /// <summary>ルーペの表示可否をモード・拡大元画像・中心から更新する。</summary>
    /// <remarks>表示可否の判断はコアの <c>FadeMaskDragRectState.IsLoupeVisible</c> が単一所有する。</remarks>
    private void UpdateLoupeVisibility()
    {
        _loupe.IsVisible = FadeMaskDragRectModes
            .Resolve(_mode)
            .IsLoupeVisible(_loupe.Source is not null, _loupe.CenterPoint is not null);
        InvalidateArrange();
    }

    /// <summary>ルーペの描画完了時に、選択矩形の破線オーバーレイを同一のキャンバスへ描画する。</summary>
    /// <param name="sender">イベント送信元（ルーペコントロール）。</param>
    /// <param name="e">描画先キャンバスと表示情報。</param>
    /// <remarks>
    /// 選択矩形の破線はルーペ内部では描画せず、本クラス（ホスト側）が描画完了直後にキャンバスへ追加描画する<br/>
    /// （破線の幾何はコアの <c>FadeMaskDragRectOverlay</c> が単一所有し、Avalonia 版と同じ見た目になる）。<br/>
    /// 切り出し領域の左上が未設定（中心未設定）の場合は描画しない。
    /// </remarks>
    private void OnLoupeRendered(object? sender, LoupeRenderedEventArgs e)
    {
        if (e.CaptureTopLeft is { } topLeft)
        {
            DrawSelectionOverlay(e.Canvas, topLeft, e.Scale, e.PixelSize);
        }
    }

    /// <summary>ルーペ画像上の選択矩形破線をキャンバスへ描画する。</summary>
    /// <param name="canvas">描画先のキャンバス（ピクセル等倍で描画できる状態であること）。</param>
    /// <param name="captureTopLeft">ルーペの切り出し領域の左上（ドメイン座標系）。</param>
    /// <param name="scale">ドメイン座標 → ルーペ画像ピクセル座標の変換係数。</param>
    /// <param name="pixelSize">ルーペ画像の一辺のサイズ（ピクセル）。切り出し領域との交差判定に使用する。</param>
    /// <remarks>
    /// 選択矩形（<see cref="SelectionRect"/> = ドメイン座標）は <c>(ドメイン座標 − captureTopLeft) × scale</c> で<br/>
    /// ルーペ画像ピクセル座標へ変換する（変換と破線の幾何はコアが単一所有する）。<br/>
    /// 破線を描画しない場合（選択矩形を使わないモード・選択なし）は何もしない。
    /// </remarks>
    internal void DrawSelectionOverlay(SKCanvas canvas, EsPoint captureTopLeft, double scale, int pixelSize)
    {
        // 破線を描くかどうかはモード（コアが単一所有する）と選択矩形の有無で決まる
        if (!FadeMaskDragRectModes.Resolve(_mode).ShowSelectionOverlay
            || _overlay.LogicalSelectionRect is not { } selectionRect)
        {
            return;
        }

        FadeMaskDragRectSkiaRenderer.DrawSelectionRect(
            canvas,
            selectionRect,
            captureTopLeft,
            scale,
            pixelSize,
            _options.SelectionRectColorA,
            _options.SelectionRectColorB);
    }

    /// <summary>選択状態を暗転マスクへ同期する。</summary>
    /// <param name="rect">選択矩形（ドメイン座標）。null は選択なし。</param>
    /// <remarks>
    /// <see cref="SetSelection"/> と <see cref="ClearSelection"/> で共用する（重複排除）。<br/>
    /// ルーペ画像上の破線は描画のたびに選択矩形から組み立てるため、再描画を要求する（破線はルーペのプロパティに現れない）。
    /// </remarks>
    private void SyncSelection(EsRect? rect)
    {
        _overlay.HasSelection = rect is not null;
        _overlay.LogicalSelectionRect = rect;
        _loupe.InvalidateVisual();
    }

    /// <summary>ルーペ位置の更新（ドラッグ選択からの通知）を処理する。</summary>
    /// <param name="domainPos">ルーペ中心（ドメイン座標）。</param>
    /// <remarks>
    /// 拡大元画像が未設定の間は表示しない（ホストが <see cref="SetLoupeSource"/> を呼んだ後、<br/>
    /// <see cref="SetLoupePosition"/> で表示を開始する）。<br/>
    /// ホストへの通知は追加制御（拡大元画像の設定等）のフックとして発行する。
    /// </remarks>
    private void OnLoupePositionMoved(EsPoint domainPos)
    {
        UpdateLoupePosition(domainPos);
        RaiseLoupePositionMoved(domainPos);
    }

    // ====================================================================
    // イベント発火（ホストへの通知。発火形式を一貫させるため private ヘルパーに集約）
    // ====================================================================

    /// <summary>確定要求イベントを発火する。</summary>
    /// <param name="rect">確定した選択矩形（ドメイン座標）。</param>
    private void RaiseConfirmRequested(EsRect rect)
        => ConfirmRequested?.Invoke(this, new FadeMaskDragRectSelectionEventArgs(rect));

    /// <summary>キャンセル要求イベントを発火する。</summary>
    private void RaiseCancelRequested()
        => CancelRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>ズーム要求イベントを発火する。</summary>
    /// <param name="request">ズーム要求（方向と発生位置）。</param>
    private void RaiseZoomRequested(DragRectZoomRequest request)
        => ZoomRequested?.Invoke(this, request);

    /// <summary>スクロール要求イベントを発火する。</summary>
    /// <param name="request">スクロール要求（生の回転量と修飾キー）。</param>
    private void RaiseScrollRequested(DragRectScrollRequest request)
        => ScrollRequested?.Invoke(this, request);

    /// <summary>ルーペ中心更新イベントを発火する。</summary>
    /// <param name="domainPos">ルーペ中心（ドメイン座標）。</param>
    private void RaiseLoupePositionMoved(EsPoint domainPos)
        => LoupePositionMoved?.Invoke(this, new FadeMaskDragRectPointEventArgs(domainPos));
}
