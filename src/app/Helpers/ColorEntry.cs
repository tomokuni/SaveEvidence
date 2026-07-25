using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Avalonia.Media;

namespace app.Helpers;

/// <summary>色の並び替えモード。</summary>
public enum ColorSortMode
{
    /// <summary>色名（英名）のアルファベット順。</summary>
    ByName,
    /// <summary>色相順（虹色順）。</summary>
    ByHue,
}

/// <summary>色名と16進カラーコードの組を表すレコード。</summary>
/// <param name="Name">色の英語名（例: "Red"）。</param>
/// <param name="Hex">16進カラーコード（例: "#FFFFFF", "Red"）。</param>
public record ColorEntry(string Name, string Hex)
{
    /// <summary>日本語名（不明な場合は null）。</summary>
    public string? JapaneseName { get; init; }

    /// <summary>ドロップダウンに表示する名前（例: "Red (赤)"）。</summary>
    public string DisplayName => JapaneseName is not null ? $"{Name} ({JapaneseName})" : Name;

    /// <summary>モデル保存用 — 常に英語名を返す。</summary>
    public override string ToString() => Name;

    /// <summary>色名（英名）アルファベット順の全色一覧。</summary>
    public static IReadOnlyList<ColorEntry> All => s_byName;

    /// <summary>色相順の全色一覧。</summary>
    public static IReadOnlyList<ColorEntry> AllByHue => s_byHue;

    private static readonly IReadOnlyList<ColorEntry> s_byName;
    private static readonly IReadOnlyList<ColorEntry> s_byHue;
    private static readonly Dictionary<string, ColorEntry> s_nameMap;

    /// <summary>色名から表示名を取得する。</summary>
    /// <param name="name">英語の色名（例: "Red"）。</param>
    /// <param name="display">表示名（例: "Red (赤)"）。見つからない場合は <paramref name="name"/> そのまま。</param>
    /// <returns>常に true（常に <paramref name="name"/> が返る）。</returns>
    public static bool TryGetDisplayName(string name, out string display)
    {
        if (s_nameMap.TryGetValue(name, out var entry))
        {
            display = entry.DisplayName;
            return true;
        }
        display = name;
        return true;
    }

    // ──────────────── 静的初期化 ────────────────

