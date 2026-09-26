# MarqueeControl.UIBase

`MarqueeControl`（マーキー選択＝ドラッグで矩形を選び、移動・リサイズできるコントロール）を**コントロールとして成り立たせる UI 非依存の層**です。Avalonia 版と MewUI 版が同じ処理を共有します。

本層は 2 つの関心事を持ちます。どちらも「単体で使う `Marquee` だけでは決まらない」ため、コア（`Feature.MarqueeCore`）には置きません。

| 関心事 | 内容 | 代表型 |
| --- | --- | --- |
| **描画手順** | 色（描画にだけ必要な設定）と「どう描くか」（描画 API の呼び出し手順＝層の順序と写し方） | `MarqueePaint` / `MarqueeRenderer` / `MarqueeOuterRenderer` / `MarqueeInnerRenderer` / `MarqueeLabelPlan` / `MarqueeHostLabelRenderer`（写し方の基底はコアの `MarqueeRendererBase`） |
| **組み込み規則** | 「他のコントロールへ組み込むときにだけ必要な規則」（ホイールの解釈・ビューポート変化の再通知） | `MarqueeWheelInterpreter` / `MarqueeViewportNotifier` / `WheelZoomRequest` / `WheelScrollRequest` / `IWheelScrollTarget` |

## 目的

- **「どう描くか」を 1 箇所に集約する**。コア（`Feature.MarqueeCore`）は「何を描くか（判断とデータ）」だけを決め、描画 API の呼び出し手順は本層が単一所有する。
- **色を 1 箇所に集約する**。ハンドル・十字マーカー・ラベル・装飾の色は**描画にだけ必要な設定**であり、コアの計算・判断には入らない。したがってコアには置かず、本層の `MarqueePaint` が単一所有する（コアは寸法・表示可否・対話パラメータを持つ）。
- **描画を差し替えられる**。描画先は `IEsCanvas`（`Platform.Core`、UI 非依存の描画ポート）であり、描画手順は `IMarqueeRenderer`（`Feature.MarqueeCore` の契約）を実装した型で置き換えられる（ファサードの `Marquee.Renderer` へ設定する）。UI の追加・描画方式の変更はポートまたは実装の差し替えだけで済む。
- **組み込み規則を UI をまたいで 1 箇所に集約する**。ホイールの解釈（Ctrl = ズーム / 通常 = スクロール）とビューポート変化の再通知は、UI ごとに書くと片方だけ変わる。
- **UI 実装を薄くする**。UI 層は入力の受付・効果の適用・通知の転送・描画先の用意・ラベル要素の配置だけを行い、判断と手順は本層とコアが持つ。
- **UI 初期化なしで検証できる**。描画の実行は Skia（`SkiaEsCanvas`）で行うため、ウィンドウなしの画素テストが書ける（`tests/`）。ホイールの解釈も UI 型を持たないため `[Fact]` で検証できる。
- UI フレームワークへ依存しない（参照は `Feature.MarqueeCore` と `Platform.Core` のみ）。
- UI 層が使う面は `public`、本層の内部手順は `internal`（単体テストにのみ `InternalsVisibleTo` で公開する）。

## 層の分担

層の全体像（Core / UIBase / UI）は [`src/README.md`](../README.md) を参照してください。

```text
UI 層（MarqueeControl.AvaloniaUI / MarqueeControl.MewUI）
  入力の受付・効果の適用・通知の転送・描画先の用意・ラベル要素の配置
    ├──> MarqueeControl.UIBase ← 本プロジェクト   どう描くか（色・描画手順）＋ 組み込み先がある場合にだけ必要な規則
    └──> Feature.MarqueeCore   何を描くか + 判断（状態・値・純粋計算・コア⇔UI の契約）
           ├──> Common（IEsCanvas・EsLabelLayout（ラベルの体裁））
           └──> Platform.Core（Es* 値型・ICoordTransform・ICursorService）
※ コアは本層を参照しない（本層の描画実装はコアの契約 IMarqueeRenderer を実装する）。
```

**帰属の判定**: 「何を描くか（判断・データ・寸法・表示可否）」はコア、「どう 描くか（色・API 呼び出し）」は本層。`Marquee.ResolveFrame`（描くかどうかの判断）はコアに残る。

## 収録する型

### 描画手順

