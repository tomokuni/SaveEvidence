# MarqueeControl.Core

`MarqueeControl`（マーキー選択＝ドラッグで矩形を選び、移動・リサイズできるコントロール）の**UI 非依存の状態層**です。Avalonia 版と MewUI 版が同じ状態管理・ロジックを共有します。

## 目的

- 選択矩形の幾何計算・状態機械・**入力の解釈（判断）**を 1 箇所に集約し、UI ごとの実装差から切り離す。
- **操作の唯一の窓口は公開ファサード `Marquee`**。UI 層とホストは本型だけを参照する（状態機械とフレーム解決は内部契約として隠す）。
- UI 層は「入力イベントの受付」「効果の適用」「通知の転送」「描画」だけを担う薄い層にし、判断を持たない。
- UI 非依存のためヘッドレス実行が不要で、`[Fact]` で並列実行できる。
- 座標は `EsPoint` / `EsRect` / `EsSize`（`EsUtil.Platform.Core.Geometry`）で扱う。

## 層の分担

```text
UI 層（MarqueeControl.AvaloniaUI / .MewUI）
  ポインター・キー入力をコア型へ変換してファサードへ通知 → 返された描画内容を自分の描画 API へ写す
  （ホイールは組み込みのための要求であり、`MarqueeControl.CoreForControl` を経由する）
  効果（フォーカス / 再描画 / カーソル操作・形状 / ポインターキャプチャ）は IMarqueeEffects 実装が受ける
        │ Marquee（公開ファサード = 操作の唯一の窓口。設定・効果の適用・通知も本型を通る）
        │ Marquee.ResolveContent（1 フレーム分の描画内容。描くかどうかの判断はコアが持つ）
        ▼
状態層（本プロジェクト）
  MarqueeSession  入力の解釈と状態遷移の判断（nudge セッション・ダブルクリックの確定/キャンセル・
                   ハンドルのスナップ・クリック検出・最小サイズによるキャンセル・エコー移動の抑制）
        │ MarqueeNotice（通知: 種別・通知座標・スナップショット）
        ▼
UI 層（通知を公開イベントへ転送。文字はホストが描くため Rendered で描画内容を渡す）
```

## 構成

| 型 | 可視性 | 責務 |
| --- | --- | --- |
| `HitTarget` | public | ヒット対象の種別（8 方向のハンドル + `Move`。`None` は対象なし） |
| `MarqueeInteractionState` | public | 対話状態（NoSelection / StartPointNudging / Dragging / DraggingNudging / Selected / SelectedNudging） |
| `MarqueeSnapshot` | public | 通知時点の状態の値オブジェクト（`selectionRect` は `EsRect`、`mousePosition` は `EsPoint`） |
| `MarqueeOptions` | public | 設定の値オブジェクト（外観 10 項目 + 対話パラメータ 3 項目）。既定値とフォントサイズの補正を単一所有する |
| `Marquee` | public | **公開ファサード（操作の唯一の窓口）**。設定・環境値の保持、入力の通知、外部からの操作、描画内容の解決、効果の適用を行う |
| `IMarqueeEffects` | public | コアが要求する効果（フォーカス・再描画・カーソル形状・ポインターキャプチャ・座標変換）の適用先。UI 層が自 UI の操作へ接続する唯一の境界 |
| `MarqueeFrame` | internal | 1 フレーム分の**描画内容**（ハンドル・始点マーカー・座標ラベル）を解決する（セッションの状態 + 設定 → `MarqueeDrawContent`）。描くかどうかの判断（ハンドル非表示・選択なし・nudge 中など）はすべて本型が持つ。外部は `Marquee.ResolveContent` を使う |
| `MarqueeDrawContent` | public | 1 フレーム分の描画契約（`Decoration` / `InnerDecoration` / `HoleRect` / `Handles` / `Marker` / `SizeLabel` / `CoordLabel` / `Layers`）。UI は `Layers` の順に処理するだけ |
| `MarqueeHandles` / `MarqueeMarker` / `MarqueeCoordLabel` | public | ハンドル・始点マーカー・カーソル座標ラベルの描画契約（描画に必要な値のみ。座標ラベルは `ComputeFrame` で配置を求める） |
| `MarqueeSession` | internal | **状態管理クラス**。入力の通知を受けて状態を更新し、効果（`MarqueeFeedback`）と通知（`MarqueeNotice`）を返す。公開ファサード `Marquee` の内部実装 |
| `MarqueeNotice` / `MarqueeNoticeKind` | public | コアから UI 層への通知（種別・通知座標・スナップショット）。**生のポインター入力（押下・解放・クリック・移動）は通知しない**（入力は UI が受け取りホストも自 UI で観測できるため、通知すると往復になる。押下状態は `MarqueeSnapshot.IsMouseDown` で取得できる） |
| `MarqueeFeedback` / `MarqueeCursorAction` | internal | コアから UI 層への効果（再描画・フォーカス・マウスキャプチャ・カーソル操作・カーソル形状）。適用順は `Marquee` が単一所有し、UI 層は値を受け取らない（ファサードへ入力を通知するだけ） |
| `MarqueeState` | internal | 選択矩形・ドラッグ・nudge の状態保持と更新ロジック（純粋計算の適用）。可変プロパティを多数持つため、外部には公開しない |
| `MarqueeAppearance` | public | UI 実装の描画で共有する外観値（マーカー太さ・ラベル余白・フォントサイズ範囲）とその配置計算・値補正・ハンドル色の解決 |
| `MarqueeCursorKind` | public | 操作位置に対応するカーソル形状の種別（UI は自 UI のカーソル型へ写像するだけ） |
| `SelectionGeometry` | public | 純粋な幾何計算（正規化・ハンドル位置・近傍判定・対角・境界クリップ・ドラッグ変形）・描画対象ハンドル一覧・カーソル種別変換 |
| `MarqueeLogMessage` | internal | ログの LoggerMessage 定義（入力の判断・設定変更）。呼ぶのは状態機械だけであり、UI 層はロガー（`Marquee.Log`）を渡すだけである（自 UI 固有の操作ログは UI 層が持つ。ズーム要求のログは `MarqueeControl.CoreForControl` が単一所有する） |

