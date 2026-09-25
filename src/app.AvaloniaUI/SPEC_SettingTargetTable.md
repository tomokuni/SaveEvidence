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
  ├─ MarqueeControl
  ├─ LoupeControl
  └─ Platform.AvaloniaUI（Avalonia アダプタ）／Platform.Native（サービス実装）／Platform.Core（抽象・値型）
```

- **`CaptureSessionControl.AvaloniaUI` は `CropViewControl` を内包しません**。`StartCropAsync(CropViewControl, ...)` の引数として外部から受け取るだけです。
- **切り出しの対象画像は `CropViewControl.Source`（app が設定するプレビュー画像）から解決**します（画像を引数で渡さない。確定時の範囲は現在の画像へクランプするため、実行中の差し替えにも追従する）。
- **`CropViewControl` のインスタンスは `app.AvaloniaUI` が所有**します（`CapturePage.axaml` の `crop:CropViewControl x:Name="CropView"`）。プレビュー表示（`Source` / `CenterAlign` / `ViewportBackground`）は app の責務です。
- `FadeMaskDragRectControl` は**2 つの場所**で再利用されます（資源分離・再利用）：
  1. `OverlayWindow.SelectionOverlay`：画面キャプチャ（領域選択）用
  2. `CropViewControl._selectionOverlay`：プレビュー Crop（切出し）用
- 両者は独立したインスタンスで、異なる座標系・モードで動作します（画面キャプチャ = 仮想スクリーンピクセル / Crop = 元画像ピクセル）。

### 2.2 各コントロールが公開する外観プロパティ

外観設定は、階層の各コントロールが**公開 API（`Options` / `AppearanceSettings` / 個別プロパティ）**として提供する。
下位コントロールの設定は、`FadeMaskDragRectControl`（→ `FadeMaskDragRectOptions`）→ `CropViewControl` → `CaptureSessionControl.AvaloniaUI`（→ `CaptureSettings`）→ `app.AvaloniaUI` の順に委譲して公開される。

| コントロール | 公開する外観プロパティ | 備考 |
| --- | --- | --- |
| `app.AvaloniaUI` | — | `CaptureSessionControl.AvaloniaUI` の `AppearanceSettings`（`CaptureSettings`）へ `CaptureSettingsMapper` で変換して渡す |
| `CaptureSessionControl.AvaloniaUI` | `AppearanceSettings`（`CaptureSettings`） | 部分更新（null は現在値維持）。実行中の Crop セッションへ即時反映 |
| `CropViewControl` | `SelectionOverlayOptions`（`FadeMaskDragRectOptions`）/ `Scaling` / `LoupeRenderScaling` / `ViewportBackground` / `CenterAlign` | 設定は `FadeMaskDragRectControl` へ委譲。DPI スケーリングは個別プロパティ（コードから動的設定） |
| `ZoomViewControl` | `Background` / `CenterAlign` / `ZoomPercent` / `IsFit` | 画像ビューポートの外観 |
| `FadeMaskDragRectControl` | `Options`（`FadeMaskDragRectOptions`）/ `Scaling` / `DisplayScaleScaling` / `LoupeRenderScaling` / `Mode` / `ShowFullOverlay` / `SizeLabelOverrideProvider` / `OperationTextProvider` / `InfoTextProvider` | 下位（Marquee（選択の設定） / 装飾（`Deco`） / Loupe）の設定を一括公開（コアの設定値 1 つ）+ DPI スケーリングとラベル文言プロバイダを個別公開 |
| `MarqueeControl` | `Options`（`MarqueeOptions`。ハンドル・マーカー・サイズラベル・座標ラベル・対話パラメータ）/ `DecoOptions`（装飾層の `MarqueeDecoOptions`。暗転マスク・穴あけ枠）/ `Scaling` / `DisplayScaleScaling` / `SizeLabelOverrideProvider` | `FadeMaskDragRectOptions.Marquee`（選択の設定）と `.Deco`（装飾の設定）からそれぞれ設定（枠線は `Deco.OuterAppearance` が単一所有する）。**操作・情報ラベルは本コントロールの概念ではない**ため、ホスト（`FadeMaskDragRectControl`）が `Options.Labels` で描く |
| `LoupeControl` | `Options`（`LoupeOptions`。表示サイズ・拡大率・外観）/ `RenderScaling`（DPI 自動取得）/ `PositionOffset` | `FadeMaskDragRectOptions.Loupe` から設定（`RenderScaling` 以外） |
| `Platform.AvaloniaUI` | —（共通基盤） | カーソル操作（`ICursorService`）・座標変換ポート（`ICoordTransform`。`Platform.Core` 提供）を提供 |

### 2.3 反映経路

```text
SettingsPage.axaml（ColorPicker / CheckBox / NumericUpDown / ComboBox）
  → Settings（ObservableProperty・変更通知）
  → MainWindow.ApplyCaptureSessionSettings（CaptureSettingsMapper.From で CaptureSettings 生成）
  → CaptureSessionControl.AppearanceSettings（CaptureSettings・部分更新マージ）
  → CropViewControl.SelectionOverlayOptions（ToFadeMaskDragRectOptions で変換した FadeMaskDragRectOptions）
  → FadeMaskDragRectControl.Options（設定値の分配）
  → Options.Marquee / Options.Deco.OuterAppearance / Options.Loupe → 内包コントロールの各設定値
