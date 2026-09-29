# Feature.Marquee.Presenter システム仕様書

`Feature.Marquee.Presenter`（`MarqueeControl` の見た目を定めるコントロール化を前提としない UI 非依存層）の構造・依存・例外処理・コンポーネント構成を記載します。外部機能と公開 API は [SPEC_ExtFunc](SPEC_ExtFunc.md)、利用手順は [README](README.md) を参照してください。

## 1. システム概要

| 項目 | 内容 |
| --- | --- |
| 目的 | 描画計画（層の順序・装飾矩形・文言・配置）と外観（色・フォント・枠線の太さ・表示可否）を 1 箇所に集約する |
| 提供形態 | クラスライブラリ（`Feature.Marquee.Presenter.dll`） |
| 想定利用者 | UI 実装（`MarqueeControl.AvaloniaUI` / `MarqueeControl.MewUI`）・それらを組み込むホストアプリケーション・独自コントロールの作者 |
| 汎用性 | 特定のアプリ・UI・コントロールに依存しない。寸法・対話パラメータはコアが、外観と描画計画は本層が単一所有する |
| 前提としないもの | **コントロール化**（ホイール操作・ビューポート連携・組み込み先の概念は UI 層が持つ） |

## 2. 技術スタック

| 項目 | 内容 |
| --- | --- |
| ターゲット | `net10.0` |
| 言語 | C# 14（`Nullable` 有効・`ImplicitUsings` 有効） |
| ルート名前空間 | `EsUtil.Feature.Marquee.Presenter`（配下はフォルダに追従: `.Setting` / `.Drawing` / `.Label` / `.Logging`） |
| ドキュメント生成 | `GenerateDocumentationFile` 有効（XML は配布物へ出さず `cref` の検証のみに使う） |
| テスト | xUnit v3（UI 初期化不要。`InternalsVisibleTo` で `internal` を検証する） |
| 依存 | UI フレームワークを一切参照しない。直接依存は `Feature.Marquee.Core`・`Platform.Core`・`Microsoft.Extensions.Logging.Abstractions` |

## 3. アーキテクチャ

### 3.1 依存方向

層の全体像（Core / Presenter / UI）は [`src/README.md`](../README.md) を参照してください。

```text
UI 層（MarqueeControl.AvaloniaUI / MarqueeControl.MewUI）
  入力の受付・効果の適用・通知の転送・描画（自 UI の API）・ラベル要素の配置・ホスト結線
  ホイール操作とビューポートの変化の追従（コントロール化を前提とした処理）
    ├──> Feature.Marquee.Presenter ← 本プロジェクト   提示（描画計画の解決）＋ 外観
    └──> Feature.Marquee.Core   何を計算するか（状態・判断・幾何）
           └──> Platform.Core（Es* 値型・ICoordTransform・ICursorService）
※ コアは本層を参照しない（本層がコアの幾何（`MarqueeGeometry`）を使う向きだけ）。
```

- 本層は UI フレームワークを参照しません（Avalonia / MewUI の型を持たない）。
- **描画 API の抽象を持ちません**。UI フレームワークごとの描画モデルの差は描画 API の抽象では吸収しきれない（WinUI3 / WinForms 等へ展開する場合に抽象が肥大する）ため、本層は描画計画というデータを返し、描画は各 UI が自 UI の API で行います。
- **描画 API の抽象を持ちません**: 本層は描画計画というデータを返し、描画は各 UI が自 UI の API で行います。

### 3.2 データフロー（入力 → 描画）

```mermaid
sequenceDiagram
    participant UI as UI 層（コントロール）
    participant P as MarqueePresenter（本層）
    participant M as Marquee（コア）
    participant D as MarqueeDrawPlan（本層）
    participant H as ホスト

    UI->>P: Press / Move / Release / KeyDown（ドメイン座標）
    P->>M: 入力の転送
    M-->>P: 効果（IMarqueeEffects 経由）・通知
    P-->>UI: Notified を転送
    UI->>H: 公開イベント
    UI->>P: ResolveDrawPlan(描画領域のサイズ, 実寸測定)
    P->>M: CreateRequest(描画領域のサイズ)
    M-->>P: MarqueeRenderRequest
    P->>M: ResolveGeometry(request)
    M-->>P: MarqueeGeometry（穴あけ矩形・ハンドル・マーカー・マウス位置）
    P->>P: 装飾矩形の導出・ラベルの文言と配置・層の順序・外観の解決
    P-->>D: 構築
    P-->>UI: MarqueeDrawPlan
    UI->>UI: Layers の順に自 UI の描画 API を呼ぶ
    UI->>H: Rendered（描画計画を渡す）
```

### 3.3 描画計画の解決フロー

