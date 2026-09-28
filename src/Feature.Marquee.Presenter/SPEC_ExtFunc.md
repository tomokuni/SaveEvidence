# Feature.Marquee.Presenter 外部機能仕様書

`Feature.Marquee.Presenter`（`MarqueeControl` をコントロールとして成り立たせる UI 非依存層）が提供する外部機能と公開 API の仕様です。利用者（UI 実装・ホスト・独自コントロールの作者）から見える情報のみを記載し、内部実装の詳細は記載しません。設計の意図は [README](README.md)、構造と依存は [SPEC_System](SPEC_System.md) を参照してください。

本層の機能は 3 つに分かれます。

| 区分 | 機能 |
| --- | --- |
| **提示（Presenter）** | 描画計画の解決（幾何 + 外観から層の順序・装飾矩形・文言・ラベル配置を導出） |
| **外観** | 色・フォントサイズ・枠線の太さ・表示可否の単一値 |
| **組み込み規則** | ホイール入力の解釈と通知・連携先へのスクロール適用・ビューポート変化の再通知 |

## 1. 提供機能

### 1.1 提示（Presenter）

| 機能 | 提供元 | 説明 |
| --- | --- | --- |
| 描画計画の解決 | `MarqueePresenter.ResolveDrawPlan` | 幾何（コア）と外観から、層の順序・装飾矩形・ラベル配置を解決した 1 値（`MarqueeDrawPlan`）を返す。**UI が見る唯一の描画値**である |
| 装飾矩形の計算 | `MarqueeDecorationGeometry` | 枠線 4 辺の矩形と、暗転領域（描画面から穴あけ矩形を除いた 4 領域）を求める純粋関数 |
| ラベルの文言と基準点 | `MarqueeLabelTexts` | サイズラベル（位置と寸法の書式・表示スケール）とカーソル座標ラベルの文言・基準点を生成する |
| ラベル要素の配置 | `MarqueeLabelPlan.Resolve` | 実寸（描画 API の測定値）からラベル要素の配置指示（`MarqueeLabelPlacement`）を解決する。**要素として置くか画像として描くかは UI 層が決める** |
| ホスト付加情報ラベルの描画 | `MarqueeHostLabelRenderer.Draw` | ホストが与えた文言を、サイズラベルと同じ体裁（内側余白・背景 → 文字の順序）で描く |

#### 1.1.1 前提条件

- **描画は本層が行いません**。本層は描画計画というデータを返し、描画は各 UI（`MarqueeContextPainter` / `MarqueeSkiaPainter`）が自 UI の API で行います（UI フレームワークごとの描画モデルの差は描画 API の抽象では吸収しきれないため、抽象を持ちません）。
- 描画計画の座標は**ローカル DIP**です（原点 (0, 0)・サイズは描画領域）。ドメイン → DIP の変換はコアが済ませています。
- 実寸測定（`measureText`）は描画 API 依存のため UI 層が関数として渡します。
- 本層は外観（`MarqueeAppearance`）を単一所有します（描画にだけ必要な設定のため）。寸法・対話パラメータはコア（`MarqueeOptions`）が単一所有します。

### 1.2 組み込み規則

| 機能 | 提供元 | 説明 |
| --- | --- | --- |
| ホイール入力の解釈と通知 | `MarqueeWheelInterpreter` | Ctrl 併用はズーム要求、それ以外はスクロール（連携先があれば本層が適用し、無ければ要求として通知する） |
| 連携先へのスクロール適用 | `MarqueeWheelInterpreter.AttachScrollTarget` + `IWheelScrollTarget` | 回転量を移動量（DIP）へ換算し、UI 層が適合させた適用先へ渡す（1 ノッチ = 48 DIP・Shift で横） |
| ビューポート変化の再通知 | `MarqueeViewportNotifier` | 現在の状態スナップショットの通知と再描画の要求（選択の状態は変更しない） |

#### 1.2.1 前提条件

- ホイールの解釈は修飾キーの真偽と回転量だけを受け取ります（UI 型に依存しない）。
- ズーム率の決定・表示の適用・スクロール先の決定は**受け手（ホスト）**が行います（本層はビューの状態を保持しません）。
- スクロールの適用先を持つ UI だけが `IWheelScrollTarget` を実装します（持たない UI はスクロール要求として受け取ります）。

