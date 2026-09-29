# Feature.Loupe.Presenter 外部機能仕様書

`LoupeControl`（拡大鏡）の見た目を定める **コントロール化を前提としない UI 非依存層**が提供する外部機能と公開 API の仕様です。利用者（UI 実装・ホスト）から見える情報のみを記載します。設計の意図は [README](README.md)、構造と依存は [SPEC_System](SPEC_System.md) を参照してください。

## 1. 提供機能

| 機能 | 提供元 | 説明 |
| --- | --- | --- |
| 描画計画の解決 | `LoupePresenter.ResolveDrawPlan` | コアの幾何（`LoupeGeometry`）と外観から、1 フレーム分の描画計画（`LoupeDrawPlan`：描画面・拡大画像の幾何・十字線 / 枠線の矩形・層の順序・ラベル配置）を組み立てる。**描画は行わない** |
| 表示レイアウトの解決 | `LoupePresenter.ResolveLayout` | 親領域サイズから表示サイズ・位置を解決する（コアへ委譲する） |
| 外観 | `LoupeAppearance` | 色・フォント・角丸・枠線の太さ・表示可否（描画にだけ必要な設定の単一値） |
| 装飾矩形の計算 | `LoupePresenter`（`DrawPlan` partial） | 十字線（縦横 2 本）と枠線（4 辺）の矩形の計算（純粋関数） |
| ラベル計画 | `LoupePresenter`（`LabelTexts` partial） / `LoupePresenter`（`LabelPlan` partial） | 文言と基準点の生成（`EsPoint.Format`・左上の内側）と、実寸 → 配置指示（`LoupeLabelPlacement`）の写像 |
| ログ定義 | `LoupePresenterLogMessage` | 本層が単一所有する設定（外観）の変更ログ |

**本層はコントロール化を前提とした処理を提供しません**（拡大元画像・再描画のスケジューリング・組み込み先の概念は、コントロール固有の値や UI の機構であるため UI 層が持ちます）。

### 前提条件

- 本層は UI フレームワークに依存しません（依存は `Feature.Loupe.Core` と `Platform.Core` のみ）。**描画 API の抽象も持ちません**（描画計画というデータを返し、描画は各 UI が自 UI の API で行います）。
- 座標は `EsPoint` / `EsSize` / `EsRect`（`EsUtil.Platform.Core`）、色は UI 非依存の `System.Drawing.Color` で扱います。
- 描画の単位は**物理ピクセル**です（`LoupeGeometry.PixelSize` が作る正方形が描画面）。DIP → 物理ピクセルの換算（表示サイズと枠線の太さ）は本層とコアが済ませているため、UI 層は換算しません。
- 中心座標ラベルは **DIP** で配置します（文言・基準点は本層が、実寸測定と要素の配置は UI 層が担います）。

## 2. 公開 API 一覧

### `EsUtil.Feature.Loupe.Presenter`（提示・外観）

ルート直下（`EsUtil.Feature.Loupe.Presenter`）に置くのはファサード（`LoupePresenter`）だけです。以降の型は用途別フォルダの名前空間（`.Setting` / `.Drawing` / `.Label` / `.Logging`）に属します（**名前空間はフォルダに追従**）。

| 型 | 種別 | 説明 |
| --- | --- | --- |
| `LoupePresenter` | class | 提示のファサード（コアのラップ・外観の保持・描画計画の解決）。`sealed` ではない（派生で見た目を差し替えられる） |
| `Setting.LoupeAppearance` | record | 外観（色・フォント・角丸・枠線の太さ・表示可否） |
| `Drawing.LoupeDrawPlan` | record struct | 1 フレーム分の描画計画（描画面・矩形・層の順序・ラベル配置・通知用の値） |
| `Drawing.LoupeDrawLayer` | enum | 描画層（`Pasteboard` / `Image` / `Crosshair` / `Border` / `Label`） |
| `Drawing.LoupeTextPlacement` | record struct | 描画層が UI 層へ渡すテキスト（文言・基準点・描画領域） |
| `Label.LoupeLabelPlacement` | record struct | ラベル要素の配置指示（背景矩形・内側余白・文言・フォントサイズ・色・角丸） |
| `LoupePresenter.LabelPlan.cs` | static class | 実寸 → `LoupeLabelPlacement` の写像 |
| `Logging.LoupePresenterLogMessage` | static partial class | 本層が単一所有する設定の変更ログ（内部手順。ホストは参照しない） |

`LoupePresenter.DrawPlan.cs` / `LoupePresenter.LabelTexts.cs` / `Logging.LoupePresenterLogMessage` は `internal`（本層とテストのみが使う）。

### `LoupePresenter` のメンバー

ルーペの**提示の窓口**（適用パターン: Facade / Presenter）。UI 層は本型だけを参照します。

| メンバー | 種別 | 説明 |
| --- | --- | --- |
| `Options` | property | コアの設定（計算に必要な値）を転送する |
| `DipToDeviceScaling` / `CenterPoint` / `PositionOffset` | property | コアの環境値・拡大中心・オフセットを転送する |
| `Log` | property | コアのロガーを転送する |
| `Appearance` | property | 外観（本層が単一所有する） |
| `SetCoordTransform(ICoordTransform?)` | method | コアへ委譲する |
| `ResolveLayout(EsSize)` | method | コアへ委譲する（表示サイズ + 位置） |
| `ResolveDrawPlan(double displaySize, EsSize? availableSize, Func<string, double, EsSize> measureText)` | method（`virtual`） | 描画計画（`LoupeDrawPlan`）を解決する。`measureText` が null なら `ArgumentNullException` |

