# EsUtil.Ui.AvaloniaUI.Annotation サンプルアプリケーション

注釈レイヤーの**動作と外観を確認する**ためのサンプルです（保存は行いません）。

## 1. 概要

市松模様のサンプル画像の上に、枠線 1 件と吹き出し 1 件を置いた状態で起動します。

- **枠線**: 赤い枠・角丸（内側は塗らない）。
- **吹き出し**: 白い内側・黒い枠・しっぽ（吹き出し口）。 本文の一部だけ太字 + 赤色。

## 2. 使い方

```powershell
dotnet run --project src/EsUtil.Ui.AvaloniaUI/Annotation/sample/EsUtil.Ui.AvaloniaUI.Annotation.Sample.csproj
```

| 操作 | 確認できること |
| --- | --- |
| 枠線・吹き出しをクリック | 選択（**8 方向のハンドル**が Marquee の提示層で描かれる） |
| 本体をドラッグ | 移動（Marquee の通知が文書へ書き戻る） |
| ハンドルをドラッグ | サイズ変更 |
| しっぽの先端をドラッグ | 吹き出し口の移動（本体と独立） |
| ダブルクリック / Enter | 本文の編集（本文の矩形に入力欄が重なる） |
| 「枠線を追加」「吹き出しを追加」 | 新しい注釈の追加 |
| Delete | 選択中の注釈の削除 |
| 方向キー | nudge（1 px。 `Shift` 併用で 10 px） |

## 3. テスト

本サンプルは**目視の確認**用です（自動テストは持ちません）。
判断（当たり判定・折り返し・JSON）のテストは [`../../../Feature.Annotation.Core`](../../../Feature.Annotation.Core/README.md) が持ちます。
手順と合否基準は [`.github/検証手順.md`](../../../../.github/検証手順.md) が単一所有します。

## 4. ドキュメント

- コントロールの API … [`../docs/SPEC_API.md`](../docs/SPEC_API.md)
- システム仕様書 … [`../docs/SPEC_System.md`](../docs/SPEC_System.md)
- コア（モデル・描画計画） … [`../../../Feature.Annotation.Core/README.md`](../../../Feature.Annotation.Core/README.md)

## 5. ライセンス

[MIT License](../../../../LICENSE)
