using EsUtil.Feature.MarqueeCore.Decoration;
using EsUtil.SaveEvidence.Marquee.UIBase;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using EsUtil.Platform.Core;
using EsUtil.Platform.Native;
using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MarqueeFacade = EsUtil.Feature.MarqueeCore.Marquee;
using static EsUtil.Feature.MarqueeCore.Marquee.Limits;
using EsUtil.Feature.MarqueeCore.Contract;
using EsUtil.Feature.MarqueeCore.Drawing;
using EsUtil.Feature.MarqueeCore.Interaction;
using EsUtil.Feature.MarqueeCore.Setting;
using EsUtil.Platform.AvaloniaUI.Interop;
using EsUtil.Platform.AvaloniaUI.Coordinate;


namespace EsUtil.SaveEvidence.Marquee.AvaloniaUI;

/// <summary>リサイズ可能なドラッグ矩形の UI コントロール（Avalonia 版）。</summary>
/// <remarks>
/// <b>層の構成</b>: 選択・ドラッグ・nudge の判断は UI 非依存のコアのファサード（<c>Feature.MarqueeCore</c> の
/// <see cref="MarqueeFacade"/>）が担い、本コントロールは「入力の受付」「効果の適用」「通知の転送」「描画」のみを行う薄い層である。<br/>
/// <br/>
/// 本クラスの責務:<br/>
/// 1. ポインター / キー入力をコア型（<see cref="EsPoint"/> / <see cref="EsKey"/>）へ変換してコア（ファサード）へ通知する<br/>
///    （ホイールはコントロール化の層（<c>MarqueeControl.UIBase</c>）へ通知し、ビューポート操作の要求として受け取る）。<br/>
/// 2. コアが返す効果（再描画・フォーカス・カーソル操作）を適用する。<br/>
/// 3. コアからの通知を公開イベントへ転送する（判断はしない）。<br/>
/// 4. コアが解決した描画内容を描画する（枠線・ハンドル・十字マーカー。計算は描画層へ委譲）。<br/>
/// <br/>
/// <b>文字は描かない</b>: ラベル（サイズラベル・操作ラベル・情報ラベル）と座標ラベルは、テキストの実寸測定が必要なため<br/>
/// <see cref="Rendered"/> で通知し、ホストが描く（サイズラベル・座標ラベルはテキスト要素として配置する）。<br/>
/// <br/>
/// <b>自己完結</b>: 矢印キーの検出・nudge 後の再描画・カーソル表示/非表示・ダブルクリックの確定/キャンセル判定を
/// コアが担うため、ホスト依存のない動作になっている。ホストは <see cref="SelectionStarted"/>（始点決定）/
/// <see cref="SelectionCompleted"/>（終点決定）/ <see cref="SelectionCancelled"/>（領域キャンセル）/
/// <see cref="ConfirmRequested"/> / <see cref="CancelRequested"/> イベントを購読して利用する。<br/>
/// <br/>
/// 外観・対話パラメータの設定は 1 つの依存関係プロパティ（<see cref="Options"/>）で公開し、外部バインディングで制御できる<br/>
/// （色の不透明度は色のアルファに統合。設定値の既定値と範囲補正はコアの <see cref="MarqueeOptions"/> が単一所有する）。<br/>
/// 対話パラメータ（最小選択サイズ・境界外選択の禁止・ハンドルのヒットテスト半径）はファサードへ伝搬し、判断はコアが行う。<br/>
/// ログは <see cref="Log"/> プロパティで注入する（デフォルトは <see cref="NullLogger"/>）。<br/>
/// <br/>
/// 選択矩形の変更は <see cref="SelectionChanged"/> イベントで外部（上位 Mediator）に通知する。
/// </remarks>
public sealed partial class MarqueeControl : Panel
{
    // ====================================================================
    // 内部状態（コンポジション）
    // ====================================================================

    /// <summary>コアのファサード（入力の解釈・状態遷移の判断・描画内容の解決・設定の窓口）。</summary>
    private readonly MarqueeFacade _marquee;

    /// <summary>描画の実装（描画内容を描画面へ写す戦略）。既定は自 UI の描画実装（色を保持する）。</summary>
    /// <remarks>
    /// 描画は UI の関心事であるためコアは保持しない（コアは描画内容と層の順序をデータとして返すだけである）。<br/>
    /// 層の順序に従った描画手順は UIBase の基底（<c>MarqueeRendererBase</c>）が単一所有する。<br/>
    /// 既定値は <see cref="Paint"/> の既定値（<see cref="MarqueePaint.Default"/>）と同じ色で組み立てる。
    /// </remarks>
    private IMarqueeRenderer _renderer = new MarqueeRendererAvaloniaUI(MarqueePaint.Default);

    /// <summary>コアが要求する効果（フォーカス・再描画・カーソル形状・座標変換）の適用先。</summary>
    private readonly IMarqueeEffects _effects;

    /// <summary>ホイール入力を解釈してビューポート操作の要求（ズーム / スクロール）を出す補助。</summary>
    /// <remarks>
    /// 本クラスはファサードへ接続し、ホイールの解釈（Ctrl = ズーム・連携先への適用）は<br/>
    /// <c>MarqueeControl.UIBase</c> が単一所有する<br/>
    /// （本層は修飾キーの真偽と回転量を渡し、要求を公開イベントへ転送するだけ）。
    /// </remarks>
    private readonly MarqueeWheelInterpreter _wheel;

    /// <summary>ビューポート（パン・ズーム）の変化を再通知する補助。</summary>
    /// <remarks>
    /// 「ビューポートの変化 = 現状態の再通知 + 再描画」というビューポート連携の規則は<br/>
    /// <c>MarqueeControl.UIBase</c> が単一所有する（本層は自 UI の公開イベントへ転送するだけ）。
    /// </remarks>
    private readonly MarqueeViewportNotifier _viewport;

