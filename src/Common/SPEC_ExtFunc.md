# Common 外部機能仕様書

`Common` は、SaveEvidence のソリューション内で共有する **UI 非依存の描画ポート**（描画内容を描画面へ写す契約）を提供します。名前空間は `EsUtil.SaveEvidence.Common.Imaging`、アセンブリ名は `Common` です。

## 公開 API の可視性

| 型 | 可視性 | 参照可能なアセンブリ |
| --- | --- | --- |
| `IEsCanvas` | `public` | 任意（各 UI が自 UI の描画 API を適合させる契約） |

本プロジェクトは UI フレームワークにも外部パッケージにも依存しないため、全型が `public` として公開されます。

## `IEsCanvas`（描画ポート）

描画内容を描画面へ写す**即時描画ポート**です。UI 側の共有層（`Feature.Marquee.Presenter` / `Feature.Loupe.Presenter`）の描画手順は本ポートだけを呼び、UI フレームワーク固有の API は各 UI のアダプタが適合させます。

| メンバー | 内容 |
| --- | --- |
| `Clear(EsRect surface, DrawingColor color)` | 指定矩形を単色で塗る（全面消去の代替。描画面の寿命は UI が所有するため矩形を引数で受ける） |
| `FillRect(EsRect rect, DrawingColor color)` | 矩形を塗る |
| `FillRects(IReadOnlyList<EsRect> rects, DrawingColor color)` | 複数矩形を同じ色で塗る |
| `FillRoundRect(EsRect rect, double cornerRadius, DrawingColor color)` | 角丸矩形を塗る |
| `StrokeRect(EsRect rect, DrawingColor color, double thickness)` | 矩形の枠線を描く |
| `DrawLine(EsPoint from, EsPoint to, DrawingColor color, double thickness)` | 線分を描く |
| `DrawLabel(EsRect bounds, EsPoint textOrigin, string text, double fontSize, DrawingColor textColor, DrawingColor backgroundColor, double cornerRadius)` | 背景を塗ってから文字を描く（**順序を本メソッドが保証する**） |
| `DrawImage(EsBitmap image, EsRect source, EsRect destination)` | ビットマップを切り出して描く |
| `MeasureText(string text, double fontSize)` | テキストの実寸を測る（実寸は描画 API でしか測れないため、配置計算へ渡す唯一の入口） |

**色の型**は UI 非依存の `System.Drawing.Color`（`System.Drawing.Primitives`）です。

## 依存関係

| 依存 | 用途 |
| --- | --- |
| `Platform.Core`（`EsUtil.Platform.Core.Geometry` / `.Imaging`） | 幾何値型（`EsPoint` / `EsSize` / `EsRect`）とビットマップ（`EsBitmap`） |

UI フレームワーク（Avalonia / MewUI / Skia）への依存はありません。外部パッケージへの直接依存もありません。

## 本プロジェクトを置く理由

| 置き場所の候補 | 可否 | 理由 |
| --- | --- | --- |
| `Platform.Core` | 不可 | 判定基準が「**このソリューション以外でもそのまま使える型**」であり、描画ポートはソリューション固有の共有契約である |
| 各コントロールのコア | 不可 | Marquee と Loupe の両方が同じ契約を使うため、どちらか一方に置くと他方が依存する |
| `Common`（本プロジェクト） | 採用 | ソリューション固有の共有契約だけを置く層として、`Platform.Core` と各層の間に位置する |

**ラベル専用の型（配置の値・体裁・要素への写像）は置きません**。ラベルの体裁と実寸 → 配置矩形の写像は UI 層の中で閉じるため、描画層（`Feature.Marquee.Presenter` / `Feature.Loupe.Presenter`）が単一所有します。コアは文言・基準点・アライン・領域だけを渡します。
