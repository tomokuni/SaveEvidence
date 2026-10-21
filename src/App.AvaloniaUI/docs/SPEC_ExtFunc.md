# App.AvaloniaUI 外部機能仕様書

## 目次 <!-- omit from toc -->

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

- **タイトル バーの帯は作りません**（ページ領域はクライアント領域の先頭から始まります）。 左上には、タスク バーと同じアプリ アイコンだけを置きます（**ナビゲーションのアイコンと同じ大きさ**で、**縦位置はツールバーのアイコンと揃え**、**横位置はナビゲーションの開閉アイコンと同じ列**に置きます）。 **左クリックでナビの開閉**、**右クリックでウィンドウ メニュー**（「元のサイズに戻す / 移動 / サイズ変更 / 最小化 / 最大化 / 閉じる」。 項目と並びは `WindowMenu`（App.Core）が単一所有する）を出します。
- **ナビゲーション パネル**はページ領域の上へ重なり（上端はページ領域の先頭、下端はステータス バーに被る）、一番上にアプリ アイコン、その下に開閉ボタン（アイコンのみ。 開いているとき `ic_fluent_panel_left_contract_24_filled.svg`、閉じているとき `ic_fluent_panel_right_contract_24_filled.svg`）を置きます。 **展開時は左上のアプリ アイコンの右にアプリ名（SaveEvidence）**を表示します（アイコンとの間隔は狭くします）。 展開しても**ページ領域は動きません**。
- 展開中に**ナビ領域の外**（ページ領域・タイトル バー相当の上端・ウィンドウの外）を操作すると収納へ戻ります。
- ウィンドウの最小外寸は枠を含めて 640 × 400 DIP、既定外寸は枠を含めて 1200 × 800 DIP です（`WindowMetrics`（App.Core）が単一所有する）。
- ツールバーのボタン高・アイコン寸法・文字サイズは `ToolbarMetrics`（App.Core）が共有し、アイコン単独のボタンは 32 × 32 DIP です。 キャプチャ メニューの保存・スクリーン / ウィンドウ / 範囲選択・切り出しには各ツールバーと同じ SVG アイコンを表示します。
- ナビゲーションのアイコン（項目・開閉・左上のアプリ アイコン）は**ツールバーのアイコンと同じ大きさ**（`NavPanelMetrics.IconSize` = 16 DIP。 App.Core が単一所有）です。 項目の高さは**開閉・文字の有無で変えず 40 DIP**（`NavPanelMetrics.ItemHeight`）に固定します（変えると開閉で項目の位置がずれます）。

## 2. 目的

画面キャプチャ・プレビュー・保存・フォルダ管理・Undo 履歴を提供するアプリケーションです。

- キャプチャ（スクリーン / ウィンドウ / 範囲選択）の開始と結果の反映
- プレビュー画像のズーム・パン・Fit 表示（`CropViewControl` が内包する `ZoomViewControl` へ委譲）
- 画像の保存（ファイル名テンプレート・オートインクリメント・上書き確認）
- フォルダビュー / Undo 一覧 / 設定画面
- テーマ（ライト / ダーク / システム）

ズーム率の遷移・表示方式などの詳細仕様は `CropViewControl`（`ZoomViewControl` を内包）の [`../../Parts.CapturePreview.ControlAvaloniaUI/docs/SPEC_API.md`](../../Parts.CapturePreview.ControlAvaloniaUI/docs/SPEC_API.md)（5.5 / 5.7）を参照してください。

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
| フォルダリスト背景 | Fluent の `ListBox` 既定 | 薄いグレー（`#F2F2F2`） | 暗いグレー（`#2B2B2B`） |
| ファイル名ラベル背景 | 透明（リストの背景が見える） | — | — |
| ファイル名文字 | `SystemControlForegroundBaseHighBrush` | 黒 | 白 |
| サムネイル影 | `ShadowPlan`（App.Core。 幅・濃さの変化の単一所有者） | 単一値（計画から `BoxShadow` を生成） | 単一値（同左） |

- 背景・文字色は **Fluent テーマの標準リソース（DynamicResource）** を参照し、テーマ切替に自動追従する（カスタム ThemeDictionaries は不使用）
- **画像の周りは影だけ**にする（枠線・下地・角丸は描かない）
- サムネイル影は `ShadowFactory` が `ShadowPlan`（App.Core）の値（`BlurRadius` / `ColorArgb`）から `BoxShadow` を生成する（キャプチャページの画像・Undo 一覧と同じ計画を使う。 位置はずらさない）
- **キャプチャページの画像の影**は `CropViewControl.SurroundRenderer`（実体は画像より奥の子 `SurroundPainter`）が `ShadowRenderer` で同じ計画から描く（`BuildDeviceBands` の 1 画素の帯を単色で塗るだけ。 隅は画像の隅の頂点からの斜めの距離で濃さを決める。 `ClipToBounds` でプレビュー領域の外へ描かない。 画像より奥へ置くため、画像の端の混色の下地が影になり、白い線が出ない）
- ホバー時は影を非表示にし、一覧の項目（`ListBoxItem`）の帯（`#DADADA`）を表示する

