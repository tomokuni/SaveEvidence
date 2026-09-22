using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Estable.CaptureSession;
using Estable.FadeMask;
using Estable.FadeMaskDragRect;
using Estable.MewUI.FadeMaskDragRect;
using EsUtil.Platform.MewUI;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using DrawingColor = System.Drawing.Color;
using MewUIColor = Aprillz.MewUI.Color;

namespace Estable.MewUI.CaptureSession;

/// <summary>画面全体を覆うキャプチャ選択オーバーレイ（MewUI 版）。</summary>
/// <remarks>
/// Avalonia 版 <c>CaptureSessionControl.AvaloniaUI</c> の <c>OverlayWindow</c> に相当する。撮影から選択・確定までの<br/>
/// 1 セッションを担い、確定した領域を <see cref="EsBitmap"/> として返す。<br/>
/// <br/>
/// <b>全体の流れ</b>:<br/>
/// 1. 設定に従ってオーナーウィンドウを画面外へ退避する（<c>HideMainWindowOnCapture</c>）。<br/>
/// 2. 仮想スクリーン（全モニタ）を 1 枚の画像として取得する（<see cref="ICaptureService"/>）。<br/>
/// 3. 取得した画像を敷いたボーダーレス・最前面のウィンドウを仮想スクリーン全面へ表示する。<br/>
/// 4. 選択を確定すると、選択矩形で画像を切り出して返す（Escape はキャンセル）。<br/>
/// 5. オーナーウィンドウを元の位置へ戻す。<br/>
/// <br/>
/// <b>座標系</b>: 取得画像のピクセル = 画面物理ピクセル（仮想スクリーン原点が画像の (0, 0)）。<br/>
/// ドメイン座標（画像ピクセル）とローカル DIP の変換は <see cref="ICoordTransform"/> が担い、<br/>
/// 選択コントロールへ注入する（コントロール側は座標系を意識しない）。<br/>
/// <br/>
/// <b>モード</b>:<br/>
/// - 範囲選択（<see cref="CaptureType.AreaSelect"/>）: ドラッグで矩形を指定する（<c>FadeMaskDragRectControl</c> の範囲選択モード）。<br/>
/// - スクリーン選択 / ウィンドウ選択: カーソル下の要素をハイライトし、クリックまたは Enter で確定する。<br/>
/// <br/>
/// <b>入力</b>: Enter = 確定 / Escape = キャンセル。範囲選択は矢印キーでの微調整にも対応する（コントロール側の機能）。
/// </remarks>
public sealed class CaptureOverlay
{
    /// <summary>ロガー（ホストが注入する。null の場合は出力しない）。</summary>
    private readonly ILogger _log;

    /// <summary>選択確定の結果（キャンセル時は null の <see cref="Bitmap"/>）。</summary>
    private EsBitmap? _result;

    /// <summary>取得したスクリーン画像（セッション中のみ保持し、終了時に破棄する）。</summary>
    private readonly EsBitmap _screen;

    /// <summary>取得対象のスクリーンの矩形（画面物理ピクセル）。</summary>
    private readonly EsPixelRect _screenBounds;

    /// <summary>ローカル DIP → 画像ピクセルの倍率。</summary>
    /// <remarks>表示に使うウィンドウの DPI で決まるため、ウィンドウの生成後に <see cref="Show"/> で確定する。</remarks>
    private double _scale = 1.0;

    /// <summary>モニターごとの表示倍率（モニター矩形と DPI スケールの組）。</summary>
    /// <remarks>
    /// サイズラベルの表示倍率（DisplayScale）に使う。仮想スクリーン全体を 1 枚のウィンドウで覆うと<br/>
    /// ウィンドウの倍率は 1 つに定まるため、モニターごとの実倍率は位置から解決する必要がある。<br/>
    /// セッション中はモニター構成が変わらないため、<see cref="Show"/> で一度だけ解決する。
    /// </remarks>
    private IReadOnlyList<(EsPixelRect Bounds, double Scale)> _monitorScales = [];

    /// <summary>選択コントロール（暗転マスク + 選択またはハイライトの描画）。</summary>
    private readonly FadeMaskDragRectControl _selector;

    /// <summary>カーソル下の要素を検出するサービス（スクリーン選択 / ウィンドウ選択のみ使用する）。</summary>
    private readonly IElementFinder? _finder;

    /// <summary>確定時に使用する選択矩形（ハイライト矩形または選択矩形）の取得元。</summary>
    private readonly Func<EsRect?> _selection;

