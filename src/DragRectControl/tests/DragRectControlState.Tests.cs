using Estable.AvaloniaUI.Platform;
using Avalonia.Media;
using Rect = Estable.RectD;
using Estable;
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
        => new() { Width = 200, Height = 200, HandleHitRadius = 12.0, MinSelectionSize = 10.0 };

    // ====================================================================
    // 依存プロパティのデフォルト値
    // ====================================================================

    [Fact]
    public void 依存プロパティのデフォルト値が正しい()
    {
        var control = CreateControl();

        // 枠線の既定は DarkGray（設定画面の選択枠の色と一致）
        Assert.Equal(Color.FromAColor(0xFF, Colors.DarkGray), control.BorderColor);
        Assert.Equal(2.0, control.BorderThickness);
        Assert.Equal(Colors.White, control.HandleColor);
        Assert.Equal(10.0, control.HandleSize);
        Assert.Equal(12.0, control.HandleHitRadius);
        Assert.Equal(10.0, control.MinSelectionSize); // CreateControl で設定
        Assert.True(control.ClampToDomainBounds);
        Assert.Equal(Colors.White, control.MarkerColor);
        Assert.Equal(16.0, control.MarkerLength);
        Assert.False(control.ShowCoordLabel);
        Assert.Equal(Colors.White, control.CoordLabelColor);
        // 背景色は不透明度をアルファに統合（黒 60% = #99000000）
        Assert.Equal(Color.FromAColor(153, Colors.Black), control.CoordLabelBackgroundColor);
        Assert.Equal(12.0, control.CoordLabelFontSize);
        Assert.NotNull(control.Log);
    }

    [Fact]
    public void 外観プロパティを設定すると反映される()
    {
        var control = CreateControl();

        control.BorderColor = Colors.Red;
        control.BorderThickness = 3.0;
        control.HandleColor = Colors.Blue;
        control.HandleSize = 14.0;
        control.HandleHitRadius = 20.0;
        control.MinSelectionSize = 5.0;
        control.ClampToDomainBounds = true;
        control.MarkerColor = Colors.Yellow;
        control.MarkerLength = 24.0;

        Assert.Equal(Colors.Red, control.BorderColor);
        Assert.Equal(3.0, control.BorderThickness);
        Assert.Equal(Colors.Blue, control.HandleColor);
        Assert.Equal(14.0, control.HandleSize);
        Assert.Equal(20.0, control.HandleHitRadius);
        Assert.Equal(5.0, control.MinSelectionSize);
        Assert.True(control.ClampToDomainBounds);
        Assert.Equal(Colors.Yellow, control.MarkerColor);
        Assert.Equal(24.0, control.MarkerLength);

        // カーソル座標ラベル
        control.ShowCoordLabel = true;
        control.CoordLabelColor = Colors.Cyan;
        control.CoordLabelBackgroundColor = Color.FromAColor(128, Colors.DarkGreen);
        control.CoordLabelFontSize = 14.0;
        Assert.True(control.ShowCoordLabel);
        Assert.Equal(Colors.Cyan, control.CoordLabelColor);
        Assert.Equal(Color.FromAColor(128, Colors.DarkGreen), control.CoordLabelBackgroundColor);
        Assert.Equal(14.0, control.CoordLabelFontSize);
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
        Assert.Equal(new RectD(10, 10, 50, 40), snap1.SelectionRect);
        Assert.True(snap1.HasSelection);
        Assert.False(snap1.IsDragging);
        Assert.False(snap1.NudgeActive);
    }

    [Fact]
    public void DragRectSnapshot_HasSelection_幅か高さが0ならfalseを返す()
    {
        // Width>0 かつ Height=0（幅のみの矩形は選択として無効）
        var snap1 = new DragRectSnapshot(DragRectInteractionState.Selected, new RectD(0, 0, 100, 0), DragHandle.None, default, false, false, false);
        Assert.False(snap1.HasSelection);

        // Width=0 かつ Height>0（高さのみの矩形は選択として無効）
        var snap2 = new DragRectSnapshot(DragRectInteractionState.Selected, new RectD(0, 0, 0, 100), DragHandle.None, default, false, false, false);
        Assert.False(snap2.HasSelection);
    }

    [Fact]
    public void DragRectSnapshot_MousePositionが取得できる()
    {
        var snap = new DragRectSnapshot(DragRectInteractionState.Selected, default, DragHandle.None, new PointD(5, 7), false, false, false);

        Assert.Equal(new PointD(5, 7), snap.MousePosition);
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
        control.CoordLabelFontSize = double.NaN;
        Assert.Equal(12.0, control.CoordLabelFontSize);

        // 上限 48 にクランプ
        control.CoordLabelFontSize = 100.0;
        Assert.Equal(48.0, control.CoordLabelFontSize);

        // 下限 6 にクランプ
        control.CoordLabelFontSize = 1.0;
        Assert.Equal(6.0, control.CoordLabelFontSize);
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
        Assert.Equal(new RectD(10, 10, 50, 40), raised!.Rect);
    }

    [Fact]
    public void SetSelection_ClampToDomainBounds有効時は画像外の矩形をクリップする()
    {
        var control = CreateControl();
        control.ClampToDomainBounds = true;
        control.SetCoordinateTransform(new CoordinatePort(p => p, p => p, () => new SizeD(200, 200)));
        SelectionRectEventArgs? raised = null;
        control.SelectionChanged += (_, e) => raised = e;

        // 画像外にはみ出す矩形（右下が 200 超え）を復元 → 境界内 (200,200) にクリップされる
        control.SetSelection(new Rect(150, 150, 100, 100));

        Assert.NotNull(raised);
        Assert.Equal(new RectD(150, 150, 50, 50), raised!.Rect);
    }

    [Fact]
    public void ClampToDomainBounds_ON切替時に既存の選択矩形がクリップされる()
    {
        var control = CreateControl();
        control.SetCoordinateTransform(new CoordinatePort(p => p, p => p, () => new SizeD(200, 200)));
        var rects = new List<RectD>();
        control.SelectionChanged += (_, e) => rects.Add(e.Rect);

        // 既定は ON のため、OFF → ON の切替シナリオを明示する
        control.ClampToDomainBounds = false;

        // 画像外にはみ出す矩形（左上が負・右下が 200 超え）を選択状態にする（OFF のためクリップされない）
        control.SetSelection(new Rect(-50, -30, 300, 260));
        Assert.Equal(new RectD(-50, -30, 300, 260), rects[^1]);

        // ON に切り替えた瞬間に境界内 (0,0)-(200,200) へクリップされる
        control.ClampToDomainBounds = true;

        Assert.Equal(new RectD(0, 0, 200, 200), rects[^1]);
    }

    [Fact]
    public void ClampToDomainBounds_ON切替時に境界内の矩形は変更されない()
    {
        var control = CreateControl();
        control.SetCoordinateTransform(new CoordinatePort(p => p, p => p, () => new SizeD(200, 200)));
        var rects = new List<RectD>();
        control.SelectionChanged += (_, e) => rects.Add(e.Rect);

        control.SetSelection(new Rect(20, 20, 100, 80));
        Assert.Single(rects);

        // 境界内の矩形なので、ON に切り替えてもクリップされない（SelectionChanged は再発火しない）
        control.ClampToDomainBounds = true;

        Assert.Single(rects);
        Assert.Equal(new RectD(20, 20, 100, 80), rects[^1]);
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
        Assert.Equal(new RectD(10, 10, 50, 40), raised!.Snapshot.SelectionRect);
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
        Assert.Equal(new RectD(10, 10, 50, 40), raised!.Rect);
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
