using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Estable.AvaloniaUI.Marquee;
using Estable.AvaloniaUI.Loupe;
using Estable.Marquee;
using Estable.Marquee.CoreForControl;
using Estable.FadeMaskDragRect;
using EsUtil.Platform.AvaloniaUI;
using EsUtil.Platform.AvaloniaUI.Imaging;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using Microsoft.Extensions.Logging;
using System.Globalization;
using DrawingColor = System.Drawing.Color;

namespace Estable.AvaloniaUI.FadeMaskDragRect;

/// <summary>領域選択の複合コントロール（選択矩形 + 暗転マスク + ルーペの組み立て・配線・状態同期を 1 箇所に集約）。</summary>
/// <remarks>
/// <b>適用パターン</b>: Facade / Composite（内包コントロールの公開 API を統合し、ホストへ統一インターフェースを提供）。<br/>
/// <br/>
/// 内包コントロールの生成・座標変換注入・イベント購読・状態同期・後片付けの配線を本クラスに集約し、<br/>
/// ホストはライフサイクル（生成・配置・後片付け）とモード固有処理（キャプチャ・確定フロー）に専念できる（SRP）。<br/>
/// <br/>
/// <b>UI 層の責務（本クラス）</b>: 設定（<see cref="Options"/>）とモード（<see cref="Mode"/>）を内包コントロールへ分配し、<br/>
/// 入力の受付（内包コントロールが担う）・効果の適用・通知の転送・描画先の選択のみを行う。<br/>
/// モードごとの可否（選択矩形・ルーペ・全面暗転・破線）はコアの <c>FadeMaskDragRectModes</c> が、<br/>
/// 選択矩形の可否はコアの <c>FadeMaskDragRectSelection</c> が単一所有する（UI 層は判断を持たず、コアの結果を割り当てるだけになる）。<br/>
/// <br/>
/// <b>公開型のカプセル化</b>: 公開 API は内包コントロール（MarqueeControl / LoupeControl）の型を<br/>
/// 参照しない（イベント引数は本クラス独自の <see cref="FadeMaskDragRectSelectionEventArgs"/> / <see cref="FadeMaskDragRectPointEventArgs"/>、<br/>
/// 状態は <see cref="HasSelection"/> / <see cref="SelectionRect"/> / ハイライトプロパティで提供）。<br/>
/// 内包コントロールへの直接アクセスは internal（テスト用）であり、ホストは本クラスの公開 API のみを使う。<br/>
/// <br/>
/// <b>選択状態の単一のソース</b>: 選択矩形は選択矩形コントロールのセッションが単一所有する。本クラスは選択状態を複製せず、<br/>
/// 読み取り（<see cref="HasSelection"/> / <see cref="SelectionRect"/>）と設定（<see cref="SetSelection"/> / <see cref="ClearSelection"/>）を委譲するだけとする。<br/>
/// <br/>
/// <b>内部構造</b>:<br/>
/// - MarqueeControl: ドラッグ選択の本体（ドメイン座標 + 座標変換ポート）。<br/>
///   選択矩形の<b>外側に描く装飾</b>（暗転マスク・穴あけ枠・サイズラベル・ハイライト）も本コントロールが描画する。<br/>
/// - LoupeControl: 拡大鏡（中心 = ドメイン座標、表示位置 = 座標変換ポートから自己計算）。<br/>
/// <br/>
/// <b>配線（本クラス内で完結）</b>:<br/>
/// - SelectionChanged → ルーペの再生成（選択矩形の破線を描き直すため）。<br/>
/// - LoupePositionMoved → Loupe の CenterPoint を更新 + 表示開始。<br/>
/// - ConfirmRequested / CancelRequested / ZoomRequested / ScrollRequested → ホストへ中継（独自イベント引数形式）。<br/>
/// <br/>
/// <b>モード（<see cref="Mode"/>）</b>:<br/>
/// - <see cref="FadeMaskDragRectMode.Crop"/>: Marquee + Loupe を有効化（プレビュー内切出し・全面暗転）。<br/>
/// - <see cref="FadeMaskDragRectMode.AreaSelect"/>: Marquee + Loupe を有効化（全画面領域選択・全面暗転）。<br/>
/// - <see cref="FadeMaskDragRectMode.ScreenSelect"/> / <see cref="FadeMaskDragRectMode.WindowSelect"/>: ハイライトのみを描画し、<br/>
///   入力を受けない（Loupe も無効。クリック確定フローはホストが担う）。<br/>
/// <br/>
/// <b>描画先の選択（フレームワーク差）</b>: ルーペ画像上の選択矩形の破線は、描画完了イベント（<see cref="LoupeRendered"/>）の<br/>
/// 引数で受け取った Bitmap へ本クラスが直接描く（MewUI 版は描画完了イベントで渡されるキャンバスへ直接描く）。<br/>
/// 破線の幾何はコアが単一所有するため両 UI の見た目は一致する。<br/>
/// <br/>
/// <b>UI 層に残す例外（UI 固有のホスト結線）</b>: 子コントロールの生成・重なり順（ZIndex）・ヒットテストの有無・クランプ・<br/>
/// フレームワーク固有の描画先（Bitmap / キャンバス / レイアウト要素）は UI 層が担う。
/// </remarks>
public sealed class FadeMaskDragRectControl : Panel
{
    /// <summary>ドラッグ選択コントロール（選択矩形・ハンドル・nudge と、外側の装飾（暗転マスク・枠・ラベル）の描画）。</summary>
    private readonly MarqueeControl _marquee = new() { IsHitTestVisible = true, ZIndex = 60, ClipToBounds = true };

