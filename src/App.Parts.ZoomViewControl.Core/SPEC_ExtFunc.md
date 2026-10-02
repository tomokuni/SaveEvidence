# App.Parts.ZoomViewControl.Core 外部機能仕様書

`App.Parts.ZoomViewControl.Core` は UI フレームワーク非依存のズーム・パンロジックを提供します。 **状態と判断は `ZoomViewSession` が単一所有**し、UI 層は入力の変換・効果の適用・描画だけを担います。

## 公開 API の可視性

| 型 | 可視性 | 参照可能なアセンブリ |
| --- | --- | --- |
| `ZoomViewSession` | `public` | 任意（UI 実装が状態と判断を委譲する） |
| `ZoomConfig` | `public` | 任意（本コアの拡張点。 ホストがズーム範囲・倍率・レベル集合を指定する） |
| `ZoomState` | `public` | 任意（通知する 4 値） |
| `ZoomFeedback` | `public` | 任意（UI が適用する効果） |
| `ZoomNotice` / `ZoomNoticeKind` | `public` | 任意（セッションから UI への通知） |
| `ZoomViewAppearance` | `public` | 任意（外観の単一値） |
| `ZoomScrollBar` / `ZoomScrollBarMetrics` | `public` | 任意（スクロールバーの矩形と同期値） |
| `ZoomLogMessage` / `ZoomLogFormat` | `public` | 任意（ログ定義と書式） |
| `ZoomTransition` | `internal` | `App.Parts.CapturePreviewControl.AvaloniaUI` / `App.Parts.CapturePreviewControl.MewUI` / 各テスト |
| `ZoomMath` | `internal` | 同上 |
| `ZoomAvailability` | `internal` | 同上 |
| `ZoomScrollBarGeometry` | `internal` | 同上 |

名前空間はすべて `EsUtil.SaveEvidence.Parts.ZoomView` です。

## `ZoomViewSession`（状態管理クラス）

ズームビューの状態（内部ズーム率・オフセット・設定・表示条件）を保持し、入力の解釈と判断を単一所有します。

### 単一所有する判断

| # | 判断 |
| --- | --- |
| 1 | Fit の表現（内部ズーム率 0 = 自動）と実効ズーム率の導出（Fit 中はクランプした実効倍率） |
| 2 | Fit 実効倍率の算出・キャッシュと無効化（コンテンツ・ビューポートの変更時のみ） |
| 3 | ズーム可否と通知値 `ZoomState` の確定 |
| 4 | ズーム遷移の適用（段階・ホイール）と、ズーム中心を維持したオフセット再計算 |
| 5 | パン（grab 方式）・スクロール範囲へのクランプ・ホイール回転量からの換算（1 ノッチ = 48 DIP） |
| 6 | 画像変更時の Fit 復帰、ビューポート変更時のキャッシュ無効化 |
| 7 | スクロールバーのドラッグ（サムのヒット判定・トラック押下のサム移動・サム位置からのオフセット逆算） |
| 8 | 実効ズーム率の設定（0 以下の拒否とクランプ） |

### 入力（判断を伴う操作）

