# EsUtil.Ui.AvaloniaUI.Annotation

画像の上へ注釈（枠線・吹き出し）を重ねる **Avalonia UI 版のレイヤー コントロール**です。

## 1. 概要

- **`AnnotationTextEditor`**: **本文の表示・その場編集と書式バーを 1 つにまとめたコントロール**です（内側の `Layer` で画像・文書・選択を操作します）。 ホストはこれ 1 つを置けば、本文の編集と書式の変更ができます。
- **`AnnotationLayerControl`**: 画像の表示・注釈の描画・入力の受付を担います（`AnnotationTextEditor.Layer` として使います）。
- **判断は `EsUtil.Feature.Annotation` が単一所有**します（当たり判定・折り返し・描画計画）。 本コントロールは自 UI の描画 API へ写すだけです。
- **選択枠と 8 方向のハンドルは Marquee の提示層を再利用**します（移動・サイズ変更・方向キーの nudge・カーソル形状が 4 つの UI 版で同じになる）。
- **本文はその場で編集します**（ダブルクリックまたは Enter でキャレットが立ち、表示中のレイアウトのまま入力できます）。 IME（日本語入力）にも対応します。

**現状**: 本文は**入力欄を重ねずにその場で編集**します（キャレット・選択範囲を本文の上に描く。 IME 対応）。
**書式（文字単位）は選択範囲への適用**で変わり、**適用するとその場で反映**されます
（右クリックのフライアウト、ホストが置く書式バー）。
描画は run ごとの書式（太字・斜体・下線・取り消し線・色・大きさ・フォント）に対応しています。

**色の選択は `EsUtil.Ui.AvaloniaUI.ColorSelector` を使います**（自前の色入力を作らない）。
入力欄は**「■ + 色名」**（現在色の四角 + 色名）で表示します。
書式バーは**代表色 16 色（色名付き）と ColorPicker** のタブ、右クリックのフライアウトは**代表色 16 色**のタブを出します（テーマ・色名のタブは隠す。 タブが 1 つなら題名も出さない）。
代表色の並べは**色名の一覧**と**色の四角だけを 6 列**から選べます（`AnnotationTextEditor.PaletteLayout`）。
編集 UI の行の部品は `AnnotationEditorControls` が単一所有し、右クリックのフライアウトとホストの設定ペインが共有します。

**現状（対応範囲）**: 4 つの UI 版のうち Avalonia UI 版のみです（MewUI / WinUI 3 / WinForms は後段）。

## 2. 使い方

```csharp
// 本文の表示・その場編集と書式バーをまとめたコントロール（これ 1 つを置く）
var editor = new AnnotationTextEditor { Width = 1920, Height = 1080 };
var layer = editor.Layer;
layer.Source = image;           // IImage（例: EsBitmapAdapter）
layer.SourcePixelWidth = 1920;
layer.SourcePixelHeight = 1080;
layer.Document = new AnnotationDocument(1920, 1080, "shot.png");

// 追加（既定の外観を使う）
layer.AddFrame(new EsRect(60, 60, 340, 220));
layer.AddBubble(new EsRect(900, 120, 380, 170), "ここが重要です。");

// 本文の編集（書式バーは AnnotationTextEditor が出し入れする）
layer.BeginTextEditing();
layer.EndTextEditing();

// 保存（注釈 JSON と合成画像の書き出し）はホストが行う
var path = AnnotationJson.GetDocumentPath(imagePath);
File.WriteAllText(path, AnnotationJson.Serialize(layer.Document), new UTF8Encoding(false));
```

主な API は次のとおりです。

