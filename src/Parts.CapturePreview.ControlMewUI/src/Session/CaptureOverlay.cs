using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using EsUtil.Platform.Core.Capture;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Platform.Core.Native;
using EsUtil.Platform.Core.Window;
using EsUtil.Platform.Core;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Capture;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Contract;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Crop;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Logging;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Presentation;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.SelectionOverlay;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Session;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Setting;
using EsUtil.SaveEvidence.Parts.CapturePreview.MewUI.SelectionOverlay;
using EsUtil.Ui.MewUI.Interop;
using EsUtil.Ui.MewUI.Interop.Imaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.MewUI.Session;

/// <summary>画面全体を覆うキャプチャ選択オーバーレイ（MewUI 版）。</summary>
/// <remarks>
/// Avalonia 版 <c>Parts.CapturePreview.ControlAvaloniaUI</c> の <c>CaptureOverlayWindow</c> に相当する。 撮影から選択・確定までの<br/>
/// 1 セッションを担い、確定した領域を <see cref="EsBitmap"/> として返す。<br/>
/// <b>1 セッション = 1 インスタンス</b>であり、生成・実行・破棄は Facade（<see cref="CaptureSessionControl"/>）が担う<br/>
/// （本クラスは公開しない。 排他の受付は Facade がコアの <c>CaptureSessionSlot</c> で行う）。<br/>
/// <br/>
/// <b>全体の流れ</b>:<br/>
/// 1. 設定に従ってオーナーウィンドウを画面外へ退避する（<see cref="CaptureSettings.HideOwnerWindow"/>）。<br/>
/// 2. 仮想スクリーン（全モニタ）を 1 枚の画像として取得する（<see cref="ICaptureService"/>）。<br/>
/// 3. 取得した画像を敷いたボーダーレス・最前面のウィンドウを仮想スクリーン全面へ表示する。<br/>
/// 4. 選択を確定すると、選択矩形で画像を切り出して返す（Escape はキャンセル）。<br/>
/// 5. オーナーウィンドウを元の位置へ戻す。<br/>
/// <br/>
/// <b>座標系</b>: 対象 = 画像ローカルピクセル（画像の左上が原点）。 撮影範囲の原点（仮想スクリーンの原点。<br/>
/// 負値になりうる）が画像の (0, 0) に対応するため、画面物理ピクセルとは原点の分だけ異なる。<br/>
/// 画像ローカルとローカル DIP の変換は <see cref="ICoordTransform"/> が担い、<br/>
/// 選択コントロールへ注入する（コントロール側は座標系を意識しない）。 要素の検出とマウス入力は画面物理ピクセルで扱う。<br/>
/// <b>切り出し矩形の求解は画面物理座標に統一する</b>（選択矩形（画像ローカル）は確定の入口で <c>ToScreenRect</c> により戻す）。<br/>
/// <br/>
/// <b>モード</b>:<br/>
/// - 範囲選択（<see cref="CaptureType.AreaSelect"/>）: ドラッグで矩形を指定する（<c>SelectionOverlayControl</c> の範囲選択モード）。<br/>
/// - スクリーン選択 / ウィンドウ選択: カーソル下の要素をハイライトし、左ボタンの解放または Enter で確定する。<br/>
/// <br/>
/// <b>入力</b>: Enter = 確定 / Escape = キャンセル。 範囲選択は矢印キーでの微調整にも対応する（コントロール側の機能）。
/// </remarks>
internal sealed class CaptureOverlay
{
    /// <summary>ロガー（ホストが注入する。 null の場合は出力しない）。</summary>
    private readonly ILogger _log;

    /// <summary>選択確定の結果（キャンセル時は null）。</summary>
    /// <remarks>
    /// 結果の単一確定（確定とキャンセルの競合で二重完了にしない）はコアの <see cref="CaptureCompletion{TResult}"/> が単一所有する<br/>
    /// （UI 側でフィールドと null 判定を書かない）。<br/>
    /// 結果の型はコア型（<see cref="CaptureResult"/>）とし、UI 型は境界より外へ出さない（Avalonia 版と同じ契約）。
    /// </remarks>
    private readonly CaptureCompletion<CaptureResult?> _result = new();

    /// <summary>取得したスクリーン画像（読み取りの面。 セッション中のみ保持し、終了時に破棄する）。</summary>
    /// <remarks>
    /// <see cref="RunAsync"/> の冒頭で設定する（それ以外では null）。<br/>
    /// 撮影が完了した画像は以降書き換えないため、凍結して 0 コピーで共有した読み取り専用の面を保持する。
    /// </remarks>
    private EsReadOnlyBitmap? _screen;

    /// <summary>取得対象のスクリーンの矩形（画面物理ピクセル）。<see cref="RunAsync"/> の冒頭で設定する。</summary>
    private EsRectInt _screenBounds;

    /// <summary>取得したスクリーン画像を取得する。</summary>
    /// <value>撮影した仮想スクリーンの画像（読み取りの面）。</value>
    /// <remarks>参照は <see cref="RunAsync"/> が設定した後に限られる（撮影はセッションの実行の一部である）。</remarks>
    private EsReadOnlyBitmap Screen => _screen!;

    /// <summary>ローカル DIP → 画像ピクセルの倍率。</summary>
    /// <remarks>
    /// 表示に使うウィンドウの DPI で決まるため、ウィンドウの生成後に <see cref="ShowAsync"/> で確定する。<br/>
    /// 確定するまでは基準値（DIP と画像ピクセルが 1:1）を使う。
    /// </remarks>
    private double _scale = DpiScaler.StandardScaling;

    /// <summary>モニターごとの表示倍率（モニター矩形と DPI スケールの組）。</summary>
    /// <remarks>
    /// サイズラベルの表示倍率（DisplayScale）に使う。 仮想スクリーン全体を 1 枚のウィンドウで覆うと<br/>
    /// ウィンドウの倍率は 1 つに定まるため、モニターごとの実倍率は位置から解決する必要がある。<br/>
    /// セッション中はモニター構成が変わらないため、<see cref="ShowAsync"/> で一度だけ解決する。
    /// </remarks>
    private IReadOnlyList<(EsRectInt Bounds, double Scale)> _monitorScales = [];

    /// <summary>選択コントロール（暗転マスク + 選択またはハイライトの描画）。</summary>
    private readonly SelectionOverlayControl _selector;

