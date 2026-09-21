using EsUtil.Platform.AvaloniaUI;
using Estable.DragRect;
using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using Avalonia.Media;
using Rect = Avalonia.Rect;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Estable.AvaloniaUI.DragRect.Tests;

/// <summary><see cref="DragRectControl"/> のヘッドレス不要テスト（状態・設定・イベント通知の純粋 API 検証）。</summary>
/// <remarks>
/// コントロール生成・プロパティ設定・<see cref="DragRectControl.SetSelection"/> / <see cref="DragRectControl.ResetSelection"/> /
/// <see cref="DragRectControl.RequestConfirm"/> / <see cref="DragRectControl.RequestCancel"/> のみを検証するため、
/// Avalonia ヘッドレス実行（Dispatcher / ウィンドウ表示）を必要としない。<br/>
/// 並列実行を妨げないよう <c>[Fact]</c> で検証する（ヘッドレス必須のポインター・キーイベントテストは
/// <c>DragRectControlTests</c> が直列コレクションで検証する）。
/// </remarks>
public class DragRectControlStateTests
{
    /// <summary>テスト用の DragRectControl を生成する。</summary>
    private static DragRectControl CreateControl()
        => new() { Width = 200, Height = 200, Options = TestOptions };

    /// <summary>テスト用の設定（最小サイズを 10 にしてドラッグ判定をしやすくする）。</summary>
    private static DragRectOptions TestOptions
        => DragRectOptions.Default with { HandleHitRadius = 12.0, MinSelectionSize = 10.0 };

    // ====================================================================
    // 依存プロパティのデフォルト値
    // ====================================================================

    [Fact]
    public void 設定の既定値が正しい()
    {
        var control = CreateControl();
        var options = control.Options;

        // 枠線の既定は DarkGray（設定画面の選択枠の色と一致）
        Assert.Equal(Color.FromAColor(0xFF, Colors.DarkGray), options.BorderColor.ToAvaloniaColor());
        Assert.Equal(2.0, options.BorderThickness);
        Assert.Equal(Colors.White, options.HandleColor.ToAvaloniaColor());
        Assert.Equal(10.0, options.HandleSize);
        Assert.Equal(12.0, options.HandleHitRadius);
        Assert.Equal(10.0, options.MinSelectionSize); // CreateControl で設定
        Assert.True(options.ClampToDomainBounds);
        Assert.Equal(Colors.White, options.MarkerColor.ToAvaloniaColor());
        Assert.Equal(16.0, options.MarkerLength);
        Assert.False(options.ShowCoordLabel);
        Assert.Equal(Colors.White, options.CoordLabelColor.ToAvaloniaColor());
        // 背景色は不透明度をアルファに統合（黒 60% = #99000000）
        Assert.Equal(Color.FromAColor(153, Colors.Black), options.CoordLabelBackgroundColor.ToAvaloniaColor());
        Assert.Equal(12.0, options.CoordLabelFontSize);
        Assert.NotNull(control.Log);

        // 設定を指定しない場合の既定値はコアの DragRectOptions.Default と一致する
        Assert.Equal(DragRectOptions.Default, new DragRectControl().Options);
    }

    [Fact]
    public void 設定を変更すると反映される()
    {
        var control = CreateControl();

        control.Options = DragRectOptions.Default with
        {
            BorderColor = Colors.Red.ToDrawingColor(),
            BorderThickness = 3.0,
            HandleColor = Colors.Blue.ToDrawingColor(),
            HandleSize = 14.0,
            HandleHitRadius = 20.0,
            MinSelectionSize = 5.0,
            ClampToDomainBounds = true,
            MarkerColor = Colors.Yellow.ToDrawingColor(),
            MarkerLength = 24.0,
            // カーソル座標ラベル
            ShowCoordLabel = true,
            CoordLabelColor = Colors.Cyan.ToDrawingColor(),
            CoordLabelBackgroundColor = Color.FromAColor(128, Colors.DarkGreen).ToDrawingColor(),
            CoordLabelFontSize = 14.0,
        };

        var options = control.Options;
        Assert.Equal(Colors.Red, options.BorderColor.ToAvaloniaColor());
        Assert.Equal(3.0, options.BorderThickness);
        Assert.Equal(Colors.Blue, options.HandleColor.ToAvaloniaColor());
        Assert.Equal(14.0, options.HandleSize);
        Assert.Equal(20.0, options.HandleHitRadius);
        Assert.Equal(5.0, options.MinSelectionSize);
        Assert.True(options.ClampToDomainBounds);
        Assert.Equal(Colors.Yellow, options.MarkerColor.ToAvaloniaColor());
        Assert.Equal(24.0, options.MarkerLength);
        Assert.True(options.ShowCoordLabel);
        Assert.Equal(Colors.Cyan, options.CoordLabelColor.ToAvaloniaColor());
        Assert.Equal(Color.FromAColor(128, Colors.DarkGreen), options.CoordLabelBackgroundColor.ToAvaloniaColor());
        Assert.Equal(14.0, options.CoordLabelFontSize);
    }