## 公開面の階層

公開面は「誰が何のために使うか」で 3 段に分かれます。**外部が使わない内部契約は `internal`** とし、単体テストにのみ `InternalsVisibleTo` で公開します。

| 層 | 利用者 | 型 |
| --- | --- | --- |
| **1. ホスト設定** | アプリ・ホスト | `MarqueeOptions` / `HitTarget` / `MarqueeInteractionState` / `MarqueeSnapshot` |
| **2. 公開ファサードと UI アダプタ契約** | UI 実装（入力層・描画層）とホスト（文字の描画） | `Marquee`（操作の唯一の窓口。既定値は `DefaultScaling`）/ `IMarqueeEffects` / `MarqueeCursorKind` / `MarqueeNotice` / `MarqueeNoticeKind` / `MarqueeDrawContent` / `MarqueeHandles` / `MarqueeMarker` / `MarqueeCoordLabel` / `MarqueeAppearance` / `SelectionGeometry` |
| **3. 内部ヘルパー** | 本コアのみ（テストのみ IVT） | `MarqueeFeedback` / `MarqueeCursorAction` / `MarqueeLogMessage`（適用とログ出力はファサード・状態機械が行う）/ `MarqueeSession` / `MarqueeFrame`（「1 フレーム分の描画内容の解決」は `Marquee.ResolveContent` が提供する）/ `MarqueeState` |

- **操作はファサード経由に一本化**しています（状態機械とフレーム解決は `internal` のため、UI 層はファサード以外の入口を持ちません）。
- **層 3 は外部参照 0 件**を実測して絞り込んだ結果です。「描画内容の解決（`Marquee.ResolveContent`）と描画に必要な値」だけが公開面です。
- **装飾（暗転マスク・穴あけ枠・ハイライト・内側の塗り）は Core 内のサブ概念です**（`Decoration` 名前空間）。コントロールを撮影オーバーレイ等へ組み込むときにだけ必要になる表示のため `Selection/` と対にして同居させ、描画順は本コアが指示します。
- **層 2 は UI アダプタを自作する場合の契約**です（独自 UI を追加するには、この面だけで完結します）。
- **層 1 は設定と受け渡しの値**です（ファサードを持たず、設定値と描画契約だけを使う軽い用途でも使えます）。
- `[EditorBrowsable]` による「見えにくくする」対処はしません。公開面も契約も変わらず（見つけにくくなるだけ）、手段と目的が食い違うためです。**隠すか公開するかの二択**とし、隠す場合は `internal` で除きます。

