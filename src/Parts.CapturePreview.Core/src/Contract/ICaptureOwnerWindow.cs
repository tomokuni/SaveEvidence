using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Session;
namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.Contract;

/// <summary>撮影中に画面から隠す対象（オーナーウィンドウ）を抽象化する。</summary>
/// <remarks>
/// 撮影の手順（キャプチャ除外の設定 → 画面外へ退避 → 撮影 → 復帰 → 除外の解除）は<br/>
/// <see cref="CaptureOwnerScope"/> が単一所有する。 本ポートは UI 固有の操作だけを担う。<br/>
/// <br/>
/// <b>退避の実装指針</b>:<br/>
/// - 非表示（<c>Hide</c>）は使わない。 復帰時にウィンドウが再作成されて「一瞬表示 → 非表示 → 再表示」の<br/>
///   ちらつきが出るため、位置の変更（画面外配置）で代用する（両 UI で実測済み）。<br/>
/// - 不透明度を持つ UI では 0 にして、退避中の合成を避ける（復帰時に元の値へ戻す）。<br/>
/// - 保存した配置は実装側が保持し、<see cref="RestorePlacement"/> で復元する。
/// </remarks>
public interface ICaptureOwnerWindow
{
    /// <summary>現在表示されているかどうかを取得する。</summary>
    /// <value>表示中の場合は true。</value>
    bool IsVisible { get; }

    /// <summary>現在の配置を保存し、ウィンドウを画面外へ退避する。</summary>
    /// <remarks>
    /// 画面外の座標は仮想スクリーンの外（負の大きな値）を使う。 Windows では標準的な「画面外配置」で、<br/>
    /// ヒットテスト・画面キャプチャ・要素検出の対象外になる。<br/>
    /// 退避できない場合（保存済みの配置が無い等）は何もしない（<see cref="RestorePlacement"/> も無効になる）。
    /// </remarks>
    void HideOffScreen();

    /// <summary><see cref="HideOffScreen"/> で保存した配置へ復帰する。</summary>
    /// <remarks>退避していない場合は何もしない（多重呼び出しでも安全）。</remarks>
    void RestorePlacement();
}

