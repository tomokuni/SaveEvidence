# DragRectControl.Core

`DragRectControl`（リサイズ可能なドラッグ矩形）の**UI 非依存の状態層**です。Avalonia 版と MewUI 版が同じ状態管理・ロジックを共有します。

## 目的

- 選択矩形の幾何計算・状態機械・**入力の解釈（判断）**を 1 箇所に集約し、UI ごとの実装差から切り離す。
- UI 層は「入力イベントの受付」「効果の適用」「通知の転送」「描画」だけを担う薄い層にし、判断を持たない。
- UI 非依存のためヘッドレス実行が不要で、`[Fact]` で並列実行できる。
- 座標は `EsPoint` / `EsRect` / `EsSize`（`EsUtil.Platform.Core.Geometry`）で扱う。

## 層の分担

```text
UI 層（DragRectControl.AvaloniaUI / .MewUI）
  ポインター・キー入力をコア型へ変換して通知 → 返された効果を適用 → 通知を公開イベントへ転送 → 状態を描画
        │ DragRectFeedback（効果: 再描画 / フォーカス / マウスキャプチャ / カーソル操作・形状）
        ▼
状態層（本プロジェクト）
  DragRectSession  入力の解釈と状態遷移の判断（nudge セッション・ダブルクリックの確定/キャンセル・
                   ハンドルのスナップ・クリック検出・最小サイズによるキャンセル・エコー移動の抑制）
        │ DragRectNotice（通知: 種別・通知座標・スナップショット）
        ▼
UI 層（公開イベントへ転送）
```

## 構成

| 型 | 可視性 | 責務 |
| --- | --- | --- |
| `DragHandle` | public | ドラッグハンドル種別（8 方向 + `Move`） |
| `DragRectInteractionState` | public | 対話状態（NoSelection / StartPointNudging / Dragging / DraggingNudging / Selected / SelectedNudging） |
| `DragRectSnapshot` | public | 通知時点の状態の値オブジェクト（`selectionRect` は `EsRect`、`mousePosition` は `EsPoint`） |
| `DragRectScrollRequest` / `DragRectZoomRequest` | public | ホイール操作の要求（生の回転量・修飾キー・発生位置）。スクロール先とズーム率の決定は転送先（ホスト）が担う |
| `DragRectOptions` | public | 設定の値オブジェクト（外観 10 項目 + 対話パラメータ 3 項目）。既定値とフォントサイズの補正を単一所有する |
| `DragRectSession` | internal | **状態管理クラス**。入力の通知を受けて状態を更新し、効果（`DragRectFeedback`）と通知（`DragRectNotice`）を返す |
| `DragRectNotice` / `DragRectNoticeKind` | internal | コアから UI 層への通知（種別・通知座標・スナップショット） |
| `DragRectFeedback` / `DragRectCursorAction` | internal | コアから UI 層への効果（再描画・フォーカス・マウスキャプチャ・カーソル操作・カーソル形状） |
| `DragRectState` | internal | 選択矩形・ドラッグ・nudge の状態保持と更新ロジック（純粋計算の適用） |
| `DragRectAppearance` | internal | 両 UI の描画で共有する外観値（マーカー太さ・ラベル余白・フォントサイズ範囲）とその配置計算・値補正 |
| `DragCursorKind` | internal | 操作位置に対応するカーソル形状の種別（UI は自 UI のカーソル型へ写像するだけ） |
| `SelectionGeometry` | internal | 純粋な幾何計算（正規化・ハンドル位置・近傍判定・対角・境界クリップ・ドラッグ変形）・描画対象ハンドル一覧・カーソル種別変換 |
| `LogMessage` | internal | ログの LoggerMessage 定義（入力の判断・ズーム要求・設定変更）。UI 層は自 UI 固有の操作ログのみを持つ |

名前空間は `Estable.DragRect`（幾何は `Estable.DragRect.Selection`）。アセンブリ名は `DragRectControl.Core`。

## 依存関係

