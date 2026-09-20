using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Estable.FadeMask;
using Estable.FadeMaskDragRect;
using Estable.MewUI.CropView;
using Estable.MewUI.FadeMask;
using Estable.MewUI.FadeMaskDragRect;
using Estable.Platform.Coordinate;
using Estable.Platform.Geometry;
using Estable.SaveEvidence.Models;
using Estable.SaveEvidence.ViewModels;
using Microsoft.Extensions.Logging;
using SaveEvidence.MewUI.Logging;
using SaveEvidence.MewUI.Services;
using DrawingColor = System.Drawing.Color;

namespace SaveEvidence.MewUI.Views;

/// <summary>プレビュー画像の切り出し（Crop）セッション（MewUI 版）。</summary>
/// <remarks>
/// Avalonia 版の <c>CropSession</c>（<c>SnipCaptureUI</c>）に相当する。プレビューの上へ領域選択の<br/>
/// オーバーレイを重ね、ドラッグで指定した領域で現在の画像を切り出す。<br/>
/// <br/>
/// <b>座標変換</b>: プレビューはズーム・パンできるため、ローカル DIP ↔ 画像ピクセルの変換は<br/>
/// 表示中の画像矩形と実効倍率から求める（<see cref="CreatePort"/>）。ズーム・パンで表示が変わった時は<br/>
/// <see cref="CropViewControl.ViewportChanged"/> でポートを作り直す。<br/>
/// <br/>
/// <b>ライフサイクル</b>: 開始でオーバーレイを表示し、確定（Enter / イベント）またはキャンセル（Escape）で終了する。<br/>
/// 確定時は <see cref="MainViewModel.CropPreviewCommand"/> を通すため、Undo 履歴への退避（<c>BeforeEditAction</c>）も<br/>
/// キャプチャページと共通の経路になる。<br/>
/// <br/>
/// <b>未対応</b>: 切り出し中のプレビューのズーム操作（Ctrl+ホイール）は、オーバーレイが入力を占有するため無効になる。
/// </remarks>
internal sealed class PreviewCropSession
{
    /// <summary>ロガー（Avalonia 版と同じログファイルへ出力する）。</summary>
    private static readonly ILogger s_logger = AppLog.CreateLogger(nameof(PreviewCropSession));

    /// <summary>切り出し中の選択枠線の太さ（DIP）。</summary>
    private const double BorderThickness = 2;

    /// <summary>切り出し中の選択枠線の色（白）。</summary>
    private static readonly DrawingColor s_borderColor = DrawingColor.FromArgb(255, 255, 255, 255);

    /// <summary>切り出し中の暗転の色（黒 40%）。</summary>
    private static readonly DrawingColor s_overlayColor = DrawingColor.FromArgb(102, 0, 0, 0);

    /// <summary>対象のプレビューコントロール。</summary>
    private readonly CropViewControl _view;

    /// <summary>アプリのメイン ViewModel（切り出しの適用先）。</summary>
    private readonly MainViewModel _viewModel;

    /// <summary>設定（選択枠・ハンドル・ルーペの外観）。</summary>
    private readonly Settings _settings;

    /// <summary>選択オーバーレイ（暗転マスク + 選択矩形 + ルーペ）。</summary>
    private readonly FadeMaskDragRectControl _selector;

