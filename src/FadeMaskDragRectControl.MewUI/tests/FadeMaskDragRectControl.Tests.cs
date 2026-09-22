using Estable.FadeMask;
using Estable.FadeMaskDragRect;
using Estable.MewUI.FadeMask;
using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using Aprillz.MewUI;
using SkiaSharp;
using System.Reflection;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace Estable.MewUI.FadeMaskDragRect.Tests;

/// <summary><see cref="FadeMaskDragRectControl"/>（MewUI 版の複合コントロール）の単体テスト。</summary>
/// <remarks>
/// ウィンドウを必要としない範囲（モード切替・状態同期・配線・配置計算）を検証する。<br/>
/// マウス・キー入力はウィンドウを必要とするため、内包コントロールのテスト（<c>DragRectControl.MewUI.Tests</c>）に委ねる。
/// </remarks>
public class FadeMaskDragRectControlTests
{
    /// <summary>テスト用の拡大元画像を生成する。</summary>
    /// <returns>生成した画像。破棄は呼び出し側。</returns>
    private static EsUtil.Platform.Core.Imaging.EsBitmap CreateSource()
        => EsUtil.Platform.Core.Imaging.EsBitmap.Create(32, 32, DrawingColor.White);

    /// <summary>等倍（1 ドメインピクセル = 1 DIP）で境界 200x200 の座標変換ポートを生成する。</summary>
    /// <returns>座標変換ポート。</returns>
    private static ICoordTransform CreatePort()
        => new CoordTransform(p => p, p => p, () => new EsSize(200, 200));

    // ====================================================================
    // 既定値とモード
    // ====================================================================

    [Fact]
    public void 既定値_範囲選択モードで選択なし全面暗転になる()
    {
        var control = new FadeMaskDragRectControl();

        Assert.Equal(FadeMaskDragRectMode.AreaSelect, control.Mode);
        Assert.False(control.HasSelection);
        Assert.Equal(default, control.SelectionRect);
        Assert.False(control.HasHighlight);
        Assert.Null(control.LogicalHighlightRect);
        Assert.True(control.ShowFullOverlay);
        Assert.True(control.DragRect.IsVisible);
        Assert.Null(control.LoupeCenterPoint);
    }

    /// <summary>モードに応じて選択矩形・ルーペの可視性と全面暗転が切り替わることを検証する。</summary>
    /// <param name="mode">設定する動作モード。</param>
    /// <param name="expectedShowFullOverlay">全面暗転の期待値。</param>
    /// <param name="expectedDragRectVisible">選択矩形の可視性の期待値。</param>
    [Theory]
    [InlineData(FadeMaskDragRectMode.Crop, true, true)]
    [InlineData(FadeMaskDragRectMode.AreaSelect, true, true)]
    [InlineData(FadeMaskDragRectMode.ScreenSelect, false, false)]
    [InlineData(FadeMaskDragRectMode.WindowSelect, false, false)]
    public void モード切替_選択矩形の可視性と全面暗転を切り替える(
        FadeMaskDragRectMode mode, bool expectedShowFullOverlay, bool expectedDragRectVisible)
    {
        var control = new FadeMaskDragRectControl { Mode = mode };

        Assert.Equal(expectedShowFullOverlay, control.ShowFullOverlay);
        Assert.Equal(expectedDragRectVisible, control.DragRect.IsVisible);
    }

    [Fact]
    public void モード切替_同じ値を設定しても状態は変わらない()
    {
        var control = new FadeMaskDragRectControl { Mode = FadeMaskDragRectMode.AreaSelect };

        // 同じ値を再設定しても例外にならず、状態は維持される
        control.Mode = FadeMaskDragRectMode.AreaSelect;

        Assert.Equal(FadeMaskDragRectMode.AreaSelect, control.Mode);
        Assert.True(control.ShowFullOverlay);
    }

    // ====================================================================
    // 選択状態の同期
    // ====================================================================

    [Fact]
    public void SetSelection_選択矩形を暗転マスクへ同期する()
    {
        var control = new FadeMaskDragRectControl();

        control.SetSelection(new EsRect(10, 20, 100, 80));

        Assert.True(control.HasSelection);
        Assert.Equal(new EsRect(10, 20, 100, 80), control.SelectionRect);
        Assert.Equal(new EsRect(10, 20, 100, 80), control.Overlay.LogicalSelectionRect);
        Assert.True(control.Overlay.HasSelection);
    }

