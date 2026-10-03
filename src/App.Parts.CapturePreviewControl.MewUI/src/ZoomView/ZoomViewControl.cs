using Aprillz.MewUI;
using Aprillz.MewUI.Skia.Controls;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.Platform.MewUI;
using EsUtil.SaveEvidence.Platform.MewUI.Imaging;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using EsUtil.SaveEvidence.Platform.Skia;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core;


namespace EsUtil.SaveEvidence.Parts.CapturePreview.MewUI;

/// <summary>ズーム・パン可能な画像表示コントロール（MewUI 実装。 Skia のキャンバスへ直接描画する）。</summary>
/// <remarks>
/// <b>UI 層の責務は 3 つだけ</b>である（判断・値・計算はコアの <see cref="ZoomViewSession"/> が単一所有する）。<br/>
/// 1. <b>入力ブリッジ</b>: マウス・ホイールのイベントをコア型へ変換してセッションへ渡し、返された効果（再描画・ポインタキャプチャ）を適用する。<br/>
/// 2. <b>描画</b>: セッションの値（画像表示矩形・スクロールバー・外観）を Skia の描画 API へ写す（外観値・判断を持たない）。<br/>
/// 3. <b>ホスト結線</b>: プロパティ機構（<c>MewProperty</c>）・DPI 追従・ポインタキャプチャ・レイアウト。<br/>
/// <br/>
/// <b>Avalonia 版との違い</b>: 内部の <c>Image</c> + <c>RenderTransform</c> + <c>ScrollBar</c> を持たず、<br/>
/// Skia のキャンバスへ 1 回の <c>DrawBitmap</c> で描画する（中間ビットマップも Transform の合成も不要）。<br/>
/// スクロールバーはオーバーレイとして自前描画する（描画内容はセッションの値に従う）。<br/>
/// 見た目（スクロールバーの太さ・余白・市松模様）は <see cref="ZoomViewAppearance"/> が単一所有し、<br/>
/// ホストが設定する（Avalonia 版は標準 <c>ScrollBar</c> を使うため太さ・余白は本値を使用しない）。
/// </remarks>
public sealed class ZoomViewControl : SkiaCanvasView
{
    /// <summary><see cref="Source"/> プロパティ（ズーム対象の画像）。</summary>
    /// <remarks>設定すると Fit 表示（ズーム率 0・オフセット 0）へリセットする。</remarks>
    public static readonly MewProperty<EsBitmap?> SourceProperty =
        MewProperty<EsBitmap?>.Register<ZoomViewControl>(
            nameof(Source), null, MewPropertyOptions.AffectsRender,
            changed: static (owner, _, _) => owner.OnSourceChanged());

    /// <summary><see cref="CenterAlign"/> プロパティ（画像がビューポートより小さい軸の中央寄せ）。</summary>
    public static readonly MewProperty<bool> CenterAlignProperty =
        MewProperty<bool>.Register<ZoomViewControl>(
            nameof(CenterAlign), true, MewPropertyOptions.AffectsRender,
            changed: static (owner, _, _) => owner.OnCenterAlignChanged());

    /// <summary><see cref="DipToDeviceScaling"/> プロパティ（DIP → 物理ピクセルのスケーリング係数）。</summary>
    public static readonly MewProperty<double> DipToDeviceScalingProperty =
        MewProperty<double>.Register<ZoomViewControl>(nameof(DipToDeviceScaling), 1.0, MewPropertyOptions.AffectsRender);

    /// <summary>ズームの状態・判断・計算を単一所有するセッション。</summary>
    private readonly ZoomViewSession _session;

    /// <summary>ズームビューコントロールを既定の設定で初期化する。</summary>
    public ZoomViewControl() : this(ZoomOptions.Default) { }