### `LoupeAppearance` のメンバー

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `PasteboardColor` | `Color` | `#FF535353` | 画像外（ペーストボード）の色 |
| `BorderColor` / `CrossColor` | `Color` | `#FFD3D3D3` / `#66FF0000` | 枠線 / 十字線の色 |
| `CoordLabelColor` / `CoordLabelBackgroundColor` | `Color` | 白 / `#99000000` | 中心座標ラベルの文字色 / 背景色 |
| `CoordLabelFontSize` / `CoordLabelCornerRadius` | `double` / `int` | `12.0` / `2` | 中心座標ラベルのフォントサイズ / 角丸（6〜48 / 0〜64 へ補正） |
| `BorderThickness` | `int` | `2` | 枠線の太さ（DIP。1〜64 へ補正。安全域は `Loupe.Limits` が単一所有する） |
| `ShowCrosshair` / `ShowBorder` / `ShowCoordLabel` | `bool` | `true` | 表示可否（描画にだけ必要。透明色を代用にしない） |

| メンバー | 説明 |
| --- | --- |
| `Default` | すべての項目を既定値にした外観 |
| `ShouldShowCoordLabel(bool hasCenterPoint)` | 中心座標ラベルを描画するかを判定（`ShowCoordLabel` かつ拡大中心あり） |

### `LoupeDrawPlan` のメンバー

| メンバー | 説明 |
| --- | --- |
| `Appearance` | 外観（描画時に色・フォントを使うため保持する） |
| `SurfaceBounds` | 描画面の全体（物理ピクセルの正方形） |
| `MagnifiedImage` | 拡大画像の切り出しと描画の矩形（`LoupeMagnifiedImage`。中心未設定は null） |
| `CrosshairRects` / `BorderRects` | 十字線（縦横 2 本）/ 枠線（4 辺）の矩形 |
| `Layers` | 描く層とその順序（先頭から処理する） |
| `Labels` | ラベルの配置指示（`LoupeLabelPlacement` の一覧。空は表示なし） |
| `CaptureTopLeft` / `ZoomLevel` / `PixelSize` | 描画完了通知（`Rendered`）の組み立てに使う値 |

### `LoupeDrawLayer` の値

| 値 | 内容 |
| --- | --- |
| `Pasteboard` | ペーストボード（画像外の色）。常に含む（描画面を置き換えて前フレームの残像を残さない） |
| `Image` | 拡大画像（切り出し領域を解決できた場合のみ） |
| `Crosshair` / `Border` | 十字線 / 枠線（表示可否が有効な場合のみ） |
| `Label` | 中心座標ラベル（ラベルがある場合のみ。常に最上位） |

### `LoupePresenter`（`LabelPlan` partial） のメンバー

| メンバー | 説明 |
| --- | --- |
| `Resolve(LoupeTextPlacement? coordLabel, LoupeAppearance appearance, Func<string, double, EsSize> measureText)` | 配置するラベル要素（`LoupeLabelPlacement`）を 1 件返す（テキストが無い場合は空）。`appearance` または `measureText` が null なら `ArgumentNullException` |

### `LoupePresenterLogMessage` のメンバー

| メンバー | 説明 |
| --- | --- |
| `LogAppearanceChanged(LoupeAppearance, LoupeAppearance)` | 外観の変更ログ（**変化した項目だけ**。LogLevel: Debug、書式 `[Feature.Loupe.Presenter] Setting: {Name} {OldValue} → {NewValue}`） |

**拡大元画像（`Source`）と描画の例外のログは各 UI が所有します**（コントロール固有の値であるため。接頭辞だけを揃える）。

## 3. 契約（利用者が守る・受け取る値）

### 描画計画（`LoupeDrawPlan`）

描画計画は**UI が見る唯一の描画値**です。UI は `Layers` の順に自 UI の描画 API を呼び、`Labels` を要素として置くか文字として描くかを決めます。**描く値はすべて解決済み**であり、UI は幾何計算（矩形の導出・寸法の換算）を行いません。

### ラベル（`LoupePresenter`（`LabelPlan` partial） → `LoupeLabelPlacement`）

- `Labels` の各要素は**配置矩形・内側余白・文言・フォントサイズ・色・角丸**を持ちます（背景の大きさは「実寸 + 内側余白 × 2」。クランプは `LoupePresenter`（`LabelPlan` partial） が単一所有する）。
- **要素として置くか canvas へ描くかは UI 層が決めます**（要素は親より後に描かれるため常に最上位になる）。

### 描画の順序

`LoupeDrawLayer` の順序（`Pasteboard` → `Image` → `Crosshair` → `Border` → `Label`）は `LoupeDrawPlan.Layers` が単一所有します。UI は順序を判断しません。

## 4. 座標系

| 座標系 | 用途 |
| --- | --- |
| 物理ピクセル | 描画面・矩形（`LoupeDrawPlan` の矩形） |
| DIP | ラベル（`LoupeLabelPlacement.Bounds` / `PaddingX` / `PaddingY`） |

## 5. 制限事項

- **描画は行いません**（描画計画というデータを返すだけです。描画は各 UI が自 UI の API で行います）。
- 描画 API の抽象（キャンバスの抽象）は持ちません（UI フレームワークごとの描画モデルの差を吸収しきれないため）。
- 中間ビットマップの非同期生成と世代番号管理（`LoupeRenderCoordinator`）は `LoupeControl.AvaloniaUI` が所有します（中間ビットマップを持つ UI にだけ必要な処理です）。

## 6. ログ

UI が出力するログ（拡大元画像の変更・描画の例外）の定義と書式は本層が単一所有します（`[Feature.Loupe.Presenter]` の接頭辞）。**UI 固有の処理に対応するログは各 UI プロジェクトが所有します**（中間ビットマップを生成する描画パイプライン固有のログ等は `LoupeControl.AvaloniaUI` にあります）。

## 7. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
