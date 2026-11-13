namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>ステータス バーの固定幅の項目（マウスポインタ位置・画像サイズ）の寸法（DIP）。</summary>
/// <param name="Position">マウスポインタ位置の表示幅（右側の「WW」の空きを含む）。</param>
/// <param name="ImageSize">画像サイズの表示幅（右側の「WW」の空きを含む）。</param>
/// <remarks>
/// 値を <see cref="StatusBarMetrics.ResolveFieldWidths"/> が求める（基準の文字列と測り方は同クラスが単一所有する）。<br/>
/// 幅には右側の空きが含まれるため、使う側は右の余白（<c>Margin</c>・<c>Padding</c>）を足さないこと。<br/>
/// 次の項目の左の余白も 0 にすること（足すと空きが「WW」より広くなる）。
/// </remarks>
public readonly record struct StatusBarFieldWidths(double Position, double ImageSize);
