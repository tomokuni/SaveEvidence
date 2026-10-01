using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using EsUtil.SaveEvidence.Parts.CaptureSession.MewUI.Sessions;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Imaging;
using Microsoft.Extensions.Logging;

namespace EsUtil.SaveEvidence.Parts.CaptureSession.MewUI;

/// <summary>撮影と切り出しのセッションを統括する Facade（MewUI 版）。</summary>
/// <remarks>
/// <b>適用パターン</b>: Facade（撮影・切り出しの 2 セッションの窓口を 1 つの API にまとめる）。<br/>
/// Avalonia 版の <c>CaptureSessionControl</c> に相当し、<b>本ライブラリの公開 API は本クラスだけ</b>である。<br/>
/// <br/>
/// <b>排他の単一所有</b>: 撮影と切り出しは同時に 1 つだけ実行できる。 この規則はコアの<br/>
/// <c>CaptureSessionSlot</c> が単一所有し、本クラスがその唯一の適用者になる（両 UI で同じ規則が働く）。<br/>
/// 実行中の要求は無視する（既存のセッションを中断しない）。<br/>
/// <br/>
/// <b>セッションの寿命</b>: 撮影（<see cref="Capture"/>）は呼び出し内で完結し、切り出し（<see cref="ToggleCrop"/>）は<br/>
/// 開始から確定・キャンセルまで長生きする。 どちらもセッションごとにインスタンスを生成し、終了時に破棄する<br/>
/// （切り出しのオーバーレイは表示ツリーへ加えて、終了時に取り外す）。<br/>
/// <br/>
/// <b>UI 固有のホスト結線</b>: プレビューの切り出しは既存のビュー（<see cref="CropViewControl"/>）とオーバーレイを<br/>
/// 重ねる領域（<see cref="Grid"/>）が必要なため、ホストが生成して本クラスへ渡す。<br/>
/// <br/>
/// <b>テーマ</b>: 切り出しのルーペの市松模様の下地色はテーマに依存する。 テーマ変更の購読はホストが行い、<br/>
/// <see cref="AppearanceSettings"/> で反映する（本クラスはテーマの通知元を知らない）。
/// </remarks>
public sealed class CaptureSessionControl
{
    /// <summary>進行中のセッション（撮影と切り出しで同時に 1 つまで）。</summary>
    /// <remarks>排他規則（片方が実行中なら他方も開始できない）はコアの <c>CaptureSessionSlot</c> が単一所有する。</remarks>
    private readonly CaptureSessionSlot<CaptureOverlay, CropSession> _slot = new();

    /// <summary>切り出しの対象となるプレビューコントロール。</summary>
    private readonly CropViewControl _cropView;

    /// <summary>切り出しの適用先（アプリ側が実装するポート）。</summary>
    private readonly ICaptureTarget _cropTarget;

    /// <summary>切り出しのオーバーレイを重ねる領域（プレビューと同じセル）。</summary>
    private readonly Grid _cropHost;

    /// <summary>内部ロガー（インスタンス単位。 複数インスタンスで混線しない）。</summary>
    private readonly ILogger _log;

    /// <summary>設定（全メンバー non-null）。 撮影と切り出しが同じ値を使う（本クラスが単一所有する）。</summary>
    private CaptureSettings _settings;

    /// <summary>インスタンスを生成する。</summary>
    /// <param name="cropView">切り出しの対象となるプレビューコントロール。</param>
    /// <param name="cropTarget">切り出しの適用先（アプリ側の実装）。</param>
    /// <param name="cropHost">切り出しのオーバーレイを重ねる領域（プレビューと同じセル）。</param>
    /// <param name="initialSettings">初期設定。 null メンバーはデフォルト値が使用される。</param>
    /// <param name="logger">内部ログの転送先。 null の場合はログを出力しない。</param>
    /// <exception cref="ArgumentNullException"><paramref name="cropView"/> / <paramref name="cropTarget"/> / <paramref name="cropHost"/> が null の場合。</exception>
    public CaptureSessionControl(
        CropViewControl cropView, ICaptureTarget cropTarget, Grid cropHost,
        CaptureSettings? initialSettings = null, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(cropView);
        ArgumentNullException.ThrowIfNull(cropTarget);
        ArgumentNullException.ThrowIfNull(cropHost);

        _cropView = cropView;
        _cropTarget = cropTarget;
        _cropHost = cropHost;
        // 入力なし（null）は既定値で埋める（規則はコアが単一所有する）
        _settings = CaptureSettingsMerge.Merge(initialSettings);
        _log = logger.OrNullLogger();
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
        get => _settings;
        set
        {
            // 部分更新のマージ・検証・同値スキップ・適用ログはコアが単一所有する
            if (!CaptureSettingsMerge.Apply(value, ref _settings, _log)) return;

            // 実行中の切り出しセッションへ最新設定を再適用する（切り出し中の設定変更の即時反映）
            _slot.Crop?.ApplySettings(_settings);
        }
    }

