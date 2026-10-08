using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Color = System.Drawing.Color;

namespace EsUtil.SaveEvidence.Platform.AvaloniaUI.Imaging;

/// <summary>Avalonia の画像またはコアの画像を、表示境界の <see cref="IImage"/> とコアの読み取り面 <see cref="IEsBitmap"/> の両面で公開するアダプタ。</summary>
/// <remarks>
/// <b>目的</b>: 表示境界（<c>CropViewControl.Source</c> 等）が画像の型に依存しないようにする。 ホストは
/// 「Avalonia の <see cref="Bitmap"/>」でも「コアの画像（<see cref="EsBitmap"/> / <see cref="EsWritableBitmap"/>）」でも
/// 本クラスで包み、コアの処理は <see cref="IEsBitmap"/> として画像を取り出す。<br/>
/// <br/>
/// <b>読み取りの面しか公開しない</b>: 本クラスは <see cref="IEsBitmap"/> だけを実装する（<see cref="IEsWritableBitmap"/> を
/// 実装しない）。 これは<b>表示境界の画像を書き換えられないようにする</b>ためであり、破棄の権利も公開しない
/// （<see cref="IImage"/> は非所有の契約であり、真実の画像を破棄するのは保持するホストである）。<br/>
/// <b>真実は 1 つ</b>: 生成時に受け取った側（<see cref="Bitmap"/> またはコアの画像）が真実であり、反対側は
/// <b>初回に一度だけ</b>作る射影である（再描画や再取得では作り直さない）。 射影の変換はピクセルを複製するため
/// （実測: 4K で約 6 ms）、変換の回数を「画像 1 枚につき 1 回」に固定する。<br/>
/// <b>不変条件</b>: 真実の内容は<b>本クラスを生成した後は書き換えないこと</b>（射影は初回で固定されるため、
/// 書き換えても表示へ反映されない）。 内容を変える場合は新しい画像と新しい本クラスを生成する。<br/>
/// <b>破棄済みの扱い</b>: 真実が破棄済みの場合、寸法やピクセルの取得は <see cref="ObjectDisposedException"/> を投げる
/// （無効な画像の値を正常値として渡さない。 <see cref="Draw"/> だけは no-op）。<br/>
/// <b>本クラス自体の破棄</b>: <see cref="IImage"/> は非所有の契約（<see cref="IImage"/> は <see cref="IDisposable"/> を継承せず、
/// <c>Image.Source</c> の設定では破棄されない）のため、<b>本クラスを保持するホストが差し替え時に <see cref="Dispose"/> する</b>。
/// 破棄しないと射影（Avalonia の <see cref="Bitmap"/>）が残る（4K で 33 MB/枚）。<br/>
/// <b>差し替えの順序</b>: 差し替えるときは<b>新しいアダプタを先に表示（<c>Image.Source</c> 等）へ設定し、
/// 旧アダプタは後から <see cref="Dispose"/> する</b>。 逆順にすると、表示が旧アダプタの射影を参照している間に
/// 破棄され、描画が破棄済みバッファへ触れる（例外・チラつき）。
/// </remarks>
public sealed class EsBitmapAdapter : IImage, IEsBitmap, IDisposable
{
    /// <summary>真実の Avalonia 画像（非所有。 コアの画像を真実とした場合は null）。</summary>
    private readonly Bitmap? _avalonia;

    /// <summary>真実のコア画像（非所有。 <see cref="Bitmap"/> を真実とした場合は null）。</summary>
    private readonly IEsBitmap? _es;

    /// <summary>コア側の射影（<see cref="Bitmap"/> を真実とした場合に初回だけ作る）。 所有権は本クラス。</summary>
    private EsWritableBitmap? _esProjection;

    /// <summary>Avalonia 側の射影（コアの画像を真実とした場合に初回だけ作る）。 所有権は本クラス。</summary>
    private Bitmap? _avaloniaProjection;

    /// <summary>表示に使う縮小射影（<see cref="DisplayMaxSize"/> が正のときだけ作る）。 所有権は本クラス。</summary>
    private Bitmap? _displayProjection;

