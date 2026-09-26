using EsUtil.Platform.Core.Geometry;
using SkiaSharp;
using Xunit;
using DrawingColor = System.Drawing.Color;
using EsUtil.Feature.MarqueeCore.Drawing;
using EsUtil.SaveEvidence.Marquee.UIBase;


namespace EsUtil.SaveEvidence.FadeMaskDragRect.UIBase.Tests;

/// <summary><see cref="FadeMaskHostLabelRenderer"/> の描画テスト（描画ポートの画素を検証する）。</summary>
/// <remarks>
/// 描画の実行は UI 非依存の描画ポート（Skia）で行うため、UI 初期化とウィンドウなしで検証できる。<br/>
/// 本テストが「付加情報ラベルの積み上げ」の唯一の検証点である（各 UI はアダプタのみを持つため、UI ごとの描画テストは不要）。
/// </remarks>
public class FadeMaskHostLabelRendererTests
{
    /// <summary>検証用の背景色（不透明。文字の描かれない余白で判定する）。</summary>
    private static readonly DrawingColor s_operationBackground = DrawingColor.FromArgb(255, 10, 20, 30);

    /// <summary>検証用の情報ラベルの背景色（不透明）。</summary>
    private static readonly DrawingColor s_infoBackground = DrawingColor.FromArgb(255, 40, 50, 60);

    /// <summary>操作ラベルのフォントサイズ（DIP）。</summary>
    private const double OperationFontSize = 12.0;

    /// <summary>情報ラベルのフォントサイズ（DIP）。</summary>
    private const double InfoFontSize = 14.0;

    /// <summary>穴あけ矩形の左上（DIP）。</summary>
    private static readonly EsPoint HoleTopLeft = new(20, 30);

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

    /// <summary>テスト用の描画内容を生成する。</summary>
    /// <param name="holeRect">穴あけ矩形（null の場合は穴あけなし）。</param>
    /// <param name="sizeLabel">サイズラベルのテキスト配置（null の場合は描かれていない）。</param>
    /// <returns>描画内容（装飾・ハンドル・マーカーは本テストの対象外のため既定値）。</returns>
    private static MarqueeDrawContent CreateContent(EsRect? holeRect, MarqueeTextPlacement? sizeLabel = null)
        => new(default, default, holeRect, null, null, sizeLabel, null, []);

    /// <summary>テスト用の選択矩形コントロールの描画設定を生成する（ラベルの行送りにフォントサイズを使う）。</summary>
    /// <returns>描画の設定。</returns>
    private static MarqueePaint CreatePaint() => new();

    /// <summary>穴あけ矩形の左上から次の行の上端を求める（行送り = 行の高さ + 内側余白 × 2）。</summary>
    /// <param name="top">現在の上端。</param>
    /// <param name="fontSize">フォントサイズ。</param>
    /// <returns>次の行の上端。</returns>
    private static double NextTop(double top, double fontSize)
        => top + fontSize + CreatePaint().LabelPadding * 2;

    [Fact]
    public void 描画_穴あけ矩形が無ければ何も描かない()
    {
        using var target = TestHelper.CreateTarget(160, 120);

        FadeMaskHostLabelRenderer.Draw(target.Canvas, CreateContent(null), CreateLabels(), CreatePaint(), "確定: Enter", "メモ帳");

        Assert.Equal(new SKColor(0, 0, 0, 0), target.GetPixel((int)HoleTopLeft.X, (int)HoleTopLeft.Y));
    }

    [Fact]
    public void 描画_操作ラベルと情報ラベルを縦に積む()
    {
        using var target = TestHelper.CreateTarget(200, 160);
        var content = CreateContent(new EsRect(HoleTopLeft.X, HoleTopLeft.Y, 100, 80));

        FadeMaskHostLabelRenderer.Draw(target.Canvas, content, CreateLabels(), CreatePaint(), "確定: Enter", "メモ帳");

        // 操作ラベルは穴あけ矩形の左上に置かれる（背景の余白部分を判定する）
        Assert.Equal(new SKColor(10, 20, 30), target.GetPixel((int)HoleTopLeft.X + 1, (int)HoleTopLeft.Y + 1));

        // 情報ラベルは操作ラベルの下（行送りはコアが単一所有する）に置かれる
        var infoTop = (int)NextTop(HoleTopLeft.Y, OperationFontSize);
        Assert.Equal(new SKColor(40, 50, 60), target.GetPixel((int)HoleTopLeft.X + 1, infoTop + 1));
    }

