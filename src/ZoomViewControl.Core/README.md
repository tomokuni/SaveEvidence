# ZoomViewControl.Core

`ZoomViewControl.AvaloniaUI`（Avalonia UI 実装）と `ZoomViewControl.MewUI`（MewUI 実装）が共有する、**UI フレームワーク非依存のズーム・パンロジック**を提供するプロジェクトです。

## 目的

ズーム表示の計算と遷移決定のうち、UI フレームワークに依存しない部分（幾何計算・ズーム遷移・設定値の検証）を本プロジェクトへ切り出すことで、同じロジックを MewUI など別 UI からも再利用できます。

**状態と判断（Fit の表現・実効ズーム率の導出・可否・キャッシュの無効化・遷移の適用・パン・スクロールバー）は `ZoomViewSession` が単一所有します**。UI 層は入力の変換・効果の適用・描画だけを担い、判断を持ちません。

## 収録する型

| 型 | 公開 | 責務 |
| --- | --- | --- |
| `ZoomViewSession` | `public` | **状態管理クラス**。ズーム率（0 = Fit）・オフセット・設定・表示条件を保持し、入力の解釈と判断（実効ズーム率・可否・遷移の適用・パン・スクロールバー）を単一所有する |
| `ZoomConfig` | `public` | ズーム遷移のパラメータ（`MinZoom` / `MaxZoom` / `WheelFactor` / `ButtonZoomLevels`）をまとめた検証付き不変値。`With` で検証付きの部分更新を行う |
| `ZoomState` | `public` | 通知する 4 値（実効ズーム率・Fit 状態・ズーム可否）の値オブジェクト |
| `ZoomFeedback` | `public` | セッションから UI へ返す効果（再描画・ポインタキャプチャ）。適用順を単一所有する |
| `ZoomNotice` / `ZoomNoticeKind` | `public` | セッションから UI への通知（種別と通知時点の値） |
| `ZoomViewAppearance` | `public` | 外観（下地・市松模様・スクロールバー）の単一値。描画可否と市松のタイル矩形、タイルサイズの実効値（`ResolveCheckerTileSize`）も持つ |
| `ZoomScrollBar` / `ZoomScrollBarMetrics` | `public` | スクロールバーの矩形、および標準スクロールバー部品へ写す同期値 |
| `ZoomLogMessage` / `ZoomLogFormat` | `public` | ログ定義（両 UI で同一の書式）と値の文字列化・丸め |
| `ZoomTransition` | `internal` | ⊖⊕ ボタンの 23 段階遷移・Ctrl+ホイールの ×1.1 遷移・ズーム可否の判定（`ZoomConfig` を引数で受け取る純粋関数） |
| `ZoomMath` | `internal` | Fit 実効倍率・ズーム中心維持（スケール変化時のオフセット）・スクロール範囲クランプ・ビューポート ↔ コンテンツ座標変換・% ↔ 倍率の換算（純粋関数） |
| `ZoomAvailability` | `internal` | ズーム可否（ズームイン・ズームアウト）を単一に保持する値オブジェクト |
| `ZoomScrollBarGeometry` | `internal` | オーバーレイスクロールバーのトラックとサムの矩形・可動範囲・サム位置からのオフセット逆算（純粋関数） |

`internal` の型は UI 実装（`ZoomViewControl.AvaloniaUI` / `ZoomViewControl.MewUI`）とテストからのみ `InternalsVisibleTo` で参照できます。

## 層の責務の境界

| 層 | 責務 |
| --- | --- |
| Core（本プロジェクト） | 状態の保持、入力の解釈（判断）、両 UI 共通の値と計算、コア⇔UI の契約（`ZoomFeedback` / `ZoomNotice`）、外観の既定値、ログ定義 |
| UI 層（`ZoomViewControl.*`） | 入力イベントの受付とコア型への変換、返された効果の適用、通知の公開イベントへの転送、自 UI の描画先への描画、UI 固有のホスト結線（プロパティ機構・DPI・ポインタキャプチャ・レイアウト） |

**判断・値・計算の重複はありません**（UI 層に残るものは宣言・委譲・転送・フレームワーク型の扱いのみ）。両 UI の正規化行の積集合は 22 行で、そのすべてが宣言・委譲・イベント転送・通知の対応表です。

UI 層に残してよいもの（コアへ移せない例外）:

1. 描画先（Avalonia = 子 `Image` + `RenderTransform` + 標準 `ScrollBar` / MewUI = `SKCanvas`）
2. DPI の取得と `DipToDeviceScaling`、ポインタキャプチャ API
3. レイアウト（`Bounds` の確定）
4. 見た目の差（スクロールバーの太さ・余白。Avalonia は標準部品を使うため廃止し、MewUI は外観値を使う）

## 公開面の階層

| 層 | 利用者 | 型 |
| --- | --- | --- |
| **1. ホスト設定・通知** | アプリ・ホスト | `ZoomViewAppearance` / `ZoomState` / `ZoomConfig`（拡張点） |
| **2. UI アダプタ契約** | UI 実装 | `ZoomViewSession` / `ZoomFeedback` / `ZoomNotice`（+ `Kind`）/ `ZoomScrollBar` / `ZoomScrollBarMetrics` / `ZoomLogMessage` / `ZoomLogFormat` |
| **3. コア内部の計算** | 本コアのみ（UI はセッション経由） | `ZoomTransition` / `ZoomMath` / `ZoomAvailability` / `ZoomScrollBarGeometry` |

## 依存方向

```text
ZoomViewControl.AvaloniaUI（Avalonia 実装） / ZoomViewControl.MewUI
  └──> ZoomViewControl.Core（本プロジェクト）
         └──> Platform.Core（コア幾何型・スクロール計算）
```

本プロジェクトが UI フレームワークへ依存することはありません。外部パッケージへの直接依存もありません。

## 座標型

UI フレームワークの幾何型（`Avalonia.Point` / `Vector` / `Size`）ではなく、`Platform.Core` のコア幾何型 `EsPoint` / `EsVector` / `EsSize` を使用します（`ZoomMath` の引数・戻り値）。

UI 実装側は、境界で 1 行の相互変換（`.ToEsVector()` / `.ToEsSize()` / `.ToEsPoint()` と逆方向の `.ToAvaloniaVector()` 等）を行います。UI 内部の状態（`RenderTransform` 等）とイベント引数は UI 側の型のままです。

## ビルド

```powershell
dotnet build src/ZoomViewControl.Core/src/ZoomViewControl.Core.csproj
```

## テスト

`tests/` が本プロジェクトの単体テストを持ちます。UI 初期化が不要なため、すべて `[Fact]` で並列実行されます。

```powershell
dotnet test src/ZoomViewControl.Core/tests/ZoomViewControl.Core.Tests.csproj
```