```text
DragRectControl.Core
    ├── Platform.Core（Es* 幾何型 / EsKey / ICoordTransform / ICursorService）
    └── Microsoft.Extensions.Logging.Abstractions（LoggerMessage ソース生成のみ）
```

**UI フレームワークへ依存しません。** カーソル操作は `ICursorService` 抽象にのみ依存し、未指定（`null`）の場合は何もしません。既定実装（Win32）の解決は UI 側の責務です。

## 主な API

```csharp
var session = new DragRectSession(cursorService, options) { Log = logger };  // cursorService / options / logger は null 可
session.SetPort(new CoordTransform(p => p, p => p, () => new EsSize(1920, 1080))); // ドメイン境界

// 入力の通知（UI 層から呼ぶ）
var feedback = session.Press(new EsPoint(100, 100), clickCount: 1);   // 押下（新規選択・ハンドル・確定/キャンセル）
feedback = session.Move(new EsPoint(400, 300));                       // 移動（ドラッグ・ホバー）
feedback = session.Release(new EsPoint(400, 300));                    // 解放（確定・キャンセル・クリック）
feedback = session.KeyDown(EsKey.Right, cursorPosition: null);        // キー（nudge・Escape・Enter）
feedback = session.ApplyOptions(options);                            // 設定の反映（対話パラメータ）
feedback = session.NotifyViewportChanged();                          // ビューポートの変化（再通知・再描画）
feedback.ApplyCursor(cursorService, toScreen);                       // 効果: カーソル操作（移動→非表示→再表示）
feedback.ApplyVisual(() => Focus(), InvalidateVisual, SetCursor);    // 効果: フォーカス→カーソル形状→再描画
session.Notified += (_, notice) => Raise(notice);                    // 通知（公開イベントへ転送）
```

| メソッド | 用途 |
| --- | --- |
| `Press` / `Move` / `Release` | ポインター入力の通知（ドメイン座標）。ダブルクリックの確定/キャンセル・スナップ・クリック検出を含む |
| `KeyDown` / `NudgeByKey` | キー入力の通知（`EsKey`）。Escape / Enter（`Return` も同一）と矢印キーの nudge |
| `EndNudge` / `Detach` | nudge の終了・対話の終了（フォーカス喪失・ビジュアルツリーからの切断） |
| `SetSelection` / `Reset` / `RequestConfirm` / `RequestCancel` / `ApplyClampToBounds` | ホスト・プロパティ変更からの操作 |
| `ApplyOptions` | 設定（`DragRectOptions`）の反映。対話パラメータを更新し、境界外選択の禁止が変化したときだけログと即時クリップを行う |
| `NotifyViewportChanged` | ビューポート（ズーム・スクロール）の変化の通知。現在の選択矩形 + スナップショットを再通知して再描画を要求する（状態は変えない） |
| `ToDomain` / `ToDip` / `ToDipRect` / `SetPort` | 座標変換（ドメイン ⇔ ローカル DIP。選択矩形は閉区間として変換） |
| `Options` / `Log` / `SelectionRect` / `HasSelection` / `NudgeActive` / `MarkerPosition` / `LastMousePosition` | 描画用の状態読み取り（`MinSelectionSize` / `ClampToDomainBounds` / `HandleHitRadius` は `Options` の参照。`Log` は null を `NullLogger` へ吸収する） |

UI 層が適用する効果（`DragRectFeedback`）の適用順はコアが単一所有する（UI 層は自 UI の操作へ接続するだけ）:

| メソッド | 適用内容・順序 |
| --- | --- |
| `ApplyCursor` | カーソル操作（移動 → 非表示 → 再表示）。画面座標の変換のみデリゲートで受け取る |
| `ApplyVisual` | フォーカス → カーソル形状 → 再描画（要求されたものだけ） |

UI 層が使う共有値・計算（`DragRectAppearance`）:

| メンバー | 用途 |
| --- | --- |
| `MarkerThickness` / `HandlePenThickness` / `LabelOffset` / `LabelPadding` | 両 UI で共有する描画の外観値（片方だけ変えると見た目がずれるため単一所有） |
| `CoerceCoordLabelFontSize` | 座標ラベルのフォントサイズを安全域（6〜48）へ補正（非数は既定値） |
| `ComputeCoordLabelPosition` | 座標ラベルの左上位置（カーソル右側 + 描画領域内へのクランプ） |
| `SelectionGeometry.DirectionalHandles` | 描画対象の 8 方向ハンドル（列挙順は `DragHandle` の宣言順） |
| `SelectionGeometry.ToCursorKind` | ハンドル → `DragCursorKind`（どのハンドルが同じカーソルになるか） |

## 設計上の約束

1. **判断はコア・反映は UI**: 「どうするか」（nudge の開始/終了・確定/キャンセル・スナップの有無・キャンセル可否・カーソル種別）はコアが決め、UI は効果を適用するだけにする。
2. **単一所有**: 幾何計算は `SelectionGeometry` のみ、状態は `DragRectState` のみ、入力の解釈は `DragRectSession` のみが持つ。
3. **座標系**: `EsRect` / `EsPoint` は常にドメイン座標。UI 論理座標（DIP）との変換は `ICoordTransform`（`Port`）が担う。
4. **境界 1 行変換**: 各 UI のイベント引数は自前の型（例: Avalonia の `Rect`）で公開し、境界で 1 行変換する。キーは `EsKey` を介して通知する。
5. **カーソル操作の分離**: 状態機械は `ICursorService` 抽象のみに依存し、プラットフォーム既定実装を生成しない。カーソル種別は `DragHandle` として返し、UI が自 UI のカーソル型へ写像する。
6. **Enter と Return は同一**: Avalonia の `Key` は両者が同値のエイリアスで、名前ベースの相互変換では `Return` が得られるため、双方を確定キーとして扱う。
7. **両 UI で共通の値はコアが単一所有**: 外観値（マーカー太さ・ラベル余白・フォントサイズ範囲）・配置計算・値補正・描画対象ハンドル一覧・カーソル形状の種別・**設定の既定値と範囲補正（`DragRectOptions`）**は、Avalonia 版と MewUI 版で同じ結果になる必要があるためコアが持つ（片方だけを直すと見た目や既定値がずれる）。UI 層が持つのは自 UI の描画・カーソル型への写像・座標変換のみ。
8. **設定の適用はコアが判断する**: 設定（`DragRectOptions`）の反映は `ApplyOptions` が担い、対話パラメータの更新・境界外選択の禁止の変化の検出・変更ログ・即時クリップを 1 箇所に集約する（UI 層は「変更通知を渡す」だけになる）。
9. **効果の適用順もコアが持つ**: 「どの効果をどの順で適用するか」（カーソル操作の順序・フォーカス → カーソル形状 → 再描画）は `DragRectFeedback` が単一所有し、UI 層は自 UI の操作（`Focus` / `InvalidateVisual` / カーソル型への写像）へ接続するだけでよい。
10. **持続資源の保持もコア**: ロガーは `DragRectSession.Log` が保持し、null を `NullLogger` へ吸収して伝搬する（UI は get/set を委譲するだけ）。

## テスト

```text
tests/
├── SelectionGeometry.Tests.cs    # 純粋幾何（17 件）
├── DragRectState.Tests.cs        # 状態遷移・ヒットテスト・ドラッグ・nudge・境界クリップ（32 件）
├── DragRectFeedback.Tests.cs     # 効果の適用（カーソル操作・表示上の効果の順序）（6 件）
├── DragRectSession.Tests.cs      # 入力の解釈・通知・効果・設定の反映・ビューポート通知（52 件）
├── DragRectAppearance.Tests.cs   # ラベル配置・値補正・ハンドル一覧・カーソル種別（19 件）
├── DragRectOptions.Tests.cs      # 既定値・部分変更・値補正・等価性（13 件）
└── LogMessage.Tests.cs           # ログ定義の生成コード（IsEnabled 両パス・書式）（3 件）
```

```powershell
dotnet test src/DragRectControl.Core/tests/DragRectControl.Core.Tests.csproj
```

## ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