**UI 非依存の汎用ライブラリ**として、特定のアプリ・UI 実装に依存しません（文言・モード等のアプリ固有情報を持ちません）。

名前空間は `Estable.Marquee`（幾何は `Estable.Marquee.Selection`、装飾は `Estable.Marquee.Decoration`）。アセンブリ名は `MarqueeControl.Core`。

## 名前の由来と一般的な呼称

`Marquee` は**マーキー選択**（ドラッグで矩形の範囲を指定する操作）の標準語に由来する名前です。Photoshop の矩形マーキー、Windows Explorer のラバーバンド選択、Qt の `QRubberBand` が同じ操作を指します。

この動作・部品を指す一般的な呼称は次のとおりです。

| 層 | 一般的な呼称 | 実例 |
| --- | --- | --- |
| 動作 | marquee selection / rubber-band selection | Photoshop の矩形マーキー、Qt `QRubberBand`、Windows Explorer のラバーバンド選択 |
| コントロール | rect tracker | MFC `CRectTracker`（「8 個のハンドル付き矩形を移動・リサイズする」＝本コントロールと同じ責務） |
| 用途 | region selector | ShareX / Snipaste / Greenshot の領域キャプチャ |
| 部品 | resize handle / grip / thumb | WPF `Thumb`、Win32 のリサイズグリップ |
| 部品（キー微調整） | nudge | VS / Figma（本コントロールは既にこの用語を使用） |

### 名称の選定理由

`DragRect`（操作に由来する名前で責務を覆い切っていない）から **`Marquee` へ改名**しました（型 20 種・コードとドキュメントの参照約 2,800 箇所。テキスト一括置換ではなく LSP の改名を使用）。

判定は**使用中のバージョン**のアセンブリ（Avalonia 12.1.3 / MewUI 0.21.1 / WinForms = `Microsoft.WindowsDesktop.App` 10.0.12 / WinUI3 = Windows App SDK 1.8 / .NET 10 BCL）のメタデータから**型定義テーブル**を読んで行いました（文字列走査だけでは古いキャッシュ `avalonia/0.10.0` を拾って誤判定し、`ProgressBarStyle.Marquee` のような**列挙メンバーを型と誤認**します）。

| 候補 | 長さ | 型名の衝突（実測） | 評価 |
| --- | --- | --- | --- |
| `Marquee` | 7 | **なし**（WinForms / WinUI3 / Avalonia / MewUI / .NET 10 BCL の**すべてで 0 件**。`MarqueeControl` 等の派生名も 0 件） | 動作の標準語。短い。**採用** |
| `Region` | 6 | **あり**（`System.Drawing.Region`。WinForms 版は `using System.Drawing;` が必須のため実衝突する） | 最短級だが WinForms 追加時に衝突する |
| `Area` | 4 | なし | 最短。汎用すぎて意味が弱い |
| `RubberBand` | 10 | 型は 0 件。ただし Avalonia に `_isRubberBanding` / `RubberBandFactor` / `RubberBandReturnDuration`（伸縮スクロール）があり、**同一フレームワーク内で別の意味に使われている** | Qt の標準語。長い |
| `RegionSelect` | 12 | なし | 役割を最も明確に表す。長い |
| `RectTracker` | 11 | なし | MFC の系譜で Windows では最も正確。`Tracker` が状態管理クラスと紛らわしい |
| `Selector` / `Thumb` | — | **あり**（`Avalonia.Styling.Selector` / `System.Drawing`・`System.Windows.Forms`・`Microsoft.UI.Xaml.Controls.Primitives.Thumb`） | 使用不可 |

**`Marquee` の「進行バー」の連想について**: WinForms には `ProgressBarStyle.Marquee` / `ProgressBar.MarqueeAnimationSpeed` / `MarqueePaused` という**メンバー**があり、WinUI3 には `Marquee` を含むメンバーが**存在しない**（不確定表示は `IsIndeterminate`）。C# では**メンバー名は型名と衝突しない**（`ProgressBarStyle.Marquee` は型で修飾して使う）ため、`MarqueeControl` / `MarqueeOptions` 等の型名に影響はない。

