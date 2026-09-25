# MarqueeControl.Rendering システム仕様書

`MarqueeControl.Rendering`（`MarqueeControl` の UI 非依存の描画層）の構造・依存・例外処理・コンポーネント構成を記載します。外部機能と公開 API は [SPEC_ExtFunc](SPEC_ExtFunc.md)、利用手順は [README](README.md) を参照してください。

## 1. システム概要

| 項目 | 内容 |
| --- | --- |
| 目的 | 描画 API の呼び出し手順（層の順序と写し方）を 1 箇所へ集約し、UI の追加・描画方式の変更を描画ポートの実装だけで済ませる |
| 提供形態 | クラスライブラリ（`MarqueeControl.Rendering.dll`） |
| 想定利用者 | UI 実装（`MarqueeControl.AvaloniaUI` / `MarqueeControl.MewUI`）・独自コントロールの実装者 |
| 汎用性 | 特定のアプリ・UI に依存しない。色・寸法・文言を持たない（コアの契約が単一所有する） |

## 2. 技術スタック

| 項目 | 内容 |
| --- | --- |
| ターゲット | `net10.0` |
| 言語 | C# 14（`Nullable` 有効・`ImplicitUsings` 有効） |
| ルート名前空間 | `EsUtil.SaveEvidence.Marquee.Rendering` |
| ドキュメント生成 | `GenerateDocumentationFile` 有効（XML は配布物へ出さず `cref` の検証のみに使う） |
| テスト | xUnit v3（UI 初期化不要。描画は Skia のビットマップ上で画素検証する） |

## 3. アーキテクチャ

### 3.1 依存方向（一方向）

層の全体像（Core / Rendering / CoreForControl / UI）は [`src/README.md`](../README.md) を参照してください。

```text
UI 層（MarqueeControl.AvaloniaUI / MarqueeControl.MewUI）
  入力の受付・効果の適用・通知の転送・描画先の用意・ラベル要素の配置
    ├──> MarqueeControl.Rendering ← 本プロジェクト   どう描くか（描画 API の呼び出し手順。IMarqueeRenderer を実装する）
    ├──> MarqueeControl.CoreForControl   組み込み先がある場合にだけ必要な規則
    └──> Feature.MarqueeCore   何を描くか + 判断（状態・値・純粋計算・コア⇔UI の契約）
           └──> Platform.Core（Es* 値型・ICoordTransform・ICursorService・IEsCanvas・EsLabelLayout）
※ Rendering と CoreForControl の間に依存はない（兄弟）。Core は Rendering / CoreForControl を参照しない。
```

- 上位層は下位層のみを参照します（逆参照なし）。
- 本層は UI フレームワークを参照しません（描画 API は `IEsCanvas` 経由）。
- コアは本層を参照しません（**コアは「何を描くか」、本層は「どう描くか」**）。

### 3.2 データフロー（どこから描画されるか）

```mermaid
sequenceDiagram
    participant UI as UI アダプタ（コントロール）
    participant M as Marquee（コアのファサード）
    participant R as MarqueeRenderer（本層）
    participant C as IEsCanvas（描画ポート）
    participant H as ホスト

    UI->>M: ResolveContent(描画領域のサイズ)
    M-->>UI: MarqueeDrawContent（データ＋層の順序）
    UI->>R: DrawShapes(canvas, content)
    R->>R: Layers の順に装飾・ハンドル・マーカーを選択
    R->>C: FillRect / FillRects / StrokeRect / DrawLine
    R-->>UI: （ラベルは描かない）
    UI->>UI: MarqueeLabelPlan.Resolve → ラベル要素を配置
    UI->>H: Rendered（描画内容を渡す。ホストが付加情報を描く）
```

### 3.3 描画の分岐

