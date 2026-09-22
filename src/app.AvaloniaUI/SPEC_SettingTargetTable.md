# 設定項目と反映先コントロールの対応表（SPEC_SettingTargetTable）

## 1. 文書概要

| 項目 | 内容 |
| --- | --- |
| 対象 | 設定画面（`SettingsPage.axaml`）の全設定項目 |
| プロジェクト | `app.AvaloniaUI` |
| 対応の範囲 | 設定項目 → `Settings` プロパティ → 反映先コントロール |
| 関連文書 | `SPEC_System.md` / `SPEC_ExtFunc.md` |

本表は、設定画面で変更できる各項目が、**どの `Settings` プロパティ**に保存され、**どのコントロールのどのプロパティ**へ反映されるかを一覧化したものです。

## 2. コントロール階層と外観プロパティの公開

### 2.1 コントロール階層（内包・使用関係）

```text
app
 ├─ CaptureSessionControl.AvaloniaUI（CaptureSessionControl：キャプチャセッション統括の Facade）
 │    ├─ 画面キャプチャ（ScreenSelect / WindowSelect / AreaSelect）
 │    │    └─ OverlayWindow（全画面オーバーレイ）
 │    │         └─ FadeMaskDragRectControl（SelectionOverlay）
 │    └─ プレビュー Crop（切出し）
 │         └─ StartCropAsync(cropView: CropViewControl, ...) ──◆ CropViewControl を「引数」として受け取る（内包しない）
 │
 └─ CapturePage（app が CropViewControl を生成・配置・所有 = プレビュー表示責務）
      └─ CropViewControl（CropView）
           ├─ ZoomViewControl（画像ビューポート）
           └─ FadeMaskDragRectControl（_selectionOverlay）

FadeMaskDragRectControl（_selectionOverlay）
  ├─ FadeMaskControl
  ├─ DragRectControl
  ├─ LoupeControl
  └─ Platform.AvaloniaUI（Avalonia アダプタ）／Platform.Native（サービス実装）／Platform.Core（抽象・値型）
```

- **`CaptureSessionControl.AvaloniaUI` は `CropViewControl` を内包しません**。`StartCropAsync(CropViewControl, ...)` の引数として外部から受け取るだけです。
- **`CropViewControl` のインスタンスは `app.AvaloniaUI` が所有**します（`CapturePage.axaml` の `crop:CropViewControl x:Name="CropView"`）。プレビュー表示（`Source` / `CenterAlign` / `ViewportBackground`）は app の責務です。
- `FadeMaskDragRectControl` は**2 つの場所**で再利用されます（資源分離・再利用）：
  1. `OverlayWindow.SelectionOverlay`：画面キャプチャ（領域選択）用
  2. `CropViewControl._selectionOverlay`：プレビュー Crop（切出し）用
- 両者は独立したインスタンスで、異なる座標系・モードで動作します（画面キャプチャ = 仮想スクリーンピクセル / Crop = 元画像ピクセル）。

### 2.2 各コントロールが公開する外観プロパティ

外観設定は、階層の各コントロールが**公開 API（`AppearanceSettings` / 個別プロパティ）**として提供する。
下位コントロールの外観は、`FadeMaskDragRectControl`（→ `FadeMaskDragRectSettings`）→ `CropViewControl` → `CaptureSessionControl.AvaloniaUI`（→ `CaptureSettings`）→ `app.AvaloniaUI` の順に委譲して公開される。

