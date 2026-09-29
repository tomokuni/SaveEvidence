using Avalonia.Controls;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Native;
using Xunit;
using Avalonia.Headless.XUnit;
using EsUtil.Platform.AvaloniaUI.Interop;

namespace EsUtil.Platform.AvaloniaUI.Tests;

/// <summary>
/// <see cref="CursorOverrideGuardAvaloniaExtensions"/> の単体テスト（Avalonia の <see cref="TopLevel"/> からのハンドル取得）。
/// </summary>
/// <remarks>
/// <see cref="TopLevel"/> を生成するため、Avalonia ヘッドレス（<c>[AvaloniaFact]</c>）で直列実行する。<br/>
/// コア側の <see cref="ICursorOverrideGuard.Attach(EsPlatformHandle)"/> のテストは
/// <c>Platform.Core.Tests</c> が担う。
/// </remarks>
[Collection("AvaloniaHeadless")]
public class CursorOverrideGuardAvaloniaExtensionsTests
{
    [AvaloniaFact]
    public void Attach_ウィンドウ指定で例外なく完了する()
    {
        var guard = new StubGuard();
        var window = new Window();

        // ヘッドレス環境ではネイティブハンドルを取得できない場合があるが、例外なく完了する
        guard.Attach(window);

        Assert.True(guard.AttachCalled);
    }

    /// <summary>Attach の呼び出しを記録するスタブ（Avalonia 拡張の委譲先を検証する）。</summary>
    private sealed class StubGuard : ICursorOverrideGuard
    {
        /// <summary>Attach が呼び出されたかどうか。</summary>
        public bool AttachCalled { get; private set; }

        /// <inheritdoc/>
        public bool IsAttached => false;

        /// <inheritdoc/>
        public void Attach(EsPlatformHandle handle) => AttachCalled = true;

        /// <inheritdoc/>
        public void Dispose()
        {
        }
    }
}
