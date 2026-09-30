# Feature.Loupe.Presenter システム仕様書

ルーペ（拡大鍵）の見た目を定める **コントロール化を前提としない UI 非依存層**の構造・依存・例外処理・設計判断を記載します。 公開 API は [`SPEC_API.md`](SPEC_API.md)、使い方は [`../README.md`](../README.md) を参照してください。

## 1. システム概要

| 項目 | 内容 |
| --- | --- |
| 目的 | 「どう見せるか」（描画計画と外観）を 1 箇所に集約し、UI ごとの実装差から切り離す |
| 提供形態 | クラスライブラリ（`Feature.Loupe.Presenter.dll`） |
| 想定利用者 | UI 実装と、それらを組み込むホストアプリケーション |
| 汎用性 | 特定のアプリに依存しない（描画 API の抽象も持たないため、UI フレームワーク非依存） |

## 2. 技術スタック

| 項目 | 内容 |
| --- | --- |
| ターゲット | `net10.0` |
| 言語 | C# 14（`Nullable` 有効・`ImplicitUsings` 有効） |
| ルート名前空間 | `EsUtil.Feature.Loupe.Presenter`（配下はフォルダに追従: `.Setting` / `.Drawing` / `.Label` / `.Logging`） |
| ドキュメント生成 | `GenerateDocumentationFile` 有効（`cref` の検証のみ） |
| テスト | xUnit v3（UI 初期化不要。 `InternalsVisibleTo` で `internal` を検証する） |
| 依存 | `Feature.Loupe.Core` と `Platform.Core` のみ（UI フレームワーク・SkiaSharp を参照しない） |

## 3. アーキテクチャ

### 3.1 依存方向

```mermaid
graph LR
  UI["UI 実装"] --> R[Feature.Loupe.Presenter]
  R --> B[Feature.Loupe.Core]
  B --> D[Platform.Core]
```

- 本層は UI フレームワークを参照しません（**描画 API の抽象も持ちません**）。
- コアは本層を参照しません（本層がコアの幾何を使う向きだけ）。

### 3.2 データフロー（描画計画 → 描画）

```mermaid
sequenceDiagram
  participant UI as UI 実装
  participant P as LoupePresenter（本層）
  participant L as Loupe（コア）
  participant Painter as 描画実装（UI）

  UI->>P: ResolveDrawPlan(表示サイズ, 実寸測定)
  P->>L: ResolveGeometry(表示サイズ)
  L-->>P: LoupeGeometry（物理ピクセル・切り出し領域）
  P->>P: 十字線 / 枠線の矩形・文言・層の順序・配置を解決（外観を使用）
  P-->>UI: LoupeDrawPlan
  UI->>Painter: Paint(canvas, source, plan)
  Painter-->>UI: 層の順に描画（自 UI の API）
```

## 4. コンポーネント構成

ルート（`src/`）に置くのは**ファサード本体**（`LoupePresenter.cs`）とその partial（`LoupePresenter.DrawPlan.cs` / `LoupePresenter.LabelPlan.cs` / `LoupePresenter.LabelTexts.cs`）です。 描画計画の値・外観・ラベル要素の配置指示は用途別のフォルダへまとめます。
**1 ファイル 1 型**とし、**名前空間はフォルダに追従**させます（ルート直下のみ `EsUtil.Feature.Loupe.Presenter`。 `Setting` / `Drawing` / `Label` / `Logging` は `EsUtil.Feature.Loupe.Presenter.<フォルダ名>`）。 コア（`Feature.Loupe.Core`）と同じ規約です。

| ファイル | 型 | 可視性 | 責務 |
| --- | --- | --- | --- |
| `src/LoupePresenter.cs` | `LoupePresenter` | public | 提示のファサード。 コアのラップ、外観の保持、描画計画（`ResolveDrawPlan`）と表示レイアウト（`ResolveLayout`）の解決、層の組合せ表の単一所有 |
| `src/Setting/LoupeAppearance.cs` | `LoupeAppearance` | public | 外観（色・フォント・角丸・枠線の太さ・表示可否）の単一所有と補正 |
| `src/Drawing/LoupeDrawPlan.cs` | `LoupeDrawPlan` | public | 描画計画の値（UI が見る唯一の描画値） |
| `src/Drawing/LoupeDrawLayer.cs` | `LoupeDrawLayer` | public | 描画層と順序の単一所有 |
| `src/Drawing/LoupeTextPlacement.cs` | `LoupeTextPlacement` | public | 文言・基準点・描画領域 |
| `src/LoupePresenter.DrawPlan.cs` | `LoupePresenter`（`DrawPlan` partial） | internal | 十字線（縦横 2 本）と枠線（4 辺）の矩形の計算 |
| `src/Label/LoupeLabelPlacement.cs` | `LoupeLabelPlacement` | public | ラベル要素の配置指示 |
| `src/LoupePresenter.LabelPlan.cs` | `LoupePresenter`（`LabelPlan` partial） | internal | 実寸 → 配置指示の写像（クランプの単一所有） |
| `src/LoupePresenter.LabelTexts.cs` | `LoupePresenter`（`LabelTexts` partial） | internal | 文言（`EsPoint.Format`）と基準点（左上の内側）の生成 |
| `src/Logging/LoupePresenterLogMessage.cs` | `LoupePresenterLogMessage` | internal | 本層が単一所有する設定（外観）の変更ログ |

## 5. 単一所有（同じ真実を 2 箇所に持たない）

