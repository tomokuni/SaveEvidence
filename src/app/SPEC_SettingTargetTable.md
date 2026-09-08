# 設定項目 と 反映先コントロール の対応表（SPEC_SettingTargetTable）

## 1. 文書概要

| 項目 | 内容 |
| --- | --- |
| 対象 | 設定画面（`SettingsPage.axaml`）の全設定項目 |
| プロジェクト | `app` |
| 対応の範囲 | 設定項目 → `Settings` プロパティ → 反映先コントロール |
| 関連文書 | `SPEC_System.md` / `SPEC_ExtFunc.md` |

本表は、設定画面で変更できる各項目が、**どの `Settings` プロパティ**に保存され、**どのコントロールのどのプロパティ**へ反映されるかを一覧化したものです。

## 2. 反映経路

```text
SettingsPage.axaml（ColorPicker / CheckBox / NumericUpDown / ComboBox）
  → Settings（ObservableProperty・変更通知）
  → MainWindow.ApplySnipCaptureSettings（CaptureSettings 生成）
  → SnipCapture.AppearanceSettings（部分更新マージ）
  → FadeMaskDragRectControl.ApplyAppearance
  → DragRectControl / FadeMaskRenderer / LoupeControl の各プロパティ
```

- 設定画面の変更は `Settings` の `PropertyChanged` で **即時反映** される。
- SnipCaptureUI への反映は `CaptureSettings`（nullable record・部分更新）を介して行い、`null` のメンバーは現在値を維持する。
- テーマ・画像配置・市松模様等、app 内で直接処理する項目は個別の購読で反映される。

## 3. 外観設定

| 設定項目 | Settings プロパティ | 反映先コントロール / プロパティ |
| --- | --- | --- |
| テーマ（ライト / ダーク / システム） | `ThemeMode` | `App.RequestedThemeVariant`（アプリ全体） |

- `ThemeMode.System` は OS の設定に追従（`ThemeVariant.Default`）。

### 外観設定（詳細・折り畳みなし）

下記の項目は設定画面下部の「外観設定」セクションに配置される（キャプチャ設定 / ルーペ設定から移動）。

| 設定項目 | Settings プロパティ | 反映先コントロール / プロパティ |
| --- | --- | --- |
| 選択枠の色（不透明度含む） | `CaptureBorderColor` | `DragRectControl.BorderColor` / `FadeMaskRenderer.BorderColor`（アルファ込み） |
| 選択枠の太さ | `CaptureBorderThickness` | `DragRectControl.BorderThickness` + `FadeMaskRenderer.BorderThickness` |
| 選択矩形の最小サイズ | `MinSelectionSize` | `DragRectControl.MinSelectionSize` |
| 暗転オーバーレイの色（不透明度含む） | `CaptureOverlayColor` | `FadeMaskRenderer.OverlayArgbColor`（アルファ込み） |
| ルーペの拡大率 | `LoupeZoomLevel` | `LoupeControl.ZoomLevel` |
| ルーペのサイズ | `LoupeSize` | `LoupeControl.RequestedSize` |
| ルーペのカーソルからのオフセット | `LoupeOffset` | `LoupeControl.PositionOffset` |

## 4. 保存設定

| 設定項目 | Settings プロパティ | 反映先コントロール / プロパティ |
| --- | --- | --- |
| 保存先フォルダ | `SaveFolderPath` | 保存処理（SaveImageAsync）/ `FolderViewPage` / SaveButton ToolTip |
| ファイル名テンプレート | `FileNameTemplate` | 保存処理（SaveImageAsync）・CapturePage のテキストボックス |

## 5. 画像設定

| 設定項目 | Settings プロパティ | 反映先コントロール / プロパティ |
| --- | --- | --- |
| スクロール不要時は中央に配置 | `CenterAlign` | `CropView.CenterAlign`（画像配置） |
| 市松模様 A（色） | `PreviewCheckerColorA` | `CropView.ViewportBackground`（プレビュー）+ `LoupeControl.CheckerColorA`（ルーペ） |
| 市松模様 B（色） | `PreviewCheckerColorB` | `CropView.ViewportBackground`（プレビュー）+ `LoupeControl.CheckerColorB`（ルーペ） |
| 市松模様のタイルサイズ | `PreviewCheckerSize` | `CropView.ViewportBackground`（プレビュー）+ `LoupeControl.CheckerSize`（ルーペ） |

