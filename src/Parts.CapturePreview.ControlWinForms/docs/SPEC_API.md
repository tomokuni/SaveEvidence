# Parts.CapturePreview.ControlWinForms API リファレンス

`Parts.CapturePreview.ControlWinForms` は撮影スタックの UI 層を WinForms で提供します。 名前空間は `EsUtil.SaveEvidence.Parts.CapturePreview.WinForms` 配下（`ZoomView` / `Crop` / `SelectionOverlay` / `Session` / `Overlay`）です。

## 目次 <!-- omit from toc -->

- [1. 名前空間と型](#1-名前空間と型)
- [2. `ZoomViewControl`](#2-zoomviewcontrol)
- [3. `CropViewControl`](#3-cropviewcontrol)
- [4. `SelectionOverlayControl`](#4-selectionoverlaycontrol)
- [5. `CaptureSessionControl`](#5-capturesessioncontrol)
- [6. イベント引数](#6-イベント引数)
  - [6.1 `ZoomChangedEventArgs`](#61-zoomchangedeventargs)
  - [6.2 `LoupePositionEventArgs`](#62-loupepositioneventargs)
  - [6.3 `SelectionConfirmedEventArgs`](#63-selectionconfirmedeventargs)
- [7. 内部の型](#7-内部の型)
  - [7.1 `ZoomGraphicsRenderer`（内部・描画層）](#71-zoomgraphicsrenderer内部描画層)
  - [7.2 `RectOverlayCanvas`（内部・オーバーレイ コントロール）](#72-rectoverlaycanvas内部オーバーレイ-コントロール)
  - [7.3 `RectOverlayGraphicsRenderer`（内部・描画層）](#73-rectoverlaygraphicsrenderer内部描画層)
  - [7.4 `SelectionOverlayGraphicsRenderer`（内部・描画層）](#74-selectionoverlaygraphicsrenderer内部描画層)
  - [7.5 `CaptureOverlay`（内部・撮影セッション）](#75-captureoverlay内部撮影セッション)
  - [7.6 `CaptureOverlayServices`（内部・サービスの束）](#76-captureoverlayservices内部サービスの束)
  - [7.7 `CropSession`（内部・切り出しセッション）](#77-cropsession内部切り出しセッション)
  - [7.8 `OverlayHitTest`（内部・入力の透過）](#78-overlayhittest内部入力の透過)
- [8. 使用例](#8-使用例)
- [9. 制限事項](#9-制限事項)
- [10. 前提条件](#10-前提条件)

## 1. 名前空間と型

| 名前空間 | 公開型 | 内部型 |
| --- | --- | --- |
| `...WinForms.ZoomView` | `ZoomViewControl` / `ZoomChangedEventArgs` | `ZoomGraphicsRenderer` |
| `...WinForms.Crop` | `CropViewControl` | `RectOverlayCanvas` / `RectOverlayGraphicsRenderer` |
| `...WinForms.SelectionOverlay` | `SelectionOverlayControl` / `LoupePositionEventArgs` / `SelectionConfirmedEventArgs` | `SelectionOverlayGraphicsRenderer` |
| `...WinForms.Session` | `CaptureSessionControl` | `CaptureOverlay` / `CaptureOverlayServices` / `CropSession` |
| `...WinForms.Overlay` | — | `OverlayHitTest` |

`...` は `EsUtil.SaveEvidence.Parts.CapturePreview` を表します。

## 2. `ZoomViewControl`

ズーム・パン可能な画像表示コントロール（`Control` 派生）です。

| メンバー | 説明 |
| --- | --- |
| `Source`（`EsReadOnlyBitmap?`） | ズーム対象の画像。 設定すると Fit 表示（ズーム率 0・オフセット 0）へリセットし、GDI+ のビットマップへ複製する。 null または破棄済みの場合は何も表示しない |
| `CenterAlign`（`bool`） | 画像の中央寄せ / 左上寄せ（画像がビューポートより小さい軸にのみ作用する）。 既定は true |
| `ClientToPixelScaling`（`double`） | 表示 DIP を物理ピクセルへ変換するスケーリング係数。 DPI の変更時に自動更新される（描画面から実測できない場合の代替値） |
| `Appearance`（`ZoomViewAppearance`） | 下地とスクロールバーの外観（コアが単一所有する。 既定は暗色の下地・スクロールバー表示） |
| `ZoomPercent`（`double`） | 実効ズーム率（%）。 setter は 0 より大きい値のみ受け付ける（Fit へは `ZoomFit` を使う） |
| `IsFit`（`bool`） | Fit 表示中かどうか（`ZoomPercent` は実効値のため 0 にならない） |
| `CanZoomIn` / `CanZoomOut`（`bool`） | ズームの可否（画像未設定時は false） |
| `Log`（`ILogger`） | ズーム操作ログの出力先。 null を設定した場合は出力なしとして扱う（null の吸収はコアが単一所有する） |
| `LogPrefix`（`string?`） | ログの接頭辞。 null または空白で既定値（`[CapturePreview]`）へ戻る |
| `ZoomChanged`（イベント） | ズーム率・Fit 状態・ズーム可否の変更を通知する |
| `ViewportChanged`（イベント） | パン・ズーム・表示サイズの変化を通知する（オーバーレイを追従させる） |

| メソッド | 説明 |
| --- | --- |
| `DisplaySize()` → `EsSize` | 画像の表示サイズ（DIP）。 画像未設定時は default |
| `ImageRectViewport()` → `EsRect` | 画像の表示矩形（ビューポート座標・DIP）。 画像未設定時は default |
| `ClientToDisplayedSource(this EsPoint viewportPos)` → `EsPoint` | ビューポート座標 → 元画像ピクセル（画像表示矩形の原点基準。 Fit の中央寄せ補正を含む） |
| `DisplayedSourceToClient(this EsPoint contentPos)` → `EsPoint` | 元画像ピクセル → ビューポート座標 |
| `PanByWheel(this EsVector delta, bool shift)` | ホイールの回転量でパンする（1 ノッチ = 1.0。 `shift` は縦回転を横パンとして扱う） |
| `ZoomByWheel(bool zoomIn, EsPoint? pointerPosition = null)` | Ctrl+ホイールのズームを適用する（ホイール操作の公開 API） |
| `ZoomIn()` / `ZoomOut()` | 段階ズーム（移動先がなければ何もしない） |
| `ZoomFit()` | Fit 表示へ戻す（Fit へ移行する唯一の経路） |

入力はコントロールが直接受け取ります。

| 入力 | 動作 |
| --- | --- |
| 左ボタン押下 + ドラッグ | パン（スクロールバーのサム・トラックのドラッグを優先する。 ポインタはキャプチャされる） |
| Ctrl + ホイール | ポインタ位置を中心としたズーム |
| ホイール | パン（Shift 併用で横方向） |
| 矢印キー | （ズーム ビューは扱わない。 選択矩形の微調整は `SelectionOverlayControl` が担う） |

WinForms の `MouseEventArgs.Delta` は 1 ノッチ = 120 であるため、コアの契約（1 ノッチ = 1.0）へ換算します。 横方向の回転は `MouseEventArgs` から取得できないため常に 0 として扱います。

## 3. `CropViewControl`

画像表示（拡大・パン・Fit）と外部提供矩形のオーバーレイを統合した複合コントロール（`UserControl` 派生）です。

| メンバー | 説明 |
| --- | --- |
| `Source`（`EsReadOnlyBitmap?`） | 表示する画像（ズーム・切り出しの対象）。 設定するとルーペの拡大元にも同じ画像を設定する |
| `CenterAlign`（`bool`） | 画像の中央寄せ / 左上寄せ |
| `Appearance`（`ZoomViewAppearance`） | 下地とスクロールバーの外観 |
| `Rects`（`IReadOnlyList<OverlayRect>`） | 外部提供矩形（対象座標）。 設定すると矩形オーバーレイを再描画する |
| `HighlightSourceRect`（`EsRect?`） | 強調する矩形（対象座標）。 null の場合は強調しない。 **画像が未設定の間は描画しない** |
| `HighlightAppearance`（`MarqueeAppearance`） | 強調の外観（既定は `HighlightDefaultAppearance`） |
| `HighlightDefaultAppearance`（static） | 強調の既定の外観（内側の塗りと枠線だけを描く） |
| `ZoomView`（`ZoomViewControl`） | 内包するズーム ビューポート |
| `Log` / `LogPrefix` | ログの出力先と接頭辞（内包するズームビューへ伝搬する） |
| `ZoomPercent` / `CanZoomIn` / `CanZoomOut` | ズーム率と可否（内包するズームビューへ委譲する） |
| `SelectionMode`（`SelectionOverlayMode`） | 領域選択オーバーレイの動作モード。 既定は `Crop` |
| `SelectionOverlayOptions`（`SelectionOverlayOptions`） | 領域選択の設定（単一値。 既定は `SelectionOverlayOptions.Default`） |
| `ClientToSourceScaling` / `LoupeClientToPixelScaling`（`double`） | 環境値（ビューポート DIP → 対象座標の倍率 / ルーペの描画倍率） |
| `SelectionOverlayMarqueeAppearance` / `SelectionOverlayLoupeAppearance` | 領域選択の選択矩形と ルーペの外観 |
| `HasSelection`（`bool`） / `SelectionSourceRect`（`EsRect`） | 選択の有無と選択矩形（対象座標） |
| `SizeLabelOverrideProvider` / `OperationTextProvider` / `InfoTextProvider` | ラベルの文言プロバイダ（文言はホストが与える） |
| `ZoomChanged` / `ViewportChanged` | 内包するズームビューから中継するイベント |
| `ConfirmRequested` / `CancelRequested` | 領域選択オーバーレイから中継するイベント |

| メソッド | 説明 |
| --- | --- |
| `ImageRectViewport()` → `EsRect` | 画像の表示矩形（ビューポート座標・DIP） |
| `ZoomIn()` / `ZoomOut()` / `ZoomFit()` | 段階ズームと Fit（内包するズームビューへ委譲する） |
| `ClientToSource(this EsPoint viewportPos)` → `EsPoint` | ビューポート座標 → 対象座標（元画像ピクセル） |
| `SourceToClient(this EsPoint contentPos)` → `EsPoint` | 対象座標 → ビューポート座標 |
| `PanByWheel(this EsVector delta, bool shift)` / `ZoomByWheel(bool zoomIn, EsPoint? viewportPosition = null)` | ホイール操作の転送（ホストが他のコントロールで受けたホイール用） |
| `TranslatePositionFrom(this Control source, EsPoint position)` → `EsPoint` | 指定のコントロールのローカル座標を本コントロールのローカル座標へ変換する（整数の座標を経由するため小数部は丸められる） |
| `SetSelection(this EsRect rect)` / `ClearSelection()` | 選択矩形の設定とクリア |
| `SetSelectionOverlayVisible(bool visible)` | 領域選択オーバーレイの表示切替。 **非表示へ戻すときは選択を破棄する** |
| `SetCoordTransform(this ICoordTransform? port)` | 領域選択オーバーレイへ座標変換ポートを設定する |
| `FocusMarquee()` | 選択矩形コントロールへフォーカスを移す（キー操作の受け口） |
| `SetLoupeSource(this EsReadOnlyBitmap? source)` / `ClearLoupe()` | ルーペの拡大元画像の設定と解放 |
| `NotifyViewportChanged()` | ビューポートの変化を領域選択オーバーレイへ通知する |
| `RequestConfirm()` / `RequestCancel()` | 選択の確定・キャンセルの要求（キー操作と同じ経路） |

重ね合わせの順序（奥 → 手前）は 画像 → 領域選択オーバーレイ → 外部提供矩形 → 強調 です。 領域選択オーバーレイと強調オーバーレイは**入力を受け取りません**（下のズームビューが入力を受け取ります）。 WinForms では「無効なコントロールは入力が下のコントロールへ通る」性質で表します。

## 4. `SelectionOverlayControl`

領域選択の複合コントロール（`Control` 派生。 選択矩形 + 暗転マスク + ルーペ）です。 内包コントロールの生成・配置・配線・状態同期を本クラスへ集約します。

| メンバー | 説明 |
| --- | --- |
| `Options`（`SelectionOverlayOptions`） | 設定（選択矩形・暗転マスク・ルーペ・選択矩形破線）。 同じ値の再設定では更新しない |
| `Mode`（`SelectionOverlayMode`） | 動作モード。 既定は `AreaSelect`。 Crop / AreaSelect は選択矩形とルーペを有効化し、ScreenSelect / WindowSelect はハイライトのみになる |
| `HasSelection`（`bool`） / `SelectionSourceRect`（`EsRect`） | 選択の有無と選択矩形（対象座標） |
| `HighlightSourceRect`（`EsRect?`） | ハイライト矩形（対象座標）。 null が「ハイライトしない」を意味し、選択矩形より優先される |
| `ShowFullOverlay`（`bool`） | 全面暗転を表示するかどうか（モードから導出される読み取り専用の値） |
| `LoupeCenterPoint`（`EsPoint?`） | ルーペ中心（対象座標）。 未設定時は null |
| `MarqueeAppearance` / `LoupeAppearance` | 選択矩形と ルーペの外観（描画にだけ必要な設定） |
| `OperationTextProvider` / `InfoTextProvider` | 付加情報ラベルの文言プロバイダ（アプリ固有の文言はホストが与える） |
| `SizeLabelOverrideProvider` / `SizeLabelDisplayScalingProvider` | サイズラベルの文言と表示スケーリング |
| `ClientToSourceScaling` / `DisplayScaleScaling` / `LoupeClientToPixelScaling` | 環境値（DPI・マルチスクリーン） |
| `Log` / `LogPrefix` | ログの出力先と接頭辞（内包するコントロールへ伝搬する） |
| `ConfirmRequested` / `CancelRequested` | 確定・キャンセルの要求 |
| `LoupePositionMoved` | ルーペ中心の更新（対象座標。 ホストの追加制御用フック） |
| `MarqueeRendered` | 選択矩形（暗転マスクを含む）の描画完了（描画のたびに発火する） |

| メソッド | 説明 |
| --- | --- |
| `SetCoordTransform(this ICoordTransform? port)` | 対象座標 ⇔ ローカル DIP の座標変換ポートを内包コントロールへ一括設定する |
| `RequestConfirm()` / `RequestCancel()` | 確定・キャンセルの要求（キー操作と同じ経路） |
| `FocusMarquee()` | ドラッグ選択へフォーカスを設定する（矢印キー nudge の受付開始。 入力を受け取らないモードでは取得できない） |
| `SetSelection(this EsRect rect)` | 選択矩形を設定する（選択として成立しないサイズは選択なしとして扱う） |
| `ClearSelection()` | 選択状態をクリアして全面暗転へ戻す |
| `SetLoupePosition(this EsPoint sourcePos)` | ルーペの中心を設定して表示を開始する（拡大元画像が未設定の間は表示しない） |
| `SetLoupeSource(this EsReadOnlyBitmap? source)` | ルーペの拡大元画像を設定する（設定だけでは表示しない） |
| `ClearLoupe()` | ルーペを非表示にして拡大元画像と中心を解放する |
| `RequestRedraw()` | 選択オーバーレイの再描画を要求する |
| `NotifyViewportChanged()` | 表示の変化（パン・ズーム・表示サイズ）へ追従させる |

## 5. `CaptureSessionControl`

撮影と切り出しのセッションを統括する窓口（`IDisposable`）です。 **本ライブラリの公開 API は本クラスと上記のコントロールです。**

| メンバー | 説明 |
| --- | --- |
| `CaptureCompleted`（イベント） | キャプチャ完了を通知する（**同期実行**）。 引数は結果（キャンセル時は null） |
| `AppearanceSettings`（`CaptureSettings`） | 現在の設定（全メンバー non-null）。 setter は部分更新であり、null 以外のメンバーだけを反映する |
| `IsCropping`（`bool`） | 切り出しセッションが進行中かどうか |
| `LogPrefix`（`string?`） | ログの接頭辞 |

| メソッド | 説明 |
| --- | --- |
| `StartCaptureAsync(this CaptureType type, Form owner)` → `Task<CaptureResult?>` | 撮影を開始する。 キャンセル・取得失敗・退化した選択の場合は null。 実行中の要求は無視する |
| `StartCropAsync(this CropViewControl cropView, Form hostWindow)` → `Task<EsRectInt?>` | 切り出しを開始する。 確定時は元画像上の範囲、キャンセル時は 0x0 の空矩形、開始できなかった場合は null |
| `ResolveCropAction()` → `CropSessionAction` | 切り出し操作（開始・確定・キャンセル）のうち、いま行うべき操作を返す（キーとボタンで同じ規則を使う） |
| `Cancel()` | 進行中の操作をキャンセルする（切り出しは 0x0 の空結果で完了する） |
| `Confirm()` | 進行中の切り出しを現在の選択矩形で確定する（Enter キー相当） |
| `NotifyViewportChanged()` | 実行中の切り出しへビューポートの変化を通知する |
| `Dispose()` | 進行中のセッションをキャンセルして完了させる |

**画像の反映はホストが行います**（Undo 退避・画像の差し替えはアプリ側の責務です）。 キャプチャ完了はオーナーウィンドウの再表示より前に発火するため、ハンドラで画像を差し替えてから再表示できます。

## 6. イベント引数

### 6.1 `ZoomChangedEventArgs`

| メンバー | 説明 |
| --- | --- |
| `State`（`ZoomState`） | 通知時点のズーム状態（実効ズーム率・Fit 状態・可否） |
| `ZoomPercent`（`double`） | 実効ズーム率（%）。 Fit 中でも 0 にならない |
| `IsFit`（`bool`） | Fit 表示中かどうか |
| `CanZoomIn` / `CanZoomOut`（`bool`） | ズームの可否 |

### 6.2 `LoupePositionEventArgs`

| メンバー | 説明 |
| --- | --- |
| `Position`（`EsPoint`） | ルーペ中心（対象座標） |

### 6.3 `SelectionConfirmedEventArgs`

| メンバー | 説明 |
| --- | --- |
| `SelectionSourceRect`（`EsRect`） | 確定した選択矩形（対象座標） |

## 7. 内部の型

テスト（`InternalsVisibleTo`）からのみ参照できる型です。 ホストは公開 API だけを使います。

### 7.1 `ZoomGraphicsRenderer`（内部・描画層）

| メソッド | 説明 |
| --- | --- |
| `Render(this Graphics graphics, Bitmap? source, ZoomViewSession session)` | セッションが確定した表示矩形へ画像を 1 回で描画する |
| `RenderScrollBar(this Graphics graphics, ZoomScrollBar? bar, Color trackColor, Color thumbColor, bool drawTrack, Color borderColor, double borderThickness)` | スクロールバー 1 本を描画する（トラック → 枠 → サムの順。 枠はトラックの内側へ描く） |

### 7.2 `RectOverlayCanvas`（内部・オーバーレイ コントロール）

| メンバー | 説明 |
| --- | --- |
| `RectOverlayCanvas(this ZoomViewControl zoomView)` | 座標変換の基準とするズームビューを受け取って初期化する |
| `SetRects(this IReadOnlyList<OverlayRect> rects)` | 描画する矩形（対象座標）を設定する |

### 7.3 `RectOverlayGraphicsRenderer`（内部・描画層）

| メソッド | 説明 |
| --- | --- |
| `Render(this Graphics graphics, OverlayRectFrame frame)` | コアが確定した描画入力を GDI+ へ描画する（実線は前景色の枠、破線は背景色の実線 → 前景色の破線の 2 回描画） |

### 7.4 `SelectionOverlayGraphicsRenderer`（内部・描画層）

| メソッド | 説明 |
| --- | --- |
| `DrawSelectionRect(this Graphics graphics, EsRect selectionSourceRect, EsPoint captureTopLeft, double scale, int pixelSize, Color colorA, Color colorB)` | ルーペ画像上の選択矩形の破線を描画する（幾何はコアが列挙する） |

### 7.5 `CaptureOverlay`（内部・撮影セッション）

| メンバー | 説明 |
| --- | --- |
| `CaptureOverlay(this CaptureType type, CaptureSettings settings, IElementFinder finder, ILogger? log, Action<CaptureResult?>? onCaptured = null)` | 1 セッション分のオーバーレイを生成する |
| `RunAsync(this Form owner, CaptureOverlayServices services)` → `Task<CaptureResult?>` | 撮影セッションを実行し、確定した領域を返す |
| `Cancel()` | 進行中の撮影をキャンセルする |

### 7.6 `CaptureOverlayServices`（内部・サービスの束）

撮影セッションが必要とするサービス（画面キャプチャ・要素検出・ウィンドウ操作・ロガー）の束です。

### 7.7 `CropSession`（内部・切り出しセッション）

| メンバー | 説明 |
| --- | --- |
| `HasSelection`（`bool`） | 選択矩形が有効かどうか |
| `RunAsync(this EsReadOnlyBitmap sourceImage)` → `Task<EsRectInt?>` | 切り出しセッションを実行する（確定待機 → 結果返却） |
| `Cancel()` / `Confirm()` | 進行中の切り出しのキャンセル・確定 |
| `NotifyViewportChanged()` | ビューポートの変化をオーバーレイへ通知する |
| `ApplySettings(this CaptureSettings newSettings)` | 実行中に最新の設定を再適用する |

### 7.8 `OverlayHitTest`（内部・入力の透過）

| メンバー | 説明 |
| --- | --- |
| `ShouldPassThrough(this int message, bool enabled)` → `bool` | 受信したウィンドウ メッセージを下のウィンドウへ通すかどうかを判定する（規則を 1 箇所に閉じる） |

## 8. 使用例

ホスト（アプリ）が行う結線は「コントロールを置く」「開始する」「結果を適用する」の 3 つだけです。

```csharp
// 切り出しビューの外観と文言はホストが与える
cropView.Appearance = ZoomViewAppearance.Default with { ShowScrollBars = true };
cropView.OperationTextProvider = () => CaptureLabelTexts.GetOperationText(cropView.SelectionMode);
cropView.SizeLabelOverrideProvider = () => CropLabelTexts.ResolveSizeLabel(
    cropView.HasSelection, cropView.SelectionSourceRect, cropView.ZoomPercent);

// ズーム操作の可否はイベントで受け取り、ボタンの有効・無効へ写す
cropView.ZoomChanged += (_, e) =>
{
    zoomInButton.Enabled = e.CanZoomIn;
    zoomOutButton.Enabled = e.CanZoomOut;
};
```

## 9. 制限事項

| 制限 | 内容 |
| --- | --- |
| 描画の補間 | 画像の拡大・縮小は最近傍である（`EsUtil.Ui.WinForms.Interop` の描画層が定める） |
| 横ホイール | WinForms の `MouseEventArgs` から横方向の回転量を取得できないため、常に縦方向として扱う |
| 座標変換の精度 | `CropViewControl.TranslatePositionFrom` は整数の座標（`Point`）を経由するため、小数部が丸められる |
| ルーペの配置 | `SelectionOverlayControl` はレイアウト時にルーペの位置とサイズを再解決する（親のサイズ変更に追従する）。 移動時は**移動前の領域を内包する `MarqueeControl` で無効化**して描き直す（本コントロールは背景を描かず、全面を覆う子が画像を透かして描くため、自分の無効化では残像が残る） |
| 撮影の対象範囲 | 全モニター（仮想スクリーン）である（1 モニターに限定しない） |
| モーダル表示 | 撮影オーバーレイはモーダル ダイアログではない（入れ子ループを作らないため、ホストは操作を無効化して排他を保つ） |

## 10. 前提条件

| 項目 | 内容 |
| --- | --- |
| 対象フレームワーク | `net10.0-windows`（`UseWindowsForms` を有効にする） |
| プラットフォーム | Windows のみ（Win32 の画面キャプチャ・要素検出・ウィンドウ操作を使う） |
| UI スレッド | `StartCaptureAsync` / `StartCropAsync` は UI スレッドから呼び出す（コントロールの生成と描画を伴う） |
| 切り出しの対象画像 | `CropViewControl.Source` が単一所有する。 画像を別引数で渡さない（二重に所有しない） |
| 座標系 | 切り出しと選択の対象は**元画像ピクセル**。 撮影の対象は**画面物理ピクセル**（仮想スクリーンの原点は負値になりうる） |
| 画像の所有権 | `CropViewControl.Source` に設定した画像はホストが所有する（コントロールは破棄しない） |
