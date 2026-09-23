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
        │ DragRectFrame.Resolve（選択矩形の外側に描く装飾のフレーム。判断は装飾コアが持つ）
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
| `DragRectOptions` | public | 設定の値オブジェクト（外側の装飾 6 項目 + 外観 9 項目 + 対話パラメータ 3 項目）。既定値・装飾の色と太さの所有・フォントサイズの補正を単一所有する |
| `DragRectFrame` | internal | 選択矩形の**外側に描く装飾**（暗転マスク・穴あけ枠・ラベル）の描画内容を解決する（セッションの状態 + 外側の装飾の設定 → 装飾コアのフレーム） |
| `DragRectSession` | internal | **状態管理クラス**。入力の通知を受けて状態を更新し、効果（`DragRectFeedback`）と通知（`DragRectNotice`）を返す |
| `DragRectNotice` / `DragRectNoticeKind` | internal | コアから UI 層への通知（種別・通知座標・スナップショット） |
| `DragRectFeedback` / `DragRectCursorAction` | internal | コアから UI 層への効果（再描画・フォーカス・マウスキャプチャ・カーソル操作・カーソル形状） |
| `DragRectState` | internal | 選択矩形・ドラッグ・nudge の状態保持と更新ロジック（純粋計算の適用） |
| `DragRectAppearance` | internal | 両 UI の描画で共有する外観値（マーカー太さ・ラベル余白・フォントサイズ範囲）とその配置計算・値補正 |
| `DragCursorKind` | internal | 操作位置に対応するカーソル形状の種別（UI は自 UI のカーソル型へ写像するだけ） |
| `SelectionGeometry` | internal | 純粋な幾何計算（正規化・ハンドル位置・近傍判定・対角・境界クリップ・ドラッグ変形）・描画対象ハンドル一覧・カーソル種別変換 |
| `DragRectLogMessage` | internal | ログの LoggerMessage 定義（入力の判断・ズーム要求・設定変更）。UI 層は自 UI 固有の操作ログのみを持つ |

名前空間は `Estable.DragRect`（幾何は `Estable.DragRect.Selection`、装飾は `Estable.DragRect.Decoration`）。アセンブリ名は `DragRectControl.Core`。

## 名前の由来と一般的な呼称

`DragRect` は**操作**（ドラッグで矩形を描く）に由来する名前です。役割は「矩形領域の選択と表示」であり、`ScreenSelect` / `WindowSelect` のようにドラッグしないモードも含むため、名前が責務を覆い切っていません。

この動作・部品を指す一般的な呼称は次のとおりです（改名する場合の候補）。

| 層 | 一般的な呼称 | 実例 |
| --- | --- | --- |
| 動作 | marquee selection / rubber-band selection | Photoshop の矩形マーキー、Qt `QRubberBand`、Windows Explorer のラバーバンド選択 |
| コントロール | rect tracker | MFC `CRectTracker`（「8 個のハンドル付き矩形を移動・リサイズする」＝本コントロールと同じ責務） |
| 用途 | region selector | ShareX / Snipaste / Greenshot の領域キャプチャ |
| 部品 | resize handle / grip / thumb | WPF `Thumb`、Win32 のリサイズグリップ |
| 部品（キー微調整） | nudge | VS / Figma（本コントロールは既にこの用語を使用） |

改名の候補と評価（**未実施。公開 API の破壊的変更のため判断待ち**）:

判定は**使用中のバージョン**のアセンブリ（Avalonia 12.1.3 / MewUI 0.21.1 / WinForms = `Microsoft.WindowsDesktop.App` 10.0.12 / WinUI3 = Windows App SDK 1.8 / .NET 10 BCL）のメタデータから**型定義テーブル**を読んで行った（文字列走査だけでは古いキャッシュ `avalonia/0.10.0` を拾って誤判定し、`ProgressBarStyle.Marquee` のような**列挙メンバーを型と誤認**する）。

| 候補 | 長さ | 型名の衝突（実測） | 評価 |
| --- | --- | --- | --- |
| `Marquee` | 7 | **なし**（WinForms / WinUI3 / Avalonia / MewUI / .NET 10 BCL の**すべてで 0 件**。`MarqueeControl` 等の派生名も 0 件） | 動作の標準語。短い。**推奨** |
| `Region` | 6 | **あり**（`System.Drawing.Region`。WinForms 版は `using System.Drawing;` が必須のため実衝突する） | 最短級だが WinForms 追加時に衝突する |
| `Area` | 4 | なし | 最短。汎用すぎて意味が弱い |
| `RubberBand` | 10 | 型は 0 件。ただし Avalonia に `_isRubberBanding` / `RubberBandFactor` / `RubberBandReturnDuration`（伸縮スクロール）があり、**同一フレームワーク内で別の意味に使われている** | Qt の標準語。長い |
| `RegionSelect` | 12 | なし | 役割を最も明確に表す。長い |
| `RectTracker` | 11 | なし | MFC の系譜で Windows では最も正確。`Tracker` が状態管理クラスと紛らわしい |
| `Selector` / `Thumb` | — | **あり**（`Avalonia.Styling.Selector` / `System.Drawing`・`System.Windows.Forms`・`Microsoft.UI.Xaml.Controls.Primitives.Thumb`） | 使用不可 |

