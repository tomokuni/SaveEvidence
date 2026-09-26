using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Feature.MarqueeCore.Decoration;
using Microsoft.Extensions.Logging;
using EsUtil.Feature.MarqueeCore.Contract;
using EsUtil.Feature.MarqueeCore.Drawing;
using EsUtil.Feature.MarqueeCore.Interaction;
using EsUtil.Feature.MarqueeCore.Setting;
using static EsUtil.Feature.MarqueeCore.Marquee.Limits;


namespace EsUtil.Feature.MarqueeCore;

/// <summary>Marquee（ドラッグ矩形の選択）の操作を UI 層・ホストへ提供するファサード。</summary>
/// <remarks>
/// <b>適用パターン</b>: Facade（コアの状態機械・描画内容の解決・設定を 1 つの窓口へ集約する）。<br/>
/// <br/>
/// <b>責務</b>: Marquee の「操作（入力の通知・外部からの状態操作・描画内容の解決・設定）」を単一の窓口で提供し、<br/>
/// UI 層とホストは本クラスだけを参照する（コアの内部契約である状態機械 <see cref="MarqueeSession"/> へ直接触れない）。<br/>
/// <br/>
/// <b>本クラスが単一所有するもの</b>:<br/>
/// - セッション（入力の解釈と状態遷移の判断）の生成と保持<br/>
/// - 環境値の保持（座標変換ポート・倍率）と、ポート未指定時に倍率を使う規則の適用<br/>
/// - 効果（フォーカス・カーソル操作・カーソル形状・ポインターキャプチャ・再描画）の適用<br/>
/// - 1 フレーム分の描画内容の解決（描くかどうかの判断）<br/>
/// <br/>
/// <b>本クラスが持つもの</b>: 選択の判断・状態・描画内容（選択矩形・装飾・ラベル）と描画順のデータ、<br/>
/// 装飾の設定の保持、ラベルの文言プロバイダの保持。<br/>
/// <br/>
/// <b>描画は本コアの関心事ではない</b>: 本コアは描画 API も描画面も知らない（<see cref="MarqueeDrawContent"/> と<br/>
/// その <see cref="MarqueeDrawContent.Layers"/> というデータを返すだけである）。描画は UI 層が行い、<br/>
/// 層の順序に従った描画手順は UI 側の共有層（<c>MarqueeControl.UIBase</c> の <c>MarqueeRendererBase</c>）が単一所有する。<br/>
/// <br/>
/// <b>UI 層の責務</b>: 入力イベントの受付と座標変換（<see cref="ToDomain"/> / <see cref="ToDip"/>）、<br/>
/// <see cref="IMarqueeEffects"/> の実装、通知（<see cref="Notified"/>）の公開イベントへの転送、<br/>
/// 返された描画内容の反映（図形は描画実装へ委譲、文字はテキスト要素の配置）。<b>描画順はコアの指示に従う</b>。<br/>
/// <br/>
/// <b>依存分離</b>: UI 非依存のコア型（<see cref="EsPoint"/> / <see cref="EsRect"/> / <see cref="EsKey"/>）と<br/>
/// 抽象（<see cref="ICursorService"/> / <see cref="ICoordTransform"/> / <see cref="IMarqueeEffects"/>）にのみ依存する。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で検証できる。
/// </remarks>
public sealed partial class Marquee
{
    /// <summary>入力の解釈と状態遷移の判断を担うセッション（コア内部の状態機械）。</summary>
    private readonly MarqueeSession _session;

    /// <summary>効果（フォーカス・再描画・カーソル操作）の適用先（UI 層が実装する）。</summary>
    private readonly IMarqueeEffects _effects;

    /// <summary>カーソルサービス（nudge のカーソル移動・表示切替）。null の場合はカーソル操作を行わない。</summary>
    private readonly ICursorService? _cursor;

    /// <summary>外部から指定された座標変換ポート（null = 未指定。未指定時は <see cref="DomainToDipScaling"/> を倍率として使う）。</summary>
    /// <remarks>
    /// セッションは null を等価変換に置換して保持するため、<b>指定の有無（null かどうか）は本フィールドだけが保持する</b>。<br/>
    /// この区別が必要なのは、ポート未指定時に倍率（<see cref="DomainToDipScaling"/>）を使う規則をコアが単一所有するためである。
    /// </remarks>
    private ICoordTransform? _coordinatePort;

