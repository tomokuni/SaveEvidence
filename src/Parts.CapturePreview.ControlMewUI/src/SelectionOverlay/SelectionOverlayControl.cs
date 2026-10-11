using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using EsUtil.Feature.Loupe;
using EsUtil.Feature.Loupe.MewUI;
using EsUtil.Feature.Marquee;
using EsUtil.Feature.Marquee.MewUI;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Logging;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Presentation;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.SelectionOverlay;
using EsUtil.SaveEvidence.Platform.MewUI;
using EsUtil.SaveEvidence.Platform.Skia;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;


namespace EsUtil.SaveEvidence.Parts.CapturePreview.MewUI.SelectionOverlay;

/// <summary>領域選択の複合コントロール（選択矩形 + 暗転マスク + ルーペの組み立て・配線・状態同期を 1 箇所に集約）。</summary>
/// <remarks>
/// <b>適用パターン</b>: Facade / Composite（内包コントロールの公開 API を統合し、ホストへ統一インターフェースを提供）。<br/>
/// 内包コントロールの生成・座標変換注入・イベント購読・状態同期・後片付けの配線を本クラスに集約し、<br/>
/// ホストはライフサイクル（生成・配置・後片付け）とモード固有処理（キャプチャ・確定フロー）に専念できる（SRP）。<br/>
/// <br/>
/// <b>UI 層の責務（本クラス）</b>: 設定（<see cref="Options"/>）とモード（<see cref="Mode"/>）を内包コントロールへ分配し、<br/>
/// 入力の受付（内包コントロールが担う）・効果の適用・通知の転送・描画先の選択のみを行う。<br/>
/// モードごとの可否（選択矩形・ルーペ・全面暗転・破線）はコアの <c>SelectionOverlayState</c> が、<br/>
/// 選択矩形の可否はコアの <c>SelectionUsability</c> が単一所有する（UI 層は判断を持たず、コアの結果を割り当てるだけになる）。<br/>
/// <br/>
/// <b>選択状態の単一のソース</b>: 選択矩形はドラッグ選択コントロールのセッションが単一所有する。 本クラスは選択状態を複製せず、<br/>
/// 読み取り（<see cref="HasSelection"/> / <see cref="SelectionSourceRect"/>）と設定（<see cref="SetSelection"/> / <see cref="ClearSelection"/>）を委譲するだけとする。<br/>
/// <br/>
/// <b>内部構造</b>:<br/>
/// - <see cref="MarqueeControl"/>: ドラッグ選択の本体（対象座標 + 座標変換ポート）。<br/>
///   選択矩形の<b>外側に描く装飾</b>（暗転マスク・外枠・サイズラベル・ハイライト）も本コントロールが描画する。<br/>
/// - <see cref="LoupeControl"/>: 拡大鏡（中心 = 対象座標、表示位置はコアの計算で本クラスが配置する）。<br/>
/// <br/>
/// <b>配線（本クラス内で完結）</b>:<br/>
/// - 選択矩形の変更 → ルーペ画像上の破線を描き直すためにルーペを再描画する（選択矩形自体はドラッグ選択コントロールが描く）。<br/>
/// - ルーペ位置の通知 → ルーペの中心を更新し、表示位置を再計算する。<br/>
/// - 確定・キャンセル・ズーム要求 → ホストへ中継する（独自のイベント引数形式）。<br/>
/// <br/>
/// <b>ラベル</b>: サイズラベル（1 行目）とサブライン（2 行目以降）は外側の装飾（ドラッグ選択コントロール）へ委譲する。<br/>
/// 文言はホストが <see cref="SizeLabelOverrideProvider"/> で与える（表示位置はコアの計算が単一所有する）。<br/>
/// <br/>
/// <b>動作モード（<see cref="Mode"/>）</b>:<br/>
/// - <see cref="SelectionOverlayMode.Crop"/> / <see cref="SelectionOverlayMode.AreaSelect"/>: 選択矩形 + 暗転マスク + ルーペを有効化する。<br/>
/// - <see cref="SelectionOverlayMode.ScreenSelect"/> / <see cref="SelectionOverlayMode.WindowSelect"/>: ハイライトのみを描画し、<br/>
///   入力を受けない（クリック確定フローはホストが担う）。<br/>
/// <br/>
/// <b>座標系</b>: 選択矩形・ルーペ中心は対象座標で扱う。 対象 ↔ ローカル DIP の変換は<br/>
/// 座標変換ポート（<see cref="SetCoordTransform"/>）が担い、ルーペへも同じポートを適用する。<br/>
/// <br/>
/// <b>描画先の選択（フレームワーク差）</b>: ルーペ画像上の選択矩形の破線は、描画完了イベントで通知されたキャンバスへ<br/>
/// 本クラスが直接描く（Avalonia 版は描画完了イベントをホストへ中継し、ホストがルーペ画像へ描く）。<br/>
/// 破線の幾何はコアが単一所有するため両 UI の見た目は一致する。<br/>
/// <br/>
/// <b>UI 層に残す例外（UI 固有のホスト結線）</b>: 子コントロールの生成・重なり順・ヒットテストの有無・クランプ・<br/>
/// フレームワーク固有の描画先（Bitmap / キャンバス / レイアウト要素）は UI 層が担う。
/// </remarks>
public sealed class SelectionOverlayControl : Panel
{
    /// <summary>ドラッグ選択コントロール（選択矩形・ハンドル・nudge と、外側の装飾（暗転マスク・枠・ラベル）の描画）。</summary>
    private readonly MarqueeControl _marquee = new() { IsHitTestVisible = true };

