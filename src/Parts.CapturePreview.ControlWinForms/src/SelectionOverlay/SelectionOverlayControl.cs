using System.ComponentModel;
using EsUtil.Feature.Loupe;
using EsUtil.Feature.Loupe.WinForms;
using EsUtil.Feature.Marquee;
using EsUtil.Feature.Marquee.WinForms;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Logging;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Presentation;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.SelectionOverlay;
using EsUtil.SaveEvidence.Parts.CapturePreview.WinForms.Overlay;
using EsUtil.SaveEvidence.Platform.WinForms.Imaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.WinForms.SelectionOverlay;

/// <summary>領域選択の複合コントロール（選択矩形 + 暗転マスク + ルーペの組み立て・配線・状態同期を 1 箇所に集約）。</summary>
/// <remarks>
/// <b>適用パターン</b>: Facade / Composite（内包コントロールの公開 API を統合し、ホストへ統一インターフェースを提供）。<br/>
/// 内包コントロールの生成・座標変換注入・イベント購読・状態同期・後片付けの配線を本クラスに集約し、<br/>
/// ホストはライフサイクル（生成・配置・後片付け）とモード固有処理（キャプチャ・確定フロー）に専念できる。<br/>
/// <br/>
/// <b>UI 層の責務（本クラス）</b>: 設定（<see cref="Options"/>）とモード（<see cref="Mode"/>）を内包コントロールへ分配し、<br/>
/// 入力の受付（内包コントロールが担う）・効果の適用・通知の転送・描画先の選択のみを行う。<br/>
/// モードごとの可否（選択矩形・ルーペ・全面暗転・破線）はコアの <c>SelectionOverlayState</c> が、<br/>
/// 選択矩形の可否はコアの <c>SelectionUsability</c> が単一所有する（UI 層は判断を持たず、コアの結果を割り当てるだけになる）。<br/>
/// <br/>
/// <b>内部構造</b>:<br/>
/// - <see cref="MarqueeControl"/>: ドラッグ選択の本体（対象座標 + 座標変換ポート）。<br/>
///   選択矩形の<b>外側に描く装飾</b>（暗転マスク・外枠・サイズラベル・ハイライト）も本コントロールが描画する。<br/>
/// - <see cref="LoupeControl"/>: 拡大鏡（中心 = 対象座標、表示位置はコアの計算で本クラスが配置する）。<br/>
/// <br/>
/// <b>WinForms 固有の結線（他の UI 版との差）</b>:<br/>
/// - ルーペ画像上の選択矩形の破線は、ルーペの <c>Rendered</c> で通知された描画面へ本クラスが直接描く。<br/>
/// - 付加情報ラベル（操作・情報）は <see cref="GdiLabel"/> で描画面へ直接描く（WinForms の子コントロールは半透明の背景を持てない）。<br/>
/// - ルーペの位置とサイズは <see cref="Control.Bounds"/> をコアの解決結果で設定する（レイアウト時に再評価する）。<br/>
/// - 入力の受け取りは <see cref="Control.Enabled"/> で表す（無効なコントロールは入力が下のコントロールへ通る）。<br/>
/// <br/>
/// <b>座標系</b>: 選択矩形・ルーペ中心は対象座標で扱う。 対象 ↔ ローカル DIP の変換は<br/>
/// 座標変換ポート（<see cref="SetCoordTransform"/>）が担い、ルーペへも同じポートを適用する。
/// </remarks>
public sealed class SelectionOverlayControl : Control
{
    /// <summary>ドラッグ選択コントロール（選択矩形・ハンドル・nudge と、外側の装飾（暗転マスク・枠・ラベル）の描画）。</summary>
    private readonly MarqueeControl _marquee = new();

    /// <summary>ルーペコントロール（表示位置は本クラスが配置する）。</summary>
    private readonly LoupeControl _loupe = new();

    /// <summary>直前にルーペを置いた領域（残像を消すために使う）。 未配置は <see cref="Rectangle.Empty"/>。</summary>
    private Rectangle _lastLoupeBounds = Rectangle.Empty;

    /// <summary>設定（選択矩形・暗転マスク・ルーペ・選択矩形破線の外観）。</summary>
    private SelectionOverlayOptions _options = SelectionOverlayOptions.Default;

    /// <summary>動作モード（既定は範囲選択）。</summary>
    private SelectionOverlayMode _mode = SelectionOverlayMode.AreaSelect;

    /// <summary>ログの接頭辞（未設定は null。 既定値は定義クラスが持つ）。</summary>
    private string? _logPrefix;