    /// <summary>現在の装飾の設定（暗転マスク・穴あけ枠・枠線・内側の塗り）。</summary>
    private MarqueeDecoOptions _decoOptions = MarqueeDecoOptions.Default;

    /// <summary>サイズラベルの自動生成に使うスケーリング係数（0 以下は <see cref="DomainToDipScaling"/> と同値）。</summary>
    private double _displayScaleScaling;

    /// <summary>ポート未指定時に適用する倍率（0 以下・等倍は等価変換）。</summary>
    private double _scaling = DomainToDipScalingDefault;

    /// <summary>サイズラベルの文言プロバイダ（null = 未指定。未指定時はコアが自動生成する）。</summary>
    private Func<string?>? _sizeLabelOverrideProvider;

    /// <summary>サイズラベルの表示スケーリングを矩形ごとに解決するプロバイダ（null = 未指定。未指定時は表示スケーリングを使う）。</summary>
    private Func<EsRect, double>? _sizeLabelDisplayScalingProvider;

    /// <summary>ファサードを初期化する。</summary>
    /// <param name="effects">効果（フォーカス・再描画・カーソル形状・キャプチャ・座標変換）の適用先。null 不可。</param>
    /// <param name="cursor">カーソルサービス。null 可（null の場合はカーソル操作を行わない）。</param>
    /// <param name="options">初期設定。null 可（null の場合は <see cref="MarqueeOptions.Default"/>）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="effects"/> が null の場合。</exception>
    public Marquee(
        IMarqueeEffects effects,
        ICursorService? cursor = null,
        MarqueeOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(effects);

        _effects = effects;
        _cursor = cursor;
        _session = new MarqueeSession(options ?? MarqueeOptions.Default);
        _session.Notified += OnSessionNotified;
    }

    /// <summary>上位（UI 層）へ伝える通知。UI 層は自 UI の公開イベントへ転送する。</summary>
    /// <remarks>通知の種別と公開イベントの対応は UI 層が持ち、判断（確定・キャンセル等）は本クラスが済ませている。</remarks>
    public event EventHandler<MarqueeNotice>? Notified;

    /// <summary>ロガー。null を設定した場合は出力なし。</summary>
    /// <value>注入されたロガー。null 可（null の場合は <see cref="Microsoft.Extensions.Logging.Abstractions.NullLogger"/> へフォールバックする）。</value>
    /// <remarks>保持・フォールバック・各判断のログ出力はセッション（コア）が単一所有する（本クラスは委譲するだけ）。</remarks>
    public ILogger Log
    {
        get => _session.Log;
        set => _session.Log = value;
    }

    /// <summary>現在の設定（外観・対話パラメータ）。</summary>
    /// <value>反映済みの設定。既定は <see cref="MarqueeOptions.Default"/>。</value>
    /// <remarks>
    /// 設定すると対話パラメータをセッションへ伝搬し（境界外選択の禁止の即時クリップを含む）、再描画を要求する。<br/>
    /// 同値の設定を再適用しても効果は発生しない（判断はコアが単一所有する）。
    /// </remarks>
    public MarqueeOptions Options
    {
        get => _session.Options;
        set => Apply(_session.ApplyOptions(value));
    }

    /// <summary>ポート未指定時に適用する倍率。</summary>
    /// <value>ドメイン → DIP の倍率。既定は 1.0。設定時は 0.1〜8.0 へ補正される（0 以下・等倍は等価変換として扱う）。</value>
    /// <remarks>
    /// 環境値（DPI・マルチスクリーン）のため <see cref="Options"/> には含めない。<br/>
    /// 座標変換ポート（<see cref="SetCoordinateTransform"/>）が設定されている場合は使われない（ポート優先。規則はコアが単一所有する）。<br/>
    /// 選択矩形と十字マーカーの描画はセッションのポートのみを使うため本値の影響を受けない<br/>
    /// （本値が影響するのはサイズラベル・座標ラベルの位置と寸法）。
    /// </remarks>
    public double DomainToDipScaling
    {
        get => _scaling;
        set => SetIfChanged(ref _scaling, CoerceDomainToDipScaling(value), out _);
    }

