using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using app.Views.Components;

namespace app.Controls;

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
public sealed class ZoomableView : Panel
{
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
    private Vector _offset;

    /// <summary>マウスドラッグによるパン中かどうか。</summary>
    private bool _isPanning;

    /// <summary>ドラッグ開始時に掴んだコンテンツ座標（grab 原点。この位置がポインタに追従する）。</summary>
    private Point _grabContentPos;

    /// <summary>画像がビューポートより小さい場合に中央寄せするか（false で左上寄せ）。</summary>
    private bool _centerAlign = true;

    /// <summary>スクロールバー同期中の再入防止フラグ。</summary>
    private bool _isSyncingScrollBars;

    /// <summary>縦スクロールバー（オーバーレイ表示）。</summary>
    private readonly ScrollBar _verticalScrollBar = new()
    {
        Orientation = Orientation.Vertical,
        Width = 10,
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Stretch,
        IsVisible = false,
    };

    /// <summary>横スクロールバー（オーバーレイ表示）。</summary>
    private readonly ScrollBar _horizontalScrollBar = new()
    {
        Orientation = Orientation.Horizontal,
        Height = 10,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Bottom,
        IsVisible = false,
    };

    /// <summary>画像表示コントロール（AccessKey 等の外部参照用）。</summary>
    public Image ImageControl => _image;

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
            if (_zoomPercent > 0) return Scale;
            if (_image.Source is not { } src) return 1;
            return FitScale(new Size(src.Size.Width, src.Size.Height));
        }
    }

    /// <summary>現在のパン位置（コンテンツ座標）。</summary>
    public Vector Offset => _offset;

    /// <summary>画像がビューポートより小さい場合に中央寄せするか（false で左上寄せ）。</summary>
    public bool CenterAlign
    {
        get => _centerAlign;
        set
        {
            if (_centerAlign == value) return;
            _centerAlign = value;
            InvalidateVisual();
        }
    }

    /// <summary>ズーム要求イベント（Ctrl+ホイール。ズーム方向とマウスポインタ位置を通知）。</summary>
    /// <remarks>ズーム率の決定（遷移ロジック）は上位（ZoomManager）が行う。</remarks>
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
    /// ズーム率の遷移ロジック（段階・倍率）は上位（ZoomManager / ZoomTransition）が決定し、<br/>
    /// 本メソッドはズーム中心維持を含む表示の適用のみを行う。<br/>
    /// Fit（0）の場合は画像をビューポートに収めてオフセットをリセットする。
    /// </remarks>
    public void SetZoom(double zoomPercent, Point? center = null)
    {
        var oldScale = Scale;
        zoomPercent = Math.Clamp(zoomPercent, 0, 500);
        _zoomPercent = zoomPercent;
        center ??= new Point(Bounds.Width / 2, Bounds.Height / 2);

        if (zoomPercent <= 0)
        {
            // Fit: オフセットをリセット（ズーム表示は Fit スケールで描画）
            _offset = default;
        }
        else if (oldScale > 0 && _image.Source is not null)
        {
            // ズーム中心維持: コンテンツ座標（元画像ピクセル）を不変に保つ
            _offset = ZoomMath.ComputeOffsetAfterScaleChange(
                _offset, center.Value, oldScale, Scale,
                new Size(_image.Source.Size.Width, _image.Source.Size.Height), Bounds.Size);
        }

        UpdateLayoutAndTransform();
    }

    /// <summary>パン位置を設定する（コンテンツ座標。クランプされる）。</summary>
    /// <param name="offset">パン位置（コンテンツ座標）。</param>
    public void SetOffset(Vector offset)
    {
        _offset = ClampCurrentOffset(offset);
        UpdateLayoutAndTransform();
    }

    /// <summary>ビューポート座標をコンテンツ座標（元画像ピクセル）に変換する。</summary>
    /// <param name="viewportPos">ビューポート座標。</param>
    /// <returns>コンテンツ座標。</returns>
    /// <remarks>スケールは実効スケール（Fit 中は FitScale）を使用する。変換は ZoomMath の純粋関数に委譲する。</remarks>
    public Point ViewportToContent(Point viewportPos)
        => ZoomMath.ViewportToContent(viewportPos, _offset, EffectiveScale);

    /// <summary>コンテンツ座標（元画像ピクセル）をビューポート座標に変換する。</summary>
    /// <param name="contentPos">コンテンツ座標。</param>
    /// <returns>ビューポート座標。</returns>
    /// <remarks>スケールは実効スケール（Fit 中は FitScale）を使用する。変換は ZoomMath の純粋関数に委譲する。</remarks>
    public Point ContentToViewport(Point contentPos)
        => ZoomMath.ContentToViewport(contentPos, _offset, EffectiveScale);

    /// <summary>画像の表示サイズ（ビューポート座標）を取得する。</summary>
    /// <returns>表示サイズ。画像未設定時は default。</returns>
    public Size DisplaySize()
    {
        if (_image.Source is not { } src) return default;
        var content = new Size(src.Size.Width, src.Size.Height);
        if (_zoomPercent <= 0)
        {
            // Fit: ビューポートに収まるスケール（Uniform）
            var scale = FitScale(content);
            return new Size(content.Width * scale, content.Height * scale);
        }
        return new Size(content.Width * Scale, content.Height * Scale);
    }

    /// <summary>画像の表示矩形（ビューポート座標）を取得する。</summary>
    /// <returns>画像の表示矩形。画像未設定時は default。</returns>
    public Rect ImageRectViewport()
    {
        var display = DisplaySize();
        var origin = ImageOriginViewport(display);
        return new Rect(origin, display);
    }

    /// <summary>画像原点のビューポート座標を取得する。</summary>
    /// <returns>画像原点のビューポート座標。</returns>
    public Point ImageOriginViewport() => ImageOriginViewport(DisplaySize());

    /// <summary>画像原点のビューポート座標を取得する（表示サイズは呼び出し側で計算済みのものを渡す）。</summary>
    /// <param name="display">画像の表示サイズ（ビューポート座標）。</param>
    /// <returns>画像原点のビューポート座標。</returns>
    private Point ImageOriginViewport(Size display)
    {
        if (_zoomPercent <= 0)
        {
            // Fit: CenterAlign に応じてビューポート中央（画像が小さい場合）または原点
            if (_centerAlign)
            {
                return new Point(
                    Math.Max(0, (Bounds.Width - display.Width) / 2),
                    Math.Max(0, (Bounds.Height - display.Height) / 2));
            }
            return new Point(0, 0);
        }
        return new Point(-_offset.X * Scale, -_offset.Y * Scale);
    }

    /// <summary>パンする（コンテンツ座標の移動量）。</summary>
    /// <param name="delta">コンテンツ座標の移動量。</param>
    public void PanBy(Vector delta)
    {
        _offset = ClampCurrentOffset(new Vector(_offset.X + delta.X, _offset.Y + delta.Y));
        UpdateLayoutAndTransform();
    }

    /// <summary>現在のオフセットをスクロール範囲内にクランプする。</summary>
    /// <param name="offset">クランプするオフセット。</param>
    /// <returns>クランプ後のオフセット。</returns>
    private Vector ClampCurrentOffset(Vector offset)
    {
        if (_image.Source is not { } src || _zoomPercent <= 0) return default;
        return ZoomMath.ClampOffset(offset, Scale,
            new Size(src.Size.Width, src.Size.Height), Bounds.Size);
    }

    /// <summary>Fit 時のスケール（Uniform）を計算する。</summary>
    /// <param name="content">コンテンツサイズ。</param>
    /// <returns>Fit スケール。</returns>
    private double FitScale(Size content)
    {
        if (content.Width <= 0 || content.Height <= 0) return 1;
        var sx = Bounds.Width / content.Width;
        var sy = Bounds.Height / content.Height;
        var s = Math.Min(sx, sy);
        return s > 0 ? s : 1;
    }

    /// <summary>表示（レイアウトサイズ・RenderTransform・スクロールバー）を更新する。</summary>
    private void UpdateLayoutAndTransform()
    {
        if (_image.Source is not { } src) return;

        if (_zoomPercent <= 0)
        {
            // Fit: 画像を Uniform で表示（明示サイズなし・RenderTransform なし）
            _image.Stretch = Stretch.Uniform;
            _image.Width = double.NaN;
            _image.Height = double.NaN;
            _image.MaxWidth = Bounds.Width;
            _image.MaxHeight = Bounds.Height;
            _image.RenderTransform = null;
        }
        else
        {
            // 固定倍率: 元サイズのまま RenderTransform でスケール + パン
            _image.Stretch = Stretch.None;
            _image.Width = src.Size.Width;
            _image.Height = src.Size.Height;
            _image.MaxWidth = double.PositiveInfinity;
            _image.MaxHeight = double.PositiveInfinity;

            // 描画位置 = Scale × (コンテンツ − オフセット)
            _translateTransform.X = -_offset.X;
            _translateTransform.Y = -_offset.Y;
            _scaleTransform.ScaleX = Scale;
            _scaleTransform.ScaleY = Scale;
            _image.RenderTransform = _transformGroup;
        }

        UpdateScrollBars();
        InvalidateVisual();
        // ビューポート変化を通知する（オーバーレイ等の追従用。座標変換は受け側が最新表示から再評価する）
        ViewportChanged?.Invoke(this, EventArgs.Empty);
    }

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

            // 表示スケール（Fit 中は Fit スケール。画像はビューポートに収まるため範囲 0 になる）
            var content = new Size(src.Size.Width, src.Size.Height);
            var scale = _zoomPercent > 0 ? Scale : FitScale(content);

            // 縦スクロールバー
            var vMax = ScrollMetrics.MaxOffset(content.Height, Bounds.Height, scale);
            var vViewport = ScrollMetrics.ViewportSize(Bounds.Height, scale);
            _verticalScrollBar.Maximum = vMax;
            _verticalScrollBar.ViewportSize = vViewport;
            _verticalScrollBar.Value = Math.Clamp(_offset.Y, 0, vMax);
            _verticalScrollBar.IsVisible = vMax > 0;

            // 横スクロールバー
            var hMax = ScrollMetrics.MaxOffset(content.Width, Bounds.Width, scale);
            var hViewport = ScrollMetrics.ViewportSize(Bounds.Width, scale);
            _horizontalScrollBar.Maximum = hMax;
            _horizontalScrollBar.ViewportSize = hViewport;
            _horizontalScrollBar.Value = Math.Clamp(_offset.X, 0, hMax);
            _horizontalScrollBar.IsVisible = hMax > 0;
        }
        finally
        {
            _isSyncingScrollBars = false;
        }
    }

    /// <summary>スクロールバーの操作（ユーザーによる Value 変更）でパンする。</summary>
    /// <param name="sender">イベント送信元（ScrollBar）。</param>
    /// <param name="e">値変更イベント。</param>
    /// <remarks>プログラム側の同期（<see cref="UpdateScrollBars"/>）による変更は無視する。</remarks>
    private void OnScrollBarValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isSyncingScrollBars) return;
        if (sender == _verticalScrollBar)
        {
            _offset = ClampCurrentOffset(new Vector(_offset.X, e.NewValue));
        }
        else
        {
            _offset = ClampCurrentOffset(new Vector(e.NewValue, _offset.Y));
        }
        UpdateLayoutAndTransform();
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
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _isPanning = true;
        _grabContentPos = ViewportToContent(e.GetPosition(this));
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
        // 掴んだ位置（コンテンツ座標）がポインタに追従するようオフセットを設定（クランプは SetOffset 内で行う）
        SetOffset(new Vector(
            _grabContentPos.X - pos.X / EffectiveScale,
            _grabContentPos.Y - pos.Y / EffectiveScale));
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
            ZoomRequested?.Invoke(this, new ZoomRequestedEventArgs(e.Delta.Y > 0, pointerPos));
            e.Handled = true;
            return;
        }

        // 通常ホイール: パン（Shift 併用で横スクロール）
        const double scrollUnit = 48.0;
        var isShift = (e.KeyModifiers & KeyModifiers.Shift) != 0;
        var dx = isShift ? e.Delta.Y * scrollUnit : e.Delta.X * scrollUnit;
        var dy = isShift ? 0 : e.Delta.Y * scrollUnit;
        PanBy(new Vector(dx / EffectiveScale, dy / EffectiveScale));
        e.Handled = true;
    }
}