    /// <summary>カーソルサービス（nudge 時のカーソル移動・表示切替。既定実装の解決は UI 側で行う）。</summary>
    private readonly ICursorService _cursor;

    /// <summary>フォーカス非依存の矢印キー nudge 用に購読した TopLevel。解除時に使用する。</summary>
    private TopLevel? _attachedTopLevel;

    /// <summary>nudge 終了（ウィンドウ非アクティブ化）用に購読したウィンドウ。解除時に使用する。</summary>
    private WindowBase? _attachedWindow;

    /// <summary>ズーム・スクロール検知用に購読した祖先 ScrollViewer。解除時に使用する。</summary>
    private ScrollViewer? _attachedScrollViewer;

    // 標準カーソルは Lazy で遅延生成する（呼び出し時に ICursorFactory を解決する）。
    // static readonly フィールドとして型初期化時に new Cursor(...) すると、Avalonia ヘッドレス初期化前に
    // 本コントロールが参照された場合に Unable to locate 'ICursorFactory' で失敗するため（並列テストの競合）。
    // Lazy を使うことで、実際にカーソルを設定する時点（UpdateCursor）まで生成を遅延し、AvaloniaLocator 解決後に行う。
    /// <summary>左上/右下ハンドル用カーソル（遅延生成・静的共有）。</summary>
    private static readonly Lazy<Cursor> s_topLeftCornerCursor = new(() => new Cursor(StandardCursorType.TopLeftCorner));

    /// <summary>右上/左下ハンドル用カーソル（遅延生成・静的共有）。</summary>
    private static readonly Lazy<Cursor> s_topRightCornerCursor = new(() => new Cursor(StandardCursorType.TopRightCorner));

    /// <summary>上下リサイズ用カーソル（遅延生成・静的共有）。</summary>
    private static readonly Lazy<Cursor> s_sizeNorthSouthCursor = new(() => new Cursor(StandardCursorType.SizeNorthSouth));

    /// <summary>左右リサイズ用カーソル（遅延生成・静的共有）。</summary>
    private static readonly Lazy<Cursor> s_sizeWestEastCursor = new(() => new Cursor(StandardCursorType.SizeWestEast));

    /// <summary>移動用カーソル（遅延生成・静的共有）。</summary>
    private static readonly Lazy<Cursor> s_sizeAllCursor = new(() => new Cursor(StandardCursorType.SizeAll));

    // ====================================================================
    // コンストラクタ
    // ====================================================================

    /// <summary><see cref="MarqueeControl"/> の新しいインスタンスを初期化します（AXAML および既定構成向け）。</summary>
    /// <remarks>
    /// カーソル操作はデフォルトの Win32 実装（<c>WinCursorService</c>。生成は <see cref="PlatformFactory.CreateCursorService"/>）で動作する。<br/>
    /// nudge 後の再描画要求は本コントロール自身の <see cref="Avalonia.Visual.InvalidateVisual"/> に配線し、<br/>
    /// 矢印キーを受け取れるようフォーカス可能にする（自己完結・ホスト依存なし）。<br/>
    /// </remarks>
    public MarqueeControl()
        : this(null)
    {
    }

    /// <summary>カーソルサービスを注入して初期化する。</summary>
    /// <param name="cursorService">カーソルサービス。null の場合は Windows 既定実装を使用する。</param>
    /// <remarks>
    /// カーソルサービスの既定実装（Win32）の解決は UI 側で行い、コア（状態管理クラス）は抽象のみに依存する。<br/>
    /// コアからの通知は公開イベントへ転送し、設定（<see cref="Options"/>）をファサードへ伝搬する。
    /// </remarks>
    public MarqueeControl(ICursorService? cursorService)
    {
        Focusable = true;
        _cursor = cursorService ?? PlatformFactory.CreateCursorService();
        _effects = new Effects(this);
        _marquee = new MarqueeFacade(_effects, _cursor, Options);
        // 現在の環境値（倍率）をファサードへ引き継ぐ（以降の変更はプロパティ変更ハンドラが伝搬する）
        _marquee.Scaling = GetValue(ScalingProperty);
        _marquee.Notified += OnNotified;
        // 装飾の設定と環境値はコアが単一所有する（本層は公開プロパティをコアへ委譲するだけ）
        _marquee.DecoOptions = GetValue(DecoOptionsProperty);
        _marquee.DisplayScaleScaling = GetValue(DisplayScaleScalingProperty);
        // ホイール要求（ズーム / スクロール）の解釈はコントロール化の層が単一所有する（本層は自 UI の公開イベントへ転送するだけ）
        _wheel = new MarqueeWheelInterpreter(_marquee);
        _wheel.ZoomRequested += (_, request) => ZoomRequested?.Invoke(this, request);
        _wheel.ScrollRequested += (_, request) => ScrollRequested?.Invoke(this, request);
        // ビューポート変化の通知（再通知 + 再描画）もコントロール化の層が単一所有する
        _viewport = new MarqueeViewportNotifier(_marquee, _effects);
        _viewport.ViewportChanged += (_, snapshot) => ViewportChanged?.Invoke(this, ToRectArgs(snapshot));

        // 図形（装飾・ハンドル・始点マーカー）は専用の子コントロールが描く。
        // Panel は Render をシールするため自前で描けず、また子として持つことでラベル要素（後から追加する子）が
        // 必ず図形より後に描かれ「ラベルは常に最上位」というコアの描画順契約が構造で保証される。
        Children.Add(new ShapeLayer(this));
    }