    [Fact]
    public void SetSelection_サイズ0以下は選択なしとして扱う()
    {
        var control = new FadeMaskDragRectControl();

        control.SetSelection(new EsRect(10, 20, 0, 80));

        Assert.False(control.HasSelection);
        Assert.Null(control.Overlay.LogicalSelectionRect);
    }

    [Fact]
    public void ClearSelection_選択状態をクリアする()
    {
        var control = new FadeMaskDragRectControl();
        control.SetSelection(new EsRect(10, 20, 100, 80));

        control.ClearSelection();

        Assert.False(control.HasSelection);
        Assert.False(control.Overlay.HasSelection);
    }

    [Fact]
    public void SetDragRectSelection_ドラッグ選択から暗転マスクへ同期する()
    {
        var control = new FadeMaskDragRectControl();

        // DragRect の選択変更が暗転マスクへ伝播する（コンストラクタの配線）
        control.SetDragRectSelection(new EsRect(5, 6, 30, 40));

        Assert.True(control.HasSelection);
        Assert.Equal(new EsRect(5, 6, 30, 40), control.SelectionRect);
    }

    [Fact]
    public void ハイライト_設定と取得が委譲される()
    {
        var control = new FadeMaskDragRectControl
        {
            HasHighlight = true,
            LogicalHighlightRect = new EsRect(1, 2, 3, 4),
        };

        Assert.True(control.HasHighlight);
        Assert.Equal(new EsRect(1, 2, 3, 4), control.LogicalHighlightRect);
        Assert.True(control.Overlay.HasHighlight);
        Assert.Equal(new EsRect(1, 2, 3, 4), control.Overlay.LogicalHighlightRect);
    }

    // ====================================================================
    // 外観の委譲
    // ====================================================================

    [Fact]
    public void 外観は暗転マスクとドラッグ矩形の両方へ反映される()
    {
        var control = new FadeMaskDragRectControl();

        control.Appearance = new FadeMaskAppearance
        {
            BorderColor = DrawingColor.Red,
            BorderThickness = 3.0,
            OverlayColor = DrawingColor.FromArgb(128, 255, 0, 0),
            ShowSizeLabel = false,
            SizeLabelFontSize = 18.0,
            SizeLabelColor = DrawingColor.Yellow,
            SizeLabelBackgroundColor = DrawingColor.FromArgb(200, 0, 0, 255),
        };

        var appearance = control.Overlay.Appearance;
        Assert.Equal(DrawingColor.Red, appearance.BorderColor);
        Assert.Equal(3.0, appearance.BorderThickness);
        Assert.Equal(DrawingColor.FromArgb(128, 255, 0, 0), appearance.OverlayColor);
        Assert.False(appearance.ShowSizeLabel);
        Assert.Equal(18.0, appearance.SizeLabelFontSize);
        Assert.Equal(DrawingColor.Yellow, appearance.SizeLabelColor);
        Assert.Equal(DrawingColor.FromArgb(200, 0, 0, 255), appearance.SizeLabelBackgroundColor);
        // 枠線はドラッグ矩形と共用される（見た目の一貫性）
        Assert.Equal(DrawingColor.Red, control.DragRect.Options.BorderColor);
        Assert.Equal(3.0, control.DragRect.Options.BorderThickness);
    }

    [Fact]
    public void WindowTitle_暗転マスクへ反映される()
    {
        var control = new FadeMaskDragRectControl { WindowTitle = "メモ帳" };

        Assert.Equal("メモ帳", control.Overlay.WindowTitle);
    }

    [Fact]
    public void ラベル文言は暗転マスクへ反映される()
    {
        var control = new FadeMaskDragRectControl();
        Func<string?> provider = () => "(10, 20) - (30, 40)";

        control.SizeLabelOverrideProvider = provider;

        Assert.Same(provider, control.Overlay.SizeLabelOverrideProvider);
        // 取得側も同じ値になる（ホストが設定の再適用に使える）
        Assert.Same(provider, control.SizeLabelOverrideProvider);
    }

    [Fact]
    public void ラベルの既定値_サイズラベルを表示し文言プロバイダなし()
    {
        var control = new FadeMaskDragRectControl();

        Assert.True(control.Overlay.Appearance.ShowSizeLabel);
        Assert.Null(control.SizeLabelOverrideProvider);
    }