## 2. 公開 API 一覧

### `EsUtil.Feature.Marquee.Presenter`（提示・外観）

| 型 | 種別 | 説明 |
| --- | --- | --- |
| `MarqueePresenter` | class（`sealed` でない） | 提示のファサード。コアを内部に保持し、設定・環境値・入力の転送と、外観の保持、描画計画の解決を行う。**継承して `ResolveDrawPlan` を上書きできることが役割**（独自の見た目を提供する） |
| `MarqueeDrawPlan` | readonly record struct | 1 フレーム分の描画計画（外観・描画面・穴あけ矩形・暗転領域・枠線矩形・内側の塗りと枠線・ハンドル・マーカー・層の順序・ラベル配置）。**UI が見る唯一の描画値** |
| `MarqueeDrawLayer` | enum | 描画層（`Decoration` / `Handles` / `Marker` / `Label`）。**層にある ⇔ 描く値がある** |
| `MarqueeAppearance` | sealed record | **外観の単一所有者**（色・ラベルの体裁・枠線の太さ・表示可否）。`Default` を基点に `with` 式で部分変更する |
| `MarqueeTextPlacement` | readonly record struct | 描画層が UI 層へ渡すテキスト（文言・基準点・アライン・クランプ領域）。**要素として置くか canvas へ描くかは UI 層が決める** |
| `MarqueeLabelPlacement` | readonly record struct | ラベル要素の配置指示（背景矩形・内側余白・文言・フォントサイズ・色） |
| `MarqueeLabelPlan` | static class | `MarqueeTextPlacement` から `MarqueeLabelPlacement` を解決する |
| `MarqueeHostLabelRenderer` | static class | ホスト付加情報ラベルの描画（体裁の適用） |
| `MarqueeDecorationGeometry` | internal static class | 装飾矩形の計算（本層の内部手順。UI 層は解決済みの矩形を `MarqueeDrawPlan` から受け取る） |
| `MarqueeLabelTexts` | internal static class | ラベルの文言と基準点の生成（本層の内部手順。UI 層は `MarqueeDrawPlan.Labels` を受け取る） |

### `MarqueePresenter` のメンバー

| メンバー | 説明 |
| --- | --- |
| `MarqueePresenter(IMarqueeEffects effects, ICursorService? cursor = null, MarqueeOptions? options = null)` | コアのファサードを内部に生成する。`effects` は null 不可（null なら `ArgumentNullException`）。`cursor` は null 可（カーソル操作を行わない） |
| `Appearance` | 外観（`MarqueeAppearance`）。既定は `MarqueeAppearance.Default`。設定すると次の描画から反映される（`set`） |
| `ResolveDrawPlan(EsSize clientSize, Func<string, double, EsSize> measureText)` | 1 フレーム分の描画計画を解決する（`virtual`）。処理は「幾何の解決 → 装飾矩形の導出 → ラベルの文言と配置 → 層の順序」の順。`measureText` が null なら `ArgumentNullException` |
| `DisplayScaleScaling` | サイズラベルの DisplayScale 表示に使う係数（0 以下は `DomainToDipScaling` と同値） |
| `SizeLabelOverrideProvider` | サイズラベルの文言プロバイダ（null または空文字を返す場合は自動生成） |
| `SizeLabelDisplayScalingProvider` | 表示スケールを矩形ごとに解決するプロバイダ（null の場合は `DisplayScaleScaling`） |
| `Options` | 選択の設定（寸法・対話パラメータ）。コアへ委譲する |
| `DomainToDipScaling` / `HighlightRect` | 環境値・ホストが与える実行時状態。コアへ委譲する |
| `Log` | ロガー。null は `NullLogger` へ吸収される |
| `Notified` | 通知（`MarqueeNotice`）。UI 層は自 UI の公開イベントへ転送する |
| `HasSelection` / `SelectionRect` / `CreateSnapshot(EsPoint?)` | 状態の読み取り |
| `SetCoordinateTransform(ICoordTransform?)` / `ToDomain(EsPoint)` / `ToDip(EsPoint)` | 座標変換（コアへ委譲） |
| `Press(EsPoint, int)` / `Move(EsPoint)` / `Release(EsPoint)` / `KeyDown(EsKey, EsPoint?)` / `EndNudge()` | 入力の転送（コアへ委譲） |
| `SetSelection(EsRect)` / `ResetSelection()` / `RequestConfirm()` / `RequestCancel()` | 外部操作の転送（コアへ委譲） |