| コントロール | 公開する外観プロパティ | 備考 |
| --- | --- | --- |
| `app.AvaloniaUI` | — | `CaptureSessionControl.AvaloniaUI` の `AppearanceSettings`（`CaptureSettings`）へ `CaptureSettingsMapper` で変換して渡す |
| `CaptureSessionControl.AvaloniaUI` | `AppearanceSettings`（`CaptureSettings`） | 部分更新（null は現在値維持）。実行中の Crop セッションへ即時反映 |
| `CropViewControl` | `AppearanceSettings`（`FadeMaskDragRectSettings`）/ `Scaling` / `DisplayScaleScaling` / `LoupeRenderScaling` / `ViewportBackground` / `CenterAlign` / `ZoomView`（`ZoomViewControl`） | 外観は `FadeMaskDragRectControl` へ委譲。DPI スケーリングは個別プロパティ（コードから動的設定） |
| `ZoomViewControl` | `Background` / `CenterAlign` / `ZoomPercent` / `IsFit` | 画像ビューポートの外観 |
| `FadeMaskDragRectControl` | `AppearanceSettings`（`FadeMaskDragRectSettings`）/ `Scaling` / `DisplayScaleScaling` / `LoupeRenderScaling` / `Mode` / `WindowTitle` / `ShowFullOverlay` | 下位（DragRect / FadeMask / Loupe）の外観を一括公開 + DPI スケーリングを個別公開 |
| `FadeMaskControl` | `Appearance`（`FadeMaskAppearance`）+ 描画状態プロパティ | 外観は 1 つの値、描画状態（選択矩形・ハイライト・全面暗転・モード）は個別プロパティ（いずれも `FadeMaskDragRectSettings` 経由で設定） |
| `DragRectControl` | `BorderColor` / `BorderThickness` / `HandleColor` / `HandleSize` / `HandleHitRadius` / `MinSelectionSize` / `ClampToDomainBounds` / `MarkerColor` / `MarkerLength` / `ShowCoordLabel` / `CoordLabelColor` / `CoordLabelBackgroundColor` / `CoordLabelFontSize` | `FadeMaskDragRectSettings` から `ApplyAppearance` で設定 |
| `LoupeControl` | `RequestedSize` / `ZoomLevel` / `RenderScaling`（DPI 自動取得）/ `Appearance`（`LoupeAppearance`。十字線・市松模様・外枠・座標ラベル）/ `PositionOffset` | `FadeMaskDragRectSettings` から `ApplyAppearance` で設定（`RenderScaling` 以外。外観は 1 つの値に組み立てる） |
| `Platform.AvaloniaUI` | —（共通基盤） | カーソル操作（`ICursorService`）・座標変換ポート（`ICoordTransform`。`Platform.Core` 提供）を提供 |

### 2.3 反映経路

```text
SettingsPage.axaml（ColorPicker / CheckBox / NumericUpDown / ComboBox）
  → Settings（ObservableProperty・変更通知）
  → MainWindow.ApplyCaptureSessionSettings（CaptureSettingsMapper.From で CaptureSettings 生成）
  → CaptureSessionControl.AppearanceSettings（CaptureSettings・部分更新マージ）
  → CropViewControl.AppearanceSettings（FadeMaskDragRectSettings・部分更新マージ）
  → FadeMaskDragRectControl.ApplyAppearance
  → DragRectOptions / FadeMaskAppearance / LoupeControl の各プロパティ
```

- 設定画面の変更は `Settings` の `PropertyChanged` で **即時反映** される。
- CaptureSessionControl.AvaloniaUI への反映は `CaptureSettings`（nullable record・部分更新）を介して行い、`null` のメンバーは現在値を維持する。
- テーマ・画像配置・市松模様等、app 内で直接処理する項目は個別の購読で反映される。
- `Platform.AvaloniaUI` は共通基盤のため、外観設定の公開対象ではない（参考として記載）。

## 3. 下位コントロールが公開する外観プロパティと上位での公開有無

本節は、**各コントロールが公開する外観プロパティが、直接上位コントロールで公開されているか**を一覧化する。
下位コントロールの外観は、`FadeMaskDragRectControl`（→ `FadeMaskDragRectSettings`）→ `CropViewControl` → `CaptureSessionControl.AvaloniaUI`（→ `CaptureSettings`）→ `app.AvaloniaUI` の順に委譲して公開される。
各上位コントロールで「直接公開（個別プロパティ）」または「`AppearanceSettings` 経由で公開」かを示す。

### 3.1 DragRectControl の公開外観プロパティと FadeMaskDragRectControl での公開有無

| DragRectControl の公開プロパティ | FadeMaskDragRectControl での公開有無 | 設定画面で設定 | 備考 |
| --- | --- | --- | --- |
| `BorderColor` | ✅ `AppearanceSettings.BorderColor` | ✅ | `DragRectOptions` と `FadeMaskAppearance.BorderColor` に共通適用 |
| `BorderThickness` | ✅ `AppearanceSettings.BorderThickness` | ✅ | 同上 |
| `HandleColor` | ✅ `AppearanceSettings.HandleColor` | ✅ | |
| `HandleSize` | ✅ `AppearanceSettings.HandleSize` | ✅ | |
| `HandleHitRadius` | ✅ `AppearanceSettings.HandleHitRadius` | ✅ | |
| `MinSelectionSize` | ✅ `AppearanceSettings.MinSelectionSize` | ✅ | |
| `ClampToDomainBounds` | ✅ `AppearanceSettings.ClampToDomainBounds` | ✅ | |
| `MarkerColor` | ✅ `AppearanceSettings.MarkerColor` | ✅ | |
| `MarkerLength` | ✅ `AppearanceSettings.MarkerLength` | ✅ | |
| `ShowCoordLabel` | ✅ `AppearanceSettings.ShowCoordLabel` | ✅ | カーソル座標ラベルの表示有無 |
| `CoordLabelColor` | ✅ `AppearanceSettings.CoordLabelColor` | ✅ | |
| `CoordLabelBackgroundColor` | ✅ `AppearanceSettings.CoordLabelBackgroundColor` | ✅ | |
| `CoordLabelFontSize` | ✅ `AppearanceSettings.CoordLabelFontSize` | ✅ | |

