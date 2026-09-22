using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using EsUtil.Platform.AvaloniaUI;
using EsUtil.Platform.Core.Geometry;
using Estable.FadeMask;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace Estable.AvaloniaUI.FadeMask.Tests;

/// <summary><see cref="FadeMaskDrawer"/> の描画テスト（DrawingContext 描画の例外なし検証）。</summary>
/// <remarks>
/// 対象: <c>FadeMaskDrawer.cs</c>（フレームの描画のみ）。<br/>
/// 「どの矩形を暗転するか」「どのラベルを描くか」の判断はコアの <c>FadeMaskFrame</c> が単一所有するため、<br/>
/// 本テストは「与えられたフレームを描く」こと（例外なし・ブラシキャッシュの容量管理）のみを検証する。<br/>
/// フレームの解決（判断）はコアの <c>FadeMaskFrameTests</c> が検証する。
/// </remarks>
[Collection("AvaloniaHeadless")]
public class FadeMaskDrawerTests
{
    /// <summary>描画に使う暗転色（赤 50%）。</summary>
    private static readonly DrawingColor s_overlay = DrawingColor.FromArgb(128, 255, 0, 0);

    /// <summary>描画に使う枠線色（青・不透明）。</summary>
    private static readonly DrawingColor s_border = DrawingColor.FromArgb(255, 0, 0, 255);

    /// <summary>描画コンテキストを生成する。</summary>
    /// <returns>描画先のコンテキスト。破棄は呼び出し側。</returns>
    private static DrawingContext CreateContext()
    {
        var rtb = new RenderTargetBitmap(new PixelSize(300, 300), new Vector(96, 96));
        return rtb.CreateDrawingContext();
    }

    /// <summary>テスト用のフレームを生成する。</summary>
    /// <param name="overlayRegions">暗転領域。null の場合は空。</param>
    /// <param name="borderRects">枠線の矩形。null の場合は空。</param>
    /// <param name="labelAnchor">ラベルの基準矩形。</param>
    /// <param name="sizeLabelText">サイズラベルの文言。</param>
    /// <param name="subLines">2 行目以降の行。</param>
    /// <returns>生成したフレーム。</returns>
    private static FadeMaskFrame CreateFrame(
        IReadOnlyList<EsRect>? overlayRegions = null,
        IReadOnlyList<EsRect>? borderRects = null,
        EsRect? labelAnchor = null,
        string? sizeLabelText = null,
        IReadOnlyList<FadeMaskSubLine>? subLines = null)
        => new(
            overlayRegions ?? [],
            s_overlay,
            borderRects ?? [],
            s_border,
            labelAnchor,
            sizeLabelText,
            new FadeMaskLabelStyle(14.0, DrawingColor.White, DrawingColor.FromArgb(128, 0, 0, 0)),
            subLines ?? []);

    /// <summary>暗転領域と枠線を描画することを検証する。</summary>
    [AvaloniaFact]
    public void Draw_暗転領域と枠線を描画する()
    {
        using var ctx = CreateContext();

        FadeMaskDrawer.Draw(ctx, CreateFrame(
            overlayRegions: [new EsRect(0, 0, 300, 50), new EsRect(0, 50, 50, 250)],
            borderRects: [new EsRect(50, 50, 100, 2), new EsRect(50, 128, 100, 2)]));
    }

    /// <summary>サイズラベルを描画することを検証する。</summary>
    [AvaloniaFact]
    public void Draw_サイズラベルを描画する()
    {
        using var ctx = CreateContext();

        FadeMaskDrawer.Draw(ctx, CreateFrame(
            labelAnchor: new EsRect(50, 50, 100, 80),
            sizeLabelText: "(50, 50) - (151, 131),  (101×81), DisplayScale: (100%)"));
    }

    /// <summary>サイズラベルの文言が無い場合はサブラインを 1 行目から描画することを検証する。</summary>
    [AvaloniaFact]
    public void Draw_サイズラベル文言なしならサブラインを繰り上げて描画する()
    {
        using var ctx = CreateContext();

        FadeMaskDrawer.Draw(ctx, CreateFrame(
            labelAnchor: new EsRect(50, 50, 100, 80),
            sizeLabelText: null,
            subLines: [new FadeMaskSubLine("ダブルクリック: 確定  Esc / 矩形外クリック: キャンセル", 12.0, DrawingColor.White)]));
    }

    /// <summary>複数のサブラインを縦に積んで描画することを検証する。</summary>
    [AvaloniaFact]
    public void Draw_複数のサブラインを描画する()
    {
        using var ctx = CreateContext();

        FadeMaskDrawer.Draw(ctx, CreateFrame(
            labelAnchor: new EsRect(50, 50, 100, 80),
            sizeLabelText: "サイズ",
            subLines:
            [
                new FadeMaskSubLine("Enter / クリック: 確定  Esc: キャンセル", 12.0, DrawingColor.White, DrawingColor.FromArgb(179, 0, 0, 139)),
                new FadeMaskSubLine("Test Window", 11.0, DrawingColor.LightGray, DrawingColor.FromArgb(77, 139, 0, 0)),
            ]));
    }

    /// <summary>ラベルの基準矩形が無い場合はラベルを描画しない（例外も出ない）ことを検証する。</summary>
    [AvaloniaFact]
    public void Draw_ラベル基準がなければ何も描画しない()
    {
        using var ctx = CreateContext();

        FadeMaskDrawer.Draw(ctx, CreateFrame(
            labelAnchor: null,
            sizeLabelText: "サイズ",
            subLines: [new FadeMaskSubLine("操作ラベル", 12.0, DrawingColor.White)]));
    }

    /// <summary>何も持たないフレームでは何も描画しない（例外も出ない）ことを検証する。</summary>
    [AvaloniaFact]
    public void Draw_空のフレームでは何も描画しない()
    {
        using var ctx = CreateContext();

        FadeMaskDrawer.Draw(ctx, CreateFrame());
    }

    /// <summary>ブラシキャッシュ容量超過時も例外なく描画することを検証する。</summary>
    [AvaloniaFact]
    public void Draw_ブラシキャッシュ容量超過時も例外なく描画する()
    {
        // キャッシュ容量（128）を超える 129 色を順次描画し、容量超過時のキャッシュクリア経路を実行する
        for (int i = 0; i <= FadeMaskDrawer.BrushCacheCapacity; i++)
        {
            using var ctx = CreateContext();
            var argb = Color.FromArgb((byte)(100 + i % 100), (byte)(i * 2), (byte)(255 - i), (byte)(i * 3));
            var color = argb.ToDrawingColor();
            var frame = new FadeMaskFrame(
                [new EsRect(0, 0, 300, 50)],
                color,
                [new EsRect(0, 0, 300, 2)],
                color,
                new EsRect(50, 50, 100, 80),
                "サイズ",
                new FadeMaskLabelStyle(14.0, color, color),
                [new FadeMaskSubLine("操作ラベル", 12.0, color, color)]);

            FadeMaskDrawer.Draw(ctx, frame);
        }
    }
}
