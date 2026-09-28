# Feature.Marquee.Presenter

`MarqueeControl`（マーキー選択＝ドラッグで矩形を選び、移動・リサイズできるコントロール）を**コントロールとして成り立たせる UI 非依存の層**です。Avalonia 版と MewUI 版が同じ処理を共有します。

本層は 2 つの関心事を持ちます。どちらも「単体で使う `Marquee` だけでは決まらない」ため、コア（`Feature.Marquee.Core`）には置きません。

| 関心事 | 内容 | 代表型 |
| --- | --- | --- |
| **提示（Presenter）** | 描画の幾何（`MarqueeGeometry`）と外観から**描画計画**（層の順序・装飾矩形・文言・ラベル配置）を解決する | `MarqueePresenter` / `MarqueeDrawPlan` / `MarqueeDrawLayer` / `MarqueeDecorationGeometry` / `MarqueeLabelTexts` / `MarqueeLabelPlan` / `MarqueeHostLabelRenderer` |
| **外観** | 色・フォント・枠線の太さ・表示可否（描画にだけ必要な設定の単一値） | `MarqueeAppearance` |
| **組み込み規則** | 「他のコントロールへ組み込むときにだけ必要な規則」（ホイールの解釈・ビューポート変化の再通知） | `MarqueeWheelInterpreter` / `MarqueeViewportNotifier` / `WheelZoomRequest` / `WheelScrollRequest` / `IWheelScrollTarget` |

## 目的

- **「どう見せるか」を 1 箇所に集約する**。コア（`Feature.Marquee.Core`）は「何を計算するか（幾何・判断）」だけを決め、描画計画（層の順序・装飾矩形・文言・配置）と外観は本層が単一所有する。
- **描画は各 UI が行う（描画 API の抽象を持たない）**。UI フレームワークごとの描画モデルの差は描画 API の抽象では吸収しきれない（WinUI3 / WinForms 等へ展開する場合に抽象が肥大する）ため、本層は**描画計画というデータ**を返し、描画は各 UI（`MarqueeContextPainter` / `MarqueeSkiaPainter`）が自 UI の API で行う。
- **色・体裁・表示可否を 1 箇所に集約する**。ハンドル・十字マーカー・ラベル・装飾の色、ラベルのフォントサイズ、枠線の太さ、描くかどうかは**描画にだけ必要な設定**であり、コアの計算・判断には入らない（コアには寸法・対話パラメータを残す）。
- **組み込み規則を UI をまたいで 1 箇所に集約する**。ホイールの解釈（Ctrl = ズーム / 通常 = スクロール）とビューポート変化の再通知は、UI ごとに書くと片方だけ変わる。
- **UI 実装を薄くする**。UI 層は描画計画の取得と描画、ラベル要素の配置、入力の転送、通知の転送だけを行い、装飾矩形・文言・配置の計算を持たない。
- **UI 初期化なしで検証できる**。描画計画の値（矩形・層・配置）は UI 型を持たないため `[Fact]` で検証でき、画素の検証は MewUI（Skia）のテストが担う。ホイールの解釈も UI 型を持たない。
- UI フレームワークへ依存しない（参照は `Feature.Marquee.Core` と `Platform.Core` のみ）。
- UI 層が使う面は `public`、本層の内部手順は `internal`（単体テストにのみ `InternalsVisibleTo` で公開する）。

## 依存方向

層の全体像（Core / Presenter / UI）は [`src/README.md`](../README.md) を参照してください。

```text
UI 層（MarqueeControl.AvaloniaUI / MarqueeControl.MewUI）
  入力の受付・効果の適用・通知の転送・描画（自 UI の API）・ラベル要素の配置・ホスト結線
    ├──> Feature.Marquee.Presenter ← 本プロジェクト   提示（描画計画の解決）＋ 外観 ＋ 組み込み先があるときだけ必要な規則
    └──> Feature.Marquee.Core   何を計算するか（状態・判断・幾何）
           └──> Platform.Core（Es* 値型・ICoordTransform・ICursorService）
※ コアは本層を参照しない（本層がコアの幾何（`MarqueeGeometry`）を使う向きだけ）。
```

**帰属の判定**: 「何を計算するか（状態・判断・幾何）」はコア、「どう見せるか（描画計画・外観）」は本層、「実際に描く」は UI 層。

## 収録する型

