# CaptureSessionControl.MewUI 外部機能仕様

MewUI 版の撮影セッションが提供する**公開 API** を記載します。内部実装（描画手順・キャッシュ等）は記載しません。

## 1. 公開型の構成

| 型 | 種別 | 役割 |
| --- | --- | --- |
| `CaptureOverlay` | 静的クラス（ファサード） | 撮影セッションの実行 |
| `CaptureOverlayServices` | record | セッションが必要とするサービスと設定の束 |
| `CropOverlay` | クラス | プレビューの切り出しセッション（オーバーレイの表示・確定・キャンセル） |

名前空間は `Estable.MewUI.CaptureSession` です。

## 2. CaptureOverlay

| メソッド | シグネチャ | 内容 |
| --- | --- | --- |
| `Capture` | `EsBitmap? Capture(Window owner, CaptureType type, CaptureOverlayServices services)` | オーバーレイを表示し、確定した領域を切り出して返す。キャンセル・取得失敗・退化した選択の場合は null |

### 2.1 動作

1. 設定（`CaptureOverlayServices.Settings`）に従ってオーナーウィンドウをキャプチャ除外に設定し、画面外へ退避します（`CaptureOwnerScope` が担う）。
2. 仮想スクリーン（全モニターを包含する領域）を 1 枚の画像として取得します（`ICaptureService.CaptureVirtualScreen`）。
3. 画像を敷いたボーダーレス・最前面のウィンドウを仮想スクリーン全面へ表示し、確定またはキャンセルまで待ちます（`ShowDialog` の入れ子ループ）。
4. 確定した領域を切り出して返し、オーナーの配置を復帰して例外を解除します（例外時も `finally` で復帰）。

### 2.2 操作

| モード | 操作 |
| --- | --- |
| スクリーン選択 | カーソル下のモニターをハイライトし、左クリックまたは Enter で確定 |
| ウィンドウ選択 | カーソル下の最前面ウィンドウをハイライトし、左クリックまたは Enter で確定 |
| 範囲選択 | ドラッグで矩形を指定し、Enter で確定（矢印キーで微調整できる） |

- 右クリックでは確定しません。要素を検出できない位置のクリックでも確定しません（誤操作で撮影を取りこぼさないため）。
- Escape または他ウィンドウへの切り替え（非アクティブ化）でキャンセルします。
- オーバーレイはタスクバー・Alt+Tab の一覧に表示しません（一時的な表示のため）。

### 2.3 座標系

| 座標 | 意味 |
| --- | --- |
| 画像ピクセル（ドメイン） | 取得画像のピクセル。仮想スクリーン原点が画像の (0, 0) |
| 画面物理ピクセル | 検出結果・入力（マウス）の座標系 |

ドメイン座標とローカル DIP の変換は `ICoordTransform` を選択コントロールへ注入して行います（コントロールは座標系を意識しません）。

### 2.4 切り出し規則

- 範囲選択は**閉区間**（始点・終点のピクセルを含む）で切り出します。
- スクリーン選択・ウィンドウ選択は検出結果が全ピクセルを含むため、終点の 1 ピクセルを加算しません。
- 最小サイズ未満・画像と重ならない選択はキャンセル扱い（null）になります。
- 計算は `CaptureSessionControl.Core` の `CropBoundsMath` が単一所有します。

## 3. CaptureOverlayServices

| メンバー | 型 | 内容 |
| --- | --- | --- |
| `Capture` | `ICaptureService` | 画面キャプチャ（仮想スクリーンの取得） |
| `Finder` | `IElementFinder` | 要素検出（カーソル下のモニター・ウィンドウ） |
| `Window` | `IWindowService` | ネイティブウィンドウ操作（キャプチャ除外・退避・物理ピクセルでの配置） |
| `Settings` | `CaptureSettings` | キャプチャ設定（外観・動作） |
| `Logger` | `ILogger?` | ロガー（null の場合は出力しない） |
| `Title` | `string` | オーバーレイウィンドウのタイトル（既定値 `SaveEvidence キャプチャ`。検証スクリプトがウィンドウを識別するため「キャプチャ」を含めること） |

`ICaptureService` / `IElementFinder` / `IWindowService` は `Platform.Core`（名前空間 `EsUtil.Platform.Core`）が提供します。生成は `PlatformFactory.CreateXxx()` が唯一の経路です。

## 4. 使用する設定項目

`CaptureSettings`（`CaptureSessionControl.Core`）のうち、本ライブラリが参照する項目です。

| 項目 | 用途 |
| --- | --- |
| `HideOwnerWindow` | 撮影中にオーナーウィンドウを画面外へ退避するか |
| `MinSelectionSize` | 選択矩形の最小サイズ（未満はキャンセル扱い） |
| `BorderColor` / `BorderThickness` / `OverlayColor` | 選択枠と暗転の外観（`FadeMaskAppearance` へ反映。枠線は選択矩形と共用） |
| `HandleColor` / `HandleSize` / `HandleHitRadius` | ドラッグハンドル |
| `MarkerColor` / `MarkerLength` | 微調整時の十字マーカー |
| `ClampToDomainBounds` | 画像外の選択を禁止するか |
| （ホイール操作） | 選択オーバーレイ上のホイールをビューポート（ズーム・パン）へ転送する。ズーム率・パン量の決定は `CropViewControl` が単一所有する |
| `ShowCoordLabel` / `CoordLabelColor` / `CoordLabelBackgroundColor` / `CoordLabelFontSize` | カーソル位置の座標ラベル |
| `SizeLabelVisible` / `SizeLabelColor` / `TextBackgroundColor` / `SizeLabelFontSize` | サイズラベル（`FadeMaskAppearance` の SizeLabel 系へ反映） |
| `InfoLabelVisible` / `InfoLabelColor` / `InfoLabelBackgroundColor` / `InfoLabelFontSize` | 情報ラベル（ウィンドウタイトル。`FadeMaskAppearance` の InfoLabel 系へ反映） |
| `ShowSubLines` / `SubLineFontSize` / `SubLineColor` / `SubLineBackgroundColor` | 操作ラベル（サブライン。`FadeMaskAppearance` の SubLine 系へ反映） |
| `LoupeSize` / `LoupeZoomLevel` / `LoupeOffset` / `LoupeCrossColor` / `LoupeFrameColor` / `LoupeFrameThickness` | ルーペ |
| `LoupeCheckerSize` / `LoupeCheckerColorA` / `LoupeCheckerColorB` | ルーペの市松模様 |
| `LoupeSelectionRectColorA` / `LoupeSelectionRectColorB` | ルーペ画像上の選択矩形破線 |
| `LoupeShowCoordLabel` / `LoupeCoordLabelColor` / `LoupeCoordLabelBackgroundColor` / `LoupeCoordLabelFontSize` | ルーペの中心座標ラベル |

### 4.1 適用先

上表の項目は撮影（`CaptureOverlay`）と切り出し（`CropOverlay`）の**双方**が同じ対応で反映します（変換はコアの `FadeMaskDragRectOptionsMapper` が単一所有する）。
UI ごとに固定値（白枠 2 DIP 等）を持たないため、設定を変えるとプレビューの切り出しにも反映されます。
情報ラベルは `FadeMaskLabels.ShouldShowInfoLabel` が WindowSelect モードに限定するため、切り出しでは表示されません。
