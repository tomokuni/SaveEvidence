# CaptureSessionControl.Core 外部機能仕様

撮影・切り出しの共通規則が提供する**公開 API** を記載します。内部実装（呼び出し手順・キャッシュ等）は記載しません。

## 1. 公開型の構成

| 型 | 種別 | 役割 |
| --- | --- | --- |
| `CropBoundsMath` | 静的クラス | 切り出し矩形の計算（純粋関数） |
| `CaptureLabelTexts` | 静的クラス | キャプチャ種別→動作モードの写像と、ラベル文言（操作ラベル・情報ラベル）の単一所有 |
| `CaptureSettings` | record | 外観・動作設定（全メンバー nullable） |
| `CaptureResult` | record | キャプチャ結果（画像と領域） |
| `ICaptureTarget` | インターフェイス | 切り出し結果の適用先（アプリ側が実装するポート） |
| `ImageDrawMath` | 静的クラス | 画像を描画先の領域へ配置する矩形の計算（純粋関数） |

名前空間は `Estable.CaptureSession` です。

## 2. CaptureLabelTexts

キャプチャ固有のラベル文言とモード写像を単一所有します。選択矩形の複合コントロールは汎用化により固有の文言を持たないため、ホスト（両 UI のセッション）が本クラスの値をプロバイダで与えます。

| メソッド | シグネチャ | 内容 |
| --- | --- | --- |
| `ToSelectionMode` | `FadeMaskDragRectMode ToSelectionMode(CaptureType captureType)` | キャプチャ種別（全画面 / ウィンドウ / 範囲）→ 動作モード。未定義の種別は範囲選択として扱う（切出しは対象外） |
| `GetOperationText` | `string GetOperationText(FadeMaskDragRectMode mode)` | 操作ラベル（確定・キャンセル方法）。スクリーン選択・ウィンドウ選択は Enter 系、範囲選択・切出しはダブルクリック系（常に非空） |
| `GetInfoText` | `string? GetInfoText(FadeMaskDragRectMode mode, string? windowTitle)` | 情報ラベルの文言。ウィンドウ選択のみウィンドウタイトルを返し、他モードは null（描画しない） |

### 2.1 規則

| # | 規則 |
| --- | --- |
| 1 | モード写像は `CaptureType` の 3 種別を全て扱い、未定義値は範囲選択へフォールバックする |
| 2 | 操作ラベルの文言は 2 系統（Enter 系 / ダブルクリック系）に集約する |
| 3 | 情報ラベルはウィンドウ選択でのみ意味を持つため、他モードでは null を返す（表示可否の最終判定（設定）は描画側） |

## 3. CropBoundsMath

| メソッド | シグネチャ | 内容 |
| --- | --- | --- |
| `ComputeCropBounds` | `EsPixelRect ComputeCropBounds(EsRect selection, EsSize imageSize, double minSelectionSize, bool closedInterval = true)` | 選択矩形から切り出し範囲を求める。切り出せない場合は `IsEmpty` が true の矩形 |
| `ToPixelRect` | `EsPixelRect ToPixelRect(EsRect selection, EsSize imageSize)` | 半開区間のまま画像内の整数矩形へ変換（クランプ・四捨五入） |
| `ToClosedInterval` | `EsPixelRect ToClosedInterval(EsPixelRect rect, EsSize imageSize)` | 終点の 1 ピクセルを加える（画像の端でクランプ） |

### 3.1 規則

| # | 規則 |
| --- | --- |
| 1 | 画像サイズが 0 以下の場合は空を返す |
| 2 | 選択矩形が `minSelectionSize` 未満（画像内へクランプする前の値で判定）の場合は空を返す |
| 3 | 座標は四捨五入（0.5 は切り上げ）し、画像の範囲へクランプする |
| 4 | 幅・高さが 1 ピクセル未満の場合は空を返す |
| 5 | `closedInterval` が true の場合は終点の 1 ピクセルを加える（画像の右端・下端でクランプ） |

### 3.2 使い分け

| 用途 | `closedInterval` |
| --- | --- |
| 範囲選択（ドラッグ）の切り出し | `true`（始点・終点のピクセルを含む） |
| スクリーン選択・ウィンドウ選択の切り出し | `false`（検出結果が全ピクセルを含むため加算しない） |

## 4. CaptureSettings

外観・動作設定を保持します。**全メンバーが nullable** で、用途により null の意味が異なります。

| 用途 | null の意味 |
| --- | --- |
| 初期設定（`CaptureSessionControl` のコンストラクタ） | デフォルト値を適用する |
| 既存インスタンスへの適用（`AppearanceSettings` の setter） | 現在値を維持する（部分更新） |

主なメンバー（抜粋）:

| メンバー | 型 | 内容 |
| --- | --- | --- |
| `HideOwnerWindow` | `bool?` | 撮影時にオーナーウィンドウを画面外へ退避するか |
| `LoupeVisible` | `bool?` | ルーペの表示状態 |
| `MinSelectionSize` | `int?` | 選択矩形の最小サイズ（1 以上） |
| `BorderColor` / `BorderThickness` | `System.Drawing.Color?` / `double?` | 選択枠の色・太さ（DIP） |
| `OverlayColor` | `System.Drawing.Color?` | 暗転オーバーレイの色（不透明度はアルファに統合） |
| `LoupeSize` / `LoupeZoomLevel` / `LoupeOffset` | `int?` / `int?` / `double?` | ルーペのサイズ・倍率・オフセット |
| `LoupeRenderScaling` | `double?` | ルーペの描画倍率（0.1〜8.0） |
| `SizeLabelVisible` / `SizeLabelColor` / `SizeLabelFontSize` | `bool?` / `System.Drawing.Color?` / `double?` | サイズラベルの表示・色・サイズ |
| `InfoLabelVisible` / `ShowSubLines` / `SubLineFontSize` | `bool?` / `bool?` / `double?` | 情報ラベル・サブライン |
| `SizeLabelOverride` | `string?` | サイズラベルの文言明示指定（null = 自動生成） |
| `DisplayScaleScaling` | `double?` | サイズラベル表示用のスケーリング（0 以下は `Scaling` と同値） |

| メソッド | シグネチャ | 内容 |
| --- | --- | --- |
| `Validate` | `void Validate()` | 設定値を検証する。範囲外の場合は `ArgumentOutOfRangeException`（null は検証対象外） |
| `ToNonNull` | `CaptureSettings ToNonNull()` | null をデフォルト値で埋めた non-null 版を返す |

**色は `System.Drawing.Color`** です（UI 非依存のため）。`Validate` が検証する範囲は `MinSelectionSize` / `HandleSize` / `HandleHitRadius` / `MarkerLength` / `LoupeSize` / `LoupeZoomLevel` / `LoupeFrameThickness` / `LoupeCheckerSize`（1 以上）、`BorderThickness` / `LoupeOffset`（0 以上）、`LoupeRenderScaling`（0.1〜8.0）、`HideBeforeCaptureDelay`（負の値不可）です。

## 5. CaptureResult

| メンバー | 型 | 内容 |
| --- | --- | --- |
| `Image` | `EsBitmap?` | キャプチャした画像。キャンセル時は null |
| `SourceBounds` | `EsPixelRect?` | キャプチャした領域（仮想スクリーン座標）。キャンセル時は 0x0 |

`Image` と `SourceBounds` は常に同時に null または同時に非 null になります。`SourceBounds` は複数モニターを包含する座標空間のため、プライマリモニターが (0, 0) にない場合は負値になり得ます。