    /// <summary>領域選択の複合コントロールを初期化する（子コントロールの生成・配置・配線を実施する）。</summary>
    /// <remarks>
    /// ドラッグ選択 → ルーペの順に重ねる（ルーペが選択矩形の描画の上に載る）。<br/>
    /// イベント配線（状態同期・ホストへのイベント中継）を行い、モードを既定（範囲選択）で適用する。<br/>
    /// <b>入力の可否はモードから決まる</b>（<see cref="ApplyOptions"/> が設定する。 ハイライトのみのモードでは受け取らない）。
    /// </remarks>
    public SelectionOverlayControl()
    {
        SetStyle(
            ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor,
            true);

        // 親（ズームビュー）の描画を自分の背景として使う（下の画像を透かす）
        BackColor = Color.Transparent;

        _marquee.Dock = DockStyle.Fill;
        _loupe.Visible = false;

        Controls.Add(_marquee);
        Controls.Add(_loupe);
        // ルーペは選択矩形の描画の上に載せる（追加の順序に依存しないよう明示する）
        _loupe.BringToFront();

        // ── 配線 ──
        // 選択矩形の変更は、ルーペ画像上の破線を描き直すためにルーペの再描画を要求する
        // （選択矩形の描画自体と外側の装飾はドラッグ選択コントロールがセッションから直接読んで描画する）。
        _marquee.SelectionChanged += (_, _) => _loupe.Invalidate();
        _marquee.LoupePositionMoved += (_, e) => OnLoupePositionMoved(e.Point);
        _marquee.ConfirmRequested += (_, e) => RaiseConfirmRequested(e.Rect);
        _marquee.CancelRequested += (_, _) => RaiseCancelRequested();
        // サイズラベルとカーソル座標ラベルは選択矩形コントロールが描く。
        // 操作ラベル・情報ラベルはアプリ固有の付加情報のため、本クラス（ホスト）が描画完了時に描く。
        _marquee.Rendered += OnMarqueeRendered;
        // ルーペ画像上の選択矩形破線は、ルーペの描画完了直後に同一の描画面へ描く
        _loupe.Rendered += OnLoupeRendered;

        // 設定の既定値を子コントロールへ分配する（モードの状態適用は ApplyOptions の内部で行われる）
        ApplyOptions();
    }

    /// <summary>Enter キーまたは <see cref="RequestConfirm"/> による確定要求イベント（選択確定後のみ発火する）。</summary>
    public event EventHandler<SelectionConfirmedEventArgs>? ConfirmRequested;

    /// <summary>Escape キーまたは <see cref="RequestCancel"/> によるキャンセル要求イベント。</summary>
    public event EventHandler? CancelRequested;

    /// <summary>ルーペ中心の更新イベント（対象座標。 ホストの追加制御用フック）。</summary>
    public event EventHandler<LoupePositionEventArgs>? LoupePositionMoved;

    /// <summary>選択矩形（暗転マスクを含む）の描画が完了したことを通知するイベント。</summary>
    /// <remarks>
    /// 描画のたびに発火する（マスクの描画完了を待ってから何かを行いたいホストが購読する）。<br/>
    /// 撮影オーバーレイは透明度を使わず、下地色を最終の見た目に合わせているため本イベントを使わない。
    /// </remarks>
    public event EventHandler? MarqueeRendered;

    /// <summary>設定（選択矩形・暗転マスク・ルーペ・選択矩形破線の外観）を取得または設定する。</summary>
    /// <value>設定の単一値。 既定は <see cref="SelectionOverlayOptions.Default"/>（内包コントロールの既定値）。</value>
    /// <remarks>
    /// 一部の項目だけを変える場合は <c>SelectionOverlayOptions.Default with { ... }</c> の形で指定する（指定しない項目は既定値を保つ）。<br/>
    /// 設定を 1 つの値にまとめているため、内包コントロールへ分配する規則（枠線の共用等）はコアが単一所有する。<br/>
    /// 同じ値の再設定では更新をスキップする（無駄な再適用を防ぐ）。
    /// </remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public SelectionOverlayOptions Options
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
    /// <value>動作モード。 既定値は <see cref="SelectionOverlayMode.AreaSelect"/>。</value>
    /// <remarks>
    /// - Crop / AreaSelect: 選択矩形とルーペを表示し、全面暗転を有効化する。<br/>
    /// - ScreenSelect / WindowSelect: 選択矩形とルーペを非表示にし、全面暗転を無効化する（ハイライトのみ）。
    /// </remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public SelectionOverlayMode Mode
    {
        get => _mode;
        set
        {
            if (_mode == value)
            {
                return;
            }

            _mode = value;
            ApplyOptions();
        }
    }

    /// <summary>現在の選択状態（選択矩形の有無）を取得する。</summary>
    /// <value>選択矩形が有効な場合は true。</value>
    /// <remarks>選択状態の真のソースはドラッグ選択コントロールのセッションであり、本プロパティはその読み取りを委譲するだけである。</remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool HasSelection => _marquee.HasSelection;