    // ====================================================================
    // ルーペ画像上の選択矩形破線オーバーレイ
    // ====================================================================

    [Fact]
    public void 選択矩形破線の色_既定は白と黒()
    {
        var control = new FadeMaskDragRectControl();

        Assert.Equal(DrawingColor.White, control.SelectionOverlayColorA);
        Assert.Equal(DrawingColor.Black, control.SelectionOverlayColorB);
    }

    [Fact]
    public void 選択矩形破線の色_設定が反映される()
    {
        var control = new FadeMaskDragRectControl();

        control.SelectionOverlayColorA = DrawingColor.Yellow;
        control.SelectionOverlayColorB = DrawingColor.Magenta;

        Assert.Equal(DrawingColor.Yellow, control.SelectionOverlayColorA);
        Assert.Equal(DrawingColor.Magenta, control.SelectionOverlayColorB);
    }

    [Fact]
    public void DrawSelectionOverlay_選択がある場合は破線を描画する()
    {
        using var target = TestHelper.CreateTarget(64, 64);
        var control = new FadeMaskDragRectControl();
        control.SetSelection(new EsRect(10, 10, 20, 20));

        // 切り出し左上 (0,0)・倍率 4 → 画像座標 (40,40)-(124,124)
        control.DrawSelectionOverlay(target.Canvas, new EsPoint(0, 0), 4.0, 64);

        // 1 色目（白）で上辺が描画される
        Assert.Equal(new SKColor(255, 255, 255), target.GetPixel(40, 40));
        Assert.NotEqual(new SKColor(0, 0, 0, 0), target.GetPixel(40, 41));
        // 選択矩形の内側は描画されない
        Assert.Equal(new SKColor(0, 0, 0, 0), target.GetPixel(50, 50));
    }

    [Fact]
    public void DrawSelectionOverlay_選択がない場合は描画しない()
    {
        using var target = TestHelper.CreateTarget(64, 64);
        var control = new FadeMaskDragRectControl();

        control.DrawSelectionOverlay(target.Canvas, new EsPoint(0, 0), 4.0, 64);

        Assert.Equal(0, target.CountColor(new SKColor(255, 255, 255)));
    }

    [Fact]
    public void DrawSelectionOverlay_選択矩形を使わないモードでは描画しない()
    {
        using var target = TestHelper.CreateTarget(64, 64);
        var control = new FadeMaskDragRectControl { Mode = FadeMaskDragRectMode.WindowSelect };
        control.SetSelection(new EsRect(10, 10, 20, 20));

        control.DrawSelectionOverlay(target.Canvas, new EsPoint(0, 0), 4.0, 64);

        Assert.Equal(0, target.CountColor(new SKColor(255, 255, 255)));
    }

    [Fact]
    public void DrawSelectionOverlay_設定した色で破線を描画する()
    {
        using var target = TestHelper.CreateTarget(64, 64);
        var control = new FadeMaskDragRectControl
        {
            SelectionOverlayColorA = DrawingColor.Red,
            SelectionOverlayColorB = DrawingColor.Blue,
        };
        control.SetSelection(new EsRect(10, 10, 20, 20));

        control.DrawSelectionOverlay(target.Canvas, new EsPoint(0, 0), 4.0, 64);

        Assert.Equal(new SKColor(255, 0, 0), target.GetPixel(40, 40));
        Assert.Equal(new SKColor(0, 0, 255), target.GetPixel(43, 40));
    }

    // ====================================================================
    // ログ
    // ====================================================================

    [Fact]
    public void Log_既定はNullLoggerで例外にならない()
    {
        var control = new FadeMaskDragRectControl();

        // ログ出力を伴う操作を実行しても例外にならない（既定は NullLogger）
        control.DragRect.Options = control.DragRect.Options with { ClampToDomainBounds = false };
        control.SetSelection(new EsRect(10, 10, 50, 40));
        control.RequestCancel();
    }

    [Fact]
    public void Log_内包するドラッグ選択コントロールと同一のロガーになる()
    {
        var control = new FadeMaskDragRectControl();

        Assert.Same(control.DragRect.Log, control.Log);

        control.Log = null!;

        // null は NullLogger へフォールバックし、内包コントロールと同一のインスタンスを共有する
        Assert.NotNull(control.Log);
        Assert.Same(control.DragRect.Log, control.Log);
    }

