using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using EsUtil.Platform.AvaloniaUI;
using Estable.AvaloniaUI.ZoomView.Logging;
using Estable.ZoomView;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using EsUtil.Platform.Core;

namespace Estable.AvaloniaUI.ZoomView;

/// <summary>
/// 画像プレビューのズーム・パン・Fit 表示を提供する自己完結型複合コントロール。
/// </summary>
/// <remarks>
/// 内部 ZoomableView（ScrollViewer 不使用のパン・ズームビューポート）を内包し、<br/>
/// ズーム率の遷移（⊖⊕ 23 段階 / Ctrl+ホイール ×1.1）・適用・Fit 表示を統括する。<br/>
/// <br/>
/// <b>ズーム率の単一所有</b>: ズーム率は内包する ZoomableView が単一所有し、<br/>
/// 本コントロールは <see cref="ZoomPercent"/>（読み書き可能）でホストへ操作窓口を提供する。<br/>
/// ズーム可否（<see cref="CanZoomIn"/> / <see cref="CanZoomOut"/>）も本コントロールが所有する。<br/>
/// <br/>
/// <b>自己完結</b>: Ctrl+ホイールのズーム要求（ZoomableView.ZoomRequested）とビューポートリサイズ時の<br/>
/// Fit 再適用（ZoomableView.SizeChanged）を内部で購読し、ホスト側の配線を不要にする。<br/>
/// イベント（<see cref="ZoomChanged"/>）でズーム率・可否の変化をホストへ通知し、任意の UI（スライダ・ボタン等）へ反映できる。<br/>
/// <br/>
/// <b>汎用性</b>: 特定コントロール（Slider 等）へは依存しない。ズーム表示の反映は <see cref="ZoomChanged"/> で疎結合にする。<br/>
/// 計算（<see cref="ZoomMath"/>）と遷移（<see cref="ZoomTransition"/>）は純粋関数に分離し、単体テストで仕様を検証できる。
/// </remarks>
public sealed class ZoomViewControl : Panel
{
    /// <summary>ズーム状態反映（ApplyZoom）の再入防止フラグ。</summary>
    private bool _isApplyingZoom;

    /// <summary>ズーム操作の可否（null = 未計算。参照時に計算してキャッシュ）。</summary>
    /// <remarks>
    /// 2 つの可否（<see cref="CanZoomIn"/> / <see cref="CanZoomOut"/>）を単一の値として保持し、<br/>
    /// 「片方だけ計算済み」という矛盾状態を構造的に排除する。<br/>
    /// null の場合はプロパティ参照時に <see cref="ZoomTransition"/> から遅延計算する。
    /// </remarks>
    private ZoomAvailability? _availability;

    /// <summary>内包するパン・ズームビューポート。</summary>
    private readonly ZoomableView _zoomableView = new();

    /// <summary>ズーム遷移の設定（コンストラクタまたはプロパティで設定。不変値）。</summary>
    private ZoomConfig _config;

    /// <summary>Fit 実効倍率（%）のキャッシュ。null = 未計算。イメージ・ビューポートサイズ変更時に無効化する。</summary>
    /// <remarks>呼び出し毎にビューポートと元画像サイズから計算し直すのを避け、ホットパス（ズーム可否・実効倍率参照）で再利用する。</remarks>
    private double? _fitZoomCache;

    /// <summary>ズーム率の最小値（固定倍率）。</summary>
    /// <remarks>
    /// <see cref="ZoomConfig"/> の <see cref="ZoomConfig.MinZoom"/> を返す。設定時は <see cref="ZoomConfig.With"/>（検証付き）で再構築する。<br/>
    /// クランプ・書式化に参照できる。
    /// </remarks>
    public double MinZoom
    {
        get => _config.MinZoom;
        set => SetConfig(_config.With(minZoom: value));
    }

    /// <summary>ズーム率の最大値（固定倍率）。</summary>
    /// <remarks>
    /// <see cref="ZoomConfig"/> の <see cref="ZoomConfig.MaxZoom"/> を返す。設定時は <see cref="ZoomConfig.With"/>（検証付き）で再構築する。<br/>
    /// クランプ・書式化に参照できる。
    /// </remarks>
    public double MaxZoom
    {
        get => _config.MaxZoom;
        set => SetConfig(_config.With(maxZoom: value));
    }

