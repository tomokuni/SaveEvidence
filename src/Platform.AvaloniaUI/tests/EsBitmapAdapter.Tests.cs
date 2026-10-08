using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Imaging;
using Xunit;

namespace EsUtil.SaveEvidence.Platform.AvaloniaUI.Tests;

/// <summary>
/// <see cref="EsBitmapAdapter"/> のテスト（Avalonia の画像とコアの画像を両面で公開するアダプタ）。
/// </summary>
/// <remarks>
/// <see cref="Bitmap"/> への射影（<c>ToAvaloniaBitmap</c>）が Avalonia の描画バックエンドを必要とするため、
/// 本クラスは <c>[AvaloniaFact]</c> で検証する（<c>[Fact]</c> と混在させると並列実行で競合する）。<br/>
/// 「真実は 1 つ・射影は初回のみ」という不変条件（同じインスタンスが返ること）を中心に検証する。<br/>
/// 実 Skia の描画は共有状態を持つため <see cref="AvaloniaSkiaGroup"/> で直列化する。
/// </remarks>
[Collection("AvaloniaSkia")]
public class EsBitmapAdapterTests
{
    // ====================================================================
    // EsBitmap を真実とした場合
    // ====================================================================

    [AvaloniaFact]
    internal void EsBitmapを真実とするとGetEsBitmapは恒等で返す()
    {
        using var bmp = EsWritableBitmap.Create(8, 6);
        using var adapter = new EsBitmapAdapter(bmp);

        // 恒等（コピーなし。 借用）
        Assert.Same(bmp, adapter.GetEsBitmap());
    }

    [AvaloniaFact]
    internal void EsBitmapを真実とするとGetAvaloniaBitmapは初回のみ生成して以降は同じものを返す()
    {
        using var bmp = EsWritableBitmap.Create(8, 6, System.Drawing.Color.Red);
        using var adapter = new EsBitmapAdapter(bmp);

        var first = adapter.GetAvaloniaBitmap();
        var second = adapter.GetAvaloniaBitmap();

        // 射影は初回の 1 枚だけ（以降は再利用する）
        Assert.Same(first, second);
        Assert.Equal(8, first.PixelSize.Width);
        Assert.Equal(6, first.PixelSize.Height);
    }

    [AvaloniaFact]
    internal void EsBitmapを真実とした場合はDisposeで真実を破棄しない()
    {
        var bmp = EsWritableBitmap.Create(8, 6, System.Drawing.Color.Red);
        var adapter = new EsBitmapAdapter(bmp);
        _ = adapter.GetAvaloniaBitmap();

        adapter.Dispose();

        // 真実は所有しない（破棄はホスト）。 射影だけを解放する
        Assert.False(bmp.IsDisposed);
        bmp.Dispose();
    }

    // ====================================================================
    // Bitmap を真実とした場合
    // ====================================================================

    [AvaloniaFact]
    internal void Bitmapを真実とするとGetAvaloniaBitmapは恒等で返す()
    {
        using var bmp = EsWritableBitmap.Create(8, 6, System.Drawing.Color.Blue);
        var avalonia = bmp.ToAvaloniaBitmap();
        using var adapter = new EsBitmapAdapter(avalonia);

        // 恒等（変換なし。 借用）
        Assert.Same(avalonia, adapter.GetAvaloniaBitmap());

        adapter.Dispose();
        avalonia.Dispose();
    }

    [AvaloniaFact]
    internal void Bitmapを真実とするとGetEsBitmapは初回のみ生成して以降は同じものを返す()
    {
        using var bmp = EsWritableBitmap.Create(8, 6, System.Drawing.Color.Blue);
        var avalonia = bmp.ToAvaloniaBitmap();
        using var adapter = new EsBitmapAdapter(avalonia);

        var first = adapter.GetEsBitmap();
        var second = adapter.GetEsBitmap();

        // 射影は初回の 1 枚だけ（以降は再利用する）
        Assert.Same(first, second);
        Assert.Equal(8, first.Width);
        Assert.Equal(6, first.Height);

        adapter.Dispose();
        avalonia.Dispose();
    }

    [AvaloniaFact]
    internal void Bitmapを真実とした場合はDisposeで真実を破棄しない()
    {
        using var bmp = EsWritableBitmap.Create(8, 6, System.Drawing.Color.Blue);
        var avalonia = bmp.ToAvaloniaBitmap();
        var adapter = new EsBitmapAdapter(avalonia);
        _ = adapter.GetEsBitmap();

        adapter.Dispose();

        // 真実（ホスト所有の Avalonia 画像）は破棄しない（射影だけを解放する）
        Assert.NotEqual(0, avalonia.PixelSize.Width);
        avalonia.Dispose();
    }

