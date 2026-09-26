# Feature.MarqueeCore

`MarqueeControl`（マーキー選択＝ドラッグで矩形を選び、移動・リサイズできるコントロール）の**UI 非依存の状態層**です。Avalonia 版と MewUI 版が同じ状態管理・ロジックを共有します。

## 目的

- 選択矩形の幾何計算・状態機械・**入力の解釈（判断）**を 1 箇所に集約し、UI ごとの実装差から切り離す。
- **操作の唯一の窓口は公開ファサード `Marquee`**。UI 層とホストは本型だけを参照する（状態機械とフレーム解決は内部契約として隠す）。
- **描画は本コアの関心事ではない**。本コアは描画 API も描画ポートも持たない（依存先は `Platform.Core` だけである）。返すのは「何を描くか」のデータ（`MarqueeDrawContent` と `Layers`）だけであり、層の順序に従った描画手順は UI 側の共有層（`MarqueeControl.UIBase` の `MarqueeRendererBase`）が単一所有する。
- UI 層は「入力イベントの受付」「効果の適用」「通知の転送」「描画」だけを担う薄い層にし、判断を持たない。
- UI 非依存のためヘッドレス実行が不要で、`[Fact]` で並列実行できる。
- 座標は `EsPoint` / `EsRect` / `EsSize`（`EsUtil.Platform.Core.Geometry`）で扱う。

## 層の分担

層の全体像（Core / UIBase / UI）は [`src/README.md`](../README.md) を参照してください。

```text
UI 層（MarqueeControl.AvaloniaUI / MarqueeControl.MewUI）
  入力の受付・効果の適用・通知の転送・描画先の用意・ラベル要素の配置
    ├──> MarqueeControl.UIBase   どう描くか（UIBase の IMarqueeRenderer を実装し、描画内容を写す）＋ 組み込み先がある場合にだけ必要な規則
    └──> Feature.MarqueeCore ← 本プロジェクト   何を描くか + 判断（状態・値・純粋計算・描画内容の契約）
           └──> Platform.Core（Es* 値型・ICoordTransform・ICursorService）
※ Core は UIBase を参照しない（UIBase がコアの描画内容を使う向きだけ）。Core は Common も参照しない。
```

## 構成

ルート（`src/`）に置くのは**ファサード本体とその partial**（`Marquee.cs` / `Marquee.DrawContent.cs`）だけです。それ以外は用途別のフォルダへまとめます。
**1 ファイル 1 型**とします（複数の型を 1 ファイルへ含めない。`partial` による分割は可）。
**名前空間はフォルダに追従**させます（ルート直下のみ `EsUtil.Feature.MarqueeCore`。`Contract` / `Drawing` / `Interaction` / `Setting` / `Logging` / `Selection` / `Decoration` は `EsUtil.Feature.MarqueeCore.<フォルダ名>`）。公開面も複数のフォルダに分かれるため、利用者は用途に応じて `using` を追加します。

