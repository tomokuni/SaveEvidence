using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Estable.AvaloniaUI.DragRect;
using Estable.AvaloniaUI.FadeMask;
using Estable.AvaloniaUI.Loupe;
using Estable.AvaloniaUI.Platform;
using Estable.AvaloniaUI.Platform.Geometry;
using Estable.AvaloniaUI.Platform.Imaging;
using Estable;
using AvaloniaPoint = Avalonia.Point;
using AvaloniaRect = Avalonia.Rect;
using AvaloniaSize = Avalonia.Size;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Estable.AvaloniaUI.FadeMaskDragRect;

/// <summary>領域選択の複合コントロール（DragRect + FadeMask + Loupe の組み立て・配線・状態同期を 1 箇所に集約）。</summary>
/// <remarks>
/// <b>適用パターン</b>: Facade / Composite（内包コントロールの公開 API を統合し、ホストへ統一インターフェースを提供）。<br/>
/// <br/>
/// 3 コントロールの生成・座標変換注入・イベント購読・状態同期・後片付けの配線を本クラスに集約し、<br/>
/// ホストはライフサイクル（生成・配置・後片付け）とモード固有処理（キャプチャ・確定フロー）に専念できる（SRP）。<br/>
/// <br/>
/// <b>公開型のカプセル化</b>: 公開 API は内包コントロール（DragRectControl / FadeMaskControl / LoupeControl）の型を<br/>
/// 参照しない（イベント引数は本クラス独自の <see cref="FadeMaskDragRectSelectionEventArgs"/> / <see cref="FadeMaskDragRectPointEventArgs"/>、<br/>
/// 状態は <see cref="HasSelection"/> / <see cref="SelectionRect"/> / ハイライトプロパティで提供）。<br/>
/// 内包コントロールへの直接アクセス（<c>Loupe</c> / <c>Renderer</c> / <c>Overlay</c> / <c>DragRect</c>）は internal（テスト用）。<br/>
/// <br/>
/// <b>内部構造</b>:<br/>
/// - DragRectControl: ドラッグ選択の本体（ドメイン座標 + 座標変換ポート）。<br/>
/// - FadeMaskControl + FadeMaskRenderer: 暗転マスク・選択枠・サイズラベル・ハイライトの描画。<br/>
/// - LoupeControl: 拡大鏡（中心 = ドメイン座標、表示位置 = 座標変換ポートから自己計算）。<br/>
/// <br/>
/// <b>配線（本クラス内で完結）</b>:<br/>
/// - SelectionChanged / ViewportChanged → FadeMask の LogicalSelectionRect / HasSelection を同期 + 再描画。<br/>
/// - SelectionStarted / SelectionCancelled → FadeMask の選択状態をクリア（前の矩形の残留防止）。<br/>
/// - LoupePositionMoved → Loupe の CenterPoint を更新 + 表示開始。<br/>
/// - ConfirmRequested / CancelRequested / ZoomRequested → ホストへ中継（独自イベント引数形式）。<br/>
/// <br/>
/// <b>モード（<see cref="Mode"/>）</b>:<br/>
/// - <see cref="FadeMaskDragRectMode.Crop"/>: DragRect + FadeMask + Loupe を有効化（プレビュー内切出し・全面暗転）。<br/>
/// - <see cref="FadeMaskDragRectMode.AreaSelect"/>: DragRect + FadeMask + Loupe を有効化（全画面領域選択・全面暗転）。<br/>
/// - <see cref="FadeMaskDragRectMode.ScreenSelect"/> / <see cref="FadeMaskDragRectMode.WindowSelect"/>: FadeMask のみ有効化<br/>
///   （DragRect / Loupe は無効。ハイライト表示のみで、クリック確定フローはホストが担う）。<br/>
/// <br/>
/// <b>注意点</b>:<br/>
/// - 本クラスは Panel のサブクラスで、3 つの子コントロールを配置する（各子は ZIndex で重なり順を制御）。<br/>
/// - 全面暗転（<see cref="FadeMaskRenderer.ShowFullOverlay"/>）は Crop / AreaSelect でのみ有効<br/>
///   （ScreenSelect / WindowSelect ではハイライトのみ表示）。<br/>
/// - ルーペソース（<see cref="SetLoupeSource"/>）はホストが設定する。<br/>
/// - サイズラベルは <see cref="SizeLabelOverrideProvider"/> で描画時に評価する（Crop のズーム率追従等）。<br/>
/// </remarks>
public sealed class FadeMaskDragRectControl : Panel
{
    /// <summary>ブラシキャッシュ（色 → ブラシ。容量制限付き）。</summary>
    private static readonly Dictionary<Color, SolidColorBrush> s_brushCache = [];

    /// <summary>ブラシキャッシュの最大容量（超過時は全クリアして無制限の増加を防ぐ）。</summary>
    private const int BrushCacheCapacity = 32;

    /// <summary>色に対応するブラシを静的キャッシュから取得する（未登録なら生成して登録）。</summary>
    /// <param name="color">ブラシの色。</param>
    /// <returns>色に対応する <see cref="SolidColorBrush"/>。</returns>
    /// <remarks>容量超過時は全クリアして無制限の増加を防ぐ。</remarks>
    private static SolidColorBrush GetSharedBrush(Color color)
    {
        if (s_brushCache.TryGetValue(color, out var cached))
        {
            return cached;
        }

        var brush = new SolidColorBrush(color);
        if (s_brushCache.Count >= BrushCacheCapacity)
        {
            s_brushCache.Clear();
        }
        s_brushCache[color] = brush;
        return brush;
    }

    /// <summary>暗転マスク描画コントロール（FadeMaskRenderer を ExternalRenderer として適用）。</summary>
    private readonly FadeMaskControl _overlay = new() { IsHitTestVisible = false, ZIndex = 50, ClipToBounds = true };

    /// <summary>ドラッグ選択コントロール（選択矩形・ハンドル・nudge）。</summary>
    private readonly DragRectControl _dragRect = new() { IsHitTestVisible = true, ZIndex = 60, ClipToBounds = true };

    /// <summary>ルーペコントロール（外観・表示状態の真のソース）。</summary>
    private readonly LoupeControl _loupeControl = new() { ZIndex = 70, IsHitTestVisible = false };

    /// <summary>オーバーレイ描画レンダラー（描画状態の真のソース）。</summary>
    private readonly FadeMaskRenderer _renderer = new();

    /// <summary>動作モード（C#14 の <c>field</c> キーワードでバッキングフィールドを暗黙定義）。</summary>
    private FadeMaskDragRectMode _mode = FadeMaskDragRectMode.AreaSelect;

