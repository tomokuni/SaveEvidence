namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.ZoomView;

/// <summary>ズーム操作の可否（ズームイン・ズームアウト）を表す値オブジェクト。</summary>
/// <param name="CanZoomIn">ズームイン可能かどうか。</param>
/// <param name="CanZoomOut">ズームアウト可能かどうか。</param>
/// <remarks>
/// 2 つの可否を単一の値として保持し、「片方だけ計算済み」という矛盾状態を構造的に排除する。<br/>
/// UI 実装のズーム可否プロパティの遅延計算キャッシュに使用し、<br/>
/// null（未計算）を <see cref="bool"/> の実値（false 等）と区別できるようにする。<br/>
/// コントロール内部のキャッシュ値オブジェクトであり、外部ホストからは直接参照しない（テストのみ internal アクセス）。
/// </remarks>
internal readonly record struct ZoomAvailability(bool CanZoomIn, bool CanZoomOut);