```

- 設定画面の変更は `Settings` の `PropertyChanged` で **即時反映** される。
- CaptureSessionControl.AvaloniaUI への反映は `CaptureSettings`（nullable record・部分更新）を介して行い、`null` のメンバーは現在値を維持する。
- テーマ・画像配置・市松模様等、app 内で直接処理する項目は個別の購読で反映される。
- `Platform.AvaloniaUI` は共通基盤のため、外観設定の公開対象ではない（参考として記載）。

## 3. 下位コントロールが公開する外観プロパティと上位での公開有無

本節は、**各コントロールが公開する外観プロパティが、直接上位コントロールで公開されているか**を一覧化する。
下位コントロールの設定は、`FadeMaskDragRectControl`（→ `FadeMaskDragRectOptions`）→ `CropViewControl` → `CaptureSessionControl.AvaloniaUI`（→ `CaptureSettings`）→ `app.AvaloniaUI` の順に委譲して公開される。
各上位コントロールで「直接公開（個別プロパティ）」または「`Options` 経由で公開」かを示す。

### 3.1 MarqueeControl の公開外観プロパティと FadeMaskDragRectControl での公開有無

| MarqueeControl の公開プロパティ | FadeMaskDragRectControl での公開有無 | 設定画面で設定 | 備考 |
| --- | --- | --- | --- |
| `HandleBorderColor`（ハンドルの枠線色） | ✅ `Options.Marquee.HandleBorderColor` / `Options.Deco.OuterAppearance.BorderColor`（同じ値） | ✅ | `MarqueeDecoOptions.OuterAppearance`（装飾層）が枠線の色を単一所有し、ホストが同じ値をハンドルの枠線へも割り当てる（選択枠線と暗転マスクの穴あけ枠は同じ矩形に重なる） |
| （枠線の太さ） | ✅ `Options.Deco.OuterAppearance.BorderThickness` | ✅ | 装飾層が単一所有する。ハンドルの枠線太さは `MarqueeAppearance.HandlePenThickness`（コアの定数） |
| `HandleColor` | ✅ `Options.Marquee.HandleColor` | ✅ | |
| `HandleSize` | ✅ `Options.Marquee.HandleSize` | ✅ | |
| `HandleHitRadius` | ✅ `Options.Marquee.HandleHitRadius` | ✅ | |
| `MinSelectionSize` | ✅ `Options.Marquee.MinSelectionSize` | ✅ | |
| `ClampToDomainBounds` | ✅ `Options.Marquee.ClampToDomainBounds` | ✅ | |
| `MarkerColor` | ✅ `Options.Marquee.MarkerColor` | ✅ | |
| `MarkerLength` | ✅ `Options.Marquee.MarkerLength` | ✅ | |
| `ShowCoordLabel` | ✅ `Options.Marquee.ShowCoordLabel` | ✅ | カーソル座標ラベルの表示有無 |
| `CoordLabelColor` | ✅ `Options.Marquee.CoordLabelColor` | ✅ | |
| `CoordLabelBackgroundColor` | ✅ `Options.Marquee.CoordLabelBackgroundColor` | ✅ | |
| `CoordLabelFontSize` | ✅ `Options.Marquee.CoordLabelFontSize` | ✅ | |

- `FadeMaskDragRectControl` は MarqueeControl の公開設定を**全て** `Options.Marquee`（`MarqueeOptions`）経由で公開する（枠線は `Options.Deco.OuterAppearance`（装飾層）が単一所有する）。
- MarqueeControl の外観プロパティは**全て**設定画面（`SettingsPage.axaml` の外観設定）で設定可能（✅）。

### 3.2 装飾・ラベル（`MarqueeDecoOptions` の `OuterDecoAppearance` と `MarqueeOptions` / `FadeMaskDragRectLabelOptions`）の外観項目と FadeMaskDragRectControl での公開有無

| 外観項目（所有する型） | FadeMaskDragRectControl での公開有無 | 設定画面で設定 | 備考 |
| --- | --- | --- | --- |
| `Scaling` | ✅ `Scaling`（個別プロパティ） | —（コードから動的設定） | DPI スケーリング |
| `DisplayScaleScaling` | ✅ `DisplayScaleScaling`（個別プロパティ） | —（コードから動的設定） | マルチスクリーン補正 |
| `OverlayColor`（`OuterDecoAppearance`） | ✅ `Options.Deco.OuterAppearance.OverlayColor` | ✅ | 暗転オーバーレイの色 |
| `SizeLabelColor`（`MarqueeOptions`） | ✅ `Options.Marquee.SizeLabelColor` | ✅ | サイズラベル（1 行目）の文字色 |
| `SizeLabelBackgroundColor`（`MarqueeOptions`） | ✅ `Options.Marquee.SizeLabelBackgroundColor` | ✅ | サイズラベル（1 行目）の背景色 |
| `SizeLabelFontSize`（`MarqueeOptions`） | ✅ `Options.Marquee.SizeLabelFontSize` | ✅ | サイズラベルのフォントサイズ |
| `ShowSizeLabel`（`MarqueeOptions`） | ✅ `Options.Marquee.ShowSizeLabel` | ✅ | サイズラベル表示 |
| `BorderColor`（`OuterDecoAppearance`） | ✅ `Options.Deco.OuterAppearance.BorderColor` | ✅ | `Options.Marquee.HandleBorderColor` と共通適用（同じ矩形に重なるため） |
| `BorderThickness`（`OuterDecoAppearance`） | ✅ `Options.Deco.OuterAppearance.BorderThickness` | ✅ | 同上 |
| `InfoColor` / `InfoBackgroundColor` / `InfoFontSize` / `ShowInfoLabel`（`FadeMaskDragRectLabelOptions`） | ✅ `Options.Labels.*` | ✅ | 情報ラベル（3 行目）。**文言の生成と描画はホスト**が行う |
| `OperationColor` / `OperationFontSize` / `OperationBackgroundColor` / `ShowOperationLabel`（`FadeMaskDragRectLabelOptions`） | ✅ `Options.Labels.*` | ✅ | 操作ラベル（2 行目）。**文言の生成と描画はホスト**が行う |
| `Mode` | —（`Mode` で公開） | —（セッションで決定） | 子コントロールの有効・無効と全面暗転（`FadeMaskDragRectMode`） |

- サイズラベルは選択矩形コア（`MarqueeOptions`）が、装飾（暗転・枠線）は `Options.Deco.OuterAppearance` が、操作・情報ラベルは `Options.Labels`（`FadeMaskDragRectLabelOptions`）が単一所有する。
- `Mode` は `FadeMaskDragRectControl.Mode`（`FadeMaskDragRectMode`）として公開される（モードが決めるのは子コントロールの有効・無効と全面暗転のみ。文言はホストがプロバイダで与える）。
- **例外**: `Scaling` / `DisplayScaleScaling` は設定（`Options`）の**対象外**の環境値で、`FadeMaskDragRectControl.Scaling` / `.DisplayScaleScaling`（個別プロパティ）として公開される。DPI スケーリング・マルチスクリーン補正のため設定画面では設定不可（セッションがコードから動的設定）。

### 3.3 LoupeControl の公開外観と FadeMaskDragRectControl での公開有無

ルーペの装飾の外観はコアの `LoupeAppearance` が単一所有し、`LoupeControl` は `Appearance` プロパティへ 1 つの値として公開します（個別プロパティは持ちません）。

| LoupeControl の公開プロパティ | FadeMaskDragRectControl での公開有無 | 設定画面で設定 | 備考 |
| --- | --- | --- | --- |
| `RequestedSize` | ✅ `Options.Loupe.RequestedSize` | ✅ | ルーペの一辺サイズ |
| `ZoomLevel` | ✅ `Options.Loupe.ZoomLevel` | ✅ | 拡大倍率 |
| `RenderScaling` | ✅ `LoupeRenderScaling`（個別プロパティ） | —（DPI から自動取得） | DIP → 物理ピクセル倍率 |
| `Appearance.CrossColor` | ✅ `Options.Loupe.Appearance.CrossColor` | ✅ | 十字線色 |
| `Appearance.BackgroundColor` | —（設定なし） | — | 下地（ベース）色。MewUI はホストがテーマ背景を設定し、Avalonia は透明（背後の画面を透かす） |
| `Appearance.CheckerSize` | ✅ `Options.Loupe.Appearance.CheckerSize` | ✅ | 市松模様タイルサイズ |
| `Appearance.CheckerColorA` | ✅ `Options.Loupe.Appearance.CheckerColorA` | ✅ | |
| `Appearance.CheckerColorB` | ✅ `Options.Loupe.Appearance.CheckerColorB` | ✅ | |
| `Appearance.FrameColor` | ✅ `Options.Loupe.Appearance.FrameColor` | ✅ | 枠色 |
| `Appearance.FrameWidth` | ✅ `Options.Loupe.Appearance.FrameWidth` | ✅ | 枠の太さ |
| `PositionOffset` | ✅ `Options.LoupeOffset` | ✅ | カーソルからのオフセット（複合コントロール固有の設定） |
| `Appearance.ShowCoordLabel` | ✅ `Options.Loupe.Appearance.ShowCoordLabel` | ✅ | 座標ラベル表示 |
| `Appearance.CoordLabelColor` | ✅ `Options.Loupe.Appearance.CoordLabelColor` | ✅ | |
| `Appearance.CoordLabelBackgroundColor` | ✅ `Options.Loupe.Appearance.CoordLabelBackgroundColor` | ✅ | |
| `Appearance.CoordLabelFontSize` | ✅ `Options.Loupe.Appearance.CoordLabelFontSize` | ✅ | |
| `Appearance.CoordLabelCornerRadius` | —（公開しない） | —（設定なし） | ラベル背景の角丸半径（コアの既定値 2 を使用） |
| —（破線色） | ✅ `Options.SelectionRectColorA / B` | ✅ | LoupeControl のプロパティではなく、ルーペ描画時（`LoupeRendered`）に破線オーバーレイを描くための色 |

- ルーペの設定も `FadeMaskDragRectControl.Options`（`FadeMaskDragRectOptions`）経由で公開され、コアの `LoupeOptions` / `LoupeAppearance` へ組み立てて一括設定される。
- `SelectionRectColorA / B` は `LoupeControl` には公開されず、`FadeMaskDragRectControl` がルーペ描画完了時に破線オーバーレイを描画する際に用いる（`Options` から参照）。
- **例外**: `RenderScaling` は設定（`Options`）の**対象外**の環境値で、`FadeMaskDragRectControl.LoupeRenderScaling`（個別プロパティ）として公開される。DPI スケーリング（`MainWindow.DesktopScaling`）から自動取得するため設定画面では設定不可。

### 3.4 FadeMaskDragRectControl の公開外観プロパティと CropViewControl での公開有無

`FadeMaskDragRectControl` は下位コントロール（Marquee / Loupe）の設定を「`Options`（`FadeMaskDragRectOptions`）」として一括公開する。
`CropViewControl` では、これを**そのまま `SelectionOverlayOptions` として委譲**し、DPI スケーリング（`Scaling` / `LoupeRenderScaling`）は**個別プロパティとして委譲**する。

| FadeMaskDragRectControl の公開プロパティ | CropViewControl での公開有無 | 設定画面で設定 | 備考 |
| --- | --- | --- | --- |
| `Options`（`FadeMaskDragRectOptions`） | ✅ `SelectionOverlayOptions`（そのまま委譲） | ✅ | 下位 2 コントロールの設定を一括公開（全て設定画面で設定可能） |
| `Scaling` | ✅ `Scaling`（個別プロパティ） | —（コードから動的設定） | DPI スケーリング |
| `DisplayScaleScaling` | —（公開しない） | — | マルチスクリーン補正。Crop では使用しない（Crop のサイズラベルは `Scaling` と同値で動作する） |
| `LoupeRenderScaling` | ✅ `LoupeRenderScaling`（個別プロパティ） | —（DPI から自動取得） | ルーペ描画スケール |
| `Mode`（`FadeMaskDragRectMode`） | ✅ `SelectionMode`（`FadeMaskDragRectMode` をそのまま委譲） | — | |
| `SizeLabelOverrideProvider` / `OperationTextProvider` / `InfoTextProvider` | ✅ `SizeLabelOverrideProvider` / `OperationTextProvider` / `InfoTextProvider`（そのまま委譲） | — | ラベル文言プロバイダ（キャプチャセッションが `CaptureLabelTexts` から与える） |
| `ShowFullOverlay` | —（公開しない） | — | モードから導出される描画状態。`FadeMaskDragRectControl` の公開面であり、`CropViewControl` は透過しない |

- `CropViewControl` は `FadeMaskDragRectControl` の `Options` と DPI スケーリング個別プロパティをそのまま公開する（型を透過）。
- `Scaling` / `LoupeRenderScaling` は `Options` には**含まれず**、個別プロパティで公開される（**コードから動的設定**のため設定画面では設定不可）。

### 3.5 CropViewControl の公開外観プロパティと CaptureSessionControl.AvaloniaUI での公開有無

`CropViewControl` は自身の公開設定として `SelectionOverlayOptions`（`FadeMaskDragRectOptions`）、`Scaling` / `LoupeRenderScaling`、`ViewportBackground`、`CenterAlign` 等を公開する。
`CaptureSessionControl.AvaloniaUI` は**`CaptureSettings`（`CaptureSessionControl.AppearanceSettings`）**を公開しており、Crop セッション開始時に `CaptureSettings` → `FadeMaskDragRectOptions`（`ToFadeMaskDragRectOptions`。コアのマッパー）に変換して `CropViewControl.SelectionOverlayOptions` へ設定する。
DPI スケーリング（`Scaling` / `LoupeRenderScaling`）は `CaptureSettings` に含まれず、セッションが個別プロパティ（`cropView.Scaling` 等）で設定する。

| CropViewControl の公開プロパティ | CaptureSessionControl.AvaloniaUI での公開有無 | 設定画面で設定 | 備考 |
| --- | --- | --- | --- |
| `SelectionOverlayOptions`（`FadeMaskDragRectOptions`） | ✅ `CaptureSettings`（コアのマッパーで変換） | ✅ | `CaptureSettings` → `FadeMaskDragRectOptions`（全て設定画面で設定可能） |
| `Scaling` | ✅ `cropView.Scaling`（セッションが設定） | —（コードから動的設定） | DPI スケーリング（Crop は 1.0 固定） |
| `LoupeRenderScaling` | ✅ `cropView.LoupeRenderScaling`（セッションが設定） | —（DPI から自動取得） | ルーペ描画スケール（`CaptureSettings.LoupeRenderScaling` 由来） |
| `ViewportBackground` | — | ✅（`Settings.PreviewCheckerColorA/B` / `PreviewCheckerSize`） | プレビュー背景（市松模様）。`app.AvaloniaUI` 側（`CapturePage`）で直接設定 |
| `CenterAlign` | — | ✅（`Settings.CenterAlign`） | `app.AvaloniaUI` 側（`CapturePage`）で直接設定 |
| `SelectionMode`（`FadeMaskDragRectMode`） | — | — | 選択モード。`CaptureSettings` に含まれず、`CropViewControl` の既定（`Crop`）を使う（`CropSession` は読み取りのみ）。 `fadeMaskDragRect` 以外の値（自動切り出しの矩形は `Rects` で描く）へ切り替えるホストは無い |

- `app.AvaloniaUI` の `Settings` → `CaptureSettingsMapper.From` → `CaptureSessionControl.AppearanceSettings`（`CaptureSettings`）が設定される。
- Capture セッション開始時に、`CaptureSettings` → `ToFadeMaskDragRectOptions` → `CropViewControl.SelectionOverlayOptions`（`FadeMaskDragRectOptions`）と**変換**して設定する（`CaptureSessionControl.AppearanceSettings` はあくまで `CaptureSettings` 型で、`CropViewControl.SelectionOverlayOptions` は `FadeMaskDragRectOptions` 型。型が異なるため変換が必要）。
- `Scaling` / `LoupeRenderScaling` は `CaptureSettings` に含まれず、セッション（`CropSession`）が個別プロパティで設定する（コードから動的設定）。
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
| 選択枠の色（不透明度含む） | `CaptureBorderColor` | `MarqueeOptions.HandleBorderColor` / `OuterDecoAppearance.BorderColor`（アルファ込み） |
| 選択枠の太さ | `CaptureBorderThickness` | `OuterDecoAppearance.BorderThickness`（装飾が単一所有する。ハンドルの枠線太さは `MarqueeAppearance.HandlePenThickness`） |
| 選択矩形の最小サイズ | `MinSelectionSize` | `MarqueeOptions.MinSelectionSize` |
| 暗転オーバーレイの色（不透明度含む） | `CaptureOverlayColor` | `OuterDecoAppearance.OverlayColor`（アルファ込み） |
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
| 市松模様のタイルサイズ | `PreviewCheckerSize` | `CropView.ViewportBackground`（プレビュー）+ `LoupeControl.Options.Appearance.CheckerSize`（ルーペ）。実効値は `ZoomViewAppearance.ResolveCheckerTileSize` が決める（0 以下 = 描かない / 1 は下限 2 へ切り上げ） |

- 市松模様は「画像設定」の詳細設定で管理し、**プレビュー背景とルーペ背景で同じ設定を共有**する。

## 7. キャプチャ設定

> 色は不透明度を統合した `#AARRGGBB` 形式で保存し、設定反映時に `Color.Parse` してそのまま渡す（コントロール側も色のアルファに統合済み）。

