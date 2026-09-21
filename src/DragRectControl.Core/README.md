# DragRectControl.Core

`DragRectControl`（リサイズ可能なドラッグ矩形）の**UI 非依存の状態層**です。Avalonia 版と MewUI 版が同じ状態管理・ロジックを共有します。

## 目的

- 選択矩形の幾何計算・状態機械・**入力の解釈（判断）**を 1 箇所に集約し、UI ごとの実装差から切り離す。
- UI 層は「入力イベントの受付」「効果の適用」「通知の転送」「描画」だけを担う薄い層にし、判断を持たない。
- UI 非依存のためヘッドレス実行が不要で、`[Fact]` で並列実行できる。
- 座標は `EsPoint` / `EsRect` / `EsSize`（`EsUtil.Platform.Core.Geometry`）で扱う。

## 層の分担

```text
UI 層（DragRectControl.AvaloniaUI / .MewUI）
  ポインター・キー入力をコア型へ変換して通知 → 返された効果を適用 → 通知を公開イベントへ転送 → 状態を描画
        │ DragRectFeedback（効果: 再描画 / フォーカス / マウスキャプチャ / カーソル操作・形状）
        ▼
状態層（本プロジェクト）
  DragRectSession  入力の解釈と状態遷移の判断（nudge セッション・ダブルクリックの確定/キャンセル・
                   ハンドルのスナップ・クリック検出・最小サイズによるキャンセル・エコー移動の抑制）
        │ DragRectNotice（通知: 種別・通知座標・スナップショット）
        ▼
UI 層（公開イベントへ転送）
```

## 構成

| 型 | 可視性 | 責務 |
| --- | --- | --- |
| `DragHandle` | public | ドラッグハンドル種別（8 方向 + `Move`） |
| `DragRectInteractionState` | public | 対話状態（NoSelection / StartPointNudging / Dragging / DraggingNudging / Selected / SelectedNudging） |
| `DragRectSnapshot` | public | 通知時点の状態の値オブジェクト（`selectionRect` は `EsRect`、`mousePosition` は `EsPoint`） |
| `DragRectSession` | internal | **状態管理クラス**。入力の通知を受けて状態を更新し、効果（`DragRectFeedback`）と通知（`DragRectNotice`）を返す |
| `DragRectNotice` / `DragRectNoticeKind` | internal | コアから UI 層への通知（種別・通知座標・スナップショット） |
| `DragRectFeedback` / `DragRectCursorAction` | internal | コアから UI 層への効果（再描画・フォーカス・マウスキャプチャ・カーソル操作・カーソル形状） |
| `DragRectState` | internal | 選択矩形・ドラッグ・nudge の状態保持と更新ロジック（純粋計算の適用） |
| `SelectionGeometry` | internal | 純粋な幾何計算（正規化・ハンドル位置・近傍判定・対角・境界クリップ・ドラッグ変形） |
| `DragRectLog` | internal | 入力の判断に伴う LoggerMessage 定義（UI 層の操作ログは各 UI 実装が持つ） |

名前空間は `Estable.DragRect`（幾何は `Estable.DragRect.Selection`）。アセンブリ名は `DragRectControl.Core`。

## 依存関係

```text
DragRectControl.Core
    ├── Platform.Core（Es* 幾何型 / EsKey / ICoordTransform / ICursorService）
    └── Microsoft.Extensions.Logging.Abstractions（LoggerMessage ソース生成のみ）
```

**UI フレームワークへ依存しません。** カーソル操作は `ICursorService` 抽象にのみ依存し、未指定（`null`）の場合は何もしません。既定実装（Win32）の解決は UI 側の責務です。

## 主な API

```csharp
var session = new DragRectSession(cursorService) { Log = logger };  // cursorService / logger は null 可
session.SetPort(new CoordTransform(p => p, p => p, () => new EsSize(1920, 1080))); // ドメイン境界

// 入力の通知（UI 層から呼ぶ）
var feedback = session.Press(new EsPoint(100, 100), clickCount: 1);   // 押下（新規選択・ハンドル・確定/キャンセル）
feedback = session.Move(new EsPoint(400, 300));                       // 移動（ドラッグ・ホバー）
feedback = session.Release(new EsPoint(400, 300));                    // 解放（確定・キャンセル・クリック）
feedback = session.KeyDown(EsKey.Right, cursorPosition: null);        // キー（nudge・Escape・Enter）
session.Notified += (_, notice) => Raise(notice);                    // 通知（公開イベントへ転送）
```

| メソッド | 用途 |
| --- | --- |
| `Press` / `Move` / `Release` | ポインター入力の通知（ドメイン座標）。ダブルクリックの確定/キャンセル・スナップ・クリック検出を含む |
| `KeyDown` / `NudgeByKey` | キー入力の通知（`EsKey`）。Escape / Enter（`Return` も同一）と矢印キーの nudge |
| `EndNudge` / `Detach` | nudge の終了・対話の終了（フォーカス喪失・ビジュアルツリーからの切断） |
| `SetSelection` / `Reset` / `RequestConfirm` / `RequestCancel` / `ApplyClampToBounds` | ホスト・プロパティ変更からの操作 |
| `ToDomain` / `ToDip` / `SetPort` | 座標変換（ドメイン ⇔ ローカル DIP） |
| `SelectionRect` / `HasSelection` / `NudgeActive` / `MarkerPosition` / `LastMousePosition` | 描画用の状態読み取り |

## 設計上の約束

1. **判断はコア・反映は UI**: 「どうするか」（nudge の開始/終了・確定/キャンセル・スナップの有無・キャンセル可否・カーソル種別）はコアが決め、UI は効果を適用するだけにする。
2. **単一所有**: 幾何計算は `SelectionGeometry` のみ、状態は `DragRectState` のみ、入力の解釈は `DragRectSession` のみが持つ。
3. **座標系**: `EsRect` / `EsPoint` は常にドメイン座標。UI 論理座標（DIP）との変換は `ICoordTransform`（`Port`）が担う。
4. **境界 1 行変換**: 各 UI のイベント引数は自前の型（例: Avalonia の `Rect`）で公開し、境界で 1 行変換する。キーは `EsKey` を介して通知する。
5. **カーソル操作の分離**: 状態機械は `ICursorService` 抽象のみに依存し、プラットフォーム既定実装を生成しない。カーソル種別は `DragHandle` として返し、UI が自 UI のカーソル型へ写像する。
6. **Enter と Return は同一**: Avalonia の `Key` は両者が同値のエイリアスで、名前ベースの相互変換では `Return` が得られるため、双方を確定キーとして扱う。

## テスト

```text
tests/
├── SelectionGeometry.Tests.cs    # 純粋幾何（17 件）
├── DragRectState.Tests.cs        # 状態遷移・ヒットテスト・ドラッグ・nudge・境界クリップ（32 件）
├── DragRectSession.Tests.cs      # 入力の解釈・通知・効果（43 件）
└── DragRectLog.Tests.cs          # ログ定義の生成コード（IsEnabled 両パス・書式）（3 件）
```

```powershell
dotnet test src/DragRectControl.Core/tests/DragRectControl.Core.Tests.csproj
```

## ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
