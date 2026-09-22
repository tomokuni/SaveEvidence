using BindingFlags = System.Reflection.BindingFlags;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Point = Avalonia.Point;
using Rect = Avalonia.Rect;
using Estable.AvaloniaUI.DragRect;
using EsUtil.Platform.AvaloniaUI;
using EsUtil.Platform.AvaloniaUI.Imaging;
using Estable.FadeMask;
using Estable.FadeMaskDragRect;
using Estable.DragRect;
using Estable.Loupe;
using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace Estable.AvaloniaUI.FadeMaskDragRect.Tests;

/// <summary><see cref="FadeMaskDragRectControl"/> の単体テスト（公開 API・配線・状態同期）。</summary>
/// <remarks>
/// 対象: <c>FadeMaskDragRectControl.cs</c>（公開 API: Mode / Options / SetCoordinateTransform /<br/>
/// SetSelection / ClearSelection / SetLoupePosition / イベント中継 / テスト用アクセサ）。<br/>
/// パブリック API 経由で内包コントロール（DragRect / FadeMask / Loupe）への設定・状態同期・イベント中継を検証する。
/// </remarks>
[Collection("AvaloniaHeadless")]
public class FadeMaskDragRectControlTests
{
    // ====================================================================
    // コンストラクタ・内部構造
    // ====================================================================

    [AvaloniaFact]
    public void コンストラクタ_子コントロールが配置される()
    {
        var control = new FadeMaskDragRectControl();

        // 暗転マスク（描画状態と外観の真のソース）・ドラッグ選択・ルーペが子として配置される
        Assert.Contains(control.Overlay, control.Children);
        Assert.Contains(control.DragRect, control.Children);
        Assert.Contains(control.Loupe, control.Children);
    }

    // ====================================================================
    // Mode（モード別の描画モード・可視性・全面暗転）
    // ====================================================================

    [AvaloniaFact]
    public void Mode_既定値はAreaSelectである()
    {
        var control = new FadeMaskDragRectControl();

        Assert.Equal(FadeMaskDragRectMode.AreaSelect, control.Mode);
    }

    [AvaloniaTheory]
    [InlineData(FadeMaskDragRectMode.Crop, FadeMaskRenderMode.Crop, true, true)]
    [InlineData(FadeMaskDragRectMode.AreaSelect, FadeMaskRenderMode.AreaSelect, true, true)]
    [InlineData(FadeMaskDragRectMode.ScreenSelect, FadeMaskRenderMode.ScreenSelect, false, false)]
    [InlineData(FadeMaskDragRectMode.WindowSelect, FadeMaskRenderMode.WindowSelect, false, false)]
    public void Mode_描画モードと可視性と全面暗転が切り替わる(
        FadeMaskDragRectMode mode, FadeMaskRenderMode expectedRenderMode, bool expectedDragRectVisible, bool expectedShowFullOverlay)
    {
        var control = new FadeMaskDragRectControl { Mode = mode };

        Assert.Equal(expectedRenderMode, control.Overlay.Mode);
        Assert.Equal(expectedDragRectVisible, control.DragRect.IsVisible);
        Assert.Equal(expectedShowFullOverlay, control.Overlay.ShowFullOverlay);
    }

    // ====================================================================
    // Options（設定の単一値。内包コントロールへの分配）
    // ====================================================================

    [AvaloniaFact]
    public void Options_既定はコアの既定値である()
    {
        var control = new FadeMaskDragRectControl();

        Assert.Equal(FadeMaskDragRectOptions.Default, control.Options);
    }

    [AvaloniaFact]
    public void Options_選択矩形の設定が反映される()
    {
        var control = new FadeMaskDragRectControl();
        var options = FadeMaskDragRectOptions.Default with
        {
            DragRect = DragRectOptions.Default with
            {
                MinSelectionSize = 5,
                HandleColor = Color.FromAColor(102, Colors.Red).ToDrawingColor(),
                BorderThickness = 3.0,
                HandleSize = 12.0,
                HandleHitRadius = 15.0,
                ClampToDomainBounds = true,
                MarkerColor = Colors.Green.ToDrawingColor(),
                MarkerLength = 20.0,
            },
            Overlay = new FadeMaskAppearance { BorderThickness = 3.0 },
        };

        control.Options = options;

        var applied = control.DragRect.Options;
        Assert.Equal(5, applied.MinSelectionSize);
        Assert.Equal(3.0, applied.BorderThickness);
        Assert.Equal(Color.FromAColor(102, Colors.Red), applied.HandleColor.ToAvaloniaColor());
        Assert.Equal(12.0, applied.HandleSize);
        Assert.Equal(15.0, applied.HandleHitRadius);
        Assert.True(applied.ClampToDomainBounds);
        Assert.Equal(Colors.Green, applied.MarkerColor.ToAvaloniaColor());
        Assert.Equal(20.0, applied.MarkerLength);
    }