- 市松模様は「画像設定」の詳細設定で管理し、**プレビュー背景とルーペ背景で同じ設定を共有**する。

## 6. キャプチャ設定

> 色は不透明度を統合した `#AARRGGBB` 形式で保存し、設定反映時に `Color.Parse` してそのまま渡す（コントロール側も色のアルファに統合済み）。

| 設定項目 | Settings プロパティ | 反映先コントロール / プロパティ |
| --- | --- | --- |
| キャプチャ時にメインウィンドウを隠す | `HideMainWindowOnCapture` | `CaptureSettings.HideOwnerWindow` |
| 非表示後の DWM 反映待機時間 | `HideBeforeCaptureDelayMs` | `CaptureSettings.HideBeforeCaptureDelay`（ミリ秒 → TimeSpan） |
| キャプチャ枠の色（不透明度含む） | `CaptureBorderColor` | `DragRectControl.BorderColor` / `FadeMaskRenderer.BorderColor`（アルファ込み） |
| キャプチャ枠の太さ | `CaptureBorderThickness` | `DragRectControl.BorderThickness` + `FadeMaskRenderer.BorderThickness` |
| 暗転オーバーレイの色（不透明度含む） | `CaptureOverlayColor` | `FadeMaskRenderer.OverlayArgbColor`（アルファ込み） |

- 選択矩形の最小サイズ（`MinSelectionSize`）は「外観設定」（§3）に配置。

## 7. ドラッグハンドル設定

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

## 8. ルーペ設定

| 設定項目 | Settings プロパティ | 反映先コントロール / プロパティ |
| --- | --- | --- |
| ルーペの中心座標を表示 | `LoupeShowCoordLabel` | `LoupeControl.ShowCoordLabel`（外観設定のラベル設定カテゴリに配置） |
| 枠色 | `LoupeFrameColor` | `LoupeControl.FrameColor` |
| 枠の太さ | `LoupeFrameWidth` | `LoupeControl.FrameWidth` |
| 十字線色（不透明度含む） | `LoupeCrossColor` | `LoupeControl.CrossColor`（アルファ込みブラシ） |
| 選択矩形破線の色A（不透明度含む） | `LoupeSelectionRectColorA` | `LoupeControl.SelectionRectColorA`（アルファ込みブラシ） |
| 選択矩形破線の色B（不透明度含む） | `LoupeSelectionRectColorB` | `LoupeControl.SelectionRectColorB`（アルファ込みブラシ） |
| 座標ラベルのテキスト色 | `LoupeCoordLabelColor` | `LoupeControl.CoordLabelColor` |
| 座標ラベルの背景色（不透明度含む） | `LoupeCoordLabelBackgroundColor` | `LoupeControl.CoordLabelBackgroundColor`（アルファ込みブラシ） |
| 座標ラベルのフォントサイズ | `LoupeCoordLabelFontSize` | `LoupeControl.CoordLabelFontSize` |

- 拡大率（`LoupeZoomLevel`）・サイズ（`LoupeSize`）・カーソルからのオフセット（`LoupeOffset`）は「外観設定」（§3）に配置。
- 市松模様は「画像設定」（§5）で管理するため、ルーペ設定には含めない。
- 座標ラベルの外観は詳細設定（Expander）内に配置。

### 描画スケール（自動設定・設定項目なし）

ルーペの描画スケール（`LoupeControl.RenderScaling`・DIP→物理ピクセル倍率）は、**ユーザー設定ではなくウィンドウの DPI スケーリングから自動取得**して反映する。

- 取得元: `MainWindow.DesktopScaling`（100% = 1.0 / 125% = 1.25 / 150% = 1.5 / 200% = 2.0）
- 反映: `CaptureSettings.LoupeRenderScaling` に `MainWindow.DesktopScaling` を渡す
- DPI 変化への追従: `MainWindow.ScalingChanged` イベントで再反映（別モニタへの移動・ディスプレイ設定変更時）
- 設定画面のルーペ設定には表示しない（ユーザー設定不要のため）

