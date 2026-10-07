using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Platform.Core.Native;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Presentation;
using EsUtil.SaveEvidence.Platform.MewUI;
using EsUtil.SaveEvidence.Platform.MewUI.Imaging;
using Microsoft.Extensions.Logging;
using EsUtil.SaveEvidence.Parts.CapturePreview.MewUI.SelectionOverlay;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Capture;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.SelectionOverlay;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Crop;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Session;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Setting;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Contract;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Logging;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.MewUI.Session;

/// <summary>画面全体を覆うキャプチャ選択オーバーレイ（MewUI 版）。</summary>
/// <remarks>
/// Avalonia 版 <c>CaptureSessionControl.AvaloniaUI</c> の <c>CaptureOverlayWindow</c> に相当する。 撮影から選択・確定までの<br/>
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
    /// （UI 側でフィールドと null 判定を書かない）。
    /// </remarks>
    private readonly CaptureCompletion<EsBitmap?> _result = new();

    /// <summary>取得したスクリーン画像（セッション中のみ保持し、終了時に破棄する）。</summary>
    /// <remarks><see cref="Run"/> の冒頭で設定する（それ以外では null）。</remarks>
    private EsBitmap? _screen;

    /// <summary>取得対象のスクリーンの矩形（画面物理ピクセル）。<see cref="Run"/> の冒頭で設定する。</summary>
    private EsRectInt _screenBounds;

    /// <summary>取得したスクリーン画像を取得する。</summary>
    /// <value>撮影した仮想スクリーンの画像。</value>
    /// <remarks>参照は <see cref="Run"/> が設定した後に限られる（撮影はセッションの実行の一部である）。</remarks>
    private EsBitmap Screen => _screen!;

    /// <summary>ローカル DIP → 画像ピクセルの倍率。</summary>
    /// <remarks>
    /// 表示に使うウィンドウの DPI で決まるため、ウィンドウの生成後に <see cref="Show"/> で確定する。<br/>
    /// 確定するまでは基準値（DIP と画像ピクセルが 1:1）を使う。
    /// </remarks>
    private double _scale = DpiScaler.StandardScaling;

    /// <summary>モニターごとの表示倍率（モニター矩形と DPI スケールの組）。</summary>
    /// <remarks>
    /// サイズラベルの表示倍率（DisplayScale）に使う。 仮想スクリーン全体を 1 枚のウィンドウで覆うと<br/>
    /// ウィンドウの倍率は 1 つに定まるため、モニターごとの実倍率は位置から解決する必要がある。<br/>
    /// セッション中はモニター構成が変わらないため、<see cref="Show"/> で一度だけ解決する。
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

    /// <summary>オーバーレイを初期化する（内部用。<see cref="CaptureSessionControl.Capture(Window, CaptureType, CaptureOverlayServices)"/> から生成する）。</summary>
    /// <param name="type">キャプチャ種別。</param>
    /// <param name="settings">設定（選択枠・暗転・ルーペ・ラベルの外観）。</param>
    /// <param name="finder">要素検出サービス（スクリーン選択 / ウィンドウ選択で使用）。</param>
    /// <param name="log">ロガー（null の場合は出力しない）。</param>
    /// <remarks>
    /// 撮影（画像の取得）は行わない（排他の受付を撮影より先に済ませるため、生成と実行を分ける）。<br/>
    /// 画像は <see cref="Run"/> の冒頭で取得して設定する。
    /// </remarks>
    internal CaptureOverlay(
        CaptureType type, CaptureSettings settings, IElementFinder finder, ILogger? log)
    {
        _type = type;
        _settings = settings;
        _log = log.OrNullLogger();

        _selector = new SelectionOverlayControl
        {
            Mode = CaptureLabelTexts.ToSelectionMode(type),
            Log = _log,         // 設定はコアのマッパーで 1 値に変換して一括適用する（変換はコアが単一所有する）。
                                // 色は描画にだけ必要な設定のため、描画の設定として与える
                                // （ルーペの市松模様の下地色は透明にし、背後の撮影画像を透かす。 Avalonia 版と同じ扱いにする）
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
    /// <returns>切り出した画像。 キャンセル・取得失敗・退化した選択の場合は null。</returns>
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
    internal EsBitmap? Run(Window owner, CaptureOverlayServices services)
    {
        // オーナーの退避とキャプチャ除外の設定・解除は CaptureOwnerScope（コア）が単一所有する
        // （手順: 除外の設定 → 画面外へ退避 → 撮影 → 配置の復帰 → 除外の解除。 Avalonia 版と同じ）
        using var ownerScope = CaptureOwnerScope.Enter(
            services.Window, owner.ToEsPlatformHandle(), new OwnerWindowAdapter(owner),
            hideOwner: _settings.HideOwnerWindow!.Value);

        // 全モニターを包含する 1 枚の画像として取得する（Avalonia 版と同じ）。
        // 1 モニターに限定すると、カーソルのあるモニターでしか選択できない（別のモニターへ
        // 移動しての選択操作ができない）ため、仮想スクリーン全体を対象にする。
        var screen = services.Capture.CaptureVirtualScreen(out var origin);
        if (screen is null)
        {
            return null;
        }

        _screen = screen;
        // 画像の占有範囲（画面物理座標）= 撮影時の原点 + 画像サイズ（原点は負値になりうる）
        _screenBounds = CropClipMath.ImageBounds(screen, origin);
        _log.LogCaptureStart(_type);
        // 撮影範囲の原点とサイズを記録する（書式はコアが単一所有する。 Avalonia 版と同じ診断情報を残す）
        _log.LogVirtualScreen(origin, screen);

        try
        {
            Show(owner, services);
            return Result;
        }
        finally
        {
            // 画像はセッションの終了時に破棄する（以降の描画・切り出しは発生しない）
            screen.Dispose();
            _screen = null;
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
            window.Activate();
            _savedPosition = null;
        }
    }

    /// <summary>透明を解除するまでに待つフレーム数。</summary>
    /// <remarks>
    /// 表示直後のフレームはテーマ背景だけで、画像は次のフレームで合成される（実測）。<br/>
    /// そのため 2 フレーム目（画像が合成されたフレーム）で透明を解除する。
    /// </remarks>
    private const int FramesUntilReveal = 2;

    /// <summary>画像の平均色を求める。</summary>
    /// <param name="image">対象の画像。</param>
    /// <returns>平均色（不透明）。</returns>
    /// <remarks>
    /// 標本の間引きと「不透明として返す」規則はコア（<see cref="ImageColorMath"/>）が単一所有する<br/>
    /// （本メソッドはコアの色型を MewUI の色型へ写すだけ。 変換は Platform 層の
    /// <c>ToMewUIColor</c> が単一所有する）。
    /// </remarks>
    private static Color AverageColor(EsBitmap image)
        => ImageColorMath.AverageColor(image).ToMewUIColor();

    /// <summary>確定した結果を取得する（未確定の場合は null）。</summary>
    /// <value>切り出した画像。 キャンセル・未確定の場合は null。</value>
    /// <remarks>完了通知（コアの <see cref="CaptureCompletion{TResult}"/>）のタスクから読む（UI 側で結果を保持しない）。</remarks>
    private EsBitmap? Result => _result.Task.IsCompletedSuccessfully ? _result.Task.Result : null;

    /// <summary>オーバーレイを表示し、確定またはキャンセルまで待つ。</summary>
    /// <param name="owner">オーナーウィンドウ。</param>
    /// <param name="services">オーバーレイの実行に必要なサービス（撮影サービス・ウィンドウ操作）。</param>
    /// <remarks>
    /// ウィンドウは仮想スクリーンの位置・サイズでボーダーレス・最前面に表示する。<br/>
    /// 確定・キャンセルのいずれでもウィンドウを閉じ、<c>ShowDialog</c> の入れ子ループを終わらせる。
    /// </remarks>
    private void Show(Window owner, CaptureOverlayServices services)
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
        content.Add(new SkiaImageView { Source = Screen });

        var window = new Window()
            .Borderless()
            .Topmost()
            .Content(content);

        // 透明の間はウィンドウの背景色だけが合成されるため、透明を解除した瞬間の 1 フレームは
        // 背景色が見える。 背景を撮影画像の平均色に合わせて、その差を目立たなくする。
        window.Background = AverageColor(Screen);

        // ウィンドウは表示時に背景色で塗られるため、そのまま表示すると画像を描くまでの
        // 数十 ms が背景色の面として見える（実測: 65 ms 表示）。 そのため透明な状態で表示し、
        // 画像が合成されたフレームで透明を解除する。
        // 物理ピクセルでの配置は起動時の配置より後（1 フレーム目）に行う必要があるため、ここで行う。
        var revealed = false;
        var placed = false;
        var frames = 0;
        window.Opacity = 0;
        window.FrameRendered += () =>
        {
            frames++;

            if (!placed)
            {
                // 全モニターを跨ぐウィンドウは、UI フレームワークの論理座標（DIP）による配置が
                // OS により作業領域内へ補正される（実測: 仮想スクリーン原点 -1200 が 0 になる）。
                // そのため表示直後（透明なうち）に物理ピクセルで仮想スクリーン全面へ置き直す。
                placed = true;
                services.Window.SetBounds(window.ToEsPlatformHandle(), _screenBounds);
            }

            if (revealed || frames < FramesUntilReveal)
            {
                return;
            }

            revealed = true;
            window.Opacity = 1;
        };

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
            WindowPlacementMath.ToDipLength(Screen.Width, creationScale),
            WindowPlacementMath.ToDipLength(Screen.Height, creationScale));

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

        // 位置は生成時のスケールで解釈される
        var position = WindowPlacementMath.ToDipPoint(_screenBounds.TopLeft, creationScale);

        window.StartManualPosition(position.X, position.Y);

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

        window.ShowDialog(owner);
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
        // 完了の記録（確定は 2 件）はコアが単一所有する。 領域は契約どおり仮想画面座標（画面物理）で渡す
        _log.LogCaptureFinished(CaptureResult.From(image, pixels));
        _result.TrySetResult(image);
        Close();
    }

    /// <summary>キャンセルする（切り出しを行わずに閉じる）。</summary>
    private void Cancel()
    {
        if (!_state.TryComplete())
        {
            return;
        }

        // 選択のキャンセル要求とセッションの完了（キャンセル 1 件）はコアが単一所有する
        _log.LogCancelRequested();
        _log.LogCaptureFinished(null);
        _result.TrySetResult(null);
        Close();
    }

    /// <summary>選択オーバーレイの確定要求（Enter キー・ダブルクリック等）を処理する。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">選択矩形イベント（矩形は対象座標 = 画像ピクセル）。</param>
    /// <remarks>
    /// 選択矩形（画像ローカル座標）を確定の入口（画面物理座標）へ揃えて渡す（記録と解決は入口が担う）。
    /// </remarks>
    private void OnSelectorConfirmRequested(object? sender, SelectionConfirmedEventArgs e)
        => ConfirmScreen(CropClipMath.ToScreenRect(e.SelectionSourceRect, _screenBounds.TopLeft));

    /// <summary>現在のオーバーレイウィンドウを閉じる。</summary>
    /// <remarks>
    /// ウィンドウはフィールドに保持しないため、表示中のウィンドウ（<c>Application.Current.AllWindows</c> の末尾）を閉じる。
    /// </remarks>
    private static void Close()
    {
        // 末尾のウィンドウが表示中のオーバーレイ（インデクサで取得し、LINQ を持ち込まない）
        var windows = Application.Current?.AllWindows;
        if (windows is { Count: > 0 })
        {
            windows[^1].Close();
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