    /// <summary>図形（装飾・ハンドル・始点マーカー）だけを描画する子コントロール。</summary>
    /// <remarks>
    /// 親（<see cref="MarqueeControl"/>）の描画内容を読んで描くだけであり、判断を持たない。<br/>
    /// ポインター操作は親が受けるため、ヒットテストの対象からは外す。
    /// </remarks>
    private sealed class ShapeLayer : Control
    {
        /// <summary>描画内容の取得元（親コントロール）。</summary>
        private readonly MarqueeControl _owner;

        /// <summary>描画先の親を指定して初期化する。</summary>
        /// <param name="owner">描画内容の取得元（親コントロール）。</param>
        public ShapeLayer(MarqueeControl owner)
        {
            _owner = owner;
            // 入力は親が受ける（本層は図形の描画のみを担う）
            IsHitTestVisible = false;
        }

        /// <summary>親の描画（図形の層）を行う。</summary>
        /// <param name="context">描画コンテキスト。</param>
        public override void Render(DrawingContext context) => _owner.RenderShapes(context);
    }

    /// <summary>静的コンストラクタ。設定（<see cref="Options"/>）の変更を再描画とファサードへ同期する。</summary>
    /// <remarks>
    /// 外観の変更は <see cref="Visual.AffectsRender{T}"/> により再描画を要求する。<br/>
    /// 対話パラメータ（最小サイズ・ヒットテスト半径・境界外選択の禁止）はファサードへ伝搬し、判断はコアが行う。
    /// </remarks>
    static MarqueeControl()
    {
        // 設定（外観・装飾を含む）の変更は再描画を要求する
        AffectsRender<MarqueeControl>(OptionsProperty, DecoOptionsProperty, ScalingProperty);

        // 色（描画にだけ必要な設定）の変更は本 UI の描画実装を差し替える（色は描画実装が単一所有する）
        PaintProperty.Changed.AddClassHandler<MarqueeControl>(
            static (o, e) => o.Renderer = new MarqueeRendererAvaloniaUI(e.GetNewValue<MarqueePaint>()));

        // 対話パラメータをファサードへ伝搬する（変更の影響判断・ログはコアが単一所有する）
        OptionsProperty.Changed.AddClassHandler<MarqueeControl>(
            static (c, e) => c._marquee.Options = e.GetNewValue<MarqueeOptions>());

        // 装飾の設定をコアへ伝搬する（描画可否の判断はコアが単一所有する）
        DecoOptionsProperty.Changed.AddClassHandler<MarqueeControl>(
            static (c, e) => c._marquee.DecoOptions = e.GetNewValue<MarqueeDecoOptions>());

        // 環境値（倍率）をファサードへ伝搬する（ポート未指定時の倍率の使い方・表示倍率の解決はコアが単一所有する）
        ScalingProperty.Changed.AddClassHandler<MarqueeControl>(
            static (c, e) => c._marquee.Scaling = e.GetNewValue<double>());
        DisplayScaleScalingProperty.Changed.AddClassHandler<MarqueeControl>(
            static (c, e) => c._marquee.DisplayScaleScaling = e.GetNewValue<double>());
    }

    // ====================================================================
    // 描画
    // ====================================================================

