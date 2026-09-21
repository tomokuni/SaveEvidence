# DragRectControl.Core

`DragRectControl`（リサイズ可能なドラッグ矩形）の**UI 非依存コア**です。Avalonia 版と MewUI 版が同じロジックを共有します。

## 目的

- 選択矩形の幾何計算・状態機械を 1 箇所に集約し、UI ごとの実装差から切り離す。
- UI 非依存のためヘッドレス実行が不要で、`[Fact]` で並列実行できる。
- 座標は `EsPoint` / `EsRect` / `EsSize`（`Estable.Platform.Geometry`）で扱う。

## 構成

| 型 | 可視性 | 責務 |
| --- | --- | --- |
| `DragHandle` | public | ドラッグハンドル種別（8 方向 + `Move`） |
| `DragRectInteractionState` | public | 対話状態（NoSelection / StartPointNudging / Dragging / DraggingNudging / Selected / SelectedNudging） |
| `DragRectSnapshot` | public | 通知時点の状態の値オブジェクト（`selectionRect` は `EsRect`、`mousePosition` は `EsPoint`） |
| `DragRectState` | internal | 選択矩形・ドラッグ・nudge の状態保持と更新ロジック |
| `SelectionGeometry` | internal | 純粋な幾何計算（正規化・ハンドル位置・近傍判定・対角・境界クリップ・ドラッグ変形） |

名前空間は `Estable.DragRect`（幾何は `Estable.DragRect.Selection`）。アセンブリ名は `DragRectControl.Core`。

## 依存関係

```text
DragRectControl.Core
    └── PlatformFactory.Core（Es* 幾何型 / ICoordTransform / ICursorService）
```

**UI フレームワークへ依存しません。** カーソル操作は `ICursorService` 抽象にのみ依存し、未指定（`null`）の場合は何もしません。既定実装（Win32）の解決は UI 側の責務です。

## 主な API

```csharp
var state = new DragRectState(cursorService);   // cursorService は null 可
state.Port = new CoordTransform(p => p, p => p, () => new EsSize(1920, 1080)); // ドメイン境界

state.StartNewSelection(new EsPoint(100, 100));
state.UpdateDrag(new EsPoint(400, 300), minSize: 10, clampToBounds: true);

DragRectSnapshot snapshot = state.CreateSnapshot();
// snapshot.SelectionRect = EsRect(100, 100, 300, 200) / snapshot.State = Dragging
```

| メソッド | 用途 |
| --- | --- |
| `HitTest(pos, hitRadius)` | 指定座標がどのハンドルに当たるか（角 → 辺 → `Move` の順） |
| `StartNewSelection` / `StartDrag` / `UpdateDrag` / `EndDrag` | ドラッグ操作 |
| `Nudge(dx, dy, minSize, clampToBounds)` | 矢印キー 1 回分の微調整（状態に応じてリサイズ・終点移動・始点移動） |
| `SetSelection` / `Reset` / `ClampSelectionToBounds` | 外部からの復元・初期化・境界クリップ |
| `GetMarkerPosition` / `GetOppositeCorner` | 十字マーカー・ルーペ追従位置 |
| `CreateSnapshot` / `GetInteractionState` | 通知用の状態取得 |

## 設計上の約束

1. **単一所有**: 幾何計算は `SelectionGeometry` のみ、状態は `DragRectState` のみが持つ。UI 側は委譲する。
2. **座標系**: `EsRect` / `EsPoint` は常にドメイン座標。UI 論理座標（DIP）との変換は `ICoordTransform`（`Port`）が担う。
3. **境界 1 行変換**: 各 UI のイベント引数は自前の型（例: Avalonia の `Rect`）で公開し、境界で 1 行変換する。
4. **カーソル操作の分離**: 状態機械は `ICursorService` 抽象のみに依存し、プラットフォーム既定実装を生成しない。

## テスト

```text
tests/
├── SelectionGeometry.Tests.cs   # 純粋幾何（19 件）
└── DragRectState.Tests.cs       # 状態遷移・ヒットテスト・ドラッグ・nudge・境界クリップ（30 件）
```

```powershell
dotnet test src/DragRectControl.Core/tests/DragRectControl.Core.Tests.csproj
```

## ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