    [AvaloniaFact]
    public void Options_暗転マスクの設定が反映される()
    {
        var control = new FadeMaskDragRectControl();
        var options = FadeMaskDragRectOptions.Default with
        {
            Overlay = new FadeMaskAppearance
            {
                OverlayColor = Color.FromAColor(128, Colors.DarkBlue).ToDrawingColor(), // 暗青 50%
                SizeLabelColor = Colors.Yellow.ToDrawingColor(),
                SizeLabelBackgroundColor = Color.FromAColor(179, Colors.DarkBlue).ToDrawingColor(), // 暗青 70%
                SizeLabelFontSize = 18.0,
                ShowSizeLabel = false,
                ShowSubLines = false,
                SubLineFontSize = 13.0,
                SubLineColor = Colors.Orange.ToDrawingColor(),
                SubLineBackgroundColor = Color.FromAColor(102, Colors.DarkGreen).ToDrawingColor(), // 暗緑 40%
                InfoLabelBackgroundColor = Color.FromAColor(128, Colors.DarkRed).ToDrawingColor(), // 暗赤 50%
                InfoLabelVisible = false,
                InfoLabelColor = Colors.Cyan.ToDrawingColor(),
                InfoLabelFontSize = 12.0,
            },
        };

        control.Options = options;

        // DPI スケーリングは設定（Options）の対象外（環境値。個別プロパティで設定）
        control.Scaling = 2.0;
        control.DisplayScaleScaling = 1.5;

        Assert.Equal(2.0, control.Overlay.Scaling);
        Assert.Equal(1.5, control.Overlay.DisplayScaleScaling);
        // 外観はコアの FadeMaskAppearance へ 1 つにまとめて反映される（色は UI 非依存の色型）
        var appearance = control.Overlay.Appearance;
        Assert.Equal(Color.FromAColor(128, Colors.DarkBlue).ToDrawingColor(), appearance.OverlayColor);
        Assert.Equal(Colors.Yellow.ToDrawingColor(), appearance.SizeLabelColor);
        Assert.Equal(Color.FromAColor(179, Colors.DarkBlue).ToDrawingColor(), appearance.SizeLabelBackgroundColor);
        Assert.Equal(18.0, appearance.SizeLabelFontSize);
        Assert.False(appearance.ShowSizeLabel);
        Assert.False(appearance.ShowSubLines);
        Assert.Equal(13.0, appearance.SubLineFontSize);
        Assert.Equal(Colors.Orange.ToDrawingColor(), appearance.SubLineColor);
        Assert.Equal(Color.FromAColor(102, Colors.DarkGreen).ToDrawingColor(), appearance.SubLineBackgroundColor);
        Assert.Equal(Color.FromAColor(128, Colors.DarkRed).ToDrawingColor(), appearance.InfoLabelBackgroundColor);
        Assert.False(appearance.InfoLabelVisible);
        Assert.Equal(Colors.Cyan.ToDrawingColor(), appearance.InfoLabelColor);
        Assert.Equal(12.0, appearance.InfoLabelFontSize);
    }

    [AvaloniaFact]
    public void Options_ルーペの設定が反映される()
    {
        var control = new FadeMaskDragRectControl();
        var options = FadeMaskDragRectOptions.Default with
        {
            DragRect = DragRectOptions.Default with
            {
                ShowCoordLabel = true,
                CoordLabelColor = Colors.Yellow.ToDrawingColor(),
                CoordLabelBackgroundColor = Color.FromAColor(179, Colors.DarkGreen).ToDrawingColor(), // 暗緑 70%
                CoordLabelFontSize = 14.0,
            },
            Overlay = new FadeMaskAppearance { BorderThickness = 2.0, BorderColor = Colors.DarkGray.ToDrawingColor() },
            Loupe = new LoupeOptions
            {
                RequestedSize = 150,
                ZoomLevel = 3,
                Appearance = new LoupeAppearance
                {
                    CrossColor = Color.FromAColor(204, Colors.Red).ToDrawingColor(), // 赤 80%
                    CheckerSize = 10,
                    CheckerColorA = Colors.Black.ToDrawingColor(),
                    CheckerColorB = Colors.White.ToDrawingColor(),
                    FrameColor = Colors.Gray.ToDrawingColor(),
                    FrameWidth = 4,
                    ShowCoordLabel = true,
                    CoordLabelColor = Colors.Cyan.ToDrawingColor(),
                    CoordLabelBackgroundColor = Color.FromAColor(153, Colors.DarkRed).ToDrawingColor(), // 暗赤 60%
                    CoordLabelFontSize = 13.0,
                },
            },
            LoupeOffset = 30.0,
        };

        control.Options = options;

        // ルーペ描画スケールは設定（Options）の対象外（環境値。個別プロパティで設定）
        control.LoupeRenderScaling = 2.0;

        Assert.Equal(150, control.Loupe.Options.RequestedSize);
        Assert.Equal(3, control.Loupe.Options.ZoomLevel);
        Assert.Equal(2.0, control.Loupe.RenderScaling);
        Assert.Equal(Color.FromAColor(204, Colors.Red), control.Loupe.Options.Appearance.CrossColor.ToAvaloniaColor());
        Assert.Equal(10, control.Loupe.Options.Appearance.CheckerSize);
        Assert.Equal(4, control.Loupe.Options.Appearance.FrameWidth);
        // 表示位置のオフセットはフレームワーク差（Avalonia 版は自己配置）のため、ルーペのプロパティへ渡される
        Assert.Equal(30.0, control.Loupe.PositionOffset);
        // 座標ラベル（DragRect / Loupe）
        var applied = control.DragRect.Options;
        Assert.True(applied.ShowCoordLabel);
        Assert.Equal(Colors.Yellow, applied.CoordLabelColor.ToAvaloniaColor());
        Assert.Equal(Color.FromAColor(179, Colors.DarkGreen), applied.CoordLabelBackgroundColor.ToAvaloniaColor());
        Assert.Equal(14.0, applied.CoordLabelFontSize);
        Assert.True(control.Loupe.Options.Appearance.ShowCoordLabel);
        Assert.Equal(13.0, control.Loupe.Options.Appearance.CoordLabelFontSize);
        Assert.Equal(Color.FromAColor(153, Colors.DarkRed), control.Loupe.Options.Appearance.CoordLabelBackgroundColor.ToAvaloniaColor());
    }

    [AvaloniaFact]
    public void Options_選択矩形破線の色は設定から読まれる()
    {
        var control = new FadeMaskDragRectControl();
        var colorA = Color.FromAColor(204, Colors.Red).ToDrawingColor();
        var colorB = Color.FromAColor(204, Colors.Blue).ToDrawingColor();

        control.Options = FadeMaskDragRectOptions.Default with
        {
            SelectionRectColorA = colorA,
            SelectionRectColorB = colorB,
        };

        Assert.Equal(colorA, control.Options.SelectionRectColorA);
        Assert.Equal(colorB, control.Options.SelectionRectColorB);
    }