### 提示（Presenter）

| 型 | 責務 |
| --- | --- |
| `MarqueePresenter` | 提示のファサード。コア（`Marquee`）を内部に保持し、設定・環境値・入力の転送と、外観の保持、描画計画の解決（`ResolveDrawPlan`）を行う。**`sealed` にしない**（外観の差し替え・`ResolveDrawPlan` の上書きで独自の見た目を提供できることが役割） |
| `MarqueeDrawPlan` | 1 フレーム分の描画計画（外観・描画面・穴あけ矩形・暗転領域・枠線矩形・内側の塗り・ハンドル・マーカー・層の順序・ラベル配置）。**UI が見る唯一の描画値** |
| `MarqueeDrawLayer` | 描画層（`Decoration` / `Handles` / `Marker` / `Label`）。描画順を単一所有する |
| `MarqueeDecorationGeometry` | 装飾の矩形の計算（枠線 4 辺・暗転領域＝描画面から穴あけ矩形を除いた 4 領域）（純粋関数） |
| `MarqueeTextPlacement` | 描画層が UI 層へ渡すテキスト（文言・基準点・アライン・クランプ領域）。**要素として置くか canvas へ描くかは UI 層が決める** |
| `MarqueeLabelTexts` | サイズラベル（位置と寸法の書式・表示スケール）・カーソル座標ラベルの文言と基準点の生成 |
| `MarqueeLabelPlacement` | ラベル要素の配置指示（背景矩形・内側余白・文言・フォントサイズ・色） |
| `MarqueeLabelPlan` | `MarqueeTextPlacement` から `MarqueeLabelPlacement` を解決する（実寸 → 配置の写像。体裁は `MarqueeAppearance` から受け取る） |
| `MarqueeHostLabelRenderer` | ホストが描く付加情報ラベル（操作・情報）の体裁（背景 = 実寸 + 内側余白 × 2、文字を後に描く）を適用する（文言・基準点はホストが与える） |

### 外観

| 型 | 責務 |
| --- | --- |
| `MarqueeAppearance` | 描画に使う色（ハンドル・ハンドル枠・十字マーカー・サイズラベル・座標ラベル・暗転・穴あけ枠・内側の塗りと枠線）・ラベルのフォントサイズと内側余白・**枠線の太さ 2 種**・**表示可否**（ハンドル・サイズラベル・座標ラベル・暗転・穴あけ枠・全面暗転・内側の塗りと枠線）の単一所有者。`Default` を基点に `with` 式で部分変更する。枠線の太さの安全域への補正は `Marquee.Limits.CoerceBorderThickness` へ委譲する |

### 組み込み規則

| 型 | 可視性 | 責務 |
| --- | --- | --- |
| `MarqueeWheelInterpreter` | public | ホイール入力を解釈し、ズーム要求の通知・連携先へのスクロール適用・スクロール要求の通知を行う（`Handle(controlKey, shiftKey, deltaX, deltaY, position)`）。解釈の単一所有とログ出力を担う |
| `IWheelScrollTarget` | public | スクロールの適用先（自 UI のスクロール機構を適合させて注入する）。固定値の移動量を自身のスクロール位置へ加算するだけ |
| `WheelScrollDelta` | internal | 回転量 → 移動量（DIP）の換算（1 ノッチ = `ScrollMath.WheelScrollUnit`・Shift で横）。適用先は換算済みの移動量を受け取るため、本層の内部手順に限定する |
| `WheelZoomRequest` | public | Ctrl+ホイールによるズーム要求（方向 `ZoomIn` と発生位置 `Position`） |
| `WheelScrollRequest` | public | 通常ホイールによるスクロール要求（`DeltaX` / `DeltaY` / `Shift` / `Position`）。回転量をベクターで受け取る API へ渡す `Delta`（`EsVector`）を提供する |
| `MarqueeWheelLogMessage` | internal | ズーム要求のログ定義（`ILogger` 拡張。書式 `[Feature.Marquee.Presenter] ZoomRequested: zoomIn={ZoomIn}`）。ホストはロガーをファサードへ渡すだけで、定義名を参照しない |
| `MarqueeViewportNotifier` | public | ビューポートの変化の再通知（現在の状態スナップショット）と再描画の要求。選択の状態は変更しない |

## 描画の単位

