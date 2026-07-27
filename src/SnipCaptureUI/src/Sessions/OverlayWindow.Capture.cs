using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Estable.AvaloniaUI.Platform.Imaging;
using Estable.AvaloniaUI.SnipCaptureUI.Logging;

namespace Estable.AvaloniaUI.SnipCaptureUI;

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
    /// <summary>ウィンドウ表示時の初期化（フォーカス・ウォッチドッグ）。</summary>
    private void OnWindowOpened(object? sender, EventArgs e)
    {
        Activate();
        Focus();
        if (TryGetPlatformHandle() is { } selfHandle)
        {
            _windowService.HideFromTaskSwitcher(selfHandle);
        }
        // ルーペの表示はマウス移動（DragRect の LoupePositionMoved）が行う。
        // ここで中央に表示するとポインタから遠くに描画され、Source 未設定の市松模様も見えるため表示しない。
        _watchdog.Start(this);
    }

    /// <summary>選択を確定してキャプチャする。</summary>
    /// <param name="screenRect">選択矩形（画面物理座標 = 仮想スクリーン原点基準）。</param>
    /// <remarks>
    /// キャプチャ矩形は <see cref="ToCapturePixelRect"/> でキャプチャ種別に応じて変換する:<br/>
    /// - AreaSelect: 半開区間（<c>幅 = 終点 − 始点</c>）を閉区間化（+1）して終点のピクセルまで含める<br/>
    ///   （仕様: 選択矩形の始点・終点は切出し対象に含まれる）。<br/>
    /// - ScreenSelect / WindowSelect: 画面・ウィンドウの矩形は最初から全ピクセルを含むため +1 しない<br/>
    ///   （+1 すると画面外・ウィンドウ外に 1px はみ出す）。<br/>
    /// キャプチャ結果の <c>SourceBounds</c> も同様に変換後の矩形で返す。<br/>
    /// <b>高速化</b>: 仮想スクリーン全体はセッション開始時にバックグラウンド取得済みのため、<br/>
    /// 確定時はその Bitmap から切り出す。枠選択中に <see cref="PreExtractClip"/> で事前切り出し済みの<br/>
    /// キャッシュ（<see cref="_cachedClipBitmap"/>）が現在の矩形と一致する場合はそれを再利用する（切り出しスキップ）。<br/>
    /// Bitmap 上の座標 = 画面物理座標 − 仮想スクリーン原点（<see cref="_virtualScreenOrigin"/>）。<br/>
    /// 単体テストから呼び出し可能（internal）。
    /// </remarks>
    internal void CompleteSelection(Rect screenRect)
    {
        if (_hasCompleted) return;
        _hasCompleted = true;
        _watchdog.Stop();
        // キャプチャ矩形（画面物理座標）: AreaSelect は閉区間（+1）、ScreenSelect / WindowSelect はそのまま
        var captureRect = ToCapturePixelRect(screenRect);
        // 事前切り出しキャッシュが現在の矩形と一致する場合は再利用する（切り出しスキップ = 高速化）
        if (_cachedClipRect == captureRect && _cachedClipBitmap is { } cached)
        {
            // 所有権を result へ移す（OnOverlayWindowClosed の後片付けで二重破棄しない）
            _cachedClipBitmap = null;
            _cachedClipRect = null;
            CompleteSelectionCore(cached, captureRect);
            return;
        }

        // キャッシュ不一致（事前切り出しが追いついていない場合）はバックグラウンドで切り出す。
        // 切り出し（ピクセルコピー）は UI スレッドをブロックするため Task.Run で実行し、
        // 完了後に UI スレッドで確定する（確定→画像表示までの待ち時間を短縮）。
        var vBitmap = _virtualScreenBitmap;
        var origin = _virtualScreenOrigin;
        Task<Bitmap?> clipTask;
        if (vBitmap is null)
        {
            // キャプチャ未完了（オーバーレイを先に表示した直後）: 確定矩形を直接キャプチャする
            // （GDI キャプチャはスレッドセーフのためバックグラウンドで実行可能）
            clipTask = Task.Run(() => _captureService.CaptureArea(captureRect));
        }
        else
        {
            clipTask = Task.Run(() => ExtractClipCore(vBitmap, origin, captureRect));
        }
        _ = clipTask.ContinueWith(
            t => Dispatcher.UIThread.Post(() => CompleteSelectionCore(t.Result, captureRect)),
            TaskScheduler.Default);
    }

    /// <summary>確定処理の共通コア（オーバーレイを閉じて結果を通知する）。UI スレッドから呼ぶ。</summary>
    /// <param name="result">切り出した Bitmap（所有権は移る）。</param>
    /// <param name="captureRect">キャプチャ矩形（閉区間・画面物理座標）。</param>
    /// <remarks>
    /// オーバーレイを閉じる（Close で仮想スクリーンが解放される。result は所有権が移っているため破棄されない）。
    /// </remarks>
    private void CompleteSelectionCore(Bitmap? result, PixelRect captureRect)
    {
        SnipCaptureLog.LogCaptureCompleted(_log, captureRect.ToString());
        Close();
        _onComplete(result, captureRect);
    }

    /// <summary>現在の枠（選択矩形）で仮想スクリーン Bitmap から事前に切り出してキャッシュする。</summary>
    /// <param name="screenRect">選択矩形（画面物理座標 = 仮想スクリーン原点基準・半開区間）。</param>
    /// <remarks>
    /// 枠選択中（スクリーン/ウィンドウ選択のホバー時・領域選択のドラッグ後）に呼び出し、<br/>
    /// 確定時の切り出しを事前に実行してキャッシュする（<see cref="_cachedClipBitmap"/>）。<br/>
    /// <b>スキップ条件</b>: 前回と同じ矩形（<see cref="_cachedClipRect"/> と一致）の場合は<br/>
    /// 同じ画像になるため切り出しをスキップする。<br/>
    /// 切り出しはバックグラウンド（Task.Run）で実行する（UI スレッドをブロックしない）。<br/>
    /// Bitmap 上の座標 = 画面物理座標 − 仮想スクリーン原点（<see cref="_virtualScreenOrigin"/>）。
    /// </remarks>
    private void PreExtractClip(Rect screenRect)
    {
        // キャプチャ矩形（画面物理座標）: AreaSelect は閉区間（+1）、ScreenSelect / WindowSelect はそのまま
        var captureRect = ToCapturePixelRect(screenRect);
        // 同じ矩形ならスキップ（枠の変更がない場合は同じ画像になる）
        if (_cachedClipRect == captureRect) return;

        // バックグラウンドで切り出す（Bitmap 破棄時は例外を捕捉してキャッシュ更新をスキップ）
        var vBitmap = _virtualScreenBitmap;
        var localRect = new PixelRect(
            captureRect.X - _virtualScreenOrigin.X,
            captureRect.Y - _virtualScreenOrigin.Y,
            captureRect.Width,
            captureRect.Height);
        _ = Task.Run(() =>
        {
            try
            {
                var clipped = vBitmap?.ExtractRegion(localRect);
                if (clipped is null) return;
                Dispatcher.UIThread.Post(() =>
                {
                    // 古いキャッシュを破棄して新しいキャッシュへ置き換える
                    _cachedClipBitmap?.Dispose();
                    _cachedClipBitmap = clipped;
                    _cachedClipRect = captureRect;
                    _preExtractCount++;
                });
            }
            catch (ObjectDisposedException)
            {
                // 仮想スクリーン Bitmap が破棄された（ウィンドウクローズ等）場合はスキップ
            }
        });
    }

    /// <summary>切り出しの共通コア（スレッドセーフ。バックグラウンドから呼び出し可能）。</summary>
    /// <param name="vBitmap">仮想スクリーン Bitmap（ローカル参照）。</param>
    /// <param name="origin">仮想スクリーンの原点（画面物理座標）。</param>
    /// <param name="captureRect">切り出し矩形（閉区間・画面物理座標）。</param>
    /// <returns>切り出した Bitmap。仮想スクリーン未設定（キャプチャ未完了）の場合は null。</returns>
    /// <remarks>
    /// Avalonia の Bitmap 読み取り（ExtractRegion）はスレッドセーフのため、<br/>
    /// バックグラウンドスレッドから安全に実行できる（PreExtractClip と同方式）。<br/>
    /// 仮想スクリーン未設定時のフォールバック（CaptureArea）は呼び出し側が行う。
    /// </remarks>
    private static Bitmap? ExtractClipCore(Bitmap? vBitmap, PixelPoint origin, PixelRect captureRect)
    {
        if (vBitmap is not { } bmp) return null;
        var localRect = new PixelRect(
            captureRect.X - origin.X,
            captureRect.Y - origin.Y,
            captureRect.Width,
            captureRect.Height);
        return bmp.ExtractRegion(localRect);
    }

    /// <summary>選択をキャンセルする（0x0 の空結果）。</summary>
    private void CompleteCancelled()
    {
        if (_hasCompleted) return;
        _hasCompleted = true;
        SnipCaptureLog.LogOverlayCancel(_log);
        _watchdog.Stop();
        _onComplete(null, null);
        Close();
    }

    /// <summary>非アクティブ化時のキャンセル（通常時の防止は本イベントが担う）。</summary>
    private void OnOverlayWindowDeactivated(object? sender, EventArgs e) => CompleteCancelled();

    /// <summary>ウィンドウクローズ時の後片付け。</summary>
    /// <remarks>
    /// 未完了のまま Close された場合はキャンセルとして完了処理を行う（<see cref="Cancel"/> や<br/>
    /// ヘッドレス環境のように Deactivated が発火しないケースでも確実に完了させる）。<br/>
    /// <see cref="CompleteCancelled"/> の <c>_hasCompleted</c> ガードにより二重完了は防止される。
    /// </remarks>
    private void OnOverlayWindowClosed(object? sender, EventArgs e)
    {
        // 未完了ならキャンセルとして完了する（Close() 経由のキャンセルを確実にする）
        CompleteCancelled();
        SelectionOverlay.ClearLoupe();
        _watchdog.Stop();
        _virtualScreenBitmap?.Dispose();
        _virtualScreenBitmap = null;
        // 事前切り出しキャッシュも解放する
        _cachedClipBitmap?.Dispose();
        _cachedClipBitmap = null;
        _cachedClipRect = null;
    }

    /// <summary>仮想スクリーン全体の Bitmap を設定する（全モード共通のキャプチャソース・切り出し元）。</summary>
    /// <param name="bitmap">仮想スクリーン全体の Bitmap。</param>
    /// <param name="origin">仮想スクリーンの原点（画面物理座標）。</param>
    /// <remarks>
    /// <paramref name="origin"/> は Bitmap の左上が対応する画面物理座標（= ウィンドウ Position）。<br/>
    /// ルーペ中心はドメイン座標（仮想スクリーンピクセル）が既に Bitmap 上の座標と一致する。<br/>
    /// 確定時の切り出し（<see cref="CompleteSelection"/>）では <paramref name="origin"/> を減算して
    /// Bitmap 上の座標に変換する（<see cref="_virtualScreenOrigin"/> に保持）。<br/>
    /// ルーペの Source も同時に設定する（AreaSelect のウィンドウ表示時は Bitmap が未取得のため、
    /// 取得完了時に改めて設定しないと市松模様のままになる）。
    /// </remarks>
    public void SetVirtualScreen(Bitmap? bitmap, PixelPoint origin)
    {
        _virtualScreenBitmap?.Dispose();
        _virtualScreenBitmap = bitmap;
        _virtualScreenOrigin = origin;
        SnipCaptureLog.LogVirtualScreen(_log, origin.X, origin.Y, bitmap?.PixelSize.Width ?? 0, bitmap?.PixelSize.Height ?? 0);
        // 固定背景画像（VirtualScreenImage）にも表示する（AreaSelect の背景・ルーペソースの視覚化）
        VirtualScreenImage.Source = bitmap;
        // ルーペの Source を設定する。
        // Source 未設定の間はルーペが表示されない（市松模様防止）ため、ここで Source を設定し、
        // 既にドラッグ中（CenterPoint 設定済み）なら SetLoupePosition を再呼び出しして表示を開始する。
        if (bitmap is not null && _captureType == CaptureType.AreaSelect)
        {
            SelectionOverlay.SetLoupeSource(bitmap);
            if (SelectionOverlay.LoupeCenterPoint is { } center)
            {
                SelectionOverlay.SetLoupePosition(center);
            }
        }
    }
}
