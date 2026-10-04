# App.AvaloniaUI 外部機能仕様書

## 目次

- [1. 文書概要](#1-文書概要)
- [2. 目的](#2-目的)
- [3. テーマ（外観設定）](#3-テーマ外観設定)
  - [3.1 テーマ対応コンポーネント](#31-テーマ対応コンポーネント)
- [4. フォルダビュー](#4-フォルダビュー)
  - [4.1 表示モード・ソート順の保存と復元](#41-表示モードソート順の保存と復元)
  - [4.2 一覧表示](#42-一覧表示)
  - [4.3 Undo 一覧](#43-undo-一覧)
  - [4.4 ラベル設定（キャプチャ・切出し時の左上ラベル）](#44-ラベル設定キャプチャ切出し時の左上ラベル)
  - [4.5 キーボードショートカット（Ctrl キー）](#45-キーボードショートカットctrl-キー)
- [5. ファイル名テンプレートのバリデーション](#5-ファイル名テンプレートのバリデーション)
  - [5.1 プレースホルダ](#51-プレースホルダ)
  - [5.2 エスケープ](#52-エスケープ)
- [6. 保存先フォルダのプレースホルダ](#6-保存先フォルダのプレースホルダ)
  - [6.1 ToolTip](#61-tooltip)
  - [6.2 パス存在バリデーション](#62-パス存在バリデーション)
- [7. 画像プレビュー（ズーム連携）](#7-画像プレビューズーム連携)
  - [7.1 拡大率表示と座標表示](#71-拡大率表示と座標表示)
- [8. ログ](#8-ログ)
  - [8.1 LogLevel 設定ポリシー](#81-loglevel-設定ポリシー)
  - [8.2 ログ定義一覧](#82-ログ定義一覧)

---

## 1. 文書概要

| 項目 | 内容 |
| --- | --- |
| 対象 | アプリ本体（テーマ / フォルダビュー / ファイル名テンプレート / 保存 / キャプチャ・プレビュー連携） |
| プロジェクト | `src/App.AvaloniaUI/src/App.AvaloniaUI.csproj` |
| 名前空間 | `EsUtil.SaveEvidence.App.AvaloniaUI.Views` / `.Services` / `.Controls` / `.Converters` / `.Logging` 等 |
| 対象フレームワーク | `net10.0` |
| UIフレームワーク | Avalonia 12.1 |
| 主な用途 | 画面キャプチャアプリ本体。 ズーム・パン表示は `CropViewControl`（`ZoomViewControl` を内包）に委譲 |

## 2. 目的

画面キャプチャ・プレビュー・保存・フォルダ管理・Undo 履歴を提供するアプリケーションです。

- キャプチャ（スクリーン / ウィンドウ / 範囲選択）の開始と結果の反映
- プレビュー画像のズーム・パン・Fit 表示（`CropViewControl` が内包する `ZoomViewControl` へ委譲）
- 画像の保存（ファイル名テンプレート・オートインクリメント・上書き確認）
- フォルダビュー / Undo 一覧 / 設定画面
- テーマ（ライト / ダーク / システム）

ズーム率の遷移・表示方式などの詳細仕様は `CropViewControl`（`ZoomViewControl` を内包）の `SPEC_ExtFunc.md`（[`../Parts.CapturePreviewControl.AvaloniaUI/SPEC_ExtFunc.md`](../../Parts.CapturePreviewControl.AvaloniaUI/docs/SPEC_API.md)）を参照してください。

## 3. テーマ（外観設定）

設定画面「外観設定」でテーマモードを選択できます。 選択は即時反映され、次回起動時に復元されます。

| モード | 挙動 |
| --- | --- |
| `ライト` | 常にライトテーマ |
| `ダーク` | 常にダークテーマ |
| `システム` | OS のダーク / ライト設定に追従（既定値） |

- **反映方式**: `ThemeMode`（`Settings.ThemeMode`）→ `App.RequestedThemeVariant` へ反映。 FluentTheme の DynamicResource により全コントロールの色が自動的に切り替わる
- **設定ファイル**: enum は文字列で保存（`"ThemeMode": "Dark"`）

### 3.1 テーマ対応コンポーネント

| コンポーネント | 標準リソース | ライト | ダーク |
| --- | --- | --- | --- |
| フォルダリスト背景 | `SystemControlBackgroundAltHighBrush` | 白 | 黒 |
| ファイル名ラベル背景 | `SystemControlBackgroundAltHighBrush`（リストと同一色） | 白 | 黒 |
| ファイル名文字 | `SystemControlForegroundBaseHighBrush` | 黒 | 白 |
| サムネイル枠背景 | `SystemControlBackgroundChromeMediumLowBrush` | 薄いグレー（`#F2F2F2`） | 暗いグレー（`#FF2B2B2B`） |
| サムネイル枠線 | `SystemControlForegroundChromeGrayBrush` | グレー（`#767676`） | グレー（`#767676`） |
| サムネイル影 | `ThumbnailShadow`（App リソース・テーマ非依存） | 単一値（`0 3 10 0 #8A000000`） | 単一値（同左） |

- 背景・枠線・文字色は **Fluent テーマの標準リソース（DynamicResource）** を参照し、テーマ切替に自動追従する（カスタム ThemeDictionaries は不使用）
- サムネイル影のみ、Fluent テーマに BoxShadows の標準リソースが存在しないため、テーマ非依存の単一値として App リソース（`ThumbnailShadow`）で定義する
- ホバー時は枠線がアクセントブルー（`#0078D4`）にハイライトされ、影は非表示になる（テーマ非依存）

## 4. フォルダビュー

### 4.1 表示モード・ソート順の保存と復元

表示モード（特大 / 大 / 中）とソート順（昇順 / 降順）の選択状態は自動保存され、次回起動時に復元されます。

| 項目 | 設定キー | 既定値 |
| --- | --- | --- |
| 表示モード | `Settings.FolderViewModeIndex`（0=特大, 1=大, 2=中） | 1（大） |
| ソート順 | `Settings.FolderViewSortAscending`（true=昇順, false=降順） | true（昇順） |

- **保存タイミング**: 表示モード・ソート切替操作のたびに保存（`SaveViewSettings`）
- **復元タイミング**: ページ初期化 / `Loaded` / ナビゲーションで表示されるたび（`IsVisible` 変更時）
- **復元時の同期**: 復元した表示モード・ソート順を ToggleButton の選択状態（チェック）へ同期する。 同期対象は対応するボタン群のみ（表示モードボタンは Tag=0〜2、ソートボタンは Tag=asc/desc。 他グループには触れない）

### 4.2 一覧表示

- サムネイル + ファイル名を `BottomAlignWrapPanel` で表示（行内下端揃え・行高は最大アイテム高さ）
- ダブルクリックで既定アプリによりファイルを開く

### 4.3 Undo 一覧

ナビパネル「Undo 一覧」から、キャプチャページの Undo / Redo スタックと現在のプレビュー画像をサムネイル一覧表示します。

- **表示順**: 時系列順で「新しい → 古い」の固定。 `Redo（新しい）→ 現在 → Undo（古い）` の順に並ぶ
  - Redo: `_redoStack[0]`（現在に最も近い未来）から順に「Redo 1」「Redo 2」…
  - 現在: 現在のプレビュー画像（ラベル「現在」）
  - Undo: `_stack[^1]`（現在の直前）から順に「Undo 1」「Undo 2」…
- **現在の画像の強調**: 現在のキャプチャページに表示中の画像は青枠（`BorderBrush #0078D4`・`BorderThickness 3`）で強調表示する
- **表示モード**: ツールバーの特大 / 大 / 中 ボタンで切替可能。 選択状態は `Settings.UndoViewModeIndex`（0=特大, 1=大, 2=中、既定 1=大）に保存・復元される。 アイコンサイズはフォルダビューと同じ値（`FolderExtraLargeIconSize` / `FolderLargeIconSize` / `FolderMediumIconSize`）を使用する
- **外部ツール表示**: サムネイルのダブルクリックで一時フォルダへ PNG として保存し、OS の既定アプリ（外部ツール）で開く（Undo / Redo スタックの画像はファイルではなく Bitmap のため）
- **更新タイミング**: ナビゲーションで Undo 一覧を表示するたびに最新の履歴を読み込む（`UndoPage` が `IsVisible` 変更を監視し、保存済み設定の復元と一覧の再構築を行う。 UndoHistory / MainViewModel は DI シングルトンから取得）
- **表示モードの保存・復元**: 表示モード切替操作のたびに保存（`SaveViewModeSettings`）。 復元はページ表示時（範囲チェック付き・`ViewModeDisplay.ClampIndex`）

### 4.4 ラベル設定（キャプチャ・切出し時の左上ラベル）

キャプチャ選択画面の左上に表示される最大 3 行のラベルは、設定画面「ラベル設定」で行ごとに設定できます。

| 行 | ラベル | 文字色の設定キー | 背景色の設定キー（不透明度は色に統合） | フォントサイズ |
| --- | --- | --- | --- | --- |
| 1 行目 | サイズラベル | `CaptureTextColor`（ラベル設定の 1 行目に一本化） | `SizeLabelBackgroundColor` | `SizeLabelFontSize` |
| 2 行目 | 操作ラベル | `SubLineColor` | `SubLineBackgroundColor` | `SubLineFontSize` |
| 3 行目 | 情報ラベル | `InfoLabelColor` | `InfoLabelBackgroundColor` | `InfoLabelFontSize` |

- **背景色**: 色と不透明度を統合した `#AARRGGBB` 形式（例: 黒 60% = `#99000000`）。 カラーピッカー（ColorPickerControl）の Flyout で不透明度（アルファ）も設定できる
- **キャプチャテキストの色の一本化**: キャプチャテキストの色は 1 行目（サイズラベル）の文字色に一本化する（設定キー `CaptureTextColor` は「ラベル設定」の 1 行目へマップ）

### 4.5 キーボードショートカット（Ctrl キー）

メニューを開かずに効く Ctrl ショートカットを提供します。 `Ctrl` とメニュー項目のアクセラレータ文字（`S` / `C` / `V` / `Z` / `Y` / `R`）の組み合わせ、およびズーム（`Ctrl + / Ctrl -`）です。

| ショートカット | 操作 |
| --- | --- |
| `Ctrl+S` | File - 画像を保存 |
| `Ctrl+C` | Clipboard - コピー |
| `Ctrl+V` | Clipboard - 貼り付け |
| `Ctrl+Z` | Image - 元に戻す |
| `Ctrl+Y` | Image - 元に戻すを取り消す |
| `Ctrl+R` | Crop - 切り出し |
| `Ctrl +` | ズームイン（⊖⊕ボタンと同じ 23 段階遷移） |
| `Ctrl -` | ズームアウト（⊖⊕ボタンと同じ 23 段階遷移） |

- **Capture 系はホットキーに一本化**: `Capture - スクリーン / ウィンドウ / 範囲選択` は Ctrl ショートカット（Ctrl+Q/W/E）を実装しない。 グローバルホットキー（WM_HOTKEY）がアプリ内外で動作するため、ショートカットの重複を避ける

- **実装**: 全ショートカットは `CapturePage.OnGlobalKeyDown`（`KeyDown` を Tunnel で購読）で一括処理する。 テキスト入力中（ファイル名テンプレート等）はテキスト編集ショートカット（Ctrl+C/V/Z/Y）を優先するため無視する
- **メニュー項目への表記**: 各メニュー項目の `InputGesture` に `KeyGesture` を設定し、メニュー展開時に項目の右側へショートカットキーを表示する。 `InputGesture` は表示のみでキー処理は行わない（処理は `OnGlobalKeyDown` またはグローバルホットキーが担当）。 ショートカット未実装の項目（Crop - 自動切り出し 等）には表示しない
- **Capture 系メニューはホットキー設定を表示**: `Capture - スクリーン / ウィンドウ / 範囲選択` の `InputGesture` は**ホットキー設定**（`SelectScreenHotKey` / `WindowSelectHotKey` / `AreaSelectHotKey`、settings.json の値）を表示する。 設定画面でホットキーを変更するとメニュー表示も即時更新される。 ホットキーが未設定（`Key.None`）の場合は表示しない。 キー操作はグローバルホットキー（WM_HOTKEY）が担当する
- **Alt アクセラレータは無効**: メニューの Header にアクセラレータ文字（`(_X)`）を含めないため、メニュー展開中の `Alt+S` 等は機能しない（Alt はメニュー展開専用）
- **ホットキーとの関係**: 設定画面のホットキー（`SelectScreenHotKey` 等・WM_HOTKEY のグローバル登録）とは独立。 Capture 系以外の Ctrl ショートカットはホットキーと競合しないキーを使用する

## 5. ファイル名テンプレートのバリデーション

保存ファイル名のテンプレート文字列を検証し、無効な場合は視覚的に通知します。

- **無効条件**: 空文字、またはファイル名に使用できない文字（`\ / : * ? " < > |`）を含む
- **赤枠表示**: バリデーションエラー時はテキストボックスの枠線が赤くなる（`validation-error` クラス）
- **保存ボタン無効化**: `CanSave` は画像あり（`PreviewImage` 非 null）+ テンプレート有効（`HasValidTemplate`）+ 未保存（`IsSaved` が false）の場合のみ true
  - 保存が完了すると `IsSaved` が true になり、保存ボタン・メニューの「画像を保存」が無効化される（同一画像の二重保存防止）
  - 画像の変更（新規キャプチャ・切り出し・貼り付け・Undo / Redo 復元）で `IsSaved` が false に戻り、再び保存可能になる
- **エラーメッセージ**: `ValidationMessage`（例: 「ファイル名に使用できない文字 ('/') が含まれています。」）
- **即時反映**: テキスト入力中も含め即時検証・反映され、エラー解消で自動的に通常表示へ戻る
- **オートインクリメント**: 保存成功後に、テンプレート文字列の**右端の数値**をインクリメントして次のファイル名を自動生成する（`FileNameTemplate.IncrementRightmostNumber`。 桁数は維持: `screenshot_001.png` → `screenshot_002.png`）。 数値がなければそのまま維持される

### 5.1 プレースホルダ

ファイル名テンプレートでは、`{ }` で囲われた文字列がプレースホルダとして扱われ、内部の日時トークンが現在日時へ置換されます。 トークンは自由に組み合わせられます。

| トークン | 内容 | 例 |
| --- | --- | --- |
| `date` | 日付（yyyyMMdd） | 20250101 |
| `time` | 時刻（HHmmss・24 時間制） | 120000 |
| `yyyy` | 年 4 桁 | 2025 |
| `MM` | 月 2 桁 | 01 |
| `dd` | 日 2 桁 | 01 |
| `HH` | 時 2 桁（24 時間制） | 12 |
| `mm` | 分 2 桁 | 34 |
| `ss` | 秒 2 桁 | 56 |

- プレースホルダ内でトークンを自由に組み合わせられる（例: `{yyyyMMdd}` → `20260819`、`{HHmm}` → `2352`、`{date}` → `20260819`）
- 未定義の文字列（例: `{example}`）はトークン置換されず、中括弧が外れてそのまま出力される
- 日時は保存実行時点の現在日時で置換される（`FileNameTemplate.Generate`。 `now` 引数へ日時を渡すと呼び出し側が時刻を固定できる）
- ToolTip にはプレースホルダの説明が表示される（`{date}` / `{time}` 等は XAML マークアップ拡張と衝突するためコードビハインドで構築）

### 5.2 エスケープ

プレースホルダの開始・終了を示す中括弧自体をリテラルとして含める場合は、二重中括弧でエスケープします。

| 入力 | 出力 |
| --- | --- |
| `{{` | `{`（リテラル） |
| `}}` | `}`（リテラル） |
| `{{date}}` | `{date}`（プレースホルダとして置換されない） |

- エスケープはプレースホルダ置換より先に解決されるため、`{{date}}` のようにエスケープで囲んだ文字列はプレースホルダとして誤置換されない
- 例: `abcd{{e}}f{yyyyMMdd}-{HHmm}` → `abcd{e}f20260819-2352`
- `{` `}` は Windows のファイル名で使用可能なため、エスケープ後の検証も有効と判定される

## 6. 保存先フォルダのプレースホルダ

設定画面「保存設定」の保存先フォルダには、**プレースホルダ `{MyPictures}`** を使用できます。

- `{MyPictures}` は `Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)` のフォルダ（ピクチャフォルダ）へ展開される
- 大文字小文字は区別しない（`{mypictures}` / `{MYPICTURES}` も展開）
- パスの一部に含められる（例: `{MyPictures}\SaveEvidence`）
- 解決は `MainViewModel.ResolveSaveFolderPath` / `SaveFolderPathResolved` が行い、保存・フォルダ一覧（FolderViewPage）・保存ボタンの ToolTip・エクスプローラーで開く操作はすべて解決済みパスを使用する
- 設定値（`Settings.SaveFolderPath`）にはプレースホルダのまま保存される

### 6.1 ToolTip

保存先フォルダのテキストボックスには、プレースホルダの説明を示す ToolTip が表示される。

- `{MyPictures}` はピクチャフォルダへ展開される旨（展開先の実パス付き）と使用例（`{MyPictures}\SaveEvidence`）、および「存在しないパスは枠が赤くなる」ことを説明
- `{MyPictures}` は XAML のマークアップ拡張（`{...}`）と衝突するため、ToolTip はコードビハインド（`SettingsPage.BuildSaveFolderToolTip`）で構築する

### 6.2 パス存在バリデーション

保存先フォルダのテキストボックスは、入力値（プレースホルダ解決後）が**存在しない場合に枠線が赤くなる**（`validation-error` クラス）。

- 判定: `MainViewModel.HasInvalidSaveFolder`（解決後パスが空、または `Directory.Exists` が false の場合 true）
- 表示: テキストボックスの枠線が赤くなる（`TextBox.validation-error /template/ Border#PART_BorderElement` スタイル。 フォーカス・ホバー状態でも優先）
- 入力中も即時反映される（`SaveFolderPath` の変更通知に連動）

## 7. 画像プレビュー（ズーム連携）

プレビュー画像のズーム・パン・Fit 表示は `CropViewControl`（`ZoomViewControl` を内包する複合コントロール）に委譲します。 CapturePage は `CropView`（`CropViewControl`）の `Source` を設定し、`ZoomChanged` / `ViewportChanged` を購読して操作を指示します。

### 7.1 拡大率表示と座標表示

- **拡大率表示**: `CropView.ZoomChanged` イベントの `e.ZoomPercent` から `MainViewModel.ZoomDisplayText`（例: "120%"）を生成し、ステータスバーがバインディングで表示する。 Fit 中も実効倍率の % を表示する（「自動」表記なし）
- **座標表示**: `CapturePage.OnImagePointerMoved` が `CropView.ImageRectViewport()` / `CropView.ClientToSource()`（`ClientToSource` ヘルパー経由）でポインタの対象座標（画像ピクセル）を算出し、ステータスバーへ表示する（画像外は `-`）
- **フライアウト選択肢**: 拡大率ボタンのクリックで表示されるフライアウト（0=自動 + 固定倍率 10〜500% の 13 段階）から選択し、`CropView.ZoomPercent`（`MainViewModel.ZoomPercent`）を設定する（`CapturePage.OnZoomFlyoutItemClick`）
- **切り出し中のズーム・パン**: 切り出し中は選択オーバーレイがプレビューを覆うため、ホイールは `CropViewControl` が内部でビューポートへ転送する（Ctrl+ホイール = カーソル位置を中心にズーム / 通常ホイール = パン / Shift+ホイール = 横パン）。 ステータスバーの ⊕⊖ ボタン・スライダ・`Ctrl +` / `Ctrl -` は切り出し中も使用できる（選択矩形は対象座標で不変のため維持される）。 スクロールバーはオーバーレイの下になり掴めないため、パンはホイールで行う

詳細仕様（遷移ルール・表示方式・公開 API）は `CropViewControl`（`ZoomViewControl` を内包）の `SPEC_ExtFunc.md`（[`../Parts.CapturePreviewControl.AvaloniaUI/SPEC_ExtFunc.md`](../../Parts.CapturePreviewControl.AvaloniaUI/docs/SPEC_API.md)）を参照してください。

## 8. ログ

本プロジェクト（app）のログ定義は `src/App.AvaloniaUI/src/Logging/AppLog.cs`（LoggerMessage ソース生成）に集約し、`ILogger` 経由で出力する。
出力先は Serilog（`%LOCALAPPDATA%\SaveEvidence\logs\SaveEvidence.log`、日次ローテーション・7日で自動アーカイブ・削除）で、`AppLog` のファイルシンク構成で制御する。
（ズーム操作のログはコア（`Parts.CapturePreviewControl.Core` の `ZoomLogMessage`）が定義し、`ZoomViewControl.Log` へ注入されたロガーへ出力される）

### 8.1 LogLevel 設定ポリシー

トレースログは LogLevel.Trace に限定せず、「重要度 × 発生頻度」でレベルを使い分ける。

| LogLevel | 対象 | 本番での意図 |
| --- | --- | --- |
| `Error` | 例外・失敗（処理継続不可） | 常時出力し、障害を確実に残す |
| `Warning` | 異常だがフォールバックで継続 | 常時出力し、異常発生の痕跡を残す |
| `Information` | ユーザー操作の主要イベント（低頻度） | 常時出力し、操作手順の再現を可能にする |
| `Debug` | 操作イベントの詳細（マウス / キー / 設定変更） | 障害時のみ有効化して解析 |
| `Trace` | 高頻度・極詳細 | 特別な性能・座標解析時のみ有効化 |

判断基準:

1. 例外・警告は常時残す（`Error` / `Warning`）
2. 操作の起点イベント（低頻度）は `Information`
3. マウス・キー等の操作イベント（中頻度）は `Debug`
4. 高頻度・極詳細は `Trace`
5. 設定変更（変更前後）は `Debug`

### 8.2 ログ定義一覧

| レベル | メソッド | 出力内容 |
| --- | --- | --- |
| `Information` | `_logger.LogCaptureMenuScreen` / `LogCaptureMenuWindow` / `LogCaptureMenuArea` | キャプチャメニュー（全画面/ウィンドウ/範囲選択）クリック |
| `Information` | `_logger.LogPreviewPointerPressed` | プレビュー上のポインタ押下（右ボタン / 切出し中） |
| `Information` | `_logger.LogHotKeyReceived` / `LogHotKeyTriggered` | WM_HOTKEY 受信 / 対応するキャプチャ種別 |
| `Information` | `_logger.LogActivated` / `LogDeactivated` | ウィンドウのアクティブ化 / 非アクティブ化 |
| `Information` | `_logger.LogStartSelectionMode` / `LogSessionReturned` | キャプチャ開始種別 / セッション戻り値（画像サイズ・矩形） |
| `Error` | `_logger.LogSettingsApplyError` | 設定変更の CaptureSessionControl 反映で例外 |
