using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Crop;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.Session;

/// <summary>撮影した画像（仮想スクリーン）の保持と所有権を単一所有する。</summary>
/// <remarks>
/// 撮影セッションは背景撮影の完了を待たずにオーバーレイを表示するため、画像の寿命がウィンドウの寿命と一致しない<br/>
/// （表示の開始時点では画像がまだ無い・取り消しの後に画像が届く）。<br/>
/// この<b>差し替え時の旧値の破棄</b>・<b>破棄後に届いた画像のその場での破棄</b>・<b>未取得時の占有範囲のフォールバック</b>という<br/>
/// 3 つの規則は UI フレームワークに依存しないため、本クラスが単一所有する<br/>
/// （UI がフラグと条件分岐で書くと、閉じた後・差し替えた後の破棄漏れが起きる）。<br/>
/// <br/>
/// <b>規則</b>:<br/>
/// 1. <see cref="Set"/> は画像と原点を保持し、<b>旧い画像を破棄する</b>。<br/>
/// 2. <see cref="Dispose"/> の後に <see cref="Set"/> された画像は保持せず<b>その場で破棄する</b>（false を返す）。<br/>
/// 3. <see cref="Bounds"/> は画像があればその占有範囲、未取得の場合はコンストラクタで指定したフォールバック範囲を返す。<br/>
/// <br/>
/// <b>保持する型は読み取りの面（<see cref="EsReadOnlyBitmap"/>）である</b>: 撮影が完了した画像は以降書き換えないため、<br/>
/// <see cref="Set"/> が凍結して 0 コピーで共有する（保持・後で読む値は書き換えの入口を持たない型で持つ）。<br/>
/// <br/>
/// <b>スレッド</b>: 背景の切り出し結果は UI スレッドで反映する想定だが、受け取りと破棄が競合しないよう内部で排他する。
/// </remarks>
/// <param name="fallbackBounds">画像が未取得の間に使う占有範囲（画面物理ピクセル。 ウィンドウが覆う範囲）。</param>
public sealed class CaptureImageHolder(EsRectInt fallbackBounds) : IDisposable
{
    /// <summary>状態を保護するゲート。</summary>
    private readonly Lock _gate = new();

    /// <summary>保持している画像（読み取りの面。 未取得の場合は null）。</summary>
    private EsReadOnlyBitmap? _image;

    /// <summary>画像の原点（画面物理ピクセル）。</summary>
    private EsPointInt _origin;

    /// <summary>破棄済みかどうか。</summary>
    private bool _disposed;

    /// <summary>保持している画像（読み取りの面）を取得する。</summary>
    /// <value>撮影した画像。 未取得（背景撮影中・取得失敗・破棄済み）の場合は null。</value>
    public EsReadOnlyBitmap? Image
    {
        get
        {
            lock (_gate)
            {
                return _image;
            }
        }
    }

    /// <summary>画像の原点（画面物理ピクセル）を取得する。</summary>
    /// <value>画像の左上が対応する画面物理座標。 画像が未取得の場合は既定値。</value>
    public EsPointInt Origin
    {
        get
        {
            lock (_gate)
            {
                return _origin;
            }
        }
    }

    /// <summary>切り出し元画像の占有範囲（画面物理ピクセル）を取得する。</summary>
    /// <value>画像があればその占有範囲、未取得の場合はフォールバック範囲。</value>
    /// <remarks>
    /// 背景撮影が完了する前に確定された場合でも切り出し範囲をクランプできるよう、<br/>
    /// 未取得の間はフォールバック範囲（ウィンドウが覆う仮想スクリーンの範囲）を返す。
    /// </remarks>
    public EsRectInt Bounds
    {
        get
        {
            lock (_gate)
            {
                return _image is { } image
                    ? CropClipMath.ImageBounds(image, _origin)
                    : fallbackBounds;
            }
        }
    }

    /// <summary>画像と原点を保持する（旧い画像は破棄する）。</summary>
    /// <param name="image">保持する画像（書き換え可能）。 null の場合は「未取得」として扱う（旧い画像を破棄して解放する）。</param>
    /// <param name="origin">画像の原点（画面物理ピクセル）。</param>
    /// <returns>保持した場合は true。 破棄済みのため受け付けなかった場合は false（渡された画像はその場で破棄する）。</returns>
    /// <remarks>
    /// 破棄済みの場合に画像をその場で破棄するのは、背景撮影の完了がウィンドウの後片付けより遅れた場合の破棄漏れを防ぐためである。<br/>
    /// 撮影が完了した画像は以降書き換えないため、<b>凍結して読み取り専用の面へ 0 コピーで渡し、書き換え可能の実体は解放する</b>
    /// （画素は読み取り専用の面が最後の 1 つとして解放する）。<br/>
    /// <b>したがって <paramref name="image"/> は本呼び出しの後に書き換えられない</b>（書き換えは例外になる）。
    /// </remarks>
    public bool Set(EsBitmap? image, EsPointInt origin)
    {
        // 撮影済みの画像を読み取りの面へ移す（凍結済みなら 0 コピーで共有する）
        EsReadOnlyBitmap? owned = null;
        if (image is not null)
        {
            image.SetReadOnly();
            owned = EsReadOnlyBitmap.FromBitmap(image);
            image.Dispose();
        }

        EsReadOnlyBitmap? replaced;
        bool accepted;
        lock (_gate)
        {
            accepted = !_disposed;
            if (_disposed)
            {
                // 破棄後は保持しない（その場で破棄して破棄漏れを防ぐ）
                replaced = owned;
            }
            else
            {
                replaced = _image;
                _image = owned;
                _origin = origin;
            }
        }

        // 旧値の破棄はロックの外で行う
        replaced?.Dispose();
        return accepted;
    }

    /// <summary>保持している画像を破棄する。</summary>
    /// <remarks>複数回呼んでも安全である。 以降の <see cref="Set"/> は渡された画像をその場で破棄する。</remarks>
    public void Dispose()
    {
        EsReadOnlyBitmap? held;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            held = _image;
            _image = null;
        }

        held?.Dispose();
    }
}