| メソッド | シグネチャ | 内容 |
| --- | --- | --- |
| `SetContent` | `ZoomFeedback SetContent(bool hasSource, EsSize contentSize)` | コンテンツ（画像）の設定。 Fit 表示へリセットする |
| `SetViewport` | `ZoomFeedback SetViewport(EsSize viewportSize)` | ビューポートのサイズ。 Fit キャッシュを常に無効化する |
| `SetCenterAlign` | `ZoomFeedback SetCenterAlign(bool centerAlign)` | 中央寄せ／左上寄せ（同値なら何もしない） |
| `SetAppearance` | `ZoomFeedback SetAppearance(ZoomViewAppearance appearance)` | 外観（同値なら何もしない） |
| `ApplyConfig` | `ZoomFeedback ApplyConfig(ZoomConfig config)` | 設定の反映（可否のキャッシュを破棄する） |
| `SetFixedZoom` | `ZoomFeedback SetFixedZoom(double zoomPercent)` | 実効ズーム率の設定（0 以下は無視、範囲へクランプ、ズーム中心を維持） |
| `SetZoom` | `ZoomFeedback SetZoom(double zoomPercent, EsPoint? center = null)` | 内部ズーム率の設定（0 以下は Fit。 ズーム中心を維持） |
| `SetOffset` | `ZoomFeedback SetOffset(EsVector offset)` | パン位置の設定（クランプ。 同値なら何もしない） |
| `PanBy` | `ZoomFeedback PanBy(EsVector delta)` | コンテンツ座標の移動量でパン |
| `PanByWheel` | `ZoomFeedback PanByWheel(EsVector delta, bool shift)` | ホイールの回転量でパン（実効倍率で除す） |
| `Press` / `Move` / `Release` | `ZoomFeedback Press(EsPoint position)` ほか | grab 方式のパン開始・追従・終了 |
| `ZoomIn` / `ZoomOut` / `ZoomFit` | `ZoomFeedback ZoomIn()` ほか | 段階遷移・Fit への移行 |
| `ZoomByWheel` | `ZoomFeedback ZoomByWheel(bool zoomIn, EsPoint? pointerPosition = null)` | ホイール倍率のズーム（中心を指定可能） |
| `TryBeginScrollBarDrag` | `bool TryBeginScrollBarDrag(EsPoint position)` | スクロールバーのドラッグ開始（トラック押下はサム移動） |
| `UpdateScrollBarDrag` / `ReleaseScrollBarDrag` | `ZoomFeedback UpdateScrollBarDrag(EsPoint position)` ほか | ドラッグの反映と終了 |

### 読み取り

`IsFit` / `InternalZoomPercent` / `ZoomPercent`（実効値）/ `Scale` / `EffectiveScale` / `Offset` / `CanZoomIn` / `CanZoomOut` / `CurrentState` / `DisplaySize` / `ImageOrigin` / `ImageRect` / `CurrentScrollBars()` / `ScrollMetrics(bool vertical)` / `ViewportToContent` / `ViewportToContentImage` / `ContentToViewportImage` / `IsPanning` / `IsDraggingScrollBar`

### 通知

`Notified`（`EventHandler<ZoomNotice>`）が `ZoomChanged`（`ZoomState` を運ぶ）と `ViewportChanged`（画像表示矩形を運ぶ）を送ります。 UI 層は自 UI の公開イベントへ転送するだけです。

## `ZoomViewAppearance`（外観）

下地色・市松模様（タイルサイズと 2 色）・スクロールバー（表示可否・太さ・余白・サム色・トラック色）を単一所有します。 描画の可否（`HasCheckerboard` / `HasScrollBarTrack`）と、市松模様を描く範囲（`ComputeCheckerArea`。 画像の表示矩形とビューポートの交差）・明色タイル矩形（`ComputeLightTileRects`）、タイルサイズの実効値（`ResolveCheckerTileSize`。 0 以下 = 描かない / 1 は下限 2 へ切り上げ）も本型が持ちます。 色は UI 非依存の `System.Drawing.Color` です。

**市松模様は画像の透明部分の背景**であり、描く範囲は画像の表示矩形の内側に限ります（画像の外＝ペーストボードは下地色 `BackgroundColor` の単色になります）。 タイルの格子はビューポートの原点 `(0, 0)` を基準にするため、画像をパンしても模様が這いません。 範囲の外へはみ出すタイルは範囲で切り取った矩形として返します（呼び出し側は描画先をクリップする必要がありません）。

## `ZoomLogMessage` / `ZoomLogFormat`（ログ）

ズーム操作の実行前後を Information で記録します。 書式は両 UI で同一です（例: `[ZoomIn] 50% -> 62%`）。 **変更前の値は実効ズーム率**（Fit 中は実効倍率）を記録し、丸めは `ZoomLogFormat.Percent` が単一所有します。

## `ZoomConfig`（設定値）

ズーム遷移のパラメータをまとめた検証付き不変値です。

