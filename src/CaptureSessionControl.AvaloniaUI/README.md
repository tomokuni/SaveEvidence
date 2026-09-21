# CaptureSessionControl.AvaloniaUI

## 目次

- [CaptureSessionControl.AvaloniaUI](#capturesessioncontrolavaloniaui)
  - [目次](#目次)
  - [1. 概要](#1-概要)
  - [2. 機能一覧](#2-機能一覧)
  - [3. システム要件](#3-システム要件)
  - [4. インストール](#4-インストール)
  - [5. クイックスタート](#5-クイックスタート)
    - [キャプチャ（ScreenSelect / WindowSelect / AreaSelect）](#キャプチャscreenselect--windowselect--areaselect)
    - [切出し（Crop）](#切出しcrop)
    - [設定の適用](#設定の適用)
    - [キャンセル / 確定](#キャンセル--確定)
  - [6. 動作フロー](#6-動作フロー)
    - [StartCaptureAsync](#startcaptureasync)
    - [StartCropAsync](#startcropasync)
  - [7. 依存プロジェクト](#7-依存プロジェクト)
  - [8. ドキュメント](#8-ドキュメント)
  - [9. プロジェクト構成](#9-プロジェクト構成)
  - [10. ライセンス](#10-ライセンス)

---

## 1. 概要

画面キャプチャ（全画面 / ウィンドウ選択 / 範囲選択）と切出し（Crop）を提供する Avalonia UI ライブラリです。**Facade パターン**の単一エントリポイント `CaptureSessionControl` から利用します。

## 2. 機能一覧

- **3 種のキャプチャモード**: `ScreenSelect`（全画面）/ `WindowSelect`（ウィンドウ選択）/ `AreaSelect`（範囲選択）
- **切出し（Crop）**: プレビュー画像上の選択矩形で元画像から切り出し範囲を取得
- **Facade パターン**: 状態は複合コントロール `FadeMaskDragRectControl`（DragRect + FadeMask + Loupe を内包）と Platform.AvaloniaUI / Platform.Native が保持し、`CaptureSessionControl` は「組み立て・ライフサイクル」のみを担う薄い統括者
- **Task ベース API**: 結果は `Task` で返却（`await` で直感的に利用）
- **キャプチャ完了イベント**: `CaptureCompleted`（同期型）で画像変更 → オーナー再表示の順序を保証
- **イベント駆動**: 選択状態の同期は各コントロールの公開イベントのみ（ポーリングなし）
- **再入禁止ガード**: 実行中の再呼び出しは `null` を返す（既存セッションを継続）
- **部分更新設定**: `CaptureSettings` は全メンバー nullable（null = デフォルト値 or 現在値維持）で部分更新が可能
- **プラットフォーム抽象**: キャプチャ・要素検出・ウィンドウ操作は Platform.Native 経由で注入（テスト可能・プラットフォーム非依存）

## 3. システム要件

- .NET 10（`net10.0-windows10.0.17763.0`）
- Avalonia 12.1 以降
- C# 14

## 4. インストール

現在は NuGet パッケージではなく、プロジェクト参照で利用します。

```xml
<ProjectReference Include="..\CaptureSessionControl.AvaloniaUI\src\CaptureSessionControl.AvaloniaUI.csproj" />
```

必要な名前空間:

```csharp
using Estable.AvaloniaUI.CaptureSession;
```

## 5. クイックスタート

### キャプチャ（ScreenSelect / WindowSelect / AreaSelect）

```csharp
var capture = new CaptureSessionControl(); // 既定設定

// 全画面キャプチャ
var result = await capture.StartCaptureAsync(CaptureType.ScreenSelect, window);
if (result?.Image is { } image)
{
    // image: キャプチャ画像 / result.SourceBounds: キャプチャ領域（仮想画面座標）
}
```

### 切出し（Crop）

```csharp
// プレビューを表示しているコントロールと元画像から切り出し範囲を取得する
// 切り出し中のホイール操作（Ctrl+ホイールのズーム・通常ホイールのパン）は CropViewControl が内部で完結する（上位の配線は不要）
var cropRect = await capture.StartCropAsync(previewControl, hostWindow, sourceImage,
    zoomIn => { if (zoomIn) zoomInAction(); else zoomOutAction(); });
if (cropRect is { } rect && rect.Width > 0 && rect.Height > 0)
{
    // rect: 元画像上の切り出し範囲（論理座標。始点・終点のピクセルを含む閉区間）
}
```

### 設定の適用

```csharp
var capture = new CaptureSessionControl(new CaptureSettings
{
    LoupeVisible = true,
    LoupeZoomLevel = 4,
    MinSelectionSize = 6,
    BorderColor = System.Drawing.Color.Red,
});

// 部分更新（null 以外のみ反映・残りは現在値維持）
capture.AppearanceSettings = capture.AppearanceSettings with { LoupeOffset = 24.0 };
```

### キャンセル / 確定

```csharp
capture.Cancel();   // 進行中セッションをキャンセル（0x0 の空結果で完了）
capture.Confirm();  // Crop のみ有効（Enter キー相当）
capture.Dispose();  // リソース解放（以降の操作は ObjectDisposedException）
```

## 6. 動作フロー

### StartCaptureAsync

1. オーナーウィンドウのスクリーン情報から DPI スケーリング係数を算出
2. `SelectionCaptureSession` を生成（Platform.AvaloniaUI からサービスを注入）
3. オーナーウィンドウをキャプチャ除外に設定（`SetCaptureExclusion`）→ 非表示（`HideOwnerWindow`）
4. **仮想スクリーン全体をバックグラウンド + タイムアウト付きでキャプチャ（全モード共通の切り出し元）**
5. `OverlayWindow` を表示（全画面透明・Topmost・Alt+Tab 非表示・ウォッチドッグ）
   - AreaSelect: DragRect ドラッグ選択 + Loupe
   - Screen / WindowSelect: カーソル位置の要素検出 → ハイライト
6. **枠選択中に事前切り出し**（ホバー時・ドラッグ後。同じ矩形ならスキップ・キャッシュ再利用）
7. **確定時: 事前切り出しキャッシュを再利用**（不一致時のみ切り出し）
8. 結果を `CaptureResult` で返却 → `CaptureCompleted`（画像変更） → オーナー再表示 → 除外解除

### StartCropAsync

1. `CropSession` を生成 → `RunAsync`
2. **FadeMaskDragRectControl**（DragRect + FadeMask + Loupe の配線を内包する複合コントロール）を祖先 Panel に配置
3. 座標変換: `CoordTransform`（`CropViewControl` の `ViewportToContent` / `ContentToViewport` に委譲）で論理（元画像 px）↔ ビューポート DIP を変換し、**`CropViewControl.SetCoordinateTransform` で内包コントロール（DragRect / FadeMask / Loupe）に一括注入**（描画・表示時に動的評価 → ウィンドウサイズ変更・ズーム・スクロールに追従）
4. マウスホイール操作（Crop 中）: CropViewControl がオーバーレイ上のホイールをビューポート（ズーム・パン）へ転送する
5. **ビューポート変化の通知**: ホストがプレビューのパン・ズームを検知したら `NotifyCropViewportChanged()` を呼ぶ（暗転マスク・選択枠・ルーペが追従。ScrollViewer 連携のスクロールも同じ経路で処理）
6. 確定 → 論理 Rect（**閉区間** = 始点・終点を含む）を `PixelRect` で返却 / キャンセル → 0x0 の空 `PixelRect`

## 7. 依存プロジェクト

| プロジェクト | 利用機能 |
| --- | --- |
| `Platform.AvaloniaUI` | `Platform.AvaloniaUI`（サービス生成）/ `ICaptureService` / `IWindowService` / `IElementFinder` は `Platform.Native`、`ICoordTransform` / `CoordTransform` は `Platform.Core`、`ScreenConverter` は `Platform.AvaloniaUI` |
| `FadeMaskDragRectControl` | `FadeMaskDragRectControl`（DragRect + FadeMask + Loupe を内包する複合コントロール。公開 API は本コントロールのみ） |

CaptureSessionControl.AvaloniaUI は内包コントロール（`DragRectControl` / `FadeMaskControl` / `LoupeControl`）を直接参照しない（窓口は `FadeMaskDragRectControl` のみ。公開型のカプセル化）。

## 8. ドキュメント

- [外部機能仕様書](SPEC_ExtFunc.md)
- [システム仕様書](SPEC_System.md)

## 9. プロジェクト構成

```text
CaptureSessionControl.AvaloniaUI/
├── src/
│   ├── CaptureSessionControl.AvaloniaUI.csproj      # ライブラリプロジェクト（Avalonia.Desktop / Logging.Abstractions）
│   ├── CaptureSessionControl.cs            # 公開 Facade（統括者。組み立て・配線・ライフサイクル）
│   └── Sessions/                 # 内部実装（SelectionCaptureSession / CropSession / OverlayWindow）
├── sample/                       # サンプルアプリ（sample/README.md 参照）
└── tests/                        # 単体テスト（79 件）
```

`CaptureType`（キャプチャ種別）は UI 非依存の共通基盤 `Platform.Core`（名前空間 `EsUtil.Platform.Core`）が提供します。利用側は `using EsUtil.Platform.Core;` を追加してください。

`CaptureSettings`（設定）と `CaptureResult`（結果）は UI 非依存の共通基盤 `CaptureSessionControl.Core`（名前空間 `Estable.CaptureSession`）が提供します（切り出し矩形の計算 `CropBoundsMath` も同プロジェクト）。利用側は `using Estable.CaptureSession;` を追加してください。**色は `System.Drawing.Color`** で指定します（従来の Avalonia の色型から変更）。

## 10. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
