# Platform.Skia

**SkiaSharp への依存を 1 箇所へ閉じ込める**プラットフォーム層です。

## 目的

`Platform.Core` の判定基準は「**外部 PackageReference なし**（`.NET 標準ライブラリ` と Microsoft 公式パッケージは許容）」です。SkiaSharp は外部パッケージであるため `Platform.Core` には置けません。

本プロジェクトは Skia に依存する処理を単一所有し、UI 非依存層が SkiaSharp を参照しない状態を保ちます。

- コアの描画ポート（`IEsCanvas`）を `SKCanvas` へ適合させる。
- コアのビットマップ（`EsBitmap`）と `SKBitmap` の相互変換を提供する。
- 色・幾何の相互変換（`DrawingColor` ⇔ `SKColor`、`EsRect` ⇔ `SKRect`）を単一所有する。

## 収録する型

| 型 | 内容 |
| --- | --- |
| `SkiaEsCanvas` | `SKCanvas` を `IEsCanvas`（描画ポート）へ適合させるアダプタ。描画ポートの実装はこれと `Platform.AvaloniaUI` の `AvaloniaCanvas` の 2 つ |
| `EsBitmapSkiaExtensions` | `EsBitmap` ⇔ `SKBitmap` の相互変換（**本プロジェクトの中心**。`ToSKBitmap` は非所有ビュー、`ToEsBitmap` は複製） |
| `SkiaFill` | 矩形・角丸矩形の塗り（`SkiaEsCanvas` から使う） |
| `SkiaLabel` | ラベル 1 件（背景 + 文字）の描画順を単一所有する |
| `SkiaText` | テキストの実寸測定・描画と、日本語フォントの解決 |
| `SkiaColorInterop` | `System.Drawing.Color` ⇔ `SKColor` の変換 |
| `SkiaGeometryInterop` | `EsRect` ⇔ `SKRect`、`SKSizeI` ⇔ `EsSize` の変換 |

## 依存関係

```text
MarqueeControl.MewUI / LoupeControl.MewUI / FadeMaskDragRectControl.MewUI / ZoomViewControl.MewUI / app.MewUI
Platform.MewUI
  └──> Platform.Skia（本プロジェクト）
         ├──> SkiaSharp（外部パッケージ。本層だけが参照する）
         ├──> Common（IEsCanvas）
         └──> Platform.Core（EsBitmap / 幾何値型）
```

**本プロジェクトが SkiaSharp を参照する唯一の場所です。** `Platform.Core` と `Common` は SkiaSharp を参照しません。

## 主な API

```csharp
// 描画ポート（コアの描画手順は IEsCanvas だけを呼ぶ）
IEsCanvas canvas = new SkiaEsCanvas(skCanvas);

// EsBitmap ⇔ SKBitmap（所有権はコア側に残る）
using var view = bitmap.ToSKBitmap();       // 非所有ビュー（EsBitmap の生存中のみ有効）
canvas.DrawBitmap(view, destination);

using var copied = skBitmap.ToEsBitmap();   // 複製（引数の所有権は呼び出し側に残る）
```

| メソッド | 用途 |
| --- | --- |
| `EsBitmapSkiaExtensions.ToSKBitmap(this EsBitmap)` | コアのバッファを指す**非所有ビュー**を返す（描画 API へ渡すため） |
| `EsBitmapSkiaExtensions.ToEsBitmap(this SKBitmap)` | Skia のビットマップを**複製**してコアのビットマップを生成する |
| `SkiaEsCanvas`（`IEsCanvas` の実装） | `SKCanvas` へ図形・文字・画像を描く |
| `SkiaText.Measure(text, fontSize)` | テキストの実寸（コアの配置計算へ渡す） |
| `SkiaText.ResolveTypeface(text)` | 日本語を含む文字列に適した書体を解決する（フォントのフォールバック） |
| `SkiaColorInterop.ToSKColor` / `ToDrawingColor` | 色の相互変換 |
| `SkiaGeometryInterop.ToSKRect` / `ToEsSize` | 幾何の相互変換 |

## 設計上の約束

1. **所有権は移動しない**: `ToSKBitmap` は非所有ビュー、`ToEsBitmap` は複製である。コアのピクセルバッファの所有権は常に `EsBitmap` に残る（`EsBitmap` を破棄した後のビューは無効）。
2. **ビューの寿命は `EsBitmap` の生存期間と一致する**: ピクセルをコピーしないため、`EsBitmap` 破棄後にビューを使うと解放済みメモリを読む。コピーが必要な場合はビューへ `Copy()` を適用する。
3. **ストライドは幅×4**: コアの `EsBitmap.Stride` は `幅 × 4`（パディングなし）で固定であり、Skia の `RowBytes` と一致する（コピー手順とピクセル配置の期待値が両者で揃う）。
4. **`SKBitmap` を確保先として使わない**: コアのビットマップは自前のバッファ（`NativeMemory.AlignedAlloc`）を持つ。Skia は描画・変換の相手としてのみ関与する。
5. **`ImageDrawMath` は本層に置かない**: 画像配置の矩形計算は純粋計算であり Skia を必要としないため `Platform.Core` に残す（戻り値は `EsRect`）。

## テスト

```text
tests/
├── Platform.Skia.Tests.csproj
├── EsBitmapSkiaExtensions.Tests.cs    # 非所有ビュー・複製・所有権・ストライドの一致（8 件）
├── SkiaColorInterop.Tests.cs          # 色の相互変換（8 件）
├── SkiaEsCanvas.Tests.cs              # 描画ポートの画素検証（12 件）
├── SkiaFill.Tests.cs                  # 矩形・角丸矩形の塗り（9 件）
├── SkiaGeometryInterop.Tests.cs       # 幾何の相互変換（5 件）
├── SkiaLabel.Tests.cs                 # ラベル 1 件の描画順（5 件）
└── SkiaText.Tests.cs                  # 実寸測定・書体解決・描画（10 件）
```

UI 初期化（ウィンドウ）が不要なため、すべて `[Fact]` / `[Theory]` で並列実行されます（`SKBitmap` / `SKCanvas` をメモリ上で直接使う）。

```powershell
dotnet test src/Platform.Skia/tests/Platform.Skia.Tests.csproj
```

## ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
