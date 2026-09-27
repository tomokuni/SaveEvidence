# Common

`SaveEvidence` のソリューション内で共有する**UI 非依存の描画ポート**（描画内容を描画面へ写す契約）を提供するプロジェクトです。

## 目的

`Platform.Core` は「**このソリューション以外でもそのまま使える型**」だけを置く層です（判定基準は後述）。描画ポートは UI 側の共有層（`Feature.Marquee.Presenter` / `Feature.Loupe.Presenter`）の描画手順（層の順序に従う写し方）と各 UI の描画 API をつなぐ**ソリューション固有の共有契約**であるため、本プロジェクトが持ちます。

- 描画ポート（`IEsCanvas`）を**単一所有**する。
- **外部パッケージへ依存しない**（UI フレームワーク・Skia のいずれにも依存しない）。
- 各コントロールのコア・UIBase・UI、および各プラットフォーム層が**同じ契約**を使うことを保証する。

**ラベル専用の型は置きません**。ラベルは「文字 + 背景」の体裁（内側余白・フォントサイズ・色・角丸）と「実寸 → 配置矩形」の写像を持つため、これらは描画層（`Feature.Marquee.Presenter` / `Feature.Loupe.Presenter` の `MarqueePaint` / `LoupePaint` と `MarqueeLabelPlan` / `LoupeLabelPlan`）が単一所有します。コアが渡すのは文言・基準点・アライン・領域（`MarqueeTextPlacement` / `LoupeTextPlacement`）だけであり、テキスト要素として置くか画像として描くかは UI 層が決めます。

## 収録する型

| 型 | 内容 |
| --- | --- |
| `Imaging.IEsCanvas` | 描画内容を描画面へ写す**即時描画ポート**（UI 非依存）。UI 側の共有層の描画手順（`MarqueeRendererBase` / `LoupeRendererBase`）は本ポートだけを呼び、各 UI が自 UI の描画 API を適合させる（Avalonia は `AvaloniaCanvas`、Skia は `Platform.Skia` の `SkiaEsCanvas`） |

## 依存関係

```text
Feature.*Core / *Control.UIBase / *Control.{AvaloniaUI,MewUI} / FadeMaskDragRectControl.*
Platform.Skia / Platform.AvaloniaUI / Platform.MewUI / app.*
  └──> Common（本プロジェクト）
         └──> Platform.Core（EsPoint / EsSize / EsRect / EsBitmap のみ）
```

**本プロジェクトは UI フレームワークにも外部パッケージにも依存しません。** 参照するのは `Platform.Core` の幾何値型とビットマップだけです。

**`Platform.Core` は本プロジェクトを参照しません**（依存の向きは一方向）。したがって本プロジェクトは `Platform.Core` と各層の間に位置します。

## 主な API

```csharp
// 描画ポート（各 UI が自 UI の描画 API を適合させて渡す）
IEsCanvas canvas = new AvaloniaCanvas(context);   // または Platform.Skia の SkiaEsCanvas
canvas.FillRect(new EsRect(0, 0, 100, 50), Color.Red);
canvas.DrawImage(bitmap, source, destination);
var size = canvas.MeasureText("Hello", 12.0);     // 実寸は描画 API でしか測れない
```

| メソッド | 用途 |
| --- | --- |
| `IEsCanvas.Clear(surface, color)` | 描画面全体を置き換える（前フレームの残像を残さない） |
| `IEsCanvas.FillRect` / `FillRects` / `FillRoundRect` / `StrokeRect` / `DrawLine` | 図形の描画（`FillRects` は同色の矩形の一覧をまとめて塗る） |
| `IEsCanvas.DrawLabel(...)` | 背景を塗ってから文字を描く（順序を本メソッドが保証する） |
| `IEsCanvas.DrawImage` | ビットマップを切り出して描く |
| `IEsCanvas.MeasureText(text, fontSize)` | テキストの実寸（配置計算へ渡す唯一の測定入口） |

## 設計上の約束

1. **描画の可変性は `IEsCanvas` が 1 回だけ抽象化する**: UI 側の共有層の描画手順は `IEsCanvas` を呼ぶだけで UI 非依存である（`~UI` が描画手順を継承実装すると同じ可変性を 2 つの機構で扱うことになる）。
2. **コアはラベルを特殊扱いしない**: コアが渡すのは文言・基準点・アライン・領域（`MarqueeTextPlacement` / `LoupeTextPlacement`）だけである。**テキスト要素として置くか画像として描くかは UI 層の判断**であり、本体裁（内側余白・フォントサイズ・色・角丸）と配置の写像は描画層が単一所有する。
3. **本プロジェクトに置くのは「複数の層が同じ契約を使うもの」だけ**: 描画ポートは UI 側の共有層（別アセンブリ）と各 UI の両方が使うため共有層が必要である。一方、ラベルの体裁と配置は UI 層の中で閉じるため、共有層に置くと層の分離（コアは UI を知らない）を崩す。
4. **`EsBitmap` は本プロジェクトに置かない**: ビットマップはソリューション固有ではなく汎用であるため `Platform.Core` に残す（`Platform.Core` から Skia 依存を除去するため、Skia の相互変換は `Platform.Skia` が持つ）。

## テスト

本プロジェクトは契約（インターフェース）のみを持ち、実装を持ちません。実装は各 UI のアダプタ（`AvaloniaCanvas` / `SkiaEsCanvas`）であり、それぞれの利用側のテストが実装を検証します。

```powershell
dotnet test src/Platform.AvaloniaUI/tests/Platform.AvaloniaUI.Tests.csproj
dotnet test src/Platform.Skia/tests/Platform.Skia.Tests.csproj
```

## ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
