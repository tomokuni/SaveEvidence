# CaptureSessionControl.AvaloniaUI 外部機能仕様書

## 目次

- [CaptureSessionControl.AvaloniaUI 外部機能仕様書](#capturesessioncontrolavaloniaui-外部機能仕様書)
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
  - [6. 閉区間キャプチャ（重要な仕様）](#6-閉区間キャプチャ重要な仕様)
  - [7. 動作フロー](#7-動作フロー)
    - [7.1 StartCaptureAsync](#71-startcaptureasync)
    - [7.2 StartCropAsync](#72-startcropasync)
  - [8. 制約事項](#8-制約事項)

---

## 1. 文書概要

| 項目 | 内容 |
| --- | --- |
| 対象 | `CaptureSessionControl.AvaloniaUI` |
| プロジェクト | `CaptureSessionControl.AvaloniaUI/CaptureSessionControl.AvaloniaUI.csproj` |
| 名前空間 | `EsUtil.SaveEvidence.CaptureSession.AvaloniaUI` |
| 対象フレームワーク | `net10.0-windows10.0.17763.0` |
| UIフレームワーク | Avalonia 12.1 |
| 主要設計パターン | Facade（CaptureSessionControl）/ セッション（SelectionCaptureSession / CropSession）/ 薄い統括者 |
| 主な用途 | 画面キャプチャ（全画面 / ウィンドウ選択 / 範囲選択）と切出し（Crop）の統括 API の提供 |

## 2. 目的

`CaptureSessionControl.AvaloniaUI` は、外部アプリケーションが画面キャプチャと切出しを**単一の API**（`CaptureSessionControl`）で利用できるようにする Facade パターンのライブラリです。

状態は複合コントロール `FadeMaskDragRectControl`（Marquee（外側の装飾を含む）+ Loupe を内包）と Platform.AvaloniaUI が保持し、
`CaptureSessionControl` は「組み立て・ライフサイクル」のみを担う薄い統括者です。  
CaptureSessionControl.AvaloniaUI は内包コントロール（MarqueeControl / LoupeControl）を直接参照しない（公開型のカプセル化）。

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
<ProjectReference Include="..\CaptureSessionControl.AvaloniaUI\src\CaptureSessionControl.AvaloniaUI.csproj" />
```

### 3.2 必要な名前空間

```csharp
using EsUtil.SaveEvidence.CaptureSession.AvaloniaUI;
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
| `AppearanceSettings` | `CaptureSettings` | 現在の外観・動作設定。getter は全メンバー non-null / setter は部分更新 + 検証 |

#### 5.1.2 CaptureResult のプロパティ

`CaptureResult`（record）は UI 非依存の共通基盤 `CaptureSessionControl.Core` が提供する型です。

| プロパティ | 型 | 説明 |
| --- | --- | --- |
| `Image` | `EsBitmap?` | キャプチャした画像（キャンセル時 null。破棄は呼び出し側） |
| `SourceBounds` | `EsRectInt?` | キャプチャした領域（**画面物理 Pixel** の仮想画面座標） |

- キャンセル時: `Image` は null、`SourceBounds` は 0x0（`EsRectInt(0, 0, 0, 0)`）
- 成功時: 両方 non-null

`SourceBounds` は仮想画面座標（複数モニターを包含する座標空間）のため、プライマリモニターが (0,0) にない場合 X / Y は負値になり得ます。

### 5.2 メソッド

#### 5.2.1 CaptureSessionControl

| メソッド | シグネチャ | 説明 |
| --- | --- | --- |
| コンストラクタ | `CaptureSessionControl(CaptureSettings? initialSettings = null, ILogger? logger = null)` | インスタンスを生成。設定値が許容範囲外なら `ArgumentOutOfRangeException` |
| `StartCaptureAsync` | `Task<CaptureResult?> StartCaptureAsync(CaptureType type, Window owner)` | 選択キャプチャを開始。実行中は null / キャンセル時は 0x0 の空結果 |
| `StartCropAsync` | `Task<EsRectInt?> StartCropAsync(CropViewControl cropView, Window hostWindow)` | 切出しを開始。戻り値は元画像上の切り出し範囲（**コア型** `EsRectInt`。UI 型は境界より外へ出さない）。キャンセル・退化した選択は 0x0 の空矩形、開始できなかった場合は null（他のセッションが実行中 / 表示中の画像が無い）。切り出しの対象画像は `cropView.Source`（表示中の画像）から解決し、確定時の範囲は**現在の画像**の範囲へクランプする（画像を引数で受け取らない）。Crop 中のズーム・パン（ホイール）は `cropView` が内部で完結するため、上位のズーム配線は不要（`CropViewControl.ZoomPercent` / `ZoomIn` / `ZoomOut` / `ZoomFit` は従来どおり使用できる） |
| `Cancel` | `void Cancel()` | 進行中セッションをキャンセル（Capture: オーバーレイを閉じる / Crop: 0x0 で完了） |
| `Confirm` | `void Confirm()` | Crop を現在の選択矩形で確定（Enter キー相当。`CropViewControl.RequestConfirm` に委譲） |
| `ResolveCropAction` | `CropSessionAction ResolveCropAction()` | 切り出し操作（開始 / 確定 / キャンセル）のうちいま行うべき操作を返す。判断はコア（`CropSessionActions.Resolve`: 未開始なら開始 / 選択済みなら確定 / 未選択ならキャンセル）が単一所有し、**ホストは戻り値を効果へ写すだけ**にする（同じ条件を UI に書かない） |
| `IsCropping` | `bool` | 切り出し中かどうか（真のソースは排他スロット。ホストが自前のフラグで判定しない） |
| `NotifyCropViewportChanged` | `void NotifyCropViewportChanged()` | 実行中の Crop セッションへビューポート（パン・ズーム・表示サイズ）の変化を通知。ビューポート実装に依存せず、ホストがビューポート変化を検知した際に中継する（暗転マスク・選択枠・ルーペの追従）。Crop 未実行時は何もしない |
| `HasSelection` | — | **削除済み**（操作の判断は `ResolveCropAction` へ一本化した。個別の判定が必要になった場合は `ResolveCropAction` を使う） |
| `AppearanceSettings` | `CaptureSettings` | 現在の外観・動作設定。設定時は null メンバーのみ現在値を維持する部分更新。**同一値の再設定はスキップ**。実行中の Crop セッションへは即時反映（`CropSession.ApplySettings`） |
| `Dispose` | `void Dispose()` | リソースを解放 |

#### 5.2.2 CaptureSettings

| メソッド | 説明 |
| --- | --- |
| `void Validate()` | 設定値を検証（null は対象外）。範囲外なら `ArgumentOutOfRangeException` |
| `CaptureSettings ToNonNull()` | null をデフォルト値で埋めた non-null 版を返す（例: MinSelectionSize=6 / LoupeSize=160 / LoupeZoomLevel=4 / LoupeOffset=20.0） |

### 5.3 イベント

#### 5.3.1 CaptureSessionControl.CaptureCompleted

| イベント | シグネチャ | 説明 |
| --- | --- | --- |
| `CaptureCompleted` | `event Action<CaptureResult?>? CaptureCompleted` | キャプチャ完了（同期実行。UI スレッドで発行） |

#### 5.3.2 キャプチャ完了イベント（画像変更 → APP 表示の順序保証）

`CaptureCompleted` は**同期型イベント**です。C# のイベントは同期実行のため、ハンドラ（ホストの画像変更）の完了後に、キャプチャセッションの後続処理（オーナー再表示）が実行されます。

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

公開型は `CaptureSessionControl`（本プロジェクト）と、コア公開型の `CaptureType`（`EsUtil.Platform.Core`）・`CaptureSettings` / `CaptureResult`（`EsUtil.SaveEvidence.CaptureSession`。`CaptureSessionControl.Core`。UI フレームワーク非依存の共通基盤が提供）です。

#### 5.4.1 CaptureSessionControl

公開 Facade（`public sealed class : IDisposable`）。`IDisposable` を実装し、Dispose 後は全ての公開操作が `ObjectDisposedException` を投げます。実行中（`StartCaptureAsync` / `StartCropAsync`）に同一インスタンスへ再呼び出しされた場合は `null` を返します（既存セッションを継続・再入禁止）。
メンバーは「5.1.1 プロパティ」「5.2.1 メソッド」「5.3.1 イベント」に記載の通りです。

#### 5.4.2 CaptureType

キャプチャ種別（UI フレームワーク非依存）。`Platform.Core`（名前空間 `EsUtil.Platform.Core`）が提供するコア公開型です。利用時は `using EsUtil.Platform.Core;` が必要です。

| 値 | 説明 |
| --- | --- |
| `ScreenSelect` | 全画面選択（カーソル位置のスクリーンを検出・ハイライト） |
| `WindowSelect` | ウィンドウ選択（カーソル位置の最前面ウィンドウを検出・ハイライト） |
| `AreaSelect` | 範囲選択（Marquee でドラッグ選択 + ルーペ表示） |

#### 5.4.3 CaptureSettings

全メンバー nullable の位置パラメータレコード（47 個）。null の意味は利用方法で異なります。

- 初期設定（コンストラクタ）: null = CaptureSessionControl.AvaloniaUI のデフォルト値として扱う。
- 設定適用（Settings の setter）: null = 現在の設定値を維持する（部分更新）。

**反映先**: **計算に必要な寸法・対話パラメータ**はコアの設定型（`MarqueeOptions` / `LoupeOptions`。`FadeMaskDragRectOptionsMapper` が写像）へ、**描画にだけ必要な外観（色・枠線の太さ・表示可否）**は両 UI が共有する `CaptureSessionControl.UIBase` の写像（`CapturePaintMapper` が `MarqueeAppearance` / `LoupeAppearance` を組み立てる）へ反映します。

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
| `SizeLabelOverride` | `string?` | サイズラベルのテキスト明示指定。未指定（null）時はドメイン矩形から自動生成（後述） |
| `DisplayScaleScaling` | `double?` | サイズラベルの DisplayScale 表示の既定値（既定値 0.0＝未指定）。本 UI は矩形の中心が含まれるモニタの実スケーリングを表示する（マルチモニタ対応）ため、明示指定は表示に反映されない |
| `TextBackgroundColor` | `Color?` | サイズラベル（1 行目）の背景色 |
| `ShowSubLines` / `SubLineFontSize` / `SubLineColor` / `SubLineBackgroundColor` | `bool?` / `double?` / `Color?` / `Color?` | 操作ラベル（2 行目・背景はアルファ込み） |
| `InfoLabelVisible` / `InfoLabelColor` / `InfoLabelBackgroundColor` / `InfoLabelFontSize` | `bool?` / `Color?` / `Color?` / `double?` | 情報ラベル（3 行目・背景はアルファ込み） |

**サイズラベルの自動生成（ScreenSelect / WindowSelect / AreaSelect）**: 選択中の矩形（ドメイン = 画面物理 − Position。物理ピクセル）から直接生成する。
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

キャプチャ結果レコード。プロパティは「5.1.2 プロパティ」に記載の通りです。

## 6. 閉区間キャプチャ（重要な仕様）

**選択矩形の始点・終点のピクセルは切出し対象に含まれます（閉区間）**。ただし、カーソル位置から検出するスクリーン・ウィンドウの矩形は最初から全ピクセルを含むため、範囲選択と扱いが異なります。

Marquee の選択矩形（範囲選択）は半開区間（`幅 = 終点 − 始点`、.NET `Rect` と同一）で保持されます。そのまま切り出すと、**終点のピクセルが 1 つ欠けます**。そのため、範囲選択では `+1` して閉区間（始点・終点を含む）に変換します。

| 種別 | 変換 | 備考 |
| --- | --- | --- |
| AreaSelect | `+1`（閉区間化） | 半開区間の選択矩形を始点・終点を含む形へ変換する |
| ScreenSelect / WindowSelect | 変換なし | 検出した画面・ウィンドウの矩形は全ピクセルを含む。`+1` すると画面外・ウィンドウ外へ 1 px はみ出す |
| Crop | `+1`（閉区間化、画像の端でクランプ） | `CropBoundsMath.ComputeCropBounds` が単一所有する |
| 撮影（全種別） | 上表の変換 + 画像内へのクランプ | 確定時の切り出し矩形は `CaptureClipMath.ResolveCaptureRect` が単一所有する（種別の性質とクランプ・丸めを組み合わせた唯一の入口。画像の右端・下端で透明な余白が付かない） |
| Crop のサイズラベル | `WH: (右端 − 左端 + 1, 下端 − 上端 + 1)` | 閉区間のピクセル数で表示（`CropLabelTexts` が単一所有する） |

種別ごとの適用有無はコアの `CaptureTypeTraits.UsesClosedInterval` が単一所有します（AreaSelect のみ true）。

具体例: ドラッグ (10,10) → (50,50) の選択矩形は `Rect(10, 10, 40, 40)`（半開区間 [10,50)）ですが、切り出しは **`EsRectInt(10, 10, 41, 41)`（[10,50] の 41×41 ピクセル）** になります。

## 7. 動作フロー

### 7.1 StartCaptureAsync

1. オーナーウィンドウのスクリーン情報から DPI スケーリング係数を算出（`owner.Screens`。0 以下は 1.0 へ正規化）
2. `SelectionCaptureSession` を生成（`IWindowService` / `ICaptureService` / `IElementFinder` を `Platform.Native` から注入）
3. オーナーウィンドウをキャプチャ除外に設定（`SetCaptureExclusion`。オーバーレイの写り込み防止）→ 画面外へ退避（`HideOwnerWindow` が true の場合）
4. **仮想スクリーン全体をバックグラウンド + 5 秒タイムアウトでキャプチャ（全モード共通。切り出し元）**
5. `OverlayWindow` を表示（全画面透明・Topmost・Alt+Tab 非表示）
   - AreaSelect: Marquee ドラッグ選択 + Loupe（仮想スクリーン Bitmap 取得完了後に表示開始。Source 未設定の「画像外の色のみ」表示を防止）
   - Screen / WindowSelect: Pointer 移動で要素検出 → ハイライト。**クリック（左ボタン押下でハイライト固定 → 解放で確定）/ Enter キーで確定 / Esc キーでキャンセル**
6. 確定時: 選択矩形を種別に応じて切り出し矩形へ変換（AreaSelect は閉区間化。ScreenSelect / WindowSelect は変換なし）し、**枠選択中に事前切り出し済みのキャッシュを再利用**（不一致時のみ切り出し。枠の変更がない場合は切り出しスキップ）
   - 非アクティブ化 / Close → キャンセル（0x0 空結果）
7. 結果を `CaptureResult(Image, SourceBounds)` で TCS 経由返却 → `CaptureCompleted` イベント（同期・画像変更） → オーナー配置復帰 → オーナー除外解除

### 7.2 StartCropAsync

1. 切り出しの対象画像は `CropViewControl` が表示している画像（`CropViewControl.Source`）から解決する（画像を引数で受け取らない）。
   - 画像が未設定、または表示アダプタ（`EsBitmapAvaloniaImage`）以外の場合は開始しない（結果 null。実行中の再呼び出しと同じ表現）
   - 画像サイズが 0 の場合は `ArgumentException`（「サイズが 0 の画像は切り出せない」規則はコアの `CaptureClipMath.ValidateImage` が単一所有する）
   - 開始できた場合は `CropSession` を生成 → `RunAsync`
2. **複合コントロール `FadeMaskDragRectControl`（Marquee（外側の装飾を含む）+ Loupe の配線を内包）を `CropViewControl.Rects` へ配置**（1 コントロールで完結）
   - セッション開始時に `cropView.SetSelectionOverlayVisible(true)` を呼び、領域選択オーバーレイを可視化する
     （プレビュー表示時は非表示のため。Crop セッション中のみ暗転マスクを表示）
   - セッション終了時（確定・キャンセル）に `cropView.SetSelectionOverlayVisible(false)` で非表示 + 選択クリアに戻す
     （キャプチャ完了後・切出し後に Crop モード（暗転）が残るのを防ぐ）
3. 座標変換: `CoordTransform`（`CropViewControl` の `ViewportToContent` / `ContentToViewport` に委譲）で論理（元画像 px）↔ ビューポート DIP を 1 段変換し、**複合コントロールの `SetCoordinateTransform` で Marquee / Loupe の全コントロールに一括注入**（描画・表示時に動的評価 → ウィンドウサイズ変更・ズーム・スクロールに追従）
   - プレビュー状態（ScrollOffset / ZoomPercent）は保持しない（スクロール・ズームは内包する `CropViewControl`（`ZoomViewControl`）が所有）
   - ルーペの表示位置・サイズは LoupeControl が自己計算（カーソル右下 + オフセット + クランプ）
   - ビューポート変化の通知は `NotifyCropViewportChanged` に一本化（ScrollViewer 連携の `ScrollChanged` も、ScrollViewer 以外のビューポート実装（RenderTransform ベースのパン・ズーム等）のホスト通知も、同じ経路で再描画・ルーペ位置再計算を行う）
4. カーソル上書き抑止（Crop 中の十字カーソル維持）
5. マウスホイール操作（Crop 中）: CropViewControl がオーバーレイ上のホイールをビューポートへ転送する（Ctrl+ホイール = ズーム / 通常ホイール = パン）
6. **Crop 中の設定変更（`AppearanceSettings` setter）**: `CropSession.ApplySettings` で外観・クリップ設定を再適用（選択状態・ルーペ位置は維持）。キャプチャセッションはモーダル全画面のため対象外
7. **矩形外クリック・選択キャンセル**: 複合コントロールが `SelectionStarted` / `SelectionCancelled` を購読し、Marquee の選択状態（`Options.LogicalSelectionRect` / `Options.HasSelection`）をクリアして全面暗転（穴あけなし）に戻す（配線は複合コントロール内で完結）
8. 確定（`ConfirmRequested` → `CropBoundsMath.ComputeCropBounds`）→ 切り出し範囲をコア型 `EsRectInt` で返却
   - 切り出し範囲は**現在の画像**（確定時点の `CropViewControl.Source`）の範囲へクランプする（開始時のサイズで固定しない。実行中に画像が差し替わっても差し替え後の画像で切り出す）
   - キャンセル（`CancelRequested` / `Deactivated`）→ 0x0 の空矩形
   - 退化した選択（画像外・最小サイズ未満）→ 0x0 の空矩形（確定と同じ経路でキャンセルとして記録する）
   - 確定時に画像が無い（差し替えで消えた等）→ 0x0 の空矩形（キャンセルとして扱う）

## 8. 制約事項

- `StartCaptureAsync` / `StartCropAsync` は **UI スレッドから呼び出す**こと。
- 実行中の再呼び出しは `null` を返します（既存セッションを継続）。呼び出し側は null を「進行中のため開始できない」と解釈してください。
- `Dispose()` 後の公開操作は `ObjectDisposedException` を投げます。
- `StartCropAsync` の `cropView` は `CropViewControl`（ズーム + 領域選択 + ルーペを内包した複合コントロール）です。選択矩形のオーバーレイ（Marquee / Loupe）はセッションが `CropViewControl.Rects` へ配置するため、利用側での配置は不要です（ズーム・パン・Fit・座標変換は内包する `ZoomViewControl` が単一所有）。切り出しの対象画像は `cropView.Source` に `EsBitmapAvaloniaImage` として設定しておく必要があります（未設定・非対応の場合は開始せず結果 null）。
- `CaptureSettings` の数値メンバーは `Validate()` で範囲チェックされ、範囲外は `ArgumentOutOfRangeException` を投げます。
- キャプチャ画像へのオーバーレイ写り込みは `IWindowService.SetCaptureExclusion`（Windows 10 2004+）で防止します。それ以前の Windows では写り込みが発生する可能性があります。
