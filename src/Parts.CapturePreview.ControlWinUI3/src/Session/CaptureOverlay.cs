using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Platform.Core.Native;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Capture;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Contract;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Crop;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Logging;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Presentation;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.SelectionOverlay;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Session;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Setting;
using EsUtil.SaveEvidence.Parts.CapturePreview.WinUI3.SelectionOverlay;
using EsUtil.SaveEvidence.Platform.WinUI3;
using EsUtil.SaveEvidence.Platform.WinUI3.Imaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.Graphics;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.WinUI3.Session;

/// <summary>画面全体を覆うキャプチャ選択オーバーレイ（WinUI 3 版）。</summary>
/// <remarks>
/// 他の UI 版の <c>CaptureOverlayWindow</c> に相当する。 撮影から選択・確定までの<br/>
/// 1 セッションを担い、確定した領域を <see cref="EsBitmap"/> として返す。<br/>
/// <b>1 セッション = 1 インスタンス</b>であり、生成・実行・破棄は Facade（<see cref="CaptureSessionControl"/>）が担う<br/>
/// （本クラスは公開しない。 排他の受付は Facade がコアの <see cref="CaptureSessionSlot{TCapture, TCrop}"/> で行う）。<br/>
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
/// 選択コントロールへ注入する（コントロール側は座標系を意識しない）。<br/>
/// <b>切り出し矩形の求解は画面物理座標に統一する</b>（選択矩形（画像ローカル）は確定の入口で <c>ToScreenRect</c> により戻す）。<br/>
/// <br/>
/// <b>モード</b>:<br/>
/// - 範囲選択（<see cref="CaptureType.AreaSelect"/>）: ドラッグで矩形を指定する（<see cref="SelectionOverlayControl"/> の範囲選択モード）。<br/>
/// - スクリーン選択 / ウィンドウ選択: カーソル下の要素をハイライトし、左ボタンの解放または Enter で確定する。<br/>
/// <br/>
/// <b>入力</b>: Enter = 確定 / Escape = キャンセル。 範囲選択は矢印キーでの微調整にも対応する（コントロール側の機能）。<br/>
/// <br/>
/// <b>WinUI 3 固有の実装</b>:<br/>
/// - ウィンドウは <see cref="AppWindow"/> の提示（ボーダーレス・最前面）と <c>MoveAndResize</c>（物理ピクセル）で組む<br/>
///   （仮想スクリーンは負の原点を持ちうるため、DIP の配置では作業領域内へ補正されるのを避ける）。<br/>
/// - 待機はウィンドウの <see cref="Window.Closed"/> で完了するタスクで行う（<c>ShowDialog</c> に相当する API がない）。<br/>
/// - 入力は <see cref="Window"/> ではなくルート要素（<see cref="CaptureOverlayRoot"/>）で受ける<br/>
///   （WinUI 3 のウィンドウはポインターとキーのイベントを公開しない）。<br/>
/// - 画像が合成されるまでの 1 フレームは撮影画像の平均色で覆う（WinUI 3 のウィンドウは透明を表現できないため、<br/>
///   透明から不透明へ切り替える方式は使えない）。
/// </remarks>
internal sealed class CaptureOverlay
{
    /// <summary>ロガー（ホストが注入する。 null の場合は出力しない）。</summary>
    private readonly ILogger _log;

    /// <summary>選択確定の結果（キャンセル時は null）。</summary>
    /// <remarks>
    /// 結果の単一確定（確定とキャンセルの競合で二重完了にしない）はコアの <see cref="CaptureCompletion{TResult}"/> が単一所有する<br/>
    /// （UI 側でフィールドと null 判定を書かない）。<br/>
    /// 結果の型はコア型（<see cref="CaptureResult"/>）とし、UI 型は境界より外へ出さない。
    /// </remarks>
    private readonly CaptureCompletion<CaptureResult?> _result = new();

    /// <summary>最初の描画フレームを待つ上限（画面外の 1x1 では合成が走らないことがあるため）。</summary>
    private static readonly TimeSpan s_firstFrameTimeout = TimeSpan.FromMilliseconds(400);

    /// <summary>最初のフレームの後に合成の完了を待つ時間（白い下地を見せないための実測値）。</summary>
    private static readonly TimeSpan s_composeSettleDelay = TimeSpan.FromMilliseconds(200);

