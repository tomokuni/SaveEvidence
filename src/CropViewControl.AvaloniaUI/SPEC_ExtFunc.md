# CropView 外部機能仕様書

本仕様書は、`EsUtil.SaveEvidence.CropView.AvaloniaUI`（CropView ライブラリ）の**外部機能仕様・公開 API 仕様**を定義します。

## 目次

- [CropView 外部機能仕様書](#cropview-外部機能仕様書)
  - [目次](#目次)
  - [1. 概要](#1-概要)
  - [2. 公開 API](#2-公開-api)
    - [2.1. CropViewControl クラス](#21-cropviewcontrol-クラス)
      - [2.1.1 プロパティ](#211-プロパティ)
      - [2.1.2 メソッド](#212-メソッド)
      - [2.1.3 イベント](#213-イベント)
    - [2.2. OverlayRect 構造体](#22-overlayrect-構造体)
    - [2.4. OverlayRectLayout / OverlayRectFrame（描画入力の解決）](#24-overlayrectlayout--overlayrectframe描画入力の解決)
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

名前空間: `EsUtil.SaveEvidence.CropView.AvaloniaUI`

### 2.1. CropViewControl クラス

画像表示 + 領域選択 + 外部提供矩形の統合コントロール（`Panel` のサブクラス）。

#### 2.1.1 プロパティ

| プロパティ | 型 | 説明 |
| --- | --- | --- |
| `Source` | `IImage?` | 表示画像。ZoomViewControl とルーペへ伝搬。変更時は Fit 表示にリセット。オーバーレイの可視性は本プロパティでは制御しない（`SetSelectionOverlayVisible` が専任）。`null` に変わるときは選択のみクリア。 |
| `Rects` | `IReadOnlyList<OverlayRect>` | 外部提供矩形の一覧（ドメイン座標）。View とルーペの両方へ描画。 |
| `SelectionMode` | `FadeMaskDragRectMode` | 選択モード（既定 `Crop`）。FadeMaskDragRectControl へ委譲。 |
| `SelectionOverlayOptions` | `FadeMaskDragRectOptions`（コア） | 領域選択オーバーレイの設定（選択矩形 / 暗転マスク / ルーペ / 選択矩形破線の寸法・表示可否）。FadeMaskDragRectControl へ委譲。 |
| `SelectionOverlayMarqueePaint` / `SelectionOverlayLoupePaint` | `MarqueePaint` / `LoupePaint`（描画層） | 領域選択オーバーレイの選択矩形・ルーペが使う色（描画にだけ必要な設定。コアの設定には含まれない）。FadeMaskDragRectControl へ委譲。 |
| `Log` | `ILogger` | 内部ロガー（ズームビューポートへ伝搬）。null の場合の吸収はコアが単一所有する（出力なし）。 |
| `DomainToDipScaling` | `double` | DPI スケーリング係数（`FadeMaskDragRectControl.DomainToDipScaling` へ委譲。コードから動的設定） |
| `LoupeDipToDeviceScaling` | `double` | ルーペの DIP → 物理ピクセル倍率（`FadeMaskDragRectControl.LoupeDipToDeviceScaling` へ委譲。DPI から自動取得） |
| `ZoomPercent` | `double` | 現在のズーム率（%）。0 = Fit（自動）。スライダ等の連続値も設定できる。 |
| `CanZoomIn` | `bool` | 現在のズーム状態でズームインが可能か。 |
| `CanZoomOut` | `bool` | 現在のズーム状態でズームアウトが可能か。 |
| `CenterAlign` | `bool` | 画像の中央寄せ / 左上寄せ（その軸で画像がビューポートより小さい場合に有効）。 |
| `ViewportBackground` | `IBrush?` | ズーム・パンビューポートの背景（市松模様等。レターボックス領域の描画）。 |
| `HasSelection` | `bool` | 現在の選択状態（選択矩形の有無）。 |
| `SelectionRect` | `Rect` | 現在の選択矩形（ドメイン座標）。未選択時は 0x0。 |
| `SizeLabelOverrideProvider` | `Func<string?>?` | サイズラベルを描画時に評価するプロバイダ（FadeMaskDragRectControl へ委譲）。 |
| `OperationTextProvider` / `InfoTextProvider` | `Func<string?>?` | 操作ラベル（2 行目）・情報ラベル（3 行目）の文言を描画時に評価するプロバイダ（FadeMaskDragRectControl へ委譲。文言はホストが与える）。 |

#### 2.1.2 メソッド

| メソッド | 説明 |
| --- | --- |
| `void ZoomIn()` | ズームイン（段階遷移。遷移の規則はコア（`EsUtil.SaveEvidence.ZoomView`）が単一所有する）。 |
| `void ZoomOut()` | ズームアウト（段階遷移。同上）。 |
| `void ZoomFit()` | Fit 表示（ウィンドウに合わせて表示）に戻す（`ZoomIn` / `ZoomOut` と対になるズーム操作）。 |
| `void SetSelection(Rect rect)` | 選択状態を外部から設定（FadeMaskDragRectControl へ委譲）。 |
| `void ClearSelection()` | 選択状態をクリア（FadeMaskDragRectControl へ委譲）。 |
| `void SetSelectionOverlayVisible(bool visible)` | 領域選択オーバーレイの可視性とヒットテストを設定（Crop セッションの開始・終了時に制御）。`false` で選択もクリア。 |
| `void SetCoordinateTransform(ICoordTransform? port)` | 座標変換ポートを設定（内包コントロールへ分配）。 |
| `void RequestConfirm()` | 選択確定要求（FadeMaskDragRectControl へ委譲）。 |
| `void RequestCancel()` | 選択キャンセル要求（FadeMaskDragRectControl へ委譲）。 |
| `void FocusMarquee()` | 内包 Marquee へフォーカスを設定（矢印キー nudge の受付開始）。 |
| `void SetLoupeSource(IImage? source)` | ルーペの拡大元画像を設定（null でルーペをクリア）。 |
| `void ClearLoupe()` | ルーペのソースをクリア（FadeMaskDragRectControl へ委譲）。 |
| `void NotifyViewportChanged()` | ビューポート（パン・ズーム・表示サイズ）の変化を内包オーバーレイへ通知。 |
| `Rect ImageRectViewport()` | 画像の表示矩形（ビューポート座標）を取得。 |
| `Point ContentToViewport(Point contentPos)` | ドメイン座標 → ビューポート座標へ変換。 |
| `Point ViewportToContent(Point viewportPos)` | ビューポート座標 → ドメイン座標へ変換。 |

#### 2.1.3 イベント

| イベント | 型 | 説明 |
| --- | --- | --- |
| `ConfirmRequested` | `EventHandler<FadeMaskDragRectSelectionEventArgs>` | 選択確定要求（FadeMaskDragRectControl へ委譲）。 |
| `CancelRequested` | `EventHandler?` | 選択キャンセル要求（FadeMaskDragRectControl へ委譲）。 |
| `ZoomChanged` | `EventHandler<ZoomChangedEventArgs>` | ズーム率変更の通知（ZoomViewControl へ委譲）。パン・nudge の座標表示は継承元の `PointerMoved` を使う。 |
| `ViewportChanged` | `EventHandler?` | ビューポート（パン・ズーム・表示状態）変化の通知（ZoomViewControl へ委譲）。 |

### 2.2. OverlayRect 構造体

外部提供矩形の描画情報（不変レコード構造体）。UI 非依存の `CropViewControl.Core`（名前空間 `EsUtil.SaveEvidence.CropView`）が提供する。

| メンバー | 型 | 説明 |
| --- | --- | --- |
| `Bounds` | `EsRect`（`EsUtil.Platform.Core.Geometry`） | 矩形（ドメイン座標 = 元画像ピクセル座標）。 |
| `Style` | `OverlayLineStyle` | 枠線スタイル。 |
| `Foreground` | `System.Drawing.Color` | 前景色（透明度を含む）。 |
| `Background` | `System.Drawing.Color` | 背景色（透明度を含む）。破線の市松風 2 色目。 |
| `Thickness` | `double` | 枠線の太さ（ピクセル）。 |

コンストラクタ: `OverlayRect(EsRect Bounds, OverlayLineStyle Style, System.Drawing.Color Foreground, System.Drawing.Color Background, double Thickness)`

色はコアの色型（`System.Drawing.Color`）です。Avalonia の色を使う場合は、境界変換（`Platform.AvaloniaUI` の `ColorAvaloniaInterop`）で変換する。

### 2.4. OverlayRectLayout / OverlayRectFrame（描画入力の解決）

矩形の描画に必要な値（負サイズのクランプ・太さの下限・破線かどうか・背景色を使うか・ダッシュ長）を解決するコア型です。UI 層は `OverlayRectLayout.Resolve` の結果を描画先の型へ写すだけで、式と判断を持ちません（詳細は `../CropViewControl.Core/SPEC_ExtFunc.md`）。

```csharp
new OverlayRect(new EsRect(10, 10, 200, 150), OverlayLineStyle.Dashed, Colors.Orange.ToDrawingColor(), Colors.White.ToDrawingColor(), 2.0)
```

### 2.3. OverlayLineStyle 列挙型

枠線スタイル（UI 非依存の `CropViewControl.Core` 提供）。ダッシュ長はコアが単一所有し、ビューポート（図形要素）とルーペ画像（ピクセル）の両経路で共有する。

| メンバー | 説明 |
| --- | --- |
| `Solid` | 実線。 |
| `Dashed` | 破線（長いダッシュ。ダッシュ長 8 / 間隔 4）。 |
| `Dotted` | 点線（短いダッシュ。ダッシュ長 2 / 間隔 2）。 |

## 3. 入力・出力

### 3.1. 画像

- `Source`（`IImage?`）で表示画像を設定します。
- 画像変更時は Fit 表示にリセットされます。
- 領域選択オーバーレイの可視性とヒットテストは `SetSelectionOverlayVisible` で制御します（Crop セッションの開始・終了時にホストが切替）。`Source` 設定では制御しません。
  - 初期状態（`Source` 未設定）はオーバーレイを不可視（`IsVisible = false`）+ ヒットテスト不可（`IsHitTestVisible = false`）にします。
    - 画像なしでの選択矩形作成と、Crop モードの全面暗転で市松模様が暗くならないようにします。
  - `Source` が `null` に変わるときは**選択のみクリア**します（オーバーレイの表示・ヒットテスト状態は維持。`SetSelectionOverlayVisible` が専任）。
  - プレビュー表示時（キャプチャ完了・画像読込）は、`Source` 設定だけではオーバーレイを自動表示しません。
    - これにより、キャプチャ完了直後に Crop モード（全面暗転）が残るのを防ぎます。
  - Crop セッション中のみオーバーレイが可視化され、暗転マスクが表示されます。

### 3.2. 外部提供矩形

- `Rects`（`IReadOnlyList<OverlayRect>`）で外部提供矩形を設定します。
- 設定すると View（ビューポート）とルーペ画像の両方へ描画されます。
- 矩形・色は UI 非依存の型（`EsRect` / `System.Drawing.Color`）で設定し、描画時に UI 側の型へ変換されます。

## 4. 座標系

- **ドメイン座標**: 元画像ピクセル（ズーム率に依存しない不変座標）。
- **ビューポート座標**: コントロールローカル DIP 座標（表示座標）。
- `ContentToViewport` / `ViewportToContent` で双方向変換します（Fit 中央寄せの原点補正を含む）。

## 5. イベント

- `ConfirmRequested` / `CancelRequested`: 領域選択の操作をホストへ通知します。
- `ZoomChanged` / `ViewportChanged`: ズームビューポートの状態変化をホストへ通知します。

## 6. スレッド安全性

- 本コントロールは Avalonia の UI スレッドで操作することを前提とします。
- 内部のコントロール（ZoomViewControl / FadeMaskDragRect / ルーペ）も同様に UI スレッドで操作します。