**併せて確認したこと（将来の WinForms / WinUI3 版に向けて）**: 本リポジトリの現在の**公開型 111 種は WinForms / WinUI3 / Avalonia / MewUI / BCL のいずれとも衝突しない**。UI を追加するときに注意するのは型名ではなく、色型（WinUI3 は `Windows.UI.Color`）とコントロール基底（WinForms は `System.Windows.Forms.Control`）の境界変換である。

### 改名に併せて揃えた名前

| 旧 | 新 | 理由 |
| --- | --- | --- |
| `DragHandle`（`None` / `Move` + 8 方向） | `HitTarget` | ハンドル以外（`None` / `Move`）も含むため、ヒット対象を表す名前にする |
| `MarqueeSnapshot.ActiveHandle` / `MarqueeState.ActiveHandle` | `ActiveTarget` | 同上 |
| `DragRectZoomRequest` / `DragRectScrollRequest` | `WheelZoomRequest` / `WheelScrollRequest` | コントロール固有の接頭辞を外し、ホイール操作の要求であることを表す |

## 選択矩形の外側に描く装飾（`Decoration` 名前空間）

**装飾（暗転マスク・穴あけ枠・ハイライト・穴の内側の塗り）は Core 内のサブ概念**（`src/Decoration/`・名前空間 `Estable.Marquee.Decoration`）です。コントロールを**撮影オーバーレイ等へ組み込むときにだけ必要になる表示**であり、選択矩形コントロール単体の責務ではありません（Core でも `Selection/` と対になるサブ概念として同居させ、別プロジェクトには分けません）。

装飾の設定（`MarqueeDecoOptions`）・描画内容（`OuterDecoFrame` / `InnerDecoFrame`）は装飾のフレーム解決が単一所有し、**描画順はコア（`MarqueeFrame`）が指示します**。

| 項目 | 所有者 | 内容 |
| --- | --- | --- |
| 装飾の設定（外観・表示ポリシー・ハイライト・内側） | `Decoration` の `MarqueeDecoOptions` | 暗転色・枠線・ハイライト矩形・穴の内側の塗り |
| 装飾の描画内容と描画の可否の判断 | `Decoration` の `OuterDecoFrame.Resolve` / `InnerDecoFrame.Resolve` | 暗転領域・枠線矩形・塗り矩形（透明指定は描かない） |
| サイズラベル（穴あけ矩形の位置と寸法） | `MarqueeSizeLabel` / `MarqueeOptions`（本コア） | 文言（`Format`）・外観・基準点（穴あけ矩形の左上・DIP） |
| カーソル座標ラベル | `MarqueeCoordLabel` / `MarqueeOptions`（本コア） | 文言（`CoordinateFormatter`）・外観・配置（`ComputeFrame`） |
| 操作ラベル・情報ラベル | **ホスト**（アプリ固有の付加情報） | 文言の生成と描画。コアは表示可否も含めて関知しない |
| 選択矩形・選択の有無のソース | 本コア（`Marquee.Session` 経由） | 装飾は参照のみ（複製しない） |
| 選択枠線（ハンドル）の枠線色 | `MarqueeOptions.HandleBorderColor` | 穴あけ枠と同じ矩形に重なるため、ホストが同じ値を割り当てる |

- 本コアは装飾の設定（`Marquee.DecoOptions`）と描画内容（`MarqueeDrawContent.Decoration` / `InnerDecoration`）を解決します。
- **描画順は本コアが単一所有します**: `MarqueeDrawContent.Layers`（`MarqueeDrawLayer`）が層と順序を指示し、UI 層はそれに従うだけです（ラベル層を必ず最後に置くため、ラベルは常に最上位になります）。

### 層の責務の境界（選択矩形）

