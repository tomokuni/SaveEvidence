using Estable.DragRect;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace Estable.MewUI.DragRect.Tests;

/// <summary>テストで使用するカーソルサービスのスパイ（呼び出しを記録する）。</summary>
/// <remarks>位置の取得・移動は記録のみ行い、表示切替の呼び出し履歴を検証に使用する。</remarks>
internal sealed class CursorServiceSpy : ICursorService
{
    /// <summary><see cref="ShowCursor"/> に渡された値の履歴（呼び出し順）。</summary>
    public List<bool> ShowCalls { get; } = [];

    /// <summary>カーソル位置の設定履歴（呼び出し順）。</summary>
    public List<EsPixelPoint> MovedCalls { get; } = [];

    /// <summary><see cref="ICursorService.GetCursorScreenPoint"/> の戻り値。</summary>
    public EsPixelPoint CursorPoint { get; set; }

    /// <inheritdoc/>
    public EsPixelPoint GetCursorScreenPoint() => CursorPoint;

    /// <inheritdoc/>
    public void SetCursorScreenPoint(EsPixelPoint point)
    {
        CursorPoint = point;
        MovedCalls.Add(point);
    }

    /// <inheritdoc/>
    public void ShowCursor(bool show) => ShowCalls.Add(show);
}

/// <summary><see cref="DragRectControl"/>（MewUI 版）の単体テスト（公開 API 経由の状態・イベント検証）。</summary>
/// <remarks>
/// コントロールの生成とプロパティ設定・公開メソッドのみを検証するため、ウィンドウ表示を必要としない。<br/>
/// 入力（マウス・キー）はウィンドウを必要とするため、公開 API 経由で検証できる範囲に限定する。<br/>
/// カーソルサービスはスパイを注入する（Win32 実装を生成しない）。
/// </remarks>
public class DragRectControlTests
{
    /// <summary>テスト用の DragRectControl を生成する。</summary>
    /// <param name="cursor">カーソルサービスのスパイ。</param>
    /// <returns>生成したコントロール。</returns>
    private static DragRectControl CreateControl(CursorServiceSpy cursor)
        => new(cursor) { Width = 200, Height = 200, Options = TestOptions };

    /// <summary>テスト用の設定（最小サイズを 10 にしてドラッグ判定をしやすくする）。</summary>
    private static DragRectOptions TestOptions
        => DragRectOptions.Default with { HandleHitRadius = 12.0, MinSelectionSize = 10.0 };

    /// <summary>ドメイン境界 200x200 を持つコントロールを生成する。</summary>
    /// <param name="cursor">カーソルサービスのスパイ。</param>
    /// <returns>座標変換ポートを設定したコントロール。</returns>
    private static DragRectControl CreateBounded(CursorServiceSpy cursor)
    {
        var control = CreateControl(cursor);
        control.SetCoordinateTransform(new CoordTransform(p => p, p => p, () => new EsSize(200, 200)));
        return control;
    }

    // ====================================================================
    // 設定の既定値
    // ====================================================================

    [Fact]
    public void 設定の既定値がAvalonia版と一致する()
    {
        var control = CreateControl(new CursorServiceSpy());
        var options = control.Options;

        // 枠線の既定は DarkGray（#FFA9A9A9。設定画面の選択枠の色と一致）。外側の装飾の外観が単一所有する
        Assert.Equal(DrawingColor.FromArgb(255, 169, 169, 169), options.OuterAppearance.BorderColor);
        Assert.Equal(2.0, options.OuterAppearance.BorderThickness);
        Assert.True(options.ShowHandles);
        Assert.Equal(DrawingColor.White, options.HandleColor);
        Assert.Equal(10.0, options.HandleSize);
        Assert.Equal(12.0, options.HandleHitRadius);
        Assert.Equal(10.0, options.MinSelectionSize); // CreateControl で設定
        Assert.True(options.ClampToDomainBounds);
        Assert.Equal(DrawingColor.White, options.MarkerColor);
        Assert.Equal(16.0, options.MarkerLength);
        Assert.False(options.ShowCoordLabel);
        Assert.Equal(DrawingColor.White, options.CoordLabelColor);
        // 背景色は不透明度をアルファに統合（黒 60% = #99000000）
        Assert.Equal(DrawingColor.FromArgb(153, 0, 0, 0), options.CoordLabelBackgroundColor);
        Assert.Equal(12.0, options.CoordLabelFontSize);
        Assert.Equal(1.0, control.RenderScaling);

        // 設定を指定しない場合の既定値はコアの DragRectOptions.Default と一致する
        Assert.Equal(DragRectOptions.Default, new DragRectControl().Options);
    }