## 4. フォルダビュー

### 4.1 表示モード・ソート順の保存と復元

表示モード（特大 / 大 / 中）とソート順（昇順 / 降順）の選択状態は自動保存され、次回起動時に復元されます。

| 項目 | 設定キー | 既定値 |
| --- | --- | --- |
| 表示モード | `Settings.FolderViewModeIndex`（0=特大, 1=大, 2=中） | 1（大） |
| ソート順 | `Settings.FolderViewSortAscending`（true=昇順, false=降順） | true（昇順） |

- **保存タイミング**: 表示モード・ソート切替操作のたびに保存（`SaveViewModeSettings`）
- **復元タイミング**: ページ初期化 / `Loaded` / ナビゲーションで表示されるたび（`IsVisible` 変更時）
- **復元時の同期**: 復元した表示モード・ソート順を ToggleButton の選択状態（チェック）へ同期する。 同期対象は対応するボタン群のみ（表示モードボタンは Tag=0〜2、ソートボタンは Tag=asc/desc。 他グループには触れない）

### 4.2 一覧表示

**一覧は 2 つのページで共通のコントロール**: フォルダビューと Undo 一覧は、同じ `ThumbnailList`（`ListBox` の派生。 テーマは `ListBox` のものを使用）を使います。 枠・影・名前の行・ホバーの表現・並べ方（`BottomAlignWrapPanel`）はこのコントロールが単一所有し、ページは並べる項目（`ThumbnailListItem`）を渡すだけです。 寸法は `ThumbnailTileMetrics`（App.Core）が単一所有します。

- 項目の外側の余白は 12 DIP（`ListBoxItem` の既定）＋ 2 DIP（項目の `Margin`）で、隣り合う画像の間隔は 28 DIP（`ThumbnailTileMetrics.ItemGap`）です。
- マウスを重ねると、項目全体（余白を含む）へ薄いグレーの帯（`#DADADA`）を出し、影を消します。

**サムネイルの縮小方式（両方とも同じ画質にするための構成）**: 2 つの一覧は画像の出所が違いますが、**最終的な縮小はどちらも面積平均**に揃えています（コーデック／Skia の縮小は「デコードのついで」に使うだけです）。

| 一覧 | 画像の出所 | デコード | 縮小 | 画質 |
| --- | --- | --- | --- | --- |
| フォルダビュー | ファイル（未デコード） | `Bitmap.DecodeToWidth`（**コーデックの縮小デコード**） | コーデックが縮小した分をそのまま使う（PNG のように縮小デコード非対応の形式はコアの面積平均へ） | 面積平均相当 |
| Undo 一覧 | メモリ上のコアの画像（デコード済み） | 不要（既にデコード済み） | コアの `EsReadOnlyBitmap.Resize`（`EsImageQuality.High` = 面積平均。 内部で並列化） | 面積平均 |

- **フォルダビューがコーデックを使う理由**: ファイルはどのみちデコードが必要で、**JPEG はコーデックが 1/8 の縮小デコード（DCT スケーリング）に対応する**ため、原寸を確保せずに読める。 一方 **PNG は縮小デコードに対応しない**（`SKCodec.GetScaledDimensions` が原寸を返す）ため、原寸デコード + 縮小の 2 段になる（それでも結果は面積平均と同じ）。
- **コーデックの縮小デコードを使っても画質は揃う**: JPEG で「1/8 デコード + 面積平均」と「原寸デコード + 面積平均」を比べても線の残り方はほぼ同じになる。 PNG は `GetScaledDimensions` が原寸を返すため両者**完全一致**する。 → **デコードは速い経路を使いつつ、縮小の規則は 1 つ（面積平均）に揃えられる**。
- **Undo 一覧がコアの縮小を使う理由**: 画像はメモリ上に既にあり**デコードが存在しない**ため、縮小だけを行う。 表示のたびにアダプタを作り直すが、面積平均は並列化されるため、枚数が増えても待ち時間が積み上がらない。
- **コストはどちらも「元画像を読む画素数」で決まる**（縮小先の寸法にはほぼよらない）。
- **MewUI 版も同じ規則に揃えている**: フォルダビューはコアの `EsReadOnlyBitmap.Load(string, int)`（WIC のスケーラーでデコードしながら縮小する。 補間は Fant）、Undo 一覧はコアの面積平均で DIP サイズへ縮小してから描画する（描画時のサンプリングは**既定が最近傍**で、同じ寸法でも画質が変わる）。

### 4.3 Undo 一覧

ナビパネル「Undo 一覧」から、キャプチャページの Undo / Redo スタックと現在のプレビュー画像をサムネイル一覧表示します。

- **表示順**: 時系列順で「新しい → 古い」の固定。 `Redo（新しい）→ 現在 → Undo（古い）` の順に並ぶ
  - Redo: `_redoStack[0]`（現在に最も近い未来）から順に「Redo 1」「Redo 2」…
  - 現在: 現在のプレビュー画像（ラベル「現在」）
  - Undo: `_stack[^1]`（現在の直前）から順に「Undo 1」「Undo 2」…