### `MarqueeDrawPlan` のメンバー

| メンバー | 型 | 説明 |
| --- | --- | --- |
| `Appearance` | `MarqueeAppearance` | 解決に使った外観（そのまま保持する。UI は色・フォントサイズをここから引く） |
| `SurfaceBounds` | `EsRect` | 描画面の全体（原点 (0, 0)・描画領域のサイズ） |
| `HoleRect` | `EsRect?` | 穴あけ矩形（DIP。選択もハイライトも無い場合は null） |
| `OverlayRegions` | `IReadOnlyList<EsRect>` | 暗転して塗る領域。描かない設定または穴あけ矩形が無い場合は空 |
| `OuterBorderRects` | `IReadOnlyList<EsRect>` | 穴あけ枠の矩形（4 辺）。描かない設定または穴あけ矩形が無い場合は空 |
| `InnerFillRect` | `EsRect?` | 穴あけ矩形の内側の塗り。塗らない設定または穴あけ矩形が無い場合は null |
| `InnerBorderRects` | `IReadOnlyList<EsRect>` | 穴あけ矩形の内側の枠線（4 辺）。描かない設定または穴あけ矩形が無い場合は空 |
| `Handles` | `MarqueeHandles?` | ドラッグハンドル（null は描かない） |
| `Marker` | `MarqueeMarker?` | 始点マーカー（null は描かない） |
| `Layers` | `IReadOnlyList<MarqueeDrawLayer>` | 描く層とその順序。先頭から順に処理する。テキストの層（`Label`）はラベルがある場合のみ末尾（常に最上位） |
| `Labels` | `IReadOnlyList<MarqueeLabelPlacement>` | ラベル要素の配置指示（実寸を反映済み）。表示しない場合は空 |
| `HasText` | `bool` | ラベルを持つ場合は true（`Labels.Count > 0` と同値） |
| `HasDecoration` | `bool` | 装飾（暗転領域・外枠・内側の塗り・内枠）のいずれかを持つ場合は true |

### `MarqueeAppearance` のメンバー（外観の単一値）

| 分類 | メンバー | 型 | 既定値 |
| --- | --- | --- | --- |
| 色 | `DefaultBorderColor`（static readonly） | `DrawingColor` | `#FFA9A9A9`（ハンドル枠と穴あけ枠が共有する） |
| 色 | `HandleColor` | `DrawingColor` | 白（全成分 0 を指定した場合は白へ解決される） |
| 色 | `HandleBorderColor` | `DrawingColor` | `#FFA9A9A9` |
| 色 | `MarkerColor` | `DrawingColor` | 白 |
| 色 | `SizeLabelColor` / `SizeLabelBackgroundColor` | `DrawingColor` | 白 / 黒 60% |
| 色 | `CoordLabelColor` / `CoordLabelBackgroundColor` | `DrawingColor` | 白 / 黒 60% |
| 色 | `OverlayColor` | `DrawingColor` | 黒 50%（`#80000000`） |
| 色 | `OuterBorderColor` | `DrawingColor` | `#FFA9A9A9` |
| 色 | `InnerFillColor` / `InnerBorderColor` | `DrawingColor` | 透明 / 透明 |
| 体裁 | `SizeLabelFontSizeDefault` / `CoordLabelFontSizeDefault` | `double`（const） | 14.0 / 12.0 |
| 体裁 | `SizeLabelFontSize` / `CoordLabelFontSize` | `double` | 6〜48 へ補正済み。非数は既定値 |
| 体裁 | `LabelFontSizeMin` / `LabelFontSizeMax` | `double`（const） | 6.0 / 48.0 |
| 体裁 | `LabelPadding` / `LabelPaddingDefault` | `double` | 4.0（ラベル共通の内側余白。行送りも本値から導く） |
| 表示可否 | `ShowHandles` / `ShowSizeLabel` / `ShowCoordLabel` | `bool` | `true` |
| 表示可否 | `ShowOverlay` / `ShowOuterBorder` | `bool` | `true` |
| 表示可否 | `ShowFullOverlay` | `bool` | `false`（穴あけ矩形が無くても全面を暗転する） |
| 表示可否 | `ShowInnerFill` / `ShowInnerBorder` | `bool` | `false` |
| 太さ | `OuterBorderThickness` / `InnerBorderThickness` | `double` | 2.0（0.5〜64 へ補正済み。安全域はコアの `Marquee.Limits` が単一所有する） |

