using EsUtil.Platform.Core.Geometry;
using Xunit;
using DrawingColor = System.Drawing.Color;
using EsUtil.Feature.Marquee.Presenter.Drawing;
using EsUtil.Feature.Marquee.Presenter.Setting;

namespace EsUtil.SaveEvidence.CaptureSession.UIBase.Tests;

/// <summary><see cref="FadeMaskHostLabelRenderer"/> の単体テスト（付加情報ラベルの配置指示の解決）。</summary>
/// <remarks>
/// 積み上げの位置と表示可否の判断は本クラスが単一所有する（両 UI が同じ組み立てを書かないことを値で固定する）。<br/>
/// 描画は各 UI が行う（本層は描画 API を持たない）ため、本テストは画素ではなく値を検証する。
/// </remarks>
public class FadeMaskHostLabelRendererTests
{
    /// <summary>操作ラベルの背景色（不透明）。</summary>
    private static readonly DrawingColor s_operationBackground = DrawingColor.FromArgb(255, 10, 20, 30);

    /// <summary>情報ラベルの背景色（不透明）。</summary>
    private static readonly DrawingColor s_infoBackground = DrawingColor.FromArgb(255, 40, 50, 60);

    /// <summary>操作ラベルのフォントサイズ（DIP）。</summary>
    private const double OperationFontSize = 12.0;

    /// <summary>情報ラベルのフォントサイズ（DIP）。</summary>
    private const double InfoFontSize = 14.0;

    /// <summary>注目矩形の左上（DIP）。</summary>
    private static readonly EsPoint FocusTopLeft = new(20, 30);

    /// <summary>測定に使う固定の実寸（テキストの実測値を模した値）。</summary>
    private static readonly EsSize MeasuredSize = new(40.0, 14.0);

    /// <summary>検証用の付加情報ラベルの外観を生成する（既定を検証用の色・寸法に差し替える）。</summary>
    /// <param name="showOperation">操作ラベルを表示するか。</param>
    /// <param name="showInfo">情報ラベルを表示するか。</param>
    /// <returns>付加情報ラベルの外観。</returns>
    private static FadeMaskDragRectLabelOptions CreateLabels(bool showOperation = true, bool showInfo = true)
        => FadeMaskDragRectLabelOptions.Default with
        {
            ShowOperationLabel = showOperation,
            OperationFontSize = OperationFontSize,
            OperationColor = DrawingColor.White,
            OperationBackgroundColor = s_operationBackground,
            ShowInfoLabel = showInfo,
            InfoFontSize = InfoFontSize,
            InfoColor = DrawingColor.White,
            InfoBackgroundColor = s_infoBackground,
        };

    /// <summary>テスト用の描画計画を生成する。</summary>
    /// <param name="focusDipRect">注目矩形（null の場合は注目なし）。</param>
    /// <param name="labelAnchor">ラベルの基準点。 null の場合は注目矩形の左上を使う。</param>
    /// <returns>描画計画（装飾・ハンドル・マーカー・ラベルは本テストの対象外のため空）。</returns>
    /// <remarks>
    /// 基準点は既定で注目矩形の左上にする（積み上げの規則だけを検証するため）。<br/>
    /// 内側へ寄せた基準点を使う場合は <paramref name="labelAnchor"/> を指定する。
    /// </remarks>
    private static MarqueeDrawPlan CreateContent(EsRect? focusDipRect, EsPoint? labelAnchor = null)
        => new(
            MarqueeAppearance.Default,
            default,
            focusDipRect,
            labelAnchor ?? focusDipRect?.TopLeft,
            [],
            [],
            null,
            [],
            null,
            null,
            [],
            []);

    /// <summary>テスト用の外観を生成する（サイズラベルを描かない基準）。</summary>
    /// <returns>外観。</returns>
    /// <remarks>
    /// サイズラベルを描く設定の場合、付加情報はその下へ積まれる（行送りの検証は別のテストが担う）ため、<br/>
    /// 本ヘルパーは「サイズラベルを描かない」を基準にする。
    /// </remarks>
    private static MarqueeAppearance CreateAppearance() => MarqueeAppearance.Default with { ShowSizeLabel = false };

    /// <summary>実寸を固定で返す測定関数。</summary>
    /// <param name="text">測定する文言。</param>
    /// <param name="fontSize">フォントサイズ。</param>
    /// <returns>固定の実寸。</returns>
    private static EsSize Measure(string text, double fontSize) => MeasuredSize;

    [Fact]
    public void 解決_注目矩形が無ければ空を返す()
    {
        var placements = FadeMaskHostLabelRenderer.Resolve(
            CreateContent(null), CreateLabels(), CreateAppearance(), "確定: Enter", "メモ帳", Measure);

        Assert.Empty(placements);
    }

    [Fact]
    public void 解決_操作ラベルと情報ラベルを縦に積む()
    {
        var placements = FadeMaskHostLabelRenderer.Resolve(
            CreateContent(new EsRect(FocusTopLeft.X, FocusTopLeft.Y, 100, 80)),
            CreateLabels(),
            CreateAppearance(),
            "確定: Enter",
            "メモ帳",
            Measure);

        Assert.Equal(2, placements.Count);
        // 操作ラベルは注目矩形の左上に置かれる（左端は注目矩形の左端に揃える）
        Assert.Equal(FocusTopLeft, placements[0].DipRect.TopLeft);
        Assert.Equal("確定: Enter", placements[0].Text);
        // 情報ラベルは操作ラベルの下（行送り = 行の高さ + 内側余白 × 2）
        var infoTop = FocusTopLeft.Y + OperationFontSize + CreateAppearance().LabelPadding * 2;
        Assert.Equal(infoTop, placements[1].DipRect.Y);
        Assert.Equal(FocusTopLeft.X, placements[1].DipRect.X);
        Assert.Equal("メモ帳", placements[1].Text);
    }