    /// <summary>Ctrl+ホイールのズーム倍率。</summary>
    /// <remarks><see cref="ZoomConfig"/> の <see cref="ZoomConfig.WheelFactor"/> を返す。設定時は <see cref="ZoomConfig.With"/>（検証付き）で再構築する。</remarks>
    public double WheelFactor
    {
        get => _config.WheelFactor;
        set => SetConfig(_config.With(wheelFactor: value));
    }

    /// <summary>⊖⊕ ボタンのズームレベル集合。</summary>
    /// <remarks><see cref="ZoomConfig"/> の <see cref="ZoomConfig.ButtonZoomLevels"/> を返す。設定時は <see cref="ZoomConfig.With"/>（検証付き）で再構築する。</remarks>
    public IReadOnlyList<double> ButtonZoomLevels
    {
        get => _config.ButtonZoomLevels;
        set => SetConfig(_config.With(buttonZoomLevels: value));
    }

    /// <summary>設定値を更新し、ズーム可否を再計算する。</summary>
    /// <param name="config">新しい設定（検証済み）。</param>
    private void SetConfig(ZoomConfig config)
    {
        _config = config;
        // ズーム範囲が変わったため、可否（CanZoomIn / CanZoomOut）を未計算に戻して再評価する
        _availability = null;
    }

    /// <summary>内部ロガー（横断的関心事）。null を設定した場合は <see cref="NullLogger"/> へフォールバックする。</summary>
    /// <value>null 可。設定しない場合はログを出力しない。</value>
    public ILogger Log
    {
        get => field.OrNullLogger();
        set => field = value.OrNullLogger();
    }

    /// <summary>ZoomViewControl を初期化し、内包する ZoomableView の配線を自己完結させる（既定の設定）。</summary>
    /// <remarks>
    /// ズーム遷移の設定は既定値（<see cref="ZoomConfig.Default"/>）を使用する。<br/>
    /// カスタム設定は <see cref="ZoomViewControl(ZoomConfig)"/> を使用する。
    /// </remarks>
    public ZoomViewControl() : this(ZoomConfig.Default) { }

    /// <summary>ZoomViewControl を初期化し、内包する ZoomableView の配線を自己完結させる。</summary>
    /// <param name="config">ズーム遷移の設定。</param>
    /// <remarks>
    /// ZoomableView を子として追加し、Ctrl+ホイールのズーム要求（ZoomableView.ZoomRequested）と<br/>
    /// ビューポートリサイズ時の Fit 再適用（ZoomableView.SizeChanged）を内部で購読する。<br/>
    /// ホストは ZoomableView.ZoomRequested / ZoomableView.SizeChanged の配線をコードビハインドで行う必要がない。<br/>
    /// ズーム範囲・倍率・レベル集合をカスタマイズする場合は本コンストラクタで <see cref="ZoomConfig"/> を注入する。<br/>
    /// 既定の設定で生成する場合は無引数コンストラクタ（<see cref="ZoomViewControl()"/>）を使用する。
    /// </remarks>
    public ZoomViewControl(ZoomConfig config)
    {
        _config = config;
        Children.Add(_zoomableView);

        // Ctrl+ホイールのズーム要求（ズーム率の決定は本コントロール。マウスポインタ位置をズーム中心に使用）。
        // ズーム後に PointerPosition をリセットする（次回のボタン・スライダ操作はビューポート中心でズームする）。
        _zoomableView.ZoomRequested += (_, e) =>
        {
            PointerPosition = e.PointerPosition;
            if (e.ZoomIn) ZoomInByWheel();
            else ZoomOutByWheel();
            PointerPosition = null;
        };

        // ビューポートリサイズ時に Fit 表示（表示テキスト）を再計算する
        _zoomableView.SizeChanged += (_, e) => OnZoomableViewSizeChanged(e);

        // ビューポート変化（パン・ズーム・表示状態）を外部へ再発火する（オーバーレイ追従用）。
        // ホストが内部 ZoomableView へ到達する必要をなくすため、本コントロールが中継する。
        _zoomableView.ViewportChanged += (_, _) => ViewportChanged?.Invoke(this, EventArgs.Empty);

        // 初期状態のズーム可否を未計算に戻す（初回参照時に遅延計算してキャッシュする）
        _availability = null;
    }

