# MarqueeControl.UIBase 外部機能仕様書

`MarqueeControl.UIBase`（`MarqueeControl` をコントロールとして成り立たせる UI 非依存層）が提供する外部機能と公開 API の仕様です。利用者（UI 実装・ホスト・独自コントロールの作者）から見える情報のみを記載し、内部実装の詳細は記載しません。設計の意図は [README](README.md)、構造と依存は [SPEC_System](SPEC_System.md) を参照してください。

本層の機能は 2 つに分かれます。

| 区分 | 機能 |
| --- | --- |
| **描画手順** | 図形の描画・ラベル要素の配置指示・ホスト付加情報ラベルの描画 |
| **組み込み規則** | ホイール入力の解釈と通知・連携先へのスクロール適用・ビューポート変化の再通知 |

## 1. 提供機能

### 1.1 描画手順

| 機能 | 提供元 | 説明 |
| --- | --- | --- |
| 描画内容の描画（図形） | `IMarqueeRenderer.DrawShapes`（本層の契約） | 装飾・ハンドル・始点マーカーを描画順に描く（**テキストは描かない**。テキストを canvas へ描く実装は基底の `DrawLabel`（既定は何もしない）を上書きする。色は `MarqueePaint` から引く）。層の順序はコア（`MarqueeDrawContent.Layers`）がデータとして単一所有し、本層の `MarqueeRendererBase` がその順に写す |
| テキスト要素の配置 | `MarqueeLabelPlan.Resolve`（UI 層が呼ぶ） | 実寸（描画 API の測定値）からラベル要素の配置指示（`MarqueeLabelPlacement`）を解決する。**要素として置くか画像として描くかは UI 層が決める** |
| ホスト付加情報ラベルの描画 | `MarqueeHostLabelRenderer.Draw` | ホストが与えた文言を、サイズラベルと同じ体裁（内側余白・背景 → 文字の順序）で描く |

#### 1.1.1 前提条件

- 描画先は `IEsCanvas`（`EsUtil.SaveEvidence.Common.Imaging`）です。Skia のキャンバスを使う場合は `SkiaEsCanvas`、Avalonia の `DrawingContext` を使う場合は `AvaloniaCanvas`（`Platform.AvaloniaUI`）を使います。
- 描画の単位は**アダプタの座標系**に従います（マーキーは DIP、ルーペは物理ピクセル）。単位の換算（DPI スケーリング）はアダプタの利用側が行います。
- 描画面の消去は呼び出し側の責務です（フレームワークが消去する UI では不要）。
- 本層は色（`MarqueePaint`）を単一所有します（描画にだけ必要な設定のため）。寸法・表示可否・文言はコアの契約が単一所有します。

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

### `EsUtil.SaveEvidence.Marquee.UIBase`（描画手順）

| 型 | 種別 | 説明 |
| --- | --- | --- |
| `MarqueePaint` | sealed record | **色の単一所有者**（ハンドル・十字マーカー・ラベル・装飾）。`Default` を基点に `with` 式で部分変更する |
| `MarqueeRenderer` | sealed class | 本層の `MarqueeRendererBase` の既定実装（`IMarqueeRenderer`（本層の契約）を実装する）。色を必須引数で受け取る（`Default` のような共有インスタンスは持たない） |
| `MarqueeOuterRenderer` | static class | 選択矩形の外側の装飾の描画（色は引数の `MarqueePaint` から引く） |
| `MarqueeInnerRenderer` | static class | 穴あけ矩形の内側の装飾の描画（色は引数の `MarqueePaint` から引く） |
| `MarqueeLabelPlan` | static class | ラベル要素の配置指示の解決（色は引数の `MarqueePaint` から写す） |
| `MarqueeHostLabelRenderer` | static class | ホスト付加情報ラベルの描画 |

### `MarqueeRenderer` のメンバー（`IMarqueeRenderer` の実装）