| 責務 | 所有者 | 内容 |
| --- | --- | --- |
| 設定と状態 | 本コア（値） / UI（保持） | 選択の設定は `MarqueeOptions`、装飾の設定は `MarqueeDecoOptions`（いずれも公開値）、選択矩形はセッションが持つ。UI はフレームワークのプロパティ機構で保持し、ファサードへ伝搬するだけ |
| 判断・計算 | 本コア | 選択の状態遷移・スナップ・最小サイズ・論理矩形 → DIP 変換（選択は閉区間 +1）・ハンドル/マーカー/ラベルの可否と値・描画順 |
| 契約 | 本コア | `MarqueeDrawContent`（装飾・ハンドル・始点マーカー・サイズラベル・座標ラベル・層の順序）・`Marquee.ResolveContent`（公開のエンドポイント。`MarqueeSession` / `MarqueeFrame` / `OuterDecoRequest` / `InnerDecoRequest` は内部契約） |
| 描画（図形） | UI（コントロール） | 描画内容の各項目を自 UI の描画 API へ写す（`FillRectangle` / `DrawRect` 等） |
| 描画（文字） | UI（コントロール・ホスト） | サイズラベル・座標ラベルは **UI 層がテキスト要素を配置**する（CoreForControl の `MarqueeLabelLayoutPlan` が実寸から配置矩形を求める）。操作・情報ラベルは**ホストが自前で描く** |
| ホスト結線 | UI | プロパティ機構・DPI 追従・キャンバスのスケーリング・ブラシキャッシュ・テキストの実寸測定とフォント解決・要素の配置 |

UI 層に残る分岐は次の 4 種のみで、いずれも本コアへ移せません。

1. **空のコレクションのスキップ**（ブラシ生成を避ける性能ガード）
2. **描画内容の項目の有無**（値を持つ項目だけを描く＝描画の前提条件）
3. **フレームワーク固有の値の解決**（DPI の 0 以下フォールバック・色型変換）
4. **描画方式の差**（直接描画 / 中間バッファ・ブラシキャッシュ）

**文字（座標ラベル）はコントロールが描かず、ホストが描きます**（テキストの実寸測定は描画 API 依存のため）。<br/>
配置（背景矩形・文字原点）の計算は本コアが単一所有し、コントロールは描画完了時（`Rendered`）に契約を渡します。

ハンドル・始点マーカーの描画は各 UI の描画層（`MarqueeControl.AvaloniaUI` の `MarqueeDrawer` / `MarqueeControl.MewUI` の `MarqueeSkiaRenderer`）が行います（本コアは描画 API を持たない）。<br/>
文字（サイズラベル・カーソル座標ラベル）は **UI 層がテキスト要素を配置**します（`Border` + `TextBlock`）。実寸から配置矩形を求める写像は `MarqueeControl.CoreForControl` の `MarqueeLabelLayoutPlan` が単一所有します。

## 依存関係

```text
MarqueeControl.Core
    ├── Platform.Core（Es* 幾何型 / EsKey / ICoordTransform / ICursorService）
    └── Microsoft.Extensions.Logging.Abstractions（LoggerMessage ソース生成のみ）
```

**UI フレームワークへ依存しません。** カーソル操作は `ICursorService` 抽象にのみ依存し、未指定（`null`）の場合は何もしません。既定実装（Win32）の解決は UI 側の責務です。

## 主な API

```csharp
var marquee = new Marquee(effects, cursorService, options);  // effects は IMarqueeEffects（UI 層の実装）/ cursorService・options は null 可
marquee.Log = logger;                                        // null は NullLogger へ吸収される
marquee.SetCoordinateTransform(new CoordTransform(p => p, p => p, () => new EsSize(1920, 1080))); // ドメイン境界

// 入力の通知（UI 層から呼ぶ。効果の適用までファサードが行う）
marquee.Press(new EsPoint(100, 100), clickCount: 1);          // 押下（新規選択・ハンドル・確定/キャンセル）
marquee.Move(new EsPoint(400, 300));                          // 移動（ドラッグ・ホバー）
marquee.Release(new EsPoint(400, 300));                       // 解放（確定・キャンセル・クリック）
var handled = marquee.KeyDown(EsKey.Right, cursorPosition: null); // キー（nudge・Escape・Enter。処理済みの真偽を返す）
// ホイールは選択の判断ではないため本コアには無い（組み込み時に MarqueeControl.CoreForControl の MarqueeWheelInterpreter が解釈する ）

// 外部（ホスト）からの操作
marquee.Options = options;                                    // 設定の反映（対話パラメータ）
marquee.SetSelection(rect);                                   // 選択の復元
// UI 層が対話を終了する場合の後片付けも ResetSelection（対話状態の初期化）を使う
// ビューポートの変化の通知はコントロール化の層（MarqueeControl.CoreForControl の MarqueeViewportNotifier）が担う

// 描画（1 フレーム分の描画内容。描くかどうかの判断はコア）
var content = marquee.ResolveContent(clientSize);             // 値を持つ項目を自 UI の描画 API へ写すだけ

// 通知（公開イベントへ転送する）
marquee.Notified += (_, notice) => Raise(notice);
```