    /// <summary>内包するコア画像を所有するかどうか（<see cref="Adopt"/> で生成した場合のみ true）。</summary>
    private readonly bool _ownsSource;

    /// <summary>表示に使う長辺の上限（ピクセル。 0 は上限なし = 等倍で表示する）。</summary>
    /// <remarks>
    /// <b>大きな画像を小さく表示する場面（一覧のサムネイル等）で、表示用の射影を縮小する</b>。
    /// 等倍の射影は画像 1 枚につき元の寸法の複製を保持するため（実測: 4K で 32 MB / 7.8 ms）、
    /// 96px のサムネイルを 5 枚並べると 160 MB を超える。 縮小版は同じ 4K から 20 KB で済む。<br/>
    /// <b>保存・クリップボード用の <see cref="GetAvaloniaBitmap"/> は等倍のまま</b>である（表示と保存で射影を分ける）。<br/>
    /// 上限を設定すると <see cref="IImage.Size"/> は縮小後の寸法を返す（Avalonia のレイアウトと描画がその寸法を基準にする）。
    /// <b>コアの読み取り面（<see cref="IEsBitmap.Size"/> / <see cref="IEsBitmap.Width"/> 等）は真実の寸法のまま</b>である。
    /// </remarks>
    public int DisplayMaxSize { get; }

    /// <summary>Avalonia の画像からアダプタを生成する（表示はそのまま、コアへは初回に複製する）。</summary>
    /// <param name="source">真実となる Avalonia の画像。 破棄はホストが行う。</param>
    /// <remarks><see cref="Bitmap"/> の派生（<c>WriteableBitmap</c> 等）も受け付ける。</remarks>
    public EsBitmapAdapter(Bitmap source)
        => _avalonia = source ?? throw new ArgumentNullException(nameof(source));