**`Marquee` の「進行バー」の連想について**: WinForms には `ProgressBarStyle.Marquee` / `ProgressBar.MarqueeAnimationSpeed` / `MarqueePaused` という**メンバー**があり、WinUI3 には `Marquee` を含むメンバーが**存在しない**（不確定表示は `IsIndeterminate`）。C# では**メンバー名は型名と衝突しない**（`ProgressBarStyle.Marquee` は型で修飾して使う）ため、`MarqueeControl` / `MarqueeOptions` 等の型名に影響はない。

改名する場合は型 20 種・コード参照約 1,700 箇所・ドキュメント約 1,070 箇所の更新を伴う（テキスト一括置換ではなく LSP の改名を使う）。名前空間は既存の `Estable.Loupe` → `LoupeControl` と同じ「名前空間 = 概念、型 = 概念 + 役割」の形になる。

**併せて確認したこと（将来の WinForms / WinUI3 版に向けて）**: 本リポジトリの現在の**公開型 111 種は WinForms / WinUI3 / Avalonia / MewUI / BCL のいずれとも衝突しない**。UI を追加するときに注意するのは型名ではなく、色型（WinUI3 は `Windows.UI.Color`）とコントロール基底（WinForms は `System.Windows.Forms.Control`）の境界変換である。

## 選択矩形の外側に描く装飾（`Estable.DragRect.Decoration`）

選択矩形の**外側に描く装飾**（暗転マスク・穴あけ枠・ラベル・ハイライト）の契約・外観・幾何・フレーム解決を本プロジェクトが単一所有します。

> **命名**: 名前空間とフォルダは装飾という**カテゴリ**を表す `Decoration` のまま、型の接頭辞 `OuterDeco*` が**外側**であることを表します（将来「内側の装飾」を追加する場合は同じ名前空間へ `InnerDeco*` を足します）。

| 型 | 可視性 | 責務 |
| --- | --- | --- |
| `OuterDecoRenderMode` | public | オーバーレイ描画のモード（AreaSelect / ScreenSelect / WindowSelect / Crop）。`DragRectOptions.OuterRenderMode` として公開する |
| `OuterDecoAppearance` | public | 外観（暗転色・枠線・ラベル）の単一所有。`DragRectOptions.OuterAppearance` として公開する |
| `OuterDecoOverlayGeometry` | internal | 論理座標 → DIP 変換（閉区間の扱いを含む）・穴あけ矩形の決定・暗転領域（4 帯）の計算・枠線矩形の計算・座標変換ポートの決定（ポート優先・無ければ倍率） |
| `OuterDecoFrame` | internal | **1 フレーム分の描画内容**（暗転領域・枠線・サイズラベルの文言・サブライン）の決定。入力は `OuterDecoRequest` |
| `OuterDecoRequest` | internal | **描画入力**（設定と状態の 1 値 `OuterDecoOptions` + 描画時に決まる値（クライアント矩形・座標変換ポート・倍率・文言プロバイダ）） |
| `OuterDecoOptions` | internal | 設定と描画状態の単一値（外観・モード・情報ラベルの文言・全面暗転・選択/ハイライト矩形とその有効フラグ）。`DragRectFrame` が組み立てる |
| `OuterDecoLabels` | internal | 情報ラベルの表示判定・操作ラベルの文言・サイズラベルの文言（`FormatSizeLabel`）・表示スケールの解決（`ResolveDisplayScaling`）・サブラインの組み立ての単一所有 |
| `OuterDecoLabelStyle` | internal | ラベル 1 行分の外観（フォントサイズ・文字色・背景色）。フレームの受け渡し専用 |
| `OuterDecoLabelLayout` | internal | ラベルの行配置（内側余白 `Padding`・背景矩形・文字の描画原点・次の行の Y 座標）の単一所有 |
| `OuterDecoSubLine` | internal | サブライン 1 行分の値（文言・フォントサイズ・文字色・背景色） |

選択矩形と選択の有無は**セッションが単一のソース**であり、装飾はそれを受け取って描画内容を決めるだけです（装飾は状態を持ちません）。

### 層の責務の境界（装飾）

UI 層（`DragRectFrame` と両 UI の描画層）は「セッションの状態と `DragRectOptions.Outer*` を `OuterDecoRequest` へ詰めて `OuterDecoFrame.Resolve` を呼び、返されたフレームを描く」だけです。

