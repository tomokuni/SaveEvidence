# Parts.CapturePreview.ControlAvaloniaUI

## 目次

- [1. 概要](#1-概要)
- [2. 機能一覧](#2-機能一覧)
- [3. システム要件](#3-システム要件)
- [4. インストール](#4-インストール)
- [5. クイックスタート](#5-クイックスタート)
  - [5.1 キャプチャ（ScreenSelect / WindowSelect / AreaSelect）](#51-キャプチャscreenselect--windowselect--areaselect)
  - [5.2 切出し（Crop）](#52-切出しcrop)
  - [5.3 ズーム（切り出しビューの内部ビューポート）](#53-ズーム切り出しビューの内部ビューポート)
  - [5.4 設定の適用](#54-設定の適用)
  - [5.5 キャンセル / 確定](#55-キャンセル--確定)
- [6. 動作フロー](#6-動作フロー)
  - [6.1 StartCaptureAsync](#61-startcaptureasync)
  - [6.2 StartCropAsync](#62-startcropasync)
- [7. 収録コントロール](#7-収録コントロール)
  - [7.1 CropViewControl（切り出しビュー）](#71-cropviewcontrol切り出しビュー)
  - [7.2 SelectionOverlayControl（領域選択）](#72-selectionoverlaycontrol領域選択)
  - [7.3 ZoomViewControl（切り出しビューの画像ビューポート）](#73-zoomviewcontrol切り出しビューの画像ビューポート)
- [8. 依存関係](#8-依存関係)
- [9. ドキュメント](#9-ドキュメント)
- [10. プロジェクト構成](#10-プロジェクト構成)
- [11. テスト](#11-テスト)
- [12. ライセンス](#12-ライセンス)

---

## 1. 概要

画面キャプチャ（全画面 / ウィンドウ選択 / 範囲選択）と切出し（Crop）を提供する Avalonia UI ライブラリです。 **Facade パターン**の単一エントリポイント `CaptureSessionControl` から利用します。

本アセンブリは、セッションの Facade に加えて 2 つの公開複合コントロール（`CropViewControl`（切り出しビュー）と `SelectionOverlayControl`（領域選択））を同一アセンブリ内に収録します。 `CropViewControl` は画像ビューポート `ZoomViewControl`（[7.3](#73-zoomviewcontrol切り出しビューの画像ビューポート)）を内包します（[7. 収録コントロール](#7-収録コントロール)）。

## 2. 機能一覧

- **3 種のキャプチャモード**: `ScreenSelect`（全画面）/ `WindowSelect`（ウィンドウ選択）/ `AreaSelect`（範囲選択）
- **切出し（Crop）**: プレビュー画像上の選択矩形で元画像から切り出し範囲を取得
- **Facade パターン**: 状態は複合コントロール `SelectionOverlayControl`（Marquee（外側の装飾を含む）+ Loupe を内包）と Platform.AvaloniaUI / EsUtil.Platform.Core（`Native` の Win32 実装を含む）が保持し、`CaptureSessionControl` は「組み立て・ライフサイクル」のみを担う薄い統括者
- **Task ベース API**: 結果は `Task` で返却（`await` で直感的に利用）
- **キャプチャ完了イベント**: `CaptureCompleted`（同期型）で画像変更 → オーナー再表示の順序を保証
- **イベント駆動**: 選択状態の同期は各コントロールの公開イベントのみ（ポーリングなし）
- **再入禁止ガード**: 実行中の再呼び出しは `null` を返す（既存セッションを継続）
- **部分更新設定**: `CaptureSettings` は全メンバー nullable（null = デフォルト値 or 現在値維持）で部分更新が可能
- **共通規則の単一所有**: 設定のマージ・実行の排他・モニタ倍率・切り出し範囲・ハイライト状態・キー操作・クリック確定・画像の保持と取得の待ち方・ログ定義は `Parts.CapturePreview.Core` が所有し、本 UI は委譲する
- **プラットフォーム抽象**: キャプチャ・要素検出・ウィンドウ操作は EsUtil.Platform.Core の `Native`（抽象）と `Native.Win`（Windows 実装）を介して注入（テスト可能・プラットフォーム非依存）
- **切り出しビュー（`CropViewControl`）**: 画像ビューポート（拡大・パン・Fit）+ 領域選択オーバーレイ + 外部提供矩形を 1 つの複合コントロールにまとめる
- **領域選択（`SelectionOverlayControl`）**: 暗転マスク + 選択矩形 + ルーペを 1 つの複合コントロールにまとめ、配線と状態同期を内側で完結させる
- **ズーム・パン（切り出しビューの画像ビューポート）**: `CropViewControl` が内包する `ZoomViewControl` が、ズーム（⊖⊕ の 23 段階 / Ctrl+ホイール ×1.1）・パン（ドラッグ / ホイール / スクロールバー）・Fit 表示・座標変換を担う（[7.3](#73-zoomviewcontrol切り出しビューの画像ビューポート)）

## 3. システム要件

- .NET 10（`net10.0-windows10.0.17763.0`）
- Avalonia 12.1 以降
- C# 14

## 4. インストール

現在は NuGet パッケージではなく、プロジェクト参照で利用します。

```xml
<ProjectReference Include="..\Parts.CapturePreview.ControlAvaloniaUI\src\Parts.CapturePreview.ControlAvaloniaUI.csproj" />
```

必要な名前空間:

```csharp
using EsUtil.SaveEvidence.Parts.CapturePreview.AvaloniaUI.Session;
```

## 5. クイックスタート

### 5.1 キャプチャ（ScreenSelect / WindowSelect / AreaSelect）

```csharp
var capture = new CaptureSessionControl(); // 既定設定

// 全画面キャプチャ
var result = await capture.StartCaptureAsync(CaptureType.ScreenSelect, window);
if (result?.Image is { } image)
{
    // image: キャプチャ画像 / result.SourceBounds: キャプチャ領域（仮想画面座標）
}
```

### 5.2 切出し（Crop）

```csharp
// 切り出しの対象画像は、プレビューが表示している画像（previewControl.Source）から解決される
// （画像を別引数で渡さない。表示している画像と切り出す画像を二重所有しない）
// 切り出し中のホイール操作（Ctrl+ホイールのズーム・通常ホイールのパン）は CropViewControl が内部で完結する（上位の配線は不要）
var cropRect = await capture.StartCropAsync(previewControl, hostWindow);
if (cropRect is { } rect && rect.Width > 0 && rect.Height > 0)
{
    // rect: 元画像上の切り出し範囲（論理座標。始点・終点のピクセルを含む閉区間）
}
```

### 5.3 ズーム（切り出しビューの内部ビューポート）

切り出しビュー（`CropViewControl`）は画像ビューポート（`ZoomViewControl`）を内包し、ズーム操作を委譲します。 ホストは `CropViewControl` の `Source` を設定し、`ZoomChanged` / `ViewportChanged` を購読します。

```csharp
// ズーム率の変化をスライダと ⊖⊕ ボタンの状態へ同期する
cropView.ZoomChanged += (_, e) =>
{
    zoomSlider.Value = e.ZoomPercent;       // Fit 中は実効倍率
    zoomInButton.IsEnabled = e.CanZoomIn;
    zoomOutButton.IsEnabled = e.CanZoomOut;
};

cropView.Source = image;      // 設定すると Fit 表示へリセット
cropView.ZoomPercent = 150;   // 固定倍率（0 以下は無視。 Fit へは ZoomFit）
cropView.ZoomIn();            // 23 段階の次のレベルへ
cropView.ZoomFit();           // Fit 表示（ビューポートに収まる Uniform 倍率）へ
```

ズーム遷移のパラメータ（`MinZoom` / `MaxZoom` / `WheelFactor` / `ButtonZoomLevels`）は `Parts.CapturePreview.Core` の `ZoomOptions`（検証付き不変値）に集約され、既定は 10〜500% の 23 段階・ホイール ×1.1 です（`CropViewControl` は既定値を使用します）。

### 5.4 設定の適用

```csharp
var capture = new CaptureSessionControl(new CaptureSettings
{
    LoupeZoomLevel = 4,
    MinSelectionSize = 6,
    BorderColor = System.Drawing.Color.Red,
});

// 部分更新（null 以外のみ反映・残りは現在値維持）
capture.AppearanceSettings = capture.AppearanceSettings with { LoupeOffset = 24.0 };
```

### 5.5 キャンセル / 確定

```csharp
capture.Cancel();   // 進行中セッションをキャンセル（0x0 の空結果で完了）
capture.Confirm();  // Crop のみ有効（Enter キー相当）
capture.Dispose();  // リソース解放（以降の操作は ObjectDisposedException）
```

## 6. 動作フロー

### 6.1 StartCaptureAsync

1. オーナーウィンドウのスクリーン情報から DPI スケーリング係数を算出（0 以下は 1.0 へ正規化）
2. `CaptureOverlaySession` を生成（Platform.AvaloniaUI からサービスを注入）
3. オーナーウィンドウをキャプチャ除外に設定（`SetCaptureExclusion`）→ 画面外へ退避（`HideOwnerWindow`。 非表示ではなく `Opacity=0` + 位置退避）
4. **仮想スクリーン全体をバックグラウンド + タイムアウト付きでキャプチャ（全モード共通の切り出し元）**
5. `CaptureOverlayWindow` を表示（全画面透明・Topmost・Alt+Tab 非表示）
   - AreaSelect: Marquee ドラッグ選択 + Loupe
   - Screen / WindowSelect: カーソル位置の要素検出 → ハイライト
6. **枠選択中に事前切り出し**（ハイライト型のホバー時。 同じ矩形ならスキップ・キャッシュ再利用。 事前切り出しの有無はコアの `CaptureTypeTraits.IsHighlighted` で決まる）
7. **確定時: 事前切り出しキャッシュを再利用**（不一致時のみ切り出し）
8. 結果を `CaptureResult` で返却 → `CaptureCompleted`（画像変更） → オーナー再表示 → 除外解除

### 6.2 StartCropAsync

1. 切り出しの対象画像は `CropViewControl` が表示している画像（`CropViewControl.Source`）から解決する（画像を引数で受け取らない）。 画像が未設定・表示アダプタ（`EsBitmapAvaloniaImage`）以外の場合は開始しない（結果 null）→ 開始できた場合は `CropSession` を生成 → `RunAsync`
2. **SelectionOverlayControl**（Marquee + Loupe の配線を内包する複合コントロール。 暗転マスクは Marquee の外側の装飾）を `CropViewControl.Rects` へ配置する
3. 座標変換: `CoordTransform`（`CropViewControl` の `ClientToSource` / `SourceToClient` に委譲）で論理（元画像 px）↔ ビューポート DIP を変換し、**`CropViewControl.SetCoordTransform` で内包コントロール（Marquee / Loupe）に一括注入**（描画・表示時に動的評価 → ウィンドウサイズ変更・ズーム・スクロールに追従）
4. マウスホイール操作（Crop 中）: CropViewControl がオーバーレイ上のホイールをビューポート（ズーム・パン）へ転送する
5. **ビューポート変化の通知**: ホストがプレビューのパン・ズームを検知したら `NotifyViewportChanged()` を呼ぶ（暗転マスク・選択枠・ルーペが追従。 ScrollViewer 連携のスクロールも同じ経路で処理）
6. 確定 → 論理 Rect（**閉区間** = 始点・終点を含む）を `EsRectInt`（コア型）で返却 / キャンセル → 0x0 の空矩形
   - 確定時の切り出し範囲は**現在の画像**（確定時点の `CropViewControl.Source`）の範囲へクランプする（実行中に画像が差し替わっても差し替え後の画像で切り出す。 画像が消えた場合はキャンセルとして扱う）

## 7. 収録コントロール

本アセンブリは、セッションの Facade に加えて 2 つの公開複合コントロールと、切り出しビューが内包する画像ビューポートを収録します。 いずれも UI 非依存の共通規則を `Parts.CapturePreview.Core` に置き、Avalonia 固有の描画・入力・配置だけを持ちます。

### 7.1 CropViewControl（切り出しビュー）

画像の表示（拡大・パン・Fit）・領域選択オーバーレイ・外部提供矩形の描画を 1 つのコントロールにまとめた複合コントロールです（Facade / Composite パターン）。 ホストは画像ソースの設定・選択の受け渡し・ズーム操作に専念できます。

- **複合構造**: 画像ビューポート（`ZoomViewControl`）+ 領域選択オーバーレイ（`SelectionOverlayControl`）+ 矩形オーバーレイ + 強調オーバーレイを内包
- **領域選択**: `SelectionMode`（`SelectionOverlayMode`。 既定 `Crop`）/ `SelectionOverlayOptions`（コアの設定）/ `SelectionOverlayMarqueeAppearance` / `SelectionOverlayLoupeAppearance`（描画にだけ必要な外観）を内包する `SelectionOverlayControl` へ委譲する。 オーバーレイの可視性とヒットテストは `SetSelectionOverlayVisible` で制御する（Crop セッションの開始・終了時にホストが切替。 初期状態は非表示・ヒットテスト不可）
- **外部提供矩形**: `Rects`（`IReadOnlyList<OverlayRect>`）をビューポートとルーペの両方へ描画する。 矩形情報はコアの `OverlayRect`（矩形・破線種類・前景色・背景色・線幅。 色は `System.Drawing.Color`）
- **強調（ハイライト）**: `HighlightSourceRect`（対象座標）+ `HighlightAppearance`（外観）で「対象矩形を目立たせる」描画をホストが行える（自動切り出しのホバー強調等）。 対象座標 → ビューポート座標の写像とビューポート変化の追従は本コントロールが単一所有する（ホストは座標変換を持たない）
- **座標変換**: `SourceToClient` / `ClientToSource` / `ImageRectViewport`（Fit 中央寄せの原点補正を含む）。 領域選択オーバーレイ上のホイールは本コントロールがビューポート（ズーム・パン）へ転送する

主な公開メンバー: `Source` / `Rects` / `HighlightSourceRect` / `HighlightAppearance` / `SelectionMode` / `SelectionOverlayOptions` / `SelectionOverlayMarqueeAppearance` / `SelectionOverlayLoupeAppearance` / `Appearance` / `CenterAlign` / `ZoomPercent` / `CanZoomIn` / `CanZoomOut` / `HasSelection` / `SelectionSourceRect` / `SizeLabelOverrideProvider` / `OperationTextProvider` / `InfoTextProvider` / `Log` / `ClientToSourceScaling` / `LoupeClientToPixelScaling`、メソッド `ZoomIn` / `ZoomOut` / `ZoomFit` / `SetSelection` / `ClearSelection` / `SetSelectionOverlayVisible` / `SetCoordTransform` / `RequestConfirm` / `RequestCancel` / `FocusMarquee` / `SetLoupeSource` / `ClearLoupe` / `NotifyViewportChanged` / `ImageRectViewport` / `SourceToClient` / `ClientToSource`、イベント `ConfirmRequested` / `CancelRequested` / `ZoomChanged` / `ViewportChanged`。

### 7.2 SelectionOverlayControl（領域選択）

選択矩形（ドラッグ）・暗転マスク・ルーペを 1 つのコントロールにまとめ、配線と状態同期を内側で完結させる複合コントロールです。 ホストは `Mode` の設定と状態の受け取りだけで利用できます。

| モード | 選択矩形 | ルーペ | 全面暗転 | 用途 |
| --- | --- | --- | --- | --- |
| `Crop` | 有効 | 有効 | 有効 | プレビュー内の切出し |
| `AreaSelect` | 有効 | 有効 | 有効 | 全画面オーバーレイの領域選択 |
| `ScreenSelect` | 無効 | 無効 | 無効 | スクリーン選択（ハイライトのみ） |
| `WindowSelect` | 無効 | 無効 | 無効 | ウィンドウ選択（ハイライトのみ） |

主な公開メンバー: `Mode` / `Options`（コアの `SelectionOverlayOptions`）/ `MarqueeAppearance` / `LoupeAppearance` / `HasSelection` / `SelectionSourceRect` / `HighlightSourceRect` / `ShowFullOverlay` / `LoupeCenterPoint` / `SizeLabelOverrideProvider` / `OperationTextProvider` / `InfoTextProvider` / `Log` / `ClientToSourceScaling` / `DisplayScaleScaling` / `LoupeClientToPixelScaling`、メソッド `SetCoordTransform` / `SetSelection` / `ClearSelection` / `SetLoupeSource` / `SetLoupePosition` / `ClearLoupe` / `RequestConfirm` / `RequestCancel` / `FocusMarquee` / `NotifyViewportChanged` / `RequestRedraw`、イベント `ConfirmRequested` / `CancelRequested` / `LoupePositionMoved` / `LoupeRendered`。

### 7.3 ZoomViewControl（切り出しビューの画像ビューポート）

画像の拡大・パン・Fit 表示を担う画像ビューポートです。 **`CropViewControl` が内包する内部部品**であり、独立した汎用コントロールではなく切り出しビューの表示を成立させるために存在します（`CropViewControl` が `Source` / `ZoomPercent` / `ZoomIn` / `ZoomOut` / `ZoomFit` / `ClientToSource` 等を委譲し、ホストは通常 `CropViewControl` 経由で利用します）。 判断（実効ズーム率の導出・可否・遷移の適用・パン・スクロールバー）は UI 非依存コア `Parts.CapturePreview.Core` の `ZoomViewSession` が単一所有します。

- **ズーム遷移**: ⊖⊕ の 23 段階（10, 12, 15, 20, 25, 33, 40, 50, 62, 75, 80, 90, 100, 110, 125, 150, 200, 250, 300, 350, 400, 450, 500）/ Ctrl+ホイールの ×1.1・÷1.1 連続遷移（現在値を基準に四捨五入し、10〜500% へクランプ）
- **Fit 表示**: ビューポートに収まる Uniform 倍率（アスペクト比維持）。 実効倍率は `min(ビューポート幅 ÷ 画像幅, ビューポート高さ ÷ 画像高さ) × 100` で計算し、リサイズに追従する。 段階遷移の際に Fit 実効倍率が間に挟まる場合はそれを経由する
- **ズーム中心維持**: マウスポインタ位置（画像外はビューポート中心）を中心に拡大縮小し、中心のコンテンツ座標を不変に保つ
- **パン**: マウスドラッグ（grab 方式）/ ホイール（縦・Shift で横）/ スクロールバー（オーバーレイ表示。 画像がビューポートより大きい軸のみ）
- **座標系**: コンテンツ座標（元画像ピクセル。 ズーム率に依存しない不変座標）とビューポート座標（コントロールのローカル DIP）。 `CropViewControl.SourceToClient` / `ClientToSource` は画像表示矩形の原点基準（Fit 中央寄せ補正込み）で変換する
- **市松模様**: 画像の透明部分の背景を `Appearance`（コアの `ZoomViewAppearance`）で指定する。 描くのは画像の表示矩形の内側だけで、画像の外（ペーストボード）は下地色の単色になる

| 操作 | 内容 |
| --- | --- |
| ⊕ / ⊖ ボタン（`ZoomIn` / `ZoomOut`） | 23 段階を順に遷移（リスト先頭・末尾で停止） |
| Ctrl + ホイール | マウスポインタ位置を中心に ×1.1 / ÷1.1 で連続遷移 |
| マウスドラッグ | パン（掴んだ位置がポインタに追従） |
| ホイール / Shift + ホイール | 縦パン / 横パン |
| Fit（`ZoomFit`） | ビューポートに収まる Uniform 倍率（唯一の Fit 移行経路） |

| 設定（コアの `ZoomOptions`） | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `MinZoom` / `MaxZoom` | `double` | `10` / `500` | 固定倍率の下限・上限（%） |
| `WheelFactor` | `double` | `1.1` | Ctrl+ホイールのズーム倍率 |
| `ButtonZoomLevels` | `IReadOnlyList<double>` | 23 段階 | ⊖⊕ のズームレベル集合 |

`ZoomChanged`（`ZoomChangedEventArgs`。 実効ズーム率・Fit 状態・可否）と `ViewportChanged`（パン・ズーム・表示状態）を通知し、ホストは `CropViewControl` 経由で購読します。 **表示文字列の生成（書式・丸め・% 表記）はホストの責務**です（`ZoomPercent` は実効ズーム率の数値を通知するのみ。 Fit 判定は `IsFit` で行う。 `ZoomPercent` は 0 にならない）。

## 8. 依存関係

| 参照先 | 利用機能 |
| --- | --- |
| `Parts.CapturePreview.Core` | 両 UI 共通の規則（`CaptureSettings` / `CaptureResult` / `CaptureSettingsMerge` / `SelectionOverlayOptionsMapper` / `CropBoundsMath` / `CropClipMath` / `CaptureImageHolder` / `CaptureVirtualScreen` / `CaptureHighlightSession` / `CaptureKeyActions` / `CaptureClickSelection` / `CaptureLabelTexts` / `CropLabelTexts` / `CaptureTypeTraits` / `MonitorScalingMath` / `CaptureSessionSlot` / `CaptureOwnerScope` / `OverlayRect` / `OverlayLineStyle` / `SelectionOverlayMode` / `SelectionOverlayOptions` / ログ定義）と、切り出しビューの画像ビューポートの状態と判断（`ZoomViewSession` / `ZoomOptions` / `ZoomState` / `ZoomViewAppearance` / `CapturePreviewLogMessage`）。 さらに `Presentation/` に設定 → 外観の写像（`CapturePaintMapper`）・子コントロールへ適用する値の解決（`SelectionOverlayChildValues`）・付加情報ラベルの配置（`SelectionOverlayLabelRenderer`）を持つ。 UI 非依存 |
| `Platform.AvaloniaUI` | サービス生成 / `ScreenConverter` / 色の相互変換（`ColorAvaloniaInterop`）。 抽象（`ICaptureService` / `IWindowService` / `IElementFinder`）は `EsUtil.Platform.Core.Native`（既定実装は `Native.Win`）、`ICoordTransform` / `CoordTransform` は `EsUtil.Platform.Core` |
| `EsUtil.Feature.Marquee.ControlAvaloniaUI` | `MarqueeControl`（選択矩形と、その外側に描く装飾（暗転マスク・外枠・ハイライト）） |
| `EsUtil.Feature.Loupe.ControlAvaloniaUI` | `LoupeControl`（拡大鏡）/ `LoupeRenderedEventArgs` |
| `EsUtil.Feature.Marquee.Presenter` | `MarqueeAppearance`（描画にだけ必要な外観。 内包コントロール（`EsUtil.Feature.Marquee.ControlAvaloniaUI`）の推移的依存） |
| `EsUtil.Feature.Loupe.Core` | ルーペの配置計算（内包コントロールの推移的依存） |
| `EsUtil.Feature.Loupe.Presenter` | `LoupeAppearance`（描画にだけ必要な外観。 内包コントロールの推移的依存） |

`CropViewControl` / `SelectionOverlayControl` は本アセンブリが公開する複合コントロールです（別プロジェクトではありません）。 `ZoomViewControl` は `CropViewControl` が内包する画像ビューポートです（独立した汎用コントロールとしてではなく、切り出しビューの内部部品として収録します）。 撮影セッション（`CaptureSessionControl` / `CaptureOverlaySession` / `CaptureOverlayWindow`）は内包コントロール（`MarqueeControl` / `LoupeControl`）を直接参照しません（窓口は `SelectionOverlayControl` のみ。 公開型のカプセル化）。

## 9. ドキュメント

- [外部機能仕様書](docs/SPEC_API.md)
- [システム仕様書](docs/SPEC_System.md)

## 10. プロジェクト構成

```text
Parts.CapturePreview.ControlAvaloniaUI/
├── src/
│   ├── Parts.CapturePreview.ControlAvaloniaUI.csproj      # ライブラリプロジェクト（Avalonia.Desktop / Logging.Abstractions）
│   ├── Crop/                       # 切り出しビュー（画像ビューポート + 矩形オーバーレイ + 強調オーバーレイ）
│   │   ├── CropViewControl.cs      # 公開複合コントロール
│   │   └── CropViewImage.cs        # 画像の表示アダプタ（internal）
│   ├── SelectionOverlay/           # 領域選択（暗転マスク + 選択矩形 + ルーペ）
│   │   ├── SelectionOverlayControl.cs             # 公開複合コントロール
│   │   ├── SelectionConfirmedEventArgs.cs         # 確定要求のイベント引数（公開クラス）
│   │   └── LoupePositionEventArgs.cs              # ルーペ位置のイベント引数（公開クラス）
│   ├── Session/                    # セッション（Facade と撮影・切り出しの実装）
│   │   ├── CaptureSessionControl.cs               # 公開 Facade（統括者。組み立て・配線・ライフサイクル）
│   │   ├── CaptureCompletedEventArgs.cs           # 完了通知のイベント引数（公開クラス）
│   │   ├── CaptureOverlaySession.cs               # 撮影セッション（internal。1 セッション = 1 インスタンス）
│   │   ├── CaptureOverlayWindow.axaml             # 撮影オーバーレイのウィンドウ（internal）
│   │   ├── CaptureOverlayWindow.axaml.cs
│   │   ├── CaptureOverlayWindow.Capture.cs
│   │   ├── CaptureOverlayWindow.Highlight.cs
│   │   └── CropSession.cs                         # 切り出しセッション（internal）
│   └── ZoomView/                   # 切り出しビューの画像ビューポート
│       ├── ZoomViewControl.cs / ZoomableView.cs / CheckerboardView.cs / ZoomChangedEventArgs.cs
├── sample/                       # サンプルアプリ（sample/README.md 参照）
└── tests/                        # 単体テスト（セッション + 収録コントロール）
    ├── Crop/ / SelectionOverlay/ / Session/ / ZoomView/
    └── AvaloniaHeadlessGroup.cs / TestAppBuilder.cs
```

`CaptureType`（キャプチャ種別）は UI 非依存の共通基盤 `EsUtil.Platform.Core`（名前空間 `EsUtil.Platform.Core`）が提供します。 利用側は `using EsUtil.Platform.Core;` を追加してください。

`CaptureSettings`（設定）と `CaptureResult`（結果）は UI 非依存の共通基盤 `Parts.CapturePreview.Core`（名前空間 `EsUtil.SaveEvidence.Parts.CapturePreview.Core.Capture`）が提供します（切り出し矩形の計算 `CropBoundsMath` は同プロジェクトの `EsUtil.SaveEvidence.Parts.CapturePreview.Core.Crop`）。 利用側はカテゴリごとの名前空間（`Capture` / `Crop` / `Overlay` / `SelectionOverlay` / `ZoomView`）を `using` してください。 **色は `System.Drawing.Color`** で指定します（従来の Avalonia の色型から変更）。

設定から描画にだけ必要な外観（`MarqueeAppearance` / `LoupeAppearance`）への写像は、両 UI が共有する `Parts.CapturePreview.Core`（`CapturePaintMapper`）が提供します。 利用側は `using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Presentation;` を追加してください。

**撮影オーバーレイ（`StartCaptureAsync`）も同じ写像で外観（色）を適用します**（切り出しと同じ見た目になる。 適用しないと設定した色が反映されない）。

## 11. テスト

`src/Parts.CapturePreview.ControlAvaloniaUI/tests` が本プロジェクトのテストを持ちます（xUnit v3）。 実行手順と合否基準は [検証手順.md](../../.github/検証手順.md) が定めます。

`powershell
dotnet test src/Parts.CapturePreview.ControlAvaloniaUI/tests/Parts.CapturePreview.ControlAvaloniaUI.Tests.csproj
`

## 12. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