| フォルダ | 型 | 可視性 | 責務 |
| --- | --- | --- | --- |
| `Interaction/` | `HitTarget` | public | ヒット対象の種別（8 方向のハンドル + `Move`。`None` は対象なし） |
| `Interaction/` | `MarqueeInteractionState` | public | 対話状態（NoSelection / StartPointNudging / Dragging / DraggingNudging / Selected / SelectedNudging） |
| `Interaction/` | `MarqueeSnapshot` | public | 通知時点の状態の値型（`readonly record struct`。`selectionRect` は `EsRect`、`mousePosition` は `EsPoint`。操作中の通知のたびにヒープを確保しない） |
| `Setting/` | `MarqueeOptions` | public | 設定の値オブジェクト（寸法・表示可否・対話パラメータ）。既定値を単一所有する（**色・フォントサイズは持たない**。これらは描画にだけ必要な設定であり、描画層 `MarqueeControl.UIBase` の `MarqueePaint` が単一所有する）。ラベルの体裁（内側余白・行送り）も描画層の `MarqueePaint` が単一所有し、本型はコントロール固有の値（`CoordLabelOffset` / `HandleBorderThickness`）を public const として公開する |
| ルート | `Marquee` | public | **公開ファサード（操作の唯一の窓口）**。設定・環境値の保持、入力の通知、外部からの操作、描画内容の解決、効果の適用を行う |
| `Contract/` | `IMarqueeEffects` | public | コアが要求する効果（フォーカス・再描画・カーソル形状・ポインターキャプチャ・座標変換）の適用先。UI 層が自 UI の操作へ接続する唯一の境界 |
| `Contract/` | `MarqueeEffectsBase` | public abstract | `IMarqueeEffects` の全メンバーを virtual の空実装で提供する基底クラス。独自コントロールは必要な効果だけ override する（不要なメンバーの空実装を書かずに済む） |
| ルート | `Marquee.DrawContent.cs`（`Marquee` の **partial**） | internal | 描画内容の**内部手順**（装飾・穴あけ矩形・ハンドル・始点マーカー・テキストの配置の解決）を持つ。公開の入口（`ResolveContent`）はファサード本体（`Marquee.cs`）が持つ。穴あけ矩形と座標変換ポートは 1 フレームに 1 回だけ解決する |
| `Drawing/` | `MarqueeDrawContent` | public | 1 フレーム分の描画契約（`Decoration` / `InnerDecoration` / `HoleRect` / `Handles` / `Marker` / `SizeLabelText` / `CoordLabelText` / `Layers`）。UI は `Layers` の順に処理するだけ |
| `Drawing/` | `MarqueeHandles` | public | ドラッグハンドルの描画契約（8 方向の矩形と枠線の太さ。位置はコアが解決済み） |
| `Drawing/` | `MarqueeMarker` | public | 始点マーカー（十字線）の描画契約（中心・太さ・半幅） |
| `Drawing/` | `MarqueeTextPlacement` | public | コアが UI 層へ渡すテキスト（文言・基準点・アライン・描画領域）。要素への写像は描画層の `MarqueeLabelPlan` が単一所有する |
| `Drawing/` | `MarqueeRenderRequest` | public | 1 フレーム分の描画内容を解決する入力（設定・状態と、描画時に決まる値）。UI 層は自分で組み立てず、コアの `Marquee.CreateRequest` が返した値を受け取る（内部状態の組み立て順を UI が持たない）。**本値から導出する値（座標変換ポートの決定・論理座標から DIP への変換・穴あけ矩形の決定）は本値が単一所有する** |
| `Interaction/` | `MarqueeSession` | internal | **状態管理クラス**。入力の通知を受けて状態を更新し、効果（`MarqueeFeedback`）と通知（`MarqueeNotice`）を返す。公開ファサード `Marquee` の内部実装 |
| `Interaction/` | `MarqueeNotice` | public | コアから UI 層への通知（種別・通知座標・スナップショット。値型）。**生のポインター入力（押下・解放・クリック・移動）は通知しない**（入力は UI が受け取りホストも自 UI で観測できるため、通知すると往復になる。押下状態は `MarqueeSnapshot.IsMouseDown` で取得できる） |
| `Interaction/` | `MarqueeNoticeKind` | public | 通知の種別（選択の開始・変更・確定・キャンセル・追従位置・確定/キャンセル要求） |
| `Interaction/` | `MarqueeFeedback` | internal | コアから UI 層への効果（再描画・フォーカス・マウスキャプチャ・カーソル操作・カーソル形状）。適用順は `Marquee` が単一所有し、UI 層は値を受け取らない（ファサードへ入力を通知するだけ） |
| `Interaction/` | `MarqueeCursorAction` | internal | 要求するカーソル操作のフラグ（移動・非表示・再表示） |
| `Interaction/` | `MarqueeCaptureAction` | internal | 要求するポインターキャプチャの操作（取得・解放） |
| `Interaction/` | `MarqueeState` | internal | 選択矩形・ドラッグ・nudge の状態保持と更新ロジック（純粋計算の適用）。可変プロパティを多数持つため、外部には公開しない |
| `Interaction/` | `MarqueeCursorKind` | public | 操作位置に対応するカーソル形状の種別（UI は自 UI のカーソル型へ写像するだけ） |
| `Selection/` | `SelectionGeometry` | internal | 純粋な幾何計算（正規化・ハンドル位置・近傍判定・対角・境界クリップ・ドラッグ変形）・描画対象ハンドル一覧・カーソル種別変換。利用者はコアの内部（描画内容の解決・状態機械）だけであり、UI 層へは解決済みの矩形が渡る |
| `Logging/` | `MarqueeLogMessage` | internal | ログの LoggerMessage 定義（入力の判断・設定変更）。呼ぶのは状態機械だけであり、UI 層はロガー（`Marquee.Log`）を渡すだけである（自 UI 固有の操作ログは UI 層が持つ。ズーム要求のログは `MarqueeControl.UIBase` が単一所有する） |

## 公開面の階層

公開面は「誰が何のために使うか」で 3 段に分かれます。**外部が使わない内部契約は `internal`** とし、単体テストにのみ `InternalsVisibleTo` で公開します。

