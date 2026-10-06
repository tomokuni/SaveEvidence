using System.Reflection;
using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using EsUtil.Feature.Marquee;
using EsUtil.Feature.Marquee.MewUI;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using SkiaSharp;
using Xunit;
using DrawingColor = System.Drawing.Color;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.FadeMaskDragRect;
using EsUtil.SaveEvidence.Parts.CapturePreview.MewUI.FadeMaskDragRect;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.MewUI.Tests.FadeMaskDragRect;

/// <summary><see cref="FadeMaskDragRectControl"/>（MewUI 版の複合コントロール）の単体テスト。</summary>
/// <remarks>
/// ウィンドウを必要としない範囲（モード切替・状態同期・配線・配置計算）を検証する。<br/>
/// マウス・キー入力はウィンドウを必要とするため、内包コントロールのテスト（<c>EsUtil.Feature.Marquee.ControlMewUI.Tests</c>）に委ねる。<br/>
/// <b>要素を生成するテストはコレクションで直列化する</b>（MewUI のビジュアル ツリーはスレッドセーフでない）。
/// </remarks>
[Collection("MewUI")]
public class FadeMaskDragRectControlTests
{
    /// <summary>テスト用の拡大元画像を生成する。</summary>
    /// <returns>生成した画像。 破棄は呼び出し側。</returns>
    private static EsUtil.Platform.Core.Imaging.EsBitmap CreateSource()
        => EsUtil.Platform.Core.Imaging.EsBitmap.Create(32, 32, DrawingColor.White);

    /// <summary>等倍（1 対象ピクセル = 1 DIP）で境界 200x200 の座標変換ポートを生成する。</summary>
    /// <returns>座標変換ポート。</returns>
    private static ICoordTransform CreatePort()
        => new CoordTransform(p => p, p => p, () => new EsSize(200, 200));

    // ====================================================================
    // 既定値とモード
    // ====================================================================

    [Fact]
    internal void 既定値_範囲選択モードで選択なし全面暗転になる()
    {
        var control = new FadeMaskDragRectControl();

        Assert.Equal(FadeMaskDragRectMode.AreaSelect, control.Mode);
        Assert.False(control.HasSelection);
        Assert.Equal(default, control.SelectionSourceRect);
        Assert.Null(control.HighlightSourceRect);
        Assert.True(control.ShowFullOverlay);
        Assert.True(control.Marquee.IsVisible);
        Assert.Null(control.LoupeCenterPoint);
    }

    /// <summary>モードに応じて選択矩形・ルーペの可視性と全面暗転が切り替わることを検証する。</summary>
    /// <param name="mode">設定する動作モード。</param>
    /// <param name="expectedShowFullOverlay">全面暗転の期待値。</param>
    /// <param name="expectedMarqueeVisible">選択矩形の可視性の期待値。</param>
    [Theory]
    [InlineData(FadeMaskDragRectMode.Crop, true, true)]
    [InlineData(FadeMaskDragRectMode.AreaSelect, true, true)]
    [InlineData(FadeMaskDragRectMode.ScreenSelect, false, false)]
    [InlineData(FadeMaskDragRectMode.WindowSelect, false, false)]
    internal void モード切替_選択矩形の可視性と全面暗転を切り替える(
        FadeMaskDragRectMode mode, bool expectedShowFullOverlay, bool expectedMarqueeVisible)
    {
        var control = new FadeMaskDragRectControl { Mode = mode };

        Assert.Equal(expectedShowFullOverlay, control.ShowFullOverlay);
        // 選択矩形を使わないモードでも暗転マスク（ハイライト）を描画するため、描画は止めず入力だけを止める
        Assert.Equal(expectedMarqueeVisible, control.Marquee.IsHitTestVisible);
        Assert.True(control.Marquee.IsVisible);
    }

