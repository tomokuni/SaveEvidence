using Aprillz.MewUI;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Platform.Core.Native;
using EsUtil.Platform.Core.Native.Win;
using Microsoft.Extensions.Logging;
using EsUtil.SaveEvidence.Parts.CapturePreview.MewUI.Crop;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Capture;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Crop;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Logging;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Session;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Setting;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.MewUI.Session;

/// <summary>撮影と切り出しのセッションを統括する Facade（MewUI 版）。</summary>
/// <remarks>
/// <b>適用パターン</b>: Facade（撮影・切り出しの 2 セッションの窓口を 1 つの API にまとめる）。<br/>
/// Avalonia 版の <c>CaptureSessionControl</c> と<b>同じ公開 API</b>を持ち、<b>本ライブラリの公開 API は本クラスだけ</b>である。<br/>
/// <br/>
/// <b>生成と破棄の単一所有</b>: セッション（撮影オーバーレイ・切り出し）と、それらが必要とするサービス<br/>
/// （キャプチャ・要素検出・ウィンドウ操作）は<b>本クラスが生成し、本クラスが破棄する</b>。<br/>
/// ホストが同じ組み立て（サービスの生成・オーバーレイの表示ツリーへの追加・取り外し）を書く必要はない<br/>
/// （Avalonia 版と同じ分担。 ホストは「いつ開始するか」「結果をどう反映するか」だけを担う）。<br/>
/// <br/>
/// <b>排他の単一所有</b>: 撮影と切り出しは同時に 1 つだけ実行できる。 この規則はコアの<br/>
/// <c>CaptureSessionSlot</c> が単一所有し、本クラスがその唯一の適用者になる（両 UI で同じ規則が働く）。<br/>
/// 実行中の要求は無視する（既存のセッションを中断しない）。<br/>
/// <br/>
/// <b>セッションの寿命</b>: 撮影（<see cref="StartCaptureAsync"/>）はオーバーレイが閉じるまで、<br/>
/// 切り出し（<see cref="StartCropAsync"/>）は確定・キャンセルまで長生きする。 どちらもセッションごとに<br/>
/// インスタンスを生成し、終了時に破棄する（切り出しのオーバーレイは表示ツリーへ加えて、終了時に取り外す）。<br/>
/// <br/>
/// <b>UI 固有のホスト結線</b>: プレビューの切り出しは既存のビュー（<see cref="CropViewControl"/>）を操作するため、<br/>
/// ホストが生成したコントロールを本クラスへ渡す（切り出しの対象画像は <see cref="CropViewControl.Source"/> が単一所有する）。<br/>
/// <br/>
/// <b>テーマ</b>: 切り出しのルーペの市松模様の下地色はテーマに依存する。 テーマ変更の購読はホストが行い、<br/>
/// <see cref="AppearanceSettings"/> で反映する（本クラスはテーマの通知元を知らない）。
/// </remarks>
public sealed class CaptureSessionControl : IDisposable
{
    /// <summary>キャプチャが完了したときに発生するイベント（同期実行）。</summary>
    /// <remarks>
    /// <b>同期型イベント</b>のため、ハンドラの完了後にキャプチャセッションの後続処理（オーナー再表示）が実行される。<br/>
    /// ホストは本イベントの受信時に画像変更（キャプチャ結果の反映）を行うことで、<br/>
    /// 「APP 非表示 → キャプチャ → 画像変更（イベント受信） → APP 表示（再表示）」の順序を保証できる。<br/>
    /// 引数はキャプチャ結果（キャンセル時は null）。 UI スレッドで発行される。
    /// </remarks>
    public event EventHandler<CaptureCompletedEventArgs>? CaptureCompleted;

    /// <summary>進行中のセッション（撮影と切り出しで同時に 1 つまで）。</summary>
    /// <remarks>排他規則（片方が実行中なら他方も開始できない）はコアの <see cref="CaptureSessionSlot{TCapture, TCrop}"/> が単一所有する。</remarks>
    private readonly CaptureSessionSlot<CaptureOverlay, CropSession> _slot = new();

    /// <summary>内部ロガー（インスタンス単位。 複数インスタンスで混線しない）。</summary>
    private readonly ILogger _log;

    /// <summary>画面キャプチャサービス（Facade が生成・所有する。 Avalonia 版と同じ分担）。</summary>
    private readonly ICaptureService _captureService;

    /// <summary>要素検出サービス（カーソル下のモニター・ウィンドウ）。</summary>
    private readonly IElementFinder _finder;