| メンバー | 説明 |
| --- | --- |
| `AnnotationTextEditor.Layer` / `IsEditingText` | 本文の表示・その場編集と書式バーをまとめたコントロール（レイヤーは `Layer` から操作する） |
| `AnnotationTextEditor.PaletteLayout` | 書式バーの代表色の並べ方（`List` = 色名の一覧 / `Grid` = 色の四角だけを 6 列） |
| `Source` / `SourcePixelWidth` / `SourcePixelHeight` | 表示する画像と、その画素数（縦横比を保って中央へ表示する） |
| `Document` / `SelectedId` | 注釈の文書と選択中の注釈 |
| `AddFrame(bounds)` / `AddBubble(bounds, text)` | 注釈の追加（既定の外観） |
| `DeleteSelected()` / `SetSelectedText(text)` | 削除と本文の置き換え |
| `TextSelectionRange` / `SelectedTextStyle` | 書式を適用する範囲（文字数）と、その位置の書式 |
| `SelectedTextMatches(match)` | 選択範囲の**すべての文字**が条件を満たすか（書式バーの押下状態。 選択が無ければキャレットの位置で判定） |
| `ApplyTextStyle(style)` / `ApplyTextStyle(start, length, style)` | 選択範囲（長さ 0 は本文全体）の書式を**丸ごと差し替える** |
| `TransformTextStyle(transform)` / `TransformTextStyle(start, length, transform)` | 範囲内の各 run の書式を**指定した属性だけ変える**（色や大きさを保つ。 B / I / U / S の切り替えはこちら） |
| `UpdateSelectedAppearance(...)` / `UpdateSelectedTail(...)` / `SetSelectedBodyPadding(...)` | 外観・しっぽ・本文の余白の変更 |
| `MoveSelectedToFront()` / `MoveSelectedToBack()` | 重なり順の変更 |
| `Undo()` / `Redo()` / `CanUndo` / `CanRedo` | 元に戻す・やり直す（1 操作 = 1 手。 ドラッグは押下から離すまでで 1 手） |
| `ShowStyleEditor()` | 外観・重なり順の編集フライアウトを表示する（右クリックと同じ。 本文の書式は書式バーで編集する） |
| `BeginTextEditing()` / `EndTextEditing()` / `IsEditingText` | 本文の**その場編集**の開始・終了 |
| `CaretIndex` / `SelectionStart` / `SelectionEnd` / `EditingChanged` | キャレットと選択範囲、編集の開始・終了の通知 |
| `TextSelectionChanged` | 本文の選択範囲（キャレット）が変わった通知（書式バーの現在値を追随させる） |
| `GetSelectedBodyRect()` | 本文の矩形（DIP。 ホストが書式バーを置く位置） |
| `DocumentChanged` / `SelectionChanged` | 変更・選択の通知 |

**文字単位の書式**: 本文を編集中に文字を選び、書式バー（ホストが置く）か `Ctrl+B` / `Ctrl+I` / `Ctrl+U`（取り消し線は `Alt+Shift+5`）を押します。
選択が無ければ本文全体へ適用します。 書式バーの装飾ボタンは、**選択範囲のすべてがその装飾のときだけ押された状態**になります（選択を変えると追随します）。
色は**一覧で同じ色を選び直しても適用**されます（文字単位で混ざった色を一律にそろえるため）。
書式バーは `AnnotationTextEditor` が出し入れします（ホストは何も置かなくてよい）。

**操作**: クリックで選択 / ドラッグで移動 / ハンドルでサイズ変更 / しっぽの先端（円）をドラッグで移動 /
ダブルクリックまたは Enter で本文編集 / **右クリックで外観・重なり順の編集**（本文の書式は書式バーで編集） / Delete で削除 /
方向キーで nudge（`Shift` 併用で 10 px）/ **Ctrl+Z で元に戻す・Ctrl+Y でやり直す**。

**編集 UI の部品**: `AnnotationEditorControls`（見出し・項目名・色・数値・選択肢・装飾の切り替え）は
フライアウトとホストの設定ペインが共有します。 `AnnotationFonts.All` は選べるフォントの一覧
（`AnnotationDefaults.PreferredFontFamilies` を先頭に置く）です。 色の行は `ColorSelectorControl` を使うため、
ホストは `Avalonia.Controls.ColorPicker/Themes/Fluent/Fluent.xaml` を読み込む必要があります。
色の行の幅は `AnnotationEditorControls.ColorSelectorWidth` で固定します（横いっぱいへ広げると 1 行が間延びします）。

**既定値**: `DraftDefaults`（枠線・吹き出し・本文）は新規作成時に使います。 既存の注釈は各注釈が持つ外観・書式で描きます。

## 3. テスト

UI の見た目と操作はサンプルで確認します（`sample/`）。

```powershell
dotnet run --project src/EsUtil.Ui.AvaloniaUI/Annotation/sample/EsUtil.Ui.AvaloniaUI.Annotation.Sample.csproj
```

判断（当たり判定・折り返し・JSON）のテストは [`../../Feature.Annotation.Core`](../../Feature.Annotation.Core/README.md) が持ちます。 手順と合否基準は [`.github/検証手順.md`](../../../.github/検証手順.md) が単一所有します。

## 4. ドキュメント

- API リファレンス … [`docs/SPEC_API.md`](docs/SPEC_API.md)
- システム仕様書 … [`docs/SPEC_System.md`](docs/SPEC_System.md)
- サンプル … [`sample/README.md`](sample/README.md)
- コア（モデル・描画計画） … [`../../Feature.Annotation.Core/README.md`](../../Feature.Annotation.Core/README.md)

## 5. ライセンス

[MIT License](../../../LICENSE)
