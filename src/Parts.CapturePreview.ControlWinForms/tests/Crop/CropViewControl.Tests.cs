using EsUtil.Platform.Core.Geometry;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Logging;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Overlay;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Presentation;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.SelectionOverlay;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.ZoomView;
using EsUtil.SaveEvidence.Parts.CapturePreview.WinForms.Crop;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.WinForms.Tests.Crop;

/// <summary><see cref="CropViewControl"/>（画像表示 + オーバーレイの複合コントロール）の状態テスト。</summary>
/// <remarks>
/// 対象: 画像・外観の委譲・オーバーレイの表示切替・選択の委譲・強調矩形の写像の入口。<br/>
/// ズーム・パンの判断は内包するズームビューのセッションが単一所有するため本テストの対象外とする<br/>
/// （座標変換の検証はビューポートを確定させる必要があり、ハンドル（ウィンドウ）を必要とする）。<br/>
/// 描画の検証は描画層（<c>ZoomGraphicsRenderer</c> / <c>RectOverlayGraphicsRenderer</c>）のテストが担う。
/// </remarks>
public sealed class CropViewControlTests
{
    /// <summary>内包するコントロールが生成され、既定のモードが切り出しであることを検証する。</summary>
    [Fact]
    internal void 既定_ズームビューと切り出しモードを用意する()
    {
        using var cropView = new CropViewControl();

        Assert.NotNull(cropView.ZoomView);
        Assert.Equal(SelectionOverlayMode.Crop, cropView.SelectionMode);
        Assert.Equal(SelectionOverlayOptions.Default, cropView.SelectionOverlayOptions);
        Assert.False(cropView.HasSelection);
    }

    /// <summary>強調の既定の外観が共有層の値と一致することを検証する。</summary>
    /// <remarks>既定値は共有層（<see cref="HighlightAppearanceDefaults.Default"/>）が単一所有する。</remarks>
    [Fact]
    internal void HighlightDefaultAppearance_共有層の既定と一致する()
        => Assert.Equal(HighlightAppearanceDefaults.Default, CropViewControl.HighlightDefaultAppearance);

    /// <summary>画像の設定と読み取りが内包するズームビューへ委譲されることを検証する。</summary>
    [Fact]
    internal void Source_内包するズームビューへ委譲する()
    {
        using var cropView = new CropViewControl();
        using var image = TestHelper.CreateBitmap(16, 16, DrawingColor.FromArgb(255, 0, 0, 255));

        cropView.Source = image;

        Assert.Same(image, cropView.Source);
        Assert.Same(image, cropView.ZoomView.Source);
    }

    /// <summary>画像を null へ戻せることを検証する。</summary>
    [Fact]
    internal void Source_nullへ戻せる()
    {
        using var cropView = new CropViewControl();
        using var image = TestHelper.CreateBitmap(16, 16, DrawingColor.FromArgb(255, 0, 0, 255));
        cropView.Source = image;

        cropView.Source = null;

        Assert.Null(cropView.Source);
        Assert.Null(cropView.ZoomView.Source);
    }

    /// <summary>外観の設定が内包するズームビューへ委譲されることを検証する。</summary>
    [Fact]
    internal void Appearance_内包するズームビューへ委譲する()
    {
        using var cropView = new CropViewControl();
        var appearance = ZoomViewAppearance.Default with { ShowScrollBars = false };

        cropView.Appearance = appearance;

        Assert.Equal(appearance, cropView.ZoomView.Appearance);
    }

    /// <summary>強調の外観の設定が強調オーバーレイへ委譲されることを検証する。</summary>
    [Fact]
    internal void HighlightAppearance_強調オーバーレイへ委譲する()
    {
        using var cropView = new CropViewControl();
        var appearance = CropViewControl.HighlightDefaultAppearance with
        {
            InnerFillColor = DrawingColor.FromArgb(64, 255, 255, 0),
        };

        cropView.HighlightAppearance = appearance;

        Assert.Equal(appearance, cropView.HighlightMarquee.Appearance);
    }

    /// <summary>外部提供矩形が矩形オーバーレイへ委譲されることを検証する。</summary>
    [Fact]
    internal void Rects_矩形オーバーレイへ委譲する()
    {
        using var cropView = new CropViewControl();
        var rects = new[]
        {
            new OverlayRect(
                new EsRect(1, 2, 3, 4),
                OverlayLineStyle.Solid,
                DrawingColor.Red,
                DrawingColor.Blue,
                1),
        };

        cropView.Rects = rects;

        Assert.Same(rects, cropView.Rects);
    }

    /// <summary>外部提供矩形に null を設定すると例外になることを検証する。</summary>
    [Fact]
    internal void Rects_nullは例外になる()
    {
        using var cropView = new CropViewControl();

        Assert.Throws<ArgumentNullException>(() => cropView.Rects = null!);
    }