| 設定項目 | Settings プロパティ | 反映先コントロール / プロパティ |
| --- | --- | --- |
| キャプチャ時にメインウィンドウを隠す | `HideMainWindowOnCapture` | `CaptureSettings.HideOwnerWindow` |
| キャプチャ枠の色（不透明度含む） | `CaptureBorderColor` | `MarqueeOptions.HandleBorderColor` / `OuterDecoAppearance.BorderColor`（アルファ込み） |
| キャプチャ枠の太さ | `CaptureBorderThickness` | `OuterDecoAppearance.BorderThickness`（装飾が単一所有する。ハンドルの枠線太さは `MarqueeAppearance.HandlePenThickness`） |
| 暗転オーバーレイの色（不透明度含む） | `CaptureOverlayColor` | `OuterDecoAppearance.OverlayColor`（アルファ込み） |

- 選択矩形の最小サイズ（`MinSelectionSize`）は「外観設定」（§4）に配置。

## 8. ドラッグハンドル設定

| 設定項目 | Settings プロパティ | 反映先コントロール / プロパティ |
| --- | --- | --- |
| 画像外の選択を禁止 | `ClampToDomainBounds` | `MarqueeOptions.ClampToDomainBounds` |
| カーソル位置の座標を表示 | `ShowCoordLabel` | `MarqueeOptions.ShowCoordLabel`（外観設定のラベル設定カテゴリに配置） |
| ハンドルの色 | `HandleColor` | `MarqueeOptions.HandleColor` |
| ハンドルのサイズ | `HandleSize` | `MarqueeOptions.HandleSize` |
| ハンドル判定半径 | `HandleHitRadius` | `MarqueeOptions.HandleHitRadius` |
| マーカーの色 | `MarkerColor` | `MarqueeOptions.MarkerColor` |
| マーカーの長さ | `MarkerLength` | `MarqueeOptions.MarkerLength` |
| 座標ラベルのテキスト色 | `CoordLabelColor` | `MarqueeOptions.CoordLabelColor` |
| 座標ラベルの背景色（不透明度含む） | `CoordLabelBackgroundColor` | `MarqueeOptions.CoordLabelBackgroundColor`（アルファ込み） |
| 座標ラベルのフォントサイズ | `CoordLabelFontSize` | `MarqueeOptions.CoordLabelFontSize` |