    /// <summary>ルーペコントロール（外観・表示状態の真のソース）。</summary>
    private readonly LoupeControl _loupeControl = new() { ZIndex = 70, IsHitTestVisible = false };

    /// <summary>設定（選択矩形・暗転マスク・ルーペ・選択矩形破線の外観）。</summary>
    private FadeMaskDragRectOptions _options = FadeMaskDragRectOptions.Default;

    /// <summary>動作モード（C#14 の <c>field</c> キーワードでバッキングフィールドを暗黙定義）。</summary>
    private FadeMaskDragRectMode _mode = FadeMaskDragRectMode.AreaSelect;

    /// <summary>操作ラベル（アプリ固有の付加情報）の文言を描画時に生成するデリゲート。</summary>
    /// <value>描画のたびに評価される関数。null または空文字を返す場合は描かない。</value>
    /// <remarks>
    /// 文言はアプリ固有（選択コントロールの概念ではない）ため、コアは関知しない<br/>
    /// （本クラスが文言の取得と描画を行い、<c>CaptureLabelTexts</c> 等が文言を単一所有する）。
    /// </remarks>
    public Func<string?>? OperationTextProvider { get; set; }

    /// <summary>情報ラベル（ウィンドウタイトル）の文言を描画時に生成するデリゲート。</summary>
    /// <value>描画のたびに評価される関数。null または空文字を返す場合は描かない。</value>
    /// <remarks>文言はアプリ固有のため、コアは関知しない（本クラスが取得と描画を行う）。</remarks>
    public Func<string?>? InfoTextProvider { get; set; }

    /// <summary>FadeMaskDragRectControl を初期化する（子コントロールの生成・配置・配線を実施）。</summary>
    /// <remarks>
    /// コンストラクタで内包コントロール（Marquee / Loupe）を生成して子として配置し、<br/>
    /// イベント配線（状態同期・ホストへのイベント中継）を実施する。<br/>
    /// ルーペは Left/Top アラインメントで配置し、表示位置は座標変換ポートから自己計算する（LoupeControl.UpdatePosition）。
    /// </remarks>
    public FadeMaskDragRectControl()
    {
        _loupeControl.HorizontalAlignment = HorizontalAlignment.Left;
        _loupeControl.VerticalAlignment = VerticalAlignment.Top;
        Children.Add(_marquee);
        Children.Add(_loupeControl);

        // ── 配線 ──
        // 選択矩形の変更は、ルーペ画像上の破線を描き直すためにルーペの再生成を要求する
        // （選択矩形の描画自体と外側の装飾は選択矩形コントロールがセッションから直接読んで描画する）。
        _marquee.SelectionChanged += OnSelectionChanged;
        _marquee.ViewportChanged += OnViewportChanged;
        _marquee.LoupePositionMoved += OnLoupePositionMoved;
        _marquee.ConfirmRequested += (_, e) => RaiseConfirmRequested(e.Rect);
        _marquee.CancelRequested += (_, _) => RaiseCancelRequested();
        _marquee.ZoomRequested += (_, request) => RaiseZoomRequested(request);
        _marquee.ScrollRequested += (_, request) => RaiseScrollRequested(request);

        // サイズラベルとカーソル座標ラベルは選択矩形コントロールがテキスト要素として配置する。
        // 操作ラベル・情報ラベルはアプリ固有の付加情報のため、本クラス（ホスト）が描画完了時に描く。
        _marquee.Rendered += OnMarqueeRendered;

        // ルーペ描画完了時に選択矩形の破線オーバーレイを描画する（LoupeControl の選択矩形 API は削除済み）
        _loupeControl.Rendered += OnLoupeRendered;

        // 設定の既定値を子コントロールへ分配する（モードの状態適用は ApplyOptions の内部で行われる）
        ApplyOptions();
    }

    // ====================================================================
    // 公開 API: 設定・モード
    // ====================================================================

    /// <summary>設定（選択矩形・暗転マスク・ルーペ・選択矩形破線の外観）を取得または設定する。</summary>
    /// <value>設定の単一値。既定は <see cref="FadeMaskDragRectOptions.Default"/>（内包コントロールの既定値）。</value>
    /// <remarks>
    /// 一部の項目だけを変える場合は <c>FadeMaskDragRectOptions.Default with { ... }</c> の形で指定する（指定しない項目は既定値を保つ）。<br/>
    /// 設定を 1 つの値にまとめているため、内包コントロールへ分配する規則（枠線の共用等）はコアが単一所有する。<br/>
    /// 同じ値の再設定では更新をスキップする（無駄な再適用を防ぐ）。<br/>
    /// ホストの設定型（CaptureSettings 等）からの変換は <c>FadeMaskDragRectOptionsMapper.ToFadeMaskDragRectOptions</c> が単一所有する。
    /// </remarks>
    public FadeMaskDragRectOptions Options
    {
        get => _options;
        set
        {
            if (_options == value) return;
            _options = value;
            ApplyOptions();
        }
    }

