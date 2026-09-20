using Avalonia;

namespace FadeMaskControl.Sample;

/// <summary>サンプルアプリケーションのメインプログラム。</summary>
/// <remarks>Avalonia デスクトップアプリケーションとして起動する。</remarks>
internal static class Program
{
    /// <summary>エントリポイント。</summary>
    [STAThread]
    public static void Main(string[] args)
        => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    /// <summary>Avalonia アプリケーションを構築する。</summary>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