| 責務 | 所有者 | 内容 |
| --- | --- | --- |
| 設定と状態 | 本コア（値） / UI（保持） | 外観と表示ポリシーは `DragRectOptions.Outer*`（公開値）、選択矩形はセッションが持つ。UI はフレームワークのプロパティ機構で保持するだけ |
| 判断・計算 | 本コア | 論理矩形 → DIP 変換（選択は閉区間 +1 / ハイライトは +0）・穴あけ矩形の優先順（ハイライト → 選択）・座標変換ポートの決定・透明指定の解決・サイズラベルの文言決定・表示スケールの解決・サブラインの組立・枠線矩形・暗転領域（4 帯）・行配置 |
| 契約 | 本コア | `OuterDecoFrame`（描画内容）・`OuterDecoRequest`（描画入力）・`OuterDecoOptions`（設定と状態） |
| 描画 | UI | フレームの各項目を自 UI の描画 API へ写す（`FillRectangle` / `DrawRect` / `DrawText` 等） |
| ホスト結線 | UI | プロパティ機構・DPI 追従・キャンバスのスケーリング・ブラシキャッシュ・テキストの実寸測定とフォント解決 |

UI 層に残る分岐は次の 4 種のみで、いずれも本コアへ移せません。

1. **空のコレクションのスキップ**（ブラシ生成を避ける性能ガード）
2. **提示位置の有無**（基準矩形・文言が null なら描かない＝描画の前提条件）
3. **フレームワーク固有の値の解決**（DPI の 0 以下フォールバック・色型変換）
4. **テスト計量差の吸収**（`FormattedText` と `SKFont` で行の高さが異なる）

暗転・穴あけ枠・ラベルの描画を行うのは `DragRectControl.AvaloniaUI` の `DragRectOuterDrawer` と `DragRectControl.MewUI` の `DragRectOuterSkiaRenderer` です（本コアは描画 API を持たない）。

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
11. **外側の装飾は選択矩形の設定が単一所有する**: 暗転マスク・穴あけ枠・ラベル（サイズ/操作/情報）の外観と表示ポリシーは `DragRectOptions` の `Outer*` 項目が持ち、枠線の色・太さもそこで単一所有する（選択枠線と暗転マスクの穴あけ枠は同じ矩形に重なるため、2 つの値に分けると見た目が崩れる）。**既定は枠線のみ**（暗転・ラベルは用途依存のため、ホストが設定値で明示的に与える）。
12. **選択の状態はセッションが単一のソース**: 外側の装飾の穴あけ矩形と選択の有無は設定値ではなくセッションの状態から `DragRectFrame` が導出する（同じデータを 2 箇所に持たない）。
13. **装飾の契約は本コアが単一所有する**: 暗転マスク・穴あけ枠・ラベルを描くための契約（`OuterDecoFrame` / `OuterDecoRequest` / `OuterDecoOptions`）と幾何は `Estable.DragRect.Decoration` が持ち、UI 層は自 UI の描画 API へ写すだけとする。公開設定として必要なのは `OuterDecoAppearance` と `OuterDecoRenderMode` のみ（`DragRectOptions` の `Outer*` 経由）で、それ以外は `internal` とする。

## テスト

```text
tests/
├── SelectionGeometry.Tests.cs        # 純粋幾何（17 件）
├── DragRectState.Tests.cs            # 状態遷移・ヒットテスト・ドラッグ・nudge・境界クリップ（32 件）
├── DragRectFeedback.Tests.cs         # 効果の適用（カーソル操作・表示上の効果の順序）（6 件）
├── DragRectSession.Tests.cs          # 入力の解釈・通知・効果・設定の反映・ビューポート通知（52 件）
├── DragRectAppearance.Tests.cs       # ラベル配置・値補正・ハンドル一覧・カーソル種別（19 件）
├── DragRectOptions.Tests.cs          # 既定値・部分変更・値補正・等価性（14 件）
├── DragRectFrame.Tests.cs            # 外側の装飾のフレーム解決（選択の導出・ハイライト・全面暗転・座標変換・ラベル）（21 件）
├── OuterDecoOverlayGeometry.Tests.cs  # 穴あけ矩形の決定・暗転領域（4 帯）・枠線矩形・閉区間の DIP 変換（25 件）
├── OuterDecoFrame.Tests.cs            # フレーム解決（暗転・枠線・ラベル・透明指定・境界値）（29 件）
├── OuterDecoLabels.Tests.cs           # 操作/情報ラベルの文言・表示判定・サイズラベルの書式（23 件）
├── OuterDecoAppearance.Tests.cs       # 外観の既定値・部分変更・ラベル背景の実効色（14 件）
├── OuterDecoLabelLayout.Tests.cs      # ラベルの行配置（余白・背景矩形・文字原点・次の行）（5 件）
├── OuterDecoOptions.Tests.cs          # 設定と状態の既定値・部分変更・等価性（5 件）
└── DragRectLogMessage.Tests.cs               # ログ定義の生成コード（IsEnabled 両パス・書式）（3 件）
```

```powershell
dotnet test src/DragRectControl.Core/tests/DragRectControl.Core.Tests.csproj
```

## ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