- `FadeMaskDragRectControl` は DragRectControl の公開外観プロパティを**全て** `AppearanceSettings`（`FadeMaskDragRectSettings`）経由で公開する。
- DragRectControl の外観プロパティは**全て**設定画面（`SettingsPage.axaml` の外観設定）で設定可能（✅）。

### 3.2 FadeMaskAppearance の外観項目と FadeMaskDragRectControl での公開有無

| FadeMaskAppearance の外観項目 | FadeMaskDragRectControl での公開有無 | 設定画面で設定 | 備考 |
| --- | --- | --- | --- |
| `Scaling` | ✅ `Scaling`（個別プロパティ） | —（コードから動的設定） | DPI スケーリング |
| `DisplayScaleScaling` | ✅ `DisplayScaleScaling`（個別プロパティ） | —（コードから動的設定） | マルチスクリーン補正 |
| `OverlayColor` | ✅ `AppearanceSettings.OverlayColor` | ✅ | 暗転オーバーレイの色 |
| `SizeLabelColor` | ✅ `AppearanceSettings.TextColor` | ✅ | サイズラベル（1 行目）の文字色 |
| `SizeLabelBackgroundColor` | ✅ `AppearanceSettings.TextBackgroundColor` | ✅ | サイズラベル（1 行目）の背景色 |
| `SizeLabelFontSize` | ✅ `AppearanceSettings.FontSize` | ✅ | サイズラベルのフォントサイズ |
| `ShowSizeLabel` | ✅ `AppearanceSettings.ShowSizeLabel` | ✅ | サイズラベル表示 |
| `BorderColor` | ✅ `AppearanceSettings.BorderColor` | ✅ | `DragRectControl.BorderColor` と共通適用 |
| `BorderThickness` | ✅ `AppearanceSettings.BorderThickness` | ✅ | 同上 |
| `InfoLabelColor` | ✅ `AppearanceSettings.InfoLabelColor` | ✅ | |
| `InfoLabelBackgroundColor` | ✅ `AppearanceSettings.InfoLabelBackgroundColor` | ✅ | |
| `InfoLabelFontSize` | ✅ `AppearanceSettings.InfoLabelFontSize` | ✅ | |
| `InfoLabelVisible` | ✅ `AppearanceSettings.InfoLabelVisible` | ✅ | 情報ラベル表示 |
| `ShowSubLines` | ✅ `AppearanceSettings.ShowSubLines` | ✅ | サブライン表示 |
| `SubLineFontSize` | ✅ `AppearanceSettings.SubLineFontSize` | ✅ | |
| `SubLineColor` | ✅ `AppearanceSettings.SubLineColor` | ✅ | |
| `SubLineBackgroundColor` | ✅ `AppearanceSettings.SubLineBackgroundColor` | ✅ | |
| `Mode` | —（`Mode` で公開） | —（セッションで決定） | 描画モード（`FadeMaskDragRectMode` → `FadeMaskRenderMode`） |

- `FadeMaskAppearance` の外観項目は、`FadeMaskDragRectControl.AppearanceSettings`（`FadeMaskDragRectSettings`）経由で公開される。
- `Mode` は `FadeMaskDragRectControl.Mode`（`FadeMaskDragRectMode`）として公開され、`FadeMaskRenderMode` へ変換される。
- **例外**: `Scaling` / `DisplayScaleScaling` は `AppearanceSettings` の**対象外**で、`FadeMaskDragRectControl.Scaling` / `.DisplayScaleScaling`（個別プロパティ）として公開される。DPI スケーリング・マルチスクリーン補正のため設定画面では設定不可（セッションがコードから動的設定）。

### 3.3 LoupeControl の公開外観と FadeMaskDragRectControl での公開有無

ルーペの装飾の外観はコアの `LoupeAppearance` が単一所有し、`LoupeControl` は `Appearance` プロパティへ 1 つの値として公開します（個別プロパティは持ちません）。