    /// <summary>サイズラベルを描画時に評価するプロバイダ（C#14 の <c>field</c> キーワードでバッキングフィールドを暗黙定義）。</summary>
    private Func<string?>? _sizeLabelOverrideProvider;

    /// <summary>ルーペに選択矩形の破線オーバーレイを描画するかどうか（モード依存。Crop / AreaSelect でのみ有効）。</summary>
    private bool _showSelectionOverlay;

    /// <summary>現在の外観設定（バッキングフィールド。全メンバー non-null）。</summary>
    private FadeMaskDragRectSettings _currentAppearance = new FadeMaskDragRectSettings().ToNonNull();

    /// <summary>FadeMaskDragRectControl を初期化する（子コントロールの生成・配置・配線を実施）。</summary>
    /// <remarks>
    /// コンストラクタで内包コントロール（FadeMask / DragRect / Loupe）を生成して子として配置し、<br/>
    /// イベント配線（状態同期・ホストへのイベント中継）を実施する。<br/>
    /// 全面暗転（ShowFullOverlay）は既定で有効（モードに応じて <see cref="ApplyMode"/> が調整）。<br/>
    /// ルーペは Left/Top アラインメントで配置し、表示位置は座標変換ポートから自己計算する（LoupeControl.UpdatePosition）。
    /// </remarks>
    public FadeMaskDragRectControl()
    {
        _loupeControl.HorizontalAlignment = HorizontalAlignment.Left;
        _loupeControl.VerticalAlignment = VerticalAlignment.Top;
        Children.Add(_overlay);
        Children.Add(_dragRect);
        Children.Add(_loupeControl);

        // 描画委譲（FadeMaskControl の描画を FadeMaskRenderer へ完全委譲。状態の真のソースをレンダラーに集約）
        _overlay.ExternalRenderer = _renderer;

        // ── 配線（DragRect のイベントを状態同期とホスト通知へ変換） ──
        _dragRect.SelectionChanged += OnSelectionChanged;
        _dragRect.SelectionStarted += OnSelectionStarted;
        _dragRect.SelectionCancelled += OnSelectionCancelled;
        _dragRect.ViewportChanged += OnViewportChanged;
        _dragRect.LoupePositionMoved += OnLoupePositionMoved;
        _dragRect.ConfirmRequested += (_, e) => RaiseConfirmRequested(e.Rect);
        _dragRect.CancelRequested += (_, _) => RaiseCancelRequested();
        _dragRect.ZoomRequested += (_, zoomIn) => RaiseZoomRequested(zoomIn);

        // ルーペ描画完了時に選択矩形の破線オーバーレイをホスト側で描画する
        // （LoupeControl の選択矩形 API は削除済み。描画は Rendered イベント経由で行う）
        _loupeControl.Rendered += OnLoupeRendered;

        // 全面暗転・描画モード・各コントロールの可視性は ApplyMode がモード（既定 AreaSelect）から一括適用する
        ApplyMode();
    }

    // ====================================================================
    // 公開 API: モード
    // ====================================================================

    /// <summary>動作モードを取得または設定する（内包コントロールの有効・無効と描画モードを自動切替）。</summary>
    /// <value><see cref="FadeMaskDragRectMode"/>（Crop / AreaSelect / ScreenSelect / WindowSelect）。既定値は AreaSelect。</value>
    /// <remarks>
    /// 設定時は <see cref="FadeMaskRenderer.Mode"/>（FadeMaskRenderMode）・<see cref="DragRectControl"/> の可視性・<br/>
    /// 全面暗転（<see cref="FadeMaskRenderer.ShowFullOverlay"/>）を一括で適用する。<br/>
    /// - Crop / AreaSelect: DragRect を表示し、全面暗転を有効化する。<br/>
    /// - ScreenSelect / WindowSelect: DragRect / Loupe を非表示にし、全面暗転を無効化する（ハイライトのみ）。
    /// </remarks>
    public FadeMaskDragRectMode Mode
    {
        get => _mode;
        set
        {
            if (_mode == value) return;
            _mode = value;
            ApplyMode();
        }
    }

    /// <summary>内部ロガー（内包コントロール（DragRect / Loupe）へ伝搬）。null の場合は出力なし。</summary>
    /// <value>null を設定した場合は <see cref="NullLogger"/> へフォールバックする。設定時に内包コントロール（<c>DragRectControl</c> / <c>LoupeControl</c>）へ伝搬する。</value>
    /// <remarks>
    /// C#14 の <c>field</c> キーワードでバッキングフィールドを暗黙定義する。<br/>
    /// 内包コントロールの詳細ログ（ルーペの生成・更新・エラー、ドラッグ操作）をホストのログ基盤（NLog）へ出力するために使用する。
    /// </remarks>
    public ILogger Log
    {
        get => field ?? NullLogger.Instance;
        set
        {
            field = value ?? NullLogger.Instance;
            // 内包コントロールへ伝搬する（ルーペの Trace ログ・ドラッグ操作ログをホストのログ基盤へ転送）
            _loupeControl.Log = field;
            _dragRect.Log = field;
        }
    }
    // ====================================================================
    // 公開 API: 状態・イベント・操作
    // ====================================================================

    /// <summary>現在の選択状態（選択矩形の有無）。</summary>
    /// <remarks>選択状態の真のソースは FadeMaskRenderer（HasSelection / LogicalSelectionRect）に集約し、<br/>DragRect の選択変更（SelectionChanged）から同期される。</remarks>
    public bool HasSelection => _renderer.HasSelection;

    /// <summary>現在の選択矩形（ドメイン座標）。未選択時は 0x0 の空矩形。</summary>
    /// <remarks>選択状態の真のソースは FadeMaskRenderer（HasSelection / LogicalSelectionRect）に集約し、<br/>DragRect の選択変更（SelectionChanged）から同期される。</remarks>
    public RectD SelectionRect => _renderer.LogicalSelectionRect ?? default;

    /// <summary>Enter キーまたは RequestConfirm による確定要求イベント（選択確定後のみ発火）。</summary>
    public event EventHandler<FadeMaskDragRectSelectionEventArgs>? ConfirmRequested;

    /// <summary>Escape キーまたは RequestCancel によるキャンセル要求イベント。</summary>
    public event EventHandler? CancelRequested;

    /// <summary>Ctrl+ホイールによるズーム要求イベント（true = ズームイン / false = ズームアウト）。Crop 用。</summary>
    public event EventHandler<bool>? ZoomRequested;