| メソッド | 用途 |
| --- | --- |
| `Press` / `Move` / `Release` | ポインター入力の通知（ドメイン座標）。ダブルクリックの確定/キャンセル・スナップ・クリック検出を含む |
| `KeyDown` | キー入力の通知（`EsKey`）。Escape / Enter（`Return` も同一）と矢印キーの nudge。処理済みの真偽を返す |
| `EndNudge` | nudge の終了（フォーカス喪失で使用する） |
| `SetSelection` / `ResetSelection` / `RequestConfirm` / `RequestCancel` | ホスト・プロパティ変更からの操作（`ResetSelection` は UI 層が対話を終了する際の後片付けにも使う） |
| `Options`（設定） | 設定（`MarqueeOptions`）の反映。対話パラメータを更新し、境界外選択の禁止が変化したときだけログと即時クリップを行う（プロパティの代入で指定する） |
| `ToDomain` / `ToDip` / `SetCoordinateTransform` | 座標変換（ドメイン ⇔ ローカル DIP。選択矩形は閉区間として変換） |
| `ResolveContent` | 1 フレーム分の描画内容の解決（描画領域のサイズを渡す。原点は常に 0 であり、親座標を混ぜるとハンドルがずれるため型で受ける） |
| `Options` / `DecoOptions` / `Scaling` / `DisplayScaleScaling` / `SizeLabelOverrideProvider` / `Log` | 設定・環境値・文言プロバイダの読み書き（`Log` は null を `NullLogger` へ吸収する） |
| `GetCoordinateTransform` | 設定中の座標変換ポートを返す（未設定は `null`。装飾層が描画時に DIP へ変換するために使う） |
| `DefaultScaling` | ポート未指定時に適用する倍率の既定値（`1.0`）。両 UI の依存関係プロパティの既定値も本値を参照する |
| `SelectionRect` / `HasSelection` / `CreateSnapshot` / `Notified` | 状態の読み取りと通知（描画・公開イベントへの転送用） |

UI 層が適用する効果（`MarqueeFeedback`）の適用順はコアが単一所有する（UI 層は `IMarqueeEffects` を実装して自 UI の操作へ接続するだけ）:

| メソッド | 適用内容・順序 |
| --- | --- |
| `ApplyCursor` | カーソル操作（移動 → 非表示 → 再表示）。画面座標の変換のみデリゲートで受け取る |
| `ApplyVisual` | フォーカス → カーソル形状 → 再描画（要求されたものだけ） |

UI 層が使う共有値・計算（`MarqueeAppearance`）:

| メンバー | 用途 |
| --- | --- |
| `MarkerThickness` / `HandlePenThickness` / `LabelOffset` / `LabelPadding` | 両 UI で共有する描画の外観値（片方だけ変えると見た目がずれるため単一所有） |
| `CoerceCoordLabelFontSize` | 座標ラベルのフォントサイズを安全域（6〜48）へ補正（非数は既定値） |
| `ComputeCoordLabelPosition` / `ComputeCoordLabelBackground` | 座標ラベルの左上位置（カーソル右側 + 描画領域内へのクランプ）と背景の大きさ |
| `ResolveHandleColor` | ハンドルの塗り色（未指定（全成分 0）は白へ解決。両 UI が同じ見た目になる） |
| `SelectionGeometry.DirectionalHandles` | 描画対象の 8 方向ハンドル（列挙順は `HitTarget` の宣言順） |
| `SelectionGeometry.ToCursorKind` | ハンドル → `MarqueeCursorKind`（どのハンドルが同じカーソルになるか） |

## 設計上の約束