    /// <summary>現在の選択矩形（対象座標）を取得する。</summary>
    /// <value>選択矩形。 未選択時は 0x0 の空矩形。</value>
    /// <remarks>選択状態の真のソースはドラッグ選択コントロールのセッションであり、本プロパティはその読み取りを委譲するだけである。</remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public EsRect SelectionSourceRect => _marquee.SelectionSourceRect;

    /// <summary>ハイライト矩形（対象座標）を取得または設定する。</summary>
    /// <value>ハイライト矩形。 未検出時は null（<b>null が「ハイライトしない」を意味する</b>）。 選択矩形より優先される。</value>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public EsRect? HighlightSourceRect
    {
        get => _marquee.HighlightSourceRect;
        set => _marquee.HighlightSourceRect = value;
    }

    /// <summary>全面暗転（注目なし）を表示するかどうかを取得する。</summary>
    /// <value>全面暗転する場合は true。 モードから導出される読み取り専用の値（外観の表示可否を参照する）。</value>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowFullOverlay => _marquee.Appearance.ShowFullOverlay;

    /// <summary>ルーペ中心（対象座標）を取得する。</summary>
    /// <value>ルーペ中心。 未設定時は null。</value>
    /// <remarks>中心の真のソースはルーペコントロールへ集約し、本プロパティはその参照のみを返す。</remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public EsPoint? LoupeCenterPoint => _loupe.CenterPoint;

    /// <summary>選択矩形コントロールの外観（描画にだけ必要な設定）を取得または設定する。</summary>
    /// <value>外観の単一値。 既定は <see cref="MarqueeAppearance.Default"/>。</value>
    /// <remarks>
    /// 色・ラベル体裁・表示可否・枠線の太さはコアの計算に入らない（描画にだけ必要）ため、<br/>
    /// 設定（<see cref="Options"/>）には含めず本プロパティが与える。<br/>
    /// 内包する選択矩形コントロールへそのまま転送し、モードから導出する表示可否を上書きし直す<br/>
    /// （外観は評価値ではなくホストが与える素材であり、代入の順序でモードの判断が失われないようにする）。
    /// </remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public MarqueeAppearance MarqueeAppearance
    {
        get => _marquee.Appearance;
        set
        {
            _marquee.Appearance = value;
            ApplyOptions();
        }
    }

    /// <summary>ルーペの描画に使う色（描画にだけ必要な設定）を取得または設定する。</summary>
    /// <value>色の単一値。 既定は <see cref="LoupeAppearance.Default"/>。</value>
    /// <remarks>
    /// 色はコアの計算に入らない（描画にだけ必要）ため、設定（<see cref="Options"/>）には含めず本プロパティが与える。<br/>
    /// 内包するルーペコントロールへそのまま転送する（下地色もここで与える。 ホストが背後の要素を透かすかを決める）。
    /// </remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public LoupeAppearance LoupeAppearance
    {
        get => _loupe.Appearance;
        set => _loupe.Appearance = value;
    }

    /// <summary>ドラッグ選択コントロール（テスト用アクセサ。 設定は <see cref="Options"/> 経由で設定する）。</summary>
    internal MarqueeControl Marquee => _marquee;

    /// <summary>ルーペコントロール（テスト用アクセサ。 配置の検証に使用する）。</summary>
    internal LoupeControl Loupe => _loupe;

    /// <summary>操作ラベル（アプリ固有の付加情報）の文言を描画時に生成するデリゲート。</summary>
    /// <value>描画のたびに評価される関数。 null または空文字を返す場合は描かない。</value>
    /// <remarks>
    /// 文言はアプリ固有（選択コントロールの概念ではない）ため、コアは関知しない<br/>
    /// （本クラスが文言の取得と描画を行い、<c>CaptureLabelTexts</c> 等が文言を単一所有する）。
    /// </remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<string?>? OperationTextProvider { get; set; }

    /// <summary>情報ラベル（ウィンドウタイトル）の文言を描画時に生成するデリゲート。</summary>
    /// <value>描画のたびに評価される関数。 null または空文字を返す場合は描かない。</value>
    /// <remarks>文言はアプリ固有のため、コアは関知しない（本クラスが取得と描画を行う）。</remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<string?>? InfoTextProvider { get; set; }

    /// <summary>サイズラベルの文言を描画時に評価するデリゲートを取得または設定する。</summary>
    /// <value>描画時に呼び出される関数。 null または空文字を返す場合はサイズラベルを描画しない。</value>
    /// <remarks>
    /// ホストは最新の選択状態から文言を生成するクロージャを設定する（設定しない場合はコアが矩形から自動生成する）。
    /// </remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<string?>? SizeLabelOverrideProvider
    {
        get => _marquee.SizeLabelOverrideProvider;
        set => _marquee.SizeLabelOverrideProvider = value;
    }