| LoupeControl の公開プロパティ | FadeMaskDragRectControl での公開有無 | 設定画面で設定 | 備考 |
| --- | --- | --- | --- |
| `RequestedSize` | ✅ `AppearanceSettings.RequestedSize` | ✅ | ルーペの一辺サイズ |
| `ZoomLevel` | ✅ `AppearanceSettings.ZoomLevel` | ✅ | 拡大倍率 |
| `RenderScaling` | ✅ `LoupeRenderScaling`（個別プロパティ） | —（DPI から自動取得） | DIP → 物理ピクセル倍率 |
| `Appearance.CrossColor` | ✅ `AppearanceSettings.CrossColor` | ✅ | 十字線色 |
| `Appearance.BackgroundColor` | —（設定なし） | — | 下地（ベース）色。MewUI はホストがテーマ背景を設定し、Avalonia は透明（背後の画面を透かす） |
| `Appearance.CheckerSize` | ✅ `AppearanceSettings.CheckerSize` | ✅ | 市松模様タイルサイズ |
| `Appearance.CheckerColorA` | ✅ `AppearanceSettings.CheckerColorA` | ✅ | |
| `Appearance.CheckerColorB` | ✅ `AppearanceSettings.CheckerColorB` | ✅ | |
| `Appearance.FrameColor` | ✅ `AppearanceSettings.FrameColor` | ✅ | 枠色 |
| `Appearance.FrameWidth` | ✅ `AppearanceSettings.FrameWidth` | ✅ | 枠の太さ |
| `PositionOffset` | ✅ `AppearanceSettings.PositionOffset` | ✅ | カーソルからのオフセット |
| `Appearance.ShowCoordLabel` | ✅ `AppearanceSettings.LoupeShowCoordLabel` | ✅ | 座標ラベル表示 |
| `Appearance.CoordLabelColor` | ✅ `AppearanceSettings.LoupeCoordLabelColor` | ✅ | |
| `Appearance.CoordLabelBackgroundColor` | ✅ `AppearanceSettings.LoupeCoordLabelBackgroundColor` | ✅ | |
| `Appearance.CoordLabelFontSize` | ✅ `AppearanceSettings.LoupeCoordLabelFontSize` | ✅ | |
| `Appearance.CoordLabelCornerRadius` | —（公開しない） | —（設定なし） | ラベル背景の角丸半径（コアの既定値 2 を使用） |
| —（破線色） | ✅ `AppearanceSettings.SelectionRectColorA / B` | ✅ | LoupeControl のプロパティではなく、ルーペ描画時（`LoupeRendered`）に破線オーバーレイを描くための色 |

- ルーペの外観も `FadeMaskDragRectControl.AppearanceSettings`（`FadeMaskDragRectSettings`）経由で公開され、コアの `LoupeAppearance` へ組み立てて一括設定される。
- `SelectionRectColorA / B` は `LoupeControl` には公開されず、`FadeMaskDragRectControl` がルーペ描画完了時に破線オーバーレイを描画する際に用いる（`_currentAppearance` から参照）。
- **例外**: `RenderScaling` は `AppearanceSettings` の**対象外**で、`FadeMaskDragRectControl.LoupeRenderScaling`（個別プロパティ）として公開される。DPI スケーリング（`MainWindow.DesktopScaling`）から自動取得するため設定画面では設定不可。

### 3.4 FadeMaskDragRectControl の公開外観プロパティと CropViewControl での公開有無

`FadeMaskDragRectControl` は下位コントロール（DragRect / FadeMask / Loupe）の外観を「`AppearanceSettings`（`FadeMaskDragRectSettings`）」として一括公開する。
`CropViewControl` では、これを**そのまま `AppearanceSettings` として委譲**し、DPI スケーリング（`Scaling` / `DisplayScaleScaling` / `LoupeRenderScaling`）は**個別プロパティとして委譲**する。

| FadeMaskDragRectControl の公開プロパティ | CropViewControl での公開有無 | 設定画面で設定 | 備考 |
| --- | --- | --- | --- |
| `AppearanceSettings`（`FadeMaskDragRectSettings`） | ✅ `AppearanceSettings`（そのまま委譲） | ✅ | 下位 3 コントロールの外観を一括公開（全て設定画面で設定可能） |
| `Scaling` | ✅ `Scaling`（個別プロパティ） | —（コードから動的設定） | DPI スケーリング |
| `DisplayScaleScaling` | ✅ `DisplayScaleScaling`（個別プロパティ） | —（コードから動的設定） | マルチスクリーン補正 |
| `LoupeRenderScaling` | ✅ `LoupeRenderScaling`（個別プロパティ） | —（DPI から自動取得） | ルーペ描画スケール |
| `Mode`（`FadeMaskDragRectMode`） | ✅ `SelectionMode`（`FadeMaskDragRectMode` をそのまま委譲） | — | |
| `WindowTitle` | — | — | キャプチャセッション（OverlayWindow）専用 |
| `ShowFullOverlay` | — | — | 描画状態（読み取り専用） |