    /// <summary>描画の実装（描画内容を描画面へ写す戦略）を取得または設定する。</summary>
    /// <value>現在の描画実装。既定は <see cref="Paint"/> の色を保持する <see cref="MarqueeRendererAvaloniaUI"/>。</value>
    /// <remarks>
    /// 描画は UI の関心事であるためコアは保持しない（コアは描画内容と層の順序をデータとして返すだけである）。<br/>
    /// 層の順序に従った描画手順は UIBase の基底（<c>MarqueeRendererBase</c>）が単一所有する。<br/>
    /// 描画先（<c>IEsCanvas</c> の自 UI アダプタ）と併せて描画方式を差し替えられる。<br/>
    /// 設定すると再描画を要求する（同値の再設定では何も起こらない）。
    /// </remarks>
    /// <exception cref="ArgumentNullException">設定値が null の場合。</exception>
    public IMarqueeRenderer Renderer
    {
        get => _renderer;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (ReferenceEquals(_renderer, value))
            {
                return;
            }

            _renderer = value;
            InvalidateFrame();
            InvalidateVisual();
            InvalidateArrange();
        }
    }

    // ====================================================================
    // 1 フレーム分の描画内容（1 フレーム = 1 回だけ解決する）
    // ====================================================================

    /// <summary>直近に解決した描画内容（null = 未解決）。</summary>
    private MarqueeDrawContent? _frame;

    /// <summary>直近に解決した描画内容の基準サイズ（DIP）。</summary>
    private EsSize _frameSize;

    /// <summary>1 フレーム分の描画内容を解決して保持する（同じ基準サイズでは再解決しない）。</summary>
    /// <param name="clientSize">描画領域のサイズ（DIP。UI 実装の描画先のサイズ）。</param>
    /// <returns>そのフレームの描画内容。</returns>
    /// <remarks>
    /// 図形の描画（<c>RenderShapes</c>）とラベルの配置（<c>ArrangeOverride</c>）が同じ描画内容を使う。<br/>
    /// 解決を 2 回行うと、選択矩形・装飾・ラベル文言の解決と確保が 1 フレームで重複する。<br/>
    /// 状態が変わると <see cref="InvalidateFrame"/> が破棄し、次の解決で最新の状態を反映する。
    /// </remarks>
    private MarqueeDrawContent ResolveFrame(EsSize clientSize)
    {
        if (_frame is not { } cached || _frameSize != clientSize)
        {
            cached = _marquee.ResolveContent(_marquee.CreateRequest(clientSize));
            _frame = cached;
            _frameSize = clientSize;
        }

        return cached;
    }

    /// <summary>保持している描画内容を取得する（未解決の場合のみ解決する）。</summary>
    /// <param name="clientSize">描画領域のサイズ（DIP。未解決時の解決にだけ使う）。</param>
    /// <returns>そのフレームの描画内容。</returns>
    /// <remarks>
    /// 描画フェーズ（<c>RenderShapes</c>）は配置フェーズが解決した値をそのまま使う<br/>
    /// （配置と描画で基準サイズが丸めによりわずかに違っても、1 フレームの解決を 2 回にしない）。
    /// </remarks>
    private MarqueeDrawContent CurrentFrame(EsSize clientSize) => _frame ?? ResolveFrame(clientSize);

    /// <summary>保持している描画内容を破棄する（次の解決で最新の状態を反映する）。</summary>
    private void InvalidateFrame() => _frame = null;

    // ====================================================================
    // ログ
    // ====================================================================

    /// <summary>内部ロガー（null の場合は出力なし）。</summary>
    /// <value>null を設定した場合は <see cref="NullLogger"/> へフォールバックする。</value>
    /// <remarks>
    /// 保持とフォールバック、ファサードへの伝搬はコア（<see cref="MarqueeFacade.Log"/>）が単一所有する（本層は委譲するだけ）。<br/>
    /// 入力の判断に伴うログはコアが同じロガーで出力する。
    /// </remarks>
    public ILogger Log
    {
        get => _marquee.Log;
        set => _marquee.Log = value;
    }

    // ====================================================================
    // イベント
    // ====================================================================

    /// <summary>ドラッグの始点が決定されたときに発生するイベント（新規選択のドラッグ開始。座標は DIP）。</summary>
    public event EventHandler<MarqueePointEventArgs>? SelectionStarted;

    /// <summary>選択矩形が変更されたときに発生するイベント（上位 Mediator への通知）。</summary>
    public event EventHandler<SelectionRectEventArgs>? SelectionChanged;

    /// <summary>ドラッグ後の終点が決定され、選択矩形が確定したときに発生するイベント。</summary>
    public event EventHandler<SelectionRectEventArgs>? SelectionCompleted;

    /// <summary>領域がキャンセルされたときに発生するイベント（枠外クリック・小さすぎる矩形等）。</summary>
    public event EventHandler? SelectionCancelled;

    /// <summary>ルーペが表示する中心座標を通知するイベント（ドメイン座標）。</summary>
    /// <remarks>
    /// nudge 前後関係なく通知する（通常追従: <see cref="OnPointerMoved"/> / nudge 後: nudge 処理）。<br/>
    /// 上位（ホスト）は nudge 中かどうかを判断せず、この座標でルーペを更新するだけである。
    /// </remarks>
    public event EventHandler<MarqueePointEventArgs>? LoupePositionMoved;

    /// <summary>ビューポート（ズーム・スクロール）が変化したときに発生するイベント。</summary>
    /// <remarks>
    /// 本コントロールが属する <see cref="ScrollViewer"/> の <c>ScrollChanged</c>（スクロール・ズーム・
    /// 画像サイズ変更による Extent/Viewport 変化）を検知して発行する。<br/>
    /// 選択矩形（ドメイン座標）はズーム・スクロールで不変だが、上位（暗転マスクの描画等）は<br/>
    /// ビューポート座標で再描画する必要があるため、現在の選択矩形 + Snapshot を再通知する。<br/>
    /// </remarks>
    public event EventHandler<SelectionRectEventArgs>? ViewportChanged;

    /// <summary>Ctrl+ホイールによるズーム要求イベント（方向と発生位置）。</summary>
    /// <remarks>
    /// <see cref="OnPointerWheelChanged"/> から発火する（転送先の有無に関わらず常に通知する）。<br/>
    /// ズームの実処理（ズーム率管理・表示の変更）は上位 AP が実施する（本コントロールは要求のみ通知）。<br/>
    /// 「Ctrl+ホイール = ズーム」という解釈はコントロール化の層（<c>MarqueeControl.UIBase</c>）が単一所有する<br/>
    /// （選択コントロールをビューポートへ組み込むときにだけ必要な規則のため）。<br/>
    /// <see cref="WheelZoomRequest.Position"/> はズーム中心（カーソル位置）として使用できる。
    /// </remarks>
    public event EventHandler<WheelZoomRequest>? ZoomRequested;

    /// <summary>通常ホイール（Ctrl なし）によるスクロール要求イベント（生の回転量・修飾キー・発生位置）。</summary>
    /// <remarks>
    /// <see cref="OnPointerWheelChanged"/> から発火する（<see cref="AttachScrollViewer"/> で連携 ScrollViewer を<br/>
    /// 設定している場合はそちらをスクロールするため本イベントは発火しない）。<br/>
    /// スクロール先（祖先 ScrollViewer・ズームビューポートのパン等）の決定は上位 AP が実施する。<br/>
    /// 選択矩形はドメイン座標で不変のため、本操作は選択状態を変更しない（ビューの操作）。
    /// </remarks>
    public event EventHandler<WheelScrollRequest>? ScrollRequested;

    /// <summary>Enter キーによる確定要求イベント（選択確定後のみ発火。ドラッグ中は発火しない）。</summary>
    /// <remarks>
    /// 本コントロールが Escape / Enter キーを自己処理し（確定・キャンセルの基本動作を内包）、<br/>
    /// モード差分（確定/キャンセル後の後始末）は上位が本イベントを購読して行う。<br/>
    /// </remarks>
    public event EventHandler<SelectionRectEventArgs>? ConfirmRequested;

    /// <summary>Escape キーによるキャンセル要求イベント（選択破棄は本コントロールが実施済み）。</summary>
    /// <remarks>
    /// 本コントロールが Escape / Enter キーを自己処理し（確定・キャンセルの基本動作を内包）、<br/>
    /// モード差分（確定/キャンセル後の後始末）は上位が本イベントを購読して行う。<br/>
    /// </remarks>
    public event EventHandler? CancelRequested;

    /// <summary>描画完了（コントロールが図形を描き終えたこと）を通知するイベント。</summary>
    /// <remarks>
    /// 本コントロールは文字（ラベル・座標ラベル）を描かない。テキストの実寸測定は描画 API に依存するため、<br/>
    /// 「どの文言をどこへ描くか」はコアが決め、実際の配置は本コントロールが行う（<see cref="MarqueeLabelPlan"/>）。<br/>
    /// 契約は文字に限定せず**描画内容全体**（<see cref="MarqueeRenderedEventArgs.Content"/>）を渡すため、ホストは文字以外の上書きもできる。<br/>
    /// <b>描画中に発火する</b>ため、ハンドラ内で描画を完了させること（コンテキストを保持しない）。<br/>
    /// 描画のたびに発火する（文字の有無は <c>Content.HasText</c> で判定できる）。
    /// </remarks>
    public event EventHandler<MarqueeRenderedEventArgs>? Rendered;

    // ====================================================================
    // 外観・動作設定（StyledProperty）
    // ====================================================================

    /// <summary><see cref="Options"/> 依存関係プロパティ（外観・対話パラメータをまとめた設定）。</summary>
    /// <remarks>
    /// 個別に公開していた設定（外観 10 項目・対話パラメータ 3 項目）を 1 つの不変オブジェクトへ統合し、公開プロパティを 1 つに縮小する。<br/>
    /// 設定値は不変（<see cref="MarqueeOptions"/>）であるため、同値の再設定では変更通知が発生しない（無駄な再描画を防ぐ）。
    /// </remarks>
    public static readonly StyledProperty<MarqueeOptions> OptionsProperty =
        AvaloniaProperty.Register<MarqueeControl, MarqueeOptions>(nameof(Options), defaultValue: MarqueeOptions.Default);

    /// <summary><see cref="Paint"/> 依存関係プロパティ（色。描画にだけ必要な設定）。</summary>
    /// <remarks>
    /// 色はコアの計算に入らない（描画にだけ必要）ため、コアの設定（<see cref="Options"/> / <see cref="DecoOptions"/>）には含めず、<br/>
    /// 描画の設定（<see cref="MarqueePaint"/>）として本コントロールが公開する（描画実装が単一所有する）。
    /// </remarks>
    public static readonly StyledProperty<MarqueePaint> PaintProperty =
        AvaloniaProperty.Register<MarqueeControl, MarqueePaint>(nameof(Paint), defaultValue: MarqueePaint.Default);

    /// <summary>外観・対話パラメータをまとめた設定を取得または設定する。</summary>
    /// <value>設定（枠線・ハンドル・十字マーカー・座標ラベルの外観、最小選択サイズ・ヒットテスト半径・境界外選択の禁止）。既定は <see cref="MarqueeOptions.Default"/>。</value>
    /// <remarks>
    /// 設定を変更すると再描画され、対話パラメータ（最小サイズ・ヒットテスト半径・境界外選択の禁止）はファサードへ伝搬する。<br/>
    /// 一部の項目だけを変える場合は <c>MarqueeOptions.Default with { ShowHandles = false }</c> の形で指定する（指定しない項目は既定値を保つ）。<br/>
    /// 色は UI 非依存の <see cref="System.Drawing.Color"/> で受け取り、描画時に Avalonia の色型へ変換する。
    /// </remarks>
    public MarqueeOptions Options
    {
        get => GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    /// <summary>描画に使う色（描画にだけ必要な設定）を取得または設定する。</summary>
    /// <value>色の単一値。既定は <see cref="MarqueePaint.Default"/>。</value>
    /// <remarks>
    /// 色はコアの計算に入らない（描画にだけ必要）ため、コアの設定（<see cref="Options"/> / <see cref="DecoOptions"/>）ではなく本プロパティが単一所有する。<br/>
    /// 変更すると描画実装（<see cref="Renderer"/>）が新しい色で差し替えられる。
    /// </remarks>
    public MarqueePaint Paint
    {
        get => GetValue(PaintProperty);
        set => SetValue(PaintProperty, value);
    }

    // ====================================================================
    // 装飾（暗転マスク・穴あけ枠・ラベル）の設定
    // ====================================================================

    /// <summary><see cref="DecoOptions"/> 依存関係プロパティ（装飾の設定）。</summary>
    /// <remarks>
    /// 装飾は選択コントロール単体の概念ではなく組み込み先の表示であるため、選択の設定（<see cref="Options"/>）とは別の値にしている。<br/>
    /// 設定値は不変（<see cref="MarqueeDecoOptions"/>）であるため、同値の再設定では変更通知が発生しない（無駄な再描画を防ぐ）。
    /// </remarks>
    public static readonly StyledProperty<MarqueeDecoOptions> DecoOptionsProperty =
        AvaloniaProperty.Register<MarqueeControl, MarqueeDecoOptions>(nameof(DecoOptions), defaultValue: MarqueeDecoOptions.Default);

    /// <summary>装飾（暗転マスク・穴あけ枠・ラベル）の設定を取得または設定する。</summary>
    /// <value>装飾の設定（暗転色・枠線・ラベル・ハイライト・内側の塗り）。既定は <see cref="MarqueeDecoOptions.Default"/>。</value>
    /// <remarks>
    /// 一部の項目だけを変える場合は <c>MarqueeDecoOptions.Default with { ... }</c> の形で指定する（指定しない項目は既定値を保つ）。<br/>
    /// 色は UI 非依存の <see cref="System.Drawing.Color"/> で受け取り、描画時に Avalonia の色型へ変換する。
    /// </remarks>
    public MarqueeDecoOptions DecoOptions
    {
        get => GetValue(DecoOptionsProperty);
        set => SetValue(DecoOptionsProperty, value);
    }

    // ====================================================================
    // 描画入力（環境値・ホスト結線・座標変換ポート）
    // ====================================================================

    /// <summary><see cref="Scaling"/> 依存関係プロパティ。</summary>
    /// <remarks>既定値はコアの <see cref="ScalingDefault"/> を参照する（既定値の二重所有を防ぐ）。</remarks>
    public static readonly StyledProperty<double> ScalingProperty =
        AvaloniaProperty.Register<MarqueeControl, double>(nameof(Scaling), ScalingDefault);

    /// <summary>ドメイン → DIP 変換の基準倍率を取得または設定する。</summary>
    /// <value>ドメイン → DIP 変換の基準倍率。既定は 1.0。</value>
    /// <remarks>
    /// <see cref="SetCoordinateTransform"/> でポートを設定した場合は使われない（ポート優先）。<br/>
    /// ポートと倍率の使い分け（ポート優先・倍率が 0 以下や等倍なら等価変換）はコアが単一所有する。<br/>
    /// 選択矩形と十字マーカーの描画はファサードの座標変換ポートのみを使うため本値の影響を受けない
    /// （本値が影響するのは装飾のサイズラベル・ラベル位置）。
    /// </remarks>
    public double Scaling
    {
        get => GetValue(ScalingProperty);
        set => SetValue(ScalingProperty, value);
    }

    /// <summary><see cref="DisplayScaleScaling"/> 依存関係プロパティ。</summary>
    /// <remarks>環境値（DPI・マルチスクリーン）だが、ホストのバインディングで設定できるよう依存関係プロパティとして公開する。</remarks>
    public static readonly StyledProperty<double> DisplayScaleScalingProperty =
        AvaloniaProperty.Register<MarqueeControl, double>(nameof(DisplayScaleScaling));

    /// <summary>サイズラベル表示用の DPI スケーリング係数（マルチスクリーン補正）を取得または設定する。</summary>
    /// <value>DisplayScale の百分率に使う係数。未設定（0 以下）の場合は <see cref="Scaling"/> と同じ値が使用される。</value>
    /// <remarks>
    /// マルチスクリーン環境では、選択矩形が存在するスクリーンのスケーリング係数を設定する。<br/>
    /// 0 以下を <see cref="Scaling"/> と同値として扱う規則はコア（<see cref="MarqueeFacade.DisplayScaleScaling"/>）が単一所有する。
    /// </remarks>
    public double DisplayScaleScaling
    {
        get => _marquee.DisplayScaleScaling;
        set => SetValue(DisplayScaleScalingProperty, value);
    }

    /// <summary>サイズラベルのテキストを描画時に評価するプロバイダを取得または設定する。</summary>
    /// <value>描画のたびに呼び出される関数。null または空文字の場合は穴あけ矩形から自動生成する。</value>
    /// <remarks>
    /// デリゲート（ホスト結線）のため <see cref="DecoOptions"/> には含めない（値の等価比較の対象外にするため）。<br/>
    /// 保持と再描画の要求はコア（<see cref="MarqueeFacade.SizeLabelOverrideProvider"/>）が単一所有する。<br/>
    /// <b>操作ラベル・情報ラベルは本コントロールの概念ではない</b>（アプリ固有の付加情報）ため、ホストが自前で描く。
    /// </remarks>
    public Func<string?>? SizeLabelOverrideProvider
    {
        get => _marquee.SizeLabelOverrideProvider;
        set => _marquee.SizeLabelOverrideProvider = value;
    }

    /// <summary>サイズラベルの表示スケーリングを矩形ごとに解決するプロバイダを取得または設定する。</summary>
    /// <value>描画のたびに呼び出される関数（引数はドメイン座標の矩形）。null の場合は <see cref="DisplayScaleScaling"/> を使う。</value>
    /// <remarks>
    /// マルチモニタでモニタごとに表示スケールが異なる場合、ホストは矩形からモニタを判定してそのスケールを返す。<br/>
    /// 文言（書式）はコアが単一所有するため、ホストは数値だけを与える。<br/>
    /// 保持と再描画の要求はコア（<see cref="MarqueeFacade.SizeLabelDisplayScalingProvider"/>）が単一所有する。
    /// </remarks>
    public Func<EsRect, double>? SizeLabelDisplayScalingProvider
    {
        get => _marquee.SizeLabelDisplayScalingProvider;
        set => _marquee.SizeLabelDisplayScalingProvider = value;
    }

    // ====================================================================
    // 公開操作 API
    // ====================================================================

    /// <summary>現在の選択状態のスナップショット（ポーリング不要の完全状態）。</summary>
    /// <remarks>
    /// <see cref="MarqueeSnapshot"/> の全項目（State / SelectionRect / ActiveTarget / MousePosition /<br/>
    /// IsMouseDown / IsDragging / NudgeActive / HasSelection）を公開する。ホスト（上位 AP）はイベント引数の<br/>
    /// Snapshot と同一形式で現在の状態を取得できる（状態のポーリングは本プロパティで完結）。<br/>
    /// 選択状態の判定（確定・キャンセル等の操作分岐）は <see cref="MarqueeSnapshot.HasSelection"/> を使用する。
    /// </remarks>
    public MarqueeSnapshot CurrentSnapshot => _marquee.CreateSnapshot();

    /// <summary>選択矩形があるかどうかを取得する。</summary>
    /// <value>選択中の場合は true。</value>
    /// <remarks>描画のたびに参照されるため、スナップショットの生成を伴わない軽量なプロパティとする。</remarks>
    public bool HasSelection => _marquee.HasSelection;

    /// <summary>現在の選択矩形（ドメイン座標）を取得する。</summary>
    /// <value>選択矩形（ドメイン座標）。未選択時は 0x0 の空矩形。</value>
    /// <remarks>描画のたびに参照されるため、スナップショットの生成を伴わない軽量なプロパティとする。</remarks>
    public EsRect SelectionRect => _marquee.SelectionRect;

    /// <summary>ドメイン ↔ コントロールローカル DIP の座標変換ポートを設定する。</summary>
    /// <param name="port">座標変換ポート（Forward = DIP→ドメイン / Inverse = ドメイン→DIP / Bounds = ドメイン境界）。null の場合は等価変換。</param>
    /// <remarks>
    /// ホスト（上位 AP）がモードに応じた変換ポートを設定する（AreaSelect: 仮想スクリーンピクセル / Crop: 元画像ピクセル）。<br/>
    /// ポートの生成はコアの <see cref="EsUtil.Platform.Core.Coordinate.CoordTransform"/> のファクトリ（<c>Compose</c> / <c>ScrollOffset</c> / <c>DomainScale</c> / <c>ImageLayout</c> 等）で行う。<br/>
    /// 未設定時は等価変換（1 ドメインピクセル = 1 DIP・無制限）。<br/>
    /// 変換関数は呼び出し毎に評価されるため、ズーム・スクロール等の動的変化を反映できる。<br/>
    /// 座標変換の変更は描画（選択矩形・ハンドル・十字線）へ即時反映されるため、再描画を要求する。
    /// </remarks>
    public void SetCoordinateTransform(ICoordTransform? port) => _marquee.SetCoordinateTransform(port);

    /// <summary>選択矩形を外部状態から復元する。</summary>
    /// <param name="rect">選択矩形（ドメイン座標）。</param>
    /// <remarks>
    /// 状態の更新と通知はコア（ファサード）が担う（<see cref="MarqueeOptions.ClampToDomainBounds"/> が有効な場合は境界内へクリップしてから設定する）。<br/>
    /// 設定後は <see cref="SelectionChanged"/> が発火し、再描画される。
    /// </remarks>
    public void SetSelection(Rect rect) => _marquee.SetSelection(rect.ToEsRect());

    /// <summary>選択状態と nudge 状態を初期化する。</summary>
    /// <remarks>状態の初期化はコア（ファサード）が担う（再描画される）。</remarks>
    public void ResetSelection() => _marquee.ResetSelection();

    /// <summary>キャンセル要求を処理する（Escape キーと同じ条件）。</summary>
    /// <remarks>
    /// 外部（ホスト）が選択操作をキャンセルする際に呼ぶ。<br/>
    /// ドラッグ中 / 選択ありの場合は選択破棄を実施した後に <see cref="CancelRequested"/> を発火する。
    /// </remarks>
    public void RequestCancel() => _marquee.RequestCancel();

    /// <summary>外部（ホスト）からの確定要求を処理する（Enter キーと同じ条件）。</summary>
    /// <remarks>
    /// UI ボタン等から Marquee の選択を確定する際に呼ぶ。<br/>
    /// ドラッグ中は無視し、選択確定後のみ <see cref="ConfirmRequested"/>（選択矩形 + Snapshot 付き）を発行する。<br/>
    /// 確定後に選択状態をクリアする（確定後の再ドラッグ・再確定を防ぐ）。
    /// </remarks>
    public void RequestConfirm() => _marquee.RequestConfirm();

    // ====================================================================
    // コアとの連携（効果の適用先・通知の転送）
    // ====================================================================

    /// <summary>コア（ファサード）が要求する効果を本コントロールへ適用する実装。</summary>
    /// <remarks>
    /// 効果の適用順序（フォーカス → カーソル形状 → 再描画）はコアが単一所有するため、<br/>
    /// 本実装は呼ばれた順に自 UI の操作へ接続するだけでよい（判断を持たない）。<br/>
    /// <see cref="MarqueeEffectsBase"/> を継承し、本 UI が必要とする効果だけを override する<br/>
    /// （ポインターキャプチャは Avalonia が押下で暗黙にキャプチャするため実装しない）。<br/>
    /// コアは <see cref="IMarqueeEffects"/> のみに依存し、UI フレームワークを知らない。
    /// </remarks>
    private sealed class Effects(MarqueeControl owner) : MarqueeEffectsBase
    {
        /// <summary>フォーカスを取得する（矢印キー nudge の受け付け開始）。</summary>
        public override void RequestFocus() => owner.Focus();

        /// <summary>再描画を要求する。</summary>
        /// <remarks>
        /// ラベル（サイズ・カーソル座標）はテキスト要素であり、配置は配置フェーズで書き換えるため、再描画と併せて配置も無効化する。<br/>
        /// 保持している描画内容も破棄する（状態の変化を次の解決へ反映させる）。
        /// </remarks>
        public override void RequestRedraw()
        {
            owner.InvalidateFrame();
            owner.InvalidateVisual();
            owner.InvalidateArrange();
        }

        /// <summary>カーソル形状をコアが示す種別に応じて更新する。</summary>
        /// <param name="kind">カーソル形状の種別（コア提供）。</param>
        public override void SetCursorShape(MarqueeCursorKind kind) => owner.ApplyCursorShape(kind);

        /// <summary>ドメイン座標を画面物理座標へ変換する（カーソル移動用）。</summary>
        /// <param name="domainPoint">ドメイン座標。</param>
        /// <returns>画面物理座標（UI 非依存のコア型）。</returns>
        public override EsPointInt ToScreenPoint(EsPoint domainPoint)
            => ScreenConverter.LocalDipToScreen(owner, owner._marquee.ToDip(domainPoint).ToAvaloniaPoint()).ToEsPointInt();
    }

    /// <summary>コアからの通知を公開イベントへ転送する（判断はしない）。</summary>
    /// <param name="sender">送信元（ファサード）。</param>
    /// <param name="notice">通知の種別・座標・スナップショット。</param>
    /// <remarks>
    /// 転送のみを担う（確定/キャンセルの判断・状態の更新はコアが済ませている）。<br/>
    /// 各 UI のイベント引数の型（Avalonia の <see cref="Rect"/> / <see cref="Point"/>）へ境界変換する。
    /// </remarks>
    private void OnNotified(object? sender, MarqueeNotice notice)
    {
        switch (notice.Kind)
        {
            // 選択（始点・変更・確定・キャンセル）
            case MarqueeNoticeKind.SelectionStarted: SelectionStarted?.Invoke(this, ToPointArgs(notice)); break;
            case MarqueeNoticeKind.SelectionChanged: SelectionChanged?.Invoke(this, ToRectArgs(notice)); break;
            case MarqueeNoticeKind.SelectionCompleted: SelectionCompleted?.Invoke(this, ToRectArgs(notice)); break;
            case MarqueeNoticeKind.SelectionCancelled: SelectionCancelled?.Invoke(this, EventArgs.Empty); break;

            // ルーペ（追従位置）・確定要求・キャンセル要求
            // コアは追従位置を汎用名（MarkerPositionMoved）で通知するため、ホスト向けの名前（ルーペ中心）へ写像する
            case MarqueeNoticeKind.MarkerPositionMoved: LoupePositionMoved?.Invoke(this, ToPointArgs(notice)); break;
            case MarqueeNoticeKind.ConfirmRequested: ConfirmRequested?.Invoke(this, ToRectArgs(notice)); break;
            case MarqueeNoticeKind.CancelRequested: CancelRequested?.Invoke(this, EventArgs.Empty); break;
        }
    }

    /// <summary>通知を点座標のイベント引数へ変換する。</summary>
    /// <param name="notice">通知。</param>
    /// <returns>通知座標（DIP）とスナップショットを含むイベント引数。</returns>
    private static MarqueePointEventArgs ToPointArgs(MarqueeNotice notice)
        => new(notice.Position.ToAvaloniaPoint(), notice.Snapshot);

    /// <summary>通知を選択矩形のイベント引数へ変換する。</summary>
    /// <param name="notice">通知。</param>
    /// <returns>選択矩形（DIP）とスナップショットを含むイベント引数。</returns>
    private static SelectionRectEventArgs ToRectArgs(MarqueeNotice notice) => ToRectArgs(notice.Snapshot);

    /// <summary>スナップショットを選択矩形のイベント引数へ変換する（コアの通知を経由しない通知用）。</summary>
    /// <param name="snapshot">通知時点の状態スナップショット。</param>
    /// <returns>選択矩形（DIP）とスナップショットを含むイベント引数。</returns>
    private static SelectionRectEventArgs ToRectArgs(MarqueeSnapshot snapshot)
        => new(snapshot.SelectionRect.ToAvaloniaRect(), snapshot);

    /// <summary>カーソル形状をコアが示す種別に応じて更新する。</summary>
    /// <param name="kind">カーソル形状の種別（コア提供）。</param>
    /// <remarks>種別と自 UI のカーソルの対応のみを持つ（どのハンドルが同じカーソルになるかはコアが判断済み）。</remarks>
    private void ApplyCursorShape(MarqueeCursorKind kind)
    {
        // 標準カーソルは Lazy で遅延共有する（ポインター移動毎の生成を排除しつつ、初回アクセスで ICursorFactory を解決）
        Cursor = kind switch
        {
            MarqueeCursorKind.DiagonalNWSE => s_topLeftCornerCursor.Value,
            MarqueeCursorKind.DiagonalNESW => s_topRightCornerCursor.Value,
            MarqueeCursorKind.Vertical => s_sizeNorthSouthCursor.Value,
            MarqueeCursorKind.Horizontal => s_sizeWestEastCursor.Value,
            MarqueeCursorKind.Move => s_sizeAllCursor.Value,
            _ => Cursor.Default,
        };
    }

    // ====================================================================
    // イベントオーバーライド（ポインター入力）
    // ====================================================================

    /// <summary>ポインター押下時の処理。左ボタンのみ処理する。</summary>
    /// <param name="e">ポインターイベント。</param>
    /// <remarks>
    /// 左ボタン以外（右クリック・中ボタン等）は無視する。<br/>
    /// 座標はコントロールローカル DIP で取得し、ドメイン座標へ変換してコア（ファサード）へ通知する（解釈はコアが行う）。<br/>
    /// ダブルクリックの確定/キャンセル判定・nudge の終了・ハンドルのスナップはコアの判断に従う。
    /// </remarks>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        // 左ボタン以外（右クリック・中ボタン等）は処理しない
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        e.Handled = true;
        _marquee.Press(ToDomain(e), e.ClickCount);
    }

    /// <summary>ポインター移動時の処理。</summary>
    /// <param name="e">ポインターイベント。</param>
    /// <remarks>
    /// 座標をドメイン座標へ変換してコア（ファサード）へ通知する。<br/>
    /// ドラッグの適用・nudge 中のエコー移動の抑制・カーソル形状の決定はコアの判断に従う。
    /// </remarks>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _marquee.Move(ToDomain(e));
    }

    /// <summary>ポインター解放時の処理。</summary>
    /// <param name="e">ポインターイベント。</param>
    /// <remarks>
    /// 座標をドメイン座標へ変換してコア（ファサード）へ通知する。<br/>
    /// 最小サイズによるキャンセル・確定・クリック検出・nudge の終了はコアの判断に従う。
    /// </remarks>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _marquee.Release(ToDomain(e));
    }

    /// <summary>ポインターイベントの座標をドメイン座標へ変換する。</summary>
    /// <param name="e">ポインターイベント。</param>
    /// <returns>ドメイン座標。</returns>
    /// <remarks>コントロールローカル DIP で取得した座標を、ファサードの座標変換ポートで変換する（境界の 1 行変換）。</remarks>
    private EsPoint ToDomain(PointerEventArgs e) => _marquee.ToDomain(e.GetPosition(this).ToEsPoint());
}