- **現在の画像の強調**: 現在のキャプチャページに表示中の画像は青枠（`BorderBrush #0078D4`・`BorderThickness 3`）で強調表示する
- **表示モード**: ツールバーの特大 / 大 / 中 ボタンで切替可能。 選択状態は `Settings.UndoViewModeIndex`（0=特大, 1=大, 2=中、既定 1=大）に保存・復元される。 アイコンサイズはフォルダビューと同じ値（`FolderExtraLargeIconSize` / `FolderLargeIconSize` / `FolderMediumIconSize`）を使用する
- **外部ツール表示**: サムネイルのダブルクリックで一時フォルダへ PNG として保存し、OS の既定アプリ（外部ツール）で開く（Undo / Redo スタックの画像はファイルではなく Bitmap のため）
- **サムネイルの縮小**: コアの `EsReadOnlyBitmap.Resize`（`EsImageQuality.High` = 面積平均）を使う（フォルダビューがコーデックの縮小デコードを使う理由との比較は §4.2 を参照）
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
- **入力欄の幅**: 入力した文字列の実測幅に合わせて広がる（最小 100 DIP・左右の余白 24 DIP）。 上限は**ツールバーが折り返さない幅**（ツールバーの幅から自分以外の要素と余白を除いた幅）で、超える分は入力欄の中でスクロールする（アイコン類が 2 段にならない）。 ウィンドウの幅が変わったときも上限を計算し直す
- **アイコン ボタンの外観**: WinUI 3 の既定のボタンと同じ外観にする（面 + 1 DIP の枠線 + 角丸 4 DIP。 面・枠線・選択中のアクセント色は `AppPalette.ButtonFace` / `ButtonBorder` / `ButtonCheckedFace` が単一所有し、MewUI 版・WinUI 3 版と揃える）。 マウスを重ねると薄いグレー、押している間はさらに濃くなる。 表示モードの `ToggleButton` は選択中にアクセント色で塗り、文字色を反転する（明色は白 / 暗色は黒）

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
- 入力欄の下に補足（例: `例: {MyPictures}\SaveEvidence（{MyPictures} はピクチャフォルダへ展開されます）`）を表示する（文言は `SettingsMetrics.SaveFolderHint`（App.Core）が単一所有し、4 UI 版で共通）

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
- **ズーム操作ボタンの下地**: ステータスバーの ⊖ / ⊕ / Fit / 拡大率のボタンは通常時の下地を塗らない（`Button.status`。 ホバー・押下は Fluent テーマの既定のまま）
- **座標表示**: `CapturePage.OnImagePointerMoved` が `CropView.ImageRectViewport()` / `CropView.ClientToSource()`（`ClientToSource` ヘルパー経由）でポインタの対象座標（画像ピクセル）を算出し、ステータスバーへ表示する（画像外は `-`）
- **通知メッセージ**: 撮影が確定したら `MainWindow.OnCaptureCompleted` が `MainViewModel.StatusMessage` へ「キャプチャしました（W x H）」を設定する。 `MainViewModel.SetPreviewImage` は `StatusMessage` を空にするため、**画像の差し替えより後**に設定する。 文言は `StatusMessages`（App.Core）が単一所有し、切り出し・貼り付け・保存失敗・自動切り出しの通知はコアが設定する。 文字色は他の UI 版と同じ青 `#007ACC`
- **フライアウト選択肢**: 拡大率ボタンのクリックで表示されるフライアウト（0=自動 + 固定倍率 10〜500% の 13 段階）から選択し、`CropView.ZoomPercent`（`MainViewModel.ZoomPercent`）を設定する（`CapturePage.OnZoomFlyoutItemClick`）
- **切り出し中のズーム・パン**: 切り出し中は選択オーバーレイがプレビューを覆うため、ホイールは `CropViewControl` が内部でビューポートへ転送する（Ctrl+ホイール = カーソル位置を中心にズーム / 通常ホイール = パン / Shift+ホイール = 横パン）。 ステータスバーの ⊕⊖ ボタン・スライダ・`Ctrl +` / `Ctrl -` は切り出し中も使用できる（選択矩形は対象座標で不変のため維持される）。 スクロールバーはオーバーレイの下になり掴めないため、パンはホイールで行う

詳細仕様（遷移ルール・表示方式・公開 API）は `CropViewControl`（`ZoomViewControl` を内包）の [`../../Parts.CapturePreview.ControlAvaloniaUI/docs/SPEC_API.md`](../../Parts.CapturePreview.ControlAvaloniaUI/docs/SPEC_API.md)（5.5 / 5.7）を参照してください。

## 8. ログ

本プロジェクト（app）のログ定義は `src/App.AvaloniaUI/src/Logging/AppLog.cs`（LoggerMessage ソース生成）に集約し、`ILogger` 経由で出力する。
出力先は Serilog（`%LOCALAPPDATA%\SaveEvidence\logs\SaveEvidence.log`、日次ローテーション・7日で自動アーカイブ・削除）で、`AppLog` のファイルシンク構成で制御する。
（ズーム操作のログはコア（`Parts.CapturePreview.Core` の `CapturePreviewLogMessage`）が定義し、`ZoomViewControl.Log` へ注入されたロガーへ出力される）

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
