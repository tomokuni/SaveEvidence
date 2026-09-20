# FadeMaskControl.MewUI 外部機能仕様書

FadeMaskControl（MewUI 版）が外部利用者へ提供する機能と公開 API の仕様です。
内部実装の詳細（描画手順・キャッシュ等）は記載しません。

## 概要

FadeMaskControl は、**選択領域以外を暗転**させ、選択領域をハイライトするオーバーレイコントロールです。
画像やプレビューの上へ重ねて使います。

**Avalonia 版と同じ UI 非依存コア（`FadeMaskControl.Core`）を使うため、見た目は両 UI で一致します。**

## 公開 API

名前空間 `Estable.MewUI.FadeMask` / クラス `FadeMaskControl`（`public sealed`、`SkiaCanvasView` 派生）。

### プロパティ

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `SelectionRect` | `EsRect?` | `null` | 論理座標（ドメイン座標 = 元画像ピクセル）の選択矩形 |
| `HighlightRect` | `EsRect?` | `null` | 論理座標のハイライト矩形。選択矩形より優先される |
| `HasSelection` | `bool` | `false` | true の場合、`SelectionRect` を穴あけ矩形として使う |
| `HasHighlight` | `bool` | `false` | true の場合、`HighlightRect` を穴あけ矩形として使う（選択より優先） |
| `OverlayColor` | `System.Drawing.Color` | 黒 50% | 暗転の色。アルファで不透明度を表す（0 の場合は暗転しない） |
| `BorderColor` | `System.Drawing.Color` | ダークグレー（不透明） | 枠線の色。アルファで不透明度を表す（0 の場合は枠線を描画しない） |
| `BorderThickness` | `double` | 2.0 | 枠線の太さ（DIP）。0.5 未満は 0.5 に正規化される |
| `ShowFullOverlay` | `bool` | `false` | true の場合は選択が無くても全面を暗転する |
| `RenderScaling` | `double` | 1.0 | DIP → 物理ピクセルの係数。DPI 変更で自動更新される |
| `Scaling` | `double` | 1.0 | 座標変換ポートへ適用する倍率（`Port` が未設定の場合のみ使用） |
| `Port` | `ICoordTransform?` | `null` | 論理座標 → DIP の座標変換ポート（設定時は `Scaling` より優先） |
| `ShowSizeLabel` | `bool` | `true` | サイズラベル（1 行目）を表示するか |
| `SizeLabelTextProvider` | `Func<string?>?` | `null` | サイズラベルの文言を返すデリゲート。null または空文字を返すと描画しない |
| `SizeLabelFontSize` | `double` | 14.0 | サイズラベルのフォントサイズ（DIP） |
| `SizeLabelColor` | `System.Drawing.Color` | 白 | サイズラベルの文字色 |
| `SizeLabelBackgroundColor` | `System.Drawing.Color` | 黒 60% | サイズラベルの背景色（アルファ 0 の場合は黒 50% へフォールバック） |
| `SubLines` | `IReadOnlyList<FadeMaskSubLine>?` | `null` | 2 行目以降の行。`FadeMaskSubLine`（文言・文字サイズ・文字色・背景色）を上から順に積む |

### サブラインの型

名前空間 `Estable.MewUI.FadeMask` / レコード `FadeMaskSubLine`（`public sealed`）。

| メンバー | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `Text` | `string` | — | 行の文言 |
| `FontSize` | `double` | — | フォントサイズ（DIP） |
| `Color` | `System.Drawing.Color` | — | 文字色 |
| `BackgroundColor` | `System.Drawing.Color` | 透明 | 背景色（アルファ 0 の場合は黒 50% へフォールバック） |

## 描画仕様

