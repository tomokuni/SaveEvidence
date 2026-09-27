# CropViewControl.Core 外部機能仕様書

`CropViewControl.Core` は UI フレームワーク非依存の矩形オーバーレイ定義とラスタライズを提供します。名前空間はすべて `EsUtil.SaveEvidence.CropView` です。

## 公開 API の可視性

| 型 | 可視性 | 参照可能なアセンブリ |
| --- | --- | --- |
| `OverlayRect` | `public` | 任意（ホストが矩形を設定する契約） |
| `OverlayLineStyle` | `public` | 任意 |
| `OverlayRectLayout` / `OverlayRectFrame` | `public` | 任意（UI 実装が描画入力を組み立てる契約） |
| `OverlayLineDash` | `internal` | `CropViewControl.AvaloniaUI` / 各テスト |
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

UI 実装の `CropViewControl.Rects` に設定すると、矩形が View（ビューポート）へ描画されます（Avalonia 版はさらにルーペ画像へも描画します）。

## `OverlayRectLayout`（描画入力の解決）

| メンバー | 説明 |
| --- | --- |
| `MinimumThickness` | 枠線の太さの下限（1 ピクセル） |
| `EffectiveThickness(double thickness)` | 太さを下限以上へ補正する |
| `ViewportRectFromCorners(EsPoint topLeft, EsPoint bottomRight)` | ビューポート座標の 2 点から矩形を組み立てる（負サイズは 0 へクランプ） |
| `Resolve(OverlayRect rect, EsPoint topLeft, EsPoint bottomRight)` | 矩形情報を描画入力（`OverlayRectFrame`）へ解決する |

**「どう描くか」の判断（負サイズのクランプ・太さの下限・破線かどうか）は本型が単一所有**します。UI 層はドメイン座標 → ビューポート座標の変換だけを行い、本メソッドへ渡します。

クランプが必要な理由は、縮小・中央寄せで右下が左上を逆転し得るためです（負のサイズを描画先へ渡すと例外になります）。MewUI の値型は負のサイズを 0 へクランプしますが Avalonia は保持するため、本メソッドを通して両 UI の挙動を揃えます。

## `OverlayRectFrame`（描画入力）

| メンバー | 型 | 説明 |
| --- | --- | --- |
| `Bounds` | `EsRect` | ビューポート座標（DIP）の矩形。幅・高さは 0 以上へクランプ済み |
| `Foreground` | `System.Drawing.Color` | 前景色 |
| `Background` | `System.Drawing.Color` | 背景色（破線のときの下地） |
| `Thickness` | `double` | 枠線の太さ（下限以上） |
| `IsDashed` | `bool` | 破線（破線または点線）かどうか |
| `DashLength` / `GapLength` | `double` | ダッシュ長と間隔（実線は 0） |
| `HalfThickness` | `double` | 枠線の半幅（矩形の辺の内側へ寄せる量） |
| `DrawsBackground` | `bool` | 背景色の下地を描くかどうか（破線のときだけ true） |
| `HasDrawableSize` | `bool` | 幅と高さがともに 0 より大きいかどうか（描画可否） |

UI 層は本値を描画先の型へ写すだけで、式と判断を持ちません。

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

- 太さの下限は `OverlayRectLayout.EffectiveThickness`（1 ピクセル）が単一所有し、本経路はそれを四捨五入して整数化します（ラスタライズ固有の処理）。
- 半幅（`太さ / 2`）だけ中心の両側へ広げて描画します。範囲外のピクセルは `EsBitmap.TrySetPixel` が無視します。

## 依存関係

| 依存 | 用途 |
| --- | --- |
| `Platform.Core`（`EsUtil.Platform.Core.*`） | コア幾何型（`EsRect` / `EsPoint`）・`EsBitmap`（描画先） |

UI フレームワーク（Avalonia / MewUI）への依存はありません。
