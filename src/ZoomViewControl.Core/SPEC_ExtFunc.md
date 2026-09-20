# ZoomViewControl.Core 外部機能仕様書

`ZoomViewControl.Core` は UI フレームワーク非依存のズーム・パンロジックを提供します。公開型は `ZoomConfig` のみで、ほかは `internal`（`ZoomViewControl` とテストからのみ参照）です。

## 公開 API の可視性

| 型 | 可視性 | 参照可能なアセンブリ |
| --- | --- | --- |
| `ZoomConfig` | `public` | 任意（本プロジェクトの唯一の公開型） |
| `ZoomTransition` | `internal` | `ZoomViewControl` / `ZoomViewControl.AvaloniaUI.Tests` / `ZoomViewControl.Core.Tests` |
| `ZoomMath` | `internal` | 同上 |
| `ZoomAvailability` | `internal` | 同上 |

名前空間はすべて `Estable.ZoomView` です。

## `ZoomConfig`（設定値）

ズーム遷移のパラメータをまとめた検証付き不変値です。

| メンバー | 型 | 説明 |
| --- | --- | --- |
| `MinZoom` | `double` | ズーム率の最小値（固定倍率。%）。0 より大きい |
| `MaxZoom` | `double` | ズーム率の最大値（固定倍率。%）。`MinZoom` 以上 |
| `WheelFactor` | `double` | Ctrl+ホイールのズーム倍率。1 より大きい |
| `ButtonZoomLevels` | `IReadOnlyList<double>` | ⊖⊕ ボタンのズームレベル集合。非空・昇順・`MinZoom`〜`MaxZoom` の範囲内 |
| `Default` | `ZoomConfig`（static） | 既定値（MinZoom=10 / MaxZoom=500 / WheelFactor=1.1 / 23 段階） |
| `ZoomConfig()` | コンストラクタ | 既定値で生成する |
| `ZoomConfig(double, double, double, IReadOnlyList<double>)` | コンストラクタ | 全プロパティを指定して生成する（検証付き） |
| `With(double? minZoom, double? maxZoom, double? wheelFactor, IReadOnlyList<double>? buttonZoomLevels)` | メソッド | 一部を差し替えた検証付きの新しい `ZoomConfig` を返す（null は現在値維持） |

### 例外

| 条件 | 例外 |
| --- | --- |
| `MinZoom <= 0` / `MaxZoom < MinZoom` / `WheelFactor <= 1` | `ArgumentOutOfRangeException` |
| `ButtonZoomLevels` が null | `ArgumentNullException` |
| `ButtonZoomLevels` が空・非昇順 | `ArgumentException` |
| `ButtonZoomLevels` に範囲外の値が含まれる | `ArgumentOutOfRangeException` |

## `ZoomTransition`（内部・純粋関数）

| メソッド | 説明 |
| --- | --- |
| `NextLevel(double current, double fitZoom, ZoomConfig? config = null)` | ズームインの次のレベル。移動先がなければ null。Fit 実効倍率がレベルの間に挟まる場合はそれを経由する |
| `PrevLevel(double current, double fitZoom, ZoomConfig? config = null)` | ズームアウトの前のレベル。移動先がなければ null |
| `WheelZoomIn(double current, double fitZoom, ZoomConfig? config = null)` | Ctrl+ホイールのズームイン倍率（`Round(現在 × WheelFactor)`、範囲にクランプ）。移動不可は null |
| `WheelZoomOut(double current, double fitZoom, ZoomConfig? config = null)` | Ctrl+ホイールのズームアウト倍率。移動不可は null |
| `CanZoomIn(double current, double fitZoom, ZoomConfig? config = null)` | ズームイン可能か（`NextLevel` が null でないか） |
| `CanZoomOut(double current, double fitZoom, ZoomConfig? config = null)` | ズームアウト可能か（`PrevLevel` が null でないか） |
| `BuildAvailability(double current, double fitZoom, ZoomConfig config, bool hasSource)` | ズーム可否（`CanZoomIn` / `CanZoomOut`）をまとめて計算する |

`current = 0` は Fit 表示を表し、`fitZoom`（Fit 実効倍率）を基準に遷移します。

## `ZoomMath`（内部・純粋関数）

| メソッド | 説明 |
| --- | --- |
| `FitScale(EsSize imageSize, EsSize viewportSize)` | Fit 表示時の実効スケール（Uniform。幅・高さのスケールの小さい方） |
| `FitZoomPercent(EsSize imageSize, EsSize viewportSize)` | Fit 実効倍率（%）= `FitScale × 100` |
| `ComputeEffectiveScale(double zoomPercent, EsSize imageSize, EsSize viewportSize)` | 現在の表示に使用する実効スケール（固定倍率は `zoomPercent / 100`、Fit は `FitScale`） |
| `MaxCentered(double displaySize, double viewportSize, bool centerAlign)` | 中央寄せの原点補正（軸単位）。スクロール対象の軸では 0 |
| `ComputeImageOrigin(EsSize displaySize, EsSize viewportSize, double zoomPercent, EsVector offset, double scale, bool centerAlign)` | 画像原点のビューポート座標 |
| `ComputeOffsetAfterScaleChange(EsVector offset, EsPoint center, double oldScale, double newScale, EsSize contentSize, EsSize viewport)` | ズーム率変更後のオフセット（ズーム中心のコンテンツ座標を不変に保つ。スクロール範囲にクランプ） |
| `ClampOffset(EsVector offset, double scale, EsSize contentSize, EsSize viewport)` | オフセットをスクロール範囲内へクランプ |
| `ViewportToContent(EsPoint viewportPos, EsVector offset, double effectiveScale)` | ビューポート座標 → コンテンツ座標（オフセット基準） |
| `ContentToViewport(EsPoint contentPos, EsVector offset, double effectiveScale)` | コンテンツ座標 → ビューポート座標（オフセット基準） |
| `ViewportToContentImage(EsPoint viewportPos, EsPoint imageOrigin, double effectiveScale)` | ビューポート座標 → コンテンツ座標（画像表示矩形の原点基準） |
| `ContentToViewportImage(EsPoint contentPos, EsPoint imageOrigin, double effectiveScale)` | コンテンツ座標 → ビューポート座標（画像表示矩形の原点基準） |

- スケールが 0 以下の場合は 1 として扱います（ゼロ除算防止）。
- スクロール範囲の計算は `PlatformFactory.Core` の `ScrollMath` へ委譲します。

## `ZoomAvailability`（内部・値オブジェクト）

```csharp
internal readonly record struct ZoomAvailability(bool CanZoomIn, bool CanZoomOut);
```

2 つの可否を単一の値として保持し、「片方だけ計算済み」という矛盾状態を構造的に排除します（null = 未計算）。

## 依存

| 依存 | 用途 |
| --- | --- |
| `PlatformFactory.Core`（`Estable.Platform.*`） | コア幾何型（`EsPoint` / `EsVector` / `EsSize`）・`ScrollMath` |

UI フレームワーク（Avalonia / MewUI）への依存はありません。
