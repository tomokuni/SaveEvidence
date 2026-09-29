# Feature.Marquee.Core

`MarqueeControl`（マーキー選択＝ドラッグで矩形を選び、移動・リサイズできるコントロール）の**UI 非依存の計算層**です。Avalonia 版と MewUI 版が同じ状態管理・判断・幾何を共有します。

## 目的

- 選択矩形の幾何計算・状態機械・**入力の解釈（判断）**を 1 箇所に集約し、UI ごとの実装差から切り離す。
- **操作の唯一の窓口は公開ファサード `Marquee`**。UI 層とホストは本型だけを参照する（状態機械と幾何の導出は内部契約として隠す）。
- **描画は本コアの関心事ではない**。本コアは描画 API も描画ポートも持たない（依存先は `Platform.Core` だけである）。返すのは**幾何**（穴あけ矩形・ハンドル・始点マーカー。`MarqueeGeometry`）だけで、色・フォント・枠線の太さ・表示可否・文言・描画順は提示層（`Feature.Marquee.Presenter`）が単一所有する。
- UI 層は「入力イベントの受付」「効果の適用」「通知の転送」「描画」だけを担う薄い層にし、判断を持たない。
- UI 非依存のためヘッドレス実行が不要で、`[Fact]` で並列実行できる。
- 座標は `EsPoint` / `EsRect` / `EsSize`（`EsUtil.Platform.Core.Geometry`）で扱う。

## 収録する型

ルート（`src/`）に置くのは**ファサード本体とその partial**（`Marquee.cs` / `Marquee.Geometry.cs` / `Marquee.Limits.cs`）だけです。それ以外は用途別のフォルダへまとめます。
**1 ファイル 1 型**とします（複数の型を 1 ファイルへ含めない。`partial` による分割は可）。
**名前空間はフォルダに追従**させます（ルート直下のみ `EsUtil.Feature.Marquee.Core`。`Contract` / `Drawing` / `Interaction` / `Setting` / `Logging` / `Selection` は `EsUtil.Feature.Marquee.Core.<フォルダ名>`）。公開面も複数のフォルダに分かれるため、利用者は用途に応じて `using` を追加します。

