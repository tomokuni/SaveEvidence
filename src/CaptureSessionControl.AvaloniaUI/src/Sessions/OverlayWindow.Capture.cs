using Avalonia;
using Avalonia.Threading;
using EsUtil.Platform.AvaloniaUI;
using EsUtil.Platform.AvaloniaUI.Imaging;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;

namespace EsUtil.SaveEvidence.CaptureSession.AvaloniaUI;

/// <summary>
/// OverlayWindow の確定・切り出し・キャンセル・ライフサイクル処理（partial 分割）。
/// </summary>
/// <remarks>
/// 選択確定（<see cref="CompleteSelection"/>）・事前切り出し（<see cref="PreExtractClip"/>）・<br/>
/// キャンセル・ウィンドウクローズ時の後片付け・仮想スクリーン設定を処理する。<br/>
/// 高速化のため、仮想スクリーン全体から確定時に切り出す（確定時の再キャプチャなし）。
/// </remarks>
internal sealed partial class OverlayWindow
{
    /// <summary>ウィンドウ表示時の初期化（フォーカス・Alt+Tab 除外）。</summary>
    private void OnWindowOpened(object? sender, EventArgs e)
    {
        Activate();
        Focus();
        if (TryGetPlatformHandle() is { } selfHandle)
        {
            _windowService.HideFromTaskSwitcher(selfHandle.ToEsPlatformHandle());
        }
        // ルーペの表示はマウス移動（Marquee の LoupePositionMoved）が行う。
        // ここで中央に表示するとポインタから遠くに描画され、Source 未設定の市松模様も見えるため表示しない。
    }

    /// <summary>選択を確定してキャプチャする（選択矩形が浮動小数 = ドラッグ選択・ハイライト）。</summary>
    /// <param name="screenRect">選択矩形（画面物理座標 = 仮想スクリーン原点基準）。</param>
    /// <remarks>
    /// 切り出し矩形の決定（種別ごとの閉区間化・画像内へのクランプ・端数の丸め）は<br/>
    /// コアの <see cref="CaptureClipMath.ResolveCaptureRect"/> が単一所有する。
    /// 切り出せる矩形が無い場合（退化した選択）はキャンセルとして完了する。<br/>
    /// <b>確定要求の記録はこの入口が担う</b>（キー・クリック・アダプタ要求のすべてがここを通るため、<br/>
    /// 経路ごとにログを書くと抜けや重複が生じる）。記録する矩形は画面物理座標（両 UI で同じ意味になる）。<br/>
    /// 単体テストから呼び出し可能（internal）。
    /// </remarks>
    internal void CompleteSelection(EsRect screenRect)
    {
        _log.LogConfirmRequested(CaptureLogFormat.Rect(screenRect));
        CompleteSelectionCore(CaptureClipMath.ResolveCaptureRect(_captureType, screenRect, CaptureImageBounds));
    }

    /// <summary>選択を確定してキャプチャする（選択矩形が整数 = クリック確定の検出矩形）。</summary>
    /// <param name="screenRect">選択矩形（画面物理座標 = 仮想スクリーン原点基準）。</param>
    /// <remarks>浮動小数版と同じ規則を適用する（コアに整数版の入口があり、 UI は型を広げない）。</remarks>
    private void CompleteSelection(EsRectInt screenRect)
    {
        _log.LogConfirmRequested(CaptureLogFormat.Rect(screenRect));
        CompleteSelectionCore(CaptureClipMath.ResolveCaptureRect(_captureType, screenRect, CaptureImageBounds));
    }