**ローカル DIP** です。描画面は `MarqueeDrawPlan.SurfaceBounds`（原点 (0, 0)・サイズは描画領域）で表します。装飾の矩形・ハンドル・マーカーの座標はすべて DIP で解決済みであり、UI は座標変換を行いません（ドメイン → DIP の変換はコアの `Marquee.ResolveGeometry` が済ませています）。

MewUI 版はキャンバス自体を DPI スケーリングしてから DIP 座標で描きます（描画計画の座標系と一致させるため）。

## ラベルは UI の要素が描く / UI が描く

| 対象 | 誰が描くか | 呼ぶ API |
| --- | --- | --- |
| 図形（装飾・ハンドル・始点マーカー） | UI 層（自 UI の描画 API） | `MarqueeDrawPlan.Layers` の順に自 UI の API を呼ぶ（`MarqueeContextPainter` / `MarqueeSkiaPainter`） |
| テキスト（サイズラベル・カーソル座標ラベル） | UI 層（要素として置くか画像として描くか） | 要素として置く場合は `MarqueeLabelPlan.Resolve`（配置指示）の結果を要素へ写す（子要素は親より後に描かれるため**常に最上位**）。canvas へ描く場合は `MarqueeDrawPlan.Labels` を自 UI の描画 API で文字として描く |
| 付加情報ラベル（操作・情報。アプリ固有） | ホスト | `MarqueeHostLabelRenderer.Draw` で体裁を揃えて描く（文言・積み上げ位置はホストが決める） |

**コアは幾何（穴あけ矩形・ハンドル・マーカー・マウス位置）だけを返します**。**文言（サイズの書式・座標）・体裁（フォントサイズ・色・内側余白）・表示可否は本層が単一所有**します。

## 見た目・描画の差し替え

| 差し替えたいもの | 方法 |
| --- | --- |
| **見た目（色・フォント・枠線の太さ・表示可否）** | `MarqueeAppearance` を変える（UI の `Appearance` プロパティへ設定する） |
| **描画計画の構成（層・装飾・文言の独自化）** | `MarqueePresenter` を継承し `ResolveDrawPlan` を上書きする、または `MarqueePresenterXxx` を作る |
| **描画そのもの** | UI 層の描画実装（`MarqueeContextPainter` / `MarqueeSkiaPainter`）を差し替える（描画 API は各 UI が自前で呼ぶ） |
| **寸法・対話パラメータ** | `MarqueeOptions`（`HandleSize` / `MarkerLength` / `HitRadius` / `MinSelectionSize` / `ClampToDomainBounds`）を変える（コアが単一所有する） |

## Core との境界（なぜ描画計画を Core に置かないか）

| 関心事 | 所有者 | 理由 |
| --- | --- | --- |
| 選択の判断（ドラッグ・nudge・確定/キャンセル・スナップ・最小サイズ）と幾何（穴あけ矩形・ハンドル・マーカー） | `Feature.Marquee.Core` | コントロールが何であっても変わらない（独立した部品の本体） |
| 描画計画（層の順序・装飾矩形・文言・配置）と外観（色・フォント・枠線の太さ・表示可否） | 本層 | 「どう見せるか」は描画の都合であり、コアの計算・判断に入らない（コアに置くと色・フォントをコアへ持ち込むことになる） |
| 装飾（暗転マスク・穴あけ枠・内側の塗り）の概念 | 本層 | 選択矩形コントロール単体の概念ではなく、撮影オーバーレイ等へ組み込むときにだけ必要になる表示である |
| ホイール要求（Ctrl = ズーム / それ以外 = スクロール）とそのログ | 本層 | 組み込み先（ビューポート）が存在して初めて意味を持つ |
| 連携先へのスクロール適用（回転量 → 移動量の換算と適用先への加算） | 本層 | 「1 ノッチ = 48 DIP」「Shift で横」は UI をまたいで同じ規則である |
| ビューポート変化の再通知（現在の状態スナップショット + 再描画） | 本層 | ビューポートが存在して初めて意味を持つ（単体の Marquee にはビューポートもズームも無い） |
| 描画するラベルの一覧と順序（サイズラベル → カーソル座標ラベル）と実寸 → 配置矩形の写像 | 本層（`MarqueeLabelPlan`） | 文字を UI 要素として置くコントロールに必要な手順であり、描画計画と同じ層が単一所有する。実寸測定は描画 API 依存のため UI 層が与える（ラベルは常に最上位） |
| 要求の実処理（ズーム率の決定・表示の適用・スクロール先の決定） | ホスト（ズームビューポート等） | ビューの状態を持つのはビューポートだけ |
| スクロール適用先の適合（UI 固有の型 → `IWheelScrollTarget`） | UI 層 | フレームワーク固有の型（Avalonia の `ScrollViewer` 等）を使う |
| 実際の描画（描画 API の呼び出し・ラベル要素の配置） | UI 層 | 描画 API はフレームワーク固有である |