    /// <summary>ズーム中心として使用するマウスポインタ位置（ビューポートローカル DIP）。null の場合はビューポート中心。</summary>
    /// <remarks>
    /// Ctrl+ホイールズーム時に設定され、マウスポインタ位置を中心に拡大縮小する。<br/>
    /// null（ボタン・スライダ・フライアウト操作）の場合は、表示されている中心位置を中心に拡大縮小する。<br/>
    /// 外部からは設定せず、内部（Ctrl+ホイールハンドラ）のみが使用する。<br/>
    /// 内部のズーム処理は <see cref="ZoomPercent"/>・<see cref="ApplyZoomTransition"/> が本位置を参照する。
    /// </remarks>
    private Point? PointerPosition { get; set; }

    /// <summary>表示する画像（ズーム対象）。設定すると Fit 表示にリセットする。</summary>
    /// <remarks>画像変更時は内部 ZoomableView へ伝搬する。null で画像なし。</remarks>
    public IImage? Source
    {
        get => _zoomableView.Source;
        set
        {
            _zoomableView.Source = value;
            // 画像サイズが変わるため、Fit 実効倍率・ズーム可否のキャッシュを無効化する
            _fitZoomCache = null;
            _availability = null;
            ApplyZoom();
        }
    }

    /// <summary>画像の中央寄せ / 左上寄せ（その軸で画像がビューポートより小さい場合に有効）。</summary>
    /// <remarks>
    /// Fit 表示中および固定倍率で、その軸で画像がビューポートより小さい場合に、画像をビューポート中央に寄せるか左上に置くかを指定する。<br/>
    /// 軸ごとに独立して判定し、一方の軸がスクロール可能（画像がビューポートより大きい）でも、他方の軸の中央寄せは維持される。<br/>
    /// スクロール可能な軸はパン制御（オフセット）が優先されるため、本設定はその軸には反映されない。
    /// </remarks>
    public bool CenterAlign
    {
        get => _zoomableView.CenterAlign;
        set
        {
            _zoomableView.CenterAlign = value;
            ApplyZoom();
        }
    }

    /// <summary>ビューポート座標をコンテンツ座標（元画像ピクセル）へ変換する（画像表示矩形の原点基準）。</summary>
    /// <param name="viewportPos">ビューポート座標。</param>
    /// <returns>コンテンツ座標（元画像ピクセル）。</returns>
    /// <remarks>
    /// ZoomableView.ViewportToContentImage へ委譲する（座標変換の単一所有）。<br/>
    /// 画像表示矩形（<see cref="ImageRectViewport"/>）の原点を基準に変換し、Fit 中央寄せの原点補正を含む。<br/>
    /// ホストが内部 ZoomableView へ到達する必要をなくすため、本コントロールが中継する。
    /// </remarks>
    public Point ViewportToContentImage(Point viewportPos)
        => _zoomableView.ViewportToContentImage(viewportPos);

    /// <summary>コンテンツ座標（元画像ピクセル）をビューポート座標へ変換する（画像表示矩形の原点基準）。</summary>
    /// <param name="contentPos">コンテンツ座標（元画像ピクセル）。</param>
    /// <returns>ビューポート座標。</returns>
    /// <remarks>
    /// ZoomableView.ContentToViewportImage へ委譲する（座標変換の単一所有）。<br/>
    /// 画像表示矩形（<see cref="ImageRectViewport"/>）の原点を基準に変換し、Fit 中央寄せの原点補正を含む。<br/>
    /// ホストが内部 ZoomableView へ到達する必要をなくすため、本コントロールが中継する。
    /// </remarks>
    public Point ContentToViewportImage(Point contentPos)
        => _zoomableView.ContentToViewportImage(contentPos);

    /// <summary>画像の表示矩形（ビューポート座標）を取得する。</summary>
    /// <returns>画像の表示矩形。画像未設定時は default。</returns>
    /// <remarks>
    /// ZoomableView.ImageRectViewport へ委譲する。<br/>
    /// 画像原点を起点とした表示サイズ（Fit 中央寄せの原点補正を含む）を取得し、画像内判定に使用する。<br/>
    /// ホストが内部 ZoomableView へ到達する必要をなくすため、本コントロールが中継する。
    /// </remarks>
    public Rect ImageRectViewport()
        => _zoomableView.ImageRectViewport();