| 計算 / 値 | 所有者 |
| --- | --- |
| 外観（色・フォント・角丸・枠線の太さ・表示可否）とその既定値 | `LoupeAppearance` |
| 枠線の太さの安全域への補正 | `LoupeAppearance`（`init` アクセサ。 規則は `Loupe.Limits`） |
| 描画層とその順序 | `LoupeDrawPlan.Layers`（組合せ表は `LoupePresenter` が事前生成する） |
| 描く層の選択（描く値がある層だけを含める） | `LoupePresenter.ResolveLayers` |
| 十字線・枠線の矩形 | `LoupePresenter`（`DrawPlan` partial） |
| 枠線の太さの物理ピクセル換算 | `LoupePresenter.ResolveDrawPlan`（換算の式は `Platform.Core` の `DpiScaler.ScaleToPixel`） |
| 中心座標ラベルの文言と基準点 | `LoupePresenter`（`LabelTexts` partial） |
| ラベルの背景矩形・文字原点（実寸 + 余白、クランプ） | `LoupePresenter`（`LabelPlan` partial） |
| 描画完了通知の組み立てに使う値の選択 | `LoupeDrawPlan`（コアの幾何から写す） |
| 本層が単一所有する設定（外観）の変更ログ | `LoupePresenterLogMessage` |

## 6. 例外処理ポリシー

`LoupePresenter.ResolveDrawPlan` / `LoupePresenter.ResolveLabelPlacements` は引数（`measureText` / `appearance`）が null なら `ArgumentNullException` を送出します。 描画中にソースが破棄された場合（`ObjectDisposedException`）の吸収は描画実装（各 UI）の責務です。

## 7. 可視性ポリシー

- UI が参照する型（`LoupePresenter` / `LoupeAppearance` / `LoupeDrawPlan` / `LoupeDrawLayer` / `LoupeTextPlacement` / `LoupeLabelPlacement` / `LoupePresenter`（`LabelPlan` partial））は `public` とします。
- 本層とテストだけが使う内部ヘルパー（`LoupePresenter`（`DrawPlan` partial） / `LoupePresenter`（`LabelTexts` partial） / `LoupePresenterLogMessage`）は `internal` とし、単体テストにのみ `InternalsVisibleTo`（`Feature.Loupe.Presenter.Tests`）で公開します。

## 8. テスト方針

| 方針 | 内容 |
| --- | --- |
| UI 初期化不要 | 描画計画の値は UI 型を持たないため `[Fact]` / `[Theory]` で並列実行する |
| 単位 | 描画計画の値・外観・装飾矩形・寸法換算・ラベル写像・ログ定義 |
| 画素の検証 | 描画 API を持つ UI 側のテストが担う（本層は描画 API を持たないため） |
| 合計 | 49 件（`[Theory]` は展開後のケース数） |

```powershell
dotnet test src/Feature.Loupe.Presenter/tests/Feature.Loupe.Presenter.Tests.csproj
```

## 9. 設計判断（UI 非依存の境界と描画 API の抽象を持たない理由）

| 判断 | 内容 |
| --- | --- |
| 描画計画を本層に置く（コアに置かない） | コアは「何を描くか（幾何・判断）」だけを決める。 装飾矩形・文言・表示可否・描画順は描画の都合であり、コアに置くと色・フォントをコアへ持ち込むことになる |
| **描画 API の抽象を持たない** | UI フレームワークごとの描画モデルは異なり、抽象に集約すると対象 UI（WinUI3 / WinForms 等）が増えるたびに抽象が肥大して差異を吸収しきれなくなる。 本層は描画計画というデータを返し、描画は各 UI が自前で行う |
| 色の型 | UI 非依存の `System.Drawing.Color` とし、UI の色型への変換は描画の境界で行う |
| ラベルの体裁 | 「背景 = 文字の実寸 + 内側余白 × 2」「クランプ」は描画にだけ必要な規則のため、本層の `LoupePresenter`（`LabelPlan` partial） が単一所有する |
| 中間ビットマップの非同期生成 | UI のディスパッチャーに依存し、中間ビットマップを持つ UI にだけ必要なため、その UI 実装が所有する |
| 継承による差し替え | `LoupePresenter` は `sealed` にしない（外観の差し替え・`ResolveDrawPlan` の上書きで独自の見た目を提供することを役割とする） |

## 10. ログ

本層が出力するログの定義と書式は本層が単一所有します（`[Feature.Loupe.Presenter]` の接頭辞）。

**値の保持と変更の記録はコアが行います**（`Feature.Loupe.Core` の `LoupeLogMessage`）。 本層はコアへ伝搬するだけの値についても**本層を通過したことを Trace で記録**します（ホストの設定が本層を通ったかを確認できるようにするため。 既定の出力では溢れさせません）。

| 契機 | 出力 | レベル |
| --- | --- | --- |
| 生成（`Log` の初回設定時） | `Presenter created` | Information |
| `Log` の set | `Logger attached`（通過の記録） | Trace |
| `Options` / `DipToDeviceScaling` / `CenterPoint` / `PositionOffset` の set | `Forwarded: {名前} {値}`（通過の記録） | Trace |
| `SetCoordTransform` | `Forwarded: SetCoordTransform {ポート}`（通過の記録） | Trace |
| `Appearance` の set | 変化した項目だけ（`LogAppearanceChanged`。 書式 `Setting: {Name} {OldValue} → {NewValue}`） | Debug |
| `ResolveLayout` / `ResolveDrawPlan` | 出力しない（毎フレーム呼ばれる） | — |

**生成の記録はコンストラクターではなく `Log` の初回設定時に行います**（構築時点では出力先が未設定であり、その場で出力しても記録が残らないため。 コアも同じ規則で生成を記録します）。

**UI が出力するログ（拡大元画像の変更・描画の例外）の定義と書式も本層が単一所有します**（`[Feature.Loupe.Presenter]` の接頭辞）。 **UI 固有の処理に対応するログは各 UI 実装が所有します**（中間ビットマップを生成する描画パイプライン固有のログ等）。
