using System.Globalization;
using System.Runtime.InteropServices;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Native.Win;
using EsUtil.Ui.WinUI3.Interop.Bootstrapping;
using Microsoft.Win32;

namespace EsUtil.SaveEvidence.App.WinUI3.Services;

/// <summary>実行環境（OS・ランタイム・表示経路・倍率・GPU）を診断用の値として取得する。</summary>
/// <remarks>
/// <b>なぜ必要か</b>: WinUI 3 の描画の不具合は「アプリの値」ではなく環境（リモート セッション・GPU・倍率）で<br/>
/// 決まることがある。 実測 2026-11-08: Windows Sandbox（Indirect Display・VRAM 128MB・GPU パーティション）では<br/>
/// 文字が描かれず撮影画像も崩れたが、同じ exe をホスト機で動かすと正常だった。<br/>
/// 環境を 1 行で残せば、次回からログだけで切り分けられる。<br/>
/// <b>失敗を例外にしない</b>: 診断のための値であり、取得できなくても起動・描画を妨げない<br/>
/// （[`.github/例外処理規約.md`](../../../../.github/例外処理規約.md) §3）。
/// </remarks>
internal static class RenderEnvironment
{
    /// <summary>モニターの DPI の照会（コアが単一所有する実装を使う）。</summary>
    private static readonly WinMonitorDpi s_monitorDpi = new();

    /// <summary>基準の DPI（96 = 表示倍率 100%）。</summary>
    private const double StandardDpi = WinMonitorDpi.StandardDpi;

    /// <summary>表示アダプターが登録されるレジストリのクラス（<c>Display</c>）。</summary>
    private const string DisplayClassPath =
        @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";

    /// <summary>セッション名を取得する。</summary>
    /// <value><c>SESSIONNAME</c> の値（例: <c>Console</c> / <c>RDP-Tcp#3</c>）。 取得できない場合は <c>(不明)</c>。</value>
    /// <remarks>リモート セッションかを判別するために使う（Win32 を呼ばずに済む値）。</remarks>
    public static string SessionName => Environment.GetEnvironmentVariable("SESSIONNAME") ?? "(不明)";