    /// <summary>現在の実効ズーム率（%）。Fit 中は実効倍率、固定倍率中はズーム率。ズーム率の単一所有（内部 ZoomableView）から読み書きする。</summary>
    /// <remarks>
    /// <b>getter</b>: 実効ズーム率を返す。Fit 中（内部ズーム率が 0）はビューポートと元画像サイズから算出した実効倍率、<br/>
    /// 固定倍率中はズーム率を返す（0 にはならない）。<br/>
    /// <b>setter</b>: <c>0 より大きい値</c> のみ受け付ける（固定倍率を設定）。<c>0 以下</c> は無視する。<br/>
    /// Fit 表示は <see cref="ZoomFit"/> を使用する（<c>ZoomPercent = 0</c> では Fit にできない）。<br/>
    /// setter は ZoomableView.SetZoom で設定した後に <see cref="ApplyZoom"/> で可否・イベントを反映する。<br/>
    /// ズーム中心維持には <see cref="PointerPosition"/> を使用する（null = ビューポート中心）。<br/>
    /// Fit 判定は <see cref="IsFit"/> を使用する（<see cref="ZoomPercent"/> は実効値のため 0 にならない）。
    /// </remarks>
    public double ZoomPercent
    {
        get => ComputeEffectiveZoomPercent(_zoomableView.InternalZoomPercent, ComputeRawFitZoomPercent());
        set
        {
            // 0 以下は Fit の意図と区別するため受け付けない（Fit は ZoomFit を使用）。範囲は設定（ZoomConfig）でクランプ
            if (value <= 0) return;
            _zoomableView.SetZoom(_config.ClampZoom(value), PointerPosition);
            ApplyZoom();
        }
    }

    /// <summary>現在 Fit 表示中かどうか（内部ズーム率が 0 = 自動）。</summary>
    /// <value>Fit 表示中なら true。</value>
    /// <remarks>
    /// 内部ズーム率が 0（Fit）かどうかを意味的に判定する。<br/>
    /// ホストが内部の <c>Image.Stretch</c> 等に依存せずに Fit 状態を判定できるよう公開する
    /// （<see cref="ZoomPercent"/> は実効ズーム率のため 0 にならない。Fit 判定は本プロパティを使用する）。
    /// </remarks>
    public bool IsFit => _zoomableView.InternalZoomPercent <= 0;

    /// <summary>現在のズーム状態でズームインが可能かどうか（段階遷移仕様に基づく遅延計算キャッシュ）。</summary>
    /// <value>ズームイン可能なら true。未計算の場合は参照時に計算してキャッシュする。</value>
    /// <remarks>
    /// <see cref="ZoomTransition.CanZoomIn"/>（段階遷移仕様）で判定し、初回参照・状態変化時にキャッシュする。<br/>
    /// イベント（<see cref="ZoomChanged"/>）を待たずに初期表示時のボタン状態を設定したい場合に参照する。<br/>
    /// 画像未設定時（<see cref="Source"/> が null）は false。
    /// </remarks>
    public bool CanZoomIn => GetAvailability().CanZoomIn;

    /// <summary>現在のズーム状態でズームアウトが可能かどうか（段階遷移仕様に基づく遅延計算キャッシュ）。</summary>
    /// <value>ズームアウト可能なら true。未計算の場合は参照時に計算してキャッシュする。</value>
    /// <remarks>
    /// <see cref="ZoomTransition.CanZoomOut"/>（段階遷移仕様）で判定し、初回参照・状態変化時にキャッシュする。<br/>
    /// イベント（<see cref="ZoomChanged"/>）を待たずに初期表示時のボタン状態を設定したい場合に参照する。<br/>
    /// 画像未設定時（<see cref="Source"/> が null）は false。
    /// </remarks>
    public bool CanZoomOut => GetAvailability().CanZoomOut;

    /// <summary>ズーム率が変更されたときに発火するイベント（ホスト側のスライダ・⊖⊕ ボタン等の同期用）。</summary>
    /// <remarks>
    /// <see cref="ApplyZoom"/> 実行後に変更後のズーム率と可否（<see cref="ZoomChangedEventArgs"/>）を通知する。<br/>
    /// ホスト側は本イベントを購読し、スライダ・⊖⊕ ボタンの Enable/Disable・表示テキスト等の任意の UI へ反映できる<br/>
    /// （本コントロールは特定コントロールに依存しない）。
    /// </remarks>
    public event EventHandler<ZoomChangedEventArgs>? ZoomChanged;

