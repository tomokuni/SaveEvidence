# FadeMaskDragRectControl.UIBase システム仕様書

## 概要

`FadeMaskDragRectControl.UIBase` は、領域選択の複合コントロール `FadeMaskDragRectControl` の両 UI が共有する処理を提供します。

1. **設定とモードから内包コントロールの値への解決**（`FadeMaskDragRectChildValues`）
2. **付加情報ラベル（操作・情報）の縦積みの配置指示**（`FadeMaskHostLabelRenderer`）と**ラベル 1 行の体裁**（`MarqueeHostLabelRenderer`）

外部機能と公開 API は [SPEC_ExtFunc](SPEC_ExtFunc.md)、利用手順は [README](README.md) を参照してください。

## 技術スタック

| 項目 | 内容 |
| --- | --- |
| 目的 | 複合コントロールの分配規則とラベル配置を 1 箇所へ集約し、両 UI の挙動が構造的に一致するようにする |
| 提供形態 | クラスライブラリ（`FadeMaskDragRectControl.UIBase.dll`） |
| 想定利用者 | `FadeMaskDragRectControl.AvaloniaUI` / `FadeMaskDragRectControl.MewUI`（および両者のテスト） |
| ターゲット | `net10.0` |
| 言語 | C# 14（`Nullable` 有効・`ImplicitUsings` 有効） |
| ルート名前空間 | `EsUtil.SaveEvidence.FadeMaskDragRect.UIBase` |
| 汎用性 | 特定のアプリ・UI に依存しない。UI フレームワークを参照しない |

## アーキテクチャ

```mermaid
graph LR
  A[FadeMaskDragRectControl.AvaloniaUI] --> U[FadeMaskDragRectControl.UIBase]
  B[FadeMaskDragRectControl.MewUI] --> U
  U --> C[FadeMaskDragRectControl.Core]
  U --> M[Feature.Marquee.Core]
  U --> L[Feature.Loupe.Core]
  U --> H[Feature.Marquee.Presenter]
  U --> P[Platform.Core]
```

依存の向きは **UI → UIBase（本層）→ Core / 各提示層**の一方向です。`Feature.Marquee.Presenter` は UI フレームワークを参照しない
（提示層の `MarqueeAppearance` を参照する）ため、本層から参照しても UI 非依存性は損なわれません。
付加情報ラベルの体裁（`MarqueeHostLabelRenderer`）は本層が所有します。

## コンポーネント構成

| ファイル | 可視性 | 内容 |
| --- | --- | --- |
| `FadeMaskDragRectChildValues.cs` | `public readonly record struct` | 設定とモードから内包コントロールへ適用する値の解決 |
| `FadeMaskHostLabelRenderer.cs` | `public static class` | 付加情報ラベルの縦積みの配置指示の解決 |
| `MarqueeHostLabelRenderer.cs` | `public static class` | ラベル 1 行の体裁（背景 = 文字の実寸 + 内側余白 × 2）の配置指示の解決 |

## クラス図

```mermaid
classDiagram
  class FadeMaskDragRectChildValues {
    <<readonly record struct>>
    +MarqueeOptions Marquee
    +MarqueeAppearance MarqueeAppearance
    +LoupeOptions Loupe
    +double LoupeOffset
    +bool IsHitTestVisible
    +Resolve(options, mode, highlightRect)$ FadeMaskDragRectChildValues
  }
  class FadeMaskHostLabelRenderer {
    <<static>>
    +Draw(canvas, content, labels, operationText, infoText)$
  }
  class FadeMaskDragRectOptions {
    <<record / Core>>
  }
  class FadeMaskDragRectModes {
    <<static / Core (internal)>>
    +Resolve(mode)$ FadeMaskDragRectState
  }
  class MarqueeHostLabelRenderer {
    <<static / 本層>>
    +Resolve(appearance, anchor, text)$ MarqueeLabelPlacement
    +Draw(canvas, anchor, text, fontSize, textColor, backgroundColor, padding)$
  }

  FadeMaskDragRectChildValues ..> FadeMaskDragRectOptions : 読む
  FadeMaskDragRectChildValues ..> FadeMaskDragRectModes : モードを解釈する
  FadeMaskHostLabelRenderer ..> MarqueeHostLabelRenderer : 体裁を委譲する
```

