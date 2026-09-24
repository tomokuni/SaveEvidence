# MarqueeControl.CoreForControl 外部機能仕様書

`MarqueeControl.CoreForControl` は、`MarqueeControl.Core`（選択の判断）を**他の UI コントロールへ組み込むときにだけ必要な処理**を提供します。UI 層が使う型は public、本層の内部手順は internal です（単体テストにのみ `InternalsVisibleTo` で公開します）。

## 公開 API の可視性

| 型 | 可視性 | 用途 |
| --- | --- | --- |
| `MarqueeWheelInterpreter` | `public` | ホイール入力の解釈と、ズーム要求の通知・連携先へのスクロール適用・スクロール要求の通知 |
| `IWheelScrollTarget` | `public` | スクロールの適用先（UI 層が適合させる抽象） |
| `WheelScrollDelta` | `internal` | 回転量 → 移動量（DIP）の換算（適用先は換算済みの移動量を受け取るため外部に公開しない） |
| `WheelZoomRequest` | `public` | Ctrl+ホイールによるズーム要求（方向・発生位置） |
| `WheelScrollRequest` | `public` | 通常ホイールによるスクロール要求（回転量・Shift・発生位置・ベクター変換） |
| `MarqueeWheelLogMessage` | `internal` | ズーム要求のログ定義（`ILogger` 拡張メソッド。ホストは定義名を参照しない） |
| `MarqueeViewportNotifier` | `public` | ビューポート変化の再通知（現在の状態スナップショット）と再描画の要求 |
| `MarqueeLabelLayoutPlan` | `public` | 描画するラベル（装飾のラベルとカーソル座標ラベル）を 1 つの一覧へまとめる |
| `MarqueeLabelElement` | `public` | ラベル 1 件の描画内容（背景矩形・文字原点・文言・寸法・色） |

## `MarqueeWheelInterpreter`（`Estable.Marquee.CoreForControl`・`public sealed class`）

ホイール入力を解釈し、ズーム要求の通知・連携先へのスクロール適用・スクロール要求の通知を行います。

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

## `IWheelScrollTarget`（`Estable.Marquee.CoreForControl`・`public interface`）

| メンバー | 説明 |
| --- | --- |
| `ScrollBy(EsVector delta)` | 移動量（DIP）だけスクロールする |

回転量 → 移動量の換算は `WheelScrollDelta` が済ませているため、実装は受け取った移動量を自身のスクロール位置へ加算するだけで済みます。

## `WheelScrollDelta`（`Estable.Marquee.CoreForControl`・`internal static class`）

| メンバー | 説明 |
| --- | --- |
| `Offset(double deltaX, double deltaY, bool shift)` | 回転量と修飾キーから移動量（DIP）を求める。1 ノッチ = `ScrollMath.WheelScrollUnit`（48 DIP）。Shift 併用時は縦の回転量を横の移動量として扱う |

## `WheelZoomRequest`（`Estable.Marquee.CoreForControl`・`public readonly record struct`）

| メンバー | 型 | 説明 |
| --- | --- | --- |
| `ZoomIn` | `bool` | true = ズームイン / false = ズームアウト |
| `Position` | `EsPoint` | ホイール操作の発生位置（コントロールローカル DIP）。ズーム中心に使える |

## `WheelScrollRequest`（`Estable.Marquee.CoreForControl`・`public readonly record struct`）

| メンバー | 型 | 説明 |
| --- | --- | --- |
| `DeltaX` | `double` | 横方向の回転量（1 ノッチ = 1.0） |
| `DeltaY` | `double` | 縦方向の回転量（1 ノッチ = 1.0。正 = 上方向） |
| `Shift` | `bool` | Shift 併用かどうか（横スクロールの意図） |
| `Position` | `EsPoint` | ホイール操作の発生位置（コントロールローカル DIP） |
| `Delta` | `EsVector` | 回転量を 1 つのベクターで取得する（回転量をベクターで受け取る API へそのまま渡せる） |

`Delta` は**コアの型同士の変換を本契約が単一所有する**ためのメンバーです（UI が `new EsVector(...)` を書くと、同じ変換が UI ごとに散ります）。

## `MarqueeViewportNotifier`（`Estable.Marquee.CoreForControl`・`public sealed class`）

ビューポート（パン・ズーム・表示サイズ）の変化を上位へ通知します。

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

## `MarqueeWheelLogMessage`（`Estable.Marquee.CoreForControl`・`internal static partial class`）

| メンバー | 説明 |
| --- | --- |
| `LogZoomRequested(this ILogger, bool zoomIn)` | Ctrl+ホイールによるズーム要求のログ。LogLevel: Debug、書式 `[Marquee] ZoomRequested: zoomIn={ZoomIn}` |

書式とレベルは両 UI で共通です（UI 実装ごとに持つと書式がずれ、片方だけ修正されます）。

## `MarqueeLabelLayoutPlan` / `MarqueeLabelElement`（`Estable.Marquee.CoreForControl`）

描画するラベル（装飾のラベルとカーソル座標ラベル）を 1 つの一覧へまとめます。

| メンバー | 説明 |
| --- | --- |
| `MarqueeLabelLayoutPlan.Resolve(in MarqueeDrawContent deco, MarqueeCoordLabel? coordLabel, Func<string, double, EsSize> measureText)` | 描画するラベルの一覧を**描画順**（装飾のラベル → カーソル座標ラベル）で返す。`measureText` は描画 API で測定した実寸を返す関数。null は `ArgumentNullException` |
| `MarqueeLabelElement.Background` | 背景矩形（DIP） |
| `MarqueeLabelElement.TextOrigin` | 文字の描画原点（左上・DIP） |
| `MarqueeLabelElement.Text` / `FontSize` / `TextColor` | 文言・フォントサイズ（DIP）・文字色 |
| `MarqueeLabelElement.BackgroundColor` | 背景色（実効色へ解決済み） |

- **描画順の単一所有**: 「装飾のラベルを上から積み、最後に座標ラベルを重ねる」順は本クラスが決めます。<br/>後から描く座標ラベルが手前に来ます。
- **両種のラベルを同じ型へ揃える**: サイズラベル（`MarqueeSizeLabel`）と座標ラベル（`MarqueeCoordLabel`）は形が同じでも別の型であり、従来は各 UI が 2 種類の配置計算と描画呼び出しを持っていました（本クラスが写像を単一所有します）。
- **描画するものが無い場合は一覧を生成しません**: 描画のたびに呼ばれるため空の確保を行いません（空の一覧を返します）。
- **実処理は UI 層**: 文字の実寸測定と描画 API の呼び出し（「背景 → 文字」の順は 1 項の一覧順に従う）を行います。

```csharp
foreach (var item in MarqueeLabelLayoutPlan.Resolve(deco, coordLabel, MeasureText))
{
    DrawLabel(item);
}
```