    /// <summary>取得したスクリーン画像（読み取りの面。 セッション中のみ保持し、終了時に破棄する）。</summary>
    /// <remarks>
    /// <see cref="RunAsync"/> の冒頭で設定する（それ以外では null）。<br/>
    /// 撮影が完了した画像は以降書き換えないため、凍結して 0 コピーで共有した読み取り専用の面を保持する。
    /// </remarks>
    private EsReadOnlyBitmap? _screen;

    /// <summary>取得対象のスクリーンの矩形（画面物理ピクセル）。<see cref="RunAsync"/> の冒頭で設定する。</summary>
    private EsRectInt _screenBounds;

    /// <summary>表示中のオーバーレイ ウィンドウ（未表示の間は null）。</summary>
    private Window? _window;

    /// <summary>取得したスクリーン画像を取得する。</summary>
    /// <value>撮影した仮想スクリーンの画像（読み取りの面）。</value>
    /// <remarks>参照は <see cref="RunAsync"/> が設定した後に限られる（撮影はセッションの実行の一部である）。</remarks>
    private EsReadOnlyBitmap Screen => _screen!;

    /// <summary>ローカル DIP → 画像ピクセルの倍率。</summary>
    /// <remarks>
    /// 表示に使うウィンドウの DPI で決まるため、ウィンドウの読み込み後に <see cref="ShowAsync"/> で確定する。<br/>
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

    /// <summary>キャプチャ完了の通知先（結果をホストへ中継する）。</summary>
    private readonly Action<CaptureResult?>? _onCaptured;

