using EsUtil.SaveEvidence.Platform.WinUI3.Imaging;

namespace EsUtil.SaveEvidence.App.WinUI3.Views;

/// <summary>画像を縦横比を保って領域いっぱいへ収める表示ビュー（WinUI 3 版）。</summary>
/// <remarks>
/// 一覧のサムネイルのように「正方形の枠へ縦横比のまま収める」用途に使う。<br/>
/// 保持する画像を UI 用ビットマップへ変換せず、Skia のキャンバスへ 1 回の描画で描く<br/>
/// （中間バッファの生成とコピーを行わない）。 描画の手順は基底の <see cref="SkiaImageView"/> が<br/>
/// 単一所有し、本クラスは「縦横比を保つ」という配置方法だけを与える。
/// </remarks>
internal sealed class ThumbnailView : SkiaImageView
{
    /// <summary>縦横比を保って領域へ収めるかどうかを取得する。</summary>
    /// <value>常に true（サムネイルは縦横比を保って収める）。</value>
    /// <remarks>余白は透明のままにする（親の背景がそのまま見える）。</remarks>
    protected override bool UniformDraw => true;
}
