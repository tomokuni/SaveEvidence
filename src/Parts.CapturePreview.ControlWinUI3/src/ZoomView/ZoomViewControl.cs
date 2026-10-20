using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Logging;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.ZoomView;
using EsUtil.SaveEvidence.Platform.WinUI3;
using EsUtil.SaveEvidence.Platform.WinUI3.Drawing;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;
using DrawingColor = System.Drawing.Color;
namespace EsUtil.SaveEvidence.Parts.CapturePreview.WinUI3.ZoomView;

/// <summary>ズーム・パン可能な画像表示コントロール（WinUI 3 実装）。</summary>
/// <remarks>
/// <b>UI 層の責務は 3 つだけ</b>である（判断・値・計算はコアの <see cref="ZoomViewSession"/> が単一所有する）。<br/>
/// 1. <b>入力ブリッジ</b>: ポインターとホイールのイベントをコア型へ変換してセッションへ渡し、返された効果（再描画・ポインター キャプチャ）を適用する。<br/>
/// 2. <b>表示の反映</b>: セッションの値（画像表示矩形・スクロールバー・外観）を Composition と XAML の要素へ写す（外観値・判断を持たない）。<br/>
/// 3. <b>ホスト結線</b>: プロパティ・DPI 追従・ポインター キャプチャ・レイアウト。<br/>
/// <br/>
/// <b>プロパティは CLR プロパティである</b>: WinUI 3 の依存関係プロパティは WinRT へ投影できる型だけを受け付けるため、<br/>
/// コアと描画層の型（<see cref="EsReadOnlyBitmap"/> / <see cref="ZoomViewAppearance"/>）は登録できない。<br/>
/// したがって本コントロールは CLR プロパティで公開し、ホストはコードビハインドから設定する（XAML のバインディング対象にはしない）。<br/>
/// <br/>
/// <b>描画ライブラリを持たない</b>: 画像は <see cref="CompositionImageView"/>（Composition のサーフェス）へ、<br/>
/// スクロールバーは <see cref="RectShapeLayer"/>（XAML の矩形）へ写す。 拡大・縮小とクリップは GPU が行うため、<br/>
/// 画素処理のコードを持たない（移行前の Skia のキャンバスは使わない）。<br/>
/// 見た目（スクロールバーの太さ・余白・下地色）は <see cref="ZoomViewAppearance"/> が単一所有し、ホストが設定する。
/// </remarks>
public sealed partial class ZoomViewControl : Grid
{
    /// <summary>マウス ホイール 1 ノッチあたりの回転量（WinUI 3 は 120 単位で報告する）。</summary>
    /// <remarks>コアの回転量の単位（1 ノッチ = 1.0）へ換算するために使う。</remarks>
    private const double WheelDeltaPerNotch = 120.0;

    /// <summary>ヒットテストを成立させるための透明ブラシ（共有する）。</summary>
    private static readonly SolidColorBrush s_hitTestBrush = new(Microsoft.UI.Colors.Transparent);

    /// <summary>ズームの状態・判断・計算を単一所有するセッション。</summary>
    private readonly ZoomViewSession _session;

    /// <summary>拡大元の画像（<see cref="Source"/> の保持値）。</summary>
    private EsReadOnlyBitmap? _source;

    /// <summary>画像の中央寄せ（<see cref="CenterAlign"/> の保持値）。</summary>
    private bool _centerAlign = true;

    /// <summary>表示 DIP → 物理ピクセルのスケーリング係数（<see cref="ClientToPixelScaling"/> の保持値）。</summary>
    private double _clientToPixelScaling = DpiScaler.StandardScaling;

    /// <summary>ログの接頭辞（未設定は null。 既定値は定義クラスが持つ）。</summary>
    private string? _logPrefix;

    /// <summary>ドラッグの追跡に使うポインター（未押下時は null）。</summary>
    private Pointer? _pressedPointer;

    /// <summary>画像を表示する面（Composition のサーフェス）。</summary>
    private readonly CompositionImageView _image;

    /// <summary>スクロールバーを表す図形の層（XAML の矩形）。</summary>
    private readonly RectShapeLayer _shapes = new() { IsHitTestVisible = false };