    [AvaloniaFact]
    public void Options_同じ値の再設定では子へ再適用しない()
    {
        var control = new FadeMaskDragRectControl();
        var options = FadeMaskDragRectOptions.Default with
        {
            Loupe = LoupeOptions.Default with { RequestedSize = 200 },
        };
        control.Options = options;
        var appliedBefore = control.Loupe.Options;

        // 現在値と同じ値を再設定しても、子コントロールへの適用は行われない（適用済みの値が同一のまま）
        control.Options = options;

        Assert.Same(appliedBefore, control.Loupe.Options);
    }

    [AvaloniaFact]
    public void Options_指定しない項目は現在値を維持する()
    {
        var control = new FadeMaskDragRectControl();
        control.Options = FadeMaskDragRectOptions.Default with
        {
            DragRect = DragRectOptions.Default with { MinSelectionSize = 5 },
            Loupe = LoupeOptions.Default with { RequestedSize = 150 },
            LoupeOffset = 30.0,
        };

        // with 式で一部だけを差し替える（指定しない項目は現在値＝既定値を維持する）
        control.Options = control.Options with
        {
            DragRect = control.Options.DragRect with { MarkerLength = 24.0 },
            Overlay = control.Options.Overlay with { BorderThickness = 4.0 },
            Loupe = control.Options.Loupe with { RequestedSize = 200 },
        };

        Assert.Equal(5, control.DragRect.Options.MinSelectionSize);           // 未指定 → 現在値維持
        Assert.Equal(24.0, control.DragRect.Options.MarkerLength);            // 指定 → 反映
        Assert.Equal(4.0, control.DragRect.Options.BorderThickness);          // 指定 → 反映（枠線は暗転マスクの外観が決める）
        Assert.Equal(200, control.Loupe.Options.RequestedSize);               // 指定 → 反映
        Assert.Equal(30.0, control.Loupe.PositionOffset);                     // 未指定 → 現在値維持
        Assert.Equal(200, control.Options.Loupe.RequestedSize);               // getter は更新後の値を返す
    }

    // ====================================================================
    // SetCoordinateTransform（ポートの一括分配）
    // ====================================================================

    [AvaloniaFact]
    public void SetCoordinateTransform_3コントロールへ同一ポートが分配される()
    {
        var control = new FadeMaskDragRectControl();
        var port = CoordTransform.Identity;

        control.SetCoordinateTransform(port);

        Assert.Same(port, GetCoordinatePort(control.DragRect));
        Assert.Same(port, GetCoordinatePort(control.Overlay));
        Assert.Same(port, GetCoordinatePort(control.Loupe));
    }

    // ====================================================================
    // SetSelection / ClearSelection（状態設定）
    // ====================================================================

    [AvaloniaFact]
    public void SetSelection_選択状態がレンダラーとルーペへ反映される()
    {
        var control = new FadeMaskDragRectControl();
        var rect = new Rect(100, 200, 400, 300);

        control.SetSelection(rect);

        Assert.Equal(rect, control.Overlay.LogicalSelectionRect);
        Assert.True(control.Overlay.HasSelection);
    }

    [AvaloniaFact]
    public void SetSelection_サイズ0の矩形は未選択として反映される()
    {
        var control = new FadeMaskDragRectControl();

        control.SetSelection(new Rect(0, 0, 0, 0));

        Assert.Null(control.Overlay.LogicalSelectionRect);
        Assert.False(control.Overlay.HasSelection);
    }

    [AvaloniaFact]
    public void ClearSelection_選択状態がクリアされる()
    {
        var control = new FadeMaskDragRectControl();
        control.SetSelection(new Rect(10, 10, 50, 40));

        control.ClearSelection();

        Assert.Null(control.Overlay.LogicalSelectionRect);
        Assert.False(control.Overlay.HasSelection);
    }

    // ====================================================================
    // 配線（DragRect のイベント → 状態同期・ホスト通知）
    // ====================================================================

    [AvaloniaFact]
    public void SelectionChanged_選択矩形がレンダラーへ同期される()
    {
        var control = new FadeMaskDragRectControl();
        var rect = new Rect(20, 30, 100, 80);

        // DragRect.SetSelection → SelectionChanged 発火 → 複合コントロールがレンダラーへ同期
        control.DragRect.SetSelection(rect);

        Assert.Equal(rect, control.Overlay.LogicalSelectionRect);
        Assert.True(control.Overlay.HasSelection);
    }

    [AvaloniaFact]
    public void ConfirmRequested_DragRectの確定要求が中継される()
    {
        var control = new FadeMaskDragRectControl();
        control.DragRect.SetSelection(new Rect(10, 10, 50, 40));
        Rect? confirmed = null;
        control.ConfirmRequested += (_, e) => confirmed = e.SelectionRect;

        control.RequestConfirm();

        Assert.Equal(new Rect(10, 10, 50, 40), confirmed);
    }

    [AvaloniaFact]
    public void ConfirmRequested_購読者がいない場合は中継されない()
    {
        var control = new FadeMaskDragRectControl();
        control.DragRect.SetSelection(new Rect(10, 10, 50, 40));

        // リフレクションで ConfirmRequested を直接発火する（購読者なし → ?.Invoke の null 分岐）
        var field = typeof(DragRectControl).GetField(
            "ConfirmRequested", BindingFlags.Instance | BindingFlags.NonPublic);
        var eventDelegate = (MulticastDelegate?)field?.GetValue(control.DragRect);
        eventDelegate?.DynamicInvoke(control.DragRect,
            new SelectionRectEventArgs(new Rect(10, 10, 50, 40), control.DragRect.CurrentSnapshot));

        // 例外なく完了する
        Assert.True(true);
    }

    [AvaloniaFact]
    public void CancelRequested_DragRectのキャンセル要求が中継される()
    {
        var control = new FadeMaskDragRectControl();
        int cancelled = 0;
        control.CancelRequested += (_, _) => cancelled++;

        control.RequestCancel();

        Assert.Equal(1, cancelled);
    }

    [AvaloniaFact]
    public void CancelRequested_購読者がいない場合は中継されない()
    {
        var control = new FadeMaskDragRectControl();

        // リフレクションで CancelRequested を直接発火する（購読者なし → ?.Invoke の null 分岐）
        var field = typeof(DragRectControl).GetField(
            "CancelRequested", BindingFlags.Instance | BindingFlags.NonPublic);
        var eventDelegate = (MulticastDelegate?)field?.GetValue(control.DragRect);
        eventDelegate?.DynamicInvoke(control.DragRect, EventArgs.Empty);

        // 例外なく完了する
        Assert.True(true);
    }