| フォルダ | 型 | 可視性 | 責務 |
| --- | --- | --- | --- |
| `Interaction/` | `MarqueeHitTarget` | public | ヒット対象の種別（8 方向のハンドル + `Move`。`None` は対象なし） |
| `Interaction/` | `MarqueeInteractionState` | public | 対話状態（NoSelection / StartPointNudging / Dragging / DraggingNudging / Selected / SelectedNudging） |
| `Interaction/` | `MarqueeSnapshot` | public | 通知時点の状態の値型（`readonly record struct`。`selectionRect` は `EsRect`、`mousePosition` は `EsPoint`。操作中の通知のたびにヒープを確保しない） |
| `Setting/` | `MarqueeOptions` | public | 設定の値オブジェクト（**計算に必要な値のみ**＝寸法・対話パラメータ）。既定値と安全域への補正を単一所有する（補正の規則は `Marquee.Limits` が持ち、生成時の `init` アクセサが適用する）。**色・フォントサイズ・枠線の太さ・表示可否は持たない**（これらは描画にだけ必要な設定であり、提示層 `Feature.Marquee.Presenter` の `MarqueeAppearance` が単一所有する） |
| ルート | `Marquee` | public | **公開ファサード（操作の唯一の窓口）**。設定・環境値の保持、入力の通知、外部からの操作、幾何の解決、効果の適用を行う |
| ルート | `Marquee.Limits`（`Marquee` の入れ子） | public | 数値パラメータの既定値・安全域と範囲外の補正、および**未設定（0 以下）の解決規則**（`ResolveDisplayScale`。表示スケールが未指定のときに倍率を使う規則。描画層のサイズラベル書式が使うため public）。ファサード以外のコア型を直接参照しないためのスキーマの窓口 |
| `Contract/` | `IMarqueeEffects` | public | コアが要求する効果（フォーカス・再描画・カーソル形状・ポインターキャプチャ・座標変換）の適用先。UI 層が自 UI の操作へ接続する唯一の境界 |
| `Contract/` | `MarqueeEffectsBase` | public abstract | `IMarqueeEffects` の全メンバーを virtual の空実装で提供する基底クラス。独自コントロールは必要な効果だけ override する（不要なメンバーの空実装を書かずに済む） |
| ルート | `Marquee.Geometry.cs`（`Marquee` の **partial**） | internal | 幾何の**内部手順**（穴あけ矩形 → DIP 変換・ハンドル位置・始点マーカーの解決）。公開の入口（`ResolveGeometry`）はファサード本体（`Marquee.cs`）が持つ。穴あけ矩形と座標変換ポートは 1 フレームに 1 回だけ解決する |
| `Drawing/` | `MarqueeGeometry` | public | 1 フレーム分の**幾何**（`ClientSize` / `HoleRect` / `HoleDomainRect` / `Handles` / `Marker` / `MousePosition` ＋ `SurfaceBounds`）。**コアが責任を持つ値の全量**であり、描画層はこれと外観だけから描画計画を組み立てる |
| `Drawing/` | `MarqueeHandles` | public | ドラッグハンドルの幾何（8 方向の矩形と枠線の太さ。位置はコアが解決済み） |
| `Drawing/` | `MarqueeMarker` | public | 始点マーカー（十字線）の幾何（中心・太さ・半幅） |
| `Drawing/` | `MarqueeRenderRequest` | public | 1 フレーム分の幾何を解決する入力（設定・状態と、描画時に決まる値）。UI 層は自分で組み立てず、コアの `Marquee.CreateRequest` が返した値を受け取る（内部状態の組み立て順を UI が持たない）。**本値から導出する値（座標変換ポートの決定・穴あけ矩形の決定・DIP 変換）は本値のインスタンスメンバーが単一所有する**（`ResolvedPort` / `Hole` / `ResolveHoleRectDip` / `ToDipRect` / `ToDomainRect`）。ハイライト矩形はホストが与える実行時状態として本値が持つ |
| `Drawing/` | `MarqueeHoleRect` | public | 穴あけ矩形と**閉区間かどうか**の組（選択矩形は閉区間（終点 +1）、ハイライト矩形はホストが全ピクセルを含む閉区間として与えるため +1 しない） |
| `Selection/` | `MarqueeSelectionGeometry` | internal | 純粋な幾何計算（正規化・ハンドル位置・近傍判定・対角・境界クリップ・ドラッグ変形）・描画対象ハンドル一覧・カーソル種別変換。利用者はコアの内部（幾何の解決・状態機械）だけであり、UI 層へは解決済みの矩形が渡る |
| `Interaction/` | `MarqueeSession` | internal | **状態管理クラス**。入力の通知を受けて状態を更新し、効果（`MarqueeFeedback`）と通知（`MarqueeNotice`）を返す。公開ファサード `Marquee` の内部実装 |
| `Interaction/` | `MarqueeNotice` | public | コアから UI 層への通知（種別・通知座標・スナップショット。値型）。**生のポインター入力（押下・解放・クリック・移動）は通知しない**（入力は UI が受け取りホストも自 UI で観測できるため、通知すると往復になる。押下状態は `MarqueeSnapshot.IsMouseDown` で取得できる） |
| `Interaction/` | `MarqueeNoticeKind` | public | 通知の種別（選択の開始・変更・確定・キャンセル・追従位置・確定/キャンセル要求） |
| `Interaction/` | `MarqueeFeedback` | internal | コアから UI 層への効果（再描画・フォーカス・マウスキャプチャ・カーソル操作・カーソル形状）。適用順は `Marquee` が単一所有し、UI 層は値を受け取らない（ファサードへ入力を通知するだけ） |
| `Interaction/` | `MarqueeCursorAction` | internal | 要求するカーソル操作のフラグ（移動・非表示・再表示） |
| `Interaction/` | `MarqueeCaptureAction` | internal | 要求するポインターキャプチャの操作（取得・解放） |
| `Interaction/` | `MarqueeState` | internal | 選択矩形・ドラッグ・nudge の状態保持と更新ロジック（純粋計算の適用）。可変プロパティを多数持つため、外部には公開しない |
| `Interaction/` | `MarqueeCursorKind` | public | 操作位置に対応するカーソル形状の種別（UI は自 UI のカーソル型へ写像するだけ） |
| `Logging/` | `MarqueeLogMessage` | internal | ログの LoggerMessage 定義（入力の判断・設定変更）。呼ぶのはコア（状態機械・ファサード）だけであり、UI 層はロガー（`Marquee.Log`）を渡すだけである（自 UI 固有の操作ログは UI 層が持つ。ズーム要求のログは `Feature.Marquee.Presenter` が単一所有する） |

## 公開面の階層

公開面は「誰が何のために使うか」で 3 段に分かれます。**外部が使わない内部契約は `internal`** とし、単体テストにのみ `InternalsVisibleTo` で公開します。

| 層 | 利用者 | 型 |
| --- | --- | --- |
| **1. ホスト設定** | アプリ・ホスト | `MarqueeOptions` / `MarqueeHitTarget` / `MarqueeInteractionState` / `MarqueeSnapshot` |
| **2. 公開ファサードと UI アダプタ契約** | UI 実装（入力層） | `Marquee`（操作の唯一の窓口。既定値は `Limits.DomainToDipScalingDefault`） / `IMarqueeEffects` / `MarqueeEffectsBase` / `MarqueeCursorKind` / `MarqueeNotice` / `MarqueeNoticeKind` / `MarqueeGeometry` / `MarqueeHandles` / `MarqueeMarker` / `MarqueeHoleRect` / `MarqueeRenderRequest` |
| **3. 内部ヘルパー** | 本コアのみ（テストのみ IVT） | `MarqueeFeedback` / `MarqueeCursorAction` / `MarqueeCaptureAction` / `MarqueeLogMessage`（適用とログ出力はファサード・状態機械が行う）/ `MarqueeSession` / `MarqueeSelectionGeometry` / `Marquee.Geometry.cs`（「1 フレーム分の幾何の解決」は `Marquee.ResolveGeometry` が提供する）/ `MarqueeState` |