```mermaid
flowchart TD
    A[ResolveDrawPlan] --> B[幾何の解決<br/>Marquee.ResolveGeometry]
    B --> C{外観の表示可否}
    C -->|ShowOverlay かつ穴あり| D[暗転領域 = 描画面 − 穴あけ矩形<br/>4 領域]
    C -->|ShowOuterBorder かつ穴あり| E[穴あけ枠 = 4 辺]
    C -->|ShowInnerFill / ShowInnerBorder かつ穴あり| F[内側の塗り・内枠]
    C -->|ShowHandles かつ選択あり| G[ハンドル]
    B --> H[マーカーはコアが nudge 中のみ返す]
    D --> I[ラベルの文言と配置<br/>MarqueeLabelTexts → MarqueeLabelPlan]
    E --> I
    F --> I
    G --> I
    H --> I
    I --> J[層の順序<br/>Decoration → Handles → Marker → Label]
    J --> K[MarqueeDrawPlan]
```

- **層は描く値がある場合だけ含めます**（層の有無 = 描く値の有無）。UI 層は層ごとの可否を判定しません。
- **テキストの層は必ず末尾**です（UI 要素として置く場合も子要素は親より後に描かれるため、常に最上位になります）。
- 穴あけ矩形は 1 回だけ解決し、暗転領域・枠線・内側・サイズラベルのすべてに同じ値を渡します（各所が個別に変換すると矩形がずれます）。

## 4. コンポーネント構成

ルート（`src/`）に置くのは**ファサード本体**（`MarqueePresenter.cs`）だけです。これ以外は用途別のフォルダへまとめます。
**1 ファイル 1 型**とし、**名前空間はフォルダに追従**させます（ルート直下のみ `EsUtil.Feature.Marquee.Presenter`。`Setting` / `Drawing` / `Label` / `Logging` は `EsUtil.Feature.Marquee.Presenter.<フォルダ名>`）。コア（`Feature.Marquee.Core`）と同じ規約です。

| ファイル | 型 | 可視性 | 責務 |
| --- | --- | --- | --- |
| `src/MarqueePresenter.cs` | `MarqueePresenter` | public（`sealed` でない） | 提示のファサード。コアの保持、設定・環境値・入力の転送、外観の保持、描画計画の解決（`ResolveDrawPlan`） |
| `src/Setting/MarqueeAppearance.cs` | `MarqueeAppearance` | public sealed record | 外観の単一所有者（色・ラベルの体裁・枠線の太さ・表示可否・述語） |
| `src/Drawing/MarqueeDrawPlan.cs` | `MarqueeDrawPlan` | public readonly record struct | 描画計画（UI が見る唯一の描画値） |
| `src/Drawing/MarqueeDrawLayer.cs` | `MarqueeDrawLayer` | public enum | 描画層の順序 |
| `src/Drawing/MarqueeDecorationGeometry.cs` | `MarqueeDecorationGeometry` | internal static | 枠線 4 辺・暗転領域の矩形の計算 |
| `src/Drawing/MarqueeTextPlacement.cs` | `MarqueeTextPlacement` | public readonly record struct | 描画層が UI 層へ渡すテキスト（文言・基準点・クランプ領域） |
| `src/Label/MarqueeLabelTexts.cs` | `MarqueeLabelTexts` | internal static | サイズラベル・カーソル座標ラベルの文言と基準点 |
| `src/Label/MarqueeLabelPlan.cs` | `MarqueeLabelPlan` | public static | 実寸 → ラベル要素の配置指示の写像 |
| `src/Label/MarqueeLabelPlacement.cs` | `MarqueeLabelPlacement` | public readonly record struct | ラベル要素の配置指示 |
| `src/Logging/MarqueePresenterLogMessage.cs` | `MarqueePresenterLogMessage` | internal static partial | 本層が単一所有する設定（外観・表示スケーリング係数）の変更ログ（変化した項目だけ） |

## 5. 単一所有（同じ真実を 2 箇所に持たない）

| 対象 | 所有者 |
| --- | --- |
| 色（ハンドル・十字マーカー・ラベル・装飾） | `MarqueeAppearance` |
| ラベルのフォントサイズ・内側余白と、その安全域（6〜48） | `MarqueeAppearance`（枠線の太さの安全域はコアの `Marquee.Limits` を参照する） |
| 表示可否（ハンドル・サイズラベル・座標ラベル・暗転・穴あけ枠・全面暗転・内側の塗りと枠線）とその述語 | `MarqueeAppearance` |
| 枠線の太さ（外側・内側）と補正 | `MarqueeAppearance`（補正の式はコアの `Marquee.Limits.CoerceBorderThickness` へ委譲する） |
| 描画する層とその順序（層にある ⇔ 描く値がある） | `MarqueePresenter.ResolveLayers`（`MarqueeDrawPlan.Layers`） |
| 枠線 4 辺の矩形（太さは辺ごとの式で導出） | `MarqueeDecorationGeometry.ComputeBorderRects` |
| 暗転領域（描画面から穴あけ矩形を除いた 4 領域） | `MarqueeDecorationGeometry.ComputeOverlayRegions` |
| サイズラベルの文言（書式・表示スケール） | `MarqueeLabelTexts.ResolveSizeLabelText`（書式は private の `FormatSizeLabel`） |
| 矩形に基づくラベルの基準点（穴あけ矩形の左上から「矩形の線幅 + 1 DIP」内側） | `MarqueeLabelTexts.ResolveLabelAnchor`（`MarqueeDrawPlan.LabelAnchor` としても公開する） |
| カーソル座標ラベルの文言と基準点（カーソル + 8.0 DIP） | `MarqueeLabelTexts.ResolveCoordLabelText` |
| ラベルの体裁（背景 = 実寸 + 内側余白 × 2・クランプ・並び順） | `MarqueeLabelPlan` |
| 本層が単一所有する設定（外観・表示スケーリング係数）の変更ログ（変化した項目だけ） | `MarqueePresenterLogMessage` |