| 型 | 責務 |
| --- | --- |
| `MarqueePaint` | **色とラベルのフォントサイズの単一所有者**（ハンドル・十字マーカー・ラベル・装飾）。`Default` を基点に `with` 式で部分変更する |
| `MarqueeRenderer` | `MarqueeRendererBase` の既定実装（sealed）。描画の設定（`MarqueePaint`）を必須引数で受け取り、`DrawShapes`（図形のみ。**テキストは描かない**）とフォントサイズ（`SizeLabelFontSize` / `CoordLabelFontSize`）を提供する |
| `MarqueeOuterRenderer` | 選択矩形の外側の装飾（暗転マスク・穴あけ枠）を描く（色は引数の `MarqueePaint` から引く） |
| `MarqueeInnerRenderer` | 穴あけ矩形の内側の装飾（塗り・内枠）を描く（色は引数の `MarqueePaint` から引く） |
| `MarqueeLabelPlan` | コアのテキスト配置（`MarqueeTextPlacement`）からラベル要素（`EsLabelElement`）の配置指示を解決する（実寸 → 配置の写像。フォントサイズと色は引数の `MarqueePaint` から写す） |
| `MarqueeHostLabelRenderer` | ホストが描く付加情報ラベル（操作・情報）の体裁を適用して描く（文言はホストが与える） |

### 組み込み規則

| 型 | 可視性 | 責務 |
| --- | --- | --- |
| `MarqueeWheelInterpreter` | public | ホイール入力を解釈し、ズーム要求の通知・連携先へのスクロール適用・スクロール要求の通知を行う（`Handle(controlKey, shiftKey, deltaX, deltaY, position)`）。解釈の単一所有とログ出力を担う |
| `IWheelScrollTarget` | public | スクロールの適用先（自 UI のスクロール機構を適合させて注入する）。固定値の移動量を自身のスクロール位置へ加算するだけ |
| `WheelScrollDelta` | internal | 回転量 → 移動量（DIP）の換算（1 ノッチ = `ScrollMath.WheelScrollUnit`・Shift で横）。適用先は換算済みの移動量を受け取るため、本層の内部手順に限定する |
| `WheelZoomRequest` | public | Ctrl+ホイールによるズーム要求（方向 `ZoomIn` と発生位置 `Position`） |
| `WheelScrollRequest` | public | 通常ホイールによるスクロール要求（`DeltaX` / `DeltaY` / `Shift` / `Position`）。回転量をベクターで受け取る API へ渡す `Delta`（`EsVector`）を提供する |
| `MarqueeWheelLogMessage` | internal | ズーム要求のログ定義（`ILogger` 拡張。書式 `[Marquee] ZoomRequested: zoomIn={ZoomIn}`）。ホストはロガーをファサードへ渡すだけで、定義名を参照しない |
| `MarqueeViewportNotifier` | public | ビューポートの変化の再通知（現在の状態スナップショット）と再描画の要求。選択の状態は変更しない |

## テキストの扱いは UI 層が決める

| 対象 | 誰が決めるか | 呼ぶ API |
| --- | --- | --- |
| 図形（装飾・ハンドル・始点マーカー） | 本層 | `DrawShapes`（**テキストは描かない**） |
| テキスト（サイズ・カーソル座標） | UI 層（要素として置くか画像として描くか） | 要素として置く場合は `MarqueeLabelPlan.Resolve`（配置指示）の結果を要素へ写す（子要素は親より後に描かれるため**常に最上位**） |

**コアはテキストを値（`MarqueeTextPlacement`）として渡します**（文言・基準点・アライン・描画領域）。**フォントサイズ・色は描画の設定（`MarqueePaint`）**、体裁（背景 = 文字の実寸 + 内側余白 × 2、文字原点、アライン、行送り）は `EsLabelLayout`（`Common`）、実寸から要素の矩形を求める写像は `MarqueeLabelPlan` が単一所有します。

## 描画の差し替え

| 差し替えたいもの | 方法 |
| --- | --- |
| **描画先（UI フレームワーク）** | `IEsCanvas` を実装する（例: `SkiaEsCanvas`（Skia）/ `AvaloniaCanvas`（Avalonia の `DrawingContext`））。**描画手順はそのまま使える** |
| **描画手順そのもの** | `IMarqueeRenderer`（`Feature.MarqueeCore`）を実装した型を作り、ファサードの `Marquee.Renderer` へ設定する（本層の `MarqueeRenderer` を使わなくてもよい） |
| **色** | `MarqueePaint` を組み立てて渡す（`MarqueeRenderer` の生成時に渡すか、UI の `Paint` プロパティで差し替える）。色はコントロールごとに異なるため、共有インスタンスを書き換えずインスタンスを差し替える |
| **寸法・表示可否** | `MarqueeOptions`（選択の設定）と `MarqueeDecoOptions`（装飾）を変える（色は含まない） |