    /// <summary>ビューポート（パン・ズーム・表示状態）が変化したときに発火するイベント（オーバーレイ追従用）。</summary>
    /// <remarks>
    /// 内包する ZoomableView.ViewportChanged を再発火する。<br/>
    /// パン・ズーム・スクロールバー・画像配置の変更後に発火し、<br/>
    /// 画像プレビュー上のオーバーレイ（Crop の暗転マスク・選択枠・ルーペ等）の追従に使用する。<br/>
    /// ホストが内部 ZoomableView へ到達する必要をなくすため、本コントロールが中継する。
    /// </remarks>
    public event EventHandler? ViewportChanged;

    /// <summary>ズームインする（⊖⊕ボタン）。</summary>
    /// <remarks>
    /// 設定されたレベル集合（ZoomConfig.ButtonZoomLevels）の次のレベルへ移動する（<see cref="ZoomTransition.NextLevel"/>）。<br/>
    /// Fit は遷移に含めない。リスト末尾（ZoomConfig.MaxZoom）で停止する。<br/>
    /// 移動先がなければ何もしない（現在のズーム率を維持）。
    /// </remarks>
    public void ZoomIn() => ApplyZoomStep((c, f) => ZoomTransition.NextLevel(c, f, _config), Log.LogZoomIn);

    /// <summary>ホイールの回転量でパンする（回転量から移動量への換算を単一所有する）。</summary>
    /// <param name="delta">ホイールの回転量（X = 横 / Y = 縦。1 ノッチ = 1.0）。</param>
    /// <param name="shift">Shift 併用かどうか（縦回転を横パンとして扱う）。</param>
    /// <remarks>
    /// 移動量は DIP で計算し、実効倍率で除してコンテンツ座標へ変換する（ズーム中も画面上の移動量が一定になる）。<br/>
    /// 本コントロール上でホイールが発生した場合（内包ビューポート）と、ホストが他のコントロール（選択オーバーレイ等）で
    /// 受けたホイールを転送する場合の双方が本メソッドを使用する（換算則の二重所有を作らない）。
    /// </remarks>
    public void PanByWheel(Vector delta, bool shift)
    {
        var dx = shift ? delta.Y * ZoomMath.WheelScrollUnit : delta.X * ZoomMath.WheelScrollUnit;
        var dy = shift ? 0 : delta.Y * ZoomMath.WheelScrollUnit;
        _zoomableView.PanBy(new Vector(dx / _zoomableView.EffectiveScale, dy / _zoomableView.EffectiveScale));
    }

    /// <summary>Ctrl+ホイールのズームを適用する（ホイール操作の公開 API）。</summary>
    /// <param name="zoomIn">true = ズームイン / false = ズームアウト。</param>
    /// <param name="pointerPosition">ズーム中心とする位置（DIP）。null の場合はビューポート中心。</param>
    /// <remarks>
    /// 遷移先の計算とログは <see cref="ApplyZoomStep"/> へ委譲する。<br/>
    /// 本コントロール上でホイールが発生した場合と、ホストが他のコントロールで受けたホイールを転送する場合の双方が使用する。
    /// </remarks>
    public void ZoomByWheel(bool zoomIn, Point? pointerPosition = null)
    {
        PointerPosition = pointerPosition;
        try
        {
            if (zoomIn) { ZoomInByWheel(); }
            else { ZoomOutByWheel(); }
        }
        finally
        {
            PointerPosition = null;
        }
    }

    /// <summary>ズームアウトする（⊖⊕ボタン）。</summary>
    /// <remarks>
    /// 設定されたレベル集合（ZoomConfig.ButtonZoomLevels）の前のレベルへ移動する（<see cref="ZoomTransition.PrevLevel"/>）。<br/>
    /// Fit は遷移に含めない。リスト先頭（ZoomConfig.MinZoom）で停止する。<br/>
    /// 移動先がなければ何もしない（現在のズーム率を維持）。
    /// </remarks>
    public void ZoomOut() => ApplyZoomStep((c, f) => ZoomTransition.PrevLevel(c, f, _config), Log.LogZoomOut);