    [Fact]
    public void 描画_サイズラベルがある場合はその下へ積む()
    {
        using var target = TestHelper.CreateTarget(200, 160);
        var sizeLabel = new MarqueeTextPlacement(
            "(10, 20), (20×20)", HoleTopLeft, EsHorizontalAlign.Left, EsVerticalAlign.Top, default);
        var content = CreateContent(new EsRect(HoleTopLeft.X, HoleTopLeft.Y, 100, 80), sizeLabel);

        FadeMaskHostLabelRenderer.Draw(
            target.Canvas, content, CreateLabels(showInfo: false), CreatePaint(), "確定: Enter", null);

        // サイズラベルの行送り分だけ下へずれる（行の高さは描画の設定のフォントサイズを使う）
        var expectedTop = (int)NextTop(HoleTopLeft.Y, CreatePaint().SizeLabelFontSize);
        Assert.Equal(new SKColor(10, 20, 30), target.GetPixel((int)HoleTopLeft.X + 1, expectedTop + 1));
        // サイズラベル自身の位置には付加情報を描かない
        Assert.Equal(new SKColor(0, 0, 0, 0), target.GetPixel((int)HoleTopLeft.X + 1, (int)HoleTopLeft.Y + 1));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void 描画_表示スイッチが無効ならその行を描かない(bool showOperation, bool showInfo)
    {
        using var target = TestHelper.CreateTarget(200, 160);
        var content = CreateContent(new EsRect(HoleTopLeft.X, HoleTopLeft.Y, 100, 80));

        FadeMaskHostLabelRenderer.Draw(target.Canvas, content, CreateLabels(showOperation, showInfo), CreatePaint(), "確定: Enter", "メモ帳");

        // 操作ラベルの位置には（表示する場合を除き）何も描かれない
        var atOperation = target.GetPixel((int)HoleTopLeft.X + 1, (int)HoleTopLeft.Y + 1);
        if (showOperation)
        {
            Assert.Equal(new SKColor(10, 20, 30), atOperation);
        }
        else if (showInfo)
        {
            // 操作ラベルを描かない場合は情報ラベルが先頭（穴あけ矩形の左上）へ繰り上がる
            Assert.Equal(new SKColor(40, 50, 60), atOperation);
        }
        else
        {
            Assert.Equal(new SKColor(0, 0, 0, 0), atOperation);
        }
    }

    /// <summary>文言が空の行を描かないことを検証する。</summary>
    /// <remarks>
    /// 空文字は「描かない」を意味する（ホストの付加情報が対象外のモード等で発生する）。<br/>
    /// 描かない行の分だけ後続の行が繰り上がる（穴あけ矩形の左上から積み直す）。
    /// </remarks>
    [Fact]
    public void 描画_文言が空ならその行を描かない()
    {
        using var target = TestHelper.CreateTarget(200, 160);
        var content = CreateContent(new EsRect(HoleTopLeft.X, HoleTopLeft.Y, 100, 80));

        FadeMaskHostLabelRenderer.Draw(target.Canvas, content, CreateLabels(), CreatePaint(), string.Empty, "メモ帳");

        // 操作ラベルは空文字のため描かれず、情報ラベルが先頭（穴あけ矩形の左上）へ繰り上がる
        Assert.Equal(new SKColor(40, 50, 60), target.GetPixel((int)HoleTopLeft.X + 1, (int)HoleTopLeft.Y + 1));
    }

    /// <summary>空白のみの文言を空として扱わないことを検証する。</summary>
    /// <remarks>「空」の判定は文字数 0 である（空白文字を描くかどうかはホストの文言生成の責務）。</remarks>
    [Fact]
    public void 描画_空白のみの文言は描く()
    {
        using var target = TestHelper.CreateTarget(200, 160);
        var content = CreateContent(new EsRect(HoleTopLeft.X, HoleTopLeft.Y, 100, 80));

        FadeMaskHostLabelRenderer.Draw(target.Canvas, content, CreateLabels(showInfo: false), CreatePaint(), "   ", null);

        // 背景は文字の実寸へ内側余白を加えた大きさで描かれる（文字が空白でも行は描かれる）
        Assert.Equal(new SKColor(10, 20, 30), target.GetPixel((int)HoleTopLeft.X + 1, (int)HoleTopLeft.Y + 1));
    }

    [Fact]
    public void 描画_描画ポートがnullなら例外()
    {
        Assert.Throws<ArgumentNullException>(
            () => FadeMaskHostLabelRenderer.Draw(null!, CreateContent(new EsRect(0, 0, 10, 10)), CreateLabels(), CreatePaint(), "操作", "情報"));
    }
}