    /// <summary>スクロールバーの描画内容（毎回の更新で詰め直して使い回す）。</summary>
    private readonly DrawList _drawList = new();

    /// <summary>最後に画像ビューへ渡した画像（同じ画像を渡し直さないための保持値）。</summary>
    private EsReadOnlyBitmap? _appliedSource;

    /// <summary>最後に下地へ設定した色（同じ色でブラシを作り直さないための保持値）。</summary>
    private DrawingColor? _appliedBackground;

    /// <summary>ズームビューコントロールを既定の設定で初期化する。</summary>
    public ZoomViewControl() : this(ZoomOptions.Default) { }

    /// <summary>ズームビューコントロールを指定の設定で初期化する。</summary>
    /// <param name="config">ズーム遷移の設定。 null の場合は既定値。</param>
    /// <remarks>
    /// 画像は静止画として扱い、ズーム・パンの変更時のみ表示を更新する。<br/>
    /// セッションの通知（ズーム状態・ビューポートの変化）を購読し、自 UI の公開イベントへ転送する。<br/>
    /// 透明のブラシを背景に設定してヒットテストを成立させる（背景を持たない要素はポインター イベントを受け取れない）。<br/>
    /// 子要素の順序は「画像 → スクロールバー」であり、後から加えた要素が前面に合成される。
    /// </remarks>
    public ZoomViewControl(ZoomOptions? config)
    {
        _session = new ZoomViewSession(config);
        _session.Subscribe(OnSessionNotified);

        _image = new CompositionImageView { IsHitTestVisible = false };
        Children.Add(_image);
        Children.Add(_shapes);

        Background = s_hitTestBrush;
        SizeChanged += OnViewSizeChanged;
        Loaded += OnLoaded;
    }

    /// <summary>ズームの状態・判断・計算を単一所有するセッションを取得する（テスト用アクセサ）。</summary>
    /// <value>セッション。 ホストは本プロパティを使わず、コントロールの公開 API を使用する。</value>
    internal ZoomViewSession Session => _session;

    /// <summary>画像の周囲へ付ける影を設定する。</summary>
    /// <param name="blurRadius">ぼかしの半径（DIP）。</param>
    /// <param name="argb">影の色（<c>0xAARRGGBB</c>）。</param>
    /// <remarks>
    /// 移行前は <c>SurroundRenderer</c> フックで 1 画素の帯を数百枚描いて影を近似していた。<br/>
    /// 現在は Composition の <c>DropShadow</c> を使うため、画像の輪郭へ実際のぼかしが付く<br/>
    /// （要素を増やさず、画像の端の混色と影の間に線が出ない）。
    /// </remarks>
    public void SetShadow(double blurRadius, uint argb) => _image.SetShadow(blurRadius, argb);

    /// <summary>画像の周囲の影を外す。</summary>
    public void ClearShadow() => _image.ClearShadow();

    /// <summary>ズーム率が変更されたときに発火する（実効ズーム率・Fit 状態・ズーム可否を通知する）。</summary>
    public event EventHandler<ZoomChangedEventArgs>? ZoomChanged;

    /// <summary>ビューポート（パン・ズーム・表示サイズ）が変化したときに発火する。</summary>
    /// <remarks>
    /// ホストが重ねたオーバーレイ（選択枠・ルーペ等）を追従させるために使用する。<br/>
    /// ズーム率の変更・パン・ビューポートのサイズ変更で発火する。
    /// </remarks>
    public event EventHandler? ViewportChanged;

    /// <summary>ズーム操作ログの出力先を取得または設定する。</summary>
    /// <value>ロガー。 null を設定した場合は出力なしとして扱う。</value>
    /// <remarks>null の吸収（<c>?? NullLogger.Instance</c>）は呼び出し側で行う（規則は .github/ログ規約.md が所有する）。</remarks>
    public ILogger Log
    {
        get => _session.Log;
        set => _session.Log = value;
    }

    /// <summary>ログの接頭辞を取得または設定する（ロガーと一緒に設定する）。</summary>
    /// <value>接頭辞（例: <c>[CapturePreview]</c>）。 null または空白を設定すると既定値へ戻す。</value>
    /// <remarks>本アセンブリのログ定義（<see cref="CapturePreviewLogMessage"/>）の接頭辞を設定する。</remarks>
    public string? LogPrefix
    {
        get => _logPrefix;
        set
        {
            _logPrefix = value;
            CapturePreviewLogMessage.SetPrefix(value);
        }
    }

