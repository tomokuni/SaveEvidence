# LoupeControl.UIBase

`LoupeControl`（拡大鏡＝カーソル位置の周囲を拡大表示するコントロール）を**コントロールとして成り立たせる UI 非依存の層**です。Avalonia 版と MewUI 版が同じ処理を共有します。

本層は 2 つの関心事を持ちます。どちらも「単体で使う `Loupe` だけでは決まらない」ため、コア（`Feature.LoupeCore`）には置きません。

| 関心事 | 内容 | 代表型 |
| --- | --- | --- |
| **提示（Presenter）** | 描画の幾何（`LoupeGeometry`）と外観から**描画計画**（層の順序・装飾矩形・文言・ラベル配置）を解決する | `LoupePresenter` / `LoupeDrawPlan` / `LoupeDrawLayer` / `LoupeDecorationGeometry` / `LoupeLabelTexts` / `LoupeLabelPlan` |
| **外観** | 色・フォント・角丸・枠線の太さ・表示可否（描画にだけ必要な設定の単一値） | `LoupeAppearance` |
| **組み込み規則** | 「他のコントロールへ組み込むときにだけ必要な規則」（寸法の物理ピクセル換算） | `LoupeMetricsScaler` |

## 目的

- **「どう描くか」を 1 箇所に集約する**。コア（`Feature.LoupeCore`）は「何を描くか（幾何・判断）」だけを決め、描画計画（層の順序・装飾矩形・文言）と外観は本層が単一所有する。
- **描画は各 UI が行う（描画 API の抽象を持たない）**。UI フレームワークごとの描画モデルの差は描画 API の抽象では吸収しきれない（WinUI3 / WinForms 等へ展開する場合に抽象が肥大する）ため、本層は**描画計画というデータ**を返し、描画は各 UI（`LoupeContextPainter` / `LoupeSkiaPainter`）が自 UI の API で行う。
- **UI 実装を薄くする**。UI 層は描画計画の取得と描画、ラベル要素の配置、入力の転送だけを行い、装飾矩形・文言・配置の計算を持たない。
- **UI 初期化なしで検証できる**。描画計画の値（矩形・層・配置）は UI 型を持たないため `[Fact]` で検証でき、画素の検証は MewUI（Skia）のテストが担う。
- UI フレームワークへ依存しない（参照は `Feature.LoupeCore` と `Platform.Core` のみ）。

## 依存方向

層の全体像（Core / UIBase / UI）は [`src/README.md`](../README.md) を参照してください。

```text
UI 層（LoupeControl.AvaloniaUI / LoupeControl.MewUI）
  入力の受付・描画（自 UI の API）・ラベル要素の配置・ホスト結線
    ├──> LoupeControl.UIBase ← 本プロジェクト   提示（描画計画の解決）＋ 外観 ＋ 組み込み先があるときだけ必要な規則
    └──> Feature.LoupeCore   何を描くか（幾何）+ 計算（状態・値・純粋計算）
           └──> Platform.Core（Es* 値型・ICoordTransform）
※ コアは本層を参照しない（本層がコアの幾何（`LoupeGeometry`）を使う向きだけ）。
```

**帰属の判定**: 「何を描くか（幾何・判断）」はコア、「どう見せるか（描画計画・外観）」は本層、「実際に描く」は UI 層。

## 収録する型

### 提示（Presenter）

| 型 | 責務 |
| --- | --- |
| `LoupePresenter` | 提示のファサード。コア（`Loupe`）を内部に保持し、設定・環境値・拡大中心の転送と、外観の保持、描画計画の解決（`ResolveDrawPlan`）・表示レイアウトの解決（`ResolveLayout`）を行う。**`sealed` にしない**（外観の差し替え・`ResolveDrawPlan` の上書きで独自の見た目を提供できることが役割） |
| `LoupeDrawPlan` | 1 フレーム分の描画計画（描画面・拡大画像の幾何・十字線 / 枠線の矩形・層の順序・ラベル配置・通知用の値）。**UI が見る唯一の描画値** |
| `LoupeDrawLayer` | 描画層（`Pasteboard` / `Image` / `Crosshair` / `Border` / `Label`）。描画順を単一所有する |
| `LoupeDecorationGeometry` | 十字線（縦横 2 本）と枠線（4 辺）の矩形の計算（純粋関数） |
| `LoupeTextPlacement` | 描画層が UI 層へ渡すテキスト（文言・基準点・アライン・描画領域）。**要素として置くか canvas へ描くかは UI 層が決める** |
| `LoupeLabelTexts` | 中心座標ラベルの文言（`EsPoint.Format`）と基準点（上端中央）の生成 |
| `LoupeLabelPlacement` | ラベル要素の配置指示（背景矩形・内側余白・文言・フォントサイズ・色・角丸） |
| `LoupeLabelPlan` | `LoupeTextPlacement` から `LoupeLabelPlacement` を解決する（体裁は `LoupeAppearance` から受け取る） |

