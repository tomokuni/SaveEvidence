using EsUtil.Platform.Core;
using Xunit;

namespace Estable.CaptureSession.Tests;

/// <summary><see cref="CaptureKeyActions"/> / <see cref="CaptureKeyAction"/> の仕様テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 「Enter = 確定 / Escape = キャンセル」と「確定対象が無い場合の Enter は何もしない」という規則を固定する。<br/>
/// Enter と Return は同一のキーとして扱い、UI ごとに届く値が異なっても同じ結果になることを検証する。
/// </remarks>
public class CaptureKeyActionsTests
{
    [Theory]
    [InlineData(EsKey.Enter)]
    [InlineData(EsKey.Return)]
    public void Resolve_確定対象があれば確定する(EsKey key)
    {
        Assert.Equal(CaptureKeyAction.Confirm, CaptureKeyActions.Resolve(key, hasConfirmTarget: true));
    }

    [Theory]
    [InlineData(EsKey.Enter)]
    [InlineData(EsKey.Return)]
    public void Resolve_確定対象が無ければ何もしない(EsKey key)
    {
        // 何も検出していない状態の Enter をキャンセル扱いにしない
        Assert.Equal(CaptureKeyAction.None, CaptureKeyActions.Resolve(key, hasConfirmTarget: false));
    }

    [Fact]
    public void Resolve_Escapeは確定対象の有無に関わらずキャンセルする()
    {
        Assert.Equal(CaptureKeyAction.Cancel, CaptureKeyActions.Resolve(EsKey.Escape, hasConfirmTarget: true));
        Assert.Equal(CaptureKeyAction.Cancel, CaptureKeyActions.Resolve(EsKey.Escape, hasConfirmTarget: false));
    }

    [Theory]
    [InlineData(EsKey.Space)]
    [InlineData(EsKey.Tab)]
    [InlineData(EsKey.Left)]
    [InlineData(EsKey.None)]
    public void Resolve_対象外のキーは何もしない(EsKey key)
    {
        Assert.Equal(CaptureKeyAction.None, CaptureKeyActions.Resolve(key, hasConfirmTarget: true));
    }

    [Fact]
    public void CaptureKeyAction_値の意味が固定されている()
    {
        // 0 は「何もしない」（default が副作用を持たない）
        Assert.Equal(CaptureKeyAction.None, default);
    }
}
