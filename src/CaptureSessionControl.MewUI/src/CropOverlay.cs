using Aprillz.MewUI.Controls;
using Estable.CaptureSession;
using Estable.Marquee.Decoration;
using Estable.FadeMaskDragRect;
using Estable.MewUI.CropView;
using Estable.MewUI.FadeMaskDragRect;
using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using Microsoft.Extensions.Logging;
using EsUtil.Platform.Core;
using DrawingColor = System.Drawing.Color;

namespace Estable.MewUI.CaptureSession;

/// <summary>プレビュー画像の切り出し（Crop）セッション（MewUI 版）。</summary>
/// <remarks>
/// Avalonia 版の <c>CropSession</c>（<c>CaptureSessionControl.AvaloniaUI</c>）に相当する。プレビューの上へ領域選択の<br/>
/// オーバーレイを重ね、ドラッグで指定した領域で現在の画像を切り出す。<br/>
/// <br/>
/// <b>座標変換</b>: プレビューはズーム・パンできるため、ローカル DIP ↔ 画像ピクセルの変換は<br/>
/// 表示中の画像矩形と実効倍率から求める（<see cref="UpdatePort"/>）。ズーム・パンで表示が変わった時は<br/>
/// <see cref="CropViewControl.ViewportChanged"/> でポートを作り直す。<br/>
/// <br/>
/// <b>ライフサイクル</b>: 開始でオーバーレイを表示し、確定（Enter / イベント）またはキャンセル（Escape）で終了する。<br/>
/// 確定時は <see cref="ICaptureTarget.ApplyCrop"/> を通すため、Undo 履歴への退避などのドメイン操作は<br/>
/// アプリ側（<see cref="ICaptureTarget"/> の実装）に委ねる。<br/>
/// <br/>
/// <b>テーマ</b>: 市松模様の下地色はテーマに依存するため、ホストが <see cref="RefreshAppearance"/> で更新する<br/>
/// （テーマ変更の購読はホストが行う。本クラスはテーマの通知元を知らない）。<br/>
/// <br/>
/// <b>外観</b>: 枠線・ハンドル・十字マーカー・座標ラベルは <see cref="CaptureSettings"/> に従う（Avalonia 版の<br/>
/// <c>CropSession</c> と同じマッピング）。白枠などの固定値は持たない（設定を変えると反映されなくなるため）。<br/>
/// <br/>
/// <b>ズーム・パン</b>: 切り出し中もホイール（Ctrl = ズーム / 通常 = パン）でプレビューを操作できる<br/>
/// （オーバーレイ上のホイールを <see cref="CropViewControl.PanByWheel"/> / <see cref="CropViewControl.ZoomByWheel"/> へ転送する）。
/// </remarks>
public sealed class CropOverlay
{
    /// <summary>ロガー（ホストが注入する。null の場合は出力しない）。</summary>
    private readonly ILogger _log;

    /// <summary>対象のプレビューコントロール。</summary>
    private readonly CropViewControl _view;

    /// <summary>切り出しの適用先（アプリ側が実装する）。</summary>
    private readonly ICaptureTarget _target;

    /// <summary>設定（全メンバー non-null）。</summary>
    private readonly CaptureSettings _settings;

    /// <summary>選択オーバーレイ（暗転マスク + 選択矩形 + ルーペ）。</summary>
    private readonly FadeMaskDragRectControl _selector;

    /// <summary>セッションの状態（切り出し中かどうか・完了は 1 回だけ）。</summary>
    private readonly CaptureSessionState _state = new();

    /// <summary>市松模様の下地色（テーマのページ背景色）。<see cref="RefreshAppearance"/> で更新する。</summary>
    private DrawingColor _themeBackground;