- **操作はファサード経由に一本化**しています（状態機械と幾何の解決は `internal` のため、UI 層はファサード以外の入口を持ちません）。
- **層 3 は外部参照 0 件**を実測して絞り込んだ結果です。「幾何の解決（`Marquee.ResolveGeometry`）と幾何に必要な値」だけが公開面です。
- **装飾（暗転マスク・穴あけ枠・ハイライト・内側の塗り）は本コアに置きません**。装飾は選択矩形コントロール単体の概念ではなく、コントロールを撮影オーバーレイ等へ組み込むときにだけ必要になる表示だからです。装飾の矩形・文言・表示可否は提示層（`Feature.Marquee.Presenter`）が単一所有し、本コアは**穴あけ矩形（幾何）だけ**を返します。
- **層 2 は UI アダプタを自作する場合の契約**です（独自 UI を追加するには、この面だけで完結します。`IMarqueeEffects` は `MarqueeEffectsBase` を継承すれば必要な効果だけを実装できます）。
- **層 1 は設定と受け渡しの値**です（ファサードを持たず、設定値と幾何だけを使う軽い用途でも使えます）。
- `[EditorBrowsable]` による「見えにくくする」対処はしません。公開面も契約も変わらず（見つけにくくなるだけ）、手段と目的が食い違うためです。**隠すか公開するかの二択**とし、隠す場合は `internal` で除きます。

**UI 非依存の汎用ライブラリ**として、特定のアプリ・UI 実装に依存しません（文言・モード等のアプリ固有情報を持ちません）。

名前空間はフォルダに追従します（ルート直下は `EsUtil.Feature.Marquee.Core`。`Contract` / `Drawing` / `Interaction` / `Setting` / `Logging` / `Selection` は `EsUtil.Feature.Marquee.Core.<フォルダ名>`）。アセンブリ名は `Feature.Marquee.Core`。

## 依存方向

層の全体像（Core / Presenter / UI）は [`src/README.md`](../README.md) を参照してください。

```text
UI 層（MarqueeControl.AvaloniaUI / MarqueeControl.MewUI）
  入力の受付・効果の適用・通知の転送・描画先の用意・ラベル要素の配置
    ├──> Feature.Marquee.Presenter   どう描くか（描画計画の解決と自 UI の描画面への写し）
    └──> Feature.Marquee.Core ← 本プロジェクト   何を計算するか（状態・判断・幾何）
           └──> Platform.Core（Es* 値型・ICoordTransform・ICursorService）
※ Core は Presenter を参照しない（Presenter がコアの幾何を使う向きだけ）。
```

本プロジェクトが UI フレームワークへ依存することはありません。**直接参照するものは UI 非依存のコア型と抽象（`Platform.Core` の `Es*` 幾何型 / `EsKey` / `ICoordTransform` / `ICursorService`）と、`Microsoft.Extensions.Logging.Abstractions`（`LoggerMessage` ソース生成）だけ**です。**カーソル操作は `ICursorService` 抽象にのみ依存**し、未指定（`null`）の場合は何もしません（既定実装（Win32）の解決は UI 側の責務です）。

## 層の責務の境界

| 責務 | 所有者 | 内容 |
| --- | --- | --- |
| 設定と状態 | 本コア（値） / UI（保持） | 計算に必要な設定は `MarqueeOptions`（公開値）、選択矩形はセッションが持つ。UI はフレームワークのプロパティ機構で保持し、ファサードへ伝搬するだけ。**設定の変更に伴う再描画は UI が要求する**（本コアは設定の保持だけを行い、再描画は入力に伴う場合にだけ効果として要求する） |
| 判断・計算 | 本コア | 選択の状態遷移・スナップ・最小サイズ・論理矩形 → DIP 変換（選択は閉区間 +1）・穴あけ矩形の優先順（ハイライト → 選択）・ハンドル / マーカーの位置 |
| 契約（幾何） | 本コア | `MarqueeGeometry`（穴あけ矩形・ハンドル・始点マーカー・マウス位置）・`Marquee.CreateRequest` / `Marquee.ResolveGeometry`（公開のエンドポイント。`MarqueeSession` は内部契約） |
| 描画計画（外観・文言・描画順・装飾の矩形） | 提示層（`Feature.Marquee.Presenter`） | 幾何と外観（`MarqueeAppearance`）から `MarqueeDrawPlan` を組み立てる。**本コアは描画計画を知らない** |
| 描画（図形） | UI（各 UI の描画クラス） | 描画計画の各項目を自 UI の描画 API へ写す（`MarqueeContextPainter` / `MarqueeSkiaPainter`）。本コアは描画 API を持たず、**幾何をデータとして渡すだけ**である |
| 描画（文字） | UI（コントロール・ホスト） | サイズラベル・座標ラベルは **UI 層がテキスト要素を配置**する（提示層の `MarqueeLabelPlan` が実寸から配置矩形を求める。色は `MarqueeAppearance` から渡す）。操作・情報ラベルは**ホストが自前で描く** |
| ホスト結線 | UI | プロパティ機構・DPI 追従・キャンバスのスケーリング・ブラシキャッシュ・テキストの実寸測定とフォント解決・要素の配置 |