    [AvaloniaFact]
    public void ZoomRequested_ズーム要求が中継される()
    {
        var control = new FadeMaskDragRectControl();
        DragRectZoomRequest? received = null;
        control.ZoomRequested += (_, request) => received = request;

        // DragRect の ZoomRequested は通常の .NET イベント（Ctrl+ホイールで発火）のため、
        // リフレクションでバッキングフィールドを取得して直接発火する（中継の検証用）。
        var field = typeof(DragRectControl).GetField(
            "ZoomRequested", BindingFlags.Instance | BindingFlags.NonPublic);
        var eventDelegate = (MulticastDelegate?)field?.GetValue(control.DragRect);
        eventDelegate?.DynamicInvoke(control.DragRect, new DragRectZoomRequest(true, new EsPoint(10, 20)));

        Assert.NotNull(received);
        Assert.True(received!.Value.ZoomIn);
        Assert.Equal(new EsPoint(10, 20), received.Value.Position);
    }

    [AvaloniaFact]
    public void ScrollRequested_スクロール要求が中継される()
    {
        var control = new FadeMaskDragRectControl();
        DragRectScrollRequest? received = null;
        control.ScrollRequested += (_, request) => received = request;

        // DragRect の ScrollRequested をリフレクションで直接発火する（中継の検証用）。
        var field = typeof(DragRectControl).GetField(
            "ScrollRequested", BindingFlags.Instance | BindingFlags.NonPublic);
        var eventDelegate = (MulticastDelegate?)field?.GetValue(control.DragRect);
        eventDelegate?.DynamicInvoke(control.DragRect, new DragRectScrollRequest(0, 1, true, new EsPoint(30, 40)));

        Assert.NotNull(received);
        Assert.Equal(1.0, received!.Value.DeltaY);
        Assert.True(received.Value.Shift);
        Assert.Equal(new EsPoint(30, 40), received.Value.Position);
    }

    [AvaloniaFact]
    public void ZoomRequested_購読者がいない場合は中継されない()
    {
        var control = new FadeMaskDragRectControl();

        // リフレクションで ZoomRequested を直接発火する（購読者なし → ?.Invoke の null 分岐）
        var field = typeof(DragRectControl).GetField(
            "ZoomRequested", BindingFlags.Instance | BindingFlags.NonPublic);
        var eventDelegate = (MulticastDelegate?)field?.GetValue(control.DragRect);
        eventDelegate?.DynamicInvoke(control.DragRect, new DragRectZoomRequest(true, new EsPoint(0, 0)));

        // 例外なく完了する
        Assert.True(true);
    }

    // ====================================================================
    // SetLoupePosition（ルーペ中心の設定）
    // ====================================================================

    [AvaloniaFact]
    public void SetLoupePosition_ルーペ中心が設定され表示される()
    {
        var control = new FadeMaskDragRectControl();
        var window = new Window { Content = new Panel { Children = { control } } };
        window.Show();

        // Source 未設定の間は表示しない（市松模様防止の仕様）
        control.SetLoupePosition(new Point(960, 540));
        Assert.False(control.Loupe.IsVisible);
        Assert.Equal(new Point(960, 540), control.Loupe.CenterPoint);

        // Source 設定後に再呼び出しすると表示される
        control.Loupe.Source = EsBitmapAvaloniaExtensions.CreateAvaloniaImage(10, 10, Colors.Transparent);
        control.SetLoupePosition(new Point(960, 540));
        Assert.True(control.Loupe.IsVisible);
        Assert.Equal(new Point(960, 540), control.Loupe.CenterPoint);

        window.Close();
    }

    // ====================================================================
    // ルーペ Bitmap の再生成要求（ホスト側オーバーレイの鮮度）
    // ====================================================================

    /// <summary>破線色のみを変更した場合でもルーペ Bitmap が再生成される（ホスト側オーバーレイはルーペのプロパティではない）。</summary>
    /// <remarks>
    /// 破線色（<c>SelectionRectColorA / B</c>）はルーペの外観（<c>LoupeAppearance</c>）に含まれず、<br/>
    /// ルーペ Bitmap へホストが描画するオーバーレイの色である。したがって破線色のみの変更では<br/>
    /// ルーペのプロパティ通知が発生せず、明示的な再生成要求が無いと Bitmap が古い色のまま残る。
    /// </remarks>
    [AvaloniaFact]
    public async Task Options_破線色のみの変更でもルーペが再描画される()
    {
        var control = new FadeMaskDragRectControl();
        var window = new Window { Content = new Panel { Children = { control } } };
        window.Show();
        control.Loupe.Source = EsBitmapAvaloniaExtensions.CreateAvaloniaImage(10, 10, Colors.Transparent);
        control.SetLoupePosition(new Point(5, 5));

        var rendered = 0;
        void OnRendered(object? sender, EventArgs e) => rendered++;
        control.Loupe.Rendered += OnRendered;
        try
        {
            // ルーペの各プロパティを設定値へ一致させ、初期同期の描画を消化してから計測を開始する
            control.Options = control.Options with
            {
                Loupe = control.Options.Loupe with { RequestedSize = 200 },
                SelectionRectColorA = Colors.White.ToDrawingColor(),
            };
            await PumpUntilAsync(() => rendered > 0);
            Assert.True(rendered > 0);
            await PumpAsync();
            rendered = 0;

            // 破線色のみの変更（ルーペ自身の外観・サイズ・ズームは不変 → ルーペのプロパティ通知は発生しない）
            var appearance = control.Loupe.Options.Appearance;
            control.Options = control.Options with { SelectionRectColorA = Colors.Lime.ToDrawingColor() };
            Assert.Equal(appearance, control.Loupe.Options.Appearance);
            Assert.Equal(200, control.Loupe.Options.RequestedSize);

            await PumpUntilAsync(() => rendered > 0);
            Assert.True(rendered > 0, "破線色のみの変更でルーペが再描画されませんでした");
        }
        finally
        {
            control.Loupe.Rendered -= OnRendered;
            window.Close();
        }
    }