| 状況 | 描画 |
| --- | --- |
| 穴あけ矩形あり | 暗転（クライアント矩形から穴あけ矩形を除いた帯 4 領域）+ 枠線（四辺） |
| 穴あけ矩形なし + `ShowFullOverlay` = true | 暗転（全面のみ）。枠線は描画しない |
| 穴あけ矩形なし + `ShowFullOverlay` = false | 何も描画しない |
| 穴あけ矩形がクライアント矩形と交差しない | 全面を暗転する（描画漏れを防ぐ） |
| 穴あけ矩形がクライアント矩形と同一 | 何も描画しない（暗転領域が 0 になる） |

### ラベルの表示仕様

| 項目 | 仕様 |
| --- | --- |
| 位置 | 穴あけ矩形の左端から内側余白（4 DIP）だけ内側、上端から内側余白だけ下 |
| 行の積み方 | 1 行目はサイズラベル、2 行目以降は `SubLines` の順。行間は内側余白 |
| 背景 | 各行は「文言 + 内側余白 × 2」の矩形を敷く（文字が読めるようにする） |
| サイズラベルの条件 | `ShowSizeLabel` = true かつ `SizeLabelTextProvider` が null でも空文字でもない文字列を返すとき |
| サイズラベルが非表示のとき | `SubLines` を 1 行目から描画する（繰り上げ） |
| 日本語の文言 | 字形を持つフォントへ自動フォールバックするため、豆腐（□）にならない |

### 穴あけ矩形の決定

`HasHighlight` が true なら `HighlightRect`、そうでなく `HasSelection` が true なら `SelectionRect`、両方 false なら穴あけなし（全面暗転または描画なし）です。

### 論理座標から DIP への変換

| 種別 | 閉区間の扱い |
| --- | --- |
| 選択矩形 | 終点に +1 する（閉区間。始点・終点のピクセルとも非暗転領域に含める） |
| ハイライト矩形 | +1 しない（既に全ピクセルを含む閉区間表現のため） |

`Scaling` が 1.0 以外の場合は等倍スケールの座標変換ポートとして適用されます。

## 操作仕様

**入力を受け取りません。** 装飾専用のコントロールであり、ホスト側で `IsHitTestVisible = false` にして
下のコントロールへ入力を通してください（Avalonia 版と同じ使い方）。

## レイアウト

| 項目 | 仕様 |
| --- | --- |
| 要求サイズ | 空（親が割り当てた領域全体を覆う） |
| 表示位置 | ホストのレイアウトで決まる |

## DPI

モニター間の移動で DPI が変わると、`RenderScaling` は `newDpi / 96` へ自動更新されます。
キャンバスは DIP 座標で描画されるため、DPI に関わらず同じ見た目になります。

## エラー時の挙動

| 状況 | 挙動 |
| --- | --- |
| 穴あけ矩形なし・`ShowFullOverlay` = false | 何も描画しない（例外にしない） |
| 暗転色のアルファが 0 | 暗転を描画しない |
| 枠線色のアルファが 0 | 枠線を描画しない |
| 穴あけ矩形がクライアント外 | 全面を暗転する |
| `SizeLabelTextProvider` が null または空文字を返す | サイズラベルを描画しない（例外にしない） |
| `SubLines` が null または 0 件 | サイズラベルのみ描画する |

## ログ

`Log` プロパティへ `ILogger` を設定すると、状態変更を出力します（未設定・null の場合は `NullLogger` になり出力しません）。

| レベル | 出力内容 |
| --- | --- |
| Debug | `[FadeMask] SelectionRect: {前} → {後}` / `[FadeMask] HighlightRect: {前} → {後}`（未設定は `null`） |
| Trace | `[FadeMask] CoordinateTransform: {設定有無}` |

同じ値の再設定では出力しません（外観設定の再適用でログが溢れないようにする）。

## 非対応（今後の対応）

| 項目 | 状況 |
| --- | --- |
| `FadeMaskRenderMode` による描画切り替え | 未実装（enum は Core に定義済み。モードの解釈は `FadeMaskDragRectControl` が担う） |