## 6. 例外処理ポリシー

| 状況 | 扱い |
| --- | --- |
| `ResolveDrawPlan` の `measureText` が null | `ArgumentNullException`（呼び出し側の実装誤り） |
| `MarqueeLabelPlan.Resolve` の `appearance` / `measureText` が null | `ArgumentNullException` |
| 表示可否が無効な項目 | 空のコレクション / null として返す（例外にしない。UI 層は層を見るだけでよい） |
| サイズラベルの文言プロバイダが null または空文字を返す | 自動生成へフォールバックする（例外にしない） |

## 7. 可視性ポリシー

- UI 層とホストが使う面（`MarqueePresenter` / `MarqueeAppearance` / `MarqueeDrawPlan` / `MarqueeDrawLayer` / `MarqueeLabelPlan` / `MarqueeLabelPlacement` / `MarqueeTextPlacement`）は `public` とします。
- **本層の内部手順は `internal`** とします（`MarqueeDecorationGeometry` / `MarqueeLabelTexts` / `MarqueePresenterLogMessage`）。UI 層は解決済みの値（`MarqueeDrawPlan` の矩形・`Labels`）を受け取るため、これらを参照する必要がありません。単体テストにのみ `InternalsVisibleTo`（`Feature.Marquee.Presenter.Tests`）で公開します。
- **クラスが `internal` の場合、そのメンバーも `internal` とします**（`public` と書いても外部から見えないため、可視性の宣言を実態と一致させる）。
- `[EditorBrowsable]` による「見えにくくする」対処は行いません（公開面も契約も変わらず、見つけにくくなるだけのため）。

## 8. テスト方針

| 方針 | 内容 |
| --- | --- |
| UI 初期化不要 | 描画計画の値（矩形・層・配置・文言）は UI 型を持たないため `[Fact]` / `[Theory]` で並列実行する |
| 画素の検証 | MewUI（Skia）のテストが担う（`MarqueeControl.MewUI.Tests`）。本層は描画 API を持たないため画素テストを持たない |
| 単位 | 描画計画の値・外観・装飾矩形の計算・ラベルの写像・外観の変更ログ |
| 配置 | `tests/`（フォルダ分割なし。ファイル名は対象型 + `.Tests.cs`） |
| 件数 | `dotnet test <csproj> --list-tests` のクラス別集計で確認する（出力は行が折り返され、並列実行では行が混ざるため、行単位で対応付けてはいけない） |

```powershell
dotnet test src/Feature.Marquee.Presenter/tests/Feature.Marquee.Presenter.Tests.csproj
```

## 9. 設計判断（UI 非依存の境界）

本プロジェクトの型は、UI フレームワークの型をシグネチャに持たないことを境界の条件としています。

| 判断 | 内容 |
| --- | --- |
| 座標・寸法の型 | コア（`EsUtil.Platform.Core.Geometry`）の `EsPoint` / `EsRect` / `EsSize` を使う |
| 色の型 | UI 非依存の `System.Drawing.Color` とし、各 UI の色型への変換は各 UI の描画境界で行う |
| 描画 API の抽象を持たない | UI フレームワークごとの描画モデルの差は描画 API の抽象では吸収しきれない（WinUI3 / WinForms 等へ展開する場合に抽象が肥大する）。本層は描画計画を返し、描画は各 UI が自 UI の API で行う |
| 実寸測定 | 描画 API 依存のため `Func<string, double, EsSize>` を UI 層から受け取る（本層は測定 API を持たない） |
| 付加情報ラベルの配置 | コントロール固有の概念であるため本層に置かない（複合コントロールの `FadeMaskDragRectControl.UIBase` が持つ） |
| 組み込み規則を置かない | ホイールの解釈・ビューポート連携は「組み込み先が存在して初めて意味を持つ」（コントロール化を前提とした処理）ため、本層に置かず UI 層（ホスト）が持つ |
| 設定変更ログの所有者 | 値を所有する主体が記録する（コアの設定はコア、本層の外観は本層、UI の値は各 UI）。接頭辞から伝搬を追える |
| 外観の拡張点 | `MarqueePresenter` を `sealed` にしない（`Appearance` の差し替えと `ResolveDrawPlan` の上書きで独自の見た目を提供できることが役割） |

## 10. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