    /// <summary>ルーペ中心の更新イベント（ドメイン座標。ホストの追加制御用フック）。</summary>
    public event EventHandler<FadeMaskDragRectPointEventArgs>? LoupePositionMoved;

    /// <summary>ルーペ描画完了イベント（描画済み Bitmap と表示情報。ホストがオーバーレイを描画できる）。</summary>
    /// <remarks>
    /// <see cref="LoupeControl.Rendered"/> を中継する。<br/>
    /// ホスト（上位 AP）は <see cref="LoupeRenderedEventArgs.Bitmap"/> を <c>Lock</c> して<br/>
    /// 外部提供矩形（破線等）を直接描画できる（描画後は表示へ反映される）。
    /// </remarks>
    public event EventHandler<LoupeRenderedEventArgs>? LoupeRendered;

    /// <summary>選択確定要求を DragRect へ委譲する（Enter キー相当）。</summary>
    public void RequestConfirm() => _dragRect.RequestConfirm();

    /// <summary>選択キャンセル要求を DragRect へ委譲する（Escape キー相当）。</summary>
    public void RequestCancel() => _dragRect.RequestCancel();

    /// <summary>DragRect の選択矩形をプログラム的に設定する（外部復元・テスト用）。</summary>
    /// <param name="rect">選択矩形（ドメイン座標）。</param>
    /// <remarks>
    /// DragRectControl.SetSelection へ委譲する。SelectionChanged が発火し、暗転マスクとルーペも同期される。<br/>
    /// マウス操作による選択（DragRect 自身が状態を管理）とは独立した、外部からの復元用 API。
    /// </remarks>
    public void SetDragRectSelection(RectD rect) => _dragRect.SetSelection(rect);

    /// <summary>DragRect へフォーカスを設定する（矢印キー nudge の受付開始）。</summary>
    /// <remarks>DragRect はフォーカス時に自身の OnKeyDown で矢印キーを処理するため、セッション開始時に呼び出す。</remarks>
    public void FocusDragRect() => _dragRect.Focus();

    /// <summary>祖先 ScrollViewer を DragRect へ明示注入する（スクロール・ズーム検知 + ホイール操作。Crop 用）。</summary>
    /// <param name="scrollViewer">連携する ScrollViewer。null で連携解除。</param>
    public void AttachScrollViewer(ScrollViewer? scrollViewer) => _dragRect.AttachScrollViewer(scrollViewer);

    /// <summary>ビューポート（パン・ズーム・表示サイズ）の変化を内包コントロールへ通知する。</summary>
    /// <remarks>
    /// ビューポート実装（ScrollViewer / RenderTransform ベース等）に依存せず、ホスト（上位 AP）が<br/>
    /// ビューポート変化を検知した際に本メソッドで中継する（ScrollViewer 連携の <c>ScrollChanged</c> 経由の<br/>
    /// <see cref="DragRectControl.ViewportChanged"/> も本メソッドへ統合される）。<br/>
    /// DragRect（選択枠）・FadeMask（暗転マスク）の再描画と、ルーペ表示位置の再計算を要求する。<br/>
    /// 座標変換ポートは呼び出し毎に最新の表示状態を評価するため、再描画だけで追従する。
    /// </remarks>
    public void NotifyViewportChanged()
    {
        _dragRect.InvalidateVisual();
        _overlay.InvalidateVisual();
        _loupeControl.RefreshPosition();
    }

    // ====================================================================
    // 公開 API: 設定・座標変換
    // ====================================================================

