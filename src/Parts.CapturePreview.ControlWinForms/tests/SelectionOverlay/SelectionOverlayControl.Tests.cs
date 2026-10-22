using EsUtil.Feature.Loupe;
using EsUtil.Platform.Core.Geometry;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Logging;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.SelectionOverlay;
using EsUtil.SaveEvidence.Parts.CapturePreview.WinForms.SelectionOverlay;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.WinForms.Tests.SelectionOverlay;

/// <summary><see cref="SelectionOverlayControl"/>（領域選択の複合コントロール）の状態テスト。</summary>
/// <remarks>
/// 対象: モード・設定の分配・選択状態の委譲・ルーペの表示可否・入力の可否。<br/>
/// 本コントロールは子コントロールの生成のみを行い、ハンドル（ウィンドウ）を生成しないため、<br/>
/// ハンドルを必要としない範囲（状態と配置の解決）を検証できる。<br/>
/// 入力の受付・ドラッグの判断は内包コントロールとコアが単一所有するため本テストの対象外とする。
/// </remarks>
public sealed class SelectionOverlayControlTests
{
    /// <summary>既定のモードと設定が適用されることを検証する。</summary>
    [Fact]
    internal void 既定_範囲選択と既定設定を適用する()
    {
        using var control = new SelectionOverlayControl();

        Assert.Equal(SelectionOverlayMode.AreaSelect, control.Mode);
        Assert.Equal(SelectionOverlayOptions.Default, control.Options);
        // 選択矩形の外観はモードから導出する表示可否を上書きされるため、ルーペの外観（モードの影響を受けない）で既定を確認する
        Assert.Equal(LoupeAppearance.Default, control.LoupeAppearance);
        Assert.False(control.HasSelection);
    }

    /// <summary>範囲選択モードでは全面暗転を表示し入力を有効にすることを検証する。</summary>
    [Fact]
    internal void 範囲選択_全面暗転と入力を有効にする()
    {
        using var control = new SelectionOverlayControl
        {
            Mode = SelectionOverlayMode.AreaSelect,
        };

        Assert.True(control.ShowFullOverlay);
        Assert.True(control.Enabled);
    }

    /// <summary>ハイライトのみのモードでは全面暗転をせず入力を受け取らないことを検証する。</summary>
    /// <param name="mode">ハイライトのみのモード。</param>
    [Theory]
    [InlineData(SelectionOverlayMode.ScreenSelect)]
    [InlineData(SelectionOverlayMode.WindowSelect)]
    internal void ハイライトのみ_全面暗転も入力もしない(SelectionOverlayMode mode)
    {
        using var control = new SelectionOverlayControl
        {
            Mode = mode,
        };

        Assert.False(control.ShowFullOverlay);
        // 入力を受け取らない（WinForms では無効状態が「入力が下のコントロールへ通る」を表す）
        Assert.False(control.Enabled);
    }

    /// <summary>選択矩形の設定と読み取りが委譲されることを検証する。</summary>
    [Fact]
    internal void SetSelection_選択を委譲する()
    {
        using var control = new SelectionOverlayControl();

        control.SetSelection(new EsRect(10, 20, 30, 40));

        Assert.True(control.HasSelection);
        Assert.Equal(new EsRect(10, 20, 30, 40), control.SelectionSourceRect);
    }

    /// <summary>選択として成立しない矩形は選択なしになることを検証する。</summary>
    /// <remarks>可否の判断はコアの <c>SelectionUsability</c> が単一所有する。</remarks>
    [Fact]
    internal void SetSelection_退化した矩形は選択なしになる()
    {
        using var control = new SelectionOverlayControl();
        control.SetSelection(new EsRect(10, 20, 30, 40));

        control.SetSelection(new EsRect(10, 20, 0, 40));

        Assert.False(control.HasSelection);
    }

    /// <summary>選択のクリアが選択状態を初期化することを検証する。</summary>
    [Fact]
    internal void ClearSelection_選択を消す()
    {
        using var control = new SelectionOverlayControl();
        control.SetSelection(new EsRect(10, 20, 30, 40));

        control.ClearSelection();

        Assert.False(control.HasSelection);
    }

