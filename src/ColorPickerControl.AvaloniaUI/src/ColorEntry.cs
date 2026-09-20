using Avalonia.Media;

namespace Estable.AvaloniaUI.ColorPicker;

/// <summary>色名と色を表す文字列の組を表すレコード。</summary>
/// <param name="Name">色の英語名（例: "Red"）。</param>
/// <param name="Value">色を表す文字列（`Color.TryParse` で解釈可能。現在は色名 = `Colors` のプロパティ名。例: "Red"）。</param>
/// <remarks>
/// ドロップダウンの選択肢データであり、コントロール内部で生成・使用する（外部から直接扱わない）。<br/>
/// そのため公開 API ではなく internal 型とする（公開 API は ColorPicker.axaml.cs に集約）。<br/>
/// 色一覧は <see cref="ColorString.ColorNames"/>（<see cref="ColorString"/> が構築済みの辞書）を再利用する
/// （リフレクション走査の重複排除による DRY 化と起動時の無駄な処理の排除）。
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

    /// <summary>静的コンストラクタ。全色一覧（色名順 / 色相順）を構築する。</summary>
    /// <remarks>
    /// <see cref="ColorString.ColorNames"/> の全色からエントリを生成し、配列へ一度だけ実体化する
    /// （遅延シーケンスを 2 回列挙する従来実装を排除。ソートは実体化済み配列に対して行う）。<br/>
    /// リフレクションによる <see cref="Colors"/> 列挙は <see cref="ColorString"/> が単一所有する
    /// （<see cref="DynamicDependency"/> 属性も <see cref="ColorString"/> 側に移設済み）。
    /// </remarks>
    static ColorEntry()
    {
        var entries = ColorString.ColorNames
            .Select(p => new ColorEntry(p.Key, p.Key))
            .ToArray();

        s_byName = entries.OrderBy(e => e.Name).ToList().AsReadOnly();
        s_byHue = entries.OrderBy(e => GetSortKey(e.Value)).ThenBy(e => e.Name).ToList().AsReadOnly();
    }

    /// <summary>色相順ソート用のキーを取得する。</summary>
    /// <remarks>無彩色（白・灰・黒）は priority=0 （brightness 昇順 → Black が先頭）、<br/>
    /// 有彩色は priority=1 （hue 昇順）で並べる。<br/>
    /// 同一キー内では <see cref="Name"/> のアルファベット順で安定ソートする。<br/>
    /// 色の解決は <see cref="ColorString.ColorNames"/> 辞書（OrdinalIgnoreCase・構築済み）の O(1) 引きで行い、<br/>
    /// 色名の文字列解析（<see cref="Color.TryParse(string, out Color)"/>）を排除する。<br/>
    /// テスト（ColorPickerControl.AvaloniaUI.Tests）が未定義色名の分岐を検証するため internal 公開する。</remarks>
    internal static (int priority, float key) GetSortKey(string colorName)
    {
        // 色名は ColorString が所有する辞書を引く（解析より高速。未定義は無彩色の先頭に置く）
        if (!ColorString.ColorNames.TryGetValue(colorName, out var c)) return (0, 0f);

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

