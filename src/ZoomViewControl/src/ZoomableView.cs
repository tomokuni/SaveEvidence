using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Estable.AvaloniaUI.Platform.Geometry;
using Estable;

namespace Estable.AvaloniaUI.ZoomView;

/// <summary>
/// パン・ズーム可能な画像表示コントロール。
/// </summary>
/// <remarks>
/// ScrollViewer を使用せず、RenderTransform（ScaleTransform + TranslateTransform）でズーム・パンを実現する。<br/>
/// ScrollViewer のレイアウト処理によるオフセット上書きが原理的に発生しないため、<br/>
/// ズーム中心維持の防御コード（保留オフセット再設定等）が不要になる。<br/>
/// <br/>
/// <b>座標系</b>:<br/>
/// - コンテンツ座標 = 元画像ピクセル（ズーム率に依存しない不変座標）<br/>
/// - ビューポート座標 = 本コントロールのローカル座標（表示座標）<br/>
/// - 描画位置 = スケール × (コンテンツ座標 − オフセット)<br/>
/// オフセットは「ビューポート左上に対応するコンテンツ座標」で、スクロール範囲（コンテンツ − ビューポート / スケール）にクランプされる。<br/>
/// <br/>
/// <b>操作</b>:<br/>
/// - マウスドラッグ: パン（grab 方式。掴んだ位置がポインタに追従し、ドラッグ方向と同じ方向へコンテンツが移動）<br/>
/// - ホイール: 縦パン / Shift+ホイール: 横パン（Ctrl+ホイールはズーム要求として <see cref="ZoomRequested"/> を発火）<br/>
/// <br/>
/// <b>適用パターン</b>: カスタムコントロール（自己完結型ビューポート）。計算は <see cref="ZoomMath"/>（純粋関数）に委譲。
/// </remarks>
internal sealed class ZoomableView : Panel
{
    /// <summary>スクロールバーの太さ（DIP）。</summary>
    private const double ScrollBarThickness = 10;

    /// <summary>ホイールパンの 1 ノッチあたりの移動量（DIP）。</summary>
    private const double WheelScrollUnit = 48.0;

