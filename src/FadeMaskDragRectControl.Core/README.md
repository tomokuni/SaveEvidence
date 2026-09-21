# FadeMaskDragRectControl.Core

`FadeMaskDragRectControl`（領域選択の複合コントロール）の**UI 非依存コア**です。Avalonia 版と MewUI 版が同じ
動作モードとモードから描画設定への写像、およびルーペの選択矩形オーバーレイ描画を共有します。

## 目的

- 動作モード（enum）の**重複定義を解消**する（両 UI が同一の型を使用する）。
- モードから描画設定への写像を単一所有し、UI 間でモードの解釈がずれないようにする。
- ルーペ画像への破線オーバーレイ描画を共有する（Avalonia 版は描画完了イベント経由、MewUI 版は描画直後に使用する）。

## 構成

| 型 | 可視性 | 責務 |
| --- | --- | --- |
| `FadeMaskDragRectMode` | public | 動作モード（Crop / AreaSelect / ScreenSelect / WindowSelect） |
| `FadeMaskDragRectModes` | internal | モード → 描画モードの写像と、選択矩形・ルーペを使うモードかの判定 |
| `FadeMaskDragRectOverlay` | internal | 選択矩形の 4 辺を 2 色の破線で描画する。破線の幾何は `EnumerateDashes` が単一所有し、`DrawSelectionRect` が `EsBitmap`（ルーペ画像）へ描く |

名前空間は `Estable.FadeMaskDragRect`。アセンブリ名は `FadeMaskDragRectControl.Core`。

## 依存関係

```text
FadeMaskDragRectControl.Core
    ├── Platform.Core（Es* 幾何型 / EsBitmap）
    └── FadeMaskControl.Core（FadeMaskRenderMode）
```

**UI フレームワークへ依存しません。**

## 主な API

```csharp
// モード → 描画設定の写像（両 UI が同じ判断を使う）
FadeMaskRenderMode renderMode = FadeMaskDragRectModes.ToRenderMode(FadeMaskDragRectMode.Crop);
bool usesDragRect = FadeMaskDragRectModes.UsesDragRect(FadeMaskDragRectMode.Crop);  // true

// ルーペ画像への選択矩形オーバーレイ（倍率 2 未満では描画しない）
FadeMaskDragRectOverlay.DrawSelectionRect(
    bitmap, selectionRect, captureTopLeft, scale, colorA, colorB);
```

| メソッド | 用途 |
| --- | --- |
| `FadeMaskDragRectModes.ToRenderMode(mode)` | モードに対応する `FadeMaskRenderMode` を返す（未定義値は `WindowSelect` 扱い） |
| `FadeMaskDragRectModes.UsesDragRect(mode)` | 選択矩形とルーペを使うモードか（Crop / AreaSelect）を判定する |
| `FadeMaskDragRectOverlay.DrawSelectionRect(...)` | 選択矩形の 4 辺を 2 色の破線でルーペ画像へ描画する |

## 設計上の約束

1. **モードの単一所有**: `FadeMaskDragRectMode` は本プロジェクトのみが定義する。各 UI は `Mode` プロパティの型として参照する。
2. **モード解釈の単一所有**: モードと描画設定の対応は `FadeMaskDragRectModes` のみが持つ。UI 側に `switch` を書かない。
3. **描画は `EsBitmap` へ直接書く**: UI の描画 API を使わないため、両 UI で共有できる。
4. **座標はコア型**: `EsRect` / `EsPoint` / `System.Drawing.Color` を使用する（変換は各 UI の境界で 1 行）。

## テスト

```text
tests/
├── FadeMaskDragRectModes.Tests.cs    # モード写像・使用判定（10 件）
└── FadeMaskDragRectOverlay.Tests.cs  # 破線描画の画素検証（7 件）
```

```powershell
dotnet test src/FadeMaskDragRectControl.Core/tests/FadeMaskDragRectControl.Core.Tests.csproj
```

## ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