    /// <summary>Fit 表示（ビューポートに収まる自動倍率）へ戻す。</summary>
    /// <remarks>
    /// 内部ズーム率を 0（Fit）に設定する（<see cref="ZoomPercent"/> setter は 0 を受け付けないため、本メソッドが唯一の Fit 移行経路）。<br/>
    /// Fit 表示中は実効倍率（ビューポートと元画像サイズから算出）が <see cref="ZoomPercent"/>（実効ズーム率）の基準になる。<br/>
    /// <see cref="ZoomIn"/> / <see cref="ZoomOut"/> と対になるズーム操作で、ホストは本メソッドで Fit へ戻す。
    /// </remarks>
    public void ZoomFit()
    {
        _zoomableView.SetZoom(0, PointerPosition);
        ApplyZoom();
    }

    /// <summary>Ctrl+ホイールでズームインする（拡大 = Round(現在の拡大率 × WheelFactor)）。</summary>
    /// <remarks>
    /// <see cref="ZoomTransition.WheelZoomIn"/> で倍率を計算する。Fit は遷移に含めない。<br/>
    /// Fit 表示中は実効倍率の WheelFactor 倍へ移動する（実効倍率 ≥ ZoomConfig.MaxZoom なら拡大不可）。<br/>
    /// 移動先がなければ何もしない（現在のズーム率を維持）。<br/>
    /// Ctrl+ホイールのハンドラのみが使用する（外部からの呼び出しは不要）。
    /// </remarks>
    private void ZoomInByWheel() => ApplyZoomStep((c, f) => ZoomTransition.WheelZoomIn(c, f, _config), Log.LogZoomInWheel);

    /// <summary>Ctrl+ホイールでズームアウトする（縮小 = Round(現在の拡大率 ÷ WheelFactor)）。</summary>
    /// <remarks>
    /// <see cref="ZoomTransition.WheelZoomOut"/> で倍率を計算する。Fit は遷移に含めない。<br/>
    /// Fit 表示中は実効倍率の 1/WheelFactor 倍へ移動する（実効倍率 ≤ ZoomConfig.MinZoom なら縮小不可）。<br/>
    /// 移動先がなければ何もしない（現在のズーム率を維持）。<br/>
    /// Ctrl+ホイールのハンドラのみが使用する（外部からの呼び出しは不要）。
    /// </remarks>
    private void ZoomOutByWheel() => ApplyZoomStep((c, f) => ZoomTransition.WheelZoomOut(c, f, _config), Log.LogZoomOutWheel);

    /// <summary>ズーム遷移の一歩を適用する（遷移計算・ログ・ZoomableView への設定・表示反映を共通化）。</summary>
    /// <param name="transition">現在のズーム率と Fit 実効倍率から遷移先のズーム率を計算する関数。</param>
    /// <param name="log">変更前後のズーム率を記録するログメソッド。</param>
    /// <remarks>
    /// ズーム操作（⊖⊕ボタン・Ctrl+ホイール）の共通処理。<br/>
    /// Fit 中（<c>zoom &lt;= 0</c>）のみ Fit 実効倍率を計算し、固定倍率中は 0 を渡す（遷移関数は Fit 中のみ fitZoom を使うため）。<br/>
    /// 遷移先が null（移動不可）の場合は何もしない。
    /// </remarks>
    private void ApplyZoomStep(Func<double, double, double?> transition, Action<int, int> log)
    {
        // 遷移関数は内部ズーム率（0 = Fit / >0 = 固定倍率）を期待する。ZoomPercent（実効ズーム率）は Fit 中も 0 にならないため
        // 内部ズーム率（_zoomableView.InternalZoomPercent）を渡す。
        var zoom = _zoomableView.InternalZoomPercent;
        // 遷移関数は Fit 中（current <= 0）のみ fitZoom を使うため、固定倍率中は計算しない
        var fitZoom = zoom <= 0 ? ComputeRawFitZoomPercent() : 0;
        ApplyZoomTransition(transition(zoom, fitZoom), log);
    }

