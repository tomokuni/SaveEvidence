# Parts.CapturePreview.ControlMewUI API リファレンス

MewUI 版の撮影セッションが提供する**公開 API** を記載します。 内部実装（描画手順・キャッシュ等）は記載しません。

## 目次 <!-- omit from toc -->

- [1. 公開型の構成](#1-公開型の構成)
- [2. CaptureSessionControl](#2-capturesessioncontrol)
  - [2.1 排他の規則](#21-排他の規則)
  - [2.2 動作](#22-動作)
  - [2.3 操作](#23-操作)
  - [2.4 座標系](#24-座標系)
  - [2.5 切り出し規則](#25-切り出し規則)
- [3. 内部で生成するサービス](#3-内部で生成するサービス)
- [4. 使用する設定項目](#4-使用する設定項目)
  - [4.1 適用先](#41-適用先)
  - [4.2 切り出し（CropSession）](#42-切り出しcropsession)
- [5. CropViewControl（切り出しビュー）](#5-cropviewcontrol切り出しビュー)
  - [5.1 プロパティ](#51-プロパティ)
  - [5.2 メソッド](#52-メソッド)
  - [5.3 イベント](#53-イベント)
  - [5.4 矩形オーバーレイの仕様](#54-矩形オーバーレイの仕様)
  - [5.5 エラー時の挙動](#55-エラー時の挙動)
- [6. SelectionOverlayControl（領域選択）](#6-selectionoverlaycontrol領域選択)
  - [6.1 動作モード](#61-動作モード)
  - [6.2 プロパティ](#62-プロパティ)
  - [6.3 メソッド](#63-メソッド)
  - [6.4 イベント](#64-イベント)
  - [6.5 座標系と操作](#65-座標系と操作)
  - [6.6 エラー時の挙動](#66-エラー時の挙動)
- [7. ZoomViewControl（切り出しビューの画像ビューポート）](#7-zoomviewcontrol切り出しビューの画像ビューポート)
  - [7.1 役割](#71-役割)
  - [7.2 ズーム遷移ルール](#72-ズーム遷移ルール)
  - [7.3 座標系と Fit 表示](#73-座標系と-fit-表示)
  - [7.4 公開 API](#74-公開-api)
  - [7.5 エラー時の挙動](#75-エラー時の挙動)

## 1. 公開型の構成

| 型 | 種別 | 役割 |
| --- | --- | --- |
| `CaptureSessionControl` | sealed クラス（Facade、`IDisposable`） | 撮影と切り出しの窓口。 排他（同時に 1 つだけ）とセッションの寿命、および撮影サービスの生成を単一所有する |
| `CropViewControl` | sealed クラス（複合コントロール） | 画像ビューポート（拡大・パン・Fit）+ 領域選択 + 矩形オーバーレイ + 強調オーバーレイ |
| `SelectionOverlayControl` | sealed クラス（複合コントロール） | 暗転マスク + 選択矩形 + ルーペ。 モードで可視性を切替 |
| `SelectionConfirmedEventArgs` | sealed class（`EventArgs` 派生） | 確定要求のイベント引数（`SelectionSourceRect: EsRect`） |
| `LoupePositionEventArgs` | sealed class（`EventArgs` 派生） | ルーペ位置のイベント引数（`Position: EsPoint`） |
| `ZoomChangedEventArgs` | sealed class（`EventArgs` 派生） | ズーム状態の変更通知（`ZoomState` の 4 値） |

`OverlayRect` / `OverlayLineStyle`（矩形オーバーレイの描画情報）と `CaptureResult` / `CaptureCompletedEventArgs`（撮影結果と完了通知の引数）は `Parts.CapturePreview.Core`（名前空間 `EsUtil.SaveEvidence.Parts.CapturePreview.Core.Overlay` / `...Core.Capture` / `...Core.Session`）が提供します。

名前空間は `Parts.CapturePreview.ControlMewUI` です。

`CaptureOverlay`（撮影セッション） / `CropSession`（切り出しセッション） / `CaptureOverlayServices`（撮影サービスの束）は `internal` です
（1 セッション = 1 インスタンスであり、生成・実行・破棄・排他の受付・サービスの生成は Facade が担います）。

## 2. CaptureSessionControl

| メンバー | シグネチャ | 内容 |
| --- | --- | --- |
| コンストラクタ | `CaptureSessionControl(CaptureSettings? initialSettings = null, ILogger? logger = null)` | Facade を生成する。 `initialSettings` の null メンバーは既定値で埋める。 撮影サービス（キャプチャ・要素検出・ウィンドウ操作）は本クラスが生成・保持する |
| `StartCaptureAsync` | `Task<CaptureResult?> StartCaptureAsync(CaptureType type, Window owner)` | オーバーレイを表示し、確定するまで待つ。 キャンセル・取得失敗・退化した選択の場合は null。 **実行中のセッションがある場合は何もせず null を返す** |
| `StartCropAsync` | `Task<EsRectInt?> StartCropAsync(CropViewControl cropView, Window hostWindow)` | 切り出しを開始し、確定・キャンセルまで待つ。 確定は元画像上の切り出し範囲（コア型）、キャンセルは 0x0 の空矩形、開始できなかった場合は null。 対象画像は `cropView.Source` から解決する（画像を引数で渡さない） |
| `ResolveCropAction` | `CropSessionAction ResolveCropAction()` | 切り出しの開始・確定・キャンセルのうち、いま行うべき操作を返す。 判断はコアの `CropSessionActions.Resolve` が単一所有する（未開始なら開始 / 選択済みなら確定 / 未選択ならキャンセル）。 **ホストは戻り値を効果へ写すだけ**にする |
| `Confirm` | `void Confirm()` | 進行中の切り出しを現在の選択矩形で確定する（Enter キー相当） |
| `Cancel` | `void Cancel()` | 進行中の操作をキャンセルする（切り出しは 0x0 の空結果で完了、撮影はオーバーレイを閉じる） |
| `IsCropping` | `bool` | 切り出し中かどうか（真のソースは排他スロット。 ホストが自前のフラグで判定しない） |
| `NotifyViewportChanged` | `void NotifyViewportChanged()` | 実行中の切り出しセッションへビューポート（パン・ズーム・表示サイズ）の変化を通知する（暗転マスク・選択枠・ルーペの追従）。 未実行時は何もしない |
| `AppearanceSettings` | `CaptureSettings` | 現在の設定（getter は全メンバー non-null）。 設定時は null メンバーのみ現在値を維持する部分更新。 **同一値の再設定はスキップ**。 検証・適用ログはコア（`CaptureSettingsMerge.Apply`）が単一所有する。 実行中の切り出しセッションへは即時反映する |
| `Dispose` | `void Dispose()` | リソースを解放する（進行中のセッションはキャンセルする）。 以降の公開操作は `ObjectDisposedException` |
| `LogPrefix` | `string?` | ログの接頭辞（ロガーと一緒に設定する。 null・空白で既定値 `[CapturePreview]` へ戻す） |

| イベント | シグネチャ | 内容 |
| --- | --- | --- |
| `CaptureCompleted` | `event EventHandler<CaptureCompletedEventArgs>? CaptureCompleted` | キャプチャ完了（同期実行。 UI スレッドで発行。 結果は `e.Result`）。 **オーナーの再表示より前に発行される**ため、ホストは本ハンドラで画像を差し替えてから再表示される（フロー: APP 非表示 → 撮影 → 画像変更 → APP 表示） |

### 2.1 排他の規則

撮影（`StartCaptureAsync`）と切り出し（`StartCropAsync`）は**同時に 1 つだけ**実行できます。  
この規則は `Parts.CapturePreview.Core` の `CaptureSessionSlot` が単一所有し、本 Facade がその唯一の適用者です。  
実行中の要求は無視し、**実行中のセッションを中断しません**（撮影中に切り出しを開始できない・切り出し中に撮影を開始できない）。

### 2.2 動作

1. 設定（Facade の `AppearanceSettings`）に従ってオーナーウィンドウをキャプチャ除外に設定し、画面外へ退避します（`CaptureOwnerScope` が担う）。
2. 仮想スクリーン（全モニターを包含する領域）を 1 枚の画像として取得します（`ICaptureService.CaptureVirtualScreen`）。
3. 画像を敷いたボーダーレス・最前面のウィンドウを仮想スクリーン全面へ表示し、確定またはキャンセルまで待ちます（`ShowDialogAsync`。 **入れ子ループを作らない非同期の待機**）。
4. 確定した領域を切り出して返し、オーナーの配置を復帰して例外を解除します（例外時も `finally` で復帰）。

### 2.3 操作

| モード | 操作 |
| --- | --- |
| スクリーン選択 | カーソル下のモニターをハイライトし、左クリックまたは Enter で確定 |
| ウィンドウ選択 | カーソル下の最前面ウィンドウをハイライトし、左クリックまたは Enter で確定 |
| 範囲選択 | ドラッグで矩形を指定し、Enter で確定（矢印キーで微調整できる） |

- 右クリックでは確定しません。 要素を検出できない位置のクリックでも確定しません（誤操作で撮影を取りこぼさないため）。
- **左クリックの確定は押下で要素を固定し、解放時に行います**（押下を伴わない解放・退化した検出では確定しない。 固定と取り出しの規則はコアの `CaptureClickSelection` が単一所有する）。
- **範囲選択では十字カーソルを表示します**（選択スタイルの判定はコアの `CaptureTypeTraits.IsHighlighted` が単一所有し、ハイライト型では表示しない。 Avalonia 版と同じ見え方にする）。
- **Enter（Return を含む）= 確定 / Escape = キャンセル**です（確定対象が無い状態の Enter は何もしません。 キーの解釈はコアの `CaptureKeyActions` が単一所有する）。
- Escape または他ウィンドウへの切り替え（非アクティブ化）でキャンセルします。
- オーバーレイはタスクバー・Alt+Tab の一覧に表示しません（一時的な表示のため）。

### 2.4 座標系

| 座標 | 意味 |
| --- | --- |
| 画像ピクセル（対象） | 取得画像のピクセル。 **画像の左上が原点**（画像の左上は撮影範囲の原点 = 仮想スクリーンの原点に対応するため、画面物理座標とは原点の分だけ異なる。 原点は負値になりうる） |
| 画面物理ピクセル | 検出結果・入力（マウス）の座標系 |

対象座標とローカル DIP の変換は `ICoordTransform` を選択コントロールへ注入して行います（コントロールは座標系を意識しません）。  
確定時の切り出し矩形は**画面物理座標の入口**（`CropClipMath.ResolveCaptureRect`）で解決します。 選択矩形は画像ローカル座標（画像の左上が原点）のため、**境界で `CropClipMath.ToScreenRect` により画面物理座標へ戻してから**渡します（１つの座標空間に揃えることで、原点が負値のマルチモニタでも切り出し位置・幅が正しくなる）。

確定要求（キー・クリック・アダプタ要求）は、すべて同じ入口（画面物理座標）で `CapturePreviewLogMessage.LogConfirmRequested` に記録します（記録する矩形の座標空間を UI  間で揃える）。 完了の領域（`CaptureResult.SourceBounds`）も契約どおり仮想画面座 標（画面物理）で返します。

### 2.5 切り出し規則

- 範囲選択は**閉区間**（始点・終点のピクセルを含む）で切り出します。
- スクリーン選択・ウィンドウ選択は検出結果が全ピクセルを含むため、終点の 1 ピクセルを加算しません。
- 切り出し矩形は画像の範囲へクランプします（画像の右端・下端で透明な余白が付かないようにするため）。
- 切り出せる矩形が無い（退化した選択）場合はキャンセル扱い（null）になります。
- 計算は `Parts.CapturePreview.Core` の `CropClipMath.ResolveCaptureRect` が単一所有します（種別の性質とクランプ・丸めを組み合わせた唯一の入口）。
- 最小サイズは選択操作（Marquee）が判定します（検出矩形は要素の大きさであり、小さい要素も切り出し対象とする）。

## 3. 内部で生成するサービス

Facade は次のサービスをコンストラクタで生成し、撮影セッションへ渡します（公開 API ではありません）。

| 役割 | 抽象（`EsUtil.Platform.Core`） | Windows 実装 |
| --- | --- | --- |
| 画面キャプチャ（仮想スクリーンの取得） | `ICaptureService` | `WinCaptureService` |
| 要素検出（カーソル下のモニター・ウィンドウ） | `IElementFinder` | `WinElementFinder` |
| ネイティブウィンドウ操作（キャプチャ除外・退避・物理ピクセルでの配置） | `IWindowService` | `WinWindowService` |

**ホストはサービスを組み立てない**: サービスの生成・保持は Facade が担う（Avalonia 版と同じ分担）。 ライブラリはアプリの型を参照せず、UI 非依存の抽象だけをコアから受け取ります。

**設定は含めません**: 設定は Facade（`AppearanceSettings`）が単一所有し、撮影と切り出しが同じ値を使います（ホストが設定の変換を 2 回呼ぶ・2 つの設定値が食い違う余地をなくす）。

## 4. 使用する設定項目

`CaptureSettings`（`Parts.CapturePreview.Core`）のうち、本ライブラリが参照する項目です。

| 項目 | 用途 |
| --- | --- |
| `HideOwnerWindow` | 撮影中にオーナーウィンドウを画面外へ退避するか |
| `MinSelectionSize` | 選択矩形のドラッグ中の最小サイズ（`MarqueeOptions.MinSelectionSize` へ反映。 切り出しの可否は選択操作そのものが判定する） |
| `BorderColor` / `BorderThickness` / `OverlayColor` | 選択枠と暗転の外観（太さと色は提示層の `MarqueeAppearance`（`OuterBorderThickness` / `OuterBorderColor` / `OverlayColor`）へ反映。 枠線は選択矩形と共用） |
| `HandleColor` / `HandleSize` / `HandleHitRadius` | ドラッグハンドル（寸法は選択矩形の設定、色は提示層の `MarqueeAppearance.HandleColor` へ反映） |
| `MarkerColor` / `MarkerLength` | 微調整時の十字マーカー（寸法は選択矩形の設定、色は提示層の `MarqueeAppearance.MarkerColor` へ反映） |
| `ClampToSourceBounds` | 画像外の選択を禁止するか |
| （ホイール操作） | 選択オーバーレイ上のホイールをビューポート（ズーム・パン）へ転送する。 ズーム率・パン量の決定は `CropViewControl` が単一所有する |
| `ShowCoordLabel` / `CoordLabelColor` / `CoordLabelBackgroundColor` / `CoordLabelFontSize` | カーソル位置の座標ラベル（表示可否・フォントサイズ・色は提示層の `MarqueeAppearance` へ反映） |
| `SizeLabelVisible` / `SizeLabelColor` / `TextBackgroundColor` / `SizeLabelFontSize` | サイズラベル（表示可否・フォントサイズ・色は提示層の `MarqueeAppearance` へ反映。 **文言はホストが単一所有する**） |
| `InfoLabelVisible` / `InfoLabelColor` / `InfoLabelBackgroundColor` / `InfoLabelFontSize` | 情報ラベル（ウィンドウタイトル。 `SelectionOverlayLabelOptions`（ホストが描く付加情報ラベルの外観）へ反映） |
| `ShowSubLines` / `SubLineFontSize` / `SubLineColor` / `SubLineBackgroundColor` | 操作ラベル（サブライン。 `SelectionOverlayLabelOptions` へ反映） |
| `LoupeSize` / `LoupeZoomLevel` / `LoupeOffset` / `LoupeCrossColor` / `LoupeFrameColor` / `LoupeFrameThickness` | ルーペ（サイズ・倍率・オフセットは `LoupeOptions`、枠線の太さは `LoupeAppearance.BorderThickness`、色は外観 `LoupeAppearance` へ反映） |
| `LoupePasteboardColor` | ルーペの画像外（ペーストボード）の色（外観 `LoupeAppearance.PasteboardColor` へ反映） |
| `LoupeSelectionRectColorA` / `LoupeSelectionRectColorB` | ルーペ画像上の選択矩形破線（選択矩形破線の色（`SelectionOverlayOptions`）へ反映） |
| `LoupeShowCoordLabel` / `LoupeCoordLabelColor` / `LoupeCoordLabelBackgroundColor` / `LoupeCoordLabelFontSize` | ルーペの中心座標ラベル（表示可否・フォントサイズ・色は外観 `LoupeAppearance` へ反映） |

### 4.1 適用先

上表の項目は撮影（`CaptureOverlay`）と切り出し（`CropSession`）が同じマッピングで反映します。 **計算に必要な寸法・対話パラメータ**はコアの `SelectionOverlayOptionsMapper` が単一所有し、**描画にだけ必要な外観（色・枠線の太さ・表示可否）**は `Parts.CapturePreview.Core` の `CapturePaintMapper`（`MarqueeAppearance` / `LoupeAppearance` を組み立ててコントロールのプロパティへ設定する）が担います。
ただし、切り出しはプレビューの表示に合わせる必要があるため、次の項目は撮影専用です。

| 項目 | 理由 |
| --- | --- |
| `LoupeClientToPixelScaling` / `DisplayScaleScaling` | 切り出しのルーペ・ラベルはプレビューの表示倍率（`CropViewControl` が所有）に従うため、設定値を与えない |

情報ラベルはキャプチャコアの `CaptureLabelTexts.GetInfoText` がウィンドウ選択に限定するため、切り出しでは表示されません。

### 4.2 切り出し（CropSession）

切り出しは Facade の `StartCropAsync` / `ResolveCropAction` / `Confirm` / `Cancel` からのみ操作します（`CropSession` は internal のため、ホストが直接操作することはありません）。  
切り出しセッションは開始時に `CropViewControl` の領域選択オーバーレイを可視化し、確定・キャンセルで非表示に戻します（ホストが表示ツリーへ加える必要はありません）。

| 操作 | 内容 |
| --- | --- |
| 開始 | 領域選択オーバーレイを可視化し、入力を選択コントロールへ移す（画像が未設定の場合は開始せず null を返す） |
| 確定（Enter 相当） | 現在の選択矩形を切り出す（退化した選択はキャンセルとして記録し、0x0 の空矩形を返す） |
| キャンセル（Escape 相当） | 選択を破棄してオーバーレイを非表示に戻す（0x0 の空矩形を返す） |

切り出しの適用先はホスト（アプリ）です。 切り出し結果（`EsRectInt`）をアプリのコマンドへ渡し、Undo 退避・画像差し替えを行うのはホストの責務です（Avalonia 版と同じ契約。 ライブラリはアプリの型を参照しません）。

## 5. CropViewControl（切り出しビュー）

画像の表示（拡大・パン・Fit）・領域選択・ホストから渡された矩形のオーバーレイ描画を 1 つのコントロールにまとめた複合コントロールです（Facade / Composite パターン）。 画像ビューポート（`ZoomViewControl`）と領域選択オーバーレイ（`SelectionOverlayControl`）を内包し、その上に透明な矩形オーバーレイと強調オーバーレイを重ねます（重なり順は「ズームビュー → 領域選択 → 矩形 → 強調」）。 領域選択オーバーレイは初期状態で非表示・ヒットテスト不可であり、切り出しセッションが `SetSelectionOverlayVisible` で切り替えます。

名前空間は `Parts.CapturePreview.ControlMewUI`、クラスは `CropViewControl`（`public sealed`、`Grid` 派生）です。

### 5.1 プロパティ

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `Source` | `EsReadOnlyBitmap?` | `null` | 表示する画像。 設定すると Fit 表示（ズーム率 0・オフセット 0）へリセットする |
| `CenterAlign` | `bool` | `true` | 画像がビューポートより小さい軸を中央寄せする（false で左上寄せ） |
| `Appearance` | `ZoomViewAppearance` | 暗色の下地 | 下地色の外観の**単一値**。 コアが単一所有する |
| `Rects` | `IReadOnlyList<OverlayRect>` | 空 | オーバーレイとして描画する矩形（対象座標 = 元画像ピクセル座標）。 null は設定できない |
| `HighlightSourceRect` | `EsRect?` | `null` | 強調する矩形（対象座標 = 元画像ピクセル座標）。 `null` は強調なし。 対象座標 → ビューポート座標の写像は本コントロールが単一所有する（ホストは座標変換を持たない） |
| `HighlightAppearance` | `MarqueeAppearance`（提示層） | 内側の塗りと枠線だけ | 強調の外観（色・枠線の太さ・表示可否）の**単一値**。 既定は `CropViewControl.HighlightDefaultAppearance` |
| `ZoomView` | `ZoomViewControl` | — | 内包する画像ビューポート |
| `ZoomPercent` | `double` | Fit の実効倍率 | 実効ズーム率（%）。 **0 より大きい値のみ設定可能**（0 以下は無視） |
| `CanZoomIn` / `CanZoomOut` | `bool` | `false` | ズーム可否（画像未設定時は false） |
| `Log` | `ILogger` | `NullLogger` | ズーム操作ログの出力先（内包する画像ビューポートへ伝搬する） |
| `LogPrefix` | `string?` | `null` | ログの接頭辞（ロガーと一緒に設定する。 null・空白で既定値 `[CapturePreview]` へ戻す） |
| `SelectionMode` | `SelectionOverlayMode`（コア） | `Crop` | 内包する領域選択オーバーレイの動作モード |
| `SelectionOverlayOptions` | `SelectionOverlayOptions`（コア） | `Default` | 領域選択の設定の単一値（内包する `SelectionOverlayControl` へ委譲する） |
| `SelectionOverlayMarqueeAppearance` / `SelectionOverlayLoupeAppearance` | 提示層の外観 | 各 `Default` | 領域選択の描画にだけ必要な外観（選択矩形・ルーペ） |
| `HasSelection` / `SelectionSourceRect` | `bool` / `EsRect` | `false` / 空矩形 | 領域選択の有無と現在値（対象座標 = 元画像ピクセル座標） |
| `SizeLabelOverrideProvider` / `OperationTextProvider` / `InfoTextProvider` | デリゲート | `null` | 領域選択の付加情報ラベルの文言（描画時に評価する。 文言はホストが単一所有する） |
| `ClientToSourceScaling` / `LoupeClientToPixelScaling` | `double` | 基準値 | DPI スケーリング（設定には含めない環境値） |

### 5.2 メソッド

| メソッド | 説明 |
| --- | --- |
| `ZoomIn()` / `ZoomOut()` | 段階レベルを 1 つ移動する（移動先がなければ何もしない） |
| `ZoomFit()` | Fit 表示へ戻す |
| `PanByWheel(EsVector, bool)` | ホイールの回転量（X/Y）と Shift 併用でパンする |
| `ZoomByWheel(bool, EsPoint?)` | Ctrl+ホイールのズームを適用する（位置はズーム中心。 null はビューポート中心） |
| `TranslatePositionFrom(Element, EsPoint)` | 重ねた要素のローカル座標を本コントロールの座標へ変換する |
| `ImageRectViewport()` | 画像の表示矩形（ビューポート座標・DIP） |
| `ClientToSource(EsPoint)` | ビューポート座標 → 対象座標（画像表示矩形の原点基準） |
| `SourceToClient(EsPoint)` | 対象座標 → ビューポート座標（画像表示矩形の原点基準） |
| `SetSelection(EsRect)` / `ClearSelection()` | 領域選択の外部設定・クリア |
| `SetSelectionOverlayVisible(bool)` | 領域選択オーバーレイの可視性とヒットテストを切り替える（切り出しセッションが使用する。 非表示へ戻すときは選択も破棄し、描画を要求する） |
| `SetCoordTransform(ICoordTransform?)` | 領域選択の座標変換ポートを設定する（null の場合は倍率からコアが解決する） |
| `SetLoupeSource(EsReadOnlyBitmap?)` / `ClearLoupe()` | ルーペのソース画像の設定・解除 |
| `RequestConfirm()` / `RequestCancel()` | Enter / Escape 相当の確定・キャンセル要求（領域選択へ委譲する） |
| `FocusMarquee()` | 選択矩形へフォーカスを設定する（矢印キー nudge の受付開始） |
| `NotifyViewportChanged()` | ビューポートの変化（パン・ズーム・リサイズ）を領域選択オーバーレイへ通知する |

### 5.3 イベント

| イベント | 引数 | 説明 |
| --- | --- | --- |
| `ZoomChanged` | `ZoomChangedEventArgs` | ズーム率・Fit 状態・ズーム可否（内包する画像ビューポートから中継） |
| `ViewportChanged` | `EventArgs` | パン・ズーム・リサイズ（内包する画像ビューポートから中継） |
| `ConfirmRequested` | `SelectionConfirmedEventArgs` | 領域選択の確定要求（内包する `SelectionOverlayControl` から中継。 切り出しセッションが購読する） |
| `CancelRequested` | `EventArgs` | 領域選択のキャンセル要求（内包する `SelectionOverlayControl` から中継） |

### 5.4 矩形オーバーレイの仕様

| 項目 | 仕様 |
| --- | --- |
| 座標系 | 対象座標（元画像ピクセル）。 表示時にビューポート座標へ変換する |
| 実線（`Solid`） | 前景色（`Foreground`）の枠のみを描画する |
| 破線（`Dashed`） / 点線（`Dotted`） | 背景色（`Background`）の実線の上へ前景色の破線を重ねる（市松風の視認性） |
| ダッシュ長 | 破線 = 8 / 間隔 4、点線 = 2 / 間隔 2（`Parts.CapturePreview.Core` の `OverlayLineDash` が単一所有） |
| 線の太さ | `Thickness`（DIP）。 0 以下は 1 として扱う。 枠は矩形の内側へ描画する |
| サイズ 0 の矩形 | 何も描画しない |
| 色 | `System.Drawing.Color`（アルファを含む） |

### 5.5 エラー時の挙動

| 状況 | 挙動 |
| --- | --- |
| `Source` が `null` | 何も描画しない（例外にしない） |
| `Rects` が空 | 何も描画しない（例外にしない） |
| `ZoomPercent` へ 0 以下を設定 | 無視する（Fit へは `ZoomFit()` を使う） |
| `Source` が `null` で `HighlightSourceRect` を設定 | 何も描画しない（写像の基準が無いため。 例外にしない） |

領域選択は本コントロールが内包する `SelectionOverlayControl` が担います（[6. SelectionOverlayControl](#6-selectionoverlaycontrol領域選択)）。

## 6. SelectionOverlayControl（領域選択）

選択矩形（ドラッグ）・暗転マスク・ルーペを 1 つのコントロールにまとめ、配線と状態同期を内側で完結させる複合コントロールです（Facade / Composite パターン）。 ホストは `Mode` の設定と状態の受け取りだけで利用できます。 動作モードとモードから描画設定への写像は UI 非依存コア（`Parts.CapturePreview.Core`）が単一所有します。

名前空間は `Parts.CapturePreview.ControlMewUI`、クラスは `SelectionOverlayControl`（`public sealed`、`Panel` 派生）です。

### 6.1 動作モード

| モード | 選択矩形 | ルーペ | 全面暗転 | 用途 |
| --- | --- | --- | --- | --- |
| `Crop` | 有効 | 有効 | 有効 | プレビュー内の切出し |
| `AreaSelect` | 有効 | 有効 | 有効 | 全画面オーバーレイの領域選択 |
| `ScreenSelect` | 無効 | 無効 | 無効 | スクリーン選択（ハイライトのみ。 確定フローはホスト） |
| `WindowSelect` | 無効 | 無効 | 無効 | ウィンドウ選択（ハイライトのみ。 確定フローはホスト） |

既定値は `AreaSelect` です。 未定義の値を設定した場合は `WindowSelect` と同じ扱いになります。

### 6.2 プロパティ

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `Mode` | `SelectionOverlayMode` | `AreaSelect` | 動作モード（可視性と全面暗転を一括切替） |
| `Options` | `SelectionOverlayOptions`（コア） | `Default` | 設定の単一値（選択矩形 / 暗転マスク / ルーペ / 選択矩形破線。 色は含まない）。 同じ値の再設定はスキップされる |
| `MarqueeAppearance` / `LoupeAppearance` | 提示層の外観 | 各 `Default` | 描画にだけ必要な外観の単一値（コアの設定値には含まれない）。 `MarqueeAppearance` の設定時はモードから導出する表示可否を上書きし直す |
| `HasSelection` / `SelectionSourceRect` | `bool` / `EsRect` | `false` / 空矩形 | 選択矩形の有無と現在値（対象座標） |
| `HighlightSourceRect` | `EsRect?` | `null` | ハイライト矩形（対象座標。 **null はハイライトなし**。 選択矩形より優先） |
| `ShowFullOverlay` | `bool`（取得のみ） | `true` | 全面暗転の有無（モードから導出） |
| `LoupeCenterPoint` | `EsPoint?`（取得のみ） | `null` | ルーペ中心（対象座標） |
| `ClientToSourceScaling` / `DisplayScaleScaling` / `LoupeClientToPixelScaling` | `double` | 各 `1.0` 等 | DPI スケーリング（設定には含めない環境値） |
| `SizeLabelOverrideProvider` / `OperationTextProvider` / `InfoTextProvider` | `Func<string?>?` | `null` | ラベル文言を描画時に評価するデリゲート（null または空文字は描画しない。 文言はホストが与える） |
| `Log` | `ILogger` | `NullLogger` | 操作ログの出力先（内包する選択コントロールとルーペへ伝搬する） |
| `LogPrefix` | `string?` | `null` | ログの接頭辞（ロガーと一緒に設定する。 null・空白で既定値へ戻す） |

`Options`（コアの `SelectionOverlayOptions`）の主な項目は、`Marquee`（`MarqueeOptions`。 寸法・表示可否。 色は含まない）/ `Loupe`（`LoupeOptions`）/ `SelectionRectColorA` / `SelectionRectColorB`（ルーペ画像上の選択矩形破線の 2 色）/ `LoupeOffset`（中心からの右下オフセット。 DIP）です。

### 6.3 メソッド

| メソッド | 説明 |
| --- | --- |
| `SetCoordTransform(ICoordTransform?)` | 座標変換ポートを一括設定する（選択矩形・暗転マスク・ルーペの配置で共用。 null は倍率からコアが解決する） |
| `SetSelection(EsRect)` / `ClearSelection()` | 選択状態の外部設定・クリア |
| `SetLoupeSource(EsReadOnlyBitmap?)` / `SetLoupePosition(EsPoint)` / `ClearLoupe()` | ルーペの制御 |
| `RequestConfirm()` / `RequestCancel()` | 確定要求・キャンセル要求（Enter / Escape 相当） |
| `FocusMarquee()` | 選択矩形へフォーカスを設定する（矢印キー nudge の受付開始） |
| `NotifyViewportChanged()` | 表示（パン・ズーム・表示サイズ）の変化へ追従する |
| `RequestRedraw()` | 暗転マスクの再描画を要求する |

### 6.4 イベント

| イベント | 引数 | 発火条件 |
| --- | --- | --- |
| `ConfirmRequested` | `SelectionConfirmedEventArgs` | 矩形内ダブルクリック・Enter・`RequestConfirm`（選択確定後のみ） |
| `CancelRequested` | `EventArgs` | 矩形外ダブルクリック・Escape・`RequestCancel` |
| `LoupePositionMoved` | `LoupePositionEventArgs` | ルーペ中心の更新（対象座標） |

座標はすべて対象座標です。

### 6.5 座標系と操作

| 座標系 | 意味 |
| --- | --- |
| 対象座標 | 選択対象の座標（画像ピクセル・仮想スクリーンピクセル等）。 選択矩形とルーペ中心はこの座標で扱う |
| ローカル DIP | コントロールのローカル座標。 入力と描画はこの座標で行う |

両者の変換は座標変換ポート（`ICoordTransform`）が担い、選択矩形・暗転マスク・ルーペの配置で共用します。

| 操作 | 動作 |
| --- | --- |
| 左ドラッグ | 選択矩形の作成・リサイズ・移動（内包する選択コントロールが処理する） |
| 矢印キー | 選択矩形の nudge（内包コントロールへフォーカスがある場合のみ） |
| Enter / Escape | 確定・キャンセル要求を発火する |
| マウス移動 | ルーペ中心を更新し、表示位置を再計算する（**実カーソルから取り直した位置を優先する**。 ポインター移動イベントの座標は入力へ積まれた分だけ古く、そのまま使うとルーペがカーソルより遅れて見える。 取り直しの可否はコアが単一所有する） |

ホイール操作（ズーム・パン）と表示の変化の追従は本コントロールの配線ではありません（コントロール化を前提とした処理）。 ホイールはホストが `MouseWheel` を購読してビューポートへ転送し、表示の変化はホストが検知して `NotifyViewportChanged()` を呼びます。

### 6.6 エラー時の挙動

| 条件 | 動作 |
| --- | --- |
| `SetCoordTransform(null)` | 倍率からコアが座標変換を解決する（倍率 1.0 なら等価変換・境界なし） |
| `SetSelection` にサイズ 0 以下の矩形 | 選択なしとして扱う（例外は発生しない） |
| ルーペ画像が未設定の状態で `SetLoupePosition` | 中心のみ記録し、表示しない（例外は発生しない） |
| 未定義の `Mode` を設定 | `WindowSelect` と同じ扱いになる（例外は発生しない） |

## 7. ZoomViewControl（切り出しビューの画像ビューポート）

### 7.1 役割

画像の拡大・パン・Fit 表示を担う画像ビューポートです。 **`CropViewControl` が内包する内部部品**であり、独立した汎用コントロールとしてではなく切り出しビューの表示を成立させるために存在します。 ホストは通常 `CropViewControl` の `ZoomPercent` / `ZoomIn` / `ZoomOut` / `ZoomFit` / `ClientToSource` / `SourceToClient` / `ImageRectViewport` を経由して利用します（`CropViewControl.ZoomView` で参照できるのは、重ねた要素の座標変換に必要となる場合のためです）。

判断（実効ズーム率の導出・可否・遷移の適用・パン・スクロールバー・Fit 復帰）は UI 非依存コア `Parts.CapturePreview.Core` の `ZoomViewSession` が単一所有し、入力の変換・効果の適用・描画・通知の転送だけを担います。

### 7.2 ズーム遷移ルール

| 操作 | 内容 |
| --- | --- |
| ⊕ / ⊖（`ZoomIn` / `ZoomOut`） | 現在値が 23 段階リスト内なら隣の段階へ、リスト外（スライダの中間値）なら次に大きい / 小さい段階へ移動する。 リスト先頭（10%）で ⊖ 停止、末尾（500%）で ⊕ 停止する |
| Ctrl + ホイール | 現在の拡大率を基準に `Round(現在値 × 1.1)` / `Round(現在値 ÷ 1.1)` で連続遷移し、10〜500% へクランプする |
| Fit 中 | 実効倍率を基準に遷移する（23 段階・×1.1 のいずれも）。 `fitZoom` は `MinZoom`〜`MaxZoom` にクランプする |
| Fit 実効倍率の挟み込み | ズームイン / アウトで隣の段階と現在値の間に Fit 実効倍率がある場合、それを次の倍率として経由する。 近接している場合は 23 段階を優先する |

23 段階のレベル集合（`ZoomOptions.ButtonZoomLevels`）:

```text
10 → 12 → 15 → 20 → 25 → 33 → 40 → 50 → 62 → 75 → 80 → 90
→ 100 → 110 → 125 → 150 → 200 → 250 → 300 → 350 → 400 → 450 → 500
```

### 7.3 座標系と Fit 表示

| 座標系 | 定義 |
| --- | --- |
| コンテンツ座標 | 元画像ピクセル（ズーム率に依存しない不変座標）。 切り出しの対象座標と一致する |
| ビューポート座標 | コントロールのローカル座標（DIP） |

- 描画位置は `実効スケール × (コンテンツ座標 − オフセット)` で決まる。 オフセットは「ビューポート左上に対応するコンテンツ座標」であり、スクロール範囲にクランプされる
- `CropViewControl.SourceToClient` / `ClientToSource` は画像表示矩形の原点基準（Fit 中央寄せの原点補正込み）で双方向変換する
- **Fit 実効倍率**は `min(ビューポート幅 ÷ 画像幅, ビューポート高さ ÷ 画像高さ) × 100`（Uniform・アスペクト比維持）で、現在の表示状態に依存せずビューポートと画像サイズから一定に計算する。 ウィンドウのリサイズに追従する
- 実効スケールは固定倍率中が `ズーム率 ÷ 100`、Fit 中が `FitScale`。 Fit 中の座標変換でもゼロ除算が発生しない
- Fit 中はパン不可（画像がビューポートに収まる。 オフセット 0）

**ズーム中心維持**: 画像内にマウスポインタがあればその位置を、画像外ならビューポート中心を中心に拡大縮小し、中心のコンテンツ座標を不変に保つ。

**パン**: 左ドラッグは grab 方式で、掴んだ位置がポインタに追従する。 ホイールは縦パン、Shift+ホイールは横パン。 スクロールバーはオーバーレイ表示で、画像がビューポートより大きい軸のみ表示する。

### 7.4 公開 API

MewUI 版はコア型（`EsReadOnlyBitmap` / `EsPoint` / `EsVector`）で入出力します。

| メンバー | シグネチャ | 説明 |
| --- | --- | --- |
| `Source` | `EsReadOnlyBitmap?`（読み書き） | ズーム対象の画像。 設定すると Fit 表示にリセット |
| `Appearance` | `ZoomViewAppearance`（読み書き） | 外観の単一値（下地色・スクロールバー）。 画像が無い部分は下地色の単色になる（市松模様は描かない） |
| `ZoomPercent` | `double`（読み書き） | 実効ズーム率（%）。 0 より大きい値のみ設定（0 以下は無視。 Fit へは `ZoomFit`）。 設定値は `ZoomOptions` の範囲へクランプする |
| `IsFit` | `bool` | Fit 表示中かどうか（Fit 判定はこちら。 `ZoomPercent` は 0 にならない） |
| `CenterAlign` | `bool`（読み書き） | 画像の中央寄せ / 左上寄せ（軸ごとに独立） |
| `ClientToPixelScaling` | `double` | DIP → 物理ピクセルの係数（DPI 変更で自動更新される環境値） |
| `ZoomIn()` / `ZoomOut()` / `ZoomFit()` | — | 23 段階の遷移 / Fit 表示への復帰（唯一の Fit 移行経路） |
| `PanByWheel(EsVector, bool)` / `ZoomByWheel(bool, EsPoint?)` | — | ホイール入力の反映（コントロールが自ら使う経路。 ホストからの転送にも使える） |
| `ImageRectViewport()` / `DisplaySize()` | — | 画像の表示矩形 / 表示サイズ（ビューポート座標・DIP） |
| `ClientToDisplayedSource(EsPoint)` / `DisplayedSourceToClient(EsPoint)` | — | 画像表示矩形の原点基準の座標変換（中央寄せ補正を含む） |
| `ZoomChanged` | `event`（`ZoomChangedEventArgs`） | ズーム率・Fit 状態・ズーム可否の通知 |
| `ViewportChanged` | `event`（`EventArgs`） | パン・ズーム・表示サイズの変化の通知（重ねたオーバーレイの追従に使う） |
| `Log` | `ILogger` | 内部ロガー（null は `NullLogger`。 ズーム操作の前後 % を Information で記録する） |
| `LogPrefix` | `string?` | ログの接頭辞（ロガーと一緒に設定する。 null・空白で既定値へ戻す） |

**`ZoomOptions`（ズーム遷移の設定値。 コアの検証付き不変値）**

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `MinZoom` / `MaxZoom` | `double` | `10` / `500` | 固定倍率の最小値・最大値（%） |
| `WheelFactor` | `double` | `1.1` | Ctrl+ホイールのズーム倍率 |
| `ButtonZoomLevels` | `IReadOnlyList<double>` | 23 段階 | ⊖⊕ のズームレベル集合 |
| `Default` | `ZoomOptions` | 既定値 | 既定の設定。 `CropViewControl` が内包する `ZoomViewControl` はこの既定値を使用する |

`ZoomOptions` は生成時に不変条件（`MinZoom` が 0 以下・`MaxZoom` が `MinZoom` 未満・`WheelFactor` が 1 以下・`ButtonZoomLevels` が範囲外 / 非昇順 / 空）を検証し、満たさない場合は例外を投げる。

**`ZoomChangedEventArgs`（イベント引数）**

`sealed record(ZoomState State)`。 実効ズーム率・Fit 状態・ズーム可否を通知する（コアの `ZoomState` を保持し、各値は個別プロパティでも参照できる）。 表示文字列の生成はホストの責務である。

### 7.5 エラー時の挙動

| 状況 | 挙動 |
| --- | --- |
| `Source` が `null` | 何も描画しない（例外にしない） |
| `ZoomPercent` へ 0 以下を設定 | 無視する（Fit へは `ZoomFit()` を使う） |
| `ZoomPercent` が範囲外 | `ZoomOptions` の範囲へクランプする |
| 画像未設定でのズーム可否 | false を返す |
