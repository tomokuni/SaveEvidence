using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using MewUIColor = Aprillz.MewUI.Color;

namespace EsUtil.SaveEvidence.App.MewUI.Views;

/// <summary>一覧（フォルダビュー / Undo 一覧）のサムネイル 1 件分の要素を組み立てる。</summary>
/// <remarks>
/// <b>構成</b>: 枠（<see cref="Border"/>。 画像 + 影付き）とラベルを縦に並べる。<br/>
/// Avalonia 版の ItemTemplate（<c>Border.thumbnail-frame</c> + <c>BoxShadow</c> + <c>TextBlock</c>）に相当する。<br/>
/// <br/>
/// <b>影</b>: MewUI には <c>BoxShadow</c> が無いため <see cref="ShadowDecorator"/> で描く。<br/>
/// 影を描く装飾要素はレイアウト上でぼかし＋オフセット分の場所を取るため、ラベルとの間隔から差し引いて<br/>
/// Avalonia 版と同じ間隔（0）にする。<br/>
/// <br/>
/// <b>ホバー</b>: <see cref="ShadowDecorator"/> の配下では <c>StateTrigger</c>（ホバー）が効かない（実測）ため、<br/>
/// <see cref="UIElement.MouseEnter"/> / <see cref="UIElement.MouseLeave"/> で枠色を直接切り替える。<br/>
/// 枠色はテーマに依存しない（Avalonia 版と同じグレー / 青）ため、ローカル値でも問題ない。
/// </remarks>
internal static class ThumbnailItem
{
    /// <summary>サムネイルの枠とラベルを縦に並べた要素を生成する。</summary>
    /// <param name="frame">画像を入れた枠（<c>StyleName</c> に <see cref="AppStyles.ThumbnailFrame"/> または <see cref="AppStyles.ThumbnailFrameCurrent"/> を設定済み）。</param>
    /// <param name="label">ファイル名またはラベルの要素（高さ・幅は呼び出し側が指定する）。</param>
    /// <param name="onDoubleClick">ダブルクリック時の処理。</param>
    /// <returns>アイテムの要素。</returns>
    /// <remarks>
    /// 1. 枠の後ろに影を付け、枠とラベルを縦に並べる。<br/>
    /// 2. 影は枠の周囲の余白（<see cref="ShadowDecorator.ShadowPadding"/>）の中へ描かれる（負のマージンで相殺すると影が切れる）。<br/>
    /// 下側の余白だけ負の間隔で差し引き、画像とラベルの間隔を 0 にする（Avalonia 版の <c>BoxShadow</c> はレイアウトに影響しない）。<br/>
    /// 3. ホバー中は枠をハイライトし、影を消して、項目全体へ薄い帯を表示する<br/>
    /// （Avalonia 版の <c>:pointerover</c> スタイルと <c>ListBox</c> 項目のホバー帯と同じ振る舞い）。<br/>
    /// 現在表示中の枠（<see cref="AppStyles.ThumbnailFrameCurrent"/>）は枠色を変えない（帯は表示する）。
    /// </remarks>
    public static FrameworkElement Create(Border frame, Element label, Action onDoubleClick)
    {
        // 現在表示中の枠は影を付けない（Avalonia 版の .current スタイルが BoxShadow = none を適用する）
        var shadow = new ShadowDecorator
        {
            Child = frame,
            BlurRadius = AppStyles.ThumbnailShadowBlur,
            OffsetY = AppStyles.ThumbnailShadowOffsetY,
            ShadowColor = frame.StyleName == AppStyles.ThumbnailFrameCurrent
                ? MewUIColor.Transparent
                : AppStyles.ThumbnailShadowColor,
            CornerRadius = AppStyles.ThumbnailCornerRadius,
        };

        // 影が確保する下側の余白を間隔から差し引いて、画像とラベルの間隔を 0 にする
        var item = new StackPanel()
            .Vertical()
            .Spacing(-shadow.ShadowPadding.Bottom)
            .Children(shadow, label);

        // ホバー帯を描くために外側を Border で包む（Panel は背景を持てない）
        var host = new Border { Child = item };

        // ホバー中は枠をハイライトし、影を消し、項目全体へ薄い帯を表示する
        // （現在表示中の枠は常に青のため、枠色と影は変えない）
        var highlightBorder = frame.StyleName == AppStyles.ThumbnailFrame;
        host.MouseEnter += () =>
        {
            host.Background = AppStyles.CurrentThumbnailHoverBackground;

            if (highlightBorder)
            {
                frame.BorderBrush = AppStyles.ThumbnailHoverBorder;
                shadow.ShadowColor = MewUIColor.Transparent;
            }
        };
        host.MouseLeave += () =>
        {
            host.Background = MewUIColor.Transparent;

            if (highlightBorder)
            {
                frame.BorderBrush = AppStyles.ThumbnailBorder;
                shadow.ShadowColor = AppStyles.ThumbnailShadowColor;
            }
        };

        // ダブルクリックはサムネイルの外（ラベル上）でも受け付ける
        host.OnMouseDoubleClick(_ => onDoubleClick());

        return host;
    }
}