    /// <summary>現在のズーム状態をズーム可否とイベント購読者へ反映する。</summary>
    /// <remarks>
    /// 内包する ZoomableView が所有する現在のズーム率から、<br/>
    /// ズーム可否を更新し、<see cref="ZoomChanged"/> でズーム率・表示用倍率・可否を通知する。<br/>
    /// <see cref="ZoomPercent"/> と ZoomableView のズーム率を同期させるため、<br/>
    /// ズーム率変更（<see cref="ZoomPercent"/> の setter・<see cref="ZoomIn"/> 等）の後に呼ぶ。<br/>
    /// ズーム率変更の適用経路（<see cref="ZoomPercent"/> setter・各ズーム操作）が内部的に呼び出す（再入防止付き）。
    /// </remarks>
    private void ApplyZoom()
    {
        if (_isApplyingZoom) return; // 再入防止
        _isApplyingZoom = true;
        try
        {
            ApplyZoomCore();
        }
        finally
        {
            _isApplyingZoom = false;
        }
    }

    /// <summary>ApplyZoom の本体（再入防止フラグは呼び出し側で管理）。</summary>
    private void ApplyZoomCore()
    {
        if (Source is null) return;

        // 内部ズーム率（0 = Fit / >0 = 固定倍率）が現在の状態
        var internalZoom = _zoomableView.InternalZoomPercent;
        var isFit = internalZoom <= 0;

        // Fit 実効倍率を計算し、実効ズーム率（Fit 中はクランプした実効倍率・固定倍率中はズーム率）を導出する
        var fitZoom = ComputeRawFitZoomPercent();
        var zoom = ComputeEffectiveZoomPercent(internalZoom, fitZoom);

        // ⊖⊕ ボタンの Enable/Disable 制御用に、段階遷移仕様で可否を即時計算してキャッシュし、通知する
        RefreshZoomAvailability(internalZoom, fitZoom);
        var availability = _availability!.Value;
        ZoomChanged?.Invoke(this, new ZoomChangedEventArgs(zoom, isFit, availability.CanZoomIn, availability.CanZoomOut));
    }

    /// <summary>現在のズーム状態からズーム可否を取得する（未計算なら遅延計算してキャッシュ）。</summary>
    /// <returns>ズーム可否。</returns>
    /// <remarks>
    /// <see cref="CanZoomIn"/> / <see cref="CanZoomOut"/> プロパティの参照時に呼ばれる。<br/>
    /// Fit 実効倍率は現在のビューポートと画像から計算する（<see cref="ComputeRawFitZoomPercent"/>）。
    /// </remarks>
    private ZoomAvailability GetAvailability()
        => _availability ??= BuildAvailability(_zoomableView.InternalZoomPercent, ComputeRawFitZoomPercent());

    /// <summary>実効ズーム率（%）を導出する（Fit 中はクランプした実効倍率・固定倍率中は内部ズーム率）。</summary>
    /// <param name="internalZoom">内部ズーム率（%）。0 = Fit。</param>
    /// <param name="fitZoom">Fit の実効倍率（%）。</param>
    /// <returns>実効ズーム率（%）。0 にはならない（Fit 中も実効倍率を返す）。</returns>
    /// <remarks><see cref="ZoomPercent"/> getter と <see cref="ApplyZoomCore"/> の実効ズーム率導出を単一の実装に集約する（DRY 原則）。</remarks>
    private double ComputeEffectiveZoomPercent(double internalZoom, double fitZoom)
        => internalZoom <= 0 ? _config.ClampZoom(fitZoom) : internalZoom;

    /// <summary>段階遷移仕様（<see cref="ZoomTransition"/>）に基づき、ズーム可否を計算してキャッシュする。</summary>
    /// <param name="zoom">現在のズーム率（%）。0 = Fit。</param>
    /// <param name="fitZoom">Fit の実効倍率（%）。</param>
    /// <remarks>
    /// <see cref="CanZoomIn"/> / <see cref="CanZoomOut"/> プロパティのキャッシュを更新する。<br/>
    /// <see cref="ApplyZoomCore"/>（fitZoom 計算済みを再利用）と <see cref="GetAvailability"/>（遅延計算）から呼ばれる。<br/>
    /// 画像未設定時（<see cref="Source"/> が null）は false にする。
    /// </remarks>
    private void RefreshZoomAvailability(double zoom, double fitZoom)
        => _availability = BuildAvailability(zoom, fitZoom);