    /// <summary>サイズラベルの表示スケーリングを矩形ごとに解決するプロバイダを取得または設定する。</summary>
    /// <value>描画時に呼び出される関数（引数は対象座標の矩形）。 null の場合は <see cref="DisplayScaleScaling"/> を使う。</value>
    /// <remarks>
    /// ホストは最新の選択状態からモニタの実表示スケールを解決するクロージャを設定する（例: 矩形を含むモニタのスケール）。<br/>
    /// 文言（書式）はコアが単一所有するため、ホストは数値だけを与える。
    /// </remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<EsRect, double>? SizeLabelDisplayScalingProvider
    {
        get => _marquee.SizeLabelDisplayScalingProvider;
        set => _marquee.SizeLabelDisplayScalingProvider = value;
    }

    /// <summary>DPI スケーリング係数（対象座標 / ローカル DIP の倍率）を取得または設定する。</summary>
    /// <value>スケーリング係数。 対象座標がローカル DIP と一致する場合は 1.0。</value>
    /// <remarks>
    /// 環境値（ユーザー設定ではない）のため設定（<see cref="Options"/>）には含めず、ホストが与える。<br/>
    /// 座標変換ポート（<see cref="SetCoordTransform"/>）が設定されている場合はそちらが優先される。
    /// </remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double ClientToSourceScaling
    {
        get => _marquee.ClientToSourceScaling;
        set => _marquee.ClientToSourceScaling = value;
    }

    /// <summary>サイズラベル表示用の DPI スケーリング係数（マルチスクリーン補正。 0 以下は <see cref="ClientToSourceScaling"/> と同値）を取得または設定する。</summary>
    /// <value>サイズラベルの DisplayScale に使う倍率。 0 以下は <see cref="ClientToSourceScaling"/> から解決される。</value>
    /// <remarks>環境値のため設定（<see cref="Options"/>）には含めず、ホストが与える。</remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double DisplayScaleScaling
    {
        get => _marquee.DisplayScaleScaling;
        set => _marquee.DisplayScaleScaling = value;
    }

    /// <summary>ルーペの DIP → 物理ピクセル倍率を取得または設定する。</summary>
    /// <value>スケーリング係数（0.1〜8.0）。 既定は 1.0。</value>
    /// <remarks>環境値（DPI）のため設定（<see cref="Options"/>）には含めず、ホストが与える。</remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double LoupeClientToPixelScaling
    {
        get => _loupe.ClientToPixelScaling;
        set
        {
            _loupe.ClientToPixelScaling = value;
            PerformLayout();
        }
    }

    /// <summary>対象 ↔ ローカル DIP の座標変換ポートを全内包コントロールへ一括設定する。</summary>
    /// <param name="port">座標変換ポート。 null の場合は <see cref="ClientToSourceScaling"/> 相当の等倍スケール（コアが解決）を使う。</param>
    /// <remarks>
    /// ドラッグ選択とルーペへ同一ポートを分配する（1 回の呼び出しで設定完了）。<br/>
    /// 変換関数は呼び出し毎に評価されるため、ズーム・スクロール・ウィンドウサイズ変更等のレイアウト変化に追従する。
    /// </remarks>
    public void SetCoordTransform(ICoordTransform? port)
    {
        _marquee.SetCoordTransform(port);
        _loupe.SetCoordTransform(port);
        PerformLayout();
    }

