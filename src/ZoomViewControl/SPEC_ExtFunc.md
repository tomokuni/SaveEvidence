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
  - [6. ズーム表示方式（ZoomableView）](#6-ズーム表示方式zoomableview)
    - [6.1 座標系](#61-座標系)
    - [6.2 ズーム中心維持](#62-ズーム中心維持)
    - [6.3 パンとスクロールバー](#63-パンとスクロールバー)
  - [7. Fit 表示](#7-fit-表示)
    - [7.1 Fit 実効倍率の計算](#71-fit-実効倍率の計算)
    - [7.2 Fit 中の遷移](#72-fit-中の遷移)
  - [8. 公開 API](#8-公開-api)
    - [8.1 ZoomViewControl](#81-zoomviewcontrol)
    - [8.2 ZoomableView](#82-zoomableview)
    - [8.3 ZoomMath / ZoomTransition / ScrollMetrics（純粋関数）](#83-zoommath--zoomtransition--scrollmetrics純粋関数)
    - [8.4 ZoomAvailability / ZoomChangedEventArgs（値オブジェクト）](#84-zoomavailability--zoomchangedeventargs値オブジェクト)
  - [9. ログ](#9-ログ)
  - [10. 制約事項](#10-制約事項)

---

## 1. 文書概要

| 項目 | 内容 |
| --- | --- |
| 対象 | `ZoomViewControl` / `ZoomableView` / `ZoomMath` / `ZoomTransition` / `ScrollMetrics` |
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

- `PointerPosition`: Ctrl+ホイールズーム時のズーム中心（ビューポートローカル DIP）。null = ビューポート中心
- ホストからズーム率を直接変更する場合は `zoomView.ZoomPercent = value` を使用する（`ZoomableView` が単一所有。読み書き可能プロパティ）
- スライダの `ValueChanged` は XAML ロード中（Slider 初期化の coerce）にも発火するため、**依存注入後のコンストラクタで購読**し、`ApplyZoomFromSlider` へ委譲する
- `ZoomViewControl` 内部で `ZoomRequested`（Ctrl+ホイールのズーム要求）と `SizeChanged`（ビューポートリサイズの Fit 再適用）を購読する

### 4.2 イベント連携

- **`ZoomChanged`**: `ZoomChangedEventArgs`（`ZoomPercent` + `Availability`）でズーム率と可否を通知する。スライダ・⊖⊕ ボタンの同期に使用する
- **`ZoomableView.ZoomRequested`**（Ctrl+ホイール）: `ZoomRequestedEventArgs`（`ZoomIn` + `PointerPosition`）でマウス位置を通知する。`ZoomViewControl` が内部で購読し、ズームを適用した後に `PointerPosition` を null へ戻す（ホスト側の配線不要）
- **`ZoomableView.ViewportChanged`**: パン・ズーム・表示状態の変化を通知する。オーバーレイ（暗転マスク・選択枠・ルーペ等）の追従に使用する
- **`ZoomableView.SizeChanged`**: ビューポートのリサイズ時に Fit 表示を再適用する（`ZoomViewControl` が内部で購読する）

## 5. ズーム遷移ルール

### 5.1 ⊖⊕ ボタンの段階（23 段階）

⊖⊕ ボタンは以下の 23 段階を順に遷移します（`ZoomTransition.ButtonZoomLevels`）。Fit は遷移に含めません。

```text
10 → 12 → 15 → 20 → 25 → 33 → 40 → 50 → 62 → 75 → 80 → 90
→ 100 → 110 → 125 → 150 → 200 → 250 → 300 → 350 → 400 → 450 → 500
```

- 現在値がリスト内 → 隣の段階へ移動
- 現在値がリスト外（スライダの中間値） → 次に大きい／小さい段階へ移動
- リスト先頭（10%）で ⊖ 停止、リスト末尾（500%）で ⊕ 停止
- Fit 表示中 → 実効倍率より大きい／小さい最初／最後の段階へ移動

### 5.2 Ctrl+ホイールの遷移（×1.1 ／ ÷1.1）

Ctrl+ホイールは現在の拡大率を基準に連続遷移します（`ZoomViewControl.ZoomInByWheel` / `ZoomOutByWheel`）。

- 拡大: `Round(現在の拡大率 × 1.1)`
- 縮小: `Round(現在の拡大率 ÷ 1.1)`
- 結果は 10〜500% にクランプされる
- Fit 表示中 → 実効倍率を基準に ×1.1 ／ ÷1.1 する（500% 以上の Fit は拡大不可、10% 以下の Fit は縮小不可）
- Fit は遷移に含めない（⊖⊕・ホイールでは Fit 表示を考慮しない）

## 6. ズーム表示方式（ZoomableView）

`ZoomableView`（`Estable.AvaloniaUI.ZoomView`）は **ScrollViewer を使用しない**自己完結型ビューポートです。ズーム・パンを RenderTransform（ScaleTransform + TranslateTransform）で実現し、ScrollViewer のレイアウト処理によるオフセット上書きが**原理的に発生しない**ため、ズーム中心維持の防御コード（保留オフセット再設定・収束ループ・Dispatcher.Post 保険）が不要です。

### 6.1 座標系

```text
- コンテンツ座標 = 元画像ピクセル（ズーム率に依存しない不変座標）
- ビューポート座標 = ZoomableView のローカル座標（表示座標）
- 描画位置 = 実効スケール × (コンテンツ座標 − オフセット)
- オフセット = 「ビューポート左上に対応するコンテンツ座標」。スクロール範囲
  （コンテンツ − ビューポート / 実効スケール）にクランプされる
```

- `ViewportToContent(Point)` / `ContentToViewport(Point)` で双方向変換
  - オフセット（スクロール）基準。ビューポート左上に対応するコンテンツ座標を基準に変換する
- `ViewportToContentImage(Point)` / `ContentToViewportImage(Point)` で双方向変換
  - 画像表示矩形（`ImageRectViewport`）の原点基準。Fit 中央寄せの原点補正を含み、画像内の正確な座標変換ができる
- 実効スケール（`EffectiveScale`）: 固定倍率中は `ズーム率 / 100`、Fit 中は `FitScale`（ビューポートに収まる Uniform スケール）。Fit 中の座標変換でゼロ除算が発生しない

### 6.2 ズーム中心維持

- **画像内にマウスポインタがある場合**: マウスポインタ位置を中心に拡大縮小
- **画像外（ビューポートの空き領域）**: ビューポート中心を基準に拡大縮小
- `ZoomRequested` イベント（`ZoomRequestedEventArgs`: `ZoomIn` + `PointerPosition`）でマウス位置を通知
- ズーム中心の計算は `ZoomMath.ComputeOffsetAfterScaleChange`（純粋関数）:

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
  - 範囲（Maximum / ViewportSize）・サムサイズは `ScrollMetrics`（純粋関数）で計算
  - スクロールバーのドラッグ・矢印・ページ送りでパン（`ValueChanged` → オフセット更新）
  - ズーム・パン時にスクロールバーの値と範囲を同期

## 7. Fit 表示

### 7.1 Fit 実効倍率の計算

Fit 実効倍率は現在の表示状態（Bounds・ZoomPercent）に依存せず、**ビューポートと元画像サイズから一定に計算**します（`ZoomMath.FitZoomPercent`）。

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

自己完結型複合コントロール（`public sealed class : Panel`）。`ZoomableView` を内包し、ズーム率・遷移・表示を統括する。

| メソッド / プロパティ | シグネチャ | 説明 |
| --- | --- | --- |
| `Source` | `IImage?`（読み書き） | ズーム対象の画像。設定すると Fit 表示にリセット |
| `ZoomPercent` | `int`（読み書き） | ズーム率（%）。getter は ZoomableView から取得、setter はズーム中心維持込みで設定（0 = Fit） |
| `CenterAlign` | `bool`（読み書き） | Fit 時の画像の中央寄せ / 左上寄せ |
| `ZoomDisplayText` | `string`（読み取り） | ステータスバー等に表示する拡大率テキスト（本コントロールが更新） |
| `ZoomIn` | `void()` | ⊖⊕ ボタンのズームイン（23 段階） |
| `ZoomOut` | `void()` | ⊖⊕ ボタンのズームアウト（23 段階） |
| `ZoomInByWheel` | `void()` | Ctrl+ホイールのズームイン（×1.1） |
| `ZoomOutByWheel` | `void()` | Ctrl+ホイールのズームアウト（÷1.1） |
| `ApplyZoomFromSlider` | `void(double)` | スライダ値（10〜500）によるズーム適用 |
| `ApplyZoomFromText` | `bool(string?)` | テキスト入力（数値 or 数値+%）のパース・適用（10〜500） |
| `CanZoomIn` | `bool` | 現在の状態でズームイン可能か（段階遷移仕様に基づく遅延計算キャッシュ） |
| `CanZoomOut` | `bool` | 現在の状態でズームアウト可能か（段階遷移仕様に基づく遅延計算キャッシュ） |
| `ZoomChanged` | `event EventHandler<ZoomChangedEventArgs>` | ズーム率変更の通知（変更後のズーム率 + 可否。ホスト側のスライダ・⊖⊕ ボタン等の同期用） |
| `PointerPosition` | `Point?` | ズーム中心（ビューポートローカル DIP）。null = ビューポート中心 |
| `ZoomableView` | `ZoomableView` | 内包するパン・ズームビューポート（低レベル機能・座標変換・オーバーレイ追従用） |
| `Log` | `ILogger` | 内部ロガー。null は `NullLogger` へフォールバック |

> `CanZoomIn` / `CanZoomOut` はキャッシュ付きの遅延計算で、`null`（未計算）状態を参照時に再計算してキャッシュする（ズーム状態が変わる箇所で未計算に戻す）。初期表示時にイベントを待たずにボタン状態を設定する場合は、本プロパティを参照する。

### 8.2 ZoomableView

| メンバー | 説明 |
| --- | --- |
| `ImageControl` | 画像表示コントロール（`Image`。Crop セッション等の外部参照用） |
| `SetSource(IImage?)` | 画像ソースを設定（Fit 表示にリセット） |
| `SetZoom(double, Point?)` | ズーム率を設定（ズーム中心維持込み。null 中心 = ビューポート中心） |
| `SetOffset(Vector)` | パン位置を設定（スクロール範囲にクランプ） |
| `PanBy(Vector)` | パン（コンテンツ座標の移動量） |
| `ViewportToContent(Point)` / `ContentToViewport(Point)` | ビューポート ↔ コンテンツ座標変換（オフセット基準） |
| `ViewportToContentImage(Point)` / `ContentToViewportImage(Point)` | ビューポート ↔ コンテンツ座標変換（画像表示矩形の原点基準。Fit 中央寄せ補正込み） |
| `DisplaySize()` | 画像の表示サイズ（ビューポート座標） |
| `ImageRectViewport()` | 画像の表示矩形（原点 + サイズ・ビューポート座標。表示サイズと原点を 1 回の計算で取得） |
| `ZoomPercent` | 現在のズーム率（%）。0 = Fit |
| `Offset` | 現在のパン位置（コンテンツ座標。ビューポート左上に対応） |
| `Scale` / `EffectiveScale` | ズーム率スケール / 実効スケール（Fit 中は FitScale） |
| `CenterAlign` | Fit 時の中央寄せ / 左上寄せ |
| `ZoomRequested` | Ctrl+ホイールのズーム要求イベント（`ZoomRequestedEventArgs`） |
| `ViewportChanged` | ビューポート（パン・ズーム・表示状態）変化イベント。表示適用後に発火し、オーバーレイ（暗転マスク・選択枠・ルーペ）の追従に使用する |

### 8.3 ZoomMath / ZoomTransition / ScrollMetrics（純粋関数）

| クラス | 関数 | 説明 |
| --- | --- | --- |
| `ZoomMath` | `FitScale` / `FitZoomPercent` | Fit 実効スケール / 実効倍率（%）の計算 |
| `ZoomMath` | `ComputeOffsetAfterScaleChange` | スケール変化時のオフセット計算（ズーム中心維持） |
| `ZoomMath` | `ClampOffset` | オフセットのスクロール範囲クランプ |
| `ZoomMath` | `ViewportToContent` / `ContentToViewport` | ビューポート ↔ コンテンツ座標変換（純粋関数・オフセット基準） |
| `ZoomMath` | `ViewportToContentImage` / `ContentToViewportImage` | ビューポート ↔ コンテンツ座標変換（純粋関数・画像表示矩形の原点基準） |
| `ZoomTransition` | `NextLevel` / `PrevLevel` | ⊖⊕ 23 段階の遷移 |
| `ZoomTransition` | `WheelZoomIn` / `WheelZoomOut` | Ctrl+ホイール ×1.1 の遷移 |
| `ZoomTransition` | `CanZoomIn` / `CanZoomOut` | ズームイン / アウト可否判定 |
| `ScrollMetrics` | `MaxOffset` / `ViewportSize` | スクロールバーの範囲・表示範囲の計算 |

### 8.4 ZoomAvailability / ZoomChangedEventArgs（値オブジェクト）

| メンバー | 説明 |
| --- | --- |
| `ZoomAvailability` | `readonly record struct`。ズーム操作の可否（`CanZoomIn` + `CanZoomOut`）を単一の値として保持する。公開面は Non-Null |
| `ZoomAvailability?` | `ZoomViewControl` 内部で「未計算」（null）を表すキャッシュに使用。参照時に遅延計算してキャッシュし、2 値の整合性を構造的に保証する |
| `ZoomChangedEventArgs` | `sealed record (int ZoomPercent, ZoomAvailability Availability)`。ズーム率と可否を Non-Null の値オブジェクトで公開。`CanZoomIn` / `CanZoomOut` はコンビニエンスプロパティ |

## 9. ログ

本プロジェクトのログ定義は `Logging/LogMessage.cs`（LoggerMessage ソース生成）に集約し、`ILogger` 経由で出力する。`ZoomViewControl.Log` へ注入されたロガーへ出力する（未設定時は `NullLogger` で無出力）。

| レベル | メソッド | 出力内容 |
| --- | --- | --- |
| `Information` | `LogZoomIn` / `LogZoomOut` | ズームイン/アウト（⊖⊕ボタン）の変更前後 % |
| `Information` | `LogZoomInWheel` / `LogZoomOutWheel` | Ctrl+ホイールズームの変更前後 % |

## 10. 制約事項

- ズーム率は `ZoomViewControl` が単一所有し、`ZoomPercent` の setter で `ZoomableView.SetZoom`（ズーム中心維持込み）と `ApplyZoom`（表示テキスト・可否・イベント通知）が 1 回だけ実行される（再入防止フラグで保護）
- スライダの範囲は 10〜500%（0=自動はフライアウトでのみ選択）。Fit 中はスライダ最小値に合わせる
- Fit 中はパン不可（画像がビューポートに収まるため）。スクロールバーも非表示
- スクロールバーはオーバーレイ表示のため、画像の表示領域を圧迫しない
- `Ctrl + マウスホイール` は ZoomableView が捕捉し、通常のパンには伝播しない（Ctrl を離すとパンに戻る）