    /// <summary>選択矩形を使わないモードへ変更すると、ルーペの表示が解除されることを検証する。</summary>
    /// <remarks>
    /// 破線の表示可否はモード依存（<c>FadeMaskDragRectModes</c>）であり、選択矩形を使わないモードでは<br/>
    /// ルーペ自体を表示しない（コアが単一所有する判断）。
    /// </remarks>
    [AvaloniaFact]
    public async Task Mode_選択矩形を使わないモードへ変更するとルーペが非表示になる()
    {
        var control = new FadeMaskDragRectControl();
        var window = new Window { Content = new Panel { Children = { control } } };
        window.Show();
        control.Loupe.Source = EsBitmapAvaloniaExtensions.CreateAvaloniaImage(10, 10, Colors.Transparent);
        control.SetLoupePosition(new Point(5, 5));

        var rendered = 0;
        void OnRendered(object? sender, EventArgs e) => rendered++;
        control.Loupe.Rendered += OnRendered;
        try
        {
            await PumpUntilAsync(() => rendered > 0);
            Assert.True(control.Loupe.IsVisible);

            // 破線を描画しないモード（ScreenSelect）へ変更する
            control.Mode = FadeMaskDragRectMode.ScreenSelect;

            // ルーペが非表示になる（以降の描画は発生しない）
            Assert.False(control.Loupe.IsVisible);
            Assert.False(control.ShowFullOverlay);
        }
        finally
        {
            control.Loupe.Rendered -= OnRendered;
            window.Close();
        }
    }

    /// <summary>選択矩形のみの変更でもルーペ Bitmap が再生成される（破線は Bitmap へ焼き込まれる）。</summary>
    /// <remarks>
    /// 破線は <see cref="LoupeControl.Rendered"/> でルーペ Bitmap へ描画されるため、<br/>
    /// 選択矩形が変わった場合は中心（ルーペのプロパティ）が変わらなくても再生成が必要になる。
    /// </remarks>
    [AvaloniaFact]
    public async Task SetSelection_選択矩形のみの変更でもルーペが再描画される()
    {
        var control = new FadeMaskDragRectControl();
        var window = new Window { Content = new Panel { Children = { control } } };
        window.Show();
        control.Loupe.Source = EsBitmapAvaloniaExtensions.CreateAvaloniaImage(10, 10, Colors.Transparent);
        control.SetLoupePosition(new Point(5, 5));

        var rendered = 0;
        void OnRendered(object? sender, EventArgs e) => rendered++;
        control.Loupe.Rendered += OnRendered;
        try
        {
            await PumpUntilAsync(() => rendered > 0);
            Assert.True(rendered > 0);
            await PumpAsync();
            rendered = 0;

            // 選択矩形のみの変更（ルーペの中心・外観・サイズは不変）
            control.SetSelection(new Rect(1, 1, 5, 5));

            await PumpUntilAsync(() => rendered > 0);
            Assert.True(rendered > 0, "選択矩形のみの変更でルーペが再描画されませんでした");
        }
        finally
        {
            control.Loupe.Rendered -= OnRendered;
            window.Close();
        }
    }

    // ====================================================================
    // SizeLabelOverrideProvider（レンダラーへの伝搬）
    // ====================================================================

    [AvaloniaFact]
    public void SizeLabelOverrideProvider_レンダラーへ伝搬される()
    {
        var control = new FadeMaskDragRectControl();
        Func<string?>? provider = () => "LT: (10, 20)";
        control.SizeLabelOverrideProvider = provider;

        // getter は設定したプロバイダを返し、レンダラーへも伝搬される
        Assert.Same(provider, control.SizeLabelOverrideProvider);
        Assert.Equal("LT: (10, 20)", control.Overlay.SizeLabelOverrideProvider?.Invoke());
    }

    // ====================================================================
    // ヘルパー
    // ====================================================================