    /// <summary>装飾（暗転マスク・穴あけ枠・枠線・内側の塗り）の設定。</summary>
    /// <value>装飾の設定。既定は <see cref="MarqueeDecoOptions.Default"/>。</value>
    /// <remarks>
    /// 装飾は選択矩形コントロール単体の概念ではない（組み込み先の表示）ため、選択の設定（<see cref="Options"/>）とは分けている。<br/>
    /// 描画の更新は UI が観測して行う（本コアは再描画を要求しない）。
    /// </remarks>
    public MarqueeDecoOptions DecoOptions
    {
        get => _decoOptions;
        set => SetIfChanged(ref _decoOptions, value, out _);
    }

    /// <summary>サイズラベルの文言を描画時に評価するプロバイダ。</summary>
    /// <value>描画のたびに呼び出される関数。null または空文字を返す場合はコアが自動生成する。</value>
    /// <remarks>
    /// デリゲート（ホスト結線）のため <see cref="Options"/> には含めない（値の等価比較の対象外にするため）。<br/>
    /// ホスト固有の文言（元画像座標・表示倍率等）を与えるための入口である。
    /// </remarks>
    public Func<string?>? SizeLabelOverrideProvider
    {
        get => _sizeLabelOverrideProvider;
        set => SetIfChanged(ref _sizeLabelOverrideProvider, value, out _);
    }

    /// <summary>サイズラベルの表示スケーリングを矩形ごとに解決するプロバイダ。</summary>
    /// <value>描画のたびに呼び出される関数（引数はドメイン座標の矩形）。null の場合は <see cref="DisplayScaleScaling"/> を使う。</value>
    /// <remarks>
    /// デリゲート（ホスト結線）のため <see cref="Options"/> には含めない（値の等価比較の対象外にするため）。<br/>
    /// マルチモニタでモニタごとに表示スケールが異なる場合、ホストは矩形からモニタを判定してそのスケールを返す<br/>
    /// （コアはモニタを知らないため、この判定はホストが担う）。<br/>
    /// <b>文言はコアが作る</b>: ホストは数値（スケール）だけを与え、書式はコアが単一所有する。
    /// </remarks>
    public Func<EsRect, double>? SizeLabelDisplayScalingProvider
    {
        get => _sizeLabelDisplayScalingProvider;
        set => SetIfChanged(ref _sizeLabelDisplayScalingProvider, value, out _);
    }

    /// <summary>サイズラベルの自動生成に使うスケーリング係数（マルチスクリーン補正）。</summary>
    /// <value>DisplayScale の百分率に使う係数。未設定（0 以下）の場合は <see cref="DomainToDipScaling"/> と同じ値。</value>
    /// <remarks>0 以下を倍率と同値として扱う規則は <see cref="ResolveDisplayScale"/> が単一所有する。</remarks>
    public double DisplayScaleScaling
    {
        get => ResolveDisplayScale(_displayScaleScaling, _scaling);
        set => SetIfChanged(ref _displayScaleScaling, value, out _);
    }

    /// <summary>選択矩形があるかどうか。</summary>
    /// <value>選択中の場合は true。</value>
    public bool HasSelection => _session.HasSelection;

    /// <summary>現在の選択矩形（ドメイン座標）。</summary>
    /// <value>選択矩形。未選択時は 0x0 の空矩形。</value>
    public EsRect SelectionRect => _session.SelectionRect;

    /// <summary>現在の選択状態のスナップショット（ポーリング不要の完全状態）を生成する。</summary>
    /// <param name="mousePosition">スナップショットに含めるマウス位置（ドメイン座標）。null の場合は直近の位置。</param>
    /// <returns>状態・選択矩形・アクティブハンドル・マウス位置・操作フラグを含むスナップショット。</returns>
    public MarqueeSnapshot CreateSnapshot(EsPoint? mousePosition = null) => _session.CreateSnapshot(mousePosition);

    // ====================================================================
    // 環境値（座標変換ポート・座標変換）
    // ====================================================================