    /// <summary>外観（色）を代入してもモードから導出する表示可否が保たれることを検証する。</summary>
    /// <remarks>
    /// 外観はホストが与える素材であり、モードから導出する表示可否（ハンドル・全面暗転）は本層が上書きする。<br/>
    /// 代入の順序（設定 → 外観）でモードの判断が失われると、<b>選択前は全面暗転が描かれず何も見えない</b><br/>
    /// （実際にキャプチャと切り出しで表示されない不具合として発生した）。
    /// </remarks>
    [Fact]
    internal void MarqueeAppearance_代入後もモード由来の表示可否が保たれる()
    {
        var control = new FadeMaskDragRectControl
        {
            Mode = FadeMaskDragRectMode.Crop,         // ホストは色だけを与える（表示可否は指定しない）
            Options = FadeMaskDragRectOptions.Default,
            MarqueeAppearance = MarqueeAppearance.Default with
            {
                OverlayColor = DrawingColor.FromArgb(90, 1, 2, 3),
            }
        };

        // 色は与えた値のまま、モード由来の表示可否（選択を使うモード = 全面暗転 + ハンドル）になる
        var appearance = control.Marquee.Appearance;
        Assert.Equal(90, appearance.OverlayColor.A);
        Assert.Equal((1, 2, 3), (appearance.OverlayColor.R, appearance.OverlayColor.G, appearance.OverlayColor.B));
        Assert.True(appearance.ShowFullOverlay);
        Assert.True(appearance.ShowHandles);
        Assert.True(control.ShowFullOverlay);

        // 選択を使わないモードへ変えた後に外観を代入しても、モードの判断（全面暗転なし）が残る
        control.Mode = FadeMaskDragRectMode.WindowSelect;
        control.MarqueeAppearance = MarqueeAppearance.Default with
        {
            OverlayColor = DrawingColor.FromArgb(90, 1, 2, 3),
        };

        Assert.False(control.Marquee.Appearance.ShowFullOverlay);
        Assert.False(control.Marquee.Appearance.ShowHandles);
        Assert.False(control.ShowFullOverlay);
    }

    /// <summary>拡大元画像と中心があるとき、選択矩形を使うモードでのみルーペを表示することを検証する。</summary>
    /// <param name="mode">設定する動作モード。</param>
    /// <param name="expected">ルーペを表示する場合は true。</param>
    [Theory]
    [InlineData(FadeMaskDragRectMode.Crop, true)]
    [InlineData(FadeMaskDragRectMode.AreaSelect, true)]
    [InlineData(FadeMaskDragRectMode.ScreenSelect, false)]
    [InlineData(FadeMaskDragRectMode.WindowSelect, false)]
    internal void モード切替_ルーペの表示可否を切り替える(FadeMaskDragRectMode mode, bool expected)
    {
        var control = new FadeMaskDragRectControl();
        control.SetCoordTransform(CreatePort());
        using var source = CreateSource();
        control.SetLoupeSource(source);
        control.SetLoupePosition(new EsPoint(5, 6));

        control.Mode = mode;

        Assert.Equal(expected, control.Loupe.IsVisible);
    }

    [Fact]
    internal void モード切替_同じ値を設定しても状態は変わらない()
    {
        var control = new FadeMaskDragRectControl { Mode = FadeMaskDragRectMode.AreaSelect };

        // 同じ値を再設定しても例外にならず、状態は維持される
        control.Mode = FadeMaskDragRectMode.AreaSelect;

        Assert.Equal(FadeMaskDragRectMode.AreaSelect, control.Mode);
        Assert.True(control.ShowFullOverlay);
    }

    // ====================================================================
    // 選択状態の同期
    // ====================================================================

    [Fact]
    internal void SetSelection_選択矩形を暗転マスクへ同期する()
    {
        var control = new FadeMaskDragRectControl();

        control.SetSelection(new EsRect(10, 20, 100, 80));

        Assert.True(control.HasSelection);
        Assert.Equal(new EsRect(10, 20, 100, 80), control.SelectionSourceRect);
    }

    [Fact]
    internal void SetSelection_サイズ0以下は選択なしとして扱う()
    {
        var control = new FadeMaskDragRectControl();

        control.SetSelection(new EsRect(10, 20, 0, 80));

        Assert.False(control.HasSelection);
        Assert.Equal(default, control.SelectionSourceRect);
    }

