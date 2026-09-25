using Avalonia;
using Avalonia.Headless;
using EsUtil.SaveEvidence.CropView.AvaloniaUI.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace EsUtil.SaveEvidence.CropView.AvaloniaUI.Tests;

/// <summary>Avalonia ヘッドレステストのアプリケーションビルダー（アセンブリ属性方式）。</summary>
/// <remarks>
/// <c>[AvaloniaFact]</c> のテストは本クラスが提供する AppBuilder で Avalonia を初期化する<br/>
/// （<see cref="AvaloniaTestApplication"/> アセンブリ属性）。<br/>
/// Skia を有効化することで <see cref="Avalonia.Media.Imaging.Bitmap"/> の読み込み・WriteableBitmap のピクセル操作が可能になる。<br/>
/// テストは全て UI スレッドで実行されるため、コントロール生成や描画操作が可能になる。
/// </remarks>
public class TestAppBuilder
{
    /// <summary>Avalonia ヘッドレステスト用の AppBuilder を構成する。</summary>
    /// <returns>構成済みの AppBuilder。</returns>
    /// <remarks>
    /// ヘッドレスプラットフォーム（<see cref="AvaloniaHeadlessPlatformOptions"/>）と Skia レンダラーを有効化し、<br/>
    /// アプリケーションはテスト用の最小構成（<see cref="Application"/>）を使用する。
    /// </remarks>
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<Application>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