    /// <summary>ドメイン ↔ ローカル DIP の座標変換ポートを設定する。</summary>
    /// <param name="port">座標変換ポート。null の場合は等価変換（1 ドメインピクセル = 1 DIP・無制限）。</param>
    /// <remarks>
    /// ホストがモードに応じた変換ポートを設定する（範囲選択: 仮想スクリーンピクセル / 切出し: 元画像ピクセル）。<br/>
    /// 変換関数は呼び出し毎に評価されるため、ズーム・スクロール等の動的変化を反映できる。<br/>
    /// 設定すると描画（選択矩形・ハンドル・十字線・外側の装飾）へ即時反映するため再描画を要求する。
    /// </remarks>
    public void SetCoordinateTransform(ICoordTransform? port)
    {
        _coordinatePort = port;
        _session.SetPort(port);
        _effects.RequestRedraw();
    }

    /// <summary>ローカル DIP 座標をドメイン座標へ変換する。</summary>
    /// <param name="localDip">ローカル DIP 座標。</param>
    /// <returns>ドメイン座標。</returns>
    public EsPoint ToDomain(EsPoint localDip) => _session.ToDomain(localDip);

    /// <summary>ドメイン座標をローカル DIP 座標へ変換する。</summary>
    /// <param name="domain">ドメイン座標。</param>
    /// <returns>ローカル DIP 座標。</returns>
    public EsPoint ToDip(EsPoint domain) => _session.ToDip(domain);

    // ====================================================================
    // 入力の通知（UI 層 → コア）
    // ====================================================================

    /// <summary>ポインター押下（左ボタン）を通知する。</summary>
    /// <param name="position">押下位置（ドメイン座標）。</param>
    /// <param name="clickCount">クリック数（2 以上 = ダブルクリック）。</param>
    /// <remarks>
    /// 入力の解釈（nudge の終了・ダブルクリックの確定/キャンセル・ハンドルのヒットテストとスナップ・新規選択の開始）は<br/>
    /// セッションが単一所有し、返された効果を本クラスが <see cref="IMarqueeEffects"/> 経由で UI へ適用する。
    /// </remarks>
    public void Press(EsPoint position, int clickCount) => Apply(_session.Press(position, clickCount));

    /// <summary>ポインター移動を通知する。</summary>
    /// <param name="position">マウス位置（ドメイン座標）。</param>
    /// <remarks>ドラッグの適用・nudge 中のエコー移動の抑制・カーソル形状の決定はセッションが単一所有する。</remarks>
    public void Move(EsPoint position) => Apply(_session.Move(position));

    /// <summary>ポインター解放（左ボタン）を通知する。</summary>
    /// <param name="position">解放位置（ドメイン座標）。</param>
    /// <remarks>
    /// 最小サイズによるキャンセル・確定・クリックの検出・nudge の終了はセッションが単一所有する。<br/>
    /// ドラッグの終了に伴うポインターキャプチャの解放もセッションが要求する（取得と解放が同じ経路になる）。
    /// </remarks>
    public void Release(EsPoint position) => Apply(_session.Release(position));

    /// <summary>キー入力を通知する（Escape のキャンセル・Enter の確定・矢印キーの微調整）。</summary>
    /// <param name="key">押下されたキー。</param>
    /// <param name="cursorPosition">押下時点のカーソル位置（ドメイン座標）。取得できない場合は null。</param>
    /// <returns>入力が処理された場合は true（UI 層は自 UI の「処理済み」フラグへ設定する）。</returns>
    /// <remarks>
    /// キーの解釈（nudge の許可条件を含む）はセッションが単一所有し、返された効果を本クラスが UI へ適用する。<br/>
    /// 確定・キャンセルは常に処理済みとして扱われる（UI 層へキーを流さない）。
    /// </remarks>
    public bool KeyDown(EsKey key, EsPoint? cursorPosition)
    {
        var feedback = _session.KeyDown(key, cursorPosition);
        Apply(feedback);
        return feedback.Handled;
    }

    /// <summary>nudge を終了する（カーソルを再表示する）。</summary>
    /// <remarks>nudge 中の状態破棄（フォーカス喪失）で使用する。</remarks>
    public void EndNudge() => Apply(_session.EndNudge());

    // ====================================================================
    // 外部（ホスト）からの操作
    // ====================================================================

