# ZoomViewControl 外部機能仕様書

## 目次

- [ZoomViewControl 外部機能仕様書](#zoomviewcontrol-外部機能仕様書)
  - [目次](#目次)
  - [1. 文書概要](#1-文書概要)
  - [2. 目的](#2-目的)
  - [3. ズーム遷移ルール](#3-ズーム遷移ルール)
    - [3.1 ⊖⊕ ボタンの段階（23 段階）](#31--ボタンの段階23-段階)
    - [3.2 Ctrl+ホイールの遷移（×1.1 ／ ÷1.1）](#32-ctrlホイールの遷移11--11)
  - [4. ズーム表示方式（ZoomableView）](#4-ズーム表示方式zoomableview)
    - [4.1 座標系](#41-座標系)
    - [4.2 ズーム中心維持](#42-ズーム中心維持)
    - [4.3 パンとスクロールバー](#43-パンとスクロールバー)
  - [5. Fit 表示](#5-fit-表示)
    - [5.1 Fit 実効倍率の計算](#51-fit-実効倍率の計算)
    - [5.2 Fit 中の遷移](#52-fit-中の遷移)
  - [6. 公開 API](#6-公開-api)
    - [6.1 ZoomManager](#61-zoommanager)
    - [6.2 ZoomableView](#62-zoomableview)
    - [6.3 IZoomHost](#63-izoomhost)
    - [6.4 ZoomMath / ZoomTransition / ScrollMetrics（純粋関数）](#64-zoommath--zoomtransition--scrollmetrics純粋関数)
  - [7. ログ](#7-ログ)
  - [8. 制約事項](#8-制約事項)

---

## 1. 文書概要

| 項目 | 内容 |
| --- | --- |
| 対象 | `ZoomManager` / `ZoomableView` / `ZoomMath` / `ZoomTransition` / `ScrollMetrics` / `IZoomHost` |
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

ホスト（上位 AP）は `IZoomHost` を実装して `ZoomManager` に注入し、`ZoomManager` がズーム率・表示状態を読み書きします。

## 3. ズーム遷移ルール

### 3.1 ⊖⊕ ボタンの段階（23 段階）

⊖⊕ ボタンは以下の 23 段階を順に遷移します（`ZoomTransition.ButtonZoomLevels`）。Fit は遷移に含めません。

```text
10 → 12 → 15 → 20 → 25 → 33 → 40 → 50 → 62 → 75 → 80 → 90
→ 100 → 110 → 125 → 150 → 200 → 250 → 300 → 350 → 400 → 450 → 500
```

- 現在値がリスト内 → 隣の段階へ移動
- 現在値がリスト外（スライダの中間値） → 次に大きい／小さい段階へ移動
- リスト先頭（10%）で ⊖ 停止、リスト末尾（500%）で ⊕ 停止
- Fit 表示中 → 実効倍率より大きい／小さい最初／最後の段階へ移動

### 3.2 Ctrl+ホイールの遷移（×1.1 ／ ÷1.1）

Ctrl+ホイールは現在の拡大率を基準に連続遷移します（`ZoomManager.ZoomInByWheel` / `ZoomOutByWheel`）。

- 拡大: `Round(現在の拡大率 × 1.1)`
- 縮小: `Round(現在の拡大率 ÷ 1.1)`
- 結果は 10〜500% にクランプされる
- Fit 表示中 → 実効倍率を基準に ×1.1 ／ ÷1.1 する（500% 以上の Fit は拡大不可、10% 以下の Fit は縮小不可）
- Fit は遷移に含めない（⊖⊕・ホイールでは Fit 表示を考慮しない）

## 4. ズーム表示方式（ZoomableView）

`ZoomableView`（`Estable.AvaloniaUI.ZoomView`）は **ScrollViewer を使用しない**自己完結型ビューポートです。ズーム・パンを RenderTransform（ScaleTransform + TranslateTransform）で実現し、ScrollViewer のレイアウト処理によるオフセット上書きが**原理的に発生しない**ため、ズーム中心維持の防御コード（保留オフセット再設定・収束ループ・Dispatcher.Post 保険）が不要です。

### 4.1 座標系

```text
- コンテンツ座標 = 元画像ピクセル（ズーム率に依存しない不変座標）
- ビューポート座標 = ZoomableView のローカル座標（表示座標）
- 描画位置 = 実効スケール × (コンテンツ座標 − オフセット)
- オフセット = 「ビューポート左上に対応するコンテンツ座標」。スクロール範囲
  （コンテンツ − ビューポート / 実効スケール）にクランプされる
```

- `ViewportToContent(Point)` / `ContentToViewport(Point)` で双方向変換
- 実効スケール（`EffectiveScale`）: 固定倍率中は `ズーム率 / 100`、Fit 中は `FitScale`（ビューポートに収まる Uniform スケール）。Fit 中の座標変換でゼロ除算が発生しない

### 4.2 ズーム中心維持

- **画像内にマウスポインタがある場合**: マウスポインタ位置を中心に拡大縮小
- **画像外（ビューポートの空き領域）**: ビューポート中心を基準に拡大縮小
- `ZoomRequested` イベント（`ZoomRequestedEventArgs`: `ZoomIn` + `PointerPosition`）でマウス位置を通知
- ズーム中心の計算は `ZoomMath.ComputeOffsetAfterScaleChange`（純粋関数）:

  ```text
  コンテンツ座標 c = offset + center / oldScale（不変に保つ）
  offset' = c − center / newScale（スクロール範囲にクランプ）
  ```

### 4.3 パンとスクロールバー

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

## 5. Fit 表示

### 5.1 Fit 実効倍率の計算

Fit 実効倍率は現在の表示状態（Bounds・ZoomPercent）に依存せず、**ビューポートと元画像サイズから一定に計算**します（`ZoomMath.FitZoomPercent`）。

```text
scaleX = ビューポート幅 ÷ 元画像幅
scaleY = ビューポート高さ ÷ 元画像高さ
実効倍率 = min(scaleX, scaleY) × 100   ※Uniform（アスペクト比維持）
```

### 5.2 Fit 中の遷移

- ⊖⊕: 実効倍率を基準に 23 段階リストを移動
- Ctrl+ホイール: 実効倍率 ×1.1 ／ ÷1.1 へ移動

## 6. 公開 API

### 6.1 ZoomManager

| メソッド | シグネチャ | 説明 |
| --- | --- | --- |
| `ZoomIn` | `void()` | ⊖⊕ ボタンのズームイン（23 段階） |
| `ZoomOut` | `void()` | ⊖⊕ ボタンのズームアウト（23 段階） |
| `ZoomInByWheel` | `void()` | Ctrl+ホイールのズームイン（×1.1） |
| `ZoomOutByWheel` | `void()` | Ctrl+ホイールのズームアウト（÷1.1） |
| `ApplyZoomFromSlider` | `void(double)` | スライダ値（10〜500）によるズーム適用 |
| `ApplyZoomFromText` | `bool(string?)` | テキスト入力（数値 or 数値+%）のパース・適用（10〜500） |
| `ApplyZoom` | `void()` | 現在の ZoomPercent を ZoomableView に適用（再入防止） |
| `SyncZoomSlider` | `void()` | ZoomPercent をスライダに反映（再入防止） |
| `UpdateImageAlignment` | `void()` | 中央寄せ設定を ZoomableView に反映 |
| `OnZoomableViewSizeChanged` | `void(SizeChangedEventArgs)` | リサイズ時の Fit 拡大率表示更新 |

| プロパティ | 型 | 説明 |
| --- | --- | --- |
| `ViewModel` | `IZoomHost` | ズーム対象のホスト（ZoomPercent / PreviewImage / ZoomDisplayText 等） |
| `ZoomableView` | `ZoomableView` | ズーム・パン対応ビューポート（ScrollViewer 不使用） |
| `ZoomSlider` | `Slider` | 拡大率スライダ |
| `PointerPosition` | `Point?` | ズーム中心（ビューポートローカル DIP）。null = ビューポート中心 |
| `Log` | `ILogger` | 内部ロガー。null は `NullLogger` へフォールバック |

### 6.2 ZoomableView

| メンバー | 説明 |
| --- | --- |
| `ImageControl` | 画像表示コントロール（`Image`。Crop セッション等の外部参照用） |
| `SetSource(IImage?)` | 画像ソースを設定（Fit 表示にリセット） |
| `SetZoom(double, Point?)` | ズーム率を設定（ズーム中心維持込み。null 中心 = ビューポート中心） |
| `SetOffset(Vector)` | パン位置を設定（スクロール範囲にクランプ） |
| `PanBy(Vector)` | パン（コンテンツ座標の移動量） |
| `ViewportToContent(Point)` / `ContentToViewport(Point)` | ビューポート ↔ コンテンツ座標変換 |
| `DisplaySize()` | 画像の表示サイズ（ビューポート座標） |
| `ImageOriginViewport()` | 画像原点のビューポート座標 |
| `ImageRectViewport()` | 画像の表示矩形（原点 + サイズ・ビューポート座標。表示サイズと原点を 1 回の計算で取得） |
| `ZoomPercent` | 現在のズーム率（%）。0 = Fit |
| `Offset` | 現在のパン位置（コンテンツ座標。ビューポート左上に対応） |
| `Scale` / `EffectiveScale` | ズーム率スケール / 実効スケール（Fit 中は FitScale） |
| `CenterAlign` | Fit 時の中央寄せ / 左上寄せ |
| `ZoomRequested` | Ctrl+ホイールのズーム要求イベント（`ZoomRequestedEventArgs`） |
| `ViewportChanged` | ビューポート（パン・ズーム・表示状態）変化イベント。表示適用後に発火し、オーバーレイ（暗転マスク・選択枠・ルーペ）の追従に使用する |

### 6.3 IZoomHost

ホスト（上位 AP の ViewModel 等）が実装する抽象インターフェース。

| メンバー | 説明 |
| --- | --- |
| `ZoomPercent` | 現在のズーム率（%）。0 = Fit（自動） |
| `PreviewImage` | ズーム対象のプレビュー画像。null の場合は画像なし |
| `CanZoomIn` / `CanZoomOut` | ズームイン / アウト可能かどうか |
| `ZoomDisplayText` | ステータスバー等に表示する拡大率テキスト |
| `CenterAlign` | Fit 時の画像の中央寄せ / 左上寄せ |

### 6.4 ZoomMath / ZoomTransition / ScrollMetrics（純粋関数）

| クラス | 関数 | 説明 |
| --- | --- | --- |
| `ZoomMath` | `FitZoomPercent` | Fit 実効倍率（%）の計算 |
| `ZoomMath` | `ComputeImagePoint` | ズーム中心の画像内の点（中央寄せ補正込み） |
| `ZoomMath` | `ComputeOffsetAfterZoom` | 画像サイズ変化時のオフセット計算 |
| `ZoomMath` | `ComputeOffsetAfterScaleChange` | スケール変化時のオフセット計算（ズーム中心維持） |
| `ZoomMath` | `ClampOffset` | オフセットのスクロール範囲クランプ |
| `ZoomMath` | `ViewportToContent` / `ContentToViewport` | ビューポート ↔ コンテンツ座標変換（純粋関数） |
| `ZoomTransition` | `NextLevel` / `PrevLevel` | ⊖⊕ 23 段階の遷移 |
| `ZoomTransition` | `WheelZoomIn` / `WheelZoomOut` | Ctrl+ホイール ×1.1 の遷移 |
| `ZoomTransition` | `CanZoomIn` / `CanZoomOut` | ズームイン / アウト可否判定 |
| `ScrollMetrics` | `MaxOffset` / `ViewportSize` / `IsScrollable` / `ThumbRatio` | スクロールバーの幾何計算 |

## 7. ログ

本プロジェクトのログ定義は `Logging/LogMessage.cs`（LoggerMessage ソース生成）に集約し、`ILogger` 経由で出力する。`ZoomManager.Log` へ注入されたロガーへ出力する（未設定時は `NullLogger` で無出力）。

| レベル | メソッド | 出力内容 |
| --- | --- | --- |
| `Information` | `LogZoomIn` / `LogZoomOut` | ズームイン/アウト（⊖⊕ボタン）の変更前後 % |
| `Information` | `LogZoomInWheel` / `LogZoomOutWheel` | Ctrl+ホイールズームの変更前後 % |

## 8. 制約事項

- ズーム率の適用は `ZoomPercent` の PropertyChanged（ホスト側のハンドラ）経由で `ApplyZoom()` が 1 回だけ実行される（直接呼び出しと重複しないよう再入防止フラグで保護）
- スライダの範囲は 10〜500%（0=自動はフライアウトでのみ選択）。Fit 中はスライダ最小値に合わせる
- Fit 中はパン不可（画像がビューポートに収まるため）。スクロールバーも非表示
- スクロールバーはオーバーレイ表示のため、画像の表示領域を圧迫しない
- `Ctrl + マウスホイール` は ZoomableView が捕捉し、通常のパンには伝播しない（Ctrl を離すとパンに戻る）