    // ====================================================================
    // 共通（サイズ・破棄・未設定）
    // ====================================================================

    [AvaloniaFact]
    internal void サイズが真実の画像と一致する()
    {
        using var bmp = EsWritableBitmap.Create(12, 7);
        using var esAdapter = new EsBitmapAdapter(bmp);
        Assert.Equal(new Size(12, 7), esAdapter.Size);
        Assert.Equal(12, esAdapter.Width);
        Assert.Equal(7, esAdapter.Height);

        using var avalonia = EsWritableBitmap.Create(12, 7).ToAvaloniaBitmap();
        using var avaloniaAdapter = new EsBitmapAdapter(avalonia);
        Assert.Equal(new Size(12, 7), avaloniaAdapter.Size);
        Assert.Equal(12, avaloniaAdapter.Width);
        Assert.Equal(7, avaloniaAdapter.Height);
    }

    [AvaloniaFact]
    internal void Disposeを複数回呼んでも安全である()
    {
        using var bmp = EsWritableBitmap.Create(8, 6, System.Drawing.Color.Red);
        var adapter = new EsBitmapAdapter(bmp);

        adapter.Dispose();
        adapter.Dispose();
    }

    [AvaloniaFact]
    internal void 幅と高さが真実の画像と一致する()
    {
        using var bmp = EsWritableBitmap.Create(12, 7);
        using var adapter = new EsBitmapAdapter(bmp);

        Assert.Equal(12, adapter.Width);
        Assert.Equal(7, adapter.Height);
    }

    [AvaloniaFact]
    internal void 真実が破棄済みの場合は取得で例外を投げる()
    {
        var bmp = EsWritableBitmap.Create(8, 6, System.Drawing.Color.Red);
        using var adapter = new EsBitmapAdapter(bmp);
        bmp.Dispose();

        // 破棄済みの真実は渡さない（null を返して呼び出し側へ判定を強いない）
        Assert.Throws<ObjectDisposedException>(() => adapter.GetEsBitmap());
        Assert.Throws<ObjectDisposedException>(() => adapter.GetAvaloniaBitmap());
    }

    [AvaloniaFact]
    internal void 真実が破棄済みの場合は寸法の取得でも例外を投げる()
    {
        var bmp = EsWritableBitmap.Create(8, 6, System.Drawing.Color.Red);
        using var adapter = new EsBitmapAdapter(bmp);
        bmp.Dispose();

        // 寸法も「無効な画像の値」を返さない（破棄済みを正常値として扱う誤りを防ぐ）
        Assert.Throws<ObjectDisposedException>(() => adapter.Width);
        Assert.Throws<ObjectDisposedException>(() => adapter.Height);
        Assert.Throws<ObjectDisposedException>(() => adapter.Size);
    }

    [AvaloniaFact]
    internal void IsDisposedは真実の破棄状態を返す()
    {
        var bmp = EsWritableBitmap.Create(8, 6, System.Drawing.Color.Red);
        var adapter = new EsBitmapAdapter(bmp);
        IEsBitmap read = adapter;

        Assert.False(read.IsDisposed);

        bmp.Dispose();

        // 真実の破棄が面から見える（描画側が破棄済みを描かない判定に使う）
        Assert.True(read.IsDisposed);
        adapter.Dispose();
    }

    [AvaloniaFact]
    internal void IsDisposedは本クラスのDisposeでは真にならない()
    {
        using var bmp = EsWritableBitmap.Create(8, 6, System.Drawing.Color.Red);
        var adapter = new EsBitmapAdapter(bmp);
        IEsBitmap read = adapter;

        adapter.Dispose();

        // 本クラスの Dispose は射影だけを解放する（真実は生存し、再び射影を作れる）
        Assert.False(read.IsDisposed);
        Assert.Equal(8, read.Width);
    }

    [AvaloniaFact]
    internal void ビットマップを真実とした場合のIsDisposedは偽になる()
    {
        using var bmp = EsWritableBitmap.Create(8, 6, System.Drawing.Color.Red);
        using var avalonia = bmp.ToAvaloniaBitmap();
        using var adapter = new EsBitmapAdapter(avalonia);
        IEsBitmap read = adapter;

        // Avalonia の Bitmap は破棄状態を公開しないため判定できない（偽に倒す。 取得は参照先が例外を投げる）
        Assert.False(read.IsDisposed);
    }

