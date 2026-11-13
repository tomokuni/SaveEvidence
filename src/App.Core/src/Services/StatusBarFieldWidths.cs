namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>ステータス バーの固定幅の項目（マウスポインタ位置・画像サイズ）の寸法（DIP）。</summary>
/// <param name="Position">マウスポインタ位置の表示幅。</param>
/// <param name="ImageSize">画像サイズの表示幅。</param>
/// <param name="RightPadding">各項目の右側の余白（区切りの幅）。</param>
/// <remarks>
/// 値を <see cref="StatusBarMetrics.ResolveFieldWidths(Func{string, double})"/> が求める（基準の文字列と測り方は同クラスが単一所有する）。
/// </remarks>
public readonly record struct StatusBarFieldWidths(double Position, double ImageSize, double RightPadding);