    /// <summary>動作モードを取得または設定する（内包コントロールの有効・無効と全面暗転を自動切替）。</summary>
    /// <value><see cref="FadeMaskDragRectMode"/>（Crop / AreaSelect / ScreenSelect / WindowSelect）。既定値は AreaSelect。</value>
    /// <remarks>
    /// 設定時は <see cref="MarqueeControl"/> の可視性・全面暗転・ルーペの表示可否を一括で適用する<br/>
    /// （判断はコアの <c>FadeMaskDragRectModes</c> が単一所有する）。ラベル文言はモードに依存しない<br/>
    /// （ホストがプロバイダで与える）。<br/>
    /// - Crop / AreaSelect: 選択矩形とルーペを表示し、全面暗転を有効化する。<br/>
    /// - ScreenSelect / WindowSelect: 選択矩形 / ルーペを非表示にし、全面暗転を無効化する（ハイライトのみ）。
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

    // ====================================================================
    // 公開 API: 状態・イベント・操作
    // ====================================================================

    /// <summary>現在の選択状態（選択矩形の有無）。</summary>
    /// <remarks>選択状態の真のソースは選択矩形コントロールのセッションであり、本プロパティはその読み取りを委譲するだけである。</remarks>
    public bool HasSelection => _marquee.HasSelection;

    /// <summary>現在の選択矩形（ドメイン座標）。未選択時は 0x0 の空矩形。</summary>
    /// <remarks>選択状態の真のソースは選択矩形コントロールのセッションであり、本プロパティはその読み取りを委譲するだけである。</remarks>
    public Rect SelectionRect => _marquee.SelectionRect.ToAvaloniaRect();

    /// <summary>Enter キーまたは RequestConfirm による確定要求イベント（選択確定後のみ発火）。</summary>
    public event EventHandler<FadeMaskDragRectSelectionEventArgs>? ConfirmRequested;

    /// <summary>Escape キーまたは RequestCancel によるキャンセル要求イベント。</summary>
    public event EventHandler? CancelRequested;

    /// <summary>Ctrl+ホイールによるズーム要求イベント（方向と発生位置）。Crop 用。</summary>
    public event EventHandler<WheelZoomRequest>? ZoomRequested;

    /// <summary>通常ホイール（Ctrl なし）によるスクロール要求イベント（生の回転量・修飾キー・発生位置）。Crop 用。</summary>
    /// <remarks>スクロール先（接続先 ScrollViewer・ズームビューポートのパン等）の決定はホストが実施する。</remarks>
    public event EventHandler<WheelScrollRequest>? ScrollRequested;

    /// <summary>ルーペ中心の更新イベント（ドメイン座標。ホストの追加制御用フック）。</summary>
    public event EventHandler<FadeMaskDragRectPointEventArgs>? LoupePositionMoved;

    /// <summary>ルーペ描画完了イベント（描画済み Bitmap と表示情報。ホストがオーバーレイを描画できる）。</summary>
    /// <remarks>
    /// <see cref="LoupeControl.Rendered"/> を中継する。<br/>
    /// ホスト（上位 AP）は <see cref="LoupeRenderedEventArgs.Bitmap"/> を <c>Lock</c> して<br/>
    /// 外部提供矩形（破線等）を直接描画できる（描画後は表示へ反映される）。
    /// </remarks>
    public event EventHandler<LoupeRenderedEventArgs>? LoupeRendered;

    /// <summary>選択確定要求を Marquee へ委譲する（Enter キー相当）。</summary>
    public void RequestConfirm() => _marquee.RequestConfirm();

    /// <summary>選択キャンセル要求を Marquee へ委譲する（Escape キー相当）。</summary>
    public void RequestCancel() => _marquee.RequestCancel();

    /// <summary>選択矩形をプログラム的に設定する（外部復元・テスト用）。</summary>
    /// <param name="rect">選択矩形（ドメイン座標）。</param>
    /// <remarks>
    /// MarqueeControl.SetSelection へ委譲する。SelectionChanged が発火し、ルーペ画像上の破線も追従する。<br/>
    /// マウス操作による選択（Marquee 自身が状態を管理）とは独立した、外部からの復元用 API。<br/>
    /// 選択として成立するかどうかの判定はコアの <c>FadeMaskDragRectSelection</c> が単一所有する。
    /// </remarks>
    public void SetSelection(Rect rect)
    {
        if (FadeMaskDragRectSelection.IsUsable(rect.Width, rect.Height))
        {
            _marquee.SetSelection(rect);
        }
        else
        {
            ClearSelection();
        }
    }

    /// <summary>選択状態をクリアして全面暗転（穴あけなし）に戻す。</summary>
    /// <remarks>セッションの選択状態と nudge をまとめて初期化する（状態の初期化はセッションが単一所有する）。</remarks>
    public void ClearSelection() => _marquee.ResetSelection();

    /// <summary>Marquee へフォーカスを設定する（矢印キー nudge の受付開始）。</summary>
    /// <remarks>Marquee はフォーカス時に自身の OnKeyDown で矢印キーを処理するため、セッション開始時に呼び出す。</remarks>
    public void FocusMarquee() => _marquee.Focus();

    /// <summary>祖先 ScrollViewer を Marquee へ明示注入する（スクロール・ズーム検知 + ホイール操作。Crop 用）。</summary>
    /// <param name="scrollViewer">連携する ScrollViewer。null で連携解除。</param>
    public void AttachScrollViewer(ScrollViewer? scrollViewer) => _marquee.AttachScrollViewer(scrollViewer);

