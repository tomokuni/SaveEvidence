# EsUtil.Ui.WinUI3.Annotation

画像の上へ注釈（枠線・吹き出し）を重ねる **WinUI 3 版のレイヤー コントロール**です。

## 1. 概要

- **`AnnotationTextEditor`**: **本文の表示・編集と書式バーを 1 つにまとめたコントロール**です（内側の `Layer` で画像・文書・選択を操作します）。 ホストはこれ 1 つを置けば、本文の編集と書式の変更ができます。
- **`AnnotationLayerControl`**: 画像の表示・注釈の描画・入力の受付を担います（`AnnotationTextEditor.Layer` として使います）。
- **判断は `EsUtil.Feature.Annotation` が単一所有**します（当たり判定・折り返し・描画計画）。 本コントロールは XAML の図形と文字へ写すだけです。
- **選択枠と 8 方向のハンドルは Marquee の提示層を再利用**します（移動・サイズ変更・方向キーの nudge が UI 版で同じになる）。
- **本文の編集はその場で行います**: 表示中のレイアウトのままキャレットと選択範囲を描いて入力します（入力欄を重ねない）。 日本語入力（IME）は `CoreTextEditContext` を本文へ繋ぎ、変換中の文字列は文書へ入れずキャレットの位置へ下線付きで描きます。
- **斜体は行の高さとベースラインを変えません**（行の高さは斜体を外した書式で測り、各 run は行の高さいっぱいの中央へ置く）。

**色の選択は `EsUtil.Ui.WinUI3.ColorSelector` を使います**（自前の色入力を作らない）。
書式バーは**代表色パレット・代表色・カラーピッカー**のタブを出し、テーマ・色名のタブは隠します（`AnnotationPalette.Configure`）。

**現状**: 4 つの UI 版すべてで使えます（Avalonia UI 版 / MewUI 版 / WinUI 3 版 / WinForms 版）。

## 2. 使い方

```csharp
// 本文の表示・編集と書式バーをまとめたコントロール（これ 1 つを置く）
var editor = new AnnotationTextEditor();
var layer = editor.Layer;

// 実寸は描くエンジンで測る（UI スレッドで設定する）
layer.MeasureText = AnnotationTextRunMeasure.Measure;

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
| `AnnotationTextEditor.TextFormat` | 書式バーと右クリックのフライアウトの色の表記（色名 + 不透明度 / カラーコード） |
| `AnnotationLayerControl.MeasureText` | 本文の実寸を測る関数（実アプリは `AnnotationTextRunMeasure.Measure`） |
| `Source` / `SourcePixelWidth` / `SourcePixelHeight` | 表示する画像と元画像の画素数 |
| `Document` / `SelectedId` / `Selected` | 注釈の文書と選択 |
| `AddFrame(bounds)` / `AddBubble(bounds, text)` | 注釈の追加（追加した識別子を返す） |
| `DeleteSelected()` / `SetSelectedText(text)` | 削除と本文の置き換え |
| `ApplyTextStyle(...)` / `TransformTextStyle(...)` | 選択範囲の書式の差し替え・属性だけの変更 |
| `SelectedTextMatches(match)` | 選択範囲のすべての文字が条件を満たすか（書式バーの押下状態） |
| `UpdateSelectedAppearance(...)` / `UpdateSelectedTail(...)` / `SetSelectedBodyPadding(...)` | 外観・しっぽ・本文の余白の変更 |
| `MoveSelectedToFront()` / `MoveSelectedToBack()` | 重なり順の変更 |
| `Undo()` / `Redo()` / `CanUndo` / `CanRedo` | 元に戻す・やり直す（1 操作 = 1 手） |
| `BeginTextEditing()` / `EndTextEditing()` | 本文の編集の開始・終了（`RichEditBox` を本文の矩形へ重ねる） |
| `TextSelectionStart` / `TextSelectionLength` / `TextSelectionRange` | 入力欄の選択範囲（書式の適用先） |
| `RenderComposedImage()` | 画像と注釈を 1:1 で合成した画像（保存用。 GDI+ で焼き込む） |
| `DocumentChanged` / `SelectionChanged` / `EditingChanged` / `TextSelectionChanged` | 変更・選択・編集開始終了・選択範囲の通知 |

**操作**: クリックで選択 / ドラッグで移動 / ハンドルでサイズ変更 / しっぽの先端（■）をドラッグで移動 /
ダブルクリックまたは Enter で本文編集 / **右クリックで外観・重なり順の編集** / Delete で削除 /
方向キーで nudge / **Ctrl+Z で元に戻す・Ctrl+Y でやり直す**。

## 3. テスト

判断（当たり判定・折り返し・JSON）のテストは [`../../Feature.Annotation.Core`](../../Feature.Annotation.Core/README.md) が持ちます。 本アセンブリのテストは色の並び・フォントの並び・実寸の近似を確かめます（XAML のランタイムを必要としない範囲）。 手順と合否基準は [`.github/検証手順.md`](../../../.github/検証手順.md) が単一所有します。

## 4. ドキュメント

- API リファレンス … [`docs/SPEC_API.md`](docs/SPEC_API.md)
- システム仕様書 … [`docs/SPEC_System.md`](docs/SPEC_System.md)
- コア（モデル・描画計画） … [`../../Feature.Annotation.Core/README.md`](../../Feature.Annotation.Core/README.md)
- 操作と描画の規則（移植元） … [`../../EsUtil.Ui.AvaloniaUI/Annotation/README.md`](../../EsUtil.Ui.AvaloniaUI/Annotation/README.md)

## 5. ライセンス

[MIT License](../../../LICENSE)