- `CropViewControl` は `FadeMaskDragRectControl` の `AppearanceSettings` と DPI スケーリング個別プロパティをそのまま公開する（型を透過）。
- `Scaling` / `DisplayScaleScaling` / `LoupeRenderScaling` は `AppearanceSettings` には**含まれず**、個別プロパティで公開される（**コードから動的設定**のため設定画面では設定不可）。

### 3.5 CropViewControl の公開外観プロパティと CaptureSessionControl.AvaloniaUI での公開有無

`CropViewControl` は自身の公開外観プロパティとして `AppearanceSettings`（`FadeMaskDragRectSettings`）、`Scaling` / `DisplayScaleScaling` / `LoupeRenderScaling`、`ViewportBackground`、`CenterAlign` 等を公開する。
`CaptureSessionControl.AvaloniaUI` は**`CaptureSettings`（`CaptureSessionControl.AppearanceSettings`）**を公開しており、Crop セッション開始時に `CaptureSettings` → `FadeMaskDragRectSettings`（`ToFadeMaskDragRectSettings`）に変換して `CropViewControl.AppearanceSettings` へ設定する。
DPI スケーリング（`Scaling` / `DisplayScaleScaling` / `LoupeRenderScaling`）は `CaptureSettings` に含まれず、セッションが個別プロパティ（`cropView.Scaling` 等）で設定する。

| CropViewControl の公開プロパティ | CaptureSessionControl.AvaloniaUI での公開有無 | 設定画面で設定 | 備考 |
| --- | --- | --- | --- |
| `AppearanceSettings`（`FadeMaskDragRectSettings`） | ✅ `CaptureSettings`（`FadeMaskDragRectSettingsExtensions` で変換） | ✅ | `FadeMaskDragRectSettings` ↔ `CaptureSettings`（全て設定画面で設定可能） |
| `Scaling` | ✅ `cropView.Scaling`（セッションが設定） | —（コードから動的設定） | DPI スケーリング（Crop は 1.0 固定） |
| `DisplayScaleScaling` | ✅ `cropView.DisplayScaleScaling`（セッションが設定） | —（コードから動的設定） | マルチスクリーン補正 |
| `LoupeRenderScaling` | ✅ `cropView.LoupeRenderScaling`（セッションが設定） | —（DPI から自動取得） | ルーペ描画スケール（`CaptureSettings.LoupeRenderScaling` 由来） |
| `ViewportBackground` | — | ✅（`Settings.PreviewCheckerColorA/B` / `PreviewCheckerSize`） | プレビュー背景（市松模様）。`app.AvaloniaUI` 側（`CapturePage`）で直接設定 |
| `CenterAlign` | — | ✅（`Settings.CenterAlign`） | `app.AvaloniaUI` 側（`CapturePage`）で直接設定 |
| `SelectionMode`（`FadeMaskDragRectMode`） | — | — | 選択モード。`CaptureSettings` に含まれず、セッション開始時に `CropSession` が決定 |
| `ZoomView`（`ZoomViewControl`） | — | — | ズーム・パン操作。セッションが内部利用 |

- `app.AvaloniaUI` の `Settings` → `CaptureSettingsMapper.From` → `CaptureSessionControl.AppearanceSettings`（`CaptureSettings`）が設定される。
- Capture セッション開始時に、`CaptureSettings` → `ToFadeMaskDragRectSettings` → `CropViewControl.AppearanceSettings`（`FadeMaskDragRectSettings`）と**変換**して設定する（`CaptureSessionControl.AppearanceSettings` はあくまで `CaptureSettings` 型で、`CropViewControl.AppearanceSettings` は `FadeMaskDragRectSettings` 型。型が異なるため変換が必要）。
- `Scaling` / `DisplayScaleScaling` / `LoupeRenderScaling` は `CaptureSettings` に含まれず、セッション（`CropSession`）が個別プロパティで設定する（コードから動的設定）。
- `ViewportBackground` / `CenterAlign` / `SelectionMode` は `CaptureSettings` に含まれず、`app.AvaloniaUI`（`CapturePage`）が直接制御する。

## 4. 外観設定

| 設定項目 | Settings プロパティ | 反映先コントロール / プロパティ |
| --- | --- | --- |
| テーマ（ライト / ダーク / システム） | `ThemeMode` | `App.RequestedThemeVariant`（アプリ全体） |

