using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Feature.MarqueeCore.Decoration;
using Microsoft.Extensions.Logging;

namespace EsUtil.Feature.MarqueeCore;

/// <summary>Marquee（ドラッグ矩形の選択）の操作を UI 層・ホストへ提供するファサード。</summary>
/// <remarks>
/// <b>適用パターン</b>: Facade（コアの状態機械・描画内容の解決・設定を 1 つの窓口へ集約する）。<br/>
/// <br/>
/// <b>責務</b>: Marquee の「操作（入力の通知・外部からの状態操作・描画内容の解決・設定）」を単一の窓口で提供し、<br/>
/// UI 層とホストは本クラスだけを参照する（コアの内部契約である状態機械 <see cref="MarqueeSession"/> と<br/>
/// フレーム解決 <see cref="MarqueeFrame"/> へ直接触れない）。<br/>
/// <br/>
/// <b>本クラスが単一所有するもの</b>:<br/>
/// - セッション（入力の解釈と状態遷移の判断）の生成と保持<br/>
/// - 環境値の保持（座標変換ポート・倍率）と、ポート未指定時に倍率を使う規則の適用<br/>
/// - 効果（フォーカス・カーソル操作・カーソル形状・ポインターキャプチャ・再描画）の適用<br/>
/// - 1 フレーム分の描画内容の解決（描くかどうかの判断）と、描画実装（<see cref="IMarqueeRenderer"/>）の保持<br/>
/// <br/>
/// <b>本クラスが持つもの</b>: 選択の判断・状態・描画内容（選択矩形・装飾・ラベル）と描画順の契約、<br/>
/// 装飾の設定の保持、ラベルの文言プロバイダの保持。<br/>
/// <br/>
/// <b>UI 層の責務</b>: 入力イベントの受付と座標変換（<see cref="ToDomain"/> / <see cref="ToDip"/>）、<br/>
/// <see cref="IMarqueeEffects"/> の実装、通知（<see cref="Notified"/>）の公開イベントへの転送、<br/>
/// 返された描画内容の反映（図形は <see cref="Renderer"/> へ委譲、文字はテキスト要素の配置）。<b>描画順はコアの指示に従う</b>。<br/>
/// <br/>
/// <b>描画の差し替え</b>: 描画は <see cref="IMarqueeRenderer"/> の実装へ委譲する（<see cref="Renderer"/> で差し替え可能）。<br/>
/// コアは描画層を参照しないため、実装は注入で受け取る（コンストラクタで必須）。<br/>
/// <br/>
/// <b>依存分離</b>: UI 非依存のコア型（<see cref="EsPoint"/> / <see cref="EsRect"/> / <see cref="EsKey"/>）と<br/>
/// 抽象（<see cref="ICursorService"/> / <see cref="ICoordTransform"/> / <see cref="IMarqueeEffects"/>）にのみ依存する。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で検証できる。
/// </remarks>
public sealed class Marquee
{
    /// <summary>入力の解釈と状態遷移の判断を担うセッション（コア内部の状態機械）。</summary>
    private readonly MarqueeSession _session;

    /// <summary>描画内容を描画面へ写す描画実装（既定は描画層の <c>MarqueeRenderer</c>。色はインスタンス状態として渡す）。</summary>
    private IMarqueeRenderer _renderer;

    /// <summary>効果（フォーカス・再描画・カーソル操作）の適用先（UI 層が実装する）。</summary>
    private readonly IMarqueeEffects _effects;

    /// <summary>カーソルサービス（nudge のカーソル移動・表示切替）。null の場合はカーソル操作を行わない。</summary>
    private readonly ICursorService? _cursor;

    /// <summary>外部から指定された座標変換ポート（null = 未指定。未指定時は <see cref="Scaling"/> を倍率として使う）。</summary>
    /// <remarks>
    /// セッションは null を等価変換に置換して保持するため、<b>指定の有無（null かどうか）は本フィールドだけが保持する</b>。<br/>
    /// この区別が必要なのは、ポート未指定時に倍率（<see cref="Scaling"/>）を使う規則をコアが単一所有するためである。
    /// </remarks>
    private ICoordTransform? _coordinatePort;

    /// <summary>現在の装飾の設定（暗転マスク・穴あけ枠・枠線・内側の塗り）。</summary>
    private MarqueeDecoOptions _decoOptions = MarqueeDecoOptions.Default;

    /// <summary>サイズラベルの自動生成に使うスケーリング係数（0 以下は <see cref="Scaling"/> と同値）。</summary>
    private double _displayScaleScaling;