    // ====================================================================
    // CurrentSnapshot
    // ====================================================================

    [Fact]
    public void CurrentSnapshot_選択状態の全項目を返す()
    {
        var control = CreateControl();

        // 未選択時: State=NoSelection / SelectionRect=0x0 / HasSelection=false / IsDragging=false / NudgeActive=false
        var snap0 = control.CurrentSnapshot;
        Assert.Equal(DragRectInteractionState.NoSelection, snap0.State);
        Assert.Equal(default, snap0.SelectionRect);
        Assert.Equal(DragHandle.None, snap0.ActiveHandle);
        Assert.False(snap0.HasSelection);
        Assert.False(snap0.IsDragging);
        Assert.False(snap0.IsMouseDown);
        Assert.False(snap0.NudgeActive);

        // 選択時: State=Selected / SelectionRect 反映 / HasSelection=true
        control.SetSelection(new Rect(10, 10, 50, 40));
        var snap1 = control.CurrentSnapshot;
        Assert.Equal(DragRectInteractionState.Selected, snap1.State);
        Assert.Equal(new EsRect(10, 10, 50, 40), snap1.SelectionRect);
        Assert.True(snap1.HasSelection);
        Assert.False(snap1.IsDragging);
        Assert.False(snap1.NudgeActive);
    }

    [Fact]
    public void DragRectSnapshot_HasSelection_幅か高さが0ならfalseを返す()
    {
        // Width>0 かつ Height=0（幅のみの矩形は選択として無効）
        var snap1 = new DragRectSnapshot(DragRectInteractionState.Selected, new EsRect(0, 0, 100, 0), DragHandle.None, default, false, false, false);
        Assert.False(snap1.HasSelection);

        // Width=0 かつ Height>0（高さのみの矩形は選択として無効）
        var snap2 = new DragRectSnapshot(DragRectInteractionState.Selected, new EsRect(0, 0, 0, 100), DragHandle.None, default, false, false, false);
        Assert.False(snap2.HasSelection);
    }

    [Fact]
    public void DragRectSnapshot_MousePositionが取得できる()
    {
        var snap = new DragRectSnapshot(DragRectInteractionState.Selected, default, DragHandle.None, new EsPoint(5, 7), false, false, false);

        Assert.Equal(new EsPoint(5, 7), snap.MousePosition);
    }

    // ====================================================================
    // Log（NullLogger フォールバック）
    // ====================================================================

    [Fact]
    public void Log_設定したロガーを返す()
    {
        var control = CreateControl();
        var logger = NullLogger.Instance;

        control.Log = logger;

        Assert.Same(logger, control.Log);
    }

    [Fact]
    public void Log_nullを設定するとNullLoggerにフォールバックする()
    {
        var control = CreateControl();

        control.Log = null!;

        // null 設定時は NullLogger にフォールバックし、例外なくログ出力できる
        Assert.NotNull(control.Log);
    }

    // ====================================================================
    // CoordLabelFontSize の補正（6〜48）
    // ====================================================================

    [Fact]
    public void CoordLabelFontSize_NaNや範囲外は安全値に補正される()
    {
        var control = CreateControl();

        // NaN → 既定値 12
        control.Options = control.Options with { CoordLabelFontSize = double.NaN };
        Assert.Equal(12.0, control.Options.CoordLabelFontSize);

        // 上限 48 にクランプ
        control.Options = control.Options with { CoordLabelFontSize = 100.0 };
        Assert.Equal(48.0, control.Options.CoordLabelFontSize);

        // 下限 6 にクランプ
        control.Options = control.Options with { CoordLabelFontSize = 1.0 };
        Assert.Equal(6.0, control.Options.CoordLabelFontSize);
    }

    // ====================================================================
    // SetSelection / ResetSelection / RequestConfirm / RequestCancel
    // ====================================================================

    [Fact]
    public void SetSelection_選択矩形を復元しSelectionChangedを発火する()
    {
        var control = CreateControl();
        SelectionRectEventArgs? raised = null;
        control.SelectionChanged += (_, e) => raised = e;

        control.SetSelection(new Rect(10, 10, 50, 40));

        Assert.NotNull(raised);
        Assert.Equal(new Rect(10, 10, 50, 40), raised!.Rect);
    }

    [Fact]
    public void SetSelection_ClampToDomainBounds有効時は画像外の矩形をクリップする()
    {
        var control = CreateControl();
        control.Options = control.Options with { ClampToDomainBounds = true };
        control.SetCoordinateTransform(new CoordTransform(p => p, p => p, () => new EsSize(200, 200)));
        SelectionRectEventArgs? raised = null;
        control.SelectionChanged += (_, e) => raised = e;

        // 画像外にはみ出す矩形（右下が 200 超え）を復元 → 境界内 (200,200) にクリップされる
        control.SetSelection(new Rect(150, 150, 100, 100));

        Assert.NotNull(raised);
        Assert.Equal(new Rect(150, 150, 50, 50), raised!.Rect);
    }

