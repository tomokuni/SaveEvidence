# CropViewControl.Core 外部機能仕様書

`CropViewControl.Core` は UI フレームワーク非依存の矩形オーバーレイ定義とラスタライズを提供します。名前空間はすべて `Estable.CropView` です。

## 公開 API の可視性

| 型 | 可視性 | 参照可能なアセンブリ |
| --- | --- | --- |
| `OverlayRect` | `public` | 任意 |
| `OverlayLineStyle` | `public` | 任意 |
| `OverlayLineDash` | `internal` | `CropViewControl` / `CropViewControl.AvaloniaUI.Tests` / `CropViewControl.Core.Tests` |
| `OverlayRectRasterizer` | `internal` | 同上 |

## `OverlayRect`（構造体）

```csharp
public readonly record struct OverlayRect(
    EsRect Bounds, OverlayLineStyle Style, Color Foreground, Color Background, double Thickness);
```

| メンバー | 型 | 説明 |
| --- | --- | --- |
| `Bounds` | `EsRect` | 矩形（ドメイン座標 = 元画像ピクセル座標） |
| `Style` | `OverlayLineStyle` | 枠線スタイル |
| `Foreground` | `System.Drawing.Color` | 前景色（透明度を含む） |
| `Background` | `System.Drawing.Color` | 背景色（透明度を含む）。破線の市松風 2 色目 |
| `Thickness` | `double` | 枠線の太さ（ピクセル） |

UI 実装の `CropViewControl.Rects` に設定すると、矩形が View（ビューポート）とルーペ画像の両方へ描画されます。

## `OverlayLineStyle`（列挙型）

| メンバー | 説明 |
| --- | --- |
| `Solid` | 実線 |
| `Dashed` | 破線（長いダッシュ。ダッシュ長 8 / 間隔 4） |
| `Dotted` | 点線（短いダッシュ。ダッシュ長 2 / 間隔 2） |

## `OverlayLineDash`（内部・ダッシュ長の単一所有）

| メンバー | 説明 |
| --- | --- |
| `Get(OverlayLineStyle style)` | スタイルに対応するダッシュ長と間隔を返す（実線は 0 / 0）。単位は呼び出し側の描画経路に従う |
| `IsDashed(OverlayLineStyle style)` | 破線または点線の場合は true（実線は false） |
| `SolidLength` / `DashedLength` / `DashedGap` / `DottedLength` / `DottedGap` | ダッシュ長の定数 |

枠線はベクター経路（UI の図形要素）とラスタ経路（ルーペ画像のピクセル）の 2 つで描画されるため、数値の所有者を本クラスに一本化し、片方だけ変更される事故を防ぎます。

## `OverlayRectRasterizer`（内部・ラスタライズ）

| メソッド | 説明 |
| --- | --- |
| `Draw(EsBitmap bitmap, OverlayRect rect, EsPoint captureTopLeft, double scale)` | 矩形の 4 辺（上・下・左・右）をルーペ画像へ描画する |

### 座標変換

- ドメイン座標 → ルーペピクセル座標: `(ドメイン座標 − captureTopLeft) × scale`（半開区間の終点を含むよう幅・高さへ +1 する）。
- 矩形が描画範囲外の場合は何もせずに戻ります（早期リターン）。

### 破線

- 破線はダッシュごとに前景色と背景色を交互に塗ります（市松風の視認性）。
- ダッシュ長は `OverlayLineDash` を使用します。

### 線の太さ

- 太さは四捨五入して整数化し、最低 1 ピクセルとします。
- 半幅（`太さ / 2`）だけ中心の両側へ広げて描画します。範囲外のピクセルは `EsBitmap.TrySetPixel` が無視します。

## 依存

| 依存 | 用途 |
| --- | --- |
| `Platform.Core`（`EsUtil.Platform.Core.*`） | コア幾何型（`EsRect` / `EsPoint`）・`EsBitmap`（描画先） |

UI フレームワーク（Avalonia / MewUI）への依存はありません。