    /// <summary>ポート未指定時に適用する倍率の既定値（ドメイン座標と DIP が 1:1）。</summary>
    /// <value>1.0（等倍）。</value>
    /// <remarks>各 UI の依存関係プロパティ（<c>Scaling</c>）の既定値も本値を参照する（既定値の二重所有を防ぐ）。</remarks>
    public const double DefaultScaling = 1.0;

    /// <summary>ポート未指定時に適用する倍率（0 以下・等倍は等価変換）。</summary>
    private double _scaling = DefaultScaling;

    /// <summary>サイズラベルの文言プロバイダ（null = 未指定。未指定時はコアが自動生成する）。</summary>
    private Func<string?>? _sizeLabelOverrideProvider;

    /// <summary>ファサードを初期化する。</summary>
    /// <param name="renderer">描画の実装（既定実装は描画層の <c>MarqueeRenderer</c>。色はインスタンス状態として渡す）。null 不可。</param>
    /// <param name="effects">効果（フォーカス・再描画・カーソル形状・キャプチャ・座標変換）の適用先。null 不可。</param>
    /// <param name="cursor">カーソルサービス。null 可（null の場合はカーソル操作を行わない）。</param>
    /// <param name="options">初期設定。null 可（null の場合は <see cref="MarqueeOptions.Default"/>）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="renderer"/> または <paramref name="effects"/> が null の場合。</exception>
    public Marquee(
        IMarqueeRenderer renderer,
        IMarqueeEffects effects,
        ICursorService? cursor = null,
        MarqueeOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(effects);

        _renderer = renderer;
        _effects = effects;
        _cursor = cursor;
        _session = new MarqueeSession(options ?? MarqueeOptions.Default);
        _session.Notified += OnSessionNotified;
    }

    /// <summary>上位（UI 層）へ伝える通知。UI 層は自 UI の公開イベントへ転送する。</summary>
    /// <remarks>通知の種別と公開イベントの対応は UI 層が持ち、判断（確定・キャンセル等）は本クラスが済ませている。</remarks>
    public event EventHandler<MarqueeNotice>? Notified;