1. **判断はコア・反映は UI**: 「どうするか」（nudge の開始/終了・確定/キャンセル・スナップの有無・キャンセル可否・カーソル種別）はコアが決め、UI は効果を適用するだけにする。
2. **単一所有**: 幾何計算は `SelectionGeometry` のみ、状態は `MarqueeState` のみ、入力の解釈は `MarqueeSession` のみが持つ。
3. **座標系**: `EsRect` / `EsPoint` は常にドメイン座標。UI 論理座標（DIP）との変換は `ICoordTransform`（`Port`）が担う。
4. **境界 1 行変換**: 各 UI のイベント引数は自前の型（例: Avalonia の `Rect`）で公開し、境界で 1 行変換する。キーは `EsKey` を介して通知する。
5. **カーソル操作の分離**: カーソル操作（非表示・再表示・移動）は**効果**として UI 層へ要求し、公開ファサードだけが `ICursorService` 抽象に依存する（プラットフォーム既定実装を生成しない）。状態機械はカーソル API を知らない。カーソル種別は `HitTarget` として返し、UI が自 UI のカーソル型へ写像する。
6. **Enter と Return は同一**: Avalonia の `Key` は両者が同値のエイリアスで、名前ベースの相互変換では `Return` が得られるため、双方を確定キーとして扱う。
7. **両 UI で共通の値はコアが単一所有**: 外観値（マーカー太さ・ラベル余白・フォントサイズ範囲）・配置計算・値補正・描画対象ハンドル一覧・カーソル形状の種別・**設定の既定値と範囲補正（`MarqueeOptions`）**は、Avalonia 版と MewUI 版で同じ結果になる必要があるためコアが持つ（片方だけを直すと見た目や既定値がずれる）。UI 層が持つのは自 UI の描画・カーソル型への写像・座標変換のみ。
8. **設定の適用はコアが判断する**: 設定（`MarqueeOptions`）の反映は `ApplyOptions` が担い、対話パラメータの更新・境界外選択の禁止の変化の検出・変更ログ・即時クリップを 1 箇所に集約する（UI 層は「変更通知を渡す」だけになる）。
9. **効果の適用順もコアが持つ**: 「どの効果をどの順で適用するか」（カーソル操作の順序・フォーカス → カーソル形状 → 再描画）は `MarqueeFeedback` が単一所有し、UI 層は自 UI の操作（`Focus` / `InvalidateVisual` / カーソル型への写像）へ接続するだけでよい。**カーソルの非表示・再表示も効果として要求する**（状態機械はカーソル API を知らない。適用の順序を 1 経路に集約し、非表示の残留を防ぐ）。
10. **持続資源の保持もコア**: ロガーは `Marquee.Log` が保持し、null を `NullLogger` へ吸収する（UI は get/set を委譲するだけ）。入力の判断のログも同じロガーで出力する。
11. **描画順は本コアが単一所有する**: `MarqueeDrawLayer`（`Decoration` → `Handles` → `Marker` → `SizeLabel` → `CoordLabel`）で層と順序を指示し、UI 層はその順に処理するだけにする。**ラベル層を必ず最後に置く**ため、ラベルは常に最上位に表示される（UI の子要素は親の描画より後に描かれる性質とも一致する）。
12. **選択の状態はセッションが単一のソース**: 選択矩形と選択の有無は設定値ではなくセッションの状態から `Marquee.ResolveContent` が導出し、装飾層も同じ値を参照する（同じデータを 2 箇所に持たない）。座標変換ポート（`GetCoordinateTransform`）も本コアが保持し、装飾層が描画時に参照する。
13. **描画内容の契約は本コアが単一所有する**: `Marquee.ResolveContent` が「装飾（`OuterDecoFrame` / `InnerDecoFrame`）・穴あけ矩形・ハンドル（`MarqueeHandles`）・始点マーカー（`MarqueeMarker`）・サイズラベル（`MarqueeSizeLabel`）・カーソル座標ラベル（`MarqueeCoordLabel`）・層の順序」を 1 つの `MarqueeDrawContent` にまとめて返し、**描くかどうかの判断（非表示の設定・選択なし・nudge 中など）をすべてコアが下す**（公開は描画内容の型と `Marquee.ResolveContent`。組み立てに使う `MarqueeSession` / `MarqueeFrame` / `OuterDecoRequest` / `InnerDecoRequest` は internal）。
14. **文字は Core が文言と配置を決め、UI 層が要素を配置する**: サイズラベル（`MarqueeSizeLabel`）とカーソル座標ラベル（`MarqueeCoordLabel`）の**文言・外観・配置の計算は本コアが単一所有**し、**実寸測定と要素への反映は UI 層**が担う（実寸は描画 API でしか測れないため）。CoreForControl の `MarqueeLabelLayoutPlan` が実寸から配置矩形を求める写像を単一所有し、UI 層は `Border` + `TextBlock` を子として置く。**操作・情報ラベルはアプリ固有の付加情報**であるため、本コアは関知せずホストが自前で描く。
15. **ハンドルの枠線色は本コアが持つ**: `MarqueeOptions.HandleBorderColor` が単一所有する（装飾層の穴あけ枠と同じ矩形に重なるため、ホストが同じ値を割り当てる。分離により 2 つの層に値が分かれたため、単一所有を維持する手段を設定値で残す）。
16. **ハンドル・マーカー・座標ラベルの可否もコアが決める**: 「ハンドルを描くか（`ShowHandles` と選択の有無）」「マーカーを描くか（nudge 中で十字線が可視か）」「座標ラベルを描くか（`ShowCoordLabel`）」の判断は `Marquee.ResolveContent` が行う。UI 層は値を持つ項目を描くだけでよい（判断の分散を防ぐ）。
17. **操作の入口はファサードのみ**: UI 層とホストは `Marquee` だけを参照し、状態機械（`MarqueeSession`）とフレーム解決（`MarqueeFrame`）には触れない（`internal` で強制する）。設定の伝搬・効果の適用・通知の転送・描画内容の解決も本型が単一所有し、UI 層は自 UI の公開プロパティ・公開イベント・描画 API へつなぐだけになる。
18. **選択の判断ではない規則はコアに置かない**: 「Ctrl+ホイール = ズーム」・「ビューポートの変化の再通知」のようなビューポート連携の規則は、単体で使う Marquee には存在しない概念であるため `MarqueeControl.CoreForControl` が単一所有する（コアは将来そのまま別プロジェクトへ独立できる）。
19. **他のコントロールの語彙を持たない**: コアは Marquee 単体の概念だけを持ち、別のコントロール（ルーペ等）との連携名を持ち込まない。追従位置は汎用名（`MarqueeNoticeKind.MarkerPositionMoved`）で通知し、ルーペ中心への写像は UI 層の公開イベントが担う（コアは将来そのまま別プロジェクトへ独立できる）。