| 層 | 利用者 | 型 |
| --- | --- | --- |
| **1. ホスト設定** | アプリ・ホスト | `MarqueeOptions` / `HitTarget` / `MarqueeInteractionState` / `MarqueeSnapshot` |
| **2. 公開ファサードと UI アダプタ契約** | UI 実装（入力層） | `Marquee`（操作の唯一の窓口。既定値は `DefaultScaling`） / `IMarqueeEffects` / `MarqueeEffectsBase` / `MarqueeCursorKind` / `MarqueeNotice` / `MarqueeNoticeKind` / `MarqueeDrawContent` / `MarqueeHandles` / `MarqueeMarker` / `MarqueeTextPlacement` / `SelectionGeometry` |
| **3. 内部ヘルパー** | 本コアのみ（テストのみ IVT） | `MarqueeFeedback` / `MarqueeCursorAction` / `MarqueeLogMessage`（適用とログ出力はファサード・状態機械 が行う）/ `MarqueeSession` / `Marquee.DrawContent.cs`（「1 フレーム分の描画内容の解決」は `Marquee.ResolveContent` が提供する）/ `MarqueeState` |

- **操作はファサード経由に一本化**しています（状態機械とフレーム解決は `internal` のため、UI 層はファサード以外の入口を持ちません）。
- **層 3 は外部参照 0 件**を実測して絞り込んだ結果です。「描画内容の解決（`Marquee.ResolveContent`）と描画に必要な値」だけが公開面です。
- **装飾（暗転マスク・穴あけ枠・ハイライト・内側の塗り）は Core 内のサブ概念です**（`Decoration` 名前空間）。コントロールを撮影オーバーレイ等へ組み込むときにだけ必要になる表示のため `Selection/` と対にして同居させ、描画順は本コアが指示します。
- **層 2 は UI アダプタを自作する場合の契約**です（独自 UI を追加するには、この面だけで完結します。`IMarqueeEffects` は `MarqueeEffectsBase` を継承すれば必要な効果だけを実装できます）。
- **層 1 は設定と受け渡しの値**です（ファサードを持たず、設定値と描画契約だけを使う軽い用途でも使えます）。
- `[EditorBrowsable]` による「見えにくくする」対処はしません。公開面も契約も変わらず（見つけにくくなるだけ）、手段と目的が食い違うためです。**隠すか公開するかの二択**とし、隠す場合は `internal` で除きます。

**UI 非依存の汎用ライブラリ**として、特定のアプリ・UI 実装に依存しません（文言・モード等のアプリ固有情報を持ちません）。

名前空間はフォルダに追従します（ルート直下は `EsUtil.Feature.MarqueeCore`。`Contract` / `Drawing` / `Interaction` / `Setting` / `Logging` / `Selection` / `Decoration` は `EsUtil.Feature.MarqueeCore.<フォルダ名>`）。アセンブリ名は `Feature.MarqueeCore`。

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

**装飾（暗転マスク・穴あけ枠・ハイライト・穴の内側の塗り）は Core 内のサブ概念**（`src/Decoration/`・名前空間 `EsUtil.Feature.MarqueeCore.Decoration`）です。コントロールを**撮影オーバーレイ等へ組み込むときにだけ必要になる表示**であり、選択矩形コントロール単体の責務ではありません（Core でも `Selection/` と対になるサブ概念として同居させ、別プロジェクトには分けません）。

装飾の設定（`MarqueeDecoOptions`）は本コアが単一所有し、**装飾の描画内容（暗転領域・枠線・塗り）の解決と描画の可否の判断は `Marquee.ResolveContent` が担います**（結果型 `OuterDecoFrame` / `InnerDecoFrame` は描画に必要な値だけを持つ純粋データであり、判断を持ちません）。**描画順もコア（`Marquee.ResolveContent`）が指示します**。