## データフロー（設定の適用）

```mermaid
sequenceDiagram
  participant Host as ホスト
  participant UI as FadeMaskDragRectControl（UI 実装）
  participant UB as FadeMaskDragRectChildValues
  participant Core as FadeMaskDragRectModes（コア）
  participant Child as 内包コントロール

  Host->>UI: Options / Mode を設定
  UI->>UB: Resolve(options, mode, 現在のハイライト矩形)
  UB->>Core: Resolve(mode)
  Core-->>UB: State（UsesSelection / ShowFullOverlay）
  UB-->>UI: ChildValues（設定＋モードの反映済み）
  UI->>Child: Options / Appearance / PositionOffset / IsHitTestVisible を代入
  Note over UI,Child: 代入には UI 固有型が必要なため UI が行う（値の決定は UIBase）
```

## データフロー（付加情報ラベルの配置指示）

```mermaid
sequenceDiagram
  participant MC as MarqueeControl（子）
  participant UI as FadeMaskDragRectControl（UI 実装）
  participant UB as FadeMaskHostLabelRenderer
  participant MH as MarqueeHostLabelRenderer

  MC->>UI: Rendered（描画計画）
  UI->>UI: 文言プロバイダを評価する（ホストの責務）
  UI->>UB: Resolve(描画計画, ラベルの外観, 選択矩形の外観, 操作文言, 情報文言, 実寸測定)
  UB->>UB: 穴あけ矩形の左上から積み上げ位置を決める
  UB->>MH: Resolve(基準点, 文言, 体裁, 実寸測定)（行ごと）
  MH-->>UB: 配置指示（背景 + 文字）
  UB-->>UI: 配置指示（描画順）
  UI->>UI: 描画ポートで描く（UI フレームワーク固有の API）
```

## 積み上げのフローチャート

```mermaid
flowchart TD
  A[Resolve] --> B{穴あけ矩形があるか}
  B -- いいえ --> Z[空のリストを返す]
  B -- はい --> C[y = 穴あけ矩形の上端]
  C --> D{サイズラベルが表示されるか}
  D -- はい --> E[y = 行送り 1 行分下げる]
  D -- いいえ --> F[操作ラベルの判定へ]
  E --> F
  F --> G{設定スイッチ ON かつ 文言が空でないか}
  G -- はい --> H[操作ラベルを加え y を 1 行下げる]
  G -- いいえ --> I[情報ラベルの判定へ]
  H --> I
  I --> J{設定スイッチ ON かつ 文言が空でないか}
  J -- はい --> K[情報ラベルを加える]
  J -- いいえ --> Z2[配置指示を返す]
  K --> Z2
```

## 単一所有（Single Source of Truth）

| 内容 | 所有者 |
| --- | --- |
| 動作モードの定義と解釈（表示可否の判断） | `FadeMaskDragRectModes` / `FadeMaskDragRectState`（コア） |
| 設定の既定値と構成 | `FadeMaskDragRectOptions`（コア） |
| 設定とモードから子の値への解決 | `FadeMaskDragRectChildValues`（本層） |
| ハイライト矩形（実行時状態） | ホスト（本層は引数で受けるだけ） |
| 付加情報ラベルの積み上げ位置と表示可否 | `FadeMaskHostLabelRenderer`（本層） |
| ラベルの体裁（背景 = 実寸 + 内側余白） | `MarqueeHostLabelRenderer`（本層） |
| 内側余白（体裁の値）と行送り | `MarqueeAppearance`（Feature.Marquee.Presenter） |
| 付加情報ラベルの文言 | ホスト |
| 選択矩形の破線の幾何 | `FadeMaskDragRectOverlay`（コア） |