- 設定は複合コントロールの設定値（`FadeMaskDragRectOptions.Marquee`）経由で子コントロールへ分配される（選択矩形コントロールは同じ項目の個別プロパティを公開しない）。

- 座標ラベルの外観（色・背景・フォント）は外観設定（詳細・折り畳みなし）内に配置（不透明度は色のアルファに統合済み）。

## 9. ルーペ設定

| 設定項目 | Settings プロパティ | 反映先コントロール / プロパティ |
| --- | --- | --- |
| ルーペの中心座標を表示 | `LoupeShowCoordLabel` | `LoupeControl.Options.Appearance.ShowCoordLabel`（外観設定のラベル設定カテゴリに配置） |
| 枠色 | `LoupeFrameColor` | `LoupeControl.Options.Appearance.FrameColor` |
| 枠の太さ | `LoupeFrameWidth` | `LoupeControl.Options.Appearance.FrameWidth` |
| 十字線色（不透明度含む） | `LoupeCrossColor` | `LoupeControl.Options.Appearance.CrossColor`（アルファ込みの色） |
| 選択矩形破線の色A（不透明度含む） | `LoupeSelectionRectColorA` | `FadeMaskDragRectOptions.SelectionRectColorA`（アルファ込み。ルーペの Bitmap へ破線を描く `FadeMaskDragRectControl` が使用） |
| 選択矩形破線の色B（不透明度含む） | `LoupeSelectionRectColorB` | `FadeMaskDragRectOptions.SelectionRectColorB`（アルファ込み。同上） |
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
| 選択矩形のサイズを表示 | `SizeLabelVisible` | `MarqueeOptions.ShowSizeLabel`（選択矩形コアが単一所有する） |
| テキスト色 | `CaptureTextColor` | `MarqueeOptions.SizeLabelColor`（キャプチャのテキスト色と共用） |
| 背景色（不透明度含む） | `SizeLabelBackgroundColor` | `MarqueeOptions.SizeLabelBackgroundColor`（アルファ込み） |
| フォントサイズ | `SizeLabelFontSize` | `MarqueeOptions.SizeLabelFontSize` |