    [Fact]
    public void 設定を変更すると反映される()
    {
        var control = CreateControl(new CursorServiceSpy());

        control.Options = DragRectOptions.Default with
        {
            OuterAppearance = DragRectOptions.Default.OuterAppearance with
            {
                BorderColor = DrawingColor.Red,
                BorderThickness = 3.0,
            },
            ShowHandles = false,
            HandleColor = DrawingColor.Blue,
            HandleSize = 14.0,
            MarkerColor = DrawingColor.Yellow,
            MarkerLength = 24.0,
            ShowCoordLabel = true,
            CoordLabelColor = DrawingColor.Cyan,
            CoordLabelBackgroundColor = DrawingColor.FromArgb(128, 0, 100, 0),
        };

        var options = control.Options;
        Assert.Equal(DrawingColor.Red, options.OuterAppearance.BorderColor);
        Assert.Equal(3.0, options.OuterAppearance.BorderThickness);
        Assert.False(options.ShowHandles);
        Assert.Equal(DrawingColor.Blue, options.HandleColor);
        Assert.Equal(14.0, options.HandleSize);
        Assert.Equal(DrawingColor.Yellow, options.MarkerColor);
        Assert.Equal(24.0, options.MarkerLength);
        Assert.True(options.ShowCoordLabel);
        Assert.Equal(DrawingColor.Cyan, options.CoordLabelColor);
        Assert.Equal(DrawingColor.FromArgb(128, 0, 100, 0), options.CoordLabelBackgroundColor);
    }

