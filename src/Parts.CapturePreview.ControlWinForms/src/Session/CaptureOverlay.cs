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
using EsUtil.SaveEvidence.Parts.CapturePreview.WinForms.SelectionOverlay;
using EsUtil.SaveEvidence.Platform.WinForms;
using EsUtil.SaveEvidence.Platform.WinForms.Imaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.WinForms.Session;

/// <summary>画面全体を覆うキャプチャ選択オーバーレイ（WinForms 版）。</summary>
/// <remarks>
/// 撮影から選択・確定までの 1 セッションを担い、確定した領域を <see cref="EsBitmap"/> として返す。<br/>
/// <b>1 セッション = 1 インスタンス</b>であり、生成・実行・破棄は Facade（<see cref="CaptureSessionControl"/>）が担う<br/>
/// （本クラスは公開しない。 排他の受付は Facade がコアの <c>CaptureSessionSlot</c> で行う）。<br/>
/// <br/>
/// <b>全体の流れ</b>:<br/>
/// 1. 設定に従ってオーナーウィンドウを画面外へ退避する（<see cref="CaptureSettings.HideOwnerWindow"/>）。<br/>
/// 2. 仮想スクリーン（全モニタ）を 1 枚の画像として取得する（<see cref="ICaptureService"/>）。<br/>
/// 3. 取得した画像を敷いたボーダーレス・最前面のフォームを仮想スクリーン全面へ表示する。<br/>
/// 4. 選択を確定すると、選択矩形で画像を切り出して返す（Escape はキャンセル）。<br/>
/// 5. オーナーウィンドウを元の位置へ戻す。<br/>
/// <br/>
/// <b>座標系</b>: 対象 = 画像ローカルピクセル（画像の左上が原点）。 撮影範囲の原点（仮想スクリーンの原点。<br/>
/// 負値になりうる）が画像の (0, 0) に対応するため、画面物理ピクセルとは原点の分だけ異なる。<br/>
/// 画像ローカルとローカル DIP の変換は <see cref="ICoordTransform"/> が担い、選択コントロールへ注入する。<br/>
/// <b>切り出し矩形の求解は画面物理座標に統一する</b>（選択矩形（画像ローカル）は確定の入口で<br/>
/// <see cref="CropClipMath.ToScreenRect(EsRect, EsPointInt)"/> により戻す）。<br/>
/// <br/>
/// <b>モード</b>:<br/>
/// - 範囲選択（<see cref="CaptureType.AreaSelect"/>）: ドラッグで矩形を指定する（選択オーバーレイの範囲選択モード）。<br/>
/// - スクリーン選択 / ウィンドウ選択: カーソル下の要素をハイライトし、左ボタンの解放または Enter で確定する。<br/>
/// <br/>
/// <b>WinForms 固有の構成（他の UI 版との差）</b>:<br/>
/// - オーバーレイは<b>モーダル ダイアログにしない</b>（入れ子ループを作らない）。 <see cref="Form.Show(IWin32Window)"/> で表示し、<br/>
///   フォームが閉じるまでをタスクで待つ（Facade がセッションの生成・破棄と排他の解放を担えるようにする）。<br/>
/// - ちらつきの抑制は不透明度で行う（画像が描かれる前の 1 フレームを見せない）。<br/>
/// - マウス入力は<b>画像を敷いたビュー</b>が受け取る（入力を受けないオーバーレイは無効状態になり、入力が親へ通る）。
/// </remarks>
internal sealed class CaptureOverlay
{
    /// <summary>ロガー（ホストが注入する。 null の場合は出力しない）。</summary>
    private readonly ILogger _log;

    /// <summary>選択確定の結果（キャンセル時は null）。</summary>
    /// <remarks>
    /// 結果の単一確定（確定とキャンセルの競合で二重完了にしない）はコアの <see cref="CaptureCompletion{TResult}"/> が単一所有する。<br/>
    /// 結果の型はコア型（<see cref="CaptureResult"/>）とし、UI 型は境界より外へ出さない。
    /// </remarks>
    private readonly CaptureCompletion<CaptureResult?> _result = new();

    /// <summary>取得したスクリーン画像（読み取りの面。 セッション中のみ保持し、終了時に破棄する）。</summary>
    private EsReadOnlyBitmap? _screen;

    /// <summary>取得対象のスクリーンの矩形（画面物理ピクセル）。</summary>
    private EsRectInt _screenBounds;

