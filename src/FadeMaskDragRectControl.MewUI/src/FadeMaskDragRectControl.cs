using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Estable.FadeMaskDragRect;
using Estable.Loupe;
using Estable.MewUI.DragRect;
using Estable.MewUI.FadeMask;
using Estable.MewUI.Loupe;
using Estable.Platform.Coordinate;
using Estable.Platform.Geometry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;

namespace Estable.MewUI.FadeMaskDragRect;

/// <summary>領域選択の複合コントロール（選択矩形 + 暗転マスク + ルーペの組み立て・配線・状態同期を 1 箇所に集約）。</summary>
/// <remarks>
/// <b>適用パターン</b>: Facade / Composite（内包コントロールの公開 API を統合し、ホストへ統一インターフェースを提供）。<br/>
/// 3 コントロールの生成・座標変換注入・イベント購読・状態同期・後片付けの配線を本クラスに集約し、<br/>
/// ホストはライフサイクル（生成・配置・後片付け）とモード固有処理（キャプチャ・確定フロー）に専念できる（SRP）。<br/>
/// <br/>
/// <b>内部構造</b>:<br/>
/// - <see cref="DragRectControl"/>: ドラッグ選択の本体（ドメイン座標 + 座標変換ポート）。<br/>
/// - <see cref="FadeMaskControl"/>: 暗転マスク・選択枠・ハイライトの描画。<br/>
/// - <see cref="LoupeControl"/>: 拡大鏡（中心 = ドメイン座標、表示位置はコアの計算で本クラスが配置する）。<br/>
/// <br/>
/// <b>配線（本クラス内で完結）</b>:<br/>
/// - 選択矩形の変更 → 暗転マスクの SelectionRect / HasSelection を同期する。<br/>
/// - 選択の開始・キャンセル → 暗転マスクの選択状態をクリアする（前の矩形の残留防止）。<br/>
/// - ルーペ位置の通知 → ルーペの中心を更新し、表示位置を再計算する。<br/>
/// - 確定・キャンセル・ズーム要求 → ホストへ中継する（独自のイベント引数形式）。<br/>
/// <br/>
/// <b>ラベル</b>: サイズラベル（1 行目）とサブライン（2 行目以降）は暗転マスクへ委譲する。<br/>
/// 文言はホストが <see cref="SizeLabelTextProvider"/> で与える（表示位置はコアの計算が単一所有する）。<br/>
/// <br/>
/// <b>動作モード（<see cref="Mode"/>）</b>:<br/>
/// - <see cref="FadeMaskDragRectMode.Crop"/> / <see cref="FadeMaskDragRectMode.AreaSelect"/>: 選択矩形 + 暗転マスク + ルーペを有効化する。<br/>
/// - <see cref="FadeMaskDragRectMode.ScreenSelect"/> / <see cref="FadeMaskDragRectMode.WindowSelect"/>: 暗転マスクのみ有効化する<br/>
///   （ハイライト表示のみで、クリック確定フローはホストが担う）。<br/>
/// <br/>
/// <b>座標系</b>: 選択矩形・ルーペ中心はドメイン座標で扱う。ドメイン ↔ ローカル DIP の変換は<br/>
/// 座標変換ポート（<see cref="SetCoordinateTransform"/>）が担い、暗転マスクとルーペへも同じポートを適用する。<br/>
/// <br/>
/// <b>未対応</b>: ルーペ画像上の選択矩形破線オーバーレイ（描画完了フックが必要）。必要になった段階で追加する。<br/>
/// ルーペの位置決めはコントロールの責務にせず、ホストが <see cref="SetCoordinateTransform"/> と <see cref="SetLoupePosition"/> で与えた情報から
/// 本クラスがコアの計算で行う（親領域サイズへのクランプを含む）。
/// </remarks>
public sealed class FadeMaskDragRectControl : Panel
{
    /// <summary>暗転マスクコントロール（入力は受け取らず、下のコントロールへ通す）。</summary>
    private readonly FadeMaskControl _overlay = new() { IsHitTestVisible = false };

    /// <summary>ドラッグ選択コントロール（選択矩形・ハンドル・nudge）。</summary>
    private readonly DragRectControl _dragRect = new() { IsHitTestVisible = true };