    /// <summary>ビューポート（パン・ズーム・表示サイズ）の変化を内包コントロールへ通知する。</summary>
    /// <remarks>
    /// ビューポート実装（ScrollViewer / RenderTransform ベース等）に依存せず、ホスト（上位 AP）が<br/>
    /// ビューポート変化を検知した際に本メソッドで中継する（ScrollViewer 連携の <c>ScrollChanged</c> 経由の<br/>
    /// <see cref="MarqueeControl.ViewportChanged"/> も本メソッドへ統合される）。<br/>
    /// Marquee（選択矩形と外側の装飾）の再描画と、ルーペ表示位置の再計算を要求する。<br/>
    /// 座標変換ポートは呼び出し毎に最新の表示状態を評価するため、再描画だけで追従する。
    /// </remarks>
    public void NotifyViewportChanged()
    {
        _marquee.InvalidateVisual();
        _loupeControl.RefreshPosition();
    }

    // ====================================================================
    // 公開 API: 環境値・座標変換・ログ
    // ====================================================================

    /// <summary>ドメイン ↔ コントロールローカル DIP の座標変換ポートを全内包コントロールへ一括設定する。</summary>
    /// <param name="port">座標変換ポート（Forward = DIP→ドメイン / Inverse = ドメイン→DIP / Bounds = ドメイン境界）。null の場合は倍率（<see cref="Scaling"/>）からコアが解決する。</param>
    /// <remarks>
    /// Marquee / Loupe の 2 コントロールへ同一ポートを分配する（1 回の呼び出しで設定完了）。<br/>
    /// 変換関数は呼び出し毎に評価されるため、ズーム・スクロール・ウィンドウサイズ変更等のレイアウト変化に追従する。
    /// </remarks>
    public void SetCoordinateTransform(ICoordTransform? port)
    {
        _marquee.SetCoordinateTransform(port);
        _loupeControl.SetCoordinateTransform(port);
    }

    /// <summary>サイズラベルを描画時に評価するプロバイダを取得または設定する。</summary>
    /// <value>描画時に呼び出される関数。null の場合は自動生成（コアの既定）を使う。</value>
    /// <remarks>
    /// ホストは最新の選択状態（<see cref="SelectionRect"/> / <see cref="HasSelection"/> 等）からサイズラベル文字列を生成するクロージャを設定する。<br/>
    /// - Crop: 論理座標（元画像ピクセル）+ 実表示ズーム率で生成（レイアウト変化に追従）。<br/>
    /// - 範囲選択（AreaSelect）等: 設定値（<c>CaptureSettings.SizeLabelOverride</c>）を返す。<br/>
    /// 設定時は選択矩形コントロールへ伝搬し、再描画を要求する。
    /// </remarks>
    public Func<string?>? SizeLabelOverrideProvider
    {
        get => _marquee.SizeLabelOverrideProvider;
        set => _marquee.SizeLabelOverrideProvider = value;
    }

    /// <summary>DPI スケーリング係数（ドメイン → DIP 変換の基準倍率）を取得または設定する。</summary>
    /// <value>スケーリング係数。ドメイン座標がローカル DIP と一致する場合は 1.0。</value>
    /// <remarks>
    /// 環境値（ユーザー設定ではない）のため設定（<see cref="Options"/>）には含めず、ホスト（セッション）が与える。<br/>
    /// 座標変換ポート（<see cref="SetCoordinateTransform"/>）が設定されている場合はそちらが優先される。
    /// </remarks>
    public double Scaling
    {
        get => _marquee.Scaling;
        set => _marquee.Scaling = value;
    }

    /// <summary>サイズラベル表示用の DPI スケーリング係数（マルチスクリーン補正。0 以下は <see cref="Scaling"/> と同値）を取得または設定する。</summary>
    /// <value>サイズラベルの DisplayScale に使う倍率。0 以下は <see cref="Scaling"/> から解決される。</value>
    /// <remarks>環境値のため設定（<see cref="Options"/>）には含めず、ホスト（セッション）が与える。</remarks>
    public double DisplayScaleScaling
    {
        get => _marquee.DisplayScaleScaling;
        set => _marquee.DisplayScaleScaling = value;
    }

    /// <summary>ルーペの DIP → 物理ピクセル倍率を取得または設定する。</summary>
    /// <value>スケーリング係数（0.1〜8.0）。既定は 1.0。</value>
    /// <remarks>
    /// 環境値（DPI）のため設定（<see cref="Options"/>）には含めず、ホスト（<c>MainWindow.DesktopScaling</c> 等）が与える。
    /// </remarks>
    public double LoupeRenderScaling
    {
        get => _loupeControl.RenderScaling;
        set => _loupeControl.RenderScaling = value;
    }

    /// <summary>内部ロガー（内包コントロール（Marquee / Loupe）へ伝搬）。null の場合は出力なし。</summary>
    /// <value>null を設定した場合は <see cref="Microsoft.Extensions.Logging.Abstractions.NullLogger"/> へフォールバックする。設定時に内包コントロールへ伝搬する。</value>
    /// <remarks>
    /// 内包コントロールの詳細ログ（ルーペの生成・更新・エラー、ドラッグ操作）をホストのログ基盤へ出力するために使用する。
    /// </remarks>
    public ILogger Log
    {
        get => _marquee.Log;
        set
        {
            var logger = value.OrNullLogger();
            _loupeControl.Log = logger;
            _marquee.Log = logger;
        }
    }