## UI ごとの利用状況

| 型 | AvaloniaUI | MewUI | 補足 |
| --- | --- | --- | --- |
| `MarqueePresenter` / `MarqueeAppearance` / `MarqueeDrawPlan` / `MarqueeDecorationGeometry` / `MarqueeLabelTexts` / `MarqueeLabelPlan` / `MarqueeHostLabelRenderer` | 使う | 使う | 描画計画と外観は両 UI で共通 |
| 描画実装（`MarqueeContextPainter` / `MarqueeSkiaPainter`） | `MarqueeContextPainter` | `MarqueeSkiaPainter` | UI フレームワーク固有のため各 UI が所有する |
| `MarqueeWheelInterpreter` / `WheelZoomRequest` / `WheelScrollRequest` | 使う | 使う | ホイールの解釈は両 UI で共通 |
| `IWheelScrollTarget` / `AttachScrollTarget` | 使う（`ScrollViewer` を適合） | **使わない** | MewUI のホストはビューポート変化を `NotifyViewportChanged` で通知し、スクロールの適用先を持たない（適用先がある UI だけがこの抽象を実装する） |
| `MarqueeViewportNotifier` | 使う（`ScrollChanged` を購読） | 使う（ホストが `NotifyViewportChanged` を呼ぶ） | 契機は UI ごとに異なるが、規則は共通 |
| `WheelScrollDelta` / `MarqueeWheelLogMessage`（internal） | 参照しない | 参照しない | 本層の内部手順（適用先は換算済みの移動量を受け取る） |

**UI フレームワークによって未使用になる型が存在します**（`IWheelScrollTarget` は適用先を持つ UI だけが使う）。これは「抽象として複数の UI で共用できる処理を本層へ置き、必要とする UI だけが実装・利用する」設計の帰結であり、コア（`Feature.Marquee.Core`）の公開面には影響しません。

## 独自のコントロールへ組み込む（最小実装）

マーキー選択だけを持つ独自コントロールは、**入力の転送**・**効果の受領**・**描画**の 3 点を書けば作れる。

```csharp
// 1. 効果（本 UI が必要とするものだけ。MarqueeEffectsBase が他の効果の空実装を提供する）
private sealed class Effects(MyControl owner) : MarqueeEffectsBase
{
    public override void RequestRedraw() => owner.InvalidateVisual();
}

// 2. 生成（カーソル操作が不要なら cursor は null でよい。外観は MarqueeAppearance が持つ）
private readonly MarqueePresenter _presenter = new(new Effects(this));

// 3. 入力の転送（座標はドメイン座標へ変換して渡す）
protected override void OnPointerPressed(PointerPressedEventArgs e)
    => _presenter.Press(e.GetPosition(this).ToDomain(), e.ClickCount);

// 4. 描画（描画計画の解決は提示層、描画は自 UI の API）
protected override void Render(DrawingContext context)
{
    var plan = _presenter.ResolveDrawPlan(Bounds.Size.ToEsSize(), MeasureText);  // 幾何 + 外観 + 層 + 配置を解決済み
    MarqueeContextPainter.Paint(context, in plan);                              // 層の順に描く
    // ラベルを要素として置く場合は plan.Labels（配置指示）を要素へ写す
}
```

- 外観を変える場合は `_presenter.Appearance = MarqueeAppearance.Default with { ... }` を設定する（UI のプロパティ機構へ委譲してもよい）。- ラベルを要素として配置する UI は 4 の後、`plan.Labels`（`MarqueeLabelPlacement`。実寸を反映済み）を要素へ写す。
- ホスト固有の付加情報ラベルは `MarqueeHostLabelRenderer.Draw` で体裁を揃えて描く。
- 座標変換（ドメイン ⇔ DIP）は `MarqueePresenter.SetCoordinateTransform` へ注入する（未指定は等倍）。

