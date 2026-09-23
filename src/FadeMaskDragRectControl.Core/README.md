# FadeMaskDragRectControl.Core

`FadeMaskDragRectControl`（領域選択の複合コントロール）の**UI 非依存コア**です。Avalonia 版と MewUI 版が同じ
設定値・動作モード・モードから導出する状態・選択矩形の可否判定、およびルーペの選択矩形オーバーレイ描画を共有します。

## 目的

- 動作モード（enum）の**重複定義を解消**する（両 UI が同一の型を使用する）。
- **設定を 1 つの値として単一所有**する（既定値・項目の対応・内包コントロールへの分配規則を UI に持たせない）。
- モードから導出する状態（表示可否）を単一所有し、UI 間で振る舞いがずれないようにする。
- 選択矩形の可否判定を単一所有する（0 サイズの選択が片方の UI だけに残らないようにする）。
- ルーペ画像への破線オーバーレイ描画を共有する（Avalonia 版は描画完了イベント経由、MewUI 版は描画直後に使用する）。

## 構成

| 型 | 可視性 | 責務 |
| --- | --- | --- |
| `FadeMaskDragRectMode` | public | 動作モード（Crop / AreaSelect / ScreenSelect / WindowSelect） |
| `FadeMaskDragRectOptions` | public | 設定の単一値（`DragRect` / `Loupe` / 選択矩形破線の色 / ルーペオフセット）。**外側の装飾の外観も `DragRect.OuterAppearance` が単一所有する**（枠線を 2 箇所に持たない） |
| `FadeMaskDragRectState` | internal | モードから導出する状態（選択矩形・破線・全面暗転の可否とルーペの表示可否） |
| `FadeMaskDragRectModes` | internal | モード → 状態の解決（`Resolve`） |
| `FadeMaskDragRectSelection` | internal | 選択矩形の可否判定（幅・高さが正か） |
| `FadeMaskDragRectOverlay` | internal | 選択矩形の 4 辺を 2 色の破線で描画する。破線の幾何は `EnumerateDashes` が単一所有し、`DrawSelectionRect` が `EsBitmap`（ルーペ画像）へ描く |

名前空間は `Estable.FadeMaskDragRect`。アセンブリ名は `FadeMaskDragRectControl.Core`。

## 依存関係

```text
FadeMaskDragRectControl.Core
    ├── Platform.Core（Es* 幾何型 / EsBitmap）
    ├── DragRectControl.Core（DragRectOptions / DragRectFrame / 選択矩形の外側の装飾（`Estable.DragRect.Decoration`））
    └── LoupeControl.Core（LoupeOptions）
```

**UI フレームワークへ依存しません。**

## 主な API

```csharp
// 設定の単一値（既定値は各内包コントロールの既定値を参照する）
FadeMaskDragRectOptions options = FadeMaskDragRectOptions.Default with
{
    Loupe = LoupeOptions.Default with { RequestedSize = 240 },
};
// 選択矩形の設定は外側の装飾（暗転マスク・穴あけ枠・ラベル）の外観も含むため、そのまま 1 回で分配できる
DragRectOptions dragRect = options.DragRect;

// モード → 状態（表示可否）の解決（両 UI が同じ判断を使う）
FadeMaskDragRectState state = FadeMaskDragRectModes.Resolve(FadeMaskDragRectMode.Crop);
bool showLoupe = state.IsLoupeVisible(hasSource: true, hasCenter: true);  // true

// 選択矩形の可否判定
bool usable = FadeMaskDragRectSelection.IsUsable(width, height);

// ルーペ画像への選択矩形オーバーレイ（倍率 2 未満では描画しない）
FadeMaskDragRectOverlay.DrawSelectionRect(
    bitmap, selectionRect, captureTopLeft, scale, colorA, colorB);
```

| メソッド | 用途 |
| --- | --- |
| `FadeMaskDragRectModes.Resolve(mode)` | モードに対応する状態を返す（未定義値は `WindowSelect` 扱い） |
| `FadeMaskDragRectState.IsLoupeVisible(hasSource, hasCenter)` | ルーペを表示するか（選択矩形を使うモード・画像あり・中心あり） |
| `FadeMaskDragRectState.ShowSelectionOverlay` | ルーペ画像上の選択矩形破線を描くか（選択矩形を使うモードのみ）。UI は描画のたびに本値を問い合わせる（派生値を自前で保持しない） |
| `FadeMaskDragRectSelection.IsUsable(width, height)` | 選択として成立するサイズか（幅・高さが正） |
| `FadeMaskDragRectOverlay.DrawSelectionRect(...)` | 選択矩形の 4 辺を 2 色の破線でルーペ画像へ描画する |

## 設計上の約束

1. **設定の単一所有**: 設定値・既定値・内包コントロールへの分配規則は `FadeMaskDragRectOptions` のみが持つ。UI 側に既定値の写しを持たない。
2. **モードの単一所有**: `FadeMaskDragRectMode` は本プロジェクトのみが定義する。各 UI は `Mode` プロパティの型として参照する。
3. **モード解釈の単一所有**: モードと状態（表示可否）の対応は `FadeMaskDragRectModes` / `FadeMaskDragRectState` のみが持つ。UI 側に `switch` を書かない。
4. **選択矩形の可否も本プロジェクトが持つ**（UI 側に「幅 > 0」の判定を書かない）。
5. **枠線は選択矩形の設定が単一所有する**: 選択枠線は暗転マスクの穴あけ枠と重なるため、`DragRect.OuterAppearance` の 1 値だけを持つ（写し替える導出処理を持たない）。
6. **描画は `EsBitmap` へ直接書く**: UI の描画 API を使わないため、両 UI で共有できる。
7. **座標はコア型**: `EsRect` / `EsPoint` / `System.Drawing.Color` を使用する（変換は各 UI の境界で 1 行）。

## テスト

```text
tests/
├── FadeMaskDragRectModes.Tests.cs      # モードの解決・ルーペ表示可否
├── FadeMaskDragRectOptions.Tests.cs    # 既定値・部分変更・外側の装飾の単一所有
├── FadeMaskDragRectSelection.Tests.cs  # 選択矩形の可否判定
└── FadeMaskDragRectOverlay.Tests.cs    # 破線描画の画素検証
```

```powershell
dotnet test src/FadeMaskDragRectControl.Core/tests/FadeMaskDragRectControl.Core.Tests.csproj
```

## ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
