# ZoomViewControl システム仕様書（SPEC_System）

## 目次

- [ZoomViewControl システム仕様書（SPEC\_System）](#zoomviewcontrol-システム仕様書spec_system)
  - [目次](#目次)
  - [1. システム概要](#1-システム概要)
  - [2. 技術スタック](#2-技術スタック)
  - [3. アーキテクチャ](#3-アーキテクチャ)
    - [3.1 レイヤー構造](#31-レイヤー構造)
    - [3.2 依存の方向](#32-依存の方向)
  - [4. 例外処理ポリシー](#4-例外処理ポリシー)
    - [4.1 基本方針](#41-基本方針)
    - [4.2 本プロジェクト固有の例外処理](#42-本プロジェクト固有の例外処理)
  - [5. コンポーネント図](#5-コンポーネント図)
  - [6. コンポーネント一覧](#6-コンポーネント一覧)
    - [6.1 ZoomViewControl](#61-zoomviewcontrol)
    - [6.2 ZoomableView](#62-zoomableview)
    - [6.3 ZoomMath / ZoomTransition / ScrollMetrics（純粋関数）](#63-zoommath--zoomtransition--scrollmetrics純粋関数)
  - [7. 依存ルール](#7-依存ルール)
  - [8. ログ](#8-ログ)

---

## 1. システム概要

画像プレビューのズーム・パン・Fit 表示を提供する独立ライブラリです。ScrollViewer を使用しない自己完結型ビューポート（`ZoomableView`）を内包する、ズーム率の遷移・適用を統括する複合コントロール（`ZoomViewControl`）、UI 非依存の純粋関数（`ZoomMath` / `ZoomTransition` / `ScrollMetrics`）で構成されます。

上位 AP（app 等）は `ZoomViewControl` の `Source` を設定し、`ZoomChanged` を購読するだけで、ズーム計算・表示・遷移の詳細は本ライブラリが担います。

## 2. 技術スタック

| 項目 | 内容 |
| --- | --- |
| **.NET** | .NET 10.0 |
| **言語** | C# 14 |
| **UI** | Avalonia 12.1（`Avalonia`） |
| **ログ** | Microsoft.Extensions.Logging.Abstractions（LoggerMessage ソース生成） |
| **テスト** | xUnit v3 / Avalonia.Headless |

## 3. アーキテクチャ

### 3.1 レイヤー構造

```text
上位 AP（app 等）: ZoomViewControl.Source 設定 → ZoomViewControl.ZoomChanged 購読
  ↓
ZoomViewControl（本ライブラリ）
  ├── ZoomViewControl（複合コントロール: 遷移決定・適用・内部配線・ZoomChanged による通知）
  ├── ZoomableView（コントロール: パン・ズーム表示）
  ├── ZoomMath / ZoomTransition / ScrollMetrics（純粋関数: 計算）
  └── Logging/LogMessage（LoggerMessage 定義）
```

### 3.2 依存の方向

- **上位 AP → ZoomViewControl の一方向**（app は本ライブラリへ参照）。本ライブラリは上位 AP に依存しない。
- `ZoomViewControl` が `ZoomableView` を内包し、ズーム率・遷移・表示を統括する（単一所有）。
- `ZoomViewControl` が `ZoomRequested` / `SizeChanged` を内部購読し、ホスト側の配線重複を排除する（DRY）。
- 純粋関数（`ZoomMath` / `ZoomTransition` / `ScrollMetrics`）は UI 非依存（Avalonia.Media / Avalonia 基本型のみ）。

## 4. 例外処理ポリシー

### 4.1 基本方針

- ライブラリは例外を原則伝播し、上位 AP（app 等）で処理する。
- ログは LoggerMessage ソース生成（`Logging/LogMessage.cs`）で出力する。ズーム操作の前後 % を Information で記録する。
- `ZoomViewControl.Log` へ注入されたロガーへ出力する（未設定時は `NullLogger` で無出力）。

### 4.2 本プロジェクト固有の例外処理

- コントロール（`ZoomableView`）のイベントハンドラ内の例外は握りつぶさず、上位 AP へ伝播する（コントロールの内部状態を不正にしない）。
- ズーム率（10〜500%）・オフセット（スクロール範囲）はクランプにより不正な値を防止する。`SetSource(null)` 等のリセット系 API は画像なし時も表示状態（スクロールバー非表示・再描画・通知）を一貫して更新する。
- 純粋関数（`ZoomMath` / `ZoomTransition` / `ScrollMetrics`）は入力の範囲チェック・クランプを内部で行い、例外を発生させない。

## 5. コンポーネント図

```mermaid
flowchart TB
    subgraph ZVC["ZoomViewControl プロジェクト"]
        ZVW["ZoomViewControl<br/>public sealed : Panel<br/>ZoomViewControl.cs（遷移決定・適用・内部配線・ZoomChanged 通知）"]
        ZV["ZoomableView<br/>public sealed : Panel<br/>ZoomableView.cs（パン・ズームビューポート）"]
        PF["ZoomMath / ZoomTransition / ScrollMetrics<br/>public static（純粋関数）"]
        EV["ZoomRequestedEventArgs<br/>ZoomableView.cs"]
        LG["LogMessage<br/>Logging/LogMessage.cs（LoggerMessage）"]
    end
    subgraph Host["上位AP（消費側）"]
        AP["app 等<br/>ZoomViewControl.Source 設定 → ZoomViewControl.ZoomChanged 購読"]
    end
    subgraph Dep["外部依存"]
        AV["Avalonia 12.1<br/>Panel / Image / RenderTransform"]
        LGAB["Microsoft.Extensions.Logging.Abstractions"]
    end

    AP -->|Source 設定 / ZoomChanged 購読| ZVW
    ZVW --> ZV
    ZVW --> PF
    ZV --> PF
    ZVW --> LG
    ZV --> AV
    LG --> LGAB
```

## 6. コンポーネント一覧

### 6.1 ZoomViewControl

- **分類**: 公開コントロール（`public sealed class : Panel`。自己完結型複合コントロール）
- **責務**: `ZoomableView` を内包し、ズーム率の遷移（⊖⊕ 23 段階 / Ctrl+ホイール ×1.1）・適用・Fit 表示を統括。ズーム要求（`ZoomRequested`）・リサイズ（`SizeChanged`）の配線を自己完結。ズーム率変更・可否は `ZoomChanged` で通知、可否は `CanZoomIn` / `CanZoomOut` で公開
- **公開メンバー**: `Source` / `ZoomPercent` / `CenterAlign` / `ZoomDisplayText` / `ZoomIn` / `ZoomOut` / `ZoomInByWheel` / `ZoomOutByWheel` / `ApplyZoomFromSlider` / `ApplyZoomFromText` / `CanZoomIn` / `CanZoomOut` / `ZoomableView` / `PointerPosition` / `Log` / `ZoomChanged`（イベント）
- **依存**: ZoomableView / ZoomMath / ZoomTransition / LogMessage / ZoomAvailability（値オブジェクト。2 値の可否を単一保持）
- **ズーム率の単一所有**: ズーム率は内包する `ZoomableView` が所有し、本コントロールは `ZoomPercent`（読み書き）で操作窓口を提供する

### 6.2 ZoomableView

- **分類**: 公開コントロール（`public sealed class : Panel`）
- **責務**: ScrollViewer 不使用のパン・ズームビューポート。RenderTransform（ScaleTransform + TranslateTransform）で表示。マウスドラッグパンは grab 方式（掴んだ位置がポインタに追従）
- **公開メンバー**: `SetSource` / `SetZoom` / `SetOffset` / `PanBy` / `ViewportToContent` / `ContentToViewport` / `ViewportToContentImage` / `ContentToViewportImage` / `DisplaySize` / `ImageRectViewport` / `ImageControl` / `ZoomPercent` / `Offset` / `Scale` / `EffectiveScale` / `CenterAlign` / `ZoomRequested` / `ViewportChanged`
- **依存**: ZoomMath / ScrollMetrics / ZoomRequestedEventArgs

### 6.3 ZoomMath / ZoomTransition / ScrollMetrics（純粋関数）

| コンポーネント | 分類 | 責務 |
| --- | --- | --- |
| `ZoomMath` | 公開静的クラス | ズーム中心維持・Fit 実効倍率・座標変換（オフセット基準 / 画像表示矩形原点基準）・オフセットクランプの計算 |
| `ZoomTransition` | 公開静的クラス | ⊖⊕ 23 段階遷移・Ctrl+ホイール ×1.1 遷移・可否判定 |
| `ScrollMetrics` | 公開静的クラス | スクロールバーの範囲・ビューポートサイズの計算 |

- UI 非依存のため、ヘッドレス不要の `[Fact]` で並列テスト可能（単体テストで仕様を検証）。

## 7. 依存ルール

- 依存は**一方向**（上位 AP → 本ライブラリ）。本ライブラリは上位 AP の型（ViewModel 等）に依存しない。
- `ZoomViewControl` が `ZoomableView` を内包し、`ZoomRequested` / `SizeChanged` / `ViewportChanged` を内部配線する（責務の集約・DRY）。
- 純粋関数は UI コントロールから分離され、テスト容易性を確保する。

## 8. ログ

- ログ定義は `Logging/LogMessage.cs`（LoggerMessage ソース生成・`this ILogger` 拡張メソッド）に集約。
- `ZoomViewControl.Log` プロパティへ注入されたロガーへ出力する。未設定（null）の場合は `NullLogger` で無出力。
- 出力先・ログレベルはホスト側（上位 AP）のロギング基盤（NLog 等）が決定する（本ライブラリはログ転送のみ）。