    /// <summary>撮影（スクリーン選択 / ウィンドウ選択 / 領域選択）を実行する。 UI スレッドから呼び出すこと。</summary>
    /// <param name="owner">オーナーウィンドウ（撮影中は画面外へ退避し、終了後に戻す）。</param>
    /// <param name="type">キャプチャ種別。</param>
    /// <param name="services">アプリ全体で共有するサービス（キャプチャ・検出・ウィンドウ操作）。</param>
    /// <returns>切り出した画像。 キャンセル・取得失敗・退化した選択の場合は null。</returns>
    /// <remarks>
    /// 1. 撮影セッションを生成し、排他の受付を試みる（実行中のセッションがある場合は何もせず null を返す）。<br/>
    /// 2. 受付できた場合は撮影セッションを実行する（画像の取得・オーバーレイの表示・確定待ち・画像の破棄）。<br/>
    /// 3. 終了したら受付を解放する（例外で終わっても解放する）。<br/>
    /// 撮影の実行内容（退避・除外・切り出し）は <see cref="CaptureOverlay"/> が担う。<br/>
    /// 設定は <see cref="AppearanceSettings"/> の現在値を渡す（ホストが撮影用に別途設定を組み立てない）。
    /// </remarks>
    public EsBitmap? Capture(Window owner, CaptureType type, CaptureOverlayServices services)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(services);

        // 受付は撮影（オーナーの退避）より先に行う（退避してから拒否すると、無駄な退避・復帰が発生する）
        var session = new CaptureOverlay(type, _settings, services.Finder, services.Logger);
        if (!_slot.TryBeginCapture(session))
        {
            return null; // 再入禁止（切り出し中・撮影中）
        }

        try
        {
            return session.Run(owner, services);
        }
        finally
        {
            _slot.EndCapture(session);
        }
    }

    /// <summary>切り出しの開始・確定・キャンセルを 1 つの操作で切り替える（ツールバーのボタン用）。</summary>
    /// <remarks>
    /// どの操作を行うかの判断はコア（<see cref="CropSessionActions.Resolve"/>）が単一所有する<br/>
    /// （Avalonia ホストの切り出しボタンと同じ規則を使う。 UI ごとに条件を書くと片方だけ変わる）。<br/>
    /// キー（Enter / Escape）と同じ操作を 1 つのメソッドへ集約し、経路違いの重複を作らない。<br/>
    /// 開始できない場合（撮影中）は何もしない。
    /// </remarks>
    public void ToggleCrop()
    {
        // 戻り値（実行すべき操作）を効果へ写すだけにする
        switch (CropSessionActions.Resolve(_slot.HasCrop, _slot.HasCropSelection(static crop => crop.HasSelection)))
        {
            case CropSessionAction.Start:
                StartCrop();
                break;
            case CropSessionAction.Confirm:
                _slot.Crop?.Confirm();
                break;
            default:
                _slot.Crop?.Cancel();
                break;
        }
    }

    /// <summary>切り出しを開始する（オーバーレイを表示し、入力を選択コントロールへ移す）。</summary>
    /// <remarks>
    /// 1. 画像が無い場合は何もしない（切り出す対象がない）。<br/>
    /// 2. 切り出しセッションを生成し、排他の受付を試みる（撮影中は受付できないため何も表示しない）。<br/>
    /// 3. 受付できた場合は開始する（確定・キャンセルで終了し、その通知で受付を解放する）。
    /// </remarks>
    private void StartCrop()
    {
        if (_cropTarget.CurrentImage is not { } image)
        {
            return;
        }

        var session = new CropSession(_cropView, _cropTarget, _settings, _cropHost, _log);

        // 受付は表示より先に行う（表示してから拒否すると、一瞬だけオーバーレイが出る）
        if (!_slot.TryBeginCrop(session))
        {
            return;
        }

        // 終了の通知で受付を解放する（確定・キャンセルのどちらでも 1 回届く）
        session.Ended += () => _slot.EndCrop(session);
        session.Start(image);
    }
}