    /// <summary>ルーペコントロール（表示位置は本クラスが配置する）。</summary>
    private readonly LoupeControl _loupe = new() { IsHitTestVisible = false, IsVisible = false };

    /// <summary>動作モード（既定は範囲選択）。</summary>
    private FadeMaskDragRectMode _mode = FadeMaskDragRectMode.AreaSelect;

    /// <summary>座標変換ポート（Forward = ローカル DIP → ドメイン / Inverse = ドメイン → ローカル DIP）。</summary>
    private ICoordTransform _port = CoordTransform.Identity;

    /// <summary>ルーペ中心（ドメイン座標）。null の場合は表示しない。</summary>
    private EsPoint? _loupeCenter;

    /// <summary>ルーペのカーソル中心からの右下オフセット（DIP）。</summary>
    private double _loupeOffset = 20.0;

    /// <summary>ルーペ画像上へ選択矩形の破線オーバーレイを描画するか（選択矩形を使うモードでのみ真）。</summary>
    private bool _showSelectionOverlay;

    /// <summary>選択矩形破線の色 A（描画開始色）。</summary>
    private System.Drawing.Color _selectionColorA = System.Drawing.Color.White;

    /// <summary>選択矩形破線の色 B（色 A と交互に使用する）。</summary>
    private System.Drawing.Color _selectionColorB = System.Drawing.Color.Black;

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
        _dragRect.ConfirmRequested += (_, e) => ConfirmRequested?.Invoke(this, new FadeMaskDragRectSelectionEventArgs(e.Rect));
        _dragRect.CancelRequested += (_, _) => CancelRequested?.Invoke(this, EventArgs.Empty);
        _dragRect.ZoomRequested += (_, zoomIn) => ZoomRequested?.Invoke(this, zoomIn);
        // ルーペ画像上の選択矩形破線は、ルーペの描画完了直後に同一のキャンバスへ描く
        _loupe.Rendered += OnLoupeRendered;