    // ====================================================================
    // 公開 API: 状態設定・テスト用アクセサ
    // ====================================================================

    /// <summary>ルーペの中心位置（ドメイン座標）を設定して表示を開始する。</summary>
    /// <param name="domainPos">ルーペ中心（ドメイン = 元画像ピクセル / 仮想スクリーンピクセル）。</param>
    /// <remarks>
    /// 表示位置・サイズは LoupeControl が座標変換ポート（<see cref="SetCoordinateTransform"/>）から自己計算する<br/>
    /// （カーソル右下 + オフセット + クランプ）。配置前（Parent 未接続）の呼び出しは無視される。<br/>
    /// <b>拡大元画像が未設定の間は表示しない</b>（未設定の市松模様を防ぐ。Source 設定後に呼び出し直すことで表示される）。
    /// </remarks>
    public void SetLoupePosition(Point domainPos)
    {
        if (_loupeControl.Parent is null) return;
        UpdateLoupePosition(domainPos);
    }

    /// <summary>ルーペの中心位置（ドメイン座標）を設定して表示可否を更新する。</summary>
    /// <param name="domainPos">ルーペ中心（ドメイン座標）。</param>
    /// <remarks>
    /// <see cref="SetLoupePosition"/> と <see cref="OnLoupePositionMoved"/> で共用する（重複排除）。<br/>
    /// 表示位置・サイズは LoupeControl が座標変換ポート（<see cref="SetCoordinateTransform"/>）から自己計算する。<br/>
    /// 表示可否の判断（選択矩形を使うモードでのみ表示する・拡大元画像が未設定なら表示しない）は<br/>
    /// コアの <c>FadeMaskDragRectState.IsLoupeVisible</c> が単一所有する。
    /// </remarks>
    private void UpdateLoupePosition(Point domainPos)
    {
        // LoupeControl.CenterPoint は StyledProperty（UI 境界）のため、境界で設定する。
        _loupeControl.CenterPoint = domainPos;
        UpdateLoupeVisibility();
    }

    /// <summary>ルーペの表示可否をモード・拡大元画像・中心から更新する。</summary>
    /// <remarks>表示可否の判断はコアの <c>FadeMaskDragRectState.IsLoupeVisible</c> が単一所有する。</remarks>
    private void UpdateLoupeVisibility()
        => _loupeControl.IsVisible = FadeMaskDragRectModes
            .Resolve(_mode)
            .IsLoupeVisible(_loupeControl.Source is not null, _loupeControl.CenterPoint is not null);

    /// <summary>選択オーバーレイ（暗転マスク・ハイライト・選択矩形）の再描画を要求する。</summary>
    /// <remarks>ハイライト等の状態を変更した後に呼び出す。</remarks>
    public void InvalidateOverlay() => _marquee.InvalidateVisual();

    /// <summary>ルーペの拡大元画像（Source）を設定する。</summary>
    /// <param name="source">拡大元画像。null で解除。</param>
    /// <remarks>
    /// LoupeControl.Source へ委譲する（Source 未設定の間はルーペを表示しない市松模様防止のため、<br/>
    /// 表示開始は <see cref="SetLoupePosition"/> が担う）。解除時は表示も解除する。
    /// </remarks>
    public void SetLoupeSource(IImage? source)
    {
        _loupeControl.Source = source;

        if (source is null)
        {
            // 拡大元画像が無い状態で表示を続けない（次の SetLoupePosition まで非表示）。
            // 表示可否の判断はコアが単一所有するため、ここでは問い合わせるだけにする。
            UpdateLoupeVisibility();
        }
    }

    /// <summary>ルーペを非表示にして後片付けする（拡大元画像・中心・Bitmap を解放する）。</summary>
    /// <remarks>セッション終了時の後片付けに使用する（表示状態・中心・拡大元画像をまとめて破棄する）。</remarks>
    public void ClearLoupe()
    {
        _loupeControl.Source = null;
        _loupeControl.CenterPoint = null;
        _loupeControl.Clear();
    }

    /// <summary>ルーペ中心（ドメイン座標）。未設定時は null。</summary>
    /// <remarks>ルーペの表示状態の確認（テスト・座標検証）に使用する。</remarks>
    public Point? LoupeCenterPoint => _loupeControl.CenterPoint;

    /// <summary>ルーペコントロール（Source 設定・テスト用アクセサ）。</summary>
    internal LoupeControl Loupe => _loupeControl;

    /// <summary>選択矩形コントロール（テスト用アクセサ）。</summary>
    internal MarqueeControl Marquee => _marquee;

    // ====================================================================
    // 公開 API: ハイライト状態（ScreenSelect / WindowSelect 用）
    // ====================================================================

    /// <summary>ハイライト矩形（ドメイン座標）。未検出時は null。</summary>
    /// <remarks>選択矩形コントロールの装飾の設定へ委譲する（変更は自動で再描画される）。</remarks>
    public Rect? LogicalHighlightRect
    {
        get => _marquee.DecoOptions.OuterHighlightRect?.ToAvaloniaRect();
        set => _marquee.DecoOptions = _marquee.DecoOptions with { OuterHighlightRect = value?.ToEsRect() };
    }