    /// <summary>解決済みの切り出し矩形でキャプチャを確定する。</summary>
    /// <param name="captureRect">切り出し矩形（画面物理座標・閉区間済み）。</param>
    /// <remarks>
    /// <b>高速化</b>: 仮想スクリーン全体はセッション開始時にバックグラウンド取得済みのため、<br/>
    /// 確定時はその Bitmap から切り出す。枠選択中に <see cref="PreExtractClip"/> で事前切り出し済みの<br/>
    /// キャッシュ（<see cref="CaptureClipCache"/>）が現在の矩形と一致する場合はそれを再利用する（切り出しスキップ）。<br/>
    /// キャッシュ不一致はバックグラウンドで切り出す（ピクセルコピーで UI スレッドをブロックしない）。<br/>
    /// Bitmap 上の座標 = 画面物理座標 − 仮想スクリーンの原点（コアの保持クラスが保持する）。
    /// </remarks>
    private void CompleteSelectionCore(EsRectInt captureRect)
    {
        // 完了は 1 回だけ受け付ける（確定とキャンセルの競合で二重完了にしない。規則はコアが単一所有する）
        if (!_state.TryComplete()) return;

        if (captureRect.IsEmpty)
        {
            // 切り出せる矩形がない（退化した選択）: 0 サイズの画像を作らずキャンセルとして完了する
            _onComplete(null, null);
            Close();
            return;
        }

        // 事前切り出しのキャッシュが同じ矩形なら再利用する（切り出しスキップ。判定と所有権の移動はコアが担う）
        if (_clipCache.Take(captureRect) is { } cached)
        {
            _onComplete(cached, captureRect);
            Close();
            return;
        }

        // キャッシュ不一致（事前切り出しが追いついていない場合）はバックグラウンドで切り出す。
        // 切り出し（ピクセルコピー）は UI スレッドをブロックするため Task.Run で実行し、
        // 完了後に UI スレッドで確定する（確定→画像表示までの待ち時間を短縮）。
        var vBitmap = VirtualScreenBitmap;
        Task<EsBitmap?> clipTask;
        if (vBitmap is null)
        {
            // キャプチャ未完了（オーバーレイを先に表示した直後）: 確定矩形を直接キャプチャする
            // （GDI キャプチャはスレッドセーフのためバックグラウンドで実行可能）
            clipTask = Task.Run(() => _captureService.CaptureArea(captureRect));
        }
        else
        {
            var origin = _captureImage.Origin;
            clipTask = Task.Run(() => CaptureClipMath.Extract(vBitmap, origin, captureRect));
        }
        _ = clipTask.ContinueWith(
            t => Dispatcher.UIThread.Post(() =>
            {
                // 確定のログはセッション層（SelectionCaptureSession）が結果を受けて 1 回だけ出力する
                _onComplete(t.Result, captureRect);
                Close();
            }),
            TaskScheduler.Default);
    }

    /// <summary>現在の枠（選択矩形）で仮想スクリーン Bitmap から事前に切り出してキャッシュする。</summary>
    /// <param name="screenRect">選択矩形（画面物理座標 = 仮想スクリーン原点基準）。</param>
    /// <remarks>
    /// ハイライト型のホバー時に呼び出し、確定時の切り出しを事前に実行してキャッシュする<br/>
    /// （キャッシュの同一判定・所有権の受け渡し・旧値の破棄はコアの <see cref="CaptureClipCache"/> が単一所有する）。<br/>
    /// 切り出し矩形が退化している場合（幅・高さが 0 以下）は切り出す対象がないため何もしない。<br/>
    /// 切り出しはバックグラウンド（Task.Run）で実行する（UI スレッドをブロックしない）。<br/>
    /// Bitmap 上の座標 = 画面物理座標 − 仮想スクリーンの原点（コアの保持クラスが保持する）。
    /// </remarks>
    private void PreExtractClip(EsRectInt screenRect)
    {
        // 切り出し矩形（画面物理座標）: 種別に応じた閉区間化・画像内へのクランプはコアが単一所有する
        var captureRect = CaptureClipMath.ResolveCaptureRect(_captureType, screenRect, CaptureImageBounds);
        // 切り出せない矩形・同じ矩形はスキップ（矩形の変更がない場合は同じ画像になる。判断はコアが担う）
        if (captureRect.IsEmpty || _clipCache.HasCached(captureRect)) return;

        // バックグラウンドで切り出す（破棄済みの画像はコアが null を返すため例外処理は不要）
        var vBitmap = VirtualScreenBitmap;
        var origin = _captureImage.Origin;
        _ = Task.Run(() =>
        {
            var clipped = CaptureClipMath.Extract(vBitmap, origin, captureRect);
            if (clipped is null) return;
            Dispatcher.UIThread.Post(() => _clipCache.Store(captureRect, clipped));
        });
    }

    /// <summary>選択をキャンセルする（0x0 の空結果）。</summary>
    private void CompleteCancelled()
    {
        // 完了は 1 回だけ受け付ける（規則はコアが単一所有する）
        if (!_state.TryComplete()) return;
        _log.LogCancelRequested();
        _onComplete(null, null);
        Close();
    }

