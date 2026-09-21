# LoupeControl.MewUI 外部機能仕様書

`LoupeControl`（MewUI 版ルーペ）が外部利用者へ提供する機能と公開 API の仕様です。
内部実装の詳細（描画手順・キャッシュ等）は記載しません。

## 概要

ルーペ（拡大鏡）は、**拡大元の 100% 画像**と**拡大中心（元画像ピクセル座標）**を受け取り、
市松模様の背景・拡大画像・十字線・外枠・座標ラベルを描画するコントロールです。

**Avalonia 版 `LoupeControl` と同じ UI 非依存コア（`LoupeControl.Core`）を使うため、設定値と見た目は両 UI で一致します。**

## 公開 API

名前空間 `Estable.MewUI.Loupe` / クラス `LoupeControl`（`public sealed`、`SkiaCanvasView` 派生）。

### プロパティ

| プロパティ | 型 | 既定値 | 安全域（範囲外は自動補正） | 説明 |
| --- | --- | --- | --- | --- |
| `Source` | `EsBitmap?` | `null` | — | 拡大元の 100% 画像。null の場合は市松模様のみ |
| `CenterPoint` | `EsPoint?` | `null` | — | 拡大中心（元画像ピクセル座標）。null の場合は市松模様のみ |
| `RequestedSize` | `int` | 160 | 1〜4096 | 一辺のサイズ（DIP） |
| `RenderScaling` | `double` | 1.0 | 0.1〜8.0（非有限は既定値） | DIP → 物理ピクセルの係数 |
| `ZoomLevel` | `int` | 4 | 1〜64 | 拡大率（偶数推奨） |
| `CrossColor` | `Color?` | `null` | — | 十字線の色（不透明度はアルファ）。null は描画しない |
| `CheckerSize` | `int` | 6 | 1〜128 | 市松模様の 1 タイルサイズ（DIP） |
| `CheckerColorA` | `Color?` | `null` | — | 市松模様の明色。null は透明 |
| `CheckerColorB` | `Color?` | `null` | — | 市松模様の暗色。null は透明 |
| `BackgroundColor` | `Color` | 白（不透明） | — | 市松模様の下地色（不透明）。市松模様の色は半透明のため、下地がないと後ろの要素が透ける |
| `FrameColor` | `Color?` | `null` | — | 外枠の色。null は描画しない |
| `FrameWidth` | `int` | 2 | 1〜64 | 外枠の太さ（DIP） |
| `ShowCoordLabel` | `bool` | `true` | — | 上端中央の座標ラベルの表示可否 |
| `CoordLabelColor` | `Color?` | `null` | — | 座標ラベルの文字色。null は白 |
| `CoordLabelBackgroundColor` | `Color?` | `null` | — | 座標ラベルの背景色（不透明度はアルファ）。null は黒 60% |
| `CoordLabelFontSize` | `double` | 12.0 | 6〜48（非有限は既定値） | 座標ラベルのフォントサイズ（DIP） |

`Color` は `Aprillz.MewUI.Color` です。

## 描画仕様

描画の順序は次のとおりです（後から描いたものが上に重なります）。

| 順序 | 要素 | 備考 |
| --- | --- | --- |
| 1 | 下地 | `BackgroundColor` で領域全面を塗る（市松模様の色は半透明のため、後ろの要素を透かさない） |
| 2 | 市松模様 | 明色で全面を塗った後、暗色タイルを市松位置へ描画する |
| 3 | 拡大画像 | `Source` と `CenterPoint` がある場合のみ。切り出し領域の外は透明（下地の市松模様が見える） |
| 4 | 十字線 | 中央の縦横 2 本（幅は拡大率と同じ物理ピクセル） |
| 5 | 外枠 | 四辺（`FrameWidth` の太さ） |
| 6 | 座標ラベル | 上端中央。`CenterPoint` を `(x, y)` 形式（整数）で表示する |

### サイズの決定

