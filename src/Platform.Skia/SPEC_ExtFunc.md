# Platform.Skia 外部機能仕様書

`Platform.Skia` は、SkiaSharp への依存を閉じ込めたプラットフォーム層です。名前空間は `EsUtil.Platform.Skia`、アセンブリ名は `Platform.Skia` です。

## 公開 API の可視性

| 型 | 可視性 | 参照可能なアセンブリ |
| --- | --- | --- |
| `SkiaEsCanvas` | `public` | 任意（`SKCanvas` を描画ポートへ適合させるアダプタ） |
| `EsBitmapSkiaExtensions` | `public` | 任意（コアのビットマップと Skia の相互変換） |
| `SkiaFill` / `SkiaLabel` / `SkiaText` | `public` | 任意（描画の部品。`SkiaEsCanvas` が使うが、利用者が直接使うこともできる） |
| `SkiaColorInterop` / `SkiaGeometryInterop` | `public` | 任意（色・幾何の相互変換） |

## `SkiaEsCanvas`（描画ポートの実装）

| メンバー | 内容 |
| --- | --- |
| `SkiaEsCanvas(SKCanvas canvas)` | 描画先のキャンバスを指定して生成する（null 不可） |
| `Clear(EsRect surface, DrawingColor color)` | 指定矩形を単色で塗る |
| `FillRect` / `FillRects` / `FillRects<TWalker>` / `FillRoundRect` | 矩形・角丸矩形の塗り（`SkiaFill` へ委譲する） |
| `StrokeRect(EsRect rect, DrawingColor color, double thickness)` | 矩形の枠線（線の中心が矩形の辺の上に来る） |
| `DrawLine(EsPoint from, EsPoint to, DrawingColor color, double thickness)` | 線分 |
| `DrawLabel(...)` | 背景を塗ってから文字を描く（順序を保証する） |
| `DrawImage(EsBitmap image, EsRect source, EsRect destination)` | ビットマップを切り出して描く（`ToSKBitmap` の非所有ビューを使う） |
| `MeasureText(string text, double fontSize)` | テキストの実寸（`SkiaText.Measure` へ委譲する） |

## `EsBitmapSkiaExtensions`（相互変換）

| メソッド | 戻り値 | 所有権 | 有効期間 |
| --- | --- | --- | --- |
| `ToSKBitmap(this EsBitmap bitmap)` | `SKBitmap` | **非所有ビュー**（`InstallPixels`。破棄してもコアのバッファは解放されない） | 対応する `EsBitmap` の生存中のみ |
| `ToEsBitmap(this SKBitmap bitmap)` | `EsBitmap` | **複製**（引数の所有権は呼び出し側に残る） | 通常の `EsBitmap` と同じ |

### 契約と不変条件

- `ToSKBitmap` が返すビューの `Width` / `Height` / `ColorType`（BGRA8888）/ `AlphaType`（Premul）/ `RowBytes` は、コアの `EsBitmap` の値と一致する（`EsBitmap.Stride` は `幅 × 4` でパディングがないため `RowBytes` と一致する）。
- ビューはピクセルをコピーしないため、コア側の書き込みが即座に反映される（逆も同様）。
- `ToSKBitmap` は破棄済みの `EsBitmap` に対して `ObjectDisposedException` を投げる。
- `ToEsBitmap` は元のビットマップが BGRA8888・アルファ乗算済みでない場合に成分順を変換しない（呼び出し側で形式を保証する）。

## `SkiaFill`（塗りの部品）

| メソッド | 内容 |
| --- | --- |
| `Rect(SKCanvas, EsRect, DrawingColor)` | 矩形を塗る |
| `Rects(SKCanvas, IReadOnlyList<EsRect>, DrawingColor)` | 複数矩形を同じ色で塗る |
| `Rects<TWalker>(SKCanvas, TWalker, DrawingColor)` | 列挙子をそのまま受け取る版（タイル数が多い場合のリスト確保を避ける） |
| `RoundRect(SKCanvas, EsRect, double cornerRadius, DrawingColor)` | 角丸矩形を塗る |

## `SkiaLabel` / `SkiaText`

| メンバー | 内容 |
| --- | --- |
| `SkiaLabel.Draw(...)` | ラベル 1 件（背景 + 文字）を描く（背景 → 文字の順序を単一所有する） |
| `SkiaText.Measure(string text, double fontSize)` | テキストの実寸（`EsSize`） |
| `SkiaText.ResolveTypeface(string text)` | 日本語を含む文字列に適した書体を解決する（フォントのフォールバック） |
| `SkiaText.Draw(SKCanvas, string text, double left, double top, double fontSize, DrawingColor color)` | テキストを描く |

## `SkiaColorInterop` / `SkiaGeometryInterop`（相互変換）

| メソッド | 内容 |
| --- | --- |
| `ToSKColor(this DrawingColor color)` | `System.Drawing.Color` → `SKColor`（成分はそのまま。乗算済みへの変換はしない） |
| `ToDrawingColor(this SKColor color)` | `SKColor` → `System.Drawing.Color` |
| `ToSKRect(this EsRect rect)` | `EsRect` → `SKRect` |
| `ToEsSize(this SKSizeI size)` | `SKSizeI` → `EsSize` |

## 依存

| 依存 | 用途 |
| --- | --- |
| `SkiaSharp`（外部パッケージ） | `SKCanvas` / `SKBitmap` / `SKColor` / `SKRect` / 書体の解決 |
| `Common`（`EsUtil.SaveEvidence.Common.Imaging`） | 描画ポート（`IEsCanvas`）とラベル契約 |
| `Platform.Core`（`EsUtil.Platform.Core.*`） | ビットマップ（`EsBitmap`）と幾何値型 |

UI フレームワーク（Avalonia / MewUI）への依存はありません。

## 本プロジェクトを置く理由

`Platform.Core` の判定基準は「**外部 PackageReference なし**（`.NET 標準ライブラリ` と Microsoft 公式パッケージは許容）」です。SkiaSharp は外部パッケージであるため、Skia に依存する型を `Platform.Core` へ置けません。本プロジェクトは Skia 依存を単一所有し、UI 非依存層が SkiaSharp を参照しない状態を保ちます。

`ImageDrawMath` / `ImageDrawMode`（画像配置の矩形計算）は Skia を必要としない純粋計算であるため、`Platform.Core` に残します（戻り値は `EsRect` であり、各 UI が自 UI の矩形型へ変換します）。
