# Platform.Skia

**SkiaSharp への依存を 1 箇所へ閉じ込める**プラットフォーム層です。

## 1. 目的

`EsUtil.Platform.Core` の判定基準は「**外部 PackageReference なし**（`.NET 標準ライブラリ` と Microsoft 公式パッケージは許容）」です。 SkiaSharp は外部パッケージであるため `EsUtil.Platform.Core` には置けません。

本プロジェクトは Skia に依存する処理を単一所有し、UI 非依存層が SkiaSharp を参照しない状態を保ちます。

- コアのビットマップ（`EsBitmap`）と `SKBitmap` の相互変換を提供する。
- 色・幾何の相互変換（`DrawingColor` ⇔ `SKColor`、`EsRect` ⇔ `SKRect`）を単一所有する。
- **描画の部品**（`SkiaFill` / `SkiaText` / `SkiaLabel`）を提供する。 描画の呼び出し（層の順序）は各 UI が行う（**描画 API の抽象は持たない**。 UI フレームワークごとの描画モデルの差は抽象では吸収しきれないため）。

## 2. 収録する型

| 型 | 内容 |
| --- | --- |
| `EsBitmapSkiaExtensions` | `EsBitmap` ⇔ `SKBitmap` の相互変換（**本プロジェクトの中心**。 `ToSKBitmap` は非所有ビュー、`ToEsBitmap` は複製） |
| `SkiaFill` | 矩形・角丸矩形の塗り |
| `SkiaLabel` | ラベル 1 件（背景 + 文字）の描画順を単一所有する |
| `SkiaText` | テキストの実寸測定・描画と、日本語フォントの解決 |
| `SkiaColorInterop` | `System.Drawing.Color` ⇔ `SKColor` の変換 |
| `SkiaGeometryInterop` | `EsRect` ⇔ `SKRect`、`SKSizeI` ⇔ `EsSize` の変換 |

## 3. 依存関係

```text
EsUtil.Feature.Marquee.ControlMewUI / EsUtil.Feature.Loupe.ControlMewUI / Parts.CapturePreview.ControlMewUI / Parts.CapturePreview.ControlMewUI / App.MewUI
Platform.MewUI
  └──> Platform.Skia（本プロジェクト）
         ├──> SkiaSharp（外部パッケージ。本層だけが参照する）
         └──> EsUtil.Platform.Core（EsBitmap / 幾何値型）
```

**本プロジェクトが SkiaSharp を参照する唯一の場所です。**

## 4. 主な API

```csharp
// EsBitmap ⇔ SKBitmap（所有権はコア側に残る）
using var view = bitmap.ToSKBitmap();       // 非所有ビュー（EsBitmap の生存中のみ有効。 呼び出し側が破棄する）
canvas.DrawBitmap(view, destination);

using var copied = skBitmap.ToEsBitmap();   // 複製（引数の所有権は呼び出し側に残る）

// 描画の部品（描画順は呼び出し側が決める）
SkiaFill.Rect(canvas, rect, color);
SkiaLabel.Draw(canvas, background, bgColor, text, origin, fontSize, textColor, cornerRadius: 0);
```

| メソッド | 用途 |
| --- | --- |
| `EsBitmapSkiaExtensions.ToSKBitmap(this EsBitmap)` | コアのバッファを指す**非所有ビュー**を返す（描画 API へ渡すため）。 **呼び出し側が破棄する**（呼び出しごとに新しい `SKBitmap` を作る） |
| `EsBitmapSkiaExtensions.ToEsBitmap(this SKBitmap)` | Skia のビットマップを**複製**してコアのビットマップを生成する |
| `SkiaFill.Rect` / `Rects` / `RoundRect` | 矩形の塗り |
| `SkiaLabel.Draw(...)` | ラベル 1 件（背景 → 文字の順）を描く |
| `SkiaText.Measure(text, fontSize)` | テキストの実寸（コアの配置計算へ渡す） |
| `SkiaText.ResolveTypeface(text)` | 日本語を含む文字列に適した書体を解決する（フォントのフォールバック） |
| `SkiaColorInterop.ToSKColor` / `ToDrawingColor` | 色の相互変換 |
| `SkiaGeometryInterop.ToSKRect` / `ToEsSize` | 幾何の相互変換 |

## 5. 設計上の約束

1. **所有権は移動しない**: `ToSKBitmap` は非所有ビュー、`ToEsBitmap` は複製である。 コアのピクセルバッファの所有権は常に `EsBitmap` に残る（`EsBitmap` を破棄した後のビューは無効）。 ビュー自体（`SKBitmap` のインスタンス）は**呼び出し側が破棄する**（ファイナライザを持つため、描画のたびに作る用途では `using` が必須）。
2. **ビューの寿命は `EsBitmap` の生存期間と一致する**: ピクセルをコピーしないため、`EsBitmap` 破棄後にビューを使うと解放済みメモリを読む。 コピーが必要な場合はビューへ `Copy()` を適用する。
3. **ストライドは幅×4**: コアの `EsBitmap.Stride` は `幅 × 4`（パディングなし）で固定であり、Skia の `RowBytes` と一致する（コピー手順とピクセル配置の期待値が両者で揃う）。
4. **`SKBitmap` を確保先として使わない**: コアのビットマップは自前のバッファ（`NativeMemory.AlignedAlloc`）を持つ。 Skia は描画・変換の相手としてのみ関与する。
5. **`ImageDrawMath` は本層に置かない**: 画像配置の矩形計算は純粋計算であり Skia を必要としないため本層に置かず、使う UI のアダプタ層（`Platform.MewUI`）が所有する（戻り値は `EsRect`）。

## 6. テスト

```text
tests/
├── Platform.Skia.Tests.csproj
├── EsBitmapSkiaExtensions.Tests.cs    # 非所有ビュー・複製・所有権・ストライドの一致
├── SkiaColorInterop.Tests.cs          # 色の相互変換
├── SkiaFill.Tests.cs                  # 矩形・角丸矩形の塗り
├── SkiaGeometryInterop.Tests.cs       # 幾何の相互変換
├── SkiaLabel.Tests.cs                 # ラベル 1 件の描画順
└── SkiaText.Tests.cs                  # 実寸測定・書体解決・描画
```

UI 初期化（ウィンドウ）が不要なため、すべて `[Fact]` / `[Theory]` で並列実行されます（`SKBitmap` / `SKCanvas` をメモリ上で直接使う）。

```powershell
dotnet test src/Platform.Skia/tests/Platform.Skia.Tests.csproj
```

## 7. ドキュメント

- [docs/SPEC_API.md](docs/SPEC_API.md) — 公開 API のリファレンス
- [docs/SPEC_System.md](docs/SPEC_System.md) — アーキテクチャ・依存・設計判断

## 8. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