    /// <summary>セッションを生成する（撮影は行わない）。</summary>
    /// <param name="type">キャプチャ種別。</param>
    /// <param name="settings">設定（全メンバー non-null を渡す）。</param>
    /// <param name="finder">要素検出サービス（ハイライト型のみ使用する）。</param>
    /// <param name="log">ロガー。 null の場合は出力しない。</param>
    /// <param name="onCaptured">確定・キャンセルの通知先。 null の場合は通知しない。</param>
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
                                // （ルーペの下地色は透明にし、背後の撮影画像を透かす）
            Options = settings.ToSelectionOverlayOptions(),
            MarqueeAppearance = CapturePaintMapper.ToMarqueeAppearance(settings),
            LoupeAppearance = CapturePaintMapper.ToLoupeAppearance(settings),
        };
        _highlight = new CaptureHighlightSession(_log);
        // 検出サービスはハイライト型のみ使う（それ以外はドラッグで選択矩形を作るため検出が不要）。
        // 「どちらの種別か」の判定は利用時にコア（CaptureTypeTraits）へ問い合わせる（導出値を null 性で保持しない）
        _finder = finder;
    }

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
    /// （<see cref="CaptureSessionControl"/>）がコアの <see cref="CaptureSessionSlot{TCapture, TCrop}"/> で行う。<br/>
    /// <b>自ウィンドウの写り込み防止</b>: 撮影の前にウィンドウを画面外へ退避させ、併せて<br/>
    /// <c>SetCaptureExclusion</c>（<c>WDA_EXCLUDEFROMCAPTURE</c>）を設定する。<br/>
    /// <b>対象範囲</b>: 全モニター（仮想スクリーン）を対象にする。 1 モニターに限定すると、<br/>
    /// カーソルのあるモニターでしか選択できないため。
    /// </remarks>
    internal async Task<CaptureResult?> RunAsync(Window owner, CaptureOverlayServices services)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(services);

        // オーナーの退避とキャプチャ除外の設定・解除は CaptureOwnerScope（コア）が単一所有する
        // （手順: 除外の設定 → 画面外へ退避 → 撮影 → 配置の復帰 → 除外の解除）
        using var ownerScope = CaptureOwnerScope.Enter(
            services.Window, owner.ToEsPlatformHandle(), new OwnerWindowAdapter(owner),
            hideOwner: _settings.HideOwnerWindow!.Value);

        // 全モニターを包含する 1 枚の画像として取得する。
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
        // 撮影範囲の原点とサイズを記録する（書式はコアが単一所有する）
        _log.LogVirtualScreen(origin, Screen);

        try
        {
            // 確定・キャンセルまで待つ（入れ子ループを作らない非同期の待機）
            await ShowAsync(services, ownerScope).ConfigureAwait(true);
            // 確定・キャンセルは必ず結果を確定させてからウィンドウを閉じるため、待機は即座に返る
            // （ウィンドウのクローズ経路は CompleteCapture / Cancel に限られる）
            var result = await _result.Task.ConfigureAwait(true);
            // 完了の記録（確定は 2 件・キャンセルは 1 件）はコアが単一所有する
            _log.LogCaptureFinished(result);
            // キャプチャ完了イベントを発行する（同期実行）。
            // C# のイベントは同期型のため、ホストのハンドラ（画像変更）が完了してから
            // 以下の finally（オーナー再表示）が実行される。
            _onCaptured?.Invoke(result);
            return result;
        }
        finally
        {
            // 画像はセッションの終了時に破棄する（以降の描画・切り出しは発生しない）
            _screen?.Dispose();
            _screen = null;
            _window = null;
        }
    }

    /// <summary>オーナーウィンドウ（WinUI 3）を撮影時に画面外へ退避するアダプタ。</summary>
    /// <remarks>
    /// 非表示（<c>Close</c>）は復帰時の再作成でちらつくため使わず、位置の変更（画面外配置）で退避する。<br/>
    /// 復帰時は前面へ戻す（撮影後にユーザーの操作対象へ戻すため）。
    /// </remarks>
    private sealed class OwnerWindowAdapter(Window window) : ICaptureOwnerWindow
    {
        /// <summary>退避前の位置（未退避の場合は null）。</summary>
        private PointInt32? _savedPosition;

        /// <inheritdoc/>
        public bool IsVisible => window.AppWindow.IsVisible;

        /// <inheritdoc/>
        public void HideOffScreen()
        {
            var appWindow = window.AppWindow;
            _savedPosition = appWindow.Position;

            // 画面外（仮想スクリーン座標の外）へ退避する。 負の大きな座標は Windows で
            // 標準的に使われる「画面外配置」であり、ヒットテスト・検出の対象外になる。
            // 値（画面外座標）はコアが単一所有する。
            var offScreen = CaptureTiming.OwnerOffScreenCoord;
            appWindow.Move(new PointInt32(offScreen, offScreen));
        }

        /// <inheritdoc/>
        public void RestorePlacement()
        {
            if (_savedPosition is not { } position)
            {
                return;
            }

            window.AppWindow.Move(position);
            window.Activate();
            _savedPosition = null;
        }
    }

    /// <summary>オーバーレイを表示し、確定またはキャンセルまで待つ。</summary>
    /// <param name="services">オーバーレイの実行に必要なサービス（撮影サービス・ウィンドウ操作）。</param>
    /// <param name="ownerScope">オーナーの退避とキャプチャ除外のスコープ（退避の実行時期を本メソッドが決める）。</param>
    /// <returns>ウィンドウが閉じるまで待機するタスク。</returns>
    /// <remarks>
    /// ウィンドウは仮想スクリーンの位置・サイズでボーダーレス・最前面に表示する。<br/>
    /// 確定・キャンセルのいずれでもウィンドウを閉じ、待機タスクが完了する。<br/>
    /// 位置とサイズは物理ピクセルで与える（仮想スクリーンの原点は負値になりうるため、DIP で与えると<br/>
    /// OS が作業領域内へ補正してしまう。 <see cref="AppWindow.MoveAndResize(RectInt32)"/> は物理ピクセルを取る）。<br/>
    /// ウィンドウはフィールドへ保持する（キャンセル要求が表示前に届いても、後から表示されたウィンドウを閉じられるようにする）。
    /// </remarks>
    private async Task ShowAsync(CaptureOverlayServices services, CaptureOwnerScope ownerScope)
    {
        // セッションを開始する（完了は 1 回だけ受け付ける）
        _state.TryBegin();

        // 表示前にキャンセルされている場合はウィンドウを出さない
        // （待機は Closed で完了するため、出さないと待機が完了しない）
        if (_result.Task.IsCompleted)
        {
            return;
        }

        // モニターごとの実倍率を解決する（サイズラベルの DisplayScale 用。 構成はセッション中に変わらない）。
        // ハイライト型のみがサイズラベルの表示倍率を使う（ドラッグ選択は対応するモニターを持たない）。
        // 倍率が未確定のモニターの除外はコア（MonitorScalingMath）が単一所有する
        if (CaptureTypeTraits.IsHighlighted(_type))
        {
            _monitorScales = MonitorScalingMath.Collect(services.Finder.ListScreens().Select(screen =>
                (screen.Bounds, DpiScaler.ToScaling(MonitorDpi.ForPoint(screen.Bounds.TopLeft)))));
        }

        var root = new CaptureOverlayRoot
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            // 画像が合成されるまでの 1 フレームは「暗転後の平均色」で覆う
            // （WinUI 3 のウィンドウは透明を表現できないため、下地色の不一致を目立たなくする。
            //  平均色のままだと、その 1 フレームが明るい画面に見える）
            Background = new SolidColorBrush(ImageColorMath.Composite(
                ImageColorMath.AverageColor(Screen), _settings.OverlayColor!.Value).ToWinUIColor()),
        };

        // 撮影画像は仮想スクリーンと等倍（領域と同じ縦横比）のため、領域全体へ敷く（Uniform にしない）
        root.Children.Add(new SkiaImageView { Source = Screen });

        // 十字カーソルはドラッグで矩形を作る種別（ハイライト型以外 = 範囲選択）で使う
        // （選択スタイルの判定はコアが単一所有する）
        if (!CaptureTypeTraits.IsHighlighted(_type))
        {
            root.ApplyCrossCursor();
        }

        _selector.SetLoupeSource(Screen);
        ApplyLabels();
        root.Children.Add(_selector);

        var window = new Window { Content = root };
        _window = window;

        ConfigureWindow(window);

        // 確定・キャンセルは選択コントロールからの要求とルート要素のキー入力の両方から受け付ける
        _selector.ConfirmRequested += OnSelectorConfirmRequested;
        _selector.CancelRequested += OnSelectorCancelRequested;
        root.KeyDown += OnRootKeyDown;

        // ウィンドウが閉じられたら待機を完了する
        // （確定・キャンセル以外の経路（Alt+F4 等）で閉じられても待機が残らないようにする）
        window.Closed += OnWindowClosed;

        // 他のウィンドウへ切り替わった（非アクティブ化した）場合はキャンセルする
        // （最前面のオーバーレイが残り続けるのを防ぐ）
        window.Activated += OnWindowActivated;

        root.Loaded += OnRootLoaded;

        if (CaptureTypeTraits.IsHighlighted(_type))
        {
            // スクリーン選択 / ウィンドウ選択: カーソル下の要素をハイライトし、クリックで確定する
            // （選択コントロールのドラッグ選択は表示されないため、入力はルート要素が受け取る）
            root.PointerMoved += OnRootPointerMoved;
            root.PointerPressed += OnRootPointerPressed;
            root.PointerReleased += OnRootPointerReleased;
        }

        // 全モニターを跨ぐウィンドウを、**最初から仮想スクリーンと同じ大きさ**で画面外に置く。
        // 大きさを変えずに位置だけ動かすのは、大きさを変えると再レイアウトが走り、
        // その瞬間にウィンドウの下地（白）が 1 フレーム見えるため（実測 2026-10-17）。
        // 枠の分だけクライアント領域が小さくなるため、クライアント領域が画面と一致するように置く
        // （一致しないと撮影画像が数ピクセル縮小され、画面の座標とずれる）
        OverlayWindowPlacer.MoveClientArea(window.AppWindow, new EsRectInt(
            CaptureTiming.OwnerOffScreenCoord, CaptureTiming.OwnerOffScreenCoord,
            _screenBounds.Width, _screenBounds.Height));
        window.Activate();

        // 最初の描画フレームまで待つ（内容の無いフレームを見せない）。
        // 画面外では合成が走らないことがあるため、待ち時間に上限を設け、超えたら続行する
        // （超えた場合も合成待ちの時間を置くため、下地だけのフレームは出ない）
        try
        {
            await WaitForFirstFrameAsync(root).WaitAsync(s_firstFrameTimeout).ConfigureAwait(true);
        }
        catch (TimeoutException)
        {
            _log.LogOperationError(
                new TimeoutException("最初の描画フレームを待てませんでした（画面外では合成が走らないことがあります）。"),
                nameof(ShowAsync));
        }
        // 合成の完了をもう少し待つ（1 フレームでは DWM へ提示される前に画面内へ移してしまい、
        // 白い下地が 1〜2 フレーム見える。 実測 2026-10-17）
        await Task.Delay(s_composeSettleDelay).ConfigureAwait(true);

        // 大きさはそのままで画面内へ移す（再レイアウトを起こさず、合成済みの面がそのまま出る）
        OverlayWindowPlacer.MoveClientArea(window.AppWindow, _screenBounds);
        // 最前面の提示（IsAlwaysOnTop）だけでは前面に出ないことがあるため、Z 順も明示的に最前面へする
        // （オーナーを退避しない場合、オーナーの背後に隠れてオーバーレイが見えなくなる）
        window.AppWindow.MoveInZOrderAtTop();

        // オーナーの退避は、キャプチャ除外が効かない場合だけ行う（自ウィンドウが撮影画像へ写るため）。
        // 除外が効いている場合は退避しない: オーバーレイが画面全体を覆っているため見た目は変わらないが、
        // 退避の瞬間にウィンドウが再合成され、一瞬だけ明るい画面が見える（実測）
        if (!ownerScope.IsExcluded)
        {
            ownerScope.ApplyPendingHide();
        }

        window.Activate();
        _isShown = true;

        // ウィンドウが閉じるまで待つ（閉じる経路は CompleteCapture / Cancel / ウィンドウの閉じる操作）
        await _closed.Task.ConfigureAwait(true);
    }

    /// <summary>要素の最初の描画フレームを待つ。</summary>
    /// <param name="element">対象の要素。</param>
    /// <returns>最初のフレームを描き終えたときに完了するタスク。</returns>
    /// <remarks>
    /// 内容を構築しただけでは XAML の合成が済んでおらず、表示した直後の 1 フレームは
    /// ウィンドウの下地（白）だけが見える（実測）。 合成の 1 フレームを待ってから画面へ出す。
    /// 要素が既に読み込まれている場合は <c>Loaded</c> を待たない。
    /// </remarks>
    private static Task WaitForFirstFrameAsync(FrameworkElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        var completed = new TaskCompletionSource();

        void OnRendering(object? sender, object args)
        {
            CompositionTarget.Rendering -= OnRendering;
            completed.TrySetResult();
        }

        void OnLoaded(object? sender, RoutedEventArgs args)
        {
            element.Loaded -= OnLoaded;
            CompositionTarget.Rendering += OnRendering;
        }

        if (element.IsLoaded)
        {
            CompositionTarget.Rendering += OnRendering;
        }
        else
        {
            element.Loaded += OnLoaded;
        }

        return completed.Task;
    }

    /// <summary>オーバーレイを画面内へ出し終えたかどうか（出し終えるまでは非アクティブ化でキャンセルしない）。</summary>
    /// <remarks>
    /// 準備中（画面外での描画待ち）に非アクティブ化しても、まだ画面に出していないため<br/>
    /// キャンセルすると撮影が始まらない。 画面内へ出した後の非アクティブ化だけをキャンセル扱いにする。
    /// </remarks>
    private bool _isShown;

    /// <summary>ウィンドウが閉じられたことを通知する完了シグナル。</summary>
    /// <remarks>
    /// <c>ShowDialog</c> に相当する API がないため、閉じる操作で完了するタスクを本クラスが所有する。
    /// </remarks>
    private readonly TaskCompletionSource _closed = new();

    /// <summary>オーバーレイ ウィンドウをボーダーレス・最前面に構成する。</summary>
    /// <param name="window">構成するウィンドウ。</param>
    /// <remarks>
    /// タイトル バーと境界を外し、サイズ変更・最大化・最小化を禁止する（タスクバーにも出さない）。<br/>
    /// 最前面（常に手前）にすることで、撮影中の他ウィンドウの前面へ出る。
    /// </remarks>
    private static void ConfigureWindow(Window window)
    {
        var presenter = OverlappedPresenter.Create();
        presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsAlwaysOnTop = true;
        window.AppWindow.SetPresenter(presenter);

        // 撮影中の一時的な表示のため、タスクバー・Alt+Tab の一覧には出さない
        window.AppWindow.IsShownInSwitchers = false;
    }

    /// <summary>サイズラベル・情報ラベルの文言を選択コントロールへ反映する。</summary>
    /// <remarks>
    /// 1. サイズラベルは対象座標（＝画像ピクセル）で表示するため、テキストは描画のたびに生成する。<br/>
    /// 2. 外観（サイズラベル・サブライン・情報ラベルの色・サイズ）は設定値の変換<br/>
    ///    （<see cref="SelectionOverlayOptionsMapper.ToSelectionOverlayOptions"/>）が単一所有するため、本メソッドでは設定しない。<br/>
    /// 3. 描画モード（操作ラベルの文言選定）は複合コントロールの <see cref="SelectionOverlayControl.Mode"/> が単一所有する。<br/>
    /// 4. 操作ラベル・情報ラベルの文言をホスト（キャプチャ文脈）がプロバイダで与える（モード別の文言と情報ラベルの表示可否は<br/>
    ///    <see cref="CaptureLabelTexts"/> が単一所有する。 表示名はコアの <see cref="CaptureHighlightSession"/> を参照する）。<br/>
    /// 5. 表示する矩形の選択（ハイライト型 / ドラッグ型）はコア（<see cref="CaptureTypeTraits.ResolveRect"/>）が単一所有し、<br/>
    ///    サイズラベルの文言（書式）もコアが単一所有する（本ホストは表示スケールだけを与える）。<br/>
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

    /// <summary>確定の対象となる選択矩形（画像ピクセル）を解決する。</summary>
    /// <returns>確定対象の矩形。 対象が無い場合は null。</returns>
    /// <remarks>
    /// 「ハイライト型はハイライト矩形・それ以外は選択矩形」という選択スタイルの判断はコア<br/>
    /// （<see cref="CaptureTypeTraits.ResolveRect"/>）が単一所有する。<br/>
    /// ハイライト矩形はコアのハイライト状態（<see cref="CaptureHighlightSession"/>）から読む<br/>
    /// （選択コントロールから読み戻すと、同じ値が 2 箇所に存在する）。
    /// </remarks>
    private EsRect? ResolveSelection()
        => CaptureTypeTraits.ResolveRect(
            _type,
            _highlight.SourceRect,
            _selector.HasSelection ? _selector.SelectionSourceRect : null);

    /// <summary>選択領域を切り出す（選択矩形は画面物理座標の浮動小数 = ドラッグ選択・キー操作）。</summary>
    /// <param name="screenRect">選択矩形（画面物理座標）。</param>
    /// <remarks>
    /// 切り出し矩形の決定（種別ごとの閉区間化・画像内へのクランプ・端数の丸め）はコアが単一所有する。<br/>
    /// <b>入口は画面物理座標に統一する</b>: 選択矩形（画像ローカル座標）は呼び出し側で<br/>
    /// <see cref="CropClipMath.ToScreenRect(EsRect, EsPointInt)"/> により戻してから渡す（占有範囲と同じ座標空間にする。<br/>
    /// 混ぜると原点が負値のマルチモニタで切り出し位置がずれ・幅が 0 になる）。<br/>
    /// 結果の領域は <see cref="CaptureResult.SourceBounds"/>（仮想画面座標）と同じ座標空間になる。
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
        // 完了の記録とホストへの通知は RunAsync（待機側）が行う（ウィンドウは結果を確定させて閉じるだけ）。
        _result.TrySetResult(CaptureResult.From(image, pixels));
        Close();
    }

    /// <summary>キャンセルする（切り出しを行わずに閉じる）。</summary>
    /// <remarks>
    /// Facade（<see cref="CaptureSessionControl"/>）が進行中のセッションを取り消す際にも呼ぶ<br/>
    /// （公開操作 <see cref="CaptureSessionControl.Cancel"/> と <see cref="CaptureSessionControl.Dispose"/> の効果）。
    /// </remarks>
    internal void Cancel()
    {
        if (!_state.TryComplete())
        {
            return;
        }

        // 選択のキャンセル要求とセッションの完了（キャンセル 1 件）はコアが単一所有する。
        // 完了の記録とホストへの通知は RunAsync（待機側）が行う
        _log.LogCancelRequested();
        _result.TrySetResult(null);
        Close();
    }

    /// <summary>現在のオーバーレイ ウィンドウを閉じる。</summary>
    /// <remarks>
    /// ウィンドウが未生成（表示前のキャンセル）の場合も待機を完了させる<br/>
    /// （<see cref="ShowAsync"/> は表示の前に完了を確認するため、ウィンドウは作られない）。
    /// </remarks>
    private void Close()
    {
        if (_window is { } window)
        {
            _window = null;
            window.Close();
            return;
        }

        _closed.TrySetResult();
    }

    /// <summary>カーソル下の要素を検出してハイライトを更新する。</summary>
    /// <param name="screenPoint">カーソル位置（画面物理ピクセル）。</param>
    /// <remarks>
    /// 検出対象はキャプチャ種別で決まる。 スクリーン選択はカーソル下のモニター（画面全体）、<br/>
    /// ウィンドウ選択はカーソル下の最前面ウィンドウを検出し、その矩形をハイライトする。<br/>
    /// 矩形の対象座標への変換・未検出の正規化・表示名の保持・ログはコア<br/>
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

    /// <summary>ルート要素のローカル座標（DIP）を画面物理ピクセルの座標へ変換する。</summary>
    /// <param name="position">ルート要素のローカル座標（DIP）。</param>
    /// <returns>画面物理ピクセルの座標（四捨五入）。</returns>
    /// <remarks>
    /// ウィンドウのクライアント領域は仮想スクリーン全面へ物理ピクセルで置かれているため、<br/>
    /// クライアント座標（物理ピクセル）は「占有範囲の原点 + ローカル DIP × 倍率」になる。<br/>
    /// 丸めはコア（<see cref="CropBoundsMath.RoundToPixel"/>）が単一所有する<br/>
    /// （既定の <see cref="Math.Round(double)"/> は銀行丸めで、座標の四捨五入と 1 ピクセルずれる）。
    /// </remarks>
    private EsPointInt ToScreenPoint(Point position)
        => new(
            _screenBounds.X + CropBoundsMath.RoundToPixel(position.X * _scale),
            _screenBounds.Y + CropBoundsMath.RoundToPixel(position.Y * _scale));

    /// <summary>選択コントロールへ注入する座標変換ポートを生成する。</summary>
    /// <returns>ローカル DIP ⇔ 画像ピクセルを変換するポート。</returns>
    /// <remarks>
    /// 画像はウィンドウのクライアント領域全体へ等倍で描画するため、変換は倍率の乗除算だけになる。<br/>
    /// 倍率の適用方向（ToSource = DIP → 画像ピクセル）は EsUtil.Platform.Core の
    /// <see cref="CoordTransform.ClientToSource(Func{double}, Func{EsSize?})"/> が<br/>
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
    /// 「原点の加算」「判定点（矩形の中心）」「フォールバック」の規則はコア（<see cref="MonitorScalingMath"/>）が単一所有する<br/>
    /// （UI ごとに判定点がずれると、同じ矩形で表示倍率が食い違う）。
    /// </remarks>
    private double DisplayScalingAt(EsRect sourceRect)
        => MonitorScalingMath.ResolveForSourceRect(
            sourceRect, _screenBounds.TopLeft, _monitorScales, _scale);

    /// <summary>ルート要素が読み込まれた時点で座標変換ポートと入力のフォーカスを整える。</summary>
    /// <param name="sender">イベントの送信元（ルート要素）。</param>
    /// <param name="e">イベント引数。</param>
    /// <remarks>
    /// 画像ピクセル ↔ DIP の倍率は表示に使うウィンドウの DPI で決める<br/>
    /// （ウィンドウのクライアント座標は物理ピクセルで届くため、この倍率で画像ピクセルへ戻せる。<br/>
    /// モニターごとに倍率が異なっても、ウィンドウの倍率は 1 つに定まるため全域で同じ式が使える）。<br/>
    /// Enter / Escape を受け取れるよう、選択コントロールへフォーカスを移す。
    /// </remarks>
    private void OnRootLoaded(object sender, RoutedEventArgs e)
    {
        // 有効な係数の判定（0 以下は未確定）はコアが単一所有する
        var scale = (sender as FrameworkElement)?.XamlRoot?.RasterizationScale ?? DpiScaler.StandardScaling;
        if (DpiScaler.IsValidScaling(scale))
        {
            _scale = scale;
            _selector.SetCoordTransform(CreatePort());
        }

        _selector.FocusMarquee();
    }

    /// <summary>ルート要素のキー入力を処理する（Enter = 確定 / Escape = キャンセル）。</summary>
    /// <param name="sender">イベントの送信元（ルート要素）。</param>
    /// <param name="e">キーの情報。</param>
    /// <remarks>
    /// 選択コントロールが処理しなかったキーだけがここへ届く（コントロール内の判断が優先される）。<br/>
    /// どのキーがどの操作かの解釈はコアが単一所有する。 確定対象が無い場合の Enter は何もしない。
    /// </remarks>
    private void OnRootKeyDown(object sender, KeyRoutedEventArgs e)
    {
        // 確定対象は 1 度だけ解決して効果へ渡す（再解決しない）
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
    }

    /// <summary>ハイライト型のポインター移動を処理する（カーソル下の要素を検出する）。</summary>
    /// <param name="sender">イベントの送信元（ルート要素）。</param>
    /// <param name="e">ポインターの情報。</param>
    private void OnRootPointerMoved(object sender, PointerRoutedEventArgs e)
        => Highlight(ToScreenPoint(e.GetCurrentPoint((UIElement)sender).Position));

    /// <summary>ハイライト型のポインター押下を処理する（検出した要素を固定する）。</summary>
    /// <param name="sender">イベントの送信元（ルート要素）。</param>
    /// <param name="e">ポインターの情報。</param>
    /// <remarks>検出矩形（画面物理）を固定する。 退化した検出結果は固定されない。</remarks>
    private void OnRootPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint((UIElement)sender);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        _clickSelection.Fix(CaptureTypeTraits.FindUnderCursor(_finder, _type, ToScreenPoint(point.Position)).Bounds);
    }

    /// <summary>ハイライト型のポインター解放を処理する（固定した要素で確定する）。</summary>
    /// <param name="sender">イベントの送信元（ルート要素）。</param>
    /// <param name="e">ポインターの情報。</param>
    /// <remarks>
    /// 固定が無い解放では確定しない（押下を伴わない解放・退化した検出）。<br/>
    /// 検出矩形は画面物理座標のため、変換せずそのまま確定する（入口の座標空間が同じ）。
    /// </remarks>
    private void OnRootPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_clickSelection.Take() is not { } fixedRect)
        {
            return;
        }

        ConfirmScreen(fixedRect);
    }

    /// <summary>選択の確定要求（Enter キー・ダブルクリック等）を処理する。</summary>
    /// <param name="sender">イベントの送信元（選択コントロール）。</param>
    /// <param name="e">選択矩形イベント（矩形は対象座標 = 画像ピクセル）。</param>
    /// <remarks>
    /// 選択矩形（画像ローカル座標）を確定の入口（画面物理座標）へ揃えて渡す（記録と解決は入口が担う）。
    /// </remarks>
    private void OnSelectorConfirmRequested(object? sender, SelectionConfirmedEventArgs e)
        => ConfirmScreen(CropClipMath.ToScreenRect(e.SelectionSourceRect, _screenBounds.TopLeft));

    /// <summary>選択のキャンセル要求を処理する。</summary>
    /// <param name="sender">イベントの送信元（選択コントロール）。</param>
    /// <param name="e">イベント引数。</param>
    private void OnSelectorCancelRequested(object? sender, EventArgs e) => Cancel();

    /// <summary>ウィンドウの非アクティブ化を処理する（キャンセルする）。</summary>
    /// <param name="sender">イベントの送信元（オーバーレイ ウィンドウ）。</param>
    /// <param name="e">ウィンドウの状態。</param>
    /// <remarks>
    /// 最前面のオーバーレイが残り続けるのを防ぐ。<br/>
    /// 画面内へ出し終える前（準備中）の非アクティブ化ではキャンセルしない（取り消されると撮影が始まらない）。
    /// </remarks>
    private void OnWindowActivated(object sender, WindowActivatedEventArgs e)
    {
        if (_isShown && e.WindowActivationState == WindowActivationState.Deactivated)
        {
            Cancel();
        }
    }

    /// <summary>ウィンドウのクローズを処理する（待機を完了させる）。</summary>
    /// <param name="sender">イベントの送信元（オーバーレイ ウィンドウ）。</param>
    /// <param name="e">イベント引数。</param>
    /// <remarks>
    /// 確定・キャンセル以外の経路（Alt+F4 等）で閉じられた場合も、キャンセルとして記録して待機を完了させる<br/>
    /// （待機が残ると Facade の排他が解放されない）。
    /// </remarks>
    private void OnWindowClosed(object sender, WindowEventArgs e)
    {
        Cancel();
        _closed.TrySetResult();
    }
}
