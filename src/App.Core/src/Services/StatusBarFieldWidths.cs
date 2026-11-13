namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>ステータス バーの固定幅の項目（マウスポインタ位置・画像サイズ）の寸法（DIP）。</summary>
/// <param name="Position">マウスポインタ位置の幅（基準の文字列の推奨幅 + 「W」）。</param>
/// <param name="ImageSize">画像サイズの幅（基準の文字列の推奨幅 + 「W」）。</param>
/// <param name="Gap">右側の空きの幅（「W」の送り幅）。 幅に含めてあるため、余白としては設定しない。</param>
/// <remarks>
/// 値を <see cref="StatusBarMetrics.ResolveFieldWidths"/> が求める（基準の文字列と測り方は同クラスが単一所有する）。<br/>
/// 使う側は、幅に <see cref="Position"/> / <see cref="ImageSize"/> を設定し、<b>文字を左寄せ</b>にし、<br/>
/// 余白（<c>Margin</c>・<c>Padding</c>）を 0 にすること。 幅は基準の文字列で決まるため、値が変わっても列は動かない。
/// </remarks>
public readonly record struct StatusBarFieldWidths(double Position, double ImageSize, double Gap);