    [Fact]
    internal void ClearSelection_選択状態をクリアする()
    {
        var control = new FadeMaskDragRectControl();
        control.SetSelection(new EsRect(10, 20, 100, 80));

        control.ClearSelection();

        Assert.False(control.HasSelection);
        Assert.False(control.HasSelection);
    }

    [Fact]
    internal void SetSelection_ドラッグ選択から選択矩形へ反映される()
    {
        var control = new FadeMaskDragRectControl();

        // 選択矩形の設定はセッションが単一所有するため、設定直後に読み取れる
        control.SetSelection(new EsRect(5, 6, 30, 40));

        Assert.True(control.HasSelection);
        Assert.Equal(new EsRect(5, 6, 30, 40), control.SelectionSourceRect);
    }

    [Fact]
    internal void ハイライト_設定と取得が委譲される()
    {
        var control = new FadeMaskDragRectControl { HighlightSourceRect = new EsRect(1, 2, 3, 4) };

        Assert.Equal(new EsRect(1, 2, 3, 4), control.HighlightSourceRect);

        // ハイライトの有無は矩形の null が表す（null にすると注目矩形として使われない）
        control.HighlightSourceRect = null;

        Assert.Null(control.HighlightSourceRect);
    }

    // ====================================================================
    // 外観の委譲
    // ====================================================================

    [Fact]
    internal void 外観は選択矩形の外観へ委譲される()
    {
        var control = new FadeMaskDragRectControl
        {
            Options = FadeMaskDragRectOptions.Default with
            {
                Marquee = MarqueeOptions.Default with
                {
                    HandleSize = 12.0,
                },
            },
            // 枠線の太さ・表示可否は描画にだけ必要な設定のため、外観として与える
            MarqueeAppearance = MarqueeAppearance.Default with
            {
                ShowOuterBorder = true,
                OuterBorderThickness = 3.0,
                ShowSizeLabel = false,
            }
        };

        Assert.True(control.MarqueeAppearance.ShowOuterBorder);
        Assert.Equal(3.0, control.MarqueeAppearance.OuterBorderThickness);

        // 設定は子コントロールへ分配される（寸法は選択の設定、枠線と表示可否は外観が持つ）
        Assert.True(control.Marquee.Appearance.ShowOuterBorder);
        Assert.Equal(3.0, control.Marquee.Appearance.OuterBorderThickness);
        Assert.False(control.Marquee.Appearance.ShowSizeLabel);
        Assert.Equal(12.0, control.Marquee.Options.HandleSize);
    }

    [Fact]
    internal void 文言プロバイダ_ホストが保持する()
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

    [Fact]
    internal void ラベル文言は暗転マスクへ反映される()
    {
        var control = new FadeMaskDragRectControl();
        Func<string?> provider = () => "(10, 20) - (30, 40)";

        control.SizeLabelOverrideProvider = provider;

        Assert.Same(provider, control.SizeLabelOverrideProvider);
        // 取得側も同じ値になる（ホストが設定の再適用に使える）
        Assert.Same(provider, control.SizeLabelOverrideProvider);
    }

    [Fact]
    internal void ラベルの既定値_サイズラベルは表示でプロバイダ未設定なら自動生成になる()
    {
        var control = new FadeMaskDragRectControl();

        // サイズラベルは選択矩形コントロール単体の概念であるため選択コアが単一所有する。<br/>
        // 文言プロバイダ未設定のため注目矩形から自動生成される
        // サイズラベルの表示可否は描画にだけ必要な設定であり、外観が単一所有する
        Assert.True(control.Marquee.Appearance.ShowSizeLabel);
        Assert.Null(control.SizeLabelOverrideProvider);
    }

    // ====================================================================
    // ルーペ画像上の選択矩形破線オーバーレイ
    // ====================================================================