本コアが返す幾何は「描く値の無い項目は null / 空」であり、**描くかどうかの判断は本コア（穴あけ矩形の有無・選択の有無・nudge 中か）と外観（表示可否のフラグ）が分担**します。UI 層に残る分岐は次の 4 種のみで、いずれも本コアへ移せません。

1. **空のコレクションのスキップ**（ブラシ生成を避ける性能ガード）
2. **描画計画の項目の有無**（値を持つ項目だけを描く＝描画の前提条件）
3. **フレームワーク固有の値の解決**（DPI の 0 以下フォールバック・色型変換）
4. **描画方式の差**（直接描画 / 中間バッファ・ブラシキャッシュ）

**文字（サイズラベル・カーソル座標ラベル）は UI 層がテキスト要素を配置**します（`Border` + `TextBlock`。ラベルは常に最上位）。文言・基準点・並び順は提示層（`Feature.Marquee.Presenter` の `MarqueeLabelTexts` / `MarqueePresenter`）が単一所有し、フォントサイズと色は外観（`MarqueeAppearance`）、実寸から配置矩形を求める写像は `MarqueeLabelPlan` が単一所有します。

## 機能差に由来する非対称（もう一方のコアとの違い）

本コアと `Feature.Loupe.Core` は同じ規約（`UI → Presenter → Core → Platform.Core` の一方向依存、幾何はコアが・外観と描画計画は提示層が所有）で作られています。揃えない（揃えると書になる）違いは機能差に由来するもので、以下に列挙します。

| 非対称 | Marquee | Loupe | 理由 |
| --- | --- | --- | --- |
| 描画パイプラインの段数 | 2 段（`CreateRequest` → `ResolveGeometry`） | 3 段（`ResolveLayout` → `ResolveGeometry`） | 本コアは親セル全面へ重ねるため配置の余地がない（`LoupeLayout` 相当を持たない）。配置先はホストの親セル全面であり、位置を決める入力がコアに無い |
| 幾何の解決の入力 | `CreateRequest(EsSize clientSize)`（描画領域＝座標空間。表示サイズと同じ） | `ResolveGeometry(double displaySize, EsSize? availableSize)`（一辺の表示サイズ＝親パネルのクランプ結果と、描画面＝ビットマップの領域が別） | 本コアは任意矩形の描画領域へ直接描く（正方形の表示サイズと描画面の区別が無い） |
| 入力の通知と効果ポート | 対話モジュール（`Interaction/` + `Contract/` + `Press` / `Move` / `Release` / `KeyDown` + `Notified`）／設定に対話パラメータ（`MinSelectionSize` / `HandleHitRadius` / `ClampToDomainBounds`）／**コアが再描画を要求**（`RequestRedraw`）／ロガーはセッションが保持 | 持たない（状態はホストが設定するだけ。設定は表示のみ。変換の設定は再描画を要求せず、表示位置の再計算は UI が行う。ロガーはファサード自身が保持） | ルーペは入力の解釈も効果ポートも持たない（ホストが状態を設定するだけであり、再描画の要求先が無い） |
| ラベル文言プロバイダ | 提示層が持つ（`MarqueePresenter.SizeLabelOverrideProvider` / `SizeLabelDisplayScalingProvider`） | 持たない（文言は提示層の `LoupeLabelTexts` が常に生成する） | 本コアのサイズラベルはホストが文言を差し替えたくなる（自動切り出しの候補サイズ等） |
| 寸法の型 | `double`（DIP のまま。線を描く） | `int`（物理ピクセルへ換算。塗る） | 描画プリミティブが違う（線 / 塗り） |
| 設定型の数 | 1 型（`MarqueeOptions`） | 1 型（`LoupeOptions`） | 装飾の設定をコアから提示層（外観）へ移したため、両コアとも 1 型 |
| 幾何の粒度 | 層ごとに専用型（`MarqueeHandles` / `MarqueeMarker`） | 層ごとに素の矩形（`BorderRects` / `CrosshairRects`） | 描画プリミティブが違う（線の座標 / 塗る矩形の列） |
| 幾何の解決の入力の座標変換ポート | 入力が保持する（`Port` ＋ 未指定時の `DomainToDipScaling`。`ResolvedPort` / `ToDipRect` が自分で解決する） | 入力に持たない（ポートは表示位置の解決だけに使う） | 本コアは選択矩形をドメイン → DIP へ変換して描き、その DIP はホストの座標系で決まる（ルーペは自身の物理ピクセル面で完結する） |
| 下地の層 | 持たない | 提示層が描画面全体をペーストボード色で塗る | 本コアは自身の表面へ直接描き、消去はフレームワークが行う（ルーペは描画先のビットマップを自分で置き換える） |
| 描画完了の通知の内容 | `MarqueeRenderedEventArgs`（描画計画: 層・穴あけ矩形・文言） | `LoupeRenderedEventArgs`（`CaptureTopLeft` / `ZoomLevel`） | ホストが重ね描きするために必要な情報が違う（選択矩形の所在 / 切り出し領域と倍率） |
| 表示可否の述語の所在 | 提示層の外観（`MarqueeAppearance.ShouldShowSizeLabel(bool)` / `ShouldDrawOverlay(bool)`） | 提示層の外観（`LoupeAppearance.ShouldShowCoordLabel(bool)`） | 表示可否は描画にだけ必要な設定であり、両コアとも外観が単一所有する |
| `Limits` の追加役割 | 未設定（0 以下）の解決規則も持つ（`ResolveDisplayScale`。public） | 安全域と補正のみ | ラベルの表示スケールは本コア固有の概念 |
| 区画数の計算 | 持たない | `LoupeGeometry.CaptureSize`（導出プロパティ）が持つ | 切り出し領域の一辺と十字線の交点が「余りを 1 区画として数える」規則を共用する |
| 選択の幾何 | `Selection/`（`MarqueeSelectionGeometry`）を持つ | 持たない | ルーペは選択操作を持たない |
| テスト補助（スパイ） | `EffectsSpy` / `CursorServiceSpy` / `RecordingLogger` | `RecordingLogger` のみ | ルーペは効果ポートもカーソルサービスも持たないため差し替える対象が無い（ログの検証だけが共通） |
| ドキュメントの追加の節 | 2 節（README「名前の由来と一般的な呼称」「選択矩形の幾何はコアが持つ」、SPEC_System「状態遷移」） | 追加の節なし | 改名の経緯・幾何の所在・状態遷移はルーペに対応する概念が無い（共通の節は両コアで同一順） |

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
| `DragHandle`（`None` / `Move` + 8 方向） | `HitTarget` → `MarqueeHitTarget` | ハンドル以外（`None` / `Move`）も含むためヒット対象を表す名前にし、公開型の接頭辞（自コア名）を付ける |
| `MarqueeSnapshot.ActiveHandle` / `MarqueeState.ActiveHandle` | `ActiveTarget` | 同上 |
| `DragRectZoomRequest` / `DragRectScrollRequest` | 削除 | コントロール化を前提とした処理（ホイール操作）のため要求型を持たなくなった（ホイールはホスト（UI 層）がビューポートへ転送する） |