### 10.2 2 行目: 操作ラベル

| 設定項目 | Settings プロパティ | 反映先コントロール / プロパティ |
| --- | --- | --- |
| 矩形操作を矩形内に表示 | `ShowSubLines` | `FadeMaskDragRectOptions.Labels.ShowOperationLabel`（**ホストが描く**。文言の生成は `CaptureLabelTexts` が単一所有する） |
| テキスト色 | `SubLineColor` | `FadeMaskDragRectOptions.Labels.OperationColor` |
| 背景色（不透明度含む） | `SubLineBackgroundColor` | `FadeMaskDragRectOptions.Labels.OperationBackgroundColor`（アルファ込み） |
| フォントサイズ | `SubLineFontSize` | `FadeMaskDragRectOptions.Labels.OperationFontSize` |

### 10.3 3 行目: 情報ラベル

| 設定項目 | Settings プロパティ | 反映先コントロール / プロパティ |
| --- | --- | --- |
| 選択中のウィンドウのタイトルを表示 | `InfoLabelVisible` | `FadeMaskDragRectOptions.Labels.ShowInfoLabel`（**ホストが描く**。文言はモードから `CaptureLabelTexts` が決める） |
| テキスト色 | `InfoLabelColor` | `FadeMaskDragRectOptions.Labels.InfoColor` |
| 背景色（不透明度含む） | `InfoLabelBackgroundColor` | `FadeMaskDragRectOptions.Labels.InfoBackgroundColor`（アルファ込み） |
| フォントサイズ | `InfoLabelFontSize` | `FadeMaskDragRectOptions.Labels.InfoFontSize` |

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
| ウィンドウの位置・サイズ | `MainWindowBounds` | `MainWindow`（起動時復元・終了時保存） |
| ウィンドウの状態 | `MainWindowState` | `MainWindow`（起動時復元・終了時保存） |

- Undo 一覧のアイコンサイズはフォルダビューと同じ値（`FolderExtraLargeIconSize` / `FolderLargeIconSize` / `FolderMediumIconSize`）を使用する

## 13. 外観設定のデフォルト値への復元

設定画面「外観設定」の右上にある **「デフォルト値に戻す」** ボタンで、外観設定（選択枠・暗転・ハンドル・マーカー・ルーペ・市松模様・選択矩形内ラベル・座標ラベル）をデフォルト値へ一括復元する。

- 実装: `Settings.ResetAppearanceToDefaults()`（新規インスタンスのフィールド初期化子 = デフォルト値をコピー）
- デフォルト値は各コントロール（`MarqueeControl` / `LoupeControl` / 装飾層の `OuterDecoAppearance`）の既定値と一致させる
- 変更は `PropertyChanged` 購読により自動保存される
- 外観設定以外（テーマ・保存設定・ホットキー等）は変更されない