    /// <summary>取得したスクリーン画像を取得する。</summary>
    /// <value>撮影した仮想スクリーンの画像（読み取りの面）。</value>
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

    /// <summary>キャプチャ完了の通知先（結果をホストへ中継する）。</summary>
    private readonly Action<CaptureResult?>? _onCaptured;

    /// <summary>表示中のオーバーレイ フォーム（未表示の場合は null）。</summary>
    private Form? _overlay;

    /// <summary>キャプチャ選択オーバーレイを初期化する。</summary>
    /// <param name="type">キャプチャ種別。</param>
    /// <param name="settings">設定（全メンバー non-null）。</param>
    /// <param name="finder">要素検出サービス（ハイライト型のみ使用する）。</param>
    /// <param name="log">ロガー（null の場合は出力しない）。</param>
    /// <param name="onCaptured">キャプチャ完了の通知先（オーナーの再表示より前に呼ばれる）。</param>
    /// <remarks>
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
            Log = _log,
            // 設定はコアのマッパーで 1 値に変換して一括適用する（変換はコアが単一所有する）。
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
    /// <param name="owner">オーナーウィンドウ（撮影中は画面外へ退避し、終了後に戻す）。 null 不可。</param>
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
    /// ウィンドウを再作成させない。<br/>
    /// <b>対象範囲</b>: 全モニター（仮想スクリーン）を対象にする。 1 モニターに限定すると、<br/>
    /// カーソルのあるモニターでしか選択できないため。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> が null の場合。</exception>
    internal async Task<CaptureResult?> RunAsync(Form owner, CaptureOverlayServices services)
    {
        ArgumentNullException.ThrowIfNull(owner);

        // オーナーの退避とキャプチャ除外の設定・解除は CaptureOwnerScope（コア）が単一所有する
        // （手順: 除外の設定 → 画面外へ退避 → 撮影 → 配置の復帰 → 除外の解除）
        using var ownerScope = CaptureOwnerScope.Enter(
            services.Window, owner.ToEsPlatformHandle(), new OwnerWindowAdapter(owner),
            hideOwner: _settings.HideOwnerWindow!.Value);

        // 全モニターを包含する 1 枚の画像として取得する。
        // 1 モニターに限定すると、カーソルのあるモニターでしか選択できないため、仮想スクリーン全体を対象にする
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
            // 確定・キャンセルまで待つ（入れ子ループを作らない非同期の待機。 ホストの継続は呼び出し側へ戻る）
            await ShowAsync(owner, services).ConfigureAwait(true);
            // 確定・キャンセルは必ず結果を確定させてからフォームを閉じるため、待機は即座に返る
            var result = await _result.Task.ConfigureAwait(true);
            // 完了の記録（確定は 2 件・キャンセルは 1 件）はコアが単一所有する
            _log.LogCaptureFinished(result);
            // キャプチャ完了イベントを発行する（同期実行）。
            // C# のイベントは同期型のため、ホストのハンドラ（画像変更）が完了してから
            // 以下の finally（オーナー再表示）が実行される。
            // フロー: アプリ非表示 → キャプチャ → 画像変更（イベント受信） → アプリ表示（再表示）
            _onCaptured?.Invoke(result);
            return result;
        }
        finally
        {
            // 画像はセッションの終了時に破棄する（以降の描画・切り出しは発生しない）
            _screen?.Dispose();
            _screen = null;
        }
    }

    /// <summary>進行中の撮影セッションをキャンセルする。</summary>
    /// <remarks>
    /// Facade（<see cref="CaptureSessionControl"/>）が進行中のセッションを取り消す際にも呼ぶ。<br/>
    /// 完了の記録とホストへの通知は <see cref="RunAsync"/>（待機側）が行う。
    /// </remarks>
    internal void Cancel()
    {
        if (!_state.TryComplete())
        {
            return;
        }

        _log.LogCancelRequested();
        _result.TrySetResult(null);
        CloseOverlay();
    }

    /// <summary>オーナーウィンドウ（WinForms）を撮影時に画面外へ退避するポート実装。</summary>
    /// <param name="window">対象のウィンドウ。</param>
    /// <remarks>
    /// 非表示（<c>Hide</c>）は使わない（復帰時にウィンドウが再作成され、ちらつきが出る）。<br/>
    /// 復帰時は前面へ戻す（撮影後にユーザーの操作対象へ戻すため）。
    /// </remarks>
    private sealed class OwnerWindowAdapter(Form window) : ICaptureOwnerWindow
    {
        /// <summary>退避前の配置（未退避の場合は null）。</summary>
        private Rectangle? _savedBounds;

        /// <inheritdoc/>
        public bool IsVisible => window.Visible;

        /// <inheritdoc/>
        public void HideOffScreen()
        {
            _savedBounds = window.Bounds;

            // 画面外（仮想スクリーン座標の外）へ退避する。 負の大きな座標は Windows で
            // 標準的に使われる「画面外配置」であり、ヒットテスト・検出の対象外になる。
            // 値（画面外座標）はコアが単一所有する
            var offScreen = CaptureTiming.OwnerOffScreenCoord;
            window.Location = new Point(offScreen, offScreen);
        }

        /// <inheritdoc/>
        public void RestorePlacement()
        {
            if (_savedBounds is not { } bounds)
            {
                return;
            }

            window.Bounds = bounds;
            window.Activate();
            _savedBounds = null;
        }
    }

    /// <summary>オーバーレイを表示し、確定またはキャンセルまで待つ。</summary>
    /// <param name="owner">オーナーウィンドウ。</param>
    /// <param name="services">オーバーレイの実行に必要なサービス（撮影サービス・ウィンドウ操作）。</param>
    /// <returns>フォームが閉じるまで待機するタスク。</returns>
    /// <remarks>
    /// フォームは仮想スクリーンの位置・サイズでボーダーレス・最前面に表示する。<br/>
    /// 確定・キャンセルのいずれでもフォームを閉じ、待機タスクが完了する。<br/>
    /// <b>モーダル表示（<c>ShowDialog</c>）を使わない</b>: 入れ子ループではセッションの開始から終了まで<br/>
    /// 呼び出し側のコードが進まず、Facade がセッションの生成・破棄と排他の解放を担えない。
    /// </remarks>
    private async Task ShowAsync(Form owner, CaptureOverlayServices services)
    {
        // セッションを開始する（完了は 1 回だけ受け付ける）
        _state.TryBegin();

        // モニターごとの実倍率を解決する（サイズラベルの表示倍率用。 構成はセッション中に変わらない）。
        // ハイライト型のみがサイズラベルの表示倍率を使う（ドラッグ選択は対応するモニターを持たない）。
        // 倍率が未確定のモニターの除外はコア（MonitorScalingMath）が単一所有する
        if (CaptureTypeTraits.IsHighlighted(_type))
        {
            _monitorScales = MonitorScalingMath.Collect(services.Finder.ListScreens().Select(
                screen => (screen.Bounds, MonitorDpi.GetScaleForPoint(screen.Bounds.TopLeft))));
        }

        // 撮影画像は仮想スクリーンと等倍（領域と同じ縦横比）のため、領域全体へ敷く（Uniform にしない）
        var imageView = new GdiImageView
        {
            Dock = DockStyle.Fill,
            Source = Screen,
        };

        var form = new Form
        {
            FormBorderStyle = FormBorderStyle.None,
            StartPosition = FormStartPosition.Manual,
            ShowInTaskbar = false,
            TopMost = true,
            KeyPreview = true,
            // 透明の間はフォームの背景色だけが合成されるため、透明を解除した瞬間の 1 フレームは
            // 背景色が見える。 背景を撮影画像の平均色に合わせて、その差を目立たなくする
            BackColor = ImageColorMath.AverageColor(Screen),
            // 位置とサイズは画面物理ピクセルで指定する（生成直後のちらつきを抑える）
            Bounds = _screenBounds.ToRectangle(),
        };
        _overlay = form;

        _selector.Visible = true;
        imageView.Controls.Add(_selector);
        _selector.BringToFront();
        form.Controls.Add(imageView);

        // 十字カーソルはドラッグで矩形を作る種別（ハイライト型以外 = 範囲選択）で使う
        // （選択スタイルの判定はコアが単一所有する）
        if (!CaptureTypeTraits.IsHighlighted(_type))
        {
            imageView.Cursor = Cursors.Cross;
        }

        _selector.SetLoupeSource(Screen);
        ApplyLabels();

        // 確定・キャンセルは選択コントロールからの要求とキー入力の両方から受け付ける
        _selector.ConfirmRequested += OnSelectorConfirmRequested;
        _selector.CancelRequested += (_, _) => Cancel();

        // Enter = 確定 / Escape = キャンセル（どのキーがどの操作かの解釈はコアが単一所有する）。
        // 確定対象が無い場合の Enter は何もしない（確定できない状態でキャンセル扱いにしない）
        form.KeyDown += (_, e) =>
        {
            // 確定対象は 1 度だけ解決して効果へ渡す（再解決しない）
            var target = ResolveSelection();
            switch (CaptureKeyActions.Resolve(e.KeyCode.ToEsKey(), target is not null))
            {
                case CaptureKeyAction.Confirm when target is { } localRect:
                    // 選択矩形は画像ローカル座標のため、確定の入口（画面物理座標）へ揃えてから渡す
                    ConfirmScreen(CropClipMath.ToScreenRect(localRect, _screenBounds.TopLeft));
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
                case CaptureKeyAction.Cancel:
                    Cancel();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
            }
        };

        if (CaptureTypeTraits.IsHighlighted(_type))
        {
            // スクリーン選択 / ウィンドウ選択: カーソル下の要素をハイライトし、クリックで確定する
            // （選択オーバーレイは入力を受け取らないため、入力は画像のビューが受け取る）
            imageView.MouseMove += (_, e) => Highlight(ToScreenPoint(e.Location, imageView));
            // 押下で検出した要素を固定し、解放で確定する（固定と取り出しの規則はコアが単一所有する）
            imageView.MouseDown += (_, e) =>
            {
                if (e.Button != MouseButtons.Left)
                {
                    return;
                }

                // 検出矩形（画面物理）を固定する。 退化した検出結果は固定されない
                _clickSelection.Fix(
                    CaptureTypeTraits.FindUnderCursor(_finder, _type, ToScreenPoint(e.Location, imageView)).Bounds);
            };
            imageView.MouseUp += (_, e) =>
            {
                if (e.Button != MouseButtons.Left)
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

        // 画像が描かれる前の 1 フレームを見せない（透明で表示し、表示後に不透明へ戻す）
        form.Opacity = 0;

        var closed = new TaskCompletionSource();
        form.Shown += (_, _) =>
        {
            // 位置とサイズは OS の作業領域補正を受けない物理ピクセル指定で確定させる
            try
            {
                services.Window.SetBounds(form.ToEsPlatformHandle(), _screenBounds);
            }
            catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
            {
                // 配置できないと全モニターの選択ができないが、表示の完了を例外で中断すると
                // セッションが終了できなくなるため、境界で記録して続行する
                _log.LogOperationError(ex, nameof(RunAsync));
            }

            // 画像ピクセル ↔ DIP の倍率は表示に使うウィンドウの DPI で決める
            // （フォームのクライアント座標は物理ピクセルで届くため、この倍率で画像ピクセルへ戻せる）
            var scale = DpiScaler.ToScaling(form.DeviceDpi);
            if (DpiScaler.IsValidScaling(scale))
            {
                _scale = scale;
                _selector.SetCoordTransform(CreatePort());
            }

            form.Opacity = 1;
            // 他のウィンドウへ切り替わった（非アクティブ化した）場合はキャンセルする
            // （最前面のオーバーレイが残り続けるのを防ぐ）
            form.Deactivate += (_, _) => Cancel();
        };
        form.FormClosed += (_, _) => closed.TrySetResult();

        form.Show(owner);

        await closed.Task.ConfigureAwait(true);
    }

    /// <summary>サイズラベル・情報ラベルの文言を選択コントロールへ反映する。</summary>
    /// <remarks>
    /// 1. サイズラベルは対象座標（＝画像ピクセル）で表示するため、テキストは描画のたびに生成する。<br/>
    /// 2. 外観（サイズラベル・情報ラベルの色・サイズ）は設定値の変換（<c>SelectionOverlayOptionsMapper</c>）が単一所有する<br/>
    ///    （本メソッドでは設定しない）。<br/>
    /// 3. 操作ラベル・情報ラベルの文言をホスト（キャプチャ文脈）がプロバイダで与える<br/>
    ///    （モード別の文言と情報ラベルの表示可否は <c>CaptureLabelTexts</c> が単一所有する。<br/>
    ///    表示名はコアの <see cref="CaptureHighlightSession"/> を参照する）。<br/>
    /// 4. 表示する矩形の選択（ハイライト型 / ドラッグ型）はコア（<see cref="CaptureTypeTraits.ResolveRect"/>）が単一所有し、<br/>
    ///    サイズラベルの文言（書式）もコアが単一所有する（本ホストは表示スケールだけを与える）。
    /// </remarks>
    private void ApplyLabels()
    {
        // サイズラベルの文言（書式）はコアが単一所有し、本ホストは矩形を含むモニタの実表示スケールだけを提供する
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
    /// ハイライト矩形はコアのハイライト状態（<see cref="CaptureHighlightSession"/>）から読む。
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
    /// <see cref="CropClipMath.ToScreenRect(EsRect, EsPointInt)"/> により戻してから渡す。
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
        // 完了の記録とホストへの通知は RunAsync（待機側）が行う（フォームは結果を確定させて閉じるだけ）
        _result.TrySetResult(CaptureResult.From(image, pixels));
        CloseOverlay();
    }

    /// <summary>選択オーバーレイの確定要求（Enter キー・ダブルクリック等）を処理する。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">選択矩形イベント（矩形は対象座標 = 画像ピクセル）。</param>
    /// <remarks>選択矩形（画像ローカル座標）を確定の入口（画面物理座標）へ揃えて渡す。</remarks>
    private void OnSelectorConfirmRequested(object? sender, SelectionConfirmedEventArgs e)
        => ConfirmScreen(CropClipMath.ToScreenRect(e.SelectionSourceRect, _screenBounds.TopLeft));

    /// <summary>現在のオーバーレイ フォームを閉じる。</summary>
    private void CloseOverlay()
    {
        var overlay = _overlay;
        _overlay = null;
        overlay?.Close();
    }

    /// <summary>カーソル下の要素を検出してハイライトを更新する。</summary>
    /// <param name="screenPoint">カーソル位置（画面物理ピクセル）。</param>
    /// <remarks>
    /// 検出対象はキャプチャ種別で決まる。 スクリーン選択はカーソル下のモニター（画面全体）、<br/>
    /// ウィンドウ選択はカーソル下の最前面ウィンドウを検出し、その矩形をハイライトする。<br/>
    /// 矩形の対象座標への変換・未検出の正規化・表示名の保持・ログはコア<br/>
    /// （<see cref="CaptureHighlightSession"/>）が単一所有する。
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

    /// <summary>クライアント座標（物理ピクセル）を画面物理ピクセルの座標へ変換する。</summary>
    /// <param name="location">コントロールのクライアント座標（物理ピクセル）。</param>
    /// <param name="source">座標の基準となるコントロール。</param>
    /// <returns>画面物理ピクセルの座標（四捨五入）。</returns>
    /// <remarks>
    /// 要素検出サービスは画面物理ピクセルを要求するため、クライアント座標を画面座標へ戻す。<br/>
    /// 丸めはコア（<see cref="CropBoundsMath.RoundToPixel"/>）が単一所有する<br/>
    /// （既定の <see cref="Math.Round(double)"/> は銀行丸めで、座標の四捨五入と 1 ピクセルずれる）。
    /// </remarks>
    private static EsPointInt ToScreenPoint(Point location, Control source)
    {
        var screen = source.PointToScreen(location);
        return new EsPointInt(CropBoundsMath.RoundToPixel(screen.X), CropBoundsMath.RoundToPixel(screen.Y));
    }

    /// <summary>選択コントロールへ注入する座標変換ポートを生成する。</summary>
    /// <returns>ローカル DIP ⇔ 画像ピクセルを変換するポート。</returns>
    /// <remarks>
    /// 画像はフォームのクライアント領域全体へ等倍で描画するため、変換は倍率の乗除算だけになる。<br/>
    /// 倍率の適用方向（ToSource = DIP → 画像ピクセル）は <see cref="CoordTransform.ClientToSource(Func{double}, Func{EsSize?})"/> が<br/>
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
    /// 「原点の加算」「判定点（矩形の中心）」「フォールバック」の規則はコア（<see cref="MonitorScalingMath"/>）が単一所有する。
    /// </remarks>
    private double DisplayScalingAt(EsRect sourceRect)
        => MonitorScalingMath.ResolveForSourceRect(
            sourceRect, _screenBounds.TopLeft, _monitorScales, _scale);
}