    /// <summary>ズームビューコントロールを指定の設定で初期化する。</summary>
    /// <param name="config">ズーム遷移の設定。 null の場合は既定値。</param>
    /// <remarks>
    /// 画像は静止画として扱い連続再描画を無効化する。 ズーム・パンの変更時のみ再描画する。<br/>
    /// セッションの通知（ズーム状態・ビューポートの変化）を購読し、自 UI の公開イベントへ転送する。
    /// </remarks>
    public ZoomViewControl(ZoomOptions? config)
    {
        _session = new ZoomViewSession(config);
        _session.Notified += OnSessionNotified;

        ContinuousAnimation = false;
        PaintSurface += OnPaintSurface;
    }

    /// <summary>ズームの状態・判断・計算を単一所有するセッションを取得する（テスト用アクセサ）。</summary>
    /// <value>セッション。 ホストは本プロパティを使わず、コントロールの公開 API を使用する。</value>
    internal ZoomViewSession Session => _session;

    /// <summary>ズーム操作ログの出力先を取得または設定する。</summary>
    /// <value>ロガー。 null を設定した場合は出力なしとして扱う。</value>
    /// <remarks>null の吸収はコア（<see cref="LoggerFallback.OrNullLogger"/>）が単一所有する（呼び出し側で null 判定しない）。</remarks>
    public ILogger Log
    {
        get => _session.Log;
        set => _session.Log = value;
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
    /// <value>ズーム対象の画像。 null の場合は何も表示しない。 設定すると Fit 表示へリセットする。</value>
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
    /// <value>スケーリング係数。 DPI 変更時に自動更新される。</value>
    public double DipToDeviceScaling
    {
        get => GetValue(DipToDeviceScalingProperty);
        set => SetValue(DipToDeviceScalingProperty, value);
    }

    /// <summary>ズームビューの外観（下地・市松模様・スクロールバー）を取得または設定する。</summary>
    /// <value>外観の単一値。 既定は暗色の下地・市松模様なし・スクロールバー表示。</value>
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
    public EsSize DisplaySize() => _session.DisplaySize;

    /// <summary>画像の表示矩形（ビューポート座標・DIP）を取得する。</summary>
    /// <returns>画像の表示矩形。 画像未設定時は default。</returns>
    public EsRect ImageRectViewport() => _session.ImageRect;

    /// <summary>ビューポート座標をコンテンツ座標（元画像ピクセル）へ変換する（画像表示矩形の原点基準）。</summary>
    /// <param name="viewportPos">ビューポート座標（DIP）。</param>
    /// <returns>コンテンツ座標（元画像ピクセル）。</returns>
    /// <remarks>Fit 中央寄せの原点補正を含む。 変換はコアのセッションへ委譲する。</remarks>
    public EsPoint ViewportToContentImage(EsPoint viewportPos) => _session.ViewportToContentImage(viewportPos);

    /// <summary>コンテンツ座標（元画像ピクセル）をビューポート座標へ変換する（画像表示矩形の原点基準）。</summary>
    /// <param name="contentPos">コンテンツ座標（元画像ピクセル）。</param>
    /// <returns>ビューポート座標（DIP）。</returns>
    /// <remarks>Fit 中央寄せの原点補正を含む。 変換はコアのセッションへ委譲する。</remarks>
    public EsPoint ContentToViewportImage(EsPoint contentPos) => _session.ContentToViewportImage(contentPos);

    /// <summary>ホイールの回転量でパンする（回転量から移動量への換算を単一所有する）。</summary>
    /// <param name="delta">ホイールの回転量（X = 横 / Y = 縦。 1 ノッチ = 1.0）。</param>
    /// <param name="shift">Shift 併用かどうか（縦回転を横パンとして扱う）。</param>
    /// <remarks>ホストが他のコントロール（選択オーバーレイ等）で受けたホイールを転送する場合にも使用する。</remarks>
    public void PanByWheel(EsVector delta, bool shift) => Apply(_session.PanByWheel(delta, shift));

    /// <summary>Ctrl+ホイールのズームを適用する（ホイール操作の公開 API）。</summary>
    /// <param name="zoomIn">true = ズームイン / false = ズームアウト。</param>
    /// <param name="pointerPosition">ズーム中心とする位置（DIP）。 null の場合はビューポート中心。</param>
    /// <remarks>ホストが他のコントロールで受けたホイールを転送する場合にも使用する。</remarks>
    public void ZoomByWheel(bool zoomIn, EsPoint? pointerPosition = null)
        => Apply(_session.ZoomByWheel(zoomIn, pointerPosition));

    /// <summary>ズームインする（⊖⊕ ボタン。 次のレベルへ移動する）。</summary>
    /// <remarks>移動先がなければ何もしない（現在のズーム率を維持する）。</remarks>
    public void ZoomIn() => Apply(_session.ZoomIn());

    /// <summary>ズームアウトする（⊖⊕ ボタン。 前のレベルへ移動する）。</summary>
    /// <remarks>移動先がなければ何もしない（現在のズーム率を維持する）。</remarks>
    public void ZoomOut() => Apply(_session.ZoomOut());

    /// <summary>Fit 表示（ビューポートに収まる自動倍率）へ戻す。</summary>
    /// <remarks>Fit へ移行する唯一の経路（<see cref="ZoomPercent"/> の setter は 0 を受け付けない）。</remarks>
    public void ZoomFit() => Apply(_session.ZoomFit());

    /// <summary>要求サイズを返す。</summary>
    /// <param name="availableSize">親から提示された利用可能サイズ（使用しない）。</param>
    /// <returns>常に <c>Size.Empty</c>（親の残余を埋める）。</returns>
    /// <remarks>ズームビューは親が割り当てた領域全体をビューポートとして使うため、自身の要求サイズを持たない。</remarks>
    protected override Size MeasureContent(Size availableSize) => Size.Empty;

    /// <summary>DPI の変更時に表示スケーリング係数を追従させる。</summary>
    /// <param name="oldDpi">変更前の DPI。</param>
    /// <param name="newDpi">変更後の DPI。</param>
    /// <remarks>モニター間の移動で DPI が変わっても物理ピクセル基準の見た目を保つ。</remarks>
    protected override void OnDpiChanged(uint oldDpi, uint newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        DipToDeviceScaling = DpiScaler.ToScaling(newDpi);
    }

    /// <summary>ビューポートのサイズ変更をセッションへ通知する。</summary>
    /// <param name="e">サイズ変更の情報。</param>
    /// <remarks>Fit 実効倍率・実効ズーム率はビューポートに依存するため、セッションがキャッシュを破棄して再通知する。</remarks>
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Apply(_session.SetViewport(CurrentViewport()));
    }

