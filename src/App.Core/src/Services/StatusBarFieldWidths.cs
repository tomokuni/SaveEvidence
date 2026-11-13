namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>ステータス バーの固定幅の項目（マウスポインタ位置・画像サイズ）の寸法（DIP）。</summary>
/// <param name="Position">マウスポインタ位置の幅（コントロールへ設定する値）。</param>
/// <param name="ImageSize">画像サイズの幅（コントロールへ設定する値）。</param>
/// <param name="Gap">右側の余白（<c>Margin</c> の右へ設定する値）。</param>
/// <remarks>
/// 値を <see cref="StatusBarMetrics.ResolveFieldWidths"/> が求める（基準の文字列と測り方は同クラスが単一所有する）。<br/>
/// 使う側は、幅に <see cref="Position"/> / <see cref="ImageSize"/> を設定し、<b>右の余白へ <see cref="Gap"/></b> を設定し、<br/>
/// 文字を右端へ寄せること。 合計（幅 + 余白）は一定のため、値が変わっても右側の項目は動かない。
/// </remarks>
public readonly record struct StatusBarFieldWidths(double Position, double ImageSize, double Gap);
