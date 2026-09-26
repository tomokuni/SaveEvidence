# Common

`SaveEvidence` のソリューション内で共有する**UI 非依存の契約**（描画ポートとラベルの体裁）を提供するプロジェクトです。

## 目的

`Platform.Core` は「**このソリューション以外でもそのまま使える型**」だけを置く層です（判定基準は後述）。しかし実際には、**このソリューション固有の共有契約**（各 UI が描画 API を適合させる描画ポート・ラベルの体裁）も同居していました。

本プロジェクトはその 2 種類を分離し、ソリューション固有の共有契約だけを持ちます。

- 描画ポート（`IEsCanvas`）とラベルの体裁（`EsLabel*`）を**単一所有**する。
- **外部パッケージへ依存しない**（UI フレームワーク・Skia のいずれにも依存しない）。
- 各コントロールのコア・UIBase・UI、および各プラットフォーム層が**同じ契約**を使うことを保証する。

## 収録する型

| 型 | 内容 |
| --- | --- |
| `Imaging.IEsCanvas` | 描画内容を描画面へ写す**即時描画ポート**（UI 非依存）。コア側の描画層は本ポートだけを呼び、各 UI が自 UI の描画 API を適合させる（Avalonia は `AvaloniaCanvas`、Skia は `Platform.Skia` の `SkiaEsCanvas`） |
| `Imaging.IEsRectWalker<TSelf>` | 矩形の列を確保なしで走査する列挙子（構造体）が実装する契約。`IEsCanvas.FillRects` が受け取る列挙子の型であり、ボックス化もリスト確保もせずにタイル列を渡すために使う |
| `Imaging.EsLabelFrame` | ラベル配置の値（背景矩形と文字の描画原点）。算出は `EsLabelLayout` が単一所有する |
| `Imaging.EsLabelLayout` | ラベルの体裁（背景 = 文字の実寸 + 内側余白×2・文字の原点・**基準点のアライン**・行送り）の**単一所有**（アラインの指定は `Platform.Core.Geometry` の `EsHorizontalAlign` / `EsVerticalAlign`） |
| `Imaging.EsLabelElement` | UI がテキスト要素を配置するための写像結果（背景矩形・文字原点・文言・フォントサイズ・色・角丸） |
| `Imaging.EsLabelMetrics` | ラベルのフォントサイズの安全域（6〜48）と補正 |
| `Imaging.EsLabelDefaults` | ラベルの既定の色（白 / 黒 60%）とフォントサイズ |

**ラベル契約（`IEsLabel`）と要素への写像（`EsLabelExtensions`）は置きません**: テキストを要素として置くか画像として描くかは UI 層の判断であり、コアは「文言・基準点・アライン・領域」だけを渡します（`MarqueeTextPlacement` / `LoupeTextPlacement`）。配置の計算は UIBase の `XxxLabelPlan` が本プロジェクトの `EsLabelLayout` を使って行います。

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

// ラベルの体裁（コアは位置だけを決め、実寸の反映は UI 層が行う）
var textSize = canvas.MeasureText("Hello", 12.0);
var box = EsLabelLayout.ComputeAlignedBox(
    anchor, areaSize, textSize, EsHorizontalAlign.Left, EsVerticalAlign.Top, EsLabelLayout.Padding, EsLabelLayout.Padding);
var origin = EsLabelLayout.ComputeOffsetTextOrigin(box.TopLeft, EsLabelLayout.Padding, EsLabelLayout.Padding);

// 要素の配置指示（UI は要素を置くか画像として描くかを選ぶ）
EsLabelElement element = EsLabelElement.Create(
    new EsLabelFrame(box, origin), "Hello", 12.0, textColor, backgroundColor);
```

| メソッド | 用途 |
| --- | --- |
| `IEsCanvas.Clear(surface, color)` | 描画面全体を置き換える（前フレームの残像を残さない） |
| `IEsCanvas.FillRect` / `FillRects` / `FillRoundRect` / `StrokeRect` / `DrawLine` | 図形の描画（`FillRects` はタイル列挙子をそのまま受け取れるオーバーロードを持つ） |
| `IEsCanvas.DrawLabel(...)` | 背景を塗ってから文字を描く（順序を本メソッドが保証する） |
| `IEsCanvas.DrawImage` | ビットマップを切り出して描く |
| `IEsCanvas.MeasureText(text, fontSize)` | テキストの実寸（配置計算へ渡す唯一の測定入口） |
| `EsLabelLayout.ComputeAlignedBox` | 基準点・アライン・領域から背景矩形を求める（領域 0 の軸はクランプしない） |
| `EsLabelLayout.ComputeOffsetTextOrigin` / `NextTop` | 文字の原点・縦積みの行送り |

## 設計上の約束

1. **描画の可変性は `IEsCanvas` が 1 回だけ抽象化する**: コア側の描画手順は `IEsCanvas` を呼ぶだけで UI 非依存である（`~UI` が描画手順を継承実装すると同じ可変性を 2 つの機構で扱うことになる）。
2. **ラベルの体裁は `EsLabelLayout` が単一所有する**: 背景は「文字の実寸 + 内側余白×2」、文字は同じ余白だけ内側へ入る。基準点の解釈（アライン）と描画領域へのクランプも同メソッドが持つ。各コントロールが同じ式を書くと余白が非対称になるため、規則を 1 箇所に集約する。
3. **コアはラベルを特殊扱いしない**: コアが渡すのは文言・基準点・アライン・領域（`MarqueeTextPlacement` / `LoupeTextPlacement`）だけである。**テキスト要素として置くか画像として描くかは UI 層の判断**であり、本プロジェクトも位置を知らない（実寸の反映は UIBase の `XxxLabelPlan` が行う）。
4. **要素の配置指示（`EsLabelElement`）への組み立ては UIBase が行う**: 各 UI が同じ組み立てを書くと写し漏れで見た目がずれるため、`MarqueeLabelPlan` / `LoupeLabelPlan` が単一所有する。
5. **`EsBitmap` は本プロジェクトに置かない**: ビットマップはソリューション固有ではなく汎用であるため `Platform.Core` に残す（`Platform.Core` から Skia 依存を除去するため、Skia の相互変換は `Platform.Skia` が持つ）。

## テスト

```text
tests/
├── Common.Tests.csproj
└── Imaging/
    ├── EsLabelElement.Tests.cs      # 配置指示の値と余白の導出
    ├── EsLabelLayout.Tests.cs       # 体裁・アライン・行送り（境界を含む）
    └── EsLabelMetrics.Tests.cs      # フォントサイズの安全域
```

```powershell
dotnet test src/Common/tests/Common.Tests.csproj
```

UI 初期化が不要なため、すべて `[Fact]` / `[Theory]` で並列実行されます。

## ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
