# LoupeControl.CoreForControl

`LoupeControl.Core`（UI 非依存のルーペロジック）を**他の UI コントロールへ組み込むときにだけ必要な処理**を置く層です。現状は `LoupeControl.AvaloniaUI` が使用します（`LoupeControl.MewUI` は中間ビットマップを持たずキャンバスへ直接描画するため、再描画のバッチ化が不要です。同じ必要が生じた時点で本層のコーディネーターを注入できます）。

## 目的

- コア（`LoupeControl.Core`）を将来そのまま別プロジェクトへ独立できるよう、**ルーペの見た目・計算ではない処理をコアへ入れない**。
- コントロール化に必要な処理（再描画のスケジューリング）を UI をまたいで 1 箇所に集約する（UI ごとに書くと片方だけ変わる）。
- UI フレームワークには依存しない（実行キューへのポストをコールバックで受け取る）ため、ヘッドレス実行なしで `[Fact]` 並列検証できる。

## 収録する型

| 型 | 可視性 | 責務 |
| --- | --- | --- |
| `LoupeRenderCoordinator` | public | 再描画要求のバッチ化（1 フレームに 1 回）と世代番号管理。表示状態・ビットマップ解放・描画開始・実行キューへのポストをコールバックで受け取る |

## 依存方向

層の全体像（Core / Rendering / CoreForControl / UI）は [`src/README.md`](../README.md) を参照してください。

```text
UI 層（LoupeControl.AvaloniaUI / LoupeControl.MewUI）
  入力の受付・効果の適用・通知の転送・描画先の用意・ラベル要素の配置
    ├──> LoupeControl.Rendering   どう描くか（描画 API の呼び出し手順。ILoupeRenderer を実装する）
    ├──> LoupeControl.CoreForControl ← 本プロジェクト   組み込み先がある場合にだけ必要な規則
    └──> LoupeControl.Core   何を描くか + 判断（状態・値・純粋計算・コア⇔UI の契約）
           └──> Platform.Core（Es* 値型・ICoordTransform・IEsCanvas・EsLabelLayout）
※ Rendering と CoreForControl の間に依存はない（兄弟）。Core は Rendering / CoreForControl を参照しない。
※ 本層を使うのは LoupeControl.AvaloniaUI のみ（MewUI は中間ビットマップを持たず同期描画するため不要）。
```

本プロジェクトは UI フレームワークへ依存しません（Avalonia / MewUI の型を参照しません）。`Dispatcher.UIThread` 等の実行キューは UI 層が `post` コールバックとして注入します。

## Core との境界（なぜスケジューリングを Core に置かないか）

| 関心事 | 所有者 | 理由 |
| --- | --- | --- |
| 表示レイアウト・描画入力・通知内容の解決（ルーペの見た目と計算） | `LoupeControl.Core` | 単体で使っても同じ結果になる（独立した部品の本体） |
| 再描画要求のバッチ化・世代番号管理 | 本プロジェクト | **非同期の再描画をいつ何回行うか**は、UI へ組み込んで初めて問題になる（単体のルーペには「再描画のスケジューリング」という概念が無い） |
| 実行キューと優先度の選択（`DispatcherPriority.Render` 等） | UI 層 | フレームワーク固有の API である |
| 描画の実行（中間ビットマップ / キャンバス直描画） | UI 層 | 描画方式（中間バッファの有無）は UI ごとに異なる |

## 使い方

```csharp
// UI 層（コントロール）: 実行キューと優先度を注入して生成する
_coordinator = new LoupeRenderCoordinator(
    isVisible: () => IsVisible,
    releaseBitmap: ReleaseBitmap,
    startRender: StartRender,
    logRenderCore: generation => Log.LogRenderCore(generation),
    post: action => Dispatcher.UIThread.Post(action, DispatcherPriority.Render));

_coordinator.RequestRender();      // 複数の要求は 1 回のポストに結合される
_coordinator.CancelAndRelease();   // 非表示遷移・明示クリア（要求の破棄 + 解放）
```

## テスト

```text
tests/
└── LoupeRenderCoordinator.Tests.cs # バッチ化・世代番号・適用判定・破棄と解放・防御分岐（12 件）
```

```powershell
dotnet test src/LoupeControl.CoreForControl/tests/LoupeControl.CoreForControl.Tests.csproj
```

## ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
