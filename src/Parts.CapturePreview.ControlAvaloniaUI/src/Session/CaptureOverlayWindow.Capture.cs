using Avalonia;
using Avalonia.Threading;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Capture;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Crop;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Logging;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Session;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Imaging;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Interop;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.AvaloniaUI.Session;

/// <summary>CaptureOverlayWindow の確定・切り出し・キャンセル・ライフサイクル処理（partial 分割）。</summary>
/// <remarks>
/// 選択確定（<see cref="CompleteSelection(EsRect)"/>）・事前切り出し（<see cref="PreExtractClip"/>）・<br/>
/// キャンセル・ウィンドウクローズ時の後片付け・仮想スクリーン設定を処理する。<br/>
/// 高速化のため、仮想スクリーン全体から確定時に切り出す（確定時の再キャプチャなし）。
/// </remarks>
internal sealed partial class CaptureOverlayWindow
{
    /// <summary>ウィンドウ表示時の初期化（フォーカス・Alt+Tab 除外）。</summary>
    private void OnWindowOpened(object? sender, EventArgs e)
    {
        Activate();
        Focus();
        if (TryGetPlatformHandle() is { } selfHandle)
        {
            try
            {
                _windowService.HideFromTaskSwitcher(selfHandle.ToEsPlatformHandle());
            }
            catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
            {
                // 除外できなくても選択操作は成立する（Alt+Tab の一覧に残るだけ）。
                // ウィンドウ イベントのハンドラーを例外で抜けないよう、境界で記録して続行する
                _log.LogOperationError(ex, nameof(OnWindowOpened));
            }
        }
        // ルーペの表示はマウス移動（Marquee の LoupePositionMoved）が行う。
        // ここで中央に表示するとポインタから遠くに描画され、Source 未設定の下地も見えるため表示しない。
    }

    /// <summary>選択を確定してキャプチャする（選択矩形が浮動小数 = ドラッグ選択・ハイライト）。</summary>
    /// <param name="screenRect">選択矩形（画面物理座標 = 仮想スクリーン原点基準）。</param>
    /// <remarks>
    /// 切り出し矩形の決定（種別ごとの閉区間化・画像内へのクランプ・端数の丸め）は<br/>
    /// コアの <see cref="CropClipMath.ResolveCaptureRect(EsUtil.Platform.Core.CaptureType, EsRect, EsRectInt)"/> が単一所有する。
    /// 切り出せる矩形が無い場合（退化した選択）はキャンセルとして完了する。<br/>
    /// <b>確定要求の記録はこの入口が担う</b>（キー・クリック・アダプタ要求のすべてがここを通るため、<br/>
    /// 経路ごとにログを書くと抜けや重複が生じる）。 記録する矩形は画面物理座標（両 UI で同じ意味になる）。<br/>
    /// 単体テストから呼び出し可能（internal）。
    /// </remarks>
    internal void CompleteSelection(EsRect screenRect)
    {
        _log.LogConfirmRequested(screenRect);
        CompleteSelectionCore(CropClipMath.ResolveCaptureRect(_captureType, screenRect, CaptureImageBounds));
    }

    /// <summary>選択を確定してキャプチャする（選択矩形が整数 = クリック確定の検出矩形）。</summary>
    /// <param name="screenRect">選択矩形（画面物理座標 = 仮想スクリーン原点基準）。</param>
    /// <remarks>浮動小数版と同じ規則を適用する（コアに整数版の入口があり、 UI は型を広げない）。</remarks>
    private void CompleteSelection(EsRectInt screenRect)
    {
        _log.LogConfirmRequested(screenRect);
        CompleteSelectionCore(CropClipMath.ResolveCaptureRect(_captureType, screenRect, CaptureImageBounds));
    }

