# ZoomViewControl.MewUI 外部機能仕様書

`ZoomViewControl`（MewUI 版ズームビュー）が外部利用者へ提供する機能と公開 API の仕様です。
内部実装の詳細（描画手順・キャッシュ等）は記載しません。

## 概要

ズームビューは、**画像**を表示し、**ドラッグ・ホイールでパン**、**Ctrl+ホイール・ボタンでズーム**できるコントロールです。

**Avalonia 版 `ZoomViewControl` と同じ UI 非依存コア（`ZoomViewControl.Core`）を使うため、ズーム挙動と設定は両 UI で一致します。**

## 公開 API

名前空間 `Estable.MewUI.ZoomView` / クラス `ZoomViewControl`（`public sealed`、`SkiaCanvasView` 派生）。

### プロパティ

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `Source` | `EsBitmap?` | `null` | ズーム対象の画像。設定すると Fit 表示（ズーム率 0・オフセット 0）へリセットする |
| `CenterAlign` | `bool` | `true` | 画像がビューポートより小さい軸を中央寄せする（false で左上寄せ） |
| `RenderScaling` | `double` | 1.0 | DIP → 物理ピクセルの係数。DPI 変更で自動更新される |
| `ShowScrollBars` | `bool` | `true` | オーバーレイのスクロールバーを表示するか |
| `ScrollBarThickness` | `double` | MewUI 標準のスクロールバーと同じ太さ（`ThemeMetrics.ScrollBarThickness`。既定 4.0） | スクロールバーの太さ（DIP） |
| `ScrollBarMargin` | `double` | `8.0` | ビューポート端からの余白（DIP）。ウィンドウのリサイズ判定領域（端から 6 DIP）を避ける値 |
| `ScrollBarColor` | `SKColor` | 白 63%（A=160） | サムの色（アルファで不透明度を表す） |
| `ScrollBarTrackColor` | `SKColor` | 白 16%（A=40） | トラックの色（アルファ 0 の場合はトラックを描画しない） |
| `ZoomPercent` | `double` | Fit の実効倍率 | 実効ズーム率（%）。**0 より大きい値のみ設定可能**（0 以下は無視）。設定値は `ZoomConfig` の範囲へクランプされる |
| `IsFit` | `bool` | `true` | Fit 表示中かどうか |
| `CanZoomIn` | `bool` | `false` | ズームイン可能かどうか（画像未設定時は false） |
| `CanZoomOut` | `bool` | `false` | ズームアウト可能かどうか（画像未設定時は false） |
| `Offset` | `EsVector` | `default` | パン位置（コンテンツ座標・読み取り専用） |

### メソッド

| メソッド | 説明 |
| --- | --- |
| `ZoomIn()` | 段階レベルを 1 つ進める（移動先がなければ何もしない） |
| `ZoomOut()` | 段階レベルを 1 つ戻す（移動先がなければ何もしない） |
| `ZoomFit()` | Fit 表示へ戻す（**Fit へ移行する唯一の経路**） |
| `SetZoom(double zoomPercent, EsPoint? center = null)` | ズーム率を設定する。0 以下は Fit。`center` を省略するとビューポート中心を維持する |
| `SetOffset(EsVector offset)` | パン位置を設定する（スクロール範囲へクランプ。同値なら何もしない） |
| `PanBy(EsVector delta)` | パン位置を移動量だけ加算する |
| `PanByWheel(EsVector delta, bool shift)` | ホイールの回転量（X/Y）でパンする（1 ノッチ = 48 DIP を実効倍率で除す。Shift 併用は縦回転を横パン） |
| `ZoomByWheel(bool zoomIn, EsPoint? pointerPosition = null)` | Ctrl+ホイールのズームを適用する（`pointerPosition` はズーム中心。null はビューポート中心） |
| `ImageRectViewport()` | 画像の表示矩形（ビューポート座標・DIP）。画像未設定時は空の矩形 |
| `DisplaySize()` | 画像の表示サイズ（ビューポート座標・DIP） |
| `ViewportToContent(EsPoint)` | ビューポート座標 → コンテンツ座標（オフセット基準） |
| `ContentToViewport(EsPoint)` | コンテンツ座標 → ビューポート座標（オフセット基準） |
| `ViewportToContentImage(EsPoint)` | ビューポート座標 → コンテンツ座標（画像表示矩形の原点基準・中央寄せ補正を含む） |
| `ContentToViewportImage(EsPoint)` | コンテンツ座標 → ビューポート座標（画像表示矩形の原点基準・中央寄せ補正を含む） |

### イベント

| イベント | 引数 | 説明 |
| --- | --- | --- |
| `ZoomChanged` | `ZoomChangedEventArgs` | ズーム率・Fit 状態・ズーム可否を通知する |
| `ViewportChanged` | `EventArgs` | パン・ズーム・表示サイズの変化を通知する（ホストが重ねたオーバーレイの追従に使う） |