    /// <summary>操作ログの出力先を取得または設定する（内包するコントロールへ伝搬する）。</summary>
    /// <value>ロガー。 null を設定した場合は <see cref="NullLogger"/> へフォールバックする（出力なし）。</value>
    /// <remarks>ドラッグ選択コントロールとルーペへ伝搬する（他の UI 版と同じ範囲）。</remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ILogger Log
    {
        get => _marquee.Log;
        set
        {
            var logger = value ?? NullLogger.Instance;
            _marquee.Log = logger;
            _loupe.Log = logger;
        }
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

    /// <summary>選択確定要求をドラッグ選択コントロールへ委譲する（Enter キー相当）。</summary>
    public void RequestConfirm()
    {
        try
        {
            _marquee.RequestConfirm();
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 公開ファサードとして内部の例外を集約して記録し、呼び出し側へ伝搬する（例外処理規約 §4）
            Log.LogOperationError(ex, nameof(RequestConfirm));
            throw;
        }
    }

    /// <summary>選択キャンセル要求をドラッグ選択コントロールへ委譲する（Escape キー相当）。</summary>
    public void RequestCancel()
    {
        try
        {
            _marquee.RequestCancel();
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 公開ファサードとして内部の例外を集約して記録し、呼び出し側へ伝搬する（例外処理規約 §4）
            Log.LogOperationError(ex, nameof(RequestCancel));
            throw;
        }
    }

    /// <summary>ドラッグ選択コントロールへフォーカスを設定する（矢印キー nudge の受付開始）。</summary>
    /// <remarks>
    /// 無効状態（入力を受け取らないモード）ではフォーカスを取得できないため何もしない<br/>
    /// （取得できたかどうかの判断は <see cref="Control.Focus"/> に委ねる）。
    /// </remarks>
    public void FocusMarquee()
    {
        try
        {
            _marquee.Focus();
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 公開ファサードとして内部の例外を集約して記録し、呼び出し側へ伝搬する（例外処理規約 §4）
            Log.LogOperationError(ex, nameof(FocusMarquee));
            throw;
        }
    }

    /// <summary>選択矩形をプログラム的に設定する（外部復元・テスト用）。</summary>
    /// <param name="rect">選択矩形（対象座標）。</param>
    /// <remarks>
    /// ドラッグ選択コントロールへ委譲する（状態の更新と通知はセッションが担う）。<br/>
    /// 選択として成立するかどうかの判定はコアの <c>SelectionUsability</c> が単一所有する。<br/>
    /// 破線（ルーペ画像上の選択枠）は選択変更の通知を受けてルーペが再描画する。
    /// </remarks>
    public void SetSelection(EsRect rect)
    {
        // 選択として成立しないサイズは選択なしとして扱う（判断はコアが単一所有する）
        if (SelectionUsability.IsUsable(rect.Width, rect.Height))
        {
            _marquee.SetSelection(rect);
        }
        else
        {
            ClearSelection();
        }
    }

    /// <summary>選択状態をクリアして全面暗転（注目なし）に戻す。</summary>
    /// <remarks>セッションの選択状態と nudge をまとめて初期化する（状態の初期化はセッションが単一所有する）。</remarks>
    public void ClearSelection()
    {
        try
        {
            _marquee.ResetSelection();
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 公開ファサードとして内部の例外を集約して記録し、呼び出し側へ伝搬する（例外処理規約 §4）
            Log.LogOperationError(ex, nameof(ClearSelection));
            throw;
        }
    }

    /// <summary>ルーペの中心位置（対象座標）を設定して表示を開始する。</summary>
    /// <param name="sourcePos">ルーペ中心（対象座標）。</param>
    /// <remarks>
    /// 表示位置・サイズは座標変換ポート（<see cref="SetCoordTransform"/>）とコアの計算から求める。<br/>
    /// <b>拡大元画像が未設定の間は表示しない</b>（未設定の下地を見せない。<see cref="SetLoupeSource"/> の後に呼び出す）。
    /// </remarks>
    public void SetLoupePosition(EsPoint sourcePos)
    {
        try
        {
            UpdateLoupePosition(sourcePos);
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 公開ファサードとして内部の例外を集約して記録し、呼び出し側へ伝搬する（例外処理規約 §4）
            Log.LogOperationError(ex, nameof(SetLoupePosition));
            throw;
        }
    }

    /// <summary>ルーペの拡大元画像を設定する。</summary>
    /// <param name="source">拡大元の画像。 null の場合は解除する。</param>
    /// <remarks>画像を設定しただけでは表示しない（表示開始は <see cref="SetLoupePosition"/> が担う）。 解除時は表示も解除する。</remarks>
    public void SetLoupeSource(EsReadOnlyBitmap? source)
    {
        _loupe.Source = source;

        if (source is null)
        {
            // 拡大元画像が無い状態で表示を続けない（次の SetLoupePosition まで非表示）。
            // 表示可否の判断はコアが単一所有するため、ここでは問い合わせるだけにする。
            UpdateLoupeVisibility();
        }

        PerformLayout();
    }

    /// <summary>ルーペを非表示にして後片付けする（拡大元画像・中心・ビットマップを解放する）。</summary>
    /// <remarks>セッション終了時の後片付けに使用する（表示状態・中心・拡大元画像をまとめて破棄する）。</remarks>
    public void ClearLoupe()
    {
        _loupe.Source = null;
        _loupe.CenterPoint = null;
        // 表示可否の判断はコアが単一所有するため、ここでは問い合わせるだけにする
        UpdateLoupeVisibility();
        PerformLayout();
    }

    /// <summary>選択オーバーレイ（暗転マスク・ハイライト・選択矩形）の再描画を要求する。</summary>
    /// <remarks>ハイライト等の状態を変更した後に呼び出す。</remarks>
    public void RequestRedraw()
    {
        try
        {
            _marquee.InvalidateRender();
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 公開ファサードとして内部の例外を集約して記録し、呼び出し側へ伝搬する（例外処理規約 §4）
            Log.LogOperationError(ex, nameof(RequestRedraw));
            throw;
        }
    }

    /// <summary>表示の変化（パン・ズーム・表示サイズ）へ内包コントロールを追従させる。</summary>
    /// <remarks>
    /// 選択矩形（対象座標）は表示の変化で不変のため再同期は行わず、再描画（と配置の再評価）のみを要求する。
    /// </remarks>
    public void NotifyViewportChanged()
    {
        _marquee.InvalidateRender();
        PerformLayout();
    }

    /// <summary>ルーペ画像上の選択矩形破線を描画面へ描画する（テスト用アクセサ）。</summary>
    /// <param name="graphics">描画先の描画面（ルーペ画像の物理ピクセル座標系）。 null 不可。</param>
    /// <param name="captureTopLeft">ルーペの切り出し領域の左上（対象座標系）。</param>
    /// <param name="scale">対象座標 → ルーペ画像ピクセル座標の変換係数。</param>
    /// <param name="clientPixelSize">ルーペ画像の一辺のサイズ（ピクセル）。 切り出し領域との交差判定に使用する。</param>
    /// <remarks>
    /// 破線を描画しない場合（選択矩形を使わないモード・選択なし）は何もしない。<br/>
    /// 判断と幾何はコアが単一所有する（本メソッドは描画先の選択と色の割り当てだけを行う）。
    /// </remarks>
    internal void DrawSelectionOverlay(Graphics graphics, EsPoint captureTopLeft, double scale, int clientPixelSize)
    {
        // 破線を描くかどうかはモードと選択矩形の有無（コアが単一所有する判断）で決まる
        if (!SelectionOverlayState.Resolve(_mode).ShouldDrawSelectionOverlay(_marquee.HasSelection))
        {
            return;
        }

        SelectionOverlayGraphicsRenderer.DrawSelectionRect(
            graphics,
            _marquee.SelectionSourceRect,
            captureTopLeft,
            scale,
            clientPixelSize,
            _options.SelectionRectColorA,
            _options.SelectionRectColorB);
    }

    /// <summary>子コントロールを配置する（ルーペはコアの解決結果の位置とサイズで置く）。</summary>
    /// <param name="levent">レイアウトの情報。 null 不可。</param>
    /// <remarks>
    /// ルーペの表示サイズ・位置はコア（ルーペコントロールのファサード）が単一所有する<br/>
    /// （親領域の最小辺へのクランプ・オフセット・親領域内へのクランプ。 親サイズが 0 の場合の扱いもコアが判断する）。<br/>
    /// 非表示（未表示・画像未設定・中心未設定）の場合は大きさ 0 で配置する。
    /// </remarks>
    protected override void OnLayout(LayoutEventArgs levent)
    {
        ArgumentNullException.ThrowIfNull(levent);
        base.OnLayout(levent);

        if (!_loupe.Visible)
        {
            HideLoupe();
            return;
        }

        // 表示レイアウトの解決は DIP を要求するため、クライアント領域を DIP へ換算して渡す
        var layout = _loupe.ResolveLayout(_loupe.ToDipSize(ClientSize));
        if (layout.Position is not { } position)
        {
            HideLoupe();
            return;
        }

        var size = _loupe.ToClientPixel(layout.ClientSize);
        var bounds = new Rectangle(_loupe.ToClientPixel(position.X), _loupe.ToClientPixel(position.Y), size, size);
        if (bounds != _loupe.Bounds)
        {
            // 直前の位置を消してから移す（消さないと、移動前のルーペが残像として一瞬残る）。
            // 自分の描画は下の画像を透かして描かれるため、移動前の領域を無効化して
            // 描き直させる（親は子の領域を描かないため、親の無効化では消えない）
            var previous = _lastLoupeBounds;
            _loupe.Bounds = bounds;
            _lastLoupeBounds = bounds;

            if (previous is { IsEmpty: false })
            {
                InvalidateBelow(previous);
            }

            Invalidate(bounds);
            Update();
        }
    }

    /// <summary>ルーペを消し、直前の位置を消す。</summary>
    private void HideLoupe()
    {
        ClearLoupeTrace();
        _loupe.Bounds = Rectangle.Empty;
        _lastLoupeBounds = Rectangle.Empty;
    }

    /// <summary>直前のルーペの領域を、下の画像ごと描き直す。</summary>
    /// <remarks>
    /// 本コントロールは背景を描かず、実際に画像を透かして描くのは全面を覆う <c>_marquee</c>（既定で透明な背景を持つ）である。<br/>
    /// 自分の領域を無効化しても、全面を覆う子（<c>_marquee</c>）に切り取られて何も描かれないため、<br/>
    /// ルーペが動いた跡は <c>_marquee</c> を無効化しないと消えない（実測）。
    /// </remarks>
    private void ClearLoupeTrace()
    {
        if (_lastLoupeBounds is not { IsEmpty: false } previous)
        {
            return;
        }

        InvalidateBelow(previous);
        _lastLoupeBounds = Rectangle.Empty;
    }

    /// <summary>ルーペの下にある描画（画像を透かす子、無ければ自分）を指定領域で描き直す。</summary>
    /// <param name="bounds">描き直す領域（本コントロールのクライアント座標）。</param>
    private void InvalidateBelow(Rectangle bounds)
    {
        // 縁のアンチエイリアス分も含めて描き直す
        bounds.Inflate(2, 2);

        // _marquee は Dock=Fill のため、そのクライアント座標は本コントロールと一致する
        if (_marquee.Visible)
        {
            _marquee.Invalidate(bounds);
            _marquee.Update();
            return;
        }

        Invalidate(bounds);
        Parent?.Invalidate(bounds, false);
        Update();
    }

    /// <summary>ウィンドウ メッセージを処理する（入力を受け取らないモードでは下のコントロールへ通す）。</summary>
    /// <param name="m">処理するメッセージ。</param>
    /// <remarks>入力の透過の規則（対象メッセージと戻り値）は <see cref="OverlayHitTest"/> が単一所有する。</remarks>
    protected override void WndProc(ref Message m)
    {
        if (OverlayHitTest.ShouldPassThrough(m.Msg, Enabled))
        {
            m.Result = OverlayHitTest.Transparent;
            return;
        }

        base.WndProc(ref m);
    }

    /// <summary>ルーペ位置の更新（ドラッグ選択からの通知）を処理する。</summary>
    /// <param name="sourcePos">ルーペ中心（対象座標）。</param>
    /// <remarks>
    /// 拡大元画像が未設定の間は表示しない（ホストが <see cref="SetLoupeSource"/> を呼んだ後、<br/>
    /// <see cref="SetLoupePosition"/> で表示を開始する）。<br/>
    /// ホストへの通知は追加制御（拡大元画像の設定等）のフックとして発行する。<br/>
    /// <b>中心はイベントの座標ではなく実カーソルから取り直す</b>（ポインター移動イベントの座標は入力へ積まれた分だけ古く、<br/>
    /// そのまま使うとルーペがカーソルより遅れて見える。 取り直しの可否はコアが単一所有する）。
    /// </remarks>
    private void OnLoupePositionMoved(EsPoint sourcePos)
    {
        var position = _marquee.TryGetCurrentLoupePosition(out var current) ? current : sourcePos;
        UpdateLoupePosition(position);
        RaiseLoupePositionMoved(position);
    }

    /// <summary>ルーペの中心位置（対象座標）を設定して表示可否を更新する。</summary>
    /// <param name="sourcePos">ルーペ中心（対象座標）。</param>
    /// <remarks>
    /// <see cref="SetLoupePosition"/> と <see cref="OnLoupePositionMoved"/> で共用する（重複排除）。<br/>
    /// 表示可否の判断はコアの <c>SelectionOverlayState.IsLoupeVisible</c> が単一所有する。
    /// </remarks>
    private void UpdateLoupePosition(EsPoint sourcePos)
    {
        _loupe.CenterPoint = sourcePos;
        UpdateLoupeVisibility();
        PerformLayout();
    }

    /// <summary>設定とモードを内包コントロールへ分配する。</summary>
    /// <remarks>
    /// 値の決定（設定の写しとモードから導出する項目の上書き）は <see cref="SelectionOverlayChildValues"/>（Core）が<br/>
    /// 単一所有する（モードの解釈はコアが単一所有する）。<br/>
    /// ハイライト（ホストが与える実行時状態）は設定値に含まれないため、子コントロールの現在値を渡して引き継ぐ。<br/>
    /// 適用後は選択矩形の再描画とルーペの再描画・再配置を要求する（選択状態・ルーペ中心は維持される）。
    /// </remarks>
    private void ApplyOptions()
    {
        // ハイライト矩形は実行時状態のため、現在値を渡して引き継ぐ（設定の差し替えで失わない）
        var values = SelectionOverlayChildValues.Resolve(_options, _mode, _marquee.Appearance);

        _marquee.Options = values.Marquee;
        _marquee.Appearance = values.MarqueeAppearance;
        _loupe.Options = values.Loupe;
        // 表示位置のオフセットは設定に含まれる値からルーペへ渡す（保持と位置の計算はコアが単一所有する）
        _loupe.PositionOffset = values.LoupeOffset;

        // 選択矩形を使わないモードでは入力を受け取らない（描画は止めない。 暗転マスクは描画し続ける）。
        // WinForms では「無効なコントロールは入力が下へ通る」性質を使う
        Enabled = values.IsHitTestVisible;

        UpdateLoupeVisibility();

        _marquee.InvalidateRender();
        // ルーペは描画のたびに選択矩形の破線を組み立てるため、破線色の変更は再描画で反映される
        _loupe.Invalidate();
        PerformLayout();
    }

    /// <summary>ルーペの表示可否をモード・拡大元画像・中心から更新する。</summary>
    /// <remarks>表示可否の判断はコアの <c>SelectionOverlayState.IsLoupeVisible</c> が単一所有する。</remarks>
    private void UpdateLoupeVisibility()
    {
        _loupe.Visible = SelectionOverlayState
            .Resolve(_mode)
            .IsLoupeVisible(_loupe.Source is not null, _loupe.CenterPoint is not null);
    }

    /// <summary>描画完了時にホスト固有の付加情報（操作ラベル・情報ラベル）を描画する。</summary>
    /// <param name="sender">イベント送信元（選択矩形コントロール）。</param>
    /// <param name="e">描画先の描画面と描画内容（描画計画）。</param>
    /// <remarks>
    /// サイズラベルとカーソル座標ラベルは選択矩形コントロールが描く。<br/>
    /// <b>操作ラベル・情報ラベルは選択コントロールの概念ではない</b>（アプリ固有の付加情報）ため、<br/>
    /// 本クラス（ホスト）が文言の生成と描画を行い、<see cref="SelectionOverlayLabelRenderer"/>（Core）が配置指示を単一所有する。<br/>
    /// 描画面には DPI の拡大が適用済みであるため、配置指示の DIP 座標をそのまま使える。
    /// </remarks>
    private void OnMarqueeRendered(object? sender, MarqueeRenderedEventArgs e)
    {
        var placements = SelectionOverlayLabelRenderer.Resolve(
            e.Content,
            _options.Labels,
            MarqueeAppearance,
            OperationTextProvider?.Invoke(),
            InfoTextProvider?.Invoke(),
            GdiText.Measure);

        foreach (var placement in placements)
        {
            DrawLabel(e.Graphics, in placement);
        }

        // 描画の完了をホストへ通知する（撮影オーバーレイはこれを受けてウィンドウを不透明にする）
        MarqueeRendered?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>付加情報ラベル 1 件を描画面へ描く（背景を塗ってから文字を描く）。</summary>
    /// <param name="graphics">描画先の描画面（DPI の拡大が適用済み）。</param>
    /// <param name="placement">配置指示（寸法は実寸を反映済み）。</param>
    /// <remarks>描画の順序（背景 → 文字）は <see cref="GdiLabel"/> が単一所有する。</remarks>
    private static void DrawLabel(Graphics graphics, in MarqueeLabelPlacement placement)
    {
        var bounds = placement.DipRect;
        var textOrigin = new EsPoint(bounds.X + placement.PaddingX, bounds.Y + placement.PaddingY);
        GdiLabel.Draw(
            graphics,
            bounds,
            placement.BackgroundColor,
            placement.Text,
            textOrigin,
            placement.FontSize,
            placement.TextColor);
    }

    /// <summary>ルーペの描画完了時に、選択矩形の破線オーバーレイを同一の描画面へ描画する。</summary>
    /// <param name="sender">イベント送信元（ルーペコントロール）。</param>
    /// <param name="e">描画先の描画面と描画計画。</param>
    /// <remarks>
    /// 選択矩形の破線はルーペ内部では描画せず、本クラス（ホスト側）が描画完了直後に描画面へ追加描画する<br/>
    /// （破線の幾何はコアの <c>SelectionOutlineRasterizer</c> が単一所有し、他の UI 版と同じ見た目になる）。<br/>
    /// 切り出し領域の左上が未設定（中心未設定）の場合は破線を描画しない。<br/>
    /// <b>中心座標ラベルはルーペコントロールが描く</b>（本クラスは破線だけを描く）。
    /// </remarks>
    private void OnLoupeRendered(object? sender, LoupeRenderedEventArgs e)
    {
        if (e.Content.CaptureTopLeft is { } topLeft)
        {
            DrawSelectionOverlay(e.Graphics, topLeft, e.Content.ZoomLevel, e.Content.ClientPixelSize);
        }
    }

    /// <summary>確定要求イベントを発火する。</summary>
    /// <param name="rect">確定した選択矩形（対象座標）。</param>
    private void RaiseConfirmRequested(EsRect rect)
    {
        var handler = ConfirmRequested;
        handler?.Invoke(this, new SelectionConfirmedEventArgs(rect));
    }

    /// <summary>キャンセル要求イベントを発火する。</summary>
    private void RaiseCancelRequested()
    {
        var handler = CancelRequested;
        handler?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>ルーペ中心更新イベントを発火する。</summary>
    /// <param name="sourcePos">ルーペ中心（対象座標）。</param>
    private void RaiseLoupePositionMoved(EsPoint sourcePos)
    {
        var handler = LoupePositionMoved;
        handler?.Invoke(this, new LoupePositionEventArgs(sourcePos));
    }
}