    /// <summary>表示する画像（ズーム対象）を取得または設定する。</summary>
    /// <value>ズーム対象の画像。 null の場合は何も表示しない。 設定すると Fit 表示へリセットする。</value>
    public EsReadOnlyBitmap? Source
    {
        get => _source;
        set
        {
            if (ReferenceEquals(_source, value))
            {
                return;
            }

            _source = value;
            Apply(_session.SetSource(value is not null, SourceSize));
        }
    }

    /// <summary>画像の中央寄せ / 左上寄せを取得または設定する（画像がビューポートより小さい軸にのみ作用する）。</summary>
    /// <value>true の場合は中央寄せ、false の場合は左上寄せ。</value>
    public bool CenterAlign
    {
        get => _centerAlign;
        set
        {
            if (_centerAlign == value)
            {
                return;
            }

            _centerAlign = value;
            Apply(_session.SetCenterAlign(value));
        }
    }

    /// <summary>表示 DIP を物理ピクセルへ変換するスケーリング係数を取得または設定する。</summary>
    /// <value>スケーリング係数。 DPI 変更時に自動更新される。</value>
    public double ClientToPixelScaling
    {
        get => _clientToPixelScaling;
        set
        {
            if (_clientToPixelScaling == value)
            {
                return;
            }

            _clientToPixelScaling = value;
            Refresh();
        }
    }

    /// <summary>ズームビューの外観（下地・スクロールバー）を取得または設定する。</summary>
    /// <value>外観の単一値。 既定は暗色の下地・スクロールバー表示。</value>
    /// <remarks>
    /// 外観はコア（<see cref="ZoomViewAppearance"/>）が単一所有する。<br/>
    /// 本プロパティはコアへ渡す窓口であり、本コントロールは外観値を保持しない（同値の再設定では再描画しない）。
    /// </remarks>
    public ZoomViewAppearance Appearance
    {
        get => _session.Appearance;
        set => Apply(_session.SetAppearance(value));
    }

    /// <summary>現在の実効ズーム率（%）を取得または設定する。</summary>
    /// <value>Fit 中は実効倍率（ビューポートと元画像サイズから算出）、固定倍率中はズーム率。 0 にはならない。</value>
    /// <remarks>
    /// <b>setter</b> は 0 より大きい値のみ受け付ける（Fit へは <see cref="ZoomFit"/> を使用する）。<br/>
    /// 設定値は <see cref="ZoomOptions"/> の範囲へクランプされ、ズーム中心を維持したまま倍率を変更する。
    /// </remarks>
    public double ZoomPercent
    {
        get => _session.ZoomPercent;
        set => Apply(_session.SetFixedZoom(value));
    }

    /// <summary>現在 Fit 表示中かどうかを取得する。</summary>
    /// <value>Fit 表示中なら true。</value>
    /// <remarks><see cref="ZoomPercent"/> は実効値のため 0 にならない。 Fit 判定には本プロパティを使用する。</remarks>
    public bool IsFit => _session.IsFit;

    /// <summary>ズームイン可能かどうかを取得する。</summary>
    /// <value>ズームイン可能なら true。 画像未設定時は false。</value>
    public bool CanZoomIn => _session.CanZoomIn;

    /// <summary>ズームアウト可能かどうかを取得する。</summary>
    /// <value>ズームアウト可能なら true。 画像未設定時は false。</value>
    public bool CanZoomOut => _session.CanZoomOut;

    /// <summary>画像の表示サイズ（ビューポート座標・DIP）を取得する。</summary>
    /// <returns>表示サイズ。 画像未設定時は default。</returns>
    public EsSize DisplaySize()
    {
        try
        {
            return _session.DisplaySize;
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 公開ファサードとして内部の例外を集約して記録し、呼び出し側へ伝搬する（例外処理規約 §4）
            Log.LogOperationError(ex, nameof(DisplaySize));
            throw;
        }
    }

