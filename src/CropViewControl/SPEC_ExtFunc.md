# CropView 外部機能仕様書

本仕様書は、`Estable.AvaloniaUI.CropView`（CropView ライブラリ）の**外部機能仕様・公開 API 仕様**を定義します。

## 目次

- [CropView 外部機能仕様書](#cropview-外部機能仕様書)
  - [目次](#目次)
  - [1. 概要](#1-概要)
  - [2. 公開 API](#2-公開-api)
    - [2.1. CropViewControl クラス](#21-cropviewcontrol-クラス)
    - [2.2. OverlayRect 構造体](#22-overlayrect-構造体)
    - [2.3. OverlayLineStyle 列挙型](#23-overlaylinestyle-列挙型)
  - [3. 入力・出力](#3-入力出力)
    - [3.1. 画像](#31-画像)
    - [3.2. 外部提供矩形](#32-外部提供矩形)
  - [4. 座標系](#4-座標系)
  - [5. イベント](#5-イベント)
  - [6. スレッド安全性](#6-スレッド安全性)

---

## 1. 概要

CropView は、画像表示（拡大・パン・Fit）と領域選択（ドラッグ・暗転・ルーペ）を統合した高レベル複合コントロールです。

- 画像表示（`ZoomViewControl`）・領域選択（`FadeMaskDragRectControl`）・外部提供矩形の描画を 1 コントロールに内包します。
- ホストは画像ソース・選択・ズーム操作・外部矩形を本コントロールへ設定するだけです。

## 2. 公開 API

名前空間: `Estable.AvaloniaUI.CropView`

### 2.1. CropViewControl クラス

画像表示 + 領域選択 + 外部提供矩形の統合コントロール（`Panel` のサブクラス）。

#### 2.1.1 プロパティ

| プロパティ | 型 | 説明 |
| --- | --- | --- |
| `Source` | `IImage?` | 表示画像。ZoomViewControl とルーペへ伝搬。変更時は Fit 表示にリセット。 |
| `Rects` | `IReadOnlyList<OverlayRect>` | 外部提供矩形の一覧（ドメイン座標）。View とルーペの両方へ描画。 |
| `SelectionMode` | `FadeMaskDragRectMode` | 選択モード（既定 `Crop`）。FadeMaskDragRectControl へ委譲。 |
| `ZoomPercent` | `int` | 現在のズーム率（%）。0 = Fit。 |
| `IsFit` | `bool` | 現在 Fit 表示中かどうか（ズーム率が 0 = 自動）。 |
| `ZoomDisplayText` | `string` | 拡大率表示テキスト（例: "100%"）。 |
| `HasSelection` | `bool` | 現在の選択状態（選択矩形の有無）。 |
| `SelectionRect` | `Rect` | 現在の選択矩形（ドメイン座標）。未選択時は 0x0。 |
| `ZoomView` | `ZoomViewControl` | 内包するズーム・パンビューポート（SnipCapture の Crop セッションが利用）。 |

#### 2.1.2 メソッド

| メソッド | 説明 |
| --- | --- |
| `void ZoomIn()` | ズームイン（`ZoomTransition.NextLevel` で段階遷移）。 |
| `void ZoomOut()` | ズームアウト（`ZoomTransition.PrevLevel` で段階遷移）。 |
| `void ZoomFit()` | Fit 表示（ウィンドウに合わせて表示）に戻す（`ZoomIn` / `ZoomOut` と対になるズーム操作）。 |
| `void SetSelection(Rect rect)` | 選択状態を外部から設定（FadeMaskDragRectControl へ委譲）。 |
| `void ClearSelection()` | 選択状態をクリア（FadeMaskDragRectControl へ委譲）。 |
| `void SetCoordinateTransform(ICoordinatePort? port)` | 座標変換ポートを設定（内包コントロールへ分配）。 |
| `void RequestConfirm()` | 選択確定要求（FadeMaskDragRectControl へ委譲）。 |
| `void RequestCancel()` | 選択キャンセル要求（FadeMaskDragRectControl へ委譲）。 |
| `Point ContentToViewport(Point contentPos)` | ドメイン座標 → ビューポート座標へ変換。 |
| `Point ViewportToContent(Point viewportPos)` | ビューポート座標 → ドメイン座標へ変換。 |

#### 2.1.3 イベント

| イベント | 型 | 説明 |
| --- | --- | --- |
| `ConfirmRequested` | `EventHandler<FadeMaskDragRectSelectionEventArgs>` | 選択確定要求（FadeMaskDragRectControl へ委譲）。 |
| `CancelRequested` | `EventHandler?` | 選択キャンセル要求（FadeMaskDragRectControl へ委譲）。 |
| `ZoomRequested` | `EventHandler<bool>` | ズーム要求（FadeMaskDragRectControl へ委譲）。 |
| `LoupePositionMoved` | `EventHandler<FadeMaskDragRectPointEventArgs>` | ルーペ中心の更新通知（FadeMaskDragRectControl へ委譲）。 |
| `RectsChanged` | `EventHandler?` | 外部提供矩形の変化通知。 |

### 2.2. OverlayRect 構造体

外部提供矩形の描画情報（不変レコード構造体）。

| メンバー | 型 | 説明 |
| --- | --- | --- |
| `Bounds` | `Rect` | 矩形（ドメイン座標 = 元画像ピクセル座標）。 |
| `Style` | `OverlayLineStyle` | 枠線スタイル。 |
| `Foreground` | `Color` | 前景色（透明度を含む）。 |
| `Background` | `Color` | 背景色（透明度を含む）。破線の市松風 2 色目。 |
| `Thickness` | `double` | 枠線の太さ（ピクセル）。 |

コンストラクタ: `OverlayRect(Rect Bounds, OverlayLineStyle Style, Color Foreground, Color Background, double Thickness)`

### 2.3. OverlayLineStyle 列挙型

枠線スタイル。

| メンバー | 説明 |
| --- | --- |
| `Solid` | 実線。 |
| `Dashed` | 破線（長いダッシュ）。 |
| `Dotted` | 点線（短いダッシュ）。 |

## 3. 入力・出力

### 3.1. 画像

- `Source`（`IImage?`）で表示画像を設定します。
- 画像変更時は Fit 表示にリセットされます。

### 3.2. 外部提供矩形

- `Rects`（`IReadOnlyList<OverlayRect>`）で外部提供矩形を設定します。
- 設定すると View（ビューポート）とルーペ画像の両方へ描画されます。

## 4. 座標系

- **ドメイン座標**: 元画像ピクセル（ズーム率に依存しない不変座標）。
- **ビューポート座標**: コントロールローカル DIP 座標（表示座標）。
- `ContentToViewport` / `ViewportToContent` で双方向変換します（Fit 中央寄せの原点補正を含む）。

## 5. イベント

- `ConfirmRequested` / `CancelRequested` / `ZoomRequested` / `LoupePositionMoved`: 領域選択の操作をホストへ通知します。
- `RectsChanged`: 外部提供矩形の変化を通知します（ルーペへの描画更新用フック）。

## 6. スレッド安全性

- 本コントロールは Avalonia の UI スレッドで操作することを前提とします。
- 内部のコントロール（ZoomViewControl / FadeMaskDragRect / ルーペ）も同様に UI スレッドで操作します。