## 選択矩形の幾何はコアが持つ（装飾は持たない）

**穴あけ矩形（選択矩形・ハイライト矩形）の解決は本コアが単一所有**します。閉区間（選択は終点 +1、ハイライトは +0）と座標変換の規則を選択のドメインと同じ規則で解く必要があり、分離すると**暗転領域・枠線・内側の塗り・サイズラベルが別々の矩形を使ってずれる**ためです。

一方、**装飾の外観（暗転マスク・穴あけ枠・内側の塗り）・文言・表示可否・描画順は本コアに置きません**。これらは選択矩形コントロール単体の概念ではなく、コントロールを撮影オーバーレイ等へ組み込むときにだけ必要になる表示だからです（提示層 `Feature.Marquee.Presenter` が単一所有します）。

| 項目 | 所有者 | 内容 |
| --- | --- | --- |
| 穴あけ矩形（選択・ハイライトの優先順と閉区間） | 本コア（`MarqueeRenderRequest.ResolveHoleRect` / `Hole` / `ResolveHoleRectDip`） | ハイライト矩形 → 選択矩形の優先順、選択は閉区間（終点 +1）、ハイライトは +0（ホストが全ピクセルを含む矩形として与える） |
| ハンドルの位置と大きさ | 本コア（`Marquee.Geometry.cs` / `MarqueeHandles`） | 8 方向の矩形（選択領域の閉区間の左上を中心に置く）と枠線の太さ（固定値） |
| 始点マーカー | 本コア（`Marquee.Geometry.cs` / `MarqueeMarker`） | 中心・太さ・半幅（nudge 中のみ） |
| カーソル座標ラベルの基準（マウス位置） | 本コア（`MarqueeGeometry.MousePosition`） | ドメイン座標（文言 `EsPoint.Format`・基準点（カーソル + 8.0 DIP）は提示層が作る） |
| 表示スケールの未設定の解決 | 本コア（`Marquee.Limits.ResolveDisplayScale`） | 0 以下のとき倍率を使う規則（描画層のサイズラベル書式が使うため public） |
| 装飾の外観・文言・表示可否・描画順 | 提示層（`Feature.Marquee.Presenter` の `MarqueeAppearance` / `MarqueeDrawPlan` / `MarqueePresenter`） | 色・フォントサイズ・内側余白・枠線の太さ・表示可否・ラベルの文言と配置・層の順序 |
| 操作ラベル・情報ラベル | **ホスト**（アプリ固有の付加情報） | 文言の生成と描画。コアも提示層も関知しない |
| 選択矩形・選択の有無のソース | 本コア（`Marquee` のセッション経由） | 提示層・UI 層は参照のみ（複製しない） |

