using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using EsUtil.Platform.Core.Imaging;

namespace EsUtil.SaveEvidence.Platform.AvaloniaUI.Imaging;

/// <summary>Avalonia の画像またはコアの画像を、表示境界の <see cref="IImage"/> とコアの画像源 <see cref="IEsImageSource"/> の両面で公開するアダプタ。</summary>
/// <remarks>
/// <b>目的</b>: 表示境界（<c>CropViewControl.Source</c> 等）が画像の型に依存しないようにする。 ホストは
/// 「Avalonia の <see cref="Bitmap"/>」でも「コアの <see cref="EsBitmap"/>」でも本クラスで包み、コアの処理は
/// <see cref="IEsImageSource"/> として画像を取り出す。<br/>
/// <br/>
/// <b>真実は 1 つ</b>: 生成時に受け取った側（<see cref="Bitmap"/> または <see cref="EsBitmap"/>）が真実であり、
/// 反対側は<b>初回に一度だけ</b>作る射影である（再描画や再取得では作り直さない）。 射影の変換はピクセルを複製するため
/// （実測: 4K で約 6 ms）、変換の回数を「画像 1 枚につき 1 回」に固定する。<br/>
/// <b>不変条件</b>: 真実の内容は<b>本クラスを生成した後は書き換えないこと</b>（射影は初回で固定されるため、
/// 書き換えても表示・コアの処理へ反映されない）。 内容を変える場合は新しい画像と新しい本クラスを生成する。<br/>
/// <b>所有権</b>: 真実（どちらで受け取っても）は所有しない（破棄はホスト）。 射影だけが本クラスの所有物で、
/// <see cref="Dispose"/> が解放する。<br/>
/// <b>変更操作は提供しない</b>: ピクセル操作・切り出しは提供しない（<see cref="EsBitmap"/> の API を使う）。
/// </remarks>
public sealed class EsImageSourceAdapter : IImage, IEsImageSource, IDisposable
{
    /// <summary>真実の Avalonia 画像（非所有。 <see cref="EsBitmap"/> を真実とした場合は null）。</summary>
    private readonly Bitmap? _avalonia;

    /// <summary>真実のコア画像（非所有。 <see cref="Bitmap"/> を真実とした場合は null）。</summary>
    private readonly EsBitmap? _es;

    /// <summary>コア側の射影（<see cref="Bitmap"/> を真実とした場合に初回だけ作る）。 所有権は本クラス。</summary>
    private EsBitmap? _esProjection;

    /// <summary>Avalonia 側の射影（<see cref="EsBitmap"/> を真実とした場合に初回だけ作る）。 所有権は本クラス。</summary>
    private Bitmap? _avaloniaProjection;

    /// <summary>Avalonia の画像からアダプタを生成する（表示はそのまま、コアへは初回に複製する）。</summary>
    /// <param name="source">真実となる Avalonia の画像。 破棄はホストが行う。</param>
    /// <remarks><see cref="Bitmap"/> の派生（<c>WriteableBitmap</c> 等）も受け付ける。</remarks>
    public EsImageSourceAdapter(Bitmap source)
        => _avalonia = source ?? throw new ArgumentNullException(nameof(source));

    /// <summary>コアの画像からアダプタを生成する（表示は初回に複製、コアへは恒等で渡す）。</summary>
    /// <param name="source">真実となるコアの画像。 破棄はホストが行う。</param>
    public EsImageSourceAdapter(EsBitmap source)
        => _es = source ?? throw new ArgumentNullException(nameof(source));

    /// <inheritdoc/>
    public int Width => _es?.Width ?? _avalonia!.PixelSize.Width;

    /// <inheritdoc/>
    public int Height => _es?.Height ?? _avalonia!.PixelSize.Height;

    /// <summary>表示サイズ（DIP）。</summary>
    /// <value>真実の画像から得られる <see cref="Size"/>。</value>
    public Size Size => _es is { } es ? new Size(es.Width, es.Height) : _avalonia!.Size;

    /// <summary>コアの画像を取得する。</summary>
    /// <returns>コアの画像。<b>借用</b>（破棄・書き換えを行わない）。</returns>
    /// <remarks>
    /// <see cref="EsBitmap"/> を真実とした場合はそれをそのまま返す（恒等）。<br/>
    /// <see cref="Bitmap"/> を真実とした場合は初回だけ <c>ToEsBitmap()</c> で複製し、以降は同じ結果を再利用する。
    /// </remarks>
    public EsBitmap GetEsBitmap()
        => _es ?? (_esProjection ??= _avalonia!.ToEsBitmap());

    /// <summary>Avalonia の画像を取得する。</summary>
    /// <returns>Avalonia の画像。<b>借用</b>（破棄しない）。</returns>
    /// <remarks>
    /// <see cref="Bitmap"/> を真実とした場合はそれをそのまま返す（恒等）。<br/>
    /// <see cref="EsBitmap"/> を真実とした場合は初回だけ <c>ToAvaloniaBitmap()</c> で複製し、以降は同じ結果を再利用する。<br/>
    /// 保存（<c>Save</c>）・クリップボード（<c>SetBitmapAsync</c>）のように具体型 <see cref="Bitmap"/> を要求する API へ渡す用途。
    /// </remarks>
    public Bitmap GetAvaloniaBitmap()
        => _avalonia ?? (_avaloniaProjection ??= _es!.ToAvaloniaBitmap());

    /// <summary>画像を描画する（<see cref="IImage"/>）。</summary>
    /// <param name="context">描画コンテキスト。</param>
    /// <param name="sourceRect">画像内の描画元矩形。</param>
    /// <param name="destRect">描画先の矩形。</param>
    /// <remarks>
    /// <see cref="Bitmap"/> を真実とした場合は変換なしでそのまま描画する（実測: 4K で約 6 ms の複製が 1 枚につき 1 回）。<br/>
    /// 破棄済みの <see cref="EsBitmap"/> を真実とした場合は何も描画しない（safe no-op）。
    /// </remarks>
    public void Draw(DrawingContext context, Rect sourceRect, Rect destRect)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (_avalonia is { } avalonia)
        {
            // Bitmap が真実: 変換せずにそのまま描画する（ピクセルの複製は 1 枚につき 0 回）。
            // Bitmap の Draw は IImage の明示的実装のため、IImage として呼び出す
            ((IImage)avalonia).Draw(context, sourceRect, destRect);
            return;
        }

        var es = _es!;
        if (es.IsDisposed)
        {
            // 真実が破棄済み: 射影が残っていれば破棄して何も描画しない
            _avaloniaProjection?.Dispose();
            _avaloniaProjection = null;
            return;
        }

        context.DrawImage(_avaloniaProjection ??= es.ToAvaloniaBitmap(), sourceRect, destRect);
    }

    /// <summary>射影を解放する（冪等）。</summary>
    /// <remarks>
    /// 真実は所有しないため破棄しない（ホストが破棄する）。 解放するのは射影のみ。<br/>
    /// 破棄後に <see cref="GetEsBitmap"/> / <see cref="GetAvaloniaBitmap"/> / <see cref="Draw"/> を呼ぶと
    /// 真実が生存していれば再び射影が作られる（真実が破棄済みの場合は <see cref="GetEsBitmap"/> が例外、
    /// <see cref="Draw"/> が no-op）。
    /// </remarks>
    public void Dispose()
    {
        _esProjection?.Dispose();
        _esProjection = null;
        _avaloniaProjection?.Dispose();
        _avaloniaProjection = null;
    }
}