    /// <summary>非アクティブ化時のキャンセル（通常時の防止は本イベントが担う）。</summary>
    private void OnOverlayWindowDeactivated(object? sender, EventArgs e) => CompleteCancelled();

    /// <summary>ウィンドウクローズ時の後片付け。</summary>
    /// <remarks>
    /// 未完了のまま Close された場合はキャンセルとして完了処理を行う（<see cref="Cancel"/> や<br/>
    /// ヘッドレス環境のように Deactivated が発火しないケースでも確実に完了させる）。<br/>
    /// 二重完了はコアの状態（<c>CaptureSessionState.TryComplete</c>）が防止する。
    /// </remarks>
    private void OnOverlayWindowClosed(object? sender, EventArgs e)
    {
        // 未完了ならキャンセルとして完了する（Close() 経由のキャンセルを確実にする）
        CompleteCancelled();
        SelectionOverlay.ClearLoupe();
        // 表示境界の画像（VirtualScreenImage・ルーペ Source は EsBitmap を共有）を解除する
        VirtualScreenImage.Source = null;
        // 保持している画像を破棄する（以降に届いた画像はコアがその場で破棄する）
        _captureImage.Dispose();
        // 事前切り出しキャッシュも解放する（保持中の画像の破棄はコアが担う）
        _clipCache.Dispose();
    }

    /// <summary>仮想スクリーン全体の Bitmap を設定する（全モード共通のキャプチャソース・切り出し元）。</summary>
    /// <param name="bitmap">仮想スクリーン全体の Bitmap。</param>
    /// <param name="origin">仮想スクリーンの原点（画面物理座標）。</param>
    /// <remarks>
    /// <paramref name="origin"/> は Bitmap の左上が対応する画面物理座標（= ウィンドウ Position）。<br/>
    /// ルーペ中心はドメイン座標（仮想スクリーンピクセル）が既に Bitmap 上の座標と一致する。<br/>
    /// 確定時の切り出し（<see cref="CompleteSelection"/>）では保持している原点を減算して Bitmap 上の座標に変換する。<br/>
    /// ルーペの Source も同時に設定する（AreaSelect のウィンドウ表示時は Bitmap が未取得のため、
    /// 取得完了時に改めて設定しないと市松模様のままになる）。<br/>
    /// <b>クローズ後</b>: ウィンドウの後片付けが済んでおり、渡された Bitmap は保持せずその場で破棄される<br/>
    /// （背景撮影の完了がキャンセルより遅れた場合の破棄漏れを防ぐ。規則はコアの保持クラスが単一所有する）。
    /// </remarks>
    public void SetVirtualScreen(EsBitmap? bitmap, PixelPoint origin)
    {
        // 差し替え時の旧値の破棄・クローズ後の破棄はコアが単一所有する
        if (!_captureImage.Set(bitmap, origin.ToEsPointInt())) return;

        _log.LogVirtualScreen(origin.ToEsPointInt(), bitmap);
        // 固定背景画像（VirtualScreenImage）とルーペの拡大元画像（Source）へ同一の EsBitmapAvaloniaImage を設定する。
        // WriteableBitmap を生成せず、EsBitmap を内包した IImage（EsBitmapAvaloniaImage）を Source に設定する
        // （Skia 直描画・ゼロコピー。破棄はコアの保持クラスの Dispose が一元管理する）。
        EsBitmapAvaloniaImage? displayImage = bitmap is null ? null : new EsBitmapAvaloniaImage(bitmap);
        VirtualScreenImage.Source = displayImage;

        // ルーペの Source を設定する（同一の EsBitmapAvaloniaImage を共有する）。
        // Source 未設定の間はルーペが表示されない（市松模様防止）ため、ここで Source を設定し、
        // 既にドラッグ中（CenterPoint 設定済み）なら SetLoupePosition を再呼び出しして表示を開始する。
        if (displayImage is not null && !CaptureTypeTraits.IsHighlighted(_captureType))
        {
            SelectionOverlay.SetLoupeSource(displayImage);
            if (SelectionOverlay.LoupeCenterPoint is { } center)
            {
                SelectionOverlay.SetLoupePosition(center);
            }
        }
    }
}
