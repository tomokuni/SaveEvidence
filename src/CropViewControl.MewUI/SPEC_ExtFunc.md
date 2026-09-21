# CropViewControl.MewUI 外部機能仕様書

CropViewControl（MewUI 版）が外部利用者へ提供する機能と公開 API の仕様です。
内部実装の詳細（描画手順・キャッシュ等）は記載しません。

## 概要

CropViewControl は、**画像の表示（拡大・パン・Fit）**と、**外部提供矩形のオーバーレイ描画**を統合した高レベル複合コントロールです。

**Avalonia 版と同じ UI 非依存コアを使うため、ズーム挙動・矩形の見た目は両 UI で一致します。**

## 公開 API

名前空間 `Estable.MewUI.CropView` / クラス `CropViewControl`（`public sealed`、`Grid` 派生）。

### プロパティ

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `Source` | `EsBitmap?` | `null` | 表示する画像。設定すると Fit 表示（ズーム率 0・オフセット 0）へリセットする |
| `CenterAlign` | `bool` | `true` | 画像がビューポートより小さい軸を中央寄せする（false で左上寄せ） |
| `Rects` | `IReadOnlyList<OverlayRect>` | 空 | オーバーレイとして描画する矩形（ドメイン座標 = 元画像ピクセル座標） |
| `ZoomView` | `ZoomViewControl` | — | 内包するズームビューポート |
| `ZoomPercent` | `double` | Fit の実効倍率 | 実効ズーム率（%）。**0 より大きい値のみ設定可能**（0 以下は無視） |
| `IsFit` | `bool` | `true` | Fit 表示中かどうか |
| `CanZoomIn` / `CanZoomOut` | `bool` | `false` | ズーム可否（画像未設定時は false） |

### メソッド

| メソッド | 説明 |
| --- | --- |
| `ZoomIn()` / `ZoomOut()` | 段階レベルを 1 つ移動する（移動先がなければ何もしない） |
| `ZoomFit()` | Fit 表示へ戻す |
| `PanByWheel(EsVector, bool)` | ホイールの回転量（X/Y）と Shift 併用でパンする（換算はズームビューが単一所有） |
| `ZoomByWheel(bool, EsPoint?)` | Ctrl+ホイールのズームを適用する（位置はズーム中心。null はビューポート中心） |
| `TranslatePositionFrom(Element, EsPoint)` | 重ねた要素のローカル座標を本コントロールの座標へ変換する |
| `ImageRectViewport()` | 画像の表示矩形（ビューポート座標・DIP） |
| `ViewportToContent(EsPoint)` | ビューポート座標 → ドメイン座標（画像表示矩形の原点基準） |
| `ContentToViewport(EsPoint)` | ドメイン座標 → ビューポート座標（画像表示矩形の原点基準） |

### イベント

| イベント | 引数 | 説明 |
| --- | --- | --- |
| `RectsChanged` | `EventArgs` | `Rects` の設定で発火する（再描画の要求後） |
| `ZoomChanged` | `ZoomChangedEventArgs` | ズーム率・Fit 状態・ズーム可否（内包するズームビューから中継） |
| `ViewportChanged` | `EventArgs` | パン・ズーム・リサイズ（内包するズームビューから中継） |

## 矩形オーバーレイの仕様

| 項目 | 仕様 |
| --- | --- |
| 座標系 | ドメイン座標（元画像ピクセル）。表示時にビューポート座標へ変換する |
| 実線（`Solid`） | 前景色（`Foreground`）の枠のみを描画する |
| 破線（`Dashed`） / 点線（`Dotted`） | 背景色（`Background`）の実線の上へ前景色の破線を重ねる（市松風の視認性） |
| ダッシュ長 | 破線 = 8 / 間隔 4、点線 = 2 / 間隔 2（`CropViewControl.Core` の `OverlayLineDash` が単一所有） |
| 線の太さ | `Thickness`（DIP）。0 以下は 1 として扱う。枠は矩形の内側へ描画する |
| サイズ 0 の矩形 | 何も描画しない |
| 色 | `System.Drawing.Color`（アルファを含む） |

## 操作仕様

内包するズームビューが入力を受け取ります（オーバーレイはヒットテスト対象外）。

| 操作 | 動作 |
| --- | --- |
| 左ボタンドラッグ | パン（grab 方式） |
| ホイール | 縦パン（1 ノッチ = 48 DIP） |
| Shift+ホイール | 横パン |
| Ctrl+ホイール | ポインタ位置を中心にズーム |

## レイアウト

| 項目 | 仕様 |
| --- | --- |
| 要求サイズ | 空（親が割り当てた領域全体をビューポートとして使う） |
| 子要素 | ズームビューと矩形オーバーレイの 2 つを重ねる（オーバーレイが上） |

## DPI

内包するズームビューが DPI 変更へ自動追従します（オーバーレイも同じスケーリングで描画されます）。
ホストによる設定は不要です。

## エラー時の挙動

| 状況 | 挙動 |
| --- | --- |
| `Source` が `null` | 何も描画しない（例外にしない） |
| `Rects` が空 | 何も描画しない（例外にしない） |
| サイズ 0 の矩形 | 何も描画しない |
| `ZoomPercent` へ 0 以下を設定 | 無視する（Fit へは `ZoomFit()` を使う） |

## ログ

`Log` プロパティへ `ILogger` を設定すると、内包するズームビューへ伝搬します（ズーム操作の前後のズーム率を Information で出力。Avalonia 版と同一の書式）。
詳細は `../ZoomViewControl.MewUI/SPEC_ExtFunc.md` を参照してください。

## 非対応（今後の対応）

| 項目 | 状況 |
| --- | --- |
| 領域選択オーバーレイ | 対象外。ホストが `FadeMaskDragRectControl.MewUI` を重ねて構成する（`app.MewUI` の切り出しはこの構成） |
| 選択状態の設定（`SetSelection` 等） | `FadeMaskDragRectControl.MewUI` が担う（本コントロールは矩形オーバーレイのみ） |
| ルーペへの矩形描画 | `LoupeControl.MewUI` の描画完了イベント（`Rendered`）でキャンバスへ打ち込める（選択矩形の破線は `FadeMaskDragRectControl.MewUI` が実装済み） |
