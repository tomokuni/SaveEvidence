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
| `SelectedId` | property | 選択中の注釈の識別子（null は未選択） |
| `AddFrame(bounds)` / `AddBubble(bounds, text)` | method | 注釈を追加し、追加した注釈を選択する（戻り値は識別子） |
| `DeleteSelected()` | method | 選択中の注釈を削除する |
| `SetSelectedText(text)` | method | 選択中の吹き出しの本文を置き換える（書式は先頭 run を引き継ぐ） |
| `GetSelectedBodyRect()` | method | 本文の矩形（DIP）。 ホストが入力欄を重ねる位置に使う |
| `DocumentChanged` | event | 文書が変わった（移動・サイズ変更・本文の変更を含む） |
| `SelectionChanged` | event | 選択が変わった |
| `EditTextRequested` | event | 本文の編集が要求された（ダブルクリック・Enter） |

操作の割り当ては次のとおりです。

| 入力 | 動作 |
| --- | --- |
| クリック | 選択（最前面の注釈。 背景は選択解除） |
| ドラッグ（本体） | 移動 |
| ドラッグ（8 方向のハンドル） | サイズ変更 |
| ドラッグ（しっぽの先端） | しっぽの移動（本体と独立） |
| ダブルクリック / Enter | 本文の編集要求 |
| Delete | 選択中の注釈を削除 |
| 方向キー（`Shift` 併用） | nudge（1 px / 10 px） |

**注意**: 保存（注釈 JSON・合成画像）は行いません。 ホストが `AnnotationJson` とプラットフォームの描画 API で書き出します。
