# Common

`SaveEvidence` のソリューション内で共有する**UI 非依存の契約**（描画ポートとラベル契約）を提供するプロジェクトです。

## 目的

`Platform.Core` は「**このソリューション以外でもそのまま使える型**」だけを置く層です（判定基準は後述）。しかし実際には、**このソリューション固有の共有契約**（各 UI が描画 API を適合させる描画ポート・ラベルの体裁）も同居していました。

本プロジェクトはその 2 種類を分離し、ソリューション固有の共有契約だけを持ちます。

- 描画ポート（`IEsCanvas`）とラベル契約（`IEsLabel` / `EsLabel*`）を**単一所有**する。
- **外部パッケージへ依存しない**（UI フレームワーク・Skia のいずれにも依存しない）。
- 各コントロールのコア・UIBase・UI、および各プラットフォーム層が**同じ契約**を使うことを保証する。

## 収録する型

| 型 | 内容 |
| --- | --- |
| `Imaging.IEsCanvas` | 描画内容を描画面へ写す**即時描画ポート**（UI 非依存）。コア側の描画層は本ポートだけを呼び、各 UI が自 UI の描画 API を適合させる（Avalonia は `AvaloniaCanvas`、Skia は `Platform.Skia` の `SkiaEsCanvas`） |
| `Imaging.IEsLabel` | ラベル 1 件の描画契約（文言・フォントサイズ・角丸と、実寸から配置を求める `ComputeFrame`）。**色は持たない**（色は描画にだけ必要な設定のため、呼び出し側が与える） |
| `Imaging.EsLabelFrame` | ラベル配置の値（背景矩形と文字の描画原点）。算出は `EsLabelLayout` が単一所有する |
| `Imaging.EsLabelLayout` | ラベルの体裁（背景 = 文字の実寸 + 内側余白×2・文字の原点・行送り・配置規則）の**単一所有** |
| `Imaging.EsLabelElement` | UI がテキスト要素を配置するための写像結果（背景矩形・文字原点・文言・フォントサイズ・色・角丸） |
| `Imaging.EsLabelExtensions` | `IEsLabel` → `EsLabelElement` の写像（`ToElement`）。色は引数で与える |
| `Imaging.EsLabelMetrics` | ラベルのフォントサイズの安全域（6〜48）と補正 |
| `Imaging.EsLabelDefaults` | ラベルの既定の色（白 / 黒 60%）とフォントサイズ |

## 依存関係

```text
Feature.*Core / *Control.UIBase / *Control.{AvaloniaUI,MewUI} / FadeMaskDragRectControl.*
Platform.Skia / Platform.AvaloniaUI / Platform.MewUI / app.*
  └──> Common（本プロジェクト）
         └──> Platform.Core（EsPoint / EsSize / EsRect のみ）
```

**本プロジェクトは UI フレームワークにも外部パッケージにも依存しません。** 参照するのは `Platform.Core` の幾何値型だけです。

**`Platform.Core` は本プロジェクトを参照しません**（依存の向きは一方向）。したがって本プロジェクトは `Platform.Core` と各層の間に位置します。

## 主な API

```csharp
// 描画ポート（各 UI が自 UI の描画 API を適合させて渡す）
IEsCanvas canvas = new AvaloniaCanvas(context);   // または Platform.Skia の SkiaEsCanvas
canvas.FillRect(new EsRect(0, 0, 100, 50), Color.Red);
canvas.DrawImage(bitmap, source, destination);
var size = canvas.MeasureText("Hello", 12.0);     // 実寸は描画 API でしか測れない

// ラベル契約（コアが実装する。色は持たない）
public readonly record struct MyLabel(string Text, double FontSize) : IEsLabel
{
    public EsLabelFrame ComputeFrame(EsSize textSize) => EsLabelLayout.ComputeBoxAtAnchor(...);
}

// 写像（実寸 → 配置 + 外観）。色は呼び出し側が与える
EsLabelElement element = label.ToElement(textSize, textColor, backgroundColor);
```

| メソッド | 用途 |
| --- | --- |
| `IEsCanvas.Clear(surface, color)` | 描画面全体を置き換える（前フレームの残像を残さない） |
| `IEsCanvas.FillRect` / `FillRects` / `FillRoundRect` / `StrokeRect` / `DrawLine` | 図形の描画（`FillRects` はタイル列挙子をそのまま受け取れるオーバーロードを持つ） |
| `IEsCanvas.DrawLabel(...)` | 背景を塗ってから文字を描く（順序を本メソッドが保証する） |
| `IEsCanvas.DrawImage` | ビットマップを切り出して描く |
| `IEsCanvas.MeasureText(text, fontSize)` | テキストの実寸（配置計算へ渡す唯一の測定入口） |
| `EsLabelLayout.ComputeBoxAtAnchor` / `ComputeOffsetTextOrigin` / `NextTop` | ラベルの体裁（背景の大きさ・文字の原点・行送り） |
| `EsLabelExtensions.ToElement(label, textSize, textColor, backgroundColor)` | ラベル契約 → UI 要素の配置指示 |

## 設計上の約束

1. **描画の可変性は `IEsCanvas` が 1 回だけ抽象化する**: コア側の描画手順は `IEsCanvas` を呼ぶだけで UI 非依存である（`~UI` が描画手順を継承実装すると同じ可変性を 2 つの機構で扱うことになる）。
2. **ラベルの体裁は `EsLabelLayout` が単一所有する**: 背景は「文字の実寸 + 内側余白×2」、文字は同じ余白だけ内側へ入る。各コントロールが同じ式を書くと余白が非対称になるため、規則を 1 箇所に集約する。
3. **ラベルは色を持たない**: 色は描画にだけ必要な設定である。`IEsLabel` は幾何と文言だけを持ち、色は `ToElement` の引数として呼び出し側が与える（既定は `EsLabelDefaults`）。
4. **要素への写像は `EsLabelExtensions.ToElement` が単一所有する**: 各ラベル型が同じ組み立てを書くと写し漏れで見た目がずれる。
5. **`EsBitmap` は本プロジェクトに置かない**: ビットマップはソリューション固有ではなく汎用であるため `Platform.Core` に残す（`Platform.Core` から Skia 依存を除去するため、Skia の相互変換は `Platform.Skia` が持つ）。

## テスト

```text
tests/
├── Common.Tests.csproj
└── Imaging/
    ├── EsLabelElement.Tests.cs      # 写像結果の値と余白の導出
    ├── EsLabelExtensions.Tests.cs   # ラベル契約 → 要素（色は引数）
    ├── EsLabelLayout.Tests.cs       # 体裁・行送り・配置（境界を含む）
    └── EsLabelMetrics.Tests.cs      # フォントサイズの安全域
```

```powershell
dotnet test src/Common/tests/Common.Tests.csproj
```

UI 初期化が不要なため、すべて `[Fact]` / `[Theory]` で並列実行されます。

## ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