### 外観

| 型 | 責務 |
| --- | --- |
| `LoupeAppearance` | 描画に使う色（ペーストボード・枠線・十字線・中心座標ラベルの 5 色）・ラベルのフォントサイズと角丸・**枠線の太さ**・**表示可否**（十字線・枠線・中心座標ラベル）の単一所有者。`Default` を基点に `with` 式で部分変更する。枠線の太さの安全域への補正は `Loupe.Limits.CoerceBorderThickness` へ委譲する |

### 組み込み規則

| 型 | 責務 |
| --- | --- |
| `LoupeMetricsScaler` | 枠線の太さ（DIP）の物理ピクセル換算（切り上げ・最低 1）。描画計画の解決時に適用する |

### UI が出力するログ

| 型 | 責務 |
| --- | --- |
| `LoupeUiLogMessage` | UI が出力するログ定義（`LoggerMessage` ソース生成。拡大元画像の変更 `LogSourceChanged` / 描画の例外 `LogUpdateError`）。**書式・レベルを UI 間で揃えるため本層が単一所有する** |
| `LoupeLogFormat` | UI がログへ出す値の書式（未設定の表記 `Null` / 画像の寸法 `DescribeImage`） |

コアが出力する定義（設定値・拡大中心の変更）は `Feature.LoupeCore` の `LoupeLogMessage`（internal）が単一所有します。**UI 固有の処理に対応するログ**（中間ビットマップを生成する描画パイプライン固有のログ等）は各 UI プロジェクトが所有します。

## 描画の単位

**物理ピクセル**です。`LoupeGeometry.PixelSize` が作る正方形が描画面になります。DIP → 物理ピクセルの換算（表示サイズは `Loupe.ResolveGeometry`、寸法は `LoupeMetricsScaler`）は**コアと本層が済ませている**ため、UI は換算しません。中心座標ラベルだけは DIP で配置します（`LoupeLabelTexts` が基準点を、`LoupeLabelPlan` が配置矩形を決める）。

## ラベルは UI の要素が描く / UI が描く

| 対象 | 誰が描くか | 呼ぶ API |
| --- | --- | --- |
| 図形（ペーストボード（画像外の色）・拡大画像・十字線・枠線） | UI 層（自 UI の描画 API） | `LoupeDrawPlan.Layers` の順に自 UI の API を呼ぶ（`LoupeContextPainter` / `LoupeSkiaPainter`） |
| テキスト（中心座標） | UI 層（要素として置くか画像として描くか） | 要素として置く場合は `LoupeLabelPlan.Resolve`（配置指示）の結果を要素へ写す（子要素は親より後に描かれるため**常に最上位**）。canvas へ描く場合は `LoupeDrawPlan.Labels` を自 UI の描画 API で文字として描く |

## 見た目・描画の差し替え

| 差し替えたいもの | 方法 |
| --- | --- |
| **見た目（色・フォント・角丸・枠線の太さ・表示可否）** | `LoupeAppearance` を変える（UI の `Appearance` プロパティへ設定する） |
| **描画計画の構成（層・配置の独自化）** | `LoupePresenter` を継承し `ResolveDrawPlan` を上書きする、または `LoupePresenterXxx` を作る |
| **描画そのもの** | UI 層の描画実装（`LoupeContextPainter` / `LoupeSkiaPainter`）を差し替える（描画 API は各 UI が自前で呼ぶ） |
| **表示サイズ・拡大率** | `LoupeOptions`（`RequestedSize` / `ZoomLevel`）を変える（コアが単一所有する） |

## Core との境界（なぜ描画計画を Core に置かないか）

| 関心事 | 所有者 | 理由 |
| --- | --- | --- |
| 表示レイアウト・描画の幾何（切り出し領域・描画面）の解決 | `Feature.LoupeCore` | 単体で使っても同じ結果になる（独立した部品の本体） |
| 描画計画（層の順序・装飾矩形・文言・配置）と外観 | 本層 | 「どう見せるか」は描画の都合であり、コアの計算・判断に入らない（コアに置くと色・フォントをコアへ持ち込むことになる） |
| 中間ビットマップの非同期生成と世代番号管理 | `LoupeControl.AvaloniaUI` | **非同期の再描画をいつ何回行うか**は、中間ビットマップを持つ UI にだけ必要な処理である（同期描画の MewUI には概念が無い） |
| 実際の描画（描画 API の呼び出し・ラベル要素の配置） | UI 層 | 描画 API はフレームワーク固有である |