    /// <summary>ネイティブウィンドウ操作サービス（キャプチャ除外・退避等）。</summary>
    private readonly IWindowService _windowService;

    /// <summary>設定（全メンバー non-null）。 撮影と切り出しが同じ値を使う（本クラスが単一所有する）。</summary>
    private CaptureSettings _settings;

    /// <summary>破棄済みかどうか。</summary>
    private bool _isDisposed;

    /// <summary>ログの接頭辞を取得または設定する（ロガーと一緒に設定する）。</summary>
    /// <value>接頭辞（例: <c>[CapturePreview]</c>）。 null または空白を設定すると既定値へ戻す。</value>
    /// <remarks>本スタックのログ定義（<see cref="CapturePreviewLogMessage"/>）の接頭辞を設定する。</remarks>
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

    /// <summary>インスタンスを生成する。</summary>
    /// <param name="initialSettings">初期設定。 null メンバーはデフォルト値が使用される。</param>
    /// <param name="logger">内部ログの転送先。 null の場合はログを出力しない。</param>
    /// <exception cref="ArgumentOutOfRangeException">設定値が許容範囲外の場合。</exception>
    /// <remarks>
    /// 撮影に必要なサービス（キャプチャ・要素検出・ウィンドウ操作）は本クラスが生成する。<br/>
    /// UI 非依存のサービスはコア（<c>EsUtil.Platform.Core.Native.Win</c>）が提供するため、ホストの組み立ては不要である。
    /// </remarks>
    public CaptureSessionControl(CaptureSettings? initialSettings = null, ILogger? logger = null)
    {
        initialSettings?.Validate();
        // 入力なし（null）は既定値で埋める（規則はコアが単一所有する）
        _settings = CaptureSettingsMerge.Merge(initialSettings);
        _log = logger.OrNullLogger();
        _captureService = new WinCaptureService(_log);
        _finder = new WinElementFinder(_log);
        _windowService = new WinWindowService(_log);
    }

    /// <summary>現在の設定（全メンバー non-null）を取得または設定する。</summary>
    /// <value>現在有効な設定。</value>
    /// <remarks>
    /// 設定時は null メンバーのみ現在値を維持し、それ以外を反映する（部分更新）。<br/>
    /// 検証・同値再設定のスキップ・適用ログはコア（<see cref="CaptureSettingsMerge.Apply"/>）が単一所有する。<br/>
    /// 実行中の切り出しセッションへは即時反映する（設定画面での変更を次の開始まで待たない）。<br/>
    /// 撮影はモーダル全画面のため、実行中の設定変更は対象外（開始時の値を使う）。
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">設定値が許容範囲外の場合。</exception>
    public CaptureSettings AppearanceSettings
    {
        get
        {
            ThrowIfDisposed();
            return _settings;
        }
        set
        {
            ThrowIfDisposed();

            // 部分更新のマージ・検証・同値スキップ・適用ログはコアが単一所有する
            if (!CaptureSettingsMerge.Apply(value, ref _settings, _log)) return;

            // 実行中の切り出しセッションへ最新設定を再適用する（切り出し中の設定変更の即時反映）
            _slot.Crop?.ApplySettings(_settings);
        }
    }

    /// <summary>撮影（スクリーン選択 / ウィンドウ選択 / 領域選択）を開始する。 UI スレッドから呼び出すこと。</summary>
    /// <param name="type">キャプチャ種別。</param>
    /// <param name="owner">オーナーウィンドウ（撮影中は画面外へ退避し、終了後に戻す）。</param>
    /// <returns>撮影結果（切り出した画像と領域）。 キャンセル・取得失敗・退化した選択の場合は null。</returns>
    /// <remarks>
    /// 1. 撮影セッションを生成し、排他の受付を試みる（実行中のセッションがある場合は何もせず null を返す）。<br/>
    /// 2. 受付できた場合は撮影セッションを実行する（画像の取得・オーバーレイの表示・確定待ち・画像の破棄）。<br/>
    /// 3. 終了したら受付を解放する（例外で終わっても解放する）。<br/>
    /// 確定した画像の反映（Undo 退避・プレビューの差し替え）はホストの <see cref="CaptureCompleted"/> ハンドラが担う<br/>
    /// （イベントはオーナーの再表示より前に発行される。 Avalonia 版と同じ順序）。<br/>
    /// 設定は <see cref="AppearanceSettings"/> の現在値を渡す（ホストが撮影用に別途設定を組み立てない）。
    /// </remarks>
    public async Task<CaptureResult?> StartCaptureAsync(CaptureType type, Window owner)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(owner);