    /// <summary>ハイライト表示の有無。</summary>
    public bool HasHighlight
    {
        get => _marquee.DecoOptions.OuterHasHighlight;
        set => _marquee.DecoOptions = _marquee.DecoOptions with { OuterHasHighlight = value };
    }

    /// <summary>全面暗転（穴あけなし）を表示するかどうか（選択矩形を使うモードで有効）。</summary>
    /// <remarks>モード（<see cref="Mode"/>）から導出される描画状態の読み取り専用プロパティ。</remarks>
    public bool ShowFullOverlay => _marquee.DecoOptions.OuterShowFullOverlay;

    // ====================================================================
    // 内部実装（設定の分配・モードの適用・配線）
    // ====================================================================

    /// <summary>設定を内包コントロールへ分配する。</summary>
    /// <remarks>
    /// 選択矩形コントロールの設定は 1 回の代入で分配する（外側の装飾の外観も同じ値が持つ）。<br/>
    /// ハイライト（ホストが与える実行時状態）は設定値に含まれないため、設定の差し替えで失わないように引き継ぐ。<br/>
    /// 適用後は選択矩形の再描画、ルーペの再配置・再生成を要求する（選択状態・ルーペ中心は維持される）。<br/>
    /// ルーペの再生成は、選択矩形破線の色がルーペの外観（<c>LoupeAppearance</c>）に含まれず、<br/>
    /// プロパティ通知では再生成されないため。
    /// </remarks>
    private void ApplyOptions()
    {
        var current = _marquee.DecoOptions;
        _marquee.Options = _options.Marquee;
        _marquee.DecoOptions = _options.Deco with
        {
            OuterHighlightRect = current.OuterHighlightRect,
            OuterHasHighlight = current.OuterHasHighlight,
        };
        _loupeControl.Options = _options.Loupe;
        // 表示位置のオフセットはフレームワーク差（Avalonia 版のルーペは自己配置する）のため、
        // 設定に含まれる値からルーペのプロパティへ個別に渡す
        _loupeControl.PositionOffset = _options.LoupeOffset;

        // モードから導出される描画状態は設定値に含まれないため、設定を適用した後に改めて適用する
        ApplyMode();

        _marquee.InvalidateVisual();
        _loupeControl.RequestRender();
    }

    /// <summary>モードに応じて内包コントロールの状態（描画モード・入力の可否・全面暗転・破線）を適用する。</summary>
    /// <remarks>
    /// モードと状態の対応はコアの <c>FadeMaskDragRectModes</c> が単一所有する。<br/>
    /// 選択矩形を使わないモード（ハイライトのみ）でも暗転マスクを描画するため、
    /// 可視性ではなく<b>入力の可否</b>（ヒットテスト）を切り替える。<br/>
    /// 選択矩形破線の表示可否もモード依存のため、既存の Bitmap から破線を消す（または描く）ために再生成を要求する。
    /// </remarks>
    private void ApplyMode()
    {
        var state = FadeMaskDragRectModes.Resolve(_mode);

        // モードから導出される描画状態（全面暗転・ハンドルの表示可否）は 1 回の変更通知でまとめて適用する
        _marquee.DecoOptions = _marquee.DecoOptions with { OuterShowFullOverlay = state.ShowFullOverlay };
        _marquee.Options = _marquee.Options with { ShowHandles = state.UsesSelection };

        // 選択矩形を使わないモードでも暗転マスク（ハイライト）は描画するため、描画は止めず入力だけを止める
        _marquee.IsHitTestVisible = state.UsesSelection;

        UpdateLoupeVisibility();
        // 破線の表示可否はルーペのプロパティに現れないため、既存の Bitmap から破線を消す（または描く）ために再生成を要求する
        _loupeControl.RequestRender();
    }

    /// <summary>選択矩形の変更時に、ルーペ画像上の破線を描き直すために再生成を要求する（SelectionChanged）。</summary>
    /// <param name="sender">イベント送信元（MarqueeControl）。</param>
    /// <param name="e">選択矩形イベント（Rect はドメイン座標）。</param>
    /// <remarks>
    /// 選択矩形の描画そのものは選択矩形コントロールがセッションから直接読んで行うため、本クラスでの同期は不要である。<br/>
    /// 破線はルーペの Bitmap へ焼き込まれるため、選択矩形が変わるたびに再生成を要求する。
    /// </remarks>
    private void OnSelectionChanged(object? sender, SelectionRectEventArgs e)
        => _loupeControl.RequestRender();

    /// <summary>ビューポート変化（Marquee の ViewportChanged = ScrollViewer 連携）を内包コントロールへ反映する。</summary>
    /// <param name="sender">イベント送信元（MarqueeControl）。</param>
    /// <param name="e">選択矩形イベント（Rect はドメイン座標）。</param>
    /// <remarks>
    /// <see cref="NotifyViewportChanged"/> と同じ処理を実行する（ScrollViewer 連携とホスト通知を単一経路に統合）。<br/>
    /// 選択矩形（ドメイン座標）はビューポート変化で不変のため、選択状態の再同期は行わず、<br/>
    /// 再描画とルーペ表示位置の再計算のみを要求する。
    /// </remarks>
    private void OnViewportChanged(object? sender, SelectionRectEventArgs e) => NotifyViewportChanged();