    /// <summary>キャプチャ種別（操作ラベル・情報ラベルの文言に影響する）。</summary>
    private readonly CaptureType _type;

    /// <summary>カーソル下の要素のタイトル（情報ラベルに表示する。未検出時は空）。</summary>
    private string _windowTitle = "";

    /// <summary>セッションの状態（再入禁止・完了は 1 回だけ）。</summary>
    /// <remarks>終了後に非アクティブ化・閉じるが重なっても二重終了（二重ログ）にしない。</remarks>
    private readonly CaptureSessionState _state = new();

    /// <summary>設定（ラベルの再組み立てに使用する）。</summary>
    private CaptureSettings _settings = null!;

    /// <summary>オーバーレイを初期化する（内部用。<see cref="Capture"/> から生成する）。</summary>
    /// <param name="screen">取得したスクリーン画像（所有権は本クラスへ移る）。</param>
    /// <param name="screenBounds">取得対象のスクリーンの矩形（画面物理ピクセル）。</param>
    /// <param name="type">キャプチャ種別。</param>
    /// <param name="settings">設定（選択枠・暗転・ルーペ・ラベルの外観）。</param>
    /// <param name="finder">要素検出サービス（スクリーン選択 / ウィンドウ選択で使用）。</param>
    /// <param name="log">ロガー（null の場合は出力しない）。
    private CaptureOverlay(
        EsBitmap screen, EsPixelRect screenBounds, CaptureType type,
        CaptureSettings settings, IElementFinder finder, ILogger? log)
    {
        _screen = screen;
        _screenBounds = screenBounds;
        _type = type;
        _settings = settings;
        _log = log.OrNullLogger();

        _selector = new FadeMaskDragRectControl { Mode = ToMode(type), Log = _log };
        ApplyAppearance(_selector, settings);

        if (type is CaptureType.ScreenSelect or CaptureType.WindowSelect)
        {
            // ハイライトはホスト（本クラス）がカーソル位置から設定する（選択矩形を持たない）
            _finder = finder;
            _selection = () => _selector.HasHighlight ? _selector.LogicalHighlightRect : null;
        }
        else
        {
            _selection = () => _selector.HasSelection ? _selector.SelectionRect : null;
        }
    }

    /// <summary>キャプチャ種別に対応する選択コントロールのモードを求める。</summary>
    /// <param name="type">キャプチャ種別。</param>
    /// <returns>選択コントロールのモード。</returns>
    private static FadeMaskDragRectMode ToMode(CaptureType type) => type switch
    {
        CaptureType.ScreenSelect => FadeMaskDragRectMode.ScreenSelect,
        CaptureType.WindowSelect => FadeMaskDragRectMode.WindowSelect,
        _ => FadeMaskDragRectMode.AreaSelect,
    };

    /// <summary>キャプチャのセッションを実行し、確定した領域を返す。</summary>
    /// <param name="services">アプリ全体で共有するコアオブジェクト。</param>
    /// <param name="owner">オーナーウィンドウ（撮影中は画面外へ退避し、終了後に戻す）。</param>
    /// <param name="type">キャプチャ種別。</param>
    /// <returns>切り出した画像。キャンセル・取得失敗・退化した選択の場合は null。</returns>
    /// <remarks>
    /// 1. 設定に従ってオーナーをキャプチャ除外に設定し、画面外へ退避する（設定: メインウィンドウを隠す）。<br/>
    /// 2. 仮想スクリーン（全モニターを包含する領域）を 1 枚の画像として取得する。<br/>
    /// 3. オーバーレイを表示し、確定またはキャンセルまで待つ。<br/>
    /// 4. オーナーを元の位置へ戻し、キャプチャ除外を解除して切り出し結果を返す。<br/>
    /// <b>自ウィンドウの写り込み防止</b>: 撮影の前にウィンドウを画面外へ退避させ、併せて<br/>
    /// <c>SetCaptureExclusion</c>（<c>WDA_EXCLUDEFROMCAPTURE</c>）を設定する。<br/>
    /// <b>チラつきの防止</b>: 退避は非表示（<c>Hide</c>）ではなく位置の変更とし、復帰時に<br/>
    /// ウィンドウを再作成させない（Avalonia 版と同じ方式）。<br/>
    /// <b>対象範囲</b>: 全モニター（仮想スクリーン）を対象にする。1 モニターに限定すると、<br/>
    /// カーソルのあるモニターでしか選択できないため（Avalonia 版と同じ挙動）。
    /// </remarks>
    public static EsBitmap? Capture(Window owner, CaptureType type, CaptureOverlayServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var log = services.Logger.OrNullLogger();
        var settings = services.Settings.ToNonNull();

        // オーナーの退避とキャプチャ除外の設定・解除は CaptureOwnerScope（コア）が単一所有する
        // （手順: 除外の設定 → 画面外へ退避 → 撮影 → 配置の復帰 → 除外の解除。Avalonia 版と同じ）
        using var ownerScope = CaptureOwnerScope.Enter(
            services.Window, owner.ToEsPlatformHandle(), new OwnerWindowAdapter(owner),
            hideOwner: settings.HideOwnerWindow!.Value);

        return CaptureCore(owner, type, services, settings);
    }