    /// <summary>切り出しセッションを初期化する（オーバーレイを生成して表示ツリーへ追加する）。</summary>
    /// <param name="view">対象のプレビューコントロール。</param>
    /// <param name="target">切り出しの適用先（アプリ側の実装）。</param>
    /// <param name="settings">キャプチャ設定（null メンバーは既定値で埋める）。</param>
    /// <param name="host">オーバーレイを重ねる領域（プレビューと同じセル）。</param>
    /// <param name="themeBackground">市松模様の下地色（テーマのページ背景色）。</param>
    /// <param name="logger">ロガー（null の場合は出力しない）。</param>
    public CropOverlay(
        CropViewControl view, ICaptureTarget target, CaptureSettings settings, Grid host,
        DrawingColor themeBackground, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(host);

        _view = view;
        _target = target;
        _settings = settings.ToNonNull();
        _themeBackground = themeBackground;
        _log = logger.OrNullLogger();

        _selector = new FadeMaskDragRectControl
        {
            Mode = FadeMaskDragRectMode.Crop,
            IsVisible = false,
            Log = _log,
        };

        _selector.ConfirmRequested += (_, _) => Confirm();
        _selector.CancelRequested += (_, _) => Cancel();
        _view.ViewportChanged += (_, _) => UpdatePort();
        _target.ImageChanged += (_, _) =>
        {
            _selector.SetLoupeSource(_target.CurrentImage);
            UpdatePort();

            // 画像の差し替えで座標系（元画像ピクセル）が変わるため、選択を破棄して残留を防ぐ。
            // ズーム・パン（ビューポートの変化）では座標系が変わらないため、選択は維持する。
            if (IsActive)
            {
                _selector.ClearSelection();
            }
        };

        // 選択オーバーレイ上のホイールをビューポート（ズーム・パン）へ転送する。
        // オーバーレイは切り出し中にプレビューを覆って入力を占有するため、ホイールはオーバーレイだけが受け取る。
        // 回転量から移動量への換算とズーム率の決定は、ズームビューポートが単一所有する。
        _selector.ScrollRequested += (_, request) =>
            _view.PanByWheel(new EsVector(request.DeltaX, request.DeltaY), request.Shift);
        _selector.ZoomRequested += (_, request) =>
            _view.ZoomByWheel(request.ZoomIn, ToViewportPosition(request.Position));

        RefreshAppearance(themeBackground);
        host.Add(_selector);
        UpdatePort();
    }

    /// <summary>選択オーバーレイのローカル座標をビューポート（ズームビュー）のローカル座標へ変換する。</summary>
    /// <param name="overlayPosition">オーバーレイのローカル座標（DIP）。</param>
    /// <returns>ビューポートのローカル座標（DIP）。</returns>
    /// <remarks>
    /// 両者は通常同一領域を占める（重ね合わせ）が、レイアウトの差に依存しないようビジュアルツリーの変換に委譲する。
    /// </remarks>
    private EsPoint ToViewportPosition(EsPoint overlayPosition)
        => _view.TranslatePositionFrom(_selector, overlayPosition);

    /// <summary>切り出し中かどうかを取得する。</summary>
    /// <value>切り出しセッションが進行中の場合は true。</value>
    public bool IsActive => _state.IsRunning;

    /// <summary>切り出しの開始・確定・キャンセルを 1 つの操作で切り替える（ツールバーのボタン用）。</summary>
    /// <remarks>
    /// 未開始なら開始、選択済みなら確定、未選択ならキャンセルする（Avalonia 版の切り出しボタンと同じ振る舞い）。<br/>
    /// キー（Enter / Escape）と同じ操作を 1 つのメソッドへ集約し、経路違いの重複を作らない。
    /// </remarks>
    public void Toggle()
    {
        if (!IsActive)
        {
            Start();
            return;
        }

        if (_selector.HasSelection)
        {
            Confirm();
            return;
        }

        Cancel();
    }

    /// <summary>切り出しを開始する（オーバーレイを表示し、入力を選択コントロールへ移す）。</summary>
    /// <remarks>画像が未設定の場合は何もしない（切り出す対象がない）。</remarks>
    public void Start()
    {
        if (_target.CurrentImage is not { } image)
        {
            return;
        }

        // 再入禁止（既に切り出し中の場合は開始しない）
        if (!_state.TryBegin())
        {
            return;
        }

        _log.LogCropStart(image.Width, image.Height);
        UpdatePort();
        _selector.SetLoupeSource(image);
        _selector.ClearSelection();
        _selector.IsVisible = true;

        // 非表示の間に描画済みのキャンバスは再描画されないため、表示後に明示的に無効化する
        _selector.InvalidateOverlay();
        _selector.NotifyViewportChanged();

        // 矢印キーの微調整と Escape を受け取れるよう、選択コントロールへフォーカスを移す
        _selector.FocusMarquee();
    }

    /// <summary>切り出しを終了する（選択を破棄してオーバーレイを隠す）。</summary>
    public void Cancel()
    {
        if (_state.TryComplete())
        {
            _log.LogCropCancelled();
        }

        _selector.ClearSelection();
        _selector.ClearLoupe();
        _selector.IsVisible = false;
        _state.End();
    }