    /// <summary>選択矩形を外部状態から復元する。</summary>
    /// <param name="rect">選択矩形（ドメイン座標）。</param>
    /// <remarks>
    /// 境界外選択の禁止が有効な場合は、はみ出す矩形を境界内へクリップしてから設定する（判断はセッションが単一所有する）。<br/>
    /// 設定後は選択変更が通知され、再描画される。
    /// </remarks>
    public void SetSelection(EsRect rect) => Apply(_session.SetSelection(rect));

    /// <summary>選択状態と nudge 状態を初期化する（再描画される）。</summary>
    /// <remarks>
    /// nudge 中だった場合はカーソルを再表示する（非表示の残留を防ぐ。判断はセッションが単一所有する）。<br/>
    /// 上位（UI 層）が対話を終了する場合の後片付けにも本メソッドを使う（「終了」自体は UI のライフサイクルであり、<br/>
    /// コアが必要とするのは「対話状態の初期化」だけである）。
    /// </remarks>
    public void ResetSelection() => Apply(_session.Reset());

    /// <summary>確定要求を処理する（Enter キー・ダブルクリック・ホストからの API 呼び出しと同じ経路）。</summary>
    /// <remarks>ドラッグ中は無視し、選択確定後のみ確定要求を通知する（確定後は選択状態をクリアする）。</remarks>
    public void RequestConfirm() => Apply(_session.RequestConfirm());

    /// <summary>キャンセル要求を処理する（Escape キー・枠外ダブルクリック・ホストからの API 呼び出しと同じ経路）。</summary>
    /// <remarks>ドラッグ中または選択ありの場合は選択を破棄した後にキャンセル要求を通知する。</remarks>
    public void RequestCancel() => Apply(_session.RequestCancel());

    // ====================================================================
    // 描画内容（1 フレーム分）
    // ====================================================================

    /// <summary>現在の状態から描画 1 フレーム分の入力を生成する。</summary>
    /// <param name="clientSize">描画領域のサイズ（DIP。UI 実装の描画先のサイズ）。</param>
    /// <returns>描画入力（設定・状態と、描画時に決まる値を解決済み）。</returns>
    /// <remarks>
    /// 設定・状態の読み出し（選択矩形・マーカー位置・マウス位置・文言プロバイダ）は本メソッドが単一所有するため、<br/>
    /// UI 層は描画領域のサイズを渡すだけでよい（内部状態の組み立て順を持たない）。<br/>
    /// 図形の描画とテキストの配置が同じ入力を使うため、UI 層は本メソッドの結果を <see cref="ResolveContent"/> へ渡す。
    /// </remarks>
    public MarqueeRenderRequest CreateRequest(EsSize clientSize)
        => new(
            _session.Options,
            _decoOptions,
            clientSize,
            _session.HasSelection ? _session.SelectionRect : null,
            _session.NudgeActive,
            _session.MarkerPosition,
            _session.LastMousePosition,
            _coordinatePort,
            _scaling,
            SizeLabelOverrideProvider,
            DisplayScaleScaling,
            SizeLabelDisplayScalingProvider);