    /// <summary>オーナーウィンドウ（MewUI）を撮影時に画面外へ退避するポート実装。</summary>
    /// <param name="window">対象のウィンドウ。</param>
    /// <remarks>
    /// 非表示（<c>Hide</c>）は復帰時の再作成でちらつくため使わず、位置の変更（画面外配置）で退避する。<br/>
    /// 復帰時は前面へ戻す（撮影後にユーザーの操作対象へ戻すため。Avalonia 版はオーバーレイの閉じる操作が担う）。
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

            // 画面外（仮想スクリーン座標の外）へ退避する。負の大きな座標は Windows で
            // 標準的に使われる「画面外配置」であり、ヒットテスト・検出の対象外になる。
            window.MoveTo(OffScreenDip, OffScreenDip);
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

    /// <summary>オーナーを退避させる画面外の座標（DIP）。</summary>
    private const double OffScreenDip = -32000;

    /// <summary>透明を解除するまでに待つフレーム数。</summary>
    /// <remarks>
    /// 表示直後のフレームはテーマ背景だけで、画像は次のフレームで合成される（実測）。<br/>
    /// そのため 2 フレーム目（画像が合成されたフレーム）で透明を解除する。
    /// </remarks>
    private const int FramesUntilReveal = 2;

    /// <summary>画像の平均色を求める。</summary>
    /// <param name="image">対象の画像。</param>
    /// <returns>平均色（不透明）。</returns>
    /// <remarks>全画素を走査すると時間がかかるため、間引いて標本する（表示までの遅延を作らない）。</remarks>
    private static Color AverageColor(EsBitmap image)
    {
        // ToSKBitmap は内部インスタンスの共有参照を返すため破棄しない
        var bitmap = image.ToSKBitmap();
        var step = Math.Max(1, Math.Min(bitmap.Width, bitmap.Height) / 40);
        long red = 0, green = 0, blue = 0;
        var count = 0;

        for (var y = 0; y < bitmap.Height; y += step)
        {
            for (var x = 0; x < bitmap.Width; x += step)
            {
                var pixel = bitmap.GetPixel(x, y);
                red += pixel.Red;
                green += pixel.Green;
                blue += pixel.Blue;
                count++;
            }
        }

        return count == 0
            ? Color.Black
            : Color.FromRgb((byte)(red / count), (byte)(green / count), (byte)(blue / count));
    }

    /// <summary>キャプチャ本体を実行する。</summary>
    /// <param name="services">アプリ全体で共有するコアオブジェクト。</param>
    /// <param name="owner">オーナーウィンドウ。</param>
    /// <param name="type">キャプチャ種別。</param>
    /// <param name="windowService">ネイティブウィンドウ操作（オーバーレイの物理配置に使用する）。</param>
    /// <returns>切り出した画像。キャンセル・取得失敗・退化した選択の場合は null。</returns>
    /// <remarks>キャプチャ除外の設定・解除とオーナーの退避・復元は呼び出し側（<see cref="Capture"/>）が担う。</remarks>
    private static EsBitmap? CaptureCore(
        Window owner, CaptureType type, CaptureOverlayServices services, CaptureSettings settings)
    {
        var finder = services.Finder;

        // 全モニターを包含する 1 枚の画像として取得する（Avalonia 版と同じ）。
        // 1 モニターに限定すると、カーソルのあるモニターでしか選択できない（別のモニターへ
        // 移動しての選択操作ができない）ため、仮想スクリーン全体を対象にする。
        var screen = services.Capture.CaptureVirtualScreen(out var origin);
        if (screen is null)
        {
            return null;
        }

        // 画像の (0, 0) が仮想スクリーン原点（画面物理座標）に対応する
        var bounds = new EsPixelRect(origin.X, origin.Y, screen.Width, screen.Height);
        var session = new CaptureOverlay(screen, bounds, type, settings, finder, services.Logger);
        session._log.LogCaptureStart(type.ToString());
        try
        {
            session.Show(owner, services);
        }
        finally
        {
            screen.Dispose();
        }

        return session._result;
    }