    /// <summary>解決済みの切り出し矩形でキャプチャを確定する。</summary>
    /// <param name="captureRect">切り出し矩形（画面物理座標・閉区間済み）。</param>
    /// <remarks>
    /// <b>高速化</b>: 仮想スクリーン全体はセッション開始時にバックグラウンド取得済みのため、<br/>
    /// 確定時はその Bitmap から切り出す。 枠選択中に <see cref="PreExtractClip"/> で事前切り出し済みの<br/>
    /// キャッシュ（<see cref="CaptureClipCache"/>）が現在の矩形と一致する場合はそれを再利用する（切り出しスキップ）。<br/>
    /// キャッシュ不一致はバックグラウンドで切り出す（ピクセルコピーで UI スレッドをブロックしない）。<br/>
    /// Bitmap 上の座標 = 画面物理座標 − 仮想スクリーンの原点（コアの保持クラスが保持する）。
    /// </remarks>
    private void CompleteSelectionCore(EsRectInt captureRect)
    {
        // 完了は 1 回だけ受け付ける（確定とキャンセルの競合で二重完了にしない。 規則はコアが単一所有する）
        if (!_state.TryComplete()) return;

        if (captureRect.IsEmpty)
        {
            // 切り出せる矩形がない（退化した選択）: 0 サイズの画像を作らずキャンセルとして完了する
            _onComplete(null, null);
            Close();
            return;
        }

        // 事前切り出しのキャッシュが同じ矩形なら再利用する（切り出しスキップ。 判定と所有権の移動はコアが担う）
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
            clipTask = Task.Run<EsBitmap?>(() => _captureService.CaptureArea(captureRect));
        }
        else
        {
            var origin = _captureImage.Origin;
            clipTask = Task.Run(() => CropClipMath.Extract(vBitmap, origin, captureRect));
        }
        _ = clipTask.ContinueWith(
            t => Dispatcher.UIThread.Post(() =>
            {
                // 確定のログはセッション層（CaptureOverlaySession）が結果を受けて 1 回だけ出力する
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
        var captureRect = CropClipMath.ResolveCaptureRect(_captureType, screenRect, CaptureImageBounds);
        // 切り出せない矩形・同じ矩形はスキップ（矩形の変更がない場合は同じ画像になる。 判断はコアが担う）
        if (captureRect.IsEmpty || _clipCache.HasCached(captureRect)) return;

        // バックグラウンドで切り出す（破棄済みの画像はコアが null を返すため例外処理は不要）
        var vBitmap = VirtualScreenBitmap;
        var origin = _captureImage.Origin;
        _ = Task.Run(() =>
        {
            var clipped = CropClipMath.Extract(vBitmap, origin, captureRect);
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
    /// 未完了のまま Close された場合はキャンセルとして完了処理を行う（<see cref="CompleteCancelled"/> や<br/>
    /// ヘッドレス環境のように Deactivated が発火しないケースでも確実に完了させる）。<br/>
    /// 二重完了はコアの状態（<c>CaptureSessionState.TryComplete</c>）が防止する。
    /// </remarks>
    private void OnOverlayWindowClosed(object? sender, EventArgs e)
    {
        // 未完了ならキャンセルとして完了する（Close() 経由のキャンセルを確実にする）
        CompleteCancelled();
        SelectionOverlay.ClearLoupe();
        // 表示境界の画像（VirtualScreenImage・ルーペ Source は同じ画像を共有）を解除する
        VirtualScreenImage.Source = null;
        _displayImage?.Dispose();
        _displayImage = null;
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
    /// ルーペ中心は対象座標（仮想スクリーンピクセル）が既に Bitmap 上の座標と一致する。<br/>
    /// 確定時の切り出し（<see cref="CompleteSelection(EsRect)"/>）では保持している原点を減算して Bitmap 上の座標に変換する。<br/>
    /// ルーペの Source も同時に設定する（AreaSelect のウィンドウ表示時は Bitmap が未取得のため、
    /// 取得完了時に改めて設定しないと下地のままになる）。<br/>
    /// <b>クローズ後</b>: ウィンドウの後片付けが済んでおり、渡された Bitmap は保持せずその場で破棄される<br/>
    /// （背景撮影の完了がキャンセルより遅れた場合の破棄漏れを防ぐ。 規則はコアの保持クラスが単一所有する）。
    /// </remarks>
    public void SetVirtualScreen(EsBitmap? bitmap, PixelPoint origin)
    {
        // 差し替え時の旧値の破棄・クローズ後の破棄はコアが単一所有する
        // （撮影の完了後に画像を書き換えないため、保持する型は読み取りの面になる。 凍結と 0 コピー共有もコアが行う）
        if (!_captureImage.Set(bitmap, origin.ToEsPointInt())) return;

        // 保持している読み取りの面を取得する（表示と切り出しが同じ面を共有する）
        var readOnly = _captureImage.Image;
        _log.LogVirtualScreen(origin.ToEsPointInt(), readOnly);
        // 新しいアダプタを先に Source へ設定し、旧アダプタは後から破棄する。
        // Image.Source が旧アダプタを参照し続ける間に破棄すると、描画が破棄済みの射影へ触れて例外・チラつきになる
        // （順序の規則は EsBitmapAvaloniaAdapter が単一所有する。 IImage は非所有のため、保持するホストが Dispose する）。
        // 元画像はコアの保持クラスが所有し、このアダプタは借用して包むだけ。
        var previousImage = _displayImage;
        var displayImage = readOnly is null ? null : new EsBitmapAvaloniaAdapter(readOnly);
        _displayImage = displayImage;
        VirtualScreenImage.Source = displayImage;
        previousImage?.Dispose();

        // ルーペのソースを設定する（固定背景画像と同一のアダプタを共有する）。
        // Source 未設定の間はルーペが表示されないため、ここで Source を設定し、
        // 既にドラッグ中（CenterPoint 設定済み）なら SetLoupePosition を再呼び出しして表示を開始する。
        // 拡大元は アダプタが内包するコアの画像（GetEsBitmap）、表示用の射影はアダプタ自身
        // （背景画像と同じ画像を表示しているため、切り出しの複製を作らずに描ける）。
        if (displayImage is not null && !CaptureTypeTraits.IsHighlighted(_captureType))
        {
            SelectionOverlay.SetLoupeSource(displayImage.GetEsBitmap(), displayImage);
            if (SelectionOverlay.LoupeCenterPoint is { } center)
            {
                SelectionOverlay.SetLoupePosition(center);
            }
        }
    }
}