色は UI 非依存の `System.Drawing.Color` です（各 UI の色型への変換は各 UI の描画境界で行う）。
**描画の可否は色のアルファでは決まりません**（本型の明示フラグが単一所有します）。

| 述語 | 条件 |
| --- | --- |
| `ShouldShowSizeLabel(bool hasHoleRect)` | 表示の設定が有効かつ穴あけ矩形がある（基準点が矩形の左上であり、文言も矩形の寸法から作るため） |
| `ShouldShowCoordLabel()` | 表示の設定が有効（基準点はカーソル位置であり、位置はコアの幾何が常に持つ） |
| `ShouldDrawOverlay(bool hasHoleRect)` | 穴あけ矩形がある、または全面暗転（`ShowFullOverlay`）が有効 |

### `MarqueeLabelPlan` のメンバー

| メンバー | 説明 |
| --- | --- |
| `Resolve(MarqueeTextPlacement? sizeLabel, MarqueeTextPlacement? coordLabel, MarqueeAppearance appearance, Func<string, double, EsSize> measureText)` | 配置するラベル要素（`MarqueeLabelPlacement`）の一覧を**順序どおり**（サイズラベル → カーソル座標ラベル）で返す。どちらも無い場合は空（一覧を生成しない）。体裁（フォントサイズ・色・内側余白）は `appearance`（外観の単一値）から写す。`appearance` または `measureText` が null なら `ArgumentNullException` |

### `MarqueeHostLabelRenderer` のメンバー

| メンバー | 説明 |
| --- | --- |
| `Resolve(EsPoint anchor, string text, double fontSize, DrawingColor textColor, DrawingColor backgroundColor, double padding, Func<string, double, EsSize> measureText)` | 基準点（背景の左上に置く座標）へ置く 1 行のラベルの配置指示を返す。**描画は各 UI が行う**（本層は描画 API を持たない）。`text` または `measureText` が null なら `ArgumentNullException` |

### `EsUtil.Feature.Marquee.Presenter`（組み込み規則）

| 型 | 可視性 | 用途 |
| --- | --- | --- |
| `MarqueeWheelInterpreter` | `public` | ホイール入力の解釈と、ズーム要求の通知・連携先へのスクロール適用・スクロール要求の通知 |
| `IWheelScrollTarget` | `public` | スクロールの適用先（UI 層が適合させる抽象） |
| `WheelScrollDelta` | `internal` | 回転量 → 移動量（DIP）の換算（適用先は換算済みの移動量を受け取るため外部に公開しない） |
| `WheelZoomRequest` | `public` | Ctrl+ホイールによるズーム要求（方向・発生位置） |
| `WheelScrollRequest` | `public` | 通常ホイールによるスクロール要求（回転量・Shift・発生位置・ベクター変換） |
| `MarqueeWheelLogMessage` | `internal` | ズーム要求のログ定義（`ILogger` 拡張メソッド。ホストは定義名を参照しない） |
| `MarqueeViewportNotifier` | `public` | ビューポート変化の再通知（現在の状態スナップショット）と再描画の要求 |

### `MarqueeWheelInterpreter`（`public sealed class`）

| メンバー | 説明 |
| --- | --- |
| `MarqueeWheelInterpreter(MarqueePresenter marquee)` | 提示層（コアのファサードを内包する）へ接続する（ロガーは `Marquee.Log` を共用する）。null は `ArgumentNullException` |
| `ZoomRequested` | `EventHandler<WheelZoomRequest>`。Ctrl 併用時に発火する |
| `ScrollRequested` | `EventHandler<WheelScrollRequest>`。Ctrl なしで連携先が無い場合に発火する |
| `AttachScrollTarget(IWheelScrollTarget? target)` | スクロールの適用先を設定する（null で解除。解除後は `ScrollRequested` を発火する） |
| `Handle(bool controlKey, bool shiftKey, double deltaX, double deltaY, EsPoint position)` | ホイール入力を解釈して通知または適用する |