        try
        {
            var session = new CaptureOverlay(
                type, _settings, _finder, _log,
                // キャプチャ完了イベントを公開イベントへ中継する（同期実行 → ホストの画像変更後に再表示）
                onCaptured: result => CaptureCompleted?.Invoke(this, new CaptureCompletedEventArgs(result)));

            if (!_slot.TryBeginCapture(session))
            {
                return null; // 再入禁止（他のセッションが実行中）
            }

            try
            {
                return await session.RunAsync(owner, CreateServices()).ConfigureAwait(true);
            }
            finally
            {
                _slot.EndCapture(session);
            }
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 公開ファサードとして内部の例外を集約して記録し、呼び出し側へ伝搬する（例外処理規約 §4）
            _log.LogOperationError(ex, nameof(StartCaptureAsync));
            throw;
        }
    }

    /// <summary>切出し（Crop）を開始する。 UI スレッドから呼び出すこと。</summary>
    /// <param name="cropView">切り出しを行う複合コントロール（ズーム + 領域選択 + ルーペを内包）。 表示中の画像が切り出しの対象になる。</param>
    /// <param name="hostWindow">ホストウィンドウ。</param>
    /// <returns>確定時は元画像上の切り出し範囲（コア型）、キャンセル時は 0x0 の空矩形。 開始できなかった場合は null。</returns>
    /// <exception cref="ArgumentException"><paramref name="cropView"/> が表示している画像のサイズが 0 の場合。</exception>
    /// <remarks>
    /// 切り出しの対象画像は <paramref name="cropView"/> が表示している画像（<see cref="CropViewControl.Source"/>）から解決する<br/>
    /// （画像を別引数で受け取らない。 表示している画像と切り出す画像を二重所有しない）。<br/>
    /// 画像が設定されていない場合は、開始できなかったことを null の結果で表す（実行中の再呼び出しと同じ表現）。<br/>
    /// 切り出しの境界（元画像の範囲）は開始時に固定せず、確定時に<b>現在の画像</b>から都度解決する<br/>
    /// （実行中に画像が差し替わっても、差し替え後の画像の範囲で切り出せる）。<br/>
    /// ズーム・パン・Fit・座標変換は <paramref name="cropView"/> が内包する ZoomViewControl が単一所有する。<br/>
    /// 切出し中のホイール操作（Ctrl+ホイールのズーム・通常ホイールのパン）は <paramref name="cropView"/> が内部で完結する（上位の配線は不要）。<br/>
    /// 実行中に同一インスタンスへ再呼び出しされた場合は完了済みのタスク（結果 null）を返す。<br/>
    /// 結果の型はコア型（<see cref="EsRectInt"/>）とし、UI 型は境界より外へ出さない（Avalonia 版と同じ契約）。
    /// </remarks>
    public Task<EsRectInt?> StartCropAsync(CropViewControl cropView, Window hostWindow)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(cropView);
        ArgumentNullException.ThrowIfNull(hostWindow);

        try
        {
            // 切り出しの対象は表示中の画像（CropViewControl.Source が単一所有する）
            if (cropView.Source is not { } sourceImage)
            {
                return Task.FromResult<EsRectInt?>(null); // 対象が無い: 開始できなかったことを null で表す
            }

            // 「サイズが 0 の画像は切り出せない」という前提条件はコアが単一所有する
            CropClipMath.ValidateImage(sourceImage, nameof(cropView));

            var session = new CropSession(cropView, hostWindow, _settings, _log);
            if (!_slot.TryBeginCrop(session))
            {
                // 再入禁止（他のセッションが実行中）: 何も起きなかったことを null の結果で表す
                return Task.FromResult<EsRectInt?>(null);
            }

            return AwaitCropAsync(session, sourceImage);
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 公開ファサードとして内部の例外を集約して記録し、呼び出し側へ伝搬する（例外処理規約 §4）
            _log.LogOperationError(ex, nameof(StartCropAsync));
            throw;
        }
    }

    /// <summary>切り出し中かどうかを取得する。</summary>
    /// <value>切り出しセッションが進行中の場合は true。</value>
    /// <remarks>
    /// 「実行中か」の真のソースは排他スロット（コアが単一所有する）である。<br/>
    /// ホストが自前のフラグ（開始したタスクの有無等）で判定すると、スロットの状態と二重所有になる。
    /// </remarks>
    public bool IsCropping
    {
        get
        {
            ThrowIfDisposed();
            return _slot.HasCrop;
        }
    }

    /// <summary>切り出し操作（開始・確定・キャンセル）のうち、いま行うべき操作を取得する。</summary>
    /// <returns>実行すべき切り出し操作。</returns>
    /// <remarks>
    /// どの操作を行うかの判断はコア（<see cref="CropSessionActions.Resolve"/>）が単一所有する<br/>
    /// （未開始なら開始 / 選択済みなら確定 / 未選択ならキャンセル）。<br/>
    /// ホストは戻り値を効果へ写すだけにする（キーとボタンで同じ規則を使う）。
    /// </remarks>
    public CropSessionAction ResolveCropAction()
    {
        ThrowIfDisposed();

        try
        {
            return CropSessionActions.Resolve(_slot.HasCrop, _slot.HasCropSelection(static crop => crop.HasSelection));
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            _log.LogOperationError(ex, nameof(ResolveCropAction));
            throw;
        }
    }

    /// <summary>進行中の操作（<see cref="StartCaptureAsync"/> / <see cref="StartCropAsync"/>）をキャンセルする。</summary>
    /// <remarks>
    /// - 切り出し実行中: セッションをキャンセルし、0x0 の空結果で完了する。<br/>
    /// - 撮影実行中: オーバーレイウィンドウを閉じてキャンセルする（非アクティブ化経由でキャンセル処理が走る）。<br/>
    /// - 操作なし: 何もしない。
    /// </remarks>
    public void Cancel()
    {
        ThrowIfDisposed();

        try
        {
            _slot.CancelAll(static s => s.Cancel(), static s => s.Cancel());
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            _log.LogOperationError(ex, nameof(Cancel));
            throw;
        }
    }

    /// <summary>進行中の切り出し（Crop）を現在の選択矩形で確定する（Enter キー相当）。</summary>
    /// <remarks>
    /// 切り出し実行中のみ有効。 選択確定時のみ複合コントロールの <see cref="CropViewControl.RequestConfirm"/> に委譲する。<br/>
    /// 撮影実行中 / 操作なしでは何もしない（撮影の確定はユーザー操作で完結する）。
    /// </remarks>
    public void Confirm()
    {
        ThrowIfDisposed();

        try
        {
            _slot.Crop?.Confirm();
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            _log.LogOperationError(ex, nameof(Confirm));
            throw;
        }
    }

    /// <summary>実行中の切り出しセッションへビューポート（パン・ズーム・表示サイズ）の変化を通知する。</summary>
    /// <remarks>
    /// ホスト（上位 AP）がプレビューのビューポート変化を検知した際に呼び出す。<br/>
    /// 切り出し実行中のみ有効（未実行時は何もしない）。 暗転マスク・選択枠・ルーペがビューポート変化に追従する。
    /// </remarks>
    public void NotifyViewportChanged()
    {
        ThrowIfDisposed();

        try
        {
            _slot.Crop?.NotifyViewportChanged();
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            _log.LogOperationError(ex, nameof(NotifyViewportChanged));
            throw;
        }
    }

    /// <summary>リソースを解放する。</summary>
    /// <remarks>進行中のセッション（撮影・切り出し）はキャンセルして完了させる（完了待ちのタスクを宙に浮かせない）。</remarks>
    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _slot.CancelAll(static s => s.Cancel(), static s => s.Cancel());
    }

    /// <summary>Crop セッションを待機し、完了後に保持を解除する。</summary>
    /// <param name="session">実行中の Crop セッション。</param>
    /// <param name="sourceImage">開始時点の元画像（開始ログのサイズに使う）。</param>
    /// <returns>切り出し結果（キャンセル時は 0x0）。</returns>
    private async Task<EsRectInt?> AwaitCropAsync(CropSession session, EsBitmap sourceImage)
    {
        try
        {
            return await session.RunAsync(sourceImage).ConfigureAwait(true);
        }
        finally
        {
            _slot.EndCrop(session);
        }
    }

    /// <summary>撮影セッションへ渡すサービスを組み立てる。</summary>
    /// <returns>撮影が必要とするサービスの束（本クラスが生成したインスタンスを束ねるだけ）。</returns>
    private CaptureOverlayServices CreateServices()
        => new(_captureService, _finder, _windowService, _log);

    /// <summary>破棄済みなら例外を投げる。</summary>
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
    }
}