    [Fact]
    public void Log_内包するドラッグ選択コントロールとルーペへ伝搬される()
    {
        var control = new FadeMaskDragRectControl();
        var logger = new RecordingLogger();

        control.Log = logger;

        // 入力とルーペは同一のロガーを共有する（ホストは 1 箇所の設定で済む）
        Assert.Same(logger, control.Log);
        Assert.Same(logger, control.DragRect.Log);
        Assert.Same(logger, control.Loupe.Log);
    }

    [Fact]
    public void Log_暗転マスクへは伝搬しない()
    {
        var control = new FadeMaskDragRectControl();
        var logger = new RecordingLogger();

        control.Log = logger;

        // 暗転マスクは選択のたびに状態変更ログを出すため、伝搬対象から外す（Avalonia 版と同じ範囲）
        Assert.NotSame(logger, control.Overlay.Log);
    }

    // ====================================================================
    // 座標変換ポート
    // ====================================================================

    [Fact]
    public void SetCoordinateTransform_暗転マスクへ伝搬する()
    {
        var control = new FadeMaskDragRectControl();
        var port = CreatePort();

        control.SetCoordinateTransform(port);

        // 暗転マスク（内包コントロール）が同じポートを受け取る
        Assert.Same(port, GetCoordinatePort(control.Overlay));
    }

    [Fact]
    public void SetCoordinateTransform_nullは暗転マスクでも未設定になる()
    {
        var control = new FadeMaskDragRectControl();
        control.SetCoordinateTransform(CreatePort());

        control.SetCoordinateTransform(null);

        // null（未設定）を倍率から解決する規則はコアが単一所有するため、本クラスは置換せずそのまま渡す
        Assert.Null(GetCoordinatePort(control.Overlay));
    }