**解釈の規則**:

| 入力 | 動作 | 内容 |
| --- | --- | --- |
| Ctrl 併用 | `ZoomRequested` | `ZoomIn` = `deltaY > 0`（上方向 = ズームイン）。`Position` はズーム中心（カーソル位置）として使える。連携先があってもズームが優先される |
| Ctrl なし + 連携先あり | 本クラスが適用 | `WheelScrollDelta.Offset` で移動量へ換算し、`IWheelScrollTarget.ScrollBy` を呼ぶ（通知しない） |
| Ctrl なし + 連携先なし | `ScrollRequested` | `DeltaX` / `DeltaY` / `Shift` / `Position` をそのまま渡す（回転量が 0 でも通知する） |

- **ログ**: ズーム要求のみ Debug で記録する（スクロールの実処理は受け手・連携先が担うため記録しない）。
- **実処理は受け手（ホスト）**: ズーム率の決定・表示の適用は本クラスが行わない（ビューの状態を保持しない）。
- **選択状態を変更しない**: コアのセッション（選択の判断）には関与しない。

```csharp
var wheel = new MarqueeWheelInterpreter(presenter);
wheel.ZoomRequested += (_, request) => ZoomRequested?.Invoke(this, request);
wheel.ScrollRequested += (_, request) => ScrollRequested?.Invoke(this, request);
wheel.AttachScrollTarget(new ScrollViewerTarget(scrollViewer));   // 連携先（自 UI のスクロール機構）

wheel.Handle(controlKey: true, shiftKey: false, deltaX: 0, deltaY: 1, new EsPoint(30, 40));
```

### `IWheelScrollTarget`（`public interface`）

| メンバー | 説明 |
| --- | --- |
| `ScrollBy(EsVector delta)` | 移動量（DIP）だけスクロールする |

回転量 → 移動量の換算は `WheelScrollDelta` が済ませているため、実装は受け取った移動量を自身のスクロール位置へ加算するだけで済みます。

### `WheelScrollDelta`（`internal static class`）

| メンバー | 説明 |
| --- | --- |
| `Offset(double deltaX, double deltaY, bool shift)` | 回転量と修飾キーから移動量（DIP）を求める。1 ノッチ = `ScrollMath.WheelScrollUnit`（48 DIP）。Shift 併用時は縦の回転量を横の移動量として扱う |

### `WheelZoomRequest`（`public readonly record struct`）

| メンバー | 型 | 説明 |
| --- | --- | --- |
| `ZoomIn` | `bool` | true = ズームイン / false = ズームアウト |
| `Position` | `EsPoint` | ホイール操作の発生位置（コントロールローカル DIP）。ズーム中心に使える |

### `WheelScrollRequest`（`public readonly record struct`）

| メンバー | 型 | 説明 |
| --- | --- | --- |
| `DeltaX` | `double` | 横方向の回転量（1 ノッチ = 1.0） |
| `DeltaY` | `double` | 縦方向の回転量（1 ノッチ = 1.0。正 = 上方向） |
| `Shift` | `bool` | Shift 併用かどうか（横スクロールの意図） |
| `Position` | `EsPoint` | ホイール操作の発生位置（コントロールローカル DIP） |
| `Delta` | `EsVector` | 回転量を 1 つのベクターで取得する（回転量をベクターで受け取る API へそのまま渡せる） |

`Delta` は**コアの型同士の変換を本契約が単一所有する**ためのメンバーです（UI が `new EsVector(...)` を書くと、同じ変換が UI ごとに散ります）。

### `MarqueeViewportNotifier`（`public sealed class`）

| メンバー | 説明 |
| --- | --- |
| `MarqueeViewportNotifier(MarqueePresenter marquee, IMarqueeEffects effects)` | 提示層（状態の取得元）と再描画の要求先（UI 層の効果）へ接続する。null は `ArgumentNullException` |
| `ViewportChanged` | `EventHandler<MarqueeSnapshot>`。ビューポート変化の通知（現在の状態スナップショットを運ぶ） |
| `Notify()` | 現在の状態スナップショットを通知し、再描画を要求する（**状態は変更しない**） |

