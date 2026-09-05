using Avalonia;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

namespace Estable.AvaloniaUI.Platform.Imaging;

/// <summary><see cref="PbBitmap"/> を Avalonia の画像入力（<see cref="IImage"/>）として公開する表示境界コンテナ。</summary>
/// <remarks>
/// <b>適用パターン</b>: Adapter（<see cref="PbBitmap"/> を <see cref="IImage"/> に適合させる）。<br/>
/// <b>依存分離</b>: <see cref="PbBitmap"/>（コア）は Skia のみに依存し、Avalonia 依存の変換・描画は本クラスに閉じる。<br/>
/// 本クラスは <see cref="PlatformFactory"/>（Avalonia / Skia 依存の境界層）に配置する。<br/>
/// <br/>
/// <b>描画（ゼロコピー）</b>: <see cref="Draw"/> は <see cref="ISkiaSharpApiLeaseFeature"/> を取得して
/// <c>SKCanvas</c> へ直接描画する（<c>WriteableBitmap</c> / <c>Bitmap</c> を生成しない）。<br/>
/// Skia 直アクセスができない環境（Avalonia ヘッドレス等）では何も描画しない（安全な no-op）。<br/>
/// <br/>
/// <b>所有権</b>: 内包する <see cref="PbBitmap"/> の所有権は本クラスが保持し、<see cref="Dispose"/> で解放する。<br/>
/// 呼び出し側（ホスト）は本クラスの <see cref="Dispose"/> を適切なタイミングで呼ぶこと（破棄漏れ防止）。<br/>
/// <br/>
/// <b>公開 API</b>: <see cref="Size"/>（<see cref="IImage"/>）・ <see cref="Draw"/>（<see cref="IImage"/>）・ <see cref="Dispose"/>。
/// </remarks>
public sealed class PbBitmapImage : IImage, IDisposable
{
    /// <summary>内部の PbBitmap（コア。所有権は本クラスが保持）。</summary>
    private PbBitmap? _bitmap;

    /// <summary><see cref="PbBitmap"/> を内包して初期化する（所有権を譲り受ける）。</summary>
    /// <param name="bitmap">内包する PbBitmap。所有権が本クラスへ移る。</param>
    /// <remarks>破棄は <see cref="Dispose"/> で行う（呼び出し側は破棄しないこと）。</remarks>
    public PbBitmapImage(PbBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        _bitmap = bitmap;
    }

    /// <summary>公開している画像のサイズ（DIP）。</summary>
    /// <value><see cref="PbBitmap"/> の幅・高さから得られる <see cref="Avalonia.Size"/>。</value>
    public Size Size
    {
        get
        {
            var b = _bitmap ?? throw new ObjectDisposedException(nameof(PbBitmapImage));
            return new Size(b.Width, b.Height);
        }
    }

    /// <summary>内包する PbBitmap へ画像を描画する（Skia 直アクセス。ゼロコピー）。</summary>
    /// <param name="context">描画コンテキスト。</param>
    /// <param name="sourceRect">画像内の描画元矩形。</param>
    /// <param name="destRect">出力先の描画先矩形。</param>
    /// <remarks>
    /// <b>Skia 直アクセス</b>: <see cref="DrawingContext.Custom"/> 経由で <see cref="ImmediateDrawingContext"/> を取得し、
    /// <see cref="ISkiaSharpApiLeaseFeature"/> から <c>SKCanvas</c> を取得して <c>DrawBitmap</c> で直接描画する
    /// （<c>WriteableBitmap</c> への複製・変換を排除）。<br/>
    /// Skia 直アクセスできない環境（ヘッドレス等）では何も描画しない（safe no-op）。<br/>
    /// <b>注意</b>: <see cref="ISkiaSharpApiLeaseFeature"/> は Avalonia.Skia の <c>[Unstable]</c> API のため、
    /// 将来のバージョンで変更される可能性がある。
    /// </remarks>
    public void Draw(DrawingContext context, Rect sourceRect, Rect destRect)
    {
        _ = _bitmap ?? throw new ObjectDisposedException(nameof(PbBitmapImage));

        // Skia 直アクセス（ゼロコピー）。
        // DrawingContext からは PlatformImpl（IDrawingContextImpl）へ直接アクセスできないため、
        // public の Custom() を経由して ImmediateDrawingContext（IOptionalFeatureProvider）を得る。
        // ImmediateDrawingContext.TryGetFeature は PlatformImpl.GetFeature に委譲される。
        // Skia が提供されない環境（ヘッドレス等）では Custom 内の lease が null になり、no-op となる。
        context.Custom(new PbBitmapImageDrawOperation(this, sourceRect, destRect));
    }