    [Fact]
    public void CoordLabelFontSize_NaNや範囲外は安全値に補正される()
    {
        var control = CreateControl(new CursorServiceSpy());

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
    // CurrentSnapshot
    // ====================================================================

    [Fact]
    public void CurrentSnapshot_未選択時の全項目が初期値になる()
    {
        var control = CreateControl(new CursorServiceSpy());

        var snapshot = control.CurrentSnapshot;

        Assert.Equal(DragRectInteractionState.NoSelection, snapshot.State);
        Assert.Equal(default, snapshot.SelectionRect);
        Assert.Equal(DragHandle.None, snapshot.ActiveHandle);
        Assert.False(snapshot.HasSelection);
        Assert.False(snapshot.IsDragging);
        Assert.False(snapshot.IsMouseDown);
        Assert.False(snapshot.NudgeActive);
    }

    [Fact]
    public void CurrentSnapshot_選択時は選択状態を返す()
    {
        var control = CreateControl(new CursorServiceSpy());

        control.SetSelection(new EsRect(10, 10, 50, 40));

        var snapshot = control.CurrentSnapshot;
        Assert.Equal(DragRectInteractionState.Selected, snapshot.State);
        Assert.Equal(new EsRect(10, 10, 50, 40), snapshot.SelectionRect);
        Assert.True(snapshot.HasSelection);
    }

    // ====================================================================
    // SetSelection / ResetSelection / 境界クリップ
    // ====================================================================

    [Fact]
    public void SetSelection_選択矩形を復元しSelectionChangedを発火する()
    {
        var control = CreateControl(new CursorServiceSpy());
        SelectionRectEventArgs? raised = null;
        control.SelectionChanged += (_, e) => raised = e;

        control.SetSelection(new EsRect(10, 10, 50, 40));

        Assert.NotNull(raised);
        Assert.Equal(new EsRect(10, 10, 50, 40), raised!.Rect);
        // Snapshot はイベント引数の矩形と同一の選択矩形（ドメイン座標）を保持する
        Assert.Equal(new EsRect(10, 10, 50, 40), raised.Snapshot.SelectionRect);
        Assert.Equal(DragRectInteractionState.Selected, raised.Snapshot.State);
    }

    [Fact]
    public void SetSelection_境界外の矩形は境界内へクリップされる()
    {
        var control = CreateBounded(new CursorServiceSpy());
        SelectionRectEventArgs? raised = null;
        control.SelectionChanged += (_, e) => raised = e;

        // 画像外にはみ出す矩形（右下が 200 超え）を復元 → 境界内 (200,200) にクリップされる
        control.SetSelection(new EsRect(150, 150, 100, 100));

        Assert.NotNull(raised);
        Assert.Equal(new EsRect(150, 150, 50, 50), raised!.Rect);
    }

    [Fact]
    public void ClampToDomainBounds_ON切替時に既存の選択矩形がクリップされる()
    {
        var control = CreateBounded(new CursorServiceSpy());
        var rects = new List<EsRect>();
        control.SelectionChanged += (_, e) => rects.Add(e.Rect);

        // 既定は ON のため、OFF → ON の切替シナリオを明示する
        control.Options = control.Options with { ClampToDomainBounds = false };

        // 画像外にはみ出す矩形（左上が負・右下が 200 超え）を選択状態にする（OFF のためクリップされない）
        control.SetSelection(new EsRect(-50, -30, 300, 260));
        Assert.Equal(new EsRect(-50, -30, 300, 260), rects[^1]);

        // ON に切り替えた瞬間に境界内 (0,0)-(200,200) へクリップされる
        control.Options = control.Options with { ClampToDomainBounds = true };
        Assert.Equal(new EsRect(0, 0, 200, 200), rects[^1]);
    }

    [Fact]
    public void ClampToDomainBounds_ON切替時に境界内の矩形は変更されない()
    {
        var control = CreateBounded(new CursorServiceSpy());
        var rects = new List<EsRect>();
        control.SelectionChanged += (_, e) => rects.Add(e.Rect);

        control.SetSelection(new EsRect(20, 20, 100, 80));
        Assert.Single(rects);

        // 境界内の矩形なので、ON に切り替えてもクリップされない（SelectionChanged は再発火しない）
        control.Options = control.Options with { ClampToDomainBounds = true };

        Assert.Single(rects);
        Assert.Equal(new EsRect(20, 20, 100, 80), rects[^1]);
    }

    [Fact]
    public void ResetSelection_選択状態を初期化する()
    {
        var control = CreateControl(new CursorServiceSpy());
        control.SetSelection(new EsRect(10, 10, 50, 40));

        control.ResetSelection();

        Assert.False(control.CurrentSnapshot.HasSelection);
        Assert.Equal(DragRectInteractionState.NoSelection, control.CurrentSnapshot.State);
    }

    // ====================================================================
    // RequestConfirm / RequestCancel
    // ====================================================================

    [Fact]
    public void RequestConfirm_選択確定後にConfirmRequestedを発火し選択をクリアする()
    {
        var control = CreateControl(new CursorServiceSpy());
        SelectionRectEventArgs? raised = null;
        control.ConfirmRequested += (_, e) => raised = e;

        control.SetSelection(new EsRect(10, 10, 50, 40));
        control.RequestConfirm();

        Assert.NotNull(raised);
        Assert.Equal(new EsRect(10, 10, 50, 40), raised!.Rect);
        // 確定後は選択状態がクリアされる（DragRect の残留を防ぐ）
        Assert.False(control.CurrentSnapshot.HasSelection);
    }

    [Fact]
    public void RequestConfirm_選択なしでは発火しない()
    {
        var control = CreateControl(new CursorServiceSpy());
        var raised = false;
        control.ConfirmRequested += (_, _) => raised = true;

        control.RequestConfirm();

        Assert.False(raised);
    }

    [Fact]
    public void RequestCancel_選択ありなら選択を破棄してCancelRequestedを発火する()
    {
        var control = CreateControl(new CursorServiceSpy());
        var raised = false;
        control.CancelRequested += (_, _) => raised = true;

        control.SetSelection(new EsRect(10, 10, 50, 40));
        control.RequestCancel();

        Assert.True(raised);
        Assert.False(control.CurrentSnapshot.HasSelection);
    }

    [Fact]
    public void RequestCancel_選択なしでもCancelRequestedを発火する()
    {
        var control = CreateControl(new CursorServiceSpy());
        var raised = false;
        control.CancelRequested += (_, _) => raised = true;

        control.RequestCancel();

        Assert.True(raised);
    }

    // ====================================================================
    // NotifyViewportChanged
    // ====================================================================

    [Fact]
    public void NotifyViewportChanged_選択矩形とスナップショットを通知する()
    {
        var control = CreateControl(new CursorServiceSpy());
        SelectionRectEventArgs? raised = null;
        control.ViewportChanged += (_, e) => raised = e;

        control.SetSelection(new EsRect(10, 10, 50, 40));
        control.NotifyViewportChanged();

        Assert.NotNull(raised);
        Assert.Equal(new EsRect(10, 10, 50, 40), raised!.Rect);
        Assert.Equal(DragRectInteractionState.Selected, raised.Snapshot.State);
    }

    [Fact]
    public void NotifyViewportChanged_選択なしでも通知する()
    {
        var control = CreateControl(new CursorServiceSpy());
        var raised = false;
        control.ViewportChanged += (_, _) => raised = true;

        control.NotifyViewportChanged();

        Assert.True(raised);
    }

    // ====================================================================
    // ログ
    // ====================================================================

    [Fact]
    public void Log_既定はNullLoggerで例外にならない()
    {
        var control = CreateControl(new CursorServiceSpy());

        // ログ出力を伴う操作を実行しても例外にならない（既定は NullLogger）
        control.Options = control.Options with { ClampToDomainBounds = false };
        control.SetSelection(new EsRect(10, 10, 50, 40));
        control.RequestCancel();
    }

    [Fact]
    public void Log_null設定はNullLoggerへフォールバックする()
    {
        var control = CreateControl(new CursorServiceSpy());

        control.Log = null!;
        control.RequestCancel();
    }

    [Fact]
    public void Log_設定変更を出力する()
    {
        var control = CreateControl(new CursorServiceSpy());
        var logger = new RecordingLogger();
        control.Log = logger;

        control.Options = control.Options with { ClampToDomainBounds = false };

        Assert.Equal("[DragRect] Setting: ClampToDomainBounds True → False", Assert.Single(logger.Messages));
    }

    [Fact]
    public void Log_同じ値の再設定では出力しない()
    {
        var control = CreateControl(new CursorServiceSpy());
        var logger = new RecordingLogger();
        control.Log = logger;

        // 既定値（true）と同じ値を設定してもログは流れない（外観設定の再適用でログが溢れない）
        control.Options = control.Options with { ClampToDomainBounds = true };

        Assert.Empty(logger.Messages);
    }

    [Fact]
    public void Log_外観のみの変更では設定変更ログを出力しない()
    {
        var control = CreateControl(new CursorServiceSpy());
        var logger = new RecordingLogger();
        control.Log = logger;

        // 外観（色・サイズ）の変更は対話パラメータを変えないため、設定変更ログは流れない
        control.Options = control.Options with
        {
            OuterAppearance = control.Options.OuterAppearance with { BorderColor = DrawingColor.Red, BorderThickness = 3.0 },
        };

        Assert.Empty(logger.Messages);
    }

    [Fact]
    public void Log_確定要求を出力する()
    {
        var control = CreateControl(new CursorServiceSpy());
        var logger = new RecordingLogger();
        control.Log = logger;
        control.SetSelection(new EsRect(10, 10, 50, 40));

        control.RequestConfirm();

        Assert.Contains("[DragRect] ConfirmRequested: rect=(10, 10, 50, 40)", logger.Messages);
    }
}