| 項目 | 所有者 | 内容 |
| --- | --- | --- |
| 装飾の設定（寸法・描画可否・ハイライト・内側） | `Decoration` の `MarqueeDecoOptions` | 枠線の太さ（`OuterAppearance` / `InnerAppearance`）・描画可否の明示フラグ（`ShowOverlay` / `ShowOuterBorder` / `ShowInnerFill` / `ShowInnerBorder`）・ハイライト矩形 |
| 装飾の描画内容と描画の可否の判断 | `Marquee.ResolveContent`（`ResolveOuterDecoration` / `ResolveInnerDecoration`） | 暗転領域・枠線矩形・塗り矩形（可否は `MarqueeDecoOptions` の明示フラグ。**アルファ 0 を「描かない」の代用にしない**）。**穴あけ矩形は 1 回だけ解決して外側・内側・サイズラベルへ同じ値を渡す** |
| サイズラベル（穴あけ矩形の位置と寸法） | `MarqueeTextPlacement`（本コア） | 文言（コアが矩形から生成し、書式は内部に閉じる）・基準点（穴あけ矩形の左上・DIP）・アライン。体裁（背景矩形・内側余白・フォントサイズ・色）は描画層 `MarqueeControl.UIBase` の `MarqueePaint` / `MarqueeLabelPlan` が単一所有する |
| カーソル座標ラベル | `MarqueeTextPlacement`（本コア） | 文言（`EsPoint.Format`）・基準点（カーソル + `MarqueeOptions.CoordLabelOffset`）・アライン。要素への写像は描画層の `MarqueeLabelPlan`、フォントサイズと色は `MarqueePaint`（描画層）が単一所有する |
| 装飾・ハンドル・マーカー・ラベルの色 | `MarqueePaint`（描画層 `MarqueeControl.UIBase`） | `OverlayColor` / `OuterBorderColor` / `InnerFillColor` / `InnerBorderColor` / `HandleColor` / `HandleBorderColor` / `MarkerColor` / `SizeLabelColor` / `SizeLabelBackgroundColor` / `CoordLabelColor` / `CoordLabelBackgroundColor` |
| 操作ラベル・情報ラベル | **ホスト**（アプリ固有の付加情報） | 文言の生成と描画。コアは表示可否も含めて関知しない |
| 選択矩形・選択の有無のソース | 本コア（`Marquee` のセッション経由） | 装飾は参照のみ（複製しない） |
| 選択枠線（ハンドル）の枠線色 | `MarqueePaint.HandleBorderColor`（描画層） | 既定は穴あけ枠（`OuterBorderColor`）と共通の `MarqueePaint.DefaultHandleBorderColor`（`#FFA9A9A9`）。別の色にする場合は `MarqueePaint` の各値を指定する |

- 本コアは装飾の設定（`Marquee.DecoOptions`）と描画内容（`MarqueeDrawContent.Decoration` / `InnerDecoration`）を解決します。
- **色は本コアに含まれません**: 装飾・ハンドル・マーカー・ラベルの色は描画にだけ必要な設定であるため、描画層（`MarqueeControl.UIBase` の `MarqueePaint`）が単一所有します（`MarqueeDrawContent` とその要素型も色を持ちません）。
- **描画順は本コアが単一所有します**: `MarqueeDrawContent.Layers`（`MarqueeDrawLayer`）が層と順序を指示し、UI 層はそれに従うだけです（テキストの層を必ず最後に置くため、テキストは常に最上位になります）。
- **テキストは層と値の両方で渡します**: 層（`MarqueeDrawLayer.Label`）は canvas へ描く実装のためのものであり、**UI 要素として置く UI は無視してよい**（要素は親の描画より後に描かれる）。描画実装が上書きするフック（`MarqueeRendererBase.DrawLabel`）の**既定は何もしない**ため、要素で描く UI はコードを書かなくて済みます。

### 層の責務の境界（選択矩形）

| 責務 | 所有者 | 内容 |
| --- | --- | --- |
| 設定と状態 | 本コア（値） / UI（保持） | 選択の設定は `MarqueeOptions`、装飾の設定は `MarqueeDecoOptions`（いずれも公開値）、選択矩形はセッションが持つ。UI はフレームワークのプロパティ機構で保持し、ファサードへ伝搬するだけ。**設定の変更に伴う再描画は UI が要求する**（本コアは設定の保持だけを行い、再描画は入力に伴う場合にだけ効果として要求する） |
| 判断・計算 | 本コア | 選択の状態遷移・スナップ・最小サイズ・論理矩形 → DIP 変換（選択は閉区間 +1）・ハンドル/マーカー/ラベルの可否と値・描画順 |
| 契約 | 本コア | `MarqueeDrawContent`（装飾・ハンドル・始点マーカー・サイズラベル・座標ラベル・層の順序）・`Marquee.CreateRequest` / `Marquee.ResolveContent`（公開のエンドポイント。`MarqueeSession` は内部契約） |
| 描画（図形） | 描画層（`MarqueeControl.UIBase` の `MarqueeRendererBase` / `MarqueeRenderer`） | 描画内容の各項目を描画ポート（`IEsCanvas`）へ写す（色は描画の設定 `MarqueePaint` から引く）。本コアは描画 API を持たず、**描画内容と層の順序（`MarqueeDrawContent.Layers`）をデータとして渡すだけ**である |
| 描画（文字） | UI（コントロール・ホスト） | サイズラベル・座標ラベルは **UI 層がテキスト要素を配置**する（描画層 `MarqueeControl.UIBase` の `MarqueeLabelPlan` が実寸から配置矩形を求める。色は `MarqueePaint` から渡す）。操作・情報ラベルは**ホストが自前で描く** |
| ホスト結線 | UI | プロパティ機構・DPI 追従・キャンバスのスケーリング・ブラシキャッシュ・テキストの実寸測定とフォント解決・要素の配置 |

