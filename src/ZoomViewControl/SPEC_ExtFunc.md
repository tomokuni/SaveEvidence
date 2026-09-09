# ZoomViewControl 外部機能仕様書

## 目次

- [ZoomViewControl 外部機能仕様書](#zoomviewcontrol-外部機能仕様書)
  - [目次](#目次)
  - [1. 文書概要](#1-文書概要)
  - [2. 目的](#2-目的)
  - [3. 外部利用条件](#3-外部利用条件)
    - [3.1 プロジェクト参照](#31-プロジェクト参照)
    - [3.2 必要な名前空間](#32-必要な名前空間)
  - [4. 基本的な使用方法](#4-基本的な使用方法)
    - [4.1 ZoomViewControl の配置](#41-zoomviewcontrol-の配置)
    - [4.2 イベント連携](#42-イベント連携)
  - [5. ズーム遷移ルール](#5-ズーム遷移ルール)
    - [5.1 ⊖⊕ ボタンの段階（23 段階）](#51--ボタンの段階23-段階)
    - [5.2 Ctrl+ホイールの遷移（×1.1 ／ ÷1.1）](#52-ctrlホイールの遷移11--11)
  - [6. 座標系（ズーム表示）](#6-座標系ズーム表示)
    - [6.1 座標系](#61-座標系)
    - [6.2 ズーム中心維持](#62-ズーム中心維持)
    - [6.3 パンとスクロールバー](#63-パンとスクロールバー)
  - [7. Fit 表示](#7-fit-表示)
    - [7.1 Fit 実効倍率の計算](#71-fit-実効倍率の計算)
    - [7.2 Fit 中の遷移](#72-fit-中の遷移)
  - [8. 公開 API](#8-公開-api)
    - [8.1 ZoomViewControl](#81-zoomviewcontrol)
    - [8.2 ZoomConfig（設定値）](#82-zoomconfig設定値)
      - [設定方法（2 通り）](#設定方法2-通り)
      - [値のガード](#値のガード)
    - [8.3 ZoomChangedEventArgs（イベント引数）](#83-zoomchangedeventargsイベント引数)
  - [9. 制約事項](#9-制約事項)

---

## 1. 文書概要

| 項目 | 内容 |
| --- | --- |
| 対象 | `ZoomViewControl`（公開コントロール） |
| プロジェクト | `ZoomViewControl/ZoomViewControl.csproj` |
| 名前空間 | `Estable.AvaloniaUI.ZoomView` / `Estable.AvaloniaUI.ZoomView.Logging` |
| 対象フレームワーク | `net10.0` |
| UIフレームワーク | Avalonia 12.1 |
| 主な用途 | 画像プレビューのズームイン・アウト・Fit 表示・ズーム中心維持・パン・スクロールバー |

## 2. 目的

画像プレビューのズーム操作（⊖⊕ ボタン・Ctrl+ホイール・フライアウト・スライダ）を統一的に管理し、以下の機能を提供します。

- ズーム率の段階遷移（⊖⊕ は 23 段階、Ctrl+ホイールは ×1.1 連続遷移）
- ズーム中心維持（マウスポインタ位置 / ビューポート中心）
- パン（マウスドラッグ・ホイール・スクロールバー）
- Fit 表示と実効倍率の計算・表示

ホスト（上位 AP）は `ZoomViewControl` の `Source` を設定し、`ZoomChanged` を購読するだけで、ズーム率・表示状態の管理は `ZoomViewControl` が担います。

## 3. 外部利用条件

### 3.1 プロジェクト参照

```xml
<ProjectReference Include="..\ZoomViewControl\src\ZoomViewControl.csproj" />
```

### 3.2 必要な名前空間

```csharp
using Estable.AvaloniaUI.ZoomView;
```

## 4. 基本的な使用方法

### 4.1 ZoomViewControl の配置

`ZoomViewControl`（自己完結型複合コントロール）を XAML に配置し、`Source` を設定する。ズーム要求（Ctrl+ホイール）とビューポートリサイズの配線は内部で自己完結する。スライダは `ZoomChanged` イベントを購読してホスト側で同期する。

```xml
<zoom:ZoomViewControl x:Name="ZoomView" Background="#FF2B2B2B"/>
```

```csharp
// ZoomViewControl に画像とロガーを設定（ズーム要求・リサイズの配線は内部で自己完結）
zoomView.Log = logger;              // 任意（未設定時は NullLogger）
zoomView.Source = image;

// ズーム率の変化をスライダへ同期する（ZoomViewControl は特定コントロールに依存しない）
zoomView.ZoomChanged += (_, e) => slider.Value = e.ZoomPercent;
```

- ホストからズーム率を直接変更する場合は `zoomView.ZoomPercent = value` を使用する（読み書き可能プロパティ。`double` でスライダの連続値も設定できる）
- スライダの `ValueChanged` は XAML ロード中（Slider 初期化の coerce）にも発火するため、**依存注入後のコンストラクタで購読**し、`zoomView.ZoomPercent = slider.Value` へ委譲する
- コントロールは Ctrl+ホイールのズーム要求とビューポートリサイズ（Fit 再適用）を内部で購読し、自己完結する（ホスト側の配線不要）

### 4.2 イベント連携

- **`ZoomChanged`**: `ZoomChangedEventArgs`（`ZoomPercent` + `IsFit` + `CanZoomIn` + `CanZoomOut`）で実効ズーム率・Fit 状態・可否を通知する。スライダ・⊖⊕ ボタンの同期と、表示文字列の生成に使う。表示文字列の生成（書式・丸め・% 表記等）はホスト側の責務（`ZoomPercent` は数値を通知するのみ）。Fit 判定は `IsFit` で行う（`ZoomPercent` は実効ズーム率のため 0 にならない）
- **`ViewportChanged`**: パン・ズーム・表示状態の変化を通知する。オーバーレイ（暗転マスク・選択枠・ルーペ等）の追従に使用する
- Ctrl+ホイールのズーム要求とビューポートリサイズ（Fit 再適用）はコントロールが内部で購読し、自己完結する（ホスト側の配線不要）

## 5. ズーム遷移ルール

### 5.1 ⊖⊕ ボタンの段階（23 段階）

⊖⊕ ボタンは以下の 23 段階を順に遷移します（このレベル集合は `ZoomConfig.ButtonZoomLevels` で定義されています）。

```text
10 → 12 → 15 → 20 → 25 → 33 → 40 → 50 → 62 → 75 → 80 → 90
→ 100 → 110 → 125 → 150 → 200 → 250 → 300 → 350 → 400 → 450 → 500
```

- 現在値がリスト内 → 隣の段階へ移動
- 現在値がリスト外（スライダの中間値） → 次に大きい／小さい段階へ移動
- リスト先頭（10%）で ⊖ 停止、リスト末尾（500%）で ⊕ 停止
- Fit 表示中 → 実効倍率より大きい／小さい最初／最後の段階へ移動
- **Fit 実効倍率を挟む**: 現在値 `current` からズームイン／アウトするとき、隣の段階 `next`／`prev` と Fit 実効倍率 `fitZoom` が
  `current < fitZoom < next`（ズームイン）・`prev < fitZoom < current`（ズームアウト）を満たす場合、`fitZoom` を次の倍率とする
  （23 段階レベルを通過する際に Fit 実効倍率が間に挟まる場合はそれを経由する。例: `100% → 102%（Fit実効倍率）→ 110%`）。
  近接（`fitZoom ≤ current` ／ `fitZoom ≥ next`・`prev ≤ fitZoom`）の場合は 23 段階レベルを優先する。
- **Fit 実効倍率のクランプ**: Fit 表示時でも `fitZoom` は `MinZoom`（10%）〜`MaxZoom`（500%）の範囲にクランプされる

### 5.2 Ctrl+ホイールの遷移（×1.1 ／ ÷1.1）

Ctrl+ホイールは現在の拡大率を基準に連続遷移します（`ZoomViewControl` 内部のハンドラが適用）。

- 拡大: `Round(現在の拡大率 × 1.1)`
- 縮小: `Round(現在の拡大率 ÷ 1.1)`
- 結果は 10〜500% にクランプされる
- Fit 表示中 → 実効倍率を基準に ×1.1 ／ ÷1.1 する（500% 以上の Fit は拡大不可、10% 以下の Fit は縮小不可）

## 6. 座標系（ズーム表示）

`ZoomViewControl` が提供する座標変換 API（`ViewportToContentImage` / `ContentToViewportImage` / `ImageRectViewport`）を理解するための座標系を説明します。

### 6.1 座標系

```text
- コンテンツ座標 = 元画像ピクセル（ズーム率に依存しない不変座標）
- ビューポート座標 = コントロールのローカル座標（表示座標）
- 描画位置 = 実効スケール × (コンテンツ座標 − オフセット)
- オフセット = 「ビューポート左上に対応するコンテンツ座標」。スクロール範囲
  （コンテンツ − ビューポート / 実効スケール）にクランプされる
```

- `ViewportToContentImage(Point)` / `ContentToViewportImage(Point)` で双方向変換（`ZoomViewControl` の公開 API）
  - 画像表示矩形（`ImageRectViewport`）の原点基準。Fit 中央寄せの原点補正を含み、画像内の正確な座標変換ができる
- 実効スケール（`EffectiveScale`）: 固定倍率中は `ズーム率 / 100`、Fit 中は `FitScale`（ビューポートに収まる Uniform スケール）。Fit 中の座標変換でゼロ除算が発生しない

### 6.2 ズーム中心維持

- **画像内にマウスポインタがある場合**: マウスポインタ位置を中心に拡大縮小
- **画像外（ビューポートの空き領域）**: ビューポート中心を基準に拡大縮小
- ズーム中心の計算はコントロール内部で行われ、以下の考え方に基づく:

  ```text
  コンテンツ座標 c = offset + center / oldScale（不変に保つ）
  offset' = c − center / newScale（スクロール範囲にクランプ）
  ```

### 6.3 パンとスクロールバー

- **マウスドラッグ（grab 方式）**: 左ボタン押下 + 移動でパン（ポインタキャプチャ付き）
  - 押下時に掴んだ位置の**コンテンツ座標（元画像ピクセル）**を保存し、その位置がポインタに追従するようオフセットを再計算する（`オフセット = 掴んだコンテンツ座標 − ポインタ位置 ÷ 実効スケール`）
  - **ドラッグ方向と同じ方向へコンテンツが移動**する（右下へドラッグ → コンテンツも右下へ。直観一致）
  - スクロール範囲端ではクランプで停止し、方向を戻すと再び追従する
  - Fit 中はパン不可（画像がビューポートに収まるため。オフセット 0）
- **ホイール**: 縦パン / Shift+ホイールで横パン（Ctrl+ホイールはズーム要求）
- **スクロールバー**: 縦横の `ScrollBar`（オーバーレイ表示）。コンテンツがビューポートより大きい場合のみ表示
  - 範囲（Maximum / ViewportSize）・サムサイズはコントロール内部で計算
  - スクロールバーのドラッグ・矢印・ページ送りでパン（`ValueChanged` → オフセット更新）
  - ズーム・パン時にスクロールバーの値と範囲を同期

## 7. Fit 表示

### 7.1 Fit 実効倍率の計算

Fit 実効倍率は現在の表示状態（Bounds・ZoomPercent）に依存せず、**ビューポートと元画像サイズから一定に計算**します。

```text
scaleX = ビューポート幅 ÷ 元画像幅
scaleY = ビューポート高さ ÷ 元画像高さ
実効倍率 = min(scaleX, scaleY) × 100   ※Uniform（アスペクト比維持）
```

### 7.2 Fit 中の遷移

- ⊖⊕: 実効倍率を基準に 23 段階リストを移動
- Ctrl+ホイール: 実効倍率 ×1.1 ／ ÷1.1 へ移動

## 8. 公開 API

### 8.1 ZoomViewControl

自己完結型複合コントロール（`public sealed class : Panel`）。内部ビューポートを内包し、ズーム率・遷移・表示を統括する。

| メソッド / プロパティ | シグネチャ | 説明 |
| --- | --- | --- |
| `Source` | `IImage?`（読み書き） | ズーム対象の画像。設定すると Fit 表示にリセット |
| `ZoomPercent` | `double`（読み書き） | 実効ズーム率（%）。getter は Fit 中は実効倍率（`MinZoom`〜`MaxZoom` にクランプ）・固定倍率中はズーム率を返す（0 にならない）。setter は 0 より大きい値のみ固定倍率を設定（0 以下は無視）。スライダ等の連続値も設定できる |
| `IsFit` | `bool` | 現在 Fit 表示中かどうか（内部ズーム率が 0 = 自動）。ホストは内部 `Image.Stretch` に依存せず Fit 状態を判定できる |
| `CenterAlign` | `bool`（読み書き） | 画像の中央寄せ / 左上寄せ（**軸ごとに独立**。その軸で画像がビューポートより小さい場合に有効。Fit 時・固定倍率とも。一方の軸がスクロール可能（画像が大きい）でも、他方の軸は中央寄せを維持） |
| `ZoomIn` | `void()` | ⊖⊕ ボタンのズームイン（23 段階） |
| `ZoomOut` | `void()` | ⊖⊕ ボタンのズームアウト（23 段階） |
| `ZoomFit` | `void()` | Fit 表示（ビューポートに収まる自動倍率）へ戻す（唯一の Fit 移行経路。`ZoomIn` / `ZoomOut` と対になるズーム操作） |
| `CanZoomIn` | `bool` | 現在の状態でズームイン可能か |
| `CanZoomOut` | `bool` | 現在の状態でズームアウト可能か |
| `ZoomChanged` | `event EventHandler<ZoomChangedEventArgs>` | ズーム率変更の通知（変更後のズーム率 + 可否。ホスト側のスライダ・⊖⊕ ボタン等の同期用） |
| `ViewportChanged` | `event EventHandler` | ビューポート（パン・ズーム・表示状態）変化の通知。オーバーレイ（暗転マスク・選択枠・ルーペ等）の追従に使用する |
| `ViewportToContentImage` | `Point(Point)` | ビューポート座標 → コンテンツ座標変換（画像表示矩形の原点基準） |
| `ContentToViewportImage` | `Point(Point)` | コンテンツ座標 → ビューポート座標変換（画像表示矩形の原点基準） |
| `ImageRectViewport` | `Rect()` | 画像の表示矩形（原点 + サイズ・ビューポート座標） |
| `Log` | `ILogger` | 内部ロガー。null は `NullLogger` へフォールバック |
| `MinZoom` | `double`（読み書き） | ズーム率の最小値（固定倍率）。設定時に `ZoomConfig.With` で再構築 |
| `MaxZoom` | `double`（読み書き） | ズーム率の最大値（固定倍率）。設定時に `ZoomConfig.With` で再構築 |
| `WheelFactor` | `double`（読み書き） | Ctrl+ホイールのズーム倍率。設定時に `ZoomConfig.With` で再構築 |
| `ButtonZoomLevels` | `IReadOnlyList<double>`（読み書き） | ⊖⊕ ボタンのズームレベル集合。設定時に `ZoomConfig.With` で再構築 |

> `CanZoomIn` / `CanZoomOut` は読み取り専用。初期表示時にイベントを待たずにボタン状態を設定したい場合は、これらのプロパティを参照する。

### 8.2 ZoomConfig（設定値）

ズーム遷移のパラメータをまとめた**検証付き不変値**（`public sealed class`）。ズーム範囲・倍率・レベル集合を外部からカスタマイズするための**公開拡張点**です。

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `MinZoom` | `double` | `10` | ズーム率の最小値（固定倍率） |
| `MaxZoom` | `double` | `500` | ズーム率の最大値（固定倍率） |
| `WheelFactor` | `double` | `1.1` | Ctrl+ホイールのズーム倍率 |
| `ButtonZoomLevels` | `IReadOnlyList<double>` | 23 段階 | ⊖⊕ ボタンのズームレベル集合 |
| `Default` | `ZoomConfig` | 既定値 | 既定の設定 |

#### 設定方法（2 通り）

| 方法 | 記述 | 備考 |
| --- | --- | --- |
| コンストラクタ注入 | `new ZoomViewControl(new ZoomConfig(20, 800, 1.2, [...]))` | 生成時に設定（コードのみ・XAML 配置では無引数コンストラクタを使用） |
| プロパティ設定 | `zoomView.MinZoom = 20;` など | 生成後に設定。`ZoomViewControl` の設定プロパティ（読み書き）経由で `ZoomConfig.With`（検証付き部分更新）が適用される |
| `ZoomConfig.With` | `With(double? minZoom, double? maxZoom, double? wheelFactor, IReadOnlyList<double>? buttonZoomLevels)` | 一部のプロパティを差し替えた検証付きの新しい `ZoomConfig` を返す（null は現在値維持）。不正な値は `ArgumentException` |

- `ZoomViewControl` の `MinZoom` / `MaxZoom` / `WheelFactor` / `ButtonZoomLevels` は**読み書き可能プロパティ**。設定するとズーム可否が再計算される。
- `ZoomConfig` は読み取り専用の不変値。プロパティの直接変更はできず、`With` 経由での検証付き部分更新のみ。
- 既定値（`ZoomConfig.Default`）で動作させる場合は無引数コンストラクタ（`ZoomViewControl()`）を使用する。

#### 値のガード

`ZoomConfig` はコントロールが動作する値・範囲を保証する。設定値が不変条件を満たさない場合（`MinZoom` が 0 以下・`MaxZoom` が `MinZoom` 未満・`WheelFactor` が 1 以下・`ButtonZoomLevels` が範囲外/非昇順/空）は、生成時に例外を投げる。

### 8.3 ZoomChangedEventArgs（イベント引数）

| メンバー | 説明 |
| --- | --- |
| `ZoomChangedEventArgs` | `sealed record (double ZoomPercent, bool IsFit, bool CanZoomIn, bool CanZoomOut)`。実効ズーム率・Fit 状態・ズーム可否を通知する |

## 9. 制約事項

- `ZoomPercent`（実効ズーム率）は 0 にならない。`ZoomPercent` setter で 0 を設定することはできない（0 以下は無視）。Fit 表示は `ZoomFit` で行う
- スライダの範囲は `ZoomConfig.MinZoom`〜`ZoomConfig.MaxZoom`。Fit 中はスライダ最小値に合わせる（`IsFit` を参照）
- Fit 中はパン不可（画像がビューポートに収まるため）。スクロールバーも非表示
- スクロールバーはオーバーレイ表示のため、画像の表示領域を圧迫しない
- `Ctrl + マウスホイール` はコントロールが捕捉し、通常のパンには伝播しない（Ctrl を離すとパンに戻る）