| メンバー | 説明 |
| --- | --- |
| `MarqueeRenderer(MarqueePaint paint)` | 描画に使う色を指定して生成する（`MarqueePaint.Default` を基点に組み立てた値を渡す）。`paint` が null なら `ArgumentNullException`。色はインスタンスごとに異なるため、共有インスタンスを書き換えず差し替える |
| `DrawShapes(IEsCanvas canvas, in MarqueeDrawContent content)` | 図形の層（装飾・ハンドル・始点マーカー）を描画順に描く（**テキストは描かない**）。`canvas` が null なら `ArgumentNullException` |
| `SizeLabelFontSize` / `CoordLabelFontSize`（描画実装＝`MarqueeRenderer` のプロパティ） | ラベルのフォントサイズ（描画の設定 `MarqueePaint` の値への入口）。**描画の契約（`IMarqueeRenderer`）には含めない** |
| `DrawLabel(IEsCanvas canvas, in MarqueeDrawContent content)`（`protected virtual`） | テキストを描画ポートへ描くフック。**既定は何もしない**（UI 要素として置く UI は上書きしない） |

### `MarqueePaint` のメンバー（色の単一値）

| メンバー | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `Default` | `MarqueePaint` | - | すべての色を既定値にした値（部分変更の基点） |
| `DefaultHandleBorderColor` | `DrawingColor`（static readonly） | `#FFA9A9A9` | ハンドル枠の既定色（穴あけ枠と共有） |
| `HandleColor` | `DrawingColor` | 白 | ハンドルの塗り色（全成分 0 を指定した場合は白へ解決される） |
| `HandleBorderColor` | `DrawingColor` | `#FFA9A9A9` | ハンドルの枠線色 |
| `MarkerColor` | `DrawingColor` | 白 | 十字マーカー（始点マーカー）の色 |
| `SizeLabelColor` / `SizeLabelBackgroundColor` | `DrawingColor` | 白 / 黒 60% | サイズラベルの文字色と背景色 |
| `CoordLabelColor` / `CoordLabelBackgroundColor` | `DrawingColor` | 白 / 黒 60% | カーソル座標ラベルの文字色と背景色 |
| `OverlayColor` | `DrawingColor` | 黒 50%（`#80000000`） | 暗転マスクの色 |
| `OuterBorderColor` | `DrawingColor` | `#FFA9A9A9` | 穴あけ枠の色 |
| `InnerFillColor` / `InnerBorderColor` | `DrawingColor` | 透明 / 透明 | 穴あけ矩形の内側の塗り色と枠線色 |

色は UI 非依存の `System.Drawing.Color` です（各 UI の色型への変換は描画ポートのアダプタが行う）。
**描画の可否は色のアルファでは決まりません**（`MarqueeDecoOptions` の明示フラグが単一所有します）。

### `MarqueeOuterRenderer` / `MarqueeInnerRenderer` のメンバー

| メンバー | 説明 |
| --- | --- |
| `MarqueeOuterRenderer.Draw(IEsCanvas canvas, in OuterDecoFrame frame, MarqueePaint paint)` | 暗転領域（`OverlayRegions`）を `paint.OverlayColor` で塗り、枠線（`BorderRects`）を `paint.OuterBorderColor` で描く（空のコレクションは何もしない） |
| `MarqueeInnerRenderer.Draw(IEsCanvas canvas, in InnerDecoFrame frame, MarqueePaint paint)` | 塗り矩形（`FillRect`。null なら何もしない）を `paint.InnerFillColor` で塗り、枠線（`BorderRects`）を `paint.InnerBorderColor` で描く |

### `MarqueeLabelPlan` のメンバー

| メンバー | 説明 |
| --- | --- |
| `Resolve(in MarqueeDrawContent content, MarqueePaint paint, Func<string, double, EsSize> measureText)` | 配置するラベル要素（`MarqueeLabelPlacement`）の一覧を**順序どおり**（サイズラベル → カーソル座標ラベル）で返す。どちらも無い場合は空（一覧を生成しない）。体裁（フォントサイズ・色・内側余白）は `paint`（描画の設定）から写す。`paint` または `measureText` が null なら `ArgumentNullException` |

