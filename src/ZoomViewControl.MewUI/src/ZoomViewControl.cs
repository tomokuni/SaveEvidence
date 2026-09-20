using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Aprillz.MewUI.Skia.Controls;
using Estable.MewUI.Platform;
using Estable.MewUI.ZoomView.Logging;
using Estable.Platform.Geometry;
using Estable.Platform.Imaging;
using Estable.ZoomView;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;

namespace Estable.MewUI.ZoomView;

/// <summary>パン・ズーム可能な画像表示コントロール（MewUI 版）。</summary>
/// <remarks>
/// Avalonia 版 <c>ZoomViewControl</c>（+ 内部 <c>ZoomableView</c>）と同じ UI 非依存コア（<c>ZoomViewControl.Core</c>）を使い、<br/>
/// 描画層と入力処理のみを MewUI で実装する。<br/>
/// <br/>
/// <b>座標系</b>:<br/>
/// - コンテンツ座標 = 元画像ピクセル（ズーム率に依存しない不変座標）<br/>
/// - ビューポート座標 = 本コントロールのローカル座標（DIP）<br/>
/// - 描画位置 = スケール × (コンテンツ座標 − オフセット)<br/>
/// <br/>
/// <b>操作</b>:<br/>
/// - 左ドラッグ: パン（grab 方式。掴んだ位置がポインタに追従する）<br/>
/// - ホイール: 縦パン / Shift+ホイール: 横パン<br/>
/// - Ctrl+ホイール: ポインタ位置を中心にズーム<br/>
/// <br/>
/// <b>Avalonia 版との違い</b>: 内部の <c>Image</c> + <c>RenderTransform</c> + <c>ScrollBar</c> を持たず、<br/>
/// Skia のキャンバスへ 1 回の <c>DrawBitmap</c> で描画する（中間ビットマップも Transform の合成も不要）。<br/>
/// スクロールバーは未実装（必要になった段階で追加する）。
/// </remarks>
public sealed class ZoomViewControl : SkiaCanvasView
{
    /// <summary>ホイールパンの 1 ノッチあたりの移動量（DIP）。</summary>
    private const double WheelScrollUnit = 48.0;

    /// <summary>描画されていない領域の背景色（暗色）。</summary>
    private static readonly SKColor s_background = new(32, 32, 32);

    /// <summary><see cref="Source"/> プロパティ（ズーム対象の画像）。</summary>
    /// <remarks>設定すると Fit 表示（ズーム率 0・オフセット 0）へリセットする。</remarks>
    public static readonly MewProperty<EsBitmap?> SourceProperty =
        MewProperty<EsBitmap?>.Register<ZoomViewControl>(
            nameof(Source), null, MewPropertyOptions.AffectsRender,
            changed: static (owner, _, _) => owner.OnSourceChanged());