## 9. ラベル設定

> 表示の ON/OFF チェックボックス（`SizeLabelVisible` / `ShowSubLines` / `InfoLabelVisible` / `ShowCoordLabel` / `LoupeShowCoordLabel`）は設定画面下部の「外観設定」の先頭カテゴリ（ラベル設定）に配置。テキスト色・背景色・フォントサイズは外観設定内の対応カテゴリ（選択矩形内のラベル / カーソル位置の座標ラベル / ルーペの中心座標ラベル）に配置。

### 9.1 1 行目: サイズラベル

| 設定項目 | Settings プロパティ | 反映先コントロール / プロパティ |
| --- | --- | --- |
| 選択矩形のサイズを表示 | `SizeLabelVisible` | `FadeMaskRenderer.ShowSizeLabel`（詳細設定の外に配置） |
| テキスト色 | `CaptureTextColor` | `FadeMaskRenderer.TextColor`（SizeLabelColor と共用） |
| 背景色（不透明度含む） | `SizeLabelBackgroundColor` | `FadeMaskRenderer.TextBackgroundColor`（アルファ込み） |
| フォントサイズ | `SizeLabelFontSize` | `FadeMaskRenderer.FontSize` |

### 9.2 2 行目: 操作ラベル

| 設定項目 | Settings プロパティ | 反映先コントロール / プロパティ |
| --- | --- | --- |
| 矩形操作を矩形内に表示 | `ShowSubLines` | `FadeMaskRenderer.ShowSubLines`（詳細設定の外に配置） |
| テキスト色 | `SubLineColor` | `FadeMaskRenderer.SubLineColor` |
| 背景色（不透明度含む） | `SubLineBackgroundColor` | `FadeMaskRenderer.SubLineBackgroundColor`（アルファ込み） |
| フォントサイズ | `SubLineFontSize` | `FadeMaskRenderer.SubLineFontSize` |

### 9.3 3 行目: 情報ラベル

| 設定項目 | Settings プロパティ | 反映先コントロール / プロパティ |
| --- | --- | --- |
| 選択中のウィンドウのタイトルを表示 | `InfoLabelVisible` | `FadeMaskRenderer.InfoLabelVisible`（詳細設定の外に配置） |
| テキスト色 | `InfoLabelColor` | `FadeMaskRenderer.InfoLabelColor` |
| 背景色（不透明度含む） | `InfoLabelBackgroundColor` | `FadeMaskRenderer.InfoLabelBackgroundColor`（アルファ込み） |
| フォントサイズ | `InfoLabelFontSize` | `FadeMaskRenderer.InfoLabelFontSize` |

## 10. ホットキー設定

| 設定項目 | Settings プロパティ | 反映先コントロール / プロパティ |
| --- | --- | --- |
| 全画面キャプチャ | `SelectScreenHotKey` | `WinGlobalHotKeyService`（WM_HOTKEY 登録） |
| ウィンドウ選択キャプチャ | `WindowSelectHotKey` | `WinGlobalHotKeyService`（WM_HOTKEY 登録） |
| 範囲選択キャプチャ | `AreaSelectHotKey` | `WinGlobalHotKeyService`（WM_HOTKEY 登録） |

## 11. 設定外・自動保存（フォルダビュー / Undo 一覧 / ウィンドウ状態）

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

## 12. 外観設定のデフォルト値への復元

設定画面「外観設定」の右上にある **「デフォルト値に戻す」** ボタンで、外観設定（選択枠・暗転・ハンドル・マーカー・ルーペ・市松模様・選択矩形内ラベル・座標ラベル）をデフォルト値へ一括復元する。

- 実装: `Settings.ResetAppearanceToDefaults()`（新規インスタンスのフィールド初期化子 = デフォルト値をコピー）
- デフォルト値は各コントロール（`DragRectControl` / `LoupeControl` / `FadeMaskRenderer`）の既定値と一致させる
- 変更は `PropertyChanged` 購読により自動保存される
- 外観設定以外（テーマ・保存設定・ホットキー等）は変更されない