UI 層に残る分岐は次の 4 種のみで、いずれも本コアへ移せません。

1. **空のコレクションのスキップ**（ブラシ生成を避ける性能ガード）
2. **描画内容の項目の有無**（値を持つ項目だけを描く＝描画の前提条件）
3. **フレームワーク固有の値の解決**（DPI の 0 以下フォールバック・色型変換）
4. **描画方式の差**（直接描画 / 中間バッファ・ブラシキャッシュ）

ハンドル・始点マーカーと装飾の描画は UI 非依存の描画層（`MarqueeControl.UIBase` の `MarqueeRenderer` / `MarqueeOuterRenderer` / `MarqueeInnerRenderer`）が行います（描画先は `Platform.Core` の `IEsCanvas`。本コアは描画 API を持たない）。描画層は本コアの描画内容（`MarqueeDrawContent`）と幾何（`SelectionGeometry.DirectionalHandles` 等）を参照します。
**文字（サイズラベル・カーソル座標ラベル）は UI 層がテキスト要素を配置**します（`Border` + `TextBlock`。ラベルは常に最上位）。文言・基準点・並び順は本コアが単一所有し、フォントサイズと色は描画層（`MarqueeControl.UIBase` の `MarqueePaint`）、実寸から配置矩形を求める写像は描画層（`MarqueeControl.UIBase` の `MarqueeLabelPlan`）が単一所有します。

## 依存関係

```text
Feature.MarqueeCore
    ├── Platform.Core（Es* 幾何型 / EsKey / ICoordTransform / ICursorService）
    └── Microsoft.Extensions.Logging.Abstractions（LoggerMessage ソース生成のみ）
```

**UI フレームワークへ依存しません。** カーソル操作は `ICursorService` 抽象にのみ依存し、未指定（`null`）の場合は何もしません。既定実装（Win32）の解決は UI 側の責務です。

## 主な API