    /// <summary>描画入力から 1 フレーム分の描画内容（選択矩形・装飾・ラベルと描画順）を解決する。</summary>
    /// <param name="request">描画入力（設定・状態と描画時に決まる値。<see cref="CreateRequest"/> の結果を渡す）。</param>
    /// <returns>描画内容（描くものが無い項目は空・null。層の順序は <see cref="MarqueeDrawContent.Layers"/> が持つ）。</returns>
    /// <remarks>
    /// 判断（描くかどうか・位置・寸法・文言）はすべて本メソッドが単一所有するため、<br/>
    /// UI 層は <see cref="MarqueeDrawContent.Layers"/> の順に各層を反映するだけでよい。<br/>
    /// ラベル（サイズ・カーソル座標）は必ず最後の層であり、常に最上位に表示される。<br/>
    /// テキストの実寸は描画 API 依存のため、コアは「どこへ何を描くか」だけを返し、実測と要素の配置は UI 層が行う。<br/>
    /// <br/>
    /// 処理フロー:<br/>
    /// 1. 装飾（暗転マスク・穴あけ枠・内側の塗り）を解決する。<br/>
    /// 2. 選択矩形そのもの（ハンドル・始点マーカー）を解決する。<br/>
    /// 3. テキスト（サイズ・カーソル座標の文言と基準点）を解決する。<br/>
    /// 4. 描く層だけを順序どおりに並べる（テキストは最後 = 最上位）。<br/>
    /// <br/>
    /// 描画順は <see cref="MarqueeDrawLayer"/> の宣言順ではなく<b>本メソッドの意図</b>で決める<br/>
    /// （列挙に層を追加しても描画順が勝手に変わらないようにするため、明示的に組み立てる）。
    /// </remarks>
    public MarqueeDrawContent ResolveContent(in MarqueeRenderRequest request)
    {
        // 座標変換ポートは 1 フレームで 1 回だけ解決する（未指定時は倍率から等倍スケールを生成するため、各項目で解決すると生成と評価が重複する）
        var port = request.ResolvedPort;

        // 穴あけ矩形も 1 回だけ解決する（外側の装飾・内側の装飾・サイズラベルが同じ矩形を使う）
        var hole = MarqueeRenderRequest.ResolveHoleRectDip(
            request.DecoOptions.OuterHighlightRect, request.SelectionRect, port);

        var handles = ResolveHandles(request, port);
        var marker = ResolveMarker(request, port);
        var sizeLabel = ResolveSizeLabelText(request, hole, port);
        var coordLabel = ResolveCoordLabelText(request, port);

        return new MarqueeDrawContent(
            ResolveOuterDecoration(request, hole),
            ResolveInnerDecoration(request, hole),
            hole,
            handles,
            marker,
            sizeLabel,
            coordLabel,
            ResolveLayers(handles, marker, sizeLabel, coordLabel));
    }

    // ====================================================================
    // 内部ヘルパー
    // ====================================================================

    /// <summary>コアが返す効果を UI 層へ適用する。</summary>
    /// <param name="feedback">適用する効果（カーソル操作・フォーカス・カーソル形状・再描画・キャプチャ）。</param>
    /// <remarks>
    /// 適用の順序（カーソル操作 → フォーカス → カーソル形状 → 再描画）はコア（<see cref="MarqueeFeedback"/>）が単一所有する。<br/>
    /// カーソルサービス未指定（null）の場合はカーソル操作を適用しない（UI 初期化なしの検証を可能にする）。<br/>
    /// 効果はデリゲートではなく <see cref="IMarqueeEffects"/> へ直接適用する（入力イベントのたびにデリゲートを生成しない）。
    /// </remarks>
    private void Apply(in MarqueeFeedback feedback)
    {
        if (_cursor is { } cursor)
        {
            feedback.ApplyCursor(cursor, _effects);
        }

        feedback.ApplyVisual(_effects);

        if (feedback.Capture != MarqueeCaptureAction.None)
        {
            _effects.SetPointerCapture(feedback.Capture == MarqueeCaptureAction.Capture);
        }
    }

    /// <summary>セッションの通知を本クラスの通知として転送する。</summary>
    /// <param name="sender">送信元（セッション）。</param>
    /// <param name="notice">通知（種別・座標・スナップショット）。</param>
    private void OnSessionNotified(object? sender, MarqueeNotice notice) => Notified?.Invoke(this, notice);

    /// <summary>新しい値を保持する（同値なら何もしない）。</summary>
    /// <typeparam name="T">値の型。</typeparam>
    /// <param name="field">保持先のフィールド。</param>
    /// <param name="value">設定する値。</param>
    /// <param name="previous">変更前の値（同値の場合は現在値）。</param>
    /// <returns>値を変更した場合は true（同値だった場合は false）。</returns>
    /// <remarks>
    /// 「同値なら何もしない」という規則を単一所有する<br/>
    /// （各プロパティが同じ数行を書くと、片方だけ同値判定が抜けて無駄な再評価が溢れる）。<br/>
    /// 変更前の値も返すため、変更を検出した側（設定の変更ログ）が前後を比較できる。<br/>
    /// <b>再描画は要求しない</b>: 設定は UI の公開プロパティ（変更通知を持つ）から渡されるため、<br/>
    /// 描画の更新は UI が観測して行う（コアは入力に伴う再描画だけを効果として要求する）。
    /// </remarks>
    private bool SetIfChanged<T>(ref T field, T value, out T previous)
    {
        previous = field;
        if (EqualityComparer<T>.Default.Equals(previous, value))
        {
            return false;
        }

        field = value;
        return true;
    }
}
