using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Avalonia.Media;

namespace Estable.AvaloniaUI.ColorPicker;

/// <summary>色名と色を表す文字列の組を表すレコード。</summary>
/// <param name="Name">色の英語名（例: "Red"）。</param>
/// <param name="Value">色を表す文字列（`Color.TryParse` で解釈可能。現在は色名 = `Colors` のプロパティ名。例: "Red"）。</param>
/// <remarks>
/// ドロップダウンの選択肢データであり、コントロール内部で生成・使用する（外部から直接扱わない）。<br/>
/// そのため公開 API ではなく internal 型とする（公開 API は ColorPicker.axaml.cs に集約）。
/// </remarks>
internal sealed record ColorEntry(string Name, string Value)
{
    /// <summary>ドロップダウンに表示する名前（英語名）。</summary>
    public override string ToString() => Name;

    /// <summary>色名（英名）アルファベット順の全色一覧。</summary>
    public static IReadOnlyList<ColorEntry> All => s_byName;

    /// <summary>色相順の全色一覧。</summary>
    public static IReadOnlyList<ColorEntry> AllByHue => s_byHue;

    private static readonly IReadOnlyList<ColorEntry> s_byName;
    private static readonly IReadOnlyList<ColorEntry> s_byHue;

    // ──────────────── 静的初期化 ────────────────

    /// <summary>トリマーに <see cref="Colors"/> の静的プロパティがリフレクションで使用されることを伝える。</summary>
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(Colors))]
    static ColorEntry()
    {
        var raw = typeof(Colors)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(Color))
            .Select(p => new ColorEntry(p.Name, p.Name));

        s_byName = raw.OrderBy(e => e.Name).ToList().AsReadOnly();
        s_byHue = raw.OrderBy(e => GetSortKey(e.Value)).ThenBy(e => e.Name).ToList().AsReadOnly();
    }

    /// <summary>色相順ソート用のキーを取得する。</summary>
    /// <remarks>無彩色（白・灰・黒）は priority=0 （brightness 昇順 → Black が先頭）、<br/>
    /// 有彩色は priority=1 （hue 昇順）で並べる。<br/>
    /// 同一キー内では <see cref="Name"/> のアルファベット順で安定ソートする。<br/>
    /// テスト（ColorPickerControl.Tests）が未解析色の分岐を検証するため internal 公開する。</remarks>
    internal static (int priority, float key) GetSortKey(string colorName)
    {
        if (!Color.TryParse(colorName, out var c)) return (0, 0f);

        var r = c.R / 255f;
        var g = c.G / 255f;
        var b = c.B / 255f;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        if (delta < 0.001f)
        {
            // 無彩色 — 輝度（max）昇順 → Black(0) → Gray(0.5) → White(1)
            return (0, max);
        }

        // 有彩色 — 色相 (0-360) でソート
        var hue = max switch
        {
            var _ when max == r => ((g - b) / delta) % 6f,
            var _ when max == g => (b - r) / delta + 2f,
            _ => (r - g) / delta + 4f,
        };
        return (1, hue * 60f);
    }
}