    /// <summary>コアの画像からアダプタを生成する（表示は初回に複製、コアへは恒等で渡す）。</summary>
    /// <param name="source">真実となるコアの画像。 破棄はホストが行う。</param>
    /// <param name="displayMaxSize">表示に使う長辺の上限（ピクセル）。 0 は上限なし（等倍。 既定）。</param>
    /// <remarks><paramref name="displayMaxSize"/> は<b>表示用の射影だけ</b>を縮小する（<see cref="DisplayMaxSize"/>）。</remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="displayMaxSize"/> が負の場合。</exception>
    public EsBitmapAdapter(IEsBitmap source, int displayMaxSize = 0)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegative(displayMaxSize);
        _es = source;
        DisplayMaxSize = displayMaxSize;
    }

    /// <summary>画像を引き取って所有するアダプタを生成する（<see cref="Dispose"/> で画像も解放する）。</summary>
    /// <param name="source">所有するコアの画像。</param>
    /// <returns>画像を所有するアダプタ。</returns>
    /// <remarks>
    /// <b>生成した画像を解放する人がいない場合に限る</b>（借用して包む場合はコンストラクタを使う）。<br/>
    /// 所有の有無を生成の入口で分けることで、コンストラクタ（非所有）の意味を変えない
    /// （アダプタは「表示の適合」と「自分の射影の解放」だけを担い、元画像は生成した側が解放するのが原則）。<br/>
    /// <paramref name="source"/> は <see cref="EsWritableBitmap"/> に限る（解放できる型だけを引き取れるようにする）。
    /// </remarks>
    public static EsBitmapAdapter Adopt(EsWritableBitmap source) => new(source, ownsSource: true);

    /// <summary>画像を所有するかどうかを指定して生成する（所有の入口は <see cref="Adopt"/> だけに集約する）。</summary>
    /// <param name="source">真実となるコアの画像。</param>
    /// <param name="ownsSource">true の場合は本クラスが <paramref name="source"/> も解放する。</param>
    private EsBitmapAdapter(IEsBitmap source, bool ownsSource)
    {
        _es = source ?? throw new ArgumentNullException(nameof(source));
        _ownsSource = ownsSource;
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">真実が破棄済みの場合。</exception>
    public int Width
    {
        get
        {
            if (_es is { } es)
            {
                ThrowIfTruthDisposed(es);
                return es.Width;
            }

            return _avalonia!.PixelSize.Width;
        }
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">真実が破棄済みの場合。</exception>
    public int Height
    {
        get
        {
            if (_es is { } es)
            {
                ThrowIfTruthDisposed(es);
                return es.Height;
            }

            return _avalonia!.PixelSize.Height;
        }
    }

    /// <inheritdoc/>
    /// <remarks><see cref="Size"/> は表示用の Avalonia 型であり、本プロパティは明示的実装にする
    /// （同名で戻り値の型が異なるため、公開面では表示用の <see cref="Size"/> を優先する）。</remarks>
    /// <exception cref="ObjectDisposedException">真実が破棄済みの場合。</exception>
    EsSizeInt IEsBitmap.Size
    {
        get
        {
            if (_es is { } es)
            {
                ThrowIfTruthDisposed(es);
            }

            return new EsSizeInt(Width, Height);
        }
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">真実が破棄済みの場合。</exception>
    public int Stride
    {
        get
        {
            if (_es is { } es)
            {
                ThrowIfTruthDisposed(es);
            }

            return Width * IEsBitmap.BytesPerPixel;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// コア側の射影（<see cref="GetEsBitmap"/>）を指すビューを返す（コピーしない）。<br/>
    /// <see cref="Bitmap"/> を真実とした場合は初回の取得で 1 回だけ複製する（以降は同じ射影を使う）。
    /// </remarks>
    /// <exception cref="ObjectDisposedException">真実が破棄済みの場合。</exception>
    public ReadOnlySpan<byte> Pixels => GetEsBitmap().Pixels;

    /// <inheritdoc/>
    /// <remarks>
    /// <b>本クラス自身の <see cref="Dispose"/> では真にならない</b>（射影だけを解放し、本クラスは生存して再び射影を作れる）。
    /// 真実の破棄状態を返す（<see cref="Bitmap"/> を真実とした場合は破棄状態を公開しないため常に false）。
    /// </remarks>
    public bool IsDisposed => _es?.IsDisposed ?? false;

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">真実が破棄済みの場合。</exception>
    public (byte B, byte G, byte R, byte A) GetPixel(int x, int y) => GetEsBitmap().GetPixel(x, y);

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">真実が破棄済みの場合。</exception>
    public Color GetPixel(EsPointInt point) => GetEsBitmap().GetPixel(point);

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">真実が破棄済みの場合。</exception>
    public string ToSizeText() => GetEsBitmap().ToSizeText();

    /// <summary>表示サイズ（DIP）。</summary>
    /// <value>真実の画像から得られる <see cref="Size"/>（<see cref="DisplayMaxSize"/> が正の場合は縮小後の寸法）。</value>
    /// <exception cref="ObjectDisposedException">真実が破棄済みの場合。</exception>
    public Size Size
    {
        get
        {
            if (_es is { } es)
            {
                ThrowIfTruthDisposed(es);
                if (DisplayMaxSize > 0)
                {
                    // 表示用の縮小射影を作り、その寸法を返す（Avalonia はこの寸法でレイアウトと描画を行う）
                    return EnsureDisplayProjection(es).Size;
                }

                return new Size(es.Width, es.Height);
            }

            return _avalonia!.Size;
        }
    }

    /// <summary>コアの画像を取得する。</summary>
    /// <returns>コアの画像。<b>借用</b>（破棄・書き換えを行わない）。</returns>
    /// <remarks>
    /// コアの画像を真実とした場合はそれをそのまま返す（恒等）。<br/>
    /// <see cref="Bitmap"/> を真実とした場合は初回だけ複製し、以降は同じ結果を再利用する。<br/>
    /// <b>真実が破棄済みの場合は <see cref="ObjectDisposedException"/></b>（無効な画像を渡さない。 null を返して
    /// 呼び出し側へ判定を強いない）。
    /// </remarks>
    /// <exception cref="ObjectDisposedException">真実が破棄済みの場合。</exception>
    public IEsBitmap GetEsBitmap()
    {
        if (_es is { } es)
        {
            ThrowIfTruthDisposed(es);
            return es;
        }

        return _esProjection ??= _avalonia!.ToEsBitmap();
    }

    /// <summary>Avalonia の画像を取得する。</summary>
    /// <returns>Avalonia の画像。<b>借用</b>（破棄しない）。</returns>
    /// <remarks>
    /// <see cref="Bitmap"/> を真実とした場合はそれをそのまま返す（恒等）。<br/>
    /// コアの画像を真実とした場合は初回だけ複製し、以降は同じ結果を再利用する。<br/>
    /// 保存（<c>Save</c>）・クリップボード（<c>SetBitmapAsync</c>）のように具体型 <see cref="Bitmap"/> を要求する API へ渡す用途。
    /// </remarks>
    /// <exception cref="ObjectDisposedException">真実が破棄済みの場合。</exception>
    public Bitmap GetAvaloniaBitmap()
    {
        if (_avalonia is { } avalonia)
        {
            return avalonia;
        }

        return _avaloniaProjection ??= GetEsBitmap().ToAvaloniaBitmap();
    }

    /// <summary>画像を描画する（<see cref="IImage"/>）。</summary>
    /// <param name="context">描画コンテキスト。</param>
    /// <param name="sourceRect">画像内の描画元矩形。</param>
    /// <param name="destRect">描画先の矩形。</param>
    /// <remarks>
    /// <see cref="Bitmap"/> を真実とした場合は変換なしでそのまま描画する（実測: 4K で約 6 ms の複製が 1 枚につき 1 回）。<br/>
    /// コアの画像を真実とし <see cref="DisplayMaxSize"/> が正の場合は<b>縮小した射影</b>を描画する（表示用。 実測: 4K から 96x54 で数十 µs）。<br/>
    /// 破棄済みのコア画像を真実とした場合は何も描画しない（safe no-op）。
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
            DisposeProjections();
            return;
        }

        if (DisplayMaxSize > 0)
        {
            // 表示用の縮小射影（寸法は Size が返す値と一致する）
            context.DrawImage(EnsureDisplayProjection(es), sourceRect, destRect);
            return;
        }

        context.DrawImage(_avaloniaProjection ??= es.ToAvaloniaBitmap(), sourceRect, destRect);
    }

    /// <summary>表示用の縮小射影を取得する（初回だけ作る）。</summary>
    /// <param name="source">真実のコア画像。</param>
    /// <returns>縮小した Avalonia の画像（本クラスが所有する）。</returns>
    private Bitmap EnsureDisplayProjection(IEsBitmap source)
        => _displayProjection ??= source.ToAvaloniaBitmap(DisplayMaxSize);

    /// <summary>射影（等倍・縮小）を解放する。</summary>
    private void DisposeProjections()
    {
        _esProjection?.Dispose();
        _esProjection = null;
        _avaloniaProjection?.Dispose();
        _avaloniaProjection = null;
        _displayProjection?.Dispose();
        _displayProjection = null;
    }

    /// <summary>射影を解放する（冪等）。</summary>
    /// <remarks>
    /// <see cref="Adopt"/> で生成した場合は内包する画像も解放する（それ以外は所有しないため破棄しない）。<br/>
    /// 破棄後に <see cref="GetEsBitmap"/> / <see cref="GetAvaloniaBitmap"/> / <see cref="Draw"/> を呼ぶと
    /// 真実が生存していれば再び射影が作られる（真実が破棄済みの場合は取得が例外、<see cref="Draw"/> が no-op）。
    /// </remarks>
    public void Dispose()
    {
        DisposeProjections();

        if (_ownsSource)
        {
            (_es as IDisposable)?.Dispose();
        }
    }

    /// <summary>真実が破棄済みなら例外を投げる。</summary>
    /// <param name="source">真実の画像。</param>
    /// <remarks>
    /// 破棄状態は読み取りの面（<see cref="IEsBitmap.IsDisposed"/>）が公開するため、真実の型に依らず判定できる
    /// （以前はコアの実体（<see cref="EsBitmapBase"/>）へのダウンキャストで調べており、本インターフェイスを
    /// 直接実装する画像を取りこぼしていた）。
    /// </remarks>
    private static void ThrowIfTruthDisposed(IEsBitmap source)
        => ObjectDisposedException.ThrowIf(source.IsDisposed, source);
}