    /// <summary>座標変換ポート（内部保持）をリフレクションで取得する。</summary>
    /// <param name="control">対象のコントロール。</param>
    /// <returns>保持している座標変換ポート。未設定の場合は null。</returns>
    /// <remarks>
    /// ポートは公開プロパティではなくメソッドで設定し、内包コントロールが内部で保持する（読み取り API を持たない）。<br/>
    /// 検証は Avalonia 版の複合コントロールテストと同じくリフレクションで行う。
    /// </remarks>
    private static ICoordTransform? GetCoordinatePort(FadeMaskControl control)
        => (ICoordTransform?)typeof(FadeMaskControl)
            .GetField("_coordinatePort", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(control);

    // ====================================================================
    // ルーペ
    // ====================================================================

    [Fact]
    public void ルーペ_画像未設定では表示しない()
    {
        var control = new FadeMaskDragRectControl();
        control.SetCoordinateTransform(CreatePort());

        control.SetLoupePosition(new EsPoint(50, 60));

        Assert.Equal(new EsPoint(50, 60), control.LoupeCenterPoint);
        Assert.False(control.Loupe.IsVisible);
    }

    [Fact]
    public void ルーペ_画像設定後は表示する()
    {
        var control = new FadeMaskDragRectControl();
        control.SetCoordinateTransform(CreatePort());

        using var source = CreateSource();
        control.SetLoupeSource(source);
        control.SetLoupePosition(new EsPoint(50, 60));

        Assert.True(control.Loupe.IsVisible);
        Assert.Same(source, control.Loupe.Source);
        Assert.Equal(new EsPoint(50, 60), control.Loupe.CenterPoint);
    }

    [Fact]
    public void ClearLoupe_画像と中心を破棄する()
    {
        var control = new FadeMaskDragRectControl();
        control.SetCoordinateTransform(CreatePort());
        using var source = CreateSource();
        control.SetLoupeSource(source);
        control.SetLoupePosition(new EsPoint(50, 60));

        control.ClearLoupe();

        Assert.Null(control.LoupeCenterPoint);
        Assert.Null(control.Loupe.Source);
        Assert.False(control.Loupe.IsVisible);
    }

    [Fact]
    public void ルーペ_中心とオフセットの位置へ配置する()
    {
        var control = new FadeMaskDragRectControl();
        control.SetCoordinateTransform(CreatePort());
        control.Loupe.Options = control.Loupe.Options with { RequestedSize = 100 };
        control.LoupeOffset = 20.0;

        using var source = CreateSource();
        control.SetLoupeSource(source);
        control.SetLoupePosition(new EsPoint(50, 60));

        control.Measure(new Size(400, 400));
        control.Arrange(new Rect(0, 0, 400, 400));

        // 中心 (50,60) + オフセット 20 = 左上 (70,80)・一辺 100（親領域 400x400 に収まる）
        Assert.Equal(70, control.Loupe.Bounds.X);
        Assert.Equal(80, control.Loupe.Bounds.Y);
        Assert.Equal(100, control.Loupe.Bounds.Width);
        Assert.Equal(100, control.Loupe.Bounds.Height);
    }

    [Fact]
    public void ルーペ_親領域の右端を超える場合は内側へクランプする()
    {
        var control = new FadeMaskDragRectControl();
        control.SetCoordinateTransform(CreatePort());
        control.Loupe.Options = control.Loupe.Options with { RequestedSize = 100 };
        control.LoupeOffset = 20.0;

        using var source = CreateSource();
        control.SetLoupeSource(source);
        control.SetLoupePosition(new EsPoint(300, 300));

        control.Measure(new Size(200, 200));
        control.Arrange(new Rect(0, 0, 200, 200));

        // 中心 + オフセット = 320 はクランプされ、親領域（200）− 一辺（100） = 100 になる
        Assert.Equal(100, control.Loupe.Bounds.X);
        Assert.Equal(100, control.Loupe.Bounds.Y);
    }

    [Fact]
    public void ルーペ_画像未設定では配置しない()
    {
        var control = new FadeMaskDragRectControl();
        control.Loupe.Options = control.Loupe.Options with { RequestedSize = 100 };

        control.Measure(new Size(400, 400));
        control.Arrange(new Rect(0, 0, 400, 400));

        Assert.Equal(0, control.Loupe.Bounds.Width);
    }

    [Fact]
    public void ルーペ_表示しないモードでは配置しない()
    {
        var control = new FadeMaskDragRectControl { Mode = FadeMaskDragRectMode.ScreenSelect };
        control.Loupe.Options = control.Loupe.Options with { RequestedSize = 100 };
        using var source = CreateSource();
        control.SetLoupeSource(source);
        control.SetLoupePosition(new EsPoint(10, 10));

        control.Measure(new Size(400, 400));
        control.Arrange(new Rect(0, 0, 400, 400));

        Assert.False(control.Loupe.IsVisible);
        Assert.Equal(0, control.Loupe.Bounds.Width);
    }

    // ====================================================================
    // ホストへの通知
    // ====================================================================

    [Fact]
    public void ConfirmRequested_確定時に選択矩形を通知する()
    {
        var control = new FadeMaskDragRectControl();
        FadeMaskDragRectSelectionEventArgs? raised = null;
        control.ConfirmRequested += (_, e) => raised = e;

        control.SetDragRectSelection(new EsRect(10, 20, 100, 80));
        control.RequestConfirm();

        Assert.NotNull(raised);
        Assert.Equal(new EsRect(10, 20, 100, 80), raised!.SelectionRect);
    }

    [Fact]
    public void CancelRequested_選択なしでも通知する()
    {
        var control = new FadeMaskDragRectControl();
        var raised = false;
        control.CancelRequested += (_, _) => raised = true;

        control.RequestCancel();

        Assert.True(raised);
    }

    [Fact]
    public void LoupePositionMoved_選択変更では通知しない()
    {
        var control = new FadeMaskDragRectControl();
        var raised = false;
        control.LoupePositionMoved += (_, _) => raised = true;

        // ルーペ位置の通知はドラッグ選択のマウス移動に由来する（選択変更では発火しない）
        control.SetDragRectSelection(new EsRect(10, 20, 100, 80));
        control.SetSelection(new EsRect(30, 40, 50, 60));

        Assert.False(raised);
    }

    [Fact]
    public void NotifyViewportChanged_例外なく完了する()
    {
        var control = new FadeMaskDragRectControl();
        control.SetCoordinateTransform(CreatePort());
        control.SetSelection(new EsRect(10, 20, 100, 80));

        control.NotifyViewportChanged();

        // 選択状態は維持される（ビューポート変化では再同期しない）
        Assert.True(control.HasSelection);
    }

    [Fact]
    public void InvalidateOverlay_例外なく完了する()
    {
        var control = new FadeMaskDragRectControl();

        control.InvalidateOverlay();

        Assert.True(control.ShowFullOverlay);
    }
}