    /// <summary>現在の外観設定（DragRect / FadeMask / Loupe の外観）を取得または設定する。</summary>
    /// <value>現在有効な外観設定（全メンバー non-null の <see cref="FadeMaskDragRectSettings"/>）。</value>
    /// <remarks>
    /// getter は現在有効な値（全メンバー non-null）を返す（メソッドではなくプロパティのため、現在値が常に参照できる）。<br/>
    /// setter は null メンバーのみ現在値を維持する部分更新。<br/>
    /// マージ結果が現在の設定と等しい場合（同じ値の再設定）は更新をスキップし、無駄な再適用を防ぐ。<br/>
    /// 設定後はオーバーレイと DragRect の再描画を要求する（選択状態・ルーペ表示位置は維持される）。<br/>
    /// ホストの設定型（CaptureSettings 等）から変換した <see cref="FadeMaskDragRectSettings"/> を受け取る（複合コントロールはホストの設定型に依存しない）。
    /// </remarks>
    public FadeMaskDragRectSettings AppearanceSettings
    {
        get => _currentAppearance;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            var merged = MergeAppearance(value, _currentAppearance);
            // 同じ値が設定された場合は更新しない（record の等値比較。部分更新の入力が全て現在値と一致する場合）
            if (merged == _currentAppearance) return;
            _currentAppearance = merged;
            ApplyAppearance(merged);
        }
    }

    /// <summary>null のメンバーを現在値で埋めた設定を生成する（部分更新マージ）。</summary>
    /// <param name="input">入力設定。null メンバーは <paramref name="current"/> の値が使用される。</param>
    /// <param name="current">現在の設定（部分更新の維持元。全メンバー non-null）。</param>
    /// <returns>全メンバー non-null の設定。</returns>
    /// <remarks>部分更新（null は現在値維持）を実現するためのマージ処理。record の with 式では nullable メンバーごとの分岐が必要なため、本メソッドで一元化する。</remarks>
    private static FadeMaskDragRectSettings MergeAppearance(FadeMaskDragRectSettings input, FadeMaskDragRectSettings current)
    {
        var i = input;
        var cur = current;
        return new FadeMaskDragRectSettings
        {
            // DragRect 外観
            MinSelectionSize = i.MinSelectionSize ?? cur.MinSelectionSize,
            BorderColor = i.BorderColor ?? cur.BorderColor,
            BorderThickness = i.BorderThickness ?? cur.BorderThickness,
            HandleColor = i.HandleColor ?? cur.HandleColor,
            HandleSize = i.HandleSize ?? cur.HandleSize,
            HandleHitRadius = i.HandleHitRadius ?? cur.HandleHitRadius,
            ClampToDomainBounds = i.ClampToDomainBounds ?? cur.ClampToDomainBounds,
            MarkerColor = i.MarkerColor ?? cur.MarkerColor,
            MarkerLength = i.MarkerLength ?? cur.MarkerLength,
            // カーソル座標ラベル（DragRect）
            ShowCoordLabel = i.ShowCoordLabel ?? cur.ShowCoordLabel,
            CoordLabelColor = i.CoordLabelColor ?? cur.CoordLabelColor,
            CoordLabelBackgroundColor = i.CoordLabelBackgroundColor ?? cur.CoordLabelBackgroundColor,
            CoordLabelFontSize = i.CoordLabelFontSize ?? cur.CoordLabelFontSize,
            // FadeMask 外観
            Scaling = i.Scaling ?? cur.Scaling,
            DisplayScaleScaling = i.DisplayScaleScaling ?? cur.DisplayScaleScaling,
            OverlayColor = i.OverlayColor ?? cur.OverlayColor,
            TextColor = i.TextColor ?? cur.TextColor,
            FontSize = i.FontSize ?? cur.FontSize,
            ShowSizeLabel = i.ShowSizeLabel ?? cur.ShowSizeLabel,
            InfoLabelVisible = i.InfoLabelVisible ?? cur.InfoLabelVisible,
            InfoLabelColor = i.InfoLabelColor ?? cur.InfoLabelColor,
            InfoLabelFontSize = i.InfoLabelFontSize ?? cur.InfoLabelFontSize,
            ShowSubLines = i.ShowSubLines ?? cur.ShowSubLines,
            SubLineFontSize = i.SubLineFontSize ?? cur.SubLineFontSize,
            SubLineColor = i.SubLineColor ?? cur.SubLineColor,
            SubLineBackgroundColor = i.SubLineBackgroundColor ?? cur.SubLineBackgroundColor,
            InfoLabelBackgroundColor = i.InfoLabelBackgroundColor ?? cur.InfoLabelBackgroundColor,
            TextBackgroundColor = i.TextBackgroundColor ?? cur.TextBackgroundColor,
            // Loupe 外観
            RequestedSize = i.RequestedSize ?? cur.RequestedSize,
            ZoomLevel = i.ZoomLevel ?? cur.ZoomLevel,
            RenderScaling = i.RenderScaling ?? cur.RenderScaling,
            CrossColor = i.CrossColor ?? cur.CrossColor,
            CheckerSize = i.CheckerSize ?? cur.CheckerSize,
            CheckerColorA = i.CheckerColorA ?? cur.CheckerColorA,
            CheckerColorB = i.CheckerColorB ?? cur.CheckerColorB,
            FrameColor = i.FrameColor ?? cur.FrameColor,
            FrameWidth = i.FrameWidth ?? cur.FrameWidth,
            SelectionRectColorA = i.SelectionRectColorA ?? cur.SelectionRectColorA,
            SelectionRectColorB = i.SelectionRectColorB ?? cur.SelectionRectColorB,
            PositionOffset = i.PositionOffset ?? cur.PositionOffset,
            // ルーペ座標ラベル
            LoupeShowCoordLabel = i.LoupeShowCoordLabel ?? cur.LoupeShowCoordLabel,
            LoupeCoordLabelColor = i.LoupeCoordLabelColor ?? cur.LoupeCoordLabelColor,
            LoupeCoordLabelBackgroundColor = i.LoupeCoordLabelBackgroundColor ?? cur.LoupeCoordLabelBackgroundColor,
            LoupeCoordLabelFontSize = i.LoupeCoordLabelFontSize ?? cur.LoupeCoordLabelFontSize,
        };
    }

    /// <summary>外観設定（DragRect / FadeMask / Loupe の外観）を内包コントロールへ反映する。</summary>
    /// <param name="settings">外観設定（全メンバー non-null）。</param>
    /// <remarks>
    /// 適用後はオーバーレイと DragRect の再描画を要求する（選択状態・ルーペ表示位置は維持される）。
    /// </remarks>
    private void ApplyAppearance(FadeMaskDragRectSettings settings)
    {
        // DragRect 外観
        _dragRect.MinSelectionSize = settings.MinSelectionSize!.Value;
        _dragRect.BorderColor = settings.BorderColor!.Value;
        _dragRect.BorderThickness = settings.BorderThickness!.Value;
        _dragRect.HandleColor = settings.HandleColor!.Value;
        _dragRect.HandleSize = settings.HandleSize!.Value;
        _dragRect.HandleHitRadius = settings.HandleHitRadius!.Value;
        _dragRect.ClampToDomainBounds = settings.ClampToDomainBounds!.Value;
        _dragRect.MarkerColor = settings.MarkerColor!.Value;
        _dragRect.MarkerLength = settings.MarkerLength!.Value;
        // カーソル座標ラベル（DragRect）
        _dragRect.ShowCoordLabel = settings.ShowCoordLabel!.Value;
        _dragRect.CoordLabelColor = settings.CoordLabelColor!.Value;
        _dragRect.CoordLabelBackgroundColor = settings.CoordLabelBackgroundColor!.Value;
        _dragRect.CoordLabelFontSize = settings.CoordLabelFontSize!.Value;

        // FadeMask 外観（FadeMaskRenderer へ直接設定。境界変換は座標変換ポートが担う）
        // 色は不透明度をアルファに統合した値（#AARRGGBB）をそのまま設定する。
        _renderer.Scaling = settings.Scaling!.Value;
        _renderer.DisplayScaleScaling = settings.DisplayScaleScaling!.Value;
        _renderer.OverlayArgbColor = settings.OverlayColor!.Value;
        _renderer.TextColor = settings.TextColor!.Value;
        _renderer.TextBackgroundColor = settings.TextBackgroundColor!.Value;
        _renderer.FontSize = settings.FontSize!.Value;
        _renderer.ShowSizeLabel = settings.ShowSizeLabel!.Value;
        _renderer.BorderColor = settings.BorderColor!.Value;
        _renderer.BorderThickness = settings.BorderThickness!.Value;
        _renderer.InfoLabelColor = settings.InfoLabelColor!.Value;
        _renderer.InfoLabelBackgroundColor = settings.InfoLabelBackgroundColor!.Value;
        _renderer.InfoLabelFontSize = settings.InfoLabelFontSize!.Value;
        _renderer.InfoLabelVisible = settings.InfoLabelVisible!.Value;
        // サブライン（サイズラベルの下の 2 行目以降）
        _renderer.ShowSubLines = settings.ShowSubLines!.Value;
        _renderer.SubLineFontSize = settings.SubLineFontSize!.Value;
        _renderer.SubLineColor = settings.SubLineColor!.Value;
        _renderer.SubLineBackgroundColor = settings.SubLineBackgroundColor!.Value;

        // Loupe 外観（LoupeControl の公開外観設定を全て適用）
        _loupeControl.RequestedSize = settings.RequestedSize!.Value;
        _loupeControl.ZoomLevel = settings.ZoomLevel!.Value;
        _loupeControl.RenderScaling = settings.RenderScaling!.Value;
        _loupeControl.CrossColor = GetSharedBrush(settings.CrossColor!.Value);
        _loupeControl.CheckerSize = settings.CheckerSize!.Value;
        _loupeControl.CheckerColorA = GetSharedBrush(settings.CheckerColorA!.Value);
        _loupeControl.CheckerColorB = GetSharedBrush(settings.CheckerColorB!.Value);
        _loupeControl.FrameColor = GetSharedBrush(settings.FrameColor!.Value);
        _loupeControl.FrameWidth = settings.FrameWidth!.Value;
        _loupeControl.PositionOffset = settings.PositionOffset!.Value;
        // ルーペ座標ラベル
        _loupeControl.ShowCoordLabel = settings.LoupeShowCoordLabel!.Value;
        _loupeControl.CoordLabelColor = GetSharedBrush(settings.LoupeCoordLabelColor!.Value);
        _loupeControl.CoordLabelBackgroundColor = GetSharedBrush(settings.LoupeCoordLabelBackgroundColor!.Value);
        _loupeControl.CoordLabelFontSize = settings.LoupeCoordLabelFontSize!.Value;

        _overlay.InvalidateVisual();
        _dragRect.InvalidateVisual();
    }

    /// <summary>ドメイン ↔ コントロールローカル DIP の座標変換ポートを全内包コントロールへ一括設定する。</summary>
    /// <param name="port">座標変換ポート（Forward = DIP→ドメイン / Inverse = ドメイン→DIP / Bounds = ドメイン境界）。null の場合は等価変換。</param>
    /// <remarks>
    /// DragRect / FadeMask / Loupe の 3 コントロールへ同一ポートを分配する（1 回の呼び出しで設定完了）。<br/>
    /// 変換関数は呼び出し毎に評価されるため、ズーム・スクロール・ウィンドウサイズ変更等のレイアウト変化に追従する。
    /// </remarks>
    public void SetCoordinateTransform(ICoordinatePort? port)
    {
        _dragRect.SetCoordinateTransform(port);
        _overlay.SetCoordinateTransform(port);
        _loupeControl.SetCoordinateTransform(port);
    }

    /// <summary>サイズラベルを描画時に評価するプロバイダを取得または設定する。</summary>
    /// <value>描画時に呼び出される関数。null の場合は自動生成（FadeMaskDrawer の既定）。</value>
    /// <remarks>
    /// ホストは最新の選択状態（<see cref="SelectionRect"/> / <see cref="HasSelection"/> 等）からサイズラベル文字列を生成するクロージャを設定する。<br/>
    /// - Crop: 論理座標（元画像ピクセル）+ 実表示ズーム率で生成（レイアウト変化に追従）。<br/>
    /// - 範囲選択（AreaSelect）等: 設定値（<c>CaptureSettings.SizeLabelOverride</c>）を返す。<br/>
    /// 設定時はレンダラーへ伝搬し、再描画を要求する。
    /// </remarks>
    public Func<string?>? SizeLabelOverrideProvider
    {
        get => _sizeLabelOverrideProvider;
        set
        {
            _sizeLabelOverrideProvider = value;
            _renderer.SizeLabelOverrideProvider = value;
            _overlay.InvalidateVisual();
        }
    }

    // ====================================================================
    // 公開 API: 状態設定・テスト用アクセサ
    // ====================================================================

    /// <summary>選択状態をオーバーレイ描画（FadeMask + ルーペの選択枠）へ反映する。</summary>
    /// <param name="rect">選択矩形（ドメイン座標）。サイズ 0 以下は選択なし。</param>
    /// <remarks>
    /// ホスト（上位 AP）が選択矩形を外部から設定する際に使用する。<br/>
    /// DIP 矩形の算出は描画時に行う（<see cref="SetCoordinateTransform"/> で設定した座標変換ポートが<br/>
    /// 最新のレイアウトから導出する。レイアウト変化に常に追従）。
    /// </remarks>
    public void SetSelection(RectD rect)
    {
        var hasSelection = rect.Width > 0 && rect.Height > 0;
        SyncSelectionState(hasSelection ? rect : null);
    }

    /// <summary>選択状態をクリアして全面暗転（穴あけなし）に戻す（新規選択開始・キャンセル時）。</summary>
    /// <remarks>矩形外クリックのドラッグ開始やキャンセル時の FadeMask 残留を防止する（DragRect は非表示でも状態が残るため）。</remarks>
    public void ClearSelection() => SyncSelectionState(null);

    /// <summary>選択状態を FadeMask レンダラーとルーペへ同期して再描画する。</summary>
    /// <param name="rect">選択矩形（ドメイン座標）。null は選択なし。</param>
    /// <remarks>
    /// <see cref="SetSelection"/> と <see cref="ClearSelection"/> で共用する（重複排除）。<br/>
    /// 選択状態の真のソースは <see cref="FadeMaskRenderer"/>（HasSelection / LogicalSelectionRect）に集約し、<br/>
    /// ルーペ画像上の選択枠（破線）も同期する。DIP 矩形の算出は描画時に行う（レイアウト変化に追従）。
    /// </remarks>
    private void SyncSelectionState(RectD? rect)
    {
        _renderer.HasSelection = rect is not null;
        _renderer.LogicalSelectionRect = rect;
        _overlay.InvalidateVisual();
    }

    /// <summary>ルーペの中心位置（ドメイン座標）を設定して表示を開始する。</summary>
    /// <param name="domainPos">ルーペ中心（ドメイン = 元画像ピクセル / 仮想スクリーンピクセル）。</param>
    /// <remarks>
    /// 表示位置・サイズは LoupeControl が座標変換ポート（<see cref="SetCoordinateTransform"/>）から自己計算する<br/>
    /// （カーソル右下 + オフセット + クランプ）。配置前（Parent 未接続）の呼び出しは無視される。<br/>
    /// <b>Source 未設定の間は表示しない</b>（未設定の市松模様を防ぐ。Source 設定後に呼び出し直すことで表示される）。
    /// </remarks>
    public void SetLoupePosition(PointD domainPos)
    {
        if (_loupeControl.Parent is null) return;
        UpdateLoupePosition(domainPos);
    }

    /// <summary>ルーペの中心位置（ドメイン座標）を設定して表示を開始する（Source 設定済みの場合のみ表示）。</summary>
    /// <param name="domainPos">ルーペ中心（ドメイン座標）。</param>
    /// <remarks>
    /// <see cref="SetLoupePosition"/> と <see cref="OnLoupePositionMoved"/> で共用する（重複排除）。<br/>
    /// 表示位置・サイズは LoupeControl が座標変換ポート（<see cref="SetCoordinateTransform"/>）から自己計算する。<br/>
    /// <b>Source 未設定の間は表示しない</b>（未設定の市松模様を防ぐ）。
    /// </remarks>
    private void UpdateLoupePosition(PointD domainPos)
    {
        // LoupeControl.CenterPoint は StyledProperty（UI 境界）のため、境界で独自型 → Avalonia 型へ変換する。
        _loupeControl.CenterPoint = domainPos.ToAvaloniaPoint();
        // Source が設定済みの場合のみ表示する（未設定の市松模様を防ぐ）
        if (_loupeControl.Source is not null)
        {
            _loupeControl.IsVisible = true;
        }
    }

    /// <summary>FadeMask オーバーレイの再描画を要求する。</summary>
    /// <remarks>レンダラーの状態（Highlight / Selection 等）を変更した後に呼び出す。</remarks>
    public void InvalidateOverlay() => _overlay.InvalidateVisual();

    /// <summary>ルーペの拡大元画像（Source）を設定する。</summary>
    /// <param name="source">拡大元画像。null で解除。</param>
    /// <remarks>LoupeControl.Source へ委譲する（Source 未設定の間はルーペを表示しない市松模様防止のため、<br/>表示開始は <see cref="SetLoupePosition"/> が担う）。</remarks>
    public void SetLoupeSource(IImage? source) => _loupeControl.Source = source;

    /// <summary>ルーペを非表示にして現在の Bitmap を解放する。</summary>
    /// <remarks>セッション終了時の後片付けに使用する（LoupeControl.Clear へ委譲）。</remarks>
    public void ClearLoupe() => _loupeControl.Clear();

    /// <summary>ルーペ中心（ドメイン座標）。未設定時は null。</summary>
    /// <remarks>ルーペの表示状態の確認（テスト・座標検証）に使用する。</remarks>
    public PointD? LoupeCenterPoint => _loupeControl.CenterPoint?.ToPointD();

    /// <summary>ルーペコントロール（Source 設定・テスト用アクセサ）。</summary>
    internal LoupeControl Loupe => _loupeControl;

    /// <summary>オーバーレイ描画レンダラー（テスト用アクセサ・ハイライト設定用）。</summary>
    internal FadeMaskRenderer Renderer => _renderer;

    /// <summary>FadeMaskControl（オーバーレイ描画コントロール。テスト用アクセサ）。</summary>
    internal FadeMaskControl Overlay => _overlay;

    /// <summary>DragRectControl（テスト用アクセサ）。</summary>
    internal DragRectControl DragRect => _dragRect;

    // ====================================================================
    // 公開 API: ハイライト状態（ScreenSelect / WindowSelect 用）
    // ====================================================================

    /// <summary>ハイライト矩形（ドメイン座標）。未検出時は null。</summary>
    /// <remarks>FadeMaskRenderer.LogicalHighlightRect へ委譲する。設定後は <see cref="InvalidateOverlay"/> で再描画する。</remarks>
    public RectD? LogicalHighlightRect
    {
        get => _renderer.LogicalHighlightRect;
        set => _renderer.LogicalHighlightRect = value;
    }

    /// <summary>ハイライト表示の有無。</summary>
    public bool HasHighlight
    {
        get => _renderer.HasHighlight;
        set => _renderer.HasHighlight = value;
    }

    /// <summary>ハイライト対象のスクリーンインデックス（未検出時は -1）。</summary>
    public int ScreenIndex
    {
        get => _renderer.ScreenIndex;
        set => _renderer.ScreenIndex = value;
    }

    /// <summary>ハイライト描画用の表示スケール（マルチスクリーン補正）。</summary>
    public double DisplayScaleScaling
    {
        get => _renderer.DisplayScaleScaling;
        set => _renderer.DisplayScaleScaling = value;
    }

    /// <summary>ハイライト対象のウィンドウタイトル（未検出時は空文字）。</summary>
    public string WindowTitle { get => _renderer.WindowTitle; set => _renderer.WindowTitle = value; }

    /// <summary>全面暗転（穴あけなし）を表示するかどうか（Crop / AreaSelect で有効）。</summary>
    /// <remarks>モード（<see cref="Mode"/>）から導出される描画状態の読み取り専用プロパティ。</remarks>
    public bool ShowFullOverlay => _renderer.ShowFullOverlay;

    // ====================================================================
    // 内部実装（配線・状態同期）
    // ====================================================================

    /// <summary>モードに応じて内包コントロールの状態（描画モード・可視性・全面暗転）を適用する。</summary>
    /// <remarks>
    /// - 描画モード: FadeMaskRenderMode（Crop / AreaSelect / ScreenSelect / WindowSelect）へ変換する。<br/>
    /// - DragRect / Loupe: Crop / AreaSelect でのみ有効（ScreenSelect / WindowSelect では非表示）。<br/>
    /// - 全面暗転: Crop / AreaSelect でのみ有効（ScreenSelect / WindowSelect ではハイライトのみ表示）。
    /// </remarks>
    private void ApplyMode()
    {
        _renderer.Mode = _mode switch
        {
            FadeMaskDragRectMode.Crop => FadeMaskRenderMode.Crop,
            FadeMaskDragRectMode.AreaSelect => FadeMaskRenderMode.AreaSelect,
            FadeMaskDragRectMode.ScreenSelect => FadeMaskRenderMode.ScreenSelect,
            _ => FadeMaskRenderMode.WindowSelect,
        };
        bool useDragRect = _mode is FadeMaskDragRectMode.Crop or FadeMaskDragRectMode.AreaSelect;
        _dragRect.IsVisible = useDragRect;
        _renderer.ShowFullOverlay = useDragRect;
        // 選択位置の視覚化用に、ルーペの選択矩形破線オーバーレイをモードに応じて有効化する
        // （描画は Rendered イベント経由でホスト側が行う。モード依存設定のため ApplyMode で管理）。
        _showSelectionOverlay = useDragRect;
        _overlay.InvalidateVisual();
    }

    /// <summary>選択状態をオーバーレイ描画へ反映する（SelectionChanged）。</summary>
    /// <param name="sender">イベント送信元（DragRectControl）。</param>
    /// <param name="e">選択矩形イベント（Rect はドメイン座標）。</param>
    /// <remarks>
    /// 選択状態の真のソースは <see cref="FadeMaskRenderer"/>（HasSelection / LogicalSelectionRect）に集約し、<br/>
    /// ルーペ画像上の選択枠（破線）も同期する。DIP 矩形の算出は描画時に行う（レイアウト変化に追従）。
    /// </remarks>
    private void OnSelectionChanged(object? sender, SelectionRectEventArgs e)
        => SetSelection(e.Rect);

    /// <summary>ビューポート変化（DragRect の ViewportChanged = ScrollViewer 連携）を内包コントロールへ反映する。</summary>
    /// <param name="sender">イベント送信元（DragRectControl）。</param>
    /// <param name="e">選択矩形イベント（Rect はドメイン座標）。</param>
    /// <remarks>
    /// <see cref="NotifyViewportChanged"/> と同じ処理を実行する（ScrollViewer 連携とホスト通知を単一経路に統合）。<br/>
    /// 選択矩形（ドメイン座標）はビューポート変化で不変のため、選択状態の再同期（SetSelection）は行わず、<br/>
    /// 再描画（DragRect / FadeMask）とルーペ表示位置の再計算のみを要求する。
    /// </remarks>
    private void OnViewportChanged(object? sender, SelectionRectEventArgs e) => NotifyViewportChanged();

    /// <summary>新規選択のドラッグ開始時に、前の選択矩形の暗転マスクをクリアする。</summary>
    /// <param name="sender">イベント送信元（DragRectControl）。</param>
    /// <param name="e">イベント引数。</param>
    /// <remarks>
    /// 矩形外クリックで新規選択が開始されると DragRect の選択矩形はサイズ 0 になり非表示になるが、<br/>
    /// ドラッグ開始（SelectionChanged）までは FadeMask に前の矩形が残る。<br/>
    /// ここで選択状態をクリアして全面暗転（穴あけなし）に戻す（押下直後の FadeMask 残留を防止）。
    /// </remarks>
    private void OnSelectionStarted(object? sender, DragRectPointEventArgs e) => ClearSelection();

    /// <summary>選択ドラッグがキャンセルされた（最小サイズ未満）時に、暗転マスクをクリアする。</summary>
    /// <param name="sender">イベント送信元（DragRectControl）。</param>
    /// <param name="e">イベント引数。</param>
    /// <remarks>ドラッグが小さすぎてキャンセルされた場合も、FadeMask に矩形が残らないようにする。</remarks>
    private void OnSelectionCancelled(object? sender, EventArgs e) => ClearSelection();

    // ====================================================================
    // ルーペの選択矩形破線オーバーレイ（Rendered イベント経由・ホスト側描画）
    // ====================================================================

    /// <summary>ルーペの描画完了時に、選択矩形の破線オーバーレイを描画済み Bitmap へ描く。</summary>
    /// <param name="sender">イベント送信元（LoupeControl）。</param>
    /// <param name="e">描画済み Bitmap と表示情報。</param>
    /// <remarks>
    /// 選択矩形の破線は <see cref="LoupeControl"/> 内部では描画せず、本クラス（ホスト側）が<br/>
    /// <see cref="LoupeControl.Rendered"/> イベントで通知された <see cref="LoupeRenderedEventArgs.Bitmap"/> へ直接描画する。<br/>
    /// 選択矩形（<see cref="SelectionRect"/> = ドメイン座標）は <c>(ドメイン座標 − CaptureTopLeft) × Scale</c> で<br/>
    /// ルーペ画像ピクセル座標へ変換する。<br/>
    /// ルーペ破線の色は <see cref="_currentAppearance"/>（<c>SelectionRectColorA / B</c>）を使用する。
    /// </remarks>
    private void OnLoupeRendered(object? sender, LoupeRenderedEventArgs e)
    {
        if (!_showSelectionOverlay || e.CaptureTopLeft is not { } topLeft)
        {
            // 選択矩形非表示でも、ホストへルーペ描画完了を中継する（外部提供矩形の描画用）
            RaiseLoupeRendered(e);
            return;
        }

        if (_renderer.LogicalSelectionRect is { } selectionRect)
        {
            var colorA = _currentAppearance.SelectionRectColorA;
            var colorB = _currentAppearance.SelectionRectColorB;
            if (colorA is not null && colorB is not null)
            {
                // 破線色（色A / 色B）を 2 つ用意し、市松風に交互に塗る
                DrawSelectionRectOverlay(e.Bitmap, selectionRect, topLeft, e.Scale, colorA.Value, colorB.Value);
            }
        }

        // ホスト（上位 AP）へルーペ描画完了を中継する（外部提供矩形の描画用）
        RaiseLoupeRendered(e);
    }

    /// <summary><see cref="LoupeRendered"/> イベントを発火する。</summary>
    /// <param name="e">ルーペ描画完了イベント引数。</param>
    private void RaiseLoupeRendered(LoupeRenderedEventArgs e) => LoupeRendered?.Invoke(this, e);

    /// <summary>選択矩形の 4 辺を 2 色破線でルーペ Bitmap へ描画する。</summary>
    /// <param name="bitmap">描画対象の PbBitmap。</param>
    /// <param name="selectionRect">選択矩形（ドメイン座標系・半開区間）。</param>
    /// <param name="captureTopLeft">ルーペの切り出し領域の左上（ドメイン座標系）。</param>
    /// <param name="scale">ドメイン座標 → ルーペ画像ピクセル座標の変換係数。</param>
    /// <param name="colorA">破線の色A（明色）。</param>
    /// <param name="colorB">破線の色B（暗色）。</param>
    /// <remarks>
    /// 選択矩形（ドメイン座標）をルーペピクセル座標へ変換（<c>(rect − captureTopLeft) × scale</c>）し、<br/>
    /// 4 辺を 1px 破線（1px 描画 → 1px 空白）で描画する（色A / 色B を 1 ピクセルごとに交互に切り替える）。<br/>
    /// 選択矩形は半開区間のため、終点のピクセルを含むよう +1（= 1 ドメインピクセル）する（閉区間化）。<br/>
    /// ズーム 1 倍では破線が潰れるため描画しない（2 倍以上で有効）。<br/>
    /// 純粋描画のため <c>internal</c> で公開し、単体テストから検証できるようにする。
    /// </remarks>
    internal static void DrawSelectionRectOverlay(
        PbBitmap bitmap, RectD selectionRect, PointD captureTopLeft, double scale, Color colorA, Color colorB)
    {
        if (scale < 2)
        {
            return;
        }

        // ドメイン座標 → ルーペピクセル座標（半開区間の終点を含むよう +1 する）
        double x = (selectionRect.X - captureTopLeft.X) * scale;
        double y = (selectionRect.Y - captureTopLeft.Y) * scale;
        double w = (selectionRect.Width + 1) * scale;
        double h = (selectionRect.Height + 1) * scale;

        // ルーペの切り出し領域と交差しない場合は描画しない
        var pixelSize = bitmap.Size.Width;
        if (x + w < 0 || y + h < 0 || x > pixelSize || y > pixelSize)
        {
            return;
        }

        // 上辺・下辺（x 方向）と左辺・右辺（y 方向）を描画する
        DrawDashedRectEdge(bitmap, new PointD(x, y), new PointD(x + w, y), colorA, colorB);
        DrawDashedRectEdge(bitmap, new PointD(x, y + h), new PointD(x + w, y + h), colorA, colorB);
        DrawDashedRectEdge(bitmap, new PointD(x, y), new PointD(x, y + h), colorA, colorB);
        DrawDashedRectEdge(bitmap, new PointD(x + w, y), new PointD(x + w, y + h), colorA, colorB);
    }

    /// <summary>PbBitmap 上に 2 色のダッシュ破線（1px 描画 → 1px 空白）を描画する。</summary>
    /// <param name="bitmap">描画対象の PbBitmap。</param>
    /// <param name="from">始点（ルーペピクセル座標）。</param>
    /// <param name="to">終点（ルーペピクセル座標）。</param>
    /// <param name="colorA">破線の色A。</param>
    /// <param name="colorB">破線の色B。</param>
    /// <remarks>水平または垂直の辺を想定する。色A / 色B を 1 ピクセルごとに交互に切り替える（市松風）。</remarks>
    private static void DrawDashedRectEdge(PbBitmap bitmap, PointD from, PointD to, Color colorA, Color colorB)
    {
        bool horizontal = Math.Abs(to.Y - from.Y) < Math.Abs(to.X - from.X);
        double length = horizontal ? to.X - from.X : to.Y - from.Y;
        double start = horizontal ? from.X : from.Y;
        double fixedCoord = horizontal ? from.Y : from.X;
        double sign = length >= 0 ? 1 : -1;
        double distance = Math.Abs(length);

        // 破線パターン: 1px 描画 → 1px 空白。色は 1 ピクセルごとに色A → 色B → 色A → … と交互に切り替える
        const int dash = 1;
        const int gap = 1;
        const int step = dash + gap;

        var width = bitmap.Size.Width;
        var height = bitmap.Size.Height;

        int dashIndex = 0;
        for (double pos = 0; pos < distance; pos += step)
        {
            double segStart = start + sign * pos;
            double segEnd = start + sign * Math.Min(pos + dash, distance);
            // ダッシュごとに色を交互（0 始まり = 色A）
            var color = dashIndex % 2 == 0 ? colorA : colorB;
            dashIndex++;

            if (horizontal)
            {
                for (var px = (int)Math.Round(segStart); px <= (int)Math.Round(segEnd); px++)
                {
                    bitmap.TrySetPixel(px, (int)Math.Round(fixedCoord), color);
                }
            }
            else
            {
                for (var py = (int)Math.Round(segStart); py <= (int)Math.Round(segEnd); py++)
                {
                    bitmap.TrySetPixel((int)Math.Round(fixedCoord), py, color);
                }
            }
        }
    }

    /// <summary>ルーペ更新（LoupePositionMoved）。中心はドメイン座標のまま LoupeControl に設定する。</summary>
    /// <param name="sender">イベント送信元（DragRectControl）。</param>
    /// <param name="e">ルーペ中心（ドメイン座標）。</param>
    /// <remarks>
    /// 表示位置・サイズは LoupeControl が座標変換ポートから自己計算する（カーソル右下 + オフセット + クランプ）。<br/>
    /// <b>Source 未設定の間は表示しない</b>（AreaSelect の背景キャプチャ完了前は Source が null のため、<br/>
    /// 表示すると市松模様が見える。Source 設定後の表示開始はホスト（<see cref="SetLoupePosition"/> 経由）が行う）。<br/>
    /// ホストへの通知（<see cref="LoupePositionMoved"/>）は追加制御（Source 設定等）のフックとして発行する。
    /// </remarks>
    private void OnLoupePositionMoved(object? sender, DragRectPointEventArgs e)
    {
        UpdateLoupePosition(e.Point);
        RaiseLoupePositionMoved(e.Point);
    }

    // ====================================================================
    // イベント発火（ホストへの通知。発火形式を一貫させるため private ヘルパーに集約）
    // ====================================================================

    /// <summary>確定要求イベントを発火する。</summary>
    /// <param name="rect">確定した選択矩形（ドメイン座標）。</param>
    private void RaiseConfirmRequested(RectD rect)
        => ConfirmRequested?.Invoke(this, new FadeMaskDragRectSelectionEventArgs(rect));

    /// <summary>キャンセル要求イベントを発火する。</summary>
    private void RaiseCancelRequested()
        => CancelRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>ズーム要求イベントを発火する。</summary>
    /// <param name="zoomIn">ズームインする場合は true。</param>
    private void RaiseZoomRequested(bool zoomIn)
        => ZoomRequested?.Invoke(this, zoomIn);

    /// <summary>ルーペ中心更新イベントを発火する。</summary>
    /// <param name="domainPos">ルーペ中心（ドメイン座標）。</param>
    private void RaiseLoupePositionMoved(PointD domainPos)
        => LoupePositionMoved?.Invoke(this, new FadeMaskDragRectPointEventArgs(domainPos));

    // ====================================================================
    // レイアウト
    // ====================================================================

    /// <summary>子コントロール（FadeMask / DragRect / Loupe）をパネル全面へ配置する。</summary>
    /// <param name="finalSize">パネルの最終サイズ。</param>
    /// <returns>パネルの最終サイズ。</returns>
    /// <remarks>
    /// 全子コントロールを同一矩形（パネル全面）に Arrange する。<br/>
    /// FadeMask / DragRect は Stretch で全面に広がり、Loupe は Left/Top アラインメントで<br/>
    /// 自身の Width/Height（座標変換ポートから算出）に配置される（重なり順は ZIndex で制御）。
    /// </remarks>
    protected override AvaloniaSize ArrangeOverride(AvaloniaSize finalSize)
    {
        // 全子コントロールを同一矩形（パネル全面）に Arrange する（矩形は一度だけ生成して共有）
        var fullRect = new AvaloniaRect(finalSize);
        foreach (var child in Children)
        {
            child.Arrange(fullRect);
        }
        return finalSize;
    }
}