    /// <summary>ルーペコントロール（表示位置は本クラスが配置する）。</summary>
    private readonly LoupeControl _loupe = new() { IsHitTestVisible = false, IsVisible = false };

    /// <summary>設定（選択矩形・暗転マスク・ルーペ・選択矩形破線の外観）。</summary>
    private SelectionOverlayOptions _options = SelectionOverlayOptions.Default;

    /// <summary>動作モード（既定は範囲選択）。</summary>
    private SelectionOverlayMode _mode = SelectionOverlayMode.AreaSelect;

    /// <summary>領域選択の複合コントロールを初期化する（子コントロールの生成・配置・配線を実施する）。</summary>
    /// <remarks>
    /// 暗転マスク → ドラッグ選択 → ルーペの順に重ねる（後に追加した子が上へ描画される）。<br/>
    /// イベント配線（状態同期・ホストへのイベント中継）を行い、モードを既定（範囲選択）で適用する。
    /// </remarks>
    public SelectionOverlayControl()
    {
        Add(_marquee);
        Add(_loupe);

        // ── 配線 ──
        // 選択矩形の変更は、ルーペ画像上の破線を描き直すためにルーペの再描画を要求する
        // （選択矩形の描画自体と外側の装飾はドラッグ選択コントロールがセッションから直接読んで描画する）。
        _marquee.SelectionChanged += (_, _) => _loupe.InvalidateVisual();
        _marquee.LoupePositionMoved += (_, e) => OnLoupePositionMoved(e.Point);
        _marquee.ConfirmRequested += (_, e) => RaiseConfirmRequested(e.Rect);
        _marquee.CancelRequested += (_, _) => RaiseCancelRequested();
        // サイズラベルとカーソル座標ラベルは選択矩形コントロールがテキスト要素として配置する。
        // 操作ラベル・情報ラベルはアプリ固有の付加情報のため、本クラス（ホスト）が描画完了時に描く。
        _marquee.Rendered += OnMarqueeRendered;
        // ルーペ画像上の選択矩形破線は、ルーペの描画完了直後に同一のキャンバスへ描く
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

    /// <summary>設定（選択矩形・暗転マスク・ルーペ・選択矩形破線の外観）を取得または設定する。</summary>
    /// <value>設定の単一値。 既定は <see cref="SelectionOverlayOptions.Default"/>（内包コントロールの既定値）。</value>
    /// <remarks>
    /// 一部の項目だけを変える場合は <c>SelectionOverlayOptions.Default with { ... }</c> の形で指定する（指定しない項目は既定値を保つ）。<br/>
    /// 設定を 1 つの値にまとめているため、内包コントロールへ分配する規則（枠線の共用等）はコアが単一所有する。<br/>
    /// 同じ値の再設定では更新をスキップする（無駄な再適用を防ぐ）。
    /// </remarks>
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
    public bool HasSelection => _marquee.HasSelection;

    /// <summary>現在の選択矩形（対象座標）を取得する。</summary>
    /// <value>選択矩形。 未選択時は 0x0 の空矩形。</value>
    /// <remarks>選択状態の真のソースはドラッグ選択コントロールのセッションであり、本プロパティはその読み取りを委譲するだけである。</remarks>
    public EsRect SelectionSourceRect => _marquee.SelectionSourceRect;

    /// <summary>ハイライト矩形（対象座標）を取得または設定する。</summary>
    /// <value>ハイライト矩形。 未検出時は null（<b>null が「ハイライトしない」を意味する</b>）。 選択矩形より優先される。</value>
    public EsRect? HighlightSourceRect
    {
        get => _marquee.HighlightSourceRect;
        set => _marquee.HighlightSourceRect = value;
    }

    /// <summary>全面暗転（注目なし）を表示するかどうかを取得する。</summary>
    /// <value>全面暗転する場合は true。 モードから導出される読み取り専用の値（外観の表示可否を参照する）。</value>
    public bool ShowFullOverlay => _marquee.Appearance.ShowFullOverlay;

    /// <summary>ルーペ中心（対象座標）を取得する。</summary>
    /// <value>ルーペ中心。 未設定時は null。</value>
    /// <remarks>中心の真のソースはルーペコントロールへ集約し、本プロパティはその参照のみを返す。</remarks>
    public EsPoint? LoupeCenterPoint => _loupe.CenterPoint;

    /// <summary>選択矩形コントロールの外観（描画にだけ必要な設定）を取得または設定する。</summary>
    /// <value>外観の単一値。 既定は <see cref="MarqueeAppearance.Default"/>。</value>
    /// <remarks>
    /// 色・ラベル体裁・表示可否・枠線の太さはコアの計算に入らない（描画にだけ必要）ため、<br/>
    /// 設定（<see cref="Options"/>）には含めず本プロパティが与える。<br/>
    /// 内包する選択矩形コントロールへそのまま転送する。
    /// </remarks>
    public MarqueeAppearance MarqueeAppearance
    {
        get => _marquee.Appearance;
        set
        {
            _marquee.Appearance = value;
            // モードから導出する表示可否（ハンドル・全面暗転）を上書きし直す
            // （外観は評価値ではなくホストが与える素材であり、代入の順序でモードの判断が失われないようにする）
            ApplyOptions();
        }
    }

    /// <summary>ルーペの描画に使う色（描画にだけ必要な設定）を取得または設定する。</summary>
    /// <value>色の単一値。 既定は <see cref="LoupeAppearance.Default"/>。</value>
    /// <remarks>
    /// 色はコアの計算に入らない（描画にだけ必要）ため、設定（<see cref="Options"/>）には含めず本プロパティが与える。<br/>
    /// 内包するルーペコントロールへそのまま転送する（下地色もここで与える。 ホストが背後の要素を透かすかを決める）。
    /// </remarks>
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
    public Func<string?>? OperationTextProvider { get; set; }

    /// <summary>情報ラベル（ウィンドウタイトル）の文言を描画時に生成するデリゲート。</summary>
    /// <value>描画のたびに評価される関数。 null または空文字を返す場合は描かない。</value>
    /// <remarks>文言はアプリ固有のため、コアは関知しない（本クラスが取得と描画を行う）。</remarks>
    public Func<string?>? InfoTextProvider { get; set; }

    /// <summary>サイズラベルの文言を描画時に評価するデリゲートを取得または設定する。</summary>
    /// <value>描画時に呼び出される関数。 null または空文字を返す場合はサイズラベルを描画しない。</value>
    /// <remarks>
    /// ホストは最新の選択状態から文言を生成するクロージャを設定する（設定しない場合はコアが矩形から自動生成する）。
    /// </remarks>
    public Func<string?>? SizeLabelOverrideProvider
    {
        get => _marquee.SizeLabelOverrideProvider;
        set => _marquee.SizeLabelOverrideProvider = value;
    }

    /// <summary>サイズラベルの表示スケーリングを矩形ごとに解決するプロバイダを取得または設定する。</summary>
    /// <value>描画時に呼び出される関数（引数は対象座標の矩形）。 null の場合は <see cref="DisplayScaleScaling"/> を使う。</value>
    /// <remarks>
    /// ホストは最新の選択状態からモニタの実表示スケールを解決するクロージャを設定する（例: 矩形を含むモニタのスケール）。<br/>
    /// 文言（書式）はコアが単一所有するため、ホストは数値だけを与える。 設定時は選択矩形コントロールへ伝搬し、再描画を要求する。
    /// </remarks>
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
    public double ClientToSourceScaling
    {
        get => _marquee.ClientToSourceScaling;
        set => _marquee.ClientToSourceScaling = value;
    }

    /// <summary>サイズラベル表示用の DPI スケーリング係数（マルチスクリーン補正。 0 以下は <see cref="ClientToSourceScaling"/> と同値）を取得または設定する。</summary>
    /// <value>サイズラベルの DisplayScale に使う倍率。 0 以下は <see cref="ClientToSourceScaling"/> から解決される。</value>
    /// <remarks>環境値のため設定（<see cref="Options"/>）には含めず、ホストが与える。</remarks>
    public double DisplayScaleScaling
    {
        get => _marquee.DisplayScaleScaling;
        set => _marquee.DisplayScaleScaling = value;
    }

    /// <summary>ルーペの DIP → 物理ピクセル倍率を取得または設定する。</summary>
    /// <value>スケーリング係数（0.1〜8.0）。 既定は 1.0。</value>
    /// <remarks>環境値（DPI）のため設定（<see cref="Options"/>）には含めず、ホストが与える。</remarks>
    public double LoupeClientToPixelScaling
    {
        get => _loupe.ClientToPixelScaling;
        set => _loupe.ClientToPixelScaling = value;
    }

    /// <summary>対象 ↔ ローカル DIP の座標変換ポートを全内包コントロールへ一括設定する。</summary>
    /// <param name="port">座標変換ポート。 null の場合は <see cref="ClientToSourceScaling"/> 相当の等倍スケール（コアが解決）を使う。</param>
    /// <remarks>
    /// ドラッグ選択とルーペへ同一ポートを分配する（1 回の呼び出しで設定完了）。<br/>
    /// 変換関数は呼び出し毎に評価されるため、ズーム・スクロール・ウィンドウサイズ変更等のレイアウト変化に追従する。<br/>
    /// null はそのまま渡す（未設定を倍率で解決する規則はコアが単一所有する）。 ルーペの表示位置計算のみ本クラスが行うため、<br/>
    /// その用途では等価変換で代替する。
    /// </remarks>
    public void SetCoordTransform(ICoordTransform? port)
    {
        _marquee.SetCoordTransform(port);
        _loupe.SetCoordTransform(port);
        InvalidateArrange();
    }

    /// <summary>操作ログの出力先を取得または設定する（内包するコントロールへ伝搬する）。</summary>
    /// <value>ロガー。 null を設定した場合は <see cref="Microsoft.Extensions.Logging.Abstractions.NullLogger"/> へフォールバックする（出力なし）。</value>
    /// <remarks>ドラッグ選択コントロールとルーペへ伝搬する（Avalonia 版と同じ範囲）。</remarks>
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
    public string? LogPrefix
    {
        get => _logPrefix;
        set
        {
            _logPrefix = value;
            CapturePreviewLogMessage.SetPrefix(value);
        }
    }

    /// <summary>ログの接頭辞（未設定は null。 既定値は定義クラスが持つ）。</summary>
    private string? _logPrefix;
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
    /// <b>拡大元画像が未設定の間は表示しない</b>（未設定の市松模様を防ぐ。<see cref="SetLoupeSource"/> の後に呼び出す）。
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

    /// <summary>ルーペの中心位置（対象座標）を設定して表示可否を更新する。</summary>
    /// <param name="sourcePos">ルーペ中心（対象座標）。</param>
    /// <remarks>
    /// <see cref="SetLoupePosition"/> と <see cref="OnLoupePositionMoved"/> で共用する（重複排除）。<br/>
    /// 表示位置・サイズは座標変換ポート（<see cref="SetCoordTransform"/>）とコアの計算から求める。<br/>
    /// 表示可否の判断はコアの <c>SelectionOverlayState.IsLoupeVisible</c> が単一所有する。
    /// </remarks>
    private void UpdateLoupePosition(EsPoint sourcePos)
    {
        _loupe.CenterPoint = sourcePos;
        UpdateLoupeVisibility();
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
    /// 本コントロールは <c>ScrollViewer</c> の自動連携を行わないため、ホストが表示の変化を検知した際に呼ぶ。<br/>
    /// 選択矩形（対象座標）は表示の変化で不変のため再同期は行わず、再描画（と配置の再評価）のみを要求する。
    /// </remarks>
    public void NotifyViewportChanged()
    {
        _marquee.InvalidateRender();
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
        _marquee.Measure(availableSize);
        _loupe.Measure(availableSize);
        return Size.Empty;
    }

    /// <summary>子コントロールを配置する（ドラッグ選択は全面、ルーペは中心 + オフセット）。</summary>
    /// <param name="bounds">本コントロールへ割り当てられた矩形（ウィンドウ絶対座標・DIP）。</param>
    /// <remarks>
    /// ルーペの表示サイズ・位置はコア（ルーペコントロールのファサード）が単一所有する<br/>
    /// （親領域の最小辺へのクランプ・オフセット・親領域内へのクランプ。 親サイズが 0 の場合の扱いもコアが判断する）。<br/>
    /// 非表示（未表示・画像未設定・中心未設定）の場合はサイズ 0 で配置する。
    /// </remarks>
    protected override void ArrangeContent(Rect bounds)
    {
        _marquee.Arrange(bounds);

        var layout = _loupe.IsVisible ? _loupe.ResolveLayout(bounds.Size.ToEsSize()) : default;
        if (layout.Position is not { } position)
        {
            _loupe.Arrange(new Rect(bounds.X, bounds.Y, 0, 0));
            return;
        }

        _loupe.Arrange(new Rect(bounds.X + position.X, bounds.Y + position.Y, layout.ClientSize, layout.ClientSize));
    }

    /// <summary>設定とモードを内包コントロールへ分配する。</summary>
    /// <remarks>
    /// 値の決定（設定の写しとモードから導出する項目の上書き）は <see cref="SelectionOverlayChildValues"/>（本層）が<br/>
    /// 単一所有する（モードの解釈はコアが単一所有する）。<br/>
    /// ハイライト（ホストが与える実行時状態）は設定値に含まれないため、現在値を渡して引き継ぐ。<br/>
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

        // 選択矩形を使わないモードでも暗転マスク（ハイライト）は描画するため、描画は止めず入力だけを止める
        _marquee.IsHitTestVisible = values.IsHitTestVisible;

        UpdateLoupeVisibility();

        _marquee.InvalidateRender();
        // ルーペは描画のたびに選択矩形の破線を組み立てるため、破線色の変更は再描画で反映される
        _loupe.InvalidateVisual();
        InvalidateArrange();
    }

    /// <summary>ルーペの表示可否をモード・拡大元画像・中心から更新する。</summary>
    /// <remarks>表示可否の判断はコアの <c>SelectionOverlayState.IsLoupeVisible</c> が単一所有する。</remarks>
    private void UpdateLoupeVisibility()
    {
        _loupe.IsVisible = SelectionOverlayState
            .Resolve(_mode)
            .IsLoupeVisible(_loupe.Source is not null, _loupe.CenterPoint is not null);
        InvalidateArrange();
    }

    /// <summary>描画完了時にホスト固有の付加情報（操作ラベル・情報ラベル）を描画する。</summary>
    /// <param name="sender">イベント送信元（選択矩形コントロール）。</param>
    /// <param name="e">描画先キャンバスと描画内容。</param>
    /// <remarks>
    /// サイズラベルとカーソル座標ラベルは選択矩形コントロールがテキスト要素として配置する。<br/>
    /// <b>操作ラベル・情報ラベルは選択コントロールの概念ではない</b>（アプリ固有の付加情報）ため、<br/>
    /// 本クラス（ホスト）が文言の生成と描画を行い、<see cref="SelectionOverlayLabelRenderer"/>（本層）が配置指示（積み上げの位置と表示可否）を単一所有する。<br/>
    /// <b>描画は本クラスが行う</b>（描画 API は UI フレームワーク固有のため、提示層は配置指示までを返す）。<br/>
    /// 本ハンドラは図形（コントロールの子の描画キャンバス）の描画中に呼ばれるため、付加情報は<br/>
    /// <b>図形の上・ラベル要素（子の末尾側）の下</b>に置かれる（付加情報はラベルと重ならない位置に置くことが前提）。
    /// </remarks>
    private void OnMarqueeRendered(object? sender, MarqueeRenderedEventArgs e)
    {
        var placements = SelectionOverlayLabelRenderer.Resolve(
            e.Content,
            _options.Labels,
            MarqueeAppearance,
            OperationTextProvider?.Invoke(),
            InfoTextProvider?.Invoke(),
            SkiaText.Measure);

        foreach (var placement in placements)
        {
            DrawLabel(e.Canvas, in placement);
        }
    }

    /// <summary>付加情報ラベル 1 件を canvas へ描く（背景を塗ってから文字を描く）。</summary>
    /// <param name="canvas">描画先の canvas。</param>
    /// <param name="placement">配置指示（寸法は実寸を反映済み）。</param>
    private static void DrawLabel(SKCanvas canvas, in MarqueeLabelPlacement placement)
    {
        var bounds = placement.DipRect;
        var textOrigin = new EsPoint(bounds.X + placement.PaddingX, bounds.Y + placement.PaddingY);
        SkiaLabel.Draw(
            canvas, bounds, placement.BackgroundColor, placement.Text, textOrigin,
            placement.FontSize, placement.TextColor, cornerRadius: 0);
    }

    /// <summary>ルーペの描画完了時に、選択矩形の破線オーバーレイを同一のキャンバスへ描画する。</summary>
    /// <param name="sender">イベント送信元（ルーペコントロール）。</param>
    /// <param name="e">描画先キャンバスと表示情報。</param>
    /// <remarks>
    /// 選択矩形の破線はルーペ内部では描画せず、本クラス（ホスト側）が描画完了直後にキャンバスへ追加描画する<br/>
    /// （破線の幾何はコアの <c>SelectionOutlineRasterizer</c> が単一所有し、Avalonia 版と同じ見た目になる）。<br/>
    /// 切り出し領域の左上が未設定（中心未設定）の場合は破線を描画しない。<br/>
    /// <b>中心座標ラベルはルーペコントロールがテキスト要素として表示する</b>（本クラスは破線だけを描く）。
    /// </remarks>
    private void OnLoupeRendered(object? sender, LoupeRenderedEventArgs e)
    {
        if (e.Content.CaptureTopLeft is { } topLeft)
        {
            DrawSelectionOverlay(e.Canvas, topLeft, e.Content.ZoomLevel, e.Content.ClientPixelSize);
        }
    }

    /// <summary>ルーペ画像上の選択矩形破線をキャンバスへ描画する。</summary>
    /// <param name="canvas">描画先のキャンバス（ピクセル等倍で描画できる状態であること）。</param>
    /// <param name="captureTopLeft">ルーペの切り出し領域の左上（対象座標系）。</param>
    /// <param name="scale">対象座標 → ルーペ画像ピクセル座標の変換係数。</param>
    /// <param name="clientPixelSize">ルーペ画像の一辺のサイズ（ピクセル）。 切り出し領域との交差判定に使用する。</param>
    /// <remarks>
    /// 選択矩形（<see cref="SelectionSourceRect"/> = 対象座標）は <c>(対象座標 − captureTopLeft) × scale</c> で<br/>
    /// ルーペ画像ピクセル座標へ変換する（変換と破線の幾何はコアが単一所有する）。<br/>
    /// 破線を描画しない場合（選択矩形を使わないモード・選択なし）は何もしない。
    /// </remarks>
    internal void DrawSelectionOverlay(SKCanvas canvas, EsPoint captureTopLeft, double scale, int clientPixelSize)
    {
        // 破線を描くかどうかはモードと選択矩形の有無（コアが単一所有する判断）で決まる
        if (!SelectionOverlayState.Resolve(_mode).ShouldDrawSelectionOverlay(_marquee.HasSelection))
        {
            return;
        }

        SelectionOverlaySkiaRenderer.DrawSelectionRect(
            canvas,
            _marquee.SelectionSourceRect,
            captureTopLeft,
            scale,
            clientPixelSize,
            _options.SelectionRectColorA,
            _options.SelectionRectColorB);
    }

    /// <summary>ルーペ位置の更新（ドラッグ選択からの通知）を処理する。</summary>
    /// <param name="sourcePos">ルーペ中心（対象座標）。</param>
    /// <remarks>
    /// 拡大元画像が未設定の間は表示しない（ホストが <see cref="SetLoupeSource"/> を呼んだ後、<br/>
    /// <see cref="SetLoupePosition"/> で表示を開始する）。<br/>
    /// ホストへの通知は追加制御（拡大元画像の設定等）のフックとして発行する。
    /// </remarks>
    private void OnLoupePositionMoved(EsPoint sourcePos)
    {
        UpdateLoupePosition(sourcePos);
        RaiseLoupePositionMoved(sourcePos);
    }

    // ====================================================================
    // イベント発火（ホストへの通知。 発火形式を一貫させるため private ヘルパーに集約）
    // ====================================================================

    /// <summary>確定要求イベントを発火する。</summary>
    /// <param name="rect">確定した選択矩形（対象座標）。</param>
    private void RaiseConfirmRequested(EsRect rect)
        => ConfirmRequested?.Invoke(this, new SelectionConfirmedEventArgs(rect));

    /// <summary>キャンセル要求イベントを発火する。</summary>
    private void RaiseCancelRequested()
        => CancelRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>ルーペ中心更新イベントを発火する。</summary>
    /// <param name="sourcePos">ルーペ中心（対象座標）。</param>
    private void RaiseLoupePositionMoved(EsPoint sourcePos)
        => LoupePositionMoved?.Invoke(this, new LoupePositionEventArgs(sourcePos));
}