    /// <summary>オーバーレイを表示し、確定またはキャンセルまで待つ。</summary>
    /// <param name="owner">オーナーウィンドウ。</param>
    /// <param name="settings">設定（ルーペの外観）。</param>
    /// <param name="windowService">ネイティブウィンドウ操作（物理ピクセルでの配置に使用する）。</param>
    /// <remarks>
    /// ウィンドウは仮想スクリーンの位置・サイズでボーダーレス・最前面に表示する。<br/>
    /// 確定・キャンセルのいずれでもウィンドウを閉じ、<see cref="ShowDialog"/> の入れ子ループを終わらせる。
    /// </remarks>
    private void Show(Window owner, CaptureOverlayServices services)
    {
        ArgumentNullException.ThrowIfNull(owner);

        // セッションを開始する（完了は 1 回だけ受け付ける）
        _state.TryBegin();

        // モニターごとの実倍率を解決する（サイズラベルの DisplayScale 用。構成はセッション中に変わらない）
        var monitorScales = new List<(EsPixelRect Bounds, double Scale)>();
        foreach (var screen in services.Finder.ListScreens())
        {
            var screenScale = DpiHelper.GetDpiForPoint(new Point(screen.Bounds.X, screen.Bounds.Y)) / 96.0;
            if (screenScale > 0)
            {
                monitorScales.Add((screen.Bounds, screenScale));
            }
        }

        _monitorScales = monitorScales;
        var content = new Grid().Columns("*").Rows("*");
        content.Add(new ImageView { Source = _screen, IsUniform = false });

        var window = new Window()
            .Title(services.Title)
            .Borderless()
            .Topmost()
            .Content(content);

        // 透明の間はウィンドウの背景色だけが合成されるため、透明を解除した瞬間の 1 フレームは
        // 背景色が見える。背景を撮影画像の平均色に合わせて、その差を目立たなくする。
        window.Background = AverageColor(_screen);

        // ウィンドウは表示時に背景色で塗られるため、そのまま表示すると画像を描くまでの
        // 数十 ms が背景色の面として見える（実測: 65 ms 表示）。そのため透明な状態で表示し、
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

        // ウィンドウ余白（Window 既定スタイルの ContainerPadding）を 0 にして、
        // 撮影画像をウィンドウ端まで敷く（余白があると縁が太い枠のように見える）
        window.Padding = Thickness.Zero;

        // 位置とサイズの DIP は生成時のスケール（プライマリモニターの DPI）で解釈される（実測）。
        // 生成直後のちらつきを抑えるため、仮想スクリーン全体のサイズを先に与える。
        // 最大化は使わない（OS が 1 モニターへ寄せてしまうため、全モニターを覆えない）。
        var creationScale = DpiHelper.GetDpiForPoint(new Point(0, 0)) / 96.0;
        window.WindowSize = WindowSize.Resizable(
            WindowPlacementMath.ToDipLength(_screen.Width, creationScale),
            WindowPlacementMath.ToDipLength(_screen.Height, creationScale));

        _selector.SetLoupeSource(_screen);
        _selector.LoupeOffset = _settings.LoupeOffset!.Value;
        ApplyLabels(_settings, mode: ToRenderMode(_type));
        content.Add(_selector);

        // 確定・キャンセルは選択コントロールからの要求とウィンドウのキー入力の両方から受け付ける
        _selector.ConfirmRequested += (_, _) => Confirm();
        _selector.CancelRequested += (_, _) => Cancel();

        // 他のウィンドウへ切り替わった（非アクティブ化した）場合はキャンセルする
        // （最前面のオーバーレイが残り続けるのを防ぐ。Avalonia 版と同じ扱い）
        window.Deactivated += Cancel;

        window.Loaded += () =>
        {
            // 画像ピクセル ↔ DIP の倍率は表示に使うウィンドウの DPI で決める
            // （ウィンドウのクライアント座標は物理ピクセルで届くため、この倍率で画像ピクセルへ戻せる。
            //  モニターごとに倍率が異なっても、ウィンドウの倍率は 1 つに定まるため全域で同じ式が使える）
            var scale = DpiHelper.GetDpiForWindow(window.Handle) / 96.0;
            if (scale > 0)
            {
                _scale = scale;
                _selector.SetCoordinateTransform(CreatePort());
            }
        };

        // 位置は生成時のスケールで解釈される
        var position = new EsPoint(
            WindowPlacementMath.ToDipLength((int)_screenBounds.X, creationScale),
            WindowPlacementMath.ToDipLength((int)_screenBounds.Y, creationScale));

        window.StartManualPosition(position.X, position.Y);

        window.PreviewKeyDown += e =>
        {
            switch (e.Key)
            {
                case Key.Enter:
                    Confirm();
                    e.Handled = true;
                    break;
                case Key.Escape:
                    Cancel();
                    e.Handled = true;
                    break;
            }
        };

        if (_finder is not null)
        {
            // スクリーン選択 / ウィンドウ選択: カーソル下の要素をハイライトし、クリックで確定する
            // （選択コントロールのドラッグ選択は表示されないため、入力はウィンドウが受け取る）
            window.MouseMove += e => Highlight(ToScreenPoint(e.ScreenPosition));
            window.MouseDown += e =>
            {
                // 確定は左ボタンのみ（右クリック等では確定しない。Avalonia 版と同じ）
                if (e.Button != MouseButton.Left)
                {
                    return;
                }

                Highlight(ToScreenPoint(e.ScreenPosition));

                // 要素を検出できない位置（ウィンドウ選択でデスクトップ等）では確定しない
                // （キャンセルは Esc。誤クリックで撮影を取りこぼさない）
                if (_selector.HasHighlight)
                {
                    Confirm();
                }
            };
        }

        window.ShowDialog(owner);
    }