- `ThemeMode.System` は OS の設定に追従（`ThemeVariant.Default`）。

### 外観設定（詳細・折り畳みなし）

下記の項目は設定画面下部の「外観設定」セクションに配置される（キャプチャ設定 / ルーペ設定から移動）。

| 設定項目 | Settings プロパティ | 反映先コントロール / プロパティ |
| --- | --- | --- |
| 選択枠の色（不透明度含む） | `CaptureBorderColor` | `DragRectOptions.BorderColor` / `FadeMaskAppearance.BorderColor`（アルファ込み） |
| 選択枠の太さ | `CaptureBorderThickness` | `DragRectOptions.BorderThickness` + `FadeMaskAppearance.BorderThickness` |
| 選択矩形の最小サイズ | `MinSelectionSize` | `DragRectControl.MinSelectionSize` |
| 暗転オーバーレイの色（不透明度含む） | `CaptureOverlayColor` | `FadeMaskAppearance.OverlayColor`（アルファ込み） |
| ルーペの拡大率 | `LoupeZoomLevel` | `LoupeControl.Options.ZoomLevel` |
| ルーペのサイズ | `LoupeSize` | `LoupeControl.Options.RequestedSize` |
| ルーペのカーソルからのオフセット | `LoupeOffset` | `LoupeControl.PositionOffset` |

## 5. 保存設定

| 設定項目 | Settings プロパティ | 反映先コントロール / プロパティ |
| --- | --- | --- |
| 保存先フォルダ | `SaveFolderPath` | 保存処理（SaveImageAsync）/ `FolderViewPage` / SaveButton ToolTip |
| ファイル名テンプレート | `FileNameTemplate` | 保存処理（SaveImageAsync）・CapturePage のテキストボックス |

## 6. 画像設定

| 設定項目 | Settings プロパティ | 反映先コントロール / プロパティ |
| --- | --- | --- |
| スクロール不要時は中央に配置 | `CenterAlign` | `CropView.CenterAlign`（画像配置） |
| 市松模様 A（色） | `PreviewCheckerColorA` | `CropView.ViewportBackground`（プレビュー）+ `LoupeControl.Options.Appearance.CheckerColorA`（ルーペ） |
| 市松模様 B（色） | `PreviewCheckerColorB` | `CropView.ViewportBackground`（プレビュー）+ `LoupeControl.Options.Appearance.CheckerColorB`（ルーペ） |
| 市松模様のタイルサイズ | `PreviewCheckerSize` | `CropView.ViewportBackground`（プレビュー）+ `LoupeControl.Options.Appearance.CheckerSize`（ルーペ） |

- 市松模様は「画像設定」の詳細設定で管理し、**プレビュー背景とルーペ背景で同じ設定を共有**する。

## 7. キャプチャ設定

> 色は不透明度を統合した `#AARRGGBB` 形式で保存し、設定反映時に `Color.Parse` してそのまま渡す（コントロール側も色のアルファに統合済み）。

| 設定項目 | Settings プロパティ | 反映先コントロール / プロパティ |
| --- | --- | --- |
| キャプチャ時にメインウィンドウを隠す | `HideMainWindowOnCapture` | `CaptureSettings.HideOwnerWindow` |
| 非表示後の DWM 反映待機時間 | `HideBeforeCaptureDelayMs` | `CaptureSettings.HideBeforeCaptureDelay`（ミリ秒 → TimeSpan） |
| キャプチャ枠の色（不透明度含む） | `CaptureBorderColor` | `DragRectOptions.BorderColor` / `FadeMaskAppearance.BorderColor`（アルファ込み） |
| キャプチャ枠の太さ | `CaptureBorderThickness` | `DragRectOptions.BorderThickness` + `FadeMaskAppearance.BorderThickness` |
| 暗転オーバーレイの色（不透明度含む） | `CaptureOverlayColor` | `FadeMaskAppearance.OverlayColor`（アルファ込み） |

- 選択矩形の最小サイズ（`MinSelectionSize`）は「外観設定」（§4）に配置。

## 8. ドラッグハンドル設定