    [Fact]
    internal void 選択矩形破線の色_既定は白と黒()
    {
        var control = new FadeMaskDragRectControl();

        Assert.Equal(DrawingColor.White, control.Options.SelectionRectColorA);
        Assert.Equal(DrawingColor.Black, control.Options.SelectionRectColorB);
    }

    [Fact]
    internal void 選択矩形破線の色_設定が反映される()
    {
        var control = new FadeMaskDragRectControl();

        control.Options = control.Options with
        {
            SelectionRectColorA = DrawingColor.Yellow,
            SelectionRectColorB = DrawingColor.Magenta,
        };

        Assert.Equal(DrawingColor.Yellow, control.Options.SelectionRectColorA);
        Assert.Equal(DrawingColor.Magenta, control.Options.SelectionRectColorB);
    }

    [Fact]
    internal void DrawSelectionOverlay_選択がある場合は破線を描画する()
    {
        using var target = TestHelper.CreateTarget(64, 64);
        var control = new FadeMaskDragRectControl();
        control.SetSelection(new EsRect(10, 10, 20, 20));

        // 切り出し左上 (0,0)・倍率 4 → 覆う画素は (40,40)-(119,119)、枠はその 1 画素外側（(39,39)-(120,120)）
        control.DrawSelectionOverlay(target.Canvas, new EsPoint(0, 0), 4.0, 64);

        // 1 色目（白）で上辺が描画される
        Assert.Equal(new SKColor(255, 255, 255), target.GetPixel(39, 39));
        Assert.NotEqual(new SKColor(0, 0, 0, 0), target.GetPixel(39, 40));
        // 選択矩形の内側は描画されない
        Assert.Equal(new SKColor(0, 0, 0, 0), target.GetPixel(50, 50));
    }

    [Fact]
    internal void DrawSelectionOverlay_選択がない場合は描画しない()
    {
        using var target = TestHelper.CreateTarget(64, 64);
        var control = new FadeMaskDragRectControl();

        control.DrawSelectionOverlay(target.Canvas, new EsPoint(0, 0), 4.0, 64);

        Assert.Equal(0, target.CountColor(new SKColor(255, 255, 255)));
    }

    [Fact]
    internal void DrawSelectionOverlay_選択矩形を使わないモードでは描画しない()
    {
        using var target = TestHelper.CreateTarget(64, 64);
        var control = new FadeMaskDragRectControl { Mode = FadeMaskDragRectMode.WindowSelect };
        control.SetSelection(new EsRect(10, 10, 20, 20));

        control.DrawSelectionOverlay(target.Canvas, new EsPoint(0, 0), 4.0, 64);

        Assert.Equal(0, target.CountColor(new SKColor(255, 255, 255)));
    }

    [Fact]
    internal void DrawSelectionOverlay_設定した色で破線を描画する()
    {
        using var target = TestHelper.CreateTarget(64, 64);
        var control = new FadeMaskDragRectControl
        {
            Options = FadeMaskDragRectOptions.Default with
            {
                SelectionRectColorA = DrawingColor.Red,
                SelectionRectColorB = DrawingColor.Blue,
            },
        };
        control.SetSelection(new EsRect(10, 10, 20, 20));

        control.DrawSelectionOverlay(target.Canvas, new EsPoint(0, 0), 4.0, 64);

        // 上辺は y = 39 から。 破線は 2 ピクセル描画 + 2 ピクセル空白の周期で色が切り替わる
        Assert.Equal(new SKColor(255, 0, 0), target.GetPixel(39, 39));
        Assert.Equal(new SKColor(0, 0, 255), target.GetPixel(41, 39));
    }

    // ====================================================================
    // ログ
    // ====================================================================

    [Fact]
    internal void Log_既定はNullLoggerで例外にならない()
    {
        var control = new FadeMaskDragRectControl();

        // ログ出力を伴う操作を実行しても例外にならない（既定は NullLogger）
        control.Options = control.Options with
        {
            Marquee = control.Options.Marquee with { ClampToSourceBounds = false },
        };
        control.SetSelection(new EsRect(10, 10, 50, 40));
        control.RequestCancel();
    }

