# LineDetection 外部機能仕様書

本仕様書は、`Estable.LineDetection`（LineDetection ライブラリ）の**外部機能仕様・公開 API 仕様**を定義します。

## 目次

- [LineDetection 外部機能仕様書](#linedetection-外部機能仕様書)
  - [目次](#目次)
  - [1. 概要](#1-概要)
  - [2. 公開 API](#2-公開-api)
    - [2.1. LineOrientation 列挙型](#21-lineorientation-列挙型)
    - [2.2. LineDetectionResult 構造体](#22-linedetectionresult-構造体)
    - [2.3. ILineDetector インターフェース](#23-ilinedetector-インターフェース)
    - [2.4. LogicLineDetector クラス](#24-logiclinedetector-クラス)
      - [2.4.1. プロパティ](#241-プロパティ)
    - [2.5. LineRectangleReconstructor クラス](#25-linerectanglereconstructor-クラス)
  - [3. 入力・出力](#3-入力出力)
    - [3.1. 入力画像](#31-入力画像)
    - [3.2. 検出結果](#32-検出結果)
  - [4. 座標系](#4-座標系)
  - [5. エラー処理](#5-エラー処理)
  - [6. スレッド安全性](#6-スレッド安全性)

---

## 1. 概要

LineDetection は、キャプチャ画像から Windows のウィンドウ枠となる水平・垂直の辺（線分）を検出するライブラリです。

- 検出方式は `ILineDetector` を通して抽象化され、ロジック方式を同一のインターフェースで利用できます。
- 入力は `EsBitmap`（BGRA・1 ピクセル 4 バイト）で、出力は線分リストです。
- OpenCvSharp は内部利用に閉じており、公開 API には露出しません。

## 2. 公開 API

名前空間: `Estable.LineDetection`（UI 非依存。旧名 `Estable.AvaloniaUI.LineDetection`）

### 2.1. LineOrientation 列挙型

線分（辺）の向き。

| メンバー | 説明 |
| --- | --- |
| `Horizontal` | 水平（左右方向）の辺。 |
| `Vertical` | 垂直（上下方向）の辺。 |

### 2.2. LineDetectionResult 構造体

検出された辺（線分）の結果（不変のレコード構造体）。

| プロパティ | 型 | 説明 |
| --- | --- | --- |
| `Start` | `EsPoint`（`Estable.Platform.Geometry`） | 始点（画像座標・ピクセル）。 |
| `End` | `EsPoint`（`Estable.Platform.Geometry`） | 終点（画像座標・ピクセル）。 |
| `Orientation` | `LineOrientation` | 線分の向き（水平 / 垂直）。 |
| `Score` | `double` | 確からしさ（長さ・信頼度等）。大きいほど有力。 |

座標は UI フレームワーク非依存のコア型（`EsPoint`）で公開します。UI 側で描画する場合は、各 UI の相互変換（Avalonia 版は `EsGeometryAvaloniaInterop.ToAvaloniaPoint`）で境界変換します。

### 2.3. ILineDetector インターフェース

辺（線分）検出器の共通インターフェース。

| メンバー | 説明 |
| --- | --- |
| `string Name { get; }` | 検出方式の表示名。 |
| `string ModelInfo { get; }` | モデルサイズの表示。モデルを使わない方式は空文字。 |
| `string? ErrorMessage { get; }` | モデル読込・実行エラーのメッセージ。正常時は null。 |
| `IReadOnlyList<LineDetectionResult> Detect(EsBitmap image)` | 画像から水平・垂直の辺（線分）を検出する。スコア降順。 |

### 2.4. LogicLineDetector クラス

ロジック方式（Canny エッジ + HoughLinesP）による検出器。`ILineDetector` を実装します。

#### 2.4.1. プロパティ

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `Name` | `string` | "ロジック（Canny + Hough）" | 方式の表示名。 |
| `ModelInfo` | `string` | `""` | モデル不使用のため空文字。 |
| `ErrorMessage` | `string?` | `null` | モデル不使用のため常に null。 |
| `CannyThreshold1` | `int` | `20` | Canny の低閾値。 |
| `CannyThreshold2` | `int` | `60` | Canny の高閾値（`CannyThreshold1` の 3 倍）。 |
| `HoughThreshold` | `int` | `40` | HoughLinesP の投票閾値。 |
| `MinLineLength` | `int` | `50` | 検出する線分の最小長（ピクセル）。 |
| `MaxLineGap` | `int` | `10` | 同一線分とみなす最大ギャップ（ピクセル）。 |
| `AngleTolerance` | `double` | `5.0` | 水平 / 垂直とみなす角度許容差（度）。 |
| `ClusterTolerance` | `int` | `1` | 辺のクラスタリング許容差（ピクセル）。 |
| `MinEdgeSupportRatio` | `double` | `0.6` | 線分に沿ったエッジピクセルの最小割合（0.0〜1.0）。 |
| `ParallelMergeTolerance` | `double` | `8.0` | 矩形の辺に統合する平行線の最大距離（ピクセル）。 |
| `CornerToleranceRatio` | `double` | `0.2` | コーナー判定の許容差（辺の長さに対する比率）。 |
| `OppositeSpanRatio` | `double` | `0.13` | 対辺の長さの差の許容比率。 |

### 2.5. LineRectangleReconstructor クラス

検出された線分（`LineDetectionResult`）から矩形（ウィンドウの 4 辺）を復元する純粋ロジック（`public static class`）。

| メソッド | 戻り値 | 説明 |
| --- | --- | --- |
| `Reconstruct(IReadOnlyList<LineDetectionResult> results)` | `IReadOnlyList<EsPixelRect>` | 線分から矩形（ウィンドウの 4 辺）の候補を復元する（画像座標・閉区間）。 |

- 4 辺が矩形を構成する組み合わせのみを候補にします（左右の垂直線が矩形の縦範囲を覆い、上下の水平線が矩形の横範囲を覆うこと）。
- 同一座標の線分は 1 本に統合し、範囲（垂直線は Y・水平線は X の最小〜最大）を合併します（断片化した辺を 1 本として扱う）。
- 戻り値は面積昇順で、最大 256 件です。UI が全候補を描画するため、候補数を上限で制限します。
- 垂直 2 本・水平 2 本が揃わない場合は空を返します。幅・高さが 1 未満の矩形は除外します。

## 3. 入力・出力

### 3.1. 入力画像

- 型: `EsBitmap`（BGRA8888）
- フォーマット: BGRA（1 ピクセル 4 バイト）
- 検出器は入力画像をグレースケール化して解析します（`Detect` 実行時に内部で変換）。

### 3.2. 検出結果

- 型: `IReadOnlyList<LineDetectionResult>`
- 並び順: スコア降順（ロジック方式は長さ）。
- 矩形（ウィンドウの 4 辺）を検出した場合は、その 4 辺が優先して返されます。

## 4. 座標系

- 線分の始点・終点は**画像のピクセル座標**です。
- ロジック方式の矩形は**枠の外側の境界（排他座標）**を表します（例: 外枠 (50,40)-(850,600)・線幅 4px は (48,38)-(852,602)）。

## 5. エラー処理

- `LogicLineDetector`: モデルを使用しないため、実行時エラーは発生しません。

## 6. スレッド安全性

- `LogicLineDetector` はステートレス（パラメータは変更可能ですが、`Detect` 中のスレッド安全は保証しません。`Detect` に対して排他アクセスしてください）。
