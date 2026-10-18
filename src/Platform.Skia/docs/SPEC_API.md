# Platform.Skia API リファレンス

`Platform.Skia` は、SkiaSharp への依存を閉じ込めたプラットフォーム層です。 名前空間は `EsUtil.SaveEvidence.Platform.Skia`、アセンブリ名は `EsUtil.SaveEvidence.Platform.Skia` です。

## 目次 <!-- omit from toc -->

- [1. 公開 API の可視性](#1-公開-api-の可視性)
- [2. `EsBitmapSkiaExtensions`（相互変換）](#2-esbitmapskiaextensions相互変換)
  - [2.1 契約と不変条件](#21-契約と不変条件)
- [3. `SkiaFill`（塗りの部品）](#3-skiafill塗りの部品)
- [4. `SkiaLabel` / `SkiaText`](#4-skialabel--skiatext)
- [5. `SkiaColorInterop` / `SkiaGeometryInterop`（相互変換）](#5-skiacolorinterop--skiageometryinterop相互変換)
- [6. `SkiaShadowRenderer` / `SkiaShadowBand`（影の描画）](#6-skiashadowrenderer--skiashadowband影の描画)
- [7. 依存関係](#7-依存関係)
- [8. 本プロジェクトを置く理由](#8-本プロジェクトを置く理由)

## 1. 公開 API の可視性

| 型 | 可視性 | 参照可能なアセンブリ |
| --- | --- | --- |
| `EsBitmapSkiaExtensions` | `public` | 任意（コアのビットマップと Skia の相互変換） |
| `SkiaFill` / `SkiaLabel` / `SkiaText` | `public` | 任意（描画の部品。 描画の呼び出しは利用者が行う） |
| `SkiaColorInterop` / `SkiaGeometryInterop` | `public` | 任意（色・幾何の相互変換） |
| `SkiaShadowRenderer` / `SkiaShadowBand` | `public` | 任意（影の描画。 濃さの計画はアプリ側が単一所有する） |

## 2. `EsBitmapSkiaExtensions`（相互変換）

| メソッド | 戻り値 | 所有権 | 有効期間 |
| --- | --- | --- | --- |
| `ToSKBitmap(this EsReadOnlyBitmap bitmap)` | `SKBitmap` | **非所有ビュー**（`InstallPixels`。 破棄してもコアのバッファは解放されない）。 **呼び出し側が破棄する**（呼び出しごとに新しいインスタンス） | 元の画像の生存中のみ |
| `ToSKBitmap(this EsPixmap view)` | `SKBitmap` | **非所有ビュー**（0 コピーの切り出しをそのまま Skia へ渡す。 行幅は元画像のストライドを渡すため**行がずれない**）。 **呼び出し側が破棄する** | 元の画像（ビューの参照が保つため、元を破棄しても有効） |
| `ToEsBitmap(this SKBitmap bitmap)` | `EsBitmap` | **複製**（引数の所有権は呼び出し側に残る） | 通常の `EsBitmap` と同じ |

### 2.1 契約と不変条件

- `ToSKBitmap` が返すビューの `Width` / `Height` / `ColorType`（BGRA8888）/ `AlphaType`（Premul）/ `RowBytes` は、元画像（`EsReadOnlyBitmap`）の値と一致する（`Stride` は `幅 × 4` でパディングがないため `RowBytes` と一致する）。 **`EsPixmap` の場合**は `RowBytes` が元画像のストライドのまま（`幅 × 4` より大きい場合がある）であり、`InstallPixels` へその値を渡すのが正しい（`幅 × 4` を渡すと行がずれる）。
- ビューはピクセルをコピーしないため、コア側の書き込みが即座に反映される（逆も同様）。
- `ToSKBitmap` は破棄済みの画像に対して `ObjectDisposedException` を投げる。
- `ToEsBitmap` は元のビットマップが BGRA8888・アルファ乗算済みでない場合に成分順を変換しない（呼び出し側で形式を保証する）。

## 3. `SkiaFill`（塗りの部品）

| メソッド | 内容 |
| --- | --- |
| `Rect(SKCanvas, EsRect, DrawingColor)` | 矩形を塗る |
| `Rects(SKCanvas, IReadOnlyList<EsRect>, DrawingColor)` | 複数矩形を同じ色で塗る |
| `RoundRect(SKCanvas, EsRect, double cornerRadius, DrawingColor)` | 角丸矩形を塗る |

## 4. `SkiaLabel` / `SkiaText`

| メンバー | 内容 |
| --- | --- |
| `SkiaLabel.Draw(...)` | ラベル 1 件（背景 + 文字）を描く（背景 → 文字の順序を単一所有する） |
| `SkiaText.Measure(string text, double fontSize)` | テキストの実寸（`EsSize`） |
| `SkiaText.ResolveTypeface(string text)` | 日本語を含む文字列に適した書体を解決する（フォントのフォールバック） |
| `SkiaText.Draw(SKCanvas, string text, double left, double top, double fontSize, DrawingColor color)` | テキストを描く |

## 5. `SkiaColorInterop` / `SkiaGeometryInterop`（相互変換）

| メソッド | 内容 |
| --- | --- |
| `ToSKColor(this DrawingColor color)` | `System.Drawing.Color` → `SKColor`（成分はそのまま。 乗算済みへの変換はしない） |
| `ToDrawingColor(this SKColor color)` | `SKColor` → `System.Drawing.Color` |
| `ToSKRect(this EsRect rect)` | `EsRect` → `SKRect` |
| `ToEsSize(this SKSizeI size)` | `SKSizeI` → `EsSize` |

## 6. `SkiaShadowRenderer` / `SkiaShadowBand`（影の描画）

`SkiaShadowRenderer` は画像の 4 辺・4 隅へ影を描く描画手順を単一所有します。 濃さの計画（幅・濃さの変化）はアプリの `ShadowPlan` が単一所有し、本クラスは渡された帯を塗るだけです。

| メンバー | 内容 |
| --- | --- |
| `SkiaShadowBand(int X, int Y, int Width, int Height, uint Argb)` | 影の 1 画素分の帯（物理ピクセルの矩形と色） |
| `SkiaShadowRenderer.Draw(SKCanvas, ReadOnlySpan<SkiaShadowBand>)` | 帯を**単色の矩形**で塗る（アンチエイリアス無効） |

**グラデーションを使わない理由**: グラデーション ブラシの端・半径の扱いは描画 API ごとに異なり、影の外側に線が出たり、隅だけ濃い点や薄い筋として見えたりします（実測）。 1 画素の帯を単色で塗れば、どの環境でも同じ結果になります。 4 隅も辺の帯を隅の正方形まで伸ばして重ねるため、本クラスに隅専用の処理はありません。

## 7. 依存関係

| 依存 | 用途 |
| --- | --- |
| `SkiaSharp`（外部パッケージ） | `SKCanvas` / `SKBitmap` / `SKColor` / `SKRect` / 書体の解決 |
| `EsUtil.Platform.Core`（`EsUtil.Platform.Core.*`） | ビットマップ（`EsReadOnlyBitmap` / `EsBitmap`）と幾何値型 |

UI フレームワーク（Avalonia / MewUI）への依存はありません。

## 8. 本プロジェクトを置く理由

`EsUtil.Platform.Core` の判定基準は「**外部 PackageReference なし**（`.NET 標準ライブラリ` と Microsoft 公式パッケージは許容）」です。 SkiaSharp は外部パッケージであるため、Skia に依存する型を `EsUtil.Platform.Core` へ置けません。 本プロジェクトは Skia 依存を単一所有し、UI 非依存層が SkiaSharp を参照しない状態を保ちます。

`ImageDrawMath` / `ImageDrawMode`（画像配置の矩形計算）は Skia を必要としない純粋計算であるため、本層には置かず、これを使う UI のアダプタ層（`Platform.MewUI`）が所有します（戻り値は `EsRect` であり、各 UI が自 UI の矩形型へ変換します）。