    /// <summary>OS の版を取得する。</summary>
    /// <returns>「<c>Microsoft Windows NT 10.0.26100.0 (X64)</c>」の形。 取得できない場合は <c>(不明)</c>。</returns>
    /// <remarks>
    /// <b>Sandbox との比較に要る</b>: Windows Sandbox は同じ OS でも表示経路が違い、それが描画の差になる<br/>
    /// （実測 2026-11-10）。 版とアーキテクチャを 1 か所に残す。
    /// </remarks>
    public static string DescribeOperatingSystem()
    {
        try
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
        }
        catch (Exception ex) when (EsErrors.IsRecoverable(ex))
        {
            // 診断用の値であり、取得できなくても描画を妨げない
            return "(不明)";
        }
    }

    /// <summary>.NET の版を取得する。</summary>
    /// <returns>「<c>10.0.0 (10.0.0)</c>」の形（説明と実行時の版）。 取得できない場合は <c>(不明)</c>。</returns>
    /// <remarks>
    /// アプリの版（<c>AppDomain</c> の版）とは別に、**実行している .NET の版**を残す<br/>
    /// （Framework-dependent と自己完結で実際に読み込まれる版が変わるため）。
    /// </remarks>
    public static string DescribeDotNet()
    {
        try
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{RuntimeInformation.FrameworkDescription} ({Environment.Version})");
        }
        catch (Exception ex) when (EsErrors.IsRecoverable(ex))
        {
            // 診断用の値であり、取得できなくても描画を妨げない
            return "(不明)";
        }
    }

    /// <summary>インストール済みの Windows App Runtime の版を取得する。</summary>
    /// <returns>「<c>2.6.5.0, 2.6.4.0</c>」の形（新しい順）。 1 件も無い場合は <c>(なし)</c>。</returns>
    /// <remarks>
    /// 自己完結の配置（Windows App Runtime を同封）では 1 件も無いことがある（同封物を使うため）。<br/>
    /// 実際に使った版は起動時の「Windows App Runtime を用意しました」の行が表す。
    /// </remarks>
    public static string DescribeWindowsAppRuntime()
    {
        try
        {
            var versions = WindowsAppRuntimeInventory.GetInstalledVersions();
            return versions.Count == 0
                ? "(なし)"
                : string.Join(", ", versions.Select(version => version.ToString(4)));
        }
        catch (Exception ex) when (EsErrors.IsRecoverable(ex))
        {
            // 診断用の値であり、取得できなくても描画を妨げない
            return "(不明)";
        }
    }

    /// <summary>このプロセスが実際に読み込んでいる Windows App Runtime の版を取得します。</summary>
    /// <returns>「<c>2.6.5.0</c>」の形。 取得できない場合は <c>(不明)</c>。</returns>
    /// <remarks>
    /// <b>「入っている版」と「使っている版」は別である</b>: 要求版より古いランタイムが選ばれると、
    /// レイアウトは正常なのに本文のグリフだけ描かれない等の分かりにくい不具合になる（実測 2026-11-10）。
    /// 環境ログの <c>loaded</c> がこれを表す。
    /// </remarks>
    public static string DescribeLoadedWindowsAppRuntime()
        => WindowsAppRuntimeInventory.TryGetLoadedVersion(out var loaded)
            ? loaded.ToString(4)
            : "(不明)";

    /// <summary>このビルドが要求する Windows App Runtime の版を取得する。</summary>
    /// <value>要求する版の文字列（例: <c>2.6.5</c>）。</value>
    public static string RequiredWindowsAppRuntime => WindowsAppRuntimeRequirement.Version.ToString(3);

    /// <summary>接続元の名前を取得する。</summary>
    /// <value><c>CLIENTNAME</c> の値。 ローカル実行の場合は <c>(ローカル)</c>。</value>
    public static string ClientName => Environment.GetEnvironmentVariable("CLIENTNAME") ?? "(ローカル)";

    /// <summary>仮想スクリーン（全モニターを包含する矩形）を取得する。</summary>
    /// <returns>「<c>x,y 幅x高さ</c>」（画面物理ピクセル）。</returns>
    public static string DescribeVirtualScreen()
    {
        var bounds = WinVirtualScreen.Bounds;
        return $"{bounds.X},{bounds.Y} {bounds.Width}x{bounds.Height}";
    }

    /// <summary>仮想スクリーンの位置からモニターの表示倍率を解決する。</summary>
    /// <returns>重複を除いた倍率（例: <c>1.25x, 1x</c>）。 解決できない場合は <c>(不明)</c>。</returns>
    /// <remarks>
    /// モニターの列挙をせず、四隅と中央の位置から実効 DPI を照会して重複を除く<br/>
    /// （ウィンドウやキャプチャが使う値と同じ経路で解決する）。
    /// </remarks>
    public static string DescribeMonitorScales()
    {
        var bounds = WinVirtualScreen.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return "(不明)";
        }

        EsPointInt[] points =
        [
            new(bounds.X + 1, bounds.Y + 1),
            new(bounds.X + 1, bounds.Y + bounds.Height - 2),
            new(bounds.X + bounds.Width - 2, bounds.Y + 1),
            new(bounds.X + bounds.Width - 2, bounds.Y + bounds.Height - 2),
            new(bounds.X + (bounds.Width / 2), bounds.Y + (bounds.Height / 2)),
        ];

        var scales = new SortedSet<double>();
        foreach (var point in points)
        {
            if (s_monitorDpi.GetEffectiveDpi(point) is { } dpi && dpi > 0)
            {
                scales.Add(dpi / StandardDpi);
            }
        }

        return scales.Count == 0
            ? "(不明)"
            : string.Join(", ", scales.Select(scale => scale.ToString("0.##", CultureInfo.InvariantCulture) + "x"));
    }

    /// <summary>表示アダプター（ドライバーの説明と VRAM）を取得する。</summary>
    /// <returns>「<c>説明 (VRAM MB) / 説明2 …</c>」。 取得できない場合は <c>(不明)</c> またはその旨。</returns>
    /// <remarks>
    /// GPU の情報は WinRT / DXGI の射影だけでは取れないため、表示クラスのレジストリを読む（読み取りのみ）。<br/>
    /// 仮想 GPU（パーティション）のアダプターは VRAM を登録しないことがある（その場合は説明だけを返す）。
    /// </remarks>
    public static string DescribeAdapters()
    {
        try
        {
            using var classKey = Registry.LocalMachine.OpenSubKey(DisplayClassPath);
            if (classKey is null)
            {
                return "(不明)";
            }

            var descriptions = new List<string>();
            foreach (var name in classKey.GetSubKeyNames())
            {
                if (name.Length != 4 || !int.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out _))
                {
                    continue;
                }

                using var key = classKey.OpenSubKey(name);
                if (key?.GetValue("DriverDesc") is not string description || description.Length == 0)
                {
                    continue;
                }

                descriptions.Add(ReadVramMegabytes(key) is { } megabytes
                    ? $"{description} ({megabytes} MB)"
                    : description);
            }

            return descriptions.Count == 0 ? "(不明)" : string.Join(" / ", descriptions);
        }
        catch (Exception ex) when (EsErrors.IsRecoverable(ex))
        {
            // 診断用の値であり、読めなくても起動・描画を妨げない
            return "(取得できませんでした)";
        }
    }

    /// <summary>アダプターの VRAM をメガバイトで取得する。</summary>
    /// <param name="key">アダプターのレジストリ キー。</param>
    /// <returns>VRAM（MB）。 登録が無い・読めない場合は <see langword="null"/>。</returns>
    /// <remarks>値は <c>REG_QWORD</c>（<see langword="long"/>）か 8 バイトのバイナリで登録される。</remarks>
    private static long? ReadVramMegabytes(RegistryKey key)
    {
        var bytes = key.GetValue(@"HardwareInformation.qwMemorySize") switch
        {
            long value => value,
            int value => value,
            byte[] { Length: >= 8 } array => BitConverter.ToInt64(array, 0),
            _ => 0L,
        };

        return bytes > 0 ? bytes / (1024 * 1024) : null;
    }
}
