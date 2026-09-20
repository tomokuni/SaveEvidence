using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Estable.FadeMask;
using Estable.FadeMaskDragRect;
using Estable.MewUI.FadeMask;
using Estable.MewUI.FadeMaskDragRect;
using Estable.Platform;
using Estable.Platform.Coordinate;
using Estable.Platform.Geometry;
using Estable.Platform.Imaging;
using Estable.SaveEvidence.Models;
using Microsoft.Extensions.Logging;
using SaveEvidence.MewUI.Logging;
using SaveEvidence.MewUI.Services;
using DrawingColor = System.Drawing.Color;

namespace SaveEvidence.MewUI.Views;

/// <summary>画面全体を覆うキャプチャ選択オーバーレイ（MewUI 版）。</summary>
/// <remarks>
/// Avalonia 版 <c>SnipCaptureUI</c> の <c>OverlayWindow</c> に相当する。撮影から選択・確定までの<br/>
/// 1 セッションを担い、確定した領域を <see cref="EsBitmap"/> として返す。<br/>
/// <br/>
/// <b>全体の流れ</b>:<br/>
/// 1. 設定に従ってオーナーウィンドウを非表示にする（<c>HideMainWindowOnCapture</c>）。<br/>
/// 2. 仮想スクリーン（全モニタ）を 1 枚の画像として取得する（<see cref="ICaptureService"/>）。<br/>
/// 3. 取得した画像を敷いたボーダーレス・最前面のウィンドウを表示する。<br/>
/// 4. 選択を確定すると、選択矩形で画像を切り出して返す（Escape はキャンセル）。<br/>
/// 5. オーナーウィンドウを再表示して元の状態へ戻す。<br/>
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
internal sealed class CaptureOverlayWindow
{
    /// <summary>ロガー（Avalonia 版と同じログファイルへ出力する）。</summary>
    private static readonly ILogger s_logger = AppLog.CreateLogger(nameof(CaptureOverlayWindow));

    /// <summary>選択確定の結果（キャンセル時は null の <see cref="Bitmap"/>）。</summary>
    private EsBitmap? _result;

    /// <summary>取得したスクリーン画像（セッション中のみ保持し、終了時に破棄する）。</summary>
    private readonly EsBitmap _screen;

    /// <summary>取得対象のスクリーンの矩形（画面物理ピクセル）。</summary>
    private readonly EsPixelRect _screenBounds;

    /// <summary>ローカル DIP → 画像ピクセルの倍率。</summary>
    /// <remarks>表示に使うウィンドウの DPI で決まるため、ウィンドウの生成後に <see cref="Show"/> で確定する。</remarks>
    private double _scale = 1.0;

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

    /// <summary>確定またはキャンセルでセッションを終了したかどうか。</summary>
    /// <remarks>終了後に非アクティブ化・閉じるが重なっても二重終了（二重ログ）にしない。</remarks>
    private bool _isFinished;

    /// <summary>設定（ラベルの再組み立てに使用する）。</summary>
    private Settings _settings = null!;

