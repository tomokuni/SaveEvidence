using Avalonia;
using Avalonia.Media;
using EsUtil.Platform.Core.Imaging;

namespace EsUtil.Platform.AvaloniaUI.Imaging;

/// <summary><see cref="EsBitmap"/> を Avalonia の画像入力（<see cref="IImage"/>）として公開する表示境界コンテナ。</summary>
/// <remarks>
/// <b>適用パターン</b>: Adapter（<see cref="EsBitmap"/> を <see cref="IImage"/> に適合させる）。<br/>
/// <b>依存分離</b>: <see cref="EsBitmap"/>（コア）は Skia のみに依存し、Avalonia 依存の変換・描画は本クラスに閉じる。<br/>
/// 本クラスは Avalonia / Skia 依存の境界層（`Platform.AvaloniaUI`）に配置する。<br/>
/// <br/>
/// <b>描画（標準経路）</b>: <see cref="Draw"/> は <see cref="EsBitmap"/> を <see cref="Bitmap"/>（<c>ImmutableBitmap</c>）
/// へ変換して <c>DrawImage</c> で標準描画する。Skia の標準描画（<c>IDrawableBitmapImpl.Draw</c>）を経由するため、
/// カスタム描画オペレーション（<c>DrawingContext.Custom</c>）と異なり確実に再描画される。<br/>
/// <br/>
/// <b>所有権</b>: 内包する <see cref="EsBitmap"/> の所有権は本クラスが保持し、<see cref="Dispose"/> で解放する。<br/>
/// 呼び出し側（ホスト）は本クラスの <see cref="Dispose"/> を適切なタイミングで呼ぶこと（破棄漏れ防止）。<br/>
/// <br/>
/// <b>公開 API</b>: <see cref="Size"/>（<see cref="IImage"/>）・ <see cref="Draw"/>（<see cref="IImage"/>）・ <see cref="Dispose"/>。
/// </remarks>
public sealed class EsBitmapAvaloniaImage : IImage, IDisposable
{
    /// <summary>内部の EsBitmap（コア。所有権は本クラスが保持）。</summary>
    private EsBitmap? _bitmap;

    /// <summary><see cref="EsBitmap"/> を内包して初期化する（所有権を譲り受ける）。</summary>
    /// <param name="bitmap">内包する EsBitmap。所有権が本クラスへ移る。</param>
    /// <remarks>破棄は <see cref="Dispose"/> で行う（呼び出し側は破棄しないこと）。</remarks>
    public EsBitmapAvaloniaImage(EsBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        _bitmap = bitmap;
    }

    /// <summary>公開している画像のサイズ（DIP）。</summary>
    /// <value><see cref="EsBitmap"/> の幅・高さから得られる <see cref="Avalonia.Size"/>。</value>
    public Size Size
    {
        get
        {
            var b = _bitmap ?? throw new ObjectDisposedException(nameof(EsBitmapAvaloniaImage));
            return new Size(b.Width, b.Height);
        }
    }

    /// <summary>内包する EsBitmap を標準描画する（<see cref="Bitmap"/> 経由）。</summary>
    /// <param name="context">描画コンテキスト。</param>
    /// <param name="sourceRect">画像内の描画元矩形。</param>
    /// <param name="destRect">出力先の描画先矩形。</param>
    /// <remarks>
    /// <b>標準描画</b>: <see cref="EsBitmap"/> を <see cref="Bitmap"/>（内部実装 <c>ImmutableBitmap</c>）へ変換し、
    /// <c>SplitDrawImage</c> / <c>DrawImage</c> の標準経路で描画する。カスタム描画オペレーション（<c>Custom</c>）ではなく
    /// 標準の <c>DrawBitmap</c> 経路を辿るため、マウス移動中の再描画でも確実に表示される。<br/>
    /// <b>注意</b>: <see cref="Bitmap"/> へ変換する際にピクセルを複製する（毎回スナップショット）。<br/>
    /// 破棄済みの場合は何も描画しない（safe no-op）。
    /// </remarks>
    public void Draw(DrawingContext context, Rect sourceRect, Rect destRect)
    {
        var b = _bitmap;
        if (b is null)
        {
            // 破棄済み（所有者が Dispose 済み）: 何も描画しない
            return;
        }

        // 内包 EsBitmap が外部から破棄されている場合も安全に描画しない（破棄済みピクセルへのアクセスを防止）。
        // EsBitmapAvaloniaImage がまだ参照している間に、共有 EsBitmap が破棄される経路（プレビュー画像の置き換え等）があるため。
        if (b.IsDisposed)
        {
            return;
        }

        // 標準 Bitmap（内部実装 ImmutableBitmap）へ変換して標準描画する。
        // ToAvaloniaBitmap はピクセルを複製する（スナップショット）ため、その後本 EsBitmap を書き換えても安全。
        using var av = b.ToAvaloniaBitmap();
        context.DrawImage(av, sourceRect, destRect);
    }

    /// <summary>内包する EsBitmap（参照アクセサ。破棄は <see cref="Dispose"/> が担う）。</summary>
    /// <value>内包する EsBitmap。破棄済みの場合は null。</value>
    /// <remarks>
    /// ホスト（上位 AP）が内部状態を参照するために使用する。<br/>
    /// <b>破棄禁止</b>: 返された EsBitmap を破棄しないこと（所有権は本クラスが保持）。<br/>
    /// 参照のみに使用し、<see cref="Dispose"/> は本クラスに委ねること。
    /// </remarks>
    public EsBitmap? Bitmap => _bitmap;

    /// <summary>内包する EsBitmap を解放する（冪等）。</summary>
    /// <remarks>
    /// <see cref="EsBitmap"/> のピクセルバッファを解放する。<br/>
    /// 破棄後に <see cref="Size"/> / <see cref="Draw"/> を呼ぶと <see cref="ObjectDisposedException"/>。
    /// </remarks>
    public void Dispose()
    {
        _bitmap?.Dispose();
        _bitmap = null;
    }
}