    /// <summary>画像が未設定の間は強調矩形を描画しないことを検証する。</summary>
    /// <remarks>写像の基準（画像の表示矩形）が無いため、強調を解除する。</remarks>
    [Fact]
    internal void HighlightSourceRect_画像未設定では描画しない()
    {
        using var cropView = new CropViewControl();

        cropView.HighlightSourceRect = new EsRect(1, 2, 3, 4);

        Assert.Equal(new EsRect(1, 2, 3, 4), cropView.HighlightSourceRect);
        Assert.Null(cropView.HighlightMarquee.HighlightSourceRect);
    }

    /// <summary>画像を設定すると強調矩形が強調オーバーレイへ写されることを検証する。</summary>
    /// <remarks>写像は対象座標 → ビューポート座標であり、負サイズは 0 へクランプされる（コアが単一所有する）。</remarks>
    [Fact]
    internal void HighlightSourceRect_画像を設定すると写す()
    {
        using var cropView = new CropViewControl();
        using var image = TestHelper.CreateBitmap(16, 16, DrawingColor.FromArgb(255, 0, 0, 255));

        cropView.Source = image;
        cropView.HighlightSourceRect = new EsRect(1, 2, 3, 4);

        Assert.NotNull(cropView.HighlightMarquee.HighlightSourceRect);
    }

    /// <summary>強調矩形を null へ戻すと強調が解除されることを検証する。</summary>
    [Fact]
    internal void HighlightSourceRect_nullで解除する()
    {
        using var cropView = new CropViewControl();
        using var image = TestHelper.CreateBitmap(16, 16, DrawingColor.FromArgb(255, 0, 0, 255));
        cropView.Source = image;
        cropView.HighlightSourceRect = new EsRect(1, 2, 3, 4);

        cropView.HighlightSourceRect = null;

        Assert.Null(cropView.HighlightSourceRect);
        Assert.Null(cropView.HighlightMarquee.HighlightSourceRect);
    }

    /// <summary>強調オーバーレイが入力を受け取らないことを検証する。</summary>
    /// <remarks>強調は描画専用であり、ズーム・ドラッグは下のレイヤが受ける（WinForms では無効状態が入力の透過を表す）。</remarks>
    [Fact]
    internal void 強調オーバーレイ_入力を受け取らない()
    {
        using var cropView = new CropViewControl();

        // 強調は描画専用であり、ズーム・ドラッグは下のレイヤが受ける（WinForms では無効状態が入力の透過を表す）
        Assert.False(cropView.HighlightMarquee.Enabled);
    }

    /// <summary>領域選択オーバーレイの表示を切り替えられることを検証する。</summary>
    [Fact]
    internal void SetSelectionOverlayVisible_表示を切り替える()
    {
        using var cropView = new CropViewControl();

        Assert.False(cropView.SelectionOverlay.Visible); // 初期状態は非表示（切り出し中のみ表示する）

        cropView.SetSelectionOverlayVisible(true);
        Assert.True(cropView.SelectionOverlay.Visible);

        cropView.SetSelection(new EsRect(1, 2, 3, 4));
        Assert.True(cropView.HasSelection);

        cropView.SetSelectionOverlayVisible(false);
        Assert.False(cropView.SelectionOverlay.Visible);
        Assert.False(cropView.HasSelection); // 非表示へ戻すときは選択を破棄する
    }

    /// <summary>選択の設定とクリアが領域選択オーバーレイへ委譲されることを検証する。</summary>
    [Fact]
    internal void SetSelection_領域選択オーバーレイへ委譲する()
    {
        using var cropView = new CropViewControl();

        cropView.SetSelection(new EsRect(1, 2, 3, 4));

        Assert.True(cropView.HasSelection);
        Assert.Equal(new EsRect(1, 2, 3, 4), cropView.SelectionSourceRect);

        cropView.ClearSelection();

        Assert.False(cropView.HasSelection);
    }

    /// <summary>画像が未設定の間はズームできないことを検証する。</summary>
    [Fact]
    internal void ズーム可否_画像未設定では不可()
    {
        using var cropView = new CropViewControl();

        Assert.False(cropView.CanZoomIn);
        Assert.False(cropView.CanZoomOut);
        Assert.True(cropView.ZoomPercent > 0);
    }

    /// <summary>ログの接頭辞が本アセンブリの定義へ反映されることを検証する。</summary>
    [Fact]
    internal void LogPrefix_定義へ反映する()
    {
        using var cropView = new CropViewControl();
        var original = CapturePreviewLogMessage.Prefix;

        try
        {
            cropView.LogPrefix = "[Test]";

            Assert.Equal("[Test]", CapturePreviewLogMessage.Prefix);
        }
        finally
        {
            cropView.LogPrefix = null;
            Assert.Equal(original, CapturePreviewLogMessage.Prefix);
        }
    }
}