## Core との境界（なぜ組み込み規則を Core に置かないか）

「Ctrl+ホイール = ズーム」は**選択（マーキー）の判断ではなく、選択コントロールをビューポートへ組み込むときの規則**です。単体で使う Marquee にはズームもスクロールもビューポートも存在しないため、コアには置かず本層が単一所有します。

| 関心事 | 所有者 | 理由 |
| --- | --- | --- |
| 選択の判断（ドラッグ・nudge・確定/キャンセル・スナップ・最小サイズ） | `Feature.MarqueeCore` | コントロールが何であっても変わらない（独立した部品の本体） |
| 色（ハンドル・十字マーカー・ラベル・装飾） | 本層（`MarqueePaint`） | 描画にだけ必要な設定であり、コアの計算・判断には入らない（コアには寸法・表示可否だけを残す） |
| 描画の可否（暗転するか・枠線を描くか・内側を塗るか） | コア（`MarqueeDecoOptions` の明示フラグ） | 描くかどうかは入出力の判断であり、色（描画の設定）ではない |
| ホイール要求（Ctrl = ズーム / それ以外 = スクロール）とそのログ | 本層 | 組み込み先（ビューポート）が存在して初めて意味を持つ |
| 連携先へのスクロール適用（回転量 → 移動量の換算と適用先への加算） | 本層 | 「1 ノッチ = 48 DIP」「Shift で横」は UI をまたいで同じ規則である |
| ビューポート変化の再通知（現在の状態スナップショット + 再描画） | 本層 | ビューポートが存在して初めて意味を持つ（単体の Marquee にはビューポートもズームも無い） |
| 描画するラベルの一覧と順序（サイズラベル → カーソル座標ラベル）と実寸 → 配置矩形の写像 | 本層（`MarqueeLabelPlan`） | 文字を UI 要素として置くコントロールに必要な手順であり、描画手順と同じ層が単一所有する。実寸測定は描画 API 依存のため UI 層が与える（ラベルは常に最上位） |
| 要求の実処理（ズーム率の決定・表示の適用・スクロール先の決定） | ホスト（ズームビューポート等） | ビューの状態を持つのはビューポートだけ |
| スクロール適用先の適合（UI 固有の型 → `IWheelScrollTarget`） | UI 層 | フレームワーク固有の型（Avalonia の `ScrollViewer` 等）を使う |

## UI ごとの利用状況

| 型 | AvaloniaUI | MewUI | 補足 |
| --- | --- | --- | --- |
| `MarqueePaint` / `MarqueeRenderer` / `MarqueeOuterRenderer` / `MarqueeInnerRenderer` / `MarqueeLabelPlan` / `MarqueeHostLabelRenderer` | 使う | 使う | 色と描画手順は両 UI で共通（色は UI の `Paint` プロパティで指定する） |
| `MarqueeWheelInterpreter` / `WheelZoomRequest` / `WheelScrollRequest` | 使う | 使う | ホイールの解釈は両 UI で共通 |
| `IWheelScrollTarget` / `AttachScrollTarget` | 使う（`ScrollViewer` を適合） | **使わない** | MewUI のホストはビューポート変化を `NotifyViewportChanged` で通知し、スクロールの適用先を持たない（適用先がある UI だけがこの抽象を実装する） |
| `MarqueeViewportNotifier` | 使う（`ScrollChanged` を購読） | 使う（ホストが `NotifyViewportChanged` を呼ぶ） | 契機は UI ごとに異なるが、規則は共通 |
| `WheelScrollDelta` / `MarqueeWheelLogMessage`（internal） | 参照しない | 参照しない | 本層の内部手順（適用先は換算済みの移動量を受け取る） |

**UI フレームワークによって未使用になる型が存在します**（`IWheelScrollTarget` は適用先を持つ UI だけが使う）。これは「抽象として複数の UI で共用できる処理を本層へ置き、必要とする UI だけが実装・利用する」設計の帰結であり、コア（`Feature.MarqueeCore`）の公開面には影響しません。

## 独自のコントロールへ組み込む（最小実装）

マーキー選択だけを持つ独自コントロールは、**入力の転送**・**効果の受領**・**描画**の 3 点を書けば作れる。

