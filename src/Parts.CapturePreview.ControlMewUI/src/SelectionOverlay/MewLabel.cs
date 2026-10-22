using Aprillz.MewUI;
using Aprillz.MewUI.Rendering;
using Aprillz.MewUI.Text;
using EsUtil.Ui.MewUI.Interop;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.MewUI.SelectionOverlay;

/// <summary>付加情報ラベル（背景 + 文字）を描画する層。</summary>
/// <remarks>
/// 配置（位置・寸法・内側余白・文字サイズ・色）はコアの <c>SelectionOverlayLabelRenderer</c> が単一所有するため、<br/>
/// 本クラスは与えられた矩形を塗り、文字を置くだけである。<br/>
/// <b>文字の描画</b>: MewUI の描画 API は文字列を直接描けないため、テキスト レイアウトを作って描く
/// （レイアウトは文字列とサイズごとに使い回す）。
/// </remarks>
internal static class MewLabel
{
    /// <summary>レイアウトのキャッシュ（文字列・フォントサイズ・DPI ごとに 1 つ）。</summary>
    private static readonly Dictionary<(string Text, double FontSize, uint Dpi), ITextLayout> s_layouts = [];

    /// <summary>キャッシュに保持するレイアウトの上限。</summary>
    private const int CacheLimit = 32;

    /// <summary>ラベル 1 件を描く（背景を塗ってから文字を描く）。</summary>
    /// <param name="context">描画先のコンテキスト。</param>
    /// <param name="bounds">ラベルの矩形（DIP）。</param>
    /// <param name="backgroundColor">背景色。</param>
    /// <param name="text">表示する文字列。</param>
    /// <param name="textOrigin">文字の左上（DIP）。</param>
    /// <param name="fontSize">フォントサイズ（DIP）。</param>
    /// <param name="textColor">文字色。</param>
    /// <param name="cornerRadius">角の半径（DIP。 0 の場合は四角形）。</param>
    public static void Draw(
        IGraphicsContext context,
        EsRect bounds,
        DrawingColor backgroundColor,
        string text,
        EsPoint textOrigin,
        double fontSize,
        DrawingColor textColor,
        double cornerRadius)
    {
        ArgumentNullException.ThrowIfNull(context);

        var rect = bounds.ToMewUIRect();
        if (cornerRadius > 0)
        {
            context.FillRoundedRectangle(rect, cornerRadius, cornerRadius, backgroundColor.ToMewUIColor());
        }
        else
        {
            context.FillRectangle(rect, backgroundColor.ToMewUIColor());
        }

        if (text.Length == 0)
        {
            return;
        }

        var layout = ResolveLayout(context, text, fontSize);
        var options = new TextDrawOptions { Foreground = textColor.ToMewUIColor() };
        context.Text.Draw(layout, new Point(textOrigin.X, textOrigin.Y), in options);
    }

    /// <summary>文字列とサイズに対応するレイアウトを取得する（初回だけ生成する）。</summary>
    /// <param name="context">描画先のコンテキスト（DPI の取得元）。</param>
    /// <param name="text">描画するテキスト。</param>
    /// <param name="fontSize">フォントサイズ（DIP）。</param>
    /// <returns>レイアウト。</returns>
    /// <exception cref="InvalidOperationException">グラフィックス バックエンドが登録されていない場合。</exception>
    private static ITextLayout ResolveLayout(IGraphicsContext context, string text, double fontSize)
    {
        var dpi = (uint)Math.Round(context.DpiScale * 96);
        var key = (text, fontSize, dpi);
        if (s_layouts.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (s_layouts.Count >= CacheLimit)
        {
            s_layouts.Clear();
        }

        var request = new TextLayoutRequest
        {
            Text = text.AsMemory(),
            Dpi = dpi,
            DefaultStyle = new TextRunStyle
            {
                FontFamily = "Segoe UI",
                FontSize = fontSize,
                Weight = FontWeight.Normal,
            },
        };

        var layout = Application.DefaultGraphicsFactory.TextEngine.CreateLayout(request);
        s_layouts[key] = layout;
        return layout;
    }
}
