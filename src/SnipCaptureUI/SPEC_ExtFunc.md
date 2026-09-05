# SnipCaptureUI 外部機能仕様書

## 目次

- [SnipCaptureUI 外部機能仕様書](#snipcaptureui-外部機能仕様書)
  - [目次](#目次)
  - [1. 文書概要](#1-文書概要)
  - [2. 目的](#2-目的)
  - [3. 外部利用条件](#3-外部利用条件)
    - [3.1 プロジェクト参照](#31-プロジェクト参照)
    - [3.2 必要な名前空間](#32-必要な名前空間)
    - [3.3 スレッド要件](#33-スレッド要件)
  - [4. 基本的な使用方法](#4-基本的な使用方法)
    - [4.1 キャプチャ（ScreenSelect / WindowSelect / AreaSelect）](#41-キャプチャscreenselect--windowselect--areaselect)
    - [4.2 切出し（Crop）](#42-切出しcrop)
    - [4.3 設定の適用](#43-設定の適用)
    - [4.4 キャンセル / 確定 / 破棄](#44-キャンセル--確定--破棄)
  - [5. 公開API](#5-公開api)
    - [5.1 プロパティ](#51-プロパティ)
      - [5.1.1 SnipCapture.AppearanceSettings](#511-snipcaptureappearancesettings)
      - [5.1.2 CaptureResult のプロパティ](#512-captureresult-のプロパティ)
    - [5.2 メソッド](#52-メソッド)
      - [5.2.1 SnipCapture](#521-snipcapture)
      - [5.2.2 CaptureSettings](#522-capturesettings)
    - [5.3 イベント](#53-イベント)
      - [5.3.1 SnipCapture.CaptureCompleted](#531-snipcapturecapturecompleted)
      - [5.3.2 キャプチャ完了イベント（画像変更 → APP 表示の順序保証）](#532-キャプチャ完了イベント画像変更--app-表示の順序保証)
    - [5.4 型](#54-型)
      - [5.4.1 SnipCapture](#541-snipcapture)
      - [5.4.2 CaptureType](#542-capturetype)
      - [5.4.3 CaptureSettings](#543-capturesettings)
      - [5.4.4 CaptureResult](#544-captureresult)
  - [6. 閉区間キャプチャ（重要な仕様）](#6-閉区間キャプチャ重要な仕様)
  - [7. 動作フロー](#7-動作フロー)
    - [7.1 StartCaptureAsync](#71-startcaptureasync)
    - [7.2 StartCropAsync](#72-startcropasync)
  - [8. ログ](#8-ログ)
    - [8.1 LogLevel 設定ポリシー](#81-loglevel-設定ポリシー)
    - [8.2 ログ定義一覧](#82-ログ定義一覧)
  - [9. 制約事項](#9-制約事項)
  - [10. 最適化施策](#10-最適化施策)

---

## 1. 文書概要

| 項目 | 内容 |
| --- | --- |
| 対象 | `SnipCaptureUI` |
| プロジェクト | `SnipCaptureUI/SnipCaptureUI.csproj` |
| 名前空間 | `Estable.AvaloniaUI.SnipCaptureUI` |
| 対象フレームワーク | `net10.0-windows10.0.17763.0` |
| UIフレームワーク | Avalonia 12.1 |
| 主要設計パターン | Facade（SnipCapture）/ セッション（CaptureSession / CropSession）/ 薄い統括者 |
| 主な用途 | 画面キャプチャ（全画面 / ウィンドウ選択 / 範囲選択）と切出し（Crop）の統括 API の提供 |

## 2. 目的

`SnipCaptureUI` は、外部アプリケーションが画面キャプチャと切出しを**単一の API**（`SnipCapture`）で利用できるようにする Facade パターンのライブラリです。

状態は複合コントロール `FadeMaskDragRectControl`（DragRect + FadeMask + Loupe を内包）と PlatformFactory が保持し、
`SnipCapture` は「組み立て・ライフサイクル」のみを担う薄い統括者です。  
SnipCaptureUI は内包コントロール（DragRectControl / FadeMaskControl / LoupeControl）を直接参照しない（公開型のカプセル化）。

次の機能を提供します。

- 全画面キャプチャ（`ScreenSelect`）
- ウィンドウ選択キャプチャ（`WindowSelect`）
- 範囲選択キャプチャ（`AreaSelect`。ルーペ付き）
- 切出し（`Crop`。プレビュー画像上の選択矩形で元画像から切り出し範囲を取得）
- 設定の部分更新（`CaptureSettings`。全メンバー nullable）
- キャンセル / 確定（`Cancel()` / `Confirm()`）

## 3. 外部利用条件

### 3.1 プロジェクト参照

```xml
<ProjectReference Include="..\SnipCaptureUI\src\SnipCaptureUI.csproj" />
```

### 3.2 必要な名前空間

```csharp
using Estable.AvaloniaUI.SnipCaptureUI;
```

### 3.3 スレッド要件

- `StartCaptureAsync` / `StartCropAsync` は **UI スレッドから呼び出す**こと（Avalonia コントロールの生成・配置を行うため）。

## 4. 基本的な使用方法

### 4.1 キャプチャ（ScreenSelect / WindowSelect / AreaSelect）

```csharp
var capture = new SnipCapture();
var result = await capture.StartCaptureAsync(CaptureType.AreaSelect, ownerWindow);

if (result?.Image is { } image && result.SourceBounds is { } bounds)
{
    // image: キャプチャ画像 / bounds: キャプチャ領域（仮想画面座標）
}
```

### 4.2 切出し（Crop）

```csharp
var cropRect = await capture.StartCropAsync(previewControl, hostWindow, sourceImage);
if (cropRect is { } rect && rect.Width > 0 && rect.Height > 0)
{
    // rect: 元画像上の切り出し範囲（論理座標 = 元画像ピクセル）
}
```

### 4.3 設定の適用

```csharp
var capture = new SnipCapture(new CaptureSettings
{
    LoupeVisible = true,
    LoupeZoomLevel = 4,
    BorderColor = Colors.Red,
});

// 部分更新（null 以外のみ反映・残りは現在値維持）
capture.AppearanceSettings = capture.AppearanceSettings with { LoupeOffset = 24.0 };
```

### 4.4 キャンセル / 確定 / 破棄

```csharp
capture.Cancel();   // 進行中セッションをキャンセル（0x0 の空結果で完了）
capture.Confirm();  // Crop のみ有効（Enter キー相当）
capture.Dispose();  // 以降の公開操作は ObjectDisposedException
```

## 5. 公開API

公開 API は「プロパティ / メソッド / イベント / 型」の 4 分類で構成されます。
座標を扱う API は、ドメイン座標系 / DIP 座標系 / 画面物理 Pixel のどれであるかを明記します。

### 5.1 プロパティ

#### 5.1.1 SnipCapture.AppearanceSettings

| プロパティ | 型 | 説明 |
| --- | --- | --- |
| `AppearanceSettings` | `CaptureSettings` | 現在の外観・動作設定。getter は全メンバー non-null / setter は部分更新 + 検証 |

#### 5.1.2 CaptureResult のプロパティ

| プロパティ | 型 | 説明 |
| --- | --- | --- |
| `Image` | `UnifiedBitmap?` | キャプチャした画像（エラー時 null。破棄は呼び出し側） |
| `SourceBounds` | `PixelRect?` | キャプチャした領域（**画面物理 Pixel** の仮想画面座標。エラー時 null） |

`Image` と `SourceBounds` は常に同時に null または同時に非 null です。

- エラー時: 両方 null
- キャンセル時: 両方 0x0（`null` と `PixelRect(0, 0, 0, 0)`）
- 成功時: 両方 non-null

`SourceBounds` は仮想画面座標（複数モニターを包含する座標空間）のため、プライマリモニターが (0,0) にない場合 X / Y は負値になり得ます。

### 5.2 メソッド

#### 5.2.1 SnipCapture

| メソッド | シグネチャ | 説明 |
| --- | --- | --- |
| コンストラクタ | `SnipCapture(CaptureSettings? initialSettings = null, ILogger? logger = null)` | インスタンスを生成。設定値が許容範囲外なら `ArgumentOutOfRangeException` |
| `StartCaptureAsync` | `Task<CaptureResult?> StartCaptureAsync(CaptureType type, Window owner)` | 選択キャプチャを開始。実行中は null / キャンセル時は 0x0 の空結果 |
| `StartCropAsync` | `Task<PixelRect?> StartCropAsync(Control previewControl, Window hostWindow, UnifiedBitmap sourceImage, Action<bool>? zoomRequestHandler = null)` | 切出しを開始。戻り値は元画像上の論理座標（**ドメイン座標系** = 元画像ピクセル）。実行中は null / キャンセル時は 0x0 の空 `PixelRect`。`zoomRequestHandler` は Ctrl+ホイールによるズーム要求（true = ズームイン / false = ズームアウト）を上位 AP へ通知する（null の場合はズーム操作なし） |
| `Cancel` | `void Cancel()` | 進行中セッションをキャンセル（Capture: オーバーレイを閉じる / Crop: 0x0 で完了） |
| `Confirm` | `void Confirm()` | Crop を現在の選択矩形で確定（Enter キー相当。FadeMaskDragRectControl.RequestConfirm に委譲） |
| `NotifyCropViewportChanged` | `void NotifyCropViewportChanged()` | 実行中の Crop セッションへビューポート（パン・ズーム・表示サイズ）の変化を通知。ビューポート実装に依存せず、ホストがビューポート変化を検知した際に中継する（暗転マスク・選択枠・ルーペの追従）。Crop 未実行時は何もしない |
| `HasSelection` | `bool` | 実行中の Crop セッションに選択矩形があるかどうか（Crop 未実行中は false）。ホスト（上位 AP）が「確定 / キャンセル」の操作分岐に使用 |
| `AppearanceSettings` | `CaptureSettings` | 現在の外観・動作設定。設定時は null メンバーのみ現在値を維持する部分更新。**同一値の再設定はスキップ**。実行中の Crop セッションへは即時反映（`CropSession.ApplySettings`） |
| `Dispose` | `void Dispose()` | リソースを解放 |

#### 5.2.2 CaptureSettings

| メソッド | 説明 |
| --- | --- |
| `void Validate()` | 設定値を検証（null は対象外）。範囲外なら `ArgumentOutOfRangeException` |
| `CaptureSettings ToNonNull()` | null をデフォルト値で埋めた non-null 版を返す（例: MinSelectionSize=10 / LoupeSize=160 / Zoom=4 / Timeout=60 秒） |

### 5.3 イベント

#### 5.3.1 SnipCapture.CaptureCompleted

| イベント | シグネチャ | 説明 |
| --- | --- | --- |
| `CaptureCompleted` | `event Action<CaptureResult?>? CaptureCompleted` | キャプチャ完了（同期実行。UI スレッドで発行） |

#### 5.3.2 キャプチャ完了イベント（画像変更 → APP 表示の順序保証）

`CaptureCompleted` は**同期型イベント**です。C# のイベントは同期実行のため、ハンドラ（ホストの画像変更）の完了後に、キャプチャセッションの後続処理（オーナー再表示）が実行されます。

フロー:

```text
APP 非表示（HideOwnerWindow=true で自動）
  → キャプチャ実行
  → CaptureCompleted イベント発行（同期）→ ホストが画像変更
  → オーナー（APP）再表示
```

```csharp
_snipCapture.CaptureCompleted += result =>
{
    // 画像変更（同期実行。このハンドラの完了後に APP が再表示される）
    if (result?.Image is not null) { UpdatePreview(result.Image); }
};

var result = await _snipCapture.StartCaptureAsync(CaptureType.AreaSelect, window);
```

- イベントは UI スレッドで発行されるため、ハンドラ内で UI 更新をそのまま実行できる。
- ハンドラを非同期（`async void`）にすると順序保証が失われるため、**同期ハンドラ**にすること（画像変更は同期で行う）。

### 5.4 型

公開型は `SnipCapture` / `CaptureType` / `CaptureSettings` / `CaptureResult` です。

#### 5.4.1 SnipCapture

公開 Facade（`public sealed class : IDisposable`）。`IDisposable` を実装し、Dispose 後は全ての公開操作が `ObjectDisposedException` を投げます。実行中（`StartCaptureAsync` / `StartCropAsync`）に同一インスタンスへ再呼び出しされた場合は `null` を返します（既存セッションを継続・再入禁止）。
メンバーは「5.1.1 プロパティ」「5.2.1 メソッド」「5.3.1 イベント」に記載の通りです。

#### 5.4.2 CaptureType

| 値 | 説明 |
| --- | --- |
| `ScreenSelect` | 全画面選択（カーソル位置のスクリーンを検出・ハイライト） |
| `WindowSelect` | ウィンドウ選択（カーソル位置の最前面ウィンドウを検出・ハイライト） |
| `AreaSelect` | 範囲選択（DragRect でドラッグ選択 + ルーペ表示） |

#### 5.4.3 CaptureSettings

全メンバー nullable の位置パラメータレコード（48 個）。null の意味は利用方法で異なります。

- 初期設定（コンストラクタ）: null = SnipCaptureUI のデフォルト値として扱う。
- 設定適用（Settings の setter）: null = 現在の設定値を維持する（部分更新）。

**キャプチャ動作（3 項目）**:

| メンバー | 型 | 説明 |
| --- | --- | --- |
| `HideOwnerWindow` | `bool?` | キャプチャ時にオーナーウィンドウを非表示にするか |
| `LoupeVisible` | `bool?` | ルーペの表示状態 |
| `HideBeforeCaptureDelay` | `TimeSpan?` | ウィンドウ非表示後の DWM 反映待機時間 |

**DragRect 外観（13 項目）**:

| メンバー | 型 | 説明 |
| --- | --- | --- |
| `MinSelectionSize` | `int?` | 選択矩形の最小サイズ（AreaSelect: **画面物理 px** / Crop: 論理 px = **ドメイン座標系**） |
| `BorderColor` / `BorderThickness` | `Color?` / `double?` | 選択枠線の外観（不透明度は色のアルファに統合） |
| `HandleColor` / `HandleSize` / `HandleHitRadius` | `Color?` / `double?` / `double?` | ドラッグハンドルの外観（色 / サイズ / ヒット半径） |
| `ClampToDomainBounds` | `bool?` | 画像外の選択を禁止するか |
| `MarkerColor` / `MarkerLength` | `Color?` / `double?` | nudge 中の十字マーカーの外観 |
| `ShowCoordLabel` | `bool?` | カーソル座標ラベルの表示有無 |
| `CoordLabelColor` / `CoordLabelBackgroundColor` / `CoordLabelFontSize` | `Color?` / `Color?` / `double?` | カーソル座標ラベルの外観 |

**FadeMask 外観（15 項目）**:

| メンバー | 型 | 説明 |
| --- | --- | --- |
| `OverlayColor` | `Color?` | 暗転オーバーレイの外観（不透明度は色のアルファに統合） |
| `SizeLabelVisible` / `SizeLabelColor` / `SizeLabelFontSize` | `bool?` / `Color?` / `double?` | サイズラベル（1 行目） |
| `SizeLabelOverride` | `string?` | サイズラベルのテキスト明示指定。未指定（null）時はドメイン矩形から自動生成（後述） |
| `DisplayScaleScaling` | `double?` | サイズラベルの DisplayScale 表示の既定値（0 以下はオーバーレイの `scaling`）。選択中の矩形がある場合は、その矩形の中心が含まれるモニタの実スケーリングが表示される（マルチモニタ対応） |

**サイズラベルの自動生成（ScreenSelect / WindowSelect / AreaSelect）**: 選択中の矩形（ドメイン = 画面物理 − Position。物理ピクセル）から直接生成する。
座標・サイズはドメインの値そのまま（スケーリングに依存しない実際の物理ピクセル）、DisplayScale は矩形の中心が含まれるモニタの実スケーリング（例: 100% モニタなら 100%）。
DIP 変換（÷ scaling）→ 物理換算（× scaling）の往復誤差（マルチモニタでサイズ・DisplayScale が一致しない問題）を排除する。
| `TextBackgroundColor` | `Color?` | サイズラベル（1 行目）の背景色 |
| `ShowSubLines` / `SubLineFontSize` / `SubLineColor` / `SubLineBackgroundColor` | `bool?` / `double?` / `Color?` / `Color?` | 操作ラベル（2 行目・背景はアルファ込み） |
| `InfoLabelVisible` / `InfoLabelColor` / `InfoLabelBackgroundColor` / `InfoLabelFontSize` | `bool?` / `Color?` / `Color?` / `double?` | 情報ラベル（3 行目・背景はアルファ込み） |
| `DisplayScaleScaling` | `double?` | サイズラベルの DisplayScale 表示の既定値（0 以下はオーバーレイの `scaling`）。選択中の矩形がある場合は、その矩形の中心が含まれるモニタの実スケーリングが表示される（マルチモニタ対応） |

**Loupe 外観（16 項目）**:

| メンバー | 型 | 説明 |
| --- | --- | --- |
| `LoupeSize` / `LoupeZoomLevel` / `LoupeOffset` | `int?` / `int?` / `double?` | ルーペのサイズ（DIP 座標系）・倍率・カーソルからのオフセット（DIP 座標系） |
| `LoupeRenderScaling` | `double?` | DIP → 物理ピクセル変換 |
| `LoupeCrossColor` | `Color?` | ルーペの十字線（不透明度は色のアルファに統合） |
| `LoupeFrameColor` / `LoupeFrameThickness` | `Color?` / `int?` | ルーペの枠 |
| `LoupeCheckerSize` / `LoupeCheckerColorA` / `LoupeCheckerColorB` | `int?` / `Color?` / `Color?` | ルーペの市松模様 |
| `LoupeSelectionRectColorA` / `LoupeSelectionRectColorB` | `Color?` / `Color?` | 選択矩形破線の色（2 色交互・市松風の視認性） |
| `LoupeShowCoordLabel` | `bool?` | 中心座標ラベルの表示有無 |
| `LoupeCoordLabelColor` / `LoupeCoordLabelBackgroundColor` / `LoupeCoordLabelFontSize` | `Color?` / `Color?` / `double?` | 中心座標ラベルの外観 |

#### 5.4.4 CaptureResult

キャプチャ結果レコード。プロパティは「5.1.2 プロパティ」に記載の通りです。

## 6. 閉区間キャプチャ（重要な仕様）

**選択矩形の始点・終点のピクセルは切出し対象に含まれます（閉区間）**。また、表示（非暗転領域・DragRect の選択枠）も閉区間に統一されています。

DragRect の選択矩形は半開区間（`幅 = 終点 − 始点`、.NET `Rect` と同一）で保持されます。そのままキャプチャ / 切り出しすると、**終点のピクセルが 1 つ欠けます**。そのため、キャプチャ・切り出し・表示のすべてで `+1` して閉区間（始点・終点を含む）に変換します。

| 操作 | 変換 | 備考 |
| --- | --- | --- |
| AreaSelect / ScreenSelect / WindowSelect（`OverlayWindow.CompleteSelection`） | `PixelRect(X, Y, Width + 1, Height + 1)` | 仮想スクリーン Bitmap からの切り出し範囲と `SourceBounds` の両方に適用 |
| Crop（`CropSession.ExecuteCropForSize`） | `PixelRect(X, Y, Width + 1, Height + 1)` | 画像右端・下端で画像サイズを超えないようクランプ |
| Crop のサイズラベル | `WH: (幅 + 1, 高さ + 1)` | 閉区間のピクセル数で表示 |
| 非暗転領域（穴）・DragRect 選択枠（表示） | `Rect(Inverse(始点), Inverse(終点 + (1, 1)))` | FadeMask / DragRect の描画時 DIP 変換で終点に +1。暗転領域は始点・終点のいずれも含まない |

具体例: ドラッグ (10,10) → (50,50) の選択矩形は `Rect(10, 10, 40, 40)`（半開区間 [10,50)）ですが、キャプチャ / 切り出しは **`PixelRect(10, 10, 41, 41)`（[10,50] の 41×41 ピクセル）** になり、表示の非暗転領域も同じ [10,50] の 41×41 ピクセルになります。

## 7. 動作フロー

### 7.1 StartCaptureAsync

1. オーナーウィンドウのスクリーン情報から DPI スケーリング係数を算出（`owner.Screens`）
2. `CaptureSession` を生成（`IWindowService` / `ICaptureService` / `IElementFinder` を PlatformFactory から注入）
3. オーナーウィンドウをキャプチャ除外に設定（`SetCaptureExclusion`。オーバーレイの写り込み防止）
4. オーナーウィンドウを非表示（`HideOwnerWindow` が true の場合）
5. **仮想スクリーン全体をバックグラウンド + 5 秒タイムアウトでキャプチャ（全モード共通。切り出し元）**
6. `OverlayWindow` を表示（全画面透明・Topmost・Alt+Tab 非表示・ウォッチドッグ）
   - AreaSelect: DragRect ドラッグ選択 + Loupe（仮想スクリーン Bitmap 取得完了後に表示開始。Source 未設定の市松模様を防止）
   - Screen / WindowSelect: Pointer 移動で要素検出 → ハイライト。**クリック（左ボタン押下でハイライト固定 → 解放で確定）/ Enter キーで確定 / Esc キーでキャンセル**
7. 確定時: 選択矩形を閉区間（+1）に変換し、**枠選択中に事前切り出し済みのキャッシュを再利用**（不一致時のみ切り出し。枠の変更がない場合は切り出しスキップ）
   - 非アクティブ化 / Close → キャンセル（0x0 空結果）
8. 結果を `CaptureResult(Image, SourceBounds)` で TCS 経由返却 → `CaptureCompleted` イベント（同期・画像変更） → オーナー再表示（次フレーム） → オーナー除外解除

### 7.2 StartCropAsync

1. `CropSession` を生成 → `RunAsync`
2. **複合コントロール `FadeMaskDragRectControl`（DragRect + FadeMask + Loupe の配線を内包）を祖先 Panel に配置**（1 コントロールで完結）
3. 座標変換: `CoordinatePort.Compose(ImageLayout, ScrollOffset)` で論理（元画像 px）↔ 親 Panel DIP を 1 段変換し、**複合コントロールの `SetCoordinateTransform` で DragRect / FadeMask / Loupe の全コントロールに一括注入**（描画・表示時に動的評価 → ウィンドウサイズ変更・ズーム・スクロールに追従）
   - プレビュー状態（ScrollOffset / ZoomPercent）は保持しない（スクロールは `TranslatePoint` に含まれる）
   - ルーペの表示位置・サイズは LoupeControl が自己計算（カーソル右下 + オフセット + クランプ）
   - DragRect にはプレビューの ScrollViewer を `AttachScrollViewer` で明示注入（スクロール・ズーム検知 + ホイール操作）
   - ビューポート変化の通知は `NotifyCropViewportChanged` に一本化（ScrollViewer 連携の `ScrollChanged` も、ScrollViewer 以外のビューポート実装（RenderTransform ベースのパン・ズーム等）のホスト通知も、同じ経路で再描画・ルーペ位置再計算を行う）
4. カーソル上書き抑止（Crop 中の十字カーソル維持）
5. マウスホイール操作（Crop 中）: Ctrl+ホイールで `ZoomRequested` → `zoomRequestHandler` に委譲 / 通常ホイールで祖先 ScrollViewer をスクロール
6. **Crop 中の設定変更（`AppearanceSettings` setter）**: `CropSession.ApplySettings` で外観・クリップ設定を再適用（選択状態・ルーペ位置は維持）。キャプチャセッションはモーダル全画面のため対象外
7. **矩形外クリック・選択キャンセル**: 複合コントロールが `SelectionStarted` / `SelectionCancelled` を購読し、FadeMask の選択状態（`LogicalSelectionRect` / `HasSelection`）をクリアして全面暗転（穴あけなし）に戻す（配線は複合コントロール内で完結）
8. 確定（`ConfirmRequested` → `ExecuteCropForSize`）→ 論理 Rect を `PixelRect` で返却
   - キャンセル（`CancelRequested` / `Deactivated`）→ 0x0 の空 `PixelRect`

## 8. ログ

本プロジェクトのログ定義は `Logging/LogMessage.cs`（LoggerMessage ソース生成）に集約し、`ILogger` の拡張メソッドとして定義する。
各メソッドは `this ILogger` 第一引数で定義し、呼び出し側では logger 引数を省略する（例: `_log.LogCaptureStart(type)`）。
`SnipCapture` コンストラクタの `logger` 引数で注入し、null の場合は出力しない（app では NLog へ転送）。
出力先・レベル制御はホストアプリケーションが所有する（本クラスは定義のみ）。

### 8.1 LogLevel 設定ポリシー

トレースログは LogLevel.Trace に限定せず、「重要度 × 発生頻度」でレベルを使い分ける。

| LogLevel | 対象 | 本番での意図 |
| --- | --- | --- |
| `Error` | 例外・失敗（処理継続不可） | 常時出力し、障害を確実に残す |
| `Warning` | 異常だがフォールバックで継続 | 常時出力し、異常発生の痕跡を残す |
| `Information` | ユーザー操作の主要イベント（低頻度） | 常時出力し、操作手順の再現を可能にする |
| `Debug` | 操作イベントの詳細（セッション開始 / 確定 / キャンセル / 設定変更） | 障害時のみ有効化して解析 |
| `Trace` | 高頻度・極詳細 | 特別な性能・座標解析時のみ有効化 |

判断基準:

1. 例外・警告は常時残す（`Error` / `Warning`）
2. 操作の起点イベント（低頻度）は `Information`
3. マウス・キー等の操作イベント（中頻度）は `Debug`
4. 高頻度・極詳細は `Trace`
5. 設定変更（変更前後）は `Debug`

### 8.2 ログ定義一覧

| レベル | メソッド | 出力内容 |
| --- | --- | --- |
| `Debug` | `LogMessage.LogCaptureStart` | キャプチャセッション開始（種別） |
| `Debug` | `LogMessage.LogCaptureCompleted` | キャプチャ確定（矩形） |
| `Debug` | `LogMessage.LogCaptureCancelled` | キャプチャキャンセル |
| `Debug` | `LogMessage.LogResult` | キャプチャ結果サイズ（画像サイズ / 矩形） |
| `Debug` | `LogMessage.LogOverlayConfirm` | オーバーレイ確定要求（Enter / ダブルクリック等） |
| `Debug` | `LogMessage.LogOverlayCancel` | オーバーレイキャンセル要求（Esc 等） |
| `Debug` | `LogMessage.LogVirtualScreen` | 仮想スクリーン設定（原点 / サイズ） |
| `Trace` | `LogMessage.LogHighlight` | ハイライト検出結果（ScreenSelect / WindowSelect・検出矩形 / タイトル / クラス名。マルチモニタ・最大化ウィンドウの矩形検証とデスクトップウィンドウの特定用） |
| `Debug` | `LogMessage.LogCropStart` | Crop セッション開始（元画像サイズ） |
| `Debug` | `LogMessage.LogCropCompleted` | Crop 確定（切り出し矩形） |
| `Debug` | `LogMessage.LogCropCancelled` | Crop キャンセル |
| `Debug` | `LogMessage.LogSettingChanged` | 設定値変更（変更前後） |

## 9. 制約事項

- `StartCaptureAsync` / `StartCropAsync` は **UI スレッドから呼び出す**こと。
- 実行中の再呼び出しは `null` を返します（既存セッションを継続）。呼び出し側は null を「進行中のため開始できない」と解釈してください。
- `Dispose()` 後の公開操作は `ObjectDisposedException` を投げます。
- `StartCropAsync` の `previewControl` は**祖先に `Panel` が必要**です（配置先がない場合 `InvalidOperationException`）。
- `CaptureSettings` の数値メンバーは `Validate()` で範囲チェックされ、範囲外は `ArgumentOutOfRangeException` を投げます。
- キャプチャ画像へのオーバーレイ写り込みは `IWindowService.SetCaptureExclusion`（Windows 10 2004+）で防止します。それ以前の Windows では写り込みが発生する可能性があります。

## 10. 最適化施策

| 施策 | 内容 |
| --- | --- |
| 薄い統括者（Facade） | SnipCapture は状態を保持せず、組み立て・配線・ライフサイクルのみを担う（各コントロールの状態保持を活用し、状態コピーを排除） |
| イベント駆動・ポーリング排除 | 選択状態の同期は各コントロールの公開イベントのみで行い、ホストは状態をポーリングしない |
| 背景キャプチャ | 仮想スクリーン全体のキャプチャはバックグラウンドスレッド + 5 秒タイムアウトで実行し、UI スレッドをブロックしない（DWM 応答不能時の安全策） |
| 確定時の切り出し（高速化） | 仮想スクリーン UnifiedBitmap をセッション開始時に取得し、確定時は `ImageProcessor.Crop` で切り出す（再キャプチャ・DWM 待機なしで確定を高速化） |
| 枠選択中の事前切り出し（高速化） | スクリーン/ウィンドウ選択のホバー時・領域選択のドラッグ後に、枠（選択矩形）をバックグラウンドで事前切り出ししてキャッシュ（確定時の切り出しをスキップ）。同じ矩形の場合は切り出しをスキップ（キャッシュ再利用） |
| 座標変換の 1 段化 | Crop の座標変換は「論理 ↔ 親 Panel DIP」の 1 段（`CoordinatePort.Compose(ImageLayout, ScrollOffset)`）で完結（プレビュー状態コピー不要） |
| 設定の部分更新 | `CaptureSettings` は全メンバー nullable とし、null で現在値維持（設定オブジェクトの再構築コストを回避） |
| 再入禁止ガード | 実行中の再呼び出しは null を返し、セッションの二重実行によるリソース競合を防止 |
| ウォッチドッグ | UI スレッド完全ハング時はバックグラウンドスレッドからオーバーレイを退避（最終保険） |