```mermaid
flowchart LR
    A[MarqueeDrawContent.Layers] --> B{層}
    B -->|Decoration| C[MarqueeOuterRenderer + MarqueeInnerRenderer]
    B -->|Handles| D[MarqueeRenderer.DrawHandles<br/>8 方向を列挙]
    B -->|Marker| E[MarqueeRenderer.DrawStartMarker<br/>十字線 2 本]
    B -->|SizeLabel / CoordLabel| F[スキップ<br/>UI の要素が描く]
```

## 4. コンポーネント構成

| ファイル | 型 | 責務 |
| --- | --- | --- |
| `src/MarqueeRenderer.cs` | `MarqueeRenderer` | 描画内容の解決結果を層の順に描画ポートへ写す（`IMarqueeRenderer` の既定実装。`Default` で共有する）。図形の層（装飾・ハンドル・マーカー）とラベルの層を分けて提供する |
| `src/MarqueeOuterRenderer.cs` | `MarqueeOuterRenderer` | `OuterDecoFrame`（暗転領域・枠線）を描く |
| `src/MarqueeInnerRenderer.cs` | `MarqueeInnerRenderer` | `InnerDecoFrame`（塗り・内枠）を描く |
| `src/MarqueeLabelPlan.cs` | `MarqueeLabelPlan` | 描画内容と実寸からラベル要素の配置指示を解決する |
| `src/MarqueeHostLabelRenderer.cs` | `MarqueeHostLabelRenderer` | ホスト付加情報ラベルへコアの体裁を適用して描く |

## 5. 単一所有（同じ真実を 2 箇所に持たない）

| 対象 | 所有者 |
| --- | --- |
| 描画する層とその順序 | `MarqueeFrame.ResolveLayers`（コア。本層は順に写すだけ） |
| 描くかどうかの判断（非表示・選択なし・nudge・透明色・穴あけ矩形なし） | コア（`MarqueeFrame`。本層は判断を持たない） |
| ハンドルの位置（8 方向）と一覧 | コア（`SelectionGeometry`） |
| ラベルの文言・色・フォントサイズ | コア（`MarqueeOptions` / `MarqueeSizeLabel` / `MarqueeCoordLabel`） |
| ラベルの体裁（背景・文字原点・行送り） | `Platform.Core`（`EsLabelLayout`） |
| ラベル要素の写像（順序・実寸の反映） | 本層（`MarqueeLabelPlan`） |
| 描画 API の呼び出し（背景 → 文字の順序） | `Platform.Core`（`SkiaLabel` / `IEsCanvas.DrawLabel`） |
| ブラシ・ペンの共有 | 描画ポートの実装（`AvaloniaCanvas` / `SkiaEsCanvas`） |

## 6. 例外処理ポリシー

| 状況 | 扱い |
| --- | --- |
| `canvas` が null | `ArgumentNullException`（呼び出し側の実装誤り） |
| `layers` / `frame` が空・null 項目 | 何もしない（描くものが無い状態は例外ではない） |
| `measureText` が null | `ArgumentNullException` |
| `MarqueeHostLabelRenderer.Draw` の `text` が null | `ArgumentNullException` |
| 基準点が描画面の外 | 例外にせず描画先のクリップに任せる |

## 7. 可視性ポリシー

- すべて `public` です（UI 実装・独自コントロールの作者が参照する API のみで構成されるため、internal にする対象がない）。
- `[EditorBrowsable]` による「見えにくくする」対処は行いません（公開面も契約も変わらないため）。

## 8. テスト方針

| 方針 | 内容 |
| --- | --- |
| UI 初期化不要 | 描画は `SkiaEsCanvas`（UI 非依存）で行い、ビットマップの画素を検証する |
| 検証点 | 描画手順の唯一の検証点である（UI 側はアダプタのみを持つため、UI ごとの描画テストは不要） |
| 合計 | 26 件 |

```powershell
dotnet test src/MarqueeControl.Rendering/tests/MarqueeControl.Rendering.Tests.csproj
```

## 9. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