> **Note**: `MarqueeControl.Core` には `SPEC_ExtFunc.md` / `SPEC_System.md` を置かず、公開面・設計を本 README に集約しています（描画内容の契約は「構成」と「公開面の階層」の表、層の分担と描画の責務は「層の責務の境界」の節が仕様を兼ねます）。

## テスト

```text
tests/
├── SelectionGeometry.Tests.cs        # 純粋幾何（17 件）
├── Marquee.Tests.cs                 # 公開ファサード（操作・効果の適用・設定の伝搬・描画内容の解決・通知の転送）（34 件）
├── MarqueeState.Tests.cs            # 状態遷移・ヒットテスト・ドラッグ・nudge・境界クリップ（32 件）
├── MarqueeFeedback.Tests.cs         # 効果の適用（カーソル操作・表示上の効果の順序）（6 件）
├── MarqueeSession.Tests.cs          # 入力の解釈・通知・効果・設定の反映・カーソル再表示の要求（50 件）
├── MarqueeAppearance.Tests.cs       # ラベル配置・値補正・ハンドル一覧・カーソル種別（19 件）
├── MarqueeOptions.Tests.cs          # 既定値・部分変更・値補正・等価性（14 件）
├── MarqueeFrame.Tests.cs            # 描画内容の解決（ハンドル・マーカー・座標ラベル・描画可否）（13 件）
└── MarqueeLogMessage.Tests.cs       # ログ定義の生成コード（IsEnabled 両パス・書式）（3 件）
```

件数は `[Theory]` を展開したケース数（合計 188 件）です。装飾（`Estable.Marquee.Decoration`）のテストは本プロジェクトではなく `MarqueeControl.Core/tests` にあります（合計 140 件）。

```powershell
dotnet test src/MarqueeControl.Core/tests/MarqueeControl.Core.Tests.csproj
```

## ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