        ApplyMode();
    }

    /// <summary>Enter キーまたは <see cref="RequestConfirm"/> による確定要求イベント（選択確定後のみ発火する）。</summary>
    public event EventHandler<FadeMaskDragRectSelectionEventArgs>? ConfirmRequested;

    /// <summary>Escape キーまたは <see cref="RequestCancel"/> によるキャンセル要求イベント。</summary>
    public event EventHandler? CancelRequested;

    /// <summary>Ctrl+ホイールによるズーム要求イベント（true = ズームイン / false = ズームアウト）。</summary>
    public event EventHandler<bool>? ZoomRequested;

    /// <summary>ルーペ中心の更新イベント（ドメイン座標。ホストの追加制御用フック）。</summary>
    public event EventHandler<FadeMaskDragRectPointEventArgs>? LoupePositionMoved;

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
    public EsRect SelectionRect => _overlay.SelectionRect ?? default;

    /// <summary>ハイライト矩形（ドメイン座標）を取得または設定する。</summary>
    /// <value>ハイライト矩形。未検出時は null。選択矩形より優先される。</value>
    public EsRect? LogicalHighlightRect
    {
        get => _overlay.HighlightRect;
        set => _overlay.HighlightRect = value;
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
    public EsPoint? LoupeCenterPoint => _loupeCenter;

    /// <summary>ルーペのカーソル中心からの右下オフセットを取得または設定する。</summary>
    /// <value>オフセット（DIP）。既定値は 20.0。</value>
    public double LoupeOffset
    {
        get => _loupeOffset;
        set
        {
            if (_loupeOffset == value)
            {
                return;
            }

            _loupeOffset = value;
            InvalidateArrange();
        }
    }

    /// <summary>暗転マスクの暗転色を取得または設定する（不透明度はアルファに統合する）。</summary>
    /// <value>暗転の色。アルファ 0 の場合は暗転しない。</value>
    public System.Drawing.Color OverlayColor
    {
        get => _overlay.OverlayColor;
        set => _overlay.OverlayColor = value;
    }

    /// <summary>選択枠線の色を取得または設定する（暗転マスクとドラッグ矩形で共用する）。</summary>
    /// <value>枠線の色（不透明度はアルファに統合する）。</value>
    /// <remarks>ドラッグ矩形の枠線と暗転マスクの穴あけ枠を同じ色に揃える（見た目の一貫性）。</remarks>
    public System.Drawing.Color BorderColor
    {
        get => _overlay.BorderColor;
        set
        {
            _overlay.BorderColor = value;
            _dragRect.BorderColor = value;
        }
    }

    /// <summary>選択枠線の太さを取得または設定する（DIP）。</summary>
    /// <value>枠線の太さ（DIP）。</value>
    public double BorderThickness
    {
        get => _overlay.BorderThickness;
        set
        {
            _overlay.BorderThickness = value;
            _dragRect.BorderThickness = value;
        }
    }

    /// <summary>サイズラベル（1 行目）を表示するかどうかを取得または設定する。</summary>
    /// <value>表示する場合は true。既定値は true。</value>
    public bool ShowSizeLabel
    {
        get => _overlay.ShowSizeLabel;
        set => _overlay.ShowSizeLabel = value;
    }

    /// <summary>サイズラベルの文言を描画時に評価するデリゲートを取得または設定する。</summary>
    /// <value>描画時に呼び出される関数。null または空文字を返す場合はサイズラベルを描画しない。</value>
    /// <remarks>
    /// ホストは最新の選択状態から文言を生成するクロージャを設定する（例: コアの
    /// <c>FadeMaskLabels.FormatSizeLabel</c> で選択矩形を整形する）。<br/>
    /// 設定（変更）時は再描画を要求する。
    /// </remarks>
    public Func<string?>? SizeLabelTextProvider
    {
        get => _overlay.SizeLabelTextProvider;
        set
        {
            _overlay.SizeLabelTextProvider = value;
            _overlay.InvalidateVisual();
        }
    }

    /// <summary>サイズラベルのフォントサイズ（DIP）を取得または設定する。</summary>
    /// <value>フォントサイズ（DIP）。既定値は 14.0。</value>
    public double SizeLabelFontSize
    {
        get => _overlay.SizeLabelFontSize;
        set => _overlay.SizeLabelFontSize = value;
    }

    /// <summary>サイズラベルの文字色を取得または設定する。</summary>
    /// <value>文字色（不透明度はアルファに統合する）。</value>
    public System.Drawing.Color SizeLabelColor
    {
        get => _overlay.SizeLabelColor;
        set => _overlay.SizeLabelColor = value;
    }

    /// <summary>サイズラベルの背景色を取得または設定する。</summary>
    /// <value>背景色（不透明度はアルファに統合する）。アルファ 0 の場合は黒 50% へフォールバックする。</value>
    public System.Drawing.Color SizeLabelBackgroundColor
    {
        get => _overlay.SizeLabelBackgroundColor;
        set => _overlay.SizeLabelBackgroundColor = value;
    }

    /// <summary>サイズラベルの下に積む行（2 行目以降）を取得または設定する。</summary>
    /// <value>行の一覧（上から順に描画する）。null または空の場合はサイズラベルのみ描画する。</value>
    /// <remarks>設定（変更）時は再描画を要求する。</remarks>
    public IReadOnlyList<FadeMaskSubLine>? SubLines
    {
        get => _overlay.SubLines;
        set
        {
            _overlay.SubLines = value;
            _overlay.InvalidateVisual();
        }
    }

    /// <summary>ルーペ画像上の選択矩形破線の色 A（描画開始色）を取得または設定する。</summary>
    /// <value>色（不透明度はアルファに統合する）。既定値は白。</value>
    /// <remarks>設定（変更）時はルーペの再描画を要求する（破線は描画のたびに組み立てる）。</remarks>
    public System.Drawing.Color SelectionOverlayColorA
    {
        get => _selectionColorA;
        set
        {
            _selectionColorA = value;
            _loupe.InvalidateVisual();
        }
    }

    /// <summary>ルーペ画像上の選択矩形破線の色 B を取得または設定する。</summary>
    /// <value>色（不透明度はアルファに統合する）。既定値は黒。</value>
    /// <remarks>設定（変更）時はルーペの再描画を要求する（破線は描画のたびに組み立てる）。</remarks>
    public System.Drawing.Color SelectionOverlayColorB
    {
        get => _selectionColorB;
        set
        {
            _selectionColorB = value;
            _loupe.InvalidateVisual();
        }
    }

    /// <summary>暗転マスクの描画に使用する座標変換ポートを取得する。</summary>
    /// <value>論理座標 → DIP の座標変換ポート。</value>
    public ICoordTransform? Port => _overlay.Port;

    /// <summary>暗転マスクコントロールを取得する（外観の詳細設定用）。</summary>
    /// <value>暗転マスクを描画するコントロール。</value>
    public FadeMaskControl Overlay => _overlay;

    /// <summary>ドラッグ選択コントロールを取得する（外観の詳細設定用）。</summary>
    /// <value>選択矩形の入力と描画を担うコントロール。</value>
    public DragRectControl DragRect => _dragRect;

    /// <summary>ルーペコントロールを取得する（外観の詳細設定用）。</summary>
    /// <value>拡大鏡を描画するコントロール。</value>
    public LoupeControl Loupe => _loupe;

    /// <summary>ドメイン ↔ ローカル DIP の座標変換ポートを全内包コントロールへ一括設定する。</summary>
    /// <param name="port">座標変換ポート。null の場合は等価変換（1 ドメインピクセル = 1 DIP・境界なし）。</param>
    /// <remarks>
    /// ドラッグ選択と暗転マスクへ同一ポートを分配する（1 回の呼び出しで設定完了）。<br/>
    /// 変換関数は呼び出し毎に評価されるため、ズーム・スクロール・ウィンドウサイズ変更等のレイアウト変化に追従する。<br/>
    /// ルーペの表示位置も本ポートで計算するため、配置を再計算する。
    /// </remarks>
    public void SetCoordinateTransform(ICoordTransform? port)
    {
        _port = port ?? CoordTransform.Identity;
        _dragRect.SetCoordinateTransform(_port);
        _overlay.Port = _port;
        InvalidateArrange();
    }

    /// <summary>操作ログの出力先を取得または設定する（内包するコントロールへ伝搬する）。</summary>
    /// <value>ロガー。null を設定した場合は <see cref="NullLogger"/> へフォールバックする（出力なし）。</value>
    /// <remarks>
    /// ドラッグ選択コントロールとルーペへ伝搬する（Avalonia 版と同じ範囲）。<br/>
    /// 暗転マスクは選択のたびに状態変更ログを出すため対象外とする（必要なら <c>Overlay.Log</c> を直接設定する）。
    /// </remarks>
    public ILogger Log
    {
        get => _dragRect.Log;
        set
        {
            var logger = value ?? NullLogger.Instance;
            _dragRect.Log = logger;
            _loupe.Log = logger;
        }
    }

    /// <summary>選択確定要求をドラッグ選択コントロールへ委譲する（Enter キー相当）。</summary></summary>
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
    public void SetSelection(EsRect rect)
    {
        var hasSelection = rect.Width > 0 && rect.Height > 0;
        SyncSelection(hasSelection ? rect : null);
    }

    /// <summary>選択状態をクリアして全面暗転（穴あけなし）に戻す。</summary>
    /// <remarks>新規選択の開始時・キャンセル時に、前の選択矩形が暗転マスクに残らないようにする。</remarks>
    public void ClearSelection() => SyncSelection(null);

    /// <summary>ルーペの中心位置（ドメイン座標）を設定して表示を開始する。</summary>
    /// <param name="domainPos">ルーペ中心（ドメイン座標）。</param>
    /// <remarks>
    /// 表示位置・サイズは座標変換ポート（<see cref="SetCoordinateTransform"/>）とコアの計算から求める。<br/>
    /// <b>拡大元画像が未設定の間は表示しない</b>（未設定の市松模様を防ぐ。<see cref="SetLoupeSource"/> の後に呼び出す）。
    /// </remarks>
    public void SetLoupePosition(EsPoint domainPos)
    {
        _loupeCenter = domainPos;
        _loupe.CenterPoint = domainPos;
        // 拡大元画像が設定済みで、かつルーペを使用するモードの場合のみ表示する
        _loupe.IsVisible = _loupe.Source is not null && FadeMaskDragRectModes.UsesDragRect(_mode);
        InvalidateArrange();
    }

    /// <summary>ルーペの拡大元画像を設定する。</summary>
    /// <param name="source">拡大元の画像。null の場合は解除する。</param>
    /// <remarks>画像を設定しただけでは表示しない（表示開始は <see cref="SetLoupePosition"/> が担う）。</remarks>
    public void SetLoupeSource(Estable.Platform.Imaging.EsBitmap? source)
    {
        _loupe.Source = source;

        if (source is null)
        {
            _loupe.IsVisible = false;
        }

        InvalidateArrange();
    }

    /// <summary>ルーペを非表示にして拡大元画像を解放する。</summary>
    /// <remarks>セッション終了時の後片付けに使用する。</remarks>
    public void ClearLoupe()
    {
        _loupe.Source = null;
        _loupeCenter = null;
        _loupe.CenterPoint = null;
        _loupe.IsVisible = false;
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
    /// （親領域の最小辺へのクランプ・オフセット・親領域内へのクランプ）。<br/>
    /// 非表示（未表示・画像未設定・中心未設定）の場合はサイズ 0 で配置する。
    /// </remarks>
    protected override void ArrangeContent(Rect bounds)
    {
        _overlay.Arrange(bounds);
        _dragRect.Arrange(bounds);

        if (!_loupe.IsVisible || _loupeCenter is not { } domainPos || _loupe.Source is null)
        {
            _loupe.Arrange(new Rect(bounds.X, bounds.Y, 0, 0));
            return;
        }

        var parentSize = new EsSize(bounds.Width, bounds.Height);
        var (size, position) = LoupePositionCalculator.Compute(
            _loupe.RequestedSize,
            parentSize,
            bounds.Width > 0 && bounds.Height > 0,
            _port.Inverse(domainPos),
            _loupeOffset);

        _loupe.Arrange(new Rect(bounds.X + position.X, bounds.Y + position.Y, size.Width, size.Height));
    }

    /// <summary>モードに応じて内包コントロールの状態（可視性・全面暗転）を適用する。</summary>
    /// <remarks>
    /// モードと描画設定の対応はコア（<see cref="FadeMaskDragRectModes"/>）が単一所有する。<br/>
    /// ルーペは選択矩形を使うモードでのみ表示する。
    /// </remarks>
    private void ApplyMode()
    {
        bool useDragRect = FadeMaskDragRectModes.UsesDragRect(_mode);

        _overlay.ShowFullOverlay = useDragRect;
        _dragRect.IsVisible = useDragRect;
        _loupe.IsVisible = useDragRect && _loupe.Source is not null;
        // ルーペ画像上の選択矩形破線は、選択矩形を使うモードでのみ描画する（モード依存のため外観設定に依存しない）
        _showSelectionOverlay = useDragRect;

        _overlay.InvalidateVisual();
        _loupe.InvalidateVisual();
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
        if (!_showSelectionOverlay || _overlay.SelectionRect is not { } selectionRect)
        {
            return;
        }

        FadeMaskDragRectSkiaRenderer.DrawSelectionRect(
            canvas, selectionRect, captureTopLeft, scale, pixelSize, _selectionColorA, _selectionColorB);
    }

    /// <summary>選択状態を暗転マスクへ同期する。</summary>
    /// <param name="rect">選択矩形（ドメイン座標）。null は選択なし。</param>
    /// <remarks><see cref="SetSelection"/> と <see cref="ClearSelection"/> で共用する（重複排除）。</remarks>
    private void SyncSelection(EsRect? rect)
    {
        _overlay.HasSelection = rect is not null;
        _overlay.SelectionRect = rect;
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
        _loupeCenter = domainPos;
        _loupe.CenterPoint = domainPos;

        if (_loupe.Source is not null)
        {
            _loupe.IsVisible = FadeMaskDragRectModes.UsesDragRect(_mode);
        }

        InvalidateArrange();
        LoupePositionMoved?.Invoke(this, new FadeMaskDragRectPointEventArgs(domainPos));
    }
}