    /// <summary>左ボタン押下でパンまたはスクロールバーのドラッグを開始する。</summary>
    /// <param name="e">マウスの情報。</param>
    /// <remarks>
    /// スクロールバーのサム・トラック上ならドラッグを開始する（パンより優先する。 判断はコア）。<br/>
    /// それ以外は grab 方式のパンを開始する（掴んだ位置のコンテンツ座標をセッションが保存する）。
    /// </remarks>
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (!e.LeftButton)
        {
            return;
        }

        var position = e.GetPosition(this).ToEsPoint();
        Apply(_session.TryBeginScrollBarDrag(position)
            ? new ZoomFeedback { CapturePointer = true }
            : _session.Press(position));
        e.Handled = true;
    }

    /// <summary>ドラッグ中は掴んだ位置がポインタに追従するようパンする（スクロールバーのドラッグも処理する）。</summary>
    /// <param name="e">マウスの情報。</param>
    /// <remarks>ドラッグ中でない場合（単なる移動）は何もしない（セッションが空の効果を返す）。</remarks>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var position = e.GetPosition(this).ToEsPoint();
        var feedback = _session.IsDraggingScrollBar
            ? _session.UpdateScrollBarDrag(position)
            : _session.Move(position);

        if (feedback != default)
        {
            Apply(feedback);
            e.Handled = true;
        }
    }

    /// <summary>ボタン解放でパンまたはスクロールバーのドラッグを終了する。</summary>
    /// <param name="e">マウスの情報。</param>
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        var feedback = _session.IsDraggingScrollBar ? _session.ReleaseScrollBarDrag() : _session.Release();
        if (feedback != default)
        {
            Apply(feedback);
            e.Handled = true;
        }
    }

    /// <summary>ホイールでパンまたはズームする。</summary>
    /// <param name="e">ホイールの情報。</param>
    /// <remarks>
    /// Ctrl+ホイールはポインタ位置をズーム中心としたズーム、それ以外はパン（Shift 併用で横パン）とする（判断はコア）。
    /// </remarks>
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);

        if (e.ControlKey)
        {
            ZoomByWheel(e.Delta.Y > 0, e.GetPosition(this).ToEsPoint());
        }
        else
        {
            PanByWheel(new EsVector(e.Delta.X, e.Delta.Y), e.ShiftKey);
        }

        e.Handled = true;
    }

    /// <summary>セッションの通知を自 UI の公開イベントへ転送する。</summary>
    /// <param name="sender">送信元（セッション）。</param>
    /// <param name="notice">通知（種別と通知時点の値）。</param>
    /// <remarks>判断を含まない種別 → イベントの対応表とする（通知値はコアが確定済み）。</remarks>
    private void OnSessionNotified(object? sender, ZoomNotice notice)
    {
        switch (notice.Kind)
        {
            case ZoomNoticeKind.ZoomChanged:
                ZoomChanged?.Invoke(this, new ZoomChangedEventArgs(notice.State));
                break;
            case ZoomNoticeKind.ViewportChanged:
                ViewportChanged?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    /// <summary>画像が変更されたときにセッションへ通知する。</summary>
    private void OnSourceChanged() => Apply(_session.SetContent(Source is not null, ContentSize));

    /// <summary>中央寄せの変更をセッションへ通知する。</summary>
    private void OnCenterAlignChanged() => Apply(_session.SetCenterAlign(CenterAlign));

    /// <summary>元画像のサイズ（コンテンツ座標）を取得する。</summary>
    /// <returns>元画像のサイズ。 画像未設定時は default。</returns>
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
    /// <returns>ビューポートのサイズ。 未レイアウト時は default。</returns>
    private EsSize CurrentViewport()
    {
        var bounds = Bounds;
        return bounds.Width > 0 && bounds.Height > 0 ? new EsSize(bounds.Width, bounds.Height) : default;
    }

    /// <summary>セッションが返した効果を適用する。</summary>
    /// <param name="feedback">セッションが返した効果（再描画・ポインタキャプチャ）。</param>
    /// <remarks>適用順（再描画 → キャプチャ／解放）は効果の型が単一所有する（UI 層で判断しない）。</remarks>
    private void Apply(ZoomFeedback feedback)
    {
        feedback.ApplyVisual(InvalidateVisual);
        feedback.ApplyPointer(() => CaptureMouse(true), () => CaptureMouse(false));
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

    /// <summary>画像・市松模様・スクロールバーを描画する。</summary>
    /// <param name="canvas">描画先のキャンバス（物理ピクセル）。</param>
    /// <param name="info">描画先の情報（幅・高さ）。</param>
    /// <remarks>
    /// キャンバスを DPI スケーリングしてから DIP 座標で描画する（コアの計算と座標系を一致させる）。<br/>
    /// 描画順（下地 → 市松模様 → 画像 → スクロールバー）は本メソッドが単一所有し、各描画内容はコアの値に従う。
    /// </remarks>
    private void OnPaintSurface(SKCanvas canvas, SKImageInfo info)
    {
        var appearance = _session.Appearance;
        canvas.Clear(appearance.BackgroundColor.ToSKColor());

        // スケーリングは描画面（物理ピクセル）と割り当て領域（DIP）の比から求める
        // （DPI の通知は追加直後のコントロールへ届かないため、保持値だけに頼ると 100% で描いてしまう）
        var scaling = SkiaSurfaceScale.Resolve(info.Width, Bounds.Size.Width, DipToDeviceScaling);

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
            DrawCheckerboard(canvas, viewport, appearance);
            ZoomSkiaRenderer.Render(canvas, Source, _session);
            DrawScrollBars(canvas, appearance);
        }
        finally
        {
            canvas.Restore();
        }
    }

    /// <summary>市松模様（画像の透明部分の背景）を描画する。</summary>
    /// <param name="canvas">描画先のキャンバス（DIP 座標で描画できる状態であること）。</param>
    /// <param name="viewport">ビューポートのサイズ（DIP）。</param>
    /// <param name="appearance">外観（市松模様の有無・タイルサイズ・色）。</param>
    /// <remarks>
    /// <b>描くのは画像の表示矩形の内側だけ</b>である（範囲は <see cref="ZoomViewAppearance.ComputeCheckerArea"/> が単一所有する）。<br/>
    /// 矩形の外（ペーストボード）は描画の最初に塗った下地色（<see cref="ZoomViewAppearance.BackgroundColor"/>）の単色になる。<br/>
    /// 描画の重ね順は「暗色（B）で範囲を塗る → 明色（A）のタイルを市松位置へ重ねる」。<br/>
    /// 明色タイルの位置はコア（<see cref="ZoomViewAppearance.ComputeLightTileRects"/>）が単一所有する（UI 層に明暗判定を置かない）。
    /// </remarks>
    private void DrawCheckerboard(SKCanvas canvas, EsSize viewport, ZoomViewAppearance appearance)
    {
        var area = appearance.ComputeCheckerArea(_session.ImageRect, viewport);
        if (area.IsEmpty)
        {
            return;
        }

        using var light = new SKPaint { IsAntialias = false, Color = appearance.CheckerColorA.ToSKColor() };
        using var dark = new SKPaint { IsAntialias = false, Color = appearance.CheckerColorB.ToSKColor() };

        // 暗色で範囲を塗る（明色タイルの下地）
        canvas.DrawRect(ToSkRect(area), dark);

        foreach (var tile in appearance.ComputeLightTileRects(area))
        {
            canvas.DrawRect(ToSkRect(tile), light);
        }
    }

    /// <summary>矩形を Skia の矩形へ変換する。</summary>
    /// <param name="rect">変換する矩形（DIP）。</param>
    /// <returns>Skia の矩形。</returns>
    private static SKRect ToSkRect(EsRect rect)
        => new((float)rect.X, (float)rect.Y, (float)rect.Right, (float)rect.Bottom);

    /// <summary>オーバーレイスクロールバーを描画する。</summary>
    /// <param name="canvas">描画先のキャンバス（DIP 座標で描画できる状態であること）。</param>
    /// <param name="appearance">外観（スクロールバーの太さ・余白・色）。</param>
    /// <remarks>
    /// スクロール不要な軸（画像がビューポートに収まる軸）には描画しない（セッションが null を返す）。<br/>
    /// トラックはアルファ 0 の場合に描画しない（サムのみの表示を要求するホスト向け）。
    /// </remarks>
    private void DrawScrollBars(SKCanvas canvas, ZoomViewAppearance appearance)
    {
        var (horizontal, vertical) = _session.CurrentScrollBars();
        if (horizontal is null && vertical is null)
        {
            return;
        }

        using var trackPaint = new SKPaint { IsAntialias = false, Style = SKPaintStyle.Fill, Color = appearance.ScrollBarTrackColor.ToSKColor() };
        using var thumbPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = appearance.ScrollBarColor.ToSKColor() };

        DrawScrollBar(canvas, horizontal, trackPaint, thumbPaint);
        DrawScrollBar(canvas, vertical, trackPaint, thumbPaint);
    }

    /// <summary>スクロールバー 1 本を描画する。</summary>
    /// <param name="canvas">描画先のキャンバス。</param>
    /// <param name="bar">描画するスクロールバー。 null の場合は何もしない。</param>
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

    /// <summary>ビューポート座標を Skia の矩形へ変換する。</summary>
    /// <param name="rect">変換元の矩形（DIP）。</param>
    /// <returns>Skia の矩形。</returns>
    private static SKRect ToSKRect(EsRect rect)
        => new((float)rect.X, (float)rect.Y, (float)rect.Right, (float)rect.Bottom);
}