    [Fact]
    public void 解決_サイズラベルを描く設定ではその下へ積む()
    {
        var appearance = CreateAppearance() with { ShowSizeLabel = true };

        var placement = Assert.Single(FadeMaskHostLabelRenderer.Resolve(
            CreateContent(new EsRect(FocusTopLeft.X, FocusTopLeft.Y, 100, 80)),
            CreateLabels(showInfo: false),
            appearance,
            "確定: Enter",
            null,
            Measure));

        // サイズラベルの行送り分だけ下へずれる（行の高さは外観のフォントサイズを使う）
        var expectedTop = FocusTopLeft.Y + appearance.SizeLabelFontSize + appearance.LabelPadding * 2;
        Assert.Equal(expectedTop, placement.DipRect.Y);
    }

    /// <summary>サイズラベルを描かない設定では付加情報が先頭へ繰り上がることを検証する。</summary>
    [Fact]
    public void 解決_サイズラベルを描かない設定なら先頭へ繰り上がる()
    {
        var placement = Assert.Single(FadeMaskHostLabelRenderer.Resolve(
            CreateContent(new EsRect(FocusTopLeft.X, FocusTopLeft.Y, 100, 80)),
            CreateLabels(showInfo: false),
            CreateAppearance(),
            "確定: Enter",
            null,
            Measure));

        Assert.Equal(FocusTopLeft.Y, placement.DipRect.Y);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void 解決_表示スイッチが無効ならその行を返さない(bool showOperation, bool showInfo)
    {
        var placements = FadeMaskHostLabelRenderer.Resolve(
            CreateContent(new EsRect(FocusTopLeft.X, FocusTopLeft.Y, 100, 80)),
            CreateLabels(showOperation, showInfo),
            CreateAppearance(),
            "確定: Enter",
            "メモ帳",
            Measure);

        if (showOperation)
        {
            // 操作ラベルが先頭（注目矩形の左上）に置かれる
            Assert.Equal("確定: Enter", placements[0].Text);
            Assert.Equal(FocusTopLeft, placements[0].DipRect.TopLeft);
        }
        else if (showInfo)
        {
            // 操作ラベルを描かない場合は情報ラベルが先頭（注目矩形の左上）へ繰り上がる
            Assert.Equal("メモ帳", Assert.Single(placements).Text);
            Assert.Equal(FocusTopLeft, placements[0].DipRect.TopLeft);
        }
        else
        {
            Assert.Empty(placements);
        }
    }

    /// <summary>基準点が注目矩形の左上と異なる場合は基準点から積むことを検証する。</summary>
    /// <remarks>基準点は背景が選択矩形の枠線に重ならないよう内側へ寄せた値であり、積み上げはその値を基準にする。</remarks>
    [Fact]
    public void 解決_基準点から積む()
    {
        var focus = new EsRect(FocusTopLeft.X, FocusTopLeft.Y, 100, 80);
        var anchor = new EsPoint(FocusTopLeft.X + 2, FocusTopLeft.Y + 2);

        var placement = Assert.Single(FadeMaskHostLabelRenderer.Resolve(
            CreateContent(focus, anchor),
            CreateLabels(showInfo: false),
            CreateAppearance(),
            "確定: Enter",
            null,
            Measure));

        Assert.Equal(anchor, placement.DipRect.TopLeft);
    }

    /// <summary>文言が空の行を返さないことを検証する。</summary>
    /// <remarks>
    /// 空文字は「描かない」を意味する（ホストの付加情報が対象外のモード等で発生する）。<br/>
    /// 描かない行の分だけ後続の行が繰り上がる（注目矩形の左上から積み直す）。
    /// </remarks>
    [Fact]
    public void 解決_文言が空ならその行を返さない()
    {
        var placement = Assert.Single(FadeMaskHostLabelRenderer.Resolve(
            CreateContent(new EsRect(FocusTopLeft.X, FocusTopLeft.Y, 100, 80)),
            CreateLabels(),
            CreateAppearance(),
            string.Empty,
            "メモ帳",
            Measure));

        Assert.Equal("メモ帳", placement.Text);
        Assert.Equal(FocusTopLeft, placement.DipRect.TopLeft);
    }

    /// <summary>空白のみの文言を空として扱わないことを検証する。</summary>
    /// <remarks>「空」の判定は文字数 0 である（空白文字を描くかどうかはホストの文言生成の責務）。</remarks>
    [Fact]
    public void 解決_空白のみの文言は返す()
    {
        var placement = Assert.Single(FadeMaskHostLabelRenderer.Resolve(
            CreateContent(new EsRect(FocusTopLeft.X, FocusTopLeft.Y, 100, 80)),
            CreateLabels(showInfo: false),
            CreateAppearance(),
            "   ",
            null,
            Measure));

        Assert.Equal("   ", placement.Text);
    }

    [Fact]
    public void 解決_外観がnullなら例外()
        => Assert.Throws<ArgumentNullException>(() => FadeMaskHostLabelRenderer.Resolve(
            CreateContent(new EsRect(0, 0, 10, 10)), CreateLabels(), null!, "操作", "情報", Measure));

    [Fact]
    public void 解決_測定関数がnullなら例外()
        => Assert.Throws<ArgumentNullException>(() => FadeMaskHostLabelRenderer.Resolve(
            CreateContent(new EsRect(0, 0, 10, 10)), CreateLabels(), CreateAppearance(), "操作", "情報", null!));
}