    /// <summary>カーソル下の要素を検出するサービス（ハイライト型（スクリーン選択 / ウィンドウ選択）のみ使用する）。</summary>
    private readonly IElementFinder _finder;

    /// <summary>キャプチャ種別（操作ラベル・情報ラベルの文言に影響する）。</summary>
    private readonly CaptureType _type;

    /// <summary>カーソル下の要素のハイライト状態（対象座標への変換・表示名・ログをコアが単一所有する）。</summary>
    private readonly CaptureHighlightSession _highlight;

    /// <summary>クリック確定の固定（押下で固定・解放で確定。 退化した検出は固定しない規則をコアが単一所有する）。</summary>
    private readonly CaptureClickSelection _clickSelection = new();

    /// <summary>セッションの状態（再入禁止・完了は 1 回だけ）。</summary>
    /// <remarks>終了後に非アクティブ化・閉じるが重なっても二重終了（二重ログ）にしない。</remarks>
    private readonly CaptureSessionState _state = new();

    /// <summary>設定（ラベルの再組み立てに使用する）。</summary>
    private readonly CaptureSettings _settings;

    /// <summary>表示中のオーバーレイ ウィンドウ（未表示の場合は null）。</summary>
    /// <remarks>
    /// 閉じる対象をフィールドで保持する。 <c>Application.Current.AllWindows</c> の末尾を使うと、<br/>
    /// 復帰時にオーナーを前面へ出した（<c>Activate</c>）後では末尾がオーナーになり、**アプリを閉じてしまう**。
    /// </remarks>
    private Window? _overlayWindow;

    /// <summary>オーナーウィンドウ（復帰後の描画待ちに使う。 <see cref="RunAsync"/> が設定する）。</summary>
    private Window? _ownerWindow;

    /// <summary>オーナーの退避と復帰のスコープ（<see cref="RunAsync"/> が設定する）。</summary>
    /// <remarks>
    /// 復帰はウィンドウを閉じる<b>前</b>に行う（オーバーレイが画面を覆っている間に行うことで、<br/>
    /// 閉じた瞬間にデスクトップが見えるのを防ぐ）。 破棄は <see cref="Finish"/> と <see cref="RunAsync"/> の<br/>
    /// 両方から呼ばれるが、二重呼び出しは無害（コアのスコープが 1 回だけ実行する）。
    /// </remarks>
    private CaptureOwnerScope? _ownerScope;

    /// <summary>キャプチャ完了の通知先（結果をホストへ中継する）。</summary>
    /// <remarks>
    /// 完了の通知はオーナーの再表示より<b>前</b>に行う（ホストが画像を差し替えてから再表示する順序を保証する。<br/>
    /// Avalonia 版の <c>CaptureOverlaySession</c> が同じ順序を <c>onCaptured</c> で保証している）。<br/>
    /// 未指定（null）の場合は通知しない。
    /// </remarks>
    private readonly Action<CaptureResult?>? _onCaptured;

    /// <summary>オーバーレイを初期化する（内部用。<see cref="CaptureSessionControl.StartCaptureAsync"/> から生成する）。</summary>
    /// <param name="type">キャプチャ種別。</param>
    /// <param name="settings">設定（選択枠・暗転・ルーペ・ラベルの外観）。</param>
    /// <param name="finder">要素検出サービス（スクリーン選択 / ウィンドウ選択で使用）。</param>
    /// <param name="log">ロガー（null の場合は出力しない）。</param>
    /// <param name="onCaptured">キャプチャ完了（確定・キャンセル）の通知先。 未指定の場合は通知しない。</param>
    /// <remarks>
    /// 撮影（画像の取得）は行わない（排他の受付を撮影より先に済ませるため、生成と実行を分ける）。<br/>
    /// 画像は <see cref="RunAsync"/> の冒頭で取得して設定する。
    /// </remarks>
    internal CaptureOverlay(
        CaptureType type, CaptureSettings settings, IElementFinder finder, ILogger? log,
        Action<CaptureResult?>? onCaptured = null)
    {
        _onCaptured = onCaptured;
        _type = type;
        _settings = settings;
        _log = log ?? NullLogger.Instance;

        _selector = new SelectionOverlayControl
        {
            Mode = CaptureLabelTexts.ToSelectionMode(type),
            Log = _log,         // 設定はコアのマッパーで 1 値に変換して一括適用する（変換はコアが単一所有する）。
                                // 色は描画にだけ必要な設定のため、描画の設定として与える
                                // （ルーペの下地色は透明にし、背後の撮影画像を透かす。 Avalonia 版と同じ扱いにする）
            Options = settings.ToSelectionOverlayOptions(),
            MarqueeAppearance = CapturePaintMapper.ToMarqueeAppearance(settings),
            LoupeAppearance = CapturePaintMapper.ToLoupeAppearance(settings)
        };
        _highlight = new CaptureHighlightSession(_log);
        // 検出サービスはハイライト型のみ使う（それ以外はドラッグで選択矩形を作るため検出が不要）。
        // 「どちらの種別か」の判定は利用時にコア（CaptureTypeTraits）へ問い合わせる（導出値を null 性で保持しない）
        _finder = finder;
    }

    /// <summary>確定の対象となる選択矩形（画像ピクセル）を解決する。</summary>
    /// <returns>確定対象の矩形。 対象が無い場合は null。</returns>
    /// <remarks>
    /// 「ハイライト型はハイライト矩形・それ以外は選択矩形」という選択スタイルの判断はコア
    /// （<see cref="CaptureTypeTraits.ResolveRect"/>）が単一所有する。<br/>
    /// ハイライト矩形はコアのハイライト状態（<see cref="CaptureHighlightSession"/>）から読む
    /// （選択コントロールから読み戻すと、同じ値が 2 箇所に存在する）。
    /// </remarks>
    private EsRect? ResolveSelection()
        => CaptureTypeTraits.ResolveRect(
            _type,
            _highlight.SourceRect,
            _selector.HasSelection ? _selector.SelectionSourceRect : null);