    /// <summary>サイズラベル・ラベル（操作ラベル・情報ラベル）を選択コントロールへ反映する。</summary>
    /// <param name="settings">設定。</param>
    /// <param name="mode">オーバーレイ描画モード（操作ラベルの文言選定に使用する）。</param>
    /// <remarks>
    /// 1. サイズラベルはドメイン座標（＝画像ピクセル）で表示するため、テキストは描画のたびに生成する。<br/>
    /// 2. サブライン（操作ラベル・情報ラベル）の文言・順序・外観はコアの <c>FadeMaskAppearance.BuildSubLines</c> が単一所有する。<br/>
    /// 3. 描画モードとウィンドウタイトルをコントロールへ渡し、行の組み立てはコントロールに委ねる（タイトルの更新時に本メソッドを呼び直す）。
    /// </remarks>
    private void ApplyLabels(CaptureSettings settings, FadeMaskRenderMode mode)
    {
        var overlay = _selector.Overlay;
        overlay.Appearance = overlay.Appearance with
        {
            ShowSizeLabel = settings.SizeLabelVisible!.Value,
            SizeLabelColor = settings.SizeLabelColor ?? DrawingColor.White,
            SizeLabelBackgroundColor = settings.TextBackgroundColor ?? DrawingColor.FromArgb(153, 0, 0, 0),
            SizeLabelFontSize = settings.SizeLabelFontSize!.Value,
            ShowSubLines = settings.ShowSubLines!.Value,
            SubLineFontSize = settings.SubLineFontSize!.Value,
            SubLineColor = settings.SubLineColor ?? DrawingColor.FromArgb(255, 220, 220, 220),
            SubLineBackgroundColor = settings.SubLineBackgroundColor ?? DrawingColor.FromArgb(153, 0, 0, 0),
            InfoLabelVisible = settings.InfoLabelVisible!.Value,
            InfoLabelFontSize = settings.InfoLabelFontSize!.Value,
            InfoLabelColor = settings.InfoLabelColor ?? DrawingColor.White,
            InfoLabelBackgroundColor = settings.InfoLabelBackgroundColor ?? DrawingColor.FromArgb(153, 0, 0, 0),
        };

        // サイズラベルは画像ピクセル（ドメイン座標）で表示する（表示時点の選択を持ち出す）
        // DisplayScale は選択位置のモニターの実表示スケール（DIP → 画像ピクセルの倍率）を表示する
        overlay.SizeLabelOverrideProvider = () =>
            _selection() is { } rect && settings.SizeLabelVisible!.Value
                ? FadeMaskLabels.FormatSizeLabel(rect, scaling: 1.0, displayScaling: DisplayScalingAt(rect))
                : null;

        // 描画モードとウィンドウタイトル（情報ラベルの文言）を渡し、行の組み立てはコントロールに委ねる
        overlay.Mode = mode;
        overlay.WindowTitle = _windowTitle;
    }