ホイール連携（ズーム・スクロール）が必要な場合は、本層の組み込み規則を次のように使います。

```csharp
// UI 層（コントロール）: 提示層へ接続して 1 つのフィールドとして持つ
_wheel = new MarqueeWheelInterpreter(_presenter);
_wheel.ZoomRequested += (_, request) => ZoomRequested?.Invoke(this, request);
_wheel.ScrollRequested += (_, request) => ScrollRequested?.Invoke(this, request);

// 連携先（自 UI のスクロール機構）があれば適合させて注入する（スクロールは本層が完結させる）
_wheel.AttachScrollTarget(scrollViewer is null ? null : new ScrollViewerTarget(scrollViewer));

// 入力（UI イベント）から呼ぶ
_wheel.Handle(controlKey: (modifiers & Control) != 0, shiftKey: (modifiers & Shift) != 0,
              delta.X, delta.Y, position);
```

ホストは要求を購読し、ズーム率の決定と表示の適用を行います（例: `ZoomViewControl.ZoomByWheel(request.ZoomIn, ToViewportPosition(request.Position))`）。

```csharp
// UI 層（コントロール）: ビューポートの変化（パン・ズーム）を上位へ再通知する
_viewport = new MarqueeViewportNotifier(_presenter, _effects);
_viewport.ViewportChanged += (_, snapshot) => ViewportChanged?.Invoke(this, ToRectArgs(snapshot));

// 変化を検知したら呼ぶ（連携 ScrollViewer の ScrollChanged・ホストからの通知）
_viewport.Notify();   // 状態スナップショットの通知 + 再描画（選択の状態は変更しない）
```

## 依存関係

```text
Feature.Marquee.Presenter
    ├── Feature.Marquee.Core（幾何・状態・判断・公開ファサード Marquee）
    └── Platform.Core（Es* 値型・ICoordTransform・ICursorService・ScrollMath）
    └── Microsoft.Extensions.Logging.Abstractions（ズーム要求の LoggerMessage 定義）
```

**UI フレームワークへ依存しません**（Avalonia / MewUI の型を参照せず、描画 API の抽象も持ちません）。

名前空間は `EsUtil.Feature.Marquee.Presenter`。アセンブリ名は `Feature.Marquee.Presenter`。

## テスト

`tests/` が本プロジェクトのテストを持ちます。描画計画の値（矩形・層・配置・文言）は UI 型を持たないため、**UI 初期化なしで検証**できます（合計 105 件）。画素の検証は `MarqueeControl.MewUI.Tests`（Skia）が担います。

```text
tests/
├── MarqueeDrawPlan.Tests.cs           # 描画計画の値（層の順序・描く値の有無・装飾矩形・座標変換）（12 件）
├── MarqueePresenter.Tests.cs          # 描画計画の解決（幾何・外観・層・配置・文言プロバイダ）・外観の保持・表示スケーリング・入力の転送（16 件）
├── MarqueeAppearance.Tests.cs         # 色・体裁・枠線の太さ・表示可否の既定値と補正（28 件）
├── MarqueeDecorationGeometry.Tests.cs # 枠線 4 辺・暗転領域の矩形の計算（9 件）
├── MarqueeLabelPlan.Tests.cs          # 実寸 → ラベル要素の写像（順序・配置・クランプ・色）（12 件）
├── MarqueeHostLabelRenderer.Tests.cs  # 付加情報ラベルの体裁（背景・文字の順序・余白）（5 件）
├── MarqueeWheelInterpreter.Tests.cs   # 解釈・通知・連携先への適用・ログ（15 件）
├── WheelRequest.Tests.cs              # 要求の値（回転量ベクター・方向と位置）（3 件）
├── MarqueeViewportNotifier.Tests.cs   # ビューポート変化の通知（スナップショット・再描画・状態不変）（5 件）
├── RecordingLogger.cs                 # ログ出力を収集するテスト用 ILogger
└── TestHelper.cs                      # テスト用の描画先（Skia）
```

件数は `dotnet test <csproj> --list-tests` のクラス別集計で確認します（`dotnet test` の出力は行が折り返され、並列実行では行が混ざるため、行単位で対応付けてはいけません）。

```powershell
dotnet test src/Feature.Marquee.Presenter/tests/Feature.Marquee.Presenter.Tests.csproj
```

## ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