## 主な API

```csharp
// 色・フォント・枠線の太さ・表示可否は提示層（Feature.Marquee.Presenter）の MarqueeAppearance が単一所有する
// （本コアの MarqueeOptions は計算に必要な寸法・対話パラメータだけを持つ）
var marquee = new Marquee(effects, cursorService, options);  // effects は IMarqueeEffects（UI 層の実装）/ cursorService・options は null 可
marquee.Log = logger;                                        // null は NullLogger へ吸収される
marquee.SetCoordTransform(new CoordTransform(p => p, p => p, () => new EsSize(1920, 1080))); // ドメイン境界

// 入力の通知（UI 層から呼ぶ。効果の適用までファサードが行う）
marquee.Press(new EsPoint(100, 100), clickCount: 1);          // 押下（新規選択・ハンドル・確定/キャンセル）
marquee.Move(new EsPoint(400, 300));                          // 移動（ドラッグ・ホバー）
marquee.Release(new EsPoint(400, 300));                       // 解放（確定・キャンセル・クリック）
var handled = marquee.KeyDown(EsKey.Right, cursorPosition: null); // キー（nudge・Escape・Enter。処理済みの真偽を返す）
// ホイールは選択の判断ではないため本コアには無い（コントロール化を前提とした処理であり、ホスト（UI 層）がビューポートへ転送する）

// 外部（ホスト）からの操作
marquee.Options = options;                                    // 設定の反映（寸法・対話パラメータ）
marquee.HighlightRect = highlightRect;                        // ハイライト矩形（ホストが与える実行時状態。選択矩形より優先される）
marquee.SetSelection(rect);                                   // 選択の復元
// UI 層が対話を終了する場合の後片付けも ResetSelection（対話状態の初期化）を使う
// 表示（パン・ズーム）の変化への追従もコントロール化を前提とした処理であり、ホストが再描画を要求する

// 幾何（1 フレーム分。コアは穴あけ矩形・ハンドル・マーカーまでを返す）
// 幾何の入力の組み立て（セッションの状態の写し）はコアが単一所有する（UI は描画領域のサイズを渡すだけ）
var request = marquee.CreateRequest(clientSize);
var geometry = marquee.ResolveGeometry(request);              // geometry.HoleRect / Handles / Marker / MousePosition
// 装飾の矩形・文言・描画順は提示層（Feature.Marquee.Presenter の MarqueePresenter.ResolveDrawPlan）が解決する

// 通知（公開イベントへ転送する）
marquee.Notified += (_, notice) => Raise(notice);
```

| メソッド | 用途 |
| --- | --- |
| `Press` / `Move` / `Release` | ポインター入力の通知（ドメイン座標）。ダブルクリックの確定/キャンセル・スナップ・クリック検出を含む |
| `KeyDown` | キー入力の通知（`EsKey`）。Escape / Enter（`Return` も同一）と矢印キーの nudge。処理済みの真偽を返す |
| `EndNudge` | nudge の終了（フォーカス喪失で使用する） |
| `SetSelection` / `ResetSelection` / `RequestConfirm` / `RequestCancel` | ホスト・プロパティ変更からの操作（`ResetSelection` は UI 層が対話を終了する際の後片付けにも使う） |
| `Options`（設定） | 設定（`MarqueeOptions`）の反映。対話パラメータを更新し、**変化した項目をログ出力**し、境界外選択の禁止が変化したときに即時クリップを行う（プロパティの代入で指定する）。環境値（`DomainToDipScaling`）も変更した項目をログ出力する |
| `HighlightRect` | ハイライト矩形（ホストが与える実行時状態）。変更は**有無の変化だけ**をログ出力する（操作中に高頻度で変化しうるため） |
| `ToDomain` / `ToDip` / `SetCoordTransform` | 座標変換（ドメイン ⇔ ローカル DIP。選択矩形は閉区間として変換） |
| `CreateRequest` / `ResolveGeometry` | 1 フレーム分の幾何の解決（描画領域のサイズを渡す。原点は常に 0 であり、親座標を混ぜるとハンドルがずれるため型で受ける）。**本コアは幾何だけを返し、装飾・文言・層の順序は返さない** |
| `Options` / `DomainToDipScaling` / `Log` | 設定・環境値の読み書き（`Log` は null を `NullLogger` へ吸収する） |
| `Limits.DomainToDipScalingDefault` | ポート未指定時に適用する倍率の既定値（`1.0`）。両 UI の依存関係プロパティの既定値も本値を参照する |
| `Limits.ResolveDisplayScale` | 表示スケールが 0 以下のときに倍率を使う規則（描画層のサイズラベル書式が使う） |
| `SelectionRect` / `HasSelection` / `CreateSnapshot` / `Notified` | 状態の読み取りと通知（描画・公開イベントへの転送用） |

