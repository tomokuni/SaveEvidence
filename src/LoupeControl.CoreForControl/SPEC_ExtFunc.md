# LoupeControl.CoreForControl 外部機能仕様書

`LoupeControl.CoreForControl` は、`Feature.LoupeCore`（ルーペの見た目・計算）を**他の UI コントロールへ組み込むときにだけ必要な処理**を提供します。**全型が public** です（`InternalsVisibleTo` は持ちません）。

## 公開 API の可視性

| 型 | 可視性 | 用途 |
| --- | --- | --- |
| `LoupeRenderCoordinator` | `public` | 再描画要求のバッチ化（1 フレームに 1 回）と世代番号管理 |

中心座標ラベルの配置指示（実寸 → 要素の配置矩形の写像）は、描画の手順と同じ層である UI 非依存の描画層（`LoupeControl.Rendering` の `LoupeLabelPlan`）が単一所有します（本層は関知しません）。実寸測定と要素の配置は UI 層が行います。

## `LoupeRenderCoordinator`（`EsUtil.SaveEvidence.Loupe.CoreForControl`・`public sealed class`）

ルーペの再描画要求をまとめ、実行キューへのポストを 1 回に結合します。UI フレームワークに依存しないため、UI 層が実行キューと優先度を注入します。

| メンバー | 説明 |
| --- | --- |
| `LoupeRenderCoordinator(Func<bool> isVisible, Action releaseBitmap, Action<long> startRender, Action<long> logRenderCore, Action<Action> post)` | 生成。すべてのコールバックは null 不可（null は `ArgumentNullException`） |
| `Generation` | 現在の世代番号（描画要求のたびに増える。古い世代のビットマップ破棄判定に使用する） |
| `ShouldApply(long generation)` | 表示中かつ指定世代が最新なら true（古い世代・非表示では破棄する） |
| `RequestRender()` | 再描画を要求する（複数要求は 1 回のポストに結合される。非表示時は解放のみ行う） |
| `CancelAndRelease()` | 未実行の要求を破棄してビットマップを解放する（非表示遷移・明示クリアで共用・冪等） |
| `CancelPending()` | 未実行の再描画要求のみを破棄する |
| `RenderCore()` | ポストされた再描画を実行する（世代番号を採番して描画を開始する・要求が取り消されていれば何もしない） |

**引数のコールバック**:

| 引数 | 用途 |
| --- | --- |
| `isVisible` | 現在表示中かどうか（非表示時は描画せず解放する判定に使う） |
| `releaseBitmap` | ビットマップを解放して表示をクリアする |
| `startRender` | 指定した世代番号で描画を開始する（中間ビットマップの生成など） |
| `logRenderCore` | 再描画コア実行の記録（出力の有無はロガー自身が判定する） |
| `post` | 実行キューへ処理を投入する（UI 層がキューと優先度を指定する）。**必須**（未指定は配線の誤り） |

```csharp
var coordinator = new LoupeRenderCoordinator(
    isVisible: () => IsVisible,
    releaseBitmap: ReleaseBitmap,
    startRender: StartRender,
    logRenderCore: generation => Log.LogRenderCore(generation),
    post: action => Dispatcher.UIThread.Post(action, DispatcherPriority.Render));

coordinator.RequestRender();
if (coordinator.ShouldApply(generation)) { /* 生成したビットマップを表示へ反映する */ }
```

**振る舞い**:

- **バッチ化**: 要求が既に積まれている間は追加要求を無視する（1 フレームに 1 回の生成へ結合する）。
- **非表示時**: 世代番号を進め、ビットマップを解放する（描画は行わない）。
- **世代番号**: 描画の実行時に採番する（`ShouldApply` により古い世代の結果を破棄できる）。
- **優先度は UI 層が決める**: 実行キューと優先度（Avalonia では `DispatcherPriority.Render` 等）はフレームワーク固有であるため、本クラスはポストの方法を判断しない。
