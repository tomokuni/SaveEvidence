# MarqueeControl.CoreForControl

`MarqueeControl.Core`（マーキー選択の UI 非依存コア）を**他の UI コントロールへ組み込むときにだけ必要な処理**を置く層です。`MarqueeControl.AvaloniaUI` / `MarqueeControl.MewUI` が共有します。

## 目的

- コア（`MarqueeControl.Core`）を将来そのまま別プロジェクトへ独立できるよう、**選択（マーキー）の判断ではない処理をコアへ入れない**。
- 「組み込み先（ビューポート）との連携規則」を UI をまたいで 1 箇所に集約する（UI ごとに書くと片方だけ変わる）。
- UI フレームワークには依存しない（修飾キーの真偽と回転量だけを受け取り、適用先と通知先は抽象・コールバックで受け取る）ため、ヘッドレス実行なしで `[Fact]` 並列検証できる。
- UI 層が使う面は `public`、本層の内部手順は `internal`（単体テストにのみ `InternalsVisibleTo` で公開する）。ファイル名はクラス名と一致させる（`MarqueeWheelInterpreter.cs`）。

## 収録する型

| 型 | 可視性 | 責務 |
| --- | --- | --- |
| `MarqueeWheelInterpreter` | public | ホイール入力を解釈し、ズーム要求の通知・連携先へのスクロール適用・スクロール要求の通知を行う（`Handle(controlKey, shiftKey, deltaX, deltaY, position)`）。解釈の単一所有とログ出力を担う |
| `IWheelScrollTarget` | public | スクロールの適用先（自 UI のスクロール機構を適合させて注入する）。固定値の移動量を自身のスクロール位置へ加算するだけ |
| `WheelScrollDelta` | internal | 回転量 → 移動量（DIP）の換算（1 ノッチ = `ScrollMath.WheelScrollUnit`・Shift で横）。適用先は換算済みの移動量を受け取るため、本層の内部手順に限定する |
| `WheelZoomRequest` | public | Ctrl+ホイールによるズーム要求（方向 `ZoomIn` と発生位置 `Position`） |
| `WheelScrollRequest` | public | 通常ホイールによるスクロール要求（`DeltaX` / `DeltaY` / `Shift` / `Position`）。回転量をベクターで受け取る API へ渡す `Delta`（`EsVector`）を提供する |
| `MarqueeWheelLogMessage` | internal | ズーム要求のログ定義（`ILogger` 拡張。書式 `[Marquee] ZoomRequested: zoomIn={ZoomIn}`）。ホストはロガーをファサードへ渡すだけで、定義名を参照しない |
| `MarqueeViewportNotifier` | public | ビューポートの変化の再通知（現在の状態スナップショット）と再描画の要求。選択の状態は変更しない |
| `MarqueeLabelLayoutPlan` / `MarqueeLabelElement` | public | 描画するラベル（装飾のラベルとカーソル座標ラベル）を 1 つの一覧へまとめる（描画順の単一所有）。実寸測定と描画 API の呼び出しは UI 層が担う |

## 依存方向

```text
MarqueeControl.AvaloniaUI / MarqueeControl.MewUI（UI 層）
  └──> MarqueeControl.CoreForControl（本プロジェクト。組み込みの転送規則と描画指示）
         ├──> MarqueeControl.Core（公開ファサード `Marquee`。選択の状態と座標変換の取得元）
         ├──> MarqueeControl.Core（装飾の描画内容 `MarqueeDrawContent`。ラベル描画計画の入力）
         └──> Platform.Core（コア幾何型 `EsPoint` / `EsVector`、移動量規則 `ScrollMath.WheelScrollUnit`）
```

本プロジェクトは UI フレームワークへ依存しません（Avalonia / MewUI の型を参照しません）。

## UI ごとの利用状況

| 型 | AvaloniaUI | MewUI | 補足 |
| --- | --- | --- | --- |
| `MarqueeWheelInterpreter` / `WheelZoomRequest` / `WheelScrollRequest` | 使う | 使う | ホイールの解釈は両 UI で共通 |
| `IWheelScrollTarget` / `AttachScrollTarget` | 使う（`ScrollViewer` を適合） | **使わない** | MewUI のホストはビューポート変化を `NotifyViewportChanged` で通知し、スクロールの適用先を持たない（適用先がある UI だけがこの抽象を実装する） |
| `MarqueeViewportNotifier` | 使う（`ScrollChanged` を購読） | 使う（ホストが `NotifyViewportChanged` を呼ぶ） | 契機は UI ごとに異なるが、規則は共通 |
| `MarqueeLabelLayoutPlan` | 使う | 使う | ラベルの描画項目の一覧と描画順を共通化する |
| `WheelScrollDelta` / `MarqueeWheelLogMessage`（internal） | 参照しない | 参照しない | 本層の内部手順（適用先は換算済みの移動量を受け取る） |