UI 層が適用する効果（`MarqueeFeedback`）の適用順はコアが単一所有する（UI 層は `IMarqueeEffects` を実装して自 UI の操作へ接続するだけ）:

| メソッド | 適用内容・順序 |
| --- | --- |
| `ApplyCursor` | カーソル操作（移動 → 非表示 → 再表示）。画面座標の変換は `IMarqueeEffects.ToScreenPoint` を使う（効果の適用先はデリゲートではなく `IMarqueeEffects` を渡す） |
| `ApplyVisual` | フォーカス → カーソル形状 → 再描画（要求されたものだけ） |

UI 層が使う共有値・計算:

| メンバー | 用途 |
| --- | --- |
| `MarqueeHandles.BorderThickness` | ハンドル枠の線の太さ（DIP。固定値。コアが `Marquee.ResolveGeometry` で解決済みとして渡すため、描画層は定数を持たない） |
| `MarqueeHandles`（`Rects`） | 8 方向のハンドル矩形（`Marquee.ResolveGeometry` がハンドルの位置と `MarqueeOptions.HandleSize` から解決済み） |
| `MarqueeGeometry.HoleDomainRect` | 穴あけ矩形のドメイン座標（ホストが矩形ごとの表示スケールを解決するために使う） |
| `Marquee.Limits.ResolveDisplayScale` | 表示スケールが未設定（0 以下）のときに倍率を使う規則（提示層のサイズラベル書式のみが使う） |

## 設計上の約束

1. **判断はコア・反映は UI**: 「どうするか」（nudge の開始/終了・確定/キャンセル・スナップの有無・キャンセル可否・カーソル種別・穴あけ矩形の優先順）はコアが決め、UI は効果を適用するだけにする。
2. **単一所有**: 幾何計算は `MarqueeSelectionGeometry` のみ、状態は `MarqueeState` のみ、入力の解釈は `MarqueeSession` のみが持つ。
3. **座標系**: `EsRect` / `EsPoint` は常にドメイン座標。UI 論理座標（DIP）との変換は `ICoordTransform`（`Port`）が担う。
4. **境界 1 行変換**: 各 UI のイベント引数は自前の型（例: Avalonia の `Rect`）で公開し、境界で 1 行変換する。キーは `EsKey` を介して通知する。
5. **カーソル操作の分離**: カーソル操作（非表示・再表示・移動）は**効果**として UI 層へ要求し、公開ファサードだけが `ICursorService` 抽象に依存する（プラットフォーム既定実装を生成しない）。状態機械はカーソル API を知らない。カーソル種別は `MarqueeHitTarget` として返し、UI が自 UI のカーソル型へ写像する。
6. **Enter と Return は同一**: Avalonia の `Key` は両者が同値のエイリアスで、名前ベースの相互変換では `Return` が得られるため、双方を確定キーとして扱う。
7. **両 UI で共通の値はコアが単一所有**: 描画対象ハンドル一覧・カーソル形状の種別・**設定の既定値と範囲補正（`MarqueeOptions`）**・**穴あけ矩形の解決**は、Avalonia 版と MewUI 版で同じ結果になる必要があるためコアが持つ（片方だけを直すと見た目や既定値がずれる）。**色・フォントサイズ・枠線の太さ・表示可否は提示層（`MarqueeAppearance`）が単一所有する**。UI 層が持つのは自 UI の描画・カーソル型への写像・座標変換のみ。
8. **設定の適用はコアが判断する**: 設定（`MarqueeOptions`）の反映は `ApplyOptions` が担い、対話パラメータの更新・変化した項目のログ・境界外選択の禁止の変化の検出・即時クリップを 1 箇所に集約する（UI 層は「変更通知を渡す」だけになる）。
9. **効果の適用順もコアが持つ**: 「どの効果をどの順で適用するか」（カーソル操作の順序・フォーカス → カーソル形状 → 再描画）は `MarqueeFeedback` が単一所有し、UI 層は自 UI の操作（`Focus` / `InvalidateVisual` / カーソル型への写像）へ接続するだけでよい。**カーソルの非表示・再表示も効果として要求する**（状態機械はカーソル API を知らない。適用の順序を 1 経路に集約し、非表示の残留を防ぐ）。
10. **持続資源の保持もコア**: ロガーは `Marquee.Log` が保持し、null を `NullLogger` へ吸収する（UI は get/set を委譲するだけ）。入力の判断のログも同じロガーで出力する。
11. **幾何の解決はコアが単一所有する**: `Marquee.ResolveGeometry` が「穴あけ矩形（`HoleRect`）・ハンドル（`MarqueeHandles`）・始点マーカー（`MarqueeMarker`）・マウス位置」を 1 つの `MarqueeGeometry` にまとめて返す。**描く値の有無（穴あけ矩形なし・選択なし・nudge 中でない）は null / 空で表し、コアが判断する**（UI 層が条件式を書かない）。**穴あけ矩形と座標変換ポートは 1 フレームに 1 回だけ解決する**（同じ矩形を各所が個別に変換するとずれるため）。
12. **選択の状態はセッションが単一のソース**: 選択矩形と選択の有無は設定値ではなくセッションの状態から `Marquee.ResolveGeometry` が導出し、提示層も同じ値を参照する（同じデータを 2 箇所に持たない）。
13. **色・体裁・表示可否・描画順は本コアが持たない**: これらは**描画にだけ必要な設定**であるため、提示層（`Feature.Marquee.Presenter` の `MarqueeAppearance` / `MarqueeDrawPlan` / `MarqueePresenter`）が単一所有する。本コアは幾何と判断だけを返し、**装飾の概念（名前空間・型）を持たない**。
14. **操作の入口はファサードのみ**: UI 層とホストは `Marquee` だけを参照し、状態機械（`MarqueeSession`）には触れない（`internal` で強制する）。設定の伝搬・効果の適用・通知の転送・幾何の解決も本型が単一所有し、UI 層は自 UI の公開プロパティ・公開イベント・描画 API へつなぐだけになる。
15. **選択の判断ではない規則はコアに置かない**: 「Ctrl+ホイール = ズーム」・「ビューポートの変化の再通知」のようなビューポート連携の規則は、単体で使う Marquee には存在しない概念であるため UI 層（ホスト）が持ちます（コアは将来そのまま別プロジェクトへ独立できる）。
16. **他のコントロールの語彙を持たない**: コアは Marquee 単体の概念だけを持ち、別のコントロール（ルーペ等）との連携名を持ち込まない。追従位置は汎用名（`MarqueeNoticeKind.MarkerPositionMoved`）で通知し、ルーペ中心への写像は UI 層の公開イベントが担う（コアは将来そのまま別プロジェクトへ独立できる）。

