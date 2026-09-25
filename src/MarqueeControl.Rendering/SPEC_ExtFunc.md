# MarqueeControl.Rendering 外部機能仕様書

`MarqueeControl.Rendering`（`MarqueeControl` の UI 非依存の描画層）が提供する外部機能と公開 API の仕様です。利用者（UI 実装・ホスト・独自コントロールの作者）から見える情報のみを記載し、内部実装の詳細は記載しません。設計の意図は [README](README.md)、構造と依存は [SPEC_System](SPEC_System.md) を参照してください。

## 1. 提供機能

| 機能 | 提供元 | 説明 |
| --- | --- | --- |
| 描画内容の描画（図形） | `IMarqueeRenderer.DrawShapes`（既定実装 `MarqueeRenderer.Default`） | 装飾・ハンドル・始点マーカーを描画順に描く（**ラベルの層は描かない**。ラベルは UI の要素が描く） |
| ラベル要素の配置 | `IMarqueeRenderer.ResolveLabels`（既定実装は `MarqueeLabelPlan.Resolve` へ委譲） | 実寸（描画 API の測定値）からラベル要素の配置指示（`EsLabelElement`）を解決する |
| ホスト付加情報ラベルの描画 | `MarqueeHostLabelRenderer.Draw` | ホストが与えた文言を、サイズラベルと同じ体裁（内側余白・背景 → 文字の順序）で描く |

### 前提条件

- 描画先は `IEsCanvas`（`EsUtil.Platform.Core.Imaging`）です。Skia のキャンバスを使う場合は `SkiaEsCanvas`、Avalonia の `DrawingContext` を使う場合は `AvaloniaCanvas`（`Platform.AvaloniaUI`）を使います。
- 描画の単位は**アダプタの座標系**に従います（マーキーは DIP、ルーペは物理ピクセル）。単位の換算（DPI スケーリング）はアダプタの利用側が行います。
- 描画面の消去は呼び出し側の責務です（フレームワークが消去する UI では不要）。
- 本層は色・寸法・文言を保持しません（すべてコアの契約が単一所有します）。

## 2. 公開 API 一覧

### `EsUtil.SaveEvidence.Marquee.Rendering`

| 型 | 種別 | 説明 |
| --- | --- | --- |
| `MarqueeRenderer` | sealed class | 描画手順の単一所有者（`IMarqueeRenderer`（`EsUtil.Feature.MarqueeCore`）の**既定実装**）。`Default` で唯一のインスタンスを共有する |
| `MarqueeOuterRenderer` | static class | 選択矩形の外側の装飾の描画 |
| `MarqueeInnerRenderer` | static class | 穴あけ矩形の内側の装飾の描画 |
| `MarqueeLabelPlan` | static class | ラベル要素の配置指示の解決 |
| `MarqueeHostLabelRenderer` | static class | ホスト付加情報ラベルの描画 |

### `MarqueeRenderer` のメンバー（`IMarqueeRenderer` の実装）

| メンバー | 説明 |
| --- | --- |
| `Default` | 既定の描画実装（唯一のインスタンス。状態を持たないためファサード間で共有できる）。生成は非公開（`new` できない） |
| `DrawShapes(IEsCanvas canvas, in MarqueeDrawContent content)` | 図形の層（装飾・ハンドル・始点マーカー）を描画順に描く（**ラベルの層は描かない**）。`canvas` が null なら `ArgumentNullException` |
| `ResolveLabels(in MarqueeDrawContent content, Func<string, double, EsSize> measureText)` | 配置するラベル要素を順序どおりに返す（`MarqueeLabelPlan.Resolve` へ委譲する）。`measureText` が null なら `ArgumentNullException` |

### `MarqueeOuterRenderer` / `MarqueeInnerRenderer` のメンバー

| メンバー | 説明 |
| --- | --- |
| `MarqueeOuterRenderer.Draw(IEsCanvas canvas, in OuterDecoFrame frame)` | 暗転領域（`OverlayRegions`）を暗転色で塗り、枠線（`BorderRects`）を描く（空のコレクションは何もしない） |
| `MarqueeInnerRenderer.Draw(IEsCanvas canvas, in InnerDecoFrame frame)` | 塗り矩形（`FillRect`。null なら何もしない）を塗り、枠線（`BorderRects`）を描く |

### `MarqueeLabelPlan` のメンバー

| メンバー | 説明 |
| --- | --- |
| `Resolve(in MarqueeDrawContent content, Func<string, double, EsSize> measureText)` | 配置するラベル要素（`EsLabelElement`）の一覧を**順序どおり**（サイズラベル → カーソル座標ラベル）で返す。どちらも無い場合は空（一覧を生成しない）。`measureText` が null なら `ArgumentNullException` |

### `MarqueeHostLabelRenderer` のメンバー

| メンバー | 説明 |
| --- | --- |
| `Draw(IEsCanvas canvas, EsPoint anchor, string text, double fontSize, DrawingColor textColor, DrawingColor backgroundColor, double padding)` | 基準点（背景の左上に置く座標）へ 1 行のラベルを描く。`canvas` または `text` が null なら `ArgumentNullException` |

## 3. 契約（利用者が守る・受け取る値）

### 描画の呼び分け

| 描く対象 | 呼ぶ API |
| --- | --- |
| 図形（装飾・ハンドル・始点マーカー） | `DrawShapes`（**ラベルの層は描かない**） |
| ラベル（サイズ・カーソル座標） | `DrawShapes` ＋ `ResolveLabels`（`Marquee.ResolveLabels` へ同じフレームの描画内容を渡す。UI の要素が描く） |

### ラベル要素（`EsLabelElement`）

| 項目 | 内容 |
| --- | --- |
| `Bounds` | 要素を置く矩形（背景の大きさ = 文字の実寸 + 内側余白 × 2） |
| `TextOrigin` | 文字の描画原点（背景の内側余白の分だけ内側） |
| `PaddingX` / `PaddingY` | 要素の内側余白（`TextOrigin` - `Bounds`。要素の `Padding` に写す） |
| `Text` / `FontSize` / `TextColor` / `BackgroundColor` / `CornerRadius` | 文言・フォントサイズ・色・角丸（コアが解決済み。UI は判断しない） |

### 付加情報ラベルの体裁

- 背景は基準点（`anchor`）を左上とし、文字の実寸へ内側余白（`padding`）を四方へ加えた大きさになります。
- 文字は同じ余白だけ内側へ入ります（**背景の四方の余白が等しい**）。
- 縦積みの行送りは `EsLabelLayout.NextTop` を使います（背景の下端と次の行の上端が一致します）。
- 文言・表示可否はホストが判断します（本層は文言を生成しません）。

## 4. 制限事項

- 描画の補間方式（画像の拡大）は描画ポートの実装に依存します（最近傍 / 線形）。
- 文字の折り返し・複数行・文字揃えは扱いません（1 行の左揃えのみ）。
- 破線・クリップ・不透明度の指定は扱いません（描画ポートの契約に含めない）。
- アンチエイリアスの有無は描画ポートの実装に依存します。

## 5. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
