# App.Parts.CapturePreviewControl.AvaloniaUI 外部機能仕様書

## 目次

- [App.Parts.CapturePreviewControl.AvaloniaUI 外部機能仕様書](#apppartscapturepreviewcontrolavaloniaui-外部機能仕様書)
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
      - [5.1.1 CaptureSessionControl.AppearanceSettings](#511-capturesessioncontrolappearancesettings)
      - [5.1.2 CaptureResult のプロパティ](#512-captureresult-のプロパティ)
    - [5.2 メソッド](#52-メソッド)
      - [5.2.1 CaptureSessionControl](#521-capturesessioncontrol)
      - [5.2.2 CaptureSettings](#522-capturesettings)
    - [5.3 イベント](#53-イベント)
      - [5.3.1 CaptureSessionControl.CaptureCompleted](#531-capturesessioncontrolcapturecompleted)
      - [5.3.2 キャプチャ完了イベント（画像変更 → APP 表示の順序保証）](#532-キャプチャ完了イベント画像変更--app-表示の順序保証)
    - [5.4 型](#54-型)
      - [5.4.1 CaptureSessionControl](#541-capturesessioncontrol)
      - [5.4.2 CaptureType](#542-capturetype)
      - [5.4.3 CaptureSettings](#543-capturesettings)
      - [5.4.4 CaptureResult](#544-captureresult)
    - [5.5 CropViewControl（切り出しビュー）](#55-cropviewcontrol切り出しビュー)
      - [5.5.1 プロパティ](#551-プロパティ)
      - [5.5.2 メソッド](#552-メソッド)
      - [5.5.3 イベント](#553-イベント)
      - [5.5.4 外部提供矩形](#554-外部提供矩形)
    - [5.6 FadeMaskDragRectControl（領域選択）](#56-fademaskdragrectcontrol領域選択)
      - [5.6.1 動作モード](#561-動作モード)
      - [5.6.2 プロパティ](#562-プロパティ)
      - [5.6.3 メソッドとイベント](#563-メソッドとイベント)
    - [5.7 ZoomViewControl（切り出しビューの画像ビューポート）](#57-zoomviewcontrol切り出しビューの画像ビューポート)
      - [5.7.1 ズーム遷移ルール](#571-ズーム遷移ルール)
      - [5.7.2 座標系と Fit 表示](#572-座標系と-fit-表示)
      - [5.7.3 公開 API](#573-公開-api)
  - [6. 閉区間キャプチャ（重要な仕様）](#6-閉区間キャプチャ重要な仕様)
  - [7. 動作フロー](#7-動作フロー)
    - [7.1 StartCaptureAsync](#71-startcaptureasync)
    - [7.2 StartCropAsync](#72-startcropasync)
  - [8. 制約事項](#8-制約事項)

---

## 1. 文書概要

| 項目 | 内容 |
| --- | --- |
| 対象 | `App.Parts.CapturePreviewControl.AvaloniaUI` |
| プロジェクト | `App.Parts.CapturePreviewControl.AvaloniaUI/App.Parts.CapturePreviewControl.AvaloniaUI.csproj` |
| 名前空間 | `EsUtil.SaveEvidence.Parts.CapturePreview.AvaloniaUI` |
| 対象フレームワーク | `net10.0-windows10.0.17763.0` |
| UIフレームワーク | Avalonia 12.1 |
| 主要設計パターン | Facade（CaptureSessionControl / CropViewControl / FadeMaskDragRectControl）/ セッション（SelectionCaptureSession / CropSession）/ Composite（内包コントロールの統合）/ 薄い統括者 |
| 主な用途 | 画面キャプチャ（全画面 / ウィンドウ選択 / 範囲選択）と切出し（Crop）の統括 API の提供、および画像表示 + 領域選択の複合コントロールの提供 |

## 2. 目的

`App.Parts.CapturePreviewControl.AvaloniaUI` は、外部アプリケーションが画面キャプチャと切出しを**単一の API**（`CaptureSessionControl`）で利用できるようにする Facade パターンのライブラリです。

状態は複合コントロール `FadeMaskDragRectControl`（Marquee（外側の装飾を含む）+ Loupe を内包）と App.Platform.AvaloniaUI が保持し、
`CaptureSessionControl` は「組み立て・ライフサイクル」のみを担う薄い統括者です。  
App.Parts.CapturePreviewControl.AvaloniaUI は内包コントロール（MarqueeControl / LoupeControl）を直接参照しない（公開型のカプセル化）。

次の機能を提供します。

- 全画面キャプチャ（`ScreenSelect`）
- ウィンドウ選択キャプチャ（`WindowSelect`）
- 範囲選択キャプチャ（`AreaSelect`。 ルーペ付き）
- 切出し（`Crop`。 プレビュー画像上の選択矩形で元画像から切り出し範囲を取得）
- 設定の部分更新（`CaptureSettings`。 全メンバー nullable）
- キャンセル / 確定（`Cancel()` / `Confirm()`）
- 切り出しビュー（`CropViewControl`）: 画像表示 + 領域選択 + 外部提供矩形の複合コントロール（[5.5](#55-cropviewcontrol切り出しビュー)）
- 切り出しビューのズーム・パン・Fit 表示（`CropViewControl` が内包する `ZoomViewControl`。 [5.7](#57-zoomviewcontrol切り出しビューの画像ビューポート)）
- 領域選択（`FadeMaskDragRectControl`）: 暗転マスク + 選択矩形 + ルーペの複合コントロール（[5.6](#56-fademaskdragrectcontrol領域選択)）

## 3. 外部利用条件

### 3.1 プロジェクト参照

```xml
<ProjectReference Include="..\App.Parts.CapturePreviewControl.AvaloniaUI\src\App.Parts.CapturePreviewControl.AvaloniaUI.csproj" />
```

### 3.2 必要な名前空間

```csharp
using EsUtil.SaveEvidence.Parts.CapturePreview.AvaloniaUI;
```

### 3.3 スレッド要件

- `StartCaptureAsync` / `StartCropAsync` は **UI スレッドから呼び出す**こと（Avalonia コントロールの生成・配置を行うため）。

## 4. 基本的な使用方法

### 4.1 キャプチャ（ScreenSelect / WindowSelect / AreaSelect）

```csharp
var capture = new CaptureSessionControl();
var result = await capture.StartCaptureAsync(CaptureType.AreaSelect, ownerWindow);

if (result?.Image is { } image && result.SourceBounds is { } bounds)
{
    // image: キャプチャ画像 / bounds: キャプチャ領域（仮想画面座標）
}
```

### 4.2 切出し（Crop）

```csharp
// 切り出しの対象画像は、CropViewControl が表示している画像（Source）から解決される
var cropRect = await capture.StartCropAsync(cropView, hostWindow);
if (cropRect is { } rect && rect.Width > 0 && rect.Height > 0)
{
    // rect: 元画像上の切り出し範囲（論理座標 = 元画像ピクセル）
}
```

### 4.3 設定の適用

```csharp
var capture = new CaptureSessionControl(new CaptureSettings
{
    LoupeZoomLevel = 4,
    BorderColor = System.Drawing.Color.Red,
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

#### 5.1.1 CaptureSessionControl.AppearanceSettings

| プロパティ | 型 | 説明 |
| --- | --- | --- |
| `AppearanceSettings` | `CaptureSettings` | 現在の外観・動作設定。 getter は全メンバー non-null / setter は部分更新 + 検証 |

#### 5.1.2 CaptureResult のプロパティ

`CaptureResult`（record）は UI 非依存の共通基盤 `App.Parts.CaptureSessionControl.Core` が提供する型です。

| プロパティ | 型 | 説明 |
| --- | --- | --- |
| `Image` | `EsBitmap?` | キャプチャした画像（キャンセル時 null。 破棄は呼び出し側） |
| `SourceBounds` | `EsRectInt?` | キャプチャした領域（**画面物理 Pixel** の仮想画面座標） |

- キャンセル時: `Image` は null、`SourceBounds` は 0x0（`EsRectInt(0, 0, 0, 0)`）
- 成功時: 両方 non-null

`SourceBounds` は仮想画面座標（複数モニターを包含する座標空間）のため、プライマリモニターが (0,0) にない場合 X / Y は負値になり得ます。

### 5.2 メソッド

#### 5.2.1 CaptureSessionControl

| メソッド | シグネチャ | 説明 |
| --- | --- | --- |
| コンストラクタ | `CaptureSessionControl(CaptureSettings? initialSettings = null, ILogger? logger = null)` | インスタンスを生成。 設定値が許容範囲外なら `ArgumentOutOfRangeException` |
| `StartCaptureAsync` | `Task<CaptureResult?> StartCaptureAsync(CaptureType type, Window owner)` | 選択キャプチャを開始。 実行中は null / キャンセル時は 0x0 の空結果 |
| `StartCropAsync` | `Task<EsRectInt?> StartCropAsync(CropViewControl cropView, Window hostWindow)` | 切出しを開始。 戻り値は元画像上の切り出し範囲（**コア型** `EsRectInt`。 UI 型は境界より外へ出さない）。 キャンセル・退化した選択は 0x0 の空矩形、開始できなかった場合は null（他のセッションが実行中 / 表示中の画像が無い）。 切り出しの対象画像は `cropView.Source`（表示中の画像）から解決し、確定時の範囲は**現在の画像**の範囲へクランプする（画像を引数で受け取らない）。 Crop 中のズーム・パン（ホイール）は `cropView` が内部で完結するため、上位のズーム配線は不要（`CropViewControl.ZoomPercent` / `ZoomIn` / `ZoomOut` / `ZoomFit` は従来どおり使用できる） |
| `Cancel` | `void Cancel()` | 進行中セッションをキャンセル（Capture: オーバーレイを閉じる / Crop: 0x0 で完了） |
| `Confirm` | `void Confirm()` | Crop を現在の選択矩形で確定（Enter キー相当。 `CropViewControl.RequestConfirm` に委譲） |
| `ResolveCropAction` | `CropSessionAction ResolveCropAction()` | 切り出し操作（開始 / 確定 / キャンセル）のうちいま行うべき操作を返す。 判断はコア（`CropSessionActions.Resolve`: 未開始なら開始 / 選択済みなら確定 / 未選択ならキャンセル）が単一所有し、**ホストは戻り値を効果へ写すだけ**にする（同じ条件を UI に書かない） |
| `IsCropping` | `bool` | 切り出し中かどうか（真のソースは排他スロット。 ホストが自前のフラグで判定しない） |
| `NotifyCropViewportChanged` | `void NotifyCropViewportChanged()` | 実行中の Crop セッションへビューポート（パン・ズーム・表示サイズ）の変化を通知。 ビューポート実装に依存せず、ホストがビューポート変化を検知した際に中継する（暗転マスク・選択枠・ルーペの追従）。 Crop 未実行時は何もしない |
| `HasSelection` | — | **削除済み**（操作の判断は `ResolveCropAction` へ一本化した。 個別の判定が必要になった場合は `ResolveCropAction` を使う） |
| `AppearanceSettings` | `CaptureSettings` | 現在の外観・動作設定。 設定時は null メンバーのみ現在値を維持する部分更新。 **同一値の再設定はスキップ**。 実行中の Crop セッションへは即時反映（`CropSession.ApplySettings`） |
| `Dispose` | `void Dispose()` | リソースを解放 |

#### 5.2.2 CaptureSettings

| メソッド | 説明 |
| --- | --- |
| `void Validate()` | 設定値を検証（null は対象外）。 範囲外なら `ArgumentOutOfRangeException` |
| `CaptureSettings ToNonNull()` | null をデフォルト値で埋めた non-null 版を返す（例: MinSelectionSize=6 / LoupeSize=160 / LoupeZoomLevel=4 / LoupeOffset=20.0） |

### 5.3 イベント

#### 5.3.1 CaptureSessionControl.CaptureCompleted

| イベント | シグネチャ | 説明 |
| --- | --- | --- |
| `CaptureCompleted` | `event Action<CaptureResult?>? CaptureCompleted` | キャプチャ完了（同期実行。 UI スレッドで発行） |

#### 5.3.2 キャプチャ完了イベント（画像変更 → APP 表示の順序保証）

`CaptureCompleted` は**同期型イベント**です。 C# のイベントは同期実行のため、ハンドラ（ホストの画像変更）の完了後に、キャプチャセッションの後続処理（オーナー再表示）が実行されます。

フロー:

```text
APP 退避（HideOwnerWindow=true で自動。Opacity=0 + 画面外配置）
  → キャプチャ実行
  → CaptureCompleted イベント発行（同期）→ ホストが画像変更
  → オーナー（APP）配置復帰
```

```csharp
_captureSessionControl.CaptureCompleted += result =>
{
    // 画像変更（同期実行。このハンドラの完了後に APP が再表示される）
    if (result?.Image is not null) { UpdatePreview(result.Image); }
};

var result = await _captureSessionControl.StartCaptureAsync(CaptureType.AreaSelect, window);
```

- イベントは UI スレッドで発行されるため、ハンドラ内で UI 更新をそのまま実行できる。
- ハンドラを非同期（`async void`）にすると順序保証が失われるため、**同期ハンドラ**にすること（画像変更は同期で行う）。

### 5.4 型

公開型は `CaptureSessionControl`（本プロジェクト）・複合コントロールの `CropViewControl` / `FadeMaskDragRectControl` とそのイベント引数（`FadeMaskDragRectSelectionEventArgs` / `FadeMaskDragRectPointEventArgs`）、およびコア公開型の `CaptureType`（`EsUtil.Platform.Core`）・`CaptureSettings` / `CaptureResult`（`EsUtil.SaveEvidence.Parts.CaptureSession`。 `App.Parts.CaptureSessionControl.Core`。 UI フレームワーク非依存の共通基盤が提供）です。

#### 5.4.1 CaptureSessionControl

公開 Facade（`public sealed class : IDisposable`）。 `IDisposable` を実装し、Dispose 後は全ての公開操作が `ObjectDisposedException` を投げます。 実行中（`StartCaptureAsync` / `StartCropAsync`）に同一インスタンスへ再呼び出しされた場合は `null` を返します（既存セッションを継続・再入禁止）。
メンバーは「5.1.1 プロパティ」「5.2.1 メソッド」「5.3.1 イベント」に記載の通りです。

#### 5.4.2 CaptureType

キャプチャ種別（UI フレームワーク非依存）。 `Platform.Core`（名前空間 `EsUtil.Platform.Core`）が提供するコア公開型です。 利用時は `using EsUtil.Platform.Core;` が必要です。

| 値 | 説明 |
| --- | --- |
| `ScreenSelect` | 全画面選択（カーソル位置のスクリーンを検出・ハイライト） |
| `WindowSelect` | ウィンドウ選択（カーソル位置の最前面ウィンドウを検出・ハイライト） |
| `AreaSelect` | 範囲選択（Marquee でドラッグ選択 + ルーペ表示） |

#### 5.4.3 CaptureSettings

全メンバー nullable の位置パラメータレコード（47 個）。 null の意味は利用方法で異なります。

- 初期設定（コンストラクタ）: null = App.Parts.CapturePreviewControl.AvaloniaUI のデフォルト値として扱う。
- 設定適用（Settings の setter）: null = 現在の設定値を維持する（部分更新）。

**反映先**: **計算に必要な寸法・対話パラメータ**はコアの設定型（`MarqueeOptions` / `LoupeOptions`。 `FadeMaskDragRectOptionsMapper` が写像）へ、**描画にだけ必要な外観（色・枠線の太さ・表示可否）**は両 UI が共有する `App.Parts.CaptureSessionControl.UIBase` の写像（`CapturePaintMapper` が `MarqueeAppearance` / `LoupeAppearance` を組み立てる）へ反映します。

**キャプチャ動作（3 項目）**:

| メンバー | 型 | 説明 |
| --- | --- | --- |
| `HideOwnerWindow` | `bool?` | キャプチャ時にオーナーウィンドウを画面外へ退避するか（非表示ではなく `Opacity=0` + 画面外配置） |

**Marquee 外観（13 項目）**:

| メンバー | 型 | 説明 |
| --- | --- | --- |
| `MinSelectionSize` | `int?` | 選択矩形の最小サイズ（AreaSelect: **画面物理 px** / Crop: 論理 px = **ドメイン座標系**） |
| `BorderColor` / `BorderThickness` | `Color?` / `double?` | 選択枠線の外観（不透明度は色のアルファに統合） |
| `HandleColor` / `HandleSize` / `HandleHitRadius` | `Color?` / `double?` / `double?` | ドラッグハンドルの外観（色 / サイズ / ヒット半径） |
| `ClampToDomainBounds` | `bool?` | 画像外の選択を禁止するか |
| `MarkerColor` / `MarkerLength` | `Color?` / `double?` | nudge 中の十字マーカーの外観 |
| `ShowCoordLabel` | `bool?` | カーソル座標ラベルの表示有無 |
| `CoordLabelColor` / `CoordLabelBackgroundColor` / `CoordLabelFontSize` | `Color?` / `Color?` / `double?` | カーソル座標ラベルの外観 |

**外側の装飾の外観（15 項目）**:

| メンバー | 型 | 説明 |
| --- | --- | --- |
| `OverlayColor` | `Color?` | 暗転オーバーレイの外観（不透明度は色のアルファに統合） |
| `SizeLabelVisible` / `SizeLabelColor` / `SizeLabelFontSize` | `bool?` / `Color?` / `double?` | サイズラベル（1 行目） |
| `SizeLabelOverride` | `string?` | サイズラベルのテキスト明示指定。 未指定（null）時はドメイン矩形から自動生成（後述） |
| `DisplayScaleScaling` | `double?` | サイズラベルの DisplayScale 表示の既定値（既定値 0.0＝未指定）。 本 UI は矩形の中心が含まれるモニタの実スケーリングを表示する（マルチモニタ対応）ため、明示指定は表示に反映されない |
| `TextBackgroundColor` | `Color?` | サイズラベル（1 行目）の背景色 |
| `ShowSubLines` / `SubLineFontSize` / `SubLineColor` / `SubLineBackgroundColor` | `bool?` / `double?` / `Color?` / `Color?` | 操作ラベル（2 行目・背景はアルファ込み） |
| `InfoLabelVisible` / `InfoLabelColor` / `InfoLabelBackgroundColor` / `InfoLabelFontSize` | `bool?` / `Color?` / `Color?` / `double?` | 情報ラベル（3 行目・背景はアルファ込み） |

**サイズラベルの自動生成（ScreenSelect / WindowSelect / AreaSelect）**: 選択中の矩形（ドメイン = 画面物理 − Position。 物理ピクセル）から直接生成する。
座標・サイズはドメインの値そのまま（スケーリングに依存しない実際の物理ピクセル）、DisplayScale は矩形の中心が含まれるモニタの実スケーリング（例: 100% モニタなら 100%）。
DIP 変換（÷ scaling）→ 物理換算（× scaling）の往復誤差（マルチモニタでサイズ・DisplayScale が一致しない問題）を排除する。

**Loupe 外観（16 項目）**:

| メンバー | 型 | 説明 |
| --- | --- | --- |
| `LoupeSize` / `LoupeZoomLevel` / `LoupeOffset` | `int?` / `int?` / `double?` | ルーペのサイズ（DIP 座標系）・倍率・カーソルからのオフセット（DIP 座標系） |
| `LoupeDipToDeviceScaling` | `double?` | DIP → 物理ピクセル変換 |
| `LoupeCrossColor` | `Color?` | ルーペの十字線（不透明度は色のアルファに統合） |
| `LoupeFrameColor` / `LoupeFrameThickness` | `Color?` / `int?` | ルーペの枠 |
| `LoupePasteboardColor` | `Color?` | ルーペの画像外（ペーストボード）の色 |
| `LoupeSelectionRectColorA` / `LoupeSelectionRectColorB` | `Color?` / `Color?` | 選択矩形破線の色（2 色交互の破線） |
| `LoupeShowCoordLabel` | `bool?` | 中心座標ラベルの表示有無 |
| `LoupeCoordLabelColor` / `LoupeCoordLabelBackgroundColor` / `LoupeCoordLabelFontSize` | `Color?` / `Color?` / `double?` | 中心座標ラベルの外観 |

#### 5.4.4 CaptureResult

キャプチャ結果レコード。 プロパティは「5.1.2 プロパティ」に記載の通りです。

### 5.5 CropViewControl（切り出しビュー）

画像表示 + 領域選択 + 外部提供矩形を統合した高レベル複合コントロール（`Panel` のサブクラス）。 本アセンブリが公開します。 画像ビューポート（`ZoomViewControl`）と領域選択オーバーレイ（`FadeMaskDragRectControl`）を内包し、ホストへ単一インターフェースを提供します。

#### 5.5.1 プロパティ

| プロパティ | 型 | 説明 |
| --- | --- | --- |
| `Source` | `IImage?` | 表示画像。 画像ビューポートとルーペへ伝搬し、変更時は Fit 表示にリセットする。 領域選択オーバーレイの可視性は `SetSelectionOverlayVisible` が専任する |
| `Rects` | `IReadOnlyList<OverlayRect>` | 外部提供矩形の一覧（ドメイン座標）。 ビューポートとルーペの両方へ描画する |
| `SelectionMode` | `FadeMaskDragRectMode` | 選択モード（既定 `Crop`）。 領域選択オーバーレイへ委譲する |
| `SelectionOverlayOptions` | `FadeMaskDragRectOptions`（コア） | 領域選択オーバーレイの設定（寸法・表示可否。 色は含まない） |
| `SelectionOverlayMarqueeAppearance` / `SelectionOverlayLoupeAppearance` | `MarqueeAppearance` / `LoupeAppearance`（提示層） | 領域選択オーバーレイの外観（描画にだけ必要な設定） |
| `Appearance` | `ZoomViewAppearance` | ズームビューの外観（下地色・市松模様等） |
| `CenterAlign` | `bool` | 画像の中央寄せ（その軸で画像がビューポートより小さい場合に有効） |
| `ZoomPercent` / `CanZoomIn` / `CanZoomOut` | `double` / `bool` / `bool` | ズーム率（%）と可否。 ズームの計算はコアが単一所有する |
| `HasSelection` / `SelectionDomainRect` | `bool` / `Rect` | 選択状態と現在の選択矩形（ドメイン座標） |
| `SizeLabelOverrideProvider` / `OperationTextProvider` / `InfoTextProvider` | `Func<string?>?` | ラベル文言のプロバイダ（文言はホストが与える） |
| `Log` | `ILogger` | 内部ロガー（画像ビューポートへ伝搬する） |
| `DipToDomainScaling` / `LoupeDipToDeviceScaling` | `double` | DPI スケーリング（環境値。 コードから動的設定） |

#### 5.5.2 メソッド

| メソッド | 説明 |
| --- | --- |
| `ZoomIn()` / `ZoomOut()` / `ZoomFit()` | 段階遷移・Fit 表示 |
| `SetSelection(Rect)` / `ClearSelection()` | 選択状態の外部設定・クリア |
| `SetSelectionOverlayVisible(bool)` | 領域選択オーバーレイの可視性とヒットテストを設定（`false` で選択もクリア） |
| `SetCoordTransform(ICoordTransform?)` | 座標変換ポートを内包コントロールへ一括分配 |
| `RequestConfirm()` / `RequestCancel()` | 選択確定 / キャンセル要求 |
| `FocusMarquee()` | 選択矩形へフォーカスを設定する（矢印キー nudge の受付開始） |
| `SetLoupeSource(IImage?)` / `ClearLoupe()` | ルーペの拡大元画像の設定・クリア |
| `NotifyViewportChanged()` | 表示（パン・ズーム・表示サイズ）の変化を内包オーバーレイへ通知 |
| `ImageRectViewport()` / `ContentToViewport(Point)` / `ViewportToContent(Point)` | 画像表示矩形の取得と、ドメイン ↔ ビューポートの座標変換 |

領域選択オーバーレイ上のホイールは本コントロールがビューポート（ズーム・パン）へ転送します（上位の配線は不要）。

#### 5.5.3 イベント

| イベント | 型 | 説明 |
| --- | --- | --- |
| `ConfirmRequested` | `EventHandler<FadeMaskDragRectSelectionEventArgs>` | 選択確定要求 |
| `CancelRequested` | `EventHandler?` | 選択キャンセル要求 |
| `ZoomChanged` | `EventHandler<ZoomChangedEventArgs>` | ズーム率変更の通知 |
| `ViewportChanged` | `EventHandler?` | ビューポート（パン・ズーム・表示状態）変化の通知 |

#### 5.5.4 外部提供矩形

`Rects` の要素（`OverlayRect`）は UI 非依存の `App.Parts.CaptureSessionControl.Core`（名前空間 `EsUtil.SaveEvidence.Parts.CaptureSession`）が提供します。 矩形（`EsRect`）・枠線スタイル（`OverlayLineStyle`）・前景色・背景色・線幅を持ち、実線は前景色の枠のみ、破線（`Dashed` / `Dotted`）は「背景色の実線 + 前景色の破線」で描画します（ダッシュ長はコアの `OverlayLineDash` が単一所有）。

### 5.6 FadeMaskDragRectControl（領域選択）

選択矩形（ドラッグ）・暗転マスク・ルーペを 1 つのコントロールにまとめ、配線と状態同期を内側で完結させる複合コントロールです。 ホストは `Mode` の設定と状態の受け取りだけで利用できます。

#### 5.6.1 動作モード

| モード | 選択矩形 | ルーペ | 全面暗転 | 用途 |
| --- | --- | --- | --- | --- |
| `Crop` | 有効 | 有効 | 有効 | プレビュー内の切出し |
| `AreaSelect` | 有効 | 有効 | 有効 | 全画面オーバーレイの範囲選択 |
| `ScreenSelect` | 無効 | 無効 | 無効 | スクリーン選択（ハイライトのみ。 確定フローはホスト） |
| `WindowSelect` | 無効 | 無効 | 無効 | ウィンドウ選択（ハイライトのみ。 確定フローはホスト） |

既定値は `AreaSelect` です。 モードの解釈（表示可否・全面暗転）はコアの `FadeMaskDragRectModes` が単一所有します。

#### 5.6.2 プロパティ

| プロパティ | 型 | 説明 |
| --- | --- | --- |
| `Mode` | `FadeMaskDragRectMode` | 動作モード（可視性と全面暗転を一括切替。 既定 `AreaSelect`） |
| `Options` | `FadeMaskDragRectOptions`（コア） | 設定の単一値（選択矩形 / 暗転マスク / ルーペ / 選択矩形破線。 色は含まない）。 同じ値の再設定はスキップされる |
| `MarqueeAppearance` / `LoupeAppearance` | 提示層の外観 | 描画にだけ必要な外観の単一値（内包コントロールへ委譲する） |
| `HasSelection` / `SelectionDomainRect` | `bool` / `Rect` | 選択矩形の有無と現在値（ドメイン座標） |
| `HighlightDomainRect` | `Rect?` | ハイライト矩形（**null はハイライトなし**） |
| `ShowFullOverlay` | `bool` | 全面暗転の有無（モードから導出。 読み取り専用） |
| `LoupeCenterPoint` | `Point?` | ルーペ中心（ドメイン座標。 未設定時は null） |
| `SizeLabelOverrideProvider` / `OperationTextProvider` / `InfoTextProvider` | `Func<string?>?` | ラベル文言のプロバイダ（文言はホストが与える） |
| `DipToDomainScaling` / `DisplayScaleScaling` / `LoupeDipToDeviceScaling` | `double` | DPI スケーリング（環境値） |
| `Log` | `ILogger` | 操作ログの出力先（内包コントロールへ伝搬する） |

#### 5.6.3 メソッドとイベント

| メソッド | 説明 |
| --- | --- |
| `SetCoordTransform(ICoordTransform?)` | 座標変換ポートを一括分配（null で倍率からコアが解決） |
| `SetSelection(Rect)` / `ClearSelection()` | 選択状態の設定・クリア |
| `SetLoupePosition(Point)` / `SetLoupeSource(IImage?)` / `ClearLoupe()` | ルーペの制御 |
| `RequestConfirm()` / `RequestCancel()` | 確定・キャンセル要求 |
| `FocusMarquee()` / `RefreshOverlay()` / `InvalidateOverlay()` | フォーカス設定・表示の変化への追従・再描画要求 |

| イベント | 型 | 説明 |
| --- | --- | --- |
| `ConfirmRequested` | `EventHandler<FadeMaskDragRectSelectionEventArgs>` | 確定要求（選択確定後のみ発火） |
| `CancelRequested` | `EventHandler?` | キャンセル要求 |
| `LoupePositionMoved` | `EventHandler<FadeMaskDragRectPointEventArgs>` | ルーペ中心の更新（ドメイン座標） |
| `LoupeRendered` | `EventHandler<LoupeRenderedEventArgs>` | ルーペ描画完了の通知（ホストがルーペ画像へ外部提供矩形を描く用） |

選択領域は**閉区間**（始点・終点を含む）として扱い、暗転マスク（非暗転領域）とルーペの選択枠が一致します。

### 5.7 ZoomViewControl（切り出しビューの画像ビューポート）

画像の拡大・パン・Fit 表示を担う画像ビューポートです。 **`CropViewControl` が内包する内部部品**であり、独立した汎用コントロールとしてではなく切り出しビューの表示を成立させるために存在します。 ホストは通常 `CropViewControl` の `ZoomPercent` / `ZoomIn` / `ZoomOut` / `ZoomFit` / `ViewportToContent` / `ContentToViewport` / `ImageRectViewport` を経由して利用します（`CropViewControl` は同じ値・操作を透過します）。

判断（実効ズーム率の導出・可否・遷移の適用・パン・スクロールバー・Fit 復帰）は UI 非依存コア `App.Parts.ZoomViewControl.Core` の `ZoomViewSession` が単一所有し、本コントロールは入力の変換・効果の適用・描画・通知の転送だけを担います。

#### 5.7.1 ズーム遷移ルール

| 操作 | 内容 |
| --- | --- |
| ⊕ / ⊖（`ZoomIn` / `ZoomOut`） | 現在値が 23 段階リスト内なら隣の段階へ、リスト外（スライダの中間値）なら次に大きい / 小さい段階へ移動する。 リスト先頭（10%）で ⊖ 停止、末尾（500%）で ⊕ 停止する |
| Ctrl + ホイール | 現在の拡大率を基準に `Round(現在値 × 1.1)` / `Round(現在値 ÷ 1.1)` で連続遷移し、10〜500% へクランプする |
| Fit 中 | 実効倍率を基準に遷移する（23 段階・×1.1 のいずれも）。 `fitZoom` は `MinZoom`〜`MaxZoom` にクランプする |
| Fit 実効倍率の挟み込み | ズームイン / アウトで隣の段階と現在値の間に Fit 実効倍率がある場合、それを次の倍率として経由する（例: `100% → 102%（Fit 実効倍率）→ 110%`）。 近接している場合は 23 段階を優先する |

23 段階のレベル集合（`ZoomConfig.ButtonZoomLevels`）:

```text
10 → 12 → 15 → 20 → 25 → 33 → 40 → 50 → 62 → 75 → 80 → 90
→ 100 → 110 → 125 → 150 → 200 → 250 → 300 → 350 → 400 → 450 → 500
```

#### 5.7.2 座標系と Fit 表示

| 座標系 | 定義 |
| --- | --- |
| コンテンツ座標 | 元画像ピクセル（ズーム率に依存しない不変座標）。 切り出しのドメイン座標と一致する |
| ビューポート座標 | コントロールのローカル座標（DIP） |

- 描画位置は `実効スケール × (コンテンツ座標 − オフセット)` で決まる。 オフセットは「ビューポート左上に対応するコンテンツ座標」であり、スクロール範囲（`コンテンツ − ビューポート ÷ 実効スケール`）にクランプされる
- `CropViewControl.ContentToViewport` / `ViewportToContent` は画像表示矩形の原点基準（Fit 中央寄せの原点補正込み）で双方向変換する
- **Fit 実効倍率**は `min(ビューポート幅 ÷ 画像幅, ビューポート高さ ÷ 画像高さ) × 100`（Uniform・アスペクト比維持）で、現在の表示状態に依存せずビューポートと画像サイズから一定に計算する。 ウィンドウのリサイズに追従する
- 実効スケールは固定倍率中が `ズーム率 ÷ 100`、Fit 中が `FitScale`。 Fit 中の座標変換でもゼロ除算が発生しない
- Fit 中はパン不可（画像がビューポートに収まる。 オフセット 0）

**ズーム中心維持**: 画像内にマウスポインタがあればその位置を、画像外（ビューポートの空き領域）ならビューポート中心を中心に拡大縮小し、中心のコンテンツ座標 `c = offset + center / oldScale` を不変に保つ（`offset' = c − center / newScale` をスクロール範囲へクランプする）。

**パン**: マウスドラッグは grab 方式で、押下時に掴んだ位置のコンテンツ座標を保存し、その位置がポインタに追従するようオフセットを再計算する（ドラッグ方向へコンテンツが移動する）。 ホイールは縦パン、Shift+ホイールは横パン。 スクロールバーはオーバーレイ表示で、画像がビューポートより大きい軸のみ表示する。

#### 5.7.3 公開 API

| メンバー | シグネチャ | 説明 |
| --- | --- | --- |
| `Source` | `IImage?`（読み書き） | ズーム対象の画像。 設定すると Fit 表示にリセット |
| `Appearance` | `ZoomViewAppearance`（読み書き） | 外観の単一値（下地色・市松模様（サイズと 2 色）・スクロールバー）。 市松模様は画像の表示矩形の内側だけに描き、画像の外（ペーストボード）は下地色の単色になる |
| `ZoomPercent` | `double`（読み書き） | 実効ズーム率（%）。 getter は Fit 中は実効倍率（`MinZoom`〜`MaxZoom` にクランプ）・固定倍率中はズーム率を返す（0 にならない）。 setter は 0 より大きい値のみ設定（0 以下は無視。 Fit へは `ZoomFit`） |
| `IsFit` | `bool` | Fit 表示中かどうか（Fit 判定はこちら。 `ZoomPercent` は 0 にならない） |
| `CenterAlign` | `bool`（読み書き） | 画像の中央寄せ / 左上寄せ（軸ごとに独立。 その軸で画像がビューポートより小さい場合に有効） |
| `ZoomIn()` / `ZoomOut()` / `ZoomFit()` | `void()` | 23 段階の遷移 / Fit 表示への復帰（唯一の Fit 移行経路） |
| `PanByWheel(Vector, bool)` / `ZoomByWheel(bool, Point?)` | — | ホストが他のコントロールで受けたホイールの転送（Ctrl+ホイールのズームはコントロール内部で自己完結する） |
| `CanZoomIn` / `CanZoomOut` | `bool` | 現在の状態でズーム可能か（初期表示時にボタン状態を設定する用途） |
| `ViewportToContentImage(Point)` / `ContentToViewportImage(Point)` | `Point(Point)` | 画像表示矩形の原点基準の座標変換 |
| `ImageRectViewport()` | `Rect()` | 画像の表示矩形（原点 + サイズ・ビューポート座標） |
| `ZoomChanged` | `event EventHandler<ZoomChangedEventArgs>` | ズーム率変更の通知（変更後の実効ズーム率 + Fit 状態 + 可否） |
| `ViewportChanged` | `event EventHandler` | ビューポート（パン・ズーム・表示状態）変化の通知。 オーバーレイの追従に使用する |
| `Log` | `ILogger` | 内部ロガー（null は `NullLogger`。 ズーム操作の前後 % を Information で記録する） |

**`ZoomConfig`（ズーム遷移の設定値。 コアの検証付き不変値）**

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `MinZoom` / `MaxZoom` | `double` | `10` / `500` | 固定倍率の最小値・最大値（%） |
| `WheelFactor` | `double` | `1.1` | Ctrl+ホイールのズーム倍率 |
| `ButtonZoomLevels` | `IReadOnlyList<double>` | 23 段階 | ⊖⊕ のズームレベル集合 |
| `Default` | `ZoomConfig` | 既定値 | 既定の設定。 `CropViewControl` が内包する `ZoomViewControl` はこの既定値を使用する |

`ZoomConfig` は生成時に不変条件（`MinZoom` が 0 以下・`MaxZoom` が `MinZoom` 未満・`WheelFactor` が 1 以下・`ButtonZoomLevels` が範囲外 / 非昇順 / 空）を検証し、満たさない場合は `ArgumentException` を投げる。

**`ZoomChangedEventArgs`（イベント引数）**

`sealed record(ZoomState State)`。 実効ズーム率（`ZoomPercent`）・Fit 状態（`IsFit`）・ズーム可否（`CanZoomIn` / `CanZoomOut`）を通知する（コアの `ZoomState` を保持し、各値は個別プロパティでも参照できる）。 表示文字列の生成（書式・丸め・% 表記）はホストの責務である。

## 6. 閉区間キャプチャ（重要な仕様）

**選択矩形の始点・終点のピクセルは切出し対象に含まれます（閉区間）**。 ただし、カーソル位置から検出するスクリーン・ウィンドウの矩形は最初から全ピクセルを含むため、範囲選択と扱いが異なります。

Marquee の選択矩形（範囲選択）は半開区間（`幅 = 終点 − 始点`、.NET `Rect` と同一）で保持されます。 そのまま切り出すと、**終点のピクセルが 1 つ欠けます**。 そのため、範囲選択では `+1` して閉区間（始点・終点を含む）に変換します。

| 種別 | 変換 | 備考 |
| --- | --- | --- |
| AreaSelect | `+1`（`Closed`） | 半開区間の選択矩形を始点・終点を含む形（終点のピクセルを加算）へ変換する |
| ScreenSelect / WindowSelect | 変換なし | 検出した画面・ウィンドウの矩形は全ピクセルを含む。 `+1` すると画面外・ウィンドウ外へ 1 px はみ出す |
| Crop | `+1`（`Closed`、画像の端でクランプ） | `CropBoundsMath.ComputeCropBounds` が単一所有する |
| 撮影（全種別） | 上表の変換 + 画像内へのクランプ | 確定時の切り出し矩形は `CaptureClipMath.ResolveCaptureRect` が単一所有する（種別の性質とクランプ・丸めを組み合わせた唯一の入口。 画像の右端・下端で透明な余白が付かない） |
| Crop のサイズラベル | `WH: (右端 − 左端 + 1, 下端 − 上端 + 1)` | 閉区間のピクセル数で表示（`CropLabelTexts` が単一所有する） |

種別ごとの適用有無はコアの `CaptureTypeTraits.ResolveInterval` が単一所有します（AreaSelect のみ `Closed`）。

具体例: ドラッグ (10,10) → (50,50) の選択矩形は `Rect(10, 10, 40, 40)`（半開区間 [10,50)）ですが、切り出しは **`EsRectInt(10, 10, 41, 41)`（[10,50] の 41×41 ピクセル）** になります。

## 7. 動作フロー

### 7.1 StartCaptureAsync

1. オーナーウィンドウのスクリーン情報から DPI スケーリング係数を算出（`owner.Screens`。 0 以下は 1.0 へ正規化）
2. `SelectionCaptureSession` を生成（`IWindowService` / `ICaptureService` / `IElementFinder` を `Platform.Core` の `Native`（既定実装は `Native.Win`）から注入）
3. オーナーウィンドウをキャプチャ除外に設定（`SetCaptureExclusion`。 オーバーレイの写り込み防止）→ 画面外へ退避（`HideOwnerWindow` が true の場合）
4. **仮想スクリーン全体をバックグラウンド + 5 秒タイムアウトでキャプチャ（全モード共通。 切り出し元）**
5. `OverlayWindow` を表示（全画面透明・Topmost・Alt+Tab 非表示）
   - AreaSelect: Marquee ドラッグ選択 + Loupe（仮想スクリーン Bitmap 取得完了後に表示開始。 Source 未設定の「画像外の色のみ」表示を防止）
   - Screen / WindowSelect: Pointer 移動で要素検出 → ハイライト。 **クリック（左ボタン押下でハイライト固定 → 解放で確定）/ Enter キーで確定 / Esc キーでキャンセル**
6. 確定時: 選択矩形を種別に応じて切り出し矩形へ変換（AreaSelect は終点のピクセルを加算。 ScreenSelect / WindowSelect は変換なし）し、**枠選択中に事前切り出し済みのキャッシュを再利用**（不一致時のみ切り出し。 枠の変更がない場合は切り出しスキップ）
   - 非アクティブ化 / Close → キャンセル（0x0 空結果）
7. 結果を `CaptureResult(Image, SourceBounds)` で TCS 経由返却 → `CaptureCompleted` イベント（同期・画像変更） → オーナー配置復帰 → オーナー除外解除

### 7.2 StartCropAsync

1. 切り出しの対象画像は `CropViewControl` が表示している画像（`CropViewControl.Source`）から解決する（画像を引数で受け取らない）。
   - 画像が未設定、または表示アダプタ（`EsBitmapAvaloniaImage`）以外の場合は開始しない（結果 null。 実行中の再呼び出しと同じ表現）
   - 画像サイズが 0 の場合は `ArgumentException`（「サイズが 0 の画像は切り出せない」規則はコアの `CaptureClipMath.ValidateImage` が単一所有する）
   - 開始できた場合は `CropSession` を生成 → `RunAsync`
2. **複合コントロール `FadeMaskDragRectControl`（Marquee（外側の装飾を含む）+ Loupe の配線を内包）を `CropViewControl.Rects` へ配置**（1 コントロールで完結）
   - セッション開始時に `cropView.SetSelectionOverlayVisible(true)` を呼び、領域選択オーバーレイを可視化する
     （プレビュー表示時は非表示のため。 Crop セッション中のみ暗転マスクを表示）
   - セッション終了時（確定・キャンセル）に `cropView.SetSelectionOverlayVisible(false)` で非表示 + 選択クリアに戻す
     （キャプチャ完了後・切出し後に Crop モード（暗転）が残るのを防ぐ）
3. 座標変換: `CoordTransform`（`CropViewControl` の `ViewportToContent` / `ContentToViewport` に委譲）で論理（元画像 px）↔ ビューポート DIP を 1 段変換し、**複合コントロールの `SetCoordTransform` で Marquee / Loupe の全コントロールに一括注入**（描画・表示時に動的評価 → ウィンドウサイズ変更・ズーム・スクロールに追従）
   - プレビュー状態（ScrollOffset / ZoomPercent）は保持しない（スクロール・ズームは内包する `CropViewControl`（`ZoomViewControl`）が所有）
   - ルーペの表示位置・サイズは LoupeControl が自己計算（カーソル右下 + オフセット + 領域内への補正）
   - ビューポート変化の通知は `NotifyCropViewportChanged` に一本化（ScrollViewer 連携の `ScrollChanged` も、ScrollViewer 以外のビューポート実装（RenderTransform ベースのパン・ズーム等）のホスト通知も、同じ経路で再描画・ルーペ位置再計算を行う）
4. カーソル上書き抑止（Crop 中の十字カーソル維持）
5. マウスホイール操作（Crop 中）: CropViewControl がオーバーレイ上のホイールをビューポートへ転送する（Ctrl+ホイール = ズーム / 通常ホイール = パン）
6. **Crop 中の設定変更（`AppearanceSettings` setter）**: `CropSession.ApplySettings` で外観・クリップ設定を再適用（選択状態・ルーペ位置は維持）。 キャプチャセッションはモーダル全画面のため対象外
7. **矩形外クリック・選択キャンセル**: 複合コントロールが `SelectionStarted` / `SelectionCancelled` を購読し、Marquee の選択状態（`Options.LogicalSelectionRect` / `Options.HasSelection`）をクリアして全面暗転（注目なし）に戻す（配線は複合コントロール内で完結）
8. 確定（`ConfirmRequested` → `CropBoundsMath.ComputeCropBounds`）→ 切り出し範囲をコア型 `EsRectInt` で返却
   - 切り出し範囲は**現在の画像**（確定時点の `CropViewControl.Source`）の範囲へクランプする（開始時のサイズで固定しない。 実行中に画像が差し替わっても差し替え後の画像で切り出す）
   - キャンセル（`CancelRequested` / `Deactivated`）→ 0x0 の空矩形
   - 退化した選択（画像外・最小サイズ未満）→ 0x0 の空矩形（確定と同じ経路でキャンセルとして記録する）
   - 確定時に画像が無い（差し替えで消えた等）→ 0x0 の空矩形（キャンセルとして扱う）

## 8. 制約事項

- `StartCaptureAsync` / `StartCropAsync` は **UI スレッドから呼び出す**こと。
- 実行中の再呼び出しは `null` を返します（既存セッションを継続）。 呼び出し側は null を「進行中のため開始できない」と解釈してください。
- `Dispose()` 後の公開操作は `ObjectDisposedException` を投げます。
- `StartCropAsync` の `cropView` は `CropViewControl`（ズーム + 領域選択 + ルーペを内包した複合コントロール）です。 選択矩形のオーバーレイ（Marquee / Loupe）はセッションが `CropViewControl.Rects` へ配置するため、利用側での配置は不要です（ズーム・パン・Fit・座標変換は内包する `ZoomViewControl` が単一所有）。 切り出しの対象画像は `cropView.Source` に `EsBitmapAvaloniaImage` として設定しておく必要があります（未設定・非対応の場合は開始せず結果 null）。
- `CropViewControl` / `FadeMaskDragRectControl` は本アセンブリが公開する複合コントロールです（別プロジェクトではありません）。
- `CaptureSettings` の数値メンバーは `Validate()` で範囲チェックされ、範囲外は `ArgumentOutOfRangeException` を投げます。
- キャプチャ画像へのオーバーレイ写り込みは `IWindowService.SetCaptureExclusion`（Windows 10 2004+）で防止します。 それ以前の Windows では写り込みが発生する可能性があります。
- `ZoomPercent`（実効ズーム率）は 0 にならない（`ZoomPercent` setter に 0 以下を設定しても無視される）。 Fit 表示は `ZoomFit` で行います。
- Fit 中はパン不可（画像がビューポートに収まるため）。 スクロールバーも非表示になります。
- `Ctrl + マウスホイール` は画像ビューポートが捕捉し、通常のパンには伝播しません（Ctrl を離すとパンに戻る）。
