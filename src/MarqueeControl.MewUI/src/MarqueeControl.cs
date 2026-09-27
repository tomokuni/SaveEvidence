using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Aprillz.MewUI.Skia.Controls;
using EsUtil.Feature.Marquee.Core.Decoration;
using EsUtil.Feature.Marquee.Presenter;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.MewUI;
using EsUtil.Platform.Native;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;
using MarqueeFacade = EsUtil.Feature.Marquee.Core.Marquee;
using static EsUtil.Feature.Marquee.Core.Marquee.Limits;
using EsUtil.Platform.Skia;
using EsUtil.Feature.Marquee.Core.Contract;
using EsUtil.Feature.Marquee.Core.Drawing;
using EsUtil.Feature.Marquee.Core.Interaction;
using EsUtil.Feature.Marquee.Core.Setting;



namespace EsUtil.SaveEvidence.Marquee.MewUI;

/// <summary>リサイズ可能なドラッグ矩形のコントロール（MewUI 版）。</summary>
/// <remarks>
/// Avalonia 版と同じ UI 非依存のコアのファサード（<c>Feature.Marquee.Core</c> の <see cref="MarqueeFacade"/>）を使い、<br/>
/// 本コントロールは「入力の受付」「効果の適用」「通知の転送」「描画」のみを行う薄い層である。<br/>
/// <br/>
/// <b>座標系</b>:<br/>
/// - ドメイン座標 = 選択対象（画像ピクセル・仮想スクリーンピクセル等）。コアはこの座標で動作する。<br/>
/// - ローカル DIP = 本コントロールのローカル座標。入力と描画はこの座標で行い、境界でポート（<see cref="ICoordTransform"/>）により変換する。<br/>
/// <br/>
/// <b>操作</b>:<br/>
/// - 左ドラッグ: 新規選択、またはハンドルのリサイズ・移動<br/>
/// - 矢印キー: nudge（1 ドメインピクセル単位の微調整。カーソルを非表示にして十字マーカーを表示する）<br/>
/// - Escape: キャンセル要求 / Enter: 確定要求<br/>
/// <br/>
/// <b>Avalonia 版との違い</b>:<br/>
/// - 描画は中間ビットマップを経由せず <see cref="SkiaCanvasView"/> のキャンバスへ直接行う。<br/>
/// - イベント引数の座標はコア型（<see cref="EsPoint"/> / <see cref="EsRect"/>）で公開する（境界変換が不要）。<br/>
/// - <c>ScrollViewer</c> の自動連携は行わない。ビューポートの変化はホストが <see cref="NotifyViewportChanged"/> で通知する。<br/>
/// - 矢印キーの nudge はフォーカスを必要とする（Avalonia 版の TopLevel 購読によるフォーカス非依存とは異なる）。<br/>
/// - ドラッグ中はマウスキャプチャを行う（コントロール外への移動でもイベントを受け取る）。<br/>
/// - ログ出力は <see cref="Log"/> プロパティで注入する（デフォルトは <see cref="NullLogger"/>。Avalonia 版と同一の書式）。
/// </remarks>
public sealed class MarqueeControl : Panel
{
    /// <summary><see cref="Options"/> プロパティ（外観・対話パラメータをまとめた設定）。</summary>
    /// <remarks>
    /// 個別に公開していた設定（外観 10 項目・対話パラメータ 3 項目）を 1 つの不変オブジェクトへ統合し、公開プロパティを 1 つに縮小する。<br/>
    /// 設定値は不変（<see cref="MarqueeOptions"/>）であるため、同値の再設定では変更通知が発生しない（無駄な再描画を防ぐ）。<br/>
    /// 対話パラメータ（最小サイズ・ヒットテスト半径・境界外選択の禁止）はファサードへ伝搬し、判断と変更ログはコアが単一所有する。
    /// </remarks>
    public static readonly MewProperty<MarqueeOptions> OptionsProperty =
        MewProperty<MarqueeOptions>.Register<MarqueeControl>(
            nameof(Options), MarqueeOptions.Default, MewPropertyOptions.AffectsRender,
            changed: static (owner, _, newValue) =>
            {
                owner._marquee.Options = newValue;
                owner.InvalidateFrame();
            });

    /// <summary><see cref="DecoOptions"/> プロパティ（装飾の設定）。</summary>
    /// <remarks>
    /// 装飾は選択コントロール単体の概念ではなく組み込み先の表示であるため、選択の設定（<see cref="Options"/>）とは別の値にしている。<br/>
    /// 設定値は不変（<see cref="MarqueeDecoOptions"/>）であるため、同値の再設定では変更通知が発生しない（無駄な再描画を防ぐ）。
    /// </remarks>
    public static readonly MewProperty<MarqueeDecoOptions> DecoOptionsProperty =
        MewProperty<MarqueeDecoOptions>.Register<MarqueeControl>(
            nameof(DecoOptions), MarqueeDecoOptions.Default, MewPropertyOptions.AffectsRender,
            changed: static (owner, _, newValue) =>
            {
                owner._marquee.DecoOptions = newValue;
                owner.InvalidateFrame();
            });

    /// <summary><see cref="Paint"/> プロパティ（色。描画にだけ必要な設定）。</summary>
    /// <remarks>
    /// 色はコアの計算に入らない（描画にだけ必要）ため、コアの設定（<see cref="Options"/> / <see cref="DecoOptions"/>）には含めず、<br/>
    /// 描画の設定（<see cref="MarqueePaint"/>）として本コントロールが公開する（描画実装が単一所有する）。
    /// </remarks>
    public static readonly MewProperty<MarqueePaint> PaintProperty =
        MewProperty<MarqueePaint>.Register<MarqueeControl>(
            nameof(Paint), MarqueePaint.Default, MewPropertyOptions.AffectsRender,
            changed: static (owner, _, newValue) =>
                owner.Renderer = new MarqueeRendererMewUI(newValue));