    /// <summary>画像の表示矩形（ビューポート座標・DIP）を取得する。</summary>
    /// <returns>画像の表示矩形。 画像未設定時は default。</returns>
    public EsRect ImageRectViewport()
    {
        try
        {
            return _session.ImageRect;
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 公開ファサードとして内部の例外を集約して記録し、呼び出し側へ伝搬する（例外処理規約 §4）
            Log.LogOperationError(ex, nameof(ImageRectViewport));
            throw;
        }
    }

    /// <summary>ビューポート座標をコンテンツ座標（元画像ピクセル）へ変換する（画像表示矩形の原点基準）。</summary>
    /// <param name="viewportPos">ビューポート座標（DIP）。</param>
    /// <returns>コンテンツ座標（元画像ピクセル）。</returns>
    /// <remarks>Fit 中央寄せの原点補正を含む。 変換はコアのセッションへ委譲する。</remarks>
    public EsPoint ClientToDisplayedSource(EsPoint viewportPos)
    {
        try
        {
            return _session.ClientToDisplayedSource(viewportPos);
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 公開ファサードとして内部の例外を集約して記録し、呼び出し側へ伝搬する（例外処理規約 §4）
            Log.LogOperationError(ex, nameof(ClientToDisplayedSource));
            throw;
        }
    }

    /// <summary>コンテンツ座標（元画像ピクセル）をビューポート座標へ変換する（画像表示矩形の原点基準）。</summary>
    /// <param name="contentPos">コンテンツ座標（元画像ピクセル）。</param>
    /// <returns>ビューポート座標（DIP）。</returns>
    /// <remarks>Fit 中央寄せの原点補正を含む。 変換はコアのセッションへ委譲する。</remarks>
    public EsPoint DisplayedSourceToClient(EsPoint contentPos)
    {
        try
        {
            return _session.DisplayedSourceToClient(contentPos);
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 公開ファサードとして内部の例外を集約して記録し、呼び出し側へ伝搬する（例外処理規約 §4）
            Log.LogOperationError(ex, nameof(DisplayedSourceToClient));
            throw;
        }
    }

    /// <summary>ホイールの回転量でパンする（回転量から移動量への換算を単一所有する）。</summary>
    /// <param name="delta">ホイールの回転量（X = 横 / Y = 縦。 1 ノッチ = 1.0）。</param>
    /// <param name="shift">Shift 併用かどうか（縦回転を横パンとして扱う）。</param>
    /// <remarks>ホストが他のコントロール（選択オーバーレイ等）で受けたホイールを転送する場合にも使用する。</remarks>
    public void PanByWheel(EsVector delta, bool shift)
    {
        try
        {
            Apply(_session.PanByWheel(delta, shift));
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 公開ファサードとして内部の例外を集約して記録し、呼び出し側へ伝搬する（例外処理規約 §4）
            Log.LogOperationError(ex, nameof(PanByWheel));
            throw;
        }
    }

    /// <summary>Ctrl+ホイールのズームを適用する（ホイール操作の公開 API）。</summary>
    /// <param name="zoomIn">true = ズームイン / false = ズームアウト。</param>
    /// <param name="pointerPosition">ズーム中心とする位置（DIP）。 null の場合はビューポート中心。</param>
    /// <remarks>ホストが他のコントロールで受けたホイールを転送する場合にも使用する。</remarks>
    public void ZoomByWheel(bool zoomIn, EsPoint? pointerPosition = null)
        => Apply(_session.ZoomByWheel(zoomIn, pointerPosition));

    /// <summary>ズームインする（⊖⊕ ボタン。 次のレベルへ移動する）。</summary>
    /// <remarks>移動先がなければ何もしない（現在のズーム率を維持する）。</remarks>
    public void ZoomIn()
    {
        try
        {
            Apply(_session.ZoomIn());
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 公開ファサードとして内部の例外を集約して記録し、呼び出し側へ伝搬する（例外処理規約 §4）
            Log.LogOperationError(ex, nameof(ZoomIn));
            throw;
        }
    }

    /// <summary>ズームアウトする（⊖⊕ ボタン。 前のレベルへ移動する）。</summary>
    /// <remarks>移動先がなければ何もしない（現在のズーム率を維持する）。</remarks>
    public void ZoomOut()
    {
        try
        {
            Apply(_session.ZoomOut());
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 公開ファサードとして内部の例外を集約して記録し、呼び出し側へ伝搬する（例外処理規約 §4）
            Log.LogOperationError(ex, nameof(ZoomOut));
            throw;
        }
    }