    /// <summary>描画の実装（描画内容を描画面へ写す戦略）。</summary>
    /// <value>現在の描画実装。既定は <see cref="IMarqueeRenderer"/> の既定実装。</value>
    /// <remarks>
    /// 設定すると再描画を要求する（同値の再設定では何も起こらない）。<br/>
    /// 描画先（<see cref="IEsCanvas"/> の実装）と描画手順（本プロパティ）の両方を差し替えできる。<br/>
    /// null は受け付けない（設定の有無で描画可否が変わると、UI の描画経路に分岐が生まれるため）。
    /// </remarks>
    /// <exception cref="ArgumentNullException">設定値が null の場合。</exception>
    public IMarqueeRenderer Renderer
    {
        get => _renderer;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            SetAndRedraw(ref _renderer, value);
        }
    }

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
    /// <value>ドメイン → DIP の倍率。既定は 1.0（0 以下・等倍は等価変換として扱う）。</value>
    /// <remarks>
    /// 環境値（DPI・マルチスクリーン）のため <see cref="Options"/> には含めない。<br/>
    /// 座標変換ポート（<see cref="SetCoordinateTransform"/>）が設定されている場合は使われない（ポート優先。規則はコアが単一所有する）。<br/>
    /// 選択矩形と十字マーカーの描画はセッションのポートのみを使うため本値の影響を受けない<br/>
    /// （本値が影響するのはサイズラベル・座標ラベルの位置と寸法）。
    /// </remarks>
    public double Scaling
    {
        get => _scaling;
        set => SetAndRedraw(ref _scaling, value);
    }

    /// <summary>装飾（暗転マスク・穴あけ枠・枠線・内側の塗り）の設定。</summary>
    /// <value>装飾の設定。既定は <see cref="MarqueeDecoOptions.Default"/>。</value>
    /// <remarks>
    /// 装飾は選択矩形コントロール単体の概念ではない（組み込み先の表示）ため、選択の設定（<see cref="Options"/>）とは分けている。<br/>
    /// 設定すると再描画を要求する。
    /// </remarks>
    public MarqueeDecoOptions DecoOptions
    {
        get => _decoOptions;
        set => SetAndRedraw(ref _decoOptions, value);
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
        set => SetAndRedraw(ref _sizeLabelOverrideProvider, value);
    }

    /// <summary>サイズラベルの自動生成に使うスケーリング係数（マルチスクリーン補正）。</summary>
    /// <value>DisplayScale の百分率に使う係数。未設定（0 以下）の場合は <see cref="Scaling"/> と同じ値。</value>
    /// <remarks>0 以下を倍率と同値として扱う規則は <see cref="MarqueeSizeLabel.ResolveDisplayScaling"/> が単一所有する。</remarks>
    public double DisplayScaleScaling
    {
        get => MarqueeSizeLabel.ResolveDisplayScaling(_displayScaleScaling, _scaling);
        set => SetAndRedraw(ref _displayScaleScaling, value);
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

    /// <summary>選択矩形コントロールの 1 フレーム分の描画内容（選択矩形・装飾・ラベルと描画順）を解決する。</summary>
    /// <param name="clientSize">描画領域のサイズ（DIP。UI 実装の描画先のサイズ）。</param>
    /// <returns>描画内容（描くものが無い項目は空・null。層の順序は <see cref="MarqueeDrawContent.Layers"/> が持つ）。</returns>
    /// <remarks>
    /// 判断（描くかどうか・位置・寸法・文言）はすべてコアが単一所有するため、<br/>
    /// UI 層は <see cref="MarqueeDrawContent.Layers"/> の順に各層を反映するだけでよい。<br/>
    /// ラベル（サイズ・カーソル座標）は必ず最後の層であり、常に最上位に表示される。<br/>
    /// テキストの実寸は描画 API 依存のため、コアは「どこへ何を描くか」だけを返し、実測と要素の配置は UI 層が行う。
    /// </remarks>
    public MarqueeDrawContent ResolveContent(EsSize clientSize)
        => MarqueeFrame.ResolveContent(new MarqueeFrameRequest(
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
            DisplayScaleScaling));

    /// <summary>解決済みの描画内容を描画面へ描く（描画起動の唯一の入口）。</summary>
    /// <param name="canvas">描画先の描画ポート（UI 層が自 UI の描画面を適合させて渡す）。null 不可。</param>
    /// <param name="content">描く内容（同じフレームの <see cref="ResolveContent"/> の結果）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="canvas"/> が null の場合。</exception>
    /// <remarks>
    /// <b>描画は本メソッドを通してのみ起動する</b>: UI 層が <see cref="Renderer"/> を直接呼ぶと、<br/>
    /// 描画実装の差し替え（<see cref="Renderer"/> の設定）と描画内容の単一所有が二重経路になる。<br/>
    /// 本メソッドを経由させることで、描画の起動・順序（<see cref="MarqueeRendererBase.DrawShapes"/>）・<br/>
    /// 差し替えのすべてがコアの単一所有になる。<br/>
    /// <b>描画面の寿命と消去は UI 層が所有する</b>（前フレームの残像の除去は本メソッドでは行わない）。<br/>
    /// 描画内容を引数で受け取る（本メソッドが <see cref="ResolveContent"/> を呼び直すと 1 フレームで解決が 2 回走る）。
    /// </remarks>
    public void Draw(IEsCanvas canvas, in MarqueeDrawContent content)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        _renderer.DrawShapes(canvas, content);
    }

    /// <summary>ラベル要素の配置指示を描画順に解決する（UI 要素として配置する UI 用）。</summary>
    /// <param name="content">配置の基準にする描画内容（同じフレームの <see cref="ResolveContent"/> の結果）。</param>
    /// <param name="measureText">テキストの実寸を測る関数（描画 API 依存のため UI 層が与える）。null 不可。</param>
    /// <returns>配置するラベル要素の一覧（サイズラベル → カーソル座標ラベルの順）。どちらも無い場合は空。</returns>
    /// <remarks>
    /// 描画内容を引数で受け取る（本メソッドが <see cref="ResolveContent"/> を呼び直すと、1 フレームで同じ解決が 2 回走る）。<br/>
    /// UI 層は 1 フレーム分の描画内容を保持し、図形の描画と本メソッドの両方へ同じ値を渡す。<br/>
    /// 要素への写像は <see cref="Renderer"/>（描画実装）へ委譲する<br/>
    /// （どのラベルを・どの順に・どの体裁で描くかはコアが単一所有し、実寸の反映だけが描画実装の責務である）。
    /// </remarks>
    public IReadOnlyList<EsLabelElement> ResolveLabels(
        in MarqueeDrawContent content, Func<string, double, EsSize> measureText)
        => _renderer.ResolveLabels(content, measureText);

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

    /// <summary>新しい値を保持し、変化があれば再描画を要求する。</summary>
    /// <typeparam name="T">値の型。</typeparam>
    /// <param name="field">保持先のフィールド。</param>
    /// <param name="value">設定する値。</param>
    /// <remarks>
    /// 「同値なら何もしない（再描画も要求しない）」という規則を単一所有する<br/>
    /// （各プロパティが同じ数行を書くと、片方だけ同値判定が抜けて再描画が溢れる）。
    /// </remarks>
    private void SetAndRedraw<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        _effects.RequestRedraw();
    }
}