    /// <summary>キャプチャ種別に対応するオーバーレイ描画モードを求める。</summary>
    /// <param name="type">キャプチャ種別。</param>
    /// <returns>オーバーレイ描画モード。</returns>
    private static FadeMaskRenderMode ToRenderMode(CaptureType type) => type switch
    {
        CaptureType.ScreenSelect => FadeMaskRenderMode.ScreenSelect,
        CaptureType.WindowSelect => FadeMaskRenderMode.WindowSelect,
        _ => FadeMaskRenderMode.AreaSelect,
    };

    /// <summary>確定して選択領域を切り出す。</summary>
    /// <remarks>
    /// 選択が空・退化している場合はキャンセルと同じ扱いにする（0 サイズの画像を作らない）。<br/>
    /// 切り出す範囲は各 UI 共通の仕様（始点・終点のピクセルを含む閉区間）に揃える<br/>
    /// （計算は <see cref="CropBoundsMath"/>（コア）が単一所有する）。<br/>
    /// 切り出し結果の所有権は呼び出し側へ移す（仮想スクリーン画像はセッション終了時に破棄される）。
    /// </remarks>
    private void Confirm()
    {
        if (_selection() is not { } rect)
        {
            Cancel();
            return;
        }

        // 切り出し範囲を求める。範囲選択は「始点・終点のピクセルを含む」閉区間とし、
        // スクリーン選択・ウィンドウ選択の矩形は検出結果が全ピクセルを含むため加算しない
        // （計算は CropBoundsMath（コア）が単一所有する。Avalonia 版と同じ規則）
        var closedInterval = _type == CaptureType.AreaSelect;
        var pixels = CropBoundsMath.ComputeCropBounds(
            rect, new EsSize(_screen.Width, _screen.Height), _settings.MinSelectionSize!.Value, closedInterval);
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

        _log.LogCaptureCompleted(FormatRect(pixels));
        _result = _screen.Crop(new System.Drawing.Rectangle(pixels.X, pixels.Y, pixels.Width, pixels.Height));
        Close();
    }

    /// <summary>キャンセルする（切り出しを行わずに閉じる）。</summary>
    private void Cancel()
    {
        if (!_state.TryComplete())
        {
            return;
        }

        _log.LogCaptureCancelled();
        _result = null;
        Close();
    }

    /// <summary>ログ用に矩形を整形する。</summary>
    /// <param name="rect">矩形（画面物理ピクセル）。</param>
    /// <returns><c>X,Y,Width,Height</c> 形式の文字列。</returns>
    private static string FormatRect(EsPixelRect rect) => $"({rect.X}, {rect.Y}, {rect.Width}, {rect.Height})";

    /// <summary>現在のオーバーレイウィンドウを閉じる。</summary>
    /// <remarks>
    /// ウィンドウはフィールドに保持しないため、表示中のウィンドウ（<c>Application.Current.AllWindows</c> の末尾）を閉じる。
    /// </remarks>
    private void Close()
        => Application.Current?.AllWindows.LastOrDefault()?.Close();

    /// <summary>カーソル下の要素を検出してハイライトを更新する。</summary>
    /// <param name="screenPoint">カーソル位置（画面物理ピクセル）。</param>
    /// <remarks>
    /// 検出対象はキャプチャ種別で決まる。スクリーン選択はカーソル下のモニター（画面全体）、<br/>
    /// ウィンドウ選択はカーソル下の最前面ウィンドウを検出し、その矩形をハイライトする。
    /// </remarks>
    private void Highlight(EsPixelPoint screenPoint)
    {
        if (_finder is not { } finder)
        {
            return;
        }

        // スクリーン選択はモニター境界、ウィンドウ選択はウィンドウ境界をハイライトする
        var found = _type == CaptureType.ScreenSelect
            ? finder.FindScreenUnderCursor(screenPoint)
            : finder.FindWindowUnderCursor(screenPoint);

        _selector.HasHighlight = found.IsDetected;
        _selector.LogicalHighlightRect = found.IsDetected ? ToDomain(found.Bounds) : null;

        // 情報ラベル（ウィンドウタイトル）は検出結果に追従させる（見つからない場合は空にして非表示にする）
        // スクリーン検出時の Title は「Screen {インデックス}」だが、情報ラベルはコアの
        // ShouldShowInfoLabel がウィンドウ選択に限定して判定するため、スクリーン選択では表示されない
        var title = found.IsDetected ? found.Title ?? "" : "";
        if (title != _windowTitle)
        {
            _windowTitle = title;
            ApplyLabels(_settings, ToRenderMode(_type));
            _selector.InvalidateOverlay();
        }
    }