    /// <summary>Fit 表示（ビューポートに収まる自動倍率）へ戻す。</summary>
    /// <remarks>Fit へ移行する唯一の経路（<see cref="ZoomPercent"/> の setter は 0 を受け付けない）。</remarks>
    public void ZoomFit()
    {
        try
        {
            Apply(_session.ZoomFit());
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 公開ファサードとして内部の例外を集約して記録し、呼び出し側へ伝搬する（例外処理規約 §4）
            Log.LogOperationError(ex, nameof(ZoomFit));
            throw;
        }
    }

    /// <summary>要求サイズを返す（親の残余を埋める）。</summary>
    /// <param name="availableSize">親から提示された利用可能サイズ（使用しない）。</param>
    /// <returns>常に 0 サイズ（親の残余を埋める）。</returns>
    /// <remarks>ズームビューは親が割り当てた領域全体をビューポートとして使うため、自身の要求サイズを持たない。</remarks>
    protected override Windows.Foundation.Size MeasureOverride(Windows.Foundation.Size availableSize)
        => default;

    /// <summary>左ボタン押下でパンまたはスクロールバーのドラッグを開始する。</summary>
    /// <param name="sender">イベントの送信元（本コントロール）。</param>
    /// <param name="e">ポインターの情報。</param>
    /// <remarks>
    /// スクロールバーのサム・トラック上ならドラッグを開始する（パンより優先する。 判断はコア）。<br/>
    /// それ以外は grab 方式のパンを開始する（掴んだ位置のコンテンツ座標をセッションが保存する）。
    /// </remarks>
    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        _pressedPointer = e.Pointer;