    /// <summary><see cref="DipToDeviceScaling"/> プロパティ（DIP → 物理ピクセルのスケーリング係数）。</summary>
    public static readonly MewProperty<double> DipToDeviceScalingProperty =
        MewProperty<double>.Register<MarqueeControl>(nameof(DipToDeviceScaling), DpiScaler.StandardScaling, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="DomainToDipScaling"/> プロパティ（座標変換ポートへ適用する倍率）。</summary>
    /// <remarks>
    /// <see cref="SetCoordinateTransform"/> でポートを設定した場合は使用しない（等倍スケール専用の簡易指定）。<br/>
    /// 既定値はコアの <see cref="DomainToDipScalingDefault"/> を参照する（既定値の二重所有を防ぐ）。
    /// </remarks>
    public static readonly MewProperty<double> DomainToDipScalingProperty =
        MewProperty<double>.Register<MarqueeControl>(
            nameof(DomainToDipScaling), DomainToDipScalingDefault, MewPropertyOptions.AffectsRender,
            changed: static (owner, _, newValue) =>
            {
                owner._marquee.DomainToDipScaling = newValue;
                owner.InvalidateFrame();
            });

    /// <summary>コアのファサード（入力の解釈・状態遷移の判断・描画内容の解決・設定の窓口）。</summary>
    private readonly MarqueeFacade _marquee;

    /// <summary>描画の実装（描画内容を描画面へ写す戦略）。既定は自 UI の描画実装（色を保持する）。</summary>
    /// <remarks>
    /// 描画は UI の関心事であるためコアは保持しない（コアは描画内容と層の順序をデータとして返すだけである）。<br/>
    /// 層の順序に従った描画手順は UIBase の基底（<c>MarqueeRendererBase</c>）が単一所有する。<br/>
    /// 既定値は <see cref="Paint"/> の既定値（<see cref="MarqueePaint.Default"/>）と同じ色で組み立てる。
    /// </remarks>
    private IMarqueeRenderer _renderer = new MarqueeRendererMewUI(MarqueePaint.Default);

    /// <summary>図形（装飾・ハンドル・始点マーカー）を描画する内部キャンバス。</summary>
    /// <remarks>
    /// <b>描画順を構造で保証する</b>: 本パネルが図形用キャンバスを先に、ラベル要素を後に子として持つため、<br/>
    /// ラベルは常に図形より後に描かれる（コアの「ラベルは最上位」契約が構造で成り立つ）。<br/>
    /// 入力は本パネルが受ける（本キャンバスはヒットテスト対象外）。
    /// </remarks>
    private readonly ShapeCanvas _shapes;

    /// <summary>配置中のラベル要素（背景 + テキスト）。子の末尾側へ置くため常に最上位になる。</summary>
    private readonly List<Border> _labels = [];

    /// <summary>コアが要求する効果（フォーカス・再描画・カーソル形状・キャプチャ・座標変換）の適用先。</summary>
    private readonly IMarqueeEffects _effects;

    /// <summary>ホイール入力を解釈してビューポート操作の要求（ズーム / スクロール）を出す補助。</summary>
    /// <remarks>
    /// ファサードへ接続し、ホイールの解釈（Ctrl = ズーム・連携先への適用）は<br/>
    /// <c>Feature.Marquee.Presenter</c> が単一所有する<br/>
    /// （本層は修飾キーの真偽と回転量を渡し、要求を公開イベントへ転送するだけ）。
    /// </remarks>
    private readonly MarqueeWheelInterpreter _wheel;

    /// <summary>ビューポート（パン・ズーム）の変化を再通知する補助。</summary>
    /// <remarks>
    /// 「ビューポートの変化 = 現状態の再通知 + 再描画」というビューポート連携の規則は<br/>
    /// <c>Feature.Marquee.Presenter</c> が単一所有する（本層は自 UI の公開イベントへ転送するだけ）。
    /// </remarks>
    private readonly MarqueeViewportNotifier _viewport;

    /// <summary>カーソルサービス（nudge のカーソル非表示・移動）。</summary>
    /// <remarks>既定実装（Win32）の解決は本 UI 層が行う（コアは抽象と <see langword="null"/> のみを扱う）。</remarks>
    private readonly ICursorService _cursor;

    /// <summary>ドラッグ矩形コントロールを既定構成で初期化する。</summary>
    public MarqueeControl()
        : this(null)
    {
    }

    /// <summary>カーソルサービスを注入してドラッグ矩形コントロールを初期化する。</summary>
    /// <param name="cursorService">カーソルサービス。null の場合は Windows 既定実装を使用する。</param>
    /// <remarks>
    /// 静止画として扱い連続再描画を無効化する（選択の変更時のみ再描画する）。<br/>
    /// フォーカスを失った時点で nudge を終了する（カーソルの非表示残留を防ぐ）。<br/>
    /// 矢印キーの nudge はフォーカスを必要とする（Avalonia 版の TopLevel 購読によるフォーカス非依存とは異なる）。<br/>
    /// コアからの通知は公開イベントへ転送し、設定（<see cref="Options"/>）をファサードへ伝搬する。
    /// </remarks>
    public MarqueeControl(ICursorService? cursorService)
    {
        Focusable = true;
        _cursor = cursorService ?? PlatformFactory.CreateCursorService();
        _effects = new Effects(this);
        _marquee = new MarqueeFacade(_effects, _cursor, Options);
        // 現在の環境値（倍率）をファサードへ引き継ぐ（以降の変更はプロパティ変更ハンドラが伝搬する）
        _marquee.DomainToDipScaling = GetValue(DomainToDipScalingProperty);
        _marquee.Notified += OnNotified;
        // 装飾の設定はコアが単一所有する（本層は公開プロパティをコアへ委譲するだけ）
        _marquee.DecoOptions = GetValue(DecoOptionsProperty);
        // ホイール要求（ズーム / スクロール）の解釈はコントロール化の層が単一所有する（本層は自 UI の公開イベントへ転送するだけ）
        _wheel = new MarqueeWheelInterpreter(_marquee);
        _wheel.ZoomRequested += (_, request) => ZoomRequested?.Invoke(this, request);
        _wheel.ScrollRequested += (_, request) => ScrollRequested?.Invoke(this, request);
        // ビューポート変化の通知（再通知 + 再描画）もコントロール化の層が単一所有する
        _viewport = new MarqueeViewportNotifier(_marquee, _effects);
        _viewport.ViewportChanged += (_, snapshot) => ViewportChanged?.Invoke(this, ToRectArgs(snapshot));

        // 図形は専用の子キャンバスが描く（ラベル要素より先に子として加えるため、常にラベルの下になる）
        _shapes = new ShapeCanvas(this) { IsHitTestVisible = false };
        Add(_shapes);

        // フォーカスを失ったら nudge を終了する（カーソル非表示の残留防止）
        this.OnLostFocus(EndNudge);
    }

    /// <summary>図形（装飾・ハンドル・始点マーカー）だけを描画する内部 Skia キャンバス。</summary>
    /// <remarks>
    /// 親（<see cref="MarqueeControl"/>）の描画内容を読んで描くだけであり、判断を持たない。<br/>
    /// 連続再描画を無効化し、親からの要求（選択・設定の変更）があるときだけ描き直す。
    /// </remarks>
    private sealed class ShapeCanvas : SkiaCanvasView
    {
        /// <summary>描画内容の取得元（親コントロール）。</summary>
        private readonly MarqueeControl _owner;

        /// <summary>描画先の親を指定して初期化する。</summary>
        /// <param name="owner">描画内容の取得元（親コントロール）。</param>
        public ShapeCanvas(MarqueeControl owner)
        {
            _owner = owner;
            // 静止画として扱う（選択・設定の変更時のみ再描画する）
            ContinuousAnimation = false;
            PaintSurface += _owner.OnPaintSurface;
        }
    }

    /// <summary>ドラッグの始点が決定されたときに発火する（新規選択のドラッグ開始。座標はドメイン座標）。</summary>
    public event EventHandler<MarqueePointEventArgs>? SelectionStarted;

    /// <summary>選択矩形が変更されたときに発火する。</summary>
    public event EventHandler<SelectionRectEventArgs>? SelectionChanged;

    /// <summary>ドラッグ後の終点が決定され、選択矩形が確定したときに発火する。</summary>
    public event EventHandler<SelectionRectEventArgs>? SelectionCompleted;

    /// <summary>領域がキャンセルされたときに発火する（枠外ダブルクリック・小さすぎる矩形等）。</summary>
    public event EventHandler? SelectionCancelled;

    /// <summary>ルーペが表示する中心座標を通知する（ドメイン座標）。</summary>
    /// <remarks>通常追従ではポインタ位置、ドラッグ・nudge 中は動いている側のハンドル位置を通知する。</remarks>
    public event EventHandler<MarqueePointEventArgs>? LoupePositionMoved;

    /// <summary>ビューポート（ズーム・スクロール）が変化したときに発火する。</summary>
    /// <remarks>
    /// 選択矩形（ドメイン座標）はズーム・スクロールで不変だが、上位（暗転マスク等）はビューポート座標で<br/>
    /// 再描画する必要がある。ホストが <see cref="NotifyViewportChanged"/> を呼ぶと、現在の選択矩形 + Snapshot を再通知する。
    /// </remarks>
    public event EventHandler<SelectionRectEventArgs>? ViewportChanged;

    /// <summary>Ctrl+ホイールによるズーム要求時に発火する（方向と発生位置）。</summary>
    /// <remarks>
    /// ズームの実処理（画像サイズ変更・ズーム率管理）は上位 AP が実施する（本コントロールは要求のみ通知する）。<br/>
    /// 「Ctrl+ホイール = ズーム」という解釈はコントロール化の層（<c>Feature.Marquee.Presenter</c>）が単一所有する<br/>
    /// （選択コントロールをビューポートへ組み込むときにだけ必要な規則のため）。<br/>
    /// <see cref="WheelZoomRequest.Position"/> はズーム中心（カーソル位置）として使用できる。
    /// </remarks>
    public event EventHandler<WheelZoomRequest>? ZoomRequested;

    /// <summary>通常ホイール（Ctrl なし）によるスクロール要求時に発火する（生の回転量・修飾キー・発生位置）。</summary>
    /// <remarks>
    /// スクロール先（ズームビューポートのパン等）の決定は上位 AP が実施する（本コントロールは回転量のみ通知する）。<br/>
    /// 選択矩形はドメイン座標で不変のため、本操作は選択状態を変更しない（ビューの操作）。
    /// </remarks>
    public event EventHandler<WheelScrollRequest>? ScrollRequested;

    /// <summary>Enter キーによる確定要求時に発火する（選択確定後のみ。ドラッグ中は発火しない）。</summary>
    public event EventHandler<SelectionRectEventArgs>? ConfirmRequested;

    /// <summary>Escape キーによるキャンセル要求時に発火する（選択の破棄は本コントロールが実施済み）。</summary>
    public event EventHandler? CancelRequested;

    /// <summary>描画完了（コントロールが図形を描き終えたこと）を通知するイベント。</summary>
    /// <remarks>
    /// 本コントロールは文字（ラベル・座標ラベル）を描かない。テキストの実寸測定は描画 API に依存するため、<br/>
    /// 「どの文言をどこへ描くか」はコアが決め、実際の配置は本コントロールが行う（<see cref="MarqueeLabelPlan"/>）。<br/>
    /// 契約は文字に限定せず**描画内容全体**（<see cref="MarqueeRenderedEventArgs.Content"/>）を渡すため、ホストは文字以外の上書きもできる。<br/>
    /// <b>描画中に発火する</b>ため、ハンドラ内で描画を完了させること（キャンバスを保持しない）。<br/>
    /// 描画のたびに発火する（文字の有無は <c>Content.HasText</c> で判定できる）。
    /// </remarks>
    public event EventHandler<MarqueeRenderedEventArgs>? Rendered;

    /// <summary>外観・対話パラメータをまとめた設定を取得または設定する。</summary>
    /// <value>設定（枠線・ハンドル・十字マーカー・座標ラベルの外観、最小選択サイズ・ヒットテスト半径・境界外選択の禁止）。既定は <see cref="MarqueeOptions.Default"/>。</value>
    /// <remarks>
    /// 設定を変更すると再描画され、対話パラメータ（最小サイズ・ヒットテスト半径・境界外選択の禁止）はファサードへ伝搬する。<br/>
    /// 一部の項目だけを変える場合は <c>MarqueeOptions.Default with { ShowHandles = false }</c> の形で指定する（指定しない項目は既定値を保つ）。<br/>
    /// 色はコアの設定に含めない（描画にだけ必要な設定のため <see cref="Paint"/> が単一所有する）。
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

    /// <summary>装飾（暗転マスク・穴あけ枠・ラベル）の設定を取得または設定する。</summary>
    /// <value>装飾の設定（暗転色・枠線・ラベル・ハイライト・内側の塗り）。既定は <see cref="MarqueeDecoOptions.Default"/>。</value>
    /// <remarks>
    /// 一部の項目だけを変える場合は <c>MarqueeDecoOptions.Default with { ... }</c> の形式で指定する（指定しない項目は既定値を保つ）。<br/>
    /// 色はコアの設定に含めない（描画にだけ必要な設定のため <see cref="Paint"/> が単一所有する）。
    /// </remarks>
    public MarqueeDecoOptions DecoOptions
    {
        get => GetValue(DecoOptionsProperty);
        set => SetValue(DecoOptionsProperty, value);
    }

    /// <summary>座標変換ポートへ適用する倍率を取得または設定する。</summary>
    /// <value>ドメイン → DIP の倍率。既定は 1.0。</value>
    /// <remarks>
    /// 環境値（DPI・マルチスクリーン）のため <see cref="Options"/> には含めない。<br/>
    /// <see cref="SetCoordinateTransform"/> が未設定の場合のみ使われる（ポート優先）。<br/>
    /// 選択矩形と十字マーカーの描画はファサードの座標変換ポートのみを使うため本値の影響を受けない<br/>
    /// （本値が影響するのは外側の装飾のサイズラベル・ラベル位置）。
    /// </remarks>
    public double DomainToDipScaling
    {
        get => GetValue(DomainToDipScalingProperty);
        set => SetValue(DomainToDipScalingProperty, value);
    }

    /// <summary>サイズラベル表示用のスケーリング係数を取得または設定する（マルチスクリーン補正）。</summary>
    /// <value>DisplayScale の百分率に使う係数。未設定（0 以下）の場合は <see cref="DomainToDipScaling"/> と同じ値が使用される。</value>
    /// <remarks>0 以下を <see cref="DomainToDipScaling"/> と同値として扱う規則はコア（<see cref="MarqueeFacade.DisplayScaleScaling"/>）が単一所有する。</remarks>
    public double DisplayScaleScaling
    {
        get => _marquee.DisplayScaleScaling;
        set
        {
            _marquee.DisplayScaleScaling = value;
            InvalidateFrame();
            InvalidateVisual();
        }
    }

    /// <summary>サイズラベルのテキストを提供する関数を取得または設定する。</summary>
    /// <value>サイズラベルのテキストを返す関数。null または空文字の場合は自動生成する。</value>
    /// <remarks>
    /// デリゲート（ホスト結線）のため <see cref="DecoOptions"/> には含めない（値の等価比較の対象外にするため）。<br/>
    /// 保持はコア（<see cref="MarqueeFacade.SizeLabelOverrideProvider"/>）が単一所有し、本 setter が再描画を要求する。<br/>
    /// <b>操作ラベル・情報ラベルは本コントロールの概念ではない</b>（アプリ固有の付加情報）ため、ホストが自前で描く。
    /// </remarks>
    public Func<string?>? SizeLabelOverrideProvider
    {
        get => _marquee.SizeLabelOverrideProvider;
        set
        {
            _marquee.SizeLabelOverrideProvider = value;
            InvalidateFrame();
            InvalidateVisual();
        }
    }

    /// <summary>サイズラベルの表示スケーリングを矩形ごとに解決するプロバイダを取得または設定する。</summary>
    /// <value>描画のたびに呼び出される関数（引数はドメイン座標の矩形）。null の場合は <see cref="DisplayScaleScaling"/> を使う。</value>
    /// <remarks>
    /// マルチモニタでモニタごとに表示スケールが異なる場合、ホストは矩形からモニタを判定してそのスケールを返す。<br/>
    /// 文言（書式）はコアが単一所有するため、ホストは数値だけを与える。<br/>
    /// 保持はコア（<see cref="MarqueeFacade.SizeLabelDisplayScalingProvider"/>）が単一所有し、本 setter が再描画を要求する。
    /// </remarks>
    public Func<EsRect, double>? SizeLabelDisplayScalingProvider
    {
        get => _marquee.SizeLabelDisplayScalingProvider;
        set
        {
            _marquee.SizeLabelDisplayScalingProvider = value;
            InvalidateFrame();
            InvalidateVisual();
        }
    }

    /// <summary>表示 DIP を物理ピクセルへ変換するスケーリング係数を取得または設定する。</summary>
    /// <value>スケーリング係数。DPI 変更時に自動更新される。</value>
    public double DipToDeviceScaling
    {
        get => GetValue(DipToDeviceScalingProperty);
        set => SetValue(DipToDeviceScalingProperty, value);
    }

    /// <summary>現在の選択状態のスナップショットを取得する（ポーリング不要の完全状態）。</summary>
    /// <value>State / SelectionRect / ActiveTarget / MousePosition / IsMouseDown / IsDragging / NudgeActive / HasSelection を含むスナップショット。</value>
    public MarqueeSnapshot CurrentSnapshot => _marquee.CreateSnapshot();

    /// <summary>選択矩形があるかどうかを取得する。</summary>
    /// <value>選択中の場合は true。</value>
    /// <remarks>描画のたびに参照されるため、スナップショットの生成を伴わない軽量なプロパティとする。</remarks>
    public bool HasSelection => _marquee.HasSelection;

    /// <summary>現在の選択矩形（ドメイン座標）を取得する。</summary>
    /// <value>選択矩形（ドメイン座標）。未選択時は 0x0 の空矩形。</value>
    /// <remarks>描画のたびに参照されるため、スナップショットの生成を伴わない軽量なプロパティとする。</remarks>
    public EsRect SelectionRect => _marquee.SelectionRect;

    /// <summary>描画の実装（描画内容を描画面へ写す戦略）を取得または設定する。</summary>
    /// <value>現在の描画実装。既定は <see cref="Paint"/> の色を保持する <see cref="MarqueeRendererMewUI"/>。</value>
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

    /// <summary>操作ログの出力先を取得または設定する。</summary>
    /// <value>ロガー。null を設定した場合は <see cref="NullLogger"/> へフォールバックする（出力なし）。</value>
    /// <remarks>
    /// 保持とフォールバック、ファサードへの伝搬はコア（<see cref="MarqueeFacade.Log"/>）が単一所有する（本層は委譲するだけ）。<br/>
    /// 入力の判断に伴うログはコアが同じロガーで出力する。
    /// </remarks>
    public ILogger Log
    {
        get => _marquee.Log;
        set => _marquee.Log = value;
    }

    /// <summary>ドメイン ↔ ローカル DIP の座標変換ポートを設定する。</summary>
    /// <param name="port">座標変換ポート。null の場合は等価変換（1 ドメインピクセル = 1 DIP・無制限）。</param>
    /// <remarks>
    /// ホスト（上位 AP）がモードに応じた変換ポートを設定する（範囲選択: 仮想スクリーンピクセル / 切出し: 元画像ピクセル）。<br/>
    /// 変換関数は呼び出し毎に評価されるため、ズーム・スクロール等の動的変化を反映できる。<br/>
    /// 保持と、ポート未指定時に倍率（<see cref="DomainToDipScaling"/>）を使う規則はコアが単一所有する（本層は委譲するだけ）。
    /// </remarks>
    public void SetCoordinateTransform(ICoordTransform? port) => _marquee.SetCoordinateTransform(port);

    /// <summary>選択矩形を外部状態から復元する。</summary>
    /// <param name="rect">選択矩形（ドメイン座標）。</param>
    /// <remarks>
    /// 状態の更新と通知はコア（ファサード）が担う（<see cref="MarqueeOptions.ClampToDomainBounds"/> が有効な場合は境界内へクリップしてから設定する）。<br/>
    /// 設定後は <see cref="SelectionChanged"/> が発火し、再描画される。
    /// </remarks>
    public void SetSelection(EsRect rect) => _marquee.SetSelection(rect);

    /// <summary>選択状態と nudge 状態を初期化する。</summary>
    /// <remarks>nudge 中だった場合はカーソルを再表示する（非表示残留の防止。状態の初期化はコアが担う）。</remarks>
    public void ResetSelection() => _marquee.ResetSelection();

    /// <summary>キャンセル要求を処理する（Escape キーと同じ条件）。</summary>
    /// <remarks>
    /// ドラッグ中 / 選択ありの場合は選択を破棄した後に <see cref="CancelRequested"/> を発火する。<br/>
    /// モード差分（キャンセル後の後始末）は上位が本イベントを購読して行う。
    /// </remarks>
    public void RequestCancel() => _marquee.RequestCancel();

    /// <summary>確定要求を処理する（Enter キーと同じ条件）。</summary>
    /// <remarks>
    /// ドラッグ中は無視し、選択確定後のみ <see cref="ConfirmRequested"/> を発火する。<br/>
    /// 確定後に選択状態をクリアする（確定後の再ドラッグ・再確定を防ぐ）。
    /// </remarks>
    public void RequestConfirm() => _marquee.RequestConfirm();

    /// <summary>ビューポートの変化を通知する（<see cref="ViewportChanged"/> を発火する）。</summary>
    /// <remarks>
    /// 本コントロールは <c>ScrollViewer</c> の自動連携を行わないため、ホスト（上位 AP）が<br/>
    /// ズーム・スクロール・画像サイズ変更の検知時に呼ぶ（暗転マスク等の再描画トリガー）。<br/>
    /// 「変化の再通知 + 再描画」の手順はコントロール化の層（<c>MarqueeViewportNotifier</c>）が単一所有する。
    /// </remarks>
    public void NotifyViewportChanged() => _viewport.Notify();

    /// <summary>要求サイズを返す（親の残余を埋める）。</summary>
    /// <param name="availableSize">親から提示された利用可能サイズ。</param>
    /// <returns>常に <c>Size.Empty</c>。</returns>
    /// <remarks>選択オーバーレイは下の画像と同じ領域を占めるため、自身の要求サイズを持たない。</remarks>
    protected override Size MeasureContent(Size availableSize)
    {
        // 子（図形のキャンバスとラベル要素）へ利用可能サイズを伝搬する
        _shapes.Measure(availableSize);

        foreach (var label in _labels)
        {
            label.Measure(availableSize);
        }

        return Size.Empty;
    }

    // ====================================================================
    // 1 フレーム分の描画内容（1 フレーム = 1 回だけ解決する）
    // ====================================================================

    /// <summary>直近に解決した描画内容（null = 未解決）。</summary>
    private MarqueeDrawContent? _frame;

    /// <summary>直近に解決した描画内容の基準サイズ（DIP）。</summary>
    private EsSize _frameSize;

    /// <summary>1 フレーム分の描画内容を解決して保持する（同じ基準サイズでは再解決しない）。</summary>
    /// <param name="clientSize">描画領域のサイズ（DIP。DIP 換算済みの描画先サイズ）。</param>
    /// <returns>そのフレームの描画内容。</returns>
    /// <remarks>
    /// 図形の描画（<c>OnPaintSurface</c>）とラベルの配置（<c>ArrangeContent</c>）が同じ描画内容を使う。<br/>
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
    /// 描画フェーズ（<c>OnPaintSurface</c>）は配置フェーズが解決した値をそのまま使う<br/>
    /// （配置と描画で基準サイズが丸めによりわずかに違っても、1 フレームの解決を 2 回にしない）。
    /// </remarks>
    private MarqueeDrawContent CurrentFrame(EsSize clientSize) => _frame ?? ResolveFrame(clientSize);

    /// <summary>保持している描画内容を破棄する（次の解決で最新の状態を反映する）。</summary>
    private void InvalidateFrame() => _frame = null;

    /// <summary>子（図形のキャンバス・ラベル要素）を配置する。</summary>
    /// <param name="bounds">本コントロールへ割り当てられた矩形（ウィンドウ絶対座標・DIP）。</param>
    /// <remarks>
    /// <b>位置はコアが決める</b>: ラベルの配置は <see cref="MarqueeLabelPlan"/>（描画層）が<br/>
    /// 実寸から求めた矩形をそのまま使い、本クラスは要素を <c>Arrange</c> するだけである。<br/>
    /// MewUI は <c>Margin</c> による絶対配置が反映されないため、配置フェーズで明示的に位置を確定させる。<br/>
    /// 子の座標はウィンドウ絶対座標のため、親の絶対座標（<paramref name="bounds"/> の左上）を加算する。
    /// </remarks>
    protected override void ArrangeContent(Rect bounds)
    {
        // 図形のキャンバスは親領域全体を埋める
        _shapes.Arrange(bounds);

        // 描画内容は本フェーズで 1 回だけ解決し、図形の描画（OnPaintSurface）と共有する
        SyncLabels(MarqueeLabelPlan.Resolve(ResolveFrame(bounds.Size.ToEsSize()), Paint, SkiaText.Measure), bounds);
    }

    /// <summary>ラベル要素（背景 + テキスト）を配置指示へ同期し、親の絶対座標を基準に配置する。</summary>
    /// <param name="placements">配置するラベル（描画順に並ぶ）。</param>
    /// <param name="bounds">本コントロールへ割り当てられた矩形（ウィンドウ絶対座標・DIP）。</param>
    /// <remarks>
    /// 余分な要素は親から外し、不足分は生成して子の末尾へ追加する（配置のたびに呼ばれる）。<br/>
    /// 子の座標はウィンドウ絶対座標のため、親の絶対座標（<paramref name="bounds"/> の左上）を加算する。
    /// </remarks>
    private void SyncLabels(IReadOnlyList<MarqueeLabelPlacement> placements, Rect bounds)
    {
        while (_labels.Count > placements.Count)
        {
            var last = _labels[^1];
            _labels.RemoveAt(_labels.Count - 1);
            Remove(last);
        }

        while (_labels.Count < placements.Count)
        {
            var border = new Border().Child(new TextBlock());
            _labels.Add(border);
            Add(border);
        }

        for (var i = 0; i < placements.Count; i++)
        {
            var placement = placements[i];
            var border = _labels[i];

            Apply(border, placement);
            border.Arrange(new Rect(
                bounds.X + placement.Bounds.X, bounds.Y + placement.Bounds.Y,
                placement.Bounds.Width, placement.Bounds.Height));
        }
    }

    /// <summary>1 つのラベル要素の内容と体裁を配置指示に合わせる。</summary>
    /// <param name="border">更新する要素（背景）。</param>
    /// <param name="placement">配置指示（寸法は実寸を反映済み）。</param>
    /// <remarks>本体は配置でサイズを確定するため、要素には内側余白だけを与える。</remarks>
    private static void Apply(Border border, in MarqueeLabelPlacement placement)
    {
        border.Padding(placement.PaddingX, placement.PaddingY);
        border.Background = placement.BackgroundColor.ToMewUIColor();

        if (border.Child is TextBlock text)
        {
            text.Text = placement.Text;
            text.FontSize = placement.FontSize;
            text.Foreground = placement.TextColor.ToMewUIColor();
        }
    }

    /// <summary>DPI の変更時に表示スケーリング係数を追従させる。</summary>
    /// <param name="oldDpi">変更前の DPI。</param>
    /// <param name="newDpi">変更後の DPI。</param>
    /// <remarks>モニター間の移動で DPI が変わっても、枠線・ハンドル・マーカーの画面上の大きさを保つ。</remarks>
    protected override void OnDpiChanged(uint oldDpi, uint newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        DipToDeviceScaling = DpiScaler.ToScaling(newDpi);
    }

    /// <summary>左ボタン押下時にドラッグを開始する。</summary>
    /// <param name="e">マウスの情報。</param>
    /// <remarks>
    /// 押下位置をドメイン座標へ変換してコア（ファサード）へ通知する（ダブルクリックの確定/キャンセル・nudge の終了・<br/>
    /// ハンドルのスナップはコアの判断に従う）。<br/>
    /// ドラッグ中はポインターがコントロール外へ出ても移動イベントを受け取れるようマウスキャプチャを取得する。
    /// </remarks>
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (!e.LeftButton)
        {
            return;
        }

        e.Handled = true;
        _marquee.Press(ToDomain(e.GetPosition(this)), e.ClickCount);
    }

