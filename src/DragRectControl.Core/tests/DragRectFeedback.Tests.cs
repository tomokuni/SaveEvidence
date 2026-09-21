using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace Estable.DragRect.Tests;

/// <summary><see cref="DragRectFeedback"/> の単体テスト（UI 層へ要求する効果の適用）。</summary>
/// <remarks>
/// 効果の選択・適用順はコアが単一所有するため、UI 実装に依存せずここで検証する。<br/>
/// 適用先（カーソルサービス・自 UI の操作）はデリゲートとして渡し、呼び出し順を記録して検証する。
/// </remarks>
public class DragRectFeedbackTests
{
    [Fact]
    public void ApplyCursor_移動してから表示を切り替える()
    {
        var cursor = new CursorServiceSpy();
        // 3 つの操作をまとめて要求する（nudge の終了と同じ組み合わせ）
        var feedback = new DragRectFeedback
        {
            Cursor = DragRectCursorAction.MoveTo | DragRectCursorAction.Hide | DragRectCursorAction.Show,
            CursorDomainPosition = new EsPoint(10, 20),
        };

        feedback.ApplyCursor(cursor, p => new EsPixelPoint((int)p.X * 2, (int)p.Y * 2));

        // 適用順は「移動 → 非表示 → 再表示」（変換関数の結果も引数として渡る）
        Assert.Equal(["move:20,40", "show:False", "show:True"], cursor.Calls);
    }

    [Fact]
    public void ApplyCursor_操作が無ければ何もしない()
    {
        var cursor = new CursorServiceSpy();
        var feedback = new DragRectFeedback();

        feedback.ApplyCursor(cursor, _ => new EsPixelPoint(999, 999));

        Assert.Empty(cursor.Calls);
    }

    [Fact]
    public void ApplyVisual_フォーカスしてからカーソル形状と再描画を適用する()
    {
        var feedback = new DragRectFeedback
        {
            RequestFocus = true,
            UpdateCursorShape = true,
            CursorKind = DragCursorKind.DiagonalNWSE,
            Redraw = true,
        };
        var applied = new List<string>();

        feedback.ApplyVisual(
            () => applied.Add("focus"),
            () => applied.Add("redraw"),
            kind => applied.Add($"cursor:{kind}"));

        Assert.Equal(["focus", "cursor:DiagonalNWSE", "redraw"], applied);
    }

    [Fact]
    public void ApplyVisual_要求されていない効果は適用しない()
    {
        var feedback = new DragRectFeedback { Redraw = true };
        var applied = new List<string>();

        feedback.ApplyVisual(
            () => applied.Add("focus"),
            () => applied.Add("redraw"),
            kind => applied.Add($"cursor:{kind}"));

        Assert.Equal(["redraw"], applied);
    }

    [Fact]
    public void ApplyVisual_効果が無ければ何も適用しない()
    {
        var feedback = new DragRectFeedback();
        var applied = new List<string>();

        feedback.ApplyVisual(
            () => applied.Add("focus"),
            () => applied.Add("redraw"),
            kind => applied.Add($"cursor:{kind}"));

        Assert.Empty(applied);
    }

    [Fact]
    public void ApplyVisual_カーソル形状の種別を渡す()
    {
        var applied = new List<DragCursorKind>();
        // 各ハンドル種別がそのまま渡ることを確認する（写像は UI 層の責務）
        foreach (var kind in new[]
                 {
                     DragCursorKind.Default, DragCursorKind.DiagonalNWSE, DragCursorKind.DiagonalNESW,
                     DragCursorKind.Vertical, DragCursorKind.Horizontal, DragCursorKind.Move,
                 })
        {
            new DragRectFeedback { UpdateCursorShape = true, CursorKind = kind }
                .ApplyVisual(() => { }, () => { }, applied.Add);
        }

        Assert.Equal(
            [
                DragCursorKind.Default, DragCursorKind.DiagonalNWSE, DragCursorKind.DiagonalNESW,
                DragCursorKind.Vertical, DragCursorKind.Horizontal, DragCursorKind.Move,
            ],
            applied);
    }
}