    /// <summary><see cref="CenterAlign"/> プロパティ（画像がビューポートより小さい軸の中央寄せ）。</summary>
    public static readonly MewProperty<bool> CenterAlignProperty =
        MewProperty<bool>.Register<ZoomViewControl>(nameof(CenterAlign), true, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="RenderScaling"/> プロパティ（DIP → 物理ピクセルのスケーリング係数）。</summary>
    public static readonly MewProperty<double> RenderScalingProperty =
        MewProperty<double>.Register<ZoomViewControl>(nameof(RenderScaling), 1.0, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="ShowScrollBars"/> プロパティ（オーバーレイ スクロールバーの表示有無）。</summary>
    public static readonly MewProperty<bool> ShowScrollBarsProperty =
        MewProperty<bool>.Register<ZoomViewControl>(nameof(ShowScrollBars), true, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="ScrollBarThickness"/> プロパティ（スクロールバーの太さ・DIP）。</summary>
    public static readonly MewProperty<double> ScrollBarThicknessProperty =
        MewProperty<double>.Register<ZoomViewControl>(nameof(ScrollBarThickness), 8.0, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="ScrollBarMargin"/> プロパティ（ビューポート端からの余白・DIP）。</summary>
    public static readonly MewProperty<double> ScrollBarMarginProperty =
        MewProperty<double>.Register<ZoomViewControl>(nameof(ScrollBarMargin), 4.0, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="ScrollBarColor"/> プロパティ（サムの色）。</summary>
    public static readonly MewProperty<SKColor> ScrollBarColorProperty =
        MewProperty<SKColor>.Register<ZoomViewControl>(nameof(ScrollBarColor), new SKColor(255, 255, 255, 160), MewPropertyOptions.AffectsRender);

    /// <summary><see cref="ScrollBarTrackColor"/> プロパティ（トラックの色）。</summary>
    public static readonly MewProperty<SKColor> ScrollBarTrackColorProperty =
        MewProperty<SKColor>.Register<ZoomViewControl>(nameof(ScrollBarTrackColor), new SKColor(255, 255, 255, 40), MewPropertyOptions.AffectsRender);

    /// <summary><see cref="CheckerSize"/> プロパティ（市松模様の 1 タイルの一辺・DIP。0 で市松模様を描画しない）。</summary>
    public static readonly MewProperty<int> CheckerSizeProperty =
        MewProperty<int>.Register<ZoomViewControl>(nameof(CheckerSize), 0, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="CheckerColorA"/> プロパティ（市松模様の明色）。</summary>
    public static readonly MewProperty<SKColor> CheckerColorAProperty =
        MewProperty<SKColor>.Register<ZoomViewControl>(nameof(CheckerColorA), new SKColor(255, 255, 255, 255), MewPropertyOptions.AffectsRender);

    /// <summary><see cref="CheckerColorB"/> プロパティ（市松模様の暗色）。</summary>
    public static readonly MewProperty<SKColor> CheckerColorBProperty =
        MewProperty<SKColor>.Register<ZoomViewControl>(nameof(CheckerColorB), new SKColor(255, 204, 204, 204), MewPropertyOptions.AffectsRender);

    /// <summary>ズーム遷移の設定（範囲・倍率・レベル集合）。</summary>
    private readonly ZoomConfig _config;

    /// <summary>内部ズーム率（%）。0 = Fit、0 より大きい値は固定倍率。</summary>
    private double _zoomPercent;

    /// <summary>パン位置（コンテンツ座標。ビューポート左上に対応する）。</summary>
    private EsVector _offset;

    /// <summary>ドラッグによるパン中かどうか。</summary>
    private bool _isPanning;

    /// <summary>ドラッグ開始時に掴んだコンテンツ座標（grab 原点）。</summary>
    private EsPoint _grabContentPos;

    /// <summary>ズーム中心として使用するポインタ位置（ビューポート座標）。null の場合はビューポート中心。</summary>
    private EsPoint? _pointerPosition;

    /// <summary>Fit 実効倍率のキャッシュ（画像・ビューポートサイズの変更で無効化する）。</summary>
    private double? _fitZoomCache;

    /// <summary>ドラッグ中のスクロールバー軸（null = スクロールバーのドラッグなし）。</summary>
    private bool? _scrollBarDragging;

    /// <summary>スクロールバーのドラッグ開始時のポインタ位置（ビューポート座標）。</summary>
    private EsPoint _scrollBarDragStart;

    /// <summary>スクロールバーのドラッグ開始時のサム位置（トラック内の相対位置・DIP）。</summary>
    private double _scrollBarDragStartThumb;

    /// <summary>ズームビューコントロールを既定の設定で初期化する。</summary>
    public ZoomViewControl() : this(ZoomConfig.Default) { }

    /// <summary>ズームビューコントロールを指定の設定で初期化する。</summary>
    /// <param name="config">ズーム遷移の設定。null の場合は既定値。</param>
    /// <remarks>
    /// 画像は静止画として扱い連続再描画を無効化する。ズーム・パンの変更時のみ再描画する。
    /// </remarks>
    public ZoomViewControl(ZoomConfig? config)
    {
        _config = config ?? ZoomConfig.Default;
        ContinuousAnimation = false;
        PaintSurface += OnPaintSurface;
    }

    /// <summary>ズーム操作ログの出力先を取得または設定する。</summary>
    /// <value>ロガー。null を設定した場合は <see cref="NullLogger"/> へフォールバックする（出力なし）。</value>
    /// <remarks>
    /// C#14 の <c>field</c> キーワードでバッキング フィールドを暗黙定義する。<br/>
    /// getter / setter とも null を <see cref="NullLogger"/> へ安全に変換する（呼び出し側で null 判定しない）。<br/>
    /// ズーム操作（⊖⊕ボタン・Ctrl+ホイール）の前後のズーム率を Information で記録する（Avalonia 版と同一の書式）。
    /// </remarks>
    public ILogger Log
    {
        get => field ?? NullLogger.Instance;
        set => field = value ?? NullLogger.Instance;
    }

    /// <summary>ズーム率が変更されたときに発火する（実効ズーム率・Fit 状態・ズーム可否を通知する）。</summary>
    public event EventHandler<ZoomChangedEventArgs>? ZoomChanged;

    /// <summary>ビューポート（パン・ズーム・表示サイズ）が変化したときに発火する。</summary>
    /// <remarks>
    /// ホストが重ねたオーバーレイ（選択枠・ルーペ等）を追従させるために使用する。<br/>
    /// ズーム率の変更・パン・ビューポートのサイズ変更で発火する。
    /// </remarks>
    public event EventHandler? ViewportChanged;

    /// <summary>表示する画像（ズーム対象）を取得または設定する。</summary>
    /// <value>ズーム対象の画像。null の場合は何も表示しない。設定すると Fit 表示へリセットする。</value>
    public EsBitmap? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>画像の中央寄せ / 左上寄せを取得または設定する（画像がビューポートより小さい軸にのみ作用する）。</summary>
    /// <value>true の場合は中央寄せ、false の場合は左上寄せ。</value>
    public bool CenterAlign
    {
        get => GetValue(CenterAlignProperty);
        set => SetValue(CenterAlignProperty, value);
    }

    /// <summary>表示 DIP を物理ピクセルへ変換するスケーリング係数を取得または設定する。</summary>
    /// <value>スケーリング係数。DPI 変更時に自動更新される。</value>
    public double RenderScaling
    {
        get => GetValue(RenderScalingProperty);
        set => SetValue(RenderScalingProperty, value);
    }

    /// <summary>オーバーレイ スクロールバーを表示するかどうかを取得または設定する。</summary>
    /// <value>true（既定）の場合は、画像がビューポートより大きい軸にスクロールバーを表示する。</value>
    /// <remarks>パン・ホイールでも操作できるため、表示はホストの選択とする。</remarks>
    public bool ShowScrollBars
    {
        get => GetValue(ShowScrollBarsProperty);
        set => SetValue(ShowScrollBarsProperty, value);
    }

    /// <summary>スクロールバーの太さを取得または設定する。</summary>
    /// <value>太さ（DIP）。</value>
    public double ScrollBarThickness
    {
        get => GetValue(ScrollBarThicknessProperty);
        set => SetValue(ScrollBarThicknessProperty, value);
    }

    /// <summary>スクロールバーとビューポート端との余白を取得または設定する。</summary>
    /// <value>余白（DIP）。</value>
    public double ScrollBarMargin
    {
        get => GetValue(ScrollBarMarginProperty);
        set => SetValue(ScrollBarMarginProperty, value);
    }

    /// <summary>スクロールバーのサムの色を取得または設定する。</summary>
    /// <value>サムの色（アルファで不透明度を表す）。</value>
    public SKColor ScrollBarColor
    {
        get => GetValue(ScrollBarColorProperty);
        set => SetValue(ScrollBarColorProperty, value);
    }

    /// <summary>スクロールバーのトラックの色を取得または設定する。</summary>
    /// <value>トラックの色（アルファ 0 の場合はトラックを描画しない）。</value>
    public SKColor ScrollBarTrackColor
    {
        get => GetValue(ScrollBarTrackColorProperty);
        set => SetValue(ScrollBarTrackColorProperty, value);
    }

    /// <summary>市松模様（画像の透明部分の背景）の 1 タイルの一辺を取得または設定する。</summary>
    /// <value>1 タイルの一辺（DIP）。0 以下の場合は市松模様を描画せず、<see cref="BackgroundColor"/> で塗る。</value>
    /// <remarks>Avalonia 版の <c>ViewportBackground</c>（市松模様ブラシ）に相当する。</remarks>
    public int CheckerSize
    {
        get => GetValue(CheckerSizeProperty);
        set => SetValue(CheckerSizeProperty, value);
    }

    /// <summary>市松模様の明色を取得または設定する。</summary>
    /// <value>市松模様の色 A（明色）。</value>
    public SKColor CheckerColorA
    {
        get => GetValue(CheckerColorAProperty);
        set => SetValue(CheckerColorAProperty, value);
    }

    /// <summary>市松模様の暗色を取得または設定する。</summary>
    /// <value>市松模様の色 B（暗色）。</value>
    public SKColor CheckerColorB
    {
        get => GetValue(CheckerColorBProperty);
        set => SetValue(CheckerColorBProperty, value);
    }

    /// <summary>市松模様を描画しない場合の背景色を取得または設定する。</summary>
    /// <value>背景色。既定は暗色。</value>
    public SKColor BackgroundColor { get; set; } = s_background;

    /// <summary>現在の実効ズーム率（%）を取得または設定する。</summary>
    /// <value>Fit 中は実効倍率（ビューポートと元画像サイズから算出）、固定倍率中はズーム率。0 にはならない。</value>
    /// <remarks>
    /// <b>setter</b> は 0 より大きい値のみ受け付ける（Fit へは <see cref="ZoomFit"/> を使用する）。<br/>
    /// 設定値は <see cref="ZoomConfig"/> の範囲へクランプされ、ズーム中心を維持したまま倍率を変更する。
    /// </remarks>
    public double ZoomPercent
    {
        get => _zoomPercent <= 0 ? _config.ClampZoom(FitZoomPercent()) : _zoomPercent;
        set
        {
            if (value <= 0)
            {
                return;
            }

            SetZoom(_config.ClampZoom(value), _pointerPosition);
        }
    }

    /// <summary>現在 Fit 表示中かどうかを取得する。</summary>
    /// <value>Fit 表示中なら true。</value>
    /// <remarks><see cref="ZoomPercent"/> は実効値のため 0 にならない。Fit 判定には本プロパティを使用する。</remarks>
    public bool IsFit => _zoomPercent <= 0;

    /// <summary>ズームイン可能かどうかを取得する。</summary>
    /// <value>ズームイン可能なら true。画像未設定時は false。</value>
    public bool CanZoomIn => BuildAvailability().CanZoomIn;

    /// <summary>ズームアウト可能かどうかを取得する。</summary>
    /// <value>ズームアウト可能なら true。画像未設定時は false。</value>
    public bool CanZoomOut => BuildAvailability().CanZoomOut;

    /// <summary>現在のパン位置（コンテンツ座標）を取得する。</summary>
    /// <value>ビューポート左上に対応するコンテンツ座標。</value>
    public EsVector Offset => _offset;

    /// <summary>画像の表示サイズ（ビューポート座標・DIP）を取得する。</summary>
    /// <returns>表示サイズ。画像未設定時は default。</returns>
    public EsSize DisplaySize()
    {
        var rect = ImageRectViewport();
        return new EsSize(rect.Width, rect.Height);
    }

    /// <summary>画像の表示矩形（ビューポート座標・DIP）を取得する。</summary>
    /// <returns>画像の表示矩形。画像未設定時は default。</returns>
    public EsRect ImageRectViewport()
        => ZoomSkiaRenderer.ComputeImageRect(ContentSize, CurrentViewport(), _zoomPercent, _offset, CenterAlign);

    /// <summary>ビューポート座標をコンテンツ座標（元画像ピクセル）へ変換する（オフセット基準）。</summary>
    /// <param name="viewportPos">ビューポート座標（DIP）。</param>
    /// <returns>コンテンツ座標（元画像ピクセル）。</returns>
    /// <remarks>中央寄せの原点補正は含まない（含む場合は <see cref="ViewportToContentImage"/> を使用する）。</remarks>
    public EsPoint ViewportToContent(EsPoint viewportPos)
        => ZoomMath.ViewportToContent(viewportPos, _offset, EffectiveScale);

    /// <summary>ビューポート座標をコンテンツ座標（元画像ピクセル）へ変換する（画像表示矩形の原点基準）。</summary>
    /// <param name="viewportPos">ビューポート座標（DIP）。</param>
    /// <returns>コンテンツ座標（元画像ピクセル）。</returns>
    /// <remarks>Fit 中央寄せの原点補正を含む。変換はコアの純粋関数へ委譲する。</remarks>
    public EsPoint ViewportToContentImage(EsPoint viewportPos)
    {
        var origin = ImageOriginViewport();
        return ZoomMath.ViewportToContentImage(viewportPos, origin, EffectiveScale);
    }

    /// <summary>コンテンツ座標（元画像ピクセル）をビューポート座標へ変換する（画像表示矩形の原点基準）。</summary>
    /// <param name="contentPos">コンテンツ座標（元画像ピクセル）。</param>
    /// <returns>ビューポート座標（DIP）。</returns>
    /// <remarks>Fit 中央寄せの原点補正を含む。変換はコアの純粋関数へ委譲する。</remarks>
    public EsPoint ContentToViewportImage(EsPoint contentPos)
    {
        var origin = ImageOriginViewport();
        return ZoomMath.ContentToViewportImage(contentPos, origin, EffectiveScale);
    }

    /// <summary>画像表示矩形の原点（ビューポート座標）を取得する。</summary>
    /// <returns>画像の表示矩形の左上座標。画像未設定時は原点。</returns>
    /// <remarks>変換の基準点を単一所有する（<c>ViewportToContentImage</c> と <c>ContentToViewportImage</c> が共用）。</remarks>
    private EsPoint ImageOriginViewport()
    {
        var rect = ImageRectViewport();
        return new EsPoint(rect.X, rect.Y);
    }

    /// <summary>ズーム率を設定する（ズーム中心を維持する）。</summary>
    /// <param name="zoomPercent">新しいズーム率（%）。0 以下は Fit。</param>
    /// <param name="center">ズーム中心（ビューポート座標）。null の場合はビューポート中心。</param>
    /// <remarks>
    /// ズーム中心のコンテンツ座標を不変に保つようオフセットを再計算する（コアの純粋関数へ委譲）。<br/>
    /// Fit へ移行する場合はオフセットをリセットする。<br/>
    /// Fit 中からの固定倍率への移行はオフセット 0 から開始する（内部スケールが 0 のため中心維持できない）。
    /// </remarks>
    public void SetZoom(double zoomPercent, EsPoint? center = null)
    {
        var oldScale = _zoomPercent / 100.0;
        _zoomPercent = zoomPercent;

        if (zoomPercent <= 0)
        {
            // Fit: オフセットをリセットする（Fit スケールで描画する。ズーム中心は不要）
            _offset = default;
        }
        else if (oldScale > 0 && Source is not null)
        {
            var viewport = CurrentViewport();
            var zoomCenter = center ?? new EsPoint(viewport.Width / 2, viewport.Height / 2);
            _offset = ZoomMath.ComputeOffsetAfterScaleChange(
                _offset, zoomCenter, oldScale, zoomPercent / 100.0, ContentSize, viewport);
        }

        InvalidateVisual();
        RaiseZoomChanged();
        ViewportChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>パン位置を設定する（コンテンツ座標）。</summary>
    /// <param name="offset">パン位置（コンテンツ座標）。</param>
    /// <remarks>スクロール範囲へクランプされる。クランプ結果が現在値と同じ場合は何もしない。</remarks>
    public void SetOffset(EsVector offset)
    {
        var clamped = ClampOffset(offset);
        if (clamped == _offset)
        {
            return;
        }

        _offset = clamped;
        InvalidateVisual();
        ViewportChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>パンする（コンテンツ座標の移動量）。</summary>
    /// <param name="delta">コンテンツ座標の移動量。</param>
    public void PanBy(EsVector delta) => SetOffset(new EsVector(_offset.X + delta.X, _offset.Y + delta.Y));

    /// <summary>ズームインする（次のレベルへ移動する）。</summary>
    /// <remarks>移動先がなければ何もしない（現在のズーム率を維持する）。</remarks>
    public void ZoomIn() => ApplyZoomStep(
        (current, fitZoom) => ZoomTransition.NextLevel(current, fitZoom, _config), Log.LogZoomIn);

    /// <summary>ズームアウトする（前のレベルへ移動する）。</summary>
    /// <remarks>移動先がなければ何もしない（現在のズーム率を維持する）。</remarks>
    public void ZoomOut() => ApplyZoomStep(
        (current, fitZoom) => ZoomTransition.PrevLevel(current, fitZoom, _config), Log.LogZoomOut);

    /// <summary>Fit 表示（ビューポートに収まる自動倍率）へ戻す。</summary>
    /// <remarks>Fit へ移行する唯一の経路（<see cref="ZoomPercent"/> の setter は 0 を受け付けない）。</remarks>
    public void ZoomFit() => SetZoom(0, null);

    /// <summary>ルーペ要求サイズ（DIP）を返す。</summary>
    /// <param name="availableSize">親から提示された利用可能サイズ（使用しない）。</param>
    /// <returns>常に <c>Size.Empty</c>（親の残余を埋める）。</returns>
    /// <remarks>
    /// ズームビューは親が割り当てた領域全体をビューポートとして使うため、自身の要求サイズを持たない。
    /// </remarks>
    protected override Size MeasureContent(Size availableSize) => Size.Empty;

    /// <summary>DPI の変更時に表示スケーリング係数を追従させる。</summary>
    /// <param name="oldDpi">変更前の DPI。</param>
    /// <param name="newDpi">変更後の DPI。</param>
    /// <remarks>モニター間の移動で DPI が変わっても物理ピクセル基準の見た目を保つ。</remarks>
    protected override void OnDpiChanged(uint oldDpi, uint newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        RenderScaling = newDpi / 96.0;
    }

    /// <summary>ビューポートのサイズ変更時に Fit 実効倍率のキャッシュを無効化する。</summary>
    /// <param name="e">サイズ変更の情報。</param>
    /// <remarks>Fit 中は実効倍率が変わるため、再描画と通知を行う。</remarks>
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        _fitZoomCache = null;

        if (IsFit)
        {
            InvalidateVisual();
            RaiseZoomChanged();
        }

        ViewportChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>左ボタン押下でパンを開始する。</summary>
    /// <param name="e">マウスの情報。</param>
    /// <remarks>掴んだ位置のコンテンツ座標を保存し、以降のドラッグでその位置をポインタへ追従させる。</remarks>
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (!e.LeftButton)
        {
            return;
        }

        var position = e.GetPosition(this).ToEsPoint();

        // スクロールバーのサム上ならドラッグを開始する（パンより優先する）
        if (TryBeginScrollBarDrag(position))
        {
            CaptureMouse(true);
            e.Handled = true;
            return;
        }

        _isPanning = true;
        _grabContentPos = ViewportToContent(position);
        CaptureMouse(true);
        e.Handled = true;
    }

    /// <summary>ドラッグ中は掴んだ位置がポインタに追従するようパンする。</summary>
    /// <param name="e">マウスの情報。</param>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_scrollBarDragging is { } vertical)
        {
            UpdateScrollBarDrag(e.GetPosition(this).ToEsPoint(), vertical);
            e.Handled = true;
            return;
        }

        if (!_isPanning)
        {
            return;
        }

        var position = e.GetPosition(this).ToEsPoint();
        var scale = EffectiveScale;

        // 掴んだ位置（コンテンツ座標）がポインタ位置に表示されるオフセットを設定する（クランプは SetOffset 内）
        SetOffset(new EsVector(_grabContentPos.X - position.X / scale, _grabContentPos.Y - position.Y / scale));
        e.Handled = true;
    }

    /// <summary>ボタン解放でパンを終了する。</summary>
    /// <param name="e">マウスの情報。</param>
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (_scrollBarDragging is not null)
        {
            _scrollBarDragging = null;
            CaptureMouse(false);
            e.Handled = true;
            return;
        }

        if (!_isPanning)
        {
            return;
        }

        _isPanning = false;
        CaptureMouse(false);
        e.Handled = true;
    }

    /// <summary>指定位置がスクロールバーのサム上ならドラッグを開始する。</summary>
    /// <param name="position">判定するビューポート座標（DIP）。</param>
    /// <returns>ドラッグを開始した場合は true。</returns>
    /// <remarks>
    /// 水平・垂直の順に判定し、先にヒットした軸のドラッグを開始する。<br/>
    /// トラック上（サム以外）を押した場合はサムをその位置へ移動する（ページジャンプではなく直接移動）。
    /// </remarks>
    private bool TryBeginScrollBarDrag(EsPoint position)
    {
        if (!ShowScrollBars)
        {
            return false;
        }

        var viewport = CurrentViewport();
        var (horizontal, vertical) = CurrentScrollBars(viewport);

        foreach (var (bar, isVertical) in new[] { (vertical, true), (horizontal, false) })
        {
            if (bar is not { } value || !value.Track.Contains(position))
            {
                continue;
            }

            _scrollBarDragging = isVertical;
            _scrollBarDragStart = position;
            _scrollBarDragStartThumb = isVertical
                ? value.Thumb.Y - value.Track.Y
                : value.Thumb.X - value.Track.X;

            // トラック上（サム以外）を押した場合は、サムをその位置へ移動する
            if (!value.Thumb.Contains(position))
            {
                var delta = isVertical ? position.Y - value.Thumb.Y : position.X - value.Thumb.X;
                _scrollBarDragStartThumb = Math.Clamp(_scrollBarDragStartThumb + delta, 0, TrackMovable(value, isVertical));
                ApplyScrollBarThumb(_scrollBarDragStartThumb, isVertical);
            }

            return true;
        }

        return false;
    }

    /// <summary>スクロールバーのドラッグを反映する。</summary>
    /// <param name="position">現在のポインタ位置（ビューポート座標・DIP）。</param>
    /// <param name="vertical">垂直軸の場合は true。</param>
    private void UpdateScrollBarDrag(EsPoint position, bool vertical)
    {
        var viewport = CurrentViewport();
        var bar = vertical ? CurrentScrollBars(viewport).Vertical : CurrentScrollBars(viewport).Horizontal;

        if (bar is not { } value)
        {
            return;
        }

        var movable = TrackMovable(value, vertical);
        var delta = vertical ? position.Y - _scrollBarDragStart.Y : position.X - _scrollBarDragStart.X;
        ApplyScrollBarThumb(Math.Clamp(_scrollBarDragStartThumb + delta, 0, movable), vertical);
    }

    /// <summary>サムの位置からスクロールオフセットを算出して適用する。</summary>
    /// <param name="thumbOffset">サムの左上位置（トラック内の相対位置・DIP）。</param>
    /// <param name="vertical">垂直軸の場合は true。</param>
    private void ApplyScrollBarThumb(double thumbOffset, bool vertical)
    {
        var contentSize = ContentSize;
        var viewport = CurrentViewport();

        if (vertical)
        {
            var maxOffset = ZoomScrollBarGeometry.MaxContentOffset(contentSize.Height, viewport.Height, EffectiveScale);
            var trackLength = Math.Max(0, viewport.Height - ScrollBarMargin * 2);
            var thumbLength = CurrentScrollBars(viewport).Vertical?.Thumb.Height ?? 0;
            var offset = ZoomScrollBarGeometry.ContentOffsetFromThumb(thumbOffset, trackLength, thumbLength, maxOffset);
            SetOffset(new EsVector(_offset.X, offset));
        }
        else
        {
            var maxOffset = ZoomScrollBarGeometry.MaxContentOffset(contentSize.Width, viewport.Width, EffectiveScale);
            var trackLength = Math.Max(0, viewport.Width - ScrollBarMargin * 2);
            var thumbLength = CurrentScrollBars(viewport).Horizontal?.Thumb.Width ?? 0;
            var offset = ZoomScrollBarGeometry.ContentOffsetFromThumb(thumbOffset, trackLength, thumbLength, maxOffset);
            SetOffset(new EsVector(offset, _offset.Y));
        }
    }

    /// <summary>サムの可動範囲（トラック長 − サム長）を取得する。</summary>
    /// <param name="bar">対象のスクロールバー。</param>
    /// <param name="vertical">垂直軸の場合は true。</param>
    /// <returns>サムが移動できる長さ（DIP）。0 未満にはならない。</returns>
    private static double TrackMovable(ZoomScrollBar bar, bool vertical)
        => vertical
            ? Math.Max(0, bar.Track.Height - bar.Thumb.Height)
            : Math.Max(0, bar.Track.Width - bar.Thumb.Width);

    /// <summary>ホイールでパンまたはズームする。</summary>
    /// <param name="e">ホイールの情報。</param>
    /// <remarks>
    /// Ctrl+ホイールはポインタ位置をズーム中心としたズーム、それ以外はパン（Shift 併用で横パン）とする。
    /// </remarks>
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);

        var delta = e.Delta.Y;

        if (e.ControlKey)
        {
            // Ctrl+ホイール: ポインタ位置をズーム中心にしてズームする
            _pointerPosition = e.GetPosition(this).ToEsPoint();
            try
            {
                ZoomByWheel(delta > 0);
            }
            finally
            {
                _pointerPosition = null;
            }

            e.Handled = true;
            return;
        }

        var scale = EffectiveScale;
        var dx = e.ShiftKey ? delta * WheelScrollUnit : 0;
        var dy = e.ShiftKey ? 0 : delta * WheelScrollUnit;
        PanBy(new EsVector(dx / scale, dy / scale));
        e.Handled = true;
    }

    /// <summary>ズーム率を現在の表示状態から変更する（ズーム操作の共通処理）。</summary>
    /// <param name="transition">現在のズーム率と Fit 実効倍率から遷移先を求める関数。</param>
    /// <param name="log">変更前後のズーム率（%）を記録するログ メソッド。</param>
    /// <remarks>遷移先が null（移動不可）の場合は何もしない（ログも出力しない）。</remarks>
    private void ApplyZoomStep(Func<double, double, double?> transition, Action<int, int> log)
    {
        var zoom = _zoomPercent;

        // 遷移関数は Fit 中（current <= 0）のみ fitZoom を使うため、固定倍率中は計算しない
        var fitZoom = zoom <= 0 ? FitZoomPercent() : 0;

        if (transition(zoom, fitZoom) is not { } target)
        {
            return;
        }

        log((int)Math.Round(ZoomPercent), (int)Math.Round(target));
        SetZoom(_config.ClampZoom(target), _pointerPosition);
    }

    /// <summary>Ctrl+ホイールのズームを適用する。</summary>
    /// <param name="zoomIn">true = ズームイン / false = ズームアウト。</param>
    /// <remarks>遷移先の計算とログは <see cref="ApplyZoomStep"/> へ委譲する（ログの種別のみここで選ぶ）。</remarks>
    private void ZoomByWheel(bool zoomIn)
    {
        if (zoomIn)
        {
            ApplyZoomStep((current, fitZoom) => ZoomTransition.WheelZoomIn(current, fitZoom, _config), Log.LogZoomInWheel);
        }
        else
        {
            ApplyZoomStep((current, fitZoom) => ZoomTransition.WheelZoomOut(current, fitZoom, _config), Log.LogZoomOutWheel);
        }
    }

    /// <summary>現在のオフセットをスクロール範囲内へクランプする。</summary>
    /// <param name="offset">クランプするオフセット（コンテンツ座標）。</param>
    /// <returns>クランプ後のオフセット。Fit 中または画像未設定時は原点。</returns>
    private EsVector ClampOffset(EsVector offset)
    {
        if (Source is null || _zoomPercent <= 0)
        {
            return default;
        }

        return ZoomMath.ClampOffset(offset, _zoomPercent / 100.0, ContentSize, CurrentViewport());
    }

    /// <summary>ズーム可否（段階遷移仕様に基づく）を計算する。</summary>
    /// <returns>ズームイン・ズームアウトの可否。</returns>
    /// <remarks>画像未設定時は何れも不可。計算はコアの純粋関数へ委譲する。</remarks>
    private (bool CanZoomIn, bool CanZoomOut) BuildAvailability()
        => ZoomTransition.BuildAvailability(_zoomPercent, FitZoomPercent(), _config, Source is not null);

    /// <summary>Fit 表示時の実表示倍率（%）を計算する（キャッシュ付き）。</summary>
    /// <returns>実表示倍率（%）。ビューポートと元画像サイズから算出、未確定時は 100。</returns>
    /// <remarks>
    /// ホットパス（ズーム可否・実効倍率の参照）で毎回計算しないようキャッシュする。<br/>
    /// キャッシュは画像の変更とビューポートのサイズ変更で無効化される。
    /// </remarks>
    private double FitZoomPercent()
    {
        if (_fitZoomCache is { } cached)
        {
            return cached;
        }

        var viewport = CurrentViewport();
        var value = Source is not null && viewport.Width > 0 && viewport.Height > 0
            ? ZoomMath.FitZoomPercent(ContentSize, viewport)
            : 100.0;

        _fitZoomCache = value;
        return value;
    }

    /// <summary>ズーム率変更を購読者へ通知する。</summary>
    private void RaiseZoomChanged()
    {
        var (canZoomIn, canZoomOut) = BuildAvailability();
        ZoomChanged?.Invoke(this, new ZoomChangedEventArgs(ZoomPercent, IsFit, canZoomIn, canZoomOut));
    }

    /// <summary>画像が変更されたときの処理（Fit 表示へリセットする）。</summary>
    private void OnSourceChanged()
    {
        _zoomPercent = 0;
        _offset = default;
        _fitZoomCache = null;
        InvalidateVisual();
        RaiseZoomChanged();
        ViewportChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>ズーム中心を維持するための実効スケールを計算する。</summary>
    /// <returns>実効スケール（Fit 中は FitScale）。</returns>
    private double EffectiveScale
        => ZoomMath.ComputeEffectiveScale(_zoomPercent, ContentSize, CurrentViewport());

    /// <summary>元画像のサイズ（コンテンツ座標）を取得する。</summary>
    /// <returns>元画像のサイズ。画像未設定時は default。</returns>
    /// <remarks><c>EsBitmap.Size</c> は <c>System.Drawing.Size</c> のため、コア幾何型へ変換する。</remarks>
    private EsSize ContentSize
    {
        get
        {
            if (Source is not { } source)
            {
                return default;
            }

            var size = source.Size;
            return new EsSize(size.Width, size.Height);
        }
    }

    /// <summary>ビューポートのサイズ（DIP）を取得する。</summary>
    /// <returns>ビューポートのサイズ。未レイアウト時は default。</returns>
    private EsSize CurrentViewport()
    {
        var bounds = Bounds;
        return bounds.Width > 0 && bounds.Height > 0 ? new EsSize(bounds.Width, bounds.Height) : default;
    }

    /// <summary>マウスキャプチャを取得または解放する。</summary>
    /// <param name="capture">true = 取得 / false = 解放。</param>
    /// <remarks>
    /// ドラッグ中にポインタがコントロール外へ出てもイベントを受け取れるようにする。<br/>
    /// ウィンドウを特定できない場合（未表示）は何もしない（パンはコントロール内でのみ成立する）。
    /// </remarks>
    private void CaptureMouse(bool capture)
    {
        if (FindVisualRoot() is not Window window)
        {
            return;
        }

        if (capture)
        {
            window.CaptureMouse(this);
        }
        else
        {
            window.ReleaseMouseCapture();
        }
    }

    /// <summary>画像を描画する。</summary>
    /// <param name="canvas">描画先のキャンバス（物理ピクセル）。</param>
    /// <param name="info">描画先の情報（幅・高さ）。</param>
    /// <remarks>
    /// キャンバスを DPI スケーリングしてから DIP 座標で描画する（コアの計算と座標系を一致させる）。<br/>
    /// 背景は描画されていない領域として全面を暗色で塗る。
    /// </remarks>
    private void OnPaintSurface(SKCanvas canvas, SKImageInfo info)
    {
        canvas.Clear(BackgroundColor);

        var scaling = RenderScaling > 0 ? RenderScaling : 1.0;

        // レイアウト前は Bounds が 0 のため、キャンバスサイズからビューポートを求める
        var viewport = CurrentViewport();
        if (viewport.Width <= 0 || viewport.Height <= 0)
        {
            viewport = new EsSize(info.Width / scaling, info.Height / scaling);
        }

        canvas.Save();
        canvas.Scale((float)scaling);
        try
        {
            DrawCheckerboard(canvas, viewport);
            ZoomSkiaRenderer.Render(canvas, Source, ContentSize, viewport, _zoomPercent, _offset, CenterAlign);
            DrawScrollBars(canvas, viewport);
        }
        finally
        {
            canvas.Restore();
        }
    }

    /// <summary>市松模様（画像の透明部分の背景）を描画する。</summary>
    /// <param name="canvas">描画先のキャンバス（DIP 座標で描画できる状態であること）。</param>
    /// <param name="viewport">ビューポートのサイズ（DIP）。</param>
    /// <remarks>
    /// <see cref="CheckerSize"/> が 0 以下の場合は何も描画しない（<see cref="BackgroundColor"/> の単色背景になる）。<br/>
    /// 描画の重ね順は Avalonia 版のプレビュー（タイルブラシ）に合わせる。すなわち<b>暗色（B）で全面を塗った後</b>、<br/>
    /// 明色（A）のタイルを市松位置（<c>(col + row)</c> が偶数）へ重ねる（明色タイルは暗色の上に乗る）。<br/>
    /// 市松模様の色は半透明のため、下地（<see cref="BackgroundColor"/>）と合成されて見える。<br/>
    /// タイルの配置はコアの明暗判定（<c>LoupeCheckerboard</c> と同じ規則）に合わせる。
    /// </remarks>
    private void DrawCheckerboard(SKCanvas canvas, EsSize viewport)
    {
        var tile = CheckerSize;
        if (tile <= 0 || viewport.Width <= 0 || viewport.Height <= 0)
        {
            return;
        }

        using var light = new SKPaint { IsAntialias = false, Color = CheckerColorA };
        using var dark = new SKPaint { IsAntialias = false, Color = CheckerColorB };

        // 暗色で全面を塗る（明色タイルの下地。Avalonia 版のタイルブラシと同じ重ね順）
        canvas.DrawRect(new SKRect(0, 0, (float)viewport.Width, (float)viewport.Height), dark);

        var columns = (int)Math.Ceiling(viewport.Width / tile);
        var rows = (int)Math.Ceiling(viewport.Height / tile);

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                if ((row + column) % 2 == 0)
                {
                    canvas.DrawRect(
                        new SKRect(column * tile, row * tile, (column + 1) * tile, (row + 1) * tile), light);
                }
            }
        }
    }

    /// <summary>オーバーレイ スクロールバーを描画する。</summary>
    /// <param name="canvas">描画先のキャンバス（DIP 座標で描画できる状態であること）。</param>
    /// <param name="viewport">ビューポートのサイズ（DIP）。</param>
    /// <remarks>
    /// スクロール不要な軸（画像がビューポートに収まる軸）には描画しない。<br/>
    /// トラックはアルファ 0 の場合に描画しない（サムのみの表示を要求するホスト向け）。
    /// </remarks>
    private void DrawScrollBars(SKCanvas canvas, EsSize viewport)
    {
        if (!ShowScrollBars)
        {
            return;
        }

        var (horizontal, vertical) = CurrentScrollBars(viewport);

        using var trackPaint = new SKPaint { IsAntialias = false, Style = SKPaintStyle.Fill, Color = ScrollBarTrackColor };
        using var thumbPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = ScrollBarColor };

        DrawScrollBar(canvas, horizontal, trackPaint, thumbPaint);
        DrawScrollBar(canvas, vertical, trackPaint, thumbPaint);
    }

    /// <summary>スクロールバー 1 本を描画する。</summary>
    /// <param name="canvas">描画先のキャンバス。</param>
    /// <param name="bar">描画するスクロールバー。null の場合は何もしない。</param>
    /// <param name="trackPaint">トラックの描画設定。</param>
    /// <param name="thumbPaint">サムの描画設定。</param>
    private static void DrawScrollBar(SKCanvas canvas, ZoomScrollBar? bar, SKPaint trackPaint, SKPaint thumbPaint)
    {
        if (bar is not { } value)
        {
            return;
        }

        if (trackPaint.Color.Alpha > 0)
        {
            canvas.DrawRect(ToSKRect(value.Track), trackPaint);
        }

        canvas.DrawRect(ToSKRect(value.Thumb), thumbPaint);
    }

    /// <summary>現在のスクロールバーの配置を計算する。</summary>
    /// <param name="viewport">ビューポートのサイズ（DIP）。</param>
    /// <returns>水平・垂直のスクロールバー。スクロール不要な軸は null。</returns>
    /// <remarks>コア（<see cref="ZoomScrollBarGeometry"/>）へ委譲し、描画と入力で同じ配置を共用する。</remarks>
    private (ZoomScrollBar? Horizontal, ZoomScrollBar? Vertical) CurrentScrollBars(EsSize viewport)
        => ZoomScrollBarGeometry.Compute(
            ContentSize, viewport, EffectiveScale, _offset, ScrollBarThickness, ScrollBarMargin);

    /// <summary>ビューポート座標を Skia の矩形へ変換する。</summary>
    /// <param name="rect">変換元の矩形（DIP）。</param>
    /// <returns>Skia の矩形。</returns>
    private static SKRect ToSKRect(EsRect rect)
        => new((float)rect.X, (float)rect.Y, (float)rect.Right, (float)rect.Bottom);
}