    /// <summary>テーマの変更を外観へ反映する（市松模様の下地色）。</summary>
    /// <param name="themeBackground">テーマのページ背景色。</param>
    /// <remarks>
    /// テーマ変更の購読はホストが行う（本クラスはテーマの通知元を知らない）。<br/>
    /// 呼び出し後は選択コントロールが描き直され、ルーペの市松模様の下地が新しいテーマ色になる。
    /// </remarks>
    public void RefreshAppearance(DrawingColor themeBackground)
    {
        _themeBackground = themeBackground;
        ApplyAppearance();
        _selector.InvalidateOverlay();
    }

    /// <summary>選択された領域を切り出す。</summary>
    /// <remarks>
    /// 1. 画像が無い・選択が退化している場合はキャンセルする（0 サイズの画像を作らない）。<br/>
    /// 2. 先にセッションを終了してから画像を差し替える（オーバーレイの後片付けを確実にする）。<br/>
    /// 3. 切り出しは <see cref="ICaptureTarget.ApplyCrop"/> へ委譲する（Undo 退避と画像差し替えはアプリ側の責務）。
    /// </remarks>
    private void Confirm()
    {
        if (_target.CurrentImage is not { } image)
        {
            Cancel();
            return;
        }

        // 切り出しは「始点・終点のピクセルを含む」閉区間のため、終点の 1 ピクセルを加える
        // （計算は CropBoundsMath（コア）が単一所有する。Avalonia 版と同じ規則）
        var pixels = CropBoundsMath.ComputeCropBounds(
            _selector.SelectionRect, new EsSize(image.Width, image.Height), _settings.MinSelectionSize!.Value);
        if (pixels.IsEmpty)
        {
            Cancel();
            return;
        }

        _log.LogCropCompleted($"{pixels.X},{pixels.Y},{pixels.Width},{pixels.Height}");
        Cancel();
        _target.ApplyCrop(pixels);
    }

    /// <summary>表示中のズーム・パンに合わせて座標変換ポートを更新する。</summary>
    /// <remarks>
    /// 画像矩形（ビューポート座標）と実効倍率（表示サイズ ÷ 画像幅）から、ローカル DIP ⇔ 画像ピクセルを<br/>
    /// 変換するポートを組み立てる。ズーム・パンで表示が変わるたびに呼び直す（ポートは都度生成する）。<br/>
    /// ドメイン座標（元画像ピクセル）はズーム・パンで不変のため、選択矩形は維持される（オーバーレイの描画位置のみが追従する）。
    /// </remarks>
    private void UpdatePort()
    {
        var image = _target.CurrentImage;
        var rect = _view.ImageRectViewport();
        var display = _view.ZoomView.DisplaySize();
        var scale = image is { Width: > 0 } && display.Width > 0
            ? display.Width / image.Width
            : 1.0;

        _selector.SetCoordinateTransform(new CoordTransform(
            p => new EsPoint((p.X - rect.X) / scale, (p.Y - rect.Y) / scale),
            p => new EsPoint(p.X * scale + rect.X, p.Y * scale + rect.Y),
            () => image is null ? null : new EsSize(image.Width, image.Height)));
    }

    /// <summary>設定を選択コントロールへ反映する。</summary>
    /// <remarks>
    /// 設定項目と外観の対応はコアのマッパー（<c>FadeMaskDragRectOptionsMapper.ToFadeMaskDragRectOptions</c>）が単一所有する<br/>
    /// （Avalonia 版の <c>CropSession</c> も同じ変換を使うため、UI ごとに見た目がずれない）。<br/>
    /// ルーペの市松模様の下地にはテーマの背景色を渡す（プレビュー画像を透かして市松色をそのまま重ねない）。
    /// </remarks>
    private void ApplyAppearance()
    {
        var s = _settings;

        // 設定を 1 値に変換して一括適用する（ラベル文言は描画時に評価するため別途設定する）
        _selector.Options = s.ToFadeMaskDragRectOptions(_themeBackground);

        // サイズラベルは画像ピクセル（ドメイン座標）で表示する（表示時点の選択を持ち出す）
        _selector.SizeLabelOverrideProvider = () =>
            _selector.HasSelection && s.SizeLabelVisible!.Value
                ? OuterDecoLabels.FormatSizeLabel(_selector.SelectionRect, scaling: 1.0)
                : null;

        // 操作ラベル・情報ラベルの文言はキャプチャコア（CaptureLabelTexts）が単一所有する。
        // 情報ラベルは切り出しでは表示しない（GetInfoText が切り出しモードで null を返す）。
        _selector.OperationTextProvider = () => CaptureLabelTexts.GetOperationText(_selector.Mode);
        _selector.InfoTextProvider = () => CaptureLabelTexts.GetInfoText(_selector.Mode, null);
    }
}
