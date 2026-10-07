# Parts.CapturePreviewControl.Core API リファレンス

撮影・切り出しの共通規則が提供する**公開 API** を記載します。 内部実装（呼び出し手順・キャッシュ等）は記載しません。

## 目次

- [1. 公開型の構成](#1-公開型の構成)
- [2. 層の責務の境界](#2-層の責務の境界)
- [3. CaptureLabelTexts](#3-capturelabeltexts)
  - [3.1 規則](#31-規則)
- [4. CropBoundsMath](#4-cropboundsmath)
  - [4.1 規則](#41-規則)
  - [4.2 使い分け](#42-使い分け)
- [5. CaptureSettings](#5-capturesettings)
- [6. CaptureResult](#6-captureresult)
- [7. 補助 API](#7-補助-api)
  - [7.1 CaptureTypeTraits](#71-capturetypetraits)
  - [7.2 MonitorScalingMath](#72-monitorscalingmath)
  - [7.3 CropLabelTexts](#73-croplabeltexts)
  - [7.4 ImageColorMath](#74-imagecolormath)
  - [7.5 CaptureSettingsMerge](#75-capturesettingsmerge)
  - [7.6 CaptureSessionState / CaptureSessionSlot](#76-capturesessionstate--capturesessionslot)
  - [7.7 CaptureOwnerScope](#77-captureownerscope)
  - [7.8 CapturePreviewLogMessage](#78-capturepreviewlogmessage)
  - [7.9 CropClipMath](#79-cropclipmath)
  - [7.10 CaptureImageHolder](#710-captureimageholder)
  - [7.11 CaptureVirtualScreen](#711-capturevirtualscreen)
  - [7.12 CaptureClipCache](#712-captureclipcache)
  - [7.13 CaptureHighlight / CaptureHighlightSession](#713-capturehighlight--capturehighlightsession)
  - [7.14 CaptureKeyActions / CaptureClickSelection](#714-capturekeyactions--captureclickselection)
  - [7.15 CropSessionActions](#715-cropsessionactions)
  - [7.16 CaptureTiming](#716-capturetiming)
  - [7.17 CropBoundsMath.RoundToPixel](#717-cropboundsmathroundtopixel)
  - [7.18 SelectionOverlayOptionsMapper](#718-selectionoverlayoptionsmapper)
  - [7.19 CaptureText](#719-capturetext)
- [8. 矩形オーバーレイ（`OverlayRect` 系）](#8-矩形オーバーレイoverlayrect-系)
  - [8.1 `OverlayRect`（record struct）](#81-overlayrectrecord-struct)
  - [8.2 `OverlayRectLayout`（描画入力の解決）](#82-overlayrectlayout描画入力の解決)
  - [8.3 `OverlayRectFrame`（描画入力）](#83-overlayrectframe描画入力)
  - [8.4 `OverlayLineStyle`（列挙型）](#84-overlaylinestyle列挙型)
- [9. 領域選択の複合コントロール（`SelectionOverlay` 系）](#9-領域選択の複合コントロールselectionoverlay-系)
  - [9.1 `SelectionOverlayMode`（列挙型）](#91-selectionoverlaymode列挙型)
  - [9.2 `SelectionOverlayOptions`（設定の単一値）](#92-selectionoverlayoptions設定の単一値)
  - [9.3 `SelectionOverlayLabelOptions`（付加情報ラベルの外観）](#93-selectionoverlaylabeloptions付加情報ラベルの外観)
- [10. 切り出しビューの画像ビューポート（`ZoomView` 系）](#10-切り出しビューの画像ビューポートzoomview-系)
  - [10.1 `ZoomViewSession`（状態と判断）](#101-zoomviewsession状態と判断)
  - [10.2 `ZoomOptions`（設定値）](#102-zoomoptions設定値)
  - [10.3 `ZoomTransition`（内部・純粋関数）](#103-zoomtransition内部純粋関数)
  - [10.4 `ZoomMath`（内部・純粋関数）](#104-zoommath内部純粋関数)
  - [10.5 `ZoomViewAppearance`（外観）](#105-zoomviewappearance外観)
  - [10.6 `ZoomScrollBar` / `ZoomScrollBarMetrics` / `ZoomScrollBarGeometry` / `ZoomAvailability`](#106-zoomscrollbar--zoomscrollbarmetrics--zoomscrollbargeometry--zoomavailability)
  - [10.7 `CapturePreviewLogMessage`（ズーム操作のログ）](#107-capturepreviewlogmessageズーム操作のログ)
  - [10.8 `ZoomState` / `ZoomFeedback` / `ZoomNotice`（契約）](#108-zoomstate--zoomfeedback--zoomnotice契約)
- [11. 提示の写像（`Presentation` 系）](#11-提示の写像presentation-系)
  - [11.1 CapturePaintMapper（内部）](#111-capturepaintmapper内部)
  - [11.2 SelectionOverlayChildValues](#112-selectionoverlaychildvalues)
  - [11.3 SelectionOverlayLabelRenderer / MarqueeHostLabelRenderer](#113-selectionoverlaylabelrenderer--marqueehostlabelrenderer)
  - [11.4 HighlightAppearanceDefaults](#114-highlightappearancedefaults)

## 1. 公開型の構成

| 型 | 種別 | 役割 |
| --- | --- | --- |
| `CropBoundsMath` | 静的クラス | 切り出し矩形の計算（純粋関数） |
| `CropClipMath` | 静的クラス | 選択矩形→切り出し矩形の解決（座標空間は画面物理）と、画像ローカル座標との相互変換 |
| `CaptureImageHolder` | クラス | 撮影した画像の保持と所有権（差し替え・破棄後の破棄・未取得時の範囲） |
| `CaptureVirtualScreen` | 静的クラス | 仮想スクリーンの撮影対象の範囲（全モニタの包含矩形）と、取得の待ち方（タイムアウトで打ち切り、未使用結果を破棄する） |
| `CaptureHighlight` | record struct | 検出結果を選択オーバーレイへ反映するための状態（ハイライト・矩形・表示名・変化） |
| `CaptureHighlightSession` | クラス | ハイライトの状態・判断・ログの単一所有（対象変換・未検出の正規化・表示名の変化） |
| `CaptureKeyAction` | 列挙型 | オーバーレイのキー操作で実行する内容（何もしない / 確定 / キャンセル） |
| `CaptureKeyActions` | 静的クラス | 押下されたキーから実行すべき操作の決定（Enter・Escape） |
| `CaptureClickSelection` | クラス | クリック確定の固定（押下で固定・解放で取り出し） |
| `CropLabelTexts` | 静的クラス | 切り出しのサイズラベル書式の単一所有 |
| `CaptureLabelTexts` | 静的クラス | キャプチャ種別→動作モードの写像と、ラベル文言（操作ラベル・情報ラベル・サイズラベル）の単一所有 |
| `CaptureTypeTraits` | 静的クラス | キャプチャ種別の性質（ハイライト型・区間の流儀・検出対象）の単一所有 |
| `CaptureElementTarget` | 列挙型 | カーソル下の検出対象（画面 / ウィンドウ） |
| `MonitorScalingMath` | 静的クラス | 対象矩形の中心を含むモニタの実表示倍率の解決（純粋関数） |
| `CropClipMath` | 静的クラス | 仮想スクリーン画像からの切り出し（画像ローカル座標への変換） |
| `CaptureClipCache` | クラス | 事前切り出しキャッシュ（同一判定・所有権の受け渡し・旧値の破棄） |
| `CaptureTiming` | 静的クラス | 撮影セッションの方針値（キャプチャのタイムアウト・画面外退避座標） |
| `CropSessionAction` | 列挙型 | 切り出しの 1 操作で実行する内容（開始 / 確定 / キャンセル） |
| `CropSessionActions` | 静的クラス | 切り出しの 1 操作で実行する内容の決定 |
| `CaptureSessionState` | クラス | セッションの実行中・完了の排他（再入禁止・単一完了） |
| `CaptureSessionSlot{TCapture, TCrop}` | クラス | 撮影・切り出しで同時に 1 つだけ実行できる排他スロット |
| `CaptureCompletion{TResult}` | クラス | 結果の単一確定（`TaskCompletionSource` のラップ） |
| `CaptureOwnerScope` | クラス | 撮影中のオーナー退避とキャプチャ除外の手順（`IDisposable` で復帰） |
| `CaptureSettings` | record | 外観・動作設定（全メンバー nullable） |
| `CaptureSettingsMerge` | 静的クラス | 設定の部分更新マージと同値スキップ |
| `CapturePreviewLogMessage` | 静的クラス | ログ定義（撮影・切り出し・設定・ズーム）。 接頭辞・整形・レベルの単一所有 |
| `CaptureText` | 静的クラス | ログ以外でも使う文字列の正規化（未取得の null を空文字へ） |
| `CaptureResult` | record | キャプチャ結果（画像と領域） |
| `ICaptureTarget` | インターフェイス | 切り出し結果の適用先（アプリ側が実装するポート） |
| `ICaptureOwnerWindow` | インターフェイス | 撮影時に退避するオーナーウィンドウ（各 UI が実装するポート） |
| `ImageColorMath` | 静的クラス | 画像の平均色（標本間引き）。 描画の背景色の決定に使う |
| `OverlayRect` | record struct | 外部提供矩形の描画情報（矩形・枠線スタイル・前景色・背景色・太さ） |
| `OverlayLineStyle` | 列挙型 | 枠線スタイル（実線 / 破線 / 点線） |
| `OverlayRectLayout` | 静的クラス | 矩形情報 → 描画入力（`OverlayRectFrame`）の解決（負サイズのクランプ・太さの下限・破線の判定） |
| `OverlayRectFrame` | record struct | 矩形 1 個分の描画入力（配置・太さ・半幅・破線の判定と長さ・色・描画可否） |
| `SelectionOverlayMode` | 列挙型 | 領域選択の複合コントロールの動作モード（切り出し / 範囲選択 / スクリーン選択 / ウィンドウ選択） |
| `SelectionOverlayOptions` | record | 複合コントロールの設定の単一値（`Marquee` / `Labels` / `Loupe` / 破線の色 2 / ルーペオフセット） |
| `SelectionOverlayLabelOptions` | record | 付加情報ラベル（操作・情報）の外観（表示可否・寸法・色）。 文言の生成と描画はホストが行う |
| `CapturePaintMapper` | 静的クラス（内部） | 設定 → 外観の写像（`CaptureSettings` → `MarqueeAppearance` / `LoupeAppearance`）。 色と枠線の太さ・表示可否を写す |
| `SelectionOverlayChildValues` | record struct | 設定とモードから内包コントロールへ適用する値（設定・外観・オフセット・入力の可否）の解決 |
| `SelectionOverlayLabelRenderer` | 静的クラス | 付加情報ラベル（操作・情報）の縦積みの配置指示の解決（表示可否の判定を含む） |
| `MarqueeHostLabelRenderer` | 静的クラス | ラベル 1 行の体裁（背景 = 文字の実寸 + 内側余白 × 2）の配置指示の解決 |
| `ZoomViewSession` | クラス | 切り出しビューの画像ビューポートの状態（ズーム・オフセット・設定・表示条件）を保持し、入力の解釈と判断を単一所有する |
| `ZoomOptions` | クラス | ズーム遷移のパラメータ（`MinZoom` / `MaxZoom` / `WheelFactor` / `ButtonZoomLevels`）をまとめた検証付き不変値 |
| `ZoomState` | record struct | 通知する 4 値（実効ズーム率・Fit 状態・ズーム可否） |
| `ZoomFeedback` | record struct | セッションから UI へ返す効果（再描画・ポインタキャプチャ）。 適用順を単一所有する |
| `ZoomNotice` / `ZoomNoticeKind` | record / 列挙型 | セッションから UI への通知（種別と通知時点の値。 `ZoomChanged` / `ViewportChanged`） |
| `ZoomViewAppearance` | record | 画像ビューポートの外観（下地・市松模様・スクロールバー）の単一値 |
| `ZoomScrollBar` / `ZoomScrollBarMetrics` | record struct | スクロールバーの矩形（トラック・サム）と、標準スクロールバー部品へ写す同期値 |
| `ZoomScrollBarGeometry` | 静的クラス | スクロールバーのトラック・サムの矩形と可動範囲、オフセットの逆算（純粋関数） |
| `ZoomTransition` | 静的クラス | ⊖⊕ の段階遷移・Ctrl+ホイールの遷移・ズーム可否の判定（純粋関数） |
| `ZoomMath` | 静的クラス | Fit 実効倍率・ズーム中心維持・スクロール範囲クランプ・座標変換・% ⇔ 倍率の換算（純粋関数） |
| `ZoomAvailability` | record struct | ズーム可否（ズームイン・ズームアウト）を単一に保持する値オブジェクト |
| `CapturePreviewLogMessage`（ズーム） | 静的クラス | 画像ビューポートの操作ログの定義（両 UI 共通の書式・丸め） |

`ZoomTransition` / `ZoomMath` / `ZoomAvailability` / `ZoomScrollBarGeometry` / `CapturePaintMapper` は `internal` です（UI 実装とテストへ `InternalsVisibleTo` で公開し、ホストは `ZoomViewSession` 経由で間接利用します）。

名前空間は `Parts.CapturePreviewControl.Core` です。

## 2. 層の責務の境界

本プロジェクトは「状態・判断・両 UI 共通の値と計算・コア⇔UI の契約・ログ定義」を担います。

| 区分 | 内容 |
| --- | --- |
| 状態 | セッションの実行中・完了（`CaptureSessionState`）、受付スロット（`CaptureSessionSlot`）、結果の確定（`CaptureCompletion`）、ハイライトとクリック確定（`CaptureHighlightSession` / `CaptureClickSelection`）、撮影画像（`CaptureImageHolder`） |
| 判断 | 切り出し範囲（`CropBoundsMath` / `CropClipMath`）、ハイライトの反映（`CaptureHighlightSession`）、キー操作（`CaptureKeyActions`）、クリック確定（`CaptureClickSelection`）、種別の性質（`CaptureTypeTraits`）、モニタ倍率（`MonitorScalingMath`）、設定のマージ（`CaptureSettingsMerge`）、退避と除外の順序（`CaptureOwnerScope`）、平均色（`ImageColorMath`）、切り出しと事前キャッシュ（`CropClipMath` / `CaptureClipCache`）、取得の打ち切り（`CaptureVirtualScreen`）、切り出しの操作内容（`CropSessionActions`）、方針値（`CaptureTiming`）、切り出しビューの画像ビューポートの状態と判断（`ZoomViewSession`） |
| 契約 | `ICaptureTarget`（結果の適用先）、`ICaptureOwnerWindow`（退避対象）、`CaptureSessionSlot`（排他）、`CaptureCompletion`（単一確定）、`CaptureClipCache`（事前切り出しの受け渡し）、`CaptureImageHolder`（画像の保持）、`CaptureHighlight` / `CaptureHighlightSession`（ハイライト状態）、`CaptureResult.From`（結果の正規化）、`ZoomFeedback` / `ZoomNotice`（画像ビューポートのコア⇔UI の契約） |
| ログ定義 | `CapturePreviewLogMessage`（接頭辞・書式・レベル・値の整形・完了時の出力の組・設定適用・ズーム操作を含む。 呼び出しは UI） |
| ログ以外の正規化 | `CaptureText`（未取得の文字列を空文字へ。 ログ定義の外に置く） |
| 提示の写像 | 設定 → 外観の写像（`CapturePaintMapper`）、子コントロールへ適用する値の解決（`SelectionOverlayChildValues`）、付加情報ラベルの配置指示（`SelectionOverlayLabelRenderer` / `MarqueeHostLabelRenderer`）。 製品の設定型と提示層の外観型を橋渡しする（UI は同じ写像を共有する） |
| UI 層に残すもの | Window の生成と表示、`Screens` / DPI の取得、ポインタ・キーの購読、`Dispatcher`、カーソル、レイアウト、Bitmap のライフサイクル |

## 3. CaptureLabelTexts

キャプチャ固有のラベル文言とモード写像を単一所有します。 選択矩形の複合コントロールは汎用化により固有の文言を持たないため、ホスト（両 UI のセッション）が本クラスの値をプロバイダで与えます。

| メソッド | シグネチャ | 内容 |
| --- | --- | --- |
| `ToSelectionMode` | `SelectionOverlayMode ToSelectionMode(CaptureType captureType)` | キャプチャ種別（全画面 / ウィンドウ / 範囲）→ 動作モード。 未定義の種別は範囲選択として扱う（切出しは対象外） |
| `GetOperationText` | `string GetOperationText(SelectionOverlayMode mode)` | 操作ラベル（確定・キャンセル方法）。 スクリーン選択・ウィンドウ選択は Enter 系、範囲選択・切出しはダブルクリック系（常に非空） |
| `GetInfoText` | `string? GetInfoText(SelectionOverlayMode mode, string? windowTitle)` | 情報ラベルの文言。 ウィンドウ選択のみウィンドウタイトルを返し、他モードは null（描画しない） |

### 3.1 規則

| # | 規則 |
| --- | --- |
| 1 | モード写像は `CaptureType` の 3 種別を全て扱い、未定義値は範囲選択へフォールバックする |
| 2 | 操作ラベルの文言は 2 系統（Enter 系 / ダブルクリック系）に集約する |
| 3 | 情報ラベルはウィンドウ選択でのみ意味を持つため、他モードでは null を返す（表示可否の最終判定（設定）は描画側） |

## 4. CropBoundsMath

| メソッド | シグネチャ | 内容 |
| --- | --- | --- |
| `ComputeCropBounds` | `EsRectInt ComputeCropBounds(EsRect selection, EsSize imageSize, double minSelectionSize)` | 選択矩形から切り出し範囲を求める。 切り出せない場合は `IsEmpty` が true の矩形 |
| `RoundToPixel` | `int RoundToPixel(double value)` | 座標を四捨五入して整数へ直す（0.5 は切り上げ。 詳細は 7.17） |
| `ToPixelRect` | `EsRectInt ToPixelRect(EsRect selection, EsSize imageSize)` | 半開区間のまま画像内の整数矩形へ変換（クランプ・四捨五入） |
| `ToClosedInterval` | `EsRectInt ToClosedInterval(EsRectInt rect)` | 終点の 1 ピクセルを加える（`+1` のみ。 クランプはしない） |
| `ToClosedInterval` | `EsRectInt ToClosedInterval(EsRectInt rect, EsSize imageSize)` | 終点の 1 ピクセルを加える（画像の端でクランプ） |

### 4.1 規則

| # | 規則 |
| --- | --- |
| 1 | 画像サイズが 0 以下の場合は空を返す |
| 2 | 選択矩形が `minSelectionSize` 未満（画像内へクランプする前の値で判定）の場合は空を返す |
| 3 | 座標は四捨五入（0.5 は切り上げ）し、画像の範囲へクランプする |
| 4 | 幅・高さが 1 ピクセル未満の場合は空を返す |
| 5 | `selection.Interval` が `Closed` の場合は終点の 1 ピクセルを加える（画像の右端・下端でクランプ） |

### 4.2 使い分け

| 用途 | `selection.Interval` |
| --- | --- |
| 範囲選択（ドラッグ）の切り出し | `Closed`（始点・終点のピクセルを含む） |
| スクリーン選択・ウィンドウ選択の切り出し | `HalfOpen`（検出結果が全ピクセルを含むため加算しない） |

終点の 1 ピクセルを加えるかの判断は**矩形の** **`EsRectInterval`** が持つため、本クラスは受け取った値に従うだけで判定を持ちません（判定を各所へ散らさない）。 種別から矩形の流儀を導くのは `CaptureTypeTraits.ResolveInterval` です。

確定時の切り出し矩形は、本クラスと種別の性質を組み合わせた `CropClipMath.ResolveCaptureRect` が求めます（両 UI が同じ規則を使う唯一の入口。 種別ごとの分岐を UI に書かない）。

## 5. CaptureSettings

外観・動作設定を保持します。 **全メンバーが nullable** で、用途により null の意味が異なります。

| 用途 | null の意味 |
| --- | --- |
| 初期設定（`CaptureSessionControl` のコンストラクタ） | デフォルト値を適用する |
| 既存インスタンスへの適用（`AppearanceSettings` の setter） | 現在値を維持する（部分更新） |

主なメンバー（抜粋）:

| メンバー | 型 | 内容 |
| --- | --- | --- |
| `HideOwnerWindow` | `bool?` | 撮影時にオーナーウィンドウを画面外へ退避するか |
| `MinSelectionSize` | `int?` | 選択矩形の最小サイズ（1 以上） |
| `BorderColor` / `BorderThickness` | `System.Drawing.Color?` / `double?` | 選択枠の色・太さ（DIP） |
| `OverlayColor` | `System.Drawing.Color?` | 暗転オーバーレイの色（不透明度はアルファに統合） |
| `LoupeSize` / `LoupeZoomLevel` / `LoupeOffset` | `int?` / `int?` / `double?` | ルーペのサイズ・倍率・オフセット |
| `LoupeClientToPixelScaling` | `double?` | ルーペの描画倍率（0.1〜8.0） |
| `SizeLabelVisible` / `SizeLabelColor` / `SizeLabelFontSize` | `bool?` / `System.Drawing.Color?` / `double?` | サイズラベルの表示・色・サイズ |
| `InfoLabelVisible` / `ShowSubLines` / `SubLineFontSize` | `bool?` / `bool?` / `double?` | 情報ラベル・サブライン |
| `DisplayScaleScaling` | `double?` | サイズラベル表示用のスケーリング（0 以下は `ClientToSourceScaling` と同値） |

| メソッド | シグネチャ | 内容 |
| --- | --- | --- |
| `Validate` | `void Validate()` | 設定値を検証する。 範囲外の場合は `ArgumentOutOfRangeException`（null は検証対象外） |
| `ToNonNull` | `CaptureSettings ToNonNull()` | null をデフォルト値で埋めた non-null 版を返す |

**色は `System.Drawing.Color`** です（UI 非依存のため）。 `Validate` が検証する範囲は `MinSelectionSize` / `HandleSize` / `HandleHitRadius` / `MarkerLength` / `LoupeSize` / `LoupeZoomLevel` / `LoupeFrameThickness`（1 以上）、`BorderThickness` / `LoupeOffset`（0 以上）、`LoupeClientToPixelScaling`（0.1〜8.0）です。

## 6. CaptureResult

| メンバー | 型 | 内容 |
| --- | --- | --- |
| `Image` | `EsBitmap?` | キャプチャした画像。 キャンセル時は null |
| `SourceBounds` | `EsRectInt?` | キャプチャした領域（仮想スクリーン座標）。 キャンセル時は 0x0 |

`Image` と `SourceBounds` は常に同時に null または同時に非 null になります。 `SourceBounds` は複数モニターを包含する座標空間のため、プライマリモニターが (0, 0) にない場合は負値になり得ます。

| メソッド | シグネチャ | 内容 |
| --- | --- | --- |
| `From` | `CaptureResult From(EsBitmap? image, EsRectInt? bounds)` | 切り出し結果と領域から結果を生成する。 **キャンセル（画像または領域が無い）は画像 null・領域 0x0 の空結果へ正規化する**（UI が null 判定を書かない） |

## 7. 補助 API

### 7.1 CaptureTypeTraits

キャプチャ種別の性質を単一所有します（種別ごとの分岐を各 UI に書かないための写像）。

| メソッド | シグネチャ | 内容 |
| --- | --- | --- |
| `IsHighlighted` | `bool IsHighlighted(CaptureType captureType)` | 選択矩形を検出対象のハイライトとして描く種別（スクリーン選択・ウィンドウ選択）かどうか。 **選択スタイル（ハイライト型 / ドラッグ型）の分区を単一所有する**ため、UI は十字カーソルの要否・ルーペの利用・確定操作の配線を本結果から導く |
| `ResolveInterval` | `EsRectInterval ResolveInterval(CaptureType captureType)` | 切り出しで終点の 1 ピクセルを加える種別（範囲選択のみ `Closed`）かどうかを区間の流儀で返す |
| `GetElementTarget` | `CaptureElementTarget GetElementTarget(CaptureType captureType)` | カーソル下から検出する対象（スクリーン選択は画面、他はウィンドウ） |
| `FindUnderCursor` | `ElementFinderResults FindUnderCursor(IElementFinder? finder, CaptureType captureType, EsPointInt screenPoint)` | カーソル下の要素を検出する。 **検出関数の呼び分けを単一所有する**（`finder` が null の場合は未検出）。 ホバー・押下の各経路で同じ分岐を書かない |
| `ResolveRect` | `EsRect? ResolveRect(CaptureType captureType, EsRect? highlightRect, EsRect? selectionRect)` | 確定対象の矩形を解決する（ハイライト型はハイライト矩形、それ以外は選択矩形）。 UI は未検出・未選択を null へ正規化して渡す |

### 7.2 MonitorScalingMath

| メソッド | シグネチャ | 内容 |
| --- | --- | --- |
| `ResolveForRect` | `double ResolveForRect(EsRectInt target, IReadOnlyList<(EsRectInt Bounds, double Scale)> monitors, double fallback)` | 対象矩形の**中心**を含むモニタの表示倍率を返す。 どのモニタにも含まれない場合は `fallback` |
| `ResolveForSourceRect` | `double ResolveForSourceRect(EsRect sourceRect, EsPointInt origin, IReadOnlyList<(EsRectInt Bounds, double Scale)> monitors, double fallback)` | 対象座標（画像ピクセル）の矩形を画面物理座標へ戻してから中心を含むモニタの表示倍率を返す。 **原点の加算**も単一所有する（各 UI が書くと判定点が 1 ピクセルずれる） |
| `Collect` | `IReadOnlyList<(EsRectInt Bounds, double Scale)> Collect(IEnumerable<(EsRectInt Bounds, double Scale)> monitors)` | 判定に使うモニタ一覧を整える（**表示倍率が未確定（0 以下）のモニタを除外する**。 入力の順序は保つ） |
| `ResolveScaling` | `double ResolveScaling(IEnumerable<double> scalings)` | 画面のスケーリング一覧から**先頭の値**を解決する（未確定は基準値へ正規化。 画面が 1 枚も無い場合は基準値） |

対象矩形の判定は中心 1 点で行います（左上で判定すると、モニタをまたぐ矩形で主モニタと異なる倍率を拾います）。 倍率未確定の除外を UI ごとに書くと、同じ環境で判定結果が変わります。

### 7.3 CropLabelTexts

| メソッド | シグネチャ | 内容 |
| --- | --- | --- |
| `FormatCropSizeLabel` | `string FormatCropSizeLabel(EsRect selectionSourceRect, double zoomPercent)` | 切り出しのサイズラベル（`LT` / `RB` / `WH` / `Zoom`）。 `WH` は含まれるピクセル数であり、終点を含めるかの判断は矩形の `EsRectInterval` が持つ（`Closed` なら右端−左端＋1） |
| `ResolveSizeLabel` | `string? ResolveSizeLabel(bool hasSelection, EsRect selectionSourceRect, double zoomPercent)` | 選択がある場合はラベル、**未選択の場合は null**（描画しない）を返す |

### 7.4 ImageColorMath

| メソッド | シグネチャ | 内容 |
| --- | --- | --- |
| `AverageColor` | `DrawingColor AverageColor(EsBitmap image)` | 画像全体の平均色。 画素を間引いて標本化する。 画像が null の場合は `ArgumentNullException` |

### 7.5 CaptureSettingsMerge

| メソッド | シグネチャ | 内容 |
| --- | --- | --- |
| `Merge` | `CaptureSettings Merge(CaptureSettings? input, CaptureSettings? current = null)` | 部分更新をマージする。 `current` が null の場合は入力をデフォルトで補完した non-null 版を返し、`input` が null の場合は現在値（無ければデフォルト）をそのまま返す |
| `Apply` | `bool Apply(CaptureSettings input, ref CaptureSettings current, ILogger? logger = null)` | 検証 → マージ → 同値ならスキップ。 適用した場合のみ true を返し、ログを出す |

### 7.6 CaptureSessionState / CaptureSessionSlot

| 型 | メンバー | 内容 |
| --- | --- | --- |
| `CaptureSessionState` | `IsRunning` / `IsCompleted` | 1 セッション内の実行中・完了済みの状態 |
| `CaptureSessionState` | `TryBegin()` / `TryComplete()` / `End()` | 実行の開始（再入禁止）・単一完了・終了（状態をリセット） |
| `CaptureSessionSlot{TCapture, TCrop}` | `HasCapture` / `HasCrop` / `IsBusy` / `Crop` | 受付中のセッションの有無 |
| `CaptureSessionSlot{TCapture, TCrop}` | `TryBeginCapture` / `EndCapture` / `TryBeginCrop` / `EndCrop` | 撮影・切り出しの開始受付と終了。 **同時に実行できるのはどちらか 1 つ** |
| `CaptureSessionSlot{TCapture, TCrop}` | `HasCropSelection(Func<TCrop, bool>)` / `CancelAll(Action<TCapture>?, Action<TCrop>?)` | 切り出しの選択有無の委譲・両セッションの一括キャンセル |

`CaptureSessionState` は 1 セッション内の進行状態、`CaptureSessionSlot` は「どのセッションが実行中か」を持ちます（責務が重なりません）。

### 7.7 CaptureOwnerScope

| メンバー | シグネチャ | 内容 |
| --- | --- | --- |
| `Enter` | `CaptureOwnerScope Enter(IWindowService windowService, EsPlatformHandle handle, ICaptureOwnerWindow owner, bool hideOwner)` | 撮影中のオーナー退避とキャプチャ除外を開始する。 **キャプチャ除外を先に、退避を後に**行う順序を単一所有する |
| `Dispose` | `void Dispose()` | 配置を復帰し、キャプチャ除外を解除する。 `hideOwner` が false の場合も同じスコープを返す |

撮影が例外で終わっても復帰するため、必ず `using` で使います。

### 7.8 CapturePreviewLogMessage

ログの定義（接頭辞・イベント ID・本文書式・整形・完了時の出力の組）を単一所有します。 カテゴリは各 UI が与えます。 接頭辞はホストが `SetPrefix` で設定します（読み取りは `Prefix`。 null または空白は `DefaultPrefix` へ戻す）。

| メソッド | 内容 |
| --- | --- |
| `SetPrefix(string? value)` | 接頭辞を設定する（null または空白は `DefaultPrefix` へ戻す規則を単一所有する） |
| `LogOperationError(Exception exception, string operation)` | 公開ファサードの操作で例外が発生したことを記録する（Error。 記録して再スローするための入口） |
| `LogCaptureStart(CaptureType captureType)` | 撮影の開始 |
| `LogCaptureFinished(CaptureResult? result)` | 撮影セッションの終了。 確定は `CaptureCompleted` → `Result` の 2 件（順序を含めて単一所有）、キャンセル（`result` が null または画像が null）は `CaptureCancelled` の 1 件 |
| `LogCropStart(EsBitmap image)` | 切り出しの開始（元画像サイズ） |
| `LogCropFinished(EsRectInt bounds)` | 切り出しセッションの終了。 **退化した選択（空の矩形）はキャンセルとして記録する**（UI が空判定とログの呼び分けを書かない） |
| `LogCropCancelled()` | 切り出しのキャンセル（Esc・非アクティブ化・ウィンドウクローズ等） |
| `LogConfirmRequested(EsRect rect)` / `LogConfirmRequested(EsRectInt rect)` | 選択の確定要求（Enter・ダブルクリック等。 診断用） |
| `LogCancelRequested()` | 選択のキャンセル要求（Esc・非アクティブ化等。 診断用） |
| `LogVirtualScreen(EsPointInt origin, EsBitmap? bitmap)` | 仮想スクリーンの原点とサイズ（診断用）。 **画像未取得はサイズ 0** へ正規化する |
| `LogHighlight(CaptureType captureType, bool detected, EsRectInt? rect, string? title, string? className)` | ハイライトの検出結果（Trace。 診断用。 `CaptureHighlightSession` が呼ぶ） |
| `LogSettingApplied()` | 設定の適用（変化時の 1 回のみ。 同値の適用はログを出さない） |

矩形・文字列の受け取りはコアの値型（`EsRectInt` / `EsRect`）とし、**整形（`"X,Y,Width,Height"`・未取得の空文字化）は本クラスが内部で行います**（呼び出し側は生の値を渡すだけ。 未取得の文字列の正規化は `CaptureText.Normalize` が単一所有する）。

`CropCompleted` / `CaptureCompleted` / `CaptureCancelled` / `Result` は `LogCropFinished` / `LogCaptureFinished` の内部実装です（UI からは終了 1 回で呼びます）。

ログに載せる値の整形（`internal`。 別クラスは持たず本クラスが単一所有する）:

| メソッド | 内容 |
| --- | --- |
| `Rect(EsRectInt)` / `Rect(EsRect)` | 矩形を `"X,Y,Width,Height"` の 1 行へ整形する |
| `RectOrEmpty(EsRectInt?)` | 未検出（null）を空文字へ正規化する |
| `Percent(double)` | ズーム率（%）を四捨五入した整数へ丸める |

### 7.9 CropClipMath

| メソッド | シグネチャ | 内容 |
| --- | --- | --- |
| `ResolveCaptureRect` | `EsRectInt ResolveCaptureRect(CaptureType captureType, EsRect screenRect, EsRectInt imageBounds)` | 選択矩形（画面物理座標）から切り出し矩形（画面物理座標）を求める。 **確定時の切り出し矩形を決める唯一の入口**（種別が決める区間の流儀に従った終点の加算・画像内へのクランプ・端数の丸めを組み合わせる）。 切り出せない場合は `IsEmpty` |
| `ResolveCaptureRect`（整数版） | `EsRectInt ResolveCaptureRect(CaptureType captureType, EsRectInt screenRect, EsRectInt imageBounds)` | クリック確定・ホバーの検出矩形（整数）向けの同じ規則の入口（UI が整数矩形を浮動小数矩形へ広げないため） |
| `ImageSize` | `EsSize ImageSize(EsBitmap image)` | 画像のピクセルサイズ（コアの値型）。 コアの型同士の変換を UI に書かせない |
| `ImageBounds` | `EsRectInt ImageBounds(EsBitmap image, EsPointInt origin)` | 画像の占有範囲（原点とサイズ。 切り出しのクランプ境界になる） |
| `ValidateImage` | `void ValidateImage(EsBitmap image, string paramName)` | 画像が切り出せる（サイズが正）ことを検証する。 **幅・高さが 0 以下の場合は `ArgumentException`** |
| `ToLocalRect` | `EsRectInt ToLocalRect(EsRectInt captureRect, EsPointInt origin)` / `EsRect ToLocalRect(EsRect captureRect, EsPointInt origin)` | 画面物理座標の矩形を画像ローカル座標（画面物理 − 原点）へ変換する |
| `ToScreenRect` | `EsRect ToScreenRect(EsRect localRect, EsPointInt origin)` / `EsRectInt ToScreenRect(EsRectInt localRect, EsPointInt origin)` | 画像ローカル座標の矩形を画面物理座標へ変換する（`ToLocalRect` の逆変換。 双方向の対称性を保つ） |
| `Extract` | `EsBitmap? Extract(EsBitmap? virtualScreen, EsPointInt origin, EsRectInt captureRect)` | 仮想スクリーン画像から切り出し矩形の領域を取り出す。 **未取得・破棄済みの場合は null**（呼び出し側が領域指定のキャプチャへフォールバックする。 破棄は例外にしない） |

`ResolveCaptureRect` は最小サイズを判定しません（選択操作そのものの可否は `MarqueeOptions.MinSelectionSize` が持ち、検出矩形は小さい要素も切り出し対象にする）。

**座標空間の契約**: 切り出し矩形の求解は**画面物理座標（仮想スクリーン座標）に統一**します。 選択矩形と占有範囲をともに画面物理座標で渡すため、座標空間の取り違えが構造的に発生しません（原点が負値のマルチモニタでも切り出し位置・幅が正しくなる）。 UI の選択矩形が画像ローカル座標（画像の左上が原点）の場合は、**境界で `ToScreenRect` により画面物理座標へ戻してから**渡します。

結果の矩形も画面物理座標になるため、`CaptureResult.SourceBounds`（契約: 仮想画面座標）やそのままログの矩形として使えます。

### 7.10 CaptureImageHolder

撮影した画像（仮想スクリーン）の保持と所有権を単一所有します。 背景撮影の完了を待たずに表示を始めるため、画像の寿命がウィンドウの寿命と一致しないことへの対処です。

| メンバー | シグネチャ | 内容 |
| --- | --- | --- |
| `Set` | `bool Set(EsBitmap? image, EsPointInt origin)` | 画像と原点を保持する（**旧い画像を破棄する**）。 **破棄済みの場合は受け付けず、渡された画像をその場で破棄して false** を返す |
| `Image` / `Origin` | `EsBitmap?` / `EsPointInt` | 保持している画像と原点（未取得は null・既定値） |
| `Bounds` | `EsRectInt` | 画像の占有範囲。 **未取得の場合はコンストラクタで指定したフォールバック範囲**（背景撮影完了前でも切り出し範囲をクランプできる） |
| `Dispose` | `void Dispose()` | 保持している画像を破棄する（複数回呼んでも安全。 以降の `Set` は渡された画像をその場で破棄する） |

### 7.11 CaptureVirtualScreen

| メソッド | シグネチャ | 内容 |
| --- | --- | --- |
| `ResolveBounds` | `EsRectInt? ResolveBounds(IEnumerable<EsRectInt>? screenBounds)` | 撮影対象の範囲（全モニタの包含矩形）を求める。 **モニタが無い場合は null**（撮影する領域が無い） |
| `AwaitAsync` | `Task<EsBitmap?> AwaitAsync(Task<EsBitmap?> captureTask, TimeSpan? timeout = null)` | 仮想スクリーンの取得をタイムアウト付きで待つ。 **間に合わない場合は null を返し、遅れて完了した結果を破棄する**（既定の待ち時間は `CaptureTiming.ScreenCaptureTimeout`） |

### 7.12 CaptureClipCache

「確定前に切り出しておき、確定時は同じ矩形なら再利用する」事前切り出しの状態と判断を単一所有します。

| メンバー | シグネチャ | 内容 |
| --- | --- | --- |
| `HasCached` | `bool HasCached(EsRectInt captureRect)` | 同じ矩形の画像を保持しているか（再切り出しのスキップ判断） |
| `Take` | `EsBitmap? Take(EsRectInt captureRect)` | 一致する画像を**所有権ごと**取り出す（以降の `Dispose` で破棄されない）。 一致しない場合は null |
| `Store` | `void Store(EsRectInt captureRect, EsBitmap? bitmap)` | 切り出した画像を格納する。 **旧いキャッシュを破棄してから**置き換える。 破棄済みの場合は渡された画像をその場で破棄する |
| `HasBitmap` / `StoreCount` | `bool` / `int` | 保持の有無・実際に格納した回数（スキップ判断の検証用） |
| `Dispose` | `void Dispose()` | 保持中の画像を破棄する（複数回呼んでも安全） |

`Store` は「新しい画像を入れてから旧値を破棄する」順序を守るため、同一インスタンスの取り違えが起きません。

### 7.13 CaptureHighlight / CaptureHighlightSession

カーソル下の要素の検出結果から「ハイライトの有無・ハイライト矩形（対象座標）・表示名」を導出する手順を単一所有します（両 UI が同じ手順・同じログになることの根拠）。

| メンバー | シグネチャ | 内容 |
| --- | --- | --- |
| `Update` | `CaptureHighlight Update(CaptureType captureType, ElementFinderResults detection, EsPointInt imageOrigin)` | 検出結果を反映する（対象座標への変換・未検出の正規化・表示名の変化の判定・診断ログの記録） |
| `SourceRect` | `EsRect?` | 直前に検出したハイライト矩形（対象座標 = 画像ローカル物理ピクセル）。 未検出時は null（確定対象の有無の判定も兼ねる） |
| `Title` | `string` | 直前に検出した要素の表示名（未検出時は空文字）。 情報ラベルの文言プロバイダが参照する |

| メンバー | 型 | 内容 |
| --- | --- | --- |
| `SourceRect` | `EsRect?` | ハイライト矩形（対象座標 = 画像ローカル物理ピクセル）。 未検出時は null（**null が「ハイライトなし」を意味する**） |
| `TitleChanged` | `bool` | 直前の検出と比べて表示名が変わったかどうか（情報ラベルの再描画判断） |

UI 層は戻り値を選択コントロールへ設定するだけにし、座標変換・空文字への正規化・変化の判定・診断ログを書きません（直前の状態はセッションのプロパティから読みます）。

### 7.14 CaptureKeyActions / CaptureClickSelection

オーバーレイのキー操作と、クリック確定（押下で固定・解放で確定）の状態を単一所有します。

| メンバー | シグネチャ | 内容 |
| --- | --- | --- |
| `Resolve` | `CaptureKeyAction Resolve(EsKey key, bool hasConfirmTarget)` | 実行すべき操作を返す。 Enter（Return を含む）→ `Confirm`（確認対象が無い場合は `None`）、Escape → `Cancel`、その他 → `None` |

`CaptureKeyAction` は `None` / `Confirm` / `Cancel` の 3 値です（`None` が既定値）。

| メンバー | シグネチャ | 内容 |
| --- | --- | --- |
| `Fix` | `bool Fix(EsRectInt target)` | 押下時に確定対象を固定する。 退化した矩形（幅・高さが 0 以下）は固定せず、前回の固定も破棄する |
| `Take` | `EsRectInt? Take()` | 解放時に固定した矩形を取り出す（1 回限り）。 固定が無い場合は null（確定しない） |

### 7.15 CropSessionActions

1 つのボタン（キー）で「開始・確定・キャンセル」を切り替える判断を単一所有します。

| メソッド | シグネチャ | 内容 |
| --- | --- | --- |
| `Resolve` | `CropSessionAction Resolve(bool isRunning, bool hasSelection)` | 実行すべき操作を返す（未実行→`Start` / 実行中かつ選択あり→`Confirm` / 実行中かつ選択なし→`Cancel`） |

`CropSessionAction` は `Start` / `Confirm` / `Cancel` の 3 値です。 UI 層は返された操作の効果を適用するだけにします。

### 7.16 CaptureTiming

UI が発明してはいけない運用方針値を単一所有します。

| メンバー | 型 | 内容 |
| --- | --- | --- |
| `ScreenCaptureTimeout` | `TimeSpan` | 仮想スクリーン全体の取得のタイムアウト（既定 5 秒）。 DWM 応答不能時に取得を待ち続けないための上限 |
| `OwnerOffScreenCoord` | `int` | オーナーウィンドウを退避させる画面外の座標（`-32000`）。 ヒットテスト・要素検出の対象外になる |

### 7.17 CropBoundsMath.RoundToPixel

| メソッド | シグネチャ | 内容 |
| --- | --- | --- |
| `RoundToPixel` | `int RoundToPixel(double value)` | 座標を四捨五入して整数へ直す（0.5 は切り上げ = `MidpointRounding.AwayFromZero`） |

同じ丸めを UI が `(int)` 切り捨てや既定の `Math.Round` で書くと、同じ座標が UI ごとに 1 ピクセルずれるため公開します。

### 7.18 SelectionOverlayOptionsMapper

| メソッド | シグネチャ | 内容 |
| --- | --- | --- |
| `ToSelectionOverlayOptions` | `SelectionOverlayOptions ToSelectionOverlayOptions(this CaptureSettings settings)` | 設定（全メンバー non-null）から複合コントロールの設定（**寸法・表示可否**）を組み立てる。 **色・枠線の太さ・表示可否は写像しない**（描画にだけ必要な設定のため、UI 層のホストが提示層の `MarqueeAppearance` / `LoupeAppearance` を組み立てる） |

### 7.19 CaptureText

**ログに限らない**文字列の正規化を単一所有します。 ログ定義（`CapturePreviewLogMessage`）の中へ置くとログ以外の利用者を呼び込み、名前と責務が食い違うため独立した型にしています。

| メソッド | シグネチャ | 内容 |
| --- | --- | --- |
| `Normalize` | `string Normalize(string? value)` | 未取得（null）の文字列を空文字へ正規化する。 **値は変えない**（空白の除去・トリムを行わない） |

**対象はログではない**ため、`CapturePreviewLogMessage` の整形（`Rect` / `RectOrEmpty` / `Percent`）とは別の型です。 ハイライトの表示名（`CaptureHighlightSession.Title`）と、ログ本文の未取得の正規化（`LogHighlight` の内部）が同じ規則を共有します。

## 8. 矩形オーバーレイ（`OverlayRect` 系）

画像上へ実線・破線・点線の矩形を描く契約と、**「どう描くか」の判断**を単一所有します。 UI 層は対象座標 → ビューポート座標の変換だけを行い、確定した描画入力（`OverlayRectFrame`）を描画先の型へ写します。

### 8.1 `OverlayRect`（record struct）

```csharp
public readonly record struct OverlayRect(
    EsRect Bounds, OverlayLineStyle Style, Color Foreground, Color Background, double Thickness);
```

| メンバー | 型 | 内容 |
| --- | --- | --- |
| `Bounds` | `EsRect` | 矩形（対象座標 = 元画像ピクセル座標） |
| `Style` | `OverlayLineStyle` | 枠線スタイル |
| `Foreground` | `System.Drawing.Color` | 前景色（遅明度を含む） |
| `Background` | `System.Drawing.Color` | 背景色（遅明度を含む）。 破線の市松風 2 色目 |
| `Thickness` | `double` | 枠線の太さ（ピクセル） |

### 8.2 `OverlayRectLayout`（描画入力の解決）

| メンバー | 内容 |
| --- | --- |
| `MinimumThickness` | 枠線の太さの下限（1 ピクセル） |
| `EffectiveThickness(double thickness)` | 太さを下限以上へ補正する |
| `ViewportRectFromCorners(EsPoint topLeft, EsPoint bottomRight)` | ビューポート座標の 2 点から矩形を組み立てる（負サイズは 0 へクランプ） |
| `Resolve(OverlayRect rect, EsPoint topLeft, EsPoint bottomRight)` | 矩形情報を描画入力（`OverlayRectFrame`）へ解決する |

### 8.3 `OverlayRectFrame`（描画入力）

| メンバー | 型 | 内容 |
| --- | --- | --- |
| `Bounds` | `EsRect` | ビューポート座標（DIP）の矩形。 幅・高さは 0 以上へクランプ済み |
| `Foreground` / `Background` | `System.Drawing.Color` | 前景色 / 背景色（破線のときの下地） |
| `Thickness` / `HalfThickness` | `double` | 枠線の太さ（下限以上）と半幅（矩形の辺の内側へ寄せる量） |
| `IsDashed` | `bool` | 破線（破線または点線）かどうか |
| `DashLength` / `GapLength` | `double` | ダッシュ長と間隔（実線は 0） |
| `DrawsBackground` | `bool` | 背景色の下地を描くかどうか（破線のときだけ true） |
| `HasDrawableSize` | `bool` | 幅と高さがともに 0 より大きいかどうか（描画可否） |

### 8.4 `OverlayLineStyle`（列挙型）

| メンバー | 内容 |
| --- | --- |
| `Solid` | 実線 |
| `Dashed` | 破線（長いダッシュ。 ダッシュ長 8 / 間隔 4） |
| `Dotted` | 点線（短いダッシュ。 ダッシュ長 2 / 間隔 2） |

## 9. 領域選択の複合コントロール（`SelectionOverlay` 系）

領域選択の複合コントロール（Avalonia 版 / MewUI 版）が共有する型と設定を提供します。 **動作モードの型・設定の単一値・モードから導出する表示可否・選択矩形の可否判定・ルーペ画像への破線描画**を単一所有します。

### 9.1 `SelectionOverlayMode`（列挙型）

| メンバー | 内容 |
| --- | --- |
| `Crop` | 切り出し（プレビューコントロール上のオーバーレイ。 全面暗転 + 選択矩形 + ルーペ） |
| `AreaSelect` | 範囲選択（全画面オーバーレイ。 全面暗転 + 選択矩形 + ルーペ + ドラッグハンドル） |
| `ScreenSelect` | スクリーン選択（ハイライト + スクリーン番号ラベルのみ。 クリック確定はホストが担う） |
| `WindowSelect` | ウィンドウ選択（ハイライト + ウィンドウタイトルラベルのみ。 クリック確定はホストが担う） |

`Crop` / `AreaSelect` は選択矩形を使うモード、`ScreenSelect` / `WindowSelect` は使わないモードです。 この区別は本プロジェクトのモード解釈が単一所有し、UI 実装は `switch` を持ちません。

### 9.2 `SelectionOverlayOptions`（設定の単一値）

| メンバー | 型 | 既定値 | 内容 |
| --- | --- | --- | --- |
| `Marquee` | `MarqueeOptions` | `MarqueeOptions.Default` | 選択矩形の設定（選択の判断に使う項目のみ。 色は含まない） |
| `Labels` | `SelectionOverlayLabelOptions` | 既定値 | ホストが描く操作・情報ラベルの外観 |
| `Loupe` | `LoupeOptions` | `LoupeOptions.Default` | ルーペの設定（拡大率・表示サイズ・寸法） |
| `SelectionRectColorA` | `System.Drawing.Color` | 白 | ルーペ画像上の選択矩形破線の色 A（描画開始色） |
| `SelectionRectColorB` | `System.Drawing.Color` | 黒 | 同 色 B（色 A と交互に使用する） |
| `LoupeOffset` | `double` | `20.0` | ルーペのカーソル中心からの右下オフセット（DIP） |
| `Default`（static） | `SelectionOverlayOptions` | — | 部分変更の基点（`Default with { ... }`） |

**色は描画にだけ必要な設定のため本値に含みません**（提示層の `MarqueeAppearance` / `LoupeAppearance` が単一所有する）。 例外はルーペ画像へ直接書き込む破線の色（`SelectionRectColorA` / `SelectionRectColorB`）で、これは描画 API ではなくピクセル書き込みに使うため本値が持ちます。

**実行時状態**（選択矩形・ハイライト矩形・ルーペ中心・拡大元画像）と**環境値**（DPI スケーリング）は本値に含みません。

### 9.3 `SelectionOverlayLabelOptions`（付加情報ラベルの外観）

| メンバー | 型 | 既定値 | 内容 |
| --- | --- | --- | --- |
| `ShowOperationLabel` | `bool` | `true` | 操作ラベル（確定・キャンセルの操作方法）を表示するか |
| `OperationFontSize` | `double` | `12.0` | 操作ラベルのフォントサイズ（DIP） |
| `OperationColor` | `System.Drawing.Color` | `#FFDCDCDC` | 操作ラベルの文字色 |
| `OperationBackgroundColor` | `System.Drawing.Color` | `#99000000` | 操作ラベルの背景色（黒 60%） |
| `ShowInfoLabel` | `bool` | `true` | 情報ラベル（対象ウィンドウのタイトル等）を表示するか |
| `InfoFontSize` | `double` | `14.0` | 情報ラベルのフォントサイズ（DIP） |
| `InfoColor` | `System.Drawing.Color` | 白 | 情報ラベルの文字色 |
| `InfoBackgroundColor` | `System.Drawing.Color` | `#99000000` | 情報ラベルの背景色（黒 60%） |
| `Default`（static） | `SelectionOverlayLabelOptions` | — | 部分変更の基点 |

**表示可否は 2 つの条件がともに成立したときだけ真**です。 本値のスイッチに加え、ホストが返す文言が空でないことが必要です（対象外のモードでは文言が空になるため、スイッチだけでは判定できません）。

**操作ラベル・情報ラベルは選択矩形コントロールの概念ではありません**（撮影オーバーレイ等へ組み込むホストが必要とする付加情報）。 したがって文言の生成と描画はホストが行い、本値はその描画に使う外観のみを保持します。

## 10. 切り出しビューの画像ビューポート（`ZoomView` 系）

切り出しビューが内包する画像ビューポート（拡大・パン・Fit 表示）の規則です。 **状態と判断は `ZoomViewSession` が単一所有**し、UI 層は入力の変換・効果の適用・描画だけを担います。

### 10.1 `ZoomViewSession`（状態と判断）

内部ズーム率（0 = Fit）・オフセット・設定・表示条件を保持し、入力の解釈と判断を単一所有します。

| # | 単一所有する判断 |
| --- | --- |
| 1 | Fit の表現（内部ズーム率 0 = 自動）と実効ズーム率の導出（Fit 中はクランプした実効倍率） |
| 2 | Fit 実効倍率の算出・キャッシュと無効化（コンテンツ・ビューポートの変更時のみ） |
| 3 | ズーム可否と通知値 `ZoomState` の確定 |
| 4 | ズーム遷移の適用（段階・ホイール）と、ズーム中心を維持したオフセット再計算 |
| 5 | パン（grab 方式）・スクロール範囲へのクランプ・ホイール回転量からの換算（1 ノッチ = 48 DIP） |
| 6 | 画像変更時の Fit 復帰、ビューポート変更時のキャッシュ無効化 |
| 7 | スクロールバーのドラッグ（サムのヒット判定・トラック押下のサム移動・サム位置からのオフセット逆算） |
| 8 | 実効ズーム率の設定（0 以下の拒否とクランプ） |

| メソッド | シグネチャ | 内容 |
| --- | --- | --- |
| `SetSource` | `ZoomFeedback SetSource(bool hasSource, EsSize sourceSize)` | コンテンツ（画像）の設定。 Fit 表示へリセットする |
| `SetViewport` | `ZoomFeedback SetViewport(EsSize viewportSize)` | ビューポートのサイズ。 Fit キャッシュを常に無効化する |
| `SetCenterAlign` | `ZoomFeedback SetCenterAlign(bool centerAlign)` | 中央寄せ／左上寄せ（同値なら何もしない） |
| `SetAppearance` | `ZoomFeedback SetAppearance(ZoomViewAppearance appearance)` | 外観（同値なら何もしない） |
| `ApplyConfig` | `ZoomFeedback ApplyConfig(ZoomOptions config)` | 設定の反映（可否のキャッシュを破棄する） |
| `SetFixedZoom` | `ZoomFeedback SetFixedZoom(double zoomPercent)` | 実効ズーム率の設定（0 以下は無視、範囲へクランプ、ズーム中心を維持） |
| `SetZoom` | `ZoomFeedback SetZoom(double zoomPercent, EsPoint? center = null)` | 内部ズーム率の設定（0 以下は Fit。 ズーム中心を維持） |
| `SetOffset` | `ZoomFeedback SetOffset(EsVector offset)` | パン位置の設定（クランプ。 同値なら何もしない） |
| `PanBy` / `PanByWheel` | `ZoomFeedback PanBy(EsVector delta)` / `PanByWheel(EsVector delta, bool shift)` | コンテンツ座標の移動量でパン / ホイールの回転量でパン（実効倍率で除す） |
| `Press` / `Move` / `Release` | `ZoomFeedback Press(EsPoint position)` ほか | grab 方式のパン開始・追従・終了 |
| `ZoomIn` / `ZoomOut` / `ZoomFit` | `ZoomFeedback ZoomIn()` ほか | 段階遷移・Fit への移行 |
| `ZoomByWheel` | `ZoomFeedback ZoomByWheel(bool zoomIn, EsPoint? pointerPosition = null)` | ホイール倍率のズーム（中心を指定可能） |
| `TryBeginScrollBarDrag` | `bool TryBeginScrollBarDrag(EsPoint position)` | スクロールバーのドラッグ開始（トラック押下はサム移動） |
| `UpdateScrollBarDrag` / `ReleaseScrollBarDrag` | `ZoomFeedback UpdateScrollBarDrag(EsPoint position)` ほか | ドラッグの反映と終了 |

**読み取り**: `IsFit` / `InternalZoomPercent` / `ZoomPercent`（実効値）/ `Scale` / `EffectiveScale` / `Offset` / `CanZoomIn` / `CanZoomOut` / `CurrentState` / `DisplaySize` / `ImageOrigin` / `ImageRect` / `CurrentScrollBars()` / `ScrollMetrics(bool vertical)` / `ClientToSource` / `ClientToDisplayedSource` / `DisplayedSourceToClient` / `IsPanning` / `IsDraggingScrollBar`。

**通知**: `IDisposable Subscribe(Action<ZoomNotice>)` の購読者が `ZoomChanged`（`ZoomState` を運ぶ）と `ViewportChanged`（画像表示矩形を運ぶ）を受け取ります（値型の通知は `event` を使わない。 解除は戻り値の `Dispose`）。 UI 層は自 UI の公開イベントへ転送するだけです。

**座標系**: UI フレームワークの幾何型ではなく、`EsUtil.Platform.Core` の `EsPoint` / `EsVector` / `EsSize` を使います。 UI 実装は境界で 1 行の相互変換を行い、UI 内部の状態とイベント引数は UI 側の型のまま維持します。

### 10.2 `ZoomOptions`（設定値）

ズーム遷移のパラメータをまとめた検証付き不変値です。

| メンバー | 型 | 説明 |
| --- | --- | --- |
| `MinZoom` | `double` | ズーム率の最小値（固定倍率。 %）。 0 より大きい |
| `MaxZoom` | `double` | ズーム率の最大値（固定倍率。 %）。 `MinZoom` 以上 |
| `WheelFactor` | `double` | Ctrl+ホイールのズーム倍率。 1 より大きい |
| `ButtonZoomLevels` | `IReadOnlyList<double>` | ⊖⊕ ボタンのズームレベル集合。 非空・昇順・`MinZoom`〜`MaxZoom` の範囲内 |
| `Default` | `ZoomOptions`（static） | 既定値（`MinZoom=10` / `MaxZoom=500` / `WheelFactor=1.1` / 23 段階） |
| `ZoomOptions()` | コンストラクタ | 既定値で生成する |
| `ZoomOptions(double, double, double, IReadOnlyList<double>)` | コンストラクタ | 全プロパティを指定して生成する（検証付き） |
| `With(double?, double?, double?, IReadOnlyList<double>?)` | メソッド | 一部を差し替えた検証付きの新しい `ZoomOptions` を返す（null は現在値維持） |

| 条件 | 例外 |
| --- | --- |
| `MinZoom <= 0` / `MaxZoom < MinZoom` / `WheelFactor <= 1` | `ArgumentOutOfRangeException` |
| `ButtonZoomLevels` が null | `ArgumentNullException` |
| `ButtonZoomLevels` が空・非昇順 | `ArgumentException` |
| `ButtonZoomLevels` に範囲外の値が含まれる | `ArgumentOutOfRangeException` |

### 10.3 `ZoomTransition`（内部・純粋関数）

| メソッド | 説明 |
| --- | --- |
| `NextLevel` / `PrevLevel` | ズームインの次のレベル / ズームアウトの前のレベル。 移動先がなければ null（Fit 実効倍率がレベルの間に挟まる場合はそれを経由する） |
| `WheelZoomIn` / `WheelZoomOut` | Ctrl+ホイールのズームイン／ズームアウト倍率（`Round(現在 × WheelFactor)`、範囲にクランプ）。 移動不可は null |
| `CanZoomIn` / `CanZoomOut` | ズームイン／ズームアウト可能か（`NextLevel` / `PrevLevel` が null でないか） |
| `BuildAvailability` | ズーム可否（`CanZoomIn` / `CanZoomOut`）をまとめて計算する |

`current = 0` は Fit 表示を表し、`fitZoom`（Fit 実効倍率）を基準に遷移します。

### 10.4 `ZoomMath`（内部・純粋関数）

| メソッド | 説明 |
| --- | --- |
| `FitScale` / `FitZoomPercent` | ビューポートに収まる Uniform（アスペクト比維持）の実効倍率・実効ズーム率 |
| `ComputeEffectiveScale` | 内部ズーム率（0 = Fit）から実効倍率を導出する |
| `ComputeOffsetAfterScaleChange` | ズーム中心を維持するためのオフセットを計算する |
| `ClampOffset` / `MaxCentered` | スクロール範囲へのクランプ・中央寄せ／左上寄せの表示サイズ |
| `ComputeImageOrigin` | 画像の表示矩形の原点（中央寄せ補正込み）を求める |
| `ClientToSource` | ビューポート座標 → コンテンツ座標の変換（画像表示矩形の原点基準の版も持つ） |
| `ScaleFromPercent` | % → 倍率の換算 |

スケールが 0 以下の場合は 1 として扱い、例外を発生させません。

### 10.5 `ZoomViewAppearance`（外観）

下地色・市松模様（タイルサイズと 2 色）・スクロールバー（表示可否・太さ・余白・サム色・トラック色）を単一所有します。 描画の可否（`HasCheckerboard` / `HasScrollBarTrack`）と、市松模様を描く範囲（`ComputeCheckerArea`。 画像の表示矩形とビューポートの交差）・明色タイル矩形（`ComputeLightTileRects`）・タイルサイズの実効値（`ResolveCheckerTileSize`。 0 以下 = 描かない / 1 は下限 2 へ切り上げ）も本型が持ちます。 色は UI 非依存の `System.Drawing.Color` です。

**市松模様は画像の透明部分の背景**であり、描く範囲は画像の表示矩形の内側に限ります（画像の外＝ペーストボードは下地色 `BackgroundColor` の単色になります）。 タイルの格子はビューポートの原点 `(0, 0)` を基準にするため、画像をパンしても模様が這いません。 範囲の外へはみ出すタイルは範囲で切り取った矩形として返します（呼び出し側は描画先をクリップする必要がありません）。

### 10.6 `ZoomScrollBar` / `ZoomScrollBarMetrics` / `ZoomScrollBarGeometry` / `ZoomAvailability`

| 型 | 内容 |
| --- | --- |
| `ZoomScrollBar` | オーバーレイスクロールバーの矩形（`Track` / `Thumb`） |
| `ZoomScrollBarMetrics` | 標準スクロールバー部品へ写す同期値（`MaxOffset` / `ViewportSize` / `IsScrollable`） |
| `ZoomScrollBarGeometry`（internal） | `Compute`（トラック・サムの矩形）・`MovableThumbRange`（可動範囲）・`SourceOffsetFromThumbInRange`（サム位置からのオフセット逆算）。 最大スクロール量は `EsUtil.Platform.Core.Coord.ScrollMath.MaxOffset` を使う |
| `ZoomAvailability`（internal） | ズーム可否（`CanZoomIn` / `CanZoomOut`）を単一に保持する値オブジェクト |

### 10.7 `CapturePreviewLogMessage`（ズーム操作のログ）

ズーム操作の実行前後を Information で記録します。 書式は両 UI で同一です（例: `[CapturePreviewControl] ZoomIn: 50% -> 62%`）。 **変更前の値は実効ズーム率**（Fit 中は実効倍率）を記録し、丸め（四捨五入）は `CapturePreviewLogMessage.Percent` が単一所有します。 カテゴリは各 UI が与えます。

| メソッド | 内容 |
| --- | --- |
| `LogZoomIn(double current, double target)` | ボタン・キーによるズームイン |
| `LogZoomOut(double current, double target)` | ボタン・キーによるズームアウト |
| `LogZoomInWheel(double current, double target)` | ホイールによるズームイン |
| `LogZoomOutWheel(double current, double target)` | ホイールによるズームアウト |

引数は実効ズーム率（%）です。 **丸めは `Percent` が 1 箇所で行う**ため、呼び出し側は丸めを書かず、Information が無効な場合は `IsEnabled` で早期に戻します。

### 10.8 `ZoomState` / `ZoomFeedback` / `ZoomNotice`（契約）

| 型 | 内容 |
| --- | --- |
| `ZoomState` | 通知する 4 値（`ZoomPercent` / `IsFit` / `CanZoomIn` / `CanZoomOut`） |
| `ZoomFeedback` | UI が適用する効果（`ShouldRedraw` / `ShouldCapturePointer` / `ShouldReleasePointer`）。 `ApplyVisual` / `ApplyPointer` に**適用順を単一所有**する |
| `ZoomNotice` / `ZoomNoticeKind` | 通知の種別（`ZoomChanged` / `ViewportChanged`）と通知時点の値（`State` / `ImageRect`）。 `ZoomChanged` は確定した 4 値、`ViewportChanged` は画像の表示矩形を運び、オーバーレイが同じ矩形を参照できるようにする |

## 11. 提示の写像（`Presentation` 系）

製品の設定（`CaptureSettings`）と、提示層（`EsUtil.Feature.*.Presenter`）の外観型を橋渡しする写像です。 **UI フレームワークに依存しません**（実寸測定はデリゲートで受け取ります）。 製品の設定型と提示層の外観型の両方を必要とするため、両者を参照できる本プロジェクトが単一所有します（UI ごとに書くと写像漏れが片方だけに生じます）。

### 11.1 CapturePaintMapper（内部）

`CaptureSettings` → 外観の写像を単一所有します（`internal`。 両 UI のホストとテストへ `InternalsVisibleTo` で公開）。

| メソッド | 内容 |
| --- | --- |
| `ToMarqueeAppearance(CaptureSettings settings)` | 選択矩形コントロールの外観（色 9 項目 + 枠線の太さ + 表示可否 4 項目）を組み立てる。 枠線の色はハンドルの枠線と暗転マスクの外枠の両方へ同じ値を割り当てる（同じ矩形に重なるため） |
| `ToLoupeAppearance(CaptureSettings settings)` | ルーペの外観（色 5 項目 + 枠線の太さ + 座標ラベルの表示可否）を組み立てる |

引数の `settings` は `ToNonNull` 済みを前提とします（null は `ArgumentNullException`）。 **寸法・対話パラメータは写像しません**（コアの設定値（`SelectionOverlayOptions`）が持ち、`SelectionOverlayOptionsMapper` が単一所有します）。 内側の塗り・内側の枠線は写像せず既定の透明を保ちます（描画の可否は外観の明示フラグが単一所有します）。

### 11.2 SelectionOverlayChildValues

設定（`SelectionOverlayOptions`）とモード（`SelectionOverlayMode`）から、内包コントロール（選択矩形・ルーペ）へ適用する値を解決します。 モードの解釈はコアの `SelectionOverlayState.Resolve` が単一所有し、本型は「設定の値を写し、モードから導出する項目（ハンドルの表示可否・全面暗転の可否・入力の可否）だけを上書きする」手順を単一所有します。 **ハイライト矩形は実行時状態**のため本値に含めません（子コントロールが現在値を持ち続けます）。

### 11.3 SelectionOverlayLabelRenderer / MarqueeHostLabelRenderer

付加情報ラベル（操作・情報）の配置指示を解決します（**描画は行いません**。 実寸測定はデリゲートで受け取ります）。

| 型 | 内容 |
| --- | --- |
| `SelectionOverlayLabelRenderer` | 注目矩形の基準点から、操作ラベル → 情報ラベルの順に**縦積み**の配置指示を返す。 サイズラベルが描かれている場合はその下から始める（行送り = 行の高さ + 内側余白 × 2）。 表示スイッチと文言（空文字を除く）の両方が成立した行だけを返す |
| `MarqueeHostLabelRenderer` | ラベル 1 行の体裁（背景 = 文字の実寸 + 内側余白 × 2）と、基準点を背景の左上とする配置指示を返す。 体裁の値（内側余白）は提示層の外観（`MarqueeAppearance`）が単一所有する |

文言の生成はホストが担います（付加情報は複合コントロールの概念ではないため）。

### 11.4 HighlightAppearanceDefaults

切り出しビューの「強調（ハイライト）」の既定の外観（`MarqueeAppearance`）を単一所有します。 内側の塗りと枠線だけを描き、ラベル・ハンドル・始点マーカー・暗転・外枠は描きません（色はホストが与えるため透明のまま）。 両 UI の `CropViewControl` とアプリが同じ既定を使います。