| 設定項目 | Settings プロパティ | 反映先コントロール / プロパティ |
| --- | --- | --- |
| 画像外の選択を禁止 | `ClampToDomainBounds` | `DragRectControl.ClampToDomainBounds` |
| カーソル位置の座標を表示 | `ShowCoordLabel` | `DragRectControl.ShowCoordLabel`（外観設定のラベル設定カテゴリに配置） |
| ハンドルの色 | `HandleColor` | `DragRectControl.HandleColor` |
| ハンドルのサイズ | `HandleSize` | `DragRectControl.HandleSize` |
| ハンドル判定半径 | `HandleHitRadius` | `DragRectControl.HandleHitRadius` |
| マーカーの色 | `MarkerColor` | `DragRectControl.MarkerColor` |
| マーカーの長さ | `MarkerLength` | `DragRectControl.MarkerLength` |
| 座標ラベルのテキスト色 | `CoordLabelColor` | `DragRectControl.CoordLabelColor` |
| 座標ラベルの背景色（不透明度含む） | `CoordLabelBackgroundColor` | `DragRectControl.CoordLabelBackgroundColor`（アルファ込み） |
| 座標ラベルのフォントサイズ | `CoordLabelFontSize` | `DragRectControl.CoordLabelFontSize` |

- 座標ラベルの外観（色・背景・フォント）は外観設定（詳細・折り畳みなし）内に配置（不透明度は色のアルファに統合済み）。

## 9. ルーペ設定

| 設定項目 | Settings プロパティ | 反映先コントロール / プロパティ |
| --- | --- | --- |
| ルーペの中心座標を表示 | `LoupeShowCoordLabel` | `LoupeControl.Options.Appearance.ShowCoordLabel`（外観設定のラベル設定カテゴリに配置） |
| 枠色 | `LoupeFrameColor` | `LoupeControl.Options.Appearance.FrameColor` |
| 枠の太さ | `LoupeFrameWidth` | `LoupeControl.Options.Appearance.FrameWidth` |
| 十字線色（不透明度含む） | `LoupeCrossColor` | `LoupeControl.Options.Appearance.CrossColor`（アルファ込みの色） |
| 選択矩形破線の色A（不透明度含む） | `LoupeSelectionRectColorA` | `FadeMaskDragRectSettings.SelectionRectColorA`（アルファ込み。ルーペの Bitmap へ破線を描く `FadeMaskDragRectControl` が使用） |
| 選択矩形破線の色B（不透明度含む） | `LoupeSelectionRectColorB` | `FadeMaskDragRectSettings.SelectionRectColorB`（アルファ込み。同上） |
| 座標ラベルのテキスト色 | `LoupeCoordLabelColor` | `LoupeControl.Options.Appearance.CoordLabelColor` |
| 座標ラベルの背景色（不透明度含む） | `LoupeCoordLabelBackgroundColor` | `LoupeControl.Options.Appearance.CoordLabelBackgroundColor`（アルファ込みの色） |
| 座標ラベルのフォントサイズ | `LoupeCoordLabelFontSize` | `LoupeControl.Options.Appearance.CoordLabelFontSize` |

- 拡大率（`LoupeZoomLevel`）・サイズ（`LoupeSize`）・カーソルからのオフセット（`LoupeOffset`）は「外観設定」（§4）に配置。
- 市松模様は「画像設定」（§6）で管理するため、ルーペ設定には含めない。
- 座標ラベルの外観は詳細設定（Expander）内に配置。

### 描画スケール（自動設定・設定項目なし）

ルーペの描画スケール（`LoupeControl.RenderScaling`・DIP→物理ピクセル倍率）は、**ユーザー設定ではなくウィンドウの DPI スケーリングから自動取得**して反映する。

- 取得元: `MainWindow.DesktopScaling`（100% = 1.0 / 125% = 1.25 / 150% = 1.5 / 200% = 2.0）
- 反映: `CaptureSettings.LoupeRenderScaling` に `MainWindow.DesktopScaling` を渡す
- DPI 変化への追従: `MainWindow.ScalingChanged` イベントで再反映（別モニタへの移動・ディスプレイ設定変更時）
- 設定画面のルーペ設定には表示しない（ユーザー設定不要のため）

## 10. ラベル設定

> 表示の ON/OFF チェックボックス（`SizeLabelVisible` / `ShowSubLines` / `InfoLabelVisible` / `ShowCoordLabel` / `LoupeShowCoordLabel`）は設定画面下部の「外観設定」の先頭カテゴリ（ラベル設定）に配置。テキスト色・背景色・フォントサイズは外観設定内の対応カテゴリ（選択矩形内のラベル / カーソル位置の座標ラベル / ルーペの中心座標ラベル）に配置。

### 10.1 1 行目: サイズラベル