    /// <summary>条件が成立するまで Dispatcher をポンプする（上限で打ち切る）。</summary>
    /// <param name="condition">成立を待つ条件。</param>
    /// <remarks>
    /// ルーペの描画は Dispatcher へのポスト（描画フェーズ）と非同期生成で行われるため、<br/>
    /// 待機中は Dispatcher をポンプして描画ジョブと非同期の継続を進める。上限に達しても例外にはしない。
    /// </remarks>
    private static async Task PumpUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < PumpLimit && !condition(); i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Yield();
        }
    }

    /// <summary>保留中の描画ジョブを消化する（計測の区切り用）。</summary>
    private static async Task PumpAsync()
    {
        for (var i = 0; i < PumpOnce; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Yield();
        }
    }

    /// <summary>Dispatcher をポンプする回数の上限（完了しない場合に無限ループにしない）。</summary>
    private const int PumpLimit = 200;

    /// <summary>保留中の描画ジョブを消化する回数。</summary>
    private const int PumpOnce = 8;

    /// <summary>コントロールの座標変換ポートをリフレクションで取得する。</summary>
    /// <param name="control">対象のコントロール。</param>
    /// <returns>設定されている座標変換ポート。</returns>
    /// <remarks>
    /// DragRect はポートをコアの状態管理クラス（セッション）が保持するため、<c>_session.State.Port</c> を辿る。<br/>
    /// 他のコントロール（FadeMask / Loupe）は自前の <c>_coordinatePort</c> フィールドを保持する。
    /// </remarks>
    private static ICoordTransform GetCoordinatePort(Control control)
    {
        var sessionField = control.GetType().GetField(
            "_session", BindingFlags.NonPublic | BindingFlags.Instance);
        if (sessionField is not null)
        {
            var session = sessionField.GetValue(control)!;
            var state = session.GetType().GetProperty("State")!.GetValue(session)!;
            return (ICoordTransform)state.GetType().GetProperty("Port")!.GetValue(state)!;
        }

        var field = control.GetType().GetField(
            "_coordinatePort", BindingFlags.NonPublic | BindingFlags.Instance);
        return (ICoordTransform)field!.GetValue(control)!;
    }

    // ====================================================================
    // HasSelection / SelectionRect / AttachScrollViewer / InvalidateOverlay
    // ====================================================================

    [AvaloniaFact]
    public void HasSelection_選択状態を反映する()
    {
        var control = new FadeMaskDragRectControl();

        // 未選択時: HasSelection = false / SelectionRect は空矩形
        Assert.False(control.HasSelection);
        Assert.Equal(default, control.SelectionRect);

        // 選択時: HasSelection = true かつ矩形が同期される（DragRect.SetSelection → SelectionChanged → 同期）
        control.DragRect.SetSelection(new Rect(10, 10, 50, 40));
        Assert.True(control.HasSelection);
        Assert.Equal(new Rect(10, 10, 50, 40), control.SelectionRect);
    }

    [AvaloniaFact]
    public void SetDragRectSelection_DragRectの選択状態を設定して同期される()
    {
        var control = new FadeMaskDragRectControl();

        // プログラム的な選択設定（復元・テスト用）→ SelectionChanged 発火 → 暗転マスクとルーペが同期される
        control.SetDragRectSelection(new Rect(10, 10, 50, 40));

        Assert.True(control.HasSelection);
        Assert.Equal(new Rect(10, 10, 50, 40), control.SelectionRect);
        Assert.Equal(new Rect(10, 10, 50, 40), control.Overlay.LogicalSelectionRect);
    }

    [AvaloniaFact]
    public void AttachScrollViewer_DragRectへ明示注入される()
    {
        var control = new FadeMaskDragRectControl();
        var scrollViewer = new ScrollViewer();

        // 例外なく設定できる（DragRect.AttachScrollViewer へ委譲）
        control.AttachScrollViewer(scrollViewer);
        control.AttachScrollViewer(null);
    }

    [AvaloniaFact]
    public void InvalidateOverlay_オーバーレイの再描画を要求する()
    {
        var control = new FadeMaskDragRectControl();

        // 例外なく再描画を要求できる
        control.InvalidateOverlay();
    }

    [AvaloniaFact]
    public void NotifyViewportChanged_選択状態を維持したまま再描画を要求する()
    {
        var control = new FadeMaskDragRectControl();
        control.DragRect.SetSelection(new Rect(10, 10, 50, 40));

        // ビューポート（パン・ズーム）変化の通知 → 例外なく再描画・再計算できる
        control.NotifyViewportChanged();

        // 選択状態は維持される（再描画要求のみ）
        Assert.True(control.HasSelection);
        Assert.Equal(new Rect(10, 10, 50, 40), control.SelectionRect);
    }

    [AvaloniaFact]
    public void ViewportChanged_イベントで選択状態を維持したまま再描画が要求される()
    {
        var control = new FadeMaskDragRectControl();
        control.DragRect.SetSelection(new Rect(10, 10, 50, 40));

        // DragRect の ViewportChanged を発火（ScrollViewer 連携の ScrollChanged → ViewportChanged の模擬）。
        // 配線は NotifyViewportChanged に統合されている（選択状態の再同期は行わず、再描画のみ要求）
        var field = typeof(DragRectControl).GetField(
            "ViewportChanged", BindingFlags.Instance | BindingFlags.NonPublic);
        var eventDelegate = (MulticastDelegate?)field?.GetValue(control.DragRect);
        eventDelegate?.DynamicInvoke(control.DragRect,
            new SelectionRectEventArgs(new Rect(10, 10, 50, 40), control.DragRect.CurrentSnapshot));

        // 選択状態は維持される（再描画要求のみ）
        Assert.True(control.HasSelection);
        Assert.Equal(new Rect(10, 10, 50, 40), control.SelectionRect);
    }

    [AvaloniaFact]
    public void ViewportChanged_イベントでルーペ位置が再計算される()
    {
        var control = new FadeMaskDragRectControl();
        control.DragRect.SetSelection(new Rect(10, 10, 50, 40));

        // ルーペの表示位置計算（UpdatePosition）は親 Panel 接続 + IsVisible が条件のため、
        // ウィンドウに配置してツリーへ接続し、AXAML 既定（IsVisible=False）を明示的に表示へ切り替えてから中心を設定する
        var window = new Window { Content = control };
        window.Show();
        control.Loupe.IsVisible = true;
        control.Loupe.CenterPoint = new Point(50, 50);
        var before = control.Loupe.RenderTransform;
        Assert.NotNull(before); // 接続後に CenterPoint 設定の位置計算で RenderTransform が生成される

        // DragRect の ViewportChanged を発火（ScrollViewer 連携のスクロール変化の模擬）
        var field = typeof(DragRectControl).GetField(
            "ViewportChanged", BindingFlags.Instance | BindingFlags.NonPublic);
        var eventDelegate = (MulticastDelegate?)field?.GetValue(control.DragRect);
        eventDelegate?.DynamicInvoke(control.DragRect,
            new SelectionRectEventArgs(new Rect(10, 10, 50, 40), control.DragRect.CurrentSnapshot));

        // ルーペ表示位置が再計算される（NotifyViewportChanged → Loupe.RefreshPosition 経由で RenderTransform が更新される。
        // TranslateTransform は同一インスタンスの値更新のため Same で検証）
        Assert.NotNull(control.Loupe.RenderTransform);
        Assert.Same(before, control.Loupe.RenderTransform);
        window.Close();
    }

    // ====================================================================
    // SelectionStarted / SelectionCancelled（選択状態のクリア）
    // ====================================================================

    [AvaloniaFact]
    public void SelectionStarted_選択状態がクリアされる()
    {
        var control = new FadeMaskDragRectControl();
        control.DragRect.SetSelection(new Rect(10, 10, 50, 40));
        Assert.True(control.Overlay.HasSelection);

        // DragRect.SetSelection の再設定（SelectionStarted は発火しない）のため、
        // リフレクションで SelectionStarted イベントを直接発火する（クリアの検証用）
        var field = typeof(DragRectControl).GetField(
            "SelectionStarted", BindingFlags.Instance | BindingFlags.NonPublic);
        var eventDelegate = (MulticastDelegate?)field?.GetValue(control.DragRect);
        eventDelegate?.DynamicInvoke(control.DragRect,
            new DragRectPointEventArgs(new Point(0, 0), control.DragRect.CurrentSnapshot));

        Assert.False(control.Overlay.HasSelection);
    }

    [AvaloniaFact]
    public void SelectionCancelled_選択状態がクリアされる()
    {
        var control = new FadeMaskDragRectControl();
        control.DragRect.SetSelection(new Rect(10, 10, 50, 40));
        Assert.True(control.Overlay.HasSelection);

        // リフレクションで SelectionCancelled イベントを直接発火する（クリアの検証用）
        var field = typeof(DragRectControl).GetField(
            "SelectionCancelled", BindingFlags.Instance | BindingFlags.NonPublic);
        var eventDelegate = (MulticastDelegate?)field?.GetValue(control.DragRect);
        eventDelegate?.DynamicInvoke(control.DragRect, EventArgs.Empty);

        Assert.False(control.Overlay.HasSelection);
    }

    // ====================================================================
    // LoupePositionMoved（ホストへのイベント中継）
    // ====================================================================

    [AvaloniaFact]
    public void LoupePositionMoved_DragRectのルーペ位置更新が中継される()
    {
        var control = new FadeMaskDragRectControl();
        var window = new Window { Content = new Panel { Children = { control } } };
        window.Show();
        control.Loupe.Source = EsBitmapAvaloniaExtensions.CreateAvaloniaImage(10, 10, Colors.Transparent);

        FadeMaskDragRectPointEventArgs? received = null;
        control.LoupePositionMoved += (_, e) => received = e;

        // リフレクションで LoupePositionMoved イベントを直接発火する（中継の検証用）
        var field = typeof(DragRectControl).GetField(
            "LoupePositionMoved", BindingFlags.Instance | BindingFlags.NonPublic);
        var eventDelegate = (MulticastDelegate?)field?.GetValue(control.DragRect);
        var args = new DragRectPointEventArgs(new Point(100, 200), control.DragRect.CurrentSnapshot);
        eventDelegate?.DynamicInvoke(control.DragRect, args);

        // ルーペ中心が更新され、ホストへ独自イベント引数（位置）で中継される
        Assert.Equal(new Point(100, 200), control.Loupe.CenterPoint);
        Assert.True(control.Loupe.IsVisible);
        Assert.NotNull(received);
        Assert.Equal(new Point(100, 200), received!.Position);

        window.Close();
    }

    [AvaloniaFact]
    public void LoupePositionMoved_購読者がいない場合は中継されない()
    {
        var control = new FadeMaskDragRectControl();

        // リフレクションで LoupePositionMoved イベントを直接発火する（購読者なし → ?.Invoke の null 分岐）
        var field = typeof(DragRectControl).GetField(
            "LoupePositionMoved", BindingFlags.Instance | BindingFlags.NonPublic);
        var eventDelegate = (MulticastDelegate?)field?.GetValue(control.DragRect);
        var args = new DragRectPointEventArgs(new Point(100, 200), control.DragRect.CurrentSnapshot);
        eventDelegate?.DynamicInvoke(control.DragRect, args);

        // 購読者がいないため例外なく完了する（ルーペ中心は更新されるが表示はしない）
        Assert.Equal(new Point(100, 200), control.Loupe.CenterPoint);
    }

    // ====================================================================
    // 実ポインターイベント（矩形外クリック・小さすぎるドラッグ → 選択状態のクリア）
    // ====================================================================

    /// <summary>コントロールローカル座標をスクリーン座標（PointerEventArgs の rootPoint 用）に変換する。</summary>
    private static Point ToScreenPoint(Visual source, Point local)
    {
        var screen = source.PointToScreen(local);
        return new Point(screen.X, screen.Y);
    }

    /// <summary>左ボタン押下状態の PointerPointProperties を生成する。</summary>
    private static PointerPointProperties LeftPressed()
        => new(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);

    /// <summary>左ボタン解放状態の PointerPointProperties を生成する。</summary>
    private static PointerPointProperties LeftReleased()
        => new(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased);

    /// <summary>テスト用のポインターを生成する。</summary>
    private static Pointer CreatePointer()
        => new(1, PointerType.Mouse, true);

    [AvaloniaFact]
    public void 矩形外クリックでSelectionStartedにより選択状態がクリアされる()
    {
        var control = new FadeMaskDragRectControl { Mode = FadeMaskDragRectMode.Crop };
        var window = new Window { Content = control, Width = 400, Height = 300 };
        window.Show();
        try
        {
            control.SetDragRectSelection(new Rect(10, 10, 50, 40));
            Assert.True(control.HasSelection);

            // 矩形外クリック（ポインター押下 → 新規選択開始 = SelectionStarted 発火）で選択状態がクリアされる。
            // 押下位置 (300,200) は選択矩形 (10,10,50,40) の外側ハンドル帯（HandleHitRadius=12）からも離れている
            var pointer = CreatePointer();
            var pressed = new PointerPressedEventArgs(control.DragRect, pointer, window,
                ToScreenPoint(control.DragRect, new Point(300, 200)), 0UL, LeftPressed(), KeyModifiers.None, 1);
            control.DragRect.RaiseEvent(pressed);

            Assert.False(control.HasSelection);
            Assert.Equal(default, control.SelectionRect);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void 小さすぎるドラッグでSelectionCancelledにより選択状態がクリアされる()
    {
        var control = new FadeMaskDragRectControl { Mode = FadeMaskDragRectMode.Crop };
        var window = new Window { Content = control };
        window.Show();
        try
        {
            // 新規選択ドラッグ（3px 移動 = MinSelectionSize 未満 → キャンセル = SelectionCancelled 発火）
            var pointer = CreatePointer();
            var pressed = new PointerPressedEventArgs(control.DragRect, pointer, window,
                ToScreenPoint(control.DragRect, new Point(20, 20)), 0UL, LeftPressed(), KeyModifiers.None, 1);
            control.DragRect.RaiseEvent(pressed);
            // ドラッグ中（SelectionChanged で選択状態が設定される）
            var moved = new PointerEventArgs(InputElement.PointerMovedEvent, control.DragRect, pointer, window,
                ToScreenPoint(control.DragRect, new Point(23, 23)), 0UL, LeftPressed(), KeyModifiers.None);
            control.DragRect.RaiseEvent(moved);
            // 解放（3px = MinSelectionSize 未満 → SelectionCancelled 発火 → 選択状態クリア）
            var released = new PointerReleasedEventArgs(control.DragRect, pointer, window,
                ToScreenPoint(control.DragRect, new Point(23, 23)), 0UL, LeftReleased(), KeyModifiers.None, MouseButton.Left);
            control.DragRect.RaiseEvent(released);

            Assert.False(control.HasSelection);
            Assert.Equal(default, control.SelectionRect);
        }
        finally
        {
            window.Close();
        }
    }

    // ====================================================================
    // SetLoupeSource / ClearLoupe / LoupeCenterPoint / FocusDragRect
    // ====================================================================

    [AvaloniaFact]
    public void SetLoupeSource_ルーペのSourceが設定される()
    {
        var control = new FadeMaskDragRectControl();
        var bitmap = EsBitmapAvaloniaExtensions.CreateAvaloniaImage(10, 10, Colors.Transparent);

        control.SetLoupeSource(bitmap);

        Assert.Same(bitmap, control.Loupe.Source);
    }

    [AvaloniaFact]
    public void SetLoupeSource_解除すると非表示になる()
    {
        var control = new FadeMaskDragRectControl();
        control.SetLoupeSource(EsBitmapAvaloniaExtensions.CreateAvaloniaImage(10, 10, Colors.Transparent));
        control.SetLoupePosition(new Point(10, 10));
        Assert.True(control.Loupe.IsVisible);

        // 拡大元画像を解除すると表示を続けない（表示可否はコアが判断する）
        control.SetLoupeSource(null);

        Assert.False(control.Loupe.IsVisible);
    }

    [AvaloniaFact]
    public void ClearLoupe_ルーペが非表示になる()
    {
        var control = new FadeMaskDragRectControl();
        var window = new Window { Content = control };
        window.Show();
        control.SetLoupeSource(EsBitmapAvaloniaExtensions.CreateAvaloniaImage(10, 10, Colors.Transparent));
        control.SetLoupePosition(new Point(10, 10));
        Assert.True(control.Loupe.IsVisible);

        control.ClearLoupe();

        // Loupe.Clear は非表示（+ Bitmap 解放）を担う（IsVisible のみ公開検証。Bitmap 解放は LoupeControl の責務）
        Assert.False(control.Loupe.IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void LoupeCenterPoint_設定済みの中心を返す()
    {
        var control = new FadeMaskDragRectControl();
        var window = new Window { Content = control };
        window.Show();

        Assert.Null(control.LoupeCenterPoint);
        control.SetLoupePosition(new Point(100, 200));
        Assert.Equal(new Point(100, 200), control.LoupeCenterPoint);
        window.Close();
    }

    [AvaloniaFact]
    public void FocusDragRect_DragRectへフォーカスを設定する()
    {
        var control = new FadeMaskDragRectControl();
        var window = new Window { Content = control };
        window.Show();

        // 例外なくフォーカスを設定できる（ヘッドレス環境では IsKeyboardFocusWithin の検証は省略）
        control.FocusDragRect();
        window.Close();
    }

    // ====================================================================
    // ハイライト状態（ScreenSelect / WindowSelect 用）
    // ====================================================================

    [AvaloniaFact]
    public void ハイライトプロパティ_暗転マスクの状態へ反映される()
    {
        var control = new FadeMaskDragRectControl();

        // 未設定時: 既定値
        Assert.Null(control.LogicalHighlightRect);
        Assert.False(control.HasHighlight);
        Assert.Equal("", control.WindowTitle);

        // 設定時: 暗転マスク（FadeMaskControl）へ反映される
        control.LogicalHighlightRect = new Rect(100, 50, 1920, 1080);
        control.HasHighlight = true;
        control.DisplayScaleScaling = 1.5;
        control.WindowTitle = "Sample Window";

        Assert.Equal(new Rect(100, 50, 1920, 1080), control.Overlay.LogicalHighlightRect);
        Assert.True(control.Overlay.HasHighlight);
        Assert.Equal(1.5, control.Overlay.DisplayScaleScaling);
        Assert.Equal("Sample Window", control.Overlay.WindowTitle);
        // コントロール経由の getter も検証する（暗転マスクへの委譲）
        Assert.Equal(1.5, control.DisplayScaleScaling);
        Assert.Equal("Sample Window", control.WindowTitle);
    }

    [AvaloniaFact]
    public void ShowFullOverlay_モードに応じた全面暗転状態を返す()
    {
        // Crop / AreaSelect は全面暗転あり、ScreenSelect / WindowSelect はなし
        Assert.True(new FadeMaskDragRectControl { Mode = FadeMaskDragRectMode.Crop }.ShowFullOverlay);
        Assert.True(new FadeMaskDragRectControl { Mode = FadeMaskDragRectMode.AreaSelect }.ShowFullOverlay);
        Assert.False(new FadeMaskDragRectControl { Mode = FadeMaskDragRectMode.ScreenSelect }.ShowFullOverlay);
        Assert.False(new FadeMaskDragRectControl { Mode = FadeMaskDragRectMode.WindowSelect }.ShowFullOverlay);
    }

    // ====================================================================
    // ルーペの選択矩形破線オーバーレイ（Rendered イベント経由・ホスト側描画）
    // ====================================================================
    // 選択矩形破線オーバーレイの描画は純粋な静的メソッド（DrawSelectionRectOverlay）のため、
    // EsBitmap のみを使用し、ヘッドレス不要で並列実行できるテスト（FadeMaskDragRectOverlayTests）で検証する。
}
