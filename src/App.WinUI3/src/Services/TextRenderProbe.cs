using System.Globalization;
using EsUtil.Platform.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.UI.ViewManagement;

namespace EsUtil.SaveEvidence.App.WinUI3.Services;

/// <summary>文字が描かれるかを、レイアウト結果の計測で切り分ける。</summary>
/// <remarks>
/// <b>なぜ必要か</b>: 「文字だけが描かれない」現象は、原因が 2 つに分かれる。<br/>
/// 1. フォント解決・文字倍率の異常（レイアウトの大きさが 0 になる）<br/>
/// 2. 描画（ラスタライズ）の異常（大きさは正常なのに見えない）<br/>
/// 実測 2026-11-09: Windows Sandbox で文字だけが描かれない現象があり、ログだけでは 1 と 2 を区別できなかった。<br/>
/// <b>失敗を例外にしない</b>: 診断のための値であり、取得できなくても描画を妨げない
/// （[`.github/例外処理規約.md`](../../../../.github/例外処理規約.md) §3）。
/// </remarks>
internal static class TextRenderProbe
{
    /// <summary>計測に使うフォント サイズ（本文と同じ 14px。 装置非依存ピクセル）。</summary>
    private const double ProbeFontSize = 14;

    /// <summary>計測に使う文字（上がり・下がり・字幅のすべてを持つ 2 文字）。</summary>
    private const string ProbeText = "Ag";

    /// <summary>計測に使う明示的なフォント（OS に必ずあるフォント）。</summary>
    private const string ReferenceFontFamily = "Segoe UI";

    /// <summary>OS の文字の大きさ（拡大率）を取得する。</summary>
    /// <returns>文字の大きさ（1.0 = 100%）。 取得できない場合は負の値。</returns>
    public static double GetTextScaleFactor()
    {
        try
        {
            return new UISettings().TextScaleFactor;
        }
        catch (Exception ex) when (EsErrors.IsRecoverable(ex))
        {
            // 診断用の値であり、取得できなくても描画を妨げない
            return -1;
        }
    }

    /// <summary>既定のフォントと明示したフォントで 1 行を計測する。</summary>
    /// <returns>「<c>既定 幅x高さ / Segoe UI 幅x高さ</c>」（装置非依存ピクセル）。</returns>
    /// <remarks>
    /// 要素ツリーへ追加せずに <see cref="UIElement.Measure"/> を呼ぶ（副作用が無く、ウィンドウの表示を待たない）。<br/>
    /// 既定のフォントは <see langword="null"/> のまま計測する（テーマの <c>XamlAutoFontFamily</c> が解決される）。
    /// </remarks>
    public static string Measure()
    {
        var automatic = Measure(ProbeFontSize, fontFamily: null);
        var reference = Measure(ProbeFontSize, new FontFamily(ReferenceFontFamily));
        return string.Create(
            CultureInfo.InvariantCulture,
            $"既定 {automatic} / {ReferenceFontFamily} {reference}");
    }

    /// <summary>指定したフォントで 1 行を計測する。</summary>
    /// <param name="fontSize">フォント サイズ（装置非依存ピクセル）。</param>
    /// <param name="fontFamily">フォント。 <see langword="null"/> の場合はテーマの既定を使う。</param>
    /// <returns>「<c>幅x高さ</c>」。 計測できなかった場合は <c>(失敗)</c>。</returns>
    private static string Measure(double fontSize, FontFamily? fontFamily)
    {
        try
        {
            var probe = new TextBlock
            {
                Text = ProbeText,
                FontSize = fontSize,
            };
            if (fontFamily is not null)
            {
                probe.FontFamily = fontFamily;
            }

            probe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var size = probe.DesiredSize;
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{size.Width:0.##}x{size.Height:0.##}");
        }
        catch (Exception ex) when (EsErrors.IsRecoverable(ex))
        {
            // 計測できなくても描画を妨げない（値は「失敗」として残す）
            return "(失敗)";
        }
    }
}