    /// <summary>画像表示コントロール（RenderTransform でズーム・パン）。</summary>
    private readonly Image _image = new()
    {
        Stretch = Stretch.None,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative),
    };

    /// <summary>固定倍率表示用の翻訳変換（描画位置 = スケール × (コンテンツ − オフセット)）。</summary>
    private readonly TranslateTransform _translateTransform = new();

    /// <summary>固定倍率表示用の拡大縮小変換。</summary>
    private readonly ScaleTransform _scaleTransform = new();

    /// <summary>翻訳 → 拡大縮小の合成変換（Children は先頭から適用される）。</summary>
    private readonly TransformGroup _transformGroup = new();

    /// <summary>ズーム率（0 = Fit / 100 = 100%）。</summary>
    private double _zoomPercent;

    /// <summary>パン位置（コンテンツ座標。ビューポート左上に対応）。</summary>
    private VectorD _offset;

    /// <summary>マウスドラッグによるパン中かどうか。</summary>
    private bool _isPanning;

    /// <summary>ドラッグ開始時に掴んだコンテンツ座標（grab 原点。この位置がポインタに追従する）。</summary>
    private PointD _grabContentPos;

    /// <summary>画像がビューポートより小さい場合に中央寄せするか（false で左上寄せ）。</summary>
    private bool _centerAlign = true;

    /// <summary>スクロールバー同期中の再入防止フラグ。</summary>
    private bool _isSyncingScrollBars;

    /// <summary>現在の実効スケール（表示に使用するスケール）。表示状態の変更時に再計算する。</summary>
    private double _effectiveScale = 1;

    /// <summary>実効スケールの再計算が必要かどうか。</summary>
    private bool _effectiveScaleDirty = true;

    /// <summary>縦スクロールバー（オーバーレイ表示）。</summary>
    private readonly ScrollBar _verticalScrollBar = new()
    {
        Orientation = Orientation.Vertical,
        Width = ScrollBarThickness,
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Stretch,
        IsVisible = false,
    };

    /// <summary>横スクロールバー（オーバーレイ表示）。</summary>
    private readonly ScrollBar _horizontalScrollBar = new()
    {
        Orientation = Orientation.Horizontal,
        Height = ScrollBarThickness,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Bottom,
        IsVisible = false,
    };

    /// <summary>画像表示コントロール（内部実装。テスト用アクセサとして公開）。</summary>
    internal Image ImageControl => _image;

    /// <summary>表示する画像（ズーム対象）。設定すると Fit 表示にリセットする。</summary>
    /// <remarks>
    /// 内部 <c>Image</c> の <c>Source</c> を委譲する。<br/>
    /// <c>Image</c> そのもの（<see cref="ImageControl"/>）はテスト専用のアクセサであり、ホストは本プロパティを使用する。
    /// </remarks>
    public IImage? Source
    {
        get => _image.Source;
        set => SetSource(value);
    }

    /// <summary>現在のズーム率（%）。0 = Fit。</summary>
    public double ZoomPercent => _zoomPercent;

    /// <summary>現在のスケール（ズーム率 / 100）。Fit 中は 0。</summary>
    public double Scale => _zoomPercent / 100.0;

    /// <summary>現在の実効スケール（表示に使用するスケール）。Fit 中は FitScale、固定倍率中は <see cref="Scale"/>。</summary>
    /// <remarks>
    /// Fit 表示中（<c>_zoomPercent = 0</c>）は <see cref="Scale"/> が 0 になるため、<br/>
    /// 座標変換（<see cref="ViewportToContent"/>）・パン量計算では実効スケールを使用する（ゼロ除算防止）。
    /// </remarks>
    public double EffectiveScale
    {
        get
        {
            if (_effectiveScaleDirty) RefreshEffectiveScale();
            return _effectiveScale;
        }
    }

    /// <summary>実効スケールを現在の表示状態から再計算する。</summary>
    private void RefreshEffectiveScale()
    {
        _effectiveScaleDirty = false;
        if (_zoomPercent > 0)
        {
            _effectiveScale = Scale;
        }
        else if (_image.Source is { } src)
        {
            _effectiveScale = ZoomMath.FitScale(
                new SizeD(src.Size.Width, src.Size.Height), Bounds.Size.ToSizeD());
        }
        else
        {
            _effectiveScale = 1;
        }
    }

    /// <summary>現在のパン位置（コンテンツ座標）。</summary>
    public VectorD Offset => _offset;

    /// <summary>その軸で画像がビューポートより小さい場合に中央寄せするか（false で左上寄せ）。</summary>
    /// <remarks>
    /// Fit 表示中および固定倍率で、その軸で画像がビューポートより小さい場合に有効（軸ごとに独立して判定）。<br/>
    /// 一方の軸がスクロール可能（画像がビューポートより大きい）でも、他方の軸の中央寄せは維持される。<br/>
    /// スクロール可能な軸はパン制御（オフセット）が優先されるため、本設定はその軸には反映されない。
    /// </remarks>
    public bool CenterAlign
    {
        get => _centerAlign;
        set
        {
            if (_centerAlign == value) return;
            _centerAlign = value;
            // 画像配置（中央/左上）とオーバーレイ座標の両方に影響するため、表示を更新する
            UpdateLayoutAndTransform();
        }
    }

    /// <summary>ズーム要求イベント（Ctrl+ホイール。ズーム方向とマウスポインタ位置を通知）。</summary>
    /// <remarks>ズーム率の決定（遷移ロジック）は上位（ZoomViewControl）が行う。</remarks>
    public event EventHandler<ZoomRequestedEventArgs>? ZoomRequested;

    /// <summary>ビューポート（パン・ズーム・表示状態）が変化したときに発生するイベント。</summary>
    /// <remarks>
    /// パン（<see cref="PanBy"/> / <see cref="SetOffset"/> / スクロールバー）・ズーム（<see cref="SetZoom"/>）の
    /// 表示適用後に発火する。<br/>
    /// 画像プレビュー上のオーバーレイ（Crop の暗転マスク・選択枠・ルーペ等）は、ビューポート座標で描画するため、
    /// 本イベントを受けて再描画する（座標変換ポートは呼び出し毎に最新の表示状態を評価する）。
    /// </remarks>
    public event EventHandler? ViewportChanged;

    /// <summary>縦スクロールバー（テスト用アクセサ）。</summary>
    internal ScrollBar VerticalScrollBar => _verticalScrollBar;

    /// <summary>横スクロールバー（テスト用アクセサ）。</summary>
    internal ScrollBar HorizontalScrollBar => _horizontalScrollBar;

    /// <summary>ZoomableView を初期化する。</summary>
    public ZoomableView()
    {
        Children.Add(_image);
        Children.Add(_verticalScrollBar);
        Children.Add(_horizontalScrollBar);
        ClipToBounds = true;
        Focusable = true;
        // 高品質補間（ズーム時の画質）
        RenderOptions.SetBitmapInterpolationMode(_image, BitmapInterpolationMode.HighQuality);

        _transformGroup.Children.Add(_translateTransform);
        _transformGroup.Children.Add(_scaleTransform);

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerWheelChanged += OnPointerWheelChanged;

        // ビューポートサイズ（Bounds）の変更で実効スケールが変わるため、再計算を予約して表示を更新する
        // （Fit 表示の MaxWidth/MaxHeight は Bounds 依存のため、リサイズ時に再適用が必要）
        SizeChanged += (_, _) =>
        {
            _effectiveScaleDirty = true;
            UpdateLayoutAndTransform();
        };

        // スクロールバーの操作（ドラッグ・矢印・ページ送り）でパンする
        _verticalScrollBar.ValueChanged += OnScrollBarValueChanged;
        _horizontalScrollBar.ValueChanged += OnScrollBarValueChanged;
    }

    /// <summary>コンテンツサイズ（元画像サイズ）を設定する。</summary>
    /// <param name="source">画像ソース。null の場合はコンテンツなし。</param>
    /// <remarks>画像変更時は Fit 表示（ズーム率 0）にリセットする。</remarks>
    public void SetSource(IImage? source)
    {
        _image.Source = source;
        _zoomPercent = 0; // Fit
        _offset = default;
        UpdateLayoutAndTransform();
    }

    /// <summary>ズーム率を設定する（ズーム中心を維持）。</summary>
    /// <param name="zoomPercent">ズーム率（%）。0 = Fit。</param>
    /// <param name="center">ズーム中心（ビューポート座標）。null の場合はビューポート中心。</param>
    /// <remarks>
    /// ズーム率の遷移ロジック（段階・倍率）は上位（ZoomViewControl / ZoomTransition）が決定し、<br/>
    /// 本メソッドはズーム中心維持を含む表示の適用のみを行う。<br/>
    /// Fit（0）の場合は画像をビューポートに収めてオフセットをリセットする。
    /// </remarks>
    public void SetZoom(double zoomPercent, PointD? center = null)
    {
        var oldScale = Scale;
        _zoomPercent = zoomPercent;

        if (zoomPercent <= 0)
        {
            // Fit: オフセットをリセット（ズーム表示は Fit スケールで描画）。ズーム中心は不要
            _offset = default;
        }
        else if (oldScale > 0 && _image.Source is { } src)
        {
            // ズーム中心維持: コンテンツ座標（元画像ピクセル）を不変に保つ
            var bounds = Bounds;
            var zoomCenter = center ?? new PointD(bounds.Width / 2, bounds.Height / 2);
            _offset = ZoomMath.ComputeOffsetAfterScaleChange(
                _offset, zoomCenter, oldScale, Scale,
                src.Size.ToSizeD(), bounds.Size.ToSizeD());
        }

        UpdateLayoutAndTransform();
    }

    /// <summary>パン位置を設定する（コンテンツ座標。クランプされる）。</summary>
    /// <param name="offset">パン位置（コンテンツ座標）。</param>
    public void SetOffset(VectorD offset)
    {
        var clamped = ClampCurrentOffset(offset);
        // クランプ結果が現在値と等しい場合は更新・通知をスキップする
        if (clamped == _offset) return;
        _offset = clamped;
        // パンはオフセット変更のみで画像配置（レイアウト）は不変のため、Transform 更新とスクロールバー同期・再描画のみ行う
        UpdateTransformAndSync();
    }

    /// <summary>ビューポート座標をコンテンツ座標（元画像ピクセル）に変換する。</summary>
    /// <param name="viewportPos">ビューポート座標。</param>
    /// <returns>コンテンツ座標。</returns>
    /// <remarks>スケールは実効スケール（Fit 中は FitScale）を使用する。変換は ZoomMath の純粋関数に委譲する。</remarks>
    public PointD ViewportToContent(PointD viewportPos)
        => ZoomMath.ViewportToContent(viewportPos, _offset, EffectiveScale);

    /// <summary>コンテンツ座標（元画像ピクセル）をビューポート座標に変換する。</summary>
    /// <param name="contentPos">コンテンツ座標。</param>
    /// <returns>ビューポート座標。</returns>
    /// <remarks>スケールは実効スケール（Fit 中は FitScale）を使用する。変換は ZoomMath の純粋関数に委譲する。</remarks>
    public PointD ContentToViewport(PointD contentPos)
        => ZoomMath.ContentToViewport(contentPos, _offset, EffectiveScale);

    /// <summary>ビューポート座標をコンテンツ座標（元画像ピクセル）へ変換する（画像表示矩形の原点基準）。</summary>
    /// <param name="viewportPos">ビューポート座標。</param>
    /// <returns>コンテンツ座標。</returns>
    /// <remarks>
    /// <see cref="ViewportToContent"/>（オフセット基準）とは異なり、<b>画像表示矩形の原点</b>
    /// （<see cref="ImageRectViewport"/>）を基準に変換する（Fit 中央寄せの原点補正を含む）。<br/>
    /// 変換は <see cref="ZoomMath.ViewportToContentImage"/> に委譲する。
    /// </remarks>
    public PointD ViewportToContentImage(PointD viewportPos)
        => ZoomMath.ViewportToContentImage(
            viewportPos, ImageRectViewport().Position, EffectiveScale);

    /// <summary>コンテンツ座標（元画像ピクセル）をビューポート座標へ変換する（画像表示矩形の原点基準）。</summary>
    /// <param name="contentPos">コンテンツ座標。</param>
    /// <returns>ビューポート座標。</returns>
    /// <remarks>
    /// <see cref="ContentToViewport"/>（オフセット基準）とは異なり、<b>画像表示矩形の原点</b>
    /// （<see cref="ImageRectViewport"/>）を基準に変換する（Fit 中央寄せの原点補正を含む）。<br/>
    /// 変換は <see cref="ZoomMath.ContentToViewportImage"/> に委譲する。
    /// </remarks>
    public PointD ContentToViewportImage(PointD contentPos)
        => ZoomMath.ContentToViewportImage(
            contentPos, ImageRectViewport().Position, EffectiveScale);

    /// <summary>画像の表示サイズ（ビューポート座標）を取得する。</summary>
    /// <returns>表示サイズ。画像未設定時は default。</returns>
    public SizeD DisplaySize()
    {
        if (_image.Source is not { } src) return default;
        var size = src.Size;
        var scale = EffectiveScale;
        return new SizeD(size.Width * scale, size.Height * scale);
    }

    /// <summary>画像の表示矩形（ビューポート座標）を取得する。</summary>
    /// <returns>画像の表示矩形。画像未設定時は default。</returns>
    public RectD ImageRectViewport()
    {
        var display = DisplaySize();
        // 画像未設定時（DisplaySize が default）は原点補正を行わず default を返す
        if (display == default) return default;
        var origin = ImageOriginViewport(display);
        return new RectD(origin.X, origin.Y, display.Width, display.Height);
    }

    /// <summary>画像原点のビューポート座標を取得する（表示サイズは呼び出し側で計算済みのものを渡す）。</summary>
    /// <param name="display">画像の表示サイズ（ビューポート座標）。</param>
    /// <returns>画像原点のビューポート座標。</returns>
    private PointD ImageOriginViewport(SizeD display)
    {
        var bounds = Bounds;
        if (_zoomPercent <= 0)
        {
            // Fit: CenterAlign に応じてビューポート中央（画像が小さい場合）または原点
            if (_centerAlign)
            {
                return new PointD(
                    Math.Max(0, (bounds.Width - display.Width) / 2),
                    Math.Max(0, (bounds.Height - display.Height) / 2));
            }
            return new PointD(0, 0);
        }
        var scale = Scale;
        // 固定倍率では軸ごとに独立して扱う:
        // - スクロール可能な軸（表示サイズ > ビューポート）: オフセットによるパン位置（-offset*scale）を維持。
        // - スクロール不能な軸（表示サイズ < ビューポート）: CenterAlign に応じて中央寄せ補正（(ビューポート−表示サイズ)/2）を適用。
        //   どちらの軸もクランプによりオフセットは 0（原点）になるため、パン位置と中央寄せは排他である。
        // このビューポート座標の原点は、RenderTransform の translate（pre-scale・コンテンツ座標）を
        // scale 倍した実効描画位置と一致する（UpdateTransformAndSync の中央寄せ補正）。
        var x = display.Width <= bounds.Width
            ? MaxCentered(display.Width, bounds.Width)
            : -_offset.X * scale;
        var y = display.Height <= bounds.Height
            ? MaxCentered(display.Height, bounds.Height)
            : -_offset.Y * scale;
        return new PointD(x, y);
    }

    /// <summary>中央寄せの原点補正（ビューポート座標）を軸単位で計算する。</summary>
    /// <param name="displaySize">画像の表示サイズ（ビューポート座標）。</param>
    /// <param name="viewportSize">ビューポートのサイズ。</param>
    /// <returns>中央寄せ原点。CenterAlign が false の場合、または表示サイズがビューポートより大きい場合は 0。</returns>
    private double MaxCentered(double displaySize, double viewportSize)
    {
        if (!_centerAlign || displaySize > viewportSize) return 0;
        return (viewportSize - displaySize) / 2;
    }

    /// <summary>パンする（コンテンツ座標の移動量）。</summary>
    /// <param name="delta">コンテンツ座標の移動量。</param>
    public void PanBy(VectorD delta)
        => SetOffset(_offset + delta);

    /// <summary>現在のオフセットをスクロール範囲内にクランプする。</summary>
    /// <param name="offset">クランプするオフセット。</param>
    /// <returns>クランプ後のオフセット。</returns>
    private VectorD ClampCurrentOffset(VectorD offset)
    {
        if (_image.Source is not { } src || _zoomPercent <= 0) return default;
        return ZoomMath.ClampOffset(offset, Scale, src.Size.ToSizeD(), Bounds.Size.ToSizeD());
    }

    /// <summary>表示（レイアウトサイズ・RenderTransform・スクロールバー）を更新する。</summary>
    private void UpdateLayoutAndTransform()
    {
        // ズーム率・画像ソース・ビューポートサイズのいずれかが変わったため、実効スケールを再計算対象にする
        _effectiveScaleDirty = true;

        if (_image.Source is { } src)
        {
            if (_zoomPercent <= 0)
            {
                // Fit: 画像を Uniform で表示（明示サイズなし・RenderTransform なし）
                // CenterAlign に応じて画像自体の配置も切り替える（オーバーレイ座標との一致）
                var bounds = Bounds;
                _image.Stretch = Stretch.Uniform;
                _image.Width = double.NaN;
                _image.Height = double.NaN;
                _image.MaxWidth = bounds.Width;
                _image.MaxHeight = bounds.Height;
                _image.HorizontalAlignment = _centerAlign ? HorizontalAlignment.Center : HorizontalAlignment.Left;
                _image.VerticalAlignment = _centerAlign ? VerticalAlignment.Center : VerticalAlignment.Top;
                _image.RenderTransform = null;
            }
            else
            {
                // 固定倍率: 元サイズのまま RenderTransform でスケール + パン
                // 画像の配置は常に左上とし、パン・中央寄せは RenderTransform の translate で行う。
                // HorizontalAlignment による中央寄せは、RenderTransform が左上（RenderTransformOrigin=(0,0)）を
                // 基準に拡大縮小するためスケール ≠ 1 で中央からずれる。中央寄せは UpdateTransformAndSync が
                // translate（送り画像原点 / スケール）で行い、座標変換（ImageOriginViewport）と描画位置を一致させる。
                var imageSize = src.Size;
                _image.Stretch = Stretch.None;
                _image.Width = imageSize.Width;
                _image.Height = imageSize.Height;
                _image.MaxWidth = double.PositiveInfinity;
                _image.MaxHeight = double.PositiveInfinity;
                _image.HorizontalAlignment = HorizontalAlignment.Left;
                _image.VerticalAlignment = VerticalAlignment.Top;

                // 描画位置 = Scale × (コンテンツ − オフセット)
                _image.RenderTransform = _transformGroup;
            }
        }

        UpdateTransformAndSync();
    }

    /// <summary>RenderTransform の値更新とスクロールバー同期・再描画・ビューポート通知を行う。</summary>
    /// <remarks>
    /// パン（<see cref="SetOffset"/>）と表示変更（<see cref="UpdateLayoutAndTransform"/>）の共通処理。<br/>
    /// パン時は画像配置（レイアウト）が不変のため、Transform 更新とスクロールバー同期・再描画のみを行う。<br/>
    /// 固定倍率中のみ Transform を更新し、Fit 中（RenderTransform = null）はスキップする。<br/>
    /// 画像がビューポートより小さい場合の中央寄せ（<see cref="CenterAlign"/>）は、
    /// レイアウト（HorizontalAlignment）ではなく translate で行う（スケール ≠ 1 でも中央一致）。
    /// </remarks>
    private void UpdateTransformAndSync()
    {
        // 固定倍率中のみ TranslateTransform / ScaleTransform を更新する（Fit 中は RenderTransform = null）
        if (_zoomPercent > 0)
        {
            // 描画位置 = Scale × (コンテンツ − オフセット)
            var scale = Scale;
            _translateTransform.X = -_offset.X;
            _translateTransform.Y = -_offset.Y;
            _scaleTransform.ScaleX = scale;
            _scaleTransform.ScaleY = scale;

            // CenterAlign: 軸ごとに独立して中央寄せ補正を加える。
            // TransformGroup の translate は ScaleTransform の内側（pre-scale）に適用され、描画位置は「scale × translate」になるため、
            // translate はコンテンツ座標系で指定する。中央寄せ補正（ビューポート座標）をコンテンツ座標へ変換するには scale で除する。
            // スクロール可能な軸（表示サイズ > ビューポート）はオフセットによるパン位置を維持し、スクロール不能な軸のみ中央寄せする。
            if (_centerAlign && _image.Source is { } src && _image.Width > 0 && _image.Height > 0)
            {
                var bounds = Bounds;
                var displayWidth = _image.Width * scale;
                var displayHeight = _image.Height * scale;
                if (displayWidth <= bounds.Width)
                {
                    _translateTransform.X += (bounds.Width - displayWidth) / 2 / scale;
                }
                if (displayHeight <= bounds.Height)
                {
                    _translateTransform.Y += (bounds.Height - displayHeight) / 2 / scale;
                }
            }
        }

        // 画像なし時は UpdateScrollBars が両バーを非表示にする（SetSource(null) 対応）。表示・通知は常に実行する
        UpdateScrollBars();
        InvalidateVisual();
        // ビューポート変化を通知する（オーバーレイ等の追従用。座標変換は受け側が最新表示から再評価する）
        RaiseViewportChanged();
    }

    /// <summary>ビューポート変化イベントを発火する。</summary>
    private void RaiseViewportChanged() => ViewportChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>スクロールバーの表示状態・範囲・値を現在の表示状態に同期する。</summary>
    /// <remarks>
    /// 範囲（Maximum / ViewportSize）は <see cref="ScrollMetrics"/> で計算する。<br/>
    /// コンテンツがビューポートより大きい場合のみ表示する（Fit 表示では非表示）。
    /// </remarks>
    private void UpdateScrollBars()
    {
        if (_isSyncingScrollBars) return;
        _isSyncingScrollBars = true;
        try
        {
            if (_image.Source is not { } src)
            {
                _verticalScrollBar.IsVisible = false;
                _horizontalScrollBar.IsVisible = false;
                return;
            }

            // Fit 中は画像がビューポートに収まるためスクロール範囲が必ず 0（スクロールバー非表示）
            if (_zoomPercent <= 0)
            {
                _verticalScrollBar.IsVisible = false;
                _horizontalScrollBar.IsVisible = false;
                return;
            }

            // 表示スケール（固定倍率中は Scale）
            var content = src.Size;
            var scale = EffectiveScale;

            // 縦スクロールバー
            SyncScrollBar(_verticalScrollBar, content.Height, _offset.Y, scale);

            // 横スクロールバー
            SyncScrollBar(_horizontalScrollBar, content.Width, _offset.X, scale);
        }
        finally
        {
            _isSyncingScrollBars = false;
        }
    }

    /// <summary>スクロールバーの範囲・値・表示状態を現在のパン位置に同期する。</summary>
    /// <param name="bar">対象のスクロールバー。</param>
    /// <param name="contentSize">コンテンツサイズ（コンテンツ座標）。</param>
    /// <param name="offset">現在のオフセット（コンテンツ座標）。</param>
    /// <param name="scale">表示スケール。</param>
    private void SyncScrollBar(ScrollBar bar, double contentSize, double offset, double scale)
    {
        var viewportSize = bar == _verticalScrollBar ? Bounds.Height : Bounds.Width;
        // コンテンツ座標での表示範囲（サムサイズ）と最大オフセットを ScrollMetrics へ委譲する（単一所有・DRY）
        var vs = ScrollMetrics.ViewportSize(viewportSize, scale);
        var max = ScrollMetrics.MaxOffset(contentSize, viewportSize, scale);
        bar.Maximum = max;
        bar.ViewportSize = vs;
        bar.Value = Math.Clamp(offset, 0, max);
        bar.IsVisible = max > 0;
    }

    /// <summary>スクロールバーの操作（ユーザーによる Value 変更）でパンする。</summary>
    /// <param name="sender">イベント送信元（ScrollBar）。</param>
    /// <param name="e">値変更イベント。</param>
    /// <remarks>プログラム側の同期（<see cref="UpdateScrollBars"/>）による変更は無視する。</remarks>
    private void OnScrollBarValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isSyncingScrollBars) return;
        // クランプ・表示更新は SetOffset に委譲する（パン操作との共通化）
        if (sender == _verticalScrollBar)
        {
            SetOffset(new VectorD(_offset.X, e.NewValue));
        }
        else
        {
            SetOffset(new VectorD(e.NewValue, _offset.Y));
        }
    }

    // ====================================================================
    // ポインター操作（パン・ズーム要求）
    // ====================================================================

    /// <summary>マウス押下でパンを開始する。</summary>
    /// <remarks>
    /// 掴んだ位置のコンテンツ座標（元画像ピクセル）を保存する。<br/>
    /// この位置が以降のドラッグでポインタに追従する（grab 方式: コンテンツが手に付いて動く）。
    /// </remarks>
    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed) return;
        _isPanning = true;
        _grabContentPos = ViewportToContent(point.Position.ToPointD());
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    /// <summary>マウス移動でパンする。</summary>
    /// <remarks>
    /// 掴んだ位置（コンテンツ座標）が現在のポインタ位置に表示されるようオフセットを再計算する。<br/>
    /// 描画位置 = スケール × (コンテンツ − オフセット) がポインタ位置と一致するため、
    /// <c>オフセット = 掴んだコンテンツ座標 − ポインタ位置 / 実効スケール</c> となる。<br/>
    /// マウスを右下へドラッグするとコンテンツも右下へ移動し（直観一致）、
    /// スクロール範囲端でクランプされる。クランプが解除されると再び追従する。<br/>
    /// ビューポート座標のドラッグ量は実効スケール（Fit 中は FitScale）で除してコンテンツ座標へ変換する。
    /// </remarks>
    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPanning) return;
        var pos = e.GetPosition(this);
        var scale = EffectiveScale;
        // 掴んだ位置（コンテンツ座標）がポインタに追従するようオフセットを設定（クランプは SetOffset 内で行う）
        SetOffset(new VectorD(
            _grabContentPos.X - pos.X / scale,
            _grabContentPos.Y - pos.Y / scale));
        e.Handled = true;
    }

    /// <summary>マウス解放でパンを終了する。</summary>
    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isPanning) return;
        _isPanning = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    /// <summary>ホイールでパンまたはズーム要求を行う。</summary>
    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if ((e.KeyModifiers & KeyModifiers.Control) != 0)
        {
            // Ctrl+ホイール: ズーム要求（ズーム率の決定は上位が行う。マウスポインタ位置をズーム中心に使用）
            var pointerPos = e.GetPosition(this);
            RaiseZoomRequested(e.Delta.Y > 0, pointerPos.ToPointD());
            e.Handled = true;
            return;
        }

        // 通常ホイール: パン（Shift 併用で横スクロール）
        var isShift = (e.KeyModifiers & KeyModifiers.Shift) != 0;
        var delta = e.Delta;
        var dx = isShift ? delta.Y * WheelScrollUnit : delta.X * WheelScrollUnit;
        var dy = isShift ? 0 : delta.Y * WheelScrollUnit;
        var scale = EffectiveScale;
        PanBy(new VectorD(dx / scale, dy / scale));
        e.Handled = true;
    }

    /// <summary>ズーム要求イベントを発火する。</summary>
    /// <param name="zoomIn">true = ズームイン / false = ズームアウト。</param>
    /// <param name="pointerPosition">ズーム中心として使用するマウスポインタ位置（ビューポートローカル座標）。</param>
    private void RaiseZoomRequested(bool zoomIn, PointD pointerPosition)
        => ZoomRequested?.Invoke(this, new ZoomRequestedEventArgs(zoomIn, pointerPosition));
}