| 設定項目 | Settings プロパティ | 反映先コントロール / プロパティ |
| --- | --- | --- |
| 選択矩形のサイズを表示 | `SizeLabelVisible` | `FadeMaskAppearance.ShowSizeLabel`（詳細設定の外に配置） |
| テキスト色 | `CaptureTextColor` | `FadeMaskAppearance.SizeLabelColor`（キャプチャのテキスト色と共用） |
| 背景色（不透明度含む） | `SizeLabelBackgroundColor` | `FadeMaskAppearance.SizeLabelBackgroundColor`（アルファ込み） |
| フォントサイズ | `SizeLabelFontSize` | `FadeMaskAppearance.SizeLabelFontSize` |

### 10.2 2 行目: 操作ラベル

| 設定項目 | Settings プロパティ | 反映先コントロール / プロパティ |
| --- | --- | --- |
| 矩形操作を矩形内に表示 | `ShowSubLines` | `FadeMaskAppearance.ShowSubLines`（詳細設定の外に配置） |
| テキスト色 | `SubLineColor` | `FadeMaskAppearance.SubLineColor` |
| 背景色（不透明度含む） | `SubLineBackgroundColor` | `FadeMaskAppearance.SubLineBackgroundColor`（アルファ込み） |
| フォントサイズ | `SubLineFontSize` | `FadeMaskAppearance.SubLineFontSize` |

### 10.3 3 行目: 情報ラベル

| 設定項目 | Settings プロパティ | 反映先コントロール / プロパティ |
| --- | --- | --- |
| 選択中のウィンドウのタイトルを表示 | `InfoLabelVisible` | `FadeMaskAppearance.InfoLabelVisible`（詳細設定の外に配置） |
| テキスト色 | `InfoLabelColor` | `FadeMaskAppearance.InfoLabelColor` |
| 背景色（不透明度含む） | `InfoLabelBackgroundColor` | `FadeMaskAppearance.InfoLabelBackgroundColor`（アルファ込み） |
| フォントサイズ | `InfoLabelFontSize` | `FadeMaskAppearance.InfoLabelFontSize` |

## 11. ホットキー設定

| 設定項目 | Settings プロパティ | 反映先コントロール / プロパティ |
| --- | --- | --- |
| 全画面キャプチャ | `SelectScreenHotKey` | `WinGlobalHotKeyService`（WM_HOTKEY 登録） |
| ウィンドウ選択キャプチャ | `WindowSelectHotKey` | `WinGlobalHotKeyService`（WM_HOTKEY 登録） |
| 範囲選択キャプチャ | `AreaSelectHotKey` | `WinGlobalHotKeyService`（WM_HOTKEY 登録） |

## 12. 設定外・自動保存（フォルダビュー / Undo 一覧 / ウィンドウ状態）

設定画面には表示しないが、`Settings` に保存され起動時に復元される項目。

| 項目 | Settings プロパティ | 反映先 |
| --- | --- | --- |
| フォルダビューの表示モード（特大 / 大 / 中） | `FolderViewModeIndex` | `FolderViewPage`（ToggleButton 選択状態） |
| フォルダビューのソート順 | `FolderViewSortAscending` | `FolderViewPage`（ソート処理・ToggleButton） |
| Undo 一覧の表示モード（特大 / 大 / 中） | `UndoViewModeIndex` | `UndoPage`（ToggleButton 選択状態） |
| ナビゲーションパネルの展開状態 | `IsNavigationExpanded` | `MainWindow`（ナビパネル幅 180 / 40） |
| ルーペの表示モード | `LoupeModeValue` | `CaptureSettings.LoupeVisible`（0=非表示, 1=表示, 2=自動） |
| ウィンドウの位置・サイズ | `MainWindowBounds` | `MainWindow`（起動時復元・終了時保存） |
| ウィンドウの状態 | `MainWindowState` | `MainWindow`（起動時復元・終了時保存） |

- Undo 一覧のアイコンサイズはフォルダビューと同じ値（`FolderExtraLargeIconSize` / `FolderLargeIconSize` / `FolderMediumIconSize`）を使用する

## 13. 外観設定のデフォルト値への復元

設定画面「外観設定」の右上にある **「デフォルト値に戻す」** ボタンで、外観設定（選択枠・暗転・ハンドル・マーカー・ルーペ・市松模様・選択矩形内ラベル・座標ラベル）をデフォルト値へ一括復元する。

- 実装: `Settings.ResetAppearanceToDefaults()`（新規インスタンスのフィールド初期化子 = デフォルト値をコピー）
- デフォルト値は各コントロール（`DragRectControl` / `LoupeControl` / `FadeMaskAppearance`）の既定値と一致させる
- 変更は `PropertyChanged` 購読により自動保存される
- 外観設定以外（テーマ・保存設定・ホットキー等）は変更されない
