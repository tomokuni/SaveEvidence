namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>ステータス バーの固定幅の項目（マウスポインタ位置・画像サイズ）の寸法（DIP）。</summary>
/// <param name="Position">マウスポインタ位置の表示幅（「8888 x 8888」ちょうど）。</param>
/// <param name="ImageSize">画像サイズの表示幅（「8888 x 8888」ちょうど）。</param>
/// <param name="Gap">項目の右側に置く空きの幅（「WW」ちょうど）。</param>
/// <remarks>
/// 値を <see cref="StatusBarMetrics.ResolveFieldWidths(Func{string, double})"/> が求める（基準の文字列と測り方は同クラスが単一所有する）。<br/>
/// 使う側は、項目の文字を右端へ寄せ、項目の直後に幅 <see cref="Gap"/> の空きの要素を置くこと<br/>
/// （文字を左へ寄せると、値が短いときに余りが空きへ加わって「WW」より広く見える）。
/// </remarks>
public readonly record struct StatusBarFieldWidths(double Position, double ImageSize, double Gap);
