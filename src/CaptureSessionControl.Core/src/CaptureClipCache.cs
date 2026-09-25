using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;

namespace EsUtil.SaveEvidence.CaptureSession;

/// <summary>撮影の確定時に使う切り出し画像の事前キャッシュ（状態と判断）を単一所有する。</summary>
/// <remarks>
/// 撮影セッションは「確定前に選択中の矩形で切り出しておき、確定時はそれを再利用する」ことで確定から<br/>
/// 画像表示までの待ち時間を短縮する。この<b>キャッシュの同一判定・所有権の受け渡し・旧値の破棄順序</b>は<br/>
/// UI フレームワークに依存しない規則であるため、本クラスが単一所有する。<br/>
/// <br/>
/// <b>規則</b>:<br/>
/// 1. <see cref="HasCached(EsPixelRect)"/> は「同じ矩形なら同じ画像になる」ため再切り出しをスキップする判断を与える。<br/>
/// 2. <see cref="Take(EsPixelRect)"/> は一致するキャッシュを<b>所有権ごと</b>返す（以降の <see cref="Dispose"/> で破棄されない）。<br/>
/// 3. <see cref="Store(EsPixelRect, EsBitmap)"/> は<b>新しい画像を入れてから旧値を破棄する</b>（同一インスタンスの取り違えを避ける）。<br/>
/// 4. <see cref="Dispose"/> で保持中の画像を破棄する（複数回呼んでも安全）。<br/>
/// <br/>
/// <b>スレッド</b>: 切り出しはバックグラウンドで行い、結果の反映は UI スレッドで行う想定のため、内部で排他する。
/// </remarks>
public sealed class CaptureClipCache : IDisposable
{
    /// <summary>状態を保護するゲート。</summary>
    private readonly object _gate = new();

    /// <summary>キャッシュ済みの切り出し矩形（未キャッシュの場合は null）。</summary>
    private EsPixelRect? _rect;

    /// <summary>キャッシュ済みの切り出し画像（未キャッシュの場合は null）。</summary>
    private EsBitmap? _bitmap;

    /// <summary>破棄済みかどうか。</summary>
    private bool _disposed;

    /// <summary>キャッシュを格納した回数を取得する。</summary>
    /// <value>再切り出しをスキップしなかった（実際に切り出して格納した）回数。</value>
    /// <remarks>「同じ矩形ではスキップする」という判断が効いていることを検証するために公開する。</remarks>
    public int StoreCount { get; private set; }

    /// <summary>画像を保持しているかどうかを取得する。</summary>
    /// <value>キャッシュに画像がある場合は true。</value>
    public bool HasBitmap
    {
        get
        {
            lock (_gate)
            {
                return _bitmap is not null;
            }
        }
    }

    /// <summary>指定した矩形の結果がキャッシュ済みかどうか（再切り出しのスキップ判断）を取得する。</summary>
    /// <param name="captureRect">判定する切り出し矩形（画面物理ピクセル・閉区間）。</param>
    /// <returns>同じ矩形で画像を保持している場合は true（再切り出しは不要）。</returns>
    /// <remarks>矩形のみで判定する（同じ矩形なら切り出し結果も同じ画像になる）。</remarks>
    public bool HasCached(EsPixelRect captureRect)
    {
        lock (_gate)
        {
            return _rect == captureRect && _bitmap is not null;
        }
    }

    /// <summary>指定した矩形のキャッシュを取り出す（所有権は呼び出し側へ移る）。</summary>
    /// <param name="captureRect">取り出す切り出し矩形（画面物理ピクセル・閉区間）。</param>
    /// <returns>一致するキャッシュ画像。一致しない・未キャッシュの場合は null。</returns>
    /// <remarks>
    /// 取り出した画像は以降 <see cref="Dispose"/> では破棄されない（呼び出し側が所有する）。<br/>
    /// 同じ矩形で <see cref="Take"/> を 2 回呼ぶと 2 回目は null になる（二重破棄を防ぐ）。
    /// </remarks>
    public EsBitmap? Take(EsPixelRect captureRect)
    {
        lock (_gate)
        {
            if (_rect != captureRect || _bitmap is not { } cached)
            {
                return null;
            }

            // 所有権を呼び出し側へ移す（以降の Dispose で二重破棄しない）
            _bitmap = null;
            _rect = null;
            return cached;
        }
    }

    /// <summary>切り出した画像をキャッシュへ格納する（旧いキャッシュは破棄する）。</summary>
    /// <param name="captureRect">切り出し矩形（画面物理ピクセル・閉区間）。</param>
    /// <param name="bitmap">切り出した画像。null の場合は何もしない。</param>
    /// <remarks>
    /// 破棄済みの場合は渡された画像を破棄する（呼び出し側の後片付けを不要にする）。<br/>
    /// 格納できるのは 1 件のみで、置き換え時に旧値を破棄する。
    /// </remarks>
    public void Store(EsPixelRect captureRect, EsBitmap? bitmap)
    {
        if (bitmap is null)
        {
            return;
        }

        EsBitmap? replaced;
        lock (_gate)
        {
            if (_disposed)
            {
                // 破棄後に届いた結果は保持しない（破棄漏れを防ぐためここで破棄する）
                replaced = bitmap;
                bitmap = null!;
            }
            else
            {
                replaced = _bitmap;
                _bitmap = bitmap;
                _rect = captureRect;
                StoreCount++;
            }
        }

        // 旧値の破棄はロックの外で行う（Dispose の待ち時間を短くする）
        replaced?.Dispose();
    }

    /// <summary>保持している画像を破棄する。</summary>
    /// <remarks>複数回呼んでも安全である。以降の <see cref="Store"/> は渡された画像をその場で破棄する。</remarks>
    public void Dispose()
    {
        EsBitmap? cached;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            cached = _bitmap;
            _bitmap = null;
            _rect = null;
        }

        cached?.Dispose();
    }
}