    /// <summary>ハイライト矩形が実行時状態として保持されることを検証する。</summary>
    [Fact]
    internal void HighlightSourceRect_設定した値を保持する()
    {
        using var control = new SelectionOverlayControl();

        control.HighlightSourceRect = new EsRect(1, 2, 3, 4);

        Assert.Equal(new EsRect(1, 2, 3, 4), control.HighlightSourceRect);
    }

    /// <summary>ルーペは拡大元画像と中心がそろって初めて表示されることを検証する。</summary>
    /// <remarks>表示可否の判断はコアの <c>SelectionOverlayState</c> が単一所有する。</remarks>
    [Fact]
    internal void ルーペ_画像と中心がそろうと表示する()
    {
        using var control = new SelectionOverlayControl();
        using var image = TestHelper.CreateBitmap(8, 8, DrawingColor.FromArgb(255, 255, 0, 0));

        control.SetLoupePosition(new EsPoint(4, 4));
        Assert.False(control.Loupe.Visible); // 拡大元画像が無い間は表示しない

        control.SetLoupeSource(image);
        control.SetLoupePosition(new EsPoint(4, 4));

        Assert.NotNull(control.LoupeCenterPoint);
        Assert.True(control.Loupe.Visible);
    }

    /// <summary>ルーペの後片付けが中心と拡大元画像を解放することを検証する。</summary>
    [Fact]
    internal void ClearLoupe_中心と画像を解放する()
    {
        using var control = new SelectionOverlayControl();
        using var image = TestHelper.CreateBitmap(8, 8, DrawingColor.FromArgb(255, 255, 0, 0));
        control.SetLoupeSource(image);
        control.SetLoupePosition(new EsPoint(4, 4));

        control.ClearLoupe();

        Assert.Null(control.LoupeCenterPoint);
        Assert.False(control.Loupe.Visible);
    }

    /// <summary>ハイライトのみのモードでは画像と中心がそろってもルーペを表示しないことを検証する。</summary>
    [Fact]
    internal void ルーペ_ハイライトのみのモードでは表示しない()
    {
        using var control = new SelectionOverlayControl { Mode = SelectionOverlayMode.WindowSelect };
        using var image = TestHelper.CreateBitmap(8, 8, DrawingColor.FromArgb(255, 255, 0, 0));

        control.SetLoupeSource(image);
        control.SetLoupePosition(new EsPoint(4, 4));

        Assert.False(control.Loupe.Visible);
    }

    /// <summary>設定を差し替えてもハイライト矩形が失われないことを検証する。</summary>
    /// <remarks>ハイライトは実行時状態であり、設定値に含まれない（本クラスが引き継ぐ）。</remarks>
    [Fact]
    internal void Options_差し替えでもハイライトを引き継ぐ()
    {
        using var control = new SelectionOverlayControl();
        control.HighlightSourceRect = new EsRect(1, 2, 3, 4);

        control.Options = SelectionOverlayOptions.Default with { LoupeOffset = 32 };

        Assert.Equal(new EsRect(1, 2, 3, 4), control.HighlightSourceRect);
    }

    /// <summary>選択確定・キャンセルの要求が例外なく受け付けられることを検証する。</summary>
    /// <remarks>確定は「選択が成立しているときだけ」発火する規則をコアが単一所有するため、<br/>要求そのものは常に受け付ける。</remarks>
    [Fact]
    internal void 確定とキャンセル_要求を受け付ける()
    {
        using var control = new SelectionOverlayControl();
        var confirmed = 0;
        control.ConfirmRequested += (_, _) => confirmed++;

        control.RequestConfirm();

        Assert.Equal(0, confirmed); // 選択が無い状態の確定は何もしない

        // キャンセルは要求として受け付けられる（例外にしない）
        control.RequestCancel();
    }

    /// <summary>ログの接頭辞が本アセンブリの定義へ反映されることを検証する。</summary>
    [Fact]
    internal void LogPrefix_定義へ反映する()
    {
        using var control = new SelectionOverlayControl();
        var original = CapturePreviewLogMessage.Prefix;

        try
        {
            control.LogPrefix = "[Test]";

            Assert.Equal("[Test]", CapturePreviewLogMessage.Prefix);
        }
        finally
        {
            control.LogPrefix = null;
            Assert.Equal(original, CapturePreviewLogMessage.Prefix);
        }
    }
}