- **UI 層の責務**: UI の公開イベントへ転送する（コアの通知を経由しないため、スナップショットからイベント引数を組み立てる）。
- **ホストの責務**: 通知を受けてビューポート座標で再描画する（暗転マスク・選択枠の追従等）。

```csharp
var viewport = new MarqueeViewportNotifier(presenter, effects);
viewport.ViewportChanged += (_, snapshot) => ViewportChanged?.Invoke(this, new SelectionRectEventArgs(snapshot.SelectionRect, snapshot));
viewport.Notify();   // 変化を検知したら呼ぶ
```

両クラスは提示層（`MarqueePresenter`）を受け取ります（コアのファサードを UI 層が直接扱わないため）。

### `MarqueeWheelLogMessage`（`internal static partial class`）

| メンバー | 説明 |
| --- | --- |
| `LogZoomRequested(this ILogger, bool zoomIn)` | Ctrl+ホイールによるズーム要求のログ。LogLevel: Debug、書式 `[Feature.Marquee.Presenter] ZoomRequested: zoomIn={ZoomIn}` |

書式とレベルは両 UI で共通です（UI 実装ごとに持つと書式がずれ、片方だけ修正されます）。

## 3. 契約（利用者が守る・受け取る値）

### 描画の呼び分け

| 描く対象 | 呼ぶ API |
| --- | --- |
| 図形（装飾・ハンドル・始点マーカー） | `MarqueeDrawPlan.Layers` の順に自 UI の描画 API を呼ぶ（`MarqueeContextPainter.Paint` / `MarqueeSkiaPainter.Paint`） |
| テキスト（サイズ・カーソル座標） | `MarqueeDrawPlan.Labels`（`MarqueeLabelPlacement`。実寸を反映済み）を要素へ写す、または自 UI の描画 API で文字として描く |
| 付加情報ラベルの配置 | `MarqueeHostLabelRenderer.Resolve`（文言と基準点はホストが決める） |

### ラベル要素（`MarqueeLabelPlacement`）

| 項目 | 内容 |
| --- | --- |
| `Bounds` | 要素を置く矩形（背景の大きさ = 文字の実寸 + 内側余白 × 2。アラインと領域クランプ適用済み） |
| `PaddingX` / `PaddingY` | 要素の内側余白（外観の値。要素の `Padding` にそのまま写す） |
| `Text` / `FontSize` / `TextColor` / `BackgroundColor` | 文言・フォントサイズ・色は外観（`MarqueeAppearance`）と `MarqueeLabelTexts` が与える（UI は判断しない） |
| 角丸 | 持たない（選択矩形のラベルは矩形である） |

### 付加情報ラベルの体裁

- 背景は基準点（`anchor`）を左上とし、文字の実寸へ内側余白（`padding`）を四方へ加えた大きさになります。
- 文字は同じ余白だけ内側へ入ります（**背景の四方の余白が等しい**）。
- 縦積みの行送りは「行の高さ + 内側余白 × 2」です（背景の下端と次の行の上端が一致します）。
- 文言・表示可否はホストが判断します（本層は文言を生成しません）。

### ホイール要求の受け取り

- `ZoomRequested` / `ScrollRequested` は購読者がいなければ何も起きません（例外になりません）。
- 連携先が未設定（または解除後）の場合、スクロールは破棄せず `ScrollRequested` として通知されます。

## 4. 制限事項

- 描画そのものは提供しません（描画計画というデータまでを返し、描画は各 UI が自 UI の API で行う）。
- 文字の折り返し・複数行・文字揃えは扱いません（1 行の左揃えのみ。カーソル座標ラベルは左端・上端、サイズラベルは穴あけ矩形の左上）。
- 破線・クリップ・不透明度の指定は扱いません（描画計画は矩形と色までの情報を持つ）。
- ホイールの解釈は「Ctrl = ズーム」の 1 規則のみです（修飾キーの組み合わせを増やす場合は `Handle` の契約が変わります）。

## 5. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
