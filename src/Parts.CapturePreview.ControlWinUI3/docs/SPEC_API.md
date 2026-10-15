# Parts.CapturePreview.ControlWinUI3 API リファレンス

画面キャプチャの撮影スタックを WinUI 3 で提供する公開 API の仕様です。
本プロジェクトの公開 API は `Session.CaptureSessionControl`（Facade）と、それが操作するコントロールです。

## 目次 <!-- omit from toc -->

- [1. 名前空間と型](#1-名前空間と型)
- [2. Session.CaptureSessionControl](#2-sessioncapturesessioncontrol)
- [3. Crop.CropViewControl](#3-cropcropviewcontrol)
- [4. ZoomView.ZoomViewControl](#4-zoomviewzoomviewcontrol)
- [5. SelectionOverlay.SelectionOverlayControl](#5-selectionoverlayselectionoverlaycontrol)
- [6. イベント引数](#6-イベント引数)
- [7. 内部の型（単体テストだけが参照する）](#7-内部の型単体テストだけが参照する)
- [8. 使用例](#8-使用例)
- [9. 制限事項](#9-制限事項)

## 1. 名前空間と型

| 名前空間 | 型 | 公開範囲 |
| --- | --- | --- |
| `EsUtil.SaveEvidence.Parts.CapturePreview.WinUI3.Session` | `CaptureSessionControl` | 公開 |
| `EsUtil.SaveEvidence.Parts.CapturePreview.WinUI3.Crop` | `CropViewControl` | 公開 |
| `EsUtil.SaveEvidence.Parts.CapturePreview.WinUI3.ZoomView` | `ZoomViewControl` | 公開 |
| `EsUtil.SaveEvidence.Parts.CapturePreview.WinUI3.ZoomView` | `ZoomChangedEventArgs` | 公開 |
| `EsUtil.SaveEvidence.Parts.CapturePreview.WinUI3.SelectionOverlay` | `SelectionOverlayControl` | 公開 |
| `EsUtil.SaveEvidence.Parts.CapturePreview.WinUI3.SelectionOverlay` | `SelectionConfirmedEventArgs` | 公開 |
| `EsUtil.SaveEvidence.Parts.CapturePreview.WinUI3.SelectionOverlay` | `LoupePositionEventArgs` | 公開 |
| `EsUtil.SaveEvidence.Parts.CapturePreview.WinUI3.Session` | `CaptureOverlay`（内部・撮影セッション） | 単体テストのみ |
| `EsUtil.SaveEvidence.Parts.CapturePreview.WinUI3.Session` | `CropSession`（内部・切り出しのライフサイクル） | 単体テストのみ |
| `EsUtil.SaveEvidence.Parts.CapturePreview.WinUI3.Session` | `CaptureOverlayServices`（内部・サービスの束） | 単体テストのみ |
| `EsUtil.SaveEvidence.Parts.CapturePreview.WinUI3.Session` | `CaptureOverlayRoot`（内部・ルート要素） | 単体テストのみ |
| `EsUtil.SaveEvidence.Parts.CapturePreview.WinUI3.Session` | `MonitorDpi`（内部・モニターの DPI 照会） | 単体テストのみ |
| `EsUtil.SaveEvidence.Parts.CapturePreview.WinUI3.Crop` | `RectOverlayCanvas`（内部・矩形オーバーレイ） | 単体テストのみ |
| `EsUtil.SaveEvidence.Parts.CapturePreview.WinUI3.Crop` | `CropSkiaRenderer`（内部・描画） | 単体テストのみ |
| `EsUtil.SaveEvidence.Parts.CapturePreview.WinUI3.ZoomView` | `ZoomSkiaRenderer`（内部・描画） | 単体テストのみ |
| `EsUtil.SaveEvidence.Parts.CapturePreview.WinUI3.SelectionOverlay` | `SelectionOverlaySkiaRenderer`（内部・描画） | 単体テストのみ |

前提条件（座標系・単位）:

- **対象座標**は元画像のピクセル座標（画像の左上が原点）です。
- **ビューポート座標**はコントロールのローカル DIP です。 対象座標との変換は `CropViewControl.ClientToSource` / `CropViewControl.SourceToClient` の対で行います。
- **画面物理座標**は仮想スクリーン座標です（撮影の確定はこの座標で行います）。

## 2. Session.CaptureSessionControl

撮影と切り出しのセッションを統括する Facade です（`IDisposable`）。 サービスの生成・破棄と排他を所有します。

| メンバー | 説明 |
| --- | --- |
| `CaptureSessionControl(CaptureSettings? initialSettings = null, ILogger? logger = null)` | サービス（画面キャプチャ・要素検出・ウィンドウ操作）を生成する。 `initialSettings` の null メンバーは既定値で埋める |
| `event EventHandler<CaptureCompletedEventArgs>? CaptureCompleted` | 撮影の完了（同期実行）。 引数の結果はキャンセル時に null |
| `CaptureSettings AppearanceSettings { get; set; }` | 現在の設定。 設定時は null メンバーのみ現在値を維持する（部分更新）。 実行中の切り出しへは即時反映する |
| `string? LogPrefix { get; set; }` | ログの接頭辞（null または空白は既定値へ戻す） |
| `Task<CaptureResult?> StartCaptureAsync(CaptureType type, Window owner)` | 撮影を開始し、確定またはキャンセルまで待つ（画像と領域を返す。 キャンセル・取得失敗は null） |
| `Task<EsRectInt?> StartCropAsync(CropViewControl cropView, Window hostWindow)` | 切り出しを開始する（確定は元画像上の範囲、キャンセルは 0x0、開始できなければ null） |
| `bool IsCropping { get; }` | 切り出しが進行中かどうか |
| `CropSessionAction ResolveCropAction()` | 切り出し操作（開始・確定・キャンセル）のうち、いま行うべき操作を返す |
| `void Cancel()` | 進行中の操作を取り消す（切り出しは 0x0 で完了し、撮影はオーバーレイを閉じる） |
| `void Confirm()` | 進行中の切り出しを現在の選択矩形で確定する（Enter キー相当） |
| `void NotifyViewportChanged()` | 実行中の切り出しへビューポートの変化を通知する（ホストがプレビューの変化を検知したときに呼ぶ） |
| `void Dispose()` | 進行中のセッションをキャンセルして完了させる |

制限事項:

- **`StartCaptureAsync` / `StartCropAsync` は UI スレッドから呼ぶ**（描画とウィンドウ操作を行うため）。
- 実行中に同じ操作を呼び直した場合は開始しない（切り出しは結果 null の完了済みタスク、撮影は null を返す）。
- `CropViewControl` が画像を表示していない場合、`StartCropAsync` は開始できない（結果 null）。

## 3. Crop.CropViewControl

画像表示（拡大・パン・Fit）とオーバーレイを統合した複合コントロールです（`Grid` 派生）。

| メンバー | 説明 |
| --- | --- |
| `EsReadOnlyBitmap? Source { get; set; }` | 表示する画像。 設定すると Fit 表示へリセットする |
| `bool CenterAlign { get; set; }` | 画像がビューポートより小さい軸の中央寄せ / 左上寄せ |
| `ZoomViewAppearance Appearance { get; set; }` | 画像の下地とスクロールバーの外観 |
| `IReadOnlyList<OverlayRect> Rects { get; set; }` | ビューポートへ重ねる矩形（外部提供。 対象座標） |
| `static MarqueeAppearance HighlightDefaultAppearance { get; }` | 強調の既定の外観（内側の塗りと枠線だけ） |
| `EsRect? HighlightSourceRect { get; set; }` | 強調する矩形（対象座標）。 null の場合は強調しない |
| `MarqueeAppearance HighlightAppearance { get; set; }` | 強調の外観（色・枠線・表示可否） |
| `ZoomViewControl ZoomView { get; }` | 内包する画像ビューポート（座標変換と Fit 状態の参照に使う） |
| `ILogger Log { get; set; }` | ログの出力先（内包するビューポートへ伝搬する） |
| `string? LogPrefix { get; set; }` | ログの接頭辞 |
| `double ZoomPercent { get; set; }` | 現在の実効ズーム率（%）。 0 より大きい値のみ設定できる |
| `bool CanZoomIn { get; }` / `bool CanZoomOut { get; }` | ズームの可否（画像未設定時は false） |
| `event EventHandler<ZoomChangedEventArgs>? ZoomChanged` | ズーム率の変更 |
| `event EventHandler? ViewportChanged` | ビューポート（パン・ズーム・表示サイズ）の変化 |
| `EsRect ImageRectViewport()` | 画像の表示矩形（ビューポート座標） |
| `void ZoomIn()` / `void ZoomOut()` / `void ZoomFit()` | ズームの段階遷移と Fit 表示への復帰 |
| `EsPoint ClientToSource(EsPoint viewportPos)` | ビューポート座標 → 対象座標 |
| `EsPoint SourceToClient(EsPoint contentPos)` | 対象座標 → ビューポート座標 |
| `void PanByWheel(EsVector delta, bool shift)` | ホイールの回転量でパンする（1 ノッチ = 1.0） |
| `void ZoomByWheel(bool zoomIn, EsPoint? viewportPosition = null)` | ホイールのズームを適用する（中心はビューポート座標） |
| `EsPoint TranslatePositionFrom(UIElement element, EsPoint position)` | 他の要素のローカル座標を本コントロールのローカル座標へ変換する |
| `SelectionOverlayMode SelectionMode { get; set; }` | 領域選択の動作モード（既定は切り出し） |
| `SelectionOverlayOptions SelectionOverlayOptions { get; set; }` | 領域選択の設定（単一値） |
| `double ClientToSourceScaling { get; set; }` | ビューポート DIP → 対象座標の倍率（座標変換ポートが未設定の場合に使う） |
| `double LoupeClientToPixelScaling { get; set; }` | ルーペ画像の描画倍率 |
| `MarqueeAppearance SelectionOverlayMarqueeAppearance { get; set; }` | 領域選択の選択矩形の外観 |
| `LoupeAppearance SelectionOverlayLoupeAppearance { get; set; }` | 領域選択のルーペの外観 |
| `bool HasSelection { get; }` / `EsRect SelectionSourceRect { get; }` | 選択矩形の有無と値（対象座標） |
| `void SetSelection(EsRect rect)` / `void ClearSelection()` | 選択矩形の設定と消去 |
| `void SetSelectionOverlayVisible(bool visible)` | 領域選択オーバーレイの表示と入力の可否（非表示へ戻すときは選択を破棄する） |
| `void SetCoordTransform(ICoordTransform? port)` | 領域選択オーバーレイへ座標変換ポートを設定する |
| `void FocusMarquee()` | 選択矩形コントロールへフォーカスを移す（キー操作の受け口） |
| `void SetLoupeSource(EsReadOnlyBitmap? source)` / `void ClearLoupe()` | ルーペの拡大元の設定と解放 |
| `Func<string?>? SizeLabelOverrideProvider` / `OperationTextProvider` / `InfoTextProvider` | ラベルの文言プロバイダ（文言はホストが与える） |
| `void NotifyViewportChanged()` | ビューポートの変化を領域選択オーバーレイへ通知する |
| `void RequestConfirm()` / `void RequestCancel()` | 選択の確定 / キャンセルを要求する（キー操作と同じ経路） |
| `event EventHandler<SelectionConfirmedEventArgs>? ConfirmRequested` | 選択の確定要求 |
| `event EventHandler? CancelRequested` | 選択のキャンセル要求 |

## 4. ZoomView.ZoomViewControl

ズーム・パン可能な画像表示コントロールです（`SKXamlCanvas` 派生。 Skia のキャンバスへ直接描画する）。

| メンバー | 説明 |
| --- | --- |
| `ZoomViewControl()` / `ZoomViewControl(ZoomOptions? config)` | ズーム遷移の設定を指定して生成する（null は既定値） |
| `EsReadOnlyBitmap? Source { get; set; }` | ズーム対象の画像。 設定すると Fit 表示へリセットする |
| `bool CenterAlign { get; set; }` | 画像がビューポートより小さい軸の中央寄せ / 左上寄せ |
| `double ClientToPixelScaling { get; set; }` | 表示 DIP → 物理ピクセルのスケーリング係数 |
| `ZoomViewAppearance Appearance { get; set; }` | 下地とスクロールバーの外観 |
| `ILogger Log { get; set; }` | ズーム操作ログの出力先 |
| `string? LogPrefix { get; set; }` | ログの接頭辞 |
| `double ZoomPercent { get; set; }` | 現在の実効ズーム率（%）。 0 より大きい値のみ設定できる |
| `bool IsFit { get; }` / `bool CanZoomIn { get; }` / `bool CanZoomOut { get; }` | Fit 表示とズームの可否 |
| `event EventHandler<ZoomChangedEventArgs>? ZoomChanged` | ズーム率の変更 |
| `event EventHandler? ViewportChanged` | ビューポートの変化 |
| `EsSize DisplaySize()` | 画像の表示サイズ（ビューポート座標） |
| `EsRect ImageRectViewport()` | 画像の表示矩形（ビューポート座標） |
| `EsPoint ClientToDisplayedSource(EsPoint viewportPos)` | ビューポート座標 → コンテンツ座標（元画像ピクセル） |
| `EsPoint DisplayedSourceToClient(EsPoint contentPos)` | コンテンツ座標 → ビューポート座標 |
| `void PanByWheel(EsVector delta, bool shift)` | ホイールの回転量でパンする |
| `void ZoomByWheel(bool zoomIn, EsPoint? pointerPosition = null)` | ホイールのズームを適用する |
| `void ZoomIn()` / `void ZoomOut()` / `void ZoomFit()` | ズームの段階遷移と Fit 表示への復帰 |

入力（既定の結線）: 左ボタンのドラッグでパン（スクロールバーの上ではスクロールバーのドラッグ）、
`Ctrl` + ホイールでズーム、`Shift` + ホイールで横パンです。

## 5. SelectionOverlay.SelectionOverlayControl

領域選択の複合コントロールです（`Panel` 派生）。

| メンバー | 説明 |
| --- | --- |
| `SelectionOverlayOptions Options { get; set; }` | 設定（選択矩形・暗転マスク・ルーペ・破線の外観。 単一値） |
| `SelectionOverlayMode Mode { get; set; }` | 動作モード（切り出し / 領域選択 / スクリーン選択 / ウィンドウ選択） |
| `bool HasSelection { get; }` / `EsRect SelectionSourceRect { get; }` | 選択矩形の有無と値（対象座標） |
| `EsRect? HighlightSourceRect { get; set; }` | ハイライト矩形（対象座標）。 null は「ハイライトしない」 |
| `bool ShowFullOverlay { get; }` | 全面暗転の表示可否（モードから導出する読み取り専用の値） |
| `EsPoint? LoupeCenterPoint { get; }` | ルーペ中心（対象座標）。 未設定時は null |
| `MarqueeAppearance MarqueeAppearance { get; set; }` | 選択矩形の外観（描画にだけ必要な設定） |
| `LoupeAppearance LoupeAppearance { get; set; }` | ルーペの外観（描画にだけ必要な設定） |
| `Func<string?>? SizeLabelOverrideProvider` | サイズラベルの文言プロバイダ |
| `Func<EsRect, double>? SizeLabelDisplayScalingProvider` | サイズラベルの表示スケーリングを矩形ごとに解決するプロバイダ |
| `Func<string?>? OperationTextProvider` / `InfoTextProvider` | 付加情報ラベルの文言プロバイダ |
| `double ClientToSourceScaling { get; set; }` | 対象座標 / ローカル DIP の倍率 |
| `double DisplayScaleScaling { get; set; }` | サイズラベルの表示倍率（マルチモニター補正） |
| `double LoupeClientToPixelScaling { get; set; }` | ルーペの DIP → 物理ピクセル倍率 |
| `ILogger Log { get; set; }` / `string? LogPrefix { get; set; }` | ログの出力先と接頭辞 |
| `void SetCoordTransform(ICoordTransform? port)` | 対象 ↔ ローカル DIP の座標変換ポートを内包コントロールへ設定する |
| `void RequestConfirm()` / `void RequestCancel()` | 選択の確定 / キャンセルを要求する |
| `void FocusMarquee()` | ドラッグ選択コントロールへフォーカスを設定する |
| `void SetSelection(EsRect rect)` / `void ClearSelection()` | 選択矩形の設定と消去（成立しないサイズは選択なしとして扱う） |
| `void SetLoupePosition(EsPoint sourcePos)` | ルーペ中心を設定する（拡大元の設定後に呼ぶ） |
| `void SetLoupeSource(EsReadOnlyBitmap? source)` / `void ClearLoupe()` | ルーペの拡大元の設定と解放 |
| `void RequestRedraw()` / `void NotifyViewportChanged()` | 再描画の要求とビューポート変化への追従 |
| `event EventHandler<SelectionConfirmedEventArgs>? ConfirmRequested` | 選択の確定要求 |
| `event EventHandler? CancelRequested` | 選択のキャンセル要求 |
| `event EventHandler<LoupePositionEventArgs>? LoupePositionMoved` | ルーペ中心の更新 |

## 6. イベント引数

| 型 | メンバー |
| --- | --- |
| `ZoomChangedEventArgs` | `State`（ズーム状態の値オブジェクト）/ `ZoomPercent` / `IsFit` / `CanZoomIn` / `CanZoomOut` |
| `SelectionConfirmedEventArgs` | `SelectionSourceRect`（選択矩形。 対象座標） |
| `LoupePositionEventArgs` | `Position`（ルーペ中心。 対象座標） |

## 7. 内部の型（単体テストだけが参照する）

| 型 | 役割 |
| --- | --- |
| `CropSkiaRenderer` | オーバーレイ矩形の描画入力（配置・太さ・破線・色）を Skia のキャンバスへ写す |
| `SelectionOverlaySkiaRenderer` | 選択矩形の破線オーバーレイを Skia のキャンバスへ写す |
| `ZoomSkiaRenderer` | ズーム画像をセッションが確定した矩形へ 1 回で描く |
| `RectOverlayCanvas` | 外部提供矩形をビューポート座標へ変換して重ねる（入力は下へ通す） |
| `CaptureOverlay` | 撮影の 1 セッション（ウィンドウの表示・入力の受付・切り出し） |
| `CropSession` | 切り出しのライフサイクル（確定・キャンセル・ホストの非アクティブ化） |
| `CaptureOverlayServices` | 撮影が必要とするサービスの束 |
| `CaptureOverlayRoot` | 撮影オーバーレイのルート要素（十字カーソルの設定を持つ） |
| `MonitorDpi` | モニターごとの DPI の照会（Win32） |

## 8. 使用例

```csharp
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Capture;
using EsUtil.SaveEvidence.Parts.CapturePreview.WinUI3.Crop;
using EsUtil.SaveEvidence.Parts.CapturePreview.WinUI3.Session;

// 撮影スタックを生成し、撮影してプレビューへ反映する
using var capture = new CaptureSessionControl(initialSettings: settings, logger: logger);
capture.CaptureCompleted += (_, e) => cropView.Source = e.Result?.Image;

var result = await capture.StartCaptureAsync(CaptureType.WindowSelect, window);

// 切り出し（確定は元画像上の範囲、キャンセルは 0x0）
var cropAction = capture.ResolveCropAction();
if (cropAction == CropSessionAction.Start)
{
    var bounds = await capture.StartCropAsync(cropView, window);
}
```

## 9. 制限事項

- **XAML の型はヘッドレスで生成できない**。 XAML のランタイムを初期化していないプロセスでコントロールを生成すると `COMException`（0x80040154 `REGDB_E_CLASSNOTREG`）になる。 コントロールの入力・配置はアプリでの動作確認に委ねる。
- **プロパティは依存関係プロパティではない**。 コアの型と描画の型は WinRT へ投影できないため、ホストはコードビハインドから設定する。
- **撮影はウィンドウを必要とする**（オーナーの退避と、仮想スクリーン全面のボーダーレス ウィンドウの表示）。