    /// <summary>描画完了時にホスト固有の付加情報（操作ラベル・情報ラベル）を描画する。</summary>
    /// <param name="sender">イベント送信元（選択矩形コントロール）。</param>
    /// <param name="e">描画コンテキストと描画内容。</param>
    /// <remarks>
    /// サイズラベルとカーソル座標ラベルは選択矩形コントロールがテキスト要素として配置する。<br/>
    /// <b>操作ラベル・情報ラベルは選択コントロールの概念ではない</b>（アプリ固有の付加情報）ため、<br/>
    /// 本クラス（ホスト）が文言の生成と描画を行い、コアは関知しない。<br/>
    /// 外観（表示可否・寸法・色）は設定（<see cref="FadeMaskDragRectOptions.Labels"/>）が単一所有する。<br/>
    /// 本ハンドラはコントロールの子要素（ラベル）より後に呼ばれるため、付加情報はラベルより前面に置かれる。
    /// </remarks>
    private void OnMarqueeRendered(object? sender, MarqueeRenderedEventArgs e)
    {
        // 付加情報を重ねる基準は穴あけ矩形（選択またはハイライトの領域）の左上である
        if (e.Content.HoleRect is not { } hole)
        {
            return;
        }

        var labels = _options.Labels;
        var y = hole.Y;

        // サイズラベルが描かれている場合はその下へ積む（重なりを避ける。行送りはコアが単一所有する）
        if (e.Content.SizeLabel is { } sizeLabel)
        {
            y = MarqueeAppearance.NextLabelTop(y, sizeLabel.FontSize);
        }

        // 表示可否はホストが判定する（設定スイッチと文言の有無の両方が成立したときだけ描く）
        if (labels.ShowOperationLabel && OperationTextProvider?.Invoke() is { Length: > 0 } operation)
        {
            DrawHostLabel(
                e.Context, hole.X, y, operation,
                labels.OperationFontSize, labels.OperationColor, labels.OperationBackgroundColor);
            y = MarqueeAppearance.NextLabelTop(y, labels.OperationFontSize);
        }

        if (labels.ShowInfoLabel && InfoTextProvider?.Invoke() is { Length: > 0 } info)
        {
            DrawHostLabel(
                e.Context, hole.X, y, info,
                labels.InfoFontSize, labels.InfoColor, labels.InfoBackgroundColor);
        }
    }

    /// <summary>ホスト固有のラベル 1 行（背景 + 文字）を描く。</summary>
    /// <param name="context">描画コンテキスト（<c>Render</c> の実行中のみ有効）。</param>
    /// <param name="x">ラベルの左端（DIP）。</param>
    /// <param name="y">ラベルの上端（DIP）。</param>
    /// <param name="text">文言。</param>
    /// <param name="fontSize">フォントサイズ（DIP）。</param>
    /// <param name="color">文字色。</param>
    /// <param name="background">背景色。</param>
    /// <remarks>
    /// サイズラベルと同じ体裁（背景 + 文字の縦積み）で描く（体裁はコアが単一所有する）。<br/>
    /// テキストの実寸測定と描画は本クラス（ホスト）が行う（コアは文字を描かない）。
    /// </remarks>
    private static void DrawHostLabel(
        DrawingContext context, double x, double y, string text, double fontSize, DrawingColor color, DrawingColor background)
    {
        var formatted = new FormattedText(
            text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, fontSize,
            s_hostLabelBrushes.GetOrCreate(color.ToAvaloniaColor()));

        // 体裁（内側余白）はコアが単一所有する（MewUI ホストと同じ組み立てに揃える）
        var textOrigin = new EsPoint(x, y + MarqueeAppearance.SizeLabelPadding);
        context.FillRectangle(
            s_hostLabelBrushes.GetOrCreate(background.ToAvaloniaColor()),
            MarqueeAppearance.ComputeHostLabelBackground(
                textOrigin, new EsSize(formatted.Width, formatted.Height)).ToAvaloniaRect());
        context.DrawText(formatted, textOrigin.ToAvaloniaPoint());
    }

    /// <summary>ホスト固有ラベルのブラシを共有する容量制限付きキャッシュ。</summary>
    /// <remarks>
    /// 描画のたびにブラシを生成するとポインター移動中の負荷が増えるため、プラットフォーム層の<br/>
    /// 容量制限付きキャッシュを共用する（本クラスのラベルは色数が少ないため上限に達しない）。\n
    /// </remarks>
    private static readonly CapacityLimitedCache<Color, SolidColorBrush> s_hostLabelBrushes =
        new(static color => new SolidColorBrush(color));

