# EsUtil.Ui.WinForms.Annotation

画像の上へ注釈（枠線・吹き出し）を重ねる **WinForms 版のレイヤー コントロール**です。

## 1. 概要

- **`AnnotationTextEditor`**: **本文の表示・編集と書式バーを 1 つにまとめたコントロール**です（内側の `Layer` で画像・文書・選択を操作します）。 ホストはこれ 1 つを置けば、本文の編集と書式の変更ができます。
- **`AnnotationLayerControl`**: 画像の表示・注釈の描画・入力の受付を担います（`AnnotationTextEditor.Layer` として使います）。
- **判断は `EsUtil.Feature.Annotation` が単一所有**します（当たり判定・折り返し・描画計画）。 本コントロールは GDI+ へ写すだけです。
- **選択枠と 8 方向のハンドルは Marquee の提示層を再利用**します（移動・サイズ変更・方向キーの nudge が UI 版で同じになる）。
- **本文の編集はリッチ テキストの入力欄を本文の矩形へ重ねて行います**: WinForms の `RichTextBox` が IME・選択・書式の適用・クリップボードを担い、入力のたびに本文と文字単位の書式をコアのモデルへ写します。
- **斜体は行の高さを変えません**（行の高さはフォントの行送りから求め、各 run は行の高さいっぱいの中央へ置く）。

**色の選択は代表色のメニューと色ダイアログで行います**（WinForms には色選択コントロールが無いため）。
代表色は 40 色で、4 つの UI 版と同じ色名・同じ値です（`AnnotationPalette`）。

**現状**: 4 つの UI 版すべてで使えます（Avalonia UI 版 / MewUI 版 / WinUI 3 版 / WinForms 版）。

## 2. 使い方

```csharp
// 本文の表示・編集と書式バーをまとめたコントロール（これ 1 つを置く）
var editor = new AnnotationTextEditor { Dock = DockStyle.Fill };
var layer = editor.Layer;
layer.Source = image;           // EsReadOnlyBitmap
layer.SourcePixelWidth = 1920;
layer.SourcePixelHeight = 1080;
layer.Document = new AnnotationDocument(1920, 1080, "shot.png");

// 追加（既定の外観を使う）
layer.AddFrame(new EsRect(60, 60, 340, 220));
layer.AddBubble(new EsRect(900, 120, 380, 170), "ここが重要です。");

// 保存（注釈 JSON と合成画像の書き出し）はホストが行う
var path = AnnotationJson.GetDocumentPath(imagePath);
File.WriteAllText(path, AnnotationJson.Serialize(layer.Document), new UTF8Encoding(false));

using var composed = layer.RenderComposedImage();
composed?.Save(AnnotationJson.GetComposedPath(imagePath)); // PNG
```

主な API は次のとおりです。

| メンバー | 説明 |
| --- | --- |
| `AnnotationTextEditor.Layer` / `IsEditingText` | 注釈の表示・編集と書式バーをまとめたコントロール |
| `AnnotationTextEditor.TextFormat` | 書式バーの色の表記（色のメニューは表記を持たないため保持だけする） |
| `AnnotationLayerControl.Source` / `SourcePixelWidth` / `SourcePixelHeight` | 表示する画像と元画像の画素数 |
| `Document` / `SelectedId` / `Selected` | 注釈の文書と選択 |
| `AddFrame(bounds)` / `AddBubble(bounds, text)` | 注釈の追加（追加した識別子を返す） |
| `DeleteSelected()` / `SetSelectedText(text)` | 削除と本文の置き換え |
| `ApplyTextStyle(...)` / `TransformTextStyle(...)` | 選択範囲の書式の差し替え・属性だけの変更 |
| `SelectedTextMatches(match)` | 選択範囲のすべての文字が条件を満たすか |
| `UpdateSelectedAppearance(...)` / `UpdateSelectedTail(...)` / `SetSelectedBodyPadding(...)` | 外観・しっぽ・本文の余白の変更 |
| `MoveSelectedToFront()` / `MoveSelectedToBack()` | 重なり順の変更 |
| `Undo()` / `Redo()` / `CanUndo` / `CanRedo` | 元に戻す・やり直す（1 操作 = 1 手） |
| `BeginTextEditing()` / `EndTextEditing()` / `IsEditingText` | 本文の編集の開始・終了（`RichTextBox` を重ねる） |
| `TextSelectionStart` / `TextSelectionLength` / `TextSelectionRange` | 入力欄の選択範囲 |
| `RenderComposedImage()` | 画像と注釈を 1:1 で合成した画像（保存用。 GDI+ で焼き込む） |
| `DocumentChanged` / `SelectionChanged` / `EditingChanged` / `TextSelectionChanged` | 変更・選択・編集開始終了・選択範囲の通知 |

**操作**: クリックで選択 / ドラッグで移動 / ハンドルでサイズ変更 / しっぽの先端（■）をドラッグで移動 /
ダブルクリックまたは Enter で本文編集 / **右クリックで外観・重なり順の編集** / Delete で削除 /
方向キーで nudge / **Ctrl+Z で元に戻す・Ctrl+Y でやり直す**。

## 3. テスト

判断（当たり判定・折り返し・JSON）のテストは [`../../Feature.Annotation.Core`](../../Feature.Annotation.Core/README.md) が持ちます。 本アセンブリのテストはコントロールとコアの接続・合成画像・実寸の測定を確かめます。 手順と合否基準は [`.github/検証手順.md`](../../../.github/検証手順.md) が単一所有します。

## 4. ドキュメント

- API リファレンス … [`docs/SPEC_API.md`](docs/SPEC_API.md)
- システム仕様書 … [`docs/SPEC_System.md`](docs/SPEC_System.md)
- コア（モデル・描画計画） … [`../../Feature.Annotation.Core/README.md`](../../Feature.Annotation.Core/README.md)
- 操作と描画の規則（移植元） … [`../../EsUtil.Ui.AvaloniaUI/Annotation/README.md`](../../EsUtil.Ui.AvaloniaUI/Annotation/README.md)

## 5. ライセンス

[MIT License](../../../LICENSE)