    [Fact]
    internal void Log_内包するドラッグ選択コントロールと同一のロガーになる()
    {
        var control = new FadeMaskDragRectControl();

        Assert.Same(control.Marquee.Log, control.Log);

        control.Log = null!;

        // null は NullLogger へフォールバックし、内包コントロールと同一のインスタンスを共有する
        Assert.NotNull(control.Log);
        Assert.Same(control.Marquee.Log, control.Log);
    }

    [Fact]
    internal void Log_内包するドラッグ選択コントロールとルーペへ伝搬される()
    {
        var control = new FadeMaskDragRectControl();
        var logger = new RecordingLogger();

        control.Log = logger;

        // 入力とルーペは同一のロガーを共有する（ホストは 1 箇所の設定で済む）
        Assert.Same(logger, control.Log);
        Assert.Same(logger, control.Marquee.Log);
        Assert.Same(logger, control.Loupe.Log);
    }

    [Fact]
    internal void Log_選択矩形コントロールへ伝搬する()
    {
        var control = new FadeMaskDragRectControl();
        var logger = new RecordingLogger();

        control.Log = logger;

        // 選択矩形コントロールはドラッグ操作と外側の装飾の描画を担うため、ログも同じ範囲へ伝搬する
        Assert.Same(logger, control.Marquee.Log);
    }

    // ====================================================================
    // 座標変換ポート
    // ====================================================================

    [Fact]
    internal void SetCoordTransform_選択矩形コントロールへ伝搬する()
    {
        var control = new FadeMaskDragRectControl();
        var port = CreatePort();

        control.SetCoordTransform(port);

        // 選択矩形コントロール（内包コントロール）が同じポートを受け取る
        Assert.Same(port, GetCoordPort(control.Marquee));
    }

    [Fact]
    internal void SetCoordTransform_nullは未設定として渡される()
    {
        var control = new FadeMaskDragRectControl();
        control.SetCoordTransform(CreatePort());

        control.SetCoordTransform(null);

        // null（未設定）を倍率から解決する規則はコアが単一所有するため、本クラスは置換せずそのまま渡す
        Assert.Null(GetCoordPort(control.Marquee));
    }

    /// <summary>座標変換ポート（コアのファサードが内部保持）をリフレクションで取得する。</summary>
    /// <param name="control">対象のコントロール。</param>
    /// <returns>保持している座標変換ポート。 未設定の場合は null。</returns>
    /// <remarks>
    /// ポートは公開プロパティではなくメソッドで設定し、コアのファサード（<c>MarqueeCore</c>）が内部で保持する（読み取り API を持たない）。<br/>
    /// 提示層（Presenter）がコアを内包するため、提示層の内部フィールドを経由してコアまで辿る。<br/>
    /// 検証は Avalonia 版の複合コントロールテストと同じくリフレクションで行う。
    /// </remarks>
    private static ICoordTransform? GetCoordPort(MarqueeControl control)
    {
        var anyInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var presenter = typeof(MarqueeControl).GetField("_marquee", anyInstance)!.GetValue(control)!;
        var core = presenter.GetType().GetField("_core", anyInstance)!.GetValue(presenter)!;
        return (ICoordTransform?)core.GetType().GetField("_coordPort", anyInstance)!.GetValue(core);
    }

    // ====================================================================
    // ルーペ
    // ====================================================================

    [Fact]
    internal void ルーペ_画像未設定では表示しない()
    {
        var control = new FadeMaskDragRectControl();
        control.SetCoordTransform(CreatePort());

        control.SetLoupePosition(new EsPoint(50, 60));

        Assert.Equal(new EsPoint(50, 60), control.LoupeCenterPoint);
        Assert.False(control.Loupe.IsVisible);
    }

