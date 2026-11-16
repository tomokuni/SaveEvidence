# EsUtil.Ui.AvaloniaUI.Annotation

画像の上へ注釈（枠線・吹き出し）を重ねる **Avalonia UI 版のレイヤー コントロール**です。

## 1. 概要

- **`AnnotationLayerControl`**: 画像の表示・注釈の描画・入力の受付を担います。
- **判断は `EsUtil.Feature.Annotation` が単一所有**します（当たり判定・折り返し・描画計画）。 本コントロールは自 UI の描画 API へ写すだけです。
- **選択枠と 8 方向のハンドルは Marquee の提示層を再利用**します（移動・サイズ変更・方向キーの nudge・カーソル形状が 4 つの UI 版で同じになる）。
- **本文の編集はホストが行います**（ダブルクリックで `EditTextRequested` が発生し、ホストが入力欄を重ねる）。

**現状**: 本文の編集は単一書式です（文字単位の書式の適用は後段。 描画は run ごとの書式に対応済み）。

## 2. 使い方

```csharp
var layer = new AnnotationLayerControl
{
    Source = image,             // IImage（例: EsBitmapAdapter）
    SourcePixelWidth = 1920,
    SourcePixelHeight = 1080,
    Document = new AnnotationDocument(1920, 1080, "shot.png"),
};

// 追加（既定の外観を使う）
layer.AddFrame(new EsRect(60, 60, 340, 220));
layer.AddBubble(new EsRect(900, 120, 380, 170), "ここが重要です。");

// 編集の要求（ホストが入力欄を重ね、確定した文字列を戻す）
layer.EditTextRequested += (_, _) => ShowEditor(layer.GetSelectedBodyRect());
layer.SetSelectedText("新しい本文");

// 保存（注釈 JSON と合成画像の書き出し）はホストが行う
var path = AnnotationJson.GetDocumentPath(imagePath);
File.WriteAllText(path, AnnotationJson.Serialize(layer.Document), new UTF8Encoding(false));
```

主な API は次のとおりです。

| メンバー | 説明 |
| --- | --- |
| `Source` / `SourcePixelWidth` / `SourcePixelHeight` | 表示する画像と、その画素数（縦横比を保って中央へ表示する） |
| `Document` / `SelectedId` | 注釈の文書と選択中の注釈 |
| `AddFrame(bounds)` / `AddBubble(bounds, text)` | 注釈の追加（既定の外観） |
| `DeleteSelected()` / `SetSelectedText(text)` | 削除と本文の置き換え |
| `GetSelectedBodyRect()` | 本文の矩形（DIP。 ホストが入力欄を重ねる位置） |
| `DocumentChanged` / `SelectionChanged` / `EditTextRequested` | 変更・選択・編集要求の通知 |

**操作**: クリックで選択 / ドラッグで移動 / ハンドルでサイズ変更 / しっぽの先端をドラッグで移動 / ダブルクリックまたは Enter で本文編集 / Delete で削除 / 方向キーで nudge（`Shift` 併用で 10 px）。

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