| 項目 | 決定方法 |
| --- | --- |
| 表示サイズ（物理ピクセル） | `RequestedSize`（DIP） × `RenderScaling`。キャンバスより大きい場合はキャンバスに合わせる |
| 拡大率 | `ZoomLevel` 倍（最近傍で拡大する） |
| 切り出し領域の一辺 | `ceil(表示サイズ / ZoomLevel)` ドメインピクセル |
| 切り出し領域の左上 | 拡大中心 − 切り出し一辺 / 2（整数化）。画像の外へはみ出した部分（左・上・右・下）は透明パディングとなる |

### 座標ラベルの表示条件

`ShowCoordLabel` が `true` かつ `CenterPoint` が設定済みの場合のみ表示します。

## レイアウト

| 項目 | 仕様 |
| --- | --- |
| 要求サイズ | 一辺 `RequestedSize`（DIP）の正方形（`RequestedSize` の変更はレイアウトを再評価する） |
| 表示位置 | **本コントロールは位置を決定しない**。ホストが親のレイアウト（`HorizontalAlignment` / `VerticalAlignment` / `Margin` / `Canvas` 等）で配置する |

## DPI

モニター間の移動で DPI が変わると、`RenderScaling` は `newDpi / 96` へ自動更新されます。
ホストがスケーリング係数を設定する必要はありません（明示設定した値も DPI 変更時は上書きされます）。

## エラー時の挙動

| 状況 | 挙動 |
| --- | --- |
| `Source` が `null` | 市松模様のみを描画する（例外にしない） |
| `CenterPoint` が `null` | 市松模様のみを描画する（例外にしない） |
| `Source` が破棄済み | 市松模様のみを描画する（例外にしない） |
| 色がすべて `null` | 何も描画しない（例外にしない） |
| 安全域外の数値 | 安全域へ補正する（例外にしない） |

## 描画完了イベント

`Rendered` イベントは、ルーペを描画した直後（同一フレーム内）に引数 `LoupeRenderedEventArgs` で通知します。ホストはハンドラ内で描画先キャンバスへ追加描画（選択矩形の破線等のオーバーレイ）を行えます。

| メンバー | 型 | 説明 |
| --- | --- | --- |
| `Canvas` | `SKCanvas` | ルーペを描画したキャンバス（座標系はルーペ画像のピクセル） |
| `PixelSize` | `int` | ルーペの一辺のサイズ（物理ピクセル） |
| `ZoomLevel` | `int` | 拡大率 |
| `CenterPoint` | `EsPoint?` | 拡大中心（ドメイン座標）。未設定時は null |
| `CaptureTopLeft` | `EsPoint?` | 切り出し領域の左上（ドメイン座標）。中心未設定時は null（オーバーレイを描画できない） |
| `Scale` | `double` | ドメイン座標 → ルーペ画像ピクセル座標の変換係数 |

オーバーレイの描画位置は `(ドメイン座標 − CaptureTopLeft) × Scale` で求めます（切り出し領域と倍率の計算は Avalonia 版と同じコアの計算を共有します）。

## ログ

`Log` プロパティへ `ILogger` を設定すると、設定変更と描画の例外を出力します（未設定・null の場合は `NullLogger` になり出力しません）。

| レベル | 出力内容 |
| --- | --- |
| Debug | `[LoupeCtrl] Setting: {名前} {前} → {後}`（Source / CenterPoint / RequestedSize / RenderScaling / ZoomLevel。補正後・同一値なら出力しない） |
| Error | `[LoupeCtrl] Update exception`（描画の例外。スタックトレース付き。アプリは継続する） |

Avalonia 版の再描画パイプライン（RequestRender / RenderCore / GenerateAsyncDone）に相当する Trace ログは持ちません（MewUI 版は同期描画のため）。

## 非対応（今後の対応）

| 項目 | 状況 |
| --- | --- |
| 座標変換ポート（`SetCoordinateTransform`） | 未実装（位置計算はホストが `LoupePositionCalculator` で行う） |
| 再描画の要求メソッド（`Clear` / `ForceUpdate` / `RenderAsync`） | 未実装（プロパティ変更で自動再描画される） |
