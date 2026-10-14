using Avalonia.Headless.XUnit;
using Avalonia.Media;
using EsUtil.Feature.Loupe.AvaloniaUI;
using EsUtil.Feature.Marquee;
using EsUtil.Feature.Marquee.AvaloniaUI;
using EsUtil.SaveEvidence.Parts.CapturePreview.AvaloniaUI.SelectionOverlay;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.SelectionOverlay;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Imaging;
using Xunit;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.AvaloniaUI.Tests.SelectionOverlay;

/// <summary><see cref="SelectionOverlayControl"/> の単体テスト（生成・モードから導出される状態・設定の初期適用）。</summary>
/// <remarks>
/// <see cref="SelectionOverlayControl"/> は LoupeControl（SolidColorBrush 等のスレッドアフィニティ）を内包するため、
/// Avalonia ヘッドレス実行（Dispatcher）を必要とする。<c>[AvaloniaFact]</c> で直列コレクションに属して実行する。<br/>
/// 設定値そのもの（既定値・部分変更・枠線の導出）の検証はコアの <c>SelectionOverlayOptionsTests</c> が担う。
/// </remarks>
[Collection("AvaloniaHeadless")]
public class SelectionOverlayControlStateTests
{
    /// <summary>テスト用の拡大元画像を生成する（ルーペの表示可否の検証に使う）。</summary>
    /// <returns>生成した画像。</returns>
    private static IImage CreateSource() => EsBitmapAvaloniaExtensions.CreateAvaloniaImage(10, 10, Colors.Transparent);

    [AvaloniaFact]
    internal void コンストラクタ_内包コントロールが2つ配置される()
    {
        var control = new SelectionOverlayControl();

        // 選択矩形の外側の装飾（暗転マスク・枠・ラベル）は選択矩形コントロール自身が描画する
        Assert.Equal(2, control.Children.Count);
        Assert.IsType<MarqueeControl>(control.Children[0]);
        Assert.IsType<LoupeControl>(control.Children[1]);
    }

    // ====================================================================
    // モードから導出される状態（コアの判断が子コントロールへ適用される）
    // ====================================================================

    /// <summary>拡大元画像があるとき、選択矩形を使うモードでのみルーペを表示することを検証する。</summary>
    /// <param name="mode">動作モード。</param>
    /// <param name="expected">ルーペを表示する場合は true。</param>
    [AvaloniaTheory]
    [InlineData(SelectionOverlayMode.Crop, true)]
    [InlineData(SelectionOverlayMode.AreaSelect, true)]
    [InlineData(SelectionOverlayMode.ScreenSelect, false)]
    [InlineData(SelectionOverlayMode.WindowSelect, false)]
    internal void モード_ルーペの表示可否を切り替える(SelectionOverlayMode mode, bool expected)
    {
        var control = new SelectionOverlayControl();
        control.SetLoupeSource(CreateSource());
        // 表示開始は SetLoupePosition が担う（中心が未設定の間は表示しない）
        control.SetLoupePosition(new Avalonia.Point(5, 6));

        control.Mode = mode;

        Assert.Equal(expected, control.Loupe.IsVisible);
    }

    /// <summary>拡大元画像が未設定の場合はルーペを表示しないことを検証する。</summary>
    /// <param name="mode">動作モード。</param>
    [AvaloniaTheory]
    [InlineData(SelectionOverlayMode.Crop)]
    [InlineData(SelectionOverlayMode.AreaSelect)]
    internal void モード_画像未設定ではルーペを表示しない(SelectionOverlayMode mode)
    {
        var control = new SelectionOverlayControl
        {
            Mode = mode
        };

        Assert.False(control.Loupe.IsVisible);
    }

    /// <summary>モードを切り替えてもルーペ中心は維持されることを検証する（再表示で同じ位置を使える）。</summary>
    [AvaloniaFact]
    internal void モード_切り替えてもルーペ中心は維持される()
    {
        var control = new SelectionOverlayControl();
        control.SetLoupeSource(CreateSource());
        control.SetLoupePosition(new Avalonia.Point(5, 6));

        control.Mode = SelectionOverlayMode.WindowSelect;
        control.Mode = SelectionOverlayMode.AreaSelect;

        Assert.Equal(new Avalonia.Point(5, 6), control.LoupeCenterPoint);
        Assert.True(control.Loupe.IsVisible);
    }

    // ====================================================================
    // 設定の初期適用（既定値が子コントロールへ分配される）
    // ====================================================================

    [AvaloniaFact]
    internal void 設定_既定値が暗転マスクと選択矩形へ分配される()
    {
        var control = new SelectionOverlayControl();

        // 既定の設定（コアの既定値）がそのまま子コントロールへ渡される
        Assert.Equal(MarqueeAppearance.Default.OuterBorderThickness, control.MarqueeAppearance.OuterBorderThickness);
        // モードから導出される描画状態（既定は AreaSelect = 全面暗転）だけが外観の既定と異なる
        Assert.True(control.MarqueeAppearance.ShowFullOverlay);
        Assert.True(control.MarqueeAppearance.ShowHandles);
        Assert.Equal(SelectionOverlayOptions.Default.Loupe, control.Loupe.Options);
        Assert.Equal(SelectionOverlayOptions.Default.LoupeOffset, control.Loupe.PositionOffset);
    }

    [AvaloniaFact]
    internal void 設定_枠線はハンドル枠と暗転マスクで共用される()
    {
        var control = new SelectionOverlayControl
        {
            Options = SelectionOverlayOptions.Default
        };
        // 選択枠線（装飾の外枠）とハンドルの枠線は同じ矩形に重なるため、ホストが同じ色・太さを与える
        control.MarqueeAppearance = control.MarqueeAppearance with
        {
            OuterBorderColor = System.Drawing.Color.Red,
            HandleBorderColor = System.Drawing.Color.Red,
            OuterBorderThickness = 1.0,
        };

        Assert.Equal(System.Drawing.Color.Red, control.MarqueeAppearance.OuterBorderColor);
        Assert.Equal(control.MarqueeAppearance.HandleBorderColor, control.MarqueeAppearance.OuterBorderColor);
        Assert.Equal(1.0, control.MarqueeAppearance.OuterBorderThickness);
    }
}