        var position = point.Position.ToEsPoint();
        Apply(_session.TryBeginScrollBarDrag(position)
            ? new ZoomFeedback { ShouldCapturePointer = true }
            : _session.Press(position));
        e.Handled = true;
    }

    /// <summary>ドラッグ中は掴んだ位置がポインターに追従するようパンする（スクロールバーのドラッグも処理する）。</summary>
    /// <param name="sender">イベントの送信元（本コントロール）。</param>
    /// <param name="e">ポインターの情報。</param>
    /// <remarks>ドラッグ中でない場合（単なる移動）は何もしない（セッションが空の効果を返す）。</remarks>
    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var position = e.GetCurrentPoint(this).Position.ToEsPoint();
        var feedback = _session.IsDraggingScrollBar
            ? _session.UpdateScrollBarDrag(position)
            : _session.Move(position);

        if (feedback != default)
        {
            Apply(feedback);
            e.Handled = true;
        }
    }

    /// <summary>左ボタン解放でパンまたはスクロールバーのドラッグを終了する。</summary>
    /// <param name="sender">イベントの送信元（本コントロール）。</param>
    /// <param name="e">ポインターの情報。</param>
    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _pressedPointer = null;

        var feedback = _session.IsDraggingScrollBar ? _session.ReleaseScrollBarDrag() : _session.Release();
        if (feedback != default)
        {
            Apply(feedback);
            e.Handled = true;
        }
    }

    /// <summary>ポインターのキャプチャが失われたときにドラッグを終了する。</summary>
    /// <param name="sender">イベントの送信元（本コントロール）。</param>
    /// <param name="e">ポインターの情報。</param>
    /// <remarks>
    /// ウィンドウが非アクティブ化した場合などに解放が届かないため、キャプチャの喪失でも終了させる<br/>
    /// （ドラッグ中の状態が残ると、次の移動で表示が動き続ける）。
    /// </remarks>
    private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        _pressedPointer = null;
        Apply(_session.IsDraggingScrollBar ? _session.ReleaseScrollBarDrag() : _session.Release());
    }

    /// <summary>ホイールでパンまたはズームする。</summary>
    /// <param name="sender">イベントの送信元（本コントロール）。</param>
    /// <param name="e">ポインターの情報（ホイールの回転量を含む）。</param>
    /// <remarks>
    /// Ctrl+ホイールはポインタ位置をズーム中心としたズーム、それ以外はパン（Shift 併用で横パン）とする（判断はコア）。<br/>
    /// WinUI 3 は回転量を 120 単位で報告するため、コアの単位（1 ノッチ = 1.0）へ換算する。
    /// </remarks>
    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        var delta = point.Properties.MouseWheelDelta / WheelDeltaPerNotch;
        var isHorizontal = point.Properties.IsHorizontalMouseWheel;
        var modifiers = e.KeyModifiers;

        var intent = ZoomWheelIntent.Resolve(
            (modifiers & VirtualKeyModifiers.Control) != 0,
            (modifiers & VirtualKeyModifiers.Shift) != 0,
            delta);
        if (intent.IsZoom)
        {
            ZoomByWheel(intent.ZoomIn, point.Position.ToEsPoint());
        }
        else if (isHorizontal)
        {
            PanByWheel(new EsVector(delta, 0), intent.ScrollHorizontal);
        }
        else
        {
            PanByWheel(new EsVector(0, delta), intent.ScrollHorizontal);
        }

        e.Handled = true;
    }

    /// <summary>セッションの通知を自 UI の公開イベントへ転送する。</summary>
    /// <param name="notice">通知（種別と通知時点の値）。</param>
    /// <remarks>判断を含まない種別 → イベントの対応表とする（通知値はコアが確定済み）。</remarks>
    private void OnSessionNotified(ZoomNotice notice)
    {
        switch (notice.Kind)
        {
            case ZoomNoticeKind.ZoomChanged:
                RaiseZoomChanged(notice.State);
                break;
            case ZoomNoticeKind.ViewportChanged:
                RaiseViewportChanged();
                break;
        }
    }

    /// <summary>ズーム率変更イベントを発火する。</summary>
    /// <param name="state">通知時点のズーム状態。</param>
    /// <remarks>購読の有無を先に判定してから発火する（<c>?.Invoke</c> をインラインで書かない）。</remarks>
    private void RaiseZoomChanged(ZoomState state)
    {
        var handler = ZoomChanged;
        handler?.Invoke(this, new ZoomChangedEventArgs(state));
    }

    /// <summary>ビューポート変更イベントを発火する。</summary>
    private void RaiseViewportChanged()
    {
        var handler = ViewportChanged;
        handler?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>コントロールの読み込み時に透明の背景と入力の配線を確立する。</summary>
    /// <param name="sender">イベントの送信元（本コントロール）。</param>
    /// <param name="e">イベント引数。</param>
    /// <remarks>
    /// 背景を持たない要素はポインター ヒットテストの対象にならないため、読み込みのたびに背景を戻す<br/>
    /// （背景が無い要素はポインター ヒットテストの対象にならず、パン・ズームの入力を受け取れない）。<br/>
    /// 入力の配線は解除してから登録する（再読み込みによる二重購読を防ぐ）。
    /// </remarks>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Background ??= s_hitTestBrush;

        PointerPressed -= OnPointerPressed;
        PointerPressed += OnPointerPressed;
        PointerMoved -= OnPointerMoved;
        PointerMoved += OnPointerMoved;
        PointerReleased -= OnPointerReleased;
        PointerReleased += OnPointerReleased;
        PointerCaptureLost -= OnPointerCaptureLost;
        PointerCaptureLost += OnPointerCaptureLost;
        PointerWheelChanged -= OnPointerWheelChanged;
        PointerWheelChanged += OnPointerWheelChanged;

        Refresh();
    }

    /// <summary>ビューポートのサイズ変更をセッションへ通知する。</summary>
    /// <param name="sender">イベントの送信元（本コントロール）。</param>
    /// <param name="e">サイズ変更の情報。</param>
    /// <remarks>Fit 実効倍率・実効ズーム率はビューポートに依存するため、セッションがキャッシュを破棄して再通知する。</remarks>
    private void OnViewSizeChanged(object sender, SizeChangedEventArgs e)
        => Apply(_session.SetViewport(CurrentViewport()));

    /// <summary>元画像のサイズ（コンテンツ座標）を取得する。</summary>
    /// <returns>元画像のサイズ。 画像未設定時は default。</returns>
    /// <remarks><c>EsReadOnlyBitmap.Size</c> は <c>System.Drawing.Size</c> のため、コア幾何型へ変換する。</remarks>
    private EsSize SourceSize
    {
        get
        {
            if (_source is not { } source)
            {
                return default;
            }

            var size = source.Size;
            return new EsSize(size.Width, size.Height);
        }
    }

    /// <summary>ビューポートのサイズ（DIP）を取得する。</summary>
    /// <returns>ビューポートのサイズ。 未レイアウト時は default。</returns>
    private EsSize CurrentViewport()
    {
        var width = ActualWidth;
        var height = ActualHeight;
        return width > 0 && height > 0 ? new EsSize(width, height) : default;
    }

    /// <summary>セッションが返した効果を適用する。</summary>
    /// <param name="feedback">セッションが返した効果（再描画・ポインター キャプチャ）。</param>
    /// <remarks>適用順（再描画 → キャプチャ／解放）は効果の型が単一所有する（UI 層で判断しない）。</remarks>
    private void Apply(ZoomFeedback feedback)
    {
        feedback.ApplyVisual(Refresh);
        feedback.ApplyPointer(BeginPointerCapture, EndPointerCapture);
    }

    /// <summary>ポインターをキャプチャする（ドラッグ中の追跡）。</summary>
    /// <remarks>
    /// 押下で保持したポインターを捕捉する（コントロール外へ出てもイベントを受け取れるようにする）。<br/>
    /// 押下を伴わない要求（未押下）では何もしない。
    /// </remarks>
    private void BeginPointerCapture()
    {
        if (_pressedPointer is not { } pointer)
        {
            return;
        }

        CapturePointer(pointer);
    }

    /// <summary>ポインターのキャプチャを解放する。</summary>
    private void EndPointerCapture()
    {
        if (_pressedPointer is not { } pointer)
        {
            return;
        }

        ReleasePointerCapture(pointer);
        _pressedPointer = null;
    }

    /// <summary>画像とスクロールバーの表示をセッションの値へ合わせる。</summary>
    /// <remarks>
    /// <b>画像は差し替わったときだけ渡す</b>: 無圧縮 BMP の組み立てには 4K で約 41ms かかるため、<br/>
    /// パンのたびに渡し直すと操作が重くなる（セッションは画像の差し替えを通知しないため、保持値で判定する）。<br/>
    /// <b>スクロールバーは毎回作り直す</b>: 矩形は数十件であり、要素は <see cref="RectShapeLayer"/> が使い回す。<br/>
    /// 表示の順序（下地 → 画像 → スクロールバー）は子要素の順序と背景が単一所有する。
    /// </remarks>
    private void Refresh()
    {
        var appearance = _session.Appearance;
        ApplyBackground(appearance.BackgroundColor);

        if (!ReferenceEquals(_appliedSource, _source))
        {
            _appliedSource = _source;
            _image.Source = _source;
        }

        var area = _session.ImageRect;
        _image.SetBounds(new Windows.Foundation.Rect(area.X, area.Y, area.Width, area.Height));

        _drawList.Clear();
        AddScrollBars(appearance);
        _shapes.Apply(_drawList);
    }

    /// <summary>下地の色を反映する。</summary>
    /// <param name="color">下地の色。</param>
    /// <remarks>同じ色ではブラシを作り直さない（DPI の変更などで更新が続くため）。</remarks>
    private void ApplyBackground(DrawingColor color)
    {
        if (_appliedBackground == color)
        {
            return;
        }

        _appliedBackground = color;
        Background = new SolidColorBrush(Windows.UI.Color.FromArgb(color.A, color.R, color.G, color.B));
    }

    /// <summary>オーバーレイ スクロールバーを描画リストへ追加する。</summary>
    /// <param name="appearance">外観（スクロールバーの太さ・余白・色）。</param>
    /// <remarks>
    /// 矩形の計算はコアが単一所有し、描画順と色は <see cref="ZoomScrollBarShapes"/> が単一所有する<br/>
    /// （本メソッドは両者を結ぶだけである）。
    /// </remarks>
    private void AddScrollBars(ZoomViewAppearance appearance)
        => ZoomScrollBarShapes.AddScrollBars(_drawList, _session.CurrentScrollBars(), appearance);
}