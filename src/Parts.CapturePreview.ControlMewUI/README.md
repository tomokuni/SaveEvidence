# Parts.CapturePreview.ControlMewUI

## 目次 <!-- omit from toc -->

- [1. 概要](#1-概要)
- [2. 機能一覧](#2-機能一覧)
- [3. 依存関係](#3-依存関係)
- [4. クイックスタート](#4-クイックスタート)
- [5. 収録コントロール](#5-収録コントロール)
  - [5.1 CropViewControl（切り出しビュー）](#51-cropviewcontrol切り出しビュー)
  - [5.2 SelectionOverlayControl（領域選択）](#52-selectionoverlaycontrol領域選択)
  - [5.3 ZoomViewControl（切り出しビューの画像ビューポート）](#53-zoomviewcontrol切り出しビューの画像ビューポート)
- [6. ドキュメント](#6-ドキュメント)
- [7. プロジェクト構成](#7-プロジェクト構成)
- [8. テスト](#8-テスト)
- [9. ライセンス](#9-ライセンス)

---

## 1. 概要

画面キャプチャ（スクリーン選択 / ウィンドウ選択 / 範囲選択）の **MewUI 版セッション**です。 仮想スクリーン（全モニター）を 1 枚の画像として敷いたオーバーレイを表示し、選択された領域を切り出して返します。

撮影・切り出しの共通規則（切り出し矩形・設定・セッション状態・オーナー退避）は `Parts.CapturePreview.Core` が単一所有し、本プロジェクトは MewUI の描画・入力・ウィンドウ操作だけを担います。

本アセンブリは、セッションの Facade に加えて 2 つの公開複合コントロール（`CropViewControl`（切り出しビュー）と `SelectionOverlayControl`（領域選択））を同一アセンブリ内に収録します。 `CropViewControl` は画像ビューポート `ZoomViewControl`（[5.3](#53-zoomviewcontrol切り出しビューの画像ビューポート)）を内包します（[5. 収録コントロール](#5-収録コントロール)）。

## 2. 機能一覧

- **3 種のキャプチャモード**: スクリーン選択（カーソル下のモニター）/ ウィンドウ選択（カーソル下の最前面ウィンドウ）/ 範囲選択（ドラッグ）
- **仮想スクリーン全体を対象**: 全モニターを 1 枚の画像として取得し、オーバーレイで全面を覆う（1 モニターに限定するとカーソルのあるモニターでしか操作できないため）
- **自ウィンドウの写り込み防止**: 撮影前にキャプチャ除外（`WDA_EXCLUDEFROMCAPTURE`）を設定し、画面外へ退避する
- **ちらつきの防止**: オーナーの退避は非表示ではなく位置の変更。 オーバーレイは透明で表示し、画像が合成されたフレームで透明を解除する
- **選択矩形のクロップ**: 確定時は種別に応じて切り出す（範囲選択は閉区間 = 始点・終点のピクセルを含む。 切り出し矩形の解決は Core の `CropClipMath.ResolveCaptureRect`（**画面物理座標の入口**。 選択矩形は画像ローカル座標のため `ToScreenRect` で戻してから渡す）、切り出しは Core の `CropClipMath`）
- **キーボード操作**: Enter（Return を含む）= 確定 / Escape = キャンセル（確定対象が無い状態の Enter は何もしない。 解釈は Core の `CaptureKeyActions` が単一所有する）
- **クリック確定**: 左押下でカーソル下の要素を固定し、解放時に確定する（押下を伴わない解放・退化した検出では確定しない。 規則は Core の `CaptureClickSelection` が単一所有する）
- **プレビューの切り出し**（`CaptureSessionControl.StartCropAsync` / `ResolveCropAction`）: プレビュー（`CropViewControl`）の上へ領域選択のオーバーレイを重ね、ドラッグで指定した領域を現在の画像から切り出す。 切り出しの適用（Undo 退避・画像差し替え）は**ホストの責務**（戻り値の `EsRectInt` をアプリのコマンドへ渡す）
- **撮影と切り出しの排他**: 同時に実行できるのはどちらか 1 つ（規則は Core の `CaptureSessionSlot` が単一所有し、本ライブラリの Facade がその唯一の適用者になる）
- **設定の単一所有と即時反映**: 設定（`AppearanceSettings`）は Facade が単一所有し、撮影と切り出しで同じ値を使う。 設定時は部分更新（null メンバーのみ現在値を維持）・検証・同値スキップ・適用ログを Core（`CaptureSettingsMerge.Apply`）が担い、実行中の切り出しセッションへは即時反映する
- **ラベル描画**: サイズラベル・操作ラベル・情報ラベルを選択矩形の左上へ表示する
- **撮影サービスの内部生成**: キャプチャ・要素検出・ウィンドウ操作（`EsUtil.Platform.Core` の抽象の Win32 実装）は Facade が生成・保持する（ホストは組み立てない。 Avalonia 版と同じ分担）
- **切り出しビュー（`CropViewControl`）**: 画像ビューポート（拡大・パン・Fit）と外部提供矩形オーバーレイ・強調オーバーレイを 1 つの複合コントロールにまとめる
- **ズーム・パン（切り出しビューの画像ビューポート）**: `CropViewControl` が内包する `ZoomViewControl` が、ズーム（⊖⊕ の 23 段階 / Ctrl+ホイール ×1.1）・パン・Fit 表示・座標変換を担う（[5.3](#53-zoomviewcontrol切り出しビューの画像ビューポート)）
- **領域選択（`SelectionOverlayControl`）**: 暗転マスク + 選択矩形 + ルーペを 1 つの複合コントロールにまとめ、配線と状態同期を内側で完結させる

## 3. 依存関係

```text
Parts.CapturePreview.ControlMewUI
    ├── Parts.CapturePreview.Core（撮影・切り出しの共通規則・複合コントロールの設定とモード・切り出しビューの画像ビューポートの状態と判断・設定 → 外観の写像・付加情報ラベルの配置）
    ├── Platform.MewUI（コア型 ⇔ MewUI 型の Interop / DIP 変換）
    ├── EsUtil.Platform.Core（ICaptureService / IElementFinder / IWindowService の Win32 実装を Facade が生成する）
    ├── Platform.Skia（Skia 依存の閉じ込め）
    ├── EsUtil.Feature.Marquee.ControlMewUI（選択矩形と、その外側に描く装飾。 描画計画と外観は EsUtil.Feature.Marquee.Core / EsUtil.Feature.Marquee.Presenter を推移参照する）
    └── EsUtil.Feature.Loupe.ControlMewUI（拡大鏡。 配置計算と外観は EsUtil.Feature.Loupe.Core / EsUtil.Feature.Loupe.Presenter を推移参照する）
```

依存は一方向です（アプリから本ライブラリを参照し、逆は参照しない）。

## 4. クイックスタート

```csharp
using Aprillz.MewUI;
using EsUtil.SaveEvidence.Parts.CapturePreview.MewUI.Crop;
using EsUtil.SaveEvidence.Parts.CapturePreview.MewUI.Session;
using EsUtil.Platform.Core;

// 撮影・切り出しの Facade を生成する。
// 撮影サービス（キャプチャ・要素検出・ウィンドウ操作）は Facade が内部で生成・保持する（ホストは組み立てない）
var session = new CaptureSessionControl(
    captureSettings,           // Parts.CapturePreview.Core の CaptureSettings
    logger);

// 撮影: オーバーレイを表示し、確定するまで待つ（null はキャンセル。切り出し中は受付しない）。
// 確定した画像の反映は CaptureCompleted（同期）で行う（オーナーの再表示より前に発行される）
session.CaptureCompleted += (_, e) => { /* e.Result?.Image をプレビューへ設定する */ };
CaptureResult? result = await session.StartCaptureAsync(CaptureType.AreaSelect, ownerWindow);

// 切り出し: 操作の判断（開始 / 確定 / キャンセル）は Facade へ問い合わせる（規則はコアが単一所有する）
switch (session.ResolveCropAction())
{
    case CropSessionAction.Start:
        // 切り出しの対象は CropViewControl.Source（表示中の画像）から解決される
        var bounds = await session.StartCropAsync(cropView, ownerWindow);
        if (bounds is { } rect && !rect.IsEmpty) { /* アプリのコマンドで切り出す（Undo 退避 → 画像差し替え） */ }
        break;
    case CropSessionAction.Confirm:
        session.Confirm();     // 進行中の切り出しを確定する
        break;
    default:
        session.Cancel();      // 選択が無い確定操作 → キャンセル
        break;
}

// 設定の変更（設定画面など）は同じ Facade へ反映する（実行中の切り出しセッションへ即時反映される）
session.AppearanceSettings = changed;

// 終了時は Dispose で進行中のセッションをキャンセルする
session.Dispose();
```

`CaptureSettings` は `Parts.CapturePreview.Core`（名前空間 `EsUtil.SaveEvidence.Parts.CapturePreview.Core.Capture`）が提供します。 ホストは設定画面の値からこれを作り、**Facade（コンストラクタまたは `AppearanceSettings`）へ渡します**（撮影と切り出しが同じ値を使う。 ホストの設定型 → `CaptureSettings` の変換は上位のアプリが担います）。

## 5. 収録コントロール

本アセンブリは、セッションの Facade に加えて 2 つの公開複合コントロールを収録します。 `CropViewControl` は画像ビューポート `ZoomViewControl`（[5.3](#53-zoomviewcontrol切り出しビューの画像ビューポート)）を内包します。 いずれも UI 非依存の共通規則を `Parts.CapturePreview.Core` に置き、MewUI 固有の描画・入力・配置だけを持ちます。

### 5.1 CropViewControl（切り出しビュー）

画像の表示（拡大・パン・Fit）と、ホストから渡された矩形のオーバーレイ描画、切り出しの領域選択を 1 つのコントロールにまとめた複合コントロールです。 内部で画像ビューポート（`ZoomViewControl`）・領域選択オーバーレイ（`SelectionOverlayControl`）・矩形オーバーレイ・強調オーバーレイを重ね、ホストへ統一インターフェースを提供します（Facade / Composite パターン）。

- **複合構造**: 画像ビューポート（`ZoomViewControl`）+ 領域選択オーバーレイ（`SelectionOverlayControl`）+ 矩形オーバーレイ + 強調オーバーレイを内包する（重なり順は「ズームビュー → 領域選択 → 矩形 → 強調」で、切り出し中も自動切り出しの強調が暗転より上に残る）
- **領域選択**: `SelectionMode`（`SelectionOverlayMode`。 既定 `Crop`）/ `SelectionOverlayOptions` / `SelectionOverlayMarqueeAppearance` / `SelectionOverlayLoupeAppearance` を内包する `SelectionOverlayControl` へ委譲する。 オーバーレイの可視性とヒットテストは `SetSelectionOverlayVisible` で制御する（切り出しセッションの開始・終了時に切り替わる。 初期状態は非表示・ヒットテスト不可）
- **切り出し中のホイール**: 領域選択オーバーレイが入力を占有するため、本コントロールがホイールを受け取って内包する画像ビューポートへ転送する（Ctrl 併用 = ズーム / それ以外 = パン。 解釈はコアの `ZoomWheelIntent` が単一所有する）
- **矩形オーバーレイ**: `Rects`（`IReadOnlyList<OverlayRect>`。 対象座標）を表示時にビューポート座標へ変換して描画する。 実線は前景色の枠のみ、破線（`Dashed` / `Dotted`）は「背景色の実線 + 前景色の破線」の 2 回描画で市松風の視認性を出す（ダッシュ長はコアの `OverlayLineDash` が単一所有）
- **強調（ハイライト）**: `HighlightSourceRect`（対象座標）+ `HighlightAppearance`（外観）で「対象矩形を目立たせる」描画をホストが行える（自動切り出しのホバー強調等。 既定は内側の塗りと枠線だけを描く）。 対象座標 → ビューポート座標の写像とビューポート変化の追従は本コントロールが単一所有する
- **座標変換**: `ClientToSource` / `SourceToClient` / `ImageRectViewport`（画像表示矩形の原点基準。 Fit 中央寄せの補正を含む）
- **入力**: 画像ビューポートが受け取る（オーバーレイはヒットテスト対象外。 切り出し中は領域選択オーバーレイが受け取る）。 ホイールの転送用に `PanByWheel` / `ZoomByWheel` を公開する

| プロパティ | 型 | 説明 |
| --- | --- | --- |
| `Source` | `EsReadOnlyBitmap?` | 表示する画像。 設定すると Fit 表示へリセットする |
| `CenterAlign` | `bool` | 画像がビューポートより小さい軸を中央寄せする |
| `Appearance` | `ZoomViewAppearance` | 下地色の外観の単一値 |
| `Rects` | `IReadOnlyList<OverlayRect>` | オーバーレイとして描画する矩形（対象座標） |
| `HighlightSourceRect` | `EsRect?` | 強調する矩形（対象座標）。 `null` は強調なし |
| `HighlightAppearance` | `MarqueeAppearance` | 強調の外観（色・枠線の太さ・表示可否）の単一値。 既定は内側の塗りと枠線だけ |
| `ZoomView` | `ZoomViewControl` | 内包する画像ビューポート |
| `ZoomPercent` | `double` | 実効ズーム率（%）。 0 より大きい値のみ設定できる |
| `CanZoomIn` / `CanZoomOut` | `bool` | ズーム可否（画像未設定時は false） |
| `Log` | `ILogger` | ズーム操作ログの出力先（内包する画像ビューポートへ伝搬する） |
| `SelectionMode` | `SelectionOverlayMode` | 領域選択の動作モード（基底は `Crop`） |
| `SelectionOverlayOptions` | `SelectionOverlayOptions` | 領域選択の設定の単一値（コアの型） |
| `SelectionOverlayMarqueeAppearance` / `SelectionOverlayLoupeAppearance` | 提示層の外観 | 描画にだけ必要な外観（選択矩形・ルーペ） |
| `HasSelection` / `SelectionSourceRect` | `bool` / `EsRect` | 現在の選択状態（対象座標） |

| メソッド | 説明 |
| --- | --- |
| `ZoomIn()` / `ZoomOut()` / `ZoomFit()` | 段階遷移・Fit 表示 |
| `PanByWheel(EsVector, bool)` | ホイールの回転量でパンする（ホストがオーバーレイ等で受けたホイールの転送用） |
| `ZoomByWheel(bool, EsPoint?)` | Ctrl+ホイールのズームを適用する（位置はズーム中心） |
| `TranslatePositionFrom(Element, EsPoint)` | 重ねた要素のローカル座標を本コントロールの座標へ変換する |
| `ImageRectViewport()` | 画像の表示矩形（ビューポート座標・DIP） |
| `ClientToSource(EsPoint)` / `SourceToClient(EsPoint)` | 画像表示矩形の原点基準の座標変換 |
| `SetSelection(EsRect)` / `ClearSelection()` | 領域選択の外部設定・クリア |
| `SetSelectionOverlayVisible(bool)` | 領域選択オーバーレイの可視性とヒットテストの切り替え（切り出しセッションが使う。 非表示へ戻すときは選択も破棄する） |
| `SetCoordTransform(ICoordTransform?)` | 領域選択の座標変換ポートを設定する |
| `SetLoupeSource(EsReadOnlyBitmap?)` / `ClearLoupe()` | ルーペのソース画像の設定・解除 |
| `RequestConfirm()` / `RequestCancel()` / `FocusMarquee()` | 確定・キャンセル要求と選択矩形へのフォーカス |
| `NotifyViewportChanged()` | ビューポートの変化を領域選択オーバーレイへ通知する |
| `SizeLabelOverrideProvider` / `OperationTextProvider` / `InfoTextProvider` | 領域選択の付加情報ラベルの文言（描画時に評価する） |
| `ClientToSourceScaling` / `LoupeClientToPixelScaling` | DPI スケーリング（設定には含めない環境値） |

イベントは `ZoomChanged` / `ViewportChanged`（内包する画像ビューポートから中継）と、`ConfirmRequested` / `CancelRequested`（領域選択から中継）です。

### 5.2 SelectionOverlayControl（領域選択）

選択矩形（ドラッグ）・暗転マスク・ルーペを 1 つのコントロールにまとめ、配線と状態同期を内側で完結させる複合コントロールです（Facade / Composite パターン）。 ホストは `Mode` の設定と状態の受け取りだけで利用できます。

| モード | 選択矩形 | ルーペ | 全面暗転 | 用途 |
| --- | --- | --- | --- | --- |
| `Crop` | 有効 | 有効 | 有効 | プレビュー内の切出し |
| `AreaSelect` | 有効 | 有効 | 有効 | 全画面オーバーレイの領域選択 |
| `ScreenSelect` | 無効 | 無効 | 無効 | スクリーン選択（ハイライトのみ） |
| `WindowSelect` | 無効 | 無効 | 無効 | ウィンドウ選択（ハイライトのみ） |

既定値は `AreaSelect` です。 モードの解釈（表示可否・全面暗転）はコアの `SelectionOverlayState` が単一所有します。

| メンバー | 説明 |
| --- | --- |
| `Mode` | 動作モード（既定は `AreaSelect`） |
| `Options` | 設定の単一値（選択矩形 / 暗転マスク / ルーペ / 選択矩形破線。 コアの `SelectionOverlayOptions`。 色は含まない） |
| `MarqueeAppearance` / `LoupeAppearance` | 描画にだけ必要な外観の単一値（提示層の外観。 内包コントロールへ委譲） |
| `HasSelection` / `SelectionSourceRect` | 現在の選択状態（対象座標） |
| `HighlightSourceRect` | ハイライト（`ScreenSelect` / `WindowSelect` の注目矩形。 null はハイライトなし） |
| `ShowFullOverlay` | 全面暗転の有無（モードから導出） |
| `LoupeCenterPoint` | ルーペ中心（対象座標） |
| `OperationTextProvider` / `InfoTextProvider` / `SizeLabelOverrideProvider` | 操作ラベル（2 行目）・情報ラベル（3 行目）・サイズラベルの文言を描画時に生成するデリゲート（文言はホストが与える） |
| `ClientToSourceScaling` / `DisplayScaleScaling` / `LoupeClientToPixelScaling` | DPI スケーリング（設定には含めない環境値） |
| `Log` | 操作ログの出力先（内包する選択コントロールとルーペへ伝搬する） |

| メソッド | 説明 |
| --- | --- |
| `SetCoordTransform(ICoordTransform?)` | 座標変換ポートを一括設定（選択矩形・暗転マスク・ルーペの配置で共用。 null の場合は倍率からコアが解決する） |
| `SetSelection(EsRect)` / `ClearSelection()` | 選択状態の外部設定・クリア |
| `SetLoupeSource(EsReadOnlyBitmap?)` / `SetLoupePosition(EsPoint)` / `ClearLoupe()` | ルーペの制御 |
| `RequestConfirm()` / `RequestCancel()` | Enter / Escape 相当の確定・キャンセル要求 |
| `FocusMarquee()` | 選択矩形へフォーカスを設定する（矢印キー nudge の受付開始） |
| `NotifyViewportChanged()` / `RequestRedraw()` | 表示の変化への追従・暗転マスクの再描画要求 |

イベントは `ConfirmRequested`（`SelectionConfirmedEventArgs`）/ `CancelRequested` / `LoupePositionMoved`（`LoupePositionEventArgs`）です。 イベントの座標はコア型（`EsPoint` / `EsRect`）で公開します。

ホイール操作（ズーム・パン）と表示の変化の追従は本コントロールの配線ではありません（コントロール化を前提とした処理）。 ホイールはホストが受け取ってビューポートへ転送し、表示の変化はホストが検知して `NotifyViewportChanged()` を呼びます。

### 5.3 ZoomViewControl（切り出しビューの画像ビューポート）

画像の拡大・パン・Fit 表示を担う画像ビューポートです。 **`CropViewControl` が内包する内部部品**であり、独立した汎用コントロールとしてではなく切り出しビューの表示を成立させるために存在します（`CropViewControl` が `Source` / `ZoomPercent` / `ZoomIn` / `ZoomOut` / `ZoomFit` / `ClientToSource` / `SourceToClient` を委譲し、ホストは通常 `CropViewControl` 経由で利用します。 `CropViewControl.ZoomView` で参照できるのは、重ねた要素の座標変換に必要となる場合のためです）。 判断（実効ズーム率の導出・可否・遷移の適用・パン・スクロールバー）は UI 非依存コア `Parts.CapturePreview.Core` の `ZoomViewSession` が単一所有します。

- **ズーム遷移**: ⊖⊕ の 23 段階（10, 12, 15, 20, 25, 33, 40, 50, 62, 75, 80, 90, 100, 110, 125, 150, 200, 250, 300, 350, 400, 450, 500）/ Ctrl+ホイールの ×1.1・÷1.1 連続遷移（現在値を基準に四捨五入し、10〜500% へクランプ）
- **Fit 表示**: ビューポートに収まる Uniform 倍率（`min(ビューポート幅 ÷ 画像幅, ビューポート高さ ÷ 画像高さ) × 100`。 アスペクト比維持）。 リサイズに追従し、段階遷移の際に Fit 実効倍率が間に挟まる場合はそれを経由する
- **ズーム中心維持**: マウスポインタ位置（画像外はビューポート中心）を中心に拡大縮小し、中心のコンテンツ座標を不変に保つ
- **パン**: 左ドラッグ（grab 方式）/ ホイール（縦・Shift で横）/ スクロールバー（オーバーレイ表示）
- **描画**: SkiaSharp で画像を 1 回の `DrawBitmap` で表示矩形へ描画する（Avalonia 版は `Image` + `RenderTransform`）
- **下地**: 画像が無い部分は `Appearance`（コアの `ZoomViewAppearance`）の下地色の単色になる（市松模様は描かない）

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

`ZoomChanged`（`ZoomChangedEventArgs`。 実効ズーム率・Fit 状態・可否）と `ViewportChanged`（パン・ズーム・表示サイズ）を通知し、ホストは `CropViewControl` 経由で購読します。 **表示文字列の生成（書式・丸め・% 表記）はホストの責務**です（`ZoomPercent` は実効ズーム率の数値を通知するのみ。 Fit 判定は `IsFit` で行う）。

## 6. ドキュメント

- [SPEC_API.md](docs/SPEC_API.md) — 公開 API のリファレンス
- [SPEC_System.md](docs/SPEC_System.md) — 設計・依存・実測で判明した制約

## 7. プロジェクト構成

```text
src/
│   ├── Parts.CapturePreview.ControlMewUI.csproj  # ライブラリ（net10.0-windows）
│   ├── Crop/                               # 切り出しビュー（画像ビューポート + 領域選択 + 矩形オーバーレイ + 強調オーバーレイ）
│   │   ├── CropViewControl.cs              # 切り出しビュー（公開）
│   │   ├── RectOverlayCanvas.cs            # 透明な矩形オーバーレイ（internal）
│   │   └── CropSkiaRenderer.cs             # コアの描画入力を Skia へ写す（internal）
│   ├── SelectionOverlay/                   # 領域選択（暗転マスク + 選択矩形 + ルーペ）
│   │   ├── SelectionOverlayControl.cs              # 領域選択（公開）
│   │   ├── SelectionOverlaySkiaRenderer.cs         # 選択矩形破線の描画（internal）
│   │   ├── SelectionConfirmedEventArgs.cs          # 確定要求のイベント引数（公開クラス）
│   │   └── LoupePositionEventArgs.cs               # ルーペ位置のイベント引数（公開クラス）
│   ├── Session/                            # セッション（Facade と撮影・切り出しの実装）
│   │   ├── CaptureSessionControl.cs        # Facade（公開 API。撮影と切り出しの窓口）
│   │   ├── CaptureOverlay.cs               # 撮影セッション（internal。1 セッション = 1 インスタンス）
│   │   ├── CaptureOverlayServices.cs       # サービス（キャプチャ・検出・ウィンドウ・ロガー）の束（internal。 Facade が生成する）
│   │   └── CropSession.cs                  # 切り出しセッション（internal）
│   ├── ZoomView/                           # 切り出しビューの画像ビューポート（ZoomViewControl / ZoomSkiaRenderer / ZoomChangedEventArgs）
│   └── （撮影画像の描画は Platform.MewUI の SkiaImageView を使う）
```

本プロジェクトは UI 操作（描画・入力・ウィンドウ）のみを持ち、共通規則は `Parts.CapturePreview.Core` が単一所有します。 画面を伴うセッションは実機検証で確認し、複合コントロールは本プロジェクトのテスト（`tests/` 配下。 切り出しビューと領域選択のサブフォルダに分ける）で検証します。

設定から描画にだけ必要な外観（`MarqueeAppearance` / `LoupeAppearance`）への写像は、両 UI が共有する `Parts.CapturePreview.Core`（`CapturePaintMapper`）が提供します。

## 8. テスト

`src/Parts.CapturePreview.ControlMewUI/tests` が本プロジェクトのテストを持ちます（xUnit v3）。 実行手順と合否基準は [検証手順.md](../../.github/検証手順.md) が定めます。

```powershell
dotnet test src/Parts.CapturePreview.ControlMewUI/tests/Parts.CapturePreview.ControlMewUI.Tests.csproj
```

## 9. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
