using Avalonia;
using Avalonia.Headless;
using EsUtil.SaveEvidence.App.AvaloniaUI.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace EsUtil.SaveEvidence.App.AvaloniaUI.Tests;

/// <summary>Avalonia ヘッドレステストのアプリケーションビルダー（アセンブリ属性方式）。</summary>
/// <remarks>
/// <c>[AvaloniaFact]</c> / <c>[AvaloniaTheory]</c> のテストは本クラスが提供する AppBuilder で<br/>
/// Avalonia を初期化する（<see cref="Avalonia.Headless.AvaloniaTestApplicationAttribute"/> アセンブリ属性）。<br/>
/// Skia を有効化することで WriteableBitmap のピクセル操作（書き込み・読み取り）が利用可能になる。
/// </remarks>
public class TestAppBuilder
{
    /// <summary>Avalonia アプリケーションビルダーを構成する。</summary>
    /// <returns>構成済みの AppBuilder。</returns>
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<Application>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
