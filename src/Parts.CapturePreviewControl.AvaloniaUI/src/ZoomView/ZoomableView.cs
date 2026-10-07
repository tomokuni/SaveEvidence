using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using EsUtil.Platform.Core.Geometry;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Interop;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.ZoomView;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.AvaloniaUI.ZoomView;

/// <summary>パン・ズーム可能な画像表示ビューポート（Avalonia 実装）。</summary>
/// <remarks>
/// <b>UI 層の責務は 3 つだけ</b>である（判断・値・計算はコアの <see cref="ZoomViewSession"/> が単一所有する）。<br/>
/// 1. <b>入力ブリッジ</b>: ポインター・ホイールのイベントをコア型へ変換してセッションへ渡し、返された効果（再描画・ポインタキャプチャ）を適用する。<br/>
/// 2. <b>描画</b>: セッションの値（表示サイズ・原点・オフセット・スクロール可否）を <see cref="Image"/> の<br/>
///    レイアウトと <c>RenderTransform</c>、標準 <see cref="ScrollBar"/> へ写す（外観値・判断を持たない）。<br/>
/// 3. <b>ホスト結線</b>: <see cref="Image"/> と <see cref="ScrollBar"/> の生成、<c>ClipToBounds</c>、DPI 非依存のレイアウト。<br/>
/// <br/>
/// <b>描画方式</b>: <c>ScrollViewer</c> を使用せず、<c>RenderTransform</c>（<c>ScaleTransform</c> + <c>TranslateTransform</c>）で<br/>
/// ズーム・パンを実現する（レイアウト処理によるオフセット上書きが原理的に発生しない）。<br/>
/// 描画位置 = スケール × (コンテンツ座標 − オフセット) であり、その値はコアが確定する。<br/>
/// <br/>
/// <b>座標系</b>: コンテンツ座標 = 元画像ピクセル、ビューポート座標 = 本コントロールのローカル座標。<br/>
/// セッションのビューポートは本コントロールの <c>Bounds</c>（本コントロールがビューポートの所有者である）。
/// </remarks>
internal sealed class ZoomableView : Panel
{
    /// <summary>スクロールバーの太さ（DIP）。</summary>
    /// <remarks>標準 <see cref="ScrollBar"/> の見た目であり、コアの外観（<see cref="ZoomViewAppearance"/>）は参照しない。</remarks>
    private const double ScrollBarThickness = 10;

    /// <summary>ズームの状態・判断・計算を単一所有するセッション。</summary>
    private readonly ZoomViewSession _session = new();

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

    /// <summary>スクロールバー同期中の再入防止フラグ（プログラム同期とユーザー操作の循環を断つ）。</summary>
    private bool _isSyncingScrollBars;

    /// <summary>表示更新中の再入防止フラグ（セッションの通知と再計算の循環を断つ）。</summary>
    private bool _isUpdating;

    /// <summary>パン・ズームビューポートを初期化し、入力・レイアウト・スクロールバーの結線を自己完結させる。</summary>
    /// <remarks>
    /// セッションの通知（ズーム状態・ビューポートの変化）を購読し、表示（画像配置・Transform・スクロールバー）を更新する。<br/>
    /// ビューポートのサイズは本コントロールの <c>Bounds</c> であり、サイズ変更時にセッションへ通知する。
    /// </remarks>
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

        // セッションの通知を表示更新へ写す（通知はコアが発火し、UI は描き直すだけ）
        _session.Subscribe(_ => UpdateVisual());

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerWheelChanged += OnPointerWheelChanged;

        // ビューポートサイズ（Bounds）の変更で実効スケールが変わるため、表示を更新する
        SizeChanged += (_, _) => UpdateVisual();