| メンバー | 型 | 説明 |
| --- | --- | --- |
| `MinZoom` | `double` | ズーム率の最小値（固定倍率。 %）。 0 より大きい |
| `MaxZoom` | `double` | ズーム率の最大値（固定倍率。 %）。 `MinZoom` 以上 |
| `WheelFactor` | `double` | Ctrl+ホイールのズーム倍率。 1 より大きい |
| `ButtonZoomLevels` | `IReadOnlyList<double>` | ⊖⊕ ボタンのズームレベル集合。 非空・昇順・`MinZoom`〜`MaxZoom` の範囲内 |
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
| `NextLevel(double current, double fitZoom, ZoomConfig? config = null)` | ズームインの次のレベル。 移動先がなければ null。 Fit 実効倍率がレベルの間に挟まる場合はそれを経由する |
| `PrevLevel(double current, double fitZoom, ZoomConfig? config = null)` | ズームアウトの前のレベル。 移動先がなければ null |
| `WheelZoomIn(double current, double fitZoom, ZoomConfig? config = null)` | Ctrl+ホイールのズームイン倍率（`Round(現在 × WheelFactor)`、範囲にクランプ）。 移動不可は null |
| `WheelZoomOut(double current, double fitZoom, ZoomConfig? config = null)` | Ctrl+ホイールのズームアウト倍率。 移動不可は null |
| `CanZoomIn(double current, double fitZoom, ZoomConfig? config = null)` | ズームイン可能か（`NextLevel` が null でないか） |
| `CanZoomOut(double current, double fitZoom, ZoomConfig? config = null)` | ズームアウト可能か（`PrevLevel` が null でないか） |
| `BuildAvailability(double current, double fitZoom, ZoomConfig config, bool hasSource)` | ズーム可否（`CanZoomIn` / `CanZoomOut`）をまとめて計算する |

`current = 0` は Fit 表示を表し、`fitZoom`（Fit 実効倍率）を基準に遷移します。

## `ZoomMath`（内部・純粋関数）

| メソッド | 説明 |
| --- | --- |
| `FitScale(EsSize imageSize, EsSize viewportSize)` | Fit 表示時の実効スケール（Uniform。 幅・高さのスケールの小さい方） |
| `FitZoomPercent(EsSize imageSize, EsSize viewportSize)` | Fit 実効倍率（%）= `FitScale × 100` |
| `ComputeEffectiveScale(double zoomPercent, EsSize imageSize, EsSize viewportSize)` | 現在の表示に使用する実効スケール（固定倍率は `zoomPercent / 100`、Fit は `FitScale`） |
| `MaxCentered(double displaySize, double viewportSize, bool centerAlign)` | 中央寄せの原点補正（軸単位）。 スクロール対象の軸では 0 |
| `ComputeImageOrigin(EsSize displaySize, EsSize viewportSize, double zoomPercent, EsVector offset, double scale, bool centerAlign)` | 画像原点のビューポート座標 |
| `ComputeOffsetAfterScaleChange(EsVector offset, EsPoint center, double oldScale, double newScale, EsSize contentSize, EsSize viewport)` | ズーム率変更後のオフセット（ズーム中心のコンテンツ座標を不変に保つ。 スクロール範囲にクランプ） |
| `ClampOffset(EsVector offset, double scale, EsSize contentSize, EsSize viewport)` | オフセットをスクロール範囲内へクランプ |
| `ViewportToContent(EsPoint viewportPos, EsVector offset, double effectiveScale)` | ビューポート座標 → コンテンツ座標（オフセット基準） |
| `ContentToViewport(EsPoint contentPos, EsVector offset, double effectiveScale)` | コンテンツ座標 → ビューポート座標（オフセット基準） |
| `ViewportToContentImage(EsPoint viewportPos, EsPoint imageOrigin, double effectiveScale)` | ビューポート座標 → コンテンツ座標（画像表示矩形の原点基準） |
| `ContentToViewportImage(EsPoint contentPos, EsPoint imageOrigin, double effectiveScale)` | コンテンツ座標 → ビューポート座標（画像表示矩形の原点基準） |

- スケールが 0 以下の場合は 1 として扱います（ゼロ除算防止）。
- スクロール範囲の計算は `Platform.Core` の `ScrollMath` へ委譲します。

## `ZoomAvailability`（内部・値オブジェクト）

```csharp
internal readonly record struct ZoomAvailability(bool CanZoomIn, bool CanZoomOut);
```

2 つの可否を単一の値として保持し、「片方だけ計算済み」という矛盾状態を構造的に排除します（null = 未計算）。

## 依存関係

| 依存 | 用途 |
| --- | --- |
| `Platform.Core`（`EsUtil.Platform.Core.*`） | コア幾何型（`EsPoint` / `EsVector` / `EsSize`）・`ScrollMath` |

UI フレームワーク（Avalonia / MewUI）への依存はありません。
