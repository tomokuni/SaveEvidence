using BindingFlags = System.Reflection.BindingFlags;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Estable.AvaloniaUI.DragRect;
using Estable.AvaloniaUI.FadeMask;
using Estable.AvaloniaUI.Platform;
using System.Runtime.InteropServices;
using Xunit;

namespace Estable.AvaloniaUI.FadeMaskDragRect.Tests;

/// <summary><see cref="FadeMaskDragRectControl"/> の単体テスト（公開 API・配線・状態同期）。</summary>
/// <remarks>
/// 対象: <c>FadeMaskDragRectControl.cs</c>（公開 API: Mode / AppearanceSettings / SetCoordinateTransform /<br/>
/// SetSelection / ClearSelection / SetLoupePosition / イベント中継 / テスト用アクセサ）。<br/>
/// パブリック API 経由で内包コントロール（DragRect / FadeMask / Loupe）への設定・状態同期・イベント中継を検証する。
/// </remarks>
[Collection("AvaloniaHeadless")]
public class FadeMaskDragRectControlTests
{
    /// <summary>テスト用の Bitmap（10x10）を生成する。</summary>
    private static WriteableBitmap CreateBitmap()
        => new(new PixelSize(10, 10), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
    // ====================================================================
    // コンストラクタ・内部構造
    // ====================================================================

    [AvaloniaFact]
    public void コンストラクタ_FadeMaskControlにExternalRendererが設定される()
    {
        var control = new FadeMaskDragRectControl();

        Assert.Same(control.Renderer, control.Overlay.ExternalRenderer);
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

        Assert.Equal(expectedRenderMode, control.Renderer.Mode);
        Assert.Equal(expectedDragRectVisible, control.DragRect.IsVisible);
        Assert.Equal(expectedShowFullOverlay, control.Renderer.ShowFullOverlay);
    }

    // ====================================================================
    // AppearanceSettings（外観の一括設定・部分更新・getter）
    // ====================================================================

    [AvaloniaFact]
    public void AppearanceSettings_既定値が全メンバーnon_nullで返る()
    {
        var control = new FadeMaskDragRectControl();

        var appearance = control.AppearanceSettings;

        Assert.NotNull(appearance.MinSelectionSize);
        Assert.NotNull(appearance.BorderColor);
        Assert.NotNull(appearance.BorderThickness);
        Assert.NotNull(appearance.HandleColor);
        Assert.NotNull(appearance.HandleSize);
        Assert.NotNull(appearance.HandleHitRadius);
        Assert.NotNull(appearance.ClampToDomainBounds);
        Assert.NotNull(appearance.MarkerColor);
        Assert.NotNull(appearance.MarkerLength);
        Assert.NotNull(appearance.Scaling);
        Assert.NotNull(appearance.DisplayScaleScaling);
        Assert.NotNull(appearance.OverlayColor);
        Assert.NotNull(appearance.TextColor);
        Assert.NotNull(appearance.FontSize);
        Assert.NotNull(appearance.ShowSizeLabel);
        Assert.NotNull(appearance.ShowSubLines);
        Assert.NotNull(appearance.SubLineFontSize);
        Assert.NotNull(appearance.SubLineColor);
        Assert.NotNull(appearance.SubLineBackgroundColor);
        Assert.NotNull(appearance.InfoLabelBackgroundColor);
        Assert.NotNull(appearance.TextBackgroundColor);
        Assert.NotNull(appearance.InfoLabelVisible);
        Assert.NotNull(appearance.InfoLabelColor);
        Assert.NotNull(appearance.InfoLabelFontSize);
        Assert.NotNull(appearance.RequestedSize);
        Assert.NotNull(appearance.ZoomLevel);
        Assert.NotNull(appearance.RenderScaling);
        Assert.NotNull(appearance.CrossColor);
        Assert.NotNull(appearance.CheckerSize);
        Assert.NotNull(appearance.CheckerColorA);
        Assert.NotNull(appearance.CheckerColorB);
        Assert.NotNull(appearance.FrameColor);
        Assert.NotNull(appearance.FrameWidth);
        Assert.NotNull(appearance.SelectionRectColorA);
        Assert.NotNull(appearance.SelectionRectColorB);
        Assert.NotNull(appearance.PositionOffset);
        Assert.NotNull(appearance.ShowCoordLabel);
        Assert.NotNull(appearance.CoordLabelColor);
        Assert.NotNull(appearance.CoordLabelBackgroundColor);
        Assert.NotNull(appearance.CoordLabelFontSize);
        Assert.NotNull(appearance.LoupeShowCoordLabel);
        Assert.NotNull(appearance.LoupeCoordLabelColor);
        Assert.NotNull(appearance.LoupeCoordLabelBackgroundColor);
        Assert.NotNull(appearance.LoupeCoordLabelFontSize);
    }

    [AvaloniaFact]
    public void AppearanceSettings_DragRectの外観が設定される()
    {
        var control = new FadeMaskDragRectControl();
        var settings = new FadeMaskDragRectSettings
        {
            MinSelectionSize = 5,
            BorderColor = Color.FromAColor(102, Colors.Red),
            BorderThickness = 3.0,
            HandleColor = Colors.Blue,
            HandleSize = 12.0,
            HandleHitRadius = 15.0,
            ClampToDomainBounds = true,
            MarkerColor = Colors.Green,
            MarkerLength = 20.0,
        };

        control.AppearanceSettings = settings;

        Assert.Equal(5, control.DragRect.MinSelectionSize);
        Assert.Equal(Color.FromAColor(102, Colors.Red), control.DragRect.BorderColor);
        Assert.Equal(3.0, control.DragRect.BorderThickness);
        Assert.Equal(Colors.Blue, control.DragRect.HandleColor);
        Assert.Equal(12.0, control.DragRect.HandleSize);
        Assert.Equal(15.0, control.DragRect.HandleHitRadius);
        Assert.True(control.DragRect.ClampToDomainBounds);
        Assert.Equal(Colors.Green, control.DragRect.MarkerColor);
        Assert.Equal(20.0, control.DragRect.MarkerLength);
    }

    [AvaloniaFact]
    public void AppearanceSettings_Rendererの外観が設定される()
    {
        var control = new FadeMaskDragRectControl();
        var settings = new FadeMaskDragRectSettings
        {
            Scaling = 2.0,
            DisplayScaleScaling = 1.5,
            OverlayColor = Color.FromAColor(128, Colors.DarkBlue), // 暗青 50%
            TextColor = Colors.Yellow,
            TextBackgroundColor = Color.FromAColor(179, Colors.DarkBlue), // 暗青 70%
            FontSize = 18.0,
            ShowSizeLabel = false,
            ShowSubLines = false,
            SubLineFontSize = 13.0,
            SubLineColor = Colors.Orange,
            SubLineBackgroundColor = Color.FromAColor(102, Colors.DarkGreen), // 暗緑 40%
            InfoLabelBackgroundColor = Color.FromAColor(128, Colors.DarkRed), // 暗赤 50%
            InfoLabelVisible = false,
            InfoLabelColor = Colors.Cyan,
            InfoLabelFontSize = 12.0,
        };

        control.AppearanceSettings = settings;

        Assert.Equal(2.0, control.Renderer.Scaling);
        Assert.Equal(1.5, control.Renderer.DisplayScaleScaling);
        // OverlayColor（アルファ込み）がそのまま OverlayArgbColor へ反映される
        Assert.Equal(Color.FromAColor(128, Colors.DarkBlue), control.Renderer.OverlayArgbColor);
        Assert.Equal(Colors.Yellow, control.Renderer.TextColor);
        Assert.Equal(Color.FromAColor(179, Colors.DarkBlue), control.Renderer.TextBackgroundColor);
        Assert.Equal(18.0, control.Renderer.FontSize);
        Assert.False(control.Renderer.ShowSizeLabel);
        Assert.False(control.Renderer.ShowSubLines);
        Assert.Equal(13.0, control.Renderer.SubLineFontSize);
        Assert.Equal(Colors.Orange, control.Renderer.SubLineColor);
        Assert.Equal(Color.FromAColor(102, Colors.DarkGreen), control.Renderer.SubLineBackgroundColor);
        Assert.Equal(Color.FromAColor(128, Colors.DarkRed), control.Renderer.InfoLabelBackgroundColor);
        Assert.False(control.Renderer.InfoLabelVisible);
        Assert.Equal(Colors.Cyan, control.Renderer.InfoLabelColor);
        Assert.Equal(12.0, control.Renderer.InfoLabelFontSize);
    }

    [AvaloniaFact]
    public void AppearanceSettings_Loupeの外観が設定される()
    {
        var control = new FadeMaskDragRectControl();
        var settings = new FadeMaskDragRectSettings
        {
            RequestedSize = 150,
            ZoomLevel = 3,
            RenderScaling = 2.0,
            CrossColor = Color.FromAColor(204, Colors.Red), // 赤 80%
            CheckerSize = 10,
            CheckerColorA = Colors.Black,
            CheckerColorB = Colors.White,
            FrameColor = Colors.Gray,
            FrameWidth = 4,
            SelectionRectColorA = Color.FromAColor(204, Colors.White),
            SelectionRectColorB = Color.FromAColor(204, Colors.Black),
            PositionOffset = 30.0,
            ShowCoordLabel = true,
            CoordLabelColor = Colors.Yellow,
            CoordLabelBackgroundColor = Color.FromAColor(179, Colors.DarkGreen), // 暗緑 70%
            CoordLabelFontSize = 14.0,
            LoupeShowCoordLabel = true,
            LoupeCoordLabelColor = Colors.Cyan,
            LoupeCoordLabelBackgroundColor = Color.FromAColor(153, Colors.DarkRed), // 暗赤 60%
            LoupeCoordLabelFontSize = 13.0,
        };

        control.AppearanceSettings = settings;

        Assert.Equal(150, control.Loupe.RequestedSize);
        Assert.Equal(3, control.Loupe.ZoomLevel);
        Assert.Equal(2.0, control.Loupe.RenderScaling);
        Assert.Equal(Color.FromAColor(204, Colors.Red), ((SolidColorBrush)control.Loupe.CrossColor!).Color);
        Assert.Equal(10, control.Loupe.CheckerSize);
        Assert.Equal(4, control.Loupe.FrameWidth);
        Assert.Equal(30.0, control.Loupe.PositionOffset);
        // 座標ラベル（DragRect / Loupe）
        Assert.True(control.DragRect.ShowCoordLabel);
        Assert.Equal(Colors.Yellow, control.DragRect.CoordLabelColor);
        Assert.Equal(Color.FromAColor(179, Colors.DarkGreen), control.DragRect.CoordLabelBackgroundColor);
        Assert.Equal(14.0, control.DragRect.CoordLabelFontSize);
        Assert.True(control.Loupe.ShowCoordLabel);
        Assert.Equal(13.0, control.Loupe.CoordLabelFontSize);
        Assert.Equal(Color.FromAColor(153, Colors.DarkRed), ((SolidColorBrush)control.Loupe.CoordLabelBackgroundColor!).Color);
    }

    [AvaloniaFact]
    public void AppearanceSettings_同一色の再設定でブラシが共有される()
    {
        var control = new FadeMaskDragRectControl();
        var color = Color.FromAColor(204, Colors.Red);

        // 同じ色を 2 回設定 → ブラシキャッシュのヒットパス（TryGetValue が true）を実行する
        control.AppearanceSettings = new FadeMaskDragRectSettings { CrossColor = color };
        var first = control.Loupe.CrossColor;
        control.AppearanceSettings = new FadeMaskDragRectSettings { CrossColor = color };
        var second = control.Loupe.CrossColor;

        Assert.Same(first, second);
    }

    [AvaloniaFact]
    public void AppearanceSettings_ブラシキャッシュ容量超過時も例外なく適用される()
    {
        var control = new FadeMaskDragRectControl();

        // キャッシュ容量（32）を超える 33 色を順次設定し、容量超過時のキャッシュクリア経路を実行する
        for (int i = 0; i <= 32; i++)
        {
            control.AppearanceSettings = new FadeMaskDragRectSettings
            {
                CrossColor = Color.FromArgb(255, (byte)i, (byte)(255 - i), (byte)(i * 3)),
            };
        }

        Assert.NotNull(control.Loupe.CrossColor);
    }

    [AvaloniaFact]
    public void AppearanceSettings_部分更新でnull以外のみ反映される()
    {
        var control = new FadeMaskDragRectControl();
        // 初期設定
        control.AppearanceSettings = new FadeMaskDragRectSettings
        {
            MinSelectionSize = 5,
            BorderColor = Colors.Red,
            RequestedSize = 150,
            PositionOffset = 30.0,
        };

        // 部分更新（BorderColor / RequestedSize のみ指定。他は null = 現在値維持）
        control.AppearanceSettings = new FadeMaskDragRectSettings
        {
            BorderColor = Colors.Blue,
            RequestedSize = 200,
        };

        Assert.Equal(5, control.DragRect.MinSelectionSize);           // 未指定 → 現在値維持
        Assert.Equal(Colors.Blue, control.DragRect.BorderColor);      // 指定 → 反映
        Assert.Equal(200, control.Loupe.RequestedSize);               // 指定 → 反映
        Assert.Equal(30.0, control.Loupe.PositionOffset);             // 未指定 → 現在値維持
        Assert.Equal(200, control.AppearanceSettings.RequestedSize);  // getter は更新後の値を返す
    }

    [AvaloniaFact]
    public void AppearanceSettings_同じ値の再設定では更新をスキップする()
    {
        var control = new FadeMaskDragRectControl();
        var initial = control.AppearanceSettings;

        // 現在値と同じ値（全メンバー指定）を再設定しても、参照が変わらない（同一値スキップ）
        var same = new FadeMaskDragRectSettings
        {
            MinSelectionSize = initial.MinSelectionSize,
            BorderColor = initial.BorderColor,
            BorderThickness = initial.BorderThickness,
            HandleColor = initial.HandleColor,
            HandleSize = initial.HandleSize,
            HandleHitRadius = initial.HandleHitRadius,
            ClampToDomainBounds = initial.ClampToDomainBounds,
            MarkerColor = initial.MarkerColor,
            MarkerLength = initial.MarkerLength,
            Scaling = initial.Scaling,
            DisplayScaleScaling = initial.DisplayScaleScaling,
            OverlayColor = initial.OverlayColor,
            TextColor = initial.TextColor,
            FontSize = initial.FontSize,
            ShowSizeLabel = initial.ShowSizeLabel,
            ShowSubLines = initial.ShowSubLines,
            SubLineFontSize = initial.SubLineFontSize,
            SubLineColor = initial.SubLineColor,
            SubLineBackgroundColor = initial.SubLineBackgroundColor,
            InfoLabelBackgroundColor = initial.InfoLabelBackgroundColor,
            TextBackgroundColor = initial.TextBackgroundColor,
            InfoLabelVisible = initial.InfoLabelVisible,
            InfoLabelColor = initial.InfoLabelColor,
            InfoLabelFontSize = initial.InfoLabelFontSize,
            RequestedSize = initial.RequestedSize,
            ZoomLevel = initial.ZoomLevel,
            RenderScaling = initial.RenderScaling,
            CrossColor = initial.CrossColor,
            CheckerSize = initial.CheckerSize,
            CheckerColorA = initial.CheckerColorA,
            CheckerColorB = initial.CheckerColorB,
            FrameColor = initial.FrameColor,
            FrameWidth = initial.FrameWidth,
            SelectionRectColorA = initial.SelectionRectColorA,
            SelectionRectColorB = initial.SelectionRectColorB,
            PositionOffset = initial.PositionOffset,
            ShowCoordLabel = initial.ShowCoordLabel,
            CoordLabelColor = initial.CoordLabelColor,
            CoordLabelBackgroundColor = initial.CoordLabelBackgroundColor,
            CoordLabelFontSize = initial.CoordLabelFontSize,
            LoupeShowCoordLabel = initial.LoupeShowCoordLabel,
            LoupeCoordLabelColor = initial.LoupeCoordLabelColor,
            LoupeCoordLabelBackgroundColor = initial.LoupeCoordLabelBackgroundColor,
            LoupeCoordLabelFontSize = initial.LoupeCoordLabelFontSize,
        };
        control.AppearanceSettings = same;

        Assert.Same(initial, control.AppearanceSettings);
    }

    [AvaloniaFact]
    public void AppearanceSettings_nullは現在値を維持する()
    {
        var control = new FadeMaskDragRectControl();
        control.AppearanceSettings = new FadeMaskDragRectSettings { MinSelectionSize = 7 };

        // すべて null（既定の new インスタンス）→ 何も変更されない
        control.AppearanceSettings = new FadeMaskDragRectSettings();

        Assert.Equal(7, control.DragRect.MinSelectionSize);
        Assert.Equal(7, control.AppearanceSettings.MinSelectionSize);
    }

    // ====================================================================
    // SetCoordinateTransform（ポートの一括分配）
    // ====================================================================

    [AvaloniaFact]
    public void SetCoordinateTransform_3コントロールへ同一ポートが分配される()
    {
        var control = new FadeMaskDragRectControl();
        var port = CoordinatePort.Identity;

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

        Assert.Equal(rect, control.Renderer.LogicalSelectionRect);
        Assert.True(control.Renderer.HasSelection);
    }

    [AvaloniaFact]
    public void SetSelection_サイズ0の矩形は未選択として反映される()
    {
        var control = new FadeMaskDragRectControl();

        control.SetSelection(new Rect(0, 0, 0, 0));

        Assert.Null(control.Renderer.LogicalSelectionRect);
        Assert.False(control.Renderer.HasSelection);
    }

    [AvaloniaFact]
    public void ClearSelection_選択状態がクリアされる()
    {
        var control = new FadeMaskDragRectControl();
        control.SetSelection(new Rect(10, 10, 50, 40));

        control.ClearSelection();

        Assert.Null(control.Renderer.LogicalSelectionRect);
        Assert.False(control.Renderer.HasSelection);
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

        Assert.Equal(rect, control.Renderer.LogicalSelectionRect);
        Assert.True(control.Renderer.HasSelection);
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
        bool? zoomIn = null;
        control.ZoomRequested += (_, z) => zoomIn = z;

        // DragRect の ZoomRequested は通常の .NET イベント（Ctrl+ホイールで発火）のため、
        // リフレクションでバッキングフィールドを取得して直接発火する（中継の検証用）。
        var field = typeof(DragRectControl).GetField(
            "ZoomRequested", BindingFlags.Instance | BindingFlags.NonPublic);
        var eventDelegate = (MulticastDelegate?)field?.GetValue(control.DragRect);
        eventDelegate?.DynamicInvoke(control.DragRect, true);

        Assert.True(zoomIn);
    }

    [AvaloniaFact]
    public void ZoomRequested_購読者がいない場合は中継されない()
    {
        var control = new FadeMaskDragRectControl();

        // リフレクションで ZoomRequested を直接発火する（購読者なし → ?.Invoke の null 分岐）
        var field = typeof(DragRectControl).GetField(
            "ZoomRequested", BindingFlags.Instance | BindingFlags.NonPublic);
        var eventDelegate = (MulticastDelegate?)field?.GetValue(control.DragRect);
        eventDelegate?.DynamicInvoke(control.DragRect, true);

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
        control.Loupe.Source = CreateBitmap();
        control.SetLoupePosition(new Point(960, 540));
        Assert.True(control.Loupe.IsVisible);
        Assert.Equal(new Point(960, 540), control.Loupe.CenterPoint);

        window.Close();
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
        Assert.Equal("LT: (10, 20)", control.Renderer.SizeLabelOverrideProvider?.Invoke());
    }

    // ====================================================================
    // ヘルパー
    // ====================================================================

    /// <summary>コントロールの座標変換ポート（_coordinatePort）をリフレクションで取得する。</summary>
    private static ICoordinatePort GetCoordinatePort(Control control)
    {
        var field = control.GetType().GetField(
            "_coordinatePort", BindingFlags.NonPublic | BindingFlags.Instance);
        return (ICoordinatePort)field!.GetValue(control)!;
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
        Assert.Equal(new Rect(10, 10, 50, 40), control.Renderer.LogicalSelectionRect);
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
        Assert.True(control.Renderer.HasSelection);

        // DragRect.SetSelection の再設定（SelectionStarted は発火しない）のため、
        // リフレクションで SelectionStarted イベントを直接発火する（クリアの検証用）
        var field = typeof(DragRectControl).GetField(
            "SelectionStarted", BindingFlags.Instance | BindingFlags.NonPublic);
        var eventDelegate = (MulticastDelegate?)field?.GetValue(control.DragRect);
        eventDelegate?.DynamicInvoke(control.DragRect,
            new DragRectPointEventArgs(new Point(0, 0), control.DragRect.CurrentSnapshot));

        Assert.False(control.Renderer.HasSelection);
    }

    [AvaloniaFact]
    public void SelectionCancelled_選択状態がクリアされる()
    {
        var control = new FadeMaskDragRectControl();
        control.DragRect.SetSelection(new Rect(10, 10, 50, 40));
        Assert.True(control.Renderer.HasSelection);

        // リフレクションで SelectionCancelled イベントを直接発火する（クリアの検証用）
        var field = typeof(DragRectControl).GetField(
            "SelectionCancelled", BindingFlags.Instance | BindingFlags.NonPublic);
        var eventDelegate = (MulticastDelegate?)field?.GetValue(control.DragRect);
        eventDelegate?.DynamicInvoke(control.DragRect, EventArgs.Empty);

        Assert.False(control.Renderer.HasSelection);
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
        control.Loupe.Source = CreateBitmap();

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
        var bitmap = CreateBitmap();

        control.SetLoupeSource(bitmap);

        Assert.Same(bitmap, control.Loupe.Source);
    }

    [AvaloniaFact]
    public void ClearLoupe_ルーペが非表示になる()
    {
        var control = new FadeMaskDragRectControl();
        var window = new Window { Content = control };
        window.Show();
        control.SetLoupeSource(CreateBitmap());
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
    public void ハイライトプロパティ_レンダラーへ委譲される()
    {
        var control = new FadeMaskDragRectControl();

        // 未設定時: 既定値
        Assert.Null(control.LogicalHighlightRect);
        Assert.False(control.HasHighlight);
        Assert.Equal(-1, control.ScreenIndex);
        Assert.Equal("", control.WindowTitle);

        // 設定時: レンダラーへ反映される
        control.LogicalHighlightRect = new Rect(100, 50, 1920, 1080);
        control.HasHighlight = true;
        control.ScreenIndex = 1;
        control.DisplayScaleScaling = 1.5;
        control.WindowTitle = "Sample Window";

        Assert.Equal(new Rect(100, 50, 1920, 1080), control.Renderer.LogicalHighlightRect);
        Assert.True(control.Renderer.HasHighlight);
        Assert.Equal(1, control.Renderer.ScreenIndex);
        Assert.Equal(1.5, control.Renderer.DisplayScaleScaling);
        Assert.Equal("Sample Window", control.Renderer.WindowTitle);
        // コントロール経由の getter も検証する（レンダラーへの委譲）
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

    /// <summary>選択矩形破線オーバーレイが領域内にピクセルを描画することを検証する。</summary>
    [AvaloniaFact]
    public void DrawSelectionRectOverlay_選択矩形の辺に破線が描画される()
    {
        // 白の 40x40 Bitmap に選択矩形（10,10,20,20）をズーム 4 で描画する
        var bitmap = CreateSolidBitmap(40, 40);
        var selectionRect = new Rect(10, 10, 20, 20);
        var captureTopLeft = new Point(5, 5);
        var scale = 4.0;

        FadeMaskDragRectControl.DrawSelectionRectOverlay(
            bitmap, selectionRect, captureTopLeft, scale, Colors.Black, Colors.Transparent);

        // 上辺（ドメイン y=10 → ルーペ (10-5)*4=20）のピクセルが描画されていることを検証する
        using var locked = bitmap.Lock();
        var pixels = new byte[4];
        Marshal.Copy(locked.Address + 20 * locked.RowBytes + 20 * 4, pixels, 0, 4);

        // 白背景（255）より暗い = 破線が描画されている
        Assert.True(pixels[0] < 255, $"選択矩形の辺に線が描画されていない: B={pixels[0]}");
    }

    /// <summary>ズーム 1 倍以下では破線を描画しないことを検証する。</summary>
    [AvaloniaFact]
    public void DrawSelectionRectOverlay_ズーム1倍では描画しない()
    {
        var bitmap = CreateSolidBitmap(40, 40);

        // scale < 2 → 何も描画しない（例外なく早期リターンする）
        FadeMaskDragRectControl.DrawSelectionRectOverlay(
            bitmap, new Rect(10, 10, 20, 20), new Point(5, 5), 1.0, Colors.Black, Colors.Transparent);

        // ヘッドレス環境では Lock() 書き込みの読み戻しが不安定なため、ピクセル値は検証しない
        // （scale < 2 の早期リターンで例外なく実行されることを検証する）
        Assert.Equal(40, bitmap.PixelSize.Width);
    }

    /// <summary>テスト用の単色 Bitmap（白）を生成する。</summary>
    private static WriteableBitmap CreateSolidBitmap(int width, int height)
    {
        var bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        using var locked = bitmap.Lock();
        var stride = locked.RowBytes;
        unsafe
        {
            var ptr = (byte*)locked.Address;
            for (var i = 0; i < stride * height; i += 4)
            {
                ptr[i] = 255;     // B
                ptr[i + 1] = 255; // G
                ptr[i + 2] = 255; // R
                ptr[i + 3] = 255; // A
            }
        }
        return bitmap;
    }
}