    /// <summary>トリマーに <see cref="Colors"/> の静的プロパティがリフレクションで使用されることを伝える。</summary>
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(Colors))]
    static ColorEntry()
    {
        var raw = typeof(Colors)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(Color))
            .Select(p => new ColorEntry(p.Name, p.Name)
            {
                JapaneseName = s_japaneseNames.GetValueOrDefault(p.Name),
            });

        s_byName = raw.OrderBy(e => e.Name).ToList().AsReadOnly();
        s_byHue = raw.OrderBy(e => GetSortKey(e.Hex)).ThenBy(e => e.Name).ToList().AsReadOnly();
        s_nameMap = s_byName.ToDictionary(e => e.Name);
    }

    /// <summary>色相順ソート用のキーを取得する。</summary>
    /// <remarks>無彩色（白・灰・黒）は priority=0 （brightness 昇順 → Black が先頭）、<br/>
    /// 有彩色は priority=1 （hue 昇順）で並べる。<br/>
    /// 同一キー内では <see cref="Name"/> のアルファベット順で安定ソートする。</remarks>
    private static (int priority, float key) GetSortKey(string colorName)
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

    // ──────────────── 英語名→日本語名 辞書 ────────────────

    private static readonly Dictionary<string, string> s_japaneseNames = new()
    {
        // 白・灰・黒
        ["White"] = "白",
        ["Snow"] = "雪",
        ["GhostWhite"] = "ゴーストホワイト",
        ["WhiteSmoke"] = "ホワイトスモーク",
        ["Gainsboro"] = "ゲインズボロ",
        ["LightGray"] = "薄灰",
        ["Silver"] = "銀",
        ["DarkGray"] = "暗灰",
        ["Gray"] = "灰",
        ["DimGray"] = "濃灰",
        ["SlateGray"] = "スレートグレー",
        ["LightSlateGray"] = "薄スレートグレー",
        ["DarkSlateGray"] = "暗スレートグレー",
        ["Black"] = "黒",

        // 赤系
        ["IndianRed"] = "橙赤",
        ["LightCoral"] = "薄コーラル",
        ["Salmon"] = "サーモン",
        ["DarkSalmon"] = "暗サーモン",
        ["LightSalmon"] = "薄サーモン",
        ["Crimson"] = "クリムゾン",
        ["Red"] = "赤",
        ["Firebrick"] = "ファイアーレンガ",
        ["DarkRed"] = "暗赤",
        ["Maroon"] = "マルーン",
        ["Brown"] = "茶",
        ["SaddleBrown"] = "サドルブラウン",
        ["Sienna"] = "シェンナ",
        ["Chocolate"] = "チョコレート",
        ["Peru"] = "ペルー",
        ["SandyBrown"] = "サンディブラウン",
        ["RosyBrown"] = "ロージーブラウン",

        // 橙・黄系
        ["Coral"] = "コーラル",
        ["Tomato"] = "トマト",
        ["OrangeRed"] = "オレンジレッド",
        ["DarkOrange"] = "暗橙",
        ["Orange"] = "橙",
        ["Gold"] = "金",
        ["Goldenrod"] = "ゴールデンロッド",
        ["DarkGoldenrod"] = "暗ゴールデンロッド",
        ["PaleGoldenrod"] = "薄ゴールデンロッド",
        ["Khaki"] = "カーキ",
        ["DarkKhaki"] = "暗カーキ",
        ["Yellow"] = "黄",
        ["LightYellow"] = "薄黄",
        ["LemonChiffon"] = "レモンシフォン",
        ["LightGoldenrodYellow"] = "薄ゴールデンロッドイエロー",
        ["Moccasin"] = "モカシン",
        ["PeachPuff"] = "ピーチパフ",
        ["NavajoWhite"] = "ナバホホワイト",
        ["Bisque"] = "ビスク",
        ["BlanchedAlmond"] = "ブランチアーモンド",
        ["PapayaWhip"] = "パパイヤホイップ",
        ["Cornsilk"] = "コーンシルク",
        ["AntiqueWhite"] = "アンティークホワイト",

        // 緑系
        ["GreenYellow"] = "黄緑",
        ["YellowGreen"] = "イエローグリーン",
        ["LawnGreen"] = "ローングリーン",
        ["Chartreuse"] = "シャルトリューズ",
        ["Lime"] = "ライム",
        ["LimeGreen"] = "ライム緑",
        ["LightGreen"] = "薄緑",
        ["PaleGreen"] = "薄緑",
        ["MediumSeaGreen"] = "中海緑",
        ["SeaGreen"] = "海緑",
        ["ForestGreen"] = "林",
        ["Green"] = "緑",
        ["DarkGreen"] = "暗緑",
        ["OliveDrab"] = "オリーブドラブ",
        ["Olive"] = "オリーブ",
        ["DarkOliveGreen"] = "暗オリーブ緑",
        ["MediumAquamarine"] = "中アクアマリン",
        ["Aquamarine"] = "アクアマリン",
        ["SpringGreen"] = "スプリンググリーン",
        ["MediumSpringGreen"] = "中スプリンググリーン",
        ["DarkSeaGreen"] = "暗海緑",
        ["SeaShell"] = "シーシェル",

        // 青緑・水色系
        ["Teal"] = "青緑",
        ["DarkCyan"] = "暗シアン",
        ["LightSeaGreen"] = "薄海緑",
        ["MediumTurquoise"] = "中ターコイズ",
        ["Turquoise"] = "ターコイズ",
        ["DarkTurquoise"] = "暗ターコイズ",
        ["PaleTurquoise"] = "薄ターコイズ",
        ["Aqua"] = "水",
        ["Cyan"] = "シアン",
        ["LightCyan"] = "薄シアン",
        ["PowderBlue"] = "パウダーブルー",
        ["LightBlue"] = "薄青",
        ["SkyBlue"] = "空色",
        ["LightSkyBlue"] = "薄空色",
        ["DeepSkyBlue"] = "深空色",
        ["DodgerBlue"] = "ドジャーブルー",
        ["CornflowerBlue"] = "コーンフラワーブルー",
        ["SteelBlue"] = "スチールブルー",
        ["CadetBlue"] = "カデットブルー",
        ["MediumSlateBlue"] = "中スレートブルー",
        ["SlateBlue"] = "スレートブルー",
        ["DarkSlateBlue"] = "暗スレートブルー",
        ["RoyalBlue"] = "ロイヤルブルー",
        ["Blue"] = "青",
        ["MediumBlue"] = "中青",
        ["DarkBlue"] = "暗青",
        ["Navy"] = "紺",
        ["MidnightBlue"] = "ミッドナイトブルー",

        // 紫系
        ["Lavender"] = "ラベンダー",
        ["LavenderBlush"] = "ラベンダーブラッシュ",
        ["Thistle"] = "シスル",
        ["Plum"] = "プラム",
        ["Violet"] = "バイオレット",
        ["Orchid"] = "蘭紫",
        ["MediumOrchid"] = "中蘭紫",
        ["DarkOrchid"] = "暗蘭紫",
        ["MediumPurple"] = "中紫",
        ["BlueViolet"] = "青紫",
        ["DarkViolet"] = "暗バイオレット",
        ["DarkMagenta"] = "暗紫",
        ["Magenta"] = "マゼンタ",
        ["Fuchsia"] = "フクシア",
        ["Purple"] = "紫",
        ["Indigo"] = "インディゴ",
        ["RebeccaPurple"] = "レベッカパープル",

        // ピンク系
        ["Pink"] = "ピンク",
        ["LightPink"] = "薄ピンク",
        ["HotPink"] = "ホットピンク",
        ["DeepPink"] = "ディープピンク",
        ["PaleVioletRed"] = "薄バイオレットレッド",
        ["MediumVioletRed"] = "中バイオレットレッド",

        // ベージュ・クリーム・その他淡色
        ["Beige"] = "ベージュ",
        ["Linen"] = "リネン",
        ["OldLace"] = "オールドレース",
        ["MintCream"] = "ミントクリーム",
        ["Honeydew"] = "ハニーデュー",
        ["FloralWhite"] = "フローラルホワイト",
        ["Ivory"] = "アイボリー",
        ["AliceBlue"] = "アリスブルー",
        ["Azure"] = "アズール",
        ["MistyRose"] = "ミスティローズ",
        ["LightSteelBlue"] = "薄スチールブルー",

        // その他
        ["Tan"] = "タン",
        ["BurlyWood"] = "バーリーウッド",
        ["Wheat"] = "ウィート",
        ["Transparent"] = "透明",
    };
}