`ZoomChangedEventArgs(double ZoomPercent, bool IsFit, bool CanZoomIn, bool CanZoomOut)`

## 座標系

| 座標系 | 定義 |
| --- | --- |
| コンテンツ座標 | 元画像ピクセル（ズーム率に依存しない不変座標） |
| ビューポート座標 | コントロールのローカル座標（DIP） |

描画位置は `スケール × (コンテンツ座標 − オフセット)` で決まります。
オフセットは「ビューポート左上に対応するコンテンツ座標」で、スクロール範囲（コンテンツ − ビューポート ÷ スケール）にクランプされます。

## 操作仕様

| 操作 | 動作 |
| --- | --- |
| 左ボタンドラッグ | パン（grab 方式。掴んだ位置がポインタに追従し、ドラッグ方向へコンテンツが移動する） |
| ホイール | 縦パン（1 ノッチ = 48 DIP） |
| Shift+ホイール | 横パン |
| Ctrl+ホイール | ポインタ位置を中心にズーム（Fit 中は Fit 実効倍率の前後へ移動する） |
| サムのドラッグ | スクロールバーによる移動（トラック内でのサム位置に応じたオフセットへ移動する） |
| トラックのクリック | サムをその位置へ移動する（1 画面ずつのページ送りは行わない） |

スクロールバーは画像がビューポートより大きい軸にのみ表示されます（パン不要の軸には表示しません）。
表示するかどうかは `ShowScrollBars` で指定します（既定は表示）。

## ズーム仕様

| 項目 | 仕様 |
| --- | --- |
| 範囲 | `MinZoom` 10% 〜 `MaxZoom` 500%（既定） |
| ⊕⊖ ボタン | 23 段階のレベル集合（10, 12, 15, 20, 25, 33, 40, 50, 62, 75, 80, 90, 100, 110, 125, 150, 200, 250, 300, 350, 400, 450, 500）。Fit は含まない |
| Ctrl+ホイール | 現在値 × 1.1（ズームイン） / ÷ 1.1（ズームアウト）。四捨五入して範囲へクランプする |
| ズーム中心 | ポインタ位置を中心に拡大縮小する。中心のコンテンツ座標を不変に保つ |
| Fit | ビューポートに収まる Uniform 倍率（アスペクト比維持）。リサイズに追従する |
| Fit 実効倍率の通過 | 段階レベルを跨ぐ際に Fit 実効倍率が間に挟まる場合、それを経由する |

## 表示仕様

| 項目 | 仕様 |
| --- | --- |
| 画像の拡大方式 | 元画像の全面を表示矩形へ描画する（表示矩形の位置決めで Uniform 相当を実現する） |
| 中央寄せ | 画像がビューポートより小さい軸のみ中央寄せする（軸ごとに独立して判定する） |
| スクロール可能な軸 | パン制御が優先される（中央寄せは適用されない） |
| 描画されていない領域 | 暗色（RGB 32,32,32）で塗る |

## レイアウト

| 項目 | 仕様 |
| --- | --- |
| 要求サイズ | 空（親が割り当てた領域全体をビューポートとして使う） |
| 表示位置 | ホストのレイアウト（`Grid` の Star 行等）で決まる |

## DPI

モニター間の移動で DPI が変わると、`RenderScaling` は `newDpi / 96` へ自動更新されます。
キャンバスは DIP 座標で描画されるため、DPI に関わらず同じ見た目になります。

## エラー時の挙動

| 状況 | 挙動 |
| --- | --- |
| `Source` が `null` | 何も描画しない（例外にしない） |
| `ZoomPercent` へ 0 以下を設定 | 無視する（Fit へは `ZoomFit()` を使う） |
| `ZoomPercent` が範囲外 | 範囲（10〜500%）へクランプする |
| スクロール範囲外のオフセット | 範囲へクランプする |
| 画像未設定でのズーム可否 | false を返す |

## ログ

`Log` プロパティへ `ILogger` を設定すると、ズーム操作の前後のズーム率を出力します（未設定・null の場合は `NullLogger` になり出力しません）。

| レベル | 出力内容 |
| --- | --- |
| Information | `[ZoomIn] {前}% -> {後}%` / `[ZoomOut] ...` / `[ZoomInWheel] ...` / `[ZoomOutWheel] ...`（Avalonia 版と同一の書式） |

移動先がない場合（上限・下限）は遷移しないため、ログも出力しません。

## 非対応（今後の対応）

| 項目 | 状況 |
| --- | --- |
| スクロールバー | 画像がビューポートより大きい軸にオーバーレイ表示する（Avalonia 版は標準の `ScrollBar` を同期表示する） |
| ズーム率のバインディング | 未実装（`ZoomChanged` イベントで通知する。`ZoomPercent` は `MewProperty` ではない） |