    /// <summary>内包する PbBitmap（参照アクセサ。破棄は <see cref="Dispose"/> が担う）。</summary>
    /// <value>内包する PbBitmap。破棄済みの場合は null。</value>
    /// <remarks>
    /// ホスト（上位 AP）が内部状態を参照するために使用する。<br/>
    /// <b>破棄禁止</b>: 返された PbBitmap を破棄しないこと（所有権は本クラスが保持）。<br/>
    /// 参照のみに使用し、<see cref="Dispose"/> は本クラスに委ねること。
    /// </remarks>
    public PbBitmap? Bitmap => _bitmap;

    /// <summary>内包する PbBitmap を解放する（冪等）。</summary>
    /// <remarks>
    /// <see cref="PbBitmap"/> のピクセルバッファを解放する。<br/>
    /// 破棄後に <see cref="Size"/> / <see cref="Draw"/> を呼ぶと <see cref="ObjectDisposedException"/>。
    /// </remarks>
    public void Dispose()
    {
        _bitmap?.Dispose();
        _bitmap = null;
    }

    /// <summary>Skia 直アクセスで PbBitmap を描画するカスタム描画オペレーション。</summary>
    /// <remarks>
    /// <see cref="PbBitmapImage.Draw"/> が <see cref="DrawingContext.Custom"/> 経由で登録し、
    /// <see cref="Render"/> で <see cref="ISkiaSharpApiLeaseFeature"/> を取得して <c>SKCanvas</c> へ直接描画する。<br/>
    /// <b>所有権</b>: 本オペレーションは <see cref="PbBitmapImage"/> を参照するのみで、破棄しない
    /// （破棄は <see cref="PbBitmapImage.Dispose"/> が担う）。<br/>
    /// <b>描画</b>: Skia が提供されない環境（ヘッドレス等）では何も描画しない（safe no-op）。
    /// </remarks>
    private sealed class PbBitmapImageDrawOperation : ICustomDrawOperation
    {
        private readonly PbBitmapImage _owner;
        private readonly Rect _sourceRect;
        private readonly Rect _destRect;

        /// <summary>描画オペレーションを初期化する。</summary>
        /// <param name="owner">描画する <see cref="PbBitmapImage"/>（所有権は移らない）。</param>
        /// <param name="sourceRect">画像内の描画元矩形。</param>
        /// <param name="destRect">出力先の描画先矩形。</param>
        public PbBitmapImageDrawOperation(PbBitmapImage owner, Rect sourceRect, Rect destRect)
        {
            _owner = owner;
            _sourceRect = sourceRect;
            _destRect = destRect;
        }

        /// <summary>描画内容の境界（出力先矩形）。</summary>
        public Rect Bounds => _destRect;

        /// <summary>指定点が描画先矩形内かどうか（ヒットテスト）。</summary>
        public bool HitTest(Point p) => _destRect.Contains(p);

        /// <summary>Skia 直アクセスで内部 SKBitmap を描画する。</summary>
        /// <param name="context">描画コンテキスト。</param>
        public void Render(ImmediateDrawingContext context)
        {
            var b = _owner._bitmap;
            if (b is null)
            {
                // 破棄済み（所有者が Dispose 済み）: 何も描画しない
                return;
            }

            // Skia 直アクセス（ゼロコピー）。Skia が提供されない環境（ヘッドレス等）では no-op。
            var lease = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (lease is null)
            {
                return;
            }

            using var leaseScope = lease.Lease();
            var canvas = leaseScope.SkCanvas;
            var skBitmap = b.ToSKBitmap();
            var src = new SKRect(
                (float)_sourceRect.X, (float)_sourceRect.Y,
                (float)_sourceRect.Right, (float)_sourceRect.Bottom);
            var dst = new SKRect(
                (float)_destRect.X, (float)_destRect.Y,
                (float)_destRect.Right, (float)_destRect.Bottom);

            // SKImage 経由で SKSamplingOptions 版の DrawImage を使う（旧形式 SKPaint.FilterQuality の CS0618 警告を回避）。
            // SKImage.FromBitmap は SKBitmap のピクセルを共有し、SKImage の破棄は SKBitmap に影響しない。
            // SKSamplingOptions(Linear) は拡大縮小時のバイリニア補間を提供する。
            using var image = SKImage.FromBitmap(skBitmap);
            var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
            canvas.DrawImage(image, src, dst, sampling);
        }

        /// <summary>描画オペレーション同士の等価比較（参照比較）。</summary>
        public bool Equals(ICustomDrawOperation? other)
            => other is PbBitmapImageDrawOperation otherOp
               && ReferenceEquals(_owner, otherOp._owner)
               && _sourceRect == otherOp._sourceRect
               && _destRect == otherOp._destRect;

        /// <summary>呼び出し側が破棄しない（所有権は <see cref="PbBitmapImage"/> が保持）。</summary>
        public void Dispose()
        {
            // リソースは PbBitmapImage が所有するため、本オペレーションでは何も解放しない。
        }
    }
}