    /// <summary>段階遷移仕様（<see cref="ZoomTransition"/>）に基づき、ズーム可否を計算する。</summary>
    /// <param name="zoom">現在のズーム率（%）。0 = Fit。</param>
    /// <param name="fitZoom">Fit の実効倍率（%）。</param>
    /// <returns>ズーム可否。画像未設定時（<see cref="Source"/> が null）は false。</returns>
    /// <remarks>可否の導出は <see cref="ZoomTransition.BuildAvailability"/>（純粋関数）へ委譲する。</remarks>
    private ZoomAvailability BuildAvailability(double zoom, double fitZoom)
    {
        var (canZoomIn, canZoomOut) = ZoomTransition.BuildAvailability(zoom, fitZoom, _config, Source is not null);
        return new ZoomAvailability(canZoomIn, canZoomOut);
    }

    /// <summary>ズーム遷移の適用（ログ記録 + ZoomableView へのズーム率設定）を共通化する。</summary>
    /// <param name="target">遷移先のズーム率。null の場合は何もしない（移動不可）。</param>
    /// <param name="log">変更前後のズーム率を記録するログメソッド。</param>
    /// <remarks>ズーム率は ZoomableView が単一所有するため、ZoomableView.SetZoom で変更する。</remarks>
    private void ApplyZoomTransition(double? target, Action<int, int> log)
    {
        if (target is not { } t) return;
        var zoom = (int)Math.Round(_zoomableView.InternalZoomPercent);
        log(zoom, (int)Math.Round(t));
        _zoomableView.SetZoom(_config.ClampZoom(t), PointerPosition);
        ApplyZoom();
    }

    /// <summary>ビューポートリサイズ時に Fit モードのズーム可否と表示用倍率を更新する。</summary>
    /// <remarks>
    /// Fit 実効倍率はビューポートと元画像サイズから算出する（<see cref="ZoomMath.FitZoomPercent"/> に委譲）。<br/>
    /// Fit 中のズーム可否（<see cref="CanZoomIn"/> / <see cref="CanZoomOut"/>）も実効倍率に依存するため、
    /// 未計算（null）に戻して次の参照時に遅延計算する。また <see cref="ZoomChanged"/> を発火して表示用倍率を再通知する。
    /// </remarks>
    private void OnZoomableViewSizeChanged(SizeChangedEventArgs e)
    {
        if (IsFit)
        {
            // ズーム可否は実効倍率に依存するため、未計算（null）に戻して次の参照時に再計算（遅延）する
            _availability = null;
            // ビューポートサイズが変わるため、Fit 実効倍率のキャッシュを無効化する
            _fitZoomCache = null;
            // 表示用倍率（実効倍率）が変わったため、ホストへ再通知する
            ApplyZoom();
        }
    }

    /// <summary>Fit 表示時の実表示倍率（%）をクランプなしで計算する。</summary>
    /// <returns>実表示倍率（%）。ビューポートと元画像サイズから算出、画像未設定時は 100。</returns>
    /// <remarks>
    /// 現在の表示状態に依存しないため、固定倍率表示中でも常に一定の Fit 実効倍率を返す。<br/>
    /// 必ずビューポートと元画像サイズから計算する（<see cref="ZoomMath.FitZoomPercent"/> に委譲）。<br/>
    /// 元画像サイズは <see cref="IImage.Size"/>（DIP）から取得する。Avalonia の <c>Bitmap</c>（<c>System.Drawing</c> でなく <c>Avalonia.Media.Imaging</c>）に限定せず、<br/>
    /// <see cref="IImage"/> を実装する任意の画像ソースでも正しく計算する。
    /// </remarks>
    private double ComputeRawFitZoomPercent()
    {
        // ホットパス（ズーム可否・実効倍率の参照）で毎回計算し直さないようキャッシュする。
        // キャッシュは Source / ビューポートサイズの変更時に無効化される。
        if (_fitZoomCache is { } cached) return cached;
        var fitZoom = Source is { } img
            ? ZoomMath.FitZoomPercent(
                img.Size.ToEsSize(),
                _zoomableView.Bounds.Size.ToEsSize())
            : 100;
        _fitZoomCache = fitZoom;
        return fitZoom;
    }
}