**UI フレームワークによって未使用になる型が存在します**（`IWheelScrollTarget` は適用先を持つ UI だけが使う）。これは「抽象として複数の UI で共用できる処理を本層へ置き、必要とする UI だけが実装・利用する」設計の帰結であり、コア（`MarqueeControl.Core`）の公開面には影響しません。

## Core との境界（なぜズームを Core に置かないか）

「Ctrl+ホイール = ズーム」は**選択（マーキー）の判断ではなく、選択コントロールをビューポートへ組み込むときの規則**です。単体で使う Marquee にはズームもスクロールも存在しないため、コアには置かず本層が単一所有します。

| 関心事 | 所有者 | 理由 |
| --- | --- | --- |
| 選択の判断（ドラッグ・nudge・確定/キャンセル・スナップ・最小サイズ） | `MarqueeControl.Core` | コントロールが何であっても変わらない（独立した部品の本体） |
| ホイール要求（Ctrl = ズーム / それ以外 = スクロール）とそのログ | 本プロジェクト | 組み込み先（ビューポート）が存在して初めて意味を持つ |
| 連携先へのスクロール適用（回転量 → 移動量の換算と適用先への加算） | 本プロジェクト | 「1 ノッチ = 48 DIP」「Shift で横」は UI をまたいで同じ規則である |
| ビューポート変化の再通知（現在の状態スナップショット + 再描画） | 本プロジェクト | ビューポートが存在して初めて意味を持つ（単体の Marquee にはビューポートもズームも無い） |
| 描画するラベルの一覧と描画順（装飾のラベル → 座標ラベル） | 本プロジェクト | 文字の描画をホストへ委ねるコントロールでだけ必要な手順（単体の Marquee には重ね順の概念が無い）。実寸測定は描画 API 依存のため UI 層が与える |
| 要求の実処理（ズーム率の決定・表示の適用・スクロール先の決定） | ホスト（ズームビューポート等） | ビューの状態を持つのはビューポートだけ |
| スクロール適用先の適合（UI 固有の型 → `IWheelScrollTarget`） | UI 層 | フレームワーク固有の型（Avalonia の `ScrollViewer` 等）を使う |

## 使い方

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

ホストは要求を購読し、ズーム率の決定と表示の適用を行う（例: `ZoomViewControl.ZoomByWheel(request.ZoomIn, ToViewportPosition(request.Position))`）。

```csharp
// UI 層（コントロール）: ビューポートの変化（パン・ズーム）を上位へ再通知する
_viewport = new MarqueeViewportNotifier(_marquee, _effects);
_viewport.ViewportChanged += (_, snapshot) => ViewportChanged?.Invoke(this, ToRectArgs(snapshot));

// 変化を検知したら呼ぶ（連携 ScrollViewer の ScrollChanged・ホストからの通知）
_viewport.Notify();   // 状態スナップショットの通知 + 再描画（選択の状態は変更しない）
```

```csharp
// UI 層（コントロール）: ラベルの描画項目を 1 回の走査で描く
// （実寸測定は描画 API 依存のため、UI 層が測定関数を渡す）
foreach (var item in MarqueeLabelLayoutPlan.Resolve(deco, coordLabel, MeasureText))
{
    DrawLabel(item);   // 背景を塗ってから文字を描く（自 UI の描画 API へ写すだけ）
}
```

## テスト

```text
tests/
├── MarqueeWheelInterpreter.Tests.cs # 解釈・通知・連携先への適用・ログ（Ctrl の判定・方向・位置・Shift の横変換・連続通知・読者なし）（15 件）
├── WheelRequest.Tests.cs            # 要求の値（回転量ベクター・方向と位置）（3 件）
├── MarqueeViewportNotifier.Tests.cs # ビューポート変化の通知（スナップショット・再描画・状態不変・読者なし・引数の検証）（5 件）
└── MarqueeLabelLayoutPlan.Tests.cs    # ラベル描画項目の解決（空・ラベルのみ・座標ラベルのみ・並び順・配置・測定の呼ばれ方・引数の検証）（8 件）
```

```powershell
dotnet test src/MarqueeControl.CoreForControl/tests/MarqueeControl.CoreForControl.Tests.csproj
```

## ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
