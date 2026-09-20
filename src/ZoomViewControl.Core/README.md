# ZoomViewControl.Core

`ZoomViewControl.AvaloniaUI`（Avalonia UI 実装）と `ZoomViewControl`（MewUI 実装）が共有する、**UI フレームワーク非依存のズーム・パンロジック**を提供するプロジェクトです。

## 目的

ズーム表示の計算と遷移決定のうち、UI フレームワークに依存しない部分（幾何計算・ズーム遷移・設定値の検証）を本プロジェクトへ切り出すことで、同じロジックを MewUI など別 UI からも再利用できます。

## 収録する型

| 型 | 公開 | 責務 |
| --- | --- | --- |
| `ZoomConfig` | `public` | ズーム遷移のパラメータ（`MinZoom` / `MaxZoom` / `WheelFactor` / `ButtonZoomLevels`）をまとめた検証付き不変値。`With` で検証付きの部分更新を行う |
| `ZoomTransition` | `internal` | ⊖⊕ ボタンの 23 段階遷移・Ctrl+ホイールの ×1.1 遷移・ズーム可否の判定（`ZoomConfig` を引数で受け取る純粋関数） |
| `ZoomMath` | `internal` | Fit 実効倍率・ズーム中心維持（スケール変化時のオフセット）・スクロール範囲クランプ・ビューポート ↔ コンテンツ座標変換（純粋関数） |
| `ZoomAvailability` | `internal` | ズーム可否（ズームイン・ズームアウト）を単一に保持する値オブジェクト |
| `ZoomScrollBarGeometry` | `internal` | オーバーレイスクロールバーのトラックとサムの矩形・サム位置からのオフセット逆算（純粋関数）。`ZoomScrollBar`（`Track` / `Thumb`）を返す |

`internal` の型は UI 実装（`ZoomViewControl` / `ZoomViewControl.MewUI`）とテスト（`ZoomViewControl.AvaloniaUI.Tests` / `ZoomViewControl.MewUI.Tests` / `ZoomViewControl.Core.Tests`）からのみ `InternalsVisibleTo` で参照できます。公開 API は `ZoomConfig` のみです。

## 依存方向

```text
ZoomViewControl.AvaloniaUI（Avalonia 実装） / ZoomViewControl.MewUI
  └──> ZoomViewControl.Core（本プロジェクト）
         └──> PlatformFactory.Core（コア幾何型・スクロール計算）
```

本プロジェクトが UI フレームワークへ依存することはありません。外部パッケージへの直接依存もありません。

## 座標型

UI フレームワークの幾何型（`Avalonia.Point` / `Vector` / `Size`）ではなく、`PlatformFactory.Core` のコア幾何型 `EsPoint` / `EsVector` / `EsSize` を使用します（`ZoomMath` の引数・戻り値）。

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
