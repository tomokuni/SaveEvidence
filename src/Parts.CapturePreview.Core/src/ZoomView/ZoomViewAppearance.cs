using DrawingColor = System.Drawing.Color;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.ZoomView;

/// <summary>ズームビューの外観（下地・スクロールバー）をまとめた不変の設定値。</summary>
/// <remarks>
/// UI 実装が描画に使う外観値を 1 つの値として所有する（UI 層に色・サイズのリテラルを置かない）。<br/>
/// 描画の可否（トラックを描くか）も本型が判定し、UI 層は <c>Has*</c> を問い合わせるだけでよい<br/>
/// （両 UI で判定がずれない）。<br/>
/// <b>画像が無い部分は下地色 <see cref="BackgroundColor"/> の単色</b>で塗る（市松模様は描かない）。<br/>
/// 色は UI 非依存の <see cref="DrawingColor"/> を使用し、各 UI の色型への変換は境界（UI 層）で 1 行行う。<br/>
/// スクロールバーの太さ・余白は UI ごとに見た目が異なってよい値であり、ホストが本値で与える<br/>
/// （Avalonia 版は標準 <c>ScrollBar</c> を使うため本値の幾何は参照しない）。
/// </remarks>
public sealed record ZoomViewAppearance
{
    /// <summary>スクロールバーの太さの既定値（DIP）。</summary>
    /// <remarks>MewUI 標準のスクロールバーと同じ太さに揃える。</remarks>
    public const double DefaultScrollBarThickness = 4.0;

    /// <summary>スクロールバーとビューポート端との余白の既定値（DIP）。</summary>
    /// <remarks>
    /// MewUI のウィンドウは端の数 DIP をサイズ変更の判定領域にするため、余白 0 ではサム上でポインタが<br/>
    /// ウィンドウサイズ変更になり、マウスでスクロールできない。<br/>
    /// 判定領域の外側へ出る最小限の値とする（Avalonia 版は標準 <c>ScrollBar</c> のため本値を使用しない）。
    /// </remarks>
    public const double DefaultScrollBarMargin = 8.0;

    /// <summary>すべての項目を既定値にした外観。</summary>
    /// <value>暗色の下地・スクロールバー表示（白のサム）。</value>
    public static ZoomViewAppearance Default { get; } = new();

    /// <summary>画像が描画されていない領域の下地色を取得する。</summary>
    /// <value>アルファを含む色。 既定は暗色（#FF202020）。</value>
    /// <remarks>画像の表示矩形の外（ペーストボード）も本色の単色である。</remarks>
    public DrawingColor BackgroundColor { get; init; } = DrawingColor.FromArgb(255, 32, 32, 32);

    /// <summary>オーバーレイスクロールバーを表示するかどうかを取得する。</summary>
    /// <value>true（既定）の場合は、画像がビューポートより大きい軸にスクロールバーを表示する。</value>
    /// <remarks>パン・ホイールでも操作できるため、表示はホストの選択とする。</remarks>
    public bool ShowScrollBars { get; init; } = true;

    /// <summary>スクロールバーの太さ（DIP）を取得する。</summary>
    /// <value>太さ（DIP）。 既定は <see cref="DefaultScrollBarThickness"/>。</value>
    public double ScrollBarThickness { get; init; } = DefaultScrollBarThickness;

    /// <summary>スクロールバーとビューポート端との余白（DIP）を取得する。</summary>
    /// <value>余白（DIP）。 既定は <see cref="DefaultScrollBarMargin"/>。</value>
    public double ScrollBarMargin { get; init; } = DefaultScrollBarMargin;

    /// <summary>スクロールバーのサムの色を取得する。</summary>
    /// <value>アルファを含む色。 既定は白 63%。</value>
    public DrawingColor ScrollBarColor { get; init; } = DrawingColor.FromArgb(160, 255, 255, 255);

    /// <summary>スクロールバーのトラックの色を取得する。</summary>
    /// <value>アルファを含む色。 既定は白 16%。</value>
    /// <remarks>アルファ 0 の場合はトラックを描かない（サムのみの表示を要求するホスト向け）。</remarks>
    public DrawingColor ScrollBarTrackColor { get; init; } = DrawingColor.FromArgb(40, 255, 255, 255);

    /// <summary>スクロールバーのトラックを描画するかどうかを取得する。</summary>
    /// <value>トラック色のアルファが 0 より大きい場合は true。</value>
    public bool HasScrollBarTrack => ScrollBarTrackColor.A > 0;

    /// <summary>画像の周囲に確保する余白（DIP）を取得する。</summary>
    /// <value>余白（DIP）。 既定は 0（余白なし）。</value>
    /// <remarks>
    /// <b>Fit の計算に含める</b>ための値である。 画像の周囲へ影を描く場合、画像だけをビューポートへ<br/>
    /// 収めると影が切れる（ビューポートの端で見えなくなる）。 本値の分だけ内側へ収めた大きさで Fit を<br/>
    /// 計算すると、影も含めた全体がビューポートへ収まる（ホストが影の広がりを設定する）。<br/>
    /// 画像の中央寄せはビューポート全体を基準にするため、画像は自然に本値以上の余白を空けて置かれる。
    /// </remarks>
    public double FitInset { get; init; }
}