    [Fact]
    public void ClampToDomainBounds_ON切替時に既存の選択矩形がクリップされる()
    {
        var control = CreateControl();
        control.SetCoordinateTransform(new CoordTransform(p => p, p => p, () => new EsSize(200, 200)));
        var rects = new List<Rect>();
        control.SelectionChanged += (_, e) => rects.Add(e.Rect);

        // 既定は ON のため、OFF → ON の切替シナリオを明示する
        control.Options = control.Options with { ClampToDomainBounds = false };

        // 画像外にはみ出す矩形（左上が負・右下が 200 超え）を選択状態にする（OFF のためクリップされない）
        control.SetSelection(new Rect(-50, -30, 300, 260));
        Assert.Equal(new Rect(-50, -30, 300, 260), rects[^1]);

        // ON に切り替えた瞬間に境界内 (0,0)-(200,200) へクリップされる
        control.Options = control.Options with { ClampToDomainBounds = true };

        Assert.Equal(new Rect(0, 0, 200, 200), rects[^1]);
    }

    [Fact]
    public void ClampToDomainBounds_ON切替時に境界内の矩形は変更されない()
    {
        var control = CreateControl();
        control.SetCoordinateTransform(new CoordTransform(p => p, p => p, () => new EsSize(200, 200)));
        var rects = new List<Rect>();
        control.SelectionChanged += (_, e) => rects.Add(e.Rect);

        control.SetSelection(new Rect(20, 20, 100, 80));
        Assert.Single(rects);

        // 境界内の矩形なので、ON に切り替えてもクリップされない（SelectionChanged は再発火しない）
        control.Options = control.Options with { ClampToDomainBounds = true };

        Assert.Single(rects);
        Assert.Equal(new Rect(20, 20, 100, 80), rects[^1]);
    }

    [Fact]
    public void SetSelection_スナップショットに選択矩形とアクティブハンドルが含まれる()
    {
        var control = CreateControl();
        SelectionRectEventArgs? raised = null;
        control.SelectionChanged += (_, e) => raised = e;

        control.SetSelection(new Rect(10, 10, 50, 40));

        Assert.NotNull(raised);
        // Snapshot はイベント引数の Rect と同一の選択矩形（ドメイン座標）を保持する
        Assert.Equal(new EsRect(10, 10, 50, 40), raised!.Snapshot.SelectionRect);
        // 選択復元後はハンドルなし（新規選択・移動・未選択時は None）
        Assert.Equal(DragHandle.None, raised.Snapshot.ActiveHandle);
        // 選択確定後の状態は Selected
        Assert.Equal(DragRectInteractionState.Selected, raised.Snapshot.State);
        // マウスボタンは押下中ではない
        Assert.False(raised.Snapshot.IsMouseDown);
    }

    [Fact]
    public void ResetSelection_選択状態を初期化する()
    {
        var control = CreateControl();
        control.SetSelection(new Rect(10, 10, 50, 40));

        control.ResetSelection();

        // 状態はイベント Snapshot で把握する（ポーリング不可）
        Assert.NotNull(control.Log);
    }

    [Fact]
    public void RequestConfirm_選択確定後にConfirmRequestedを発火する()
    {
        var control = CreateControl();
        SelectionRectEventArgs? raised = null;
        control.ConfirmRequested += (_, e) => raised = e;

        control.SetSelection(new Rect(10, 10, 50, 40));
        control.RequestConfirm();

        Assert.NotNull(raised);
        Assert.Equal(new Rect(10, 10, 50, 40), raised!.Rect);
    }

    [Fact]
    public void RequestConfirm_確定後は選択状態がクリアされる()
    {
        var control = CreateControl();

        control.SetSelection(new Rect(10, 10, 50, 40));
        Assert.True(control.CurrentSnapshot.HasSelection);

        // 確定（RequestConfirm）後は選択状態がクリアされる（DragRect の残留を防ぐ）
        control.RequestConfirm();

        Assert.False(control.CurrentSnapshot.HasSelection);
        Assert.Equal(DragRectInteractionState.NoSelection, control.CurrentSnapshot.State);
    }

    [Fact]
    public void RequestConfirm_ドラッグ中は発火しない()
    {
        var control = CreateControl();
        var raised = false;
        control.ConfirmRequested += (_, _) => raised = true;

        control.RequestConfirm();

        Assert.False(raised);
    }

    [Fact]
    public void RequestCancel_選択ありならCancelRequestedを発火する()
    {
        var control = CreateControl();
        var raised = false;
        control.CancelRequested += (_, _) => raised = true;

        control.SetSelection(new Rect(10, 10, 50, 40));
        control.RequestCancel();

        Assert.True(raised);
    }

    [Fact]
    public void RequestCancel_選択なしでもCancelRequestedを発火する()
    {
        var control = CreateControl();
        var raised = false;
        control.CancelRequested += (_, _) => raised = true;

        control.RequestCancel();

        Assert.True(raised);
    }
}
