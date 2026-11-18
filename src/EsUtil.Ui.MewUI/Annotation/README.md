# EsUtil.Ui.MewUI.Annotation

画像の上へ注釈（枠線・吹き出し）を重ねる **MewUI 版のレイヤー コントロール**です。

## 1. 概要

- **`AnnotationTextEditor`**: **本文の表示・その場編集と書式バーを 1 つにまとめたコントロール**です（内側の `Layer` で画像・文書・選択を操作します）。 ホストはこれ 1 つを置けば、本文の編集と書式の変更ができます。
- **`AnnotationLayerControl`**: 画像の表示・注釈の描画・入力の受付を担います（`AnnotationTextEditor.Layer` として使います）。
- **判断は `EsUtil.Feature.Annotation` が単一所有**します（当たり判定・折り返し・描画計画）。 本コントロールは自 UI の描画 API へ写すだけです。
- **選択枠と 8 方向のハンドルは Marquee の提示層を再利用**します（移動・サイズ変更・方向キーの nudge・カーソル形状が UI 版で同じになる）。
- **本文はその場で編集します**（ダブルクリックまたは Enter でキャレットが立ち、表示中のレイアウトのまま入力できます）。 IME（日本語入力）にも対応します。

**色の選択は `EsUtil.Ui.MewUI.ColorSelector` を使います**（自前の色入力を作らない）。
書式バーは**代表色パレット・代表色・カラーピッカー**のタブを出し、テーマ・色名のタブは隠します（`AnnotationPalette.Configure`）。
任意の色を選ばせたいときは `AnnotationPalette.ConfigureAll` を使います。
編集 UI の行の部品は `AnnotationEditorControls` が単一所有し、右クリックのフライアウトとホストの設定ペインが共有します。

**書式（文字単位）は選択範囲への適用**で変わり、**適用するとその場で反映**されます
（右クリックのフライアウト、ホストが置く書式バー）。
描画は run ごとの書式（太字・斜体・下線・色・大きさ・フォント）に対応しています。
**斜体は行の高さとベースラインを変えません**（Avalonia UI 版と同じ規則。 幅は指定どおりの書式で測り、行の高さは斜体を外した書式で測り、描画は行のベースラインで揃える）。

**現状**: MewUI 版のみ実装済みです（Avalonia UI 版は [`../../EsUtil.Ui.AvaloniaUI/Annotation/README.md`](../../EsUtil.Ui.AvaloniaUI/Annotation/README.md)。 WinUI 3 / WinForms は後段）。
下線・取り消し線は MewUI のテキスト描画 API に無いため、**区間の罫線として自前で引きます**（4 つの UI 版で同じ見た目にするため）。

## 2. 使い方

```csharp
// 本文の表示・その場編集と書式バーをまとめたコントロール（これ 1 つを置く）
var editor = new AnnotationTextEditor { Width = 1920, Height = 1080 };
var layer = editor.Layer;
layer.Source = image;           // EsBitmap
layer.SourcePixelWidth = 1920;
layer.SourcePixelHeight = 1080;
layer.Document = new AnnotationDocument(1920, 1080, "shot.png");

// 追加（既定の外観を使う）
layer.AddFrame(new EsRect(60, 60, 340, 220));
layer.AddBubble(new EsRect(900, 120, 380, 170), "ここが重要です。");

// 保存（注釈 JSON と合成画像の書き出し）はホストが行う
var path = AnnotationJson.GetDocumentPath(imagePath);
File.WriteAllText(path, AnnotationJson.Serialize(layer.Document), new UTF8Encoding(false));

// 合成画像はレイヤーの描画を 1:1 で書き出す（選択枠・キャレットは含まない）
using var composed = layer.RenderComposedImage();
composed.Save(AnnotationJson.GetComposedPath(imagePath)); // PNG
```

主な API は次のとおりです。