    /// <summary>マウス位置（画面物理ピクセル）をそのままコア型へ変換する。</summary>
    /// <param name="position">画面物理ピクセルの座標。</param>
    /// <returns>画面物理ピクセルの座標（四捨五入）。</returns>
    /// <remarks>
    /// MewUI の <c>MouseEventArgs.ScreenPosition</c> はデバイスピクセル（＝画面物理ピクセル）で返るため、<br/>
    /// DPI スケールを掛けずにそのまま使える（要素検出サービスと同じ座標系）。
    /// </remarks>
    private static EsPixelPoint ToScreenPoint(Point position)
        => new((int)Math.Round(position.X), (int)Math.Round(position.Y));

    /// <summary>画面物理ピクセルの矩形をドメイン座標（画像ピクセル）へ変換する。</summary>
    /// <param name="bounds">画面物理ピクセルの矩形。</param>
    /// <returns>画像ピクセル座標の矩形。</returns>
    /// <remarks>対象モニターの画像の (0, 0) はモニターの原点に対応するため、原点分を差し引く。</remarks>
    private EsRect ToDomain(EsPixelRect bounds)
        => new(bounds.X - _screenBounds.X, bounds.Y - _screenBounds.Y, bounds.Width, bounds.Height);

    /// <summary>選択コントロールへ注入する座標変換ポートを生成する。</summary>
    /// <returns>ローカル DIP ⇔ 画像ピクセルを変換するポート。</returns>
    /// <remarks>
    /// 画像はウィンドウのクライアント領域全体へ等倍で描画するため、Forward は DIP の乗算（× 倍率）だけになる。<br/>
    /// 境界は画像サイズ（ドメイン座標）で、選択を画像外へ出さないためのクランプに使われる。
    /// </remarks>
    private ICoordTransform CreatePort()
        => new CoordTransform(
            p => new EsPoint(p.X * _scale, p.Y * _scale),
            p => new EsPoint(p.X / _scale, p.Y / _scale),
            () => new EsSize(_screen.Width, _screen.Height));

    /// <summary>選択矩形（ドメイン座標）が属するモニターの表示倍率を返す。</summary>
    /// <param name="domainRect">選択矩形（画像ピクセル = 仮想スクリーン座標から原点を引いた値）。</param>
    /// <returns>そのモニターの DPI スケール（1.0 = 100%）。該当が無い場合はウィンドウの倍率。</returns>
    /// <remarks>ドメイン座標を画面物理座標（原点を加算）へ戻してからモニターを判定する。</remarks>
    private double DisplayScalingAt(EsRect domainRect)
    {
        var x = (int)Math.Round(domainRect.X) + _screenBounds.X;
        var y = (int)Math.Round(domainRect.Y) + _screenBounds.Y;

        foreach (var (bounds, scale) in _monitorScales)
        {
            if (x >= bounds.X && x < bounds.Right && y >= bounds.Y && y < bounds.Bottom)
            {
                return scale;
            }
        }

        return _scale;
    }