    /// <summary>オーバーレイを初期化する（内部用。<see cref="Capture"/> から生成する）。</summary>
    /// <param name="screen">取得したスクリーン画像（所有権は本クラスへ移る）。</param>
    /// <param name="screenBounds">取得対象のスクリーンの矩形（画面物理ピクセル）。</param>
    /// <param name="type">キャプチャ種別。</param>
    /// <param name="settings">設定（選択枠・暗転・ルーペ・ラベルの外観）。</param>
    /// <param name="finder">要素検出サービス（スクリーン選択 / ウィンドウ選択で使用）。</param>
    private CaptureOverlayWindow(
        EsBitmap screen, EsPixelRect screenBounds, CaptureType type,
        Settings settings, IElementFinder finder)
    {
        _screen = screen;
        _screenBounds = screenBounds;
        _type = type;

        _selector = new FadeMaskDragRectControl { Mode = ToMode(type), Log = s_logger };
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
    /// <param name="owner">オーナーウィンドウ（非表示・再表示の対象）。</param>
    /// <param name="type">キャプチャ種別。</param>
    /// <returns>切り出した画像。キャンセル・取得失敗・退化した選択の場合は null。</returns>
    /// <remarks>
    /// 1. 設定に従ってオーナーを非表示にし、DWM の反映を待つ。<br/>
    /// 2. カーソルのあるモニターを対象に決め、その領域をキャプチャする（失敗した場合はオーナーを戻して null）。<br/>
    /// 3. オーバーレイを表示し、確定またはキャンセルまで待つ。<br/>
    /// 4. オーナーを再表示し、取得画像を破棄して切り出し結果を返す。<br/>
    /// <b>対象モニター</b>: 全モニターを跨ぐ 1 枚のオーバーレイは OS の配置補正で位置がずれる（実測）ため、<br/>
    /// カーソルのあるモニターだけを対象にする（一般的なスニッピングツールと同じ挙動）。
    /// </remarks>
    public static EsBitmap? Capture(AppServices services, Window owner, CaptureType type)
    {
        var settings = services.Settings.Current;
        var wasVisible = owner.IsVisible;

        if (settings.HideMainWindowOnCapture)
        {
            owner.Hide();

            // 非表示がコンポジションへ反映されるまで待つ（待たないと自ウィンドウが写り込む）
            var delay = (int)Math.Clamp(settings.HideBeforeCaptureDelayMs, 0, 1000);
            if (delay > 0)
            {
                Thread.Sleep(delay);
            }
        }

        var finder = PlatformFactory.CreateElementFinder();
        var screens = finder.ListScreens();
        var cursor = PlatformFactory.CreateCursorService().GetCursorScreenPoint();

        // カーソルを含むモニターを対象にする（見つからない場合は先頭のモニター）
        var target = CaptureSelectionMath.SelectScreen(cursor, [.. screens.Select(static s => s.Bounds)])
            ?? (screens.Count > 0 ? screens[0].Bounds : new EsPixelRect(0, 0, 0, 0));
        if (target.Width <= 0 || target.Height <= 0)
        {
            Restore(owner, wasVisible);
            return null;
        }

        var screen = PlatformFactory.CreateCaptureService().CaptureArea(target);
        if (screen is null)
        {
            Restore(owner, wasVisible);
            return null;
        }

        var session = new CaptureOverlayWindow(screen, target, type, settings, finder);
        s_logger.LogCaptureStart(type.ToString());
        try
        {
            session.Show(owner, settings);
        }
        finally
        {
            screen.Dispose();
            Restore(owner, wasVisible);
        }

        return session._result;
    }

    /// <summary>オーナーウィンドウを元の表示状態へ戻す。</summary>
    /// <param name="owner">対象のウィンドウ。</param>
    /// <param name="wasVisible">キャプチャ開始前に表示されていたかどうか。</param>
    /// <remarks>
    /// アプリ終了中（オーナーがクローズ済み）にホットキーが届いた場合は復元できない（<c>Show</c> が例外になる）ため、<br/>
    /// 復元は失敗しても無視する（アプリの終了処理を妨げない）。
    /// </remarks>
    private static void Restore(Window owner, bool wasVisible)
    {
        if (!wasVisible)
        {
            return;
        }

        try
        {
            owner.Show();
            owner.Activate();
        }
        catch (InvalidOperationException)
        {
            // オーナーが既に閉じられている（アプリ終了中のホットキー等）場合は復元しない
        }
    }

    /// <summary>オーバーレイを表示し、確定またはキャンセルまで待つ。</summary>
    /// <param name="owner">オーナーウィンドウ。</param>
    /// <param name="settings">設定（ルーペの外観）。</param>
    /// <remarks>
    /// ウィンドウは仮想スクリーンの位置・サイズ（DIP）でボーダーレス・最前面に表示する。<br/>
    /// 確定・キャンセルのいずれでもウィンドウを閉じ、<see cref="ShowDialog"/> の入れ子ループを終わらせる。
    /// </remarks>
    private void Show(Window owner, Settings settings)
    {
        ArgumentNullException.ThrowIfNull(owner);

        _settings = settings;
        var content = new Grid().Columns("*").Rows("*");
        content.Add(new ImageView { Source = _screen, IsUniform = false });

        var window = new Window()
            .Title("SaveEvidence キャプチャ")
            .Borderless()
            .Topmost()
            .Content(content);

        // 撮影中の一時的な表示のため、タスクバー・Alt+Tab の一覧には出さない
        // （Avalonia 版は HideFromTaskSwitcher で除外する）
        window.ShowInTaskbar = false;

        // ウィンドウ余白（Window 既定スタイルの ContainerPadding）を 0 にして、
        // 撮影画像をウィンドウ端まで敷く（余白があると縁が太い枠のように見える）
        window.Padding = Thickness.Zero;

        // 位置とサイズの DIP は生成時のスケール（プライマリモニターの DPI）で解釈される（実測）。
        // 生成直後のちらつきを抑えるため、対象モニター全面のサイズを先に与える
        var creationScale = DpiHelper.GetDpiForPoint(new Point(0, 0)) / 96.0;
        window.WindowSize = WindowSize.Resizable(
            WindowPlacementMath.ToDipLength(_screen.Width, creationScale),
            WindowPlacementMath.ToDipLength(_screen.Height, creationScale));

        _selector.SetLoupeSource(_screen);
        _selector.LoupeOffset = settings.LoupeOffset;
        ApplyLabels(settings, mode: ToRenderMode(_type));
        content.Add(_selector);

        // 確定・キャンセルは選択コントロールからの要求とウィンドウのキー入力の両方から受け付ける
        _selector.ConfirmRequested += (_, _) => Confirm();
        _selector.CancelRequested += (_, _) => Cancel();

        // 他のウィンドウへ切り替わった（非アクティブ化した）場合はキャンセルする
        // （最前面のオーバーレイが残り続けるのを防ぐ。Avalonia 版と同じ扱い）
        window.Deactivated += Cancel;

        window.Loaded += () =>
        {
            // 物理サイズは OS（最大化）に任せる。MewUI の DIP → 物理の換算はプライマリモニターの
            // スケールを使うため、倍率が異なるモニターでは DIP 指定だと物理サイズがモニターと一致しない（実測）。
            // 最大化なら OS が対象モニターの実サイズへ合わせる。
            window.WindowState = WindowState.Maximized;

            // 画像ピクセル ↔ DIP の倍率は表示に使うウィンドウの DPI で決める
            // （対象モニターの倍率と異なる場合があるため、実際のウィンドウの DPI を使う）
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

    /// <summary>サイズラベル・サブライン（操作ラベル・情報ラベル）を選択コントロールへ反映する。</summary>
    /// <param name="settings">設定。</param>
    /// <param name="mode">オーバーレイ描画モード（操作ラベルの文言選定に使用する）。</param>
    /// <remarks>
    /// 1. サイズラベルはドメイン座標（＝画像ピクセル）で表示するため、テキストは描画のたびに生成する。<br/>
    /// 2. 操作ラベルの文言は <c>FadeMaskLabels.GetOperationText</c>（コア）が単一所有する。<br/>
    /// 3. 情報ラベル（ウィンドウタイトル）はスクリーン選択 / ウィンドウ選択でのみ表示する（コアの判定）。<br/>
    /// 4. 行の組み立てはラベルが変わった時点で行う（タイトルの更新時は本メソッドを呼び直す）。
    /// </remarks>
    private void ApplyLabels(Settings settings, FadeMaskRenderMode mode)
    {
        var overlay = _selector.Overlay;
        overlay.ShowSizeLabel = settings.SizeLabelVisible;
        overlay.SizeLabelColor = SettingsColorMath.ToDrawingColor(settings.CaptureTextColor, DrawingColor.White);
        overlay.SizeLabelBackgroundColor = SettingsColorMath.ToDrawingColor(settings.SizeLabelBackgroundColor, DrawingColor.FromArgb(153, 0, 0, 0));
        overlay.SizeLabelFontSize = settings.SizeLabelFontSize;

        // サイズラベルは画像ピクセル（ドメイン座標）で表示する（表示時点の選択を持ち出す）
        // DisplayScale は画面の実表示スケール（DIP → 画像ピクセルの倍率 = モニターの DPI スケール）を表示する
        overlay.SizeLabelTextProvider = () =>
            _selection() is { } rect && settings.SizeLabelVisible
                ? FadeMaskLabels.FormatSizeLabel(rect, scaling: 1.0, displayScaling: _scale)
                : null;

        overlay.SubLines = BuildSubLines(settings, mode);
    }

    /// <summary>サブライン（2 行目以降）を組み立てる。</summary>
    /// <param name="settings">設定。</param>
    /// <param name="mode">オーバーレイ描画モード。</param>
    /// <returns>サブラインの一覧。表示する行がない場合は空。</returns>
    /// <remarks>
    /// 2 行目 = 操作ラベル（モード別の確定・キャンセル方法。設定 <c>ShowSubLines</c> で表示可否）。<br/>
    /// 3 行目 = 情報ラベル（ウィンドウタイトル。モードとタイトルの有無で表示可否をコアが判定する）。
    /// </remarks>
    private IReadOnlyList<FadeMaskSubLine> BuildSubLines(Settings settings, FadeMaskRenderMode mode)
    {
        var lines = new List<FadeMaskSubLine>(2);

        if (settings.ShowSubLines)
        {
            lines.Add(new FadeMaskSubLine(
                FadeMaskLabels.GetOperationText(mode),
                settings.SubLineFontSize,
                SettingsColorMath.ToDrawingColor(settings.SubLineColor, DrawingColor.FromArgb(255, 220, 220, 220)),
                SettingsColorMath.ToDrawingColor(settings.SubLineBackgroundColor, DrawingColor.FromArgb(153, 0, 0, 0))));
        }

        if (FadeMaskLabels.ShouldShowInfoLabel(mode, settings.InfoLabelVisible, _windowTitle))
        {
            lines.Add(new FadeMaskSubLine(
                _windowTitle,
                settings.InfoLabelFontSize,
                SettingsColorMath.ToDrawingColor(settings.InfoLabelColor, DrawingColor.White),
                SettingsColorMath.ToDrawingColor(settings.InfoLabelBackgroundColor, DrawingColor.FromArgb(153, 0, 0, 0))));
        }

        return lines;
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
    /// 切り出す範囲は各 UI 共通の仕様（始点・終点のピクセルを含む閉区間）に揃える（<see cref="CaptureSelectionMath.ToClosedInterval"/>）。<br/>
    /// 切り出し結果の所有権は呼び出し側へ移す（仮想スクリーン画像はセッション終了時に破棄される）。
    /// </remarks>
    private void Confirm()
    {
        if (_isFinished)
        {
            return;
        }

        if (_selection() is not { } rect)
        {
            Cancel();
            return;
        }

        var pixels = CaptureSelectionMath.ToPixelRect(rect, _screen.Width, _screen.Height);
        if (pixels.IsEmpty)
        {
            Cancel();
            return;
        }

        // 範囲選択は「始点・終点のピクセルを含む」閉区間のため、終点の 1 ピクセルを加える
        // （スクリーン選択・ウィンドウ選択の矩形は検出結果が全ピクセルを含むため加算しない。Avalonia 版と同じ規則）
        if (_type == CaptureType.AreaSelect)
        {
            pixels = CaptureSelectionMath.ToClosedInterval(pixels, _screen.Width, _screen.Height);
        }

        _isFinished = true;
        s_logger.LogCaptureCompleted(FormatRect(pixels));
        _result = _screen.Crop(pixels);
        Close();
    }

    /// <summary>キャンセルする（切り出しを行わずに閉じる）。</summary>
    private void Cancel()
    {
        if (_isFinished)
        {
            return;
        }

        _isFinished = true;
        s_logger.LogCaptureCancelled();
        _result = null;
        Close();
    }

    /// <summary>ログ用に矩形を整形する。</summary>
    /// <param name="rect">矩形（画面物理ピクセル）。</param>
    /// <returns><c>X,Y,Width,Height</c> 形式の文字列。</returns>
    private static string FormatRect(System.Drawing.Rectangle rect) => $"({rect.X}, {rect.Y}, {rect.Width}, {rect.Height})";

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

    /// <summary>設定の外観を選択コントロールへ反映する。</summary>
    /// <param name="selector">対象の選択コントロール。</param>
    /// <param name="settings">設定。</param>
    private static void ApplyAppearance(FadeMaskDragRectControl selector, Settings settings)
    {
        selector.OverlayColor = SettingsColorMath.ToDrawingColor(settings.CaptureOverlayColor, DrawingColor.FromArgb(128, 0, 0, 0));
        selector.BorderColor = SettingsColorMath.ToDrawingColor(settings.CaptureBorderColor, DrawingColor.DarkGray);
        selector.BorderThickness = settings.CaptureBorderThickness;
        selector.LoupeOffset = settings.LoupeOffset;
        selector.DragRect.HandleColor = SettingsColorMath.ToDrawingColor(settings.HandleColor, DrawingColor.White);
        selector.DragRect.HandleSize = settings.HandleSize;
        selector.DragRect.HandleHitRadius = settings.HandleHitRadius;
        selector.DragRect.MinSelectionSize = settings.MinSelectionSize;
        selector.DragRect.ClampToDomainBounds = settings.ClampToDomainBounds;
        selector.DragRect.MarkerColor = SettingsColorMath.ToDrawingColor(settings.MarkerColor, DrawingColor.White);
        selector.DragRect.MarkerLength = settings.MarkerLength;
        selector.DragRect.ShowCoordLabel = settings.ShowCoordLabel;
        selector.DragRect.CoordLabelColor = SettingsColorMath.ToDrawingColor(settings.CoordLabelColor, DrawingColor.White);
        selector.DragRect.CoordLabelBackgroundColor = SettingsColorMath.ToDrawingColor(settings.CoordLabelBackgroundColor, DrawingColor.FromArgb(153, 0, 0, 0));
        selector.DragRect.CoordLabelFontSize = settings.CoordLabelFontSize;

        // ルーペの外観（拡大率・サイズ・色）は LoupeAppearance が単一所有する
        LoupeAppearance.Apply(selector.Loupe, settings);
        LoupeAppearance.ApplySelectionOverlay(selector, settings);
    }
}
