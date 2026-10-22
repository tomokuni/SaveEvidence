using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Native;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Crop;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.Capture;

/// <summary>カーソル下の要素の検出結果を選択オーバーレイへ反映するための状態を表す。</summary>
/// <param name="SourceRect">ハイライト矩形（対象座標 = 画像ローカル物理ピクセル）。 未検出の場合は null（<b>null が「ハイライトなし」を意味する</b>）。</param>
/// <param name="TitleChanged">直前の検出と比べて表示名が変わったかどうか。</param>
/// <remarks>
/// UI 層は本値を選択コントロールへ設定するだけにし、座標変換・空文字への正規化・変化の判定を書かない。<br/>
/// 値型かつ文字列は参照共有のため、ホットパス（マウス移動）で追加の割り当てを発生させない。<br/>
/// 直前の状態（矩形・表示名）は <see cref="CaptureHighlightSession"/> が保持する<br/>
/// （本値は 1 回の更新の結果であり、更新後の状態はセッションのプロパティから読む）。
/// </remarks>
public readonly record struct CaptureHighlight(EsRect? SourceRect, bool TitleChanged);

/// <summary>ハイライト（スクリーン選択 / ウィンドウ選択）の状態・判断・ログを単一所有する。</summary>
/// <remarks>
/// カーソル下の要素の検出結果から「ハイライトの有無・ハイライト矩形（対象座標）・表示名」を導出する手順は<br/>
/// UI フレームワークに依存しないため、本クラスが単一所有する（両 UI が同じ手順で同じ表示にする）。<br/>
/// <br/>
/// <b>所有する判断</b>:<br/>
/// 1. 未検出時の正規化（矩形は null・表示名は空文字。 検出矩形を使わない）。<br/>
/// 2. 画面物理座標 → 対象座標の変換（画像の原点を減算する。<see cref="CropClipMath.ToLocalRect(EsRectInt, EsPointInt)"/> へ委譲）。<br/>
/// 3. 表示名の変化の判定（情報ラベルの再描画に必要な最小限の判断。 UI ごとの文字列比較を書かせない）。<br/>
/// 4. 検出結果のログ（<see cref="CapturePreviewLogMessage.LogHighlight"/>。 両 UI で同じ事象を同じ書式で記録する）。<br/>
/// <br/>
/// <b>ハイライトの有無は <see cref="SourceRect"/> の null が表す</b>（有効フラグを別に持たない）。<br/>
/// <b>状態</b>は「直前の表示名」とハイライト矩形を持ち、矩形の複製を選択コントロールへ渡す。
/// </remarks>
/// <param name="logger">内部ログの転送先。 null の場合はログを出力しない。</param>
public sealed class CaptureHighlightSession(ILogger? logger = null)
{
    /// <summary>内部ロガー（null の場合は出力なしに正規化）。</summary>
    private readonly ILogger _log = logger ?? NullLogger.Instance;

    /// <summary>直前に検出した要素の表示名（未検出・未取得の場合は空文字）を取得する。</summary>
    /// <value>情報ラベル（ウィンドウタイトル）に表示する文言。 未検出の場合は空文字。</value>
    /// <remarks>情報ラベルの文言プロバイダが参照する（表示時のみ評価される）。</remarks>
    public string Title { get; private set; } = TextNormalize.Normalize(null);

    /// <summary>直前に検出した要素のハイライト矩形（対象座標）を取得する。</summary>
    /// <value>ハイライト矩形（画像ローカル物理ピクセル）。 未検出の場合は null。</value>
    /// <remarks>サイズラベルの対象の解決（<see cref="CaptureTypeTraits.ResolveRect"/>）が参照する（未検出の判定も兼ねる）。</remarks>
    public EsRect? SourceRect { get; private set; }

    /// <summary>検出結果を反映し、選択オーバーレイへ設定する状態を返す。</summary>
    /// <param name="captureType">キャプチャ種別（ログと対象変換に使う）。</param>
    /// <param name="detection">検出結果（画面物理座標）。</param>
    /// <param name="imageOrigin">切り出し元画像の原点（画面物理ピクセル）。</param>
    /// <returns>対象座標のハイライト矩形（未検出の場合は null）と、表示名の変化の有無。</returns>
    /// <remarks>
    /// 1. 検出できていない場合は矩形を null・表示名を空文字へ正規化する。<br/>
    /// 2. 検出できている場合は検出矩形を対象座標へ変換する。<br/>
    /// 3. 表示名の変化を判定し、状態を更新する。<br/>
    /// 4. 検出結果をログへ記録する（未検出も含めて記録する）。<br/>
    /// マウス移動ごとに呼ばれるため、割り当て（配列・リスト・文字列整形）を行わない。
    /// </remarks>
    public CaptureHighlight Update(CaptureType captureType, ElementFinderResults detection, EsPointInt imageOrigin)
    {
        var local = detection.IsDetected
            ? CropClipMath.ToLocalRect(detection.Bounds, imageOrigin)
            : default;
        var title = TextNormalize.Normalize(detection.IsDetected ? detection.Title : null);
        var titleChanged = !string.Equals(title, Title, StringComparison.Ordinal);

        Title = title;
        SourceRect = detection.IsDetected ? new EsRect(local.X, local.Y, local.Width, local.Height) : null;

        // 整形（文字列化）と有効判定はログ定義（LogHighlight）が単一所有する（本クラスは検出結果を渡すだけ）
        _log.LogHighlight(
            captureType,
            detection.IsDetected,
            detection.IsDetected ? local : null,
            title,
            detection.ClassName);

        return new CaptureHighlight(SourceRect, titleChanged);
    }
}