> **Note**: 本 README は**利用仕様書**（使い方と設計の意図）です。公開 API の仕様は [SPEC_ExtFunc.md](SPEC_ExtFunc.md)、構造・依存・例外処理・単一所有の一覧は [SPEC_System.md](SPEC_System.md) を参照してください。

## 座標型

UI フレームワークの幾何型（`Avalonia.Point` / `Rect` 等）ではなく、`Platform.Core` のコア幾何型 `EsPoint` / `EsRect` / `EsSize` / `EsKey` を使用します。

UI 実装側は、境界（ポインターイベントの受付・通知の生成・描画）で 1 行の相互変換（`.ToEsPoint()` / `.ToEsRect()` / `.ToAvaloniaRect()`）を行います。UI 内部の状態・描画は UI 側の型のままで構いません。

## ビルド

```powershell
dotnet build src/Feature.Marquee.Core/src/Feature.Marquee.Core.csproj
```

マーキー系の 4 プロジェクト（Core / Presenter / AvaloniaUI / MewUI）は `GenerateDocumentationFile` を有効にし、**存在しないメンバーへの `<see cref>` をビルド警告（CS1574）で検出**します（メンバー改名・削除でドキュメントが腐るのを防ぐ）。XML は検証にのみ使い、配布物（`bin` / `publish`）へは出しません（`CopyDocumentationFileToOutputDirectory=false`）。公開メンバーの未記載（CS1591）は対象外です。

## テスト

```text
tests/
├── Marquee.Tests.cs                 # 公開ファサード（操作・効果の適用・設定の伝搬・通知の転送・設定変更ログ・確保量の固定）
├── Marquee.Geometry.Tests.cs        # 幾何の解決（穴あけ矩形・ハンドル・始点マーカー・描く値の有無）
├── Marquee.Limits.Tests.cs          # 数値パラメータの既定値・安全域と補正・未設定時の解決規則
├── Contract/                        # 効果の基底クラス（全メンバーが空実装であること・派生で必要なものだけを override できること）
├── Drawing/                         # 幾何の解決の入力（組み立て・座標変換ポートの決定・DIP 変換・穴あけ矩形の優先順）
├── Interaction/                     # 状態遷移・ヒットテスト・ドラッグ・nudge・入力の解釈・効果の適用・設定の差分検出
├── Logging/                         # ログ定義の生成コード（IsEnabled 両パス・書式・差分検出）
├── Selection/                       # 純粋幾何（正規化・ハンドル位置・近傍判定・境界クリップ・カーソル種別）
└── Setting/                         # 既定値・部分変更・安全域への補正・等価性
```

**テストはソースのフォルダを鏡映します**（`src/Drawing/` の型は `tests/Drawing/` で検証する）。同じファサード型の partial（`Marquee.cs` / `Marquee.Geometry.cs` / `Marquee.Limits.cs`）のテストはルートに置きます。

プロジェクト単位の件数は `dotnet test <csproj> --list-tests` のクラス別集計で確認します（`dotnet test` の出力は行が折り返され、並列実行では行が混ざるため、行単位でプロジェクトと件数を対応付けてはいけません）。

```powershell
dotnet test src/Feature.Marquee.Core/tests/Feature.Marquee.Core.Tests.csproj
```

## ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
