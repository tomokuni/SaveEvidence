# EsUtil.Ui.AvaloniaUI.Annotation API リファレンス

## 目次

- [1. `AnnotationLayerControl`（クラス）](#1-annotationlayercontrolクラス)

## 1. `AnnotationLayerControl`（クラス）

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
| `AddFrame(bounds)` / `AddBubble(bounds, text)` | method | 注釈を追加し、追加した注釈を選択する（戻り値は識別子） |
| `DeleteSelected()` | method | 選択中の注釈を削除する |
| `SetSelectedText(text)` | method | 選択中の吹き出しの本文を置き換える（**書式は文字位置で引き継ぐ**） |
| `ApplyTextStyle(style)` / `UpdateTextStyle(transform)` | method | 本文へ書式を適用する（`TextSelectionRange` の範囲。 **文字単位**） |
| `UpdateSelectedAppearance(transform)` | method | 外観（枠線・内側・角丸）を差し替える |
| `SetSelectedBodyPadding(padding)` / `UpdateSelectedTail(transform)` | method | 吹き出しの本文の余白・しっぽ（幅・辺）を差し替える |
| `MoveSelectedToFront()` / `MoveSelectedToBack()` | method | 重なり順を変える |
| `ShowStyleEditor()` | method | 外観・書式・重なり順の編集フライアウトを表示する（右クリックと同じ） |
| `GetSelectedBodyRect()` | method | 本文の矩形（DIP）。 ホストが入力欄を重ねる位置に使う |
| `DocumentChanged` / `SelectionChanged` / `EditTextRequested` | event | 文書の変更・選択の変更・本文の編集要求 |

操作の割り当ては次のとおりです。

| 入力 | 動作 |
| --- | --- |
| クリック | 選択（最前面の注釈。 背景は選択解除） |
| **ハンドルの外周をクリック** | サイズ変更の開始（**カーソルが変わる範囲と同じ判定**。 選択は解除されない） |
| ドラッグ（本体） | 移動 |
| ドラッグ（8 方向のハンドル） | サイズ変更 |
| ドラッグ（しっぽの先端の円） | しっぽの移動（本体と独立。 円の上ではカーソルが手の形になる） |
| ダブルクリック / Enter | 本文の編集要求 |
| **右クリック** | **外観・本文の書式・重なり順の編集フライアウト**（選択していない注釈なら先に選択する） |
| Delete | 選択中の注釈を削除 |
| 方向キー（`Shift` 併用） | nudge（1 px / 10 px） |

**注意**: 保存（注釈 JSON・合成画像）は行いません。 ホストが `AnnotationJson` とプラットフォームの描画 API で書き出します。