    /// <summary>ルーペの描画完了時に、選択矩形の破線オーバーレイを描画済み Bitmap へ描く。</summary>
    /// <param name="sender">イベント送信元（LoupeControl）。</param>
    /// <param name="e">描画済み Bitmap と表示情報。</param>
    /// <remarks>
    /// 選択矩形の破線は <see cref="LoupeControl"/> 内部では描画せず、本クラス（ホスト側）が<br/>
    /// <see cref="LoupeControl.Rendered"/> イベントで通知された <see cref="LoupeRenderedEventArgs.Bitmap"/> へ直接描画する。<br/>
    /// 選択矩形（<see cref="SelectionRect"/> = ドメイン座標）は <c>(ドメイン座標 − CaptureTopLeft) × Scale</c> で<br/>
    /// ルーペ画像ピクセル座標へ変換する（変換と破線の幾何はコアが単一所有する）。<br/>
    /// <b>中心座標ラベルはルーペコントロールがテキスト要素として表示する</b>（本クラスは破線だけを描く）。<br/>
    /// 描画しない場合（選択矩形を使わないモード・選択なし・切り出し領域の左上が未設定）でも、<br/>
    /// ホストへルーペ描画完了を中継する（外部提供矩形の描画用）。
    /// </remarks>
    private void OnLoupeRendered(object? sender, LoupeRenderedEventArgs e)
    {
        // 破線を描くかどうかはモード（コアが単一所有する）と切り出し領域・選択矩形の有無で決まる
        if (FadeMaskDragRectModes.Resolve(_mode).ShowSelectionOverlay
            && e.CaptureTopLeft is { } topLeft
            && _marquee.HasSelection)
        {
            // 破線色（色A / 色B）を 2 つ用意し、交互に塗る（描画はコアが単一所有する）
            FadeMaskDragRectOverlay.DrawSelectionRect(
                e.Bitmap,
                _marquee.SelectionRect,
                topLeft.ToEsPoint(),
                e.Scale,
                _options.SelectionRectColorA,
                _options.SelectionRectColorB);
        }

        // ホスト（上位 AP）へルーペ描画完了を中継する（外部提供矩形の描画用）
        RaiseLoupeRendered(e);
    }

    /// <summary><see cref="LoupeRendered"/> イベントを発火する。</summary>
    /// <param name="e">ルーペ描画完了イベント引数。</param>
    private void RaiseLoupeRendered(LoupeRenderedEventArgs e) => LoupeRendered?.Invoke(this, e);

    /// <summary>ルーペ更新（LoupePositionMoved）。中心はドメイン座標のまま LoupeControl に設定する。</summary>
    /// <param name="sender">イベント送信元（MarqueeControl）。</param>
    /// <param name="e">ルーペ中心（ドメイン座標）。</param>
    /// <remarks>
    /// 表示位置・サイズは LoupeControl が座標変換ポートから自己計算する（カーソル右下 + オフセット + クランプ）。<br/>
    /// <b>拡大元画像が未設定の間は表示しない</b>（AreaSelect の背景キャプチャ完了前は Source が null のため、<br/>
    /// 表示すると市松模様が見える。Source 設定後の表示開始はホスト（<see cref="SetLoupePosition"/> 経由）が行う）。<br/>
    /// ホストへの通知（<see cref="LoupePositionMoved"/>）は追加制御（Source 設定等）のフックとして発行する。
    /// </remarks>
    private void OnLoupePositionMoved(object? sender, MarqueePointEventArgs e)
    {
        UpdateLoupePosition(e.Point);
        RaiseLoupePositionMoved(e.Point);
    }

    // ====================================================================
    // イベント発火（ホストへの通知。発火形式を一貫させるため private ヘルパーに集約）
    // ====================================================================

    /// <summary>確定要求イベントを発火する。</summary>
    /// <param name="rect">確定した選択矩形（ドメイン座標）。</param>
    private void RaiseConfirmRequested(Rect rect)
        => ConfirmRequested?.Invoke(this, new FadeMaskDragRectSelectionEventArgs(rect));

    /// <summary>キャンセル要求イベントを発火する。</summary>
    private void RaiseCancelRequested()
        => CancelRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>ズーム要求イベントを発火する。</summary>
    /// <param name="request">ズーム要求（方向と発生位置）。</param>
    private void RaiseZoomRequested(WheelZoomRequest request)
        => ZoomRequested?.Invoke(this, request);

    /// <summary><see cref="ScrollRequested"/> を発火する。</summary>
    /// <param name="request">スクロール要求（生の回転量と修飾キー）。</param>
    private void RaiseScrollRequested(WheelScrollRequest request)
        => ScrollRequested?.Invoke(this, request);

    /// <summary>ルーペ中心更新イベントを発火する。</summary>
    /// <param name="domainPos">ルーペ中心（ドメイン座標）。</param>
    private void RaiseLoupePositionMoved(Point domainPos)
        => LoupePositionMoved?.Invoke(this, new FadeMaskDragRectPointEventArgs(domainPos));

    // ====================================================================
    // レイアウト
    // ====================================================================

    /// <summary>子コントロール（Marquee / Loupe）をパネル全面へ配置する。</summary>
    /// <param name="finalSize">パネルの最終サイズ。</param>
    /// <returns>パネルの最終サイズ。</returns>
    /// <remarks>
    /// 全子コントロールを同一矩形（パネル全面）に Arrange する。<br/>
    /// Marquee は Stretch で全面に広がり、Loupe は Left/Top アラインメントで<br/>
    /// 自身の Width/Height（座標変換ポートから算出）に配置される（重なり順は ZIndex で制御）。
    /// </remarks>
    protected override Size ArrangeOverride(Size finalSize)
    {
        // 全子コントロールを同一矩形（パネル全面）に Arrange する（矩形は一度だけ生成して共有）
        var fullRect = new Rect(finalSize);
        foreach (var child in Children)
        {
            child.Arrange(fullRect);
        }
        return finalSize;
    }
}