    /// <summary>切り出しセッションを初期化する（オーバーレイを生成して表示ツリーへ追加する）。</summary>
    /// <param name="view">対象のプレビューコントロール。</param>
    /// <param name="viewModel">アプリのメイン ViewModel。</param>
    /// <param name="settings">設定。</param>
    /// <param name="host">オーバーレイを重ねる領域（プレビューと同じセル）。</param>
    public PreviewCropSession(CropViewControl view, MainViewModel viewModel, Settings settings, Grid host)
    {
        _view = view;
        _viewModel = viewModel;
        _settings = settings;

        _selector = new FadeMaskDragRectControl
        {
            Mode = FadeMaskDragRectMode.Crop,
            IsVisible = false,
            Log = s_logger,
        };

        _selector.ConfirmRequested += (_, _) => Confirm();
        _selector.CancelRequested += (_, _) => Cancel();
        _view.ViewportChanged += (_, _) => UpdatePort();
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.PreviewImage))
            {
                _selector.SetLoupeSource(_viewModel.PreviewImage);
                UpdatePort();
            }
        };

        ApplyAppearance();
        host.Add(_selector);
        UpdatePort();

        // テーマが変わったらルーペの市松模様の下地色（テーマのページ背景色）を描き直す
        AppStyles.ThemeChanged += ApplyAppearance;
    }

    /// <summary>切り出し中かどうかを取得する。</summary>
    /// <value>オーバーレイを表示中の場合は true。</value>
    public bool IsActive => _selector.IsVisible;

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
        if (_viewModel.PreviewImage is null || IsActive)
        {
            return;
        }

        s_logger.LogCropStart(_viewModel.PreviewImage.Width, _viewModel.PreviewImage.Height);
        UpdatePort();
        _selector.SetLoupeSource(_viewModel.PreviewImage);
        _selector.ClearSelection();
        _selector.IsVisible = true;

        // 非表示の間に描画済みのキャンバスは再描画されないため、表示後に明示的に無効化する
        _selector.InvalidateOverlay();
        _selector.DragRect.InvalidateVisual();

        // 矢印キーの微調整と Escape を受け取れるよう、選択コントロールへフォーカスを移す
        _selector.FocusDragRect();
    }

    /// <summary>切り出しを終了する（選択を破棄してオーバーレイを隠す）。</summary>
    public void Cancel()
    {
        if (IsActive)
        {
            s_logger.LogCropCancelled();
        }

        _selector.ClearSelection();
        _selector.ClearLoupe();
        _selector.IsVisible = false;
    }

    /// <summary>選択された領域を切り出す。</summary>
    /// <remarks>
    /// 1. 画像が無い・選択が退化している場合はキャンセルする（0 サイズの画像を作らない）。<br/>
    /// 2. 先にセッションを終了してから画像を差し替える（オーバーレイの後片付けを確実にする）。<br/>
    /// 3. 切り出しは <c>CropPreviewCommand</c> へ委譲する（Undo 退避と画像差し替えが共通になる）。
    /// </remarks>
    private void Confirm()
    {
        if (_viewModel.PreviewImage is not { } image)
        {
            Cancel();
            return;
        }

        var pixels = CaptureSelectionMath.ToPixelRect(_selector.SelectionRect, image.Width, image.Height);
        if (pixels.IsEmpty)
        {
            Cancel();
            return;
        }

        // 切り出しは「始点・終点のピクセルを含む」閉区間のため、終点の 1 ピクセルを加える（Avalonia 版と同じ規則）
        pixels = CaptureSelectionMath.ToClosedInterval(pixels, image.Width, image.Height);

        s_logger.LogCropCompleted($"{pixels.X},{pixels.Y},{pixels.Width},{pixels.Height}");
        Cancel();
        _viewModel.CropPreviewCommand.Execute(new EsPixelRect(pixels.X, pixels.Y, pixels.Width, pixels.Height));
    }

    /// <summary>表示中のズーム・パンに合わせて座標変換ポートを更新する。</summary>
    /// <remarks>
    /// 画像矩形（ビューポート座標）と実効倍率（表示サイズ ÷ 画像幅）から、ローカル DIP ⇔ 画像ピクセルを<br/>
    /// 変換するポートを組み立てる。ズーム・パンで表示が変わるたびに呼び直す（ポートは都度生成する）。
    /// </remarks>
    private void UpdatePort()
    {
        var image = _viewModel.PreviewImage;
        var rect = _view.ImageRectViewport();
        var display = _view.ZoomView.DisplaySize();
        var scale = image is { Width: > 0 } && display.Width > 0
            ? display.Width / image.Width
            : 1.0;

        _selector.SetCoordinateTransform(new CoordTransform(
            p => new EsPoint((p.X - rect.X) / scale, (p.Y - rect.Y) / scale),
            p => new EsPoint(p.X * scale + rect.X, p.Y * scale + rect.Y),
            () => image is null ? null : new EsSize(image.Width, image.Height)));

        // 画像差し替えで座標系が変わるため、選択を破棄して残留を防ぐ
        if (IsActive)
        {
            _selector.ClearSelection();
        }
    }
    /// <summary>設定の外観を選択コントロールへ反映する。</summary>
    /// <remarks>ルーペの外観（拡大率・サイズ・色）は <see cref="LoupeAppearance"/> が単一所有する。</remarks>
    private void ApplyAppearance()
    {
        _selector.OverlayColor = SettingsColorMath.ToDrawingColor(_settings.CaptureOverlayColor, s_overlayColor);

        // プレビュー上の切り出しは白枠で統一する（暗い画像でも視認できるようにする）
        _selector.BorderColor = s_borderColor;
        _selector.BorderThickness = BorderThickness;

        _selector.LoupeOffset = _settings.LoupeOffset;
        _selector.DragRect.HandleColor = s_borderColor;
        _selector.DragRect.HandleSize = _settings.HandleSize;
        _selector.DragRect.HandleHitRadius = _settings.HandleHitRadius;
        _selector.DragRect.MinSelectionSize = _settings.MinSelectionSize;
        _selector.DragRect.ClampToDomainBounds = _settings.ClampToDomainBounds;
        _selector.DragRect.ShowCoordLabel = _settings.ShowCoordLabel;
        _selector.DragRect.CoordLabelFontSize = _settings.CoordLabelFontSize;
        LoupeAppearance.Apply(_selector.Loupe, _settings);
        LoupeAppearance.ApplySelectionOverlay(_selector, _settings);

        // ラベル（サイズ・操作）は切り出しモードの文言を使う（情報ラベルは表示しない）
        var overlay = _selector.Overlay;
        overlay.ShowSizeLabel = _settings.SizeLabelVisible;
        overlay.SizeLabelColor = SettingsColorMath.ToDrawingColor(_settings.CaptureTextColor, DrawingColor.White);
        overlay.SizeLabelBackgroundColor = SettingsColorMath.ToDrawingColor(_settings.SizeLabelBackgroundColor, DrawingColor.FromArgb(153, 0, 0, 0));
        overlay.SizeLabelFontSize = _settings.SizeLabelFontSize;

        // サイズラベルは画像ピクセル（ドメイン座標）で表示する（表示時点の選択を持ち出す）
        overlay.SizeLabelTextProvider = () =>
            _selector.HasSelection && _settings.SizeLabelVisible
                ? FadeMaskLabels.FormatSizeLabel(_selector.SelectionRect, scaling: 1.0)
                : null;

        overlay.SubLines = _settings.ShowSubLines
            ?
            [
                new FadeMaskSubLine(
                    FadeMaskLabels.GetOperationText(FadeMaskRenderMode.Crop),
                    _settings.SubLineFontSize,
                    SettingsColorMath.ToDrawingColor(_settings.SubLineColor, DrawingColor.FromArgb(255, 220, 220, 220)),
                    SettingsColorMath.ToDrawingColor(_settings.SubLineBackgroundColor, DrawingColor.FromArgb(153, 0, 0, 0))),
            ]
            : null;
    }
}