    /// <summary>撮影セッションを実行し、確定した領域を返す。</summary>
    /// <param name="owner">オーナーウィンドウ（撮影中は画面外へ退避し、終了後に戻す）。</param>
    /// <param name="services">アプリ全体で共有するコアオブジェクト。</param>
    /// <returns>撮影結果（切り出した画像と領域）。 キャンセル・取得失敗・退化した選択の場合は null。</returns>
    /// <remarks>
    /// 1. 設定に従ってオーナーをキャプチャ除外に設定し、画面外へ退避する（設定: メインウィンドウを隠す）。<br/>
    /// 2. 仮想スクリーン（全モニターを包含する領域）を 1 枚の画像として取得する。<br/>
    /// 3. オーバーレイを表示し、確定またはキャンセルまで待つ。<br/>
    /// 4. 取得した画像を破棄してオーナーを元の位置へ戻し、キャプチャ除外を解除する。<br/>
    /// <b>排他</b>: 撮影と切り出しは同時に 1 つだけ実行できる。 受付は Facade<br/>
    /// （<see cref="CaptureSessionControl"/>）がコアの <c>CaptureSessionSlot</c> で行う（本クラスは受付を判定しない）。<br/>
    /// <b>自ウィンドウの写り込み防止</b>: 撮影の前にウィンドウを画面外へ退避させ、併せて<br/>
    /// <c>SetCaptureExclusion</c>（<c>WDA_EXCLUDEFROMCAPTURE</c>）を設定する。<br/>
    /// <b>チラつきの防止</b>: 退避は非表示（<c>Hide</c>）ではなく位置の変更とし、復帰時に<br/>
    /// ウィンドウを再作成させない（Avalonia 版と同じ方式）。<br/>
    /// <b>対象範囲</b>: 全モニター（仮想スクリーン）を対象にする。 1 モニターに限定すると、<br/>
    /// カーソルのあるモニターでしか選択できないため（Avalonia 版と同じ挙動）。
    /// </remarks>
    internal async Task<CaptureResult?> RunAsync(Window owner, CaptureOverlayServices services)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _ownerWindow = owner;

        // オーナーの退避とキャプチャ除外の設定・解除は CaptureOwnerScope（コア）が単一所有する
        // （手順: 除外の設定 → 画面外へ退避 → 撮影 → 配置の復帰 → 除外の解除。 Avalonia 版と同じ）
        using var ownerScope = CaptureOwnerScope.Enter(
            services.Window, owner.ToEsPlatformHandle(), new OwnerWindowAdapter(owner),
            hideOwner: _settings.HideOwnerWindow!.Value);
        // 復帰をウィンドウのクローズ前に行えるよう、スコープを保持する（破棄は Finish が行う）
        _ownerScope = ownerScope;

        // 全モニターを包含する 1 枚の画像として取得する（Avalonia 版と同じ）。
        // 1 モニターに限定すると、カーソルのあるモニターでしか選択できない（別のモニターへ
        // 移動しての選択操作ができない）ため、仮想スクリーン全体を対象にする。
        var screen = services.Capture.CaptureVirtualScreen(out var origin);
        if (screen is null)
        {
            return null;
        }

        // 撮影が完了した画像は以降書き換えないため、凍結して読み取り専用の面へ 0 コピーで引き渡す
        // （画素は読み取り専用の面が最後の 1 つとして解放する）
        screen.SetReadOnly();
        _screen = EsReadOnlyBitmap.FromBitmap(screen);
        screen.Dispose();
        // 画像の占有範囲（画面物理座標）= 撮影時の原点 + 画像サイズ（原点は負値になりうる）
        _screenBounds = CropClipMath.ImageBounds(Screen, origin);
        _log.LogCaptureStart(_type);
        // 撮影範囲の原点とサイズを記録する（書式はコアが単一所有する。 Avalonia 版と同じ診断情報を残す）
        _log.LogVirtualScreen(origin, Screen);