    /// <summary>ドラッグ中のマウス移動を処理する。</summary>
    /// <param name="e">マウスの情報。</param>
    /// <remarks>
    /// 押下位置をドメイン座標へ変換してコア（ファサード）へ通知する。<br/>
    /// ドラッグの適用・nudge 中のエコー移動の抑制・カーソル形状の決定はコアの判断に従う。
    /// </remarks>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        _marquee.Move(ToDomain(e.GetPosition(this)));
    }

    /// <summary>左ボタン解放時にドラッグを終了する。</summary>
    /// <param name="e">マウスの情報。</param>
    /// <remarks>
    /// 押下位置をドメイン座標へ変換してコア（ファサード）へ通知し、マウスキャプチャを解放する。<br/>
    /// 最小サイズによるキャンセル・確定・クリック検出・nudge の終了はコアの判断に従う。
    /// </remarks>
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        e.Handled = true;
        _marquee.Release(ToDomain(e.GetPosition(this)));
    }

    /// <summary>ホイールでズーム要求・スクロール要求を行う。</summary>
    /// <param name="e">ホイールの情報。</param>
    /// <remarks>
    /// Ctrl+ホイールは <see cref="ZoomRequested"/>（ズームの実処理は上位 AP）、それ以外は <see cref="ScrollRequested"/><br/>
    /// （スクロールの実処理は上位 AP）を発火する。回転量はそのまま通知し、解釈（Ctrl の判定・連携先への適用・ログ）は<br/>
    /// コントロール化の層（<c>Feature.Marquee.Presenter</c> の <see cref="MarqueeWheelInterpreter"/>）が単一所有する。<br/>
    /// ホイールはビューの操作であり選択矩形の状態を変えない。
    /// </remarks>
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);

        _wheel.Handle(e.ControlKey, e.ShiftKey, e.Delta.X, e.Delta.Y, e.GetPosition(this).ToEsPoint());
        e.Handled = true;
    }

    /// <summary>キーダウン処理（矢印キーの nudge・Escape のキャンセル・Enter の確定）。</summary>
    /// <param name="e">キーイベント。</param>
    /// <remarks>
    /// キーをコア型（<see cref="EsKey"/>）へ変換して通知し、返された効果（再描画・カーソル操作）を適用するだけである。<br/>
    /// 矢印キー nudge・Escape・Enter の判断（nudge の許可条件を含む）はコアが行う。<br/>
    /// <c>|| e.Handled</c> は、先に誰かが処理済みにしたフラグを false へ戻さないため。<br/>
    /// 本コントロールにフォーカスがある場合のみ呼ばれる（Avalonia 版のフォーカス非依存 nudge とは異なる）。
    /// </remarks>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        e.Handled = _marquee.KeyDown(e.Key.ToEsKey(), CurrentCursorDomainPosition()) || e.Handled;
    }

    /// <summary>現在のカーソル位置（ドメイン座標）を取得する。</summary>
    /// <returns>カーソル位置（ドメイン座標）。ウィンドウを特定できない場合は null。</returns>
    /// <remarks>
    /// nudge の基点精度を上げるために記録する（マウス移動が未発生でも現在位置から微調整を開始でき、原点へ飛ばない）。<br/>
    /// 画面物理座標 → ウィンドウのクライアント座標 → ドメイン座標の順に変換する（境界の 1 行変換）。
    /// </remarks>
    private EsPoint? CurrentCursorDomainPosition()
    {
        if (FindVisualRoot() is not Window window)
        {
            return null;
        }

        var screen = _cursor.GetCursorScreenPoint();
        var local = window.ScreenToClient(screen.ToMewUIPoint());
        return _marquee.ToDomain(local.ToEsPoint());
    }

    /// <summary>nudge を終了し、マウスポインターを再表示する。</summary>
    /// <remarks>フォーカス喪失時に呼び出し、nudge 中の非表示ポインターを復元する。</remarks>
    private void EndNudge() => _marquee.EndNudge();

    /// <summary>ローカル DIP 座標をドメイン座標へ変換する。</summary>
    /// <param name="localDip">ローカル DIP 座標。</param>
    /// <returns>ドメイン座標。</returns>
    private EsPoint ToDomain(Point localDip) => _marquee.ToDomain(localDip.ToEsPoint());

    /// <summary>コア（ファサード）が要求する効果を本コントロールへ適用する実装。</summary>
    /// <remarks>
    /// 効果の適用順序（フォーカス → カーソル形状 → 再描画）はコアが単一所有するため、<br/>
    /// 本実装は呼ばれた順に自 UI の操作へ接続するだけでよい（判断を持たない）。<br/>
    /// <see cref="MarqueeEffectsBase"/> を継承し、本 UI が必要とする効果だけを override する。<br/>
    /// コアは <see cref= "IMarqueeEffects"/> のみに依存し、UI フレームワークを知らない。
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

        /// <summary>ポインターキャプチャを取得または解放する。</summary>
        /// <param name="capture">true = 取得 / false = 解放。</param>
        /// <remarks>ドラッグ中にポインターがコントロール外へ出ても移動イベントを受け取れるようにする。</remarks>
        public override void SetPointerCapture(bool capture) => owner.CaptureMouse(capture);

        /// <summary>ドメイン座標を画面物理座標へ変換する。</summary>
        /// <param name="domainPoint">ドメイン座標。</param>
        /// <returns>画面物理座標。ウィンドウを特定できない場合は既定値。</returns>
        public override EsPointInt ToScreenPoint(EsPoint domainPoint)
            => owner.ToScreen(domainPoint);
    }

    /// <summary>ドメイン座標を画面物理座標へ変換する。</summary>
    /// <param name="domainPos">ドメイン座標。</param>
    /// <returns>画面物理座標。ウィンドウを特定できない場合は default。</returns>
    /// <remarks>カーソルの移動（nudge）に使用する。変換できない場合は原点へカーソルが飛ぶため、呼び出し側は nudge 中のウィンドウ表示を前提とする。</remarks>
    private EsPointInt ToScreen(EsPoint domainPos)
    {
        if (FindVisualRoot() is not Window window)
        {
            return default;
        }

        var localDip = _marquee.ToDip(domainPos);
        var screen = window.ClientToScreen(localDip.ToMewUIPoint());
        return screen.ToEsPointInt();
    }

    /// <summary>マウスキャプチャを取得または解放する。</summary>
    /// <param name="capture">true = 取得 / false = 解放。</param>
    /// <remarks>ドラッグ中にポインタがコントロール外へ出ても移動イベントを受け取れるようにする。</remarks>
    private void CaptureMouse(bool capture)
    {
        if (FindVisualRoot() is not Window window)
        {
            return;
        }

        if (capture)
        {
            window.CaptureMouse(this);
        }
        else
        {
            window.ReleaseMouseCapture();
        }
    }

    /// <summary>コアからの通知を公開イベントへ転送する（判断はしない）。</summary>
    /// <param name="sender">送信元（ファサード）。</param>
    /// <param name="notice">通知の種別・座標・スナップショット。</param>
    /// <remarks>
    /// 転送のみを担う（確定/キャンセルの判断・状態の更新はコアが済ませている）。<br/>
    /// 本 UI は押下・解放の公開イベントを持たない（基底 <c>UIElement</c> の同名メンバーがサービスを提供済みのため）。
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
    /// <returns>通知座標（ドメイン座標）とスナップショットを含むイベント引数。</returns>
    private static MarqueePointEventArgs ToPointArgs(MarqueeNotice notice) => new(notice.Position, notice.Snapshot);

    /// <summary>通知を選択矩形のイベント引数へ変換する。</summary>
    /// <param name="notice">通知。</param>
    /// <returns>選択矩形（ドメイン座標）とスナップショットを含むイベント引数。</returns>
    private static SelectionRectEventArgs ToRectArgs(MarqueeNotice notice) => ToRectArgs(notice.Snapshot);

    /// <summary>スナップショットを選択矩形のイベント引数へ変換する（コアの通知を経由しない通知用）。</summary>
    /// <param name="snapshot">通知時点の状態スナップショット。</param>
    /// <returns>選択矩形とスナップショットを含むイベント引数。</returns>
    private static SelectionRectEventArgs ToRectArgs(MarqueeSnapshot snapshot)
        => new(snapshot.SelectionRect, snapshot);

    /// <summary>カーソル形状をコアが示す種別に応じて更新する。</summary>
    /// <param name="kind">カーソル形状の種別（コア提供）。</param>
    /// <remarks>種別と自 UI のカーソルの対応のみを持つ（どのハンドルが同じカーソルになるかはコアが判断済み）。</remarks>
    private void ApplyCursorShape(MarqueeCursorKind kind)
    {
        Cursor = kind switch
        {
            MarqueeCursorKind.DiagonalNWSE => CursorType.SizeNWSE,
            MarqueeCursorKind.DiagonalNESW => CursorType.SizeNESW,
            MarqueeCursorKind.Vertical => CursorType.SizeNS,
            MarqueeCursorKind.Horizontal => CursorType.SizeWE,
            MarqueeCursorKind.Move => CursorType.SizeAll,
            _ => null,
        };
    }

    /// <summary>選択矩形の外側に描く装飾・ハンドル・十字マーカーを描画し、付加情報の描画機会をホストへ通知する。</summary>
    /// <param name="canvas">描画先のキャンバス（物理ピクセル）。</param>
    /// <param name="info">描画先の情報（幅・高さ）。</param>
    /// <remarks>
    /// <b>描画の手順は UI 非依存のレンダラが単一所有する</b>: 描画内容（装飾・選択矩形・ラベル）はコアが 1 値で解決し、<br/>
    /// 層の順序と描画 API への写し方は <see cref="MarqueeRenderer"/>（<c>Feature.Marquee.Presenter</c>）が担う。<br/>
    /// 本メソッドは自 UI の描画面をレンダラへ渡すだけである（<b>ラベルは本パネルの子要素が描く</b>ため図形の層のみ）。<br/>
    /// 選択枠線は装飾の穴あけ枠（同じ矩形の内側）と重なる（別々に描くと線幅が二重になる）。<br/>
    /// キャンバスを DPI スケーリングしてから DIP 座標で描画する（コアの計算と座標系を一致させる）。<br/>
    /// 背景は塗らない（透明のままにして下の画像を透かす）。
    /// </remarks>
    private void OnPaintSurface(SKCanvas canvas, SKImageInfo info)
    {
        var scaling = DpiScaler.NormalizeScaling(DipToDeviceScaling);

        canvas.Save();
        canvas.Scale((float)scaling);
        try
        {
            // 前フレームの描画（選択矩形・暗転マスク）を残さないため、自分の描画面を消してから描く
            canvas.Clear(SKColors.Transparent);

            // 描画内容は配置フェーズ（ArrangeContent）が解決した値を共有する（未解決の場合のみ本メソッドが解決する）
            var content = CurrentFrame(DpiScaler.ToDip(info.Size.ToEsSize(), scaling));

            // 図形の層を描く（描画面の確保は本 UI の描画実装、層の順序は UIBase の基底が単一所有する）
            if (_renderer is MarqueeRendererMewUI uiRenderer)
            {
                uiRenderer.Draw(canvas, in content);
            }
            else
            {
                // 差し替えられた描画実装は描画ポート（自 UI の描画面を適合させたもの）経由で描く
                _renderer.DrawShapes(new SkiaEsCanvas(canvas), in content);
            }

            // 描画完了をホストへ通知する（操作・情報ラベルなどホスト固有の付加情報を描くための入口）。
            Rendered?.Invoke(this, new MarqueeRenderedEventArgs(canvas, content));
        }
        finally
        {
            canvas.Restore();
        }
    }
}
