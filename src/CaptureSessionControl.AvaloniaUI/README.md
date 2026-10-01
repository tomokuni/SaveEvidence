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
  - [7. 収録コントロール](#7-収録コントロール)
    - [7.1 CropViewControl（切り出しビュー）](#71-cropviewcontrol切り出しビュー)
    - [7.2 FadeMaskDragRectControl（領域選択）](#72-fademaskdragrectcontrol領域選択)
  - [8. 依存関係](#8-依存関係)
  - [9. ドキュメント](#9-ドキュメント)
  - [10. プロジェクト構成](#10-プロジェクト構成)
  - [11. ライセンス](#11-ライセンス)

---

## 1. 概要

画面キャプチャ（全画面 / ウィンドウ選択 / 範囲選択）と切出し（Crop）を提供する Avalonia UI ライブラリです。 **Facade パターン**の単一エントリポイント `CaptureSessionControl` から利用します。

本アセンブリは、セッションの Facade に加えて 2 つの公開複合コントロール（`CropViewControl`（切り出しビュー）と `FadeMaskDragRectControl`（領域選択））を同一アセンブリ内に収録します（[7. 収録コントロール](#7-収録コントロール)）。

## 2. 機能一覧

- **3 種のキャプチャモード**: `ScreenSelect`（全画面）/ `WindowSelect`（ウィンドウ選択）/ `AreaSelect`（範囲選択）
- **切出し（Crop）**: プレビュー画像上の選択矩形で元画像から切り出し範囲を取得
- **Facade パターン**: 状態は複合コントロール `FadeMaskDragRectControl`（Marquee（外側の装飾を含む）+ Loupe を内包）と Platform.AvaloniaUI / Platform.Core（`Native` の Win32 実装を含む）が保持し、`CaptureSessionControl` は「組み立て・ライフサイクル」のみを担う薄い統括者
- **Task ベース API**: 結果は `Task` で返却（`await` で直感的に利用）
- **キャプチャ完了イベント**: `CaptureCompleted`（同期型）で画像変更 → オーナー再表示の順序を保証
- **イベント駆動**: 選択状態の同期は各コントロールの公開イベントのみ（ポーリングなし）
- **再入禁止ガード**: 実行中の再呼び出しは `null` を返す（既存セッションを継続）
- **部分更新設定**: `CaptureSettings` は全メンバー nullable（null = デフォルト値 or 現在値維持）で部分更新が可能
- **共通規則の単一所有**: 設定のマージ・実行の排他・モニタ倍率・切り出し範囲・ハイライト状態・キー操作・クリック確定・画像の保持と取得の待ち方・ログ定義は `CaptureSessionControl.Core` が所有し、本 UI は委譲する
- **プラットフォーム抽象**: キャプチャ・要素検出・ウィンドウ操作は Platform.Core の `Native`（抽象）と `Native.Win`（Windows 実装）を介して注入（テスト可能・プラットフォーム非依存）
- **切り出しビュー（`CropViewControl`）**: 画像ビューポート（拡大・パン・Fit）+ 領域選択オーバーレイ + 外部提供矩形を 1 つの複合コントロールにまとめる
- **領域選択（`FadeMaskDragRectControl`）**: 暗転マスク + 選択矩形 + ルーペを 1 つの複合コントロールにまとめ、配線と状態同期を内側で完結させる

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
using EsUtil.SaveEvidence.CaptureSession.AvaloniaUI;
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
// 切り出しの対象画像は、プレビューが表示している画像（previewControl.Source）から解決される
// （画像を別引数で渡さない。表示している画像と切り出す画像を二重所有しない）
// 切り出し中のホイール操作（Ctrl+ホイールのズーム・通常ホイールのパン）は CropViewControl が内部で完結する（上位の配線は不要）
var cropRect = await capture.StartCropAsync(previewControl, hostWindow);
if (cropRect is { } rect && rect.Width > 0 && rect.Height > 0)
{
    // rect: 元画像上の切り出し範囲（論理座標。始点・終点のピクセルを含む閉区間）
}
```

### 設定の適用

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

### キャンセル / 確定

```csharp
capture.Cancel();   // 進行中セッションをキャンセル（0x0 の空結果で完了）
capture.Confirm();  // Crop のみ有効（Enter キー相当）
capture.Dispose();  // リソース解放（以降の操作は ObjectDisposedException）
```

## 6. 動作フロー

### StartCaptureAsync

1. オーナーウィンドウのスクリーン情報から DPI スケーリング係数を算出（0 以下は 1.0 へ正規化）
2. `SelectionCaptureSession` を生成（Platform.AvaloniaUI からサービスを注入）
3. オーナーウィンドウをキャプチャ除外に設定（`SetCaptureExclusion`）→ 画面外へ退避（`HideOwnerWindow`。 非表示ではなく `Opacity=0` + 位置退避）
4. **仮想スクリーン全体をバックグラウンド + タイムアウト付きでキャプチャ（全モード共通の切り出し元）**
5. `OverlayWindow` を表示（全画面透明・Topmost・Alt+Tab 非表示）
   - AreaSelect: Marquee ドラッグ選択 + Loupe
   - Screen / WindowSelect: カーソル位置の要素検出 → ハイライト
6. **枠選択中に事前切り出し**（ハイライト型のホバー時。 同じ矩形ならスキップ・キャッシュ再利用。 事前切り出しの有無はコアの `CaptureTypeTraits.IsHighlighted` で決まる）
7. **確定時: 事前切り出しキャッシュを再利用**（不一致時のみ切り出し）
8. 結果を `CaptureResult` で返却 → `CaptureCompleted`（画像変更） → オーナー再表示 → 除外解除

### StartCropAsync

1. 切り出しの対象画像は `CropViewControl` が表示している画像（`CropViewControl.Source`）から解決する（画像を引数で受け取らない）。 画像が未設定・表示アダプタ（`EsBitmapAvaloniaImage`）以外の場合は開始しない（結果 null）→ 開始できた場合は `CropSession` を生成 → `RunAsync`
2. **FadeMaskDragRectControl**（Marquee + Loupe の配線を内包する複合コントロール。 暗転マスクは Marquee の外側の装飾）を `CropViewControl.Rects` へ配置する
3. 座標変換: `CoordTransform`（`CropViewControl` の `ViewportToContent` / `ContentToViewport` に委譲）で論理（元画像 px）↔ ビューポート DIP を変換し、**`CropViewControl.SetCoordTransform` で内包コントロール（Marquee / Loupe）に一括注入**（描画・表示時に動的評価 → ウィンドウサイズ変更・ズーム・スクロールに追従）
4. マウスホイール操作（Crop 中）: CropViewControl がオーバーレイ上のホイールをビューポート（ズーム・パン）へ転送する
5. **ビューポート変化の通知**: ホストがプレビューのパン・ズームを検知したら `NotifyCropViewportChanged()` を呼ぶ（暗転マスク・選択枠・ルーペが追従。 ScrollViewer 連携のスクロールも同じ経路で処理）
6. 確定 → 論理 Rect（**閉区間** = 始点・終点を含む）を `EsRectInt`（コア型）で返却 / キャンセル → 0x0 の空矩形
   - 確定時の切り出し範囲は**現在の画像**（確定時点の `CropViewControl.Source`）の範囲へクランプする（実行中に画像が差し替わっても差し替え後の画像で切り出す。 画像が消えた場合はキャンセルとして扱う）

## 7. 収録コントロール

本アセンブリは、セッションの Facade に加えて 2 つの公開複合コントロールを収録します。 どちらも UI 非依存の共通規則を `CaptureSessionControl.Core` に置き、Avalonia 固有の描画・入力・配置だけを持ちます。

### 7.1 CropViewControl（切り出しビュー）

画像の表示（拡大・パン・Fit）・領域選択オーバーレイ・外部提供矩形の描画を 1 つのコントロールにまとめた複合コントロールです（Facade / Composite パターン）。 ホストは画像ソースの設定・選択の受け渡し・ズーム操作に専念できます。

- **複合構造**: 画像ビューポート（`ZoomViewControl`）+ 領域選択オーバーレイ（`FadeMaskDragRectControl`）+ 矩形オーバーレイを内包
- **領域選択**: `SelectionMode`（`FadeMaskDragRectMode`。 既定 `Crop`）/ `SelectionOverlayOptions`（コアの設定）/ `SelectionOverlayMarqueeAppearance` / `SelectionOverlayLoupeAppearance`（描画にだけ必要な外観）を内包する `FadeMaskDragRectControl` へ委譲する。 オーバーレイの可視性とヒットテストは `SetSelectionOverlayVisible` で制御する（Crop セッションの開始・終了時にホストが切替。 初期状態は非表示・ヒットテスト不可）
- **外部提供矩形**: `Rects`（`IReadOnlyList<OverlayRect>`）をビューポートとルーペの両方へ描画する。 矩形情報はコアの `OverlayRect`（矩形・破線種類・前景色・背景色・線幅。 色は `System.Drawing.Color`）
- **座標変換**: `ContentToViewport` / `ViewportToContent` / `ImageRectViewport`（Fit 中央寄せの原点補正を含む）。 領域選択オーバーレイ上のホイールは本コントロールがビューポート（ズーム・パン）へ転送する

主な公開メンバー: `Source` / `Rects` / `SelectionMode` / `SelectionOverlayOptions` / `SelectionOverlayMarqueeAppearance` / `SelectionOverlayLoupeAppearance` / `Appearance` / `CenterAlign` / `ZoomPercent` / `CanZoomIn` / `CanZoomOut` / `HasSelection` / `SelectionDomainRect` / `SizeLabelOverrideProvider` / `OperationTextProvider` / `InfoTextProvider` / `Log` / `DipToDomainScaling` / `LoupeDipToDeviceScaling`、メソッド `ZoomIn` / `ZoomOut` / `ZoomFit` / `SetSelection` / `ClearSelection` / `SetSelectionOverlayVisible` / `SetCoordTransform` / `RequestConfirm` / `RequestCancel` / `FocusMarquee` / `SetLoupeSource` / `ClearLoupe` / `NotifyViewportChanged` / `ImageRectViewport` / `ContentToViewport` / `ViewportToContent`、イベント `ConfirmRequested` / `CancelRequested` / `ZoomChanged` / `ViewportChanged`。

### 7.2 FadeMaskDragRectControl（領域選択）

選択矩形（ドラッグ）・暗転マスク・ルーペを 1 つのコントロールにまとめ、配線と状態同期を内側で完結させる複合コントロールです。 ホストは `Mode` の設定と状態の受け取りだけで利用できます。

| モード | 選択矩形 | ルーペ | 全面暗転 | 用途 |
| --- | --- | --- | --- | --- |
| `Crop` | 有効 | 有効 | 有効 | プレビュー内の切出し |
| `AreaSelect` | 有効 | 有効 | 有効 | 全画面オーバーレイの領域選択 |
| `ScreenSelect` | 無効 | 無効 | 無効 | スクリーン選択（ハイライトのみ） |
| `WindowSelect` | 無効 | 無効 | 無効 | ウィンドウ選択（ハイライトのみ） |

主な公開メンバー: `Mode` / `Options`（コアの `FadeMaskDragRectOptions`）/ `MarqueeAppearance` / `LoupeAppearance` / `HasSelection` / `SelectionDomainRect` / `HighlightDomainRect` / `ShowFullOverlay` / `LoupeCenterPoint` / `SizeLabelOverrideProvider` / `OperationTextProvider` / `InfoTextProvider` / `Log` / `DipToDomainScaling` / `DisplayScaleScaling` / `LoupeDipToDeviceScaling`、メソッド `SetCoordTransform` / `SetSelection` / `ClearSelection` / `SetLoupeSource` / `SetLoupePosition` / `ClearLoupe` / `RequestConfirm` / `RequestCancel` / `FocusMarquee` / `RefreshOverlay` / `InvalidateOverlay`、イベント `ConfirmRequested` / `CancelRequested` / `LoupePositionMoved` / `LoupeRendered`。

## 8. 依存関係

| 参照先 | 利用機能 |
| --- | --- |
| `CaptureSessionControl.Core` | `CaptureSettings` / `CaptureResult` / `CaptureSettingsMerge` / `FadeMaskDragRectOptionsMapper` / `CropBoundsMath` / `CaptureClipMath` / `CaptureImageHolder` / `CaptureVirtualScreen` / `CaptureHighlightSession` / `CaptureKeyActions` / `CaptureClickSelection` / `CaptureLabelTexts` / `CropLabelTexts` / `CaptureTypeTraits` / `MonitorScalingMath` / `CaptureSessionSlot` / `CaptureOwnerScope` / `OverlayRect` / `OverlayLineStyle` / `FadeMaskDragRectMode` / `FadeMaskDragRectOptions` / ログ定義（両 UI 共通の規則。 UI 非依存） |
| `CaptureSessionControl.UIBase` | 設定 → 外観（`MarqueeAppearance` / `LoupeAppearance`）の写像（`CapturePaintMapper`）、設定とモードから子コントロールへ適用する値の解決（`FadeMaskDragRectChildValues`）、付加情報ラベルの描画（`FadeMaskHostLabelRenderer`） |
| `Platform.AvaloniaUI` | サービス生成 / `ScreenConverter` / 色の相互変換（`ColorAvaloniaInterop`）。 抽象（`ICaptureService` / `IWindowService` / `IElementFinder`）は `Platform.Core.Native`（既定実装は `Native.Win`）、`ICoordTransform` / `CoordTransform` は `Platform.Core` |
| `ZoomViewControl.AvaloniaUI` | `ZoomViewControl`（切り出しビューの画像ビューポート）/ `ZoomViewAppearance` / `ZoomChangedEventArgs` |
| `MarqueeControl.AvaloniaUI` | `MarqueeControl`（選択矩形と、その外側に描く装飾（暗転マスク・外枠・ハイライト）） |
| `LoupeControl.AvaloniaUI` | `LoupeControl`（拡大鏡）/ `LoupeRenderedEventArgs` |
| `Feature.Marquee.Presenter` | `MarqueeAppearance`（描画にだけ必要な外観） |
| `Feature.Loupe.Core` | ルーペの配置計算（推移的依存） |

`CropViewControl` / `FadeMaskDragRectControl` は本アセンブリが公開する複合コントロールです（別プロジェクトではありません）。 撮影セッション（`CaptureSessionControl` / `SelectionCaptureSession` / `OverlayWindow`）は内包コントロール（`MarqueeControl` / `LoupeControl`）を直接参照しません（窓口は `FadeMaskDragRectControl` のみ。 公開型のカプセル化）。

## 9. ドキュメント

- [外部機能仕様書](SPEC_ExtFunc.md)
- [システム仕様書](SPEC_System.md)

## 10. プロジェクト構成

```text
CaptureSessionControl.AvaloniaUI/
├── src/
│   ├── CaptureSessionControl.AvaloniaUI.csproj      # ライブラリプロジェクト（Avalonia.Desktop / Logging.Abstractions）
│   ├── CaptureSessionControl.cs            # 公開 Facade（統括者。組み立て・配線・ライフサイクル）
│   ├── CropViewControl.cs                  # 切り出しビュー（公開。画像ビューポート + 領域選択 + 矩形オーバーレイ）
│   ├── CropViewImage.cs                    # 画像の表示アダプタ（internal）
│   ├── FadeMaskDragRectControl.cs          # 領域選択（公開。暗転マスク + 選択矩形 + ルーペ）
│   ├── FadeMaskDragRectSelectionEventArgs.cs  # 確定要求のイベント引数（公開 record）
│   ├── FadeMaskDragRectPointEventArgs.cs   # ルーペ位置のイベント引数（公開 record）
│   └── Sessions/                 # 内部実装（SelectionCaptureSession / CropSession / OverlayWindow）
├── sample/                       # サンプルアプリ（sample/README.md 参照）
└── tests/                        # 単体テスト（セッション + 収録コントロール）
```

`CaptureType`（キャプチャ種別）は UI 非依存の共通基盤 `Platform.Core`（名前空間 `EsUtil.Platform.Core`）が提供します。 利用側は `using EsUtil.Platform.Core;` を追加してください。

`CaptureSettings`（設定）と `CaptureResult`（結果）は UI 非依存の共通基盤 `CaptureSessionControl.Core`（名前空間 `EsUtil.SaveEvidence.CaptureSession`）が提供します（切り出し矩形の計算 `CropBoundsMath` も同プロジェクト）。 利用側は `using EsUtil.SaveEvidence.CaptureSession;` を追加してください。 **色は `System.Drawing.Color`** で指定します（従来の Avalonia の色型から変更）。

設定から描画にだけ必要な外観（`MarqueeAppearance` / `LoupeAppearance`）への写像は、両 UI が共有する `CaptureSessionControl.UIBase`（`CapturePaintMapper`）が提供します。 利用側は `using EsUtil.SaveEvidence.CaptureSession.UIBase;` を追加してください。

**撮影オーバーレイ（`StartCaptureAsync`）も同じ写像で外観（色）を適用します**（切り出しと同じ見た目になる。 適用しないと設定した色が反映されない）。

## 11. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