        try
        {
            // 確定・キャンセルまで待つ（入れ子ループを作らない非同期の待機。 ホストの継続は呼び出し側へ戻る）
            await ShowAsync(owner, services, ownerScope).ConfigureAwait(true);
            // 完了の記録（確定は 2 件・キャンセルは 1 件）とホストへの通知は Finish が行う
            // （ウィンドウのクローズ前に済ませる。 画像変更 → オーナーの復帰 → クローズの順）。
            // 結果の読み出しは待機で行う（完了済みのタスクを同期的に読むと、UI スレッドをブロックしうる）
            var result = await _result.Task.ConfigureAwait(true);
            return result;
        }
        finally
        {
            _ownerScope = null;
            _ownerWindow = null;
            // 画像はセッションの終了時に破棄する（以降の描画・切り出しは発生しない）
            _screen?.Dispose();
            _screen = null;
        }
    }

    /// <summary>キャプチャ結果をホストへ通知し、オーナーを復帰させる（ウィンドウを閉じる前に呼ぶ）。</summary>
    /// <param name="result">キャプチャ結果（キャンセル時は null）。</param>
    /// <remarks>
    /// 画像変更（ホスト）とオーナーの復帰をクローズ前に済ませることで、<br/>
    /// オーバーレイが消えた後にデスクトップや旧画像が一瞬見えるのを防ぐ（Avalonia 版と同じ順序）。<br/>
    /// 完了の記録は本メソッドが単一所有する（確定・キャンセルの経路からそれぞれ呼ばない）。
    /// </remarks>
    private void Finish(CaptureResult? result)
    {
        _log.LogCaptureFinished(result);
        try
        {
            _onCaptured?.Invoke(result);
        }
        finally
        {
            // 通知が例外を投げても、オーナーの復帰は必ず行う（オーバーレイが画面を覆っている間に行う）
            _ownerScope?.Dispose();
        }
    }

    /// <summary>オーナーウィンドウ（MewUI）を撮影時に画面外へ退避するポート実装。</summary>
    /// <param name="window">対象のウィンドウ。</param>
    /// <remarks>
    /// 非表示（<c>Hide</c>）は復帰時の再作成でちらつくため使わず、位置の変更（画面外配置）で退避する。<br/>
    /// 復帰時は前面へ戻す（撮影後にユーザーの操作対象へ戻すため。 Avalonia 版はオーバーレイの閉じる操作が担う）。
    /// </remarks>
    private sealed class OwnerWindowAdapter(Window window) : ICaptureOwnerWindow
    {
        /// <summary>退避前の位置（未退避の場合は null）。</summary>
        private Point? _savedPosition;

        /// <inheritdoc/>
        public bool IsVisible => window.IsVisible;

        /// <inheritdoc/>
        public void HideOffScreen()
        {
            _savedPosition = window.Position;

            // 画面外（仮想スクリーン座標の外）へ退避する。 負の大きな座標は Windows で
            // 標準的に使われる「画面外配置」であり、ヒットテスト・検出の対象外になる。
            // 値（画面外座標）はコアが単一所有する（Avalonia 版と同じ値を使う）
            var offScreen = CaptureTiming.OwnerOffScreenCoord;
            window.MoveTo(offScreen, offScreen);
        }

        /// <inheritdoc/>
        public void RestorePlacement()
        {
            if (_savedPosition is not { } position)
            {
                return;
            }

            window.MoveTo(position.X, position.Y);
            // 復帰後の内容を描き直す（オーバーレイを閉じた瞬間に旧画像が見えるのを防ぐ）
            window.InvalidateVisual();
            window.Activate();
            _savedPosition = null;
        }
    }

    /// <summary>画面外で画像を描き終えるまでに待つフレーム数。</summary>
    /// <remarks>
    /// ウィンドウを表示した直後の 1 フレーム目は下地（背景色）だけで、撮影画像は次のフレームで合成される（実測）。<br/>
    /// 画面外に置いたまま 2 フレーム待つことで、画面内へ移した時点で画像が載っている（= 下地の色が一瞬見えない）。
    /// </remarks>
    private const int FramesBeforePlace = 2;

    /// <summary>画面内へ移してからオーナーを退避するまでに待つフレーム数。</summary>
    /// <remarks>移した直後に退避すると、退避の瞬間のデスクトップが 1 フレーム見える。</remarks>
    private const int FramesBeforeHideOwner = 1;

    /// <summary>オーナーの再描画を待つフレーム数。</summary>
    /// <remarks>画像を差し替えただけでは合成されていないため、待たずに閉じると旧画像が 1〜2 フレーム見える。</remarks>
    private const int OwnerPaintFrames = 2;

    /// <summary>画面内へ出した後、画像の転送完了を待つ余裕（時間）。</summary>
    /// <remarks>画像の GPU への転送が描画フレームに遅れる環境があるため、短い余裕を置く。</remarks>
    private static readonly TimeSpan s_revealSettleDelay = TimeSpan.FromMilliseconds(20);

    /// <summary>描画フレームを待つ上限（1 回分）。</summary>
    /// <remarks>
    /// 画面外・最小化では合成が走らないことがあるため、待ち続けずに打ち切る（通常のフレーム間隔より十分に長く取る）。
    /// </remarks>
    private static readonly TimeSpan s_frameTimeout = TimeSpan.FromMilliseconds(200);

    /// <summary>オーバーレイを画面内へ出す前に、画面外での描画を待つ上限。</summary>
    /// <remarks>
    /// <see cref="s_frameTimeout"/> より短くするのは、**待てない環境でデスクトップが見える時間を延ばさない**ためである<br/>
    /// （画面外では合成が走らないことがあり、その場合は待っても画像は描かれない。 待つほど「アプリが消えてから<br/>
    /// 暗転が掛かるまで」の間が伸び、ちらつきとして見える。 60 ms は 60 Hz の 2 フレーム強に相当する）。
    /// </remarks>
    private static readonly TimeSpan s_revealFrameTimeout = TimeSpan.FromMilliseconds(60);

    /// <summary>画面外で画像を描き終えたかどうか（画面内へ移したか）。</summary>
    private bool _placed;

    /// <summary>オーナーの退避を実行したかどうか。</summary>
    private bool _ownerHidden;

    /// <summary>画像の平均色を求める。</summary>
    /// <param name="image">対象の画像。</param>
    /// <returns>平均色（不透明）。</returns>
    /// <remarks>
    /// 標本の間引きと「不透明として返す」規則はコア（<see cref="ImageColorMath"/>）が単一所有する<br/>
    /// （本メソッドはコアの色型を MewUI の色型へ写すだけ。 変換は Platform 層の
    /// <c>ToMewUIColor</c> が単一所有する）。
    /// </remarks>
    private static Color AverageColor(EsReadOnlyBitmap image)
        => ImageColorMath.AverageColor(image).ToMewUIColor();

    /// <summary>オーバーレイを表示し、確定またはキャンセルまで待つ。</summary>
    /// <param name="owner">オーナーウィンドウ。</param>
    /// <param name="services">オーバーレイの実行に必要なサービス（撮影サービス・ウィンドウ操作）。</param>
    /// <param name="ownerScope">オーナーの退避とキャプチャ除外のスコープ（退避の実行時期を本メソッドが決める）。</param>
    /// <returns>ウィンドウが閉じるまで待機するタスク。</returns>
    /// <remarks>
    /// ウィンドウは仮想スクリーンの位置・サイズでボーダーレス・最前面に表示する。<br/>
    /// 確定・キャンセルのいずれでもウィンドウを閉じ、待機タスクが完了する。<br/>
    /// 待機は <c>ShowDialogAsync</c>（入れ子ループを作らない）を使う。 入れ子ループ（<c>ShowDialog</c>）では<br/>
    /// セッションの開始から終了までの間、呼び出し側のコードが進まない（Facade がセッションの生成・破棄と<br/>
    /// 排他の解放を担えないため、Avalonia 版と契約が食い違う）。
    /// </remarks>
    private async Task ShowAsync(Window owner, CaptureOverlayServices services, CaptureOwnerScope ownerScope)
    {
        ArgumentNullException.ThrowIfNull(owner);

        // セッションを開始する（完了は 1 回だけ受け付ける）
        _state.TryBegin();

        // モニターごとの実倍率を解決する（サイズラベルの DisplayScale 用。 構成はセッション中に変わらない）。
        // ハイライト型のみがサイズラベルの表示倍率を使う（ドラッグ選択は対応するモニターを持たない）。
        // 倍率が未確定のモニターの除外はコア（MonitorScalingMath）が単一所有する
        if (CaptureTypeTraits.IsHighlighted(_type))
        {
            _monitorScales = MonitorScalingMath.Collect(services.Finder.ListScreens().Select(screen =>
                (screen.Bounds,
                 // DPI 照会は MewUI の座標を要求するため、境界変換は Interop へ委譲する
                 DpiScaler.ToScaling(DpiHelper.GetDpiForPoint(screen.Bounds.TopLeft.ToMewUIPoint())))));
        }
        var content = new Grid().Columns("*").Rows("*");
        // 撮影画像は仮想スクリーンと等倍（領域と同じ縦横比）のため、領域全体へ敷く（Uniform にしない）
        content.Add(new ImageView { Source = Screen });

        var window = new Window()
            .Borderless()
            .Topmost()
            .Content(content);
        _overlayWindow = window;
        // 閉じられたら保持を解除する（次回のセッションで別のウィンドウを閉じる）
        window.Closed += () => _overlayWindow = null;

        // 撮影画像の平均色を下地にする（万一ウィンドウが画像より先に合成されても、その 1 フレームが
        // 明るい面として目立たないようにする。 画面外で描き終えてから移すため、通常は見えない）。
        window.Background = AverageColor(Screen);

        // **画面外に置いた状態で表示し、画像を描き終えてから画面内へ移す**
        // （画面内で表示してから画像を合成すると、合成前の 1〜2 フレームに下地の色やデスクトップが見える）。
        // 大きさは変えずに位置だけ動かすのは、大きさを変えると再レイアウトが走り、
        // その瞬間にウィンドウの下地が 1 フレーム見えるためである（WinUI 3 版と同じ方式）。
        // ウィンドウが表示されてから動かす（表示前に動かすと、表示時の配置（画面外）に戻される）
        window.Loaded += () => _ = RevealAsync(window, services, ownerScope);

        // 撮影中の一時的な表示のため、タスクバー・Alt+Tab の一覧には出さない
        // （Avalonia 版は HideFromTaskSwitcher で除外する）
        window.ShowInTaskbar = false;

        // 十字カーソルはドラッグで矩形を作る種別（ハイライト型以外 = 範囲選択）で使う
        // （選択スタイルの判定はコアが単一所有する。 Avalonia 版と同じ見え方にする）
        if (!CaptureTypeTraits.IsHighlighted(_type))
        {
            window.Cursor = CursorType.Cross;
        }

        // ウィンドウ余白（Window 既定スタイルの ContainerPadding）を 0 にして、
        // 撮影画像をウィンドウ端まで敷く（余白があると縁が太い枠のように見える）
        window.Padding = Thickness.Zero;

        // 位置とサイズの DIP は生成時のスケール（プライマリモニターの DPI）で解釈される（実測）。
        // 生成直後のちらつきを抑えるため、仮想スクリーン全体のサイズを先に与える。
        // 最大化は使わない（OS が 1 モニターへ寄せてしまうため、全モニターを覆えない）。
        var creationScale = DpiScaler.ToScaling(DpiHelper.GetDpiForPoint(new Point(0, 0)));
        window.WindowSize = WindowSize.Resizable(
            EsWindowPlacementMath.ToDipLength(Screen.Width, creationScale),
            EsWindowPlacementMath.ToDipLength(Screen.Height, creationScale));

        // 画面外（仮想スクリーンの外）に置いて表示する（画面内に出すのは画像を描き終えてから）
        var offScreen = EsWindowPlacementMath.ToDipPoint(
            new EsPointInt(CaptureTiming.OwnerOffScreenCoord, CaptureTiming.OwnerOffScreenCoord),
            creationScale);
        window.StartManualPosition(offScreen.X, offScreen.Y);

        _selector.SetLoupeSource(Screen);
        ApplyLabels();
        content.Add(_selector);

        // 確定・キャンセルは選択コントロールからの要求とウィンドウのキー入力の両方から受け付ける
        _selector.ConfirmRequested += OnSelectorConfirmRequested;
        _selector.CancelRequested += (_, _) => Cancel();

        // 他のウィンドウへ切り替わった（非アクティブ化した）場合はキャンセルする
        // （最前面のオーバーレイが残り続けるのを防ぐ。 Avalonia 版と同じ扱い）
        window.Deactivated += Cancel;

        window.Loaded += () =>
        {
            // **表示された直後にもう一度だけ画面外へ置き直す**: ウィンドウは最初の表示で
            // 既定の位置・大きさ（画面中央）が適用されることがあり、その 1 フレームが
            // 「小さい画面」のちらつきになる（WinUI 3 版と同じ対策。 実測 2026-11-12）
            window.StartManualPosition(offScreen.X, offScreen.Y);

            // 画像ピクセル ↔ DIP の倍率は表示に使うウィンドウの DPI で決める
            // （ウィンドウのクライアント座標は物理ピクセルで届くため、この倍率で画像ピクセルへ戻せる。
            //  モニターごとに倍率が異なっても、ウィンドウの倍率は 1 つに定まるため全域で同じ式が使える）
            var scale = DpiScaler.ToScaling(DpiHelper.GetDpiForWindow(window.Handle));
            // 有効な係数の判定（0 以下は未確定）はコアが単一所有する
            if (DpiScaler.IsValidScaling(scale))
            {
                _scale = scale;
                _selector.SetCoordTransform(CreatePort());
            }
        };

        // 位置は生成時のスケールで解釈される（画面外に置いて表示し、画像を描き終えてから移す）
        window.StartManualPosition(offScreen.X, offScreen.Y);

        // Enter = 確定 / Escape = キャンセル（どのキーがどの操作かの解釈はコアが単一所有する）。
        // 確定対象が無い場合の Enter は何もしない（確定できない状態でキャンセル扱いにしない）
        window.PreviewKeyDown += e =>
        {
            // 確定対象は 1 度だけ解決して効果へ渡す（Avalonia 版と同じ形。 再解決しない）
            var target = ResolveSelection();
            switch (CaptureKeyActions.Resolve(e.Key.ToEsKey(), target is not null))
            {
                case CaptureKeyAction.Confirm when target is { } localRect:
                    // 選択矩形は画像ローカル座標のため、確定の入口（画面物理座標）へ揃えてから渡す
                    ConfirmScreen(CropClipMath.ToScreenRect(localRect, _screenBounds.TopLeft));
                    e.Handled = true;
                    break;
                case CaptureKeyAction.Cancel:
                    Cancel();
                    e.Handled = true;
                    break;
            }
        };

        if (CaptureTypeTraits.IsHighlighted(_type))
        {
            // スクリーン選択 / ウィンドウ選択: カーソル下の要素をハイライトし、クリックで確定する
            // （選択コントロールのドラッグ選択は表示されないため、入力はウィンドウが受け取る）
            window.MouseMove += e => Highlight(ToScreenPoint(e.ScreenPosition));

            // 押下で検出した要素を固定し、解放で確定する（固定と取り出しの規則はコアが単一所有する）
            window.MouseDown += e =>
            {
                if (e.Button != MouseButton.Left)
                {
                    return;
                }

                // 検出矩形（画面物理）を固定する。 退化した検出結果は固定されない
                _clickSelection.Fix(CaptureTypeTraits.FindUnderCursor(_finder, _type, ToScreenPoint(e.ScreenPosition)).Bounds);
            };
            window.MouseUp += e =>
            {
                if (e.Button != MouseButton.Left)
                {
                    return;
                }

                // 固定が無い解放では確定しない（押下を伴わない解放・退化した検出）
                if (_clickSelection.Take() is not { } fixedRect)
                {
                    return;
                }

                // 検出矩形は画面物理座標のため、変換せずそのまま確定する（入口の座標空間が同じ）
                ConfirmScreen(fixedRect);
            };
        }

        await window.ShowDialogAsync(owner).ConfigureAwait(true);
    }

    /// <summary>サイズラベル・情報ラベルの文言を選択コントロールへ反映する。</summary>
    /// <remarks>
    /// 1. サイズラベルは対象座標（＝画像ピクセル）で表示するため、テキストは描画のたびに生成する。<br/>
    /// 2. 外観（サイズラベル・サブライン・情報ラベルの色・サイズ）は設定値の変換
    ///    （<c>SelectionOverlayOptionsMapper.ToSelectionOverlayOptions</c>）が単一所有するため、本メソッドでは設定しない。<br/>
    /// 3. 描画モード（操作ラベルの文言選定）は複合コントロールの <see cref="SelectionOverlayControl.Mode"/> が単一所有する。
    /// 4. 操作ラベル・情報ラベルの文言をホスト（キャプチャ文脈）がプロバイダで与える（モード別の文言と情報ラベルの表示可否は
    ///    <c>CaptureLabelTexts</c> が単一所有する。 表示名はコアの <see cref="CaptureHighlightSession"/> を参照する）。
    /// 5. 表示する矩形の選択（ハイライト型 / ドラッグ型）はコア（<see cref="CaptureTypeTraits.ResolveRect"/>）が単一所有し、
    ///    サイズラベ ルの文言（書式）もコアが単一所有する（本ホストは表示スケールだけを与える）。
    /// 6. サイズラベルの表示可否はコア（<c>MarqueeOptions.ShowSizeLabel</c>）が単一所有するため本メソッドでは判定しない。
    /// </remarks>
    private void ApplyLabels()
    {
        // サイズラベルの文言（書式）はコアが単一所有し、本ホストは矩形を含むモニタの実表示スケール（マルチモニタ補正）だけを提供する。
        _selector.SizeLabelDisplayScalingProvider = DisplayScalingAt;

        // 操作ラベル・情報ラベルの文言はキャプチャコア（CaptureLabelTexts）が単一所有する
        _selector.OperationTextProvider = () => CaptureLabelTexts.GetOperationText(_selector.Mode);
        _selector.InfoTextProvider = () => CaptureLabelTexts.GetInfoText(_selector.Mode, _highlight.Title);
    }

    /// <summary>選択領域を切り出す（選択矩形は画面物理座標の浮動小数 = ドラッグ選択・キー操作）。</summary>
    /// <param name="screenRect">選択矩形（画面物理座標）。</param>
    /// <remarks>
    /// 切り出し矩形の決定（種別ごとの閉区間化・画像内へのクランプ・端数の丸め）はコアが単一所有する。<br/>
    /// <b>入口は画面物理座標に統一する</b>: 選択矩形（画像ローカル座標）は呼び出し側で
    /// <see cref="CropClipMath.ToScreenRect(EsRect, EsPointInt)"/> により戻してから渡す（占有範囲と同じ座標空間にする。
    /// 混ぜると原点が負値のマルチモニタで切り出し位置がずれ・幅が 0 になる）。<br/>
    /// 結果の領域は<see cref="CaptureResult.SourceBounds"/>（仮想画面座標）と同じ座標空間になる。
    /// </remarks>
    private void ConfirmScreen(EsRect screenRect)
    {
        _log.LogConfirmRequested(screenRect);
        CompleteCapture(CropClipMath.ResolveCaptureRect(_type, screenRect, _screenBounds));
    }

    /// <summary>選択領域を切り出す（選択矩形は画面物理座標の整数 = クリック確定の検出矩形）。</summary>
    /// <param name="screenRect">選択矩形（画面物理座標）。</param>
    /// <remarks>浮動小数版と同じ規則を適用する（コアに整数版の入口があり、UI は型を広げない）。</remarks>
    private void ConfirmScreen(EsRectInt screenRect)
    {
        _log.LogConfirmRequested(screenRect);
        CompleteCapture(CropClipMath.ResolveCaptureRect(_type, screenRect, _screenBounds));
    }

    /// <summary>解決済みの切り出し矩形でキャプチャを完了する。</summary>
    /// <param name="pixels">切り出し矩形（画面物理座標・閉区間済み）。 空の場合はキャンセルとして完了する。</param>
    /// <remarks>切り出し結果の所有権は呼び出し側へ移す（仮想スクリーン画像はセッション終了時に破棄される）。</remarks>
    private void CompleteCapture(EsRectInt pixels)
    {
        if (pixels.IsEmpty)
        {
            Cancel();
            return;
        }

        // 完了は 1 回だけ受け付ける（非アクティブ化・閉じる操作との競合で二重終了にしない）
        if (!_state.TryComplete())
        {
            return;
        }

        // 切り出し矩形は画面物理座標のため、画像の原点を渡して取り出す（座標変換はコアが単一所有する）
        var image = CropClipMath.Extract(Screen, _screenBounds.TopLeft, pixels);
        // 結果の確定（最初の 1 件だけを採用する）はコアが単一所有する。 領域は契約どおり仮想画面座標（画面物理）で渡す。
        CompleteWith(CaptureResult.From(image, pixels));
    }

    /// <summary>結果の通知・オーナーの復帰・ウィンドウのクローズを 1 か所で行う。</summary>
    /// <param name="result">キャプチャ結果（キャンセル時は null）。</param>
    /// <remarks>
    /// 通知（ホストの画像変更）が例外を投げても、オーナーの復帰（<see cref="Finish"/>）とクローズは必ず行う。<br/>
    /// 通知の失敗は待機側へ伝える（伝えないと <c>ShowAsync</c> の呼び出し元が完了しないままオーバーレイが残る）。
    /// </remarks>
    private void CompleteWith(CaptureResult? result)
    {
        try
        {
            Finish(result);
            _result.TrySetResult(result);
        }
        catch (Exception ex)
        {
            // 通知の失敗は待機側へ伝える（伝えないと ShowAsync の呼び出し元が完了しないまま残る）
            _result.TrySetException(ex);
            throw;
        }
        finally
        {
            // オーナーが新しい画像を描き終えてから閉じる
            _ = CloseAfterOwnerPaintedAsync();
        }
    }

    /// <summary>キャンセルする（切り出しを行わずに閉じる）。</summary>
    /// <remarks>
    /// Facade（<see cref="CaptureSessionControl"/>）が進行中のセッションを取り消す際にも呼ぶ<br/>
    /// （公開操作 <see cref="CaptureSessionControl.Cancel"/> と <see cref="CaptureSessionControl.Dispose"/> の効果。
    /// Avalonia 版の <c>CaptureOverlaySession.Cancel</c> と同じ）。
    /// </remarks>
    internal void Cancel()
    {
        if (!_state.TryComplete())
        {
            return;
        }

        // 選択のキャンセル要求とセッションの完了（キャンセル 1 件）はコアが単一所有する。
        // 画像変更（ホストのキャンセル通知）とオーナーの復帰はウィンドウのクローズ前に行う
        _log.LogCancelRequested();
        CompleteWith(null);
    }

    /// <summary>選択オーバーレイの確定要求（Enter キー・ダブルクリック等）を処理する。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">選択矩形イベント（矩形は対象座標 = 画像ピクセル）。</param>
    /// <remarks>
    /// 選択矩形（画像ローカル座標）を確定の入口（画面物理座標）へ揃えて渡す（記録と解決は入口が担う）。
    /// </remarks>
    private void OnSelectorConfirmRequested(object? sender, SelectionConfirmedEventArgs e)
        => ConfirmScreen(CropClipMath.ToScreenRect(e.SelectionSourceRect, _screenBounds.TopLeft));

    /// <summary>現在のオーバーレイ ウィンドウを閉じる（表示時に保持したウィンドウを使う）。</summary>
    private void CloseOverlay()
    {
        var overlay = _overlayWindow;
        _overlayWindow = null;
        overlay?.Close();
    }

    /// <summary>画面外で画像を描き終えてからオーバーレイを画面内へ出し、その後でオーナーを退避する。</summary>
    /// <param name="window">対象のオーバーレイ ウィンドウ。</param>
    /// <param name="services">ウィンドウ操作サービス。</param>
    /// <param name="ownerScope">オーナーの退避と復帰のスコープ。</param>
    /// <returns>表示が完了したときに完了するタスク。</returns>
    /// <remarks>
    /// <b>画面外で描く理由</b>: 画面内で表示すると、画像が合成されるまでの 1〜2 フレームに<br/>
    /// 下地の色（またはデスクトップ）が見える。 画面外で <see cref="FramesBeforePlace"/> フレーム描いてから<br/>
    /// 位置だけ動かすと、合成済みの面がそのまま出る。<br/>
    /// <b>フレームが来ない環境</b>（リモート デスクトップ等では画面外の合成が走らないことがある）でも<br/>
    /// 操作不能にならないよう、待ち時間には上限を設けて画面内へ出す。<br/>
    /// <b>セッションが先に完了した場合</b>（即座に確定・キャンセルされた場合）は何もしない。
    /// </remarks>
    private async Task RevealAsync(Window window, CaptureOverlayServices services, CaptureOwnerScope ownerScope)
    {
        // **待つ上限は短くする**: 画面外では合成が走らないことがあり、その場合は待っても画像は描かれない。
        // 待つほど「アプリが消えてから暗転が掛かるまで」の間が伸び、ちらつきとして見える
        await WaitForFramesAsync(window, FramesBeforePlace, s_revealFrameTimeout).ConfigureAwait(true);
        // 画像の転送（GPU へのアップロード）がフレームに遅れる環境向けの余裕
        await Task.Delay(s_revealSettleDelay).ConfigureAwait(true);

        if (_placed || _overlayWindow is null || _result.Task.IsCompleted)
        {
            return;
        }

        PlaceOnScreen(window, services);

        // 画面内へ移した次のフレームでオーナーを退避する（退避の瞬間を見せない）
        await WaitForFramesAsync(window, FramesBeforeHideOwner, s_revealFrameTimeout).ConfigureAwait(true);
        if (!_ownerHidden && _overlayWindow is not null)
        {
            _ownerHidden = true;
            ownerScope.ApplyPendingHide();
        }
    }

    /// <summary>オーバーレイを大きさを変えずに画面内（仮想スクリーン全面）へ移す。</summary>
    /// <param name="window">対象のオーバーレイ ウィンドウ。</param>
    /// <param name="services">ウィンドウ操作サービス。</param>
    /// <remarks>
    /// 大きさを変えずに位置だけ動かすのは、大きさを変えると再レイアウトが走り、その瞬間に<br/>
    /// ウィンドウの下地が 1 フレーム見えるためである（WinUI 3 版と同じ方式）。<br/>
    /// 論理座標（DIP）での配置は OS が作業領域内へ補正するため、物理ピクセルで置き直す。<br/>
    /// 失敗しても選択操作は成立しないが、描画フレームのコールバックを例外で抜けると以降のフレーム処理が止まるため、<br/>
    /// 境界で記録して続行する。
    /// </remarks>
    private void PlaceOnScreen(Window window, CaptureOverlayServices services)
    {
        _placed = true;
        try
        {
            services.Window.SetBounds(window.ToEsPlatformHandle(), _screenBounds);
        }
        catch (Exception ex) when (EsErrors.IsRecoverable(ex))
        {
            _log.LogOperationError(ex, nameof(PlaceOnScreen));
        }
    }

    /// <summary>オーナーが再描画されるまで待ってからオーバーレイを閉じる。</summary>
    /// <remarks>
    /// 画像の差し替えは次の描画フレームで合成されるため、待たずに閉じると、オーバーレイが消えた後に<br/>
    /// 旧画像が 1〜2 フレーム見える（= 再表示してから結果が出るまでのタイムラグ）。<br/>
    /// フレームが来ない環境では上限時間で打ち切る（閉じられないままにしない）。
    /// </remarks>
    private async Task CloseAfterOwnerPaintedAsync()
    {
        if (_ownerWindow is { } owner)
        {
            await WaitForFramesAsync(owner, OwnerPaintFrames).ConfigureAwait(true);
        }

        CloseOverlay();
    }

    /// <summary>指定したウィンドウの描画フレームを待つ（合計で上限時間）。</summary>
    /// <param name="window">対象のウィンドウ。</param>
    /// <param name="frameCount">待つフレーム数。</param>
    /// <param name="timeout">待つ上限（省略時は <see cref="s_frameTimeout"/>）。</param>
    /// <returns>待機が完了したときに完了するタスク。</returns>
    /// <remarks>
    /// 画面外・最小化では合成が走らないことがあるため、<see cref="s_frameTimeout"/> で打ち切る。<br/>
    /// 打ち切った場合は待機を続けない（閉じる操作を止めない）。
    /// </remarks>
    private static async Task WaitForFramesAsync(Window window, int frameCount, TimeSpan? timeout = null)
    {
        var remaining = frameCount;
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnFrame()
        {
            if (--remaining > 0)
            {
                return;
            }

            window.FrameRendered -= OnFrame;
            completed.TrySetResult();
        }

        window.FrameRendered += OnFrame;
        try
        {
            // 上限は合計で 1 回分（フレームが来ない環境で閉じる操作を待たせ続けない）
            await Task.WhenAny(completed.Task, Task.Delay(timeout ?? s_frameTimeout)).ConfigureAwait(true);
        }
        finally
        {
            window.FrameRendered -= OnFrame;
        }
    }

    /// <summary>カーソル下の要素を検出してハイライトを更新する。</summary>
    /// <param name="screenPoint">カーソル位置（画面物理ピクセル）。</param>
    /// <remarks>
    /// 検出対象はキャプチャ種別で決まる。 スクリーン選択はカーソル下のモニター（画面全体）、<br/>
    /// ウィンドウ選択はカーソル下の最前面ウィンドウを検出し、その矩形をハイライトする。<br/>
    /// 矩形の対象座標への変換・未検出の正規化・表示名の保持・ログはコア
    /// （<see cref="CaptureHighlightSession"/>）が単一所有する（本クラスは結果を設定するだけ）。
    /// </remarks>
    private void Highlight(EsPointInt screenPoint)
    {
        // 検出関数の呼び分けはコアが単一所有する（本メソッドはハイライト型のときだけ呼ばれる）
        var found = CaptureTypeTraits.FindUnderCursor(_finder, _type, screenPoint);
        var highlight = _highlight.Update(_type, found, _screenBounds.TopLeft);

        // 未検出は null で表す（null が「ハイライトしない」を意味する）
        _selector.HighlightSourceRect = highlight.SourceRect;

        // 情報ラベル（ウィンドウタイトル）の文言は描画時に評価されるため、表示名が変わった場合だけ再描画を要求する
        if (highlight.TitleChanged)
        {
            _selector.RequestRedraw();
        }
    }

    /// <summary>マウス位置（画面物理ピクセル）をそのままコア型へ変換する。</summary>
    /// <param name="position">画面物理ピクセルの座標。</param>
    /// <returns>画面物理ピクセルの座標（四捨五入）。</returns>
    /// <remarks>
    /// MewUI の <c>MouseEventArgs.ScreenPosition</c> はデバイスピクセル（＝画面物理ピクセル）で返るため、<br/>
    /// DPI スケールを掛けずにそのまま使える（要素検出サービスと同じ座標系）。<br/>
    /// 丸めはコア（<see cref="CropBoundsMath.RoundToPixel"/>）が単一所有する<br/>
    /// （既定の <see cref="Math.Round(double)"/> は銀行丸めで、座標の四捨五入と 1 ピクセルずれる）。
    /// </remarks>
    private static EsPointInt ToScreenPoint(Point position)
        => new(CropBoundsMath.RoundToPixel(position.X), CropBoundsMath.RoundToPixel(position.Y));

    /// <summary>選択コントロールへ注入する座標変換ポートを生成する。</summary>
    /// <returns>ローカル DIP ⇔ 画像ピクセルを変換するポート。</returns>
    /// <remarks>
    /// 画像はウィンドウのクライアント領域全体へ等倍で描画するため、変換は倍率の乗除算だけになる。<br/>
    /// 倍率の適用方向（ToSource = DIP → 画像ピクセル）は EsUtil.Platform.Core の <see cref="CoordTransform.ClientToSource(Func{double}, Func{EsSize?})"/> が<br/>
    /// 単一所有する（UI が倍率式を書かない）。 境界は画像サイズで、選択を画像外へ出さないためのクランプに使われる。
    /// </remarks>
    private ICoordTransform CreatePort()
        => CoordTransform.ClientToSource(
            () => _scale,
            () => CropClipMath.ImageSize(Screen));

    /// <summary>選択矩形（対象座標）が属するモニターの表示倍率を返す。</summary>
    /// <param name="sourceRect">選択矩形（画像ピクセル = 仮想スクリーン座標から原点を引いた値）。</param>
    /// <returns>そのモニターの DPI スケール（1.0 = 100%）。 該当が無い場合はウィンドウの倍率。</returns>
    /// <remarks>
    /// 対象座標を画面物理座標（原点を加算）へ戻してからモニターを判定する。<br/>
    /// 「原点の加算」「判定点（矩形の中心）」「フォールバック」の規則はコア（<see cref="MonitorScalingMath"/>）が単一所有する
    /// （UI ごとに判定点がずれると、同じ矩形で表示倍率が食い違う）。
    /// </remarks>
    private double DisplayScalingAt(EsRect sourceRect)
        => MonitorScalingMath.ResolveForSourceRect(
            sourceRect, _screenBounds.TopLeft, _monitorScales, _scale);
}