**集約による削減**: 本層の新設前は、両 UI が同じ分配（2 段の上書き）と同じ積み上げ（`y` の加算とサイズラベルの
オフセット）をそれぞれ持っていました（各 67 行）。片方だけ規則が変わると同じ設定でも UI によって見た目が変わるため、
値の決定と積み上げを本層が単一所有します。

## 例外処理ポリシー

| 状況 | 挙動 |
| --- | --- |
| 外観（`appearance`）が null | `ArgumentNullException`（呼び出し側の実装誤りであり、回復して続行すべき状況ではない） |
| 実寸測定（`measureText`）が null | `ArgumentNullException`（同上） |
| ラベル 1 行の文言が null | `ArgumentNullException`（同上） |
| 穴あけ矩形が無い | 空のリストを返す（早期リターン。選択もハイライトも無い状態は正常） |
| 付加情報の文言が null / 空 | その行を返さない（対象外のモードで発生する正常な入力） |
| ハイライト矩形が null | 「ハイライトなし」として解決する（null が無効を意味する） |

本層は外部 I/O・非同期処理・リソース解放を持ちません。

## 可視性ポリシー

- 両 UI が参照する値と配置指示は `public`（UI 層が直接使う契約）。
- `FadeMaskDragRectControl.Core` の `internal` 型（`FadeMaskDragRectModes` / `FadeMaskDragRectState`）は
  `InternalsVisibleTo`（`FadeMaskDragRectControl.UIBase`）で参照します。
- **`internal` → `public` の変更は破壊的な設計変更として扱います**。

## テスト

```text
tests/
├── FadeMaskDragRectChildValues.Tests.cs   # 値の解決（9 件）
├── FadeMaskHostLabelRenderer.Tests.cs     # ラベルの縦積み（11 件）
└── MarqueeHostLabelRenderer.Tests.cs      # ラベル 1 行の体裁（6 件）
```

合計 26 件（`[Theory]` は展開後のケース数）。

| 検証内容 | 件数 |
| --- | --- |
| 選択矩形を使うモード（Crop / AreaSelect）で全面暗転・ハンドル・入力が有効になること | 2 |
| 選択矩形を使わないモード（ScreenSelect / WindowSelect）で無効になること | 2 |
| 未定義のモードが選択矩形を使わない扱いになること | 1 |
| 外観は指定した値を保つこと | 1 |
| 外観が null の場合の例外 | 1 |
| 設定の値がそのまま写されること | 1 |
| 値の等価性 | 1 |
| 穴あけ矩形が無ければ空を返すこと | 1 |
| 操作・情報ラベルを縦に積むこと | 1 |
| サイズラベルの下から積むこと | 1 |
| サイズラベルを描かない設定では先頭へ繰り上がること | 1 |
| 表示スイッチが無効な行を返さないこと | 3 |
| 文言が空の行を返さないこと | 1 |
| 空白のみの文言は返すこと | 1 |
| 外観が null の場合の例外 | 1 |
| 測定関数が null の場合の例外 | 1 |
| 基準点を背景の左上に置くこと | 1 |
| 背景を実寸と内側余白から求めること | 1 |
| 余白が大きいほど背景が広がること | 1 |
| 文言と体裁を配置指示へ写すこと | 1 |
| 文言が null の場合の例外 | 1 |
| 測定関数が null の場合の例外 | 1 |

本層は描画 API を呼ばない（配置指示までを返す）ため、検証にウィンドウも描画バックエンドも不要です（すべて
`[Fact]` / `[Theory]` で並列実行されます）。UI 固有の検証（子コントロールへの代入・要素の配置・イベントの発火）は
各 UI のテストプロジェクトが担います。
