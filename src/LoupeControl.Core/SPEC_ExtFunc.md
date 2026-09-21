# LoupeControl.Core 外部機能仕様書

`LoupeControl.Core` は UI フレームワーク非依存のルーペロジックを提供します。型はすべて `internal` であり、公開 API はありません（`LoupeControl` とテストからのみ参照します）。

## 公開 API の可視性

| 型 | 可視性 | 参照可能なアセンブリ |
| --- | --- | --- |
| `LoupeValueCoercer` | `internal` | `LoupeControl` / `LoupeControl.AvaloniaUI.Tests` / `LoupeControl.Core.Tests` |
| `LoupePositionCalculator` | `internal` | 同上 |
| `LoupeRenderArgsCalculator` | `internal` | 同上 |
| `Imaging.LoupeImageExtractor` | `internal` | 同上 |

## `LoupeValueCoercer`（`Estable.Loupe`）

ルーペの数値パラメータの既定値と安全域を単一所有し、範囲外の値をクランプします。

### 定数

| 定数 | 値 | 説明 |
| --- | --- | --- |
| `RequestedSizeMin` / `RequestedSizeMax` / `RequestedSizeDefault` | `1` / `4096` / `160` | 表示サイズ要求値（DIP） |
| `ZoomLevelMin` / `ZoomLevelMax` / `ZoomLevelDefault` | `1` / `64` / `4` | 拡大率 |
| `RenderScalingMin` / `RenderScalingMax` / `RenderScalingDefault` | `0.1` / `8.0` / `1.0` | スケーリング係数 |
| `CheckerSizeMin` / `CheckerSizeMax` / `CheckerSizeDefault` | `1` / `128` / `6` | 市松模様の 1 タイルサイズ（ピクセル） |
| `FrameWidthMin` / `FrameWidthMax` / `FrameWidthDefault` | `1` / `64` / `2` | 外枠の太さ（ピクセル） |
| `CoordLabelFontSizeMin` / `CoordLabelFontSizeMax` / `CoordLabelFontSizeDefault` | `6` / `48` / `12.0` | 座標ラベルのフォントサイズ（DIP） |

### メソッド

| メソッド | 説明 |
| --- | --- |
| `CoerceRequestedSize(int)` | 表示サイズ要求値を 1〜4096 へクランプ |
| `CoerceZoomLevel(int)` | 拡大率を 1〜64 へクランプ |
| `CoerceRenderScaling(double)` | スケーリング係数を 0.1〜8.0 へクランプ。非有限値は `RenderScalingDefault` |
| `CoerceCheckerSize(int)` | 市松模様の 1 タイルサイズを 1〜128 へクランプ |
| `CoerceFrameWidth(int)` | 外枠の太さを 1〜64 へクランプ |
| `CoerceCoordLabelFontSize(double)` | ラベル文字サイズを 6〜48 へクランプ。非有限値は既定値 |

## `LoupePositionCalculator`（`Estable.Loupe`）

```csharp
public static (EsSize Size, EsPoint Position) Compute(
    double requestedSize, EsSize parentSize, bool hasParentSize,
    EsPoint centerDip, double offset);
```

- `Size`: 基本サイズを親領域の最小辺へクランプした正方形サイズ（幅 = 高さ）。
- `Position`: 中心（変換後の親領域 DIP 座標）+ `offset` を親領域内（`0` 〜 `親サイズ - Size`）へクランプした左上座標。
- `hasParentSize` が `false`（親領域の幅・高さが 0）のときはクランプを行いません。
- 座標変換（`ICoordTransform.Inverse`）は呼び出し側が行い、本メソッドは変換後の DIP 座標を受け取ります。

## `LoupeRenderArgsCalculator`（`Estable.Loupe`）

```csharp
public static RenderArgs Compute(double size, double renderScaling, int zoomLevel, EsPoint? center);

public readonly record struct RenderArgs(int PixelSize, int CaptureSize, EsPoint? CaptureTopLeft, double Scale);
```

| メンバー | 説明 |
| --- | --- |
| `PixelSize` | ルーペの一辺の物理ピクセルサイズ（`DpiScaler.ScaleToPixel(size, renderScaling)`。切り上げ・最低 1） |
| `CaptureSize` | 切り出し領域の一辺（ドメインピクセル。`LoupeImageExtractor.ComputeCaptureSize`） |
| `CaptureTopLeft` | 切り出し領域の左上（ドメイン座標）。`center` が `null` のときは `null` |
| `Scale` | ドメイン座標 → ルーペ画像ピクセル座標の変換係数（`size * zoomLevel / PixelSize`） |

## `Imaging.LoupeImageExtractor`（`Estable.Loupe.Imaging`）

| メソッド | 説明 |
| --- | --- |
| `ComputeCaptureSize(int pixelSize, int zoomLevel)` | ルーペの一辺（物理ピクセル）から切り出し領域の一辺（ドメインピクセル）を計算（切り上げ）。切り出し・十字線位置・選択矩形変換で共用 |
| `ComputeCaptureTopLeft(int centerX, int centerY, int size)` | 拡大中心から切り出し領域の左上（ドメイン座標）を計算 |
| `ExtractRegionAt(EsBitmap source, int centerX, int centerY, int size)` | 指定位置を中心とする正方形領域を切り出す。ソース範囲外は透明パディング。戻り値の破棄は呼び出し側 |

## 依存

| 依存 | 用途 |
| --- | --- |
| `Platform.Core`（`EsUtil.Platform.Core.*`） | コア幾何型（`EsPoint` / `EsSize`）・`DpiScaler` などの静的計算・`EsBitmap` |

UI フレームワーク（Avalonia / MewUI）への依存はありません。
