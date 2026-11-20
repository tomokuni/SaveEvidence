using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Feature.Annotation.Core.Tests;

/// <summary><see cref="AnnotationPanelController"/> の開閉とドラッグを検証する。</summary>
public sealed class AnnotationPanelControllerTests
{
    /// <summary>表示するとホストへ作成が伝わり、位置はアンカーのままになる。</summary>
    [Fact]
    internal void Show_ホストへ作成を伝える()
    {
        var host = new HostSpy();
        var controller = new AnnotationPanelController(host);

        controller.Show(AnnotationPanelKind.StyleEditor, new EsPointInt(640, 480));

        Assert.True(controller.IsOpen);
        Assert.Equal(AnnotationPanelKind.StyleEditor, controller.Kind);
        Assert.Equal(1, host.CreateCount);
        Assert.Equal(new EsPointInt(640, 480), host.LastCreatePosition);
        Assert.Equal(AnnotationPanelAppearance.StyleEditor, host.LastAppearance);
    }

    /// <summary>別の種別を表示すると、前のパネルを閉じてから作り直す（同時に 2 つ出さない）。</summary>
    [Fact]
    internal void Show_種別が変わると作り直す()
    {
        var host = new HostSpy();
        var controller = new AnnotationPanelController(host);
        controller.Show(AnnotationPanelKind.StyleEditor, new EsPointInt(10, 20));

        controller.Show(AnnotationPanelKind.FormatBar, new EsPointInt(30, 40));

        Assert.Equal(2, host.CreateCount);
        Assert.Equal(1, host.DestroyCount);
        Assert.Equal(AnnotationPanelKind.FormatBar, controller.Kind);
    }

    /// <summary>閉じるとホストへ破棄が伝わる（2 回目は何もしない）。</summary>
    [Fact]
    internal void Close_ホストへ破棄を伝える()
    {
        var host = new HostSpy();
        var controller = new AnnotationPanelController(host);
        controller.Show(AnnotationPanelKind.FormatBar, new EsPointInt(1, 2));

        controller.Close();
        controller.Close();

        Assert.False(controller.IsOpen);
        Assert.Null(controller.Kind);
        Assert.Equal(1, host.DestroyCount);
    }

    /// <summary>ドラッグでは押下時からのポインターの移動量だけをホストへ渡す。</summary>
    [Fact]
    internal void DragTo_移動量だけをホストへ渡す()
    {
        var host = new HostSpy();
        var controller = new AnnotationPanelController(host);
        controller.Show(AnnotationPanelKind.StyleEditor, new EsPointInt(100, 200));

        controller.BeginDrag(new EsPointInt(110, 210));
        controller.DragTo(new EsPointInt(130, 240));

        Assert.Equal(new EsPointInt(120, 230), host.LastMovePosition);
        Assert.True(controller.IsDragging);

        controller.EndDrag();
        Assert.False(controller.IsDragging);
    }

    /// <summary>表示していないときのドラッグ操作は無視する。</summary>
    [Fact]
    internal void DragTo_表示していないときは動かさない()
    {
        var host = new HostSpy();
        var controller = new AnnotationPanelController(host);

        controller.BeginDrag(new EsPointInt(0, 0));
        controller.DragTo(new EsPointInt(50, 50));

        Assert.False(controller.IsDragging);
        Assert.Equal(0, host.MoveCount);
    }

    /// <summary>対象に被せない位置へ出す（本文の外へ置く）。</summary>
    [Fact]
    internal void ShowOutside_対象の外へ出す()
    {
        var host = new HostSpy();
        var controller = new AnnotationPanelController(host);

        controller.ShowOutside(
            AnnotationPanelKind.FormatBar,
            targetScreen: new EsRect(400, 500, 200, 100),
            panelSize: new EsSizeInt(300, 40),
            gap: 8);

        Assert.True(controller.IsOpen);
        Assert.Equal(400, host.LastCreatePosition.X);
        Assert.Equal(500 - 8 - 40, host.LastCreatePosition.Y);
    }

    /// <summary>ホストの呼び出しを記録するスパイ。</summary>
    private sealed class HostSpy : IAnnotationPanelHost
    {
        /// <summary>作成の回数。</summary>
        public int CreateCount { get; private set; }

        /// <summary>移動の回数。</summary>
        public int MoveCount { get; private set; }

        /// <summary>破棄の回数。</summary>
        public int DestroyCount { get; private set; }

        /// <summary>最後に作成を頼まれた位置。</summary>
        public EsPointInt LastCreatePosition { get; private set; }

        /// <summary>最後に渡された外観。</summary>
        public AnnotationPanelAppearance? LastAppearance { get; private set; }

        /// <summary>最後に移動を頼まれた位置。</summary>
        public EsPointInt LastMovePosition { get; private set; }

        /// <summary>パネルの左上（作成・移動で更新する）。</summary>
        public EsPointInt PanelPosition { get; private set; }

        /// <summary>パネルの大きさ（物理ピクセル）。</summary>
        public EsSizeInt PanelSize { get; set; } = new(300, 120);

        /// <summary>画面の作業領域（物理ピクセル）。</summary>
        public EsRect WorkArea { get; set; } = new(0, 0, 1920, 1040);

        /// <inheritdoc/>
        public void CreatePanel(
            AnnotationPanelKind kind, AnnotationPanelAppearance appearance, EsPointInt screenPosition)
        {
            _ = kind;
            CreateCount++;
            LastAppearance = appearance;
            LastCreatePosition = screenPosition;
            PanelPosition = screenPosition;
        }

        /// <inheritdoc/>
        public void MovePanel(EsPointInt screenPosition)
        {
            MoveCount++;
            LastMovePosition = screenPosition;
            PanelPosition = screenPosition;
        }

        /// <inheritdoc/>
        public void DestroyPanel() => DestroyCount++;
    }
}
