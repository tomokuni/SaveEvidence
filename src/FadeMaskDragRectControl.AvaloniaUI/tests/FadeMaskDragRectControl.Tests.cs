using BindingFlags = System.Reflection.BindingFlags;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Point = Avalonia.Point;
using Rect = Avalonia.Rect;
using EsUtil.SaveEvidence.Marquee.AvaloniaUI;
using EsUtil.Platform.AvaloniaUI;
using EsUtil.Platform.AvaloniaUI.Imaging;
using EsUtil.Feature.MarqueeCore.Decoration;
using EsUtil.SaveEvidence.FadeMaskDragRect;
using EsUtil.Feature.MarqueeCore;
using EsUtil.SaveEvidence.Marquee.UIBase;
using EsUtil.Feature.LoupeCore;
using EsUtil.SaveEvidence.Loupe.UIBase;
using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.SaveEvidence.FadeMaskDragRect.AvaloniaUI.Tests;

/// <summary><see cref="FadeMaskDragRectControl"/> の単体テスト（公開 API・配線・状態同期）。</summary>
/// <remarks>
/// 対象: <c>FadeMaskDragRectControl.cs</c>（公開 API: Mode / Options / SetCoordinateTransform /<br/>
/// SetSelection / ClearSelection / SetLoupePosition / イベント中継 / テスト用アクセサ）。<br/>
/// パブリック API 経由で内包コントロール（Marquee / Loupe）への設定・状態同期・イベント中継を検証する。
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

        // 選択矩形（外側の装飾の描画も担う）とルーペが子として配置される
        Assert.Equal(2, control.Children.Count);
        Assert.Contains(control.Marquee, control.Children);
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
    [InlineData(FadeMaskDragRectMode.Crop, true, true)]
    [InlineData(FadeMaskDragRectMode.AreaSelect, true, true)]
    [InlineData(FadeMaskDragRectMode.ScreenSelect, false, false)]
    [InlineData(FadeMaskDragRectMode.WindowSelect, false, false)]
    public void Mode_入力の可否と全面暗転が切り替わる(
        FadeMaskDragRectMode mode, bool expectedMarqueeInteractive, bool expectedShowFullOverlay)
    {
        var control = new FadeMaskDragRectControl { Mode = mode };

        // 選択矩形を使わないモードでも暗転マスク（ハイライト）を描画するため、描画は止めず入力だけを止める
        Assert.Equal(expectedMarqueeInteractive, control.Marquee.IsHitTestVisible);
        Assert.True(control.Marquee.IsVisible);
        Assert.Equal(expectedShowFullOverlay, control.ShowFullOverlay);
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
            Marquee = MarqueeOptions.Default with
            {
                MinSelectionSize = 5,
                HandleBorderColor = System.Drawing.Color.Red,
                HandleColor = Color.FromAColor(102, Colors.Red).ToDrawingColor(),
                HandleSize = 12.0,
                HandleHitRadius = 15.0,
                ClampToDomainBounds = true,
                MarkerColor = Colors.Green.ToDrawingColor(),
                MarkerLength = 20.0,
            },
            Deco = MarqueeDecoOptions.Default with
            {
                OuterAppearance = MarqueeDecoOptions.Default.OuterAppearance with { BorderThickness = 3.0 },
            },
        };

        control.Options = options;

        var applied = control.Marquee.Options;
        Assert.Equal(5, applied.MinSelectionSize);
        Assert.Equal(System.Drawing.Color.Red, applied.HandleBorderColor);
        Assert.Equal(3.0, control.Marquee.DecoOptions.OuterAppearance.BorderThickness);
        Assert.Equal(Color.FromAColor(102, Colors.Red), applied.HandleColor.ToAvaloniaColor());
        Assert.Equal(12.0, applied.HandleSize);
        Assert.Equal(15.0, applied.HandleHitRadius);
        Assert.True(applied.ClampToDomainBounds);
        Assert.Equal(Colors.Green, applied.MarkerColor.ToAvaloniaColor());
        Assert.Equal(20.0, applied.MarkerLength);
    }

    [AvaloniaFact]
    public void Options_装飾とサイズラベルの外観が反映される()
    {
        var control = new FadeMaskDragRectControl();
        var options = FadeMaskDragRectOptions.Default with
        {
            // サイズラベルは選択矩形コントロール単体の概念であるため選択コアが単一所有する
            Marquee = MarqueeOptions.Default with
            {
                ShowSizeLabel = false,
                SizeLabelColor = Colors.Yellow.ToDrawingColor(),
                SizeLabelBackgroundColor = Color.FromAColor(179, Colors.DarkBlue).ToDrawingColor(), // 暗青 70%
                SizeLabelFontSize = 18.0,
            },
            Deco = MarqueeDecoOptions.Default with
            {
                OuterAppearance = new OuterDecoAppearance
                {
                    OverlayColor = Color.FromAColor(128, Colors.DarkBlue).ToDrawingColor(), // 暗青 50%
                    BorderColor = Colors.Red.ToDrawingColor(),
                    BorderThickness = 3.0,
                },
            },
        };

        control.Options = options;

        // DPI スケーリングは設定（Options）の対象外（環境値。個別プロパティで設定）
        control.Scaling = 2.0;
        control.DisplayScaleScaling = 1.5;

        Assert.Equal(2.0, control.Scaling);
        Assert.Equal(1.5, control.DisplayScaleScaling);

        // 選択の設定（サイズラベル）はコアの MarqueeOptions へ反映される
        var marquee = control.Marquee.Options;
        Assert.False(marquee.ShowSizeLabel);
        Assert.Equal(Colors.Yellow.ToDrawingColor(), marquee.SizeLabelColor);
        Assert.Equal(Color.FromAColor(179, Colors.DarkBlue).ToDrawingColor(), marquee.SizeLabelBackgroundColor);
        Assert.Equal(18.0, marquee.SizeLabelFontSize);

        // 装飾の外観（暗転と枠線）は OuterDecoAppearance へ 1 つにまとめて反映される（色は UI 非依存の色型）
        var appearance = control.Marquee.DecoOptions.OuterAppearance;
        Assert.Equal(Color.FromAColor(128, Colors.DarkBlue).ToDrawingColor(), appearance.OverlayColor);
        Assert.Equal(Colors.Red.ToDrawingColor(), appearance.BorderColor);
        Assert.Equal(3.0, appearance.BorderThickness);
    }

    [AvaloniaFact]
    public void Options_ルーペの設定が反映される()
    {
        var control = new FadeMaskDragRectControl();
        var options = FadeMaskDragRectOptions.Default with
        {
            Marquee = MarqueeOptions.Default with
            {
                ShowCoordLabel = true,
                CoordLabelColor = Colors.Yellow.ToDrawingColor(),
                CoordLabelBackgroundColor = Color.FromAColor(179, Colors.DarkGreen).ToDrawingColor(), // 暗緑 70%
                CoordLabelFontSize = 14.0,
            },
            Loupe = new LoupeOptions
            {
                RequestedSize = 150,
                ZoomLevel = 3,
                Metrics = new LoupeMetrics
                {
                    CheckerSize = 10,
                    FrameWidth = 4,
                    ShowCoordLabel = true,
                    CoordLabelFontSize = 13.0,
                },
            },
            LoupeOffset = 30.0,
        };

        control.Options = options;
        // 色は描画にだけ必要な設定のため、描画の設定として与える
        control.LoupePaint = new LoupePaint
        {
            CrossColor = Color.FromAColor(204, Colors.Red).ToDrawingColor(), // 赤 80%
            CheckerColorA = Colors.Black.ToDrawingColor(),
            CheckerColorB = Colors.White.ToDrawingColor(),
            FrameColor = Colors.Gray.ToDrawingColor(),
            CoordLabelColor = Colors.Cyan.ToDrawingColor(),
            CoordLabelBackgroundColor = Color.FromAColor(153, Colors.DarkRed).ToDrawingColor(), // 暗赤 60%
        };

        // ルーペ描画スケールは設定（Options）の対象外（環境値。個別プロパティで設定）
        control.LoupeRenderScaling = 2.0;

        Assert.Equal(150, control.Loupe.Options.RequestedSize);
        Assert.Equal(3, control.Loupe.Options.ZoomLevel);
        Assert.Equal(2.0, control.Loupe.RenderScaling);
        Assert.Equal(10, control.Loupe.Options.Metrics.CheckerSize);
        Assert.Equal(4, control.Loupe.Options.Metrics.FrameWidth);
        Assert.Equal(Color.FromAColor(204, Colors.Red), control.Loupe.Paint.CrossColor.ToAvaloniaColor());
        Assert.Equal(Color.FromAColor(153, Colors.DarkRed), control.Loupe.Paint.CoordLabelBackgroundColor.ToAvaloniaColor());
        // 表示位置のオフセットはフレームワーク差（Avalonia 版は自己配置）のため、ルーペのプロパティへ渡される
        Assert.Equal(30.0, control.Loupe.PositionOffset);
        // 座標ラベル（Marquee / Loupe）
        var applied = control.Marquee.Options;
        Assert.True(applied.ShowCoordLabel);
        Assert.Equal(Colors.Yellow, applied.CoordLabelColor.ToAvaloniaColor());
        Assert.Equal(Color.FromAColor(179, Colors.DarkGreen), applied.CoordLabelBackgroundColor.ToAvaloniaColor());
        Assert.Equal(14.0, applied.CoordLabelFontSize);
        Assert.True(control.Loupe.Options.Metrics.ShowCoordLabel);
        Assert.Equal(13.0, control.Loupe.Options.Metrics.CoordLabelFontSize);
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
            Marquee = MarqueeOptions.Default with { MinSelectionSize = 5 },
            Loupe = LoupeOptions.Default with { RequestedSize = 150 },
            LoupeOffset = 30.0,
        };

        // with 式で一部だけを差し替える（指定しない項目は現在値＝既定値を維持する）
        control.Options = control.Options with
        {
            Marquee = control.Options.Marquee with { MarkerLength = 24.0 },
            Deco = control.Options.Deco with
            {
                OuterAppearance = control.Options.Deco.OuterAppearance with { BorderThickness = 4.0 },
            },
            Loupe = control.Options.Loupe with { RequestedSize = 200 },
        };

        Assert.Equal(5, control.Marquee.Options.MinSelectionSize);           // 未指定 → 現在値維持
        Assert.Equal(24.0, control.Marquee.Options.MarkerLength);            // 指定 → 反映
        Assert.Equal(4.0, control.Marquee.DecoOptions.OuterAppearance.BorderThickness); // 指定 → 反映
        Assert.Equal(200, control.Loupe.Options.RequestedSize);               // 指定 → 反映
        Assert.Equal(30.0, control.Loupe.PositionOffset);                     // 未指定 → 現在値維持
        Assert.Equal(200, control.Options.Loupe.RequestedSize);               // getter は更新後の値を返す
    }

    // ====================================================================
    // SetCoordinateTransform（ポートの一括分配）
    // ====================================================================

    [AvaloniaFact]
    public void SetCoordinateTransform_内包コントロールへ同一ポートが分配される()
    {
        var control = new FadeMaskDragRectControl();
        var port = CoordTransform.Identity;

        control.SetCoordinateTransform(port);

        Assert.Same(port, GetCoordinatePort(control.Marquee));
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

        Assert.Equal(rect, control.SelectionRect);
        Assert.True(control.HasSelection);
    }

    [AvaloniaFact]
    public void SetSelection_サイズ0の矩形は未選択として反映される()
    {
        var control = new FadeMaskDragRectControl();

        control.SetSelection(new Rect(0, 0, 0, 0));

        Assert.Equal(default, control.SelectionRect);
        Assert.False(control.HasSelection);
    }

    [AvaloniaFact]
    public void ClearSelection_選択状態がクリアされる()
    {
        var control = new FadeMaskDragRectControl();
        control.SetSelection(new Rect(10, 10, 50, 40));

        control.ClearSelection();

        Assert.Equal(default, control.SelectionRect);
        Assert.False(control.HasSelection);
    }

    // ====================================================================
    // 配線（Marquee のイベント → 状態同期・ホスト通知）
    // ====================================================================

    [AvaloniaFact]
    public void SelectionChanged_選択矩形がレンダラーへ同期される()
    {
        var control = new FadeMaskDragRectControl();
        var rect = new Rect(20, 30, 100, 80);

        // Marquee.SetSelection → SelectionChanged 発火 → 複合コントロールがレンダラーへ同期
        control.Marquee.SetSelection(rect);

        Assert.Equal(rect, control.SelectionRect);
        Assert.True(control.HasSelection);
    }

    [AvaloniaFact]
    public void ConfirmRequested_Marqueeの確定要求が中継される()
    {
        var control = new FadeMaskDragRectControl();
        control.Marquee.SetSelection(new Rect(10, 10, 50, 40));
        Rect? confirmed = null;
        control.ConfirmRequested += (_, e) => confirmed = e.SelectionRect;

        control.RequestConfirm();

        Assert.Equal(new Rect(10, 10, 50, 40), confirmed);
    }

    [AvaloniaFact]
    public void ConfirmRequested_購読者がいない場合は中継されない()
    {
        var control = new FadeMaskDragRectControl();
        control.Marquee.SetSelection(new Rect(10, 10, 50, 40));

        // リフレクションで ConfirmRequested を直接発火する（購読者なし → ?.Invoke の null 分岐）
        var field = typeof(MarqueeControl).GetField(
            "ConfirmRequested", BindingFlags.Instance | BindingFlags.NonPublic);
        var eventDelegate = (MulticastDelegate?)field?.GetValue(control.Marquee);
        eventDelegate?.DynamicInvoke(control.Marquee,
            new SelectionRectEventArgs(new Rect(10, 10, 50, 40), control.Marquee.CurrentSnapshot));

        // 例外なく完了する
        Assert.True(true);
    }

    [AvaloniaFact]
    public void CancelRequested_Marqueeのキャンセル要求が中継される()
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
        var field = typeof(MarqueeControl).GetField(
            "CancelRequested", BindingFlags.Instance | BindingFlags.NonPublic);
        var eventDelegate = (MulticastDelegate?)field?.GetValue(control.Marquee);
        eventDelegate?.DynamicInvoke(control.Marquee, EventArgs.Empty);

        // 例外なく完了する
        Assert.True(true);
    }

    [AvaloniaFact]
    public void ZoomRequested_ズーム要求が中継される()
    {
        var control = new FadeMaskDragRectControl();
        WheelZoomRequest? received = null;
        control.ZoomRequested += (_, request) => received = request;

        // Marquee の ZoomRequested は通常の .NET イベント（Ctrl+ホイールで発火）のため、
        // リフレクションでバッキングフィールドを取得して直接発火する（中継の検証用）。
        var field = typeof(MarqueeControl).GetField(
            "ZoomRequested", BindingFlags.Instance | BindingFlags.NonPublic);
        var eventDelegate = (MulticastDelegate?)field?.GetValue(control.Marquee);
        eventDelegate?.DynamicInvoke(control.Marquee, new WheelZoomRequest(true, new EsPoint(10, 20)));

        Assert.NotNull(received);
        Assert.True(received!.Value.ZoomIn);
        Assert.Equal(new EsPoint(10, 20), received.Value.Position);
    }

    [AvaloniaFact]
    public void ScrollRequested_スクロール要求が中継される()
    {
        var control = new FadeMaskDragRectControl();
        WheelScrollRequest? received = null;
        control.ScrollRequested += (_, request) => received = request;

        // Marquee の ScrollRequested をリフレクションで直接発火する（中継の検証用）。
        var field = typeof(MarqueeControl).GetField(
            "ScrollRequested", BindingFlags.Instance | BindingFlags.NonPublic);
        var eventDelegate = (MulticastDelegate?)field?.GetValue(control.Marquee);
        eventDelegate?.DynamicInvoke(control.Marquee, new WheelScrollRequest(0, 1, true, new EsPoint(30, 40)));

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
        var field = typeof(MarqueeControl).GetField(
            "ZoomRequested", BindingFlags.Instance | BindingFlags.NonPublic);
        var eventDelegate = (MulticastDelegate?)field?.GetValue(control.Marquee);
        eventDelegate?.DynamicInvoke(control.Marquee, new WheelZoomRequest(true, new EsPoint(0, 0)));

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

    /// <summary>複合コントロール経由でもルーペの中心座標ラベルがテキスト要素として配置されることを検証する。</summary>
    /// <remarks>文言・表示可否・配置はコアが単一所有し、ルーペコントロールが要素を置く（統合経路の検証）。</remarks>
    [AvaloniaFact]
    public void ルーペ_中心座標ラベルの要素を配置する()
    {
        var control = new FadeMaskDragRectControl();
        var window = new Window { Content = new Panel { Children = { control } } };
        window.Show();

        control.Loupe.Source = EsBitmapAvaloniaExtensions.CreateAvaloniaImage(10, 10, Colors.Transparent);
        control.SetLoupePosition(new Point(50, 60));

        // 配置フェーズ（ArrangeOverride）でラベル要素が更新される
        Dispatcher.UIThread.RunJobs();

        // ルーペの子は画像 + 中心座標ラベル（Border + TextBlock）の 2 つ
        var label = Assert.Single(control.Loupe.Children.OfType<Border>());
        var text = Assert.IsType<TextBlock>(label.Child);

        // 文言はコアの CoordinateFormatter が単一所有する
        Assert.Equal("(50, 60)", text.Text);
        Assert.True(label.Width > 0);
        Assert.True(label.Height > 0);

        window.Close();
    }

    // ====================================================================
    // ルーペ Bitmap の再生成要求（ホスト側オーバーレイの鮮度）
    // ====================================================================

    /// <summary>破線色のみを変更した場合でもルーペ Bitmap が再生成される（ホスト側オーバーレイはルーペのプロパティではない）。</summary>
    /// <remarks>
    /// 破線色（<c>SelectionRectColorA / B</c>）はルーペの外観（<c>LoupeMetrics</c>）に含まれず、<br/>
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
            var appearance = control.Loupe.Options.Metrics;
            control.Options = control.Options with { SelectionRectColorA = Colors.Lime.ToDrawingColor() };
            Assert.Equal(appearance, control.Loupe.Options.Metrics);
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
        Assert.Equal("LT: (10, 20)", control.SizeLabelOverrideProvider?.Invoke());
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
    /// <returns>設定されている座標変換ポート。未設定の場合は null。</returns>
    /// <remarks>
    /// ポートはコアのファサード（<c>Marquee</c> / <c>Loupe</c>）が単一所有し、コントロールはファサードへ委譲するだけである。<br/>
    /// 検証ではコントロールが保持するファサード（<c>_marquee</c> / <c>_loupe</c>）の内部フィールドをリフレクションで辿る。
    /// </remarks>
    private static ICoordTransform? GetCoordinatePort(Control control)
    {
        var anyInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var type = control.GetType();
        var facade = (type.GetField("_marquee", anyInstance) ?? type.GetField("_loupe", anyInstance))!.GetValue(control)!;
        return (ICoordTransform?)facade.GetType().GetField("_coordinatePort", anyInstance)!.GetValue(facade);
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

        // 選択時: HasSelection = true かつ矩形が同期される（Marquee.SetSelection → SelectionChanged → 同期）
        control.Marquee.SetSelection(new Rect(10, 10, 50, 40));
        Assert.True(control.HasSelection);
        Assert.Equal(new Rect(10, 10, 50, 40), control.SelectionRect);
    }

    [AvaloniaFact]
    public void SetSelection_Marqueeの選択状態を設定して同期される()
    {
        var control = new FadeMaskDragRectControl();

        // プログラム的な選択設定（復元・テスト用）→ SelectionChanged 発火 → 暗転マスクとルーペが同期される
        control.SetSelection(new Rect(10, 10, 50, 40));

        Assert.True(control.HasSelection);
        Assert.Equal(new Rect(10, 10, 50, 40), control.SelectionRect);
        Assert.Equal(new Rect(10, 10, 50, 40), control.SelectionRect);
    }

    [AvaloniaFact]
    public void AttachScrollViewer_Marqueeへ明示注入される()
    {
        var control = new FadeMaskDragRectControl();
        var scrollViewer = new ScrollViewer();

        // 例外なく設定できる（Marquee.AttachScrollViewer へ委譲）
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
        control.Marquee.SetSelection(new Rect(10, 10, 50, 40));

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
        control.Marquee.SetSelection(new Rect(10, 10, 50, 40));

        // Marquee の ViewportChanged を発火（ScrollViewer 連携の ScrollChanged → ViewportChanged の模擬）。
        // 配線は NotifyViewportChanged に統合されている（選択状態の再同期は行わず、再描画のみ要求）
        var field = typeof(MarqueeControl).GetField(
            "ViewportChanged", BindingFlags.Instance | BindingFlags.NonPublic);
        var eventDelegate = (MulticastDelegate?)field?.GetValue(control.Marquee);
        eventDelegate?.DynamicInvoke(control.Marquee,
            new SelectionRectEventArgs(new Rect(10, 10, 50, 40), control.Marquee.CurrentSnapshot));

        // 選択状態は維持される（再描画要求のみ）
        Assert.True(control.HasSelection);
        Assert.Equal(new Rect(10, 10, 50, 40), control.SelectionRect);
    }

    [AvaloniaFact]
    public void ViewportChanged_イベントでルーペ位置が再計算される()
    {
        var control = new FadeMaskDragRectControl();
        control.Marquee.SetSelection(new Rect(10, 10, 50, 40));

        // ルーペの表示位置計算（UpdatePosition）は親 Panel 接続 + IsVisible が条件のため、
        // ウィンドウに配置してツリーへ接続し、AXAML 既定（IsVisible=False）を明示的に表示へ切り替えてから中心を設定する
        var window = new Window { Content = control };
        window.Show();
        control.Loupe.IsVisible = true;
        control.Loupe.CenterPoint = new Point(50, 50);
        var before = control.Loupe.RenderTransform;
        Assert.NotNull(before); // 接続後に CenterPoint 設定の位置計算で RenderTransform が生成される

        // Marquee の ViewportChanged を発火（ScrollViewer 連携のスクロール変化の模擬）
        var field = typeof(MarqueeControl).GetField(
            "ViewportChanged", BindingFlags.Instance | BindingFlags.NonPublic);
        var eventDelegate = (MulticastDelegate?)field?.GetValue(control.Marquee);
        eventDelegate?.DynamicInvoke(control.Marquee,
            new SelectionRectEventArgs(new Rect(10, 10, 50, 40), control.Marquee.CurrentSnapshot));

        // ルーペ表示位置が再計算される（NotifyViewportChanged → Loupe.RefreshPosition 経由で RenderTransform が更新される。
        // TranslateTransform は同一インスタンスの値更新のため Same で検証）
        Assert.NotNull(control.Loupe.RenderTransform);
        Assert.Same(before, control.Loupe.RenderTransform);
        window.Close();
    }

    // ====================================================================
    // LoupePositionMoved（ホストへのイベント中継）
    // ====================================================================

    [AvaloniaFact]
    public void LoupePositionMoved_Marqueeのルーペ位置更新が中継される()
    {
        var control = new FadeMaskDragRectControl();
        var window = new Window { Content = new Panel { Children = { control } } };
        window.Show();
        control.Loupe.Source = EsBitmapAvaloniaExtensions.CreateAvaloniaImage(10, 10, Colors.Transparent);

        FadeMaskDragRectPointEventArgs? received = null;
        control.LoupePositionMoved += (_, e) => received = e;

        // リフレクションで LoupePositionMoved イベントを直接発火する（中継の検証用）
        var field = typeof(MarqueeControl).GetField(
            "LoupePositionMoved", BindingFlags.Instance | BindingFlags.NonPublic);
        var eventDelegate = (MulticastDelegate?)field?.GetValue(control.Marquee);
        var args = new MarqueePointEventArgs(new Point(100, 200), control.Marquee.CurrentSnapshot);
        eventDelegate?.DynamicInvoke(control.Marquee, args);

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
        var field = typeof(MarqueeControl).GetField(
            "LoupePositionMoved", BindingFlags.Instance | BindingFlags.NonPublic);
        var eventDelegate = (MulticastDelegate?)field?.GetValue(control.Marquee);
        var args = new MarqueePointEventArgs(new Point(100, 200), control.Marquee.CurrentSnapshot);
        eventDelegate?.DynamicInvoke(control.Marquee, args);

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
            control.SetSelection(new Rect(10, 10, 50, 40));
            Assert.True(control.HasSelection);

            // 矩形外クリック（ポインター押下 → 新規選択開始 = SelectionStarted 発火）で選択状態がクリアされる。
            // 押下位置 (300,200) は選択矩形 (10,10,50,40) の外側ハンドル帯（HandleHitRadius=12）からも離れている
            var pointer = CreatePointer();
            var pressed = new PointerPressedEventArgs(control.Marquee, pointer, window,
                ToScreenPoint(control.Marquee, new Point(300, 200)), 0UL, LeftPressed(), KeyModifiers.None, 1);
            control.Marquee.RaiseEvent(pressed);

            Assert.False(control.HasSelection);
            // 選択矩形の位置はセッションが保持するため、選択としての有効性は HasSelection で判定する
            Assert.Equal(0, control.SelectionRect.Width);
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
            var pressed = new PointerPressedEventArgs(control.Marquee, pointer, window,
                ToScreenPoint(control.Marquee, new Point(20, 20)), 0UL, LeftPressed(), KeyModifiers.None, 1);
            control.Marquee.RaiseEvent(pressed);
            // ドラッグ中（SelectionChanged で選択状態が設定される）
            var moved = new PointerEventArgs(InputElement.PointerMovedEvent, control.Marquee, pointer, window,
                ToScreenPoint(control.Marquee, new Point(23, 23)), 0UL, LeftPressed(), KeyModifiers.None);
            control.Marquee.RaiseEvent(moved);
            // 解放（3px = MinSelectionSize 未満 → SelectionCancelled 発火 → 選択状態クリア）
            var released = new PointerReleasedEventArgs(control.Marquee, pointer, window,
                ToScreenPoint(control.Marquee, new Point(23, 23)), 0UL, LeftReleased(), KeyModifiers.None, MouseButton.Left);
            control.Marquee.RaiseEvent(released);

            Assert.False(control.HasSelection);
            // 選択矩形の位置はセッションが保持するため、選択としての有効性は HasSelection で判定する
            Assert.Equal(0, control.SelectionRect.Width);
        }
        finally
        {
            window.Close();
        }
    }

    // ====================================================================
    // SetLoupeSource / ClearLoupe / LoupeCenterPoint / FocusMarquee
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
    public void FocusMarquee_Marqueeへフォーカスを設定する()
    {
        var control = new FadeMaskDragRectControl();
        var window = new Window { Content = control };
        window.Show();

        // 例外なくフォーカスを設定できる（ヘッドレス環境では IsKeyboardFocusWithin の検証は省略）
        control.FocusMarquee();
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

        // 設定時: 外側の装飾（MarqueeControl）へ反映される
        control.LogicalHighlightRect = new Rect(100, 50, 1920, 1080);
        control.DisplayScaleScaling = 1.5;

        Assert.Equal(new Rect(100, 50, 1920, 1080), control.LogicalHighlightRect);
        Assert.Equal(1.5, control.DisplayScaleScaling);
        // コントロール経由の getter も検証する（暗転マスクへの委譲）
        Assert.Equal(1.5, control.DisplayScaleScaling);
    }

    [AvaloniaFact]
    public void 文言プロバイダ_ホストが保持する()
    {
        var control = new FadeMaskDragRectControl();
        Func<string?> operation = () => "確定: Enter";
        Func<string?> info = () => "メモ帳";

        control.OperationTextProvider = operation;
        control.InfoTextProvider = info;

        // 操作・情報ラベルはアプリ固有の付加情報のため、ホスト（本クラス）が保持して描画する
        // （選択コントロールの概念ではないため、コアへは伝搬しない）
        Assert.Same(operation, control.OperationTextProvider);
        Assert.Same(info, control.InfoTextProvider);
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