```csharp
// 色は描画層（MarqueeControl.UIBase）の MarqueePaint が単一所有する（コアの MarqueeOptions は色を持たない）
var paint = MarqueePaint.Default with { OverlayColor = Color.FromArgb(160, 0, 0, 0) };  // 色は System.Drawing.Color（UI 非依存）で指定する
// コアは描画実装を持たない（描画は UI 層の都合であり、層の順序に従う手順は UIBase が単一所有する）
var marquee = new Marquee(effects, cursorService, options);  // effects は IMarqueeEffects（UI 層の実装）/ cursorService・options は null 可
marquee.Log = logger;                                        // null は NullLogger へ吸収される
marquee.SetCoordinateTransform(new CoordTransform(p => p, p => p, () => new EsSize(1920, 1080))); // ドメイン境界

// 入力の通知（UI 層から呼ぶ。効果の適用までファサードが行う）
marquee.Press(new EsPoint(100, 100), clickCount: 1);          // 押下（新規選択・ハンドル・確定/キャンセル）
marquee.Move(new EsPoint(400, 300));                          // 移動（ドラッグ・ホバー）
marquee.Release(new EsPoint(400, 300));                       // 解放（確定・キャンセル・クリック）
var handled = marquee.KeyDown(EsKey.Right, cursorPosition: null); // キー（nudge・Escape・Enter。処理済みの真偽を返す）
// ホイールは選択の判断ではないため本コアには無い（組み込み時に MarqueeControl.UIBase の MarqueeWheelInterpreter が解釈する ）

// 外部（ホスト）からの操作
marquee.Options = options;                                    // 設定の反映（対話パラメータ）
marquee.SetSelection(rect);                                   // 選択の復元
// UI 層が対話を終了する場合の後片付けも ResetSelection（対話状態の初期化）を使う
// ビューポートの変化の通知はコントロール化の層（MarqueeControl.UIBase の MarqueeViewportNotifier）が担う

// 描画（1 フレーム分の描画内容。描くかどうかの判断はコア）
// 描画入力の組み立て（セッションの状態の写し）はコアが単一所有する（UI は描画領域のサイズを渡すだけ）
var request = marquee.CreateRequest(clientSize);
var content = marquee.ResolveContent(request);                 // 値を持つ項目を自 UI の描画 API へ写すだけ
// 描画は UI 層が行う（層の順序に従う手順は MarqueeControl.UIBase の MarqueeRendererBase が単一所有する）
// テキストを UI 要素として置く場合は content.SizeLabelText / content.CoordLabelText を
// MarqueeControl.UIBase の MarqueeLabelPlan へ渡して配置を求める（canvas へ描く場合は
// 派生が基底のフック（MarqueeRendererBase.DrawLabel）を上書きする）

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
| `ResolveContent` | 1 フレーム分の描画内容の解決（描画領域のサイズを渡す。原点は常に 0 であり、親座標を混ぜるとハンドルがずれるため型で受ける）。テキストは `SizeLabelText` / `CoordLabelText` として値で返し、層（`MarqueeDrawLayer.Label`）はテキストがある場合のみ末尾に含める |
| `Options` / `DecoOptions` / `DomainToDipScaling` / `DisplayScaleScaling` / `SizeLabelOverrideProvider` / `Log` | 設定・環境値・文言プロバイダの読み書き（`Log` は null を `NullLogger` へ吸収する） |
| `DefaultScaling` | ポート未指定時に適用する倍率の既定値（`1.0`）。両 UI の依存関係プロパティの既定値も本値を参照する |
| `SelectionRect` / `HasSelection` / `CreateSnapshot` / `Notified` | 状態の読み取りと通知（描画・公開イベントへの転送用） |

UI 層が適用する効果（`MarqueeFeedback`）の適用順はコアが単一所有する（UI 層は `IMarqueeEffects` を実装して自 UI の操作へ接続するだけ）:

| メソッド | 適用内容・順序 |
| --- | --- |
| `ApplyCursor` | カーソル操作（移動 → 非表示 → 再表示）。画面座標の変換は `IMarqueeEffects.ToScreenPoint` を使う（効果の適用先はデリゲートではなく `IMarqueeEffects` を渡す） |
| `ApplyVisual` | フォーカス → カーソル形状 → 再描画（要求されたものだけ） |

UI 層が使う共有値・計算:

| メンバー | 用途 |
| --- | --- |
| `MarqueeOptions.HandleBorderThickness` | ハンドル枠の線の太さ（DIP）。`MarqueeHandles.BorderThickness` として描画層へ渡るため、描画層は定数を持たない |
| `MarqueeHandles`（`Rects`） | 8 方向のハンドル矩形（`Marquee.ResolveContent` が `SelectionGeometry` の位置と `MarqueeOptions.HandleSize` から解決済み） |
| `MarqueeOptions.CoordLabelOffset` | カーソルからラベルを離すオフセット（DIP。本コントロール固有の値） |
| `MarqueePaint.LabelPadding`（描画層 `MarqueeControl.UIBase`） | ラベルの内側余白の既定値（全ラベル共通。既定 4.0 DIP）。ホストが描く付加情報ラベルも同じ値を使う |
| `MarqueePaint`（同） | ラベルの体裁（フォントサイズ・色・内側余白）の単一所有者。行送りは「行の高さ + 内側余白 × 2」で求める（`FadeMaskHostLabelRenderer` が単一所有する） |
| `MarqueeLabelPlacement`（同） | UI 要素として配置するための指示（配置矩形・内側余白・文言・フォント・色）。UI 層はこれを置くだけでよい（`MarqueeLabelPlan` が組み立てる） |
| `MarqueePaint.Default`（描画層 `MarqueeControl.UIBase`） | 色の既定値（部分変更の基点。`MarqueePaint.Default with { ... }`。コアの設定は色を持たない） |
| `MarqueePaint.HandleColor`（同） | ハンドルの塗り色（未指定（全成分 0）は生成時に白へ解決。両 UI が同じ見た目になる） |
| `SelectionGeometry.DirectionalHandles` | 描画対象の 8 方向ハンドル（読み取り専用の一覧。列挙順は `HitTarget` の宣言順。**描画層へは解決済みの矩形が渡る**） |
| `SelectionGeometry.ToCursorKind` | ハンドル → `MarqueeCursorKind`（どのハンドルが同じカーソルになるか） |

## 設計上の約束

1. **判断はコア・反映は UI**: 「どうするか」（nudge の開始/終了・確定/キャンセル・スナップの有無・キャンセル可否・カーソル種別）はコアが決め、UI は効果を適用するだけにする。
2. **単一所有**: 幾何計算は `SelectionGeometry` のみ、状態は `MarqueeState` のみ、入力の解釈は `MarqueeSession` のみが持つ。
3. **座標系**: `EsRect` / `EsPoint` は常にドメイン座標。UI 論理座標（DIP）との変換は `ICoordTransform`（`Port`）が担う。
4. **境界 1 行変換**: 各 UI のイベント引数は自前の型（例: Avalonia の `Rect`）で公開し、境界で 1 行変換する。キーは `EsKey` を介して通知する。
5. **カーソル操作の分離**: カーソル操作（非表示・再表示・移動）は**効果**として UI 層へ要求し、公開ファサードだけが `ICursorService` 抽象に依存する（プラットフォーム既定実装を生成しない）。状態機械はカーソル API を知らない。カーソル種別は `HitTarget` として返し、UI が自 UI のカーソル型へ写像する。
6. **Enter と Return は同一**: Avalonia の `Key` は両者が同値のエイリアスで、名前ベースの相互変換では `Return` が得られるため、双方を確定キーとして扱う。
7. **両 UI で共通の値はコアが単一所有**: 描画対象ハンドル一覧・カーソル形状の種別・**設定の既定値と範囲補正（`MarqueeOptions`）**は、Avalonia 版と MewUI 版で同じ結果になる必要があるためコアが持つ（片方だけを直すと見た目や既定値がずれる）。**描画にだけ必要な体裁（色・フォントサイズ・内側余白）は描画層（`MarqueePaint`）が単一所有する**。UI 層が持つのは自 UI の描画・カーソル型への写像・座標変換のみ。
8. **設定の適用はコアが判断する**: 設定（`MarqueeOptions`）の反映は `ApplyOptions` が担い、対話パラメータの更新・境界外選択の禁止の変化の検出・変更ログ・即時クリップを 1 箇所に集約する（UI 層は「変更通知を渡す」だけになる）。
9. **効果の適用順もコアが持つ**: 「どの効果をどの順で適用するか」（カーソル操作の順序・フォーカス → カーソル形状 → 再描画）は `MarqueeFeedback` が単一所有し、UI 層は自 UI の操作（`Focus` / `InvalidateVisual` / カーソル型への写像）へ接続するだけでよい。**カーソルの非表示・再表示も効果として要求する**（状態機械はカーソル API を知らない。適用の順序を 1 経路に集約し、非表示の残留を防ぐ）。
10. **持続資源の保持もコア**: ロガーは `Marquee.Log` が保持し、null を `NullLogger` へ吸収する（UI は get/set を委譲するだけ）。入力の判断のログも同じロガーで出力する。
11. **描画順は本コアが単一所有する**: `MarqueeDrawLayer`（`Decoration` → `Handles` → `Marker` → `Label`）で層と順序を指示し、UI 層はその順に処理するだけにする。**テキストの層を必ず最後に置く**（テキストの有無は `HasText` で決まる）ため、テキストは常に最上位に表示される。**UI 要素として置く UI は本層を無視してよい**（UIBase の基底のフック `MarqueeRendererBase.DrawLabel` の既定は何もしない）。
12. **選択の状態はセッションが単一のソース**: 選択矩形と選択の有無は設定値ではなくセッションの状態から `Marquee.ResolveContent` が導出し、装飾層も同じ値を参照する（同じデータを 2 箇所に持たない）。
13. **描画内容の契約は本コアが単一所有する**: `Marquee.ResolveContent` が「装飾（`OuterDecoFrame` / `InnerDecoFrame`）・穴あけ矩形・ハンドル（`MarqueeHandles`）・始点マーカー（`MarqueeMarker`）・サイズラベル（`MarqueeTextPlacement`）・カーソル座標ラベル（`MarqueeTextPlacement`）・層の順序」を 1 つの `MarqueeDrawContent` にまとめて返し、**描くかどうかの判断（非表示の設定・選択なし・nudge 中・装飾の描画可否フラグ・穴あけ矩形なし）をすべてコアが下す**（公開は `Marquee.CreateRequest` / `Marquee.ResolveContent` と描画の契約型。組み立てに使う `MarqueeSession` は internal。契約型は描画に必要な値だけを持つ純粋データであり、判断を持たない）。**穴あけ矩形と座標変換ポートは 1 フレームに 1 回だけ解決する**（同じ矩形を各所が個別に変換するとずれるため）。
14. **文字は Core が文言と基準点を決め、UI 層が要素を配置する**: サイズラベル（`MarqueeTextPlacement`）とカーソル座標ラベル（`MarqueeTextPlacement`）の**文言・基準点・アラインの計算は本コアが単一所有**し、**実寸測定と要素への反映は UI 層**が担う（実寸は描画 API でしか測れないため）。体裁（背景 = 実寸 + 余白×2、アライン、クランプ）は描画層（`MarqueeControl.UIBase` の `MarqueePaint` / `MarqueeLabelPlan`）が単一所有し、UI 層は `Border` + `TextBlock` を子として置く。**操作・情報ラベルはアプリ固有の付加情報**であるため、本コアは関知せずホストが自前で描く。
15. **色は本コアが持たない**: ハンドル・始点マーカー・ラベル・装飾の色は**描画にだけ必要な設定**であるため、描画層（`MarqueeControl.UIBase` の `MarqueePaint`）が単一所有する（本コアの設定と描画内容（`MarqueeDrawContent`）には色を含めない）。両 UI が同じ見た目になるよう、既定値は `MarqueePaint.Default` が単一所有し、ハンドル枠と穴あけ枠は同じ既定値（`MarqueePaint.DefaultHandleBorderColor` = `#FFA9A9A9`）を共有する。
16. **ハンドル・マーカー・座標ラベルの可否もコアが決める**: 「ハンドルを描くか（`ShowHandles` と選択の有無）」「マーカーを描くか（nudge 中で十字線が可視か）」「座標ラベルを描くか（`ShowCoordLabel`）」の判断は `Marquee.ResolveContent` が行う。UI 層は値を持つ項目を描くだけでよい（判断の分散を防ぐ）。
17. **操作の入口はファサードのみ**: UI 層とホストは `Marquee` だけを参照し、状態機械（`MarqueeSession`）には触れない（`internal` で強制する）。設定の伝搬・効果の適用・通知の転送・描画内容の解決も本型が単一所有し、UI 層は自 UI の公開プロパティ・公開イベント・描画 API へつなぐだけになる。
18. **選択の判断ではない規則はコアに置かない**: 「Ctrl+ホイール = ズーム」・「ビューポートの変化の再通知」のようなビューポート連携の規則は、単体で使う Marquee には存在しない概念であるため `MarqueeControl.UIBase` が単一所有する（コアは将来そのまま別プロジェクトへ独立できる）。
19. **他のコントロールの語彙を持たない**: コアは Marquee 単体の概念だけを持ち、別のコントロール（ルーペ等）との連携名を持ち込まない。追従位置は汎用名（`MarqueeNoticeKind.MarkerPositionMoved`）で通知し、ルーペ中心への写像は UI 層の公開イベントが担う（コアは将来そのまま別プロジェクトへ独立できる）。