### `MarqueeHostLabelRenderer` のメンバー

| メンバー | 説明 |
| --- | --- |
| `Draw(IEsCanvas canvas, EsPoint anchor, string text, double fontSize, DrawingColor textColor, DrawingColor backgroundColor, double padding)` | 基準点（背景の左上に置く座標）へ 1 行のラベルを描く。`canvas` または `text` が null なら `ArgumentNullException` |

### `EsUtil.SaveEvidence.Marquee.UIBase`（組み込み規則）

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
| `MarqueeWheelInterpreter(Marquee marquee)` | コアの公開ファサードへ接続する（ロガーは `Marquee.Log` を共用する）。null は `ArgumentNullException` |
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
var wheel = new MarqueeWheelInterpreter(marquee);
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
| `MarqueeViewportNotifier(Marquee marquee, IMarqueeEffects effects)` | コアの公開ファサード（状態の取得元）と再描画の要求先（UI 層の効果）へ接続する。null は `ArgumentNullException` |
| `ViewportChanged` | `EventHandler<MarqueeSnapshot>`。ビューポート変化の通知（現在の状態スナップショットを運ぶ） |
| `Notify()` | 現在の状態スナップショットを通知し、再描画を要求する（**状態は変更しない**） |

- **UI 層の責務**: UI の公開イベントへ転送する（コアの通知を経由しないため、スナップショットからイベント引数を組み立てる）。
- **ホストの責務**: 通知を受けてビューポート座標で再描画する（暗転マスク・選択枠の追従等）。

```csharp
var viewport = new MarqueeViewportNotifier(marquee, effects);
viewport.ViewportChanged += (_, snapshot) => ViewportChanged?.Invoke(this, new SelectionRectEventArgs(snapshot.SelectionRect, snapshot));
viewport.Notify();   // 変化を検知したら呼ぶ
```

### `MarqueeWheelLogMessage`（`internal static partial class`）

| メンバー | 説明 |
| --- | --- |
| `LogZoomRequested(this ILogger, bool zoomIn)` | Ctrl+ホイールによるズーム要求のログ。LogLevel: Debug、書式 `[Marquee] ZoomRequested: zoomIn={ZoomIn}` |

書式とレベルは両 UI で共通です（UI 実装ごとに持つと書式がずれ、片方だけ修正されます）。

## 3. 契約（利用者が守る・受け取る値）

### 描画の呼び分け

| 描く対象 | 呼ぶ API |
| --- | --- |
| 図形（装飾・ハンドル・始点マーカー） | `DrawShapes`（**テキストは描かない**） |
| テキスト（サイズ・カーソル座標） | `MarqueeLabelPlan.Resolve`（コアの `MarqueeDrawContent` のテキスト配置を渡す。UI が要素として置くか canvas へ描くかを決める） |

### ラベル要素（`MarqueeLabelPlacement`）

| 項目 | 内容 |
| --- | --- |
| `Bounds` | 要素を置く矩形（背景の大きさ = 文字の実寸 + 内側余白 × 2。アラインと領域クランプ適用済み） |
| `PaddingX` / `PaddingY` | 要素の内側余白（体裁の値。要素の `Padding` にそのまま写す） |
| `Text` / `FontSize` / `TextColor` / `BackgroundColor` | 文言・フォントサイズ・色は描画の設定（`MarqueePaint`）が与える（UI は判断しない） |
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

- 描画の補間方式（画像の拡大）は描画ポートの実装に依存します（最近傍 / 線形）。
- 文字の折り返し・複数行・文字揃えは扱いません（1 行の左揃えのみ）。
- 破線・クリップ・不透明度の指定は扱いません（描画ポートの契約に含めない）。
- アンチエイリアスの有無は描画ポートの実装に依存します。
- ホイールの解釈は「Ctrl = ズーム」の 1 規則のみです（修飾キーの組み合わせを増やす場合は `Handle` の契約が変わります）。

## 5. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