        // スクロールバーの操作（ドラッグ・矢印・ページ送り）でパンする
        _verticalScrollBar.ValueChanged += OnScrollBarValueChanged;
        _horizontalScrollBar.ValueChanged += OnScrollBarValueChanged;
    }

    /// <summary>ズームの状態・判断・計算を単一所有するセッションを取得する。</summary>
    /// <value>セッション（親コントロールと共通）。</value>
    /// <remarks>親コントロール（複合コントロール）は本セッションを介して公開 API を提供する。</remarks>
    internal ZoomViewSession Session => _session;

    /// <summary>画像表示コントロール（内部実装。 テスト用アクセサとして公開）。</summary>
    internal Image ImageControl => _image;

    /// <summary>縦スクロールバー（内部実装。 テスト用アクセサとして公開）。</summary>
    internal ScrollBar VerticalScrollBar => _verticalScrollBar;

    /// <summary>横スクロールバー（内部実装。 テスト用アクセサとして公開）。</summary>
    internal ScrollBar HorizontalScrollBar => _horizontalScrollBar;

    /// <summary>表示する画像（ズーム対象）を取得または設定する。</summary>
    /// <value>表示する画像。 null の場合は何も表示しない。 設定すると Fit 表示へリセットする。</value>
    /// <remarks>画像のサイズ（コンテンツ座標）もここでセッションへ通知する（UI がサイズ型を変換する唯一の箇所）。</remarks>
    public IImage? Source
    {
        get => _image.Source;
        set
        {
            _image.Source = value;
            ApplyFeedback(_session.SetSource(value is not null, value is null ? default : value.Size.ToEsSize()));
        }
    }

    /// <summary>セッションが返した効果を適用する（再描画のみ）。</summary>
    /// <param name="feedback">セッションが返した効果。</param>
    /// <remarks>
    /// ポインタのキャプチャ／解放は入力イベントのハンドラが <c>e.Pointer</c> を必要とするため、そこで適用する。<br/>
    /// 再描画はセッションの通知でも行われるが、通知を伴わない変更（外観の変更等）も描き直すため本メソッドで適用する。
    /// </remarks>
    internal void ApplyFeedback(ZoomFeedback feedback) => feedback.ApplyVisual(InvalidateVisual);

    /// <summary>表示（画像配置・Transform・スクロールバー）をセッションの値から更新する。</summary>
    /// <remarks>
    /// ビューポート（本コントロールの <c>Bounds</c>）をセッションへ通知したうえで、<br/>
    /// 表示サイズ・原点・オフセットを <see cref="Image"/> のレイアウトと Transform、スクロールバーへ写す。<br/>
    /// セッションの通知からも呼ばれるため、再入はフラグで断つ（通知の再帰を防ぐ）。
    /// </remarks>
    private void UpdateVisual()
    {
        if (_isUpdating)
        {
            return;
        }

        _isUpdating = true;
        try
        {
            // ビューポートのサイズをセッションへ通知する（変わった場合のみ通知と再計算が起こる）
            _session.SetViewport(Bounds.Size.ToEsSize());

            ApplyImageLayout();
            ApplyTransform();
            SyncScrollBars();
            InvalidateVisual();
        }
        finally
        {
            _isUpdating = false;
        }
    }

    /// <summary>セッションの表示条件に応じて <see cref="Image"/> のレイアウト（Fit / 固定倍率）を切り替える。</summary>
    /// <remarks>
    /// <b>Fit</b>: <c>Uniform</c> で明示サイズなし（<c>RenderTransform</c> なし）。 中央寄せは <see cref="Image"/> 自身の配置で行う。<br/>
    /// <b>固定倍率</b>: 元サイズのまま <c>RenderTransform</c> でスケール + パンする。 画像の配置は常に左上とし、<br/>
    /// 中央寄せは <see cref="ApplyTransform"/> が translate で行う（<c>RenderTransform</c> は左上基準に拡大縮小するため、<br/>
    /// <c>HorizontalAlignment</c> による中央寄せはスケール ≠ 1 でずれる。 translate に統一して座標変換と描画位置を一致させる）。
    /// </remarks>
    private void ApplyImageLayout()
    {
        if (_image.Source is not { } source)
        {
            return;
        }

        var bounds = Bounds;
        if (_session.IsFit)
        {
            _image.Stretch = Stretch.Uniform;
            _image.Width = double.NaN;
            _image.Height = double.NaN;
            _image.MaxWidth = bounds.Width;
            _image.MaxHeight = bounds.Height;
            _image.HorizontalAlignment = _session.CenterAlign ? HorizontalAlignment.Center : HorizontalAlignment.Left;
            _image.VerticalAlignment = _session.CenterAlign ? VerticalAlignment.Center : VerticalAlignment.Top;
            _image.RenderTransform = null;
            return;
        }

        var imageSize = source.Size;
        _image.Stretch = Stretch.None;
        _image.Width = imageSize.Width;
        _image.Height = imageSize.Height;
        _image.MaxWidth = double.PositiveInfinity;
        _image.MaxHeight = double.PositiveInfinity;
        _image.HorizontalAlignment = HorizontalAlignment.Left;
        _image.VerticalAlignment = VerticalAlignment.Top;
        _image.RenderTransform = _transformGroup;
    }

    /// <summary>固定倍率中の <c>RenderTransform</c> をセッションのオフセットとスケールから更新する。</summary>
    /// <remarks>
    /// 描画位置 = スケール × (コンテンツ − オフセット) を <c>TranslateTransform</c> → <c>ScaleTransform</c> で表現する。<br/>
    /// 中央寄せは軸ごとに独立して加える（スクロール可能な軸はパン位置を維持し、スクロール不能な軸のみ中央寄せする）。<br/>
    /// <c>TransformGroup</c> の translate は <c>ScaleTransform</c> の内側（pre-scale）に適用されるため、<br/>
    /// 中央寄せ補正（ビューポート座標）はスケールで除してコンテンツ座標で指定する。<br/>
    /// Fit 中は <c>RenderTransform</c> が null のため何もしない。
    /// </remarks>
    private void ApplyTransform()
    {
        if (_session.IsFit)
        {
            return;
        }

        var scale = _session.Scale;
        var offset = _session.Offset;
        _translateTransform.X = -offset.X;
        _translateTransform.Y = -offset.Y;
        _scaleTransform.ScaleX = scale;
        _scaleTransform.ScaleY = scale;

        if (!_session.CenterAlign)
        {
            return;
        }

        var bounds = Bounds;
        var display = _session.DisplaySize;
        if (scale > 0)
        {
            // 中央寄せ補正はビューポート座標の値（コアの ZoomMath.MaxCentered が単一所有）。
            // pre-scale の translate へはスケールで除して渡す（画像がビューポートより大きい軸は 0 になる）。
            _translateTransform.X += ZoomMath.MaxCentered(display.Width, bounds.Width, centerAlign: true) / scale;
            _translateTransform.Y += ZoomMath.MaxCentered(display.Height, bounds.Height, centerAlign: true) / scale;
        }
    }

    /// <summary>スクロールバーの表示状態・範囲・値をセッションの値へ同期する。</summary>
    /// <remarks>
    /// 範囲（<c>Maximum</c> / <c>ViewportSize</c>）と表示可否はコア（<see cref="ZoomViewSession.ScrollMetrics"/>）が確定する。<br/>
    /// 本メソッドは値の代入のみを行い、式・判断を持たない。
    /// </remarks>
    private void SyncScrollBars()
    {
        if (_isSyncingScrollBars)
        {
            return;
        }

        _isSyncingScrollBars = true;
        try
        {
            SyncScrollBar(_verticalScrollBar, _session.ScrollMetrics(vertical: true), _session.Offset.Y);
            SyncScrollBar(_horizontalScrollBar, _session.ScrollMetrics(vertical: false), _session.Offset.X);
        }
        finally
        {
            _isSyncingScrollBars = false;
        }
    }

    /// <summary>スクロールバー 1 本の範囲・値・表示状態を同期する。</summary>
    /// <param name="bar">対象のスクロールバー。</param>
    /// <param name="metrics">コアが確定した同期値（最大オフセット・表示範囲・スクロール可否）。</param>
    /// <param name="offset">現在のオフセット（コンテンツ座標）。</param>
    private static void SyncScrollBar(ScrollBar bar, ZoomScrollBarMetrics metrics, double offset)
    {
        bar.Maximum = metrics.MaxOffset;
        bar.ViewportSize = metrics.ViewportSize;
        bar.Value = metrics.ClampOffset(offset);
        bar.IsVisible = metrics.IsScrollable;
    }

    /// <summary>スクロールバーの操作（ユーザーによる値変更）でパンする。</summary>
    /// <param name="sender">イベント送信元（スクロールバー）。</param>
    /// <param name="e">値変更イベント。</param>
    /// <remarks>プログラム側の同期（<see cref="SyncScrollBars"/>）による変更は無視する（循環を断つ）。</remarks>
    private void OnScrollBarValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isSyncingScrollBars)
        {
            return;
        }

        var offset = _session.Offset;
        ApplyFeedback(sender == _verticalScrollBar
            ? _session.SetOffset(new EsVector(offset.X, e.NewValue))
            : _session.SetOffset(new EsVector(e.NewValue, offset.Y)));
    }

    /// <summary>左ボタン押下でパン（grab 方式）を開始する。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">ポインターイベント。</param>
    /// <remarks>
    /// 掴んだ位置のコンテンツ座標をセッションが保存し、以降のドラッグでその位置をポインターへ追従させる。<br/>
    /// ポインターのキャプチャは効果（<see cref="ZoomFeedback.ShouldCapturePointer"/>）に従って適用する。
    /// </remarks>
    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        var feedback = _session.Press(point.Position.ToEsPoint());
        feedback.ApplyVisual(InvalidateVisual);
        feedback.ApplyPointer(() => e.Pointer.Capture(this), () => e.Pointer.Capture(null));
        e.Handled = true;
    }

    /// <summary>ドラッグ中は掴んだ位置がポインターに追従するようパンする。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">ポインターイベント。</param>
    /// <remarks>ドラッグ中でない場合（単なる移動）は何もしない（セッションが空の効果を返す）。</remarks>
    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var feedback = _session.Move(e.GetPosition(this).ToEsPoint());
        if (feedback == default)
        {
            return;
        }

        ApplyFeedback(feedback);
        e.Handled = true;
    }

    /// <summary>ボタン解放でパンを終了する。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">ポインターイベント。</param>
    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var feedback = _session.Release();
        if (feedback == default)
        {
            return;
        }

        feedback.ApplyVisual(InvalidateVisual);
        feedback.ApplyPointer(() => e.Pointer.Capture(this), () => e.Pointer.Capture(null));
        e.Handled = true;
    }

    /// <summary>ホイールでパンまたはズームする。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">ホイールイベント。</param>
    /// <remarks>
    /// Ctrl+ホイールはポインター位置をズーム中心としたズーム、それ以外はパン（Shift 併用で横パン）とする（判断はコア）。
    /// </remarks>
    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        var position = e.GetPosition(this).ToEsPoint();
        var intent = ZoomWheelIntent.Resolve(
            (e.KeyModifiers & KeyModifiers.Control) != 0, (e.KeyModifiers & KeyModifiers.Shift) != 0, e.Delta.Y);
        if (intent.IsZoom)
        {
            ApplyFeedback(_session.ZoomByWheel(intent.ZoomIn, position));
        }
        else
        {
            ApplyFeedback(_session.PanByWheel(new EsVector(e.Delta.X, e.Delta.Y), intent.ScrollHorizontal));
        }

        e.Handled = true;
    }
}