> **Note**: 本 README は**利用仕様書**（使い方と設計の意図）です。公開 API の仕様は [SPEC_ExtFunc.md](SPEC_ExtFunc.md)、構造・依存・例外処理・単一所有の一覧は [SPEC_System.md](SPEC_System.md) を参照してください。

## テスト

```text
tests/
├── Marquee.Tests.cs                 # 公開ファサード（操作・効果の適用・設定の伝搬・描画内容の解決・通知の転送・確保量の固定）（36 件）
├── Marquee.DrawContent.Tests.cs     # 描画内容の解決（装飾・穴あけ矩形・ハンドル・マーカー・ラベル・描画可否・層の順序）（50 件）
├── Marquee.Limits.Tests.cs          # 数値パラメータの既定値・未設定時の解決規則・安全域への補正（18 件）
├── Contract/                        # 効果の基底クラス（全メンバーが空実装であること・派生で必要なものだけを override できること）（3 件）
├── Decoration/                      # 装飾（暗転マスク・枠線・内側の塗り・外観の安全域への補正）（24 件）
├── Drawing/                         # フレーム全体の幾何（座標変換ポート・DIP 変換・穴あけ矩形）（18 件）
├── Interaction/                     # 状態遷移・ヒットテスト・ドラッグ・nudge・入力の解釈・効果の適用（87 件）
├── Logging/                         # ログ定義の生成コード（IsEnabled 両パス・書式）（3 件）
├── Selection/                       # 純粋幾何（正規化・ハンドル位置・近傍判定・境界クリップ・カーソル種別）（21 件）
└── Setting/                         # 既定値・部分変更・値補正・等価性（8 件）
```

**テストはソースのフォルダを鏡映します**（`src/Drawing/` の型は `tests/Drawing/` で検証する）。同じファサード型の partial（`Marquee.cs` / `Marquee.DrawContent.cs`）のテストはルートに置きます。

件数は `[Theory]` を展開したケース数（合計 268 件）です。

```powershell
dotnet test src/Feature.MarqueeCore/tests/Feature.MarqueeCore.Tests.csproj
```

## ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