| メンバー | 説明 |
| --- | --- |
| `AnnotationTextEditor.Layer` / `IsEditingText` | 注釈の表示・編集と書式バーをまとめたコントロール（レイヤーは `Layer` から操作する） |
| `AnnotationTextEditor.TextFormat` | 書式バーと右クリックのフライアウトの色の表記（色名 + 不透明度 / カラーコード）。 カラーコードは `#AARRGGBB` |
| `Source` / `SourcePixelWidth` / `SourcePixelHeight` | 表示する画像と、その画素数（縦横比を保って中央へ表示する） |
| `Document` / `SelectedId` | 注釈の文書と選択中の注釈 |
| `RenderComposedImage()` | 画像と注釈を元画像ピクセルの 1:1 で合成したビットマップ（保存用。 選択枠・キャレットは描かない） |
| `AddFrame(bounds)` / `AddBubble(bounds, text)` | 注釈の追加（既定の外観） |
| `DeleteSelected()` / `SetSelectedText(text)` | 削除と本文の置き換え |
| `SelectedTextMatches(match)` | 選択範囲の**すべての文字**が条件を満たすか（書式バーの押下状態） |
| `ApplyTextStyle(style)` / `ApplyTextStyle(start, length, style)` | 選択範囲（長さ 0 は本文全体）の書式を**丸ごと差し替える** |
| `TransformTextStyle(transform)` / `TransformTextStyle(start, length, transform)` | 範囲内の各 run の書式を**指定した属性だけ変える** |
| `UpdateSelectedAppearance(...)` / `UpdateSelectedTail(...)` / `SetSelectedBodyPadding(...)` | 外観・しっぽ・本文の余白の変更 |
| `MoveSelectedToFront()` / `MoveSelectedToBack()` | 重なり順の変更 |
| `Undo()` / `Redo()` / `CanUndo` / `CanRedo` | 元に戻す・やり直す（1 操作 = 1 手。 ドラッグは押下から離すまでで 1 手） |
| `ShowStyleEditor()` | 外観・重なり順の編集フライアウトを表示する（右クリックと同じ） |
| `BeginTextEditing()` / `EndTextEditing()` / `IsEditingText` | 本文の**その場編集**の開始・終了 |
| `CaretIndex` / `SelectionStart` / `SelectionEnd` / `SelectedTextMatches` | キャレットと選択範囲、選択範囲の書式 |
| `GetSelectedBodyRect()` | 本文の矩形（DIP。 ホストが書式バーを置く位置） |
| `DocumentChanged` / `SelectionChanged` / `EditingChanged` / `TextSelectionChanged` | 変更・選択・編集開始終了の通知 |

**操作**: クリックで選択 / ドラッグで移動 / ハンドルでサイズ変更 / しっぽの先端（円）をドラッグで移動 /
ダブルクリックまたは Enter で本文編集 / **右クリックで外観・重なり順の編集**（本文の書式は書式バーで編集） / Delete で削除 /
方向キーで nudge（`Shift` 併用で 10 px）/ **Ctrl+Z で元に戻す・Ctrl+Y でやり直す**。

**既定値**: `DraftDefaults`（枠線・吹き出し・本文）は新規作成時に使います。 既存の注釈は各注釈が持つ外観・書式で描きます。

## 3. テスト

判断（当たり判定・折り返し・JSON）のテストは [`../../Feature.Annotation.Core`](../../Feature.Annotation.Core/README.md) が持ちます。 手順と合否基準は [`.github/検証手順.md`](../../../.github/検証手順.md) が単一所有します。

## 4. ドキュメント

- コア（モデル・描画計画） … [`../../Feature.Annotation.Core/README.md`](../../Feature.Annotation.Core/README.md)
- Avalonia UI 版（移植元。 操作と描画の規則は同じ） … [`../../EsUtil.Ui.AvaloniaUI/Annotation/README.md`](../../EsUtil.Ui.AvaloniaUI/Annotation/README.md)

## 5. ライセンス

[MIT License](../../../LICENSE)