```csharp
// 1. 効果（本 UI が必要とするものだけ。MarqueeEffectsBase が他の効果の空実装を提供する）
private sealed class Effects(MyControl owner) : MarqueeEffectsBase
{
    public override void RequestRedraw() => owner.InvalidateVisual();
}

// 2. 生成（カーソル操作が不要なら cursor は null でよい。第 1 引数は描画の実装で、色は MarqueePaint が持つ）
private readonly Marquee _marquee = new(new MarqueeRenderer(MarqueePaint.Default), new Effects(this));

// 3. 入力の転送（座標はドメイン座標へ変換して渡す）
protected override void OnPointerPressed(PointerPressedEventArgs e)
    => _marquee.Press(e.GetPosition(this).ToDomain(), e.ClickCount);

// 4. 描画（描画内容の解決と描画の起動はコア、色は UIBase の MarqueeRenderer）
protected override void Render(DrawingContext context)
{
    var size = Bounds.Size.ToEsSize();
    var content = _marquee.ResolveContent(size);
    _marquee.Draw(new AvaloniaCanvas(context), in content);
}
```

- 自 UI の描画実装（`MarqueeRenderer~UI`）を使う場合も、起動はコアの `Marquee.Draw` に一本化する（本 UI の型なら `Draw(context, in content)` へ委譲する）。
- ラベルを要素として配置する UI は 4 の後、`MarqueeLabelPlan.Resolve`（色は `Paint` の `MarqueePaint` から引かれる）の結果を要素へ写す。
- ホスト固有の付加情報ラベルは `MarqueeHostLabelRenderer.Draw` で体裁を揃えて描く。
- 座標変換（ドメイン ⇔ DIP）は `Marquee.SetCoordinateTransform` へ注入する（未指定は等倍）。

ホイール連携（ズーム・スクロール）が必要な場合は、本層の組み込み規則を次のように使います。

```csharp
// UI 層（コントロール）: ファサードへ接続して 1 つのフィールドとして持つ
_wheel = new MarqueeWheelInterpreter(_marquee);
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
_viewport = new MarqueeViewportNotifier(_marquee, _effects);
_viewport.ViewportChanged += (_, snapshot) => ViewportChanged?.Invoke(this, ToRectArgs(snapshot));

// 変化を検知したら呼ぶ（連携 ScrollViewer の ScrollChanged・ホストからの通知）
_viewport.Notify();   // 状態スナップショットの通知 + 再描画（選択の状態は変更しない）
```

## 依存関係

```text
MarqueeControl.UIBase
    ├── Feature.MarqueeCore（描画内容の契約・選択の判断・公開ファサード Marquee）
    ├── Common（IEsCanvas・EsLabelLayout（ラベルの体裁））
├── Platform.Core（Es* 値型・SkiaEsCanvas・ScrollMath）
    └── Microsoft.Extensions.Logging.Abstractions（ズーム要求の LoggerMessage 定義）
```

**UI フレームワークへ依存しません**（Avalonia / MewUI の型を参照しない）。UI 固有の描画 API は `IEsCanvas` の実装（各 UI プロジェクト・プラットフォーム層）が担います。

名前空間は `EsUtil.SaveEvidence.Marquee.UIBase`。アセンブリ名は `MarqueeControl.UIBase`。

## テスト

`tests/` が本プロジェクトのテストを持ちます。描画は UI 非依存の描画ポート（Skia）で行い、ホイールの解釈は UI 型を持たないため、**UI 初期化なしで検証**できます（合計 57 件）。

```text
tests/
├── MarqueePaint.Tests.cs              # 色の既定値・未指定のハンドル塗り色の解決・with 式・値等価性（9 件）
├── MarqueeRenderer.Tests.cs           # 図形の描画（ハンドル 8 方向・枠線・十字線・装飾・層の選択・引数の検証）（10 件）
├── MarqueeLabelPlan.Tests.cs          # 実寸 → ラベル要素の写像（順序・配置・色）（10 件）
├── MarqueeHostLabelRenderer.Tests.cs  # 付加情報ラベルの体裁（背景・文字の順序・余白）（5 件）
├── MarqueeWheelInterpreter.Tests.cs   # 解釈・通知・連携先への適用・ログ（15 件）
├── WheelRequest.Tests.cs              # 要求の値（回転量ベクター・方向と位置）（3 件）
├── MarqueeViewportNotifier.Tests.cs   # ビューポート変化の通知（スナップショット・再描画・状態不変）（5 件）
├── RecordingLogger.cs                 # ログ出力を収集するテスト用 ILogger
├── StubMarqueeRenderer.cs             # 描画しない描画実装（コアのファサード生成用）
└── TestHelper.cs                      # テスト用の描画先（Skia）
```

```powershell
dotnet test src/MarqueeControl.UIBase/tests/MarqueeControl.UIBase.Tests.csproj
```

## ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
