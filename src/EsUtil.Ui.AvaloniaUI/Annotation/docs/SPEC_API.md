# EsUtil.Ui.AvaloniaUI.Annotation API リファレンス

## 目次

- [1. `AnnotationTextEditor`（クラス）](#1-annotationtexteditorクラス)
- [2. `AnnotationLayerControl`（クラス）](#2-annotationlayercontrolクラス)
- [3. `AnnotationEditorControls`（クラス）](#3-annotationeditorcontrolsクラス)
- [4. `AnnotationFonts`（クラス）](#4-annotationfontsクラス)
- [5. `AnnotationPalette`（静的クラス）](#5-annotationpalette静的クラス)

## 1. `AnnotationTextEditor`（クラス）

**本文の表示・その場編集と書式バーを 1 つにまとめたコントロール**です（`Panel` の派生）。 ホストはこれを 1 つ置けば、本文の編集と文字単位の書式変更ができます。

| メンバー | 種別 | 説明 |
| --- | --- | --- |
| `Layer` | property | 内側の `AnnotationLayerControl`（画像・文書・選択・元に戻すはここから操作する） |
| `IsEditingText` | property | 本文を編集中かどうか（編集中は書式バーが出ている） |
| `PaletteLayout` | property | 書式バーの代表色の並べ方（`ColorSelectorTabLayout.List` = 色名の一覧 / `Grid` = 色の四角だけを 6 列。 既定は一覧） |
| `Palette` | property | 書式バーに出す代表色の組（`AnnotationPaletteKind.Representative` = 代表色 1 / `Accent` = 代表色 2。 既定は代表色 1） |

- **書式バーは内側で出し入れします**（`Layer.EditingChanged` を購読する）。 ホストがバーを置く必要はありません。
- **配置**: レイヤーは与えられた領域いっぱい、書式バーは**本文の矩形の上**（上・左右にはみ出すときは内側へ寄せる）。
- 文書の変更・選択の変更（`Layer.TextSelectionChanged`）でバーの表示（現在の書式・装飾の押下状態）と位置を追随させます。

## 2. `AnnotationLayerControl`（クラス）

画像と注釈を重ねて表示し、選択・移動・サイズ変更・しっぽの移動・本文の編集要求を行うコントロールです。

| メンバー | 種別 | 説明 |
| --- | --- | --- |
| `Source` | property | 表示する画像（`IImage`）。 null の場合は注釈だけを描く |
| `SourcePixelWidth` / `SourcePixelHeight` | property | 元画像の画素数（縦横比を保って中央へ表示する） |
| `Document` | property | 注釈の文書。 差し替えると選択を解除する |
| `SelectedId` / `Selected` | property | 選択中の注釈（識別子・実体） |
| `DraftDefaults` | property | 新規作成時の既定値（`AnnotationDraftDefaults`。 既存の注釈は変わらない） |
| `LineHeightFactor` | property | 行間の倍率（既定 1.25） |
| `HandleHitRadius` / `HandleSize` | property | ハンドルを掴む半径・見えている一辺の大きさ（DIP。 描画と当たり判定で同じ値を共有する） |
| `TextSelectionRange` | property | 本文の書式を適用する範囲（開始位置・長さ。 長さ 0 は全体）。 ホストが入力欄の選択範囲を写す |
| `SelectedTextStyle` | property | 選択中の本文の書式（範囲の先頭の文字。 フライアウトの初期表示） |
| `SelectedTextMatches(match)` | method | 選択範囲の**すべての文字**が条件を満たすか（書式バーの押下状態。 選択が無ければキャレットの位置で判定） |
| `AddFrame(bounds)` / `AddBubble(bounds, text)` | method | 注釈を追加し、追加した注釈を選択する（戻り値は識別子） |
| `DeleteSelected()` | method | 選択中の注釈を削除する |
| `SetSelectedText(text)` | method | 選択中の吹き出しの本文を置き換える（**書式は文字位置で引き継ぐ**） |
| `ApplyTextStyle(style)` / `ApplyTextStyle(start, length, style)` / `TransformTextStyle(transform)` / `TransformTextStyle(start, length, transform)` | method | 本文へ書式を適用する（`TextSelectionRange` の範囲、または指定した範囲。 **文字単位**。 長さ 0 は全体）。 `ApplyTextStyle` は**書式を丸ごと差し替える**、`TransformTextStyle` は**指定した属性だけを書く**（他の属性を保つ） |
| `UpdateSelectedAppearance(transform)` | method | 外観（枠線・内側・角丸）を差し替える |
| `SetSelectedBodyPadding(padding)` / `UpdateSelectedTail(transform)` | method | 吹き出しの本文の余白・しっぽ（幅・辺）を差し替える |
| `MoveSelectedToFront()` / `MoveSelectedToBack()` | method | 重なり順を変える |
| `Undo()` / `Redo()` / `CanUndo` / `CanRedo` | method / property | 元に戻す・やり直す（変更の単位は 1 操作。 ドラッグは押下から離すまでで 1 手。 選択は識別子で引き継ぐ） |
| `ShowStyleEditor()` | method | 外観・書式・重なり順の編集フライアウトを表示する（右クリックと同じ） |
| `BeginTextEditing()` / `EndTextEditing()` / `IsEditingText` | method / property | 本文を**その場で編集**する（キャレット・選択範囲を本文のレイアウトのまま描く。 入力欄を重ねない） |
| `CaretIndex` / `SelectionStart` / `SelectionEnd` | property | キャレットと選択範囲（文字数） |
| `EditingChanged` | event | 本文の編集の開始・終了（ホストは書式バーを出し入れする） |
| `TextSelectionChanged` | event | 本文の選択範囲（キャレット）の変更（書式バーの現在値を追随させる） |
| `GetSelectedBodyRect()` | method | 本文の矩形（DIP）。 ホストが書式バーを置く位置に使う |
| `DocumentChanged` / `SelectionChanged` / `EditTextRequested` | event | 文書の変更・選択の変更・本文の編集要求 |

操作の割り当ては次のとおりです。

| 入力 | 動作 |
| --- | --- |
| クリック | 選択（最前面の注釈。 背景は選択解除） |
| **ハンドルの外周をクリック** | サイズ変更の開始（**カーソルが変わる範囲と同じ判定**。 選択は解除されない） |
| ドラッグ（本体） | 移動 |
| ドラッグ（8 方向のハンドル） | サイズ変更 |
| ドラッグ（しっぽの先端のハンドル） | しっぽの移動（本体と独立。 **本体と同じ外観のハンドル**。 上ではカーソルが手の形になる） |
| ダブルクリック / Enter | 本文の編集を始める（その場で編集。 キャレットと選択範囲を本文の上に描く） |
| **右クリック** | **外観・本文の書式・重なり順の編集フライアウト**（選択していない注釈なら先に選択する） |
| Delete | 選択中の注釈を削除（**本文の編集中は 1 文字削除**） |
| Ctrl+Z / Ctrl+Y | 元に戻す・やり直す（本文の編集中は入力を取り消す） |
| Ctrl+B / Ctrl+I / Ctrl+U（編集中） | 選択範囲の太字・斜体・下線を切り替える |
| Alt+Shift+5（編集中） | 選択範囲の取り消し線を切り替える |
| 方向キー（`Shift` 併用） | nudge（1 px / 10 px） |

**注意**: 保存（注釈 JSON・合成画像）は行いません。 ホストが `AnnotationJson` とプラットフォームの描画 API で書き出します。

## 3. `AnnotationEditorControls`（クラス）

注釈の編集 UI で使う行の部品です（右クリックのフライアウトとホストの設定ペインが共有する）。

| メンバー | 種別 | 説明 |
| --- | --- | --- |
| `LabelWidth` | const | 項目名のラベルの幅（DIP。 既定 104） |
| `ColorSelectorWidth` | const | 色の選択の幅（DIP。 既定 220。 **横いっぱいへ広げない**） |
| `Heading(text)` | method | 見出しを作る |
| `Label(text)` | method | 項目名のラベルを作る |
| `ColorRow(label, initial, apply)` | method | 色の行を作る（**`ColorSelectorControl` を使う**。 幅は `ColorSelectorWidth` へ固定する。 `apply` へは検証済みの色だけを渡す） |
| `ColorRow(label, selector, apply)` | method | 構成済みの色の選択を並べた行を作る（タブの構成を呼び出し側が決めるときに使う） |
| `CreateSelector(initial, kind, layout, allPalettes)` | method | 注釈向けに構成した色の選択を作る（`allPalettes` は代表色 1・2 + 標準のタブ。 設定画面向け） |
| `NumberRow(label, initial, minimum, maximum, apply, increment)` | method | 数値の行を作る（`NumericUpDown`） |
| `ChoiceRow(label, options, selectedIndex, apply)` | method | 選択肢の行を作る（`ComboBox`） |
| `Toggle(text, isChecked, apply, bold, italic, underline, strike)` | method | 装飾の切り替え（B / I / U / S）を作る |
| `ToHex(color)` | method | 色を `#AARRGGBB` の文字列へ変換する |

**前提**: `ColorRow` は `Avalonia.Controls.ColorPicker` のテーマを要求します（ホストが
`avares://Avalonia.Controls.ColorPicker/Themes/Fluent/Fluent.xaml` を読み込む）。

## 4. `AnnotationFonts`（クラス）

| メンバー | 種別 | 説明 |
| --- | --- | --- |
| `All` | property | 本文で選べるフォント名の一覧（`IReadOnlyList<string>`）。 `AnnotationDefaults.PreferredFontFamilies` を先頭に置き、その後にインストール済みのフォントを並べる |

一覧は起動時に 1 度だけ作ります（フォントの列挙は重い）。

## 5. `AnnotationPalette`（静的クラス）

注釈の色選択で使う**代表色（色名付きの 16 色 × 2 組）**です。

| メンバー | 種別 | 説明 |
| --- | --- | --- |
| `RepresentativeColors` | property | 代表色 1 の並び（`IReadOnlyList<ColorSelectorCustomColor>`。 無彩色 → 暖色 → 寒色の順） |
| `AccentColors` | property | 代表色 2 の並び（和名の 16 色。 **代表色 1 と重ならない色**） |
| `GetColors(kind)` / `GetTitle(kind)` | method | 組（`AnnotationPaletteKind`）から色の並び・タブの題名（`代表色１` / `代表色２`）を取得する |
| `CreateTab(kind, layout)` | method | 代表色のタブの定義を作る（並べ方は一覧 / 色の四角 6 列） |
| `CreateAllTabs(layout)` | method | 代表色 1 と代表色 2 のタブの定義を作る（設定画面向け） |
| `ConfigureAll(selector, layout)` | method | 色の選択を「代表色 1・代表色 2 + テーマカラー・色名・ColorPicker」の構成にする（設定画面向け。 題名も出す） |
| `GetHex(name)` / `TryGetColor(name, out color)` | method | 色名から保存値（`#AARRGGBB`）・色を取得する |
| `AnnotationPaletteKind` | enum | 代表色の組（`Representative` = 代表色 1 / `Accent` = 代表色 2） |

- **書式バー・右クリックのフライアウト・設定ペインが同じ並びを使います**（画面ごとに色が違わない）。
- 色の選択の入力欄は**「■ + 色名」**（現在色の四角 + 色名）で表示します。
- タブは**選んだ代表色**を出し、テーマ・色名のタブは隠します（タブが 1 つなので題名も出しません）。 **書式バーは「ColorPicker」タブも出します**（右クリックのフライアウトは出しません）。
- 代表色の並べは**色名の一覧**と**色の四角だけを 6 列**から選べます（`AnnotationTextEditor.PaletteLayout`）。
- 16 進や色名を直接打ち込む入力欄は残します（代表色に無い色を指定できる）。