    [Fact]
    internal void ルーペ_画像設定後は表示する()
    {
        var control = new FadeMaskDragRectControl();
        control.SetCoordTransform(CreatePort());

        using var source = CreateSource();
        control.SetLoupeSource(source);
        control.SetLoupePosition(new EsPoint(50, 60));

        Assert.True(control.Loupe.IsVisible);
        Assert.Same(source, control.Loupe.Source);
        Assert.Equal(new EsPoint(50, 60), control.Loupe.CenterPoint);
    }

    [Fact]
    internal void SetLoupeSource_解除すると非表示になる()
    {
        var control = new FadeMaskDragRectControl();
        control.SetCoordTransform(CreatePort());
        using var source = CreateSource();
        control.SetLoupeSource(source);
        control.SetLoupePosition(new EsPoint(50, 60));
        Assert.True(control.Loupe.IsVisible);

        // 拡大元画像を解除すると表示を続けない（表示可否はコアが判断する）
        control.SetLoupeSource(null);

        Assert.False(control.Loupe.IsVisible);
    }

    [Fact]
    internal void ClearLoupe_画像と中心を破棄する()
    {
        var control = new FadeMaskDragRectControl();
        control.SetCoordTransform(CreatePort());
        using var source = CreateSource();
        control.SetLoupeSource(source);
        control.SetLoupePosition(new EsPoint(50, 60));

        control.ClearLoupe();

        Assert.Null(control.LoupeCenterPoint);
        Assert.Null(control.Loupe.Source);
        Assert.False(control.Loupe.IsVisible);
    }

    [Fact]
    internal void ルーペ_中心とオフセットの位置へ配置する()
    {
        var control = new FadeMaskDragRectControl();
        control.SetCoordTransform(CreatePort());
        control.Options = control.Options with
        {
            Loupe = control.Options.Loupe with { RequestedSize = 100 },
            LoupeOffset = 20.0,
        };

        using var source = CreateSource();
        control.SetLoupeSource(source);
        control.SetLoupePosition(new EsPoint(50, 60));

        control.Measure(new Size(400, 400));
        control.Arrange(new Rect(0, 0, 400, 400));

        // 中心 (50,60) + オフセット 20 = 左上 (70,80)・一辺 100（親領域 400x400 に収まる）
        Assert.Equal(70, control.Loupe.Bounds.X);
        Assert.Equal(80, control.Loupe.Bounds.Y);
        Assert.Equal(100, control.Loupe.Bounds.Width);
        Assert.Equal(100, control.Loupe.Bounds.Height);
    }

    /// <summary>複合コントロール経由でもルーペの中心座標ラベルがテキスト要素として配置されることを検証する。</summary>
    /// <remarks>文言・表示可否・配置はコアが単一所有し、ルーペコントロールが要素を置く（統合経路の検証）。</remarks>
    [Fact]
    internal void ルーペ_中心座標ラベルの要素を配置する()
    {
        var control = new FadeMaskDragRectControl();
        control.SetCoordTransform(CreatePort());
        control.Options = control.Options with { Loupe = control.Options.Loupe with { RequestedSize = 100 } };

        using var source = CreateSource();
        control.SetLoupeSource(source);
        control.SetLoupePosition(new EsPoint(50, 60));

        control.Measure(new Size(400, 400));
        control.Arrange(new Rect(0, 0, 400, 400));

        // ルーペの子は図形キャンバス + 中心座標ラベル（Border + TextBlock）の 2 つ
        var label = Assert.Single(control.Loupe.Children.OfType<Border>());
        var text = Assert.IsType<TextBlock>(label.Child);

        // 文言はコアの EsPoint.Format が単一所有する
        Assert.Equal("(50, 60)", text.Text);
        // ルーペの内側（左上）へ配置され、大きさを持つ（子の座標は絶対座標のため相対へ戻して判定する）
        Assert.True(label.Bounds.Width > 0);
        Assert.True(label.Bounds.Height > 0);
        var relativeX = label.Bounds.X - control.Loupe.Bounds.X;
        var relativeY = label.Bounds.Y - control.Loupe.Bounds.Y;
        Assert.InRange(relativeX, 0, control.Loupe.Bounds.Width - label.Bounds.Width);
        Assert.InRange(relativeY, 0, control.Loupe.Bounds.Height - label.Bounds.Height);
    }

