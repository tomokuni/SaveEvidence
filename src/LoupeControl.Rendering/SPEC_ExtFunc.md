# LoupeControl.Rendering 外部機能仕様書

`LoupeControl.Rendering`（`LoupeControl` の UI 非依存の描画層）が提供する外部機能と公開 API の仕様です。利用者（UI 実装・ホスト・独自コントロールの作者）から見える情報のみを記載し、内部実装の詳細は記載しません。設計の意図は [README](README.md)、構造と依存は [SPEC_System](SPEC_System.md) を参照してください。

## 1. 提供機能

| 機能 | 提供元 | 説明 |
| --- | --- | --- |
| ルーペの描画（図形） | `ILoupeRenderer.DrawShapes`（既定実装 `LoupeRenderer.Default`） | 下地・市松模様・拡大画像・十字線・外枠を描画順に描く（**ラベルの層は描かない**。ラベルは UI の要素が描く） |
| ラベル要素の配置 | `ILoupeRenderer.ResolveLabels`（既定実装は `LoupeLabelPlan.Resolve` へ委譲） | 実寸（描画 API の測定値）からラベル要素の配置指示（`EsLabelElement`）を解決する |

### 前提条件

- 描画先は `IEsCanvas`（`EsUtil.Platform.Core.Imaging`）です。Skia は `SkiaEsCanvas`、Avalonia の `DrawingContext` は `AvaloniaCanvas`（`Platform.AvaloniaUI`）を使います。
- 描画の単位は**物理ピクセル**です（`LoupeRenderRequest.PixelSize` が作る正方形が描画面）。DIP → 物理ピクセルの換算はコアが済ませています。
- 描画面の消去は下地の層（`LoupeDrawLayer.Background`）が行います（前フレームの残像を残しません）。
- 拡大元画像は UI ごとに型が異なるため引数で渡します（本層は `EsBitmap` のみを受け取ります）。

## 2. 公開 API 一覧

### `EsUtil.SaveEvidence.Loupe.Rendering`

| 型 | 種別 | 説明 |
| --- | --- | --- |
| `LoupeRenderer` | sealed class | 描画手順の単一所有者（`ILoupeRenderer`（`EsUtil.Feature.LoupeCore`）の**既定実装**）。`Default` で唯一のインスタンスを共有する |
| `LoupeLabelPlan` | static class | ラベル要素の配置指示の解決 |

### `LoupeRenderer` のメンバー（`ILoupeRenderer` の実装）

| メンバー | 説明 |
| --- | --- |
| `Default` | 既定の描画実装（唯一のインスタンス。状態を持たないためファサード間で共有できる）。生成は非公開（`new` できない） |
| `DrawShapes(IEsCanvas canvas, EsBitmap? source, in LoupeRenderRequest request, in LoupeRenderNotification notification)` | 層の順に図形を描く（層の順序は通知から読む。**ラベルの層は描かない**）。`source` が null または拡大中心が未設定の場合は拡大画像を描かない。`canvas` または通知の層が null なら `ArgumentNullException` |
| `ResolveLabels(LoupeCoordLabel? coordLabel, Func<string, double, EsSize> measureText)` | 配置するラベル要素を 1 件返す（`LoupeLabelPlan.Resolve` へ委譲する）。`measureText` が null なら `ArgumentNullException` |

### `LoupeLabelPlan` のメンバー

| メンバー | 説明 |
| --- | --- |
| `Resolve(LoupeCoordLabel? coordLabel, Func<string, double, EsSize> measureText)` | 配置するラベル要素（`EsLabelElement`）を 1 件返す（ラベルが無い場合は空）。`measureText` が null なら `ArgumentNullException` |

## 3. 契約（利用者が守る・受け取る値）

### 描画の呼び分け

| 描く対象 | 呼ぶ API |
| --- | --- |
| 図形（下地・市松模様・拡大画像・十字線・外枠） | `DrawShapes`（**ラベルの層は描かない**） |
| ラベル（中心座標） | `DrawShapes` ＋ `ResolveLabels`（ファサードの `Loupe.ResolveLabels` がラベル契約まで解決する。UI の要素が描く） |

### 描画面（`LoupeRenderRequest`）

| 項目 | 内容 |
| --- | --- |
| `PixelSize` | 一辺の物理ピクセル。描画面は左上原点の `PixelSize` 正方形 |
| `SurfaceBounds` | その正方形の矩形（下地・市松模様が塗る範囲） |
| `ZoomLevel` | 拡大率（十字線の交点と切り出し領域に使う） |
| `Appearance` | 外観（描画可否は `Has*` が判定済み） |
| `Scaling` | 外観の DIP 値 → 物理ピクセルの換算係数（換算はコアが済ませている） |

### 層（`LoupeDrawLayer`）

| 層 | 内容 |
| --- | --- |
| `Background` | 下地（`Clear` で描画面全体を置き換える） |
| `Checkerboard` | 市松模様（明色で全面を塗り、暗色タイルを列挙して塗る） |
| `Image` | 拡大画像（中心の周囲を切り出して拡大描画） |
| `Crosshair` | 十字線（切り出し領域の中央） |
| `Frame` | 外枠（4 辺） |
| `CoordLabel` | 中心座標ラベル（**本層の `DrawShapes` は描かない**。UI の要素が描く） |

## 4. 制限事項

- 拡大の補間方式（最近傍 / 線形）は描画ポートの実装に依存します。
- 拡大描画の描画先は描画面より最大 `ZoomLevel - 1` ピクセル大きくなることがあります（切り上げた切り出し領域の拡大分。はみ出しは描画先のクリップに任せます）。
- 文字の折り返し・複数行は扱いません（1 行のみ）。
- 破線・クリップ・不透明度の指定は扱いません（描画ポートの契約に含めない）。
- 描画面（オフスクリーンビットマップ等）の生成は UI の責務です（本層は与えられた描画面へ描くだけ）。

## 5. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
