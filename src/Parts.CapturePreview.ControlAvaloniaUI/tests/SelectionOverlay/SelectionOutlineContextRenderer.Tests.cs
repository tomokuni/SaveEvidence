using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using EsUtil.Platform.Core.Geometry;
using EsUtil.SaveEvidence.Parts.CapturePreview.AvaloniaUI.SelectionOverlay;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.AvaloniaUI.Tests.SelectionOverlay;

/// <summary><see cref="SelectionOutlineContextRenderer"/>（選択矩形破線オーバーレイ）の描画テスト。</summary>
/// <remarks>
/// 破線の幾何はコアの <c>SelectionOutlineRasterizer.EnumerateDashes</c> が単一所有するため、<br/>
/// 本テストは「列挙された破線が実際の描画面へ 2 色で描かれること」を画素で実測する。<br/>
/// 画素はヘッドレスでも読めるウィンドウのフレーム（<see cref="HeadlessWindowExtensions.CaptureRenderedFrame"/>）から取得する。
/// </remarks>
public class SelectionOutlineContextRendererTests
{
    /// <summary>指定の描画を行うだけのコントロール（描画内容を実測するための最小の入れ物）。</summary>
    /// <param name="draw">描画処理。</param>
    private sealed class OutlineHost(Action<DrawingContext> draw) : Control
    {
        /// <inheritdoc/>
        public override void Render(DrawingContext context) => draw(context);
    }

    /// <summary>描画を実行し、ウィンドウのフレームから画素を読む。</summary>
    /// <param name="size">一辺のサイズ（DIP）。</param>
    /// <param name="draw">描画処理。</param>
    /// <param name="pixelAt">画素を読む処理を受け取る検証本体。</param>
    private static void WithFrame(int size, Action<DrawingContext> draw, Action<Func<int, int, (byte B, byte G, byte R, byte A)>> pixelAt)
    {
        var host = new OutlineHost(draw)
        {
            Width = size,
            Height = size,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
        };
        var panel = new Panel { Width = size, Height = size, Background = Brushes.White, Children = { host } };
        var window = new Window { Width = size, Height = size, Content = panel };
        window.Show();

        // 最初のフレームは 1 回で得られないことがあるため、得られるまでフレームを進める
        WriteableBitmap? frame = null;
        for (var i = 0; i < 10 && frame is null; i++)
        {
            Dispatcher.UIThread.RunJobs();
            frame = window.CaptureRenderedFrame();
        }

        Assert.NotNull(frame);

        (byte B, byte G, byte R, byte A) Read(int x, int y)
        {
            using var buffer = frame.Lock();
            var raw = System.Runtime.InteropServices.Marshal.ReadInt32(buffer.Address + (y * buffer.RowBytes) + (x * 4));
            return buffer.Format == PixelFormat.Bgra8888
                ? ((byte)(raw & 0xFF), (byte)((raw >> 8) & 0xFF), (byte)((raw >> 16) & 0xFF), (byte)((raw >> 24) & 0xFF))
                : ((byte)((raw >> 16) & 0xFF), (byte)((raw >> 8) & 0xFF), (byte)(raw & 0xFF), (byte)((raw >> 24) & 0xFF));
        }

        try
        {
            pixelAt(Read);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>選択矩形の辺に破線が描画されることを検証する。</summary>
    [AvaloniaFact]
    internal void DrawSelectionRect_選択矩形の辺に破線を描画する()
        => WithFrame(
            64,
            context => SelectionOutlineContextRenderer.DrawSelectionRect(
                context, new EsRect(10, 10, 20, 20), new EsPoint(0, 0), 4.0, 200, DrawingColor.Red, DrawingColor.Blue),
            pixelAt =>
            {
                // 上辺と左辺が交わる点（覆う画素の 1 画素外側）が色 A で描かれる
                var corner = pixelAt(39, 39);
                Assert.Equal(255, corner.R);

                // 選択矩形の内側は描画されない（背景の白のまま）
                var inside = pixelAt(50, 50);
                Assert.Equal(255, inside.R);
                Assert.Equal(255, inside.G);
                Assert.Equal(255, inside.B);
            });

    /// <summary>2 色の破線が交互に描画されることを検証する。</summary>
    [AvaloniaFact]
    internal void DrawSelectionRect_2色の破線を交互に描画する()
        => WithFrame(
            64,
            context => SelectionOutlineContextRenderer.DrawSelectionRect(
                context, new EsRect(1, 1, 10, 10), new EsPoint(0, 0), 4.0, 64, DrawingColor.Red, DrawingColor.Blue),
            pixelAt =>
            {
                // 破線の先頭の画素は色 A（赤）、次の画素は色 B（青）
                var first = pixelAt(3, 3);
                var second = pixelAt(5, 3);
                Assert.Equal(255, first.R);
                Assert.Equal(0, first.B);
                Assert.Equal(0, second.R);
                Assert.Equal(255, second.B);
            });

    /// <summary>倍率が 2 未満の場合は描画しないことを検証する。</summary>
    /// <param name="scale">描画倍率。</param>
    [AvaloniaTheory]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(1.99)]
    internal void DrawSelectionRect_倍率2未満では描画しない(double scale)
        => WithFrame(
            64,
            context => SelectionOutlineContextRenderer.DrawSelectionRect(
                context, new EsRect(10, 10, 20, 20), new EsPoint(0, 0), scale, 200, DrawingColor.Red, DrawingColor.Blue),
            pixelAt =>
            {
                // 白のまま（1 画素も描画されない）
                var corner = pixelAt(39, 39);
                Assert.Equal(255, corner.R);
                Assert.Equal(255, corner.G);
                Assert.Equal(255, corner.B);
            });

    /// <summary>描画先が null の場合は例外になることを検証する。</summary>
    [AvaloniaFact]
    internal void DrawSelectionRect_描画先がnullの場合は例外になる()
        => Assert.Throws<ArgumentNullException>(() => SelectionOutlineContextRenderer.DrawSelectionRect(
            null!, new EsRect(0, 0, 10, 10), new EsPoint(0, 0), 4.0, 100, DrawingColor.Red, DrawingColor.Blue));
}