    [Fact]
    internal void ルーペ_親領域の右端を超える場合は内側へクランプする()
    {
        var control = new FadeMaskDragRectControl();
        control.SetCoordTransform(CreatePort());
        control.Options = control.Options with
        {
            Loupe = control.Options.Loupe with { RequestedSize = 100 },
            LoupeOffset = 20.0,
        };

        using var source = CreateSource();
        control.SetLoupeSource(source);
        control.SetLoupePosition(new EsPoint(300, 300));

        control.Measure(new Size(200, 200));
        control.Arrange(new Rect(0, 0, 200, 200));

        // 中心 + オフセット = 320 はクランプされ、親領域（200）− 一辺（100） = 100 になる
        Assert.Equal(100, control.Loupe.Bounds.X);
        Assert.Equal(100, control.Loupe.Bounds.Y);
    }

    [Fact]
    internal void ルーペ_画像未設定では配置しない()
    {
        var control = new FadeMaskDragRectControl();
        control.Options = control.Options with { Loupe = control.Options.Loupe with { RequestedSize = 100 } };

        control.Measure(new Size(400, 400));
        control.Arrange(new Rect(0, 0, 400, 400));

        Assert.Equal(0, control.Loupe.Bounds.Width);
    }

    [Fact]
    internal void ルーペ_表示しないモードでは配置しない()
    {
        var control = new FadeMaskDragRectControl { Mode = FadeMaskDragRectMode.ScreenSelect };
        control.Options = control.Options with { Loupe = control.Options.Loupe with { RequestedSize = 100 } };
        using var source = CreateSource();
        control.SetLoupeSource(source);
        control.SetLoupePosition(new EsPoint(10, 10));

        control.Measure(new Size(400, 400));
        control.Arrange(new Rect(0, 0, 400, 400));

        Assert.False(control.Loupe.IsVisible);
        Assert.Equal(0, control.Loupe.Bounds.Width);
    }

    // ====================================================================
    // ホストへの通知
    // ====================================================================

    [Fact]
    internal void ConfirmRequested_確定時に選択矩形を通知する()
    {
        var control = new FadeMaskDragRectControl();
        FadeMaskDragRectSelectionEventArgs? raised = null;
        control.ConfirmRequested += (_, e) => raised = e;

        control.SetSelection(new EsRect(10, 20, 100, 80));
        control.RequestConfirm();

        Assert.NotNull(raised);
        Assert.Equal(new EsRect(10, 20, 100, 80), raised!.SelectionSourceRect);
    }

    [Fact]
    internal void CancelRequested_選択なしでも通知する()
    {
        var control = new FadeMaskDragRectControl();
        var raised = false;
        control.CancelRequested += (_, _) => raised = true;

        control.RequestCancel();

        Assert.True(raised);
    }

    [Fact]
    internal void LoupePositionMoved_選択変更では通知しない()
    {
        var control = new FadeMaskDragRectControl();
        var raised = false;
        control.LoupePositionMoved += (_, _) => raised = true;

        // ルーペ位置の通知はドラッグ選択のマウス移動に由来する（選択変更では発火しない）
        control.SetSelection(new EsRect(10, 20, 100, 80));
        control.SetSelection(new EsRect(30, 40, 50, 60));

        Assert.False(raised);
    }

    [Fact]
    internal void RefreshOverlay_例外なく完了する()
    {
        var control = new FadeMaskDragRectControl();
        control.SetCoordTransform(CreatePort());
        control.SetSelection(new EsRect(10, 20, 100, 80));

        control.RefreshOverlay();

        // 選択状態は維持される（ビューポート変化では再同期しない）
        Assert.True(control.HasSelection);
    }

    [Fact]
    internal void InvalidateOverlay_例外なく完了する()
    {
        var control = new FadeMaskDragRectControl();

        control.InvalidateOverlay();

        Assert.True(control.ShowFullOverlay);
    }
}