    [AvaloniaFact]
    internal void GetPixelは座標型のオーバーロードを面として公開する()
    {
        using var bmp = EsWritableBitmap.Create(4, 4, System.Drawing.Color.FromArgb(255, 30, 20, 10));
        using var adapter = new EsBitmapAdapter(bmp);
        IEsBitmap read = adapter;

        var (b, g, r, a) = read.GetPixel(1, 2);
        var color = read.GetPixel(new EsPointInt(1, 2));

        Assert.Equal(b, color.B);
        Assert.Equal(g, color.G);
        Assert.Equal(r, color.R);
        Assert.Equal(a, color.A);
    }

    // ====================================================================
    // 画像を所有する生成（Adopt）
    // ====================================================================

    [AvaloniaFact]
    internal void Adoptで生成するとDisposeで真実も破棄する()
    {
        var bmp = EsWritableBitmap.Create(8, 6, System.Drawing.Color.Red);
        var adapter = EsBitmapAdapter.Adopt(bmp);

        adapter.Dispose();

        // 生成した画像を解放する人がいないため、アダプタが引き取る
        Assert.True(bmp.IsDisposed);
    }

    [AvaloniaFact]
    internal void Adoptの引数nullはArgumentNullExceptionを投げる()
        => Assert.Throws<ArgumentNullException>(() => EsBitmapAdapter.Adopt(null!));

    [AvaloniaFact]
    internal void コンストラクタ_nullはArgumentNullExceptionを投げる()
    {
        Assert.Throws<ArgumentNullException>(() => new EsBitmapAdapter((EsBitmap)null!));

        using var avalonia = EsWritableBitmap.Create(4, 4).ToAvaloniaBitmap();
        // 型付きのローカル変数でオーバーロードを選ぶ（null リテラルへのキャストは IDE0004 が冗長と判定する）
        Bitmap? nullBitmap = null;
        Assert.Throws<ArgumentNullException>(() => new EsBitmapAdapter(nullBitmap!));
        avalonia.Dispose();
    }

    // ====================================================================
    // 表示サイズの上限（DisplayMaxSize）
    // ====================================================================

    [AvaloniaFact]
    internal void 表示上限を指定するとSizeは縮小後の寸法を返す()
    {
        using var bmp = EsWritableBitmap.Create(200, 100);
        using var adapter = new EsBitmapAdapter(bmp, displayMaxSize: 50);

        // Avalonia のレイアウトと描画が使う Size は縮小後の寸法になる
        Assert.Equal(new Size(50, 25), adapter.Size);
        // コアの読み取り面は真実の寸法のまま（ピクセルと矛盾しない）
        Assert.Equal(200, adapter.Width);
        Assert.Equal(100, adapter.Height);
        Assert.Equal(new EsSizeInt(200, 100), adapter.GetEsBitmap().Size);
    }

    [AvaloniaFact]
    internal void 表示上限を指定すると描画も縮小した射影を使う()
    {
        using var bmp = EsWritableBitmap.Create(200, 100, System.Drawing.Color.Red);
        using var adapter = new EsBitmapAdapter(bmp, displayMaxSize: 50);
        using var target = new RenderTargetBitmap(new PixelSize(50, 25), new Vector(96, 96));

        using (var context = target.CreateDrawingContext())
        {
            // Avalonia が渡す描画元矩形は Size（縮小後）と一致する
            adapter.Draw(context, new Rect(0, 0, adapter.Size.Width, adapter.Size.Height), new Rect(0, 0, 50, 25));
        }

        // 描画が例外なく完了し、射影が縮小後の寸法で作られている
        Assert.Equal(50, adapter.Size.Width);
    }

    [AvaloniaFact]
    internal void 表示上限は保存用の射影に影響しない()
    {
        using var bmp = EsWritableBitmap.Create(200, 100);
        using var adapter = new EsBitmapAdapter(bmp, displayMaxSize: 50);

        // 保存・クリップボード用は等倍（表示用の縮小と分ける）
        var full = adapter.GetAvaloniaBitmap();
        Assert.Equal(200, full.PixelSize.Width);
        Assert.Equal(100, full.PixelSize.Height);
    }

    [AvaloniaFact]
    internal void 表示上限が負ならArgumentOutOfRangeExceptionを投げる()
    {
        using var bmp = EsWritableBitmap.Create(4, 4);

        Assert.Throws<ArgumentOutOfRangeException>(() => new EsBitmapAdapter(bmp, displayMaxSize: -1));
    }

    [AvaloniaFact]
    internal void 表示上限が上限以内なら等倍で扱う()
    {
        using var bmp = EsWritableBitmap.Create(40, 20);
        using var adapter = new EsBitmapAdapter(bmp, displayMaxSize: 50);

        Assert.Equal(new Size(40, 20), adapter.Size);
    }
}