    /// <summary>設定の外観を選択コントロールへ反映する。</summary>
    /// <param name="selector">対象の選択コントロール。</param>
    /// <param name="settings">設定。</param>
    private static void ApplyAppearance(FadeMaskDragRectControl selector, CaptureSettings settings)
    {
        // 暗転マスクの外観は 1 つの値へ組み立てて一括設定する（枠線はドラッグ矩形と共用される）
        selector.Appearance = new FadeMaskAppearance
        {
            OverlayColor = settings.OverlayColor ?? DrawingColor.FromArgb(128, 0, 0, 0),
            BorderColor = settings.BorderColor ?? DrawingColor.DarkGray,
            BorderThickness = settings.BorderThickness!.Value,
        };
        selector.LoupeOffset = settings.LoupeOffset!.Value;
        // 指定する項目だけを差し替える（枠線の色・太さは selector 側のプロパティが設定するため現在値を維持する）
        selector.DragRect.Options = selector.DragRect.Options with
        {
            HandleColor = settings.HandleColor ?? DrawingColor.White,
            HandleSize = settings.HandleSize!.Value,
            HandleHitRadius = settings.HandleHitRadius!.Value,
            MinSelectionSize = settings.MinSelectionSize!.Value,
            ClampToDomainBounds = settings.ClampToDomainBounds!.Value,
            MarkerColor = settings.MarkerColor ?? DrawingColor.White,
            MarkerLength = settings.MarkerLength!.Value,
            ShowCoordLabel = settings.ShowCoordLabel!.Value,
            CoordLabelColor = settings.CoordLabelColor ?? DrawingColor.White,
            CoordLabelBackgroundColor = settings.CoordLabelBackgroundColor ?? DrawingColor.FromArgb(153, 0, 0, 0),
            CoordLabelFontSize = settings.CoordLabelFontSize!.Value,
        };

        ApplyLoupe(selector, settings);
    }

    /// <summary>設定のルーペ項目を選択コントロール（内包するルーペ）へ反映する。</summary>
    /// <param name="selector">対象の選択コントロール。</param>
    /// <param name="settings">設定（全メンバー non-null）。</param>
    /// <param name="checkerBackground">市松模様の下地色（テーマのページ背景色。null の場合は下地を塗らない）。</param>
    /// <remarks>
    /// 市松色は半透明のため、下地がないと後ろの画像が透ける（撮影オーバーレイは後ろが撮影画像、<br/>
    /// プレビューの切り出しは後ろがプレビュー画像）。下地色はホストがテーマから解決して渡す。<br/>
    /// ルーペは「撮影オーバーレイ」「プレビューの切り出し」の両方に登場するため、反映内容を本メソッドへ<br/>
    /// 集約し、UI ごとのずれを防ぐ（Avalonia 版の <c>LoupeAppearance</c> と同内容）。
    /// </remarks>
    internal static void ApplyLoupe(
        FadeMaskDragRectControl selector, CaptureSettings settings, DrawingColor? checkerBackground = null)
    {
        var loupe = selector.Loupe;

        loupe.ZoomLevel = settings.LoupeZoomLevel!.Value;
        loupe.RequestedSize = settings.LoupeSize!.Value;
        loupe.CrossColor = ToMewUIColor(settings.LoupeCrossColor, MewUIColor.FromArgb(102, 255, 0, 0));
        loupe.FrameColor = ToMewUIColor(settings.LoupeFrameColor, MewUIColor.FromArgb(255, 211, 211, 211));
        loupe.FrameWidth = settings.LoupeFrameThickness!.Value;
        loupe.CheckerSize = settings.LoupeCheckerSize!.Value;
        loupe.CheckerColorA = ToMewUIColor(settings.LoupeCheckerColorA, MewUIColor.Transparent);
        loupe.CheckerColorB = ToMewUIColor(settings.LoupeCheckerColorB, MewUIColor.Transparent);

        if (checkerBackground is { } background)
        {
            loupe.BackgroundColor = MewUIColor.FromArgb(background.A, background.R, background.G, background.B);
        }

        loupe.ShowCoordLabel = settings.LoupeShowCoordLabel!.Value;
        loupe.CoordLabelColor = ToMewUIColor(settings.LoupeCoordLabelColor, MewUIColor.White);
        loupe.CoordLabelBackgroundColor = ToMewUIColor(settings.LoupeCoordLabelBackgroundColor, MewUIColor.FromArgb(153, 0, 0, 0));
        loupe.CoordLabelFontSize = settings.LoupeCoordLabelFontSize!.Value;

        // ルーペ画像上の選択矩形破線はルーペコントロールではなく選択コントロールが描画する
        selector.SelectionOverlayColorA = settings.LoupeSelectionRectColorA ?? DrawingColor.White;
        selector.SelectionOverlayColorB = settings.LoupeSelectionRectColorB ?? DrawingColor.Black;
    }

    /// <summary>色を MewUI の色型へ変換する（null の場合は既定色を返す）。</summary>
    /// <param name="color">変換する色（null 可）。</param>
    /// <param name="fallback">null の場合に使用する色。</param>
    /// <returns>MewUI の色。</returns>
    private static MewUIColor ToMewUIColor(DrawingColor? color, MewUIColor fallback)
        => color is { } c ? MewUIColor.FromArgb(c.A, c.R, c.G, c.B) : fallback;
}