## UI ごとの利用状況

| 型 | AvaloniaUI | MewUI | 補足 |
| --- | --- | --- | --- |
| `LoupePresenter` / `LoupeAppearance` / `LoupeDrawPlan` / `LoupeDecorationGeometry` / `LoupeLabelTexts` / `LoupeLabelPlan` / `LoupeMetricsScaler` | 使う | 使う | 描画計画と外観は両 UI で共通 |
| 描画実装（`LoupeContextPainter` / `LoupeSkiaPainter`） | `LoupeContextPainter` | `LoupeSkiaPainter` | UI フレームワーク固有のため各 UI が所有する |
| 再描画のバッチ化（`LoupeRenderCoordinator`） | 使う（中間ビットマップを非同期生成し `Image.Source` へ反映） | **使わない** | MewUI は `OnPaintSurface` の中で同期描画するため、要求の合流と世代番号管理が不要（`LoupeControl.AvaloniaUI` が所有する） |

**UI フレームワークによって未使用になる型が存在します**（`LoupeRenderCoordinator` は中間ビットマップを持つ UI だけが使う）。これは「必要とする UI だけが実装・利用する」設計の帰結であり、コア（`Feature.LoupeCore`）の公開面には影響しません。

## 独自のコントロールへ組み込む（最小実装）

ルーペ（拡大鏡）を独自コントロールとして実装する場合、**状態の更新**と**描画**から作れます。

```csharp
// 1. 提示層（既定実装）を生成する。外観は LoupeAppearance（既定は LoupeAppearance.Default）、設定は LoupeOptions
private readonly LoupePresenter _presenter = new();
private EsBitmap? _source;   // 拡大元の画像（本 UI が解決して渡す）

// 2. 状態の更新（マウス移動などで拡大中心を設定する）
public void SetCenter(EsPoint domainPoint)
{
    _presenter.CenterPoint = domainPoint;
    InvalidateVisual();
}

// 3. 描画（描画計画の解決は提示層、描画は自 UI の API）
protected override void Render(DrawingContext context)
{
    var plan = _presenter.ResolveDrawPlan(displaySize, surfaceSize, MeasureText);  // 幾何・層・文言・配置を解決済み
    LoupeContextPainter.Paint(context, _source, in plan);                          // 層の順に描く
    // ラベルを要素として置く場合は plan.Labels（配置指示）を要素へ写す
}
```

- 表示位置（左上）が必要なら `LoupePresenter.ResolveLayout(parentSize)` を使う（親領域へ収めるクランプはコアが単一所有する）。
- 拡大元画像は UI ごとに型が異なるため `LoupeDrawPlan` に含めません。UI が `EsBitmap` を解決して描画へ渡します。
- 実寸測定（`MeasureText`）は描画 API 依存のため UI 層が与えます。

## 依存関係

```text
LoupeControl.UIBase
    ├── Feature.LoupeCore（描画の幾何・寸法・切り出しの計算）
    └── Platform.Core（Es* 値型）
```

**UI フレームワークへ依存しません**（描画 API の抽象も持ちません）。

名前空間は `EsUtil.SaveEvidence.Loupe.UIBase`。アセンブリ名は `LoupeControl.UIBase`。

## テスト

`tests/` が本プロジェクトのテストを持ちます。描画計画の値（矩形・層・配置）は UI 型を持たないため、**UI 初期化なしで検証**できます（合計 49 件）。画素の検証は `LoupeControl.MewUI.Tests`（Skia）が担います。

```text
tests/
├── LoupeDrawPlan.Tests.cs              # 描画計画の値（描画面・層の順序・表示可否・切替）（10 件）
├── LoupeAppearance.Tests.cs            # 色・体裁・枠線の太さ・表示可否の既定値と補正（11 件）
├── LoupeDecorationGeometry.Tests.cs    # 十字線 / 枠線の矩形の計算（5 件）
├── LoupeMetricsScaler.Tests.cs         # 枠線の太さの物理ピクセル換算（4 件）
├── LoupeLabelPlan.Tests.cs             # 実寸 → ラベル要素の写像（7 件）
├── LoupeLogFormat.Tests.cs             # ログの書式（画像寸法）（5 件）
├── LoupeUiLogMessage.Tests.cs          # UI が出力するログ定義（7 件）
└── TestHelper.cs                       # テスト用のユーティリティ
```

```powershell
dotnet test src/LoupeControl.UIBase/tests/LoupeControl.UIBase.Tests.csproj
```

## ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
